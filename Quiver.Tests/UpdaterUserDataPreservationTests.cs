using FluentAssertions;
using Quiver.Services;

namespace Quiver.Tests;

public class UpdaterUserDataPreservationTests
{
    [Theory]
    [InlineData("apps.json", true)]
    [InlineData("settings.json", true)]
    [InlineData("games.json", true)]
    [InlineData("Cache", true)]
    [InlineData("Quiver.exe", false)]
    [InlineData("version.txt", false)]
    [InlineData("update_check.json", false)]
    public void IsPreservedTopLevelEntry_identifies_user_data_entries(string name, bool expected)
    {
        UpdaterUserDataPreservation.IsPreservedTopLevelEntry(name).Should().Be(expected);
    }

    [Fact]
    public void GetUpdateEntriesToApply_excludes_apps_json_that_would_wipe_library()
    {
        var updateEntries = new[] { "Quiver.exe", "apps.json", "version.txt", "settings.json", "Cache" };

        var entriesToApply = UpdaterUserDataPreservation.GetUpdateEntriesToApply(updateEntries).ToList();

        entriesToApply.Should().BeEquivalentTo(["Quiver.exe", "version.txt"]);
    }

    [Fact]
    public void BuildUnixPreserveCasePattern_includes_all_preserved_entries()
    {
        var pattern = UpdaterUserDataPreservation.BuildUnixPreserveCasePattern();

        pattern.Should().Contain("apps.json");
        pattern.Should().Contain("settings.json");
        pattern.Should().Contain("games.json");
        pattern.Should().Contain("Cache");
    }

    [Fact]
    public void BuildWindowsPreservedEntryCheckSubroutine_includes_all_preserved_entries()
    {
        var subroutine = UpdaterUserDataPreservation.BuildWindowsPreservedEntryCheckSubroutine();

        subroutine.Should().Contain(":IsPreservedUserDataEntry");
        subroutine.Should().Contain("apps.json");
        subroutine.Should().Contain("settings.json");
        subroutine.Should().Contain("games.json");
        subroutine.Should().Contain("Cache");
    }
}
