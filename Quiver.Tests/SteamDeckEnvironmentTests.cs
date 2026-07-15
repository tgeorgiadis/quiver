using FluentAssertions;
using Quiver.Services;

namespace Quiver.Tests;

public class SteamDeckEnvironmentTests
{
    [Fact]
    public void IsGamingMode_false_when_not_linux()
    {
        SteamDeckEnvironment.IsGamingMode(
            isLinux: false,
            _ => "1").Should().BeFalse();
    }

    [Theory]
    [InlineData("SteamGamepadUI")]
    [InlineData("SteamOS")]
    public void IsGamingMode_true_when_gaming_mode_env_set(string envName)
    {
        SteamDeckEnvironment.IsGamingMode(
            isLinux: true,
            name => name == envName ? "1" : null).Should().BeTrue();
    }

    [Fact]
    public void IsGamingMode_true_when_gamescope_desktop()
    {
        SteamDeckEnvironment.IsGamingMode(
            isLinux: true,
            name => name == "XDG_CURRENT_DESKTOP" ? "gamescope" : null).Should().BeTrue();
    }

    [Fact]
    public void IsGamingMode_false_when_only_SteamDeck_set()
    {
        SteamDeckEnvironment.IsGamingMode(
            isLinux: true,
            name => name == "SteamDeck" ? "1" : null).Should().BeFalse();
    }

    [Fact]
    public void IsGamingMode_false_when_linux_without_gaming_mode_env()
    {
        SteamDeckEnvironment.IsGamingMode(
            isLinux: true,
            _ => null).Should().BeFalse();
    }
}
