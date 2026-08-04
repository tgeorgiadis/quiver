using Quiver.Core.Services;

namespace Quiver.Updater;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (!TryParseArgs(args, out var options, out var error))
            {
                Console.Error.WriteLine(error);
                PrintUsage();
                return 1;
            }

            return LauncherUpdateApplier.Run(options);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Updater failed: {ex.Message}");
            return 1;
        }
    }

    internal static bool TryParseArgs(string[] args, out LauncherUpdateApplier.Options options, out string error)
    {
        options = null!;
        error = string.Empty;

        int? waitPid = null;
        string? updateDir = null;
        string? appDir = null;
        string? restart = null;
        string? versionTag = null;
        string? downloadZip = null;
        var timeout = LauncherUpdateApplier.DefaultWaitTimeoutSeconds;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unexpected argument: {arg}";
                return false;
            }

            string? value = null;
            var eq = arg.IndexOf('=');
            string key;
            if (eq > 0)
            {
                key = arg[..eq];
                value = arg[(eq + 1)..];
            }
            else
            {
                key = arg;
                if (i + 1 >= args.Length)
                {
                    error = $"Missing value for {key}";
                    return false;
                }

                value = args[++i];
            }

            switch (key)
            {
                case "--wait-pid":
                    if (!int.TryParse(value, out var pid))
                    {
                        error = $"Invalid --wait-pid value: {value}";
                        return false;
                    }

                    waitPid = pid;
                    break;
                case "--update-dir":
                    updateDir = value;
                    break;
                case "--app-dir":
                    appDir = value;
                    break;
                case "--restart":
                    restart = value;
                    break;
                case "--version-tag":
                    versionTag = value;
                    break;
                case "--download-zip":
                    downloadZip = value;
                    break;
                case "--wait-timeout":
                    if (!int.TryParse(value, out timeout) || timeout < 1)
                    {
                        error = $"Invalid --wait-timeout value: {value}";
                        return false;
                    }

                    break;
                default:
                    error = $"Unknown argument: {key}";
                    return false;
            }
        }

        if (waitPid is null)
        {
            error = "Missing required --wait-pid";
            return false;
        }

        if (string.IsNullOrWhiteSpace(updateDir))
        {
            error = "Missing required --update-dir";
            return false;
        }

        if (string.IsNullOrWhiteSpace(appDir))
        {
            error = "Missing required --app-dir";
            return false;
        }

        if (string.IsNullOrWhiteSpace(restart))
        {
            error = "Missing required --restart";
            return false;
        }

        if (string.IsNullOrWhiteSpace(versionTag))
        {
            error = "Missing required --version-tag";
            return false;
        }

        options = new LauncherUpdateApplier.Options(
            WaitPid: waitPid.Value,
            UpdateDir: updateDir,
            AppDir: appDir,
            RestartPath: restart,
            VersionTag: versionTag,
            DownloadZip: downloadZip,
            WaitTimeoutSeconds: timeout);

        return true;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine(
            "Usage: Quiver.Updater --wait-pid <pid> --update-dir <path> --app-dir <path> --restart <exe> --version-tag <tag> [--download-zip <path>] [--wait-timeout <seconds>]");
    }
}
