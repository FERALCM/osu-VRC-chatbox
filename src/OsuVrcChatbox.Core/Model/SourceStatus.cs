namespace OsuVrcChatbox.Core.Model;

/// <summary>Connection state of an <see cref="Telemetry.IOsuTelemetrySource"/>.</summary>
public enum SourceStatus
{
    Idle,
    Connecting,
    Connected,
    Disconnected,
    Reconnecting,
    Stopped
}

/// <summary>Status change plus an optional human-readable detail (for UI/logs).</summary>
public readonly record struct SourceStatusEvent(SourceStatus Status, string? Detail = null);
