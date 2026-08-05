using OsuVrcChatbox.Core.Formatting;
using OsuVrcChatbox.Core.Scheduling;
using OsuVrcChatbox.Core.Timing;

namespace OsuVrcChatbox.Core.Settings;

/// <summary>Persisted application settings (plan §16). Records → immutable; use <c>with</c> to edit.</summary>
public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public TosuSettings Tosu { get; init; } = new();
    public OscSettings Osc { get; init; } = new();
    public OutputSettings Output { get; init; } = new();
    public TemplateSettings Template { get; init; } = new();
    public AppUiSettings App { get; init; } = new();

    /// <summary>Returns a copy with all values clamped/validated into legal ranges.</summary>
    public AppSettings Normalized() => this with
    {
        SchemaVersion = CurrentSchemaVersion,
        Osc = Osc with { Port = Math.Clamp(Osc.Port, 1, 65535) },
        Tosu = Tosu with { Port = Math.Clamp(Tosu.Port, 1, 65535) },
        Output = Output with
        {
            UpdateIntervalSeconds = Math.Clamp(
                Output.UpdateIntervalSeconds,
                (int)ChatboxRateLimiter.HardFloor.TotalSeconds,
                10),
            ResultsDisplaySeconds = Math.Max(0, Output.ResultsDisplaySeconds)
        }
    };

    public TimingConfig ToTimingConfig() => new(Output.LengthSource, TimesAreRateAdjusted: false);

    public TemplateConfig ToTemplateConfig() => new(
        Preset: Template.Preset,
        CustomTemplate: string.IsNullOrWhiteSpace(Template.Custom) ? null : Template.Custom,
        PreferUnicode: Template.PreferUnicode);
}

public sealed record TosuSettings
{
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 24050;
    public bool AllowRemoteHost { get; init; }

    /// <summary>True when the host is a loopback address (safe default).</summary>
    public bool IsLoopback =>
        Host is "127.0.0.1" or "localhost" or "::1" ||
        (System.Net.IPAddress.TryParse(Host, out var ip) && System.Net.IPAddress.IsLoopback(ip));
}

public sealed record OscSettings
{
    public string Ip { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 9000;
    public bool NotificationSound { get; init; }
}

public sealed record OutputSettings
{
    public bool MasterEnabled { get; init; } = true;
    public int UpdateIntervalSeconds { get; init; } = 3;
    public bool ClearOnStop { get; init; } = true;
    public bool KeepFinalResultVisible { get; init; }
    public int ResultsDisplaySeconds { get; init; } = 8;
    public LengthSource LengthSource { get; init; } = LengthSource.Audio;
}

public sealed record TemplateSettings
{
    public MessagePreset Preset { get; init; } = MessagePreset.CompactAscii;
    public string? Custom { get; init; }
    public bool PreferUnicode { get; init; }
}

public sealed record AppUiSettings
{
    public bool StartMinimized { get; init; }
    public bool StartWithWindows { get; init; }
    public bool MinimizeToTray { get; init; } = true;
    public string GlobalPauseHotkey { get; init; } = "Control+Alt+P";
    public int? PauseForMinutes { get; init; }
}
