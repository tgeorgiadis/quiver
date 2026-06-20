namespace Quiver.Services
{
    public class CatalogSourceListItem
    {
        public string SourceId { get; init; } = "";
        public string Name { get; init; } = "";
        public string Location { get; init; } = "";
        public string StatusText { get; init; } = "";
        public bool Enabled { get; set; }

        public static CatalogSourceListItem FromSource(AppCatalogSource source) =>
            new()
            {
                SourceId = source.Id,
                Name = source.Name,
                Location = source.Location,
                Enabled = source.Enabled,
                StatusText = GetStatusText(source),
            };

        public static string GetStatusText(AppCatalogSource source)
        {
            if (source.UpdateAvailable)
                return "Update available";

            if (!string.IsNullOrEmpty(source.LastError))
                return source.LastError;

            if (source.LastFetchedUtc.HasValue)
                return $"Updated {source.LastFetchedUtc.Value.ToLocalTime():g}";

            return "Not loaded yet";
        }
    }
}
