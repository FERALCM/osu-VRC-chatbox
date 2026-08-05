using System.Net;
using System.Net.Sockets;
using System.Text;
using OsuVrcChatbox.Core.Osc;
using Xunit;

namespace OsuVrcChatbox.Core.Tests;

public class OscEncoderTests
{
    [Fact]
    public void Encodes_chatbox_input_with_correct_layout()
    {
        byte[] packet = OscMessageEncoder.Encode("/chatbox/input", "hi", bool1: true, bool2: false);
        Assert.Equal(0, packet.Length % 4); // 4-byte aligned

        var (address, tag, str, bools) = OscTestCodec.Decode(packet);
        Assert.Equal("/chatbox/input", address);
        Assert.Equal(",sTF", tag);
        Assert.Equal("hi", str);
        Assert.Equal(new[] { true, false }, bools);
    }

    [Fact]
    public void Empty_string_clear_encodes_cleanly()
    {
        var (address, tag, str, bools) = OscTestCodec.Decode(
            OscMessageEncoder.Encode("/chatbox/input", "", true, false));
        Assert.Equal("/chatbox/input", address);
        Assert.Equal("", str);
        Assert.Equal(new[] { true, false }, bools);
    }

    [Fact]
    public void Utf8_string_round_trips()
    {
        var (_, _, str, _) = OscTestCodec.Decode(
            OscMessageEncoder.Encode("/chatbox/input", "café — かめりあ", true, false));
        Assert.Equal("café — かめりあ", str);
    }
}

public class OscUdpSenderIntegrationTests
{
    [Fact]
    public async Task Sends_wire_correct_packet_to_udp_endpoint()
    {
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)receiver.Client.LocalEndPoint!).Port;

        using var sender = new OscUdpChatboxSender("127.0.0.1", port);
        await sender.SendAsync("now playing", immediate: true, notify: false);

        var receiveTask = receiver.ReceiveAsync();
        Assert.True(await Task.WhenAny(receiveTask, Task.Delay(2000)) == receiveTask, "no datagram received");

        var (address, tag, str, bools) = OscTestCodec.Decode((await receiveTask).Buffer);
        Assert.Equal("/chatbox/input", address);
        Assert.Equal(",sTF", tag);
        Assert.Equal("now playing", str);
        Assert.Equal(new[] { true, false }, bools);
        Assert.NotNull(sender.LastSentAt);
    }

    [Fact]
    public async Task Clear_sends_empty_immediate_no_notify()
    {
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)receiver.Client.LocalEndPoint!).Port;

        using var sender = new OscUdpChatboxSender("127.0.0.1", port);
        await sender.ClearAsync();

        var receiveTask = receiver.ReceiveAsync();
        Assert.True(await Task.WhenAny(receiveTask, Task.Delay(2000)) == receiveTask, "no datagram received");
        var (_, _, str, bools) = OscTestCodec.Decode((await receiveTask).Buffer);
        Assert.Equal("", str);
        Assert.Equal(new[] { true, false }, bools);
    }

    [Fact]
    public void Invalid_ip_throws()
    {
        Assert.Throws<ArgumentException>(() => new OscUdpChatboxSender("not-an-ip", 9000));
    }
}

/// <summary>Test-only OSC decoder mirroring <see cref="OscMessageEncoder"/>.</summary>
internal static class OscTestCodec
{
    public static (string Address, string Tag, string? Str, bool[] Bools) Decode(byte[] data)
    {
        int pos = 0;
        string address = ReadOscString(data, ref pos);
        string tag = ReadOscString(data, ref pos);

        string? str = null;
        var bools = new List<bool>();
        foreach (char c in tag.AsSpan(1)) // skip leading ','
        {
            switch (c)
            {
                case 's': str = ReadOscString(data, ref pos); break;
                case 'T': bools.Add(true); break;
                case 'F': bools.Add(false); break;
            }
        }
        return (address, tag, str, bools.ToArray());
    }

    private static string ReadOscString(byte[] data, ref int pos)
    {
        int start = pos;
        while (pos < data.Length && data[pos] != 0) pos++;
        string s = Encoding.UTF8.GetString(data, start, pos - start);
        pos++;                       // consume null terminator
        pos = (pos + 3) / 4 * 4;     // advance to 4-byte boundary
        return s;
    }
}
