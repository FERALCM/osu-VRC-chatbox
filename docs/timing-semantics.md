# Timing & duration semantics

tosu reports these times (ms) on the **beatmap timeline** (see plan §10):

- `beatmap.time.live` — current playback position.
- `beatmap.time.firstObject` / `lastObject` — first/last hit-object times.
- `beatmap.time.mp3Length` — audio-file duration.
- `play.mods.rate` — effective clock multiplier (DT ≈ 1.5, HT ≈ 0.75, plus custom lazer rates).

## What this app displays

Default length source is **audio** (`mp3Length`), **rate-adjusted** to real wall-clock time:

```
lengthReal    = mp3Length / rate
elapsedReal   = clamp(live, 0, mp3Length) / rate
remainingReal = clamp(mp3Length − live, 0, mp3Length) / rate
```

This is labeled **"map length"** (not "song length") because a beatmap's audio can extend past the
last note. A `LengthSource.LastObject` option uses `lastObject` instead (drain/playable length).

## Assumption to confirm in Phase 0

We assume tosu's `time.*` fields are **not** already rate-adjusted (so we divide by `rate`). If a
DoubleTime capture proves otherwise, set `TimingConfig.TimesAreRateAdjusted = true` — the calculator
then stops dividing, and nothing else changes.

## Edge cases

- **Lead-in** (`live < 0`): elapsed clamps to 0, remaining to full length.
- **Past end** (`live > length`): remaining clamps to 0.
- **Pause / fail / results**: the state machine stops the continuous timer; the last frame is what
  shows until state changes.
