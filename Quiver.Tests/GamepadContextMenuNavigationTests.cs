using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Quiver.Services;

namespace Quiver.Tests;

public class GamepadContextMenuNavigationTests
{
    [AvaloniaFact]
    public void Attach_registers_menu_before_opened_event()
    {
        var menu = new ContextMenu
        {
            Items = { new MenuItem { Header = "Option A" } },
        };

        try
        {
            GamepadContextMenuNavigation.Instance.UnregisterContextMenu(menu);
            GamepadContextMenuNavigation.Instance.HasActiveContextMenu.Should().BeFalse();

            GamepadContextMenuNavigation.Attach(menu);

            GamepadContextMenuNavigation.Instance.HasActiveContextMenu.Should().BeTrue();
        }
        finally
        {
            GamepadContextMenuNavigation.Instance.UnregisterContextMenu(menu);
        }
    }

    [Fact]
    public void MoveMenuItemIndex_moves_down_and_wraps()
    {
        GamepadContextMenuNavigation.MoveMenuItemIndex(0, NavigationDirection.Down, 3).Should().Be(1);
        GamepadContextMenuNavigation.MoveMenuItemIndex(2, NavigationDirection.Down, 3).Should().Be(0);
    }

    [Fact]
    public void MoveMenuItemIndex_moves_up_and_wraps()
    {
        GamepadContextMenuNavigation.MoveMenuItemIndex(0, NavigationDirection.Up, 3).Should().Be(2);
        GamepadContextMenuNavigation.MoveMenuItemIndex(1, NavigationDirection.Up, 3).Should().Be(0);
    }

    [Fact]
    public void MoveMenuItemIndex_treats_left_right_like_vertical_navigation()
    {
        GamepadContextMenuNavigation.MoveMenuItemIndex(0, NavigationDirection.Right, 2).Should().Be(1);
        GamepadContextMenuNavigation.MoveMenuItemIndex(1, NavigationDirection.Left, 2).Should().Be(0);
    }

    [AvaloniaFact]
    public void CollectNavigableMenuItems_skips_disabled_headers()
    {
        var menu = new ContextMenu
        {
            Items =
            {
                new MenuItem { Header = "Select executable:", IsEnabled = false },
                new Separator(),
                new MenuItem { Header = "game.exe" },
                new MenuItem { Header = "launcher.exe" },
                new MenuItem { Header = "Cancel" },
            },
        };

        var items = GamepadContextMenuNavigation.CollectNavigableMenuItems(menu);

        items.Should().HaveCount(3);
        items[0].Header.Should().Be("game.exe");
        items[2].Header.Should().Be("Cancel");
    }

    [AvaloniaFact]
    public void FindCancelMenuItemIndex_finds_cancel_option()
    {
        var items = new List<MenuItem>
        {
            new() { Header = "game.exe" },
            new() { Header = "Cancel" },
        };

        GamepadContextMenuNavigation.FindCancelMenuItemIndex(items).Should().Be(1);
    }

    [Fact]
    public void MoveMenuItemIndex_keeps_single_item_index()
    {
        GamepadContextMenuNavigation.MoveMenuItemIndex(0, NavigationDirection.Down, 1).Should().Be(0);
        GamepadContextMenuNavigation.MoveMenuItemIndex(0, NavigationDirection.Up, 1).Should().Be(0);
    }
}
