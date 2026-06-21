# Implementation Plan: Scheduled Exhaustive Validation of Broad Matrices

**Branch**: `020-exhaustive-validation` | **Date**: 2026-06-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/020-exhaustive-validation/spec.md`

**Change Tier**: Tier 1 (contracted change: adds a new public `FS.GG.SDD.Validation`
library with `.fsi` surface, a new deterministic `validation-report` JSON contract,
and a new CLI-level `fsgg-sdd validate` command). It adds **no** lifecycle stage and
changes **no** authored-source schema or existing per-command `CommandReport`
contract (FR-011).

## Summary

Deliver the one remaining SDD-owned roadmap item (Phase 13's "scheduled exhaustive
validation for broad matrices," deferred by feature 018). Add a cross-cutting
validation harness — `fsgg-sdd validate` — that exhaustively exercises SDD's broad
matrices instead of the representative samples the inner loop runs:

1. **lifecycle-output matrix** — every public lifecycle command (`init`…`ship`) and
   cross-cutting command (`agents`, `refresh`) × every output projection
   (`--json`/default, `--text`, `--rich`) × an enumerated representative set of
   work-item lifecycle states;
2. **determinism / degradation matrix** — every public generated readiness view and
   every `--json` command-output × environment class (color-disabled, `TERM=dumb`,
   non-interactive/redirected, interactive), asserting byte-identical reproduction
   and the documented rich-degradation rules;
3. **baseline-conformance matrix** — every public contract in the release schema
   reference × {locking baseline present, produced artifact conforms}, reusing the
   feature-018 `ReleaseContract.evaluate`;
4. **compatibility matrix** — every published compatibility entry × produced
   Governance handoff `contractVersion` conformance (recorded as an optional
   integration fact, no Governance verdict computed).

The harness emits **one deterministic, machine-readable `validation-report`** naming
every matrix, every cell, each cell's `pass | fail | skipped(reason)` status, plus
`coverageGap` / `notValidated` findings, with per-failure diagnostics identifying the
matrix, cell coordinates, and affected contract/artifact. It runs on demand and on a
schedule, is fully separate from the cheap inner loop (US3), runs with no Governance
runtime present (FR-010), and treats the **real produced surface as authoritative** so
no public surface escapes coverage by omission (FR-012). The concrete CI cron wiring is
operational configuration and out of scope.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (repo default), `LangVersion=preview`,
`Deterministic=true`.

**Primary Dependencies**: existing `FS.GG.SDD.Commands` (drives the lifecycle
`CommandWorkflow` + effect interpreter to produce real artifacts and reports),
`FS.GG.SDD.Artifacts` (`ReleaseContract`, `GovernanceHandoff`, canonical
serialization), `FSharp.Core`, `System.Text.Json`. **No** Spectre.Console dependency
in the new library (the report's JSON/text projections are the contract; rich is out of
MVP scope — see research Decision 6). No Governance package dependency (FR-010).

**Storage**: No new persisted authored artifact. The harness reads a disposable project
tree it drives in-process (like the existing `TestSupport` lifecycle runs) and emits the
`validation-report` to stdout (the deterministic automation contract); a scheduled run
redirects stdout to a file. Optionally the runner can write the report to a caller-named
path, but the path is not a fixed lifecycle artifact.

**Testing**: xUnit. New `FS.GG.SDD.Validation.Tests` project mirrors the existing
`FS.GG.SDD.Commands.Tests` real-fixture style (disposable temp directories, real
`CommandWorkflow` runs, no mocks). Seeded-regression, coverage-gap, byte-stability,
no-Governance, and inner-loop-isolation tests prove the independent tests in the spec.

**Target Platform**: Cross-platform CLI (Linux/macOS/Windows); scheduled CI runner.

**Project Type**: Single repository, library + CLI host. The harness is a new
**cross-cutting validation library** plus a CLI-level command (it is **not** a
`SddCommand`, so the lifecycle command surface and `CommandReport` contract stay
byte-for-byte unchanged — FR-011).

**Performance Goals**: The exhaustive run is deliberately the **expensive** path (full
cross-product), invoked on a schedule / on demand — not the inner loop. No latency
budget; the constraint is the inner loop's runtime must be **unchanged** (US3/SC-007),
which is structural (the harness adds no required step to existing commands).

**Constraints**: The `validation-report` JSON is byte-stable over identical source
inputs and excludes implicit clocks, durations, host paths, ordering nondeterminism, and
ANSI styling (FR-007). Any wall-clock/duration/host fact is carried only under an
explicitly-marked `sensed` block excluded from the deterministic comparison. The harness
defines, computes, and enforces **no** Governance route/profile/freshness/gate/release
verdict (FR-010).

**Scale/Scope**: 13 command shapes × 3 projections × ~7 representative states (lifecycle
matrix); 10 catalogued contracts × 5 environment classes incl. `PerturbedHostEnvironment`
(determinism matrix); 10 schema-reference contracts (baseline matrix); 1 compatibility
entry (compatibility matrix). One new library (3 module pairs), one new CLI command branch, one new test
project, one new `validation-report` JSON contract. No new package dependency.

## Constitution Check

*GATE: must pass before Phase 0 and re-checked after Phase 1.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec → FSI → FSI-exercise → Tests → Impl | PASS | `.fsi` authored for every new module (`ValidationContracts`, `ValidationHarness`, `ValidationRunner`) before `.fs`; public surface exercised in FSI; semantic tests precede implementation. |
| II. Structured artifacts are the machine contract | PASS | The `validation-report` JSON is the authoritative machine contract; the `--text`/`--rich` projections add/drop no facts. Determinism vs. the deterministic JSON is asserted; sensed metadata is explicitly fenced off (data-model INV-2/INV-5). |
| III. Visibility in `.fsi` | PASS | Every public module gets a `.fsi`; a `PublicSurface.baseline` is added for the new library and asserted by a surface test (mirrors `FS.GG.SDD.Cli.Tests/SurfaceBaselineTests.fs`). |
| IV. Idiomatic simplicity | PASS | Records + DUs (matrices, cells, status), plain folds over the cross-product; no custom operators, SRTP, reflection, providers, or heavy CEs. Reuses `ReleaseContract.evaluate` and the existing effect interpreter rather than re-implementing them. |
| V. Elmish/MVU boundary for state/I/O | PASS | `ValidationHarness` is a pure `init`/`update` over `Model`/`Msg`, emitting `ValidationEffect`s (run-command, read-artifact, reconcile-surface). `ValidationRunner` is the edge interpreter performing all real I/O, reusing the `FS.GG.SDD.Commands` interpreter. |
| VI. Test evidence mandatory | PASS | New tests fail-before/pass-after: seeded single-cell regression (US1), coverage-gap detection (US2), byte-stable double-run (SC-004), no-Governance run (SC-006), inner-loop isolation (SC-007), rich-degradation matrix. Real temp-dir fixtures, real command runs, no mocks. |
| VII. Agent + human one contract | PASS | CLAUDE.md, AGENTS.md, and both `fs-gg-sdd-project` skills document `fsgg-sdd validate` and its scheduled/on-demand posture identically; README/docs note the new command. The harness is not a second source of truth — the real produced surface is authoritative (FR-012). |
| VIII. Observability & safe failure | PASS | Every failing cell carries an actionable diagnostic (matrix, cell coordinates, affected contract). `skipped-with-reason` ≠ `coverageGap` ≠ `notValidated` are distinct, visible states; an interrupted/partial run marks unfinished cells `notValidated` (never pass). Governance absence degrades to an optional integration fact, never a hard failure. |

**Result**: PASS — no violations; Complexity Tracking not required.

Lifecycle-feature plan checklist (Development Workflow):

- **Authored artifacts**: none changed — no authored-source schema touched (FR-011).
- **Structured machine contracts**: new `validation-report` JSON (versioned
  `schemaVersion = 1`). Existing `CommandReport`, generated views, `release-readiness.json`,
  and `governance-handoff.json` contracts are unchanged and consumed read-only.
- **Generated views**: none changed; the harness reads existing generated views, it does
  not define a new one.
- **Schema version & migration posture**: the harness adds a **new** contract at
  `schemaVersion = 1`; no existing contract's version bumps. No migration note required
  (no breaking change). The new command is additive.
- **Agent-facing behavior (Claude & Codex)**: both gain an identical description of
  `fsgg-sdd validate` (scheduled/on-demand, separate from the inner loop, no Governance
  required).
- **Optional Governance integration**: the compatibility matrix records Governance
  handoff `contractVersion` conformance only as an optional integration fact; absence of
  Governance is a clean run, not a failure (FR-010, FR-005).
- **Tests/fixtures for stale or conflicting artifacts**: coverage-gap and
  stale-matrix-entry reconciliation tests prove declared coverage vs. the real surface
  (FR-012); byte-stability tests guard the report contract.

## Project Structure

### Documentation (this feature)

```text
specs/020-exhaustive-validation/
├── plan.md              # This file
├── research.md          # Phase 0 decisions
├── data-model.md        # Phase 1 types, matrices, invariants
├── quickstart.md        # Phase 1 validation scenarios
├── contracts/
│   ├── validation-report.md      # the deterministic report JSON contract
│   ├── matrix-runner.md          # the four matrices + harness/runner surface
│   └── cli-validate-command.md   # CLI command, flags, exit codes, projections
├── checklists/
│   ├── requirements.md               # spec-quality checklist (/speckit-specify)
│   └── requirements-traceability.md  # req → design-artifact traceability
└── tasks.md             # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.SDD.Artifacts/            # unchanged (consumed: ReleaseContract, GovernanceHandoff, Serialization)
├── FS.GG.SDD.Commands/             # unchanged (consumed: CommandWorkflow + effect interpreter, CommandSerialization)
├── FS.GG.SDD.Validation/           # NEW library — the cross-cutting harness
│   ├── FS.GG.SDD.Validation.fsproj # NEW: refs Artifacts + Commands; no Spectre, no Governance
│   ├── ValidationContracts.fsi/.fs # NEW: Matrix, Cell, CellStatus, EnvironmentClass, ValidationReport, SensedMetadata; declared matrices; canonical serialize + text projection
│   ├── ValidationHarness.fsi/.fs   # NEW: pure Elmish Model/Msg/Effect; init enumerates the cross-product; update folds cell results into the report
│   └── ValidationRunner.fsi/.fs    # NEW: edge interpreter — drives CommandWorkflow over states×projections, reads artifacts, evaluates baseline/compat/coverage
└── FS.GG.SDD.Cli/
    └── Program.fs                  # CHANGED: add a CLI-level `validate` branch (peer of `--version`), project the report json/text

tests/
├── FS.GG.SDD.Commands.Tests/       # unchanged
├── FS.GG.SDD.Cli.Tests/            # may add a `validate` CLI smoke (json/text, exit code)
└── FS.GG.SDD.Validation.Tests/     # NEW project
    ├── FS.GG.SDD.Validation.Tests.fsproj
    ├── PublicSurface.baseline      # NEW: locks the new library's public surface
    ├── SurfaceBaselineTests.fs     # NEW: asserts the baseline
    ├── LifecycleMatrixTests.fs     # NEW: command×projection×state coverage + seeded regression (US1)
    ├── DeterminismMatrixTests.fs   # NEW: byte-identical reproduction + rich degradation (US1.2/US1.3)
    ├── BaselineMatrixTests.fs      # NEW: schema-reference baseline/conformance + not-validated (US1.4)
    ├── CoverageGapTests.fs         # NEW: uncovered surface + stale-entry reconciliation (US2)
    ├── ReportDeterminismTests.fs   # NEW: byte-stable double-run, sensed fenced off (SC-004)
    └── IsolationTests.fs           # NEW: no-Governance run (SC-006) + inner-loop isolation (SC-007)

Directory.Packages.props            # unchanged (no new package)
FS.GG.SDD.sln                       # CHANGED: + FS.GG.SDD.Validation + FS.GG.SDD.Validation.Tests
```

**Structure Decision**: A **new library** `FS.GG.SDD.Validation` (not a module inside
`Commands`) keeps the harness cross-cutting and one-directional: it depends on `Commands`
and `Artifacts` and reuses their workflow + `ReleaseContract.evaluate`, but nothing in the
lifecycle surface depends on it, so the existing command contracts cannot drift to satisfy
the harness. `validate` is dispatched at the **CLI level** (a peer of `--version`), not
added to the `SddCommand` DU, so `CommandReport`, `parseCommand`, and the per-command
output contracts are untouched (FR-011). This mirrors how `agents`/`refresh` are
non-lifecycle, and how feature 018's `ReleaseContract` documents/locks existing contracts
without adding a stage.

## Phase 0 — Research

Complete. See [research.md](./research.md): surface name lock (`fsgg-sdd validate`);
harness placement (new library, CLI-level command, not a `SddCommand`); the four declared
matrices and their representative dimension values; state-construction strategy (drive the
real `CommandWorkflow`, reuse the `Commands` effect interpreter); reuse of
`ReleaseContract.evaluate` for the baseline matrix; report determinism + sensed-metadata
fence; report projection scope (json + text in MVP, rich deferred); coverage reconciliation
(real surface authoritative); and no-Governance posture. No `NEEDS CLARIFICATION` remain.

## Phase 1 — Design & Contracts

Complete:

- [data-model.md](./data-model.md) — `Matrix`, `MatrixCell`, `CellStatus`
  (`Pass | Fail | SkippedWithReason | CoverageGap | NotValidated`), `EnvironmentClass`,
  `ValidationReport`, `SensedMetadata`, the four declared matrices, and invariants
  INV-1…INV-8.
- [contracts/validation-report.md](./contracts/validation-report.md) — the deterministic
  `validation-report` JSON shape, field stability, the sensed-metadata fence, and exit-code
  rule.
- [contracts/matrix-runner.md](./contracts/matrix-runner.md) — the four matrices, the pure
  `ValidationHarness` (`init`/`update`/effects) surface, and the `ValidationRunner` edge
  interpreter behavior C-1…C-9.
- [contracts/cli-validate-command.md](./contracts/cli-validate-command.md) — the
  `fsgg-sdd validate` command, flags, projections, stream routing, and exit codes.
- [quickstart.md](./quickstart.md) — runnable validation scenarios covering each user story.

Agent context update: CLAUDE.md plan pointer updated to this plan; the optional
`speckit.agent-context.update` after_plan hook may refresh the managed section.

## Phase 2 — Task planning approach (preview only)

`/speckit-tasks` will generate `tasks.md`. Expected shape, ordered by the Constitution's
spec→fsi→fsi-exercise→tests→impl discipline and the US priorities:

1. **Setup**: scaffold `FS.GG.SDD.Validation` (fsproj refs Artifacts + Commands) and
   `FS.GG.SDD.Validation.Tests`; add both to the solution. No new package reference.
2. **Contract/signature (US1)**: author `ValidationContracts.fsi` (matrix/cell/status/report
   types + canonical serialize), `ValidationHarness.fsi` (pure Elmish surface),
   `ValidationRunner.fsi` (edge interpreter surface); add `PublicSurface.baseline`.
3. **Tests-first ([P] parallelizable)**: lifecycle-matrix coverage + seeded single-cell
   regression (US1); determinism + rich-degradation matrix (US1.2/US1.3); baseline/conformance
   + not-validated (US1.4); coverage-gap + stale-entry reconciliation (US2); byte-stable
   double-run with sensed fenced off (SC-004); no-Governance + inner-loop isolation (US3/SC-006/SC-007).
4. **Implementation (US1→US2→US3)**: `ValidationContracts` types + serialize/text projection;
   `ValidationHarness` `init`/`update` cross-product fold; `ValidationRunner` interpreter
   (drive workflow over states×projections, read artifacts, reuse `ReleaseContract.evaluate`,
   compat + coverage reconciliation); wire the `validate` branch into `Program.fs`.
5. **Agent/doc alignment (FR-011/Principle VII)**: update CLAUDE.md, AGENTS.md, both
   `fs-gg-sdd-project` skills, README, and docs to describe `fsgg-sdd validate`.
6. **Verification/evidence**: Release build, full suite, FSI public-surface transcript for the
   new modules, disposable-directory CLI smoke for `validate --json`/`--text` incl. a seeded
   regression and a no-Governance run, byte-stability double-run check, SDD/Governance boundary
   review, artifact traceability → `readiness/020-exhaustive-validation/`.

## Complexity Tracking

No constitution violations require justification. The feature is additive: a new
one-directional library and one CLI command branch, reusing the existing command workflow,
effect interpreter, and `ReleaseContract.evaluate` rather than re-implementing them. No new
runtime package, no Governance dependency, no change to any existing public contract.
