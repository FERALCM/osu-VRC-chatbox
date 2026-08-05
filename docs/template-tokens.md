# Message template tokens

Custom templates substitute these `{token}` placeholders (case-insensitive). Values come from the
current tosu snapshot (see the data-field mapping in the implementation plan).

| Token | Meaning | Source |
|-------|---------|--------|
| `{artist}` | Song artist | `beatmap.artistUnicode` ?? `beatmap.artist` (ASCII preset uses `artist`) |
| `{title}` | Song title | `beatmap.titleUnicode` ?? `beatmap.title` |
| `{difficulty}` | Difficulty name | `beatmap.version` |
| `{elapsed}` | Position in song (`m:ss`) | `beatmap.time.live`, rate-adjusted |
| `{remaining}` | Time left (`m:ss`) | `mp3Length − live`, rate-adjusted, clamped ≥ 0 |
| `{length}` | Map length (`m:ss`) | `beatmap.time.mp3Length`, rate-adjusted |
| `{pp}` | Current PP (integer) | `play.pp.current` |
| `{misses}` | Miss count | `play.hits["0"]` |
| `{combo}` | Current combo | `play.combo.current` |
| `{maxCombo}` | Max combo this play | `play.combo.max` |
| `{mods}` | Mod acronyms | `play.mods.name` |
| `{accuracy}` | Accuracy % | `play.accuracy` |

## Built-in presets

- **CompactOneLine** — `{artist} - {title} [{difficulty}] {remaining} left / {length} | {pp}pp | {misses} miss | {combo}x`
- **TwoLine** — line 1 metadata, line 2 stats.
- **CompactAscii** — CompactOneLine with non-ASCII metadata stripped (safe default).

## Degradation order when too long

Core stats (remaining, PP, misses, combo) are always preserved. Fields are shed in order:
difficulty → artist → stat labels/separators (compact form) → finally the **title** is truncated on
a grapheme boundary with `…`. The message never exceeds 144 UTF-16 units or 9 lines.
