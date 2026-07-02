using FluentAssertions;
using Quiver.Services;

namespace Quiver.Tests;

public class CatalogSourceListItemTests
{
    [Fact]
    public void GetStatusText_returns_update_available_when_flag_set()
    {
        var source = new AppCatalogSource
        {
            UpdateAvailable = true,
            LastFetchedUtc = DateTime.UtcNow,
        };

        CatalogSourceListItem.GetStatusText(source).Should().Be("Update available");
    }

    [Fact]
    public void GetStatusText_includes_version_summary_when_available()
    {
        var source = new AppCatalogSource
        {
            UpdateAvailable = true,
            CachedListVersion = "2.0.0",
            AcknowledgedListVersion = "1.0.0",
        };

        CatalogSourceListItem.GetStatusText(source).Should().Contain("Update available");
        CatalogSourceListItem.GetStatusText(source).Should().Contain("Cached v2.0.0");
    }

    [Fact]
    public void GetStatusText_returns_last_error_when_present()
    {
        var source = new AppCatalogSource
        {
            LastError = "Network timeout",
            LastFetchedUtc = DateTime.UtcNow,
        };

        CatalogSourceListItem.GetStatusText(source).Should().Be("Network timeout");
    }

    [Fact]
    public void GetStatusText_returns_not_loaded_when_never_fetched()
    {
        CatalogSourceListItem.GetStatusText(new AppCatalogSource()).Should().Be("Not loaded yet");
    }

    [Fact]
    public void GetStatusText_returns_updated_timestamp_when_fetched_successfully()
    {
        var fetchedAt = new DateTime(2026, 6, 11, 14, 30, 0, DateTimeKind.Utc);
        var source = new AppCatalogSource { LastFetchedUtc = fetchedAt };

        CatalogSourceListItem.GetStatusText(source).Should().Be($"Updated {fetchedAt.ToLocalTime():g}");
    }

    [Fact]
    public void GetReviewButtonText_shows_count_when_pending()
    {
        var source = new AppCatalogSource { PendingReviewCount = 3 };

        CatalogSourceListItem.GetReviewButtonText(source).Should().Be("Review (3)");
    }

    [Fact]
    public void GetReviewButtonText_without_pending_uses_plain_label()
    {
        CatalogSourceListItem.GetReviewButtonText(new AppCatalogSource()).Should().Be("Review");
    }

    [Fact]
    public void FromSource_maps_pending_review_count()
    {
        var source = new AppCatalogSource
        {
            Id = "id-1",
            Name = "NAS",
            Location = "http://example/apps.json",
            PendingReviewCount = 5,
        };

        var item = CatalogSourceListItem.FromSource(source);

        item.PendingReviewCount.Should().Be(5);
        item.ReviewButtonText.Should().Be("Review (5)");
        item.PendingReviewBadgeVisible.Should().BeTrue();
    }
}
