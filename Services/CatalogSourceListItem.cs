namespace Quiver.Services
{
    public class CatalogSourceListItem
    {
        public string SourceId { get; init; } = "";
        public string Name { get; init; } = "";
        public string Location { get; init; } = "";
        public string StatusText { get; init; } = "";
        public bool Enabled { get; set; }
        public bool UpdateAvailable { get; init; }
        public int PendingReviewCount { get; init; }
        public string ReviewButtonText { get; init; } = "Review";
        public bool PendingReviewBadgeVisible => PendingReviewCount > 0;

        public static CatalogSourceListItem FromSource(AppCatalogSource source) =>
            new()
            {
                SourceId = source.Id,
                Name = source.Name,
                Location = source.Location,
                Enabled = source.Enabled,
                UpdateAvailable = source.UpdateAvailable,
                PendingReviewCount = source.PendingReviewCount,
                StatusText = GetStatusText(source),
                ReviewButtonText = GetReviewButtonText(source),
            };

        public static string GetReviewButtonText(AppCatalogSource source) =>
            source.PendingReviewCount > 0
                ? $"Review ({source.PendingReviewCount})"
                : "Review";

        public static string GetStatusText(AppCatalogSource source)
        {
            var versionPart = FormatVersionSummary(source);

            if (source.UpdateAvailable)
                return string.IsNullOrEmpty(versionPart)
                    ? "Update available"
                    : $"Update available · {versionPart}";

            if (!string.IsNullOrEmpty(source.LastError))
                return source.LastError;

            if (source.LastFetchedUtc.HasValue)
            {
                var fetched = $"Updated {source.LastFetchedUtc.Value.ToLocalTime():g}";
                return string.IsNullOrEmpty(versionPart) ? fetched : $"{fetched} · {versionPart}";
            }

            return string.IsNullOrEmpty(versionPart) ? "Not loaded yet" : versionPart;
        }

        private static string FormatVersionSummary(AppCatalogSource source) =>
            CatalogCompareService.FormatCatalogVersionSummary(
                source.CachedListVersion,
                source.AcknowledgedListVersion);
    }
}
