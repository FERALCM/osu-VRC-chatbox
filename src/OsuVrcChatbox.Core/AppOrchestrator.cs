using System.Diagnostics;
using System.Globalization;
using OsuVrcChatbox.Core.Formatting;
using OsuVrcChatbox.Core.Model;
using OsuVrcChatbox.Core.Scheduling;
using OsuVrcChatbox.Core.State;
using OsuVrcChatbox.Core.Telemetry;
using OsuVrcChatbox.Core.Timing;

namespace OsuVrcChatbox.Core;

public sealed record OrchestratorOptions
{
    public TimingConfig Timing { get; init; } = new();
    public TemplateConfig Template { get; init; } = new();
    public bool ClearOnStop { get; init; } = true;
    public bool ShowResults { get; init; } = true;
    public int ResultsDisplaySeconds { get; init; } = 10;
}

public readonly record struct ProcessedResult(
    GameplaySnapshot Snapshot,
    GameplayTiming Timing,
    AppGameState State,
    bool MapChanged,
    FormattedMessage? Message);

public sealed class AppOrchestrator : IAsyncDisposable
{
    private readonly IOsuTelemetrySource _source;
    private readonly ChatboxOutputService _output;
    private readonly IChatboxFormatter _formatter;
    private readonly GameStateMachine _stateMachine = new();
    private readonly Stopwatch _endScreenTimer = new();

    private const double FailureHoldSeconds = 5;

    private OrchestratorOptions _options;
    private bool _wasSending;
    private bool _showingEndScreen;
    private bool _failureLocked;

    public event Action<ProcessedResult>? Processed;
    public event Action<SourceStatusEvent>? SourceStatusChanged;

    public AppOrchestrator(
        IOsuTelemetrySource source,
        ChatboxOutputService output,
        IChatboxFormatter formatter,
        OrchestratorOptions options)
    {
        _source = source;
        _output = output;
        _formatter = formatter;
        _options = options;

        _source.SnapshotReceived += OnSnapshot;
        _source.StatusChanged += OnStatus;
    }

    public OrchestratorOptions Options
    {
        get => _options;
        set => _options = value;
    }

    public ChatboxOutputService Output => _output;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _output.StartAsync(cancellationToken).ConfigureAwait(false);
        await _source.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync()
    {
        await _source.StopAsync().ConfigureAwait(false);
        _output.RequestClear();
        await _output.StopAsync().ConfigureAwait(false);
    }

    private void OnStatus(SourceStatusEvent e)
    {
        if (e.Status is SourceStatus.Disconnected or SourceStatus.Reconnecting or SourceStatus.Stopped)
        {
            _stateMachine.Reset();
            if (_wasSending && _options.ClearOnStop) _output.RequestClear();
            _wasSending = false;
            _showingEndScreen = false;
            _failureLocked = false;
            _endScreenTimer.Reset();
        }
        SourceStatusChanged?.Invoke(e);
    }

    private void OnSnapshot(GameplaySnapshot snapshot)
    {
        StateEvaluation eval = _stateMachine.Evaluate(snapshot);
        GameplayTiming timing = TimeCalculator.Compute(snapshot, _options.Timing);

        FormattedMessage? message = null;
        bool isEndScreen = eval.State is AppGameState.Results or AppGameState.Failed;

        if (isEndScreen && _options.ShowResults)
        {
            if (!_showingEndScreen)
            {
                _showingEndScreen = true;
                _endScreenTimer.Restart();
            }

            if (_endScreenTimer.Elapsed.TotalSeconds <= _options.ResultsDisplaySeconds)
            {
                bool isFailed = eval.State == AppGameState.Failed;

                if (isFailed && _failureLocked && _endScreenTimer.Elapsed.TotalSeconds <= FailureHoldSeconds)
                {
                    // Hold the failure message — don't update or resubmit
                    _wasSending = true;
                }
                else
                {
                    if (isFailed && !_failureLocked)
                        _failureLocked = true;

                    string text = isFailed
                        ? BuildFailureText(snapshot, timing)
                        : BuildResultsText(snapshot);

                    message = MakeMessage(text, _options.Template.MaxChars);
                    _output.Submit(text, urgent: !_wasSending);
                    _wasSending = true;
                }
            }
            else
            {
                if (_wasSending && _options.ClearOnStop) _output.RequestClear();
                _wasSending = false;
            }
        }
        else
        {
            if (_showingEndScreen)
            {
                _showingEndScreen = false;
                _failureLocked = false;
                _endScreenTimer.Reset();
            }

            bool sending = eval.State is AppGameState.GameplayActive or AppGameState.GameplayPaused;

            if (sending)
            {
                message = _formatter.Format(snapshot, timing, _options.Template);
                _output.Submit(message.Value.Text, urgent: eval.MapChanged);
            }
            else if (_wasSending && _options.ClearOnStop)
            {
                _output.RequestClear();
            }

            _wasSending = sending;
        }

        Processed?.Invoke(new ProcessedResult(snapshot, timing, eval.State, eval.MapChanged, message));
    }

    private string BuildResultsText(GameplaySnapshot s)
    {
        string starRating = s.StarRating > 0
            ? s.StarRating.ToString("0.##", CultureInfo.InvariantCulture) : "";
        long score = s.ResultsScore > 0 ? s.ResultsScore : s.Score;
        double pp = s.ResultsPp > 0 ? s.ResultsPp : s.PpCurrent;
        int combo = s.ResultsMaxCombo > 0 ? s.ResultsMaxCombo : s.MaxCombo;

        return DegradationPolicy.RenderResults(
            s.Artist, s.Title, starRating, score, pp, combo, _options.Template.MaxChars);
    }

    private string BuildFailureText(GameplaySnapshot s, GameplayTiming timing)
    {
        string starRating = s.StarRating > 0
            ? s.StarRating.ToString("0.##", CultureInfo.InvariantCulture) : "";

        return DegradationPolicy.RenderFailure(
            s.Artist, s.Title, starRating, s.Misses, timing.RemainingText, _options.Template.MaxChars);
    }

    private static FormattedMessage MakeMessage(string text, int maxChars)
    {
        int lineCount = text.Length == 0 ? 0 : text.Split('\n').Length;
        return new FormattedMessage(
            Text: text,
            CharCount: UnicodeText.Length(text),
            GraphemeCount: UnicodeText.GraphemeCount(text),
            LineCount: lineCount,
            NearLimit: UnicodeText.Length(text) >= maxChars - 14,
            Degraded: false);
    }

    public async ValueTask DisposeAsync()
    {
        _source.SnapshotReceived -= OnSnapshot;
        _source.StatusChanged -= OnStatus;
        await _source.DisposeAsync().ConfigureAwait(false);
        _output.Dispose();
    }
}
