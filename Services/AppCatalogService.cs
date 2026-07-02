using Quiver.Models;
using System.Net.Http;
using AppSettings = Quiver.AppSettings;
using AppCatalogSource = Quiver.AppCatalogSource;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Quiver.Services
{
    public class AppCatalogService
    {
        private readonly string _appsConfigPath;
        private readonly string _legacyGamesConfigPath;
        private readonly string _catalogSourcesCacheFolder;
        private readonly GameManager? _gameManager;
        private readonly ICatalogLocationReader _locationReader;

        public AppCatalogService(GameManager? gameManager = null, ICatalogLocationReader? locationReader = null)
        {
            _gameManager = gameManager;
            _locationReader = locationReader ?? CatalogLocationReader.Default;
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _appsConfigPath = Path.Combine(baseDir, "apps.json");
            _legacyGamesConfigPath = Path.Combine(baseDir, "games.json");
            _catalogSourcesCacheFolder = Path.Combine(baseDir, "Cache", "CatalogSources");
            Directory.CreateDirectory(_catalogSourcesCacheFolder);
        }

        public string AppsConfigPath => _appsConfigPath;

        public async Task<List<GameInfo>> LoadLocalAppsAsync()
        {
            if (!File.Exists(_appsConfigPath))
            {
                if (File.Exists(_legacyGamesConfigPath))
                {
                    var migratedApps = await LoadAppsFromFileAsync(_legacyGamesConfigPath).ConfigureAwait(false);
                    await SaveLocalAppsAsync(migratedApps).ConfigureAwait(false);
                    return migratedApps;
                }

                await SaveLocalAppsAsync([]).ConfigureAwait(false);
                return [];
            }

            return await LoadAppsFromFileAsync(_appsConfigPath).ConfigureAwait(false);
        }

        public async Task ValidateAndFixLocalAppsJsonAsync()
        {
            var apps = await LoadLocalAppsAsync().ConfigureAwait(false);
            await SaveLocalAppsAsync(apps).ConfigureAwait(false);
        }

        public async Task<List<GameInfo>> LoadLocalCatalogAsync(AppSettings settings)
        {
            settings.EnsureInitialized();
            var localApps = await LoadLocalAppsAsync().ConfigureAwait(false);
            foreach (var app in localApps)
            {
                app.CatalogSourceId = null;
                app.GameManager = _gameManager;
            }

            foreach (var app in localApps)
                ApplyUserAppTags(app, settings);

            return localApps;
        }

        public async Task RefreshAllSourcesAsync(HttpClient httpClient, AppSettings settings)
        {
            settings.EnsureInitialized();
            foreach (var source in settings.AppCatalogSources.Where(s => s.Enabled))
                await FetchSourceAsync(httpClient, source).ConfigureAwait(false);
        }

        public bool HasSourceCache(string sourceId) =>
            File.Exists(GetSourceCachePath(sourceId));

        public async Task EnsureDefaultCommunitySourceFetchedAsync(HttpClient httpClient, AppSettings settings)
        {
            settings.EnsureInitialized();
            var source = settings.AppCatalogSources.FirstOrDefault(CommunityCatalogDefaults.IsDefaultSource);
            if (source == null || !source.Enabled || HasSourceCache(source.Id))
                return;

            await FetchSourceAsync(httpClient, source).ConfigureAwait(false);
        }

        public async Task<(List<GameInfo> Apps, string? Version, string? Error)> TryLoadSourceAsync(
            HttpClient httpClient,
            string location)
        {
            try
            {
                var json = await _locationReader.ReadAsync(httpClient, location).ConfigureAwait(false);
                using var document = JsonDocument.Parse(json);
                var version = ResolveListVersion(document.RootElement, out var apps);
                foreach (var app in apps)
                    app.GameManager = _gameManager;

                return (apps, version, null);
            }
            catch (Exception ex)
            {
                return ([], null, ex.Message);
            }
        }

        public async Task<bool> FetchSourceAsync(HttpClient httpClient, AppCatalogSource source)
        {
            var cachePath = GetSourceCachePath(source.Id);

            try
            {
                var json = await _locationReader.ReadAsync(httpClient, source.Location).ConfigureAwait(false);
                await File.WriteAllTextAsync(cachePath, json).ConfigureAwait(false);

                using var document = JsonDocument.Parse(json);
                var version = ResolveListVersion(document.RootElement, out _);

                source.LastFetchedUtc = DateTime.UtcNow;
                source.LastError = null;
                source.CachedListVersion = version;
                CatalogCompareService.PruneIgnoredChanges(source);
                await RefreshUpdateAvailableAsync(source).ConfigureAwait(false);

                return true;
            }
            catch (Exception ex)
            {
                if (File.Exists(cachePath))
                {
                    source.LastError = $"{ex.Message} (using cached copy)";
                    await ApplyCachedVersionMetadataAsync(source).ConfigureAwait(false);
                    return true;
                }

                source.LastError = ex.Message;
                return false;
            }
        }

        public async Task RegisterNewSourceAsync(
            HttpClient httpClient,
            AppCatalogSource source,
            string? rawJson = null)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                rawJson = await _locationReader.ReadAsync(httpClient, source.Location).ConfigureAwait(false);
            }

            var cachePath = GetSourceCachePath(source.Id);
            await File.WriteAllTextAsync(cachePath, rawJson).ConfigureAwait(false);

            using var document = JsonDocument.Parse(rawJson);
            var version = ResolveListVersion(document.RootElement, out _);

            source.LastFetchedUtc = DateTime.UtcNow;
            source.LastError = null;
            source.CachedListVersion = version;
            source.AcknowledgedListVersion = version;
            source.UpdateAvailable = false;
        }

        public void AcknowledgeSourceVersion(AppCatalogSource source)
        {
            source.AcknowledgedListVersion = source.CachedListVersion;
            source.UpdateAvailable = false;
            source.IgnoredChangesAtVersion?.Clear();
        }

        public async Task RefreshUpdateAvailableAsync(AppCatalogSource source)
        {
            if (CatalogCompareService.IsReviewedVersion(source.AcknowledgedListVersion) &&
                string.Equals(source.CachedListVersion, source.AcknowledgedListVersion, StringComparison.Ordinal))
            {
                source.UpdateAvailable = false;
                source.PendingReviewCount = 0;
                return;
            }

            var localApps = await LoadLocalAppsAsync().ConfigureAwait(false);
            var externalApps = await LoadCachedAppsAsync(source.Id).ConfigureAwait(false);
            var rows = CatalogCompareService.BuildCompareRows(localApps, externalApps);
            source.PendingReviewCount = rows.Count(r => CatalogCompareService.IsActionableRow(r, source));
            if (TryAutoAcknowledgeIfReviewComplete(source, source.PendingReviewCount))
                return;

            source.UpdateAvailable = source.PendingReviewCount > 0;
        }

        public static bool TryAutoAcknowledgeIfReviewComplete(AppCatalogSource source, int pendingCount)
        {
            if (pendingCount > 0)
                return false;

            if (string.IsNullOrWhiteSpace(source.CachedListVersion))
                return false;

            if (CatalogCompareService.IsReviewedVersion(source.AcknowledgedListVersion) &&
                string.Equals(source.CachedListVersion, source.AcknowledgedListVersion, StringComparison.Ordinal))
                return false;

            source.AcknowledgedListVersion = source.CachedListVersion;
            source.UpdateAvailable = false;
            source.PendingReviewCount = 0;
            return true;
        }

        public async Task<List<GameInfo>> LoadCachedAppsAsync(string sourceId)
        {
            var path = GetSourceCachePath(sourceId);
            if (!File.Exists(path))
                return [];

            return await LoadAppsFromFileAsync(path).ConfigureAwait(false);
        }

        public void DeleteSourceCache(string sourceId)
        {
            var cachePath = GetSourceCachePath(sourceId);
            if (File.Exists(cachePath))
                File.Delete(cachePath);

            var legacyAcceptedPath = GetLegacyAcceptedCachePath(sourceId);
            if (File.Exists(legacyAcceptedPath))
                File.Delete(legacyAcceptedPath);
        }

        public static bool MigrateLegacyCatalogSources(AppSettings settings, string? cacheFolder = null)
        {
            settings.EnsureInitialized();
            cacheFolder ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", "CatalogSources");
            Directory.CreateDirectory(cacheFolder);

            var changed = false;
            foreach (var source in settings.AppCatalogSources)
            {
                var legacyAcceptedPath = Path.Combine(cacheFolder, $"{source.Id}.accepted.json");
                if (File.Exists(legacyAcceptedPath))
                {
                    File.Delete(legacyAcceptedPath);
                    changed = true;
                }

                var cachePath = Path.Combine(cacheFolder, $"{source.Id}.json");
                if (string.IsNullOrWhiteSpace(source.CachedListVersion) && File.Exists(cachePath))
                {
                    try
                    {
                        var json = File.ReadAllText(cachePath);
                        using var document = JsonDocument.Parse(json);
                        source.CachedListVersion = ResolveListVersion(document.RootElement, out _);
                        changed = true;
                    }
                    catch
                    {
                        source.CachedListVersion = "0";
                        changed = true;
                    }
                }

                if (source.AcknowledgedListVersion == "0")
                {
                    source.AcknowledgedListVersion = null;
                    changed = true;
                }

                var updateAvailable = !string.IsNullOrWhiteSpace(source.CachedListVersion) &&
                    (!CatalogCompareService.IsReviewedVersion(source.AcknowledgedListVersion) ||
                     !string.Equals(
                         source.CachedListVersion,
                         source.AcknowledgedListVersion,
                         StringComparison.Ordinal));

                if (source.UpdateAvailable != updateAvailable)
                {
                    source.UpdateAvailable = updateAvailable;
                    changed = true;
                }
            }

            return changed;
        }

        public async Task<List<GameInfo>> GetExternalOnlyAppsForSourceAsync(string sourceId, List<GameInfo>? localApps = null)
        {
            localApps ??= await LoadLocalAppsAsync().ConfigureAwait(false);
            var localRepos = new HashSet<string>(
                localApps
                    .Where(a => !string.IsNullOrWhiteSpace(a.Repository))
                    .Select(a => a.Repository!),
                StringComparer.OrdinalIgnoreCase);

            var externalApps = await LoadCachedAppsAsync(sourceId).ConfigureAwait(false);
            return externalApps
                .Where(a => !string.IsNullOrWhiteSpace(a.Repository) && !localRepos.Contains(a.Repository))
                .ToList();
        }

        public static void ApplyUserAppTags(GameInfo app, AppSettings settings)
        {
            settings.EnsureInitialized();

            if (string.IsNullOrWhiteSpace(app.Repository))
            {
                app.Tags = TagHelper.NormalizeTags(app.Tags);
                return;
            }

            if (settings.UserAppTags.TryGetValue(app.Repository, out var userTags))
                app.Tags = TagHelper.NormalizeTags(userTags);
            else
                app.Tags = TagHelper.NormalizeTags(app.Tags);
        }

        public async Task PromoteAppsToLocalAsync(IEnumerable<GameInfo> apps)
        {
            var localApps = await LoadLocalAppsAsync().ConfigureAwait(false);
            var localRepos = new HashSet<string>(
                localApps
                    .Where(a => !string.IsNullOrWhiteSpace(a.Repository))
                    .Select(a => a.Repository!),
                StringComparer.OrdinalIgnoreCase);

            foreach (var app in apps)
            {
                if (string.IsNullOrWhiteSpace(app.Repository) || localRepos.Contains(app.Repository))
                    continue;

                localApps.Add(CatalogCompareService.CloneForLocal(app));
                localRepos.Add(app.Repository);
            }

            await SaveLocalAppsAsync(localApps).ConfigureAwait(false);
        }

        public async Task SaveLocalAppsAsync(List<GameInfo> apps)
        {
            await WriteAppsToFileAsync(_appsConfigPath, apps).ConfigureAwait(false);
        }

        public async Task ExportLocalAppsToFileAsync(string exportPath, List<GameInfo>? apps = null)
        {
            apps ??= await LoadLocalAppsAsync().ConfigureAwait(false);
            await WriteAppsToFileAsync(exportPath, apps).ConfigureAwait(false);
        }

        public static string ComputeCatalogContentHash(IEnumerable<GameInfo> apps)
        {
            var entries = apps
                .Where(a => !string.IsNullOrWhiteSpace(a.Repository))
                .Select(a => string.Join("|",
                    a.Repository!.Trim(),
                    a.Name ?? "",
                    a.FolderName ?? "",
                    a.InstallPath ?? "",
                    a.GameIconUrl ?? "",
                    a.PreferredVersion ?? "",
                    a.SkippedUpdateVersion ?? "",
                    TagHelper.FormatTagsForDisplay(a.Tags)))
                .OrderBy(e => e, StringComparer.OrdinalIgnoreCase);

            var payload = string.Join("\n", entries);
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hashBytes);
        }

        public static CatalogDiff GetCatalogDiff(List<GameInfo> baselineApps, List<GameInfo> remoteApps)
        {
            var baselineByRepo = baselineApps
                .Where(a => !string.IsNullOrWhiteSpace(a.Repository))
                .ToDictionary(a => a.Repository!, a => a, StringComparer.OrdinalIgnoreCase);

            var remoteByRepo = remoteApps
                .Where(a => !string.IsNullOrWhiteSpace(a.Repository))
                .ToDictionary(a => a.Repository!, a => a, StringComparer.OrdinalIgnoreCase);

            var diff = new CatalogDiff();

            foreach (var (repo, remote) in remoteByRepo)
            {
                if (!baselineByRepo.ContainsKey(repo))
                    diff.Added.Add(remote);
                else if (!AreCatalogFieldsEquivalent(baselineByRepo[repo], remote))
                    diff.Changed.Add(remote);
            }

            foreach (var (repo, baseline) in baselineByRepo)
            {
                if (!remoteByRepo.ContainsKey(repo))
                    diff.Removed.Add(baseline);
            }

            return diff;
        }

        public static bool AreCatalogFieldsEquivalent(GameInfo a, GameInfo b) =>
            string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.FolderName, b.FolderName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.InstallPath ?? "", b.InstallPath ?? "", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.GameIconUrl ?? "", b.GameIconUrl ?? "", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.PreferredVersion ?? "", b.PreferredVersion ?? "", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(TagHelper.FormatTagsForDisplay(a.Tags), TagHelper.FormatTagsForDisplay(b.Tags), StringComparison.OrdinalIgnoreCase);

        private async Task ApplyCachedVersionMetadataAsync(AppCatalogSource source)
        {
            var cachePath = GetSourceCachePath(source.Id);
            if (!File.Exists(cachePath))
                return;

            try
            {
                var json = await File.ReadAllTextAsync(cachePath).ConfigureAwait(false);
                using var document = JsonDocument.Parse(json);
                source.CachedListVersion = ResolveListVersion(document.RootElement, out _);
                CatalogCompareService.PruneIgnoredChanges(source);
                await RefreshUpdateAvailableAsync(source).ConfigureAwait(false);
            }
            catch
            {
                // Keep existing metadata when cache is unreadable.
            }
        }

        private static string ResolveListVersion(JsonElement root, out List<GameInfo> apps)
        {
            apps = ParseAppsFromRootStatic(root);
            if (root.TryGetProperty("version", out var versionElement) &&
                versionElement.ValueKind == JsonValueKind.String)
            {
                var version = versionElement.GetString()?.Trim();
                if (!string.IsNullOrEmpty(version))
                    return version;
            }

            return ComputeCatalogContentHash(apps);
        }

        private static async Task WriteAppsToFileAsync(string path, List<GameInfo> apps)
        {
            var data = new
            {
                apps = apps.Select(SerializeApp).ToList()
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(data, options)).ConfigureAwait(false);
        }

        public void SaveLocalApps(List<GameInfo> apps)
        {
            WriteAppsToFileAsync(_appsConfigPath, apps).GetAwaiter().GetResult();
        }

        public List<GameInfo> ParseAppsFromDictionary(Dictionary<string, JsonElement> gamesData)
        {
            var apps = new List<GameInfo>();
            if (gamesData.TryGetValue("apps", out var appsArray))
                apps.AddRange(ParseAppArray(appsArray));

            foreach (var legacySection in new[] { "standard", "experimental", "custom" })
            {
                if (gamesData.TryGetValue(legacySection, out var legacyArray))
                    apps.AddRange(ParseAppArray(legacyArray));
            }

            return DedupeByRepository(apps);
        }

        public List<GameInfo> ParseAppsFromJson(string json)
        {
            using var document = JsonDocument.Parse(json);
            return ParseAppsRoot(document.RootElement);
        }

        public static bool IsRemoteLocation(string location)
        {
            return location.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   location.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        public static string ResolveLocalPath(string location)
        {
            if (IsRemoteLocation(location) || Path.IsPathRooted(location))
                return location;

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, location);
        }

        private string GetSourceCachePath(string sourceId) =>
            Path.Combine(_catalogSourcesCacheFolder, $"{sourceId}.json");

        private static string GetLegacyAcceptedCachePath(string sourceId) =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", "CatalogSources", $"{sourceId}.accepted.json");

        private async Task<List<GameInfo>> LoadAppsFromFileAsync(string path)
        {
            try
            {
                string json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                using var document = JsonDocument.Parse(json);
                var apps = ParseAppsRoot(document.RootElement);
                foreach (var app in apps)
                    app.GameManager = _gameManager;

                return apps;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading {Path.GetFileName(path)}: {ex.Message}");
                return [];
            }
        }

        private List<GameInfo> ParseAppsRoot(JsonElement root) =>
            ParseAppsFromRootStatic(root, _gameManager);

        private static List<GameInfo> ParseAppsFromRootStatic(JsonElement root, GameManager? gameManager = null)
        {
            var apps = new List<GameInfo>();

            if (root.ValueKind == JsonValueKind.Array)
            {
                apps.AddRange(ParseAppArrayStatic(root, gameManager));
                return DedupeByRepository(apps);
            }

            if (root.TryGetProperty("apps", out var appsArray))
                apps.AddRange(ParseAppArrayStatic(appsArray, gameManager));

            foreach (var legacySection in new[] { "standard", "experimental", "custom" })
            {
                if (root.TryGetProperty(legacySection, out var legacyArray))
                    apps.AddRange(ParseAppArrayStatic(legacyArray, gameManager));
            }

            return DedupeByRepository(apps);
        }

        private static List<GameInfo> DedupeByRepository(List<GameInfo> apps) =>
            apps
                .GroupBy(app => app.Repository ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

        private List<GameInfo> ParseAppArray(JsonElement appsArray) =>
            ParseAppArrayStatic(appsArray, _gameManager);

        private static List<GameInfo> ParseAppArrayStatic(JsonElement appsArray, GameManager? gameManager)
        {
            var apps = new List<GameInfo>();

            foreach (var appElement in appsArray.EnumerateArray())
            {
                try
                {
                    var app = new GameInfo
                    {
                        Name = (appElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null) ?? string.Empty,
                        Repository = (appElement.TryGetProperty("repository", out var repoElement) ? repoElement.GetString() : null) ?? string.Empty,
                        FolderName = (appElement.TryGetProperty("folderName", out var folderElement) ? folderElement.GetString() : null) ?? string.Empty,
                        InstallPath = appElement.TryGetProperty("installPath", out var installPathElement) ? installPathElement.GetString() : null,
                        GameIconUrl = GetIconUrl(appElement),
                        PreferredVersion = appElement.TryGetProperty("preferredVersion", out var preferredVersionElement) ? preferredVersionElement.GetString() : null,
                        SkippedUpdateVersion = appElement.TryGetProperty("skippedUpdateVersion", out var skippedUpdateVersionElement) ? skippedUpdateVersionElement.GetString() : null,
                        Tags = ParseTagsProperty(appElement),
                        IsExperimental = false,
                        IsCustom = true,
                        GameManager = gameManager,
                    };

                    apps.Add(app);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing app: {ex.Message}");
                }
            }

            return apps;
        }

        private static string? GetIconUrl(JsonElement appElement)
        {
            if (appElement.TryGetProperty("appIconUrl", out var appIconUrlElement) && appIconUrlElement.ValueKind != JsonValueKind.Null)
                return appIconUrlElement.GetString();

            if (appElement.TryGetProperty("gameIconUrl", out var gameIconUrlElement) && gameIconUrlElement.ValueKind != JsonValueKind.Null)
                return gameIconUrlElement.GetString();

            if (appElement.TryGetProperty("customDefaultIconUrl", out var legacyIconElement) && legacyIconElement.ValueKind != JsonValueKind.Null)
                return legacyIconElement.GetString();

            return null;
        }

        private static List<string> ParseTagsProperty(JsonElement appElement)
        {
            if (!appElement.TryGetProperty("tags", out var tagsElement) || tagsElement.ValueKind != JsonValueKind.Array)
                return [];

            var tags = new List<string>();
            foreach (var tagElement in tagsElement.EnumerateArray())
            {
                if (tagElement.ValueKind == JsonValueKind.String)
                {
                    var tag = tagElement.GetString();
                    if (!string.IsNullOrWhiteSpace(tag))
                        tags.Add(tag);
                }
            }

            return TagHelper.NormalizeTags(tags);
        }

        private static object SerializeApp(GameInfo app)
        {
            var normalizedTags = TagHelper.NormalizeTags(app.Tags);
            if (normalizedTags.Count == 0)
            {
                return new
                {
                    name = app.Name,
                    repository = app.Repository,
                    folderName = app.FolderName,
                    installPath = app.InstallPath,
                    appIconUrl = app.GameIconUrl,
                    preferredVersion = app.PreferredVersion,
                    skippedUpdateVersion = app.SkippedUpdateVersion
                };
            }

            return new
            {
                name = app.Name,
                repository = app.Repository,
                folderName = app.FolderName,
                installPath = app.InstallPath,
                appIconUrl = app.GameIconUrl,
                preferredVersion = app.PreferredVersion,
                skippedUpdateVersion = app.SkippedUpdateVersion,
                tags = normalizedTags
            };
        }
    }
}
