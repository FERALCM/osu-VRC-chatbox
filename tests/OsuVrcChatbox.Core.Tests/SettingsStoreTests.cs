using OsuVrcChatbox.Core.Formatting;
using OsuVrcChatbox.Core.Settings;
using OsuVrcChatbox.Core.Timing;
using Xunit;

namespace OsuVrcChatbox.Core.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public SettingsStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "osu-vrc-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "settings.json");
    }

    [Fact]
    public void Missing_file_loads_defaults()
    {
        var s = new SettingsStore(_path).Load();
        Assert.Equal(AppSettings.CurrentSchemaVersion, s.SchemaVersion);
        Assert.Equal(24050, s.Tosu.Port);
        Assert.Equal(9000, s.Osc.Port);
        Assert.Equal(MessagePreset.CompactAscii, s.Template.Preset);
    }

    [Fact]
    public void Save_then_load_round_trips()
    {
        var store = new SettingsStore(_path);
        var settings = new AppSettings
        {
            Osc = new OscSettings { Ip = "127.0.0.1", Port = 9001, NotificationSound = true },
            Output = new OutputSettings { UpdateIntervalSeconds = 5, LengthSource = LengthSource.LastObject },
            Template = new TemplateSettings { Preset = MessagePreset.TwoLine }
        };
        store.Save(settings);

        var loaded = store.Load();
        Assert.Equal(9001, loaded.Osc.Port);
        Assert.True(loaded.Osc.NotificationSound);
        Assert.Equal(5, loaded.Output.UpdateIntervalSeconds);
        Assert.Equal(LengthSource.LastObject, loaded.Output.LengthSource);
        Assert.Equal(MessagePreset.TwoLine, loaded.Template.Preset);
    }

    [Fact]
    public void Enums_are_written_as_strings()
    {
        new SettingsStore(_path).Save(new AppSettings());
        string json = File.ReadAllText(_path);
        Assert.Contains("CompactAscii", json);
        Assert.Contains("Audio", json);
    }

    [Theory]
    [InlineData(0, 2)]   // below hard floor → clamped up to 2
    [InlineData(1, 2)]
    [InlineData(5, 5)]
    [InlineData(20, 10)] // above max → clamped to 10
    public void Interval_is_clamped(int input, int expected)
    {
        var normalized = (new AppSettings { Output = new OutputSettings { UpdateIntervalSeconds = input } }).Normalized();
        Assert.Equal(expected, normalized.Output.UpdateIntervalSeconds);
    }

    [Fact]
    public void Corrupt_file_recovers_to_defaults_and_backs_up()
    {
        File.WriteAllText(_path, "{ this is : not valid json ");
        var loaded = new SettingsStore(_path).Load();

        Assert.Equal(AppSettings.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.True(File.Exists(_path + ".bak"), "corrupt file should be backed up");
    }

    [Fact]
    public void Loopback_detection_flags_remote_hosts()
    {
        Assert.True(new TosuSettings { Host = "127.0.0.1" }.IsLoopback);
        Assert.True(new TosuSettings { Host = "localhost" }.IsLoopback);
        Assert.False(new TosuSettings { Host = "192.168.1.50" }.IsLoopback);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }
}
