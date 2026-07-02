namespace Quiver.Services;

using Quiver;

public static class CommunityCatalogDefaults
{
    public const string DefaultSourceId = "7e036b19-0b6d-4978-92e3-d180e5e9b9cb";
    public const string DefaultSourceName = "Quiver Community App Catalog";
    public const string DefaultCatalogUrl =
        "https://raw.githubusercontent.com/tgeorgiadis/quiver-community-app-catalog/main/quiver-community-apps-catalog.json";

    public const string FirstRunWelcomeTitle = "Welcome to Quiver";

    public const string FirstRunWelcomeMessage =
        """
        Welcome to Quiver!

        Quiver is an app manager for downloading releases from GitHub repositories.

        To get started, browse the Quiver Community App Catalog and choose which apps to add to your library. Once added, you can download them from your library.

        Let's open the catalog now.
        """;

    public static AppCatalogSource CreateDefaultSource() =>
        new()
        {
            Id = DefaultSourceId,
            Name = DefaultSourceName,
            Location = DefaultCatalogUrl,
            Enabled = true,
        };

    public static bool IsDefaultSource(AppCatalogSource source) =>
        string.Equals(source.Id, DefaultSourceId, StringComparison.Ordinal);

    public static AppCatalogSource? GetFirstRunReviewSource(AppSettings settings)
    {
        settings.EnsureInitialized();
        var defaultSource = settings.AppCatalogSources.FirstOrDefault(IsDefaultSource);
        if (defaultSource != null)
            return defaultSource;

        return settings.AppCatalogSources
            .Where(s => s.Enabled && s.PendingReviewCount > 0)
            .OrderByDescending(s => s.PendingReviewCount)
            .FirstOrDefault();
    }
}
