# Phase 1 Data Model: Rich Spectre.Console CLI Rendering

Feature: `019-spectre-rendering` · Date: 2026-06-21

This feature introduces **no new authored-source schema, no new generated machine
artifact, and no new fields on `CommandReport`**. It adds one public type case and
two CLI-edge presentation types. The "data" here is the projection mapping from
the existing report object to rich renderable content.

## Changed / new types

### `OutputFormat` (changed — `FS.GG.SDD.Commands.CommandTypes`)

Extend the existing discriminated union:

```fsharp
type OutputFormat =
    | Json
    | Text
    | Rich   // new — presentation-only terminal projection
```

- Tier 1 public-surface change → `CommandTypes.fsi` updated and any public-surface
  baseline refreshed.
- `outputFormatValue` MUST map `Rich -> "rich"` (string identity for reports/logs).
- `Rich` carries no automation meaning: `serializeReport` is unaffected; the JSON
  contract still emits whatever `OutputFormat` was requested as a string only, as
  it does today for `Text`.

### `TerminalCapabilities` (new — CLI edge, `FS.GG.SDD.Cli.Rendering`)

Pure record describing the detected output environment, computed once at the CLI
edge:

| Field | Type | Meaning |
|---|---|---|
| `IsInteractive` | `bool` | stdout is a TTY (not redirected/piped) |
| `ColorEnabled` | `bool` | color permitted (not `NO_COLOR`, not `TERM=dumb`) |
| `Width` | `int option` | terminal width if known, else `None` (renderer adapts) |

Detection is the only impure step (reads `Console.IsOutputRedirected`,
environment variables); it is isolated so the renderer itself stays pure.

### `RichRenderResult` (new — CLI edge)

Result of choosing and producing a rendering for one report:

| Field | Type | Meaning |
|---|---|---|
| `Text` | `string` | the rendered output to write |
| `UsedRichRendering` | `bool` | `true` if Spectre rich output was produced; `false` if degraded to plain text |

`UsedRichRendering=false` guarantees the `Text` field is byte-identical to
`CommandRendering.renderText report` (the existing plain-text projection).

## Projection mapping (report → rich content)

The rich renderer MUST represent every populated part of `CommandReport`. No part
of the report may be dropped and nothing absent from the report may be added
(SC-004, FR-002). Mapping (presentation grouping only — exact visuals are an
implementation detail):

| `CommandReport` source | Rich representation | Notes |
|---|---|---|
| `Command`, `WorkId`, `DryRun` | header line / title | identity |
| `Outcome` (`Succeeded` / `SucceededWithWarnings` / `Blocked` / `NoChange`) | color-coded status badge | green / yellow / red / dim |
| `ChangedArtifacts` | count + list | matches text projection counts |
| per-stage summaries (`Specification` … `Refresh`) | section panel/table for whichever is `Some` | one stage populated per command, mirrors `renderText` fields |
| `GeneratedViews` | currency table (view → state) | stale views emphasized |
| `Diagnostics` | table grouped by `DiagnosticSeverity` (Error/Warning/Info) | severity-colored |
| `NextAction` | callout with `ActionId`, next `Command`, `Reason`, required artifacts | "none" when `None` |
| `GovernanceCompatibility` | optional facts section | presentation only |

The report's machine-contract envelope metadata — `SchemaVersion`,
`ReportVersion`, `ProjectRoot`, `OutputFormat`, `OverwritePolicy` — is
**intentionally not rendered**: it exists for the JSON automation contract, not
for the human reader, and its omission is not a completeness gap (FR-002,
SC-004). All other populated fields above MUST be represented.

## Invariants

- **INV-1 (automation truth)**: `serializeReport report` bytes are independent of
  `OutputFormat` and of any rendering. Adding/selecting `Rich` changes no JSON
  byte and no exit code (FR-003, SC-002).
- **INV-2 (no ANSI on degradation)**: when `TerminalCapabilities.ColorEnabled` is
  `false` or `IsInteractive` is `false`, rendered output contains zero ANSI/color
  control sequences (FR-005, SC-003).
- **INV-3 (stream/exit parity)**: stream routing (stdout vs stderr) and exit code
  for a given `Outcome` are identical across `Json`, `Text`, and `Rich`
  (FR-006, SC-005).
- **INV-4 (no schema growth)**: no new `CommandReport` field, no new authored
  source, no new generated artifact, no new lifecycle stage; `nextLifecycleCommand`
  ordering is unchanged (FR-009).
- **INV-5 (projection completeness)**: every human-relevant populated report field
  is represented in rich output; rich output invents no fact absent from the report
  (FR-002, SC-004). The machine-contract envelope metadata (`SchemaVersion`,
  `ReportVersion`, `ProjectRoot`, `OutputFormat`, `OverwritePolicy`) is
  intentionally excluded and is not a completeness omission.
