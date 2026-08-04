using FluentAssertions;
using UpdaterProgram = Quiver.Updater.Program;

namespace Quiver.Tests;

public class QuiverUpdaterArgsTests
{
    [Fact]
    public void TryParseArgs_parses_required_and_optional_values()
    {
        var ok = UpdaterProgram.TryParseArgs(
            [
                "--wait-pid", "99",
                "--update-dir", @"C:\u",
                "--app-dir", @"C:\a",
                "--restart", @"C:\a\Quiver.exe",
                "--version-tag", "v9.9.9",
                "--download-zip", @"C:\z.zip",
                "--wait-timeout", "30",
            ],
            out var options,
            out var error);

        ok.Should().BeTrue(error);
        error.Should().BeEmpty();
        options.WaitPid.Should().Be(99);
        options.UpdateDir.Should().Be(@"C:\u");
        options.AppDir.Should().Be(@"C:\a");
        options.RestartPath.Should().Be(@"C:\a\Quiver.exe");
        options.VersionTag.Should().Be("v9.9.9");
        options.DownloadZip.Should().Be(@"C:\z.zip");
        options.WaitTimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public void TryParseArgs_rejects_missing_required_flag()
    {
        var ok = UpdaterProgram.TryParseArgs(
            ["--wait-pid", "1", "--update-dir", @"C:\u"],
            out _,
            out var error);

        ok.Should().BeFalse();
        error.Should().Contain("--app-dir");
    }
}
