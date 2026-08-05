using System.Text.Json.Serialization;

namespace OsuVrcChatbox.Core.Telemetry.Dto;

/// <summary>
/// Minimal, tolerant DTO for the subset of tosu <c>/websocket/v2</c> fields this app consumes.
/// Unknown fields are ignored by System.Text.Json; every property is null-safe so a malformed or
/// partial payload never throws during mapping (see plan §2 / §20).
/// Field paths verified against tosuapp/tosu <c>packages/tosu/src/api/types/v2.ts</c> (2026-08-05).
/// </summary>
public sealed class TosuV2Dto
{
    [JsonPropertyName("state")] public NumberName? State { get; set; }
    [JsonPropertyName("game")] public GameDto? Game { get; set; }
    [JsonPropertyName("beatmap")] public BeatmapDto? Beatmap { get; set; }
    [JsonPropertyName("play")] public PlayDto? Play { get; set; }
    [JsonPropertyName("resultsScreen")] public ResultsScreenDto? ResultsScreen { get; set; }
}

public sealed class NumberName
{
    [JsonPropertyName("number")] public int Number { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public sealed class GameDto
{
    [JsonPropertyName("focused")] public bool Focused { get; set; }
    [JsonPropertyName("paused")] public bool Paused { get; set; }
}

public sealed class BeatmapDto
{
    [JsonPropertyName("checksum")] public string? Checksum { get; set; }
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("artist")] public string? Artist { get; set; }
    [JsonPropertyName("artistUnicode")] public string? ArtistUnicode { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("titleUnicode")] public string? TitleUnicode { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("mapper")] public string? Mapper { get; set; }
    [JsonPropertyName("time")] public BeatmapTimeDto? Time { get; set; }
    [JsonPropertyName("stats")] public BeatmapStatsDto? Stats { get; set; }
}

public sealed class BeatmapTimeDto
{
    [JsonPropertyName("live")] public double Live { get; set; }
    [JsonPropertyName("firstObject")] public double FirstObject { get; set; }
    [JsonPropertyName("lastObject")] public double LastObject { get; set; }
    [JsonPropertyName("mp3Length")] public double Mp3Length { get; set; }
}

public sealed class BeatmapStatsDto
{
    [JsonPropertyName("maxCombo")] public int MaxCombo { get; set; }
    [JsonPropertyName("stars")] public StarsDto? Stars { get; set; }
}

public sealed class StarsDto
{
    [JsonPropertyName("total")] public double Total { get; set; }
}

public sealed class PlayDto
{
    [JsonPropertyName("failed")] public bool Failed { get; set; }
    [JsonPropertyName("score")] public long Score { get; set; }
    [JsonPropertyName("accuracy")] public double Accuracy { get; set; }
    [JsonPropertyName("hits")] public HitsDto? Hits { get; set; }
    [JsonPropertyName("combo")] public ComboDto? Combo { get; set; }
    [JsonPropertyName("mods")] public ModsDto? Mods { get; set; }
    [JsonPropertyName("pp")] public PpDto? Pp { get; set; }
}

public sealed class HitsDto
{
    [JsonPropertyName("0")] public int Miss { get; set; }
    [JsonPropertyName("50")] public int Fifty { get; set; }
    [JsonPropertyName("100")] public int Hundred { get; set; }
    [JsonPropertyName("300")] public int ThreeHundred { get; set; }
    [JsonPropertyName("sliderBreaks")] public int SliderBreaks { get; set; }
}

public sealed class ComboDto
{
    [JsonPropertyName("current")] public int Current { get; set; }
    [JsonPropertyName("max")] public int Max { get; set; }
}

public sealed class ModsDto
{
    [JsonPropertyName("number")] public long Number { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("rate")] public double Rate { get; set; }
}

public sealed class PpDto
{
    [JsonPropertyName("current")] public double Current { get; set; }
    [JsonPropertyName("fc")] public double Fc { get; set; }
}

public sealed class ResultsScreenDto
{
    [JsonPropertyName("score")] public long Score { get; set; }
    [JsonPropertyName("accuracy")] public double Accuracy { get; set; }
    [JsonPropertyName("hits")] public HitsDto? Hits { get; set; }
    [JsonPropertyName("mods")] public ModsDto? Mods { get; set; }
    [JsonPropertyName("maxCombo")] public int MaxCombo { get; set; }
    [JsonPropertyName("pp")] public PpDto? Pp { get; set; }
}
