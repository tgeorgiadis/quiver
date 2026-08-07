using System.Runtime.InteropServices;

namespace Quiver.Services;

/// <summary>
/// Copies legacy flat-portable user data into <see cref="QuiverPaths.UserDataRoot"/> without deleting the source.
/// </summary>
public static class UserDataMigration
{
    public static readonly string[] PreservedFileNames =
    [
        "apps.json",
        "settings.json",
        "games.json",
        "game.json",
    ];

    public static readonly string[] PreservedDirectoryNames =
    [
        "Cache",
        "Apps",
    ];

    public const string ImportMarkerFileName = ".quiver_userdata_import_offered";

    /// <summary>
    /// If the user-data root has no library yet, copy from the first legacy candidate that has data.
    /// Returns the source directory when a copy ran; otherwise null.
    /// </summary>
    public static string? TryMigrateFromLegacyCandidates(string? destinationRoot = null)
    {
        var dest = destinationRoot ?? QuiverPaths.UserDataRoot;
        Directory.CreateDirectory(dest);

        if (HasPrimaryUserData(dest))
            return null;

        foreach (var candidate in QuiverPaths.GetLegacyCandidateDirectories())
        {
            if (PathsEqual(candidate, dest))
                continue;
            if (!QuiverPaths.LooksLikeLegacyUserDataDirectory(candidate))
                continue;

            CopyUserData(candidate, dest);
            return candidate;
        }

        return null;
    }

    /// <summary>
    /// Copy user data from an explicit folder (import picker). Returns true if anything was copied.
    /// </summary>
    public static bool ImportFromDirectory(string sourceDirectory, string? destinationRoot = null)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
            return false;

        var dest = destinationRoot ?? QuiverPaths.UserDataRoot;
        if (PathsEqual(sourceDirectory, dest))
            return false;

        Directory.CreateDirectory(dest);
        return CopyUserData(sourceDirectory, dest) > 0;
    }

    public static bool ShouldOfferImportPicker(string? destinationRoot = null)
    {
        var dest = destinationRoot ?? QuiverPaths.UserDataRoot;
        if (HasPrimaryUserData(dest))
            return false;

        var marker = Path.Combine(dest, ImportMarkerFileName);
        return !File.Exists(marker);
    }

    public static void MarkImportOffered(string? destinationRoot = null)
    {
        var dest = destinationRoot ?? QuiverPaths.UserDataRoot;
        Directory.CreateDirectory(dest);
        var marker = Path.Combine(dest, ImportMarkerFileName);
        File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
    }

    public static bool HasPrimaryUserData(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return false;

        var appsJson = Path.Combine(directory, "apps.json");
        if (File.Exists(appsJson))
        {
            try
            {
                var text = File.ReadAllText(appsJson);
                if (!string.IsNullOrWhiteSpace(text) && text.Contains("\"name\"", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch
            {
                return true;
            }
        }

        if (File.Exists(Path.Combine(directory, "settings.json")))
            return true;

        var appsDir = Path.Combine(directory, "Apps");
        if (Directory.Exists(appsDir) && Directory.EnumerateFileSystemEntries(appsDir).Any())
            return true;

        return false;
    }

    public static int CopyUserData(string sourceDirectory, string destinationDirectory)
    {
        var copied = 0;
        Directory.CreateDirectory(destinationDirectory);

        foreach (var fileName in PreservedFileNames)
        {
            var src = Path.Combine(sourceDirectory, fileName);
            var dst = Path.Combine(destinationDirectory, fileName);
            if (!File.Exists(src))
                continue;
            if (File.Exists(dst))
                continue;

            File.Copy(src, dst, overwrite: false);
            copied++;
        }

        foreach (var dirName in PreservedDirectoryNames)
        {
            var srcDir = Path.Combine(sourceDirectory, dirName);
            var dstDir = Path.Combine(destinationDirectory, dirName);
            if (!Directory.Exists(srcDir))
                continue;

            copied += CopyDirectoryRecursive(srcDir, dstDir);
        }

        return copied;
    }

    private static int CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        var copied = 0;
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            if (File.Exists(destFile))
                continue;
            File.Copy(file, destFile, overwrite: false);
            copied++;
        }

        foreach (var dir in Directory.EnumerateDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            copied += CopyDirectoryRecursive(dir, Path.Combine(destDir, name));
        }

        return copied;
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            Path.GetFullPath(a.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Path.GetFullPath(b.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
}
