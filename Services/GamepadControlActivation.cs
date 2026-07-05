using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Quiver.Services;

internal static class GamepadControlActivation
{
    public static void ActivateButton(Button button)
    {
        if (!button.IsEnabled || !button.IsVisible)
            return;

        button.Focus();
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, RoutingStrategies.Bubble) { Source = button });
    }

    public static void ActivateDialogButton(Button button, Window? closeFallbackDialog = null)
    {
        if (!button.IsEnabled || !button.IsVisible)
            return;

        button.Focus();

        RaiseClickEvent(button, Button.ClickEvent);

        if (button.Command?.CanExecute(null) == true)
            button.Command.Execute(null);

        SimulateActivationKeys(button);

        if (closeFallbackDialog != null && closeFallbackDialog.IsVisible)
            closeFallbackDialog.Close();
    }

    public static void ActivateMenuItem(MenuItem item)
    {
        if (!item.IsEnabled || !item.IsVisible)
            return;

        item.Focus();

        RaiseClickEvent(item, MenuItem.ClickEvent);

        if (item.Command?.CanExecute(null) == true)
            item.Command.Execute(null);

        SimulateActivationKeys(item);

        CloseParentMenuIfOpen(item);
    }

    private static void RaiseClickEvent(Interactive control, RoutedEvent clickEvent)
    {
        control.RaiseEvent(new RoutedEventArgs(clickEvent) { Source = control });
        control.RaiseEvent(new RoutedEventArgs(clickEvent, RoutingStrategies.Bubble) { Source = control });
        control.RaiseEvent(new RoutedEventArgs(clickEvent, RoutingStrategies.Tunnel) { Source = control });
    }

    private static void SimulateActivationKeys(InputElement control)
    {
        SimulateKey(control, Key.Enter);
        SimulateKey(control, Key.Space);
    }

    private static void SimulateKey(InputElement control, Key key)
    {
        control.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = KeyModifiers.None,
            Source = control,
        });

        control.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyUpEvent,
            Key = key,
            KeyModifiers = KeyModifiers.None,
            Source = control,
        });
    }

    private static void CloseParentMenuIfOpen(MenuItem item)
    {
        var menu = item.Parent as ContextMenu
            ?? item.GetVisualAncestors().OfType<ContextMenu>().FirstOrDefault();

        if (menu != null && menu.IsOpen)
            menu.Close();
    }
}
