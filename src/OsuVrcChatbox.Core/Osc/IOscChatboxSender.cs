namespace OsuVrcChatbox.Core.Osc;

/// <summary>
/// Sends chatbox text to VRChat over OSC/UDP (plan §4). UDP is unacknowledged: a successful send
/// means the packet left the socket, never that VRChat received or displayed it — so implementations
/// must not report a "connected" state.
/// </summary>
public interface IOscChatboxSender : IDisposable
{
    /// <summary>The configured destination endpoint, for display (e.g. "127.0.0.1:9000").</summary>
    string Destination { get; }

    /// <summary>Timestamp of the last packet handed to the socket, if any.</summary>
    DateTimeOffset? LastSentAt { get; }

    /// <summary>Sends text to <c>/chatbox/input</c>.</summary>
    /// <param name="text">Message body (already length-enforced by the formatter).</param>
    /// <param name="immediate">Send now (true) vs. open the keyboard (false).</param>
    /// <param name="notify">Play the notification sound. Default policy is false.</param>
    Task SendAsync(string text, bool immediate, bool notify, CancellationToken ct = default);

    /// <summary>Clears our chatbox output by sending an empty immediate message.</summary>
    Task ClearAsync(CancellationToken ct = default);
}
