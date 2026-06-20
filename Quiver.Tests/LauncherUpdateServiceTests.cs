using FluentAssertions;
using Quiver.Services;

namespace Quiver.Tests;

public class LauncherUpdateServiceTests
{
    [Fact]
    public void IsUpdateAvailable_detects_newer_tag()
    {
        var service = new LauncherUpdateService();
        service.IsUpdateAvailable("1.0.0", "v1.1.0").Should().BeTrue();
        service.IsUpdateAvailable("1.1.0", "v1.1.0").Should().BeFalse();
    }

    [Fact]
    public void ReadInstalledVersion_reads_version_txt_from_directory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "version.txt"), "2.3.4");

        try
        {
            new LauncherUpdateService().ReadInstalledVersion(tempDir).Should().Be("2.3.4");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
