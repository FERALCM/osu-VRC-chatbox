using System.Windows;
using OsuVrcChatbox.App.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace OsuVrcChatbox.App;

public partial class App : Application
{
    private SingleInstanceGuard? _guard;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _guard = new SingleInstanceGuard();
        if (!_guard.IsPrimaryInstance)
        {
            MessageBox.Show(
                "osu! → VRChat chatbox is already running.\nRunning two copies would let them overwrite each other's chatbox output.",
                "Already running", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Continue running while minimized to tray (no visible windows).
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var window = new MainWindow(e.Args);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _guard?.Dispose();
        base.OnExit(e);
    }
}
