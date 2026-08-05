using OsuVrcChatbox.Core.Model;

namespace OsuVrcChatbox.Core.Tests;

/// <summary>
/// Synthetic tosu v2 fixtures matching the verified schema (plan §2). These stand in for recorded
/// captures until Phase-0 supplies real ones; field names/paths are taken from
/// tosuapp/tosu <c>packages/tosu/src/api/types/v2.ts</c>.
/// </summary>
internal static class Fixtures
{
    /// <summary>Active gameplay with DoubleTime (rate 1.5) and Unicode artist.</summary>
    public const string GameplayDt = """
    {
      "state": { "number": 2, "name": "play" },
      "game": { "focused": true, "paused": false },
      "beatmap": {
        "checksum": "abc123", "id": 1234,
        "artist": "Camellia", "artistUnicode": "かめりあ",
        "title": "Ghost", "titleUnicode": "Ghost",
        "mapper": "somemapper", "version": "Insane",
        "time": { "live": 102000, "firstObject": 1200, "lastObject": 195000, "mp3Length": 198000 },
        "stats": { "maxCombo": 800 }
      },
      "play": {
        "failed": false, "accuracy": 98.32,
        "hits": { "0": 2, "50": 0, "100": 5, "300": 900, "sliderBreaks": 1 },
        "combo": { "current": 438, "max": 450 },
        "mods": { "number": 64, "name": "DT", "rate": 1.5 },
        "pp": { "current": 247.6, "fc": 300.1 }
      },
      "resultsScreen": {
        "accuracy": 0, "hits": { "0": 0 }, "mods": { "name": "", "rate": 1 },
        "maxCombo": 0, "pp": { "current": 0, "fc": 0 }
      }
    }
    """;

    /// <summary>Menu state, no active map (sparse).</summary>
    public const string Menu = """
    { "state": { "number": 0, "name": "menu" }, "game": { "focused": true, "paused": false } }
    """;

    /// <summary>Results screen (state 7).</summary>
    public const string Results = """
    {
      "state": { "number": 7, "name": "resultScreen" },
      "game": { "focused": true, "paused": false },
      "beatmap": { "checksum": "abc123", "id": 1234, "artist": "Camellia", "title": "Ghost", "version": "Insane",
        "time": { "live": 195000, "firstObject": 1200, "lastObject": 195000, "mp3Length": 198000 } },
      "resultsScreen": { "accuracy": 97.5, "hits": { "0": 3 }, "mods": { "name": "DT", "rate": 1.5 },
        "maxCombo": 512, "pp": { "current": 233.0, "fc": 260.0 } }
    }
    """;

    /// <summary>A programmatically built gameplay snapshot (no JSON), for formatter/timing tests.</summary>
    public static GameplaySnapshot Gameplay(
        string artist = "Camellia", string title = "Ghost", string difficulty = "Insane",
        double live = 102000, double lastObject = 195000, double mp3Length = 198000,
        double rate = 1.5, int misses = 2, int combo = 438, int maxCombo = 450, double pp = 247.6,
        string mods = "DT", bool paused = false, bool failed = false, int stateNumber = 2,
        double starRating = 5.73, long score = 1_234_567, double healthBar = 80,
        double accuracy = 98.32) =>
        new()
        {
            StateNumber = stateNumber,
            StateName = "play",
            Paused = paused,
            Failed = failed,
            BeatmapChecksum = "abc123",
            BeatmapId = 1234,
            Artist = artist,
            ArtistUnicode = artist,
            Title = title,
            TitleUnicode = title,
            Difficulty = difficulty,
            TimeLive = live,
            TimeLastObject = lastObject,
            Mp3Length = mp3Length,
            Rate = rate,
            Misses = misses,
            Combo = combo,
            MaxCombo = maxCombo,
            MapMaxCombo = 800,
            PpCurrent = pp,
            ModsName = mods,
            Accuracy = accuracy,
            StarRating = starRating,
            Score = score,
            HealthBar = healthBar
        };
}
