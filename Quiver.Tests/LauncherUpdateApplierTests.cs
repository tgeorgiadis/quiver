using FluentAssertions;
using Quiver.Core.Services;

namespace Quiver.Tests;

public class LauncherUpdateApplierTests
{
    [Fact]
    public void Run_applies_non_preserved_files_and_writes_update_check()
    {
        var root = Path.Combine(Path.GetTempPath(), "QuiverUpdateApplier_" + Guid.NewGuid().ToString("N"));
        var appDir = Path.Combine(root, "app");
        var updateDir = Path.Combine(root, "update");
        var restartPath = Path.Combine(appDir, "Quiver.exe");

        try
        {
            Directory.CreateDirectory(appDir);
            Directory.CreateDirectory(updateDir);

            File.WriteAllText(Path.Combine(appDir, "Quiver.exe"), "old-exe");
            File.WriteAllText(Path.Combine(appDir, "apps.json"), "{\"apps\":[]}");
            File.WriteAllText(Path.Combine(appDir, "settings.json"), "{}");
            File.WriteAllText(Path.Combine(appDir, "version.txt"), "1.0.0");

            File.WriteAllText(Path.Combine(updateDir, "Quiver.exe"), "new-exe-content-large-enough");
            File.WriteAllText(Path.Combine(updateDir, "Quiver.Updater.exe"), "updater-binary-placeholder");
            File.WriteAllText(Path.Combine(updateDir, "version.txt"), "2.0.0");
            File.WriteAllText(Path.Combine(updateDir, "apps.json"), "SHOULD_NOT_APPLY");

            using var log = new StringWriter();
            var exitCode = LauncherUpdateApplier.Run(new LauncherUpdateApplier.Options(
                WaitPid: 0,
                UpdateDir: updateDir,
                AppDir: appDir,
                RestartPath: restartPath,
                VersionTag: "v2.0.0",
                Log: log));

            // Restart may fail if Quiver.exe is not a real PE; treat apply + metadata as success when exit is 0 or 4.
            exitCode.Should().BeOneOf(0, 4);

            File.ReadAllText(Path.Combine(appDir, "Quiver.exe")).Should().Be("new-exe-content-large-enough");
            File.ReadAllText(Path.Combine(appDir, "version.txt")).Should().Be("2.0.0");
            File.ReadAllText(Path.Combine(appDir, "apps.json")).Should().Be("{\"apps\":[]}");
            File.ReadAllText(Path.Combine(appDir, "settings.json")).Should().Be("{}");

            var updateCheck = File.ReadAllText(Path.Combine(appDir, LauncherUpdateApplier.UpdateCheckFileName));
            updateCheck.Should().Contain("v2.0.0");
            updateCheck.Should().Contain("\"UpdateAvailable\":false");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public void WaitForProcessExit_returns_true_for_missing_pid()
    {
        LauncherUpdateApplier.WaitForProcessExit(int.MaxValue - 7, timeoutSeconds: 1).Should().BeTrue();
    }
}
