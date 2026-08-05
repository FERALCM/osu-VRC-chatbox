using System.Globalization;

namespace OsuVrcChatbox.Core.Logging;

/// <summary>Minimal logging sink so Core has no logging-framework dependency.</summary>
public interface ILogSink
{
    void Log(string level, string message);
}

/// <summary>Well-known paths for app data (plan §16/§17).</summary>
public static class AppPaths
{
    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "osu-vrc-chatbox");

    public static string LogsFolder => Path.Combine(Root, "logs");

    public static string LogFile => Path.Combine(LogsFolder, "app.log");
}

/// <summary>
/// Thread-safe, size-rotated file logger (plan §17). Bounded so a multi-hour session cannot grow logs
/// without limit: when the active file exceeds <c>maxBytes</c> it rolls to <c>app.1.log</c> …
/// <c>app.N.log</c>, deleting the oldest. Never throws to callers — logging must not crash the app.
/// </summary>
public sealed class RollingFileLogger : ILogSink, IDisposable
{
    private readonly string _path;
    private readonly long _maxBytes;
    private readonly int _maxFiles;
    private readonly object _lock = new();

    public RollingFileLogger(string? path = null, long maxBytes = 5 * 1024 * 1024, int maxFiles = 3)
    {
        _path = path ?? AppPaths.LogFile;
        _maxBytes = Math.Max(64 * 1024, maxBytes);
        _maxFiles = Math.Max(1, maxFiles);
        try { Directory.CreateDirectory(Path.GetDirectoryName(_path)!); }
        catch { /* best effort */ }
    }

    public void Info(string message) => Log("INFO", message);
    public void Warn(string message) => Log("WARN", message);
    public void Error(string message) => Log("ERROR", message);

    public void Log(string level, string message)
    {
        string line = $"{DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} [{level}] {message}{Environment.NewLine}";
        lock (_lock)
        {
            try
            {
                RotateIfNeeded();
                File.AppendAllText(_path, line);
            }
            catch
            {
                // Never let logging failures propagate.
            }
        }
    }

    private void RotateIfNeeded()
    {
        var info = new FileInfo(_path);
        if (!info.Exists || info.Length < _maxBytes) return;

        // Delete the oldest, shift the rest up by one, then move the active file to .1.
        string Oldest() => NumberedPath(_maxFiles);
        try { if (File.Exists(Oldest())) File.Delete(Oldest()); } catch { }

        for (int i = _maxFiles - 1; i >= 1; i--)
        {
            string src = NumberedPath(i);
            string dst = NumberedPath(i + 1);
            try { if (File.Exists(src)) File.Move(src, dst, overwrite: true); } catch { }
        }

        try { File.Move(_path, NumberedPath(1), overwrite: true); } catch { }
    }

    private string NumberedPath(int index)
    {
        string dir = Path.GetDirectoryName(_path)!;
        string name = Path.GetFileNameWithoutExtension(_path);
        string ext = Path.GetExtension(_path);
        return Path.Combine(dir, $"{name}.{index}{ext}");
    }

    public void Dispose() { /* no unmanaged handles held open */ }
}
