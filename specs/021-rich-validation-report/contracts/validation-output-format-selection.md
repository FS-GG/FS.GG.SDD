# Contract: `validate` output-format selection & degradation

Governs how `fsgg-sdd validate` chooses among JSON, plain-text, and rich
projections of the `validation-report`, how rich degrades, what is persisted to
`--out`, and the stream/exit parity guarantees. Extends — does not replace — the
feature-019 output-format-selection contract for `CommandReport`.

## Flag precedence

Selection reuses `Rendering.selectFormat` (unchanged):

`--rich` > `--text` > `--json` > default (`Json`).

JSON remains the unconditional default for `validate` (the automation contract).
The previously-deferred behavior — `--rich` silently degrading to `--text` even in
an interactive terminal — is removed: `--rich` now selects the rich projection.

## Capability degradation (stdout)

`resolveValidation format capabilities report` resolves the **stdout** rendering:

| Format selected | `IsInteractive && ColorEnabled` | stdout rendering | `UsedRichRendering` |
|---|---|---|---|
| `Json` (or default) | any | `serialize report` (deterministic JSON) | `false` |
| `Text` | any | `renderText report` (portable plain text) | `false` |
| `Rich` | **true** | rich Spectre rendering | `true` |
| `Rich` | **false** (redirected/piped/`NO_COLOR`/`TERM=dumb`) | `renderText report` (zero ANSI) | `false` |

Capability detection (`detectCapabilities`) is unchanged: non-interactive iff
stdout is redirected; color disabled iff `NO_COLOR` is present (any value) or
`TERM=dumb`.

## `--out` persistence (deterministic only)

`--out <path>` always writes a **deterministic** projection — never rich ANSI:

| Format selected | bytes written to `--out` |
|---|---|
| `Json` / default | `serialize report` |
| `Text` | `renderText report` |
| `Rich` | `renderText report` (rich's deterministic, non-interactive shadow) |

Rich is presentation-only for interactive stdout and is not a persisted artifact
(FR-010). This preserves feature 020's existing `--rich --out` → text behavior.

## Stream & exit parity

- **Stream**: `validate` writes the resolved rendering to **stdout** for every
  format and every verdict (unchanged). Selecting rich never moves output to
  stderr. (FR-006/SC-005)
- **Exit code**: `0` iff `report.Summary.OverallPassed`, else non-zero — identical
  across all three formats. A single-matrix run still reports the other matrices as
  `notValidated`, so a partial run never exits `0`. (FR-006)

## Conformance checks

- `selectFormat` precedence (existing `FormatSelectionTests` already cover this).
- `resolveValidation Json/Text` returns `serialize`/`renderText` byte-for-byte
  with `UsedRichRendering=false`.
- `resolveValidation Rich` with non-interactive or color-off capabilities returns
  `renderText` with no ESC byte; with interactive+color returns rich output with
  `UsedRichRendering=true`.
- End-to-end: `validate --rich` redirected to a file/pipe contains zero ANSI;
  `validate --json` bytes (incl. the `sensed` null fence) are unchanged from before
  this feature; exit code matches across `--json`/`--text`/`--rich`.
