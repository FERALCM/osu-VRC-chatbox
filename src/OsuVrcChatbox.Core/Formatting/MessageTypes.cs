namespace OsuVrcChatbox.Core.Formatting;

/// <summary>Built-in layout presets (plan §12).</summary>
public enum MessagePreset
{
    /// <summary>Single line, Unicode metadata allowed.</summary>
    CompactOneLine,

    /// <summary>Two lines: metadata on line 1, stats on line 2.</summary>
    TwoLine,

    /// <summary>Single line, ASCII-only metadata. Safe default for the MVP.</summary>
    CompactAscii
}

/// <summary>VRChat chatbox limits and preset/template selection.</summary>
/// <param name="Preset">Which built-in layout to use when <paramref name="CustomTemplate"/> is null.</param>
/// <param name="CustomTemplate">Optional raw template with tokens; overrides <paramref name="Preset"/>.</param>
/// <param name="PreferUnicode">Prefer *Unicode metadata fields when the preset is not ASCII-only.</param>
/// <param name="MaxChars">Hard character ceiling (UTF-16 units). VRChat = 144.</param>
/// <param name="MaxLines">Hard line ceiling. VRChat = 9.</param>
public readonly record struct TemplateConfig(
    MessagePreset Preset = MessagePreset.CompactAscii,
    string? CustomTemplate = null,
    bool PreferUnicode = false,
    int MaxChars = 144,
    int MaxLines = 9);

/// <summary>Output of the formatter, ready to send and to preview.</summary>
/// <param name="Text">Final enforced text (≤ MaxChars UTF-16 units, ≤ MaxLines lines).</param>
/// <param name="CharCount">UTF-16 length of <paramref name="Text"/> (the enforced measure).</param>
/// <param name="GraphemeCount">Human-facing character count.</param>
/// <param name="LineCount">Number of lines.</param>
/// <param name="NearLimit">True when within the warning threshold of MaxChars.</param>
/// <param name="Degraded">True when any field was dropped or the title was truncated to fit.</param>
public readonly record struct FormattedMessage(
    string Text,
    int CharCount,
    int GraphemeCount,
    int LineCount,
    bool NearLimit,
    bool Degraded);
