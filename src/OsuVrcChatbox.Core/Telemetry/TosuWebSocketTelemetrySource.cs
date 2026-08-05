using System.Net.WebSockets;
using System.Text;
using OsuVrcChatbox.Core.Model;

namespace OsuVrcChatbox.Core.Telemetry;

/// <summary>Connection options for <see cref="TosuWebSocketTelemetrySource"/>.</summary>
/// <param name="Host">tosu host (default loopback). Non-loopback requires explicit user opt-in upstream.</param>
/// <param name="Port">tosu server port (default 24050, verified).</param>
/// <param name="Path">WebSocket route (default <c>/websocket/v2</c>, verified).</param>
public readonly record struct TosuConnectionOptions(
    string Host = "127.0.0.1",
    int Port = 24050,
    string Path = "/websocket/v2")
{
    public Uri BuildUri() => new($"ws://{Host}:{Port}{Path}");
}

/// <summary>
/// Reads tosu's <c>/websocket/v2</c> stream (plan §7/§21). Single connect/receive/reconnect loop with
/// exponential backoff + jitter; fully cancellation-aware; never runs overlapping reconnect loops.
/// Each text frame is parsed into a <see cref="GameplaySnapshot"/> and surfaced via
/// <see cref="SnapshotReceived"/>. Parsing/throttling for OSC happens downstream.
/// </summary>
public sealed class TosuWebSocketTelemetrySource : IOsuTelemetrySource
{
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    private readonly TosuConnectionOptions _options;
    private readonly Func<ClientWebSocket> _socketFactory;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public event Action<GameplaySnapshot>? SnapshotReceived;
    public event Action<SourceStatusEvent>? StatusChanged;

    public TosuWebSocketTelemetrySource(TosuConnectionOptions options, Func<ClientWebSocket>? socketFactory = null)
    {
        _options = options;
        _socketFactory = socketFactory ?? (() => new ClientWebSocket());
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_loop is not null) return Task.CompletedTask;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = Task.Run(() => RunAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        _loop = null;
        Raise(SourceStatus.Stopped);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var backoff = InitialBackoff;
        var jitter = new Random();

        while (!ct.IsCancellationRequested)
        {
            ClientWebSocket? socket = null;
            try
            {
                Raise(SourceStatus.Connecting, _options.BuildUri().ToString());
                socket = _socketFactory();
                await socket.ConnectAsync(_options.BuildUri(), ct).ConfigureAwait(false);
                Raise(SourceStatus.Connected, _options.BuildUri().ToString());
                backoff = InitialBackoff; // reset on success

                await ReceiveLoopAsync(socket, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Raise(SourceStatus.Disconnected, ex.Message);
            }
            finally
            {
                socket?.Dispose();
            }

            if (ct.IsCancellationRequested) break;

            // Backoff with jitter before reconnecting.
            TimeSpan delay = backoff + TimeSpan.FromMilliseconds(jitter.Next(0, 500));
            Raise(SourceStatus.Reconnecting, $"retry in {delay.TotalSeconds:0.0}s");
            try { await Task.Delay(delay, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            backoff = TimeSpan.FromMilliseconds(Math.Min(backoff.TotalMilliseconds * 2, MaxBackoff.TotalMilliseconds));
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();

        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            message.SetLength(0);
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);
                    return;
                }
                message.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            if (result.MessageType != WebSocketMessageType.Text) continue;

            string json = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            var snapshot = SnapshotParser.TryParse(json);
            if (snapshot is not null)
                SnapshotReceived?.Invoke(snapshot);
        }
    }

    private void Raise(SourceStatus status, string? detail = null) =>
        StatusChanged?.Invoke(new SourceStatusEvent(status, detail));

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _cts?.Dispose();
    }
}
