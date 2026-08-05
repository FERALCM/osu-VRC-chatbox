namespace OsuVrcChatbox.App.Services;

/// <summary>
/// Ensures only one instance runs (plan §22). Holds a named system mutex for the process lifetime;
/// a second launch fails to acquire it and should exit. Prevents two copies fighting over the
/// VRChat chatbox.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = "Global\\OsuVrcChatbox_SingleInstance";
    private readonly Mutex _mutex;

    public bool IsPrimaryInstance { get; }

    public SingleInstanceGuard()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        IsPrimaryInstance = createdNew;
    }

    public void Dispose()
    {
        try { if (IsPrimaryInstance) _mutex.ReleaseMutex(); } catch { /* ignore */ }
        _mutex.Dispose();
    }
}
