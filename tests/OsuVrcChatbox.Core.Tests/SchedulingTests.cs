using OsuVrcChatbox.Core.Osc;
using OsuVrcChatbox.Core.Scheduling;
using Xunit;

namespace OsuVrcChatbox.Core.Tests;

public class LatestValueSchedulerTests
{
    [Fact]
    public void Latest_submission_wins_capacity_one()
    {
        var s = new LatestValueScheduler<string>();
        s.Submit("a");
        s.Submit("b");
        s.Submit("c");
        Assert.True(s.TryTake(out var v));
        Assert.Equal("c", v);
        Assert.False(s.TryTake(out _)); // nothing left — history never accumulates
    }

    [Fact]
    public void Peek_does_not_remove()
    {
        var s = new LatestValueScheduler<string>();
        s.Submit("x");
        Assert.True(s.TryPeek(out var p));
        Assert.Equal("x", p);
        Assert.True(s.HasPending);
        Assert.True(s.TryTake(out _));
    }

    [Fact]
    public void Clear_discards_pending()
    {
        var s = new LatestValueScheduler<string>();
        s.Submit("x");
        s.Clear();
        Assert.False(s.HasPending);
    }
}

public class ChatboxRateLimiterTests
{
    [Fact]
    public void Configured_gap_below_hard_floor_is_clamped()
    {
        Assert.Equal(ChatboxRateLimiter.HardFloor, new ChatboxRateLimiter(TimeSpan.FromSeconds(1)).Gap);
    }

    [Fact]
    public void Default_gap_is_three_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(3), new ChatboxRateLimiter().Gap);
    }

    [Fact]
    public void First_reservation_always_succeeds()
    {
        Assert.True(new ChatboxRateLimiter(TimeSpan.FromSeconds(10)).TryReserve());
    }

    [Fact]
    public void Second_immediate_reservation_is_blocked()
    {
        var l = new ChatboxRateLimiter(TimeSpan.FromSeconds(10));
        Assert.True(l.TryReserve());
        Assert.False(l.TryReserve());
        Assert.True(l.TimeUntilNext() > TimeSpan.Zero);
    }

    [Fact]
    public void Urgent_gap_is_never_longer_than_normal_gap()
    {
        var l = new ChatboxRateLimiter(TimeSpan.FromSeconds(10));
        l.TryReserve();
        Assert.True(l.TimeUntilNext(urgent: true) <= l.TimeUntilNext(urgent: false));
    }

    [Fact]
    public void Reset_allows_immediate_reservation()
    {
        var l = new ChatboxRateLimiter(TimeSpan.FromSeconds(10));
        l.TryReserve();
        l.Reset();
        Assert.True(l.TryReserve());
    }
}

public class ChatboxOutputServiceTests
{
    private sealed class FakeSender : IOscChatboxSender
    {
        public readonly List<string> Sent = new();
        public string Destination => "fake";
        public DateTimeOffset? LastSentAt { get; private set; }
        public Task SendAsync(string text, bool immediate, bool notify, CancellationToken ct = default)
        {
            Sent.Add(text);
            LastSentAt = DateTimeOffset.UtcNow;
            return Task.CompletedTask;
        }
        public Task ClearAsync(CancellationToken ct = default) { Sent.Add(""); return Task.CompletedTask; }
        public void Dispose() { }
    }

    private sealed class FakeLimiter : IChatboxRateLimiter
    {
        public bool Ready = true;
        public TimeSpan Gap => TimeSpan.FromSeconds(3);
        public void ConfigureGap(TimeSpan gap) { }
        public bool TryReserve(bool urgent = false) => Ready;
        public TimeSpan TimeUntilNext(bool urgent = false) => Ready ? TimeSpan.Zero : TimeSpan.FromSeconds(1);
        public void Reset() => Ready = true;
    }

    [Fact]
    public async Task Sends_pending_message()
    {
        var sender = new FakeSender();
        var svc = new ChatboxOutputService(sender, new FakeLimiter());
        svc.Submit("hello");
        await svc.TickAsync(default);
        Assert.Equal(new[] { "hello" }, sender.Sent);
    }

    [Fact]
    public async Task Never_bursts_only_latest_is_sent()
    {
        var sender = new FakeSender();
        var svc = new ChatboxOutputService(sender, new FakeLimiter());
        svc.Submit("A");
        svc.Submit("B");
        svc.Submit("C");
        await svc.TickAsync(default);
        await svc.TickAsync(default); // nothing left
        Assert.Equal(new[] { "C" }, sender.Sent);
    }

    [Fact]
    public async Task Suppresses_duplicate_identical_text()
    {
        var sender = new FakeSender();
        var svc = new ChatboxOutputService(sender, new FakeLimiter());
        svc.Submit("same");
        await svc.TickAsync(default);
        svc.Submit("same");
        await svc.TickAsync(default);
        Assert.Equal(new[] { "same" }, sender.Sent);
    }

    [Fact]
    public async Task Holds_message_until_rate_gate_opens()
    {
        var sender = new FakeSender();
        var limiter = new FakeLimiter { Ready = false };
        var svc = new ChatboxOutputService(sender, limiter);
        svc.Submit("later");
        await svc.TickAsync(default);
        Assert.Empty(sender.Sent);       // gated
        limiter.Ready = true;
        await svc.TickAsync(default);
        Assert.Equal(new[] { "later" }, sender.Sent);
    }

    [Fact]
    public async Task Disabled_master_switch_drops_status_output()
    {
        var sender = new FakeSender();
        var svc = new ChatboxOutputService(sender, new FakeLimiter());
        svc.Enabled = false;             // also queues a clear
        svc.Submit("should-not-send");
        await svc.TickAsync(default);
        Assert.DoesNotContain("should-not-send", sender.Sent);
    }
}
