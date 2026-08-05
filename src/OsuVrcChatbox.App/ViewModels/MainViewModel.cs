using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using OsuVrcChatbox.App.Services;
using OsuVrcChatbox.Core;
using OsuVrcChatbox.Core.Formatting;
using OsuVrcChatbox.Core.Logging;
using OsuVrcChatbox.Core.Model;
using OsuVrcChatbox.Core.Osc;
using OsuVrcChatbox.Core.Scheduling;
using OsuVrcChatbox.Core.Settings;
using OsuVrcChatbox.Core.State;
using OsuVrcChatbox.Core.Telemetry;
using OsuVrcChatbox.Core.Timing;

namespace OsuVrcChatbox.App.ViewModels;

/// <summary>
/// Owns the Core pipeline (plan §7) and projects it into bindable UI state (plan §22). Cheap setting
/// changes are applied live; endpoint changes (tosu host/port, OSC ip/port) rebuild the pipeline via
/// <see cref="ApplyConnectionCommand"/>. Settings are persisted with a short debounce.
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly SettingsStore _store;
    private readonly RollingFileLogger _logger;
    private readonly ChatboxFormatter _formatter = new();
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _saveTimer;
    private readonly CancellationTokenSource _appCts = new();

    // Live pipeline (rebuilt on connection apply).
    private TosuWebSocketTelemetrySource? _source;
    private IOscChatboxSender? _sender;
    private ChatboxOutputService? _output;
    private ChatboxRateLimiter? _limiter;
    private AppOrchestrator? _orchestrator;

    private GameplaySnapshot? _lastSnapshot;
    private bool _loading;

    public MainViewModel(SettingsStore store, RollingFileLogger logger)
    {
        _store = store;
        _logger = logger;
        _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); PersistSettings(); };

        ToggleOutputCommand = new RelayCommand(() => MasterEnabled = !MasterEnabled);
        ClearChatboxCommand = new RelayCommand(() => _output?.RequestClear());
        OpenLogsCommand = new RelayCommand(OpenLogs);
        ApplyConnectionCommand = new RelayCommand(() => RebuildPipeline());

        LoadFromSettings(_store.Load());
        RefreshPreview();
    }

    // ── Commands ────────────────────────────────────────────────────
    public ICommand ToggleOutputCommand { get; }
    public ICommand ClearChatboxCommand { get; }
    public ICommand OpenLogsCommand { get; }
    public ICommand ApplyConnectionCommand { get; }

    // ── Combo-box sources ───────────────────────────────────────────
    public IReadOnlyList<MessagePreset> Presets { get; } = Enum.GetValues<MessagePreset>();
    public IReadOnlyList<LengthSource> LengthSources { get; } = Enum.GetValues<LengthSource>();

    // ── Live status (read-only display) ─────────────────────────────
    private string _sourceStatus = "Idle";
    public string SourceStatus { get => _sourceStatus; private set => SetProperty(ref _sourceStatus, value); }

    private string _osuState = "—";
    public string OsuState { get => _osuState; private set => SetProperty(ref _osuState, value); }

    private string _currentSong = "—";
    public string CurrentSong { get => _currentSong; private set => SetProperty(ref _currentSong, value); }

    private string _ppText = "—";
    public string PpText { get => _ppText; private set => SetProperty(ref _ppText, value); }

    private string _missesText = "—";
    public string MissesText { get => _missesText; private set => SetProperty(ref _missesText, value); }

    private string _comboText = "—";
    public string ComboText { get => _comboText; private set => SetProperty(ref _comboText, value); }

    private string _elapsedText = "—";
    public string ElapsedText { get => _elapsedText; private set => SetProperty(ref _elapsedText, value); }

    private string _lengthText = "—";
    public string LengthText { get => _lengthText; private set => SetProperty(ref _lengthText, value); }

    private string _previewText = "";
    public string PreviewText { get => _previewText; private set => SetProperty(ref _previewText, value); }

    private string _charCountText = "0 / 144";
    public string CharCountText { get => _charCountText; private set => SetProperty(ref _charCountText, value); }

    private bool _isNearLimit;
    public bool IsNearLimit { get => _isNearLimit; private set => SetProperty(ref _isNearLimit, value); }

    private string _lastSentText = "never";
    public string LastSentText { get => _lastSentText; private set => SetProperty(ref _lastSentText, value); }

    private string _destinationText = "127.0.0.1:9000";
    public string DestinationText { get => _destinationText; private set => SetProperty(ref _destinationText, value); }

    // ── Settings (two-way) ──────────────────────────────────────────
    private bool _masterEnabled = true;
    public bool MasterEnabled
    {
        get => _masterEnabled;
        set { if (SetProperty(ref _masterEnabled, value)) { if (_output is not null) _output.Enabled = value; OnPropertyChanged(nameof(OutputActive)); OnPropertyChanged(nameof(OutputStateText)); MarkDirty(); } }
    }

    public bool OutputActive => MasterEnabled;
    public string OutputStateText => MasterEnabled ? "OUTPUT ACTIVE — VRChat chatbox is being overwritten" : "Output paused";

    private int _updateIntervalSeconds = 3;
    public int UpdateIntervalSeconds
    {
        get => _updateIntervalSeconds;
        set { if (SetProperty(ref _updateIntervalSeconds, value)) { _limiter?.ConfigureGap(TimeSpan.FromSeconds(Math.Clamp(value, 2, 10))); MarkDirty(); } }
    }

    private string _tosuHost = "127.0.0.1";
    public string TosuHost { get => _tosuHost; set { if (SetProperty(ref _tosuHost, value)) MarkDirty(); } }

    private int _tosuPort = 24050;
    public int TosuPort { get => _tosuPort; set { if (SetProperty(ref _tosuPort, value)) MarkDirty(); } }

    private string _oscIp = "127.0.0.1";
    public string OscIp { get => _oscIp; set { if (SetProperty(ref _oscIp, value)) MarkDirty(); } }

    private int _oscPort = 9000;
    public int OscPort { get => _oscPort; set { if (SetProperty(ref _oscPort, value)) MarkDirty(); } }

    private bool _notificationSound;
    public bool NotificationSound
    {
        get => _notificationSound;
        set { if (SetProperty(ref _notificationSound, value)) { if (_output is not null) _output.NotificationSound = value; MarkDirty(); } }
    }

    private bool _clearOnStop = true;
    public bool ClearOnStop { get => _clearOnStop; set { if (SetProperty(ref _clearOnStop, value)) { ApplyOrchestratorOptions(); MarkDirty(); } } }

    private int _resultsDisplaySeconds = 8;
    public int ResultsDisplaySeconds { get => _resultsDisplaySeconds; set { if (SetProperty(ref _resultsDisplaySeconds, value)) { ApplyOrchestratorOptions(); MarkDirty(); } } }

    private bool _keepFinalResultVisible;
    public bool KeepFinalResultVisible { get => _keepFinalResultVisible; set { if (SetProperty(ref _keepFinalResultVisible, value)) { ApplyOrchestratorOptions(); MarkDirty(); } } }

    private MessagePreset _selectedPreset = MessagePreset.CompactAscii;
    public MessagePreset SelectedPreset { get => _selectedPreset; set { if (SetProperty(ref _selectedPreset, value)) { ApplyOrchestratorOptions(); RefreshPreview(); MarkDirty(); } } }

    private bool _useCustomTemplate;
    public bool UseCustomTemplate { get => _useCustomTemplate; set { if (SetProperty(ref _useCustomTemplate, value)) { ApplyOrchestratorOptions(); RefreshPreview(); MarkDirty(); } } }

    private string _customTemplate = "";
    public string CustomTemplate { get => _customTemplate; set { if (SetProperty(ref _customTemplate, value)) { ApplyOrchestratorOptions(); RefreshPreview(); MarkDirty(); } } }

    private bool _preferUnicode;
    public bool PreferUnicode { get => _preferUnicode; set { if (SetProperty(ref _preferUnicode, value)) { ApplyOrchestratorOptions(); RefreshPreview(); MarkDirty(); } } }

    private LengthSource _lengthSource = LengthSource.Audio;
    public LengthSource LengthSource { get => _lengthSource; set { if (SetProperty(ref _lengthSource, value)) { ApplyOrchestratorOptions(); RefreshPreview(); MarkDirty(); } } }

    private bool _startMinimized;
    public bool StartMinimized { get => _startMinimized; set { if (SetProperty(ref _startMinimized, value)) MarkDirty(); } }

    private bool _minimizeToTray = true;
    public bool MinimizeToTray { get => _minimizeToTray; set { if (SetProperty(ref _minimizeToTray, value)) MarkDirty(); } }

    private bool _startWithWindows;
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set { if (SetProperty(ref _startWithWindows, value)) { AutoStartService.Set(value); MarkDirty(); } }
    }

    private string _globalPauseHotkey = "Control+Alt+P";
    public string GlobalPauseHotkey { get => _globalPauseHotkey; set { if (SetProperty(ref _globalPauseHotkey, value)) MarkDirty(); } }

    // ── Lifecycle ───────────────────────────────────────────────────

    /// <summary>Builds and starts the pipeline. Call once after the window is ready.</summary>
    public void Start() => RebuildPipeline();

    private void RebuildPipeline()
    {
        _ = RebuildPipelineAsync();
    }

    private async Task RebuildPipelineAsync()
    {
        await TeardownPipelineAsync().ConfigureAwait(false);

        try
        {
            _limiter = new ChatboxRateLimiter(TimeSpan.FromSeconds(Math.Clamp(UpdateIntervalSeconds, 2, 10)));
            _sender = new OscUdpChatboxSender(OscIp, OscPort);
            _output = new ChatboxOutputService(_sender, _limiter) { NotificationSound = NotificationSound, Enabled = MasterEnabled };
            _source = new TosuWebSocketTelemetrySource(new TosuConnectionOptions(TosuHost, TosuPort));
            _orchestrator = new AppOrchestrator(_source, _output, _formatter, BuildOptions());

            _orchestrator.SourceStatusChanged += OnStatus;
            _orchestrator.Processed += OnProcessed;

            _dispatcher.Invoke(() => DestinationText = _sender.Destination);
            _logger.Info($"Pipeline start: tosu {TosuHost}:{TosuPort}, osc {OscIp}:{OscPort}, interval {UpdateIntervalSeconds}s");
            await _orchestrator.StartAsync(_appCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error($"Pipeline build failed: {ex.Message}");
            _dispatcher.Invoke(() => SourceStatus = $"Config error: {ex.Message}");
        }
    }

    private async Task TeardownPipelineAsync()
    {
        if (_orchestrator is not null)
        {
            _orchestrator.SourceStatusChanged -= OnStatus;
            _orchestrator.Processed -= OnProcessed;
            try { await _orchestrator.StopAsync().ConfigureAwait(false); } catch { }
            await _orchestrator.DisposeAsync().ConfigureAwait(false);
            _orchestrator = null;
        }
        _sender?.Dispose();
        _sender = null;
        _output = null;
        _source = null;
        _limiter = null;
    }

    private OrchestratorOptions BuildOptions() => new()
    {
        Timing = new TimingConfig(LengthSource, TimesAreRateAdjusted: false),
        Template = BuildTemplateConfig(),
        ClearOnStop = ClearOnStop,
        ShowResults = KeepFinalResultVisible || ResultsDisplaySeconds > 0,
        ResultsDisplaySeconds = ResultsDisplaySeconds
    };

    private TemplateConfig BuildTemplateConfig() => new(
        Preset: SelectedPreset,
        CustomTemplate: UseCustomTemplate && !string.IsNullOrWhiteSpace(CustomTemplate) ? CustomTemplate : null,
        PreferUnicode: PreferUnicode);

    private void ApplyOrchestratorOptions()
    {
        if (_orchestrator is not null) _orchestrator.Options = BuildOptions();
    }

    // ── Event handlers (marshal to UI thread) ───────────────────────
    private void OnStatus(SourceStatusEvent e)
    {
        _logger.Info($"[source] {e.Status}{(e.Detail is null ? "" : " — " + e.Detail)}");
        _dispatcher.BeginInvoke(() =>
        {
            SourceStatus = e.Detail is null ? e.Status.ToString() : $"{e.Status} — {e.Detail}";
            if (e.Status is Core.Model.SourceStatus.Disconnected or Core.Model.SourceStatus.Reconnecting)
            {
                OsuState = "—";
                CurrentSong = "—";
            }
        });
    }

    private void OnProcessed(ProcessedResult r)
    {
        _lastSnapshot = r.Snapshot;
        _dispatcher.BeginInvoke(() =>
        {
            OsuState = r.State.ToString();
            var s = r.Snapshot;
            CurrentSong = string.IsNullOrEmpty(s.Title) ? "—" : $"{s.Artist} - {s.Title} [{s.Difficulty}]";
            PpText = ((int)Math.Round(s.PpCurrent)).ToString();
            MissesText = s.Misses.ToString();
            ComboText = $"{s.Combo}x";
            ElapsedText = r.Timing.ElapsedText;
            LengthText = r.Timing.LengthText;

            if (r.Message is { } m)
            {
                PreviewText = m.Text;
                CharCountText = $"{m.CharCount} / 144";
                IsNearLimit = m.NearLimit;
            }
            if (_output?.LastSentAt is { } sent) LastSentText = sent.ToLocalTime().ToString("HH:mm:ss");
        });
    }

    /// <summary>Recomputes the preview from live data (or a sample when idle) so the editor is live.</summary>
    private void RefreshPreview()
    {
        var snap = _lastSnapshot ?? SampleSnapshot;
        var timing = TimeCalculator.Compute(snap, new TimingConfig(LengthSource, false));
        var msg = _formatter.Format(snap, timing, BuildTemplateConfig());
        PreviewText = msg.Text;
        CharCountText = $"{msg.CharCount} / 144";
        IsNearLimit = msg.NearLimit;
    }

    private static readonly GameplaySnapshot SampleSnapshot = new()
    {
        StateNumber = 2, StateName = "play",
        Artist = "Camellia", ArtistUnicode = "かめりあ",
        Title = "Ghost", TitleUnicode = "Ghost", Difficulty = "Insane",
        TimeLive = 102000, TimeLastObject = 195000, Mp3Length = 198000, Rate = 1.5,
        Misses = 2, Combo = 438, MaxCombo = 450, PpCurrent = 247.6, ModsName = "DT",
        Accuracy = 98.32, StarRating = 5.73, BeatmapChecksum = "sample"
    };

    // ── Settings persistence ────────────────────────────────────────
    private void MarkDirty()
    {
        if (_loading) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void PersistSettings()
    {
        try { _store.Save(ToSettings()); }
        catch (Exception ex) { _logger.Warn($"Settings save failed: {ex.Message}"); }
    }

    private AppSettings ToSettings() => new()
    {
        Tosu = new TosuSettings { Host = TosuHost, Port = TosuPort, AllowRemoteHost = !IsLoopback(TosuHost) },
        Osc = new OscSettings { Ip = OscIp, Port = OscPort, NotificationSound = NotificationSound },
        Output = new OutputSettings
        {
            MasterEnabled = MasterEnabled,
            UpdateIntervalSeconds = UpdateIntervalSeconds,
            ClearOnStop = ClearOnStop,
            KeepFinalResultVisible = KeepFinalResultVisible,
            ResultsDisplaySeconds = ResultsDisplaySeconds,
            LengthSource = LengthSource
        },
        Template = new TemplateSettings
        {
            Preset = SelectedPreset,
            Custom = UseCustomTemplate ? CustomTemplate : null,
            PreferUnicode = PreferUnicode
        },
        App = new AppUiSettings
        {
            StartMinimized = StartMinimized,
            StartWithWindows = StartWithWindows,
            MinimizeToTray = MinimizeToTray,
            GlobalPauseHotkey = GlobalPauseHotkey
        }
    };

    private void LoadFromSettings(AppSettings s)
    {
        _loading = true;
        TosuHost = s.Tosu.Host; TosuPort = s.Tosu.Port;
        OscIp = s.Osc.Ip; OscPort = s.Osc.Port; NotificationSound = s.Osc.NotificationSound;
        MasterEnabled = s.Output.MasterEnabled;
        UpdateIntervalSeconds = s.Output.UpdateIntervalSeconds;
        ClearOnStop = s.Output.ClearOnStop;
        KeepFinalResultVisible = s.Output.KeepFinalResultVisible;
        ResultsDisplaySeconds = s.Output.ResultsDisplaySeconds;
        LengthSource = s.Output.LengthSource;
        SelectedPreset = s.Template.Preset;
        UseCustomTemplate = !string.IsNullOrWhiteSpace(s.Template.Custom);
        CustomTemplate = s.Template.Custom ?? "";
        PreferUnicode = s.Template.PreferUnicode;
        StartMinimized = s.App.StartMinimized;
        StartWithWindows = s.App.StartWithWindows;
        MinimizeToTray = s.App.MinimizeToTray;
        GlobalPauseHotkey = s.App.GlobalPauseHotkey;
        _loading = false;
    }

    private static bool IsLoopback(string host) =>
        host is "127.0.0.1" or "localhost" or "::1" ||
        (System.Net.IPAddress.TryParse(host, out var ip) && System.Net.IPAddress.IsLoopback(ip));

    private void OpenLogs()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogsFolder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppPaths.LogsFolder,
                UseShellExecute = true
            });
        }
        catch (Exception ex) { _logger.Warn($"Open logs failed: {ex.Message}"); }
    }

    public void Dispose()
    {
        PersistSettings();
        _appCts.Cancel();
        _ = TeardownPipelineAsync();
        _appCts.Dispose();
    }
}
