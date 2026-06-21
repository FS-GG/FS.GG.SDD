# Phase 1 Data Model: Rich Rendering of the `validation-report`

This feature introduces **no new data type, field, matrix, or contract**. It
consumes the existing `validation-report` types and reuses the existing CLI
rendering types. The "model" here is the projection mapping from the report to the
rich rendering and the invariants the projection must hold.

## Consumed types (existing — unchanged)

From `FS.GG.SDD.Validation.ValidationContracts` (consumed read-only):

- `ValidationReport` = `{ SchemaVersion: int; GeneratorVersion: GeneratorVersion;
  Matrices: Matrix list; Summary: ReportSummary; Sensed: SensedMetadata }`
- `Matrix` = `{ Name: string; Dimensions: string list; Cells: MatrixCell list }`
- `MatrixCell` = `{ Coordinates: (string * string) list; Status: CellStatus }`
- `CellStatus` = `Pass | Fail of Diagnostic | SkippedWithReason of reason |
  CoverageGap of surface | NotValidated of reason`
- `ReportSummary` = `{ Passed; Failed; Skipped; CoverageGaps; NotValidated;
  OverallPassed }`
- `SensedMetadata` = `{ StartedAtUtc; DurationMs; Host }` (all `string/int option`)
- `serialize : ValidationReport -> string` — canonical JSON (sensed → `null`).
- `renderText : ValidationReport -> string` — portable plain text (non-pass cells
  only, sorted by coordinate).

## Reused CLI rendering types (existing — unchanged)

From `FS.GG.SDD.Cli.Rendering`:

- `TerminalCapabilities` = `{ IsInteractive: bool; ColorEnabled: bool;
  Width: int option }`
- `RichRenderResult` = `{ Text: string; UsedRichRendering: bool }`
- `selectFormat : string list -> OutputFormat` (`--rich > --text > --json > Json`)
- `detectCapabilities : unit -> TerminalCapabilities`

`OutputFormat` (`FS.GG.SDD.Commands.CommandTypes`) already has `Json | Text | Rich`.

## New public functions (the only surface change)

Added to `FS.GG.SDD.Cli.Rendering` (declared in `Rendering.fsi` first):

| Function | Signature | Role |
|---|---|---|
| `renderValidationRichTo` | `IAnsiConsole -> ValidationReport -> unit` | Pure over the report; writes the rich rendering to the supplied console only. |
| `resolveValidation` | `OutputFormat -> TerminalCapabilities -> ValidationReport -> RichRenderResult` | Picks the effective stdout rendering, degrading `Rich`→plain text when non-interactive or color-disabled. |

These add exactly two lines to `PublicSurface.baseline`
(`FS.GG.SDD.Cli.Rendering.renderValidationRichTo`,
`FS.GG.SDD.Cli.Rendering.resolveValidation`).

## Report → rich projection mapping

| Report fact | Rich representation | Source field |
|---|---|---|
| Overall verdict | Colored rule/header: green "passed" vs red "not passed" | `Summary.OverallPassed` |
| Summary counts | Counts line/table: passed / failed / skipped / coverageGaps / notValidated | `Summary.*` |
| Per-matrix rollup | One row per matrix: name, dimensions, per-status counts | `Matrices[].Name/.Dimensions/.Cells[].Status` |
| Non-passing cells | Listed per matrix: coordinates (`dim=value, …`) + status token | `MatrixCell.Coordinates`, `.Status` (≠ `Pass`) |
| Failure diagnostic | For `Fail`: the diagnostic message (matrix / coordinates / contract) | `Fail diagnostic.Message` |
| Coverage gap / not-validated | Status token + surface/reason, styled as failing | `CoverageGap surface` / `NotValidated reason` |
| Skipped-with-reason | Status token + reason, styled as **non-failing** | `SkippedWithReason reason` |
| Sensed (optional) | Each populated field only (`startedAtUtc`/`durationMs`/`host`) | `Sensed.*` (rendered iff `Some`) |
| Envelope metadata | **Not required** in rich; may be omitted | `SchemaVersion`, `GeneratorVersion` |

`pass` cells are summarized in the counts and rollup, not enumerated individually
(keeps a large cross-product scannable).

## Status styling map (presentation only)

| `CellStatus` | Fails the run? | Style intent |
|---|---|---|
| `Pass` | no | green (summarized only) |
| `Fail _` | **yes** | red / bold (emphasized) |
| `CoverageGap _` | **yes** | red family (emphasized) |
| `NotValidated _` | **yes** | red family (emphasized) |
| `SkippedWithReason _` | no | yellow/grey (distinct, non-failing) |

This mirrors `summarize`'s `OverallPassed = Failed = 0 && CoverageGaps = 0 &&
NotValidated = 0` so the visual emphasis matches what drives the exit code.

## Invariants

- **INV-1 (automation invariance)**: For identical inputs, `serialize report` and
  `renderText report` produce byte-identical output before and after this feature,
  and `resolveValidation` never mutates the report or its JSON. (FR-003/SC-002)
- **INV-2 (zero ANSI on degradation)**: When `IsInteractive=false` or
  `ColorEnabled=false`, `resolveValidation Rich` returns exactly `renderText
  report` with `UsedRichRendering=false` and no ESC (0x1B) byte. (FR-005/SC-003)
- **INV-3 (sensed fence untouched)**: The `sensed` block stays `null` in the JSON
  projection; the rich view may surface populated sensed fields but rendering them
  changes no JSON/text byte. (FR-002/FR-003)
- **INV-4 (not a persisted artifact / not a golden)**: Rich output is never
  written to `--out` and is excluded from every deterministic/golden contract;
  `--out` always receives JSON or plain text. (FR-008/FR-010)
- **INV-5 (projection completeness, no invention)**: Every human-relevant
  populated fact (verdict, summary counts, each matrix name+dimensions, every
  non-passing cell's coordinates+status, each failure diagnostic) appears in the
  rich output; no fact absent from the report appears.
  `schemaVersion`/`generatorVersion` are excluded from this requirement.
  (FR-002/SC-004)
- **INV-6 (status differentiation)**: `fail`, `coverageGap`, and `notValidated`
  are visually distinct as failing from `skippedWithReason` (non-failing) and
  `pass`. (FR-007)
