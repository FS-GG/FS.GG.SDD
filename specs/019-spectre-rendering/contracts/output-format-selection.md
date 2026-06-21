# Contract: CLI Output Format Selection & Degradation

Feature: `019-spectre-rendering`

Governs how `fsgg-sdd` resolves the effective rendering for a command, extending
the existing `--text` / default-JSON behavior in `src/FS.GG.SDD.Cli/Program.fs`.

## Flag resolution

Format flags are mutually exclusive in effect; if more than one is present,
precedence is `--rich` > `--text` > `--json` > default.

| Flag(s) present | Requested `OutputFormat` |
|---|---|
| none | `Json` (unchanged default) |
| `--json` | `Json` |
| `--text` | `Text` |
| `--rich` | `Rich` |

JSON remains the default for every command, including the unknown-command and
no-args paths (which already emit JSON to stderr). This preserves all existing
CLI smoke evidence and JSON fixtures.

## Effective rendering (capability degradation)

Given requested format and detected `TerminalCapabilities`:

| Requested | Interactive + color | Non-interactive OR color disabled |
|---|---|---|
| `Json` | JSON | JSON |
| `Text` | plain text | plain text |
| `Rich` | **rich (Spectre)** | **plain text fallback (zero ANSI)** |

"Color disabled" = `NO_COLOR` env var present (any value) **or** `TERM=dumb`.
"Non-interactive" = `Console.IsOutputRedirected` is true (piped/redirected) or no
TTY.

## Stream routing & exit code (unchanged across formats)

- `Outcome = Blocked` → rendered output written to **stderr**; all other outcomes
  → **stdout**. Identical for `Json`, `Text`, and `Rich`.
- Process exit code is `exitCodeForReport report`, independent of format.

## Guarantees

- Selecting `Rich` never alters `serializeReport` bytes, the `CommandReport`
  object, the chosen stream, or the exit code.
- `Rich` in any non-interactive or color-disabled context produces output
  byte-identical to `--text` for the same report.
