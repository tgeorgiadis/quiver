using FluentAssertions;
using Quiver.Services;

namespace Quiver.Tests;

public class CommunityCatalogIndexTests
{
    [Fact]
    public void ParseIndex_reads_community_index_fixture()
    {
        var index = CommunityCatalogIndexService.ParseIndex(TestFixtures.ReadCommunityIndexJson());

        index.Version.Should().Be(1);
        index.Lists.Should().ContainSingle(list =>
            list.Id == "n64-recomp" &&
            list.Name == "N64 Recomp Games" &&
            list.Location == "community-app-lists/n64-recomp.json" &&
            list.ListVersion == "1.0.0");
    }

    [Fact]
    public async Task FetchIndexAsync_reads_local_index_file()
    {
        var (index, error) = await CommunityCatalogIndexService.FetchIndexAsync(
            new HttpClient(),
            TestFixtures.CommunityIndexPath);

        error.Should().BeNull();
        index.Should().NotBeNull();
        index!.Lists.Should().NotBeEmpty();
    }
}
