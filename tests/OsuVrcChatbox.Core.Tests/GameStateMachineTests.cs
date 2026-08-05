using OsuVrcChatbox.Core.State;
using Xunit;

namespace OsuVrcChatbox.Core.Tests;

public class GameStateMachineTests
{
    [Fact]
    public void Play_active_is_gameplay_active()
    {
        var sm = new GameStateMachine();
        var e = sm.Evaluate(Fixtures.Gameplay());
        Assert.Equal(AppGameState.GameplayActive, e.State);
        Assert.True(e.ShouldSendContinuous);
    }

    [Fact]
    public void Paused_play_is_gameplay_paused_and_not_continuous()
    {
        var sm = new GameStateMachine();
        var e = sm.Evaluate(Fixtures.Gameplay(paused: true));
        Assert.Equal(AppGameState.GameplayPaused, e.State);
        Assert.False(e.ShouldSendContinuous);
    }

    [Fact]
    public void Failed_play_is_failed()
    {
        var sm = new GameStateMachine();
        Assert.Equal(AppGameState.Failed, sm.Evaluate(Fixtures.Gameplay(failed: true)).State);
    }

    [Theory]
    [InlineData(0, AppGameState.Menu)]
    [InlineData(5, AppGameState.SongSelect)]
    [InlineData(7, AppGameState.Results)]
    [InlineData(1, AppGameState.Other)]
    public void Maps_state_numbers(int stateNumber, AppGameState expected)
    {
        var sm = new GameStateMachine();
        Assert.Equal(expected, sm.Evaluate(Fixtures.Gameplay(stateNumber: stateNumber)).State);
    }

    [Fact]
    public void Null_snapshot_is_osu_not_running()
    {
        var sm = new GameStateMachine();
        Assert.Equal(AppGameState.OsuNotRunning, sm.Evaluate(null).State);
    }

    [Fact]
    public void First_seen_map_flags_change()
    {
        var sm = new GameStateMachine();
        Assert.True(sm.Evaluate(Fixtures.Gameplay()).MapChanged);
    }

    [Fact]
    public void Same_map_does_not_reflag()
    {
        var sm = new GameStateMachine();
        sm.Evaluate(Fixtures.Gameplay());
        Assert.False(sm.Evaluate(Fixtures.Gameplay(live: 120000)).MapChanged);
    }

    [Fact]
    public void Different_checksum_flags_change()
    {
        var sm = new GameStateMachine();
        sm.Evaluate(Fixtures.Gameplay());
        var other = Fixtures.Gameplay() with { BeatmapChecksum = "different" };
        Assert.True(sm.Evaluate(other).MapChanged);
    }

    [Fact]
    public void Reset_clears_map_tracking()
    {
        var sm = new GameStateMachine();
        sm.Evaluate(Fixtures.Gameplay());
        sm.Reset();
        Assert.True(sm.Evaluate(Fixtures.Gameplay()).MapChanged); // first-seen again
    }
}
