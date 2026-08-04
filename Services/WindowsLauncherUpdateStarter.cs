using System.Diagnostics;
using System.Text;
using Quiver.Core.Services;

namespace Quiver.Services;

/// <summary>
/// Launches the signed <c>Quiver.Updater.exe</c> helper for Windows self-updates
/// (replaces the previous temp <c>.cmd</c> script).
/// </summary>
public static class WindowsLauncherUpdateStarter
{
    public static string BuildUpdaterArguments(
        int waitPid,
        string updateDir,
        string appDir,
        string restartPath,
        string versionTag,
        string? downloadZip = null,
        int waitTimeoutSeconds = LauncherUpdateApplier.DefaultWaitTimeoutSeconds)
    {
        var sb = new StringBuilder();
        AppendArg(sb, "--wait-pid", waitPid.ToString());
        AppendArg(sb, "--update-dir", updateDir);
        AppendArg(sb, "--app-dir", appDir);
        AppendArg(sb, "--restart", restartPath);
        AppendArg(sb, "--version-tag", versionTag);
        AppendArg(sb, "--wait-timeout", waitTimeoutSeconds.ToString());
        if (!string.IsNullOrWhiteSpace(downloadZip))
            AppendArg(sb, "--download-zip", downloadZip);
        return sb.ToString();
    }

    /// <summary>
    /// Copies the updater from the extracted package to a temp run folder (so the update
    /// directory can be deleted), then starts it.
    /// </summary>
    public static Process StartFromUpdatePackage(
        string tempUpdateFolder,
        int waitPid,
        string appDir,
        string restartPath,
        string versionTag,
        string? downloadZip = null,
        int waitTimeoutSeconds = LauncherUpdateApplier.DefaultWaitTimeoutSeconds)
    {
        var packageUpdater = Path.Combine(tempUpdateFolder, LauncherUpdateApplier.WindowsUpdaterFileName);
        if (!File.Exists(packageUpdater))
        {
            throw new FileNotFoundException(
                $"Update package is missing {LauncherUpdateApplier.WindowsUpdaterFileName}.",
                packageUpdater);
        }

        var runDir = Path.Combine(Path.GetTempPath(), "Quiver_Updater_Run_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runDir);
        var runUpdater = Path.Combine(runDir, LauncherUpdateApplier.WindowsUpdaterFileName);
        File.Copy(packageUpdater, runUpdater, overwrite: true);

        var args = BuildUpdaterArguments(
            waitPid,
            tempUpdateFolder,
            appDir,
            restartPath,
            versionTag,
            downloadZip,
            waitTimeoutSeconds);

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = runUpdater,
            Arguments = args,
            WorkingDirectory = runDir,
            UseShellExecute = false,
            CreateNoWindow = false,
        });

        if (process == null)
            throw new InvalidOperationException("Failed to start Quiver.Updater.exe.");

        return process;
    }

    private static void AppendArg(StringBuilder sb, string key, string value)
    {
        if (sb.Length > 0)
            sb.Append(' ');
        sb.Append(key);
        sb.Append(' ');
        sb.Append('"');
        sb.Append(value.Replace("\"", "\\\""));
        sb.Append('"');
    }
}
