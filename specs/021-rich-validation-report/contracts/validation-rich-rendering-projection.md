# Contract: validation rich-rendering projection

Defines the public surface and behavioral contract of the rich `validation-report`
projection added to `FS.GG.SDD.Cli.Rendering`. The rich rendering is a pure
projection over the same `ValidationReport` the JSON and plain-text projections
use.

## Public surface (added to `Rendering.fsi`)

```fsharp
open FS.GG.SDD.Validation.ValidationContracts

/// Render a validation-report into the given Spectre console. Pure over the
/// report: the only observable mutation is to the supplied console.
val renderValidationRichTo: console: IAnsiConsole -> report: ValidationReport -> unit

/// Resolve the effective stdout rendering for a requested format + capabilities,
/// degrading Rich -> plain text when non-interactive or color-disabled.
val resolveValidation:
    format: OutputFormat ->
    capabilities: TerminalCapabilities ->
    report: ValidationReport ->
        RichRenderResult
```

These are the only two additions to the public CLI surface (two new
`PublicSurface.baseline` lines). `TerminalCapabilities`, `RichRenderResult`,
`selectFormat`, and `detectCapabilities` are reused unchanged.

## Behavioral contract

- **C-1 (pure over the report)**: `renderValidationRichTo` writes only to the
  supplied `IAnsiConsole`. It does not mutate the report, read global mutable
  state, or perform I/O beyond the console. `resolveValidation` returns a
  `RichRenderResult` and mutates nothing. (INV-1)

- **C-2 (faithful, complete projection)**: The rich rendering represents every
  human-relevant populated fact of the report — overall verdict, the five summary
  counts, each matrix's name and dimensions, every non-passing cell's coordinates
  and status, and each failure diagnostic (matrix / coordinates / affected
  contract). It invents no fact absent from the report. `schemaVersion` and
  `generatorVersion` are intentionally not required and are not counted as
  omissions. (INV-5 / FR-002)

- **C-3 (verdict + rollup + status emphasis)**: The rendering visually
  distinguishes the overall verdict (passed vs not passed), presents a per-matrix
  status rollup, and styles `fail`/`coverageGap`/`notValidated` as failing —
  distinct from `skippedWithReason` (non-failing) and `pass`. (INV-6 / FR-007)

- **C-4 (degradation = exact plain text, zero ANSI)**: `resolveValidation Rich`
  with `IsInteractive=false` or `ColorEnabled=false` returns exactly
  `renderText report` with `UsedRichRendering=false` and contains no ESC (0x1B)
  byte. `Json`/`Text` return `serialize`/`renderText` byte-for-byte. (INV-2 / C-1)

- **C-5 (no contract drift, not persisted, not golden)**: Producing the rich
  rendering changes no `serialize`/`renderText` byte, leaves the `sensed` fence
  `null` in JSON, and is never written to `--out`. The rich output is excluded
  from every deterministic/golden contract. (INV-3 / INV-4 / FR-003 / FR-008 /
  FR-010)

- **C-6 (sensed optional)**: The rich rendering MAY surface populated `sensed`
  fields for human context but MUST NOT require them; with the current
  `emptySensed` report nothing is shown and nothing is invented. (FR-002)

## Verification

Rendered to a `StringWriter`-backed Spectre `IAnsiConsole` with
`AnsiSupport.No` / `ColorSystemSupport.NoColors` and a fixed width (the established
`RichRenderingTests` pattern), assertions cover:

- presence of the verdict indicator, all five summary counts, each matrix name,
  and every non-passing cell's coordinates + status + (for `fail`) the diagnostic;
- absence of any cell/fact not in the report and of `pass`-cell enumeration noise;
- a report whose only non-passing cells are `coverageGap`/`notValidated` still
  renders a not-passed verdict;
- an all-pass report renders a passed verdict with the rollup and no invented
  diagnostics;
- a single forced-failing cell is isolated (its coordinates appear; siblings do
  not falsely appear as failing);
- zero ESC bytes in the color-off rendering and in redirected end-to-end runs;
- `serialize`/`renderText` bytes and the `sensed` null fence unchanged
  (automation invariance).
