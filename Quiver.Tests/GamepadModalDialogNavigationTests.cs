using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Quiver.Services;

namespace Quiver.Tests;

public class GamepadModalDialogNavigationTests
{
    [Fact]
    public void MoveButtonIndex_moves_right_between_horizontal_buttons()
    {
        var positions = new List<(double X, double Y)>
        {
            (0, 0),
            (100, 0),
        };

        GamepadModalDialogNavigation.MoveButtonIndex(0, NavigationDirection.Right, positions).Should().Be(1);
        GamepadModalDialogNavigation.MoveButtonIndex(1, NavigationDirection.Left, positions).Should().Be(0);
    }

    [Fact]
    public void MoveButtonIndex_wraps_down_from_bottom_row_to_top_row()
    {
        var positions = new List<(double X, double Y)>
        {
            (0, 0),
            (100, 0),
            (0, 100),
            (100, 100),
        };

        GamepadModalDialogNavigation.MoveButtonIndex(2, NavigationDirection.Down, positions).Should().Be(0);
        GamepadModalDialogNavigation.MoveButtonIndex(3, NavigationDirection.Down, positions).Should().Be(1);
    }

    [Fact]
    public void MoveButtonIndex_wraps_up_from_top_row_to_bottom_row()
    {
        var positions = new List<(double X, double Y)>
        {
            (0, 0),
            (100, 0),
            (0, 100),
            (100, 100),
        };

        GamepadModalDialogNavigation.MoveButtonIndex(0, NavigationDirection.Up, positions).Should().Be(2);
        GamepadModalDialogNavigation.MoveButtonIndex(1, NavigationDirection.Up, positions).Should().Be(3);
    }

    [AvaloniaFact]
    public void GetDefaultButtonIndex_prefers_ok_and_yes()
    {
        var buttons = new List<Button>
        {
            new() { Content = "No" },
            new() { Content = "Yes" },
        };

        GamepadModalDialogNavigation.GetDefaultButtonIndex(buttons).Should().Be(1);

        var okButtons = new List<Button>
        {
            new() { Content = "OK" },
        };

        GamepadModalDialogNavigation.GetDefaultButtonIndex(okButtons).Should().Be(0);
    }

    [AvaloniaFact]
    public void FindCancelButtonIndex_prefers_cancel_and_no()
    {
        var yesNo = new List<Button>
        {
            new() { Content = "Yes" },
            new() { Content = "No" },
        };

        GamepadModalDialogNavigation.FindCancelButtonIndex(yesNo).Should().Be(1);

        var okCancel = new List<Button>
        {
            new() { Content = "Cancel" },
            new() { Content = "OK" },
        };

        GamepadModalDialogNavigation.FindCancelButtonIndex(okCancel).Should().Be(0);
    }

    [Fact]
    public void MoveButtonIndex_keeps_single_button_index()
    {
        var positions = new List<(double X, double Y)> { (0, 0) };

        GamepadModalDialogNavigation.MoveButtonIndex(0, NavigationDirection.Right, positions).Should().Be(0);
        GamepadModalDialogNavigation.MoveButtonIndex(0, NavigationDirection.Down, positions).Should().Be(0);
    }

    [AvaloniaFact]
    public void Attach_sets_active_dialog_before_opened_event()
    {
        var dialog = new Window
        {
            Content = new Button { Content = "OK" },
        };

        try
        {
            GamepadModalDialogNavigation.Instance.UnregisterModalDialog(dialog);
            GamepadModalDialogNavigation.Instance.HasActiveDialog.Should().BeFalse();

            GamepadModalDialogNavigation.Attach(dialog);

            GamepadModalDialogNavigation.Instance.HasActiveDialog.Should().BeTrue();
        }
        finally
        {
            GamepadModalDialogNavigation.Instance.UnregisterModalDialog(dialog);
        }
    }

    [AvaloniaFact]
    public void TryHandleConfirm_closes_ok_dialog()
    {
        var dialog = new Window
        {
            Width = 420,
            Height = 200,
            Content = new StackPanel
            {
                Children = { new Button { Content = "OK" } },
            },
        };

        var closed = false;
        dialog.Closed += (_, _) => closed = true;

        try
        {
            dialog.Show();
            GamepadModalDialogNavigation.Attach(dialog);
            GamepadModalDialogNavigation.Instance.RefreshDialogButtons();

            GamepadModalDialogNavigation.Instance.TryHandleConfirm().Should().BeTrue();
            closed.Should().BeTrue();
            dialog.IsVisible.Should().BeFalse();
        }
        finally
        {
            GamepadModalDialogNavigation.Instance.UnregisterModalDialog(dialog);
            if (dialog.IsVisible)
                dialog.Close();
        }
    }
}
