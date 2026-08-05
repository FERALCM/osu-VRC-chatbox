# osu! → VRChat Chatbox (OSC)

Displays live osu! gameplay (song, position/remaining/length, PP, misses, combo) in the VRChat
chatbox over OSC. Reads gameplay **read-only** from a locally installed
[**tosu**](https://github.com/tosuapp/tosu) instance via its WebSocket — no osu!/VRChat credentials,
no memory writes, no unofficial APIs.

> Full design rationale, citations, and phase plan live in the approved implementation plan.

## Status

**Phase 1 (headless core) — complete and tested.** The `OsuVrcChatbox.Core` library plus a console
harness implement the whole pipeline:

```
tosu /websocket/v2 → SnapshotParser → GameStateMachine → TimeCalculator
   → ChatboxFormatter (+ degradation) → LatestValueScheduler → ChatboxRateLimiter
   → OSC UDP → VRChat /chatbox/input
```

**Phase 2 (WPF desktop app) — complete.** `OsuVrcChatbox.App` (net8.0-windows) provides a compact
window with the master output switch, live status/values, a live chatbox preview with character
count, template selector + custom editor, connection settings, and app options — plus system-tray
presence, a global pause hotkey, single-instance guard, start-with-Windows, minimize-to-tray, and a
rolling file log. Settings persist to `%APPDATA%\osu-vrc-chatbox\settings.json`.

Phases 3–5 (stable/lazer validation, installer/packaging, managed tosu sidecar, native reader)
remain per the plan.

## Requirements

- .NET 8 SDK (or newer).
- [tosu](https://github.com/tosuapp/tosu) running locally (default `ws://127.0.0.1:24050/websocket/v2`).
- VRChat with OSC enabled (default UDP `127.0.0.1:9000`).

## Run the desktop app

```bash
dotnet run --project src/OsuVrcChatbox.App
```

Pass `--minimized` to start hidden in the tray. Right-click the tray icon for show / pause / clear /
exit; the window's **X** minimizes to tray (exit from the tray menu).

## Run the console harness

```bash
dotnet run --project src/OsuVrcChatbox.Console
```

Flags: `--tosu-host`, `--tosu-port`, `--osc-ip`, `--osc-port`, `--interval <seconds>`,
`--preset <CompactOneLine|TwoLine|CompactAscii>`, `--no-osc` (preview without sending to VRChat).

## Test

```bash
dotnet test
```

## Safety & limits

- Output is throttled to one message per configured interval (default **3 s**, hard floor **2 s**),
  well within VRChat's chatbox rate limit; only the latest status is ever sent (never a burst).
- Every message is enforced to **≤ 144 characters / ≤ 9 lines** with Unicode-safe truncation.
- tosu connection defaults to loopback; remote hosts require explicit opt-in.

## License

tosu is LGPL-3.0 and is **not** bundled — the MVP only connects to a separately installed instance.
See [docs/licensing.md](docs/licensing.md).
