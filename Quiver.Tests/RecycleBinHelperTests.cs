using FluentAssertions;
using Quiver.Services;

namespace Quiver.Tests;

public class RecycleBinHelperTests
{
    [Fact]
    public void BuildLinuxTrashInfo_formats_path_and_timestamp()
    {
        var info = RecycleBinHelper.BuildLinuxTrashInfo(
            @"/home/deck/Games/Banjo",
            new DateTime(2026, 7, 14, 8, 30, 0, DateTimeKind.Utc));

        info.Should().Be(
            "[Trash Info]\n" +
            "Path=/home/deck/Games/Banjo\n" +
            "DeletionDate=2026-07-14T08:30:00\n");
    }

    [Fact]
    public void BuildLinuxTrashInfo_percent_encodes_spaces()
    {
        var info = RecycleBinHelper.BuildLinuxTrashInfo(
            "/home/deck/My Games/App",
            new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc));

        info.Should().Contain("Path=/home/deck/My%20Games/App\n");
    }

    [Fact]
    public void AllocateUniqueTrashName_avoids_existing_names()
    {
        var root = Path.Combine(Path.GetTempPath(), "quiver-trash-test-" + Guid.NewGuid().ToString("N"));
        var files = Path.Combine(root, "files");
        var info = Path.Combine(root, "info");
        Directory.CreateDirectory(files);
        Directory.CreateDirectory(info);

        try
        {
            Directory.CreateDirectory(Path.Combine(files, "Game"));
            File.WriteAllText(Path.Combine(info, "Game.trashinfo"), "x");

            RecycleBinHelper.AllocateUniqueTrashName(files, info, "Game").Should().Be("Game.1");

            Directory.CreateDirectory(Path.Combine(files, "Game.1"));
            RecycleBinHelper.AllocateUniqueTrashName(files, info, "Game").Should().Be("Game.2");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MoveToRecycleBin_throws_when_path_missing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "quiver-missing-" + Guid.NewGuid().ToString("N"));
        var act = () => RecycleBinHelper.MoveToRecycleBin(missing);
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void MoveToRecycleBin_moves_directory_on_supported_desktop()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        // Headless CI / non-desktop sessions may lack a usable trash; skip rather than fail the build.
        if (OperatingSystem.IsLinux() &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")) &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")) &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "quiver-recycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "marker.txt"), "keep-me");

        try
        {
            RecycleBinHelper.MoveToRecycleBin(root);
            Directory.Exists(root).Should().BeFalse();
        }
        catch (IOException) when (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // Trash may be unavailable in restricted environments.
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
