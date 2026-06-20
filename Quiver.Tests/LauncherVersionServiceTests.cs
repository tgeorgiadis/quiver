using FluentAssertions;
using Quiver.Services;

namespace Quiver.Tests;

public class LauncherVersionServiceTests
{
    [Theory]
    [InlineData("v1.2.3", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("0.9.0", "1.0.0", false)]
    [InlineData("V2.0", "1.9.9", true)]
    public void IsNewerVersion_compares_semantic_versions(string candidate, string baseline, bool expected)
    {
        LauncherVersionService.IsNewerVersion(candidate, baseline).Should().Be(expected);
    }

    [Fact]
    public void AreVersionsEquivalent_treats_v_prefix_as_equal()
    {
        LauncherVersionService.AreVersionsEquivalent("v1.0.0", "1.0.0").Should().BeTrue();
    }

    [Fact]
    public void NormalizeVersionString_pads_short_versions()
    {
        LauncherVersionService.NormalizeVersionString("v2").Should().Be("2.0.0");
    }

    [Fact]
    public void ReadInstalledVersion_reads_version_file_from_directory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "Quiver.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        File.WriteAllText(Path.Combine(tempDirectory, "version.txt"), "v1.2.3");

        try
        {
            LauncherVersionService.ReadInstalledVersion(tempDirectory).Should().Be("v1.2.3");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
