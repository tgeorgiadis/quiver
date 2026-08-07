using FluentAssertions;
using Quiver.Services;

namespace Quiver.Tests;

public class UserDataMigrationTests
{
    [Fact]
    public void CopyUserData_copies_files_and_dirs_without_overwriting()
    {
        var src = Path.Combine(Path.GetTempPath(), "QuiverMigSrc_" + Guid.NewGuid().ToString("N"));
        var dst = Path.Combine(Path.GetTempPath(), "QuiverMigDst_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(Path.Combine(src, "Apps", "GameA"));
        Directory.CreateDirectory(Path.Combine(src, "Cache"));
        File.WriteAllText(Path.Combine(src, "apps.json"), "{\"apps\":[{\"name\":\"A\"}]}");
        File.WriteAllText(Path.Combine(src, "settings.json"), "{\"x\":1}");
        File.WriteAllText(Path.Combine(src, "Apps", "GameA", "note.txt"), "keep");
        Directory.CreateDirectory(dst);
        File.WriteAllText(Path.Combine(dst, "settings.json"), "{\"x\":99}");

        try
        {
            var copied = UserDataMigration.CopyUserData(src, dst);
            copied.Should().BeGreaterThan(0);
            File.ReadAllText(Path.Combine(dst, "apps.json")).Should().Contain("A");
            File.ReadAllText(Path.Combine(dst, "settings.json")).Should().Be("{\"x\":99}");
            File.ReadAllText(Path.Combine(dst, "Apps", "GameA", "note.txt")).Should().Be("keep");
        }
        finally
        {
            Directory.Delete(src, recursive: true);
            Directory.Delete(dst, recursive: true);
        }
    }

    [Fact]
    public void HasPrimaryUserData_true_when_apps_json_has_entries()
    {
        var dir = Path.Combine(Path.GetTempPath(), "QuiverHasData_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "apps.json"), "{\"apps\":[{\"name\":\"X\"}]}");
            UserDataMigration.HasPrimaryUserData(dir).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ImportFromDirectory_returns_false_for_same_path()
    {
        var dir = Path.Combine(Path.GetTempPath(), "QuiverSame_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            UserDataMigration.ImportFromDirectory(dir, dir).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
