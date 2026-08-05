using System.Text;

namespace OsuVrcChatbox.Core.Osc;

/// <summary>
/// Minimal OSC 1.0 message encoder (plan §8). Sufficient for VRChat's <c>/chatbox/input</c>:
/// one string argument plus two booleans. OSC strings are UTF-8, null-terminated, and padded with
/// nulls to a 4-byte boundary; boolean arguments carry no payload — their value lives in the type tag
/// (<c>T</c>/<c>F</c>). VRChat accepts UTF-8 chatbox text.
/// </summary>
public static class OscMessageEncoder
{
    /// <summary>Encodes <c>address ,s&lt;b1&gt;&lt;b2&gt; stringArg</c> into an OSC packet.</summary>
    public static byte[] Encode(string address, string stringArg, bool bool1, bool bool2)
    {
        using var ms = new MemoryStream();
        WriteOscString(ms, address);
        WriteOscString(ms, "," + "s" + (bool1 ? 'T' : 'F') + (bool2 ? 'T' : 'F'));
        WriteOscString(ms, stringArg);
        return ms.ToArray();
    }

    private static void WriteOscString(Stream s, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        s.Write(bytes, 0, bytes.Length);
        s.WriteByte(0); // null terminator
        int total = bytes.Length + 1;
        int pad = (4 - total % 4) % 4;
        for (int i = 0; i < pad; i++) s.WriteByte(0);
    }
}
