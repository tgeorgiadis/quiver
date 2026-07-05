using FluentAssertions;
using Quiver.Services;

namespace Quiver.Tests;

public class GamepadNavigationRepeatTests
{
    private const int MinInterval = 250;
    private const int InitialDelay = 650;
    private const int RepeatDelay = 350;

    [Fact]
    public void ShouldAllowNavigationMove_allows_first_move_on_cold_start()
    {
        GamepadNavigationRepeat.ShouldAllowNavigationMove(0, 0, MinInterval, InitialDelay, RepeatDelay, hasPriorMove: false)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldAllowNavigationMove_blocks_new_press_before_min_interval()
    {
        GamepadNavigationRepeat.ShouldAllowNavigationMove(0, 249, MinInterval, InitialDelay, RepeatDelay, hasPriorMove: true)
            .Should().BeFalse();
        GamepadNavigationRepeat.ShouldAllowNavigationMove(0, 250, MinInterval, InitialDelay, RepeatDelay, hasPriorMove: true)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldAllowNavigationMove_blocks_second_move_before_initial_delay()
    {
        GamepadNavigationRepeat.ShouldAllowNavigationMove(1, 649, MinInterval, InitialDelay, RepeatDelay, hasPriorMove: true)
            .Should().BeFalse();
        GamepadNavigationRepeat.ShouldAllowNavigationMove(1, 650, MinInterval, InitialDelay, RepeatDelay, hasPriorMove: true)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldAllowNavigationMove_uses_repeat_delay_for_third_and_later_moves()
    {
        GamepadNavigationRepeat.ShouldAllowNavigationMove(2, 349, MinInterval, InitialDelay, RepeatDelay, hasPriorMove: true)
            .Should().BeFalse();
        GamepadNavigationRepeat.ShouldAllowNavigationMove(2, 350, MinInterval, InitialDelay, RepeatDelay, hasPriorMove: true)
            .Should().BeTrue();
        GamepadNavigationRepeat.ShouldAllowNavigationMove(5, 349, MinInterval, InitialDelay, RepeatDelay, hasPriorMove: true)
            .Should().BeFalse();
        GamepadNavigationRepeat.ShouldAllowNavigationMove(5, 350, MinInterval, InitialDelay, RepeatDelay, hasPriorMove: true)
            .Should().BeTrue();
    }
}
