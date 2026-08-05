using OsuVrcChatbox.Core.Model;

namespace OsuVrcChatbox.Core.Telemetry;

/// <summary>
/// Abstracts the osu! data provider (plan §14). The MVP implementation reads tosu's WebSocket;
/// later phases can supply a managed sidecar or a native reader without changing consumers.
/// </summary>
public interface IOsuTelemetrySource : IAsyncDisposable
{
    /// <summary>Raised for every parsed snapshot (high frequency — do not send OSC per event).</summary>
    event Action<GameplaySnapshot>? SnapshotReceived;

    /// <summary>Raised on connection lifecycle changes.</summary>
    event Action<SourceStatusEvent>? StatusChanged;

    /// <summary>Starts the connect/receive/reconnect loop. Returns once the loop has been launched.</summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>Signals shutdown and waits for the loop to stop.</summary>
    Task StopAsync();
}
