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
