namespace Quiver.Services;

public static class GamepadNavigationRepeat
{
    public static bool ShouldAllowNavigationMove(
        int movesInHold,
        double elapsedMs,
        int minInterval,
        int initialDelay,
        int repeatDelay,
        bool hasPriorMove)
    {
        return movesInHold switch
        {
            0 => !hasPriorMove || elapsedMs >= minInterval,
            1 => elapsedMs >= initialDelay,
            _ => elapsedMs >= repeatDelay,
        };
    }
}
