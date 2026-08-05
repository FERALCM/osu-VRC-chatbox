using System.Globalization;
using OsuVrcChatbox.Core.Model;
using OsuVrcChatbox.Core.Timing;

namespace OsuVrcChatbox.Core.Formatting;

/// <summary>Resolved, display-ready token values for one message (plan §9 mapping).</summary>
public sealed record MessageParts
{
    public string Artist { get; init; } = "";
    public string Title { get; init; } = "";
    public string Difficulty { get; init; } = "";
    public string Elapsed { get; init; } = "";
    public string Remaining { get; init; } = "";
    public string Length { get; init; } = "";
    public string Pp { get; init; } = "";
    public string Misses { get; init; } = "";
    public string Combo { get; init; } = "";
    public string MaxCombo { get; init; } = "";
    public string Mods { get; init; } = "";
    public string Accuracy { get; init; } = "";
    public string StarRating { get; init; } = "";
    public string Score { get; init; } = "";
    public string Health { get; init; } = "";

    /// <summary>Builds token values from a snapshot + computed timing.</summary>
    public static MessageParts Resolve(GameplaySnapshot s, GameplayTiming timing, bool preferUnicode, bool asciiOnly)
    {
        string artist = preferUnicode && !asciiOnly && !string.IsNullOrEmpty(s.ArtistUnicode) ? s.ArtistUnicode : s.Artist;
        string title = preferUnicode && !asciiOnly && !string.IsNullOrEmpty(s.TitleUnicode) ? s.TitleUnicode : s.Title;
        string difficulty = s.Difficulty;

        if (asciiOnly)
        {
            artist = UnicodeText.ToAscii(artist);
            title = UnicodeText.ToAscii(title);
            difficulty = UnicodeText.ToAscii(difficulty);
        }

        return new MessageParts
        {
            Artist = artist,
            Title = title,
            Difficulty = difficulty,
            Elapsed = timing.ElapsedText,
            Remaining = timing.RemainingText,
            Length = timing.LengthText,
            Pp = ((int)Math.Round(s.PpCurrent, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture),
            Misses = s.Misses.ToString(CultureInfo.InvariantCulture),
            Combo = s.Combo.ToString(CultureInfo.InvariantCulture),
            MaxCombo = s.MaxCombo.ToString(CultureInfo.InvariantCulture),
            Mods = string.IsNullOrEmpty(s.ModsName) ? "" : s.ModsName,
            Accuracy = s.Accuracy.ToString("0.##", CultureInfo.InvariantCulture) + (asciiOnly ? "% acc" : "% ✓"),
            StarRating = s.StarRating.ToString("0.##", CultureInfo.InvariantCulture),
            Score = s.Score.ToString("N0", CultureInfo.InvariantCulture),
            Health = ((int)Math.Round(s.HealthBar, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture) + (asciiOnly ? "<3" : "♥")
        };
    }

    /// <summary>Substitutes <c>{token}</c> placeholders in a custom template (case-insensitive).</summary>
    public string ApplyTo(string template)
    {
        return template
            .Replace("{artist}", Artist, StringComparison.OrdinalIgnoreCase)
            .Replace("{title}", Title, StringComparison.OrdinalIgnoreCase)
            .Replace("{difficulty}", Difficulty, StringComparison.OrdinalIgnoreCase)
            .Replace("{elapsed}", Elapsed, StringComparison.OrdinalIgnoreCase)
            .Replace("{remaining}", Remaining, StringComparison.OrdinalIgnoreCase)
            .Replace("{length}", Length, StringComparison.OrdinalIgnoreCase)
            .Replace("{pp}", Pp, StringComparison.OrdinalIgnoreCase)
            .Replace("{misses}", Misses, StringComparison.OrdinalIgnoreCase)
            .Replace("{combo}", Combo, StringComparison.OrdinalIgnoreCase)
            .Replace("{maxcombo}", MaxCombo, StringComparison.OrdinalIgnoreCase)
            .Replace("{mods}", Mods, StringComparison.OrdinalIgnoreCase)
            .Replace("{accuracy}", Accuracy, StringComparison.OrdinalIgnoreCase)
            .Replace("{starrating}", StarRating, StringComparison.OrdinalIgnoreCase)
            .Replace("{score}", Score, StringComparison.OrdinalIgnoreCase)
            .Replace("{health}", Health, StringComparison.OrdinalIgnoreCase);
    }
}
