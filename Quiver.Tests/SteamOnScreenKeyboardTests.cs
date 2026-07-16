using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Quiver.Services;

namespace Quiver.Tests;

public class SteamOnScreenKeyboardTests
{
    [Fact]
    public void ShouldOffer_false_when_not_linux()
    {
        SteamOnScreenKeyboard.ShouldOffer(
            isLinux: false,
            _ => "1").Should().BeFalse();
    }

    [Theory]
    [InlineData("SteamDeck")]
    [InlineData("SteamAppId")]
    [InlineData("SteamGameId")]
    [InlineData("SteamOverlayGameId")]
    public void ShouldOffer_true_when_steam_env_set(string envName)
    {
        SteamOnScreenKeyboard.ShouldOffer(
            isLinux: true,
            name => name == envName ? "1" : null).Should().BeTrue();
    }

    [Fact]
    public void ShouldOffer_true_when_gamescope_desktop()
    {
        SteamOnScreenKeyboard.ShouldOffer(
            isLinux: true,
            name => name == "XDG_CURRENT_DESKTOP" ? "gamescope" : null).Should().BeTrue();
    }

    [Fact]
    public void ShouldOffer_false_when_linux_without_steam_or_gamescope()
    {
        SteamOnScreenKeyboard.ShouldOffer(
            isLinux: true,
            _ => null).Should().BeFalse();
    }

    [Fact]
    public void TryOpen_invokes_steam_keyboard_uri_when_offered()
    {
        string? opened = null;

        var attempted = SteamOnScreenKeyboard.TryOpen(
            shouldOffer: true,
            openUri: uri => opened = uri);

        attempted.Should().BeTrue();
        opened.Should().Be(SteamOnScreenKeyboard.OpenKeyboardUri);
    }

    [Fact]
    public void TryOpen_noop_when_not_offered()
    {
        string? opened = null;

        var attempted = SteamOnScreenKeyboard.TryOpen(
            shouldOffer: false,
            openUri: uri => opened = uri);

        attempted.Should().BeFalse();
        opened.Should().BeNull();
    }

    [Fact]
    public void TryOpen_returns_false_when_openUri_throws()
    {
        var attempted = SteamOnScreenKeyboard.TryOpen(
            shouldOffer: true,
            openUri: _ => throw new InvalidOperationException("no steam"));

        attempted.Should().BeFalse();
    }

    [AvaloniaFact]
    public void ActivateTextBox_does_not_throw_for_enabled_textbox()
    {
        var box = new TextBox
        {
            IsEnabled = true,
            IsVisible = true,
        };

        var act = () => GamepadControlActivation.ActivateTextBox(box);

        act.Should().NotThrow();
    }

    [AvaloniaFact]
    public void ActivateTextBox_moves_caret_to_end()
    {
        var box = new TextBox
        {
            IsEnabled = true,
            IsVisible = true,
            Text = "n64, recomp",
            CaretIndex = 0,
        };

        GamepadControlActivation.ActivateTextBox(box);

        box.CaretIndex.Should().Be(box.Text!.Length);
        box.SelectionStart.Should().Be(box.Text.Length);
        box.SelectionEnd.Should().Be(box.Text.Length);
    }

    [AvaloniaFact]
    public void MoveCaretToEnd_sets_caret_after_existing_text()
    {
        var box = new TextBox { Text = "hello" };
        box.CaretIndex = 0;

        GamepadControlActivation.MoveCaretToEnd(box);

        box.CaretIndex.Should().Be(5);
    }
}
