using FluentAssertions;
using Quiver.Models;
using Quiver.Services;
using Quiver.ViewModels;

namespace Quiver.Tests;

public class CatalogViewModelTests
{
    [Fact]
    public void BuildSourceListItems_maps_enabled_sources()
    {
        var settings = new AppSettings();
        settings.EnsureInitialized();
        settings.AppCatalogSources.Add(new AppCatalogSource
        {
            Name = "Local",
            Location = "apps.json",
            Enabled = true,
            CachedListVersion = "1.0.0",
            AcknowledgedListVersion = "1.0.0",
        });

        var viewModel = new CatalogViewModel();
        var items = viewModel.BuildSourceListItems(settings);

        items.Should().ContainSingle();
        items[0].Name.Should().Be("Local");
        items[0].StatusText.Should().Contain("Cached v1.0.0");
    }

    [Fact]
    public void CreateSource_sets_enabled_defaults()
    {
        var viewModel = new CatalogViewModel();
        var source = viewModel.CreateSource("My List", "https://example.com/list.json");

        source.Name.Should().Be("My List");
        source.Location.Should().Be("https://example.com/list.json");
        source.Enabled.Should().BeTrue();
    }
}
