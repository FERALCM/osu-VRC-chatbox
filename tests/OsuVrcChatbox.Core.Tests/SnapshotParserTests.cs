using OsuVrcChatbox.Core.Telemetry;
using Xunit;

namespace OsuVrcChatbox.Core.Tests;

public class SnapshotParserTests
{
    [Fact]
    public void Parses_full_gameplay_fixture()
    {
        var s = SnapshotParser.TryParse(Fixtures.GameplayDt);

        Assert.NotNull(s);
        Assert.Equal(2, s!.StateNumber);
        Assert.False(s.Paused);
        Assert.Equal("Camellia", s.Artist);
        Assert.Equal("かめりあ", s.ArtistUnicode);
        Assert.Equal("Ghost", s.Title);
        Assert.Equal("Insane", s.Difficulty);
        Assert.Equal(102000, s.TimeLive);
        Assert.Equal(195000, s.TimeLastObject);
        Assert.Equal(198000, s.Mp3Length);
        Assert.Equal(2, s.Misses);            // play.hits["0"]
        Assert.Equal(1, s.SliderBreaks);
        Assert.Equal(438, s.Combo);
        Assert.Equal(450, s.MaxCombo);
        Assert.Equal(800, s.MapMaxCombo);
        Assert.Equal(247.6, s.PpCurrent, 3);
        Assert.Equal("DT", s.ModsName);
        Assert.Equal(1.5, s.Rate, 3);
    }

    [Fact]
    public void Parses_sparse_menu_without_throwing()
    {
        var s = SnapshotParser.TryParse(Fixtures.Menu);

        Assert.NotNull(s);
        Assert.Equal(0, s!.StateNumber);
        Assert.Equal("", s.Artist);
        Assert.Equal(0, s.Misses);
        Assert.Equal(1.0, s.Rate); // absent rate defaults to normal clock
    }

    [Fact]
    public void Unmodded_missing_rate_defaults_to_one()
    {
        var s = SnapshotParser.TryParse("""{ "state": {"number":2}, "play": { "mods": { "name": "" } } }""");
        Assert.Equal(1.0, s!.Rate);
    }

    [Fact]
    public void Zero_or_negative_rate_is_normalized_to_one()
    {
        var s = SnapshotParser.TryParse("""{ "play": { "mods": { "rate": 0 } } }""");
        Assert.Equal(1.0, s!.Rate);
    }

    [Fact]
    public void Unknown_fields_are_ignored()
    {
        var s = SnapshotParser.TryParse("""{ "state": {"number":2}, "somethingNew": {"x":1}, "beatmap": {"artist":"A","unknown":true} }""");
        Assert.NotNull(s);
        Assert.Equal("A", s!.Artist);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{ broken")]
    public void Malformed_input_returns_null(string input)
    {
        Assert.Null(SnapshotParser.TryParse(input));
    }

    [Fact]
    public void Parses_results_screen()
    {
        var s = SnapshotParser.TryParse(Fixtures.Results);
        Assert.Equal(7, s!.StateNumber);
        Assert.Equal(233.0, s.ResultsPp, 3);
        Assert.Equal(3, s.ResultsMisses);
        Assert.Equal(512, s.ResultsMaxCombo);
    }
}
