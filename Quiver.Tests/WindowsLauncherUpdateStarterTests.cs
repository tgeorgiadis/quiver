using FluentAssertions;
using Quiver.Services;

namespace Quiver.Tests;

public class WindowsLauncherUpdateStarterTests
{
    [Fact]
    public void BuildUpdaterArguments_includes_required_flags()
    {
        var args = WindowsLauncherUpdateStarter.BuildUpdaterArguments(
            waitPid: 4242,
            updateDir: @"C:\Temp\update",
            appDir: @"C:\Apps\Quiver",
            restartPath: @"C:\Apps\Quiver\Quiver.exe",
            versionTag: "v2.4.3",
            downloadZip: @"C:\Temp\Quiver-Windows-x64.zip",
            waitTimeoutSeconds: 90);

        args.Should().Contain("--wait-pid \"4242\"");
        args.Should().Contain("--update-dir \"C:\\Temp\\update\"");
        args.Should().Contain("--app-dir \"C:\\Apps\\Quiver\"");
        args.Should().Contain("--restart \"C:\\Apps\\Quiver\\Quiver.exe\"");
        args.Should().Contain("--version-tag \"v2.4.3\"");
        args.Should().Contain("--download-zip \"C:\\Temp\\Quiver-Windows-x64.zip\"");
        args.Should().Contain("--wait-timeout \"90\"");
    }
}
