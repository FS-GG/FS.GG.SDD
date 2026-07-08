# Implementation Plan: Plan Upstream Snapshot

**Branch**: `090-plan-upstream-snapshot` | **Date**: 2026-07-08 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/090-plan-upstream-snapshot/spec.md`

**Tracks**: FS-GG/FS.GG.SDD#163 (epic FS-GG/FS.GG.SDD#159)

## Summary

`fsgg-sdd plan` writes a synthesized `PD-###` "stale:" line into `work/<id>/plan.md` — a file the
artifact model classifies `AuthoredSource` — and leaves its own `## Source Snapshot` digests stale.
The mutation reaches disk because the staleness is reported as a `DiagnosticWarning`, and
`runHandler`'s effect gate only discards writes on a `DiagnosticError`.

The fix is small and mostly subtractive: delete `appendStalePlanDecision`, and raise the
tool-detected staleness to a new `stalePlanSnapshot` `DiagnosticError` that names the changed
sources. The existing effect gate then delivers the zero-write guarantee for free. `plan
--accept-upstream` becomes the one gesture that re-baselines the snapshot — rewriting the
`## Source Snapshot` body and nothing else. `tasks` and `analyze` detect the same staleness from
the digests, so removing the injected `stale:` marker they used to key on opens no hole.

See [research.md](research.md) for the source-verified basis of each of those claims.

## Technical Context

**Language/Version**: F# on .NET, `net10.0`

**Primary Dependencies**: none new. `Spectre.Console` only via the existing `--rich` projection edge.

**Storage**: filesystem lifecycle artifacts under `work/<id>/`; no database, no schema migration.

**Testing**: xUnit via `dotnet test`; real-filesystem fixtures (constitution VI — no mocks).
New coverage in `tests/FS.GG.SDD.Commands.Tests/PlanCommandTests.fs`.

**Target Platform**: Linux/macOS/Windows CLI (`fsgg-sdd`)

**Project Type**: CLI tool over an Elmish/MVU command core

**Performance Goals**: N/A. Three additional `sha256` computations over files already read.

**Constraints**: byte-deterministic output (FR-014); no persisted schema change (FR-015);
`plan.md` stays `AuthoredSource`; **the working set must stay inside this item's declared
`Paths:` touch-set**, because FS.GG.SDD#174 is running concurrently and owns
`HandlersEarly.fs`, `Prerequisites.fs`, `EarlyStageAuthoring.fs`, and the readiness goldens.

**Scale/Scope**: ~5 source files, ~1 test file, 16 FRs, 14 contract cells.

## Constitution Check

*GATE: passed before Phase 0; re-checked after Phase 1 design (below).*

| Principle | Assessment |
|---|---|
| **I. Spec → FSI → Semantic Tests → Implementation** | Followed. Spec committed first (`727e2dd`); the public surface change is one `CommandRequest` field and one diagnostic constructor, both declared in `.fsi` before the `.fs` bodies; semantic tests through the CLI/public command surface precede the implementation. |
| **II. Structured artifacts are the machine contract** | The prose/structured conflict *is* this feature. Ruling: the `## Source Snapshot` digests are authoritative for staleness; the `stale:` marker in `## Plan Decisions` prose is **not** a tool-writable signal and is demoted to an authored-only one. The conflict is reported as `stalePlanSnapshot` in the `CommandReport` JSON and flows into the generated work model. Recorded in [data-model.md](data-model.md). |
| **III. Visibility lives in `.fsi`** | `CommandTypes.fsi`, `CommandHelp.fsi`, `CommandReports.fsi` updated with the new field/constructor. `ChecklistPlanAuthoring.fs` is an internal module with no `.fsi` (unchanged). The repo's own surface gate is the reflection `PublicSurface.baseline` test, which must be updated if it pins these. |
| **IV. Idiomatic simplicity** | Net **subtractive**: one function deleted, one added. No custom operators, reflection, SRTP, or CEs. |
| **V. Elmish/MVU boundary** | Preserved and *strengthened*. The fix is a severity change consumed by the existing pure `runHandler` effect gate; the `WriteFile` effect stays at the edge. No new I/O and no new effect type. |
| **VI. Test evidence mandatory** | Every contract cell C1–C14 gets a test that fails before and passes after. Real filesystem fixtures. The C2 regression test (byte-identical `plan.md` after a stale re-run) is the one that fails loudest today. |
| **VII. Agent and human share one contract** | The diagnostic and flag are surfaced through the same `CommandReport` for CLI, Claude, Codex, and CI. `CommandHelp` lists the flag; `RemediationPointers` routes the next action. |
| **VIII. Observability and safe failure** | The core of the change: a stale upstream becomes an *actionable* diagnostic at the stage that owns the snapshot, naming the changed sources and the one-command recovery. It is workspace state, not a tool defect, so `IsToolDefect = false` and the blocked exit stays `1` (never `2`). |

**Change tier**: **Tier 1** — new command flag, new diagnostic id in the JSON contract, changed
exit/write behavior on the stale path. Requires spec, plan, tasks, `.fsi`, tests, docs, migration
notes. Migration posture: none required (see the contract's Migration section).

**Post-Phase-1 re-check**: no new violations. No entry in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/090-plan-upstream-snapshot/
├── plan.md              # This file
├── spec.md              # Committed 727e2dd; FR-012 + one edge case amended by Phase 0 research
├── research.md          # Phase 0 — R1..R7, all source-verified
├── data-model.md        # Phase 1 — entities, staleness predicate, state-transition tables
├── quickstart.md        # Phase 1 — end-to-end validation guide
├── contracts/
│   └── plan-accept-upstream.md   # Phase 1 — CLI surface, JSON additions, cells C1..C14
└── tasks.md             # Phase 2 output (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.SDD.Cli/
│   └── Program.fs                                  # parse `--accept-upstream` -> CommandRequest
└── FS.GG.SDD.Commands/
    ├── CommandTypes.fs / .fsi                      # + CommandRequest.AcceptUpstream: bool
    ├── CommandHelp.fs / .fsi                       # + `--accept-upstream` in `plan` help
    ├── CommandReports.fs / .fsi                    # re-export the new constructor
    ├── CommandReports/
    │   ├── DiagnosticConstructors.fs               # + stalePlanSnapshot (DiagnosticError)
    │   └── RemediationPointers.fs                  # + pointer for stalePlanSnapshot   [see note]
    └── CommandWorkflow/
        └── ChecklistPlanAuthoring.fs               # the whole behavior change

tests/
└── FS.GG.SDD.Commands.Tests/
    └── PlanCommandTests.fs                         # C1..C14
```

**Structure Decision**: The behavior change is confined to `ChecklistPlanAuthoring.fs`, which owns
both `planDiagnosticsTextAndSummary` (the `plan` stage) and
`planPrerequisiteDiagnosticsTextSummaryAndFacts` (the `tasks`/`analyze` prerequisite read). Per
research R2, emitting from those two functions covers FR-002 and FR-008 with **no signature change**,
which is what keeps `Prerequisites.fs` and `HandlersEarly.fs` — both owned by the concurrent #174 —
out of the diff. That constraint drove the design, not the other way around.

> **Note on `RemediationPointers.fs`**: it is in the declared touch-set as
> `CommandReports/DiagnosticConstructors.fs`'s neighbor but was *not* listed on the issue's `Paths:`
> line. Before editing it, re-run `scripts/fsgg-coord overlap FS.GG.SDD#174 FS.GG.SDD#163` after
> widening the line — the skill's "widen and re-check" rule. #174 does not touch it, so this is
> expected to stay DISJOINT.

## Implementation approach

1. **`ChecklistPlanAuthoring.fs`**
   - Add `changedPlanSourcePaths workId specText clarificationText checklistText existingFacts` —
     the sibling of `planSourceSnapshotStale` returning the ordinally-sorted changed paths under the
     identical predicate (research R5).
   - Add `refreshPlanSnapshot workId specText clarificationText checklistText text` —
     `replaceSectionBody "Source Snapshot" (sourceSnapshotLines …)`, mirroring `rederiveChecklist`.
   - **Delete** `appendStalePlanDecision`.
   - `planDiagnosticsTextAndSummary`: when stale and **not** `request.AcceptUpstream`, emit
     `stalePlanSnapshot` and return the **existing, unmutated** text. When stale **and**
     `AcceptUpstream`, return `refreshPlanSnapshot …` applied to the entries-appended text, and emit
     nothing. When not stale, behave exactly as today.
   - `planPrerequisiteDiagnosticsTextSummaryAndFacts`: read the three upstream texts from `model`
     (as it already reads `plan.md`), and emit `stalePlanSnapshot` when stale — **ignoring**
     `AcceptUpstream`, which is `plan`'s gesture alone.
   - Retain the `failedPlanPrerequisite "Plan contains stale decisions."` branch (FR-009).

2. **`DiagnosticConstructors.fs`** — `stalePlanSnapshot path changedPaths` via `errorDiagnostic`
   (not `markToolDefect`: exit stays 1). `RemediationPointers.suffixFor` gains the pointer, which
   `commandDiagnostic` already appends for every constructor (research R4) — one wiring point,
   satisfying FR-010.

3. **`CommandTypes` / `Program.fs` / `CommandHelp`** — the additive `AcceptUpstream` field, its
   `hasFlag "--accept-upstream"` parse, and the help row. Additive only; `schemaVersion` stays `1`
   and `reportVersion` stays `1.3.0` (no new report block).

4. **Tests** — C1..C14 plus the SC-002 section-diff invariant and an FR-016 regression (a snapshot
   entry with no digest must not block).

## Sequencing and risk

- **FR-011 (Story 4, P3 — the authoring-window advisory) is implemented last and is the one
  droppable requirement.** It adds a `DiagnosticInfo` to *every successful* `plan` report, and
  command diagnostics flow into `generatedViewPlan`. If it churns any golden under
  `tests/FS.GG.SDD.Commands.Tests/goldens/` — which is inside **#174's** touch-set — it is deferred
  to a follow-up issue rather than merged across the overlap. The spec already marks it independently
  shippable and "the last one that should block a release." Decide empirically: implement, run
  `dotnet test`, inspect the churn.
- Removing the injected `PD-###` line changes `tasks`'s failure mode on an already-corrupted plan.
  FR-009 keeps the authored-`stale:` net, so the only behavior lost is the one we are deleting on
  purpose.
- `Diagnostics.signalsStaleView` is a substring match on `"stale"`; `stalePlanSnapshot` matches it
  exactly as `stalePlanDecision` did, so the agent-refresh classification is unchanged (research R6).

## Complexity Tracking

No constitution violations. Table intentionally empty.
