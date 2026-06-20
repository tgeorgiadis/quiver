using System.Collections.ObjectModel;
using Quiver.Models;
using Quiver.Services;

namespace Quiver.ViewModels;

public class CatalogViewModel
{
    public IReadOnlyList<CatalogSourceListItem> BuildSourceListItems(AppSettings settings)
    {
        settings.EnsureInitialized();
        return settings.AppCatalogSources
            .Select(CatalogSourceListItem.FromSource)
            .ToList();
    }

    public void RefreshSourceList(ObservableCollection<CatalogSourceListItem> target, AppSettings settings)
    {
        var items = BuildSourceListItems(settings);
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }

    public bool IsAlreadySubscribed(AppSettings settings, CommunityCatalogListEntry entry) =>
        settings.AppCatalogSources.Any(s =>
            (!string.IsNullOrWhiteSpace(entry.Id) &&
             string.Equals(s.CommunityListId, entry.Id, StringComparison.OrdinalIgnoreCase)) ||
            string.Equals(s.Location, entry.Location, StringComparison.OrdinalIgnoreCase));

    public AppCatalogSource CreateSourceFromCommunityEntry(CommunityCatalogListEntry entry) =>
        new()
        {
            Name = string.IsNullOrWhiteSpace(entry.Name) ? entry.Id : entry.Name,
            Location = entry.Location,
            CommunityListId = entry.Id,
            Enabled = true,
        };

    public AppCatalogSource CreateSource(string name, string location) =>
        new()
        {
            Name = name,
            Location = location,
            Enabled = true,
            LastFetchedUtc = DateTime.UtcNow,
            LastError = null,
        };
}
