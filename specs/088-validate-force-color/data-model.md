# Data Model: Force-Color Override + Markdown Report Card

This feature adds no persisted schema. It touches in-memory presentation types only. The authoritative `ValidationReport` structure (and its JSON contract) is **unchanged**.

## Entities

### TerminalCapabilities (existing record — shape unchanged)

`FS.GG.SDD.Cli.Rendering.TerminalCapabilities`

| Field | Type | Meaning | Change |
|-------|------|---------|--------|
| `IsInteractive` | `bool` | Sink is usable for rich output. Now = `(not outputRedirected) \|\| forceColor`. | Value semantics extended (force-color); field unchanged |
| `ColorEnabled` | `bool` | Color allowed. Now = `(not NO_COLOR) && ((not TERM=dumb) \|\| forceColor)`. | Value semantics extended (force-color); field unchanged |
| `Width` | `int option` | Console width cap; `None` when `outputRedirected` (raw). | Unchanged |
| `IsInputInteractive` | `bool` | stdin is a TTY (upgrade confirm gate). | Unchanged |

The record shape is deliberately unchanged so existing fixtures and the two gate predicates (`IsInteractive && ColorEnabled`) keep working; force-color is absorbed into the effective boolean values by `detectCapabilities`.

### ValidationFormat (new, `validate`-local DU)

`FS.GG.SDD.Cli.Rendering.ValidationFormat`

| Case | Meaning |
|------|---------|
| `Standard of OutputFormat` | One of the shared `Json \| Text \| Rich` projections, resolved via `resolveValidation`. |
| `MarkdownCard` | The deterministic Markdown report card, rendered via `ValidationContracts.renderMarkdown`. |

Selected by `selectValidationFormat : string list -> ValidationFormat` with precedence `--rich > --markdown > --text > --json > default(Standard Json)`.

### ValidationReport (existing — unchanged, consumed by renderMarkdown)

`FS.GG.SDD.Validation.ValidationContracts.ValidationReport` — `SchemaVersion`, `GeneratorVersion`, `Matrices: Matrix list`, `Summary: ReportSummary`, `Sensed`. `renderMarkdown` reads `Summary` (five counts + `OverallPassed`), `Matrices` (name, dimensions, cells with coordinates + `CellStatus`), and ignores `Sensed` (determinism fence). Optional `SchemaVersion`/`GeneratorVersion` are not surfaced (parity with rich).

## Functions (public surface additions)

| Function | Signature | Module | Purity |
|----------|-----------|--------|--------|
| `forceColorRequested` | `string list -> bool` | `Rendering` | Reads `FORCE_COLOR` env (edge) + scans args; deterministic given env |
| `detectCapabilities` | `bool -> bool -> TerminalCapabilities` (`forceColor` then `outputRedirected`) | `Rendering` | Edge (reads env + console) — arity change |
| `selectValidationFormat` | `string list -> ValidationFormat` | `Rendering` | Pure |
| `renderMarkdown` | `ValidationReport -> string` | `ValidationContracts` | Pure, deterministic |

## Derivations / rules

- **Effective interactivity**: `IsInteractive = (not outputRedirected) || forceColor`.
- **Effective color**: `ColorEnabled = (not noColorPresent) && ((not dumbTerminal) || forceColor)`.
- **Force-color source**: `forceColorRequested args = forceColorEnv() || (args contains "--force-color")`, `forceColorEnv()` boolean-ish over `FORCE_COLOR`.
- **Rich gate** (unchanged): render rich iff `IsInteractive && ColorEnabled`, else degrade to `renderText`.
- **Markdown determinism**: matrices sorted by `Name`; non-passing cells sorted by coordinate text; no sensed/width/wall-clock/ANSI.

## No state transitions

All additions are pure projections or a single edge read. No lifecycle state, no persisted artifact, no migration.
