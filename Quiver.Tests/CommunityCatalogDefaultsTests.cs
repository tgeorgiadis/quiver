using FluentAssertions;
using Quiver;
using Quiver.Services;

namespace Quiver.Tests;

public class CommunityCatalogDefaultsTests
{
    [Fact]
    public void FirstRunWelcome_copy_is_present()
    {
        CommunityCatalogDefaults.FirstRunWelcomeTitle.Should().Be("Welcome to Quiver");
        CommunityCatalogDefaults.FirstRunWelcomeMessage.Should().NotBeNullOrWhiteSpace();
        CommunityCatalogDefaults.FirstRunWelcomeMessage.Should().Contain("Quiver Community App Catalog");
        CommunityCatalogDefaults.FirstRunWelcomeMessage.Should().Contain("GitHub");
    }

    [Fact]
    public void CreateDefaultSource_uses_stable_id_name_and_url()
    {
        var source = CommunityCatalogDefaults.CreateDefaultSource();

        source.Id.Should().Be(CommunityCatalogDefaults.DefaultSourceId);
        source.Name.Should().Be("Quiver Community App Catalog");
        source.Location.Should().Be(CommunityCatalogDefaults.DefaultCatalogUrl);
        source.Enabled.Should().BeTrue();
    }

    [Fact]
    public void IsDefaultSource_matches_default_source_id()
    {
        var source = CommunityCatalogDefaults.CreateDefaultSource();
        CommunityCatalogDefaults.IsDefaultSource(source).Should().BeTrue();
        CommunityCatalogDefaults.IsDefaultSource(new AppCatalogSource { Id = Guid.NewGuid().ToString() }).Should().BeFalse();
    }

    [Fact]
    public void GetFirstRunReviewSource_prefers_default_community_catalog()
    {
        var settings = new AppSettings();
        settings.EnsureInitialized();
        settings.AppCatalogSources.Clear();
        settings.AppCatalogSources.Add(new AppCatalogSource
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Other",
            Enabled = true,
            PendingReviewCount = 5,
        });
        settings.AppCatalogSources.Add(CommunityCatalogDefaults.CreateDefaultSource());

        var source = CommunityCatalogDefaults.GetFirstRunReviewSource(settings);

        source.Should().NotBeNull();
        CommunityCatalogDefaults.IsDefaultSource(source!).Should().BeTrue();
    }

    [Fact]
    public void GetFirstRunReviewSource_falls_back_to_highest_pending_source()
    {
        var settings = new AppSettings();
        settings.EnsureInitialized();
        settings.AppCatalogSources.Clear();
        settings.AppCatalogSources.Add(new AppCatalogSource
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Low pending",
            Enabled = true,
            PendingReviewCount = 1,
        });
        settings.AppCatalogSources.Add(new AppCatalogSource
        {
            Id = Guid.NewGuid().ToString(),
            Name = "High pending",
            Enabled = true,
            PendingReviewCount = 9,
        });

        CommunityCatalogDefaults.GetFirstRunReviewSource(settings)!.Name.Should().Be("High pending");
    }

    [Fact]
    public async Task EnsureDefaultCommunitySourceFetchedAsync_populates_cache_and_pending_review()
    {
        var fixtureJson = await File.ReadAllTextAsync(TestFixtures.CommunityCatalogPath);
        var service = new AppCatalogService(null, new FakeCatalogLocationReader(fixtureJson));
        var settings = new AppSettings();
        settings.EnsureInitialized();
        settings.AppCatalogSources.Clear();
        settings.AppCatalogSources.Add(CommunityCatalogDefaults.CreateDefaultSource());
        var source = settings.AppCatalogSources[0];

        var cachePath = Path.Combine(
            AppContext.BaseDirectory,
            "Cache",
            "CatalogSources",
            $"{source.Id}.json");

        try
        {
            if (File.Exists(cachePath))
                File.Delete(cachePath);

            await service.SaveLocalAppsAsync([]);
            service.HasSourceCache(source.Id).Should().BeFalse();

            await service.EnsureDefaultCommunitySourceFetchedAsync(new HttpClient(), settings);

            service.HasSourceCache(source.Id).Should().BeTrue();
            source.CachedListVersion.Should().Be("1.0.0");
            await service.RefreshUpdateAvailableAsync(source);
            source.PendingReviewCount.Should().Be(2);
        }
        finally
        {
            if (File.Exists(cachePath))
                File.Delete(cachePath);
        }
    }

    private sealed class FakeCatalogLocationReader(string json) : ICatalogLocationReader
    {
        public Task<string> ReadAsync(HttpClient httpClient, string location, CancellationToken cancellationToken = default) =>
            Task.FromResult(json);
    }
}
