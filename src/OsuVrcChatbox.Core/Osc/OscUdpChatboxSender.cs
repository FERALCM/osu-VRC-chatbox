using System.Net;
using System.Net.Sockets;

namespace OsuVrcChatbox.Core.Osc;

/// <summary>UDP implementation of <see cref="IOscChatboxSender"/> targeting VRChat's OSC input port.</summary>
public sealed class OscUdpChatboxSender : IOscChatboxSender
{
    public const string ChatboxInputAddress = "/chatbox/input";

    private readonly UdpClient _client;
    private readonly IPEndPoint _endpoint;
    private long _lastSentTicks; // 0 = never

    public OscUdpChatboxSender(string ip = "127.0.0.1", int port = 9000)
    {
        if (!IPAddress.TryParse(ip, out var address))
            throw new ArgumentException($"Invalid IP address: '{ip}'.", nameof(ip));
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be 1–65535.");

        _endpoint = new IPEndPoint(address, port);
        _client = new UdpClient();
    }

    public string Destination => $"{_endpoint.Address}:{_endpoint.Port}";

    public DateTimeOffset? LastSentAt =>
        Interlocked.Read(ref _lastSentTicks) is var t && t != 0
            ? new DateTimeOffset(t, TimeSpan.Zero)
            : null;

    public async Task SendAsync(string text, bool immediate, bool notify, CancellationToken ct = default)
    {
        byte[] packet = OscMessageEncoder.Encode(ChatboxInputAddress, text ?? string.Empty, immediate, notify);
        await _client.SendAsync(packet, packet.Length, _endpoint).WaitAsync(ct).ConfigureAwait(false);
        Interlocked.Exchange(ref _lastSentTicks, DateTimeOffset.UtcNow.UtcTicks);
    }

    public Task ClearAsync(CancellationToken ct = default) =>
        SendAsync(string.Empty, immediate: true, notify: false, ct);

    public void Dispose() => _client.Dispose();
}
