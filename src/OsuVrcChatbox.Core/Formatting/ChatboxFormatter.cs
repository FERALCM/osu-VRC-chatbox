using OsuVrcChatbox.Core.Model;
using OsuVrcChatbox.Core.Timing;

namespace OsuVrcChatbox.Core.Formatting;

/// <summary>Formats a snapshot+timing into a VRChat-safe chatbox message (plan §12).</summary>
public interface IChatboxFormatter
{
    FormattedMessage Format(GameplaySnapshot snapshot, GameplayTiming timing, TemplateConfig config);
}

/// <summary>
/// Default formatter. Built-in presets get deterministic priority degradation via
/// <see cref="DegradationPolicy"/>; custom templates get token substitution + whitespace/line
/// normalization + a grapheme-safe hard cap. Enforcement (≤ MaxChars, ≤ MaxLines) is guaranteed
/// regardless of the template.
/// </summary>
public sealed class ChatboxFormatter : IChatboxFormatter
{
    /// <summary>Warn when within this many characters of the ceiling.</summary>
    public int NearLimitThreshold { get; init; } = 14;

    public FormattedMessage Format(GameplaySnapshot snapshot, GameplayTiming timing, TemplateConfig config)
    {
        bool asciiOnly = config.Preset == MessagePreset.CompactAscii && config.CustomTemplate is null;
        var parts = MessageParts.Resolve(snapshot, timing, config.PreferUnicode, asciiOnly);

        string text;
        bool degraded;

        if (!string.IsNullOrEmpty(config.CustomTemplate))
        {
            (text, degraded) = FitCustom(config.CustomTemplate!, parts, config.MaxChars, config.MaxLines);
        }
        else
        {
            (text, degraded) = DegradationPolicy.Fit(parts, config.Preset, config.MaxChars, config.MaxLines);
        }

        int charCount = UnicodeText.Length(text);
        int lineCount = text.Length == 0 ? 0 : text.Split('\n').Length;
        bool nearLimit = charCount >= config.MaxChars - NearLimitThreshold;

        return new FormattedMessage(
            Text: text,
            CharCount: charCount,
            GraphemeCount: UnicodeText.GraphemeCount(text),
            LineCount: lineCount,
            NearLimit: nearLimit,
            Degraded: degraded);
    }

    private static (string Text, bool Degraded) FitCustom(string template, MessageParts parts, int maxChars, int maxLines)
    {
        string rendered = UnicodeText.NormalizeWhitespace(parts.ApplyTo(template), maxLines);
        if (UnicodeText.Length(rendered) <= maxChars)
            return (rendered, Degraded: false);

        // Custom templates can't be structurally degraded; guarantee the limit with a grapheme-safe cut.
        string cut = UnicodeText.NormalizeWhitespace(UnicodeText.Truncate(rendered, maxChars, withEllipsis: true), maxLines);
        return (cut, Degraded: true);
    }
}
