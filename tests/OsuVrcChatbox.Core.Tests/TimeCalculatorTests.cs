using OsuVrcChatbox.Core.Timing;
using Xunit;

namespace OsuVrcChatbox.Core.Tests;

public class TimeCalculatorTests
{
    [Fact]
    public void NoMod_audio_length_matches_map_timeline()
    {
        var s = Fixtures.Gameplay(live: 102000, mp3Length: 198000, rate: 1.0);
        var t = TimeCalculator.Compute(s, new TimingConfig(LengthSource.Audio));

        Assert.Equal("1:42", t.ElapsedText);   // 102s
        Assert.Equal("3:18", t.LengthText);     // 198s
        Assert.Equal("1:36", t.RemainingText);  // 96s
    }

    [Fact]
    public void DoubleTime_divides_times_by_rate()
    {
        var s = Fixtures.Gameplay(live: 102000, mp3Length: 198000, rate: 1.5);
        var t = TimeCalculator.Compute(s, new TimingConfig(LengthSource.Audio));

        Assert.Equal("2:12", t.LengthText);     // 198/1.5 = 132s
        Assert.Equal("1:08", t.ElapsedText);    // 102/1.5 = 68s
        Assert.Equal("1:04", t.RemainingText);  // 96/1.5  = 64s
    }

    [Fact]
    public void HalfTime_expands_times()
    {
        var s = Fixtures.Gameplay(live: 60000, mp3Length: 180000, rate: 0.75);
        var t = TimeCalculator.Compute(s, new TimingConfig(LengthSource.Audio));

        Assert.Equal("4:00", t.LengthText);   // 180/0.75 = 240s
        Assert.Equal("1:20", t.ElapsedText);  // 60/0.75  = 80s
    }

    [Fact]
    public void LeadIn_negative_live_clamps_elapsed_to_zero_and_remaining_to_length()
    {
        var s = Fixtures.Gameplay(live: -2000, mp3Length: 198000, rate: 1.0);
        var t = TimeCalculator.Compute(s, new TimingConfig(LengthSource.Audio));

        Assert.Equal(TimeSpan.Zero, t.Elapsed);
        Assert.Equal("3:18", t.RemainingText); // full length, never more
        Assert.True(t.Remaining <= t.Length);
    }

    [Fact]
    public void Remaining_never_negative_past_end()
    {
        var s = Fixtures.Gameplay(live: 250000, mp3Length: 198000, rate: 1.0);
        var t = TimeCalculator.Compute(s, new TimingConfig(LengthSource.Audio));
        Assert.Equal(TimeSpan.Zero, t.Remaining);
    }

    [Fact]
    public void LastObject_source_uses_drain_end()
    {
        var s = Fixtures.Gameplay(live: 100000, lastObject: 195000, mp3Length: 198000, rate: 1.0);
        var t = TimeCalculator.Compute(s, new TimingConfig(LengthSource.LastObject));
        Assert.Equal("3:15", t.LengthText); // 195s
    }

    [Fact]
    public void TimesAreRateAdjusted_flag_disables_division()
    {
        var s = Fixtures.Gameplay(live: 102000, mp3Length: 198000, rate: 1.5);
        var t = TimeCalculator.Compute(s, new TimingConfig(LengthSource.Audio, TimesAreRateAdjusted: true));
        Assert.Equal("3:18", t.LengthText); // not divided
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(5000, "0:05")]
    [InlineData(65000, "1:05")]
    [InlineData(3600000, "1:00:00")]
    public void Formats_durations(double ms, string expected)
    {
        Assert.Equal(expected, GameplayTiming.Format(TimeSpan.FromMilliseconds(ms)));
    }

    [Fact]
    public void Negative_duration_formats_as_zero()
    {
        Assert.Equal("0:00", GameplayTiming.Format(TimeSpan.FromSeconds(-30)));
    }
}
