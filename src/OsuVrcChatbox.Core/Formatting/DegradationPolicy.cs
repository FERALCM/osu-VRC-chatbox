namespace OsuVrcChatbox.Core.Formatting;

/// <summary>
/// Deterministic length degradation for the built-in presets. The default layout is:
/// <c>[OSU] artist - title [difficulty] stars* | -remaining | pp pp | misses miss | combo x combo</c>
/// Fields are shed in this order to fit within MaxChars:
/// difficulty → artist → misses → remaining time.
/// Dividers (<c>|</c>) are inserted only between segments that are present.
/// </summary>
public static class DegradationPolicy
{
    private const int MaxTitleTruncateIterations = 128;

    public static (string Text, bool Degraded) Fit(MessageParts parts, MessagePreset preset, int maxChars, int maxLines)
    {
        bool twoLine = preset == MessagePreset.TwoLine;

        // Degradation ladder: drop difficulty → drop artist → drop misses → drop remaining
        var ladder = new (MessageParts Parts, bool ShowMisses, bool ShowRemaining)[]
        {
            (parts, true, true),
            (parts with { Difficulty = "" }, true, true),
            (parts with { Difficulty = "", Artist = "" }, true, true),
            (parts with { Difficulty = "", Artist = "" }, false, true),
            (parts with { Difficulty = "", Artist = "" }, false, false),
        };

        for (int i = 0; i < ladder.Length; i++)
        {
            string text = Normalize(Render(ladder[i].Parts, twoLine, ladder[i].ShowMisses, ladder[i].ShowRemaining), maxLines);
            if (UnicodeText.Length(text) <= maxChars)
                return (text, Degraded: i > 0);
        }

        // Final fallback: truncate the title on the most-reduced variant until it fits.
        var reduced = ladder[^1];
        var work = reduced.Parts;
        string rendered = Normalize(Render(work, twoLine, reduced.ShowMisses, reduced.ShowRemaining), maxLines);

        for (int attempt = 0; attempt < MaxTitleTruncateIterations; attempt++)
        {
            int over = UnicodeText.Length(rendered) - maxChars;
            if (over <= 0) return (rendered, Degraded: true);

            if (work.Title.Length == 0)
                break;

            int newTitleLen = Math.Max(0, UnicodeText.Length(work.Title) - over);
            work = work with { Title = newTitleLen == 0 ? "" : UnicodeText.Truncate(work.Title, newTitleLen, withEllipsis: true) };
            rendered = Normalize(Render(work, twoLine, reduced.ShowMisses, reduced.ShowRemaining), maxLines);
        }

        string hard = UnicodeText.Truncate(rendered, maxChars, withEllipsis: true);
        return (Normalize(hard, maxLines), Degraded: true);
    }

    public static string Render(MessageParts p, bool twoLine, bool showMisses, bool showRemaining)
    {
        string head = BuildHead(p);
        string stats = BuildStats(p, showMisses, showRemaining);
        return twoLine ? $"{head}\n{stats}" : string.IsNullOrEmpty(stats) ? head : $"{head} | {stats}";
    }

    private static string BuildHead(MessageParts p)
    {
        string head = "[OSU] ";
        if (!string.IsNullOrEmpty(p.Artist))
            head += p.Artist + " - ";
        head += p.Title;
        if (!string.IsNullOrEmpty(p.Difficulty))
            head += $" [{p.Difficulty}]";
        if (!string.IsNullOrEmpty(p.StarRating))
            head += $" {p.StarRating}*";
        return head;
    }

    private static string BuildStats(MessageParts p, bool showMisses, bool showRemaining)
    {
        var segments = new List<string>();

        if (showRemaining && !string.IsNullOrEmpty(p.Remaining))
            segments.Add($"-{p.Remaining}");

        segments.Add($"{p.Pp} pp");

        if (showMisses)
            segments.Add($"{p.Misses} miss");

        segments.Add($"{p.Combo}x combo");

        return string.Join(" | ", segments);
    }

    /// <summary>Formats the results screen: two lines with song info + score stats.</summary>
    public static string RenderResults(string artist, string title, string starRating, long score, double pp, int maxCombo, int maxChars)
    {
        string scoreFormatted = score.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        string ppFormatted = ((int)Math.Round(pp, MidpointRounding.AwayFromZero)).ToString();
        string stars = !string.IsNullOrEmpty(starRating) ? $" {starRating}*" : "";
        string statsLine = $"SCORE: {scoreFormatted}, {ppFormatted}pp, {maxCombo}x";

        string full = !string.IsNullOrEmpty(artist)
            ? $"[OSU] {artist} - {title}{stars} [PASSED]\n{statsLine}"
            : $"[OSU] {title}{stars} [PASSED]\n{statsLine}";

        if (UnicodeText.Length(full) <= maxChars)
            return full;

        // Drop artist to fit
        string noArtist = $"[OSU] {title}{stars} [PASSED]\n{statsLine}";
        if (UnicodeText.Length(noArtist) <= maxChars)
            return noArtist;

        return UnicodeText.Truncate(noArtist, maxChars, withEllipsis: true);
    }

    /// <summary>Formats the failure screen: song info + failure message + misses/remaining.</summary>
    public static string RenderFailure(string artist, string title, string starRating, int misses, string remaining, int maxChars)
    {
        string stars = !string.IsNullOrEmpty(starRating) ? $" {starRating}*" : "";
        string line3 = $"{misses} miss, -{remaining} xD";

        string full = !string.IsNullOrEmpty(artist)
            ? $"[OSU] {artist} - {title}{stars}\n[FAILURE, NO PP GAINS :3c ]\n{line3}"
            : $"[OSU] {title}{stars}\n[FAILURE, NO PP GAINS :3c ]\n{line3}";

        if (UnicodeText.Length(full) <= maxChars)
            return full;

        string noArtist = $"[OSU] {title}{stars}\n[FAILURE, NO PP GAINS :3c ]\n{line3}";
        if (UnicodeText.Length(noArtist) <= maxChars)
            return noArtist;

        return UnicodeText.Truncate(noArtist, maxChars, withEllipsis: true);
    }

    private static string Normalize(string s, int maxLines) => UnicodeText.NormalizeWhitespace(s, maxLines);
}
