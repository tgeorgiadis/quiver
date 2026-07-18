using FluentAssertions;
using Quiver.Services;

namespace Quiver.Tests;

public class GamepadFocusChromeTests
{
    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, true, true)]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, true, true)]
    public void ShouldShowGamepadChrome_requires_enabled_and_pad_or_keyboard(
        bool enableGamepadInput,
        bool hasConnectedGamepad,
        bool keyboardNavActive,
        bool expected)
    {
        GamepadFocusChrome.ShouldShowGamepadChrome(
                enableGamepadInput,
                hasConnectedGamepad,
                keyboardNavActive)
            .Should().Be(expected);
    }
}
