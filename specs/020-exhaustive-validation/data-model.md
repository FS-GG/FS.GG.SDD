# Phase 1 Data Model: Scheduled Exhaustive Validation of Broad Matrices

Types are sketched in F# shorthand; the authoritative surface is the `.fsi` files
authored in Phase 2. All types live in `FS.GG.SDD.Validation`.

## Core types (`ValidationContracts`)

```fsharp
/// One declared broad-coverage matrix: a name, its ordered dimension names, and the
/// enumerated cross-product of cells it intends to cover (FR-001).
type Matrix =
    { Name: string                 // e.g. "lifecycle-output"
      Dimensions: string list      // e.g. [ "command"; "projection"; "state" ]
      Cells: MatrixCell list }     // the declared cross-product (coordinates only)

/// One coordinate in a matrix, identified by its dimension values, plus the recorded
/// status once the runner has evaluated it.
and MatrixCell =
    { Coordinates: (string * string) list   // dimension name -> value, in Dimensions order
      Status: CellStatus }

/// The four visible outcomes (Decision 7). Exactly one per evaluated cell.
and CellStatus =
    | Pass
    | Fail of Diagnostic                 // actionable; names matrix + coords + artifact
    | SkippedWithReason of reason: string   // intentional N/A (e.g. ship on a fresh project)
    | CoverageGap of surface: string        // a real surface/value no cell covers (FR-009/FR-012)
    | NotValidated of reason: string        // unproven / unfinished / missing baseline (never a pass)

/// A determinism/degradation dimension value (FR-003) plus host-variance
/// determinism (spec Edge Cases). The first four are color/TTY *degradation*
/// classes; `PerturbedHostEnvironment` is a *determinism-under-host-variance*
/// class: the same output produced under a varied locale, time zone, working
/// directory, or ordering MUST be byte-identical (these are not part of any
/// contract).
type EnvironmentClass =
    | ColorDisabled            // NO_COLOR present
    | TermDumb                 // TERM=dumb
    | NonInteractiveRedirected // output is redirected / not a TTY
    | Interactive
    | PerturbedHostEnvironment // locale / time zone / cwd / ordering varied; output MUST be unchanged

/// Operational triage facts, explicitly excluded from the deterministic comparison
/// (FR-007). Carried under a single fenced object so the byte-stable contract is the
/// rest of the report.
type SensedMetadata =
    { StartedAtUtc: string option   // ISO-8601, sensed; null in deterministic comparison
      DurationMs: int option
      Host: string option }

/// The single deterministic machine-readable report (FR-006/FR-007).
type ValidationReport =
    { SchemaVersion: int             // = 1
      GeneratorVersion: GeneratorVersion
      Matrices: Matrix list          // every matrix, every cell, every status
      Summary: ReportSummary         // counts per status, overall pass/fail
      Sensed: SensedMetadata }       // EXCLUDED from the deterministic comparison

and ReportSummary =
    { Passed: int; Failed: int; Skipped: int; CoverageGaps: int; NotValidated: int
      OverallPassed: bool }          // false if any Fail/CoverageGap/NotValidated
```

## The four declared matrices (Decision 3)

| Matrix | Dimensions | Cell source | Skip vs gap |
|---|---|---|---|
| `lifecycle-output` | command × projection × state | 13 commands × {Json,Text,Rich} × representative states | command invalid for state ⇒ `SkippedWithReason`; a command/projection/state value present in the real surface but absent from any cell ⇒ `CoverageGap` |
| `determinism` | output × environment | 9 generated views + `command-report (--json)` × 5 `EnvironmentClass` (incl. `PerturbedHostEnvironment`) | a view that cannot be produced for the fixture ⇒ `SkippedWithReason`; nondeterministic reproduction (incl. under host-variance) ⇒ `Fail` |
| `baseline-conformance` | contract × check | every `release-readiness.json` catalog entry × {baseline, conformance} | missing baseline/source/field drift ⇒ `NotValidated`/`Fail` (from `ReleaseContract.evaluate`) |
| `compatibility` | entry × check | every `compatibility[]` record × {handoff `contractVersion`, Spec Kit range} | Governance not integrated ⇒ optional fact recorded, cell `Pass`/`SkippedWithReason` — never `Fail` |

## Harness state (`ValidationHarness`, pure — Constitution V)

```fsharp
type ValidationModel =
    { PendingCells: PlannedCell list      // declared cross-product still to evaluate
      EvaluatedCells: MatrixCell list     // folded results
      Diagnostics: Diagnostic list
      Report: ValidationReport option }

type ValidationMsg =
    | CellEvaluated of matrix: string * MatrixCell
    | SurfaceReconciled of gaps: CellStatus list   // CoverageGap / stale-entry findings
    | BuildReport

/// Requested I/O the edge interpreter fulfills (Constitution V). The harness itself
/// runs no commands and touches no files.
type ValidationEffect =
    | RunCommandCell of command: SddCommand * projection: OutputFormat * state: string
    | ReadProducedArtifact of contract: string
    | ReproduceForEnvironment of contract: string * EnvironmentClass
    | EvaluateBaselineConformance        // delegates to ReleaseContract.evaluate
    | ReconcileDeclaredSurface           // declared coverage vs real surface (FR-012)
```

`init` enumerates the declared cross-product into `PendingCells` and emits the
effects above; `update` folds each `CellEvaluated`/`SurfaceReconciled` into
`EvaluatedCells`; `BuildReport` projects the final `ValidationReport`. The
`ValidationRunner` edge interpreter performs the real I/O (drive `CommandWorkflow`,
read artifacts, call `ReleaseContract.evaluate`) and feeds results back as messages.

## Invariants

- **INV-1 (total coverage)**: every declared cell of every matrix appears in the
  report with exactly one `CellStatus` — none omitted (SC-001).
- **INV-2 (byte-stable)**: serializing the report twice over identical source inputs
  is byte-identical after the `Sensed` object is excluded (SC-004); the serialized
  form carries no clock, duration, host path, ordering nondeterminism, or ANSI (FR-007).
- **INV-3 (determinism cells)**: a `determinism`-matrix cell passes iff the produced
  output reproduces byte-identically over identical inputs; a `--rich` output passes
  the degradation check iff it emits zero ANSI under `ColorDisabled`/`TermDumb`/
  `NonInteractiveRedirected` and changes no JSON byte, stream routing, or exit code
  versus the default projection (FR-003).
- **INV-3a (host-variance determinism)**: a `PerturbedHostEnvironment` cell passes iff
  the produced output is byte-identical when the locale, time zone, working directory,
  and ordering are varied — these host-environment facts are not part of any contract
  and MUST NOT change deterministic output (spec Edge Cases).
- **INV-4 (baseline never passes by absence)**: a catalog contract with no baseline,
  no source artifact, or field drift is `NotValidated`/`Fail`, never `Pass` (FR-004,
  SC-003); the matrix delegates this judgment to `ReleaseContract.evaluate`.
- **INV-5 (sensed fence)**: every wall-clock/duration/host fact appears only inside
  `Sensed`; no such fact leaks into any `Matrix`/`MatrixCell`/`Summary` field (FR-007).
- **INV-6 (skip ≠ gap ≠ not-validated)**: the three non-pass-by-default outcomes are
  distinct DU cases and a `CoverageGap` or stale-entry finding makes
  `Summary.OverallPassed = false` (FR-009/FR-012).
- **INV-7 (real surface authoritative)**: when a declared cell names a surface absent
  from the real produced surface, the cell is a detectable failure and the real surface
  wins; an uncovered real surface yields a `CoverageGap` (FR-012, SC-005). The "real
  surface" MUST be enumerated from an authoritative source **independent of the declared
  matrix**, per dimension, or a newly added surface would escape both lists and never
  surface as a gap:
  - **commands** — an **exhaustive match over the `SddCommand` DU** (adding a case is a
    compile-time break that forces the author to cover it; no reflection — Constitution IV);
  - **catalog contracts** — the `release-readiness.json` catalog (018-authoritative);
  - **generated views** — the produced `readiness/<id>/` directory listing of a real run.
- **INV-8 (no Governance verdict)**: no report field encodes a Governance route,
  profile, freshness, gate, or release verdict; Governance compatibility appears only
  as an optional integration fact and never sets `OverallPassed = false` by absence
  (FR-005/FR-010, SC-006/SC-008).

## Exit code

`OverallPassed = true` ⇒ exit 0; any `Fail`/`CoverageGap`/`NotValidated` ⇒ exit
non-zero (FR-006). Sensed metadata never affects the exit code.
