using FluentAssertions;
using Quiver.Models;
using Quiver.Services;

namespace Quiver.Tests;

public class CatalogSyncTests
{
    [Fact]
    public async Task CheckPendingCatalogUpdatesAsync_returns_diff_when_update_available()
    {
        var sourceId = Guid.NewGuid().ToString();
        var service = new AppCatalogService();
        var settings = new AppSettings();
        settings.EnsureInitialized();

        var source = new AppCatalogSource
        {
            Id = sourceId,
            Name = "Remote List",
            Location = "https://example.com/list.json",
            Enabled = true,
            UpdateAvailable = true,
        };
        settings.AppCatalogSources.Add(source);

        var accepted = new List<GameInfo>
        {
            new() { Repository = "owner/old", Name = "Old", FolderName = "OldFolder" },
        };
        var remote = new List<GameInfo>
        {
            new() { Repository = "owner/old", Name = "Updated", FolderName = "OldFolder" },
            new() { Repository = "owner/new", Name = "New", FolderName = "NewFolder" },
        };

        await service.RegisterNewSourceAsync(source, remote);
        await service.InitializeAcceptedSnapshotAsync(source, accepted);
        source.UpdateAvailable = true;

        using var httpClient = new HttpClient();
        var pending = await service.CheckPendingCatalogUpdatesAsync(httpClient, settings);

        pending.Should().ContainSingle();
        pending[0].Diff.HasChanges.Should().BeTrue();
        pending[0].Diff.AddedCount.Should().Be(1);
        pending[0].Diff.ChangedCount.Should().Be(1);
    }
}
