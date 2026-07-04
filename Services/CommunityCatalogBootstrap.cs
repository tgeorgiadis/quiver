using System.Net.Http;
using Quiver;

namespace Quiver.Services;

public sealed class CommunityCatalogBootstrap
{
    private readonly ICatalogLocationReader _locationReader;

    public CommunityCatalogBootstrap(ICatalogLocationReader? locationReader = null)
    {
        _locationReader = locationReader ?? CatalogLocationReader.Default;
    }

    public async Task<CommunityCatalogSyncResult> SyncCommunitySourcesFromIndexAsync(
        HttpClient httpClient,
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.EnsureInitialized();
        MigrateLegacyDefaultSource(settings);

        CommunityCatalogIndex? index;
        string? indexError;
        try
        {
            var remoteJson = await _locationReader
                .ReadAsync(httpClient, CommunityCatalogDefaults.RemoteIndexUrl, cancellationToken)
                .ConfigureAwait(false);
            index = CommunityCatalogIndex.TryParse(remoteJson);
            indexError = index == null || index.Lists.Count == 0
                ? "Community catalog index was empty or invalid."
                : null;
        }
        catch (Exception ex)
        {
            index = null;
            indexError = ex.Message;
        }

        if (index == null || index.Lists.Count == 0)
        {
            return new CommunityCatalogSyncResult
            {
                IndexLoaded = false,
                UsedRemoteIndex = false,
                LastError = indexError,
            };
        }

        var addedNames = new List<string>();
        var updatedCount = 0;

        foreach (var entry in index.Lists)
        {
            if (!TryGetRemoteLocation(entry, out var remoteLocation))
                continue;

            var existing = settings.AppCatalogSources.FirstOrDefault(
                s => string.Equals(s.Id, entry.Id, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                settings.AppCatalogSources.Add(CreateSourceFromEntry(entry, remoteLocation));
                addedNames.Add(entry.Name);
                continue;
            }

            if (!existing.IsCommunityManaged)
                continue;

            MigrateBundledCommunitySourceLocation(existing);
            if (UpdateSourceFromEntry(existing, entry, remoteLocation))
                updatedCount++;
        }

        foreach (var source in settings.AppCatalogSources.Where(s => s.IsCommunityManaged))
            MigrateBundledCommunitySourceLocation(source);

        return new CommunityCatalogSyncResult
        {
            IndexLoaded = true,
            UsedRemoteIndex = true,
            AddedSourceCount = addedNames.Count,
            UpdatedSourceCount = updatedCount,
            AddedSourceNames = addedNames,
        };
    }

    public static void MigrateLegacyDefaultSource(AppSettings settings)
    {
        settings.EnsureInitialized();

        var legacy = settings.AppCatalogSources
            .FirstOrDefault(CommunityCatalogDefaults.IsLegacyDefaultSource);

        if (legacy == null)
            return;

        settings.AppCatalogSources.Remove(legacy);
    }

    public static AppCatalogSource CreateSourceFromEntry(
        CommunityCatalogListEntry entry,
        string? remoteLocation = null)
    {
        remoteLocation ??= ResolveRemoteLocation(entry);
        return new AppCatalogSource
        {
            Id = entry.Id.Trim(),
            Name = entry.Name.Trim(),
            Description = entry.Description?.Trim() ?? "",
            Location = remoteLocation,
            RemoteLocation = null,
            IsCommunityManaged = true,
            Enabled = true,
        };
    }

    private static bool UpdateSourceFromEntry(
        AppCatalogSource source,
        CommunityCatalogListEntry entry,
        string remoteLocation)
    {
        var changed = false;

        var name = entry.Name.Trim();
        if (!string.Equals(source.Name, name, StringComparison.Ordinal))
        {
            source.Name = name;
            changed = true;
        }

        var description = entry.Description?.Trim() ?? "";
        if (!string.Equals(source.Description, description, StringComparison.Ordinal))
        {
            source.Description = description;
            changed = true;
        }

        if (!string.Equals(source.Location, remoteLocation, StringComparison.OrdinalIgnoreCase))
        {
            source.Location = remoteLocation;
            changed = true;
        }

        if (source.RemoteLocation != null)
        {
            source.RemoteLocation = null;
            changed = true;
        }

        return changed;
    }

    private static bool TryGetRemoteLocation(CommunityCatalogListEntry entry, out string remoteLocation)
    {
        remoteLocation = ResolveRemoteLocation(entry);
        return !string.IsNullOrWhiteSpace(remoteLocation);
    }

    private static string ResolveRemoteLocation(CommunityCatalogListEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.RemoteLocation))
            return entry.RemoteLocation.Trim();

        if (!string.IsNullOrWhiteSpace(entry.Location) &&
            AppCatalogService.IsRemoteLocation(entry.Location))
        {
            return entry.Location.Trim();
        }

        return "";
    }

    public static void MigrateBundledCommunitySourceLocation(AppCatalogSource source)
    {
        if (!source.IsCommunityManaged)
            return;

        if (string.IsNullOrWhiteSpace(source.RemoteLocation))
            return;

        if (AppCatalogService.IsRemoteLocation(source.Location))
            return;

        source.Location = source.RemoteLocation.Trim();
        source.RemoteLocation = null;
    }
}
