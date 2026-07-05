using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Quiver.Services;

public sealed class GamepadModalDialogNavigation
{
    private static GamepadModalDialogNavigation? _instance;

    private Window? _activeDialog;
    private List<Button> _dialogButtons = [];
    private int _focusedButtonIndex;

    public static GamepadModalDialogNavigation Instance => _instance ??= new GamepadModalDialogNavigation();

    public bool HasActiveDialog => _activeDialog != null;

    public void Configure(InputService inputService)
    {
    }

    public static void Attach(Window dialog)
    {
        Instance.PrepareDialog(dialog);
    }

    public void PrepareDialog(Window dialog)
    {
        _activeDialog = dialog;
        RefreshDialogButtons();

        void OnDialogOpened(object? sender, EventArgs e)
        {
            RefreshDialogButtons();

            void OnLayoutUpdated(object? s, EventArgs args)
            {
                if (!ReferenceEquals(_activeDialog, dialog))
                    return;

                RefreshDialogButtons();
                dialog.LayoutUpdated -= OnLayoutUpdated;
            }

            dialog.LayoutUpdated += OnLayoutUpdated;
        }

        void OnDialogClosed(object? sender, EventArgs e)
        {
            dialog.Opened -= OnDialogOpened;
            dialog.Closed -= OnDialogClosed;
            UnregisterModalDialog(dialog);
        }

        dialog.Opened += OnDialogOpened;
        dialog.Closed += OnDialogClosed;
    }

    public void RefreshDialogButtons()
    {
        if (_activeDialog == null)
            return;

        _dialogButtons = CollectDialogButtons(_activeDialog);
        _focusedButtonIndex = GetDefaultButtonIndex(_dialogButtons);

        Dispatcher.UIThread.Post(FocusCurrentButton, DispatcherPriority.Loaded);
    }

    public void UnregisterModalDialog(Window dialog)
    {
        if (!ReferenceEquals(_activeDialog, dialog))
            return;

        _activeDialog = null;
        _dialogButtons = [];
        _focusedButtonIndex = 0;
    }

    public bool TryHandleNavigation(NavigationDirection direction)
    {
        if (_activeDialog == null)
            return false;

        EnsureDialogButtons();
        if (_dialogButtons.Count == 0)
            return true;

        if (_dialogButtons.Count == 1)
            return true;

        var positions = GetButtonPositions(_dialogButtons, GetButtonCenter);
        _focusedButtonIndex = MoveButtonIndex(_focusedButtonIndex, direction, positions);
        FocusCurrentButton();
        return true;
    }

    public bool TryHandleConfirm()
    {
        if (_activeDialog == null)
            return false;

        EnsureDialogButtons();
        if (_dialogButtons.Count == 0)
        {
            CloseDialogIfStillOpen();
            return true;
        }

        var button = GetFocusedButton();
        if (button == null)
            return false;

        GamepadControlActivation.ActivateDialogButton(button, _activeDialog);
        CloseDialogIfStillOpen();
        return true;
    }

    public bool TryHandleCancel()
    {
        if (_activeDialog == null)
            return false;

        EnsureDialogButtons();

        if (_dialogButtons.Count == 0)
        {
            CloseDialogIfStillOpen();
            return true;
        }

        var cancelIndex = FindCancelButtonIndex(_dialogButtons);
        var button = cancelIndex >= 0
            ? _dialogButtons[cancelIndex]
            : _dialogButtons.Count == 1
                ? _dialogButtons[0]
                : GetFocusedButton();

        if (button == null)
        {
            CloseDialogIfStillOpen();
            return true;
        }

        GamepadControlActivation.ActivateDialogButton(button, _activeDialog);
        CloseDialogIfStillOpen();
        return true;
    }

    private void CloseDialogIfStillOpen()
    {
        if (_activeDialog != null && _activeDialog.IsVisible)
            _activeDialog.Close();
    }

    private void EnsureDialogButtons()
    {
        if (_activeDialog == null || _dialogButtons.Count > 0)
            return;

        _dialogButtons = CollectDialogButtons(_activeDialog);
        _focusedButtonIndex = GetDefaultButtonIndex(_dialogButtons);
    }

    public static List<Button> CollectDialogButtons(Control root)
    {
        return root.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.IsVisible && button.IsEnabled)
            .OrderBy(button => GetApproximateCenter(button)?.Y ?? 0)
            .ThenBy(button => GetApproximateCenter(button)?.X ?? 0)
            .ToList();
    }

    public static int GetDefaultButtonIndex(IReadOnlyList<Button> buttons)
    {
        if (buttons.Count == 0)
            return -1;

        var preferredLabels = new[]
        {
            "ok",
            "yes",
            "add",
            "download anyway",
            "open settings",
        };

        for (var i = 0; i < buttons.Count; i++)
        {
            var label = GetButtonLabel(buttons[i]);
            if (preferredLabels.Any(preferred => string.Equals(label, preferred, StringComparison.OrdinalIgnoreCase)))
                return i;
        }

        return 0;
    }

    public static int FindCancelButtonIndex(IReadOnlyList<Button> buttons)
    {
        var cancelLabels = new[] { "cancel", "no", "close" };

        for (var i = 0; i < buttons.Count; i++)
        {
            var label = GetButtonLabel(buttons[i]);
            if (cancelLabels.Any(cancel => string.Equals(label, cancel, StringComparison.OrdinalIgnoreCase)))
                return i;
        }

        return -1;
    }

    public static List<(double X, double Y)> GetButtonPositions(
        IReadOnlyList<Button> buttons,
        Func<Button, (double X, double Y)?> getCenter)
    {
        var positions = new List<(double X, double Y)>();
        foreach (var button in buttons)
        {
            positions.Add(getCenter(button) ?? (0, positions.Count * 40));
        }

        return positions;
    }

    public static int MoveButtonIndex(
        int currentIndex,
        NavigationDirection direction,
        IReadOnlyList<(double X, double Y)> positions,
        double rowTolerance = 24)
    {
        if (positions.Count == 0)
            return -1;

        if (currentIndex < 0 || currentIndex >= positions.Count)
            return 0;

        if (positions.Count == 1)
            return currentIndex;

        var current = positions[currentIndex];
        int? bestIndex = null;
        var bestScore = double.MaxValue;

        for (var i = 0; i < positions.Count; i++)
        {
            if (i == currentIndex)
                continue;

            var score = CalculateNavigationScore(current, positions[i], direction);
            if (!score.HasValue || score.Value >= bestScore)
                continue;

            bestScore = score.Value;
            bestIndex = i;
        }

        if (bestIndex.HasValue)
            return bestIndex.Value;

        return WrapButtonIndex(currentIndex, direction, positions, rowTolerance);
    }

    private void FocusCurrentButton()
    {
        GetFocusedButton()?.Focus();
    }

    private Button? GetFocusedButton()
    {
        if (_focusedButtonIndex < 0 || _focusedButtonIndex >= _dialogButtons.Count)
            return null;

        return _dialogButtons[_focusedButtonIndex];
    }

    private (double X, double Y)? GetButtonCenter(Button button)
    {
        if (_activeDialog == null)
            return GetApproximateCenter(button);

        var topLeft = button.TranslatePoint(new Avalonia.Point(0, 0), _activeDialog);
        if (!topLeft.HasValue)
            return null;

        var bounds = button.Bounds;
        return (topLeft.Value.X + bounds.Width / 2, topLeft.Value.Y + bounds.Height / 2);
    }

    private static (double X, double Y)? GetApproximateCenter(Control control)
    {
        var bounds = control.Bounds;
        if (bounds.Width <= 0 && bounds.Height <= 0)
            return (control.GetHashCode() % 1000, 0);

        return (bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }

    private static string GetButtonLabel(Button button)
    {
        return button.Content?.ToString()?.Trim() ?? string.Empty;
    }

    private static int WrapButtonIndex(
        int currentIndex,
        NavigationDirection direction,
        IReadOnlyList<(double X, double Y)> positions,
        double rowTolerance)
    {
        if (direction is NavigationDirection.Up or NavigationDirection.Down)
        {
            if (direction == NavigationDirection.Up && IsTopRow(positions, currentIndex, rowTolerance))
            {
                var maxY = positions.Max(position => position.Y);
                return WrapToRow(positions, currentIndex, maxY, rowTolerance);
            }

            if (direction == NavigationDirection.Down && IsBottomRow(positions, currentIndex, rowTolerance))
            {
                var minY = positions.Min(position => position.Y);
                return WrapToRow(positions, currentIndex, minY, rowTolerance);
            }
        }

        return (currentIndex + 1) % positions.Count;
    }

    private static bool IsTopRow(IReadOnlyList<(double X, double Y)> positions, int index, double rowTolerance)
    {
        var minY = positions.Min(position => position.Y);
        return positions[index].Y <= minY + rowTolerance;
    }

    private static bool IsBottomRow(IReadOnlyList<(double X, double Y)> positions, int index, double rowTolerance)
    {
        var maxY = positions.Max(position => position.Y);
        return positions[index].Y >= maxY - rowTolerance;
    }

    private static int WrapToRow(
        IReadOnlyList<(double X, double Y)> positions,
        int index,
        double targetRowY,
        double rowTolerance)
    {
        var currentX = positions[index].X;
        var bestIndex = index;
        var bestScore = double.MaxValue;

        for (var i = 0; i < positions.Count; i++)
        {
            if (Math.Abs(positions[i].Y - targetRowY) > rowTolerance)
                continue;

            var score = Math.Abs(positions[i].X - currentX);
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static double? CalculateNavigationScore(
        (double X, double Y) current,
        (double X, double Y) candidate,
        NavigationDirection direction)
    {
        var dx = candidate.X - current.X;
        var dy = candidate.Y - current.Y;

        double primaryDistance;
        double secondaryDistance;

        switch (direction)
        {
            case NavigationDirection.Up:
                if (dy >= -1)
                    return null;
                primaryDistance = Math.Abs(dy);
                secondaryDistance = Math.Abs(dx);
                break;
            case NavigationDirection.Down:
                if (dy <= 1)
                    return null;
                primaryDistance = Math.Abs(dy);
                secondaryDistance = Math.Abs(dx);
                break;
            case NavigationDirection.Left:
                if (dx >= -1)
                    return null;
                primaryDistance = Math.Abs(dx);
                secondaryDistance = Math.Abs(dy);
                break;
            case NavigationDirection.Right:
                if (dx <= 1)
                    return null;
                primaryDistance = Math.Abs(dx);
                secondaryDistance = Math.Abs(dy);
                break;
            default:
                return null;
        }

        var offAxisPenalty = secondaryDistance > 10 ? secondaryDistance * 2.5 : 0;
        return primaryDistance + (secondaryDistance * 0.3) + offAxisPenalty;
    }
}
