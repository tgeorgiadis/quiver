using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Quiver.Services;

namespace Quiver.Tests;

public class GamepadControlActivationTests
{
    [AvaloniaFact]
    public void ActivateButton_raises_click_once()
    {
        var clickCount = 0;
        var button = new Button
        {
            Content = "Check Updates",
            IsEnabled = true,
            IsVisible = true,
        };
        button.Click += (_, _) => clickCount++;

        GamepadControlActivation.ActivateButton(button);

        clickCount.Should().Be(1);
    }

    [AvaloniaFact]
    public void ActivateDialogButton_raises_click_multiple_times()
    {
        var clickCount = 0;
        var button = new Button
        {
            Content = "OK",
            IsEnabled = true,
            IsVisible = true,
        };
        button.Click += (_, _) => clickCount++;

        GamepadControlActivation.ActivateDialogButton(button);

        clickCount.Should().BeGreaterThanOrEqualTo(3);
    }
}
