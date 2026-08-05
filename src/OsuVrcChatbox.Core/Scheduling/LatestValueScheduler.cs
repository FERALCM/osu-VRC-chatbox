namespace OsuVrcChatbox.Core.Scheduling;

/// <summary>
/// A capacity-1 "latest wins" slot (plan §13). Submitting a new value overwrites any pending one, so
/// history never accumulates and the pump can never burst old updates after lag/reconnect/resume.
/// Thread-safe.
/// </summary>
public sealed class LatestValueScheduler<T>
{
    private readonly object _lock = new();
    private T? _pending;
    private bool _has;

    /// <summary>Replaces the pending value with <paramref name="item"/>.</summary>
    public void Submit(T item)
    {
        lock (_lock)
        {
            _pending = item;
            _has = true;
        }
    }

    /// <summary>Reads the pending value without removing it.</summary>
    public bool TryPeek(out T item)
    {
        lock (_lock)
        {
            if (_has)
            {
                item = _pending!;
                return true;
            }
            item = default!;
            return false;
        }
    }

    /// <summary>Takes and clears the pending value, if any.</summary>
    public bool TryTake(out T item)
    {
        lock (_lock)
        {
            if (_has)
            {
                item = _pending!;
                _pending = default;
                _has = false;
                return true;
            }
            item = default!;
            return false;
        }
    }

    public bool HasPending
    {
        get { lock (_lock) return _has; }
    }

    /// <summary>Discards any pending value without sending it.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _pending = default;
            _has = false;
        }
    }
}
