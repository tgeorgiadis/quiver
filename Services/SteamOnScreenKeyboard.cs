using System.Diagnostics;

namespace Quiver.Services;

/// <summary>
/// Opens Steam's on-screen keyboard on Steam Deck / Gamescope.
/// Avalonia X11 TextBoxes do not participate in Steam's text-input path, so the OSK
/// must be requested explicitly (see Valve gamescope#668).
/// </summary>
internal static class SteamOnScreenKeyboard
{
    public const string OpenKeyboardUri = "steam://open/keyboard";

    public static bool ShouldOffer() =>
        ShouldOffer(OperatingSystem.IsLinux(), Environment.GetEnvironmentVariable);

    public static bool ShouldOffer(bool isLinux, Func<string, string?> getEnvironmentVariable)
    {
        if (!isLinux)
            return false;

        if (!string.IsNullOrWhiteSpace(getEnvironmentVariable("SteamGameId")) ||
            !string.IsNullOrWhiteSpace(getEnvironmentVariable("SteamAppId")) ||
            !string.IsNullOrWhiteSpace(getEnvironmentVariable("SteamOverlayGameId")) ||
            !string.IsNullOrWhiteSpace(getEnvironmentVariable("SteamDeck")))
        {
            return true;
        }

        var desktop = getEnvironmentVariable("XDG_CURRENT_DESKTOP");
        return string.Equals(desktop, "gamescope", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Requests Steam's OSK when running under Steam/Gamescope on Linux.
    /// Returns true if an open was attempted.
    /// </summary>
    public static bool TryOpen(Action<string>? openUri = null) =>
        TryOpen(ShouldOffer(), openUri);

    public static bool TryOpen(bool shouldOffer, Action<string>? openUri)
    {
        if (!shouldOffer)
            return false;

        try
        {
            (openUri ?? OpenUriWithShell)(OpenKeyboardUri);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Steam OSK open failed: {ex.Message}");
            return false;
        }
    }

    private static void OpenUriWithShell(string uri)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = uri,
            UseShellExecute = true,
        });
    }
}
