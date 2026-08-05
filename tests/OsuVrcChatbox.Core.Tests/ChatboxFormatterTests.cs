using OsuVrcChatbox.Core.Formatting;
using OsuVrcChatbox.Core.Timing;
using Xunit;

namespace OsuVrcChatbox.Core.Tests;

public class ChatboxFormatterTests
{
    private static readonly ChatboxFormatter Formatter = new();
    private static readonly TimingConfig Timing = new(LengthSource.Audio);

    private static FormattedMessage Format(MessagePreset preset, int maxChars = 144,
        string artist = "Camellia", string title = "Ghost", string difficulty = "Insane")
    {
        var s = Fixtures.Gameplay(artist: artist, title: title, difficulty: difficulty);
        var t = TimeCalculator.Compute(s, Timing);
        return Formatter.Format(s, t, new TemplateConfig(preset, MaxChars: maxChars));
    }

    [Fact]
    public void Compact_one_line_matches_expected_layout()
    {
        var m = Format(MessagePreset.CompactOneLine);
        var lines = m.Text.Split('\n');
        Assert.Equal("[OSU] Camellia - Ghost [Insane] 5.73* | -1:04 | 248 pp | 2 miss | 438x combo | 98.32% ✓ | 80♥", lines[0]);
        Assert.Equal("1,234,567", lines[1]);
        Assert.Contains("5.73*", m.Text);
        Assert.False(m.Degraded);
        Assert.Equal(2, m.LineCount);
    }

    [Fact]
    public void Two_line_preset_produces_three_lines_with_score()
    {
        var m = Format(MessagePreset.TwoLine);
        var lines = m.Text.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Contains("[Insane]", lines[0]);
        Assert.Contains("5.73*", lines[0]);
        Assert.Contains("438x combo", lines[1]);
        Assert.Contains("98.32% ✓", lines[1]);
        Assert.Equal("1,234,567", lines[2]);
    }

    [Fact]
    public void Ascii_preset_strips_non_ascii_metadata()
    {
        var s = Fixtures.Gameplay(artist: "かめりあ", title: "GHOST");
        var t = TimeCalculator.Compute(s, Timing);
        var m = Formatter.Format(s, t, new TemplateConfig(MessagePreset.CompactAscii));

        Assert.All(m.Text, c => Assert.True(c < 0x80, $"non-ascii char U+{(int)c:X4}"));
        Assert.Contains("GHOST", m.Text);
    }

    [Fact]
    public void Never_exceeds_max_chars_across_a_range_of_limits()
    {
        for (int max = 10; max <= 144; max++)
        {
            var m = Format(MessagePreset.CompactOneLine, maxChars: max,
                title: "A Very Long Title That Will Definitely Need To Be Shortened Repeatedly");
            Assert.True(m.CharCount <= max, $"len {m.CharCount} > max {max}");
        }
    }

    [Fact]
    public void Degrades_in_correct_order()
    {
        // Full output: all fields present
        var full = Format(MessagePreset.CompactOneLine, maxChars: 1000,
            artist: "AAAA", title: "BBBBBBBBBBBBBBBBBBBB", difficulty: "CCCC");
        Assert.Contains("[CCCC]", full.Text);
        Assert.Contains("80♥", full.Text);
        Assert.Contains("98.32% ✓", full.Text);
        Assert.Contains("1,234,567", full.Text);
        Assert.Contains("AAAA", full.Text);
        Assert.False(full.Degraded);

        // Step 1: difficulty dropped
        int fullLen = full.CharCount;
        var noDiff = Format(MessagePreset.CompactOneLine, maxChars: fullLen - 1,
            artist: "AAAA", title: "BBBBBBBBBBBBBBBBBBBB", difficulty: "CCCC");
        Assert.DoesNotContain("[CCCC]", noDiff.Text);
        Assert.Contains("80♥", noDiff.Text);
        Assert.Contains("98.32% ✓", noDiff.Text);
        Assert.True(noDiff.Degraded);

        // Step 2: health dropped
        int noDiffLen = noDiff.CharCount;
        var noHealth = Format(MessagePreset.CompactOneLine, maxChars: noDiffLen - 1,
            artist: "AAAA", title: "BBBBBBBBBBBBBBBBBBBB", difficulty: "CCCC");
        Assert.DoesNotContain("80♥", noHealth.Text);
        Assert.Contains("98.32% ✓", noHealth.Text);
        Assert.Contains("1,234,567", noHealth.Text);

        // Step 3: accuracy dropped
        int noHealthLen = noHealth.CharCount;
        var noAcc = Format(MessagePreset.CompactOneLine, maxChars: noHealthLen - 1,
            artist: "AAAA", title: "BBBBBBBBBBBBBBBBBBBB", difficulty: "CCCC");
        Assert.DoesNotContain("98.32% ✓", noAcc.Text);
        Assert.Contains("1,234,567", noAcc.Text);

        // Step 4: score dropped
        int noAccLen = noAcc.CharCount;
        var noScore = Format(MessagePreset.CompactOneLine, maxChars: noAccLen - 1,
            artist: "AAAA", title: "BBBBBBBBBBBBBBBBBBBB", difficulty: "CCCC");
        Assert.DoesNotContain("1,234,567", noScore.Text);
        Assert.Contains("AAAA", noScore.Text);

        // Step 5: artist dropped
        int noScoreLen = noScore.CharCount;
        var noArtist = Format(MessagePreset.CompactOneLine, maxChars: noScoreLen - 1,
            artist: "AAAA", title: "BBBBBBBBBBBBBBBBBBBB", difficulty: "CCCC");
        Assert.DoesNotContain("AAAA", noArtist.Text);
        Assert.Contains("2 miss", noArtist.Text);
        Assert.Contains("438x combo", noArtist.Text);
    }

    [Fact]
    public void Degrades_by_removing_artist_then_misses()
    {
        // maxChars 64: artist dropped (no-artist variant is 65, so need to also drop misses at 56)
        var m = Format(MessagePreset.CompactOneLine, maxChars: 64,
            artist: "AAAA", title: "BBBBBBBBBBBBBBBBBBBB", difficulty: "CCCC");
        Assert.DoesNotContain("AAAA", m.Text);       // artist dropped
        Assert.DoesNotContain("[CCCC]", m.Text);      // difficulty dropped
        Assert.Contains("BBBBBBBBBBBBBBBBBBBB", m.Text); // title still whole
        Assert.Contains("438x combo", m.Text);
        Assert.True(m.CharCount <= 64);
    }

    [Fact]
    public void Final_fallback_truncates_title_but_keeps_core_stats()
    {
        var m = Format(MessagePreset.CompactOneLine, maxChars: 40,
            artist: "AAAA", title: "BBBBBBBBBBBBBBBBBBBB", difficulty: "CCCC");
        Assert.True(m.CharCount <= 40);
        Assert.Contains("438x combo", m.Text);        // combo preserved
        Assert.Contains("248 pp", m.Text);             // pp preserved
        Assert.DoesNotContain("BBBBBBBBBBBBBBBBBBBB", m.Text); // title was shortened
        Assert.True(m.Degraded);
    }

    [Fact]
    public void Unicode_truncation_never_splits_a_surrogate_pair()
    {
        var s = Fixtures.Gameplay(artist: "", title: string.Concat(Enumerable.Repeat("😀", 40)));
        var t = TimeCalculator.Compute(s, Timing);

        for (int max = 30; max <= 60; max++)
        {
            var m = Formatter.Format(s, t, new TemplateConfig(MessagePreset.CompactOneLine, MaxChars: max));
            Assert.True(m.CharCount <= max);
            Assert.False(EndsWithLoneHighSurrogate(m.Text), $"lone surrogate at max {max}");
            Assert.False(HasUnpairedSurrogate(m.Text), $"unpaired surrogate at max {max}");
        }
    }

    [Fact]
    public void Custom_template_substitutes_tokens_and_enforces_limit()
    {
        var s = Fixtures.Gameplay();
        var t = TimeCalculator.Compute(s, Timing);
        var m = Formatter.Format(s, t, new TemplateConfig(CustomTemplate: "{title} {combo}x {pp}pp"));
        Assert.Equal("Ghost 438x 248pp", m.Text);
    }

    [Fact]
    public void Custom_template_caps_line_count_at_nine()
    {
        var s = Fixtures.Gameplay();
        var t = TimeCalculator.Compute(s, Timing);
        string many = string.Join("", Enumerable.Range(0, 20).Select(i => $"line{i}\n"));
        var m = Formatter.Format(s, t, new TemplateConfig(CustomTemplate: many, MaxLines: 9));
        Assert.True(m.LineCount <= 9);
    }

    [Fact]
    public void Near_limit_flag_trips_close_to_ceiling()
    {
        var m = Format(MessagePreset.CompactOneLine, maxChars: 66,
            title: "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB");
        Assert.True(m.NearLimit || m.CharCount < 66 - 14);
    }

    [Fact]
    public void RenderResults_produces_two_line_passed_format()
    {
        string text = DegradationPolicy.RenderResults("Smi x mei", "numb numb", "5.73", 1_234_567, 142.3, 438, 144);
        Assert.Contains("[PASSED]", text);
        Assert.Contains("5.73*", text);
        var lines = text.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Contains("SCORE:", lines[1]);
        Assert.Contains("1,234,567", lines[1]);
        Assert.Contains("142pp", lines[1]);
        Assert.Contains("438x", lines[1]);
    }

    [Fact]
    public void RenderResults_drops_artist_when_over_limit()
    {
        string text = DegradationPolicy.RenderResults("A Very Long Artist Name", "A Very Long Title", "5.73", 999, 100, 100, 60);
        Assert.DoesNotContain("A Very Long Artist", text);
        Assert.Contains("[PASSED]", text);
    }

    [Fact]
    public void RenderFailure_produces_three_line_format()
    {
        string text = DegradationPolicy.RenderFailure("Smi x mei", "numb numb", "5.73", 14, "1:04", 144);
        var lines = text.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Contains("5.73*", lines[0]);
        Assert.Contains("[FAILURE, NO PP GAINS :3]", lines[1]);
        Assert.Contains("14 miss", lines[2]);
        Assert.Contains("-1:04 xD", lines[2]);
    }

    [Fact]
    public void RenderFailure_drops_artist_when_over_limit()
    {
        string text = DegradationPolicy.RenderFailure("A Very Long Artist Name", "Title", "5.73", 2, "0:30", 60);
        Assert.DoesNotContain("A Very Long Artist", text);
        Assert.Contains("[FAILURE", text);
    }

    private static bool EndsWithLoneHighSurrogate(string s) =>
        s.Length > 0 && char.IsHighSurrogate(s[^1]);

    private static bool HasUnpairedSurrogate(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]))
            {
                if (i + 1 >= s.Length || !char.IsLowSurrogate(s[i + 1])) return true;
                i++;
            }
            else if (char.IsLowSurrogate(s[i]))
            {
                return true;
            }
        }
        return false;
    }
}
