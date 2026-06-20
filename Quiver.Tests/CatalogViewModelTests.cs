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
        });

        var viewModel = new CatalogViewModel();
        var items = viewModel.BuildSourceListItems(settings);

        items.Should().ContainSingle();
        items[0].Name.Should().Be("Local");
    }

    [Fact]
    public void IsAlreadySubscribed_matches_community_id_or_location()
    {
        var settings = new AppSettings();
        settings.EnsureInitialized();
        settings.AppCatalogSources.Add(new AppCatalogSource
        {
            CommunityListId = "n64-recomp",
            Location = "https://example.com/n64-recomp.json",
        });

        var viewModel = new CatalogViewModel();
        viewModel.IsAlreadySubscribed(settings, new CommunityCatalogListEntry
        {
            Id = "n64-recomp",
            Location = "https://example.com/other.json",
        }).Should().BeTrue();
    }

    [Fact]
    public void CreateSourceFromCommunityEntry_uses_entry_name()
    {
        var viewModel = new CatalogViewModel();
        var source = viewModel.CreateSourceFromCommunityEntry(new CommunityCatalogListEntry
        {
            Id = "list-id",
            Name = "N64 Recomp",
            Location = "https://example.com/list.json",
        });

        source.Name.Should().Be("N64 Recomp");
        source.CommunityListId.Should().Be("list-id");
        source.Enabled.Should().BeTrue();
    }
}
