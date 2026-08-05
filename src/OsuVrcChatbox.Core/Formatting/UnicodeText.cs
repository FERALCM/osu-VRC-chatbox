using System.Globalization;
using System.Text;

namespace OsuVrcChatbox.Core.Formatting;

/// <summary>
/// Unicode-safe measurement and truncation (plan §4/§12). We enforce the limit against UTF-16
/// code-unit length (<c>string.Length</c>) because that is ≥ code-point count and ≥ grapheme count —
/// so staying under 144 UTF-16 units guarantees compliance whether VRChat counts code points or
/// code units. Truncation always cuts on grapheme-cluster boundaries so a code point / combined
/// character is never split.
/// </summary>
public static class UnicodeText
{
    public const string Ellipsis = "…";

    /// <summary>The enforced length measure: UTF-16 code units.</summary>
    public static int Length(string s) => s.Length;

    /// <summary>Grapheme-cluster count (what a human reads as "characters"); for UI display.</summary>
    public static int GraphemeCount(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        int count = 0;
        var e = StringInfo.GetTextElementEnumerator(s);
        while (e.MoveNext()) count++;
        return count;
    }

    /// <summary>
    /// Truncates <paramref name="s"/> so that the result's UTF-16 length ≤ <paramref name="maxUtf16"/>,
    /// cutting only on grapheme boundaries. When truncation occurs and <paramref name="withEllipsis"/>
    /// is true, an ellipsis is appended (and counted within the budget).
    /// </summary>
    public static string Truncate(string s, int maxUtf16, bool withEllipsis = true)
    {
        if (maxUtf16 <= 0) return string.Empty;
        if (Length(s) <= maxUtf16) return s;

        int budget = withEllipsis ? maxUtf16 - Ellipsis.Length : maxUtf16;
        if (budget <= 0) return withEllipsis && maxUtf16 >= Ellipsis.Length ? Ellipsis : string.Empty;

        var sb = new StringBuilder(budget);
        int used = 0;
        var e = StringInfo.GetTextElementEnumerator(s);
        while (e.MoveNext())
        {
            string cluster = (string)e.Current;
            if (used + cluster.Length > budget) break;
            sb.Append(cluster);
            used += cluster.Length;
        }

        if (withEllipsis) sb.Append(Ellipsis);
        return sb.ToString();
    }

    /// <summary>
    /// Collapses internal whitespace runs to single spaces (per line), strips control characters,
    /// trims each line, and caps the number of lines to <paramref name="maxLines"/> (extra lines dropped).
    /// Newlines are normalized to '\n'.
    /// </summary>
    public static string NormalizeWhitespace(string s, int maxLines)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        string unified = s.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] rawLines = unified.Split('\n');

        var kept = new List<string>();
        foreach (string raw in rawLines)
        {
            var sb = new StringBuilder(raw.Length);
            bool lastWasSpace = false;
            foreach (char c in raw)
            {
                if (char.IsControl(c)) continue; // drop control chars
                bool isSpace = c == ' ' || char.IsWhiteSpace(c);
                if (isSpace)
                {
                    if (lastWasSpace) continue;
                    sb.Append(' ');
                    lastWasSpace = true;
                }
                else
                {
                    sb.Append(c);
                    lastWasSpace = false;
                }
            }
            kept.Add(sb.ToString().Trim());
            if (kept.Count >= maxLines) break;
        }

        return string.Join('\n', kept);
    }

    /// <summary>Removes non-ASCII characters (keeps printable 0x20–0x7E), collapsing gaps.</summary>
    public static string ToAscii(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (c >= 0x20 && c <= 0x7E) sb.Append(c);
        }
        return sb.ToString().Trim();
    }
}
