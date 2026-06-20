using FluentAssertions;
using Quiver.Models;
using Quiver.Services;

namespace Quiver.Tests;

public class AppCatalogServiceTests
{
    private static GameInfo CreateApp(
        string repository,
        string name = "Test App",
        string folderName = "TestFolder",
        string? gameIconUrl = null,
        string? installPath = null)
    {
        return new GameInfo
        {
            Repository = repository,
            Name = name,
            FolderName = folderName,
            GameIconUrl = gameIconUrl,
            InstallPath = installPath,
        };
    }

    [Theory]
    [InlineData("https://example.com/apps.json", true)]
    [InlineData("http://example.com/apps.json", true)]
    [InlineData("HTTPS://EXAMPLE.COM/apps.json", true)]
    [InlineData("community-app-lists/n64-recomp.json", false)]
    [InlineData(@"C:\Catalogs\apps.json", false)]
    public void IsRemoteLocation_classifies_locations(string location, bool expectedRemote)
    {
        AppCatalogService.IsRemoteLocation(location).Should().Be(expectedRemote);
    }

    [Fact]
    public void ResolveLocalPath_combines_relative_paths_with_base_directory()
    {
        var resolved = AppCatalogService.ResolveLocalPath("community-app-lists/index.json");

        resolved.Should().Be(Path.Combine(AppContext.BaseDirectory, "community-app-lists/index.json"));
    }

    [Fact]
    public void ResolveLocalPath_preserves_rooted_and_remote_paths()
    {
        const string remote = "https://example.com/index.json";
        AppCatalogService.ResolveLocalPath(remote).Should().Be(remote);

        var rooted = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "apps.json"));
        AppCatalogService.ResolveLocalPath(rooted).Should().Be(rooted);
    }

    [Fact]
    public void GetCatalogDiff_detects_added_removed_and_changed_apps()
    {
        var accepted = new List<GameInfo>
        {
            CreateApp("owner/unchanged", "Unchanged", "UnchangedFolder"),
            CreateApp("owner/removed", "Removed", "RemovedFolder"),
            CreateApp("owner/changed", "Old Name", "OldFolder"),
        };

        var remote = new List<GameInfo>
        {
            CreateApp("owner/unchanged", "Unchanged", "UnchangedFolder"),
            CreateApp("owner/changed", "New Name", "OldFolder"),
            CreateApp("owner/added", "Added", "AddedFolder"),
        };

        var diff = AppCatalogService.GetCatalogDiff(accepted, remote);

        diff.HasChanges.Should().BeTrue();
        diff.Added.Should().ContainSingle(a => a.Repository == "owner/added");
        diff.Removed.Should().ContainSingle(a => a.Repository == "owner/removed");
        diff.Changed.Should().ContainSingle(a => a.Repository == "owner/changed" && a.Name == "New Name");
        diff.AddedCount.Should().Be(1);
        diff.RemovedCount.Should().Be(1);
        diff.ChangedCount.Should().Be(1);
    }

    [Fact]
    public void GetCatalogDiff_is_case_insensitive_for_repository_keys()
    {
        var accepted = new List<GameInfo> { CreateApp("Owner/App", "Same", "Folder") };
        var remote = new List<GameInfo> { CreateApp("owner/app", "Same", "Folder") };

        var diff = AppCatalogService.GetCatalogDiff(accepted, remote);

        diff.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void GetCatalogDiff_reports_no_changes_when_identical()
    {
        var accepted = new List<GameInfo>
        {
            CreateApp("owner/app", "Name", "Folder", installPath: "C:\\Games\\App", gameIconUrl: "https://example.com/icon.png"),
        };
        var remote = new List<GameInfo>
        {
            CreateApp("owner/app", "Name", "Folder", installPath: "C:\\Games\\App", gameIconUrl: "https://example.com/icon.png"),
        };
        remote[0].PreferredVersion = "v1.0.0";
        remote[0].SkippedUpdateVersion = "v2.0.0";
        accepted[0].PreferredVersion = "v1.0.0";
        accepted[0].SkippedUpdateVersion = "v2.0.0";

        AppCatalogService.GetCatalogDiff(accepted, remote).HasChanges.Should().BeFalse();
    }

    [Fact]
    public void GetCatalogDiff_detects_metadata_field_changes()
    {
        var accepted = new List<GameInfo>
        {
            CreateApp("owner/app", "Name", "Folder", installPath: null, gameIconUrl: null),
        };
        accepted[0].PreferredVersion = "v1.0.0";
        accepted[0].SkippedUpdateVersion = null;

        var remote = new List<GameInfo>
        {
            CreateApp("owner/app", "Name", "Folder", installPath: "D:\\Custom", gameIconUrl: "https://example.com/icon.png"),
        };
        remote[0].PreferredVersion = "v2.0.0";
        remote[0].SkippedUpdateVersion = "v3.0.0";

        var diff = AppCatalogService.GetCatalogDiff(accepted, remote);

        diff.HasChanges.Should().BeTrue();
        diff.Changed.Should().ContainSingle(a => a.Repository == "owner/app");
        diff.Added.Should().BeEmpty();
        diff.Removed.Should().BeEmpty();
    }

    [Fact]
    public void GetCatalogDiff_detects_tag_changes()
    {
        var accepted = new List<GameInfo> { CreateApp("owner/app", "Name", "Folder") };
        accepted[0].Tags = ["n64"];

        var remote = new List<GameInfo> { CreateApp("owner/app", "Name", "Folder") };
        remote[0].Tags = ["n64", "recomp"];

        var diff = AppCatalogService.GetCatalogDiff(accepted, remote);

        diff.HasChanges.Should().BeTrue();
        diff.Changed.Should().ContainSingle(a => a.Repository == "owner/app");
    }

    [Fact]
    public void ApplyUserAppTags_replaces_tags_when_override_exists()
    {
        var app = CreateApp("owner/app", "Name", "Folder");
        app.Tags = ["catalog"];

        var settings = new AppSettings
        {
            UserAppTags = new Dictionary<string, List<string>>
            {
                ["owner/app"] = ["custom", "n64"],
            },
        };

        AppCatalogService.ApplyUserAppTags(app, settings);

        app.Tags.Should().BeEquivalentTo(["custom", "n64"]);
    }

    [Fact]
    public void ComputeCatalogContentHash_is_stable_for_same_content_regardless_of_order()
    {
        var first = new List<GameInfo>
        {
            CreateApp("b/repo", "Beta", "BetaFolder"),
            CreateApp("a/repo", "Alpha", "AlphaFolder"),
        };

        var second = new List<GameInfo>
        {
            CreateApp("a/repo", "Alpha", "AlphaFolder"),
            CreateApp("b/repo", "Beta", "BetaFolder"),
        };

        AppCatalogService.ComputeCatalogContentHash(first)
            .Should()
            .Be(AppCatalogService.ComputeCatalogContentHash(second));
    }

    [Fact]
    public void ComputeCatalogContentHash_changes_when_app_metadata_changes()
    {
        var baseline = new List<GameInfo> { CreateApp("owner/app", "Name", "Folder") };
        var changed = new List<GameInfo> { CreateApp("owner/app", "Different Name", "Folder") };

        AppCatalogService.ComputeCatalogContentHash(baseline)
            .Should()
            .NotBe(AppCatalogService.ComputeCatalogContentHash(changed));
    }

    [Fact]
    public void ParseAppsFromJson_reads_community_n64_recomp_fixture()
    {
        var service = new AppCatalogService();
        var apps = service.ParseAppsFromJson(TestFixtures.ReadN64RecompListJson());

        apps.Should().NotBeEmpty();
        apps.Should().Contain(app => app.Repository == "Zelda64Recomp/Zelda64Recomp");
        apps.Should().OnlyContain(app => !string.IsNullOrWhiteSpace(app.Repository));
    }
}
