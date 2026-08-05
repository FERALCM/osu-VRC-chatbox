using System.Text.Json;
using OsuVrcChatbox.Core.Model;
using OsuVrcChatbox.Core.Telemetry.Dto;

namespace OsuVrcChatbox.Core.Telemetry;

/// <summary>
/// Defensive parser: raw tosu v2 JSON → normalized immutable <see cref="GameplaySnapshot"/>.
/// Tolerant of missing/unknown/null fields; never throws on structurally valid JSON that omits
/// fields we expect. Returns <c>null</c> only when the JSON itself is unparseable.
/// </summary>
public static class SnapshotParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>Parses a JSON string. Returns <c>null</c> if the text is not valid JSON.</summary>
    public static GameplaySnapshot? TryParse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        TosuV2Dto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<TosuV2Dto>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }

        return dto is null ? null : Map(dto);
    }

    /// <summary>Maps a (possibly sparse) DTO to a fully-defaulted snapshot.</summary>
    public static GameplaySnapshot Map(TosuV2Dto dto)
    {
        var beatmap = dto.Beatmap;
        var time = beatmap?.Time;
        var play = dto.Play;
        var results = dto.ResultsScreen;

        double rate = play?.Mods?.Rate ?? 0;
        if (rate <= 0) rate = 1.0; // unmodded / absent → normal clock

        return new GameplaySnapshot
        {
            StateNumber = dto.State?.Number ?? 0,
            StateName = dto.State?.Name ?? "",
            Paused = dto.Game?.Paused ?? false,
            Focused = dto.Game?.Focused ?? false,

            BeatmapChecksum = beatmap?.Checksum ?? "",
            BeatmapId = beatmap?.Id ?? 0,
            Artist = beatmap?.Artist ?? "",
            ArtistUnicode = beatmap?.ArtistUnicode ?? "",
            Title = beatmap?.Title ?? "",
            TitleUnicode = beatmap?.TitleUnicode ?? "",
            Difficulty = beatmap?.Version ?? "",
            Mapper = beatmap?.Mapper ?? "",

            TimeLive = time?.Live ?? 0,
            TimeFirstObject = time?.FirstObject ?? 0,
            TimeLastObject = time?.LastObject ?? 0,
            Mp3Length = time?.Mp3Length ?? 0,

            Failed = play?.Failed ?? false,
            Accuracy = play?.Accuracy ?? 0,
            Misses = play?.Hits?.Miss ?? 0,
            SliderBreaks = play?.Hits?.SliderBreaks ?? 0,
            Combo = play?.Combo?.Current ?? 0,
            MaxCombo = play?.Combo?.Max ?? 0,
            MapMaxCombo = beatmap?.Stats?.MaxCombo ?? 0,
            StarRating = beatmap?.Stats?.Stars?.Total ?? 0,
            PpCurrent = play?.Pp?.Current ?? 0,
            HealthBar = play?.HealthBar?.Smooth ?? 0,
            ModsName = play?.Mods?.Name ?? "",
            Rate = rate,

            Score = play?.Score ?? 0,
            ResultsScore = results?.Score ?? 0,
            ResultsPp = results?.Pp?.Current ?? 0,
            ResultsMisses = results?.Hits?.Miss ?? 0,
            ResultsMaxCombo = results?.MaxCombo ?? 0,
            ResultsAccuracy = results?.Accuracy ?? 0
        };
    }
}
