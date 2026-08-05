using System.Diagnostics;

namespace OsuVrcChatbox.Core.Scheduling;

/// <summary>Enforces a minimum gap between chatbox sends (plan §13).</summary>
public interface IChatboxRateLimiter
{
    /// <summary>Current minimum gap (never below the hard floor).</summary>
    TimeSpan Gap { get; }

    /// <summary>Sets the gap; values below the hard floor are clamped up.</summary>
    void ConfigureGap(TimeSpan gap);

    /// <summary>
    /// If the effective gap has elapsed since the last reservation, records "now" and returns true.
    /// When <paramref name="urgent"/> (e.g. a map change), the effective gap is the hard floor rather
    /// than the configured gap — so an important update can go sooner, but never below the floor.
    /// </summary>
    bool TryReserve(bool urgent = false);

    /// <summary>How long until <see cref="TryReserve"/> would succeed (Zero if ready now).</summary>
    TimeSpan TimeUntilNext(bool urgent = false);

    /// <summary>Forgets the last-send time so the next reservation succeeds immediately.</summary>
    void Reset();
}

/// <summary>
/// Monotonic-clock rate limiter. Uses <see cref="Stopwatch"/> timestamps (immune to wall-clock
/// changes). Enforces a hard 2-second floor regardless of configured value, so no code path can
/// drive VRChat's chatbox faster than the conservative policy allows.
/// </summary>
public sealed class ChatboxRateLimiter : IChatboxRateLimiter
{
    /// <summary>Absolute minimum gap between sends (plan §13).</summary>
    public static readonly TimeSpan HardFloor = TimeSpan.FromSeconds(2);

    /// <summary>Default gap between sends.</summary>
    public static readonly TimeSpan DefaultGap = TimeSpan.FromSeconds(3);

    private readonly object _lock = new();
    private long _lastTicks; // 0 = never reserved
    private TimeSpan _gap;

    public ChatboxRateLimiter() : this(DefaultGap) { }

    public ChatboxRateLimiter(TimeSpan gap) => ConfigureGap(gap);

    public TimeSpan Gap
    {
        get { lock (_lock) return _gap; }
    }

    public void ConfigureGap(TimeSpan gap)
    {
        lock (_lock) _gap = gap < HardFloor ? HardFloor : gap;
    }

    public bool TryReserve(bool urgent = false)
    {
        lock (_lock)
        {
            TimeSpan gap = EffectiveGap(urgent);
            long now = Stopwatch.GetTimestamp();
            if (_lastTicks == 0 || Elapsed(now) >= gap)
            {
                _lastTicks = now;
                return true;
            }
            return false;
        }
    }

    public TimeSpan TimeUntilNext(bool urgent = false)
    {
        lock (_lock)
        {
            if (_lastTicks == 0) return TimeSpan.Zero;
            TimeSpan gap = EffectiveGap(urgent);
            TimeSpan elapsed = Elapsed(Stopwatch.GetTimestamp());
            return elapsed >= gap ? TimeSpan.Zero : gap - elapsed;
        }
    }

    private TimeSpan EffectiveGap(bool urgent) => urgent ? HardFloor : _gap;

    public void Reset()
    {
        lock (_lock) _lastTicks = 0;
    }

    private TimeSpan Elapsed(long now) =>
        TimeSpan.FromSeconds((now - _lastTicks) / (double)Stopwatch.Frequency);
}
