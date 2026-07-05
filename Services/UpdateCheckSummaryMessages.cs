namespace Quiver.Services;

public static class UpdateCheckSummaryMessages
{
    public static string BuildManualCheckSummary(
        ManualLauncherCheckResult launcher,
        int appUpdatesPending)
    {
        var displayVersion = FormatVersion(launcher.InstalledVersion);
        var allClear = launcher.CheckSucceeded
                       && !launcher.LauncherUpdatePending
                       && appUpdatesPending == 0;

        if (allClear)
            return $"Quiver and all apps are up to date.\n\nQuiver v{displayVersion}";

        var lines = new List<string>();

        if (launcher.CheckSucceeded)
        {
            if (launcher.LauncherUpdatePending &&
                !string.IsNullOrWhiteSpace(launcher.AvailableLauncherVersion))
            {
                lines.Add(
                    $"Quiver update to {FormatVersion(launcher.AvailableLauncherVersion)} available.");
            }
            else
            {
                lines.Add($"Quiver v{displayVersion} is up to date.");
            }
        }
        else
        {
            var error = string.IsNullOrWhiteSpace(launcher.ErrorMessage)
                ? "check failed"
                : launcher.ErrorMessage;
            lines.Add($"Quiver: could not check ({error}).");
        }

        if (appUpdatesPending > 0)
        {
            lines.Add(appUpdatesPending == 1
                ? "1 app update available."
                : $"{appUpdatesPending} app updates available.");
        }
        else
        {
            lines.Add("All apps are up to date.");
        }

        return "Update check complete.\n\n" + string.Join('\n', lines);
    }

    internal static string FormatVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return "unknown";

        var trimmed = version.Trim();
        return trimmed.StartsWith('v') || trimmed.StartsWith('V')
            ? trimmed.TrimStart('v', 'V')
            : trimmed;
    }
}
