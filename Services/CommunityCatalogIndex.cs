using System.Net.Http;
using System.Text.Json;

namespace Quiver.Services
{
    public class CommunityCatalogListEntry
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Location { get; set; } = "";
        public string ListVersion { get; set; } = "";
    }

    public class CommunityCatalogIndex
    {
        public int Version { get; set; }
        public List<CommunityCatalogListEntry> Lists { get; set; } = [];
    }

    public static class CommunityCatalogIndexService
    {
        public static async Task<(CommunityCatalogIndex? Index, string? Error)> FetchIndexAsync(
            HttpClient httpClient,
            string indexUrl)
        {
            if (string.IsNullOrWhiteSpace(indexUrl))
                return (null, "Community index URL is not configured.");

            try
            {
                var json = await CatalogLocationReader.Default.ReadAsync(httpClient, indexUrl).ConfigureAwait(false);
                return (ParseIndex(json), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        public static CommunityCatalogIndex ParseIndex(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var index = new CommunityCatalogIndex
            {
                Version = root.TryGetProperty("version", out var versionElement) && versionElement.TryGetInt32(out var v)
                    ? v
                    : 1,
            };

            if (!root.TryGetProperty("lists", out var listsArray))
                return index;

            foreach (var entry in listsArray.EnumerateArray())
            {
                index.Lists.Add(new CommunityCatalogListEntry
                {
                    Id = entry.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                    Name = entry.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                    Description = entry.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                    Location = entry.TryGetProperty("location", out var loc) ? loc.GetString() ?? "" : "",
                    ListVersion = entry.TryGetProperty("listVersion", out var lv) ? lv.GetString() ?? "" : "",
                });
            }

            return index;
        }
    }
}
