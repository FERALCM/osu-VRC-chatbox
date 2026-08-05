using OsuVrcChatbox.Core;
using OsuVrcChatbox.Core.Osc;
using OsuVrcChatbox.Core.Formatting;
using OsuVrcChatbox.Core.Scheduling;
using OsuVrcChatbox.Core.Settings;
using OsuVrcChatbox.Core.Telemetry;

namespace OsuVrcChatbox.ConsoleApp;

/// <summary>
/// Phase-1 headless harness (plan §21): connects to tosu, formats, and pushes to VRChat OSC while
/// printing a live preview. Not the shipping UI — a way to exercise the Core pipeline end-to-end.
/// Flags: --tosu-host, --tosu-port, --osc-ip, --osc-port, --interval &lt;sec&gt;, --preset &lt;name&gt;, --no-osc.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var argMap = ParseArgs(args);
        var settings = new SettingsStore(SettingsStore.DefaultPath).Load();

        string tosuHost = argMap.GetValueOrDefault("tosu-host", settings.Tosu.Host);
        int tosuPort = GetInt(argMap, "tosu-port", settings.Tosu.Port);
        string oscIp = argMap.GetValueOrDefault("osc-ip", settings.Osc.Ip);
        int oscPort = GetInt(argMap, "osc-port", settings.Osc.Port);
        int interval = GetInt(argMap, "interval", settings.Output.UpdateIntervalSeconds);
        bool noOsc = argMap.ContainsKey("no-osc");

        var template = settings.ToTemplateConfig();
        if (argMap.TryGetValue("preset", out var presetName) &&
            Enum.TryParse<MessagePreset>(presetName, ignoreCase: true, out var preset))
        {
            template = template with { Preset = preset, CustomTemplate = null };
        }

        var source = new TosuWebSocketTelemetrySource(new TosuConnectionOptions(tosuHost, tosuPort));
        IOscChatboxSender sender = noOsc ? new NullSender(oscIp, oscPort) : new OscUdpChatboxSender(oscIp, oscPort);
        var limiter = new ChatboxRateLimiter(TimeSpan.FromSeconds(interval));
        var output = new ChatboxOutputService(sender, limiter) { NotificationSound = settings.Osc.NotificationSound };

        var orchestrator = new AppOrchestrator(source, output, new ChatboxFormatter(), new OrchestratorOptions
        {
            Timing = settings.ToTimingConfig(),
            Template = template,
            ClearOnStop = settings.Output.ClearOnStop,
            ShowResults = settings.Output.KeepFinalResultVisible || settings.Output.ResultsDisplaySeconds > 0
        });

        Console.WriteLine("osu! → VRChat chatbox (console harness)");
        Console.WriteLine($"  tosu:  ws://{tosuHost}:{tosuPort}/websocket/v2");
        Console.WriteLine($"  OSC:   {(noOsc ? "(disabled)" : $"{oscIp}:{oscPort} /chatbox/input")}");
        Console.WriteLine($"  every: {Math.Max(2, interval)}s   preset: {template.Preset}");
        Console.WriteLine("  Ctrl+C to stop.\n");

        orchestrator.SourceStatusChanged += e =>
            Console.WriteLine($"[source] {e.Status}{(e.Detail is null ? "" : $" — {e.Detail}")}");

        orchestrator.Processed += r =>
        {
            if (r.Message is { } m)
                Console.WriteLine($"[{r.State}] {m.CharCount,3}c{(m.Degraded ? " (deg)" : "")}: {m.Text.Replace('\n', '|')}");
        };

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        await orchestrator.StartAsync(cts.Token);
        try { await Task.Delay(Timeout.Infinite, cts.Token); }
        catch (OperationCanceledException) { }

        Console.WriteLine("\nStopping…");
        await orchestrator.StopAsync();
        await orchestrator.DisposeAsync();
        sender.Dispose();
        return 0;
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
            string key = args[i][2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                map[key] = args[++i];
            else
                map[key] = "true";
        }
        return map;
    }

    private static int GetInt(Dictionary<string, string> map, string key, int fallback) =>
        map.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : fallback;

    /// <summary>Sender that formats/encodes nothing to the network — for previewing without VRChat.</summary>
    private sealed class NullSender(string ip, int port) : IOscChatboxSender
    {
        public string Destination { get; } = $"{ip}:{port}";
        public DateTimeOffset? LastSentAt { get; private set; }
        public Task SendAsync(string text, bool immediate, bool notify, CancellationToken ct = default)
        {
            LastSentAt = DateTimeOffset.UtcNow;
            return Task.CompletedTask;
        }
        public Task ClearAsync(CancellationToken ct = default) => Task.CompletedTask;
        public void Dispose() { }
    }
}
