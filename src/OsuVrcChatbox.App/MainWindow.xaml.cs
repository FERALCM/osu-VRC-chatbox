using System.ComponentModel;
using System.Windows;
using OsuVrcChatbox.App.Services;
using OsuVrcChatbox.App.ViewModels;
using OsuVrcChatbox.Core.Logging;
using OsuVrcChatbox.Core.Settings;

namespace OsuVrcChatbox.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly RollingFileLogger _logger;
    private readonly TrayService _tray;
    private readonly HotkeyService _hotkey;
    private readonly bool _startMinimizedArg;
    private bool _exiting;

    public MainWindow(string[] args)
    {
        InitializeComponent();

        _startMinimizedArg = args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        _logger = new RollingFileLogger();
        _logger.Info("Application starting.");

        _viewModel = new MainViewModel(new SettingsStore(SettingsStore.DefaultPath), _logger);
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        _tray = new TrayService();
        _tray.ShowRequested += ShowWindow;
        _tray.TogglePauseRequested += () => _viewModel.MasterEnabled = !_viewModel.MasterEnabled;
        _tray.ClearRequested += () => _viewModel.ClearChatboxCommand.Execute(null);
        _tray.ExitRequested += ExitApplication;
        _tray.SetOutputEnabled(_viewModel.MasterEnabled);

        _hotkey = new HotkeyService();
        _hotkey.Pressed += () =>
        {
            _viewModel.MasterEnabled = !_viewModel.MasterEnabled;
            _tray.ShowBalloon("osu! → VRChat chatbox",
                _viewModel.MasterEnabled ? "Output resumed" : "Output paused");
        };

        Loaded += OnLoaded;
        StateChanged += OnStateChanged;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_hotkey.Initialize(this, _viewModel.GlobalPauseHotkey))
            _logger.Warn($"Failed to register global hotkey '{_viewModel.GlobalPauseHotkey}'.");

        _viewModel.Start();

        if (_startMinimizedArg || _viewModel.StartMinimized)
        {
            if (_viewModel.MinimizeToTray) Hide();
            else WindowState = WindowState.Minimized;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.MasterEnabled))
            _tray.SetOutputEnabled(_viewModel.MasterEnabled);
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _viewModel.MinimizeToTray)
            Hide();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // The window's X minimizes to tray; real exit comes from the tray menu.
        if (!_exiting && _viewModel.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            _tray.ShowBalloon("Still running", "osu! → VRChat chatbox is minimized to the tray.");
        }
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _exiting = true;
        _logger.Info("Application exiting.");
        _hotkey.Dispose();
        _viewModel.Dispose();      // persists settings, clears chatbox, tears down pipeline
        _tray.Dispose();
        _logger.Dispose();
        Close();
        System.Windows.Application.Current.Shutdown();
    }
}
