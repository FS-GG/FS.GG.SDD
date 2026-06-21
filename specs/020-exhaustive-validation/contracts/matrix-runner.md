# Contract: matrices, `ValidationHarness`, and `ValidationRunner`

## The four declared matrices (FR-001 / Decision 3)

Each matrix is an inspectable named record (`name`, `dimensions`, declared `cells`).
The cross-product is enumerable before any I/O so coverage is auditable.

1. **`lifecycle-output`** — `command × projection × state`. Commands: the 13 public
   commands (`init, charter, specify, clarify, checklist, plan, tasks, analyze,
   evidence, verify, ship, agents, refresh`). Projections: `Json, Text, Rich`.
   States: the representative ladder (`fresh, specified, planReady, tasksReady,
   analyzed, evidenced, verified/shipped, blocked`). (FR-002)
2. **`determinism`** — `output × environment`. Outputs: the 9 generated views plus
   `command-report (--json)` from the release catalog. Environments: `ColorDisabled,
   TermDumb, NonInteractiveRedirected, Interactive, PerturbedHostEnvironment` (locale/
   time zone/cwd/ordering varied — host facts not part of any contract). (FR-003 +
   spec Edge Cases)
3. **`baseline-conformance`** — `contract × {baseline, conformance}` over every
   `release-readiness.json` catalog entry. (FR-004)
4. **`compatibility`** — `entry × {handoff contractVersion, Spec Kit range}` over
   every `compatibility[]` record. (FR-005)

## `ValidationHarness` (pure Elmish — Constitution V)

`val init: MatrixPlan -> ValidationModel * ValidationEffect list` — enumerates the
declared cross-product into pending cells and emits the requested I/O effects.

`val update: ValidationMsg -> ValidationModel -> ValidationModel * ValidationEffect
list` — pure transition folding `CellEvaluated` / `SurfaceReconciled` results;
`BuildReport` projects the final `ValidationReport`.

The harness runs **no** commands and touches **no** files; it only computes the
declared plan and folds reported results. (data-model `ValidationEffect`)

## `ValidationRunner` (edge interpreter)

`val run: RunnerOptions -> ValidationReport` — the single entry point the CLI calls.
Performs all real I/O and feeds results back as `ValidationMsg`s.

- **C-1 (real runs)**: builds each work-item state by driving the real
  `FS.GG.SDD.Commands.CommandWorkflow` over a disposable temp directory and the
  existing `Commands` effect interpreter, then invokes the command-under-test per
  projection and records its outcome as a cell (Decision 4 / Constitution VI).
- **C-2 (skip vs gap)**: a command invalid for a state is `SkippedWithReason`; a real
  command/projection/state value with no declared cell is a `CoverageGap` (FR-009).
- **C-3 (determinism)**: produces each catalogued output twice over identical inputs
  and fails the cell on any byte difference (INV-3).
- **C-3a (host-variance)**: for the `PerturbedHostEnvironment` class, produces each
  catalogued output under a varied locale, time zone, working directory, and ordering
  and fails the cell on any byte difference from the neutral production — host facts
  are not part of any contract and MUST NOT change deterministic output (INV-3a / spec
  Edge Cases).
- **C-4 (degradation)**: for `--rich` outputs under `ColorDisabled`/`TermDumb`/
  `NonInteractiveRedirected`, asserts zero ANSI and that JSON bytes, stream routing,
  and exit code are unchanged versus the default projection (FR-003 / INV-3). Reuses
  the feature-019 `Cli.Rendering` degradation behavior; it does not re-implement it.
- **C-5 (baseline)**: snapshots a real shipped project's produced artifacts and calls
  `ReleaseContract.evaluate release produced`; each diagnostic becomes a `Fail`/
  `NotValidated` cell, an empty result is all-pass (Decision 5 / INV-4).
- **C-6 (compatibility)**: confirms the produced `governance-handoff.json`
  `contractVersion` conforms to each declared `compatibility[].governanceContract
  VersionRange`, recorded as an **optional integration fact**, never a Governance
  verdict (FR-005 / INV-8). For the **Spec Kit range**: SDD produces no Spec-Kit-version
  artifact, so the cell asserts only that each `compatibility[].specKitRange` is
  **present and parseable** in `release-readiness.json` (a well-formedness check, not a
  comparison against a running Spec Kit); the cell is `SkippedWithReason` with that
  rationale when no Spec Kit version is observable, never `Fail` by Governance/tooling
  absence.
- **C-7 (reconciliation)**: reconciles declared coverage against the real produced
  surface; a declared cell naming a vanished surface is a detectable failure and the
  real surface is authoritative (FR-012 / INV-7). The real surface is enumerated from a
  source **independent of the declared matrix**, per dimension: commands via an
  **exhaustive `SddCommand` match** (a new case is a compile-time break — no reflection),
  catalog contracts via `release-readiness.json`, generated views via the produced
  `readiness/<id>/` directory listing. This independence is what makes a newly added
  surface a detectable `CoverageGap` rather than a silent co-omission.
- **C-8 (no Governance)**: runs to completion and emits a report with no Governance
  runtime installed; Governance absence is a clean run (FR-010 / INV-8).
- **C-9 (interruption)**: any cell not reached is left `NotValidated`; the report is
  always well-formed and complete in shape (FR-007 / INV-1 / C-5 of validation-report).

## Isolation (FR-008 / US3)

`ValidationRunner` is reachable only via `fsgg-sdd validate`; no lifecycle command,
`init`/`update`, or effect interpreter references it. Adopting the harness adds no
required step to any existing command and leaves their behavior and runtime unchanged
(SC-007).
