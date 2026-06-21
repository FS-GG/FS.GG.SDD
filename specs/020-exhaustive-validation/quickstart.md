# Quickstart: Scheduled Exhaustive Validation of Broad Matrices

Runnable validation scenarios proving the feature end-to-end. Each maps to a user
story / acceptance scenario and to a test in `FS.GG.SDD.Validation.Tests`. See
[data-model.md](./data-model.md) and [contracts/](./contracts/) for the shapes.

## Prerequisites

```bash
dotnet build -c Release FS.GG.SDD.sln
CLI="dotnet run -c Release --project src/FS.GG.SDD.Cli --"   # or the published fsgg-sdd
```

## Scenario 1 — Exhaustive run produces a complete deterministic report (US1.1)

```bash
$CLI validate --json > validation-report.json
echo "exit=$?"
```

Expect: a `validation-report` with `schemaVersion: 1`, all four matrices, every
declared cell carrying exactly one status, a `summary` with per-status counts, and
exit `0` when `overallPassed` (non-zero if any cell `fail`/`coverageGap`/
`notValidated`). Maps to `LifecycleMatrixTests` / contract validation-report C-5.

## Scenario 2 — Seeded single-cell regression is caught exactly (US1 Independent Test)

In a test fixture, perturb one command/projection/state cell (e.g. make `plan --text`
diverge for the `planReady` state). Run the harness.

Expect: that exact cell is `fail` with a diagnostic naming the matrix, the cell
coordinates, and the affected artifact; **all other cells pass**. Maps to
`LifecycleMatrixTests` seeded-regression test (SC-005-adjacent / FR-006).

## Scenario 3 — Determinism & rich degradation (US1.2 / US1.3)

Expect, from the `determinism` matrix: every generated view and `command-report
(--json)` reproduces byte-identically over identical inputs (cell `pass`; a diff
fails the cell naming the view); and every `--rich` output under `ColorDisabled`,
`TermDumb`, or `NonInteractiveRedirected` emits **zero ANSI** and changes no JSON byte,
stream routing, or exit code versus the default projection. Maps to
`DeterminismMatrixTests` (FR-003 / INV-3).

## Scenario 4 — Baseline conformance, never a silent pass (US1.4)

Expect, from the `baseline-conformance` matrix: every catalogued contract has a
locking baseline and a conforming produced artifact (cell `pass`), or is reported
`notValidated`/`fail` — never `pass` by absence. Reuses `ReleaseContract.evaluate`.
Maps to `BaselineMatrixTests` (FR-004 / SC-003 / INV-4).

## Scenario 5 — No public surface escapes coverage (US2)

Add a public command/view/contract that no matrix cell covers, then run.

Expect: a `coverageGap` finding naming the uncovered surface and
`summary.overallPassed = false`. Conversely, a declared cell naming a surface that no
longer exists is a detectable failure and the real surface wins. Maps to
`CoverageGapTests` (FR-009 / FR-012 / SC-005 / INV-6 / INV-7).

## Scenario 6 — Byte-stable double run, sensed fenced off (SC-004)

```bash
$CLI validate --json > a.json
$CLI validate --json > b.json
# normalize the sensed object, then compare
diff <(jq '.sensed=null' a.json) <(jq '.sensed=null' b.json) && echo "byte-stable"
```

Expect: identical after the `sensed` object is excluded. Maps to
`ReportDeterminismTests` (FR-007 / INV-2 / INV-5).

## Scenario 7 — Runs with no Governance, no required inner-loop step (US3 / SC-006 / SC-007)

```bash
# No Governance runtime installed:
$CLI validate --json > validation-report.json && echo "ran without Governance"
# Inner loop is unchanged — validate is never invoked by a lifecycle command:
$CLI ship --root . --work 001-demo --title Demo   # behavior & runtime unchanged
```

Expect: the harness completes and emits its report with no Governance present, and no
artifact carries a Governance route/profile/freshness/gate/release verdict; the
existing fast commands gain no required `validate` step. Maps to `IsolationTests`
(FR-008 / FR-010 / SC-006 / SC-007 / INV-8).

## Scenario 8 — Compatibility recorded as an optional fact (SC-008)

Expect, from the `compatibility` matrix: each declared entry is exercised against the
produced `governance-handoff.json` `contractVersion` and the supported Spec Kit range,
recorded as an optional integration fact — never failing the run by Governance absence.
Maps to `IsolationTests` / `BaselineMatrixTests` (FR-005 / INV-8).
