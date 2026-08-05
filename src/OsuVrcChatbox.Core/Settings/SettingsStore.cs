using System.Text.Json;
using System.Text.Json.Serialization;

namespace OsuVrcChatbox.Core.Settings;

/// <summary>
/// Loads/saves <see cref="AppSettings"/> as JSON with atomic writes and corrupt-file recovery (plan §16/§18).
/// A parse failure never throws to the caller: the bad file is backed up to <c>.bak</c> and defaults
/// are returned. Enums are written as strings for human-editable, migration-friendly files.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;

    public SettingsStore(string path) => _path = path;

    /// <summary>Default location: <c>%APPDATA%\osu-vrc-chatbox\settings.json</c>.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "osu-vrc-chatbox",
        "settings.json");

    /// <summary>Loads settings, returning validated defaults if the file is missing or corrupt.</summary>
    public AppSettings Load()
    {
        if (!File.Exists(_path))
            return new AppSettings();

        try
        {
            string json = File.ReadAllText(_path);
            AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
            if (loaded is null) return new AppSettings();
            return Migrate(loaded).Normalized();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            TryBackupCorrupt();
            return new AppSettings();
        }
    }

    /// <summary>Writes settings atomically (temp file + replace) so a crash can't leave a partial file.</summary>
    public void Save(AppSettings settings)
    {
        string dir = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);

        string tmp = _path + ".tmp";
        string json = JsonSerializer.Serialize(settings.Normalized(), Options);
        File.WriteAllText(tmp, json);

        if (File.Exists(_path))
            File.Replace(tmp, _path, destinationBackupFileName: null);
        else
            File.Move(tmp, _path);
    }

    /// <summary>Applies forward migrations for older schema versions.</summary>
    private static AppSettings Migrate(AppSettings loaded)
    {
        // v0/unset → v1: nothing to transform yet; stamp the current version.
        return loaded.SchemaVersion >= AppSettings.CurrentSchemaVersion
            ? loaded
            : loaded with { SchemaVersion = AppSettings.CurrentSchemaVersion };
    }

    private void TryBackupCorrupt()
    {
        try { File.Copy(_path, _path + ".bak", overwrite: true); }
        catch { /* best-effort; never throw from Load */ }
    }
}
