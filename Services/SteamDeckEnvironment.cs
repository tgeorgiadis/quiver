namespace Quiver.Services;

/// <summary>
/// Detects Steam Deck / SteamOS Gaming Mode (Gamescope) sessions.
/// Distinct from <see cref="SteamOnScreenKeyboard.ShouldOffer"/>, which also
/// matches Desktop Mode where <c>SteamDeck=1</c> is set.
/// </summary>
internal static class SteamDeckEnvironment
{
    public static bool IsGamingMode() =>
        IsGamingMode(OperatingSystem.IsLinux(), Environment.GetEnvironmentVariable);

    public static bool IsGamingMode(bool isLinux, Func<string, string?> getEnvironmentVariable)
    {
        if (!isLinux)
            return false;

        if (!string.IsNullOrWhiteSpace(getEnvironmentVariable("SteamGamepadUI")) ||
            !string.IsNullOrWhiteSpace(getEnvironmentVariable("SteamOS")))
        {
            return true;
        }

        var desktop = getEnvironmentVariable("XDG_CURRENT_DESKTOP");
        return string.Equals(desktop, "gamescope", StringComparison.OrdinalIgnoreCase);
    }
}
