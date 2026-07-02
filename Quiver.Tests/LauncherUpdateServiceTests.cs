using FluentAssertions;
using Quiver.Services;

namespace Quiver.Tests;

public class LauncherUpdateServiceTests
{
    [Fact]
    public void ComputePendingUpdatesCount_sums_launcher_and_game_updates()
    {
        LauncherUpdateService.ComputePendingUpdatesCount(false, 0).Should().Be(0);
        LauncherUpdateService.ComputePendingUpdatesCount(true, 0).Should().Be(1);
        LauncherUpdateService.ComputePendingUpdatesCount(false, 3).Should().Be(3);
        LauncherUpdateService.ComputePendingUpdatesCount(true, 2).Should().Be(3);
        LauncherUpdateService.ComputePendingUpdatesCount(false, -1).Should().Be(0);
    }

    [Fact]
    public void IsLauncherUpdatePending_requires_newer_last_known_version()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "version.txt"), "1.0.0");
        File.WriteAllText(Path.Combine(tempDir, "update_check.json"),
            """{"LastCheckTime":"2026-01-01T00:00:00Z","UpdateAvailable":true,"LastKnownVersion":"v1.1.0","CurrentVersion":"1.0.0"}""");

        try
        {
            var service = new LauncherUpdateService();
            service.IsLauncherUpdatePending(tempDir).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

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
