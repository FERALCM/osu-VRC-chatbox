using OsuVrcChatbox.Core.Model;

namespace OsuVrcChatbox.Core.State;

/// <summary>High-level app view of osu! gameplay, derived from tosu <c>state.number</c> + flags (plan §11).</summary>
public enum AppGameState
{
    OsuNotRunning,
    Menu,
    SongSelect,
    GameplayActive,
    GameplayPaused,
    Failed,
    Results,
    Other
}

/// <summary>Result of classifying one snapshot.</summary>
/// <param name="State">Derived app state.</param>
/// <param name="MapChanged">True when the beatmap identity changed since the previous snapshot.</param>
/// <param name="ShouldSendContinuous">True only during uninterrupted active gameplay.</param>
public readonly record struct StateEvaluation(AppGameState State, bool MapChanged, bool ShouldSendContinuous);

/// <summary>
/// Stateful classifier. Uses numeric <c>state.number</c> (not the display name) for logic, and
/// tracks the last beatmap identity to flag map changes. Not thread-safe: drive it from the single
/// ingestion loop (plan §7).
/// </summary>
public sealed class GameStateMachine
{
    // osu! GameState enum indices (packages/common/enums/osu.ts).
    private const int StateMenu = 0;
    private const int StatePlay = 2;
    private const int StateSelectPlay = 5;
    private const int StateResultScreen = 7;

    private string? _lastMapKey;

    /// <summary>Clears map-change tracking (e.g. after a disconnect).</summary>
    public void Reset() => _lastMapKey = null;

    public StateEvaluation Evaluate(GameplaySnapshot? snapshot)
    {
        if (snapshot is null)
        {
            _lastMapKey = null;
            return new StateEvaluation(AppGameState.OsuNotRunning, MapChanged: false, ShouldSendContinuous: false);
        }

        var state = Classify(snapshot);
        bool mapChanged = DetectMapChange(snapshot);
        bool continuous = state == AppGameState.GameplayActive;
        return new StateEvaluation(state, mapChanged, continuous);
    }

    private static AppGameState Classify(GameplaySnapshot s) => s.StateNumber switch
    {
        StatePlay when s.Failed => AppGameState.Failed,
        StatePlay when s.Paused => AppGameState.GameplayPaused,
        StatePlay => AppGameState.GameplayActive,
        StateResultScreen => AppGameState.Results,
        StateSelectPlay => AppGameState.SongSelect,
        StateMenu => AppGameState.Menu,
        _ => AppGameState.Other
    };

    private bool DetectMapChange(GameplaySnapshot s)
    {
        string key = !string.IsNullOrEmpty(s.BeatmapChecksum)
            ? s.BeatmapChecksum
            : $"{s.BeatmapId}|{s.Difficulty}";

        // No identity yet (menu with no map) → not a change.
        if (string.IsNullOrEmpty(key) || key == "0|")
        {
            _lastMapKey = null;
            return false;
        }

        bool changed = _lastMapKey is not null && _lastMapKey != key;
        bool firstSeen = _lastMapKey is null;
        _lastMapKey = key;
        // First time we ever see a map counts as a change worth an immediate send.
        return changed || firstSeen;
    }
}
