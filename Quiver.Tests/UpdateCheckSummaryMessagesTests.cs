using FluentAssertions;
using Quiver.Services;

namespace Quiver.Tests;

public class UpdateCheckSummaryMessagesTests
{
    private static ManualLauncherCheckResult UpToDateLauncher(string version = "v2.2.0") =>
        new()
        {
            CheckSucceeded = true,
            InstalledVersion = version,
            LauncherUpdatePending = false,
        };

    [Fact]
    public void BuildManualCheckSummary_all_clear()
    {
        var summary = UpdateCheckSummaryMessages.BuildManualCheckSummary(
            UpToDateLauncher(),
            appUpdatesPending: 0);

        summary.Should().Be("Quiver and all apps are up to date.\n\nQuiver v2.2.0");
    }

    [Fact]
    public void BuildManualCheckSummary_apps_only()
    {
        var summary = UpdateCheckSummaryMessages.BuildManualCheckSummary(
            UpToDateLauncher(),
            appUpdatesPending: 3);

        summary.Should().Be(
            "Update check complete.\n\nQuiver v2.2.0 is up to date.\n3 app updates available.");
    }

    [Fact]
    public void BuildManualCheckSummary_launcher_only()
    {
        var summary = UpdateCheckSummaryMessages.BuildManualCheckSummary(
            new ManualLauncherCheckResult
            {
                CheckSucceeded = true,
                InstalledVersion = "v2.2.0",
                LauncherUpdatePending = true,
                AvailableLauncherVersion = "v2.3.0",
            },
            appUpdatesPending: 0);

        summary.Should().Be(
            "Update check complete.\n\nQuiver update to 2.3.0 available.\nAll apps are up to date.");
    }

    [Fact]
    public void BuildManualCheckSummary_launcher_and_apps()
    {
        var summary = UpdateCheckSummaryMessages.BuildManualCheckSummary(
            new ManualLauncherCheckResult
            {
                CheckSucceeded = true,
                InstalledVersion = "v2.2.0",
                LauncherUpdatePending = true,
                AvailableLauncherVersion = "v2.3.0",
            },
            appUpdatesPending: 2);

        summary.Should().Be(
            "Update check complete.\n\nQuiver update to 2.3.0 available.\n2 app updates available.");
    }

    [Fact]
    public void BuildManualCheckSummary_launcher_check_failed_with_apps_pending()
    {
        var summary = UpdateCheckSummaryMessages.BuildManualCheckSummary(
            new ManualLauncherCheckResult
            {
                CheckSucceeded = false,
                InstalledVersion = "v2.2.0",
                ErrorMessage = "network error",
            },
            appUpdatesPending: 1);

        summary.Should().Be(
            "Update check complete.\n\nQuiver: could not check (network error).\n1 app update available.");
    }

    [Fact]
    public void BuildManualCheckSummary_single_app_update_uses_singular()
    {
        var summary = UpdateCheckSummaryMessages.BuildManualCheckSummary(
            UpToDateLauncher(),
            appUpdatesPending: 1);

        summary.Should().Contain("1 app update available.");
        summary.Should().NotContain("1 app updates");
    }
}
