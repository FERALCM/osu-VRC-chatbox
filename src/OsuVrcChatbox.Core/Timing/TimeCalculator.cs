using OsuVrcChatbox.Core.Model;

namespace OsuVrcChatbox.Core.Timing;

/// <summary>Which map value acts as the "length" denominator (plan §10).</summary>
public enum LengthSource
{
    /// <summary>Audio duration (<c>beatmap.time.mp3Length</c>). Default; labeled "map length".</summary>
    Audio,

    /// <summary>Final hit-object time (<c>beatmap.time.lastObject</c>) — drain/playable length.</summary>
    LastObject
}

/// <param name="LengthSource">Denominator selection.</param>
/// <param name="TimesAreRateAdjusted">
/// When <c>false</c> (default, verified assumption): tosu times are on the map timeline and are
/// divided by <c>rate</c> to yield real wall-clock time. If Phase-0 capture proves tosu already
/// rate-adjusts these fields, set this <c>true</c> and the calculator stops dividing.
/// </param>
public readonly record struct TimingConfig(
    LengthSource LengthSource = LengthSource.Audio,
    bool TimesAreRateAdjusted = false);

/// <summary>Wall-clock (rate-adjusted) gameplay timing derived from a snapshot.</summary>
public readonly record struct GameplayTiming(TimeSpan Elapsed, TimeSpan Remaining, TimeSpan Length)
{
    public string ElapsedText => Format(Elapsed);
    public string RemainingText => Format(Remaining);
    public string LengthText => Format(Length);

    /// <summary>Formats a duration as <c>m:ss</c> (or <c>h:mm:ss</c> when ≥ 1 hour). Clamps negatives to 0.</summary>
    public static string Format(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        int totalSeconds = (int)Math.Round(t.TotalSeconds, MidpointRounding.AwayFromZero);
        int h = totalSeconds / 3600;
        int m = totalSeconds % 3600 / 60;
        int s = totalSeconds % 60;
        return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m}:{s:00}";
    }
}

/// <summary>
/// Computes rate-adjusted elapsed/remaining/length from a <see cref="GameplaySnapshot"/>.
/// Pure/stateless; safe to call from any thread. Handles lead-in (negative <c>live</c>) and
/// clamps all values so remaining ∈ [0, length] and elapsed ∈ [0, length].
/// </summary>
public static class TimeCalculator
{
    public static GameplayTiming Compute(GameplaySnapshot s, TimingConfig config)
    {
        double endMs = config.LengthSource == LengthSource.Audio ? s.Mp3Length : s.TimeLastObject;

        // Fallback if the chosen source is missing/zero.
        if (endMs <= 0) endMs = config.LengthSource == LengthSource.Audio ? s.TimeLastObject : s.Mp3Length;
        if (endMs < 0) endMs = 0;

        double rate = config.TimesAreRateAdjusted ? 1.0 : s.Rate;
        if (rate <= 0) rate = 1.0;

        double live = s.TimeLive;
        double elapsedMs = Math.Clamp(live, 0, endMs);
        double remainingMs = Math.Clamp(endMs - live, 0, endMs);

        return new GameplayTiming(
            Elapsed: TimeSpan.FromMilliseconds(elapsedMs / rate),
            Remaining: TimeSpan.FromMilliseconds(remainingMs / rate),
            Length: TimeSpan.FromMilliseconds(endMs / rate));
    }
}
