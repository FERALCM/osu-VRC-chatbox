namespace OsuVrcChatbox.Core.Model;

/// <summary>
/// Immutable, normalized snapshot of osu! state derived from one tosu /websocket/v2 message.
/// All time values are in milliseconds on the beatmap timeline exactly as tosu reports them
/// (see plan §10 — rate-adjustment is applied later by <c>TimeCalculator</c>, not here).
/// </summary>
public sealed record GameplaySnapshot
{
    // ── Game / state ───────────────────────────────────────────────
    /// <summary>tosu <c>state.number</c> (osu! GameState enum; 2 = play, 5 = selectPlay, 7 = resultScreen).</summary>
    public int StateNumber { get; init; }
    public string StateName { get; init; } = "";
    public bool Paused { get; init; }
    public bool Focused { get; init; }

    // ── Beatmap ────────────────────────────────────────────────────
    public string BeatmapChecksum { get; init; } = "";
    public int BeatmapId { get; init; }
    public string Artist { get; init; } = "";
    public string ArtistUnicode { get; init; } = "";
    public string Title { get; init; } = "";
    public string TitleUnicode { get; init; } = "";
    public string Difficulty { get; init; } = "";
    public string Mapper { get; init; } = "";

    // ── Times (ms, beatmap timeline as reported by tosu) ────────────
    public double TimeLive { get; init; }
    public double TimeFirstObject { get; init; }
    public double TimeLastObject { get; init; }
    public double Mp3Length { get; init; }

    // ── Live play ──────────────────────────────────────────────────
    public bool Failed { get; init; }
    public double Accuracy { get; init; }
    /// <summary>Miss count for the current play (tosu <c>play.hits["0"]</c>).</summary>
    public int Misses { get; init; }
    public int SliderBreaks { get; init; }
    public int Combo { get; init; }
    /// <summary>Max combo achieved so far this play (tosu <c>play.combo.max</c>).</summary>
    public int MaxCombo { get; init; }
    /// <summary>The map's full-combo value (tosu <c>beatmap.stats.maxCombo</c>).</summary>
    public int MapMaxCombo { get; init; }
    /// <summary>Star rating (tosu <c>beatmap.stats.stars.total</c>).</summary>
    public double StarRating { get; init; }
    public double PpCurrent { get; init; }
    /// <summary>Player health bar (tosu <c>play.healthBar.smooth</c>); 0–100 range.</summary>
    public double HealthBar { get; init; }
    public string ModsName { get; init; } = "";
    /// <summary>Effective clock-rate multiplier (tosu <c>play.mods.rate</c>); 1.0 when unmodded/absent.</summary>
    public double Rate { get; init; } = 1.0;

    // ── Results screen (state.number == 7) ─────────────────────────
    public long Score { get; init; }
    public long ResultsScore { get; init; }
    public double ResultsPp { get; init; }
    public int ResultsMisses { get; init; }
    public int ResultsMaxCombo { get; init; }
    public double ResultsAccuracy { get; init; }

    /// <summary>An all-default snapshot representing "no data".</summary>
    public static GameplaySnapshot Empty { get; } = new();
}
