using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Quiver.Services;

public sealed class GamepadContextMenuNavigation
{
    private static GamepadContextMenuNavigation? _instance;

    private ContextMenu? _activeMenu;
    private List<MenuItem> _menuItems = [];
    private int _focusedItemIndex;

    public static GamepadContextMenuNavigation Instance => _instance ??= new GamepadContextMenuNavigation();

    public bool HasActiveContextMenu => _activeMenu != null;

    public static void Attach(ContextMenu menu)
    {
        Instance.PrepareMenu(menu);
    }

    public void PrepareMenu(ContextMenu menu)
    {
        RegisterContextMenu(menu);

        menu.Opened += OnMenuOpened;
        menu.Closed += OnMenuClosed;

        void OnMenuOpened(object? sender, EventArgs e)
        {
            RefreshContextMenu(menu);
        }

        void OnMenuClosed(object? sender, EventArgs e)
        {
            menu.Opened -= OnMenuOpened;
            menu.Closed -= OnMenuClosed;
            UnregisterContextMenu(menu);
        }
    }

    public void RegisterContextMenu(ContextMenu menu)
    {
        RefreshContextMenu(menu);
    }

    public void RefreshContextMenu(ContextMenu menu)
    {
        if (!ReferenceEquals(_activeMenu, menu))
            _activeMenu = menu;

        _menuItems = CollectNavigableMenuItems(menu);
        _focusedItemIndex = _menuItems.Count > 0 ? 0 : -1;

        Dispatcher.UIThread.Post(FocusCurrentItem, DispatcherPriority.Loaded);
    }

    public void UnregisterContextMenu(ContextMenu menu)
    {
        if (!ReferenceEquals(_activeMenu, menu))
            return;

        _activeMenu = null;
        _menuItems = [];
        _focusedItemIndex = -1;
    }

    public bool TryHandleNavigation(NavigationDirection direction)
    {
        if (_activeMenu == null || _menuItems.Count == 0)
            return false;

        if (_menuItems.Count == 1)
            return true;

        _focusedItemIndex = MoveMenuItemIndex(_focusedItemIndex, direction, _menuItems.Count);
        FocusCurrentItem();
        return true;
    }

    public bool TryHandleConfirm()
    {
        if (_activeMenu == null || _menuItems.Count == 0)
            return false;

        var item = GetFocusedItem();
        if (item == null)
            return false;

        GamepadControlActivation.ActivateMenuItem(item);
        return true;
    }

    public bool TryHandleCancel()
    {
        if (_activeMenu == null)
            return false;

        EnsureMenuItems();

        var cancelIndex = FindCancelMenuItemIndex(_menuItems);
        if (cancelIndex >= 0)
        {
            GamepadControlActivation.ActivateMenuItem(_menuItems[cancelIndex]);
            return true;
        }

        _activeMenu.Close();
        return true;
    }

    private void EnsureMenuItems()
    {
        if (_activeMenu == null || _menuItems.Count > 0)
            return;

        _menuItems = CollectNavigableMenuItems(_activeMenu);
        _focusedItemIndex = _menuItems.Count > 0 ? 0 : -1;
    }

    public static List<MenuItem> CollectNavigableMenuItems(ContextMenu menu)
    {
        return menu.Items
            .OfType<MenuItem>()
            .Where(item => item.IsVisible && item.IsEnabled)
            .ToList();
    }

    public static int MoveMenuItemIndex(int currentIndex, NavigationDirection direction, int count)
    {
        if (count <= 0)
            return -1;

        if (currentIndex < 0 || currentIndex >= count)
            return 0;

        if (count == 1)
            return currentIndex;

        return direction switch
        {
            NavigationDirection.Down or NavigationDirection.Right => (currentIndex + 1) % count,
            NavigationDirection.Up or NavigationDirection.Left => (currentIndex - 1 + count) % count,
            _ => currentIndex,
        };
    }

    public static int FindCancelMenuItemIndex(IReadOnlyList<MenuItem> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var label = GetMenuItemLabel(items[i]);
            if (string.Equals(label, "cancel", StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private void FocusCurrentItem()
    {
        GetFocusedItem()?.Focus();
    }

    private MenuItem? GetFocusedItem()
    {
        if (_focusedItemIndex < 0 || _focusedItemIndex >= _menuItems.Count)
            return null;

        return _menuItems[_focusedItemIndex];
    }

    private static string GetMenuItemLabel(MenuItem item)
    {
        return item.Header?.ToString()?.Trim() ?? string.Empty;
    }
}
