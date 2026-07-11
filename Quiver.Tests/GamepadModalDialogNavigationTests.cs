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

    [AvaloniaFact]
    public void Attach_stacks_dialogs_and_unregister_restores_previous()
    {
        var dialogA = new Window { Content = new Button { Content = "Yes" } };
        var dialogB = new Window { Content = new Button { Content = "Update Quiver" } };
        var nav = GamepadModalDialogNavigation.Instance;

        try
        {
            nav.UnregisterModalDialog(dialogA);
            nav.UnregisterModalDialog(dialogB);

            GamepadModalDialogNavigation.Attach(dialogA);
            nav.HasActiveDialog.Should().BeTrue();
            nav.ActiveDialog.Should().BeSameAs(dialogA);
            nav.DialogStackCount.Should().Be(1);

            GamepadModalDialogNavigation.Attach(dialogB);
            nav.ActiveDialog.Should().BeSameAs(dialogB);
            nav.DialogStackCount.Should().Be(2);

            nav.UnregisterModalDialog(dialogB);
            nav.HasActiveDialog.Should().BeTrue();
            nav.ActiveDialog.Should().BeSameAs(dialogA);
            nav.DialogStackCount.Should().Be(1);

            nav.UnregisterModalDialog(dialogA);
            nav.HasActiveDialog.Should().BeFalse();
            nav.ActiveDialog.Should().BeNull();
            nav.DialogStackCount.Should().Be(0);
        }
        finally
        {
            nav.UnregisterModalDialog(dialogB);
            nav.UnregisterModalDialog(dialogA);
        }
    }

    [AvaloniaFact]
    public void Unregister_top_dialog_keeps_has_active_and_restores_previous()
    {
        var dialogA = new Window { Content = new Button { Content = "Yes" } };
        var dialogB = new Window
        {
            Content = new StackPanel
            {
                Children =
                {
                    new Button { Content = "Update Quiver" },
                    new Button { Content = "Not now" },
                },
            },
        };
        var nav = GamepadModalDialogNavigation.Instance;

        try
        {
            GamepadModalDialogNavigation.Attach(dialogA);
            GamepadModalDialogNavigation.Attach(dialogB);
            nav.DialogStackCount.Should().Be(2);

            nav.UnregisterModalDialog(dialogB);

            nav.HasActiveDialog.Should().BeTrue();
            nav.ActiveDialog.Should().BeSameAs(dialogA);
            nav.DialogStackCount.Should().Be(1);

            var act = () => nav.RefreshDialogButtons();
            act.Should().NotThrow();
        }
        finally
        {
            nav.UnregisterModalDialog(dialogB);
            nav.UnregisterModalDialog(dialogA);
        }
    }

    [AvaloniaFact]
    public void TryHandleConfirm_on_yes_invokes_question_result_callback_true()
    {
        bool? callbackResult = null;
        var yesButton = new Button { Content = "Yes", MinWidth = 80 };
        var noButton = new Button { Content = "No", MinWidth = 80 };
        var dialog = new Window
        {
            Width = 420,
            Height = 200,
            Content = new StackPanel
            {
                Children = { yesButton, noButton },
            },
        };

        yesButton.Click += (_, _) => dialog.Close();
        noButton.Click += (_, _) => dialog.Close();

        var nav = GamepadModalDialogNavigation.Instance;

        try
        {
            dialog.Show();
            GamepadModalDialogNavigation.Attach(dialog, accepted => callbackResult = accepted);
            nav.RefreshDialogButtons();

            nav.TryHandleConfirm().Should().BeTrue();
            callbackResult.Should().Be(true);
            dialog.IsVisible.Should().BeFalse();
        }
        finally
        {
            nav.UnregisterModalDialog(dialog);
            if (dialog.IsVisible)
                dialog.Close();
        }
    }

    [AvaloniaFact]
    public void TryHandleCancel_invokes_question_result_callback_false()
    {
        bool? callbackResult = null;
        var yesButton = new Button { Content = "Yes", MinWidth = 80 };
        var noButton = new Button { Content = "No", MinWidth = 80 };
        var dialog = new Window
        {
            Width = 420,
            Height = 200,
            Content = new StackPanel
            {
                Children = { yesButton, noButton },
            },
        };

        yesButton.Click += (_, _) => dialog.Close();
        noButton.Click += (_, _) => dialog.Close();

        var nav = GamepadModalDialogNavigation.Instance;

        try
        {
            dialog.Show();
            GamepadModalDialogNavigation.Attach(dialog, accepted => callbackResult = accepted);
            nav.RefreshDialogButtons();

            nav.TryHandleCancel().Should().BeTrue();
            callbackResult.Should().Be(false);
            dialog.IsVisible.Should().BeFalse();
        }
        finally
        {
            nav.UnregisterModalDialog(dialog);
            if (dialog.IsVisible)
                dialog.Close();
        }
    }

    [AvaloniaFact]
    public void ApplyDialogResultHint_sets_tag_for_yes_and_no()
    {
        var dialog = new Window();
        GamepadModalDialogNavigation.ApplyDialogResultHint(dialog, new Button { Content = "Yes" });
        dialog.Tag.Should().Be(true);

        GamepadModalDialogNavigation.ApplyDialogResultHint(dialog, new Button { Content = "No" });
        dialog.Tag.Should().Be(false);

        GamepadModalDialogNavigation.ApplyDialogResultHint(dialog, new Button { Content = "Update Quiver" });
        dialog.Tag.Should().Be(true);

        GamepadModalDialogNavigation.ApplyDialogResultHint(dialog, new Button { Content = "Not now" });
        dialog.Tag.Should().Be(false);
    }
}
