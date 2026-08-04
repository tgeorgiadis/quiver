using System.Diagnostics;
using System.Text.Json;

namespace Quiver.Core.Services;

/// <summary>
/// Applies a downloaded Quiver self-update package (wait for host exit, swap files, restart).
/// Shared by <c>Quiver.Updater</c> so GUI/CLI stay consistent.
/// </summary>
public static class LauncherUpdateApplier
{
    public const int DefaultWaitTimeoutSeconds = 120;
    public const string UpdateCheckFileName = "update_check.json";
    public const string WindowsUpdaterFileName = "Quiver.Updater.exe";

    public sealed record Options(
        int WaitPid,
        string UpdateDir,
        string AppDir,
        string RestartPath,
        string VersionTag,
        string? DownloadZip = null,
        int WaitTimeoutSeconds = DefaultWaitTimeoutSeconds,
        TextWriter? Log = null);

    /// <summary>
    /// Runs the update. Returns 0 on success, non-zero on failure.
    /// </summary>
    public static int Run(Options options)
    {
        var log = options.Log ?? Console.Out;
        var updateDir = Path.GetFullPath(options.UpdateDir);
        var appDir = Path.GetFullPath(options.AppDir);
        var restartPath = Path.GetFullPath(options.RestartPath);
        var backupDir = Path.Combine(
            Path.GetTempPath(),
            "Quiver_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));

        log.WriteLine($"Quiver Updater - Version {options.VersionTag}");
        log.WriteLine();

        if (!Directory.Exists(updateDir))
        {
            log.WriteLine($"Update directory not found: {updateDir}");
            return 1;
        }

        if (!Directory.Exists(appDir))
        {
            log.WriteLine($"App directory not found: {appDir}");
            return 1;
        }

        log.WriteLine("Waiting for Quiver to close...");
        if (!WaitForProcessExit(options.WaitPid, options.WaitTimeoutSeconds, log))
        {
            log.WriteLine("Launcher did not close in time. Aborting update to avoid replacing files while the app is still running.");
            return 2;
        }

        try
        {
            Directory.CreateDirectory(backupDir);

            var updateEntries = Directory.GetFileSystemEntries(updateDir)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Cast<string>()
                .ToList();

            var entriesToApply = UpdaterUserDataPreservation.GetUpdateEntriesToApply(updateEntries).ToList();

            log.WriteLine("Backing up current version...");
            foreach (var entry in entriesToApply)
            {
                var sourcePath = Path.Combine(appDir, entry);
                if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
                    continue;

                var backupPath = Path.Combine(backupDir, entry);
                CopyEntry(sourcePath, backupPath);
            }

            log.WriteLine("Applying update...");
            try
            {
                foreach (var entry in entriesToApply)
                {
                    var fromPath = Path.Combine(updateDir, entry);
                    var toPath = Path.Combine(appDir, entry);
                    CopyEntry(fromPath, toPath);
                }
            }
            catch (Exception ex)
            {
                log.WriteLine($"Update failed! Restoring backup... ({ex.Message})");
                try
                {
                    foreach (var entry in Directory.GetFileSystemEntries(backupDir))
                    {
                        var name = Path.GetFileName(entry);
                        CopyEntry(entry, Path.Combine(appDir, name));
                    }
                }
                catch (Exception restoreEx)
                {
                    log.WriteLine($"Backup restore also failed: {restoreEx.Message}");
                }

                return 3;
            }

            WriteUpdateCheckFile(appDir, options.VersionTag);
            log.WriteLine("Update completed successfully!");
            log.WriteLine("Restarting Quiver...");

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = restartPath,
                    WorkingDirectory = appDir,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                log.WriteLine($"Failed to restart Quiver: {ex.Message}");
                return 4;
            }

            return 0;
        }
        finally
        {
            log.WriteLine("Cleaning up temporary files...");
            TryDeleteDirectory(backupDir);
            if (!string.IsNullOrWhiteSpace(options.DownloadZip))
                TryDeleteFile(options.DownloadZip);
            TryDeleteDirectory(updateDir);
        }
    }

    public static bool WaitForProcessExit(int pid, int timeoutSeconds, TextWriter? log = null)
    {
        if (pid <= 0)
            return true;

        Process? process = null;
        try
        {
            process = Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            return true;
        }

        using (process)
        {
            for (var waited = 0; waited < timeoutSeconds; waited++)
            {
                if (process.HasExited)
                    return true;

                Thread.Sleep(1000);
            }

            return process.HasExited;
        }
    }

    public static void WriteUpdateCheckFile(string appDir, string versionTag)
    {
        var path = Path.Combine(appDir, UpdateCheckFileName);
        var payload = new Dictionary<string, object?>
        {
            ["CurrentVersion"] = versionTag,
            ["LastCheckTime"] = DateTime.UtcNow.ToString("o"),
            ["LastKnownVersion"] = versionTag,
            ["ETag"] = "",
            ["UpdateAvailable"] = false,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload));
    }

    private static void CopyEntry(string sourcePath, string destinationPath)
    {
        if (Directory.Exists(sourcePath))
        {
            CopyDirectory(sourcePath, destinationPath);
            return;
        }

        var destDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);

        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destinationDir, Path.GetFileName(directory));
            CopyDirectory(directory, destSubDir);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best effort — locked updater binaries may remain until TEMP cleanup.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort.
        }
    }
}
