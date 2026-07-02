using FluentAssertions;
using Quiver.Models;
using Quiver.Services;

namespace Quiver.Tests;

public class CatalogCompareServiceTests
{
    private static GameInfo CreateApp(
        string repository,
        string name = "Test App",
        string folderName = "TestFolder",
        string? tags = null)
    {
        return new GameInfo
        {
            Repository = repository,
            Name = name,
            FolderName = folderName,
            Tags = tags?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? [],
        };
    }

    [Fact]
    public void BuildCompareRows_classifies_local_external_unchanged_and_changed()
    {
        var local = new List<GameInfo>
        {
            CreateApp("owner/local-only", "Local Only"),
            CreateApp("owner/shared", "Old Name"),
        };

        var external = new List<GameInfo>
        {
            CreateApp("owner/shared", "New Name"),
            CreateApp("owner/external-only", "External Only"),
        };

        var rows = CatalogCompareService.BuildCompareRows(local, external);

        rows.Should().Contain(r => r.Repository == "owner/local-only" && r.Status == CatalogSyncStatus.InLocalOnly);
        rows.Should().Contain(r => r.Repository == "owner/external-only" && r.Status == CatalogSyncStatus.InExternalOnly);
        rows.Should().Contain(r => r.Repository == "owner/shared" && r.Status == CatalogSyncStatus.Changed);
    }

    [Fact]
    public void MergeExternalIntoLocal_unions_tags_and_keeps_local_skipped_version()
    {
        var local = CreateApp("owner/app", "Local Name");
        local.SkippedUpdateVersion = "v1.0.0";
        local.Tags = ["a"];

        var external = CreateApp("owner/app", "External Name");
        external.Tags = ["b"];

        var merged = CatalogCompareService.MergeExternalIntoLocal(local, external);

        merged.Name.Should().Be("External Name");
        merged.SkippedUpdateVersion.Should().Be("v1.0.0");
        merged.Tags.Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    public void ApplyAddAllExternalOnly_appends_missing_repositories()
    {
        var local = new List<GameInfo> { CreateApp("owner/existing") };
        var rows = CatalogCompareService.BuildCompareRows(
            local,
            [CreateApp("owner/existing"), CreateApp("owner/new")]);

        var updated = CatalogCompareService.ApplyAddAllExternalOnly(local, rows);

        updated.Select(a => a.Repository).Should().BeEquivalentTo(["owner/existing", "owner/new"]);
    }

    [Fact]
    public void ApplyReplaceAllChanged_overwrites_catalog_fields()
    {
        var local = new List<GameInfo> { CreateApp("owner/app", "Old Name", "OldFolder") };
        var external = new List<GameInfo> { CreateApp("owner/app", "New Name", "NewFolder") };
        var rows = CatalogCompareService.BuildCompareRows(local, external);

        var updated = CatalogCompareService.ApplyReplaceAllChanged(local, rows);

        updated.Single().Name.Should().Be("New Name");
        updated.Single().FolderName.Should().Be("NewFolder");
    }

    [Fact]
    public void FilterVisibleRows_hides_unchanged_by_default()
    {
        var local = new List<GameInfo> { CreateApp("owner/same", "Same", "Folder") };
        var external = new List<GameInfo>
        {
            CreateApp("owner/same", "Same", "Folder"),
            CreateApp("owner/new", "New", "NewFolder"),
        };
        var source = new AppCatalogSource { CachedListVersion = "1.0.0" };
        var rows = CatalogCompareService.BuildCompareRows(local, external);

        CatalogCompareService.FilterVisibleRows(rows, source, showUpToDateApps: false)
            .Should()
            .ContainSingle(r => r.Repository == "owner/new");

        CatalogCompareService.FilterVisibleRows(rows, source, showUpToDateApps: true)
            .Should()
            .HaveCount(2);
    }

    [Fact]
    public void IgnoreChangesForCurrentVersion_hides_actionable_row()
    {
        var local = new List<GameInfo> { CreateApp("owner/app", "Old", "Folder") };
        var external = new List<GameInfo> { CreateApp("owner/app", "New", "Folder") };
        var source = new AppCatalogSource { CachedListVersion = "2.0.0" };
        var rows = CatalogCompareService.BuildCompareRows(local, external);

        CatalogCompareService.HasActionableChanges(source, rows).Should().BeTrue();

        CatalogCompareService.IgnoreChangesForCurrentVersion(source, "owner/app");

        CatalogCompareService.HasActionableChanges(source, rows).Should().BeFalse();
        CatalogCompareService.FilterVisibleRows(rows, source, showUpToDateApps: false).Should().BeEmpty();
    }

    [Fact]
    public void BuildTagDiff_highlights_local_only_and_external_only_tags()
    {
        var local = CreateApp("owner/app");
        local.Tags = ["n64", "pokemon", "ai"];
        var external = CreateApp("owner/app");
        external.Tags = ["n64", "pokemon", "recomp"];

        var diff = CatalogSyncFieldDiffBuilder.BuildFieldDiffs(
            CatalogSyncStatus.Changed,
            local,
            external,
            ["tags"]).Single();

        diff.IsTagDiff.Should().BeTrue();
        diff.TagDiffs.Single(t => t.Tag == "ai").Kind.Should().Be(CatalogSyncTagDiffKind.LocalOnly);
        diff.TagDiffs.Single(t => t.Tag == "recomp").Kind.Should().Be(CatalogSyncTagDiffKind.ExternalOnly);
        diff.TagDiffs.Single(t => t.Tag == "pokemon").Kind.Should().Be(CatalogSyncTagDiffKind.Shared);
    }

    [Fact]
    public void BuildIconDiff_uses_icon_kind_with_urls()
    {
        var local = CreateApp("owner/app");
        local.GameIconUrl = "https://example.com/local.png";
        var external = CreateApp("owner/app");
        external.GameIconUrl = "https://example.com/external.png";

        var diff = CatalogSyncFieldDiffBuilder.BuildFieldDiffs(
            CatalogSyncStatus.Changed,
            local,
            external,
            ["appIconUrl"]).Single();

        diff.Kind.Should().Be(CatalogSyncFieldDiffKind.Icon);
        diff.IsIconDiff.Should().BeTrue();
        diff.IsTextValueDiff.Should().BeFalse();
        diff.LocalValue.Should().Be("https://example.com/local.png");
        diff.ExternalValue.Should().Be("https://example.com/external.png");
    }

    [Fact]
    public void BuildValueDiff_shows_old_and_new_values()
    {
        var local = CreateApp("owner/app", "Old Name", "OldFolder");
        var external = CreateApp("owner/app", "New Name", "OldFolder");

        var diff = CatalogSyncFieldDiffBuilder.BuildFieldDiffs(
            CatalogSyncStatus.Changed,
            local,
            external,
            ["name"]).Single();

        diff.LocalValue.Should().Be("Old Name");
        diff.ExternalValue.Should().Be("New Name");
        diff.ShowArrow.Should().BeTrue();
    }

    [Fact]
    public void FormatVersionForDisplay_shortens_long_hashes()
    {
        const string hash = "C926BD289048466BFFD7494256C430205E57E3D8D8430661ECF2C39A919813FC";
        CatalogCompareService.FormatVersionForDisplay(hash).Should().Be("C926BD28…919813FC");
    }

    [Fact]
    public void FormatCatalogVersionSummary_shows_not_reviewed_for_unacknowledged()
    {
        CatalogCompareService.FormatCatalogVersionSummary("1.0.0", null)
            .Should().Be("Cached v1.0.0 · Not reviewed yet");
        CatalogCompareService.FormatCatalogVersionSummary("1.0.0", "0")
            .Should().Be("Cached v1.0.0 · Not reviewed yet");
    }

    [Fact]
    public void FormatCatalogVersionSummary_shows_reviewed_version_when_acknowledged()
    {
        CatalogCompareService.FormatCatalogVersionSummary("1.0.0", "1.0.0")
            .Should().Be("Cached v1.0.0 · Reviewed v1.0.0");
    }

    [Fact]
    public void TryAutoAcknowledgeIfReviewComplete_updates_acknowledged_version()
    {
        var source = new AppCatalogSource
        {
            CachedListVersion = "1.0.0",
            AcknowledgedListVersion = "0",
            UpdateAvailable = true,
        };

        AppCatalogService.TryAutoAcknowledgeIfReviewComplete(source, pendingCount: 0).Should().BeTrue();
        source.AcknowledgedListVersion.Should().Be("1.0.0");
        source.UpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public void TryAutoAcknowledgeIfReviewComplete_does_nothing_when_pending_remains()
    {
        var source = new AppCatalogSource
        {
            CachedListVersion = "1.0.0",
            AcknowledgedListVersion = "0",
        };

        AppCatalogService.TryAutoAcknowledgeIfReviewComplete(source, pendingCount: 3).Should().BeFalse();
        source.AcknowledgedListVersion.Should().Be("0");
    }

    [Fact]
    public void BuildCompareRows_includes_inline_field_diffs_for_changed_apps()
    {
        var local = new List<GameInfo> { CreateApp("owner/app", "Old", "Folder") };
        local[0].Tags = ["ai"];
        var external = new List<GameInfo> { CreateApp("owner/app", "New", "Folder") };
        external[0].Tags = ["ai", "recomp"];

        var rows = CatalogCompareService.BuildCompareRows(local, external);
        var row = rows.Single(r => r.Repository == "owner/app");

        row.HasInlineDiff.Should().BeTrue();
        row.FieldDiffs.Should().NotBeEmpty();
    }

    [Fact]
    public void FilterByReviewFilter_all_includes_unchanged_and_ignored()
    {
        var local = new List<GameInfo>
        {
            CreateApp("owner/same", "Same", "Folder"),
            CreateApp("owner/changed", "Old", "Folder"),
        };
        var external = new List<GameInfo>
        {
            CreateApp("owner/same", "Same", "Folder"),
            CreateApp("owner/changed", "New", "Folder"),
            CreateApp("owner/new", "New App", "NewFolder"),
        };
        var source = new AppCatalogSource { CachedListVersion = "2.0.0" };
        var rows = CatalogCompareService.BuildCompareRows(local, external);
        CatalogCompareService.IgnoreChangesForCurrentVersion(source, "owner/changed");

        CatalogCompareService.FilterByReviewFilter(rows, source, CatalogReviewFilter.All)
            .Should()
            .HaveCount(3);
    }

    [Fact]
    public void FilterByReviewFilter_needs_review_excludes_ignored_and_unchanged()
    {
        var local = new List<GameInfo>
        {
            CreateApp("owner/same", "Same", "Folder"),
            CreateApp("owner/changed", "Old", "Folder"),
        };
        var external = new List<GameInfo>
        {
            CreateApp("owner/same", "Same", "Folder"),
            CreateApp("owner/changed", "New", "Folder"),
            CreateApp("owner/new", "New App", "NewFolder"),
        };
        var source = new AppCatalogSource { CachedListVersion = "2.0.0" };
        var rows = CatalogCompareService.BuildCompareRows(local, external);
        CatalogCompareService.IgnoreChangesForCurrentVersion(source, "owner/changed");

        CatalogCompareService.FilterByReviewFilter(rows, source, CatalogReviewFilter.NeedsReview)
            .Should()
            .ContainSingle(r => r.Repository == "owner/new");
    }

    [Fact]
    public void FilterByReviewFilter_not_in_library_includes_ignored_external_only()
    {
        var local = new List<GameInfo>();
        var external = new List<GameInfo>
        {
            CreateApp("owner/new", "New App", "NewFolder"),
            CreateApp("owner/ignored", "Ignored App", "IgnoredFolder"),
        };
        var source = new AppCatalogSource { CachedListVersion = "1.0.0" };
        var rows = CatalogCompareService.BuildCompareRows(local, external);
        CatalogCompareService.IgnoreChangesForCurrentVersion(source, "owner/ignored");

        CatalogCompareService.FilterByReviewFilter(rows, source, CatalogReviewFilter.New)
            .Select(r => r.Repository)
            .Should()
            .BeEquivalentTo(["owner/new"]);

        CatalogCompareService.FilterByReviewFilter(rows, source, CatalogReviewFilter.NotInLibrary)
            .Select(r => r.Repository)
            .Should()
            .BeEquivalentTo(["owner/new", "owner/ignored"]);
    }

    [Fact]
    public void FilterByReviewFilter_not_in_library_excludes_hidden_and_non_external_rows()
    {
        var local = new List<GameInfo>
        {
            CreateApp("owner/local-only", "Local Only", "LocalFolder"),
            CreateApp("owner/changed", "Old", "Folder"),
        };
        var external = new List<GameInfo>
        {
            CreateApp("owner/local-only"),
            CreateApp("owner/changed", "New", "Folder"),
            CreateApp("owner/new", "New App", "NewFolder"),
            CreateApp("owner/hidden", "Hidden App", "HiddenFolder"),
        };
        var source = new AppCatalogSource { CachedListVersion = "1.0.0" };
        var rows = CatalogCompareService.BuildCompareRows(local, external);

        CatalogCompareService.HideFromReview(source, "owner/hidden");

        CatalogCompareService.FilterByReviewFilter(rows, source, CatalogReviewFilter.NotInLibrary)
            .Select(r => r.Repository)
            .Should()
            .BeEquivalentTo(["owner/new"]);

        CatalogCompareService.FilterByReviewFilter(rows, source, CatalogReviewFilter.Hidden)
            .Select(r => r.Repository)
            .Should()
            .Contain("owner/hidden");
    }

    [Fact]
    public void BuildCompareRows_sets_icon_url_from_external_or_local()
    {
        var local = CreateApp("owner/app", "Local");
        local.GameIconUrl = "https://example.com/local.png";
        var external = CreateApp("owner/app", "External");
        external.GameIconUrl = "https://example.com/external.png";

        var row = CatalogCompareService.BuildCompareRows([local], [external]).Single();
        row.IconUrl.Should().Be("https://example.com/external.png");
    }

    [Fact]
    public void HideFromReview_excludes_row_from_all_and_needs_review_filters()
    {
        var local = new List<GameInfo>
        {
            CreateApp("owner/same", "Same", "Folder"),
            CreateApp("owner/changed", "Old", "Folder"),
        };
        var external = new List<GameInfo>
        {
            CreateApp("owner/same", "Same", "Folder"),
            CreateApp("owner/changed", "New", "Folder"),
            CreateApp("owner/new", "New App", "NewFolder"),
        };
        var source = new AppCatalogSource { CachedListVersion = "2.0.0" };
        var rows = CatalogCompareService.BuildCompareRows(local, external);

        CatalogCompareService.HideFromReview(source, "owner/new");

        CatalogCompareService.IsActionableRow(rows.Single(r => r.Repository == "owner/new"), source).Should().BeFalse();
        CatalogCompareService.FilterByReviewFilter(rows, source, CatalogReviewFilter.All)
            .Select(r => r.Repository)
            .Should().BeEquivalentTo(["owner/same", "owner/changed"]);
        CatalogCompareService.FilterByReviewFilter(rows, source, CatalogReviewFilter.NeedsReview)
            .Should()
            .ContainSingle(r => r.Repository == "owner/changed");
        CatalogCompareService.FilterByReviewFilter(rows, source, CatalogReviewFilter.New)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void Hidden_filter_shows_only_hidden_rows()
    {
        var local = new List<GameInfo> { CreateApp("owner/local-only") };
        var external = new List<GameInfo>
        {
            CreateApp("owner/local-only"),
            CreateApp("owner/new", "New App", "NewFolder"),
        };
        var source = new AppCatalogSource { CachedListVersion = "1.0.0" };
        var rows = CatalogCompareService.BuildCompareRows(local, external);

        CatalogCompareService.HideFromReview(source, "owner/new");
        CatalogCompareService.HideFromReview(source, "owner/local-only");

        CatalogCompareService.FilterByReviewFilter(rows, source, CatalogReviewFilter.Hidden)
            .Select(r => r.Repository)
            .Should()
            .BeEquivalentTo(["owner/local-only", "owner/new"]);
    }

    [Fact]
    public void UnhideFromReview_restores_row_to_review_filters()
    {
        var local = new List<GameInfo>();
        var external = new List<GameInfo> { CreateApp("owner/new", "New App", "NewFolder") };
        var source = new AppCatalogSource { CachedListVersion = "1.0.0" };
        var rows = CatalogCompareService.BuildCompareRows(local, external);

        CatalogCompareService.HideFromReview(source, "owner/new");
        CatalogCompareService.FilterByReviewFilter(rows, source, CatalogReviewFilter.New).Should().BeEmpty();

        CatalogCompareService.UnhideFromReview(source, "owner/new");
        CatalogCompareService.FilterByReviewFilter(rows, source, CatalogReviewFilter.New)
            .Should()
            .ContainSingle(r => r.Repository == "owner/new");
    }

    [Fact]
    public void Hidden_repositories_survive_acknowledge_source_version()
    {
        var local = new List<GameInfo>();
        var external = new List<GameInfo> { CreateApp("owner/new", "New App", "NewFolder") };
        var source = new AppCatalogSource
        {
            CachedListVersion = "2.0.0",
            AcknowledgedListVersion = "1.0.0",
        };
        var rows = CatalogCompareService.BuildCompareRows(local, external);

        CatalogCompareService.HideFromReview(source, "owner/new");

        var service = new AppCatalogService();
        service.AcknowledgeSourceVersion(source);

        source.HiddenFromReviewRepositories.Should().Contain("owner/new");
        CatalogCompareService.FilterByReviewFilter(rows, source, CatalogReviewFilter.Hidden)
            .Should()
            .ContainSingle(r => r.Repository == "owner/new");
        CatalogCompareService.FilterByReviewFilter(rows, source, CatalogReviewFilter.New).Should().BeEmpty();
    }
}
