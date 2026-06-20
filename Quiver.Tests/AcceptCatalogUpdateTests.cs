using FluentAssertions;
using Quiver.Models;
using Quiver.Services;

namespace Quiver.Tests;

public class AcceptCatalogUpdateTests
{
    private static GameInfo CreateApp(
        string repository,
        string name = "Test App",
        string folderName = "TestFolder")
    {
        return new GameInfo
        {
            Repository = repository,
            Name = name,
            FolderName = folderName,
        };
    }

    private static string GetAcceptedSnapshotPath(string sourceId) =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Cache",
            "CatalogSources",
            $"{sourceId}.accepted.json");

    private static async Task<List<GameInfo>> ReadAcceptedSnapshotAsync(string sourceId)
    {
        var path = GetAcceptedSnapshotPath(sourceId);
        File.Exists(path).Should().BeTrue($"expected accepted snapshot at {path}");

        var service = new AppCatalogService();
        return service.ParseAppsFromJson(await File.ReadAllTextAsync(path));
    }

    private static List<GameInfo> BuildUpdateFixture()
    {
        return
        [
            CreateApp("owner/unchanged", "Unchanged", "UnchangedFolder"),
            CreateApp("owner/changed", "New Name", "OldFolder"),
            CreateApp("owner/added", "Added", "AddedFolder"),
        ];
    }

    private static List<GameInfo> BuildAcceptedFixture()
    {
        return
        [
            CreateApp("owner/unchanged", "Unchanged", "UnchangedFolder"),
            CreateApp("owner/removed", "Removed", "RemovedFolder"),
            CreateApp("owner/changed", "Old Name", "OldFolder"),
        ];
    }

    [Fact]
    public async Task ApplyAll_replaces_accepted_with_remote()
    {
        var sourceId = Guid.NewGuid().ToString();
        var source = new AppCatalogSource { Id = sourceId, UpdateAvailable = true };
        var service = new AppCatalogService();
        var accepted = BuildAcceptedFixture();
        var remote = BuildUpdateFixture();

        await service.InitializeAcceptedSnapshotAsync(source, accepted);
        await service.AcceptCatalogUpdateAsync(source, remote, accepted, CatalogUpdateChoice.ApplyAll);

        source.UpdateAvailable.Should().BeFalse();
        var snapshot = await ReadAcceptedSnapshotAsync(sourceId);
        snapshot.Select(a => a.Repository).Should().BeEquivalentTo(["owner/unchanged", "owner/changed", "owner/added"]);
        snapshot.Should().NotContain(a => a.Repository == "owner/removed");
        snapshot.Single(a => a.Repository == "owner/changed").Name.Should().Be("New Name");
    }

    [Fact]
    public async Task ApplyNewOnly_keeps_removed_but_applies_remote_changes()
    {
        var sourceId = Guid.NewGuid().ToString();
        var source = new AppCatalogSource { Id = sourceId, UpdateAvailable = true };
        var service = new AppCatalogService();
        var accepted = BuildAcceptedFixture();
        var remote = BuildUpdateFixture();

        await service.InitializeAcceptedSnapshotAsync(source, accepted);
        await service.AcceptCatalogUpdateAsync(source, remote, accepted, CatalogUpdateChoice.ApplyNewOnly);

        var snapshot = await ReadAcceptedSnapshotAsync(sourceId);
        snapshot.Select(a => a.Repository).Should().BeEquivalentTo(
            ["owner/unchanged", "owner/removed", "owner/changed", "owner/added"]);
        snapshot.Single(a => a.Repository == "owner/changed").Name.Should().Be("New Name");
    }

    [Fact]
    public async Task KeepCurrent_clears_flag_without_touching_snapshot()
    {
        var sourceId = Guid.NewGuid().ToString();
        var source = new AppCatalogSource
        {
            Id = sourceId,
            UpdateAvailable = true,
            AcceptedContentHash = "ORIGINAL_HASH",
        };
        var service = new AppCatalogService();
        var accepted = BuildAcceptedFixture();
        var remote = BuildUpdateFixture();

        await service.InitializeAcceptedSnapshotAsync(source, accepted);
        var before = await File.ReadAllTextAsync(GetAcceptedSnapshotPath(sourceId));
        var hashBefore = source.AcceptedContentHash;

        await service.AcceptCatalogUpdateAsync(source, remote, accepted, CatalogUpdateChoice.KeepCurrent);

        source.UpdateAvailable.Should().BeFalse();
        source.AcceptedContentHash.Should().Be(hashBefore);
        var after = await File.ReadAllTextAsync(GetAcceptedSnapshotPath(sourceId));
        after.Should().Be(before);
    }

    [Fact]
    public async Task ApplyAll_updates_AcceptedContentHash()
    {
        var sourceId = Guid.NewGuid().ToString();
        var source = new AppCatalogSource { Id = sourceId, UpdateAvailable = true };
        var service = new AppCatalogService();
        var accepted = BuildAcceptedFixture();
        var remote = BuildUpdateFixture();

        await service.InitializeAcceptedSnapshotAsync(source, accepted);
        await service.AcceptCatalogUpdateAsync(source, remote, accepted, CatalogUpdateChoice.ApplyAll);

        var snapshot = await ReadAcceptedSnapshotAsync(sourceId);
        source.AcceptedContentHash.Should().Be(AppCatalogService.ComputeCatalogContentHash(snapshot));
    }

    [Fact]
    public void CatalogMerge_ApplyNewOnly_ignores_blank_repository_entries()
    {
        var accepted = new List<GameInfo> { CreateApp("owner/kept") };
        var remote = new List<GameInfo>
        {
            CreateApp(""),
            CreateApp("owner/new", "New"),
        };

        var merged = CatalogMerge.ApplyChoice(
            CatalogUpdateChoice.ApplyNewOnly,
            accepted,
            remote,
            app => new GameInfo
            {
                Repository = app.Repository,
                Name = app.Name,
                FolderName = app.FolderName,
            });

        merged.Should().NotBeNull();
        merged!.Select(a => a.Repository).Should().BeEquivalentTo(["owner/kept", "owner/new"]);
    }
}
