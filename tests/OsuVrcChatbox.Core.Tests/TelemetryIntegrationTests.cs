using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using OsuVrcChatbox.Core.Model;
using OsuVrcChatbox.Core.Telemetry;
using Xunit;

namespace OsuVrcChatbox.Core.Tests;

public class TelemetryIntegrationTests
{
    [Fact]
    public async Task Connects_receives_and_parses_a_snapshot_from_a_fake_ws_server()
    {
        using var server = new MiniWebSocketServer(Fixtures.GameplayDt);
        await using var source = new TosuWebSocketTelemetrySource(
            new TosuConnectionOptions("127.0.0.1", server.Port));

        var received = new TaskCompletionSource<GameplaySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SnapshotReceived += s => received.TrySetResult(s);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await source.StartAsync(cts.Token);

        Assert.True(await Task.WhenAny(received.Task, Task.Delay(8000)) == received.Task, "no snapshot received");
        var snapshot = await received.Task;
        Assert.Equal("Camellia", snapshot.Artist);
        Assert.Equal(2, snapshot.StateNumber);

        await source.StopAsync();
    }

    [Fact]
    public async Task Raises_status_events_during_lifecycle()
    {
        using var server = new MiniWebSocketServer(Fixtures.Menu);
        await using var source = new TosuWebSocketTelemetrySource(
            new TosuConnectionOptions("127.0.0.1", server.Port));

        var statuses = new List<SourceStatus>();
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.StatusChanged += e =>
        {
            lock (statuses) statuses.Add(e.Status);
            if (e.Status == SourceStatus.Connected) connected.TrySetResult();
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await source.StartAsync(cts.Token);
        await Task.WhenAny(connected.Task, Task.Delay(8000));
        await source.StopAsync();

        lock (statuses)
        {
            Assert.Contains(SourceStatus.Connecting, statuses);
            Assert.Contains(SourceStatus.Connected, statuses);
            Assert.Contains(SourceStatus.Stopped, statuses);
        }
    }
}

/// <summary>
/// Minimal RFC 6455 server over a TcpListener: completes the handshake and pushes a single text
/// frame. Avoids HttpListener URL-ACL requirements so the test needs no admin rights.
/// </summary>
internal sealed class MiniWebSocketServer : IDisposable
{
    private const string WsGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private readonly TcpListener _listener;
    private readonly string _payload;

    public int Port { get; }

    public MiniWebSocketServer(string payload)
    {
        _payload = payload;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = Task.Run(AcceptLoopAsync);
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            using TcpClient client = await _listener.AcceptTcpClientAsync();
            using NetworkStream stream = client.GetStream();

            string request = await ReadHttpHeadersAsync(stream);
            string key = ExtractHeader(request, "Sec-WebSocket-Key");
            string accept = Convert.ToBase64String(
                SHA1.HashData(Encoding.ASCII.GetBytes(key + WsGuid)));

            string response =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Accept: {accept}\r\n\r\n";
            byte[] responseBytes = Encoding.ASCII.GetBytes(response);
            await stream.WriteAsync(responseBytes);

            await stream.WriteAsync(BuildServerTextFrame(_payload));
            await Task.Delay(TimeSpan.FromSeconds(2)); // keep open long enough to be read
        }
        catch
        {
            // test server — swallow
        }
    }

    private static async Task<string> ReadHttpHeadersAsync(NetworkStream stream)
    {
        var buffer = new byte[4096];
        var sb = new StringBuilder();
        while (!sb.ToString().Contains("\r\n\r\n"))
        {
            int read = await stream.ReadAsync(buffer);
            if (read == 0) break;
            sb.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }
        return sb.ToString();
    }

    private static string ExtractHeader(string request, string name)
    {
        foreach (string line in request.Split("\r\n"))
        {
            if (line.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase))
                return line[(line.IndexOf(':') + 1)..].Trim();
        }
        return string.Empty;
    }

    private static byte[] BuildServerTextFrame(string payload)
    {
        byte[] data = Encoding.UTF8.GetBytes(payload);
        using var ms = new MemoryStream();
        ms.WriteByte(0x81); // FIN + text opcode
        if (data.Length < 126)
        {
            ms.WriteByte((byte)data.Length);
        }
        else
        {
            ms.WriteByte(126);
            ms.WriteByte((byte)(data.Length >> 8));
            ms.WriteByte((byte)(data.Length & 0xFF));
        }
        ms.Write(data); // server frames are unmasked
        return ms.ToArray();
    }

    public void Dispose()
    {
        try { _listener.Stop(); } catch { /* ignore */ }
    }
}
