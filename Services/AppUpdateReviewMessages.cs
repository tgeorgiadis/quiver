using Quiver.Models;

namespace Quiver.Services;

public static class AppUpdateReviewMessages
{
    public static string FormatQuiverOnlyUpdateMessage(string? launcherVersion)
    {
        var version = FormatLauncherVersion(launcherVersion);
        return $"Quiver update {version} is available.\n\nUpdate Quiver now?";
    }

    public static string FormatCombinedUpdatesMessage(
        string? launcherVersion,
        IReadOnlyList<GameInfo> games)
    {
        var version = FormatLauncherVersion(launcherVersion);
        var ordered = games
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var appHeader = ordered.Count == 1
            ? "1 app update is available:"
            : $"{ordered.Count} app updates are available:";

        var appLines = ordered.Select(FormatGameUpdateLine);
        return
            $"Quiver update {version} is available.\n\n" +
            appHeader + "\n\n" +
            string.Join('\n', appLines) +
            "\n\nWhat would you like to update?";
    }

    public static string FormatPendingAppUpdatesMessage(
        IReadOnlyList<GameInfo> games,
        bool includeOpenPrompt)
    {
        var ordered = games
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var header = ordered.Count == 1
            ? "1 app update is available:"
            : $"{ordered.Count} app updates are available:";

        var lines = ordered.Select(FormatGameUpdateLine);
        var body = header + "\n\n" + string.Join('\n', lines);

        if (!includeOpenPrompt)
            return body;

        var prompt = ordered.Count == 1
            ? "Review this update now?"
            : "Review these updates now?";
        return body + "\n\n" + prompt;
    }

    internal static string FormatGameUpdateLine(GameInfo game)
    {
        var name = string.IsNullOrWhiteSpace(game.Name) ? "Unknown app" : game.Name.Trim();
        var installed = string.IsNullOrWhiteSpace(game.InstalledVersion) ? "?" : game.InstalledVersion.Trim();
        var latest = string.IsNullOrWhiteSpace(game.LatestVersion) ? "?" : game.LatestVersion.Trim();
        return $"• {name} ({installed} → {latest})";
    }

    private static string FormatLauncherVersion(string? launcherVersion)
    {
        if (string.IsNullOrWhiteSpace(launcherVersion))
            return "unknown";

        var trimmed = launcherVersion.Trim();
        return trimmed.StartsWith('v') || trimmed.StartsWith('V')
            ? trimmed
            : $"v{trimmed}";
    }
}
