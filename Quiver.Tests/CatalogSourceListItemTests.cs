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
}
