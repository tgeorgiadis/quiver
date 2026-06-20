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

        public async Task<List<GameInfo>> LoadMergedCatalogAsync(HttpClient httpClient, AppSettings settings)
        {
            settings.EnsureInitialized();
            var localApps = await LoadLocalAppsAsync().ConfigureAwait(false);
            foreach (var app in localApps)
            {
                app.CatalogSourceId = null;
                app.GameManager = _gameManager;
            }

            var merged = new List<GameInfo>(localApps);
            var seenRepos = new HashSet<string>(
                localApps
                    .Where(a => !string.IsNullOrWhiteSpace(a.Repository))
                    .Select(a => a.Repository!),
                StringComparer.OrdinalIgnoreCase);

            foreach (var source in settings.AppCatalogSources.Where(s => s.Enabled))
            {
                var acceptedApps = await SyncAndGetAcceptedAppsAsync(httpClient, source).ConfigureAwait(false);
                foreach (var app in acceptedApps)
                {
                    if (string.IsNullOrWhiteSpace(app.Repository))
                        continue;

                    if (seenRepos.Add(app.Repository))
                    {
                        app.CatalogSourceId = source.Id;
                        app.GameManager = _gameManager;
                        merged.Add(app);
                    }
                }
            }

            foreach (var app in merged)
                ApplyUserAppTags(app, settings);

            AppSettings.Save(settings);
            return merged;
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

        public async Task<List<PendingCatalogUpdate>> CheckPendingCatalogUpdatesAsync(
            HttpClient httpClient,
            AppSettings settings)
        {
            settings.EnsureInitialized();
            var pending = new List<PendingCatalogUpdate>();

            foreach (var source in settings.AppCatalogSources.Where(s => s.Enabled && s.UpdateAvailable))
            {
                var acceptedApps = await LoadAcceptedAppsAsync(source.Id).ConfigureAwait(false);
                var remoteApps = await LoadFetchedAppsFromCacheAsync(source.Id).ConfigureAwait(false);

                if (remoteApps.Count == 0)
                    continue;

                pending.Add(new PendingCatalogUpdate
                {
                    Source = source,
                    AcceptedApps = acceptedApps,
                    RemoteApps = remoteApps,
                    Diff = GetCatalogDiff(acceptedApps, remoteApps),
                });
            }

            return pending;
        }

        public async Task AcceptCatalogUpdateAsync(
            AppCatalogSource source,
            List<GameInfo> remoteApps,
            List<GameInfo> acceptedApps,
            CatalogUpdateChoice choice)
        {
            if (choice == CatalogUpdateChoice.KeepCurrent)
            {
                source.UpdateAvailable = false;
                return;
            }

            var newAccepted = CatalogMerge.ApplyChoice(choice, acceptedApps, remoteApps, CloneCatalogApp);
            if (newAccepted == null)
                return;

            await SaveAcceptedSnapshotAsync(source, newAccepted).ConfigureAwait(false);
            source.UpdateAvailable = false;
        }

        public async Task InitializeAcceptedSnapshotAsync(AppCatalogSource source, List<GameInfo> apps)
        {
            await SaveAcceptedSnapshotAsync(source, apps).ConfigureAwait(false);
            source.UpdateAvailable = false;
        }

        public async Task RegisterNewSourceAsync(AppCatalogSource source, List<GameInfo> apps)
        {
            await WriteAppsToFileAsync(GetSourceCachePath(source.Id), apps).ConfigureAwait(false);
            source.LastFetchedUtc = DateTime.UtcNow;
            source.LastError = null;
            await InitializeAcceptedSnapshotAsync(source, apps).ConfigureAwait(false);
        }

        public async Task<(List<GameInfo> Apps, string? Error)> TryLoadSourceAsync(
            HttpClient httpClient,
            string location)
        {
            try
            {
                var json = await _locationReader.ReadAsync(httpClient, location).ConfigureAwait(false);
                using var document = JsonDocument.Parse(json);
                var apps = ParseAppsRoot(document.RootElement);
                foreach (var app in apps)
                    app.GameManager = _gameManager;

                return (apps, null);
            }
            catch (Exception ex)
            {
                return ([], ex.Message);
            }
        }

        public async Task<List<GameInfo>> GetExclusiveAppsForSourceAsync(
            HttpClient httpClient,
            AppSettings settings,
            AppCatalogSource source)
        {
            settings.EnsureInitialized();
            var localApps = await LoadLocalAppsAsync().ConfigureAwait(false);
            var localRepos = new HashSet<string>(
                localApps
                    .Where(a => !string.IsNullOrWhiteSpace(a.Repository))
                    .Select(a => a.Repository!),
                StringComparer.OrdinalIgnoreCase);

            var sourceApps = await LoadAcceptedAppsAsync(source.Id).ConfigureAwait(false);
            if (sourceApps.Count == 0)
            {
                sourceApps = await SyncAndGetAcceptedAppsAsync(httpClient, source, persistSettings: false)
                    .ConfigureAwait(false);
            }

            var sourceRepos = sourceApps
                .Where(a => !string.IsNullOrWhiteSpace(a.Repository))
                .ToDictionary(a => a.Repository!, a => a, StringComparer.OrdinalIgnoreCase);

            var otherSourceRepos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var other in settings.AppCatalogSources.Where(s => s.Enabled && s.Id != source.Id))
            {
                var otherApps = await LoadAcceptedAppsAsync(other.Id).ConfigureAwait(false);
                foreach (var app in otherApps)
                {
                    if (!string.IsNullOrWhiteSpace(app.Repository))
                        otherSourceRepos.Add(app.Repository);
                }
            }

            var exclusive = new List<GameInfo>();
            foreach (var (repo, app) in sourceRepos)
            {
                if (!localRepos.Contains(repo) && !otherSourceRepos.Contains(repo))
                    exclusive.Add(app);
            }

            return exclusive;
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

                localApps.Add(CloneForLocal(app));
                localRepos.Add(app.Repository);
            }

            await SaveLocalAppsAsync(localApps).ConfigureAwait(false);
        }

        public async Task PromoteAppToLocalIfNeededAsync(GameInfo app)
        {
            if (string.IsNullOrWhiteSpace(app.Repository))
                return;

            var localApps = await LoadLocalAppsAsync().ConfigureAwait(false);
            var exists = localApps.Any(g =>
                g.Repository != null &&
                g.Repository.Equals(app.Repository, StringComparison.OrdinalIgnoreCase));

            if (exists)
                return;

            localApps.Add(CloneForLocal(app));
            await SaveLocalAppsAsync(localApps).ConfigureAwait(false);
            app.CatalogSourceId = null;
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

        public static CatalogDiff GetCatalogDiff(List<GameInfo> acceptedApps, List<GameInfo> remoteApps)
        {
            var acceptedByRepo = acceptedApps
                .Where(a => !string.IsNullOrWhiteSpace(a.Repository))
                .ToDictionary(a => a.Repository!, a => a, StringComparer.OrdinalIgnoreCase);

            var remoteByRepo = remoteApps
                .Where(a => !string.IsNullOrWhiteSpace(a.Repository))
                .ToDictionary(a => a.Repository!, a => a, StringComparer.OrdinalIgnoreCase);

            var diff = new CatalogDiff();

            foreach (var (repo, remote) in remoteByRepo)
            {
                if (!acceptedByRepo.ContainsKey(repo))
                    diff.Added.Add(remote);
                else if (!AppsEquivalent(acceptedByRepo[repo], remote))
                    diff.Changed.Add(remote);
            }

            foreach (var (repo, accepted) in acceptedByRepo)
            {
                if (!remoteByRepo.ContainsKey(repo))
                    diff.Removed.Add(accepted);
            }

            return diff;
        }

        private static bool AppsEquivalent(GameInfo a, GameInfo b) =>
            string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.FolderName, b.FolderName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.InstallPath ?? "", b.InstallPath ?? "", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.GameIconUrl ?? "", b.GameIconUrl ?? "", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.PreferredVersion ?? "", b.PreferredVersion ?? "", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.SkippedUpdateVersion ?? "", b.SkippedUpdateVersion ?? "", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(TagHelper.FormatTagsForDisplay(a.Tags), TagHelper.FormatTagsForDisplay(b.Tags), StringComparison.OrdinalIgnoreCase);

        private async Task<List<GameInfo>> SyncAndGetAcceptedAppsAsync(
            HttpClient httpClient,
            AppCatalogSource source,
            bool persistSettings = true)
        {
            var fetchSucceeded = await FetchSourceToCacheAsync(httpClient, source, persistMetadata: persistSettings)
                .ConfigureAwait(false);

            var acceptedApps = await LoadAcceptedAppsAsync(source.Id).ConfigureAwait(false);
            var fetchedApps = await LoadFetchedAppsFromCacheAsync(source.Id).ConfigureAwait(false);

            if (acceptedApps.Count == 0 && fetchSucceeded && fetchedApps.Count > 0)
            {
                await SaveAcceptedSnapshotAsync(source, fetchedApps).ConfigureAwait(false);
                source.UpdateAvailable = false;
                return fetchedApps;
            }

            if (fetchSucceeded && fetchedApps.Count > 0)
            {
                var remoteHash = ComputeCatalogContentHash(fetchedApps);
                source.UpdateAvailable = !string.Equals(
                    remoteHash,
                    source.AcceptedContentHash,
                    StringComparison.OrdinalIgnoreCase);
            }
            else if (acceptedApps.Count > 0)
            {
                return acceptedApps;
            }

            return acceptedApps;
        }

        private async Task<bool> FetchSourceToCacheAsync(
            HttpClient httpClient,
            AppCatalogSource source,
            bool persistMetadata = true)
        {
            var cachePath = GetSourceCachePath(source.Id);

            try
            {
                var json = await _locationReader.ReadAsync(httpClient, source.Location).ConfigureAwait(false);
                await File.WriteAllTextAsync(cachePath, json).ConfigureAwait(false);

                if (persistMetadata)
                {
                    source.LastFetchedUtc = DateTime.UtcNow;
                    source.LastError = null;
                }

                return true;
            }
            catch (Exception ex)
            {
                if (File.Exists(cachePath))
                {
                    if (persistMetadata)
                        source.LastError = $"{ex.Message} (using cached copy)";
                    return true;
                }

                if (persistMetadata)
                    source.LastError = ex.Message;

                return false;
            }
        }

        private async Task<List<GameInfo>> LoadAcceptedAppsAsync(string sourceId)
        {
            var path = GetAcceptedCachePath(sourceId);
            if (!File.Exists(path))
                return [];

            return await LoadAppsFromFileAsync(path).ConfigureAwait(false);
        }

        private async Task<List<GameInfo>> LoadFetchedAppsFromCacheAsync(string sourceId)
        {
            var path = GetSourceCachePath(sourceId);
            if (!File.Exists(path))
                return [];

            return await LoadAppsFromFileAsync(path).ConfigureAwait(false);
        }

        private async Task SaveAcceptedSnapshotAsync(AppCatalogSource source, List<GameInfo> apps)
        {
            await WriteAppsToFileAsync(GetAcceptedCachePath(source.Id), apps).ConfigureAwait(false);
            source.AcceptedContentHash = ComputeCatalogContentHash(apps);
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

        private string GetAcceptedCachePath(string sourceId) =>
            Path.Combine(_catalogSourcesCacheFolder, $"{sourceId}.accepted.json");

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

        private List<GameInfo> ParseAppsRoot(JsonElement root)
        {
            var apps = new List<GameInfo>();

            if (root.ValueKind == JsonValueKind.Array)
            {
                apps.AddRange(ParseAppArray(root));
                return DedupeByRepository(apps);
            }

            if (root.TryGetProperty("apps", out var appsArray))
                apps.AddRange(ParseAppArray(appsArray));

            foreach (var legacySection in new[] { "standard", "experimental", "custom" })
            {
                if (root.TryGetProperty(legacySection, out var legacyArray))
                    apps.AddRange(ParseAppArray(legacyArray));
            }

            return DedupeByRepository(apps);
        }

        private static List<GameInfo> DedupeByRepository(List<GameInfo> apps) =>
            apps
                .GroupBy(app => app.Repository ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

        private List<GameInfo> ParseAppArray(JsonElement appsArray)
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
                        GameManager = _gameManager,
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

        private static List<string> CloneTags(IEnumerable<string>? tags) =>
            TagHelper.NormalizeTags(tags);

        private static GameInfo CloneCatalogApp(GameInfo app) =>
            new()
            {
                Name = app.Name,
                Repository = app.Repository,
                FolderName = app.FolderName,
                InstallPath = app.InstallPath,
                GameIconUrl = app.GameIconUrl,
                PreferredVersion = app.PreferredVersion,
                SkippedUpdateVersion = app.SkippedUpdateVersion,
                Tags = CloneTags(app.Tags),
                IsExperimental = false,
                IsCustom = true,
                GameManager = app.GameManager,
            };

        private static GameInfo CloneForLocal(GameInfo app) =>
            new()
            {
                Name = app.Name,
                Repository = app.Repository,
                FolderName = app.FolderName,
                InstallPath = app.InstallPath,
                GameIconUrl = app.GameIconUrl,
                PreferredVersion = app.PreferredVersion,
                SkippedUpdateVersion = app.SkippedUpdateVersion,
                Tags = CloneTags(app.Tags),
                IsExperimental = false,
                IsCustom = true,
                GameManager = app.GameManager,
                CatalogSourceId = null,
            };

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
