using OsuVrcChatbox.Core.Osc;

namespace OsuVrcChatbox.Core.Scheduling;

/// <summary>One queued outgoing message and whether it is urgent (map change).</summary>
public readonly record struct OutgoingMessage(string Text, bool Urgent);

/// <summary>
/// The single shared output pump (plan §13). Every message source funnels through one
/// <see cref="LatestValueScheduler{T}"/> (capacity 1, latest wins) and one
/// <see cref="IChatboxRateLimiter"/>. Runs its own loop; only this loop calls the sender.
/// Guarantees: ≤ 1 send per gap, no bursting of old updates, duplicate suppression, and a master
/// switch that halts (and optionally clears) output immediately.
/// </summary>
public sealed class ChatboxOutputService : IDisposable
{
    private readonly IOscChatboxSender _sender;
    private readonly IChatboxRateLimiter _limiter;
    private readonly LatestValueScheduler<OutgoingMessage> _scheduler = new();
    private readonly TimeSpan _pollInterval;

    private readonly object _stateLock = new();
    private bool _enabled = true;
    private string? _lastSentText;

    private CancellationTokenSource? _cts;
    private Task? _loop;

    /// <summary>Whether to include VRChat's notification sound (default false per policy).</summary>
    public bool NotificationSound { get; set; }

    public ChatboxOutputService(IOscChatboxSender sender, IChatboxRateLimiter limiter, TimeSpan? pollInterval = null)
    {
        _sender = sender;
        _limiter = limiter;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(100);
    }

    /// <summary>Master output switch. Turning it off clears our chatbox line and drops pending output.</summary>
    public bool Enabled
    {
        get { lock (_stateLock) return _enabled; }
        set
        {
            bool clearNeeded = false;
            lock (_stateLock)
            {
                if (_enabled == value) return;
                _enabled = value;
                if (!value)
                {
                    _scheduler.Clear();
                    clearNeeded = true;
                }
            }
            if (clearNeeded) RequestClear();
        }
    }

    public DateTimeOffset? LastSentAt => _sender.LastSentAt;
    public string Destination => _sender.Destination;

    /// <summary>Submits a status message (latest wins). Ignored while output is disabled.</summary>
    public void Submit(string text, bool urgent = false)
    {
        lock (_stateLock)
        {
            if (!_enabled) return;
        }
        _scheduler.Submit(new OutgoingMessage(text, urgent));
    }

    /// <summary>
    /// Queues a chatbox clear (empty string). Routed through the same limiter, but the limiter is
    /// reset so a user-initiated clear goes out promptly rather than waiting a full gap.
    /// </summary>
    public void RequestClear()
    {
        _limiter.Reset();
        _scheduler.Submit(new OutgoingMessage(string.Empty, Urgent: true));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_loop is not null) return Task.CompletedTask;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = Task.Run(() => PumpAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        _loop = null;
    }

    private async Task PumpAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await TickAsync(ct).ConfigureAwait(false);
                await Task.Delay(_pollInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // A failed UDP send must never kill the pump; back off one poll and continue.
                try { await Task.Delay(_pollInterval, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    /// <summary>One pump step. Internal for deterministic testing without the timer loop.</summary>
    internal async Task TickAsync(CancellationToken ct)
    {
        // Peek first so a value we can't send yet stays as the latest (no take-then-resubmit race).
        if (!_scheduler.TryPeek(out var pending)) return;

        // Duplicate suppression: consume identical text without sending or spending rate budget.
        if (pending.Text == _lastSentText)
        {
            _scheduler.TryTake(out _);
            return;
        }

        if (!_limiter.TryReserve(pending.Urgent)) return; // gap not elapsed; keep pending

        // Reserved: now take the (possibly newer) latest value and send it.
        _scheduler.TryTake(out pending);
        await _sender.SendAsync(pending.Text, immediate: true, notify: NotificationSound, ct).ConfigureAwait(false);
        _lastSentText = pending.Text;
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }
}
