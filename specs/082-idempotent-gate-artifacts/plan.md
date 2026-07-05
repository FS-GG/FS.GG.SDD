# Implementation Plan: Idempotent Generated Gate Artifacts

**Branch**: `082-idempotent-gate-artifacts` | **Date**: 2026-07-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/082-idempotent-gate-artifacts/spec.md`

## Summary

Two generated gate artifacts poison their own re-run because the tool re-parses its own
output as authored input: `checklist` re-ingests prior `CHK-###` blocking rows on the
"not stale" path (dedup on their `SourceId`s suppresses re-derivation and preserves the
rows as authoritative — #146), and `tasks` relabels rows `Stale` on an upstream change but
never re-derives the graph, so a newly-added plan decision disposition never appears (#147).
Root cause: both files are written/read as `AuthoredSource` with **no per-row provenance**,
and the re-run paths *carry forward* generated rows instead of *re-deriving* them.

The fix is **unconditional re-derivation of the machine-owned regions from current
sources**, made safe by the fact (confirmed in [research.md](./research.md)) that every
`CHK-###`/`CR-###` row and every task row is tool-derived — the only author-owned state is
(a) the authored prose sections of `checklist.md` and (b) per-task `status`/`owner` in
`tasks.yml`:

- **Checklist**: collapse the stale/not-stale branch so the five machine-derived sections
  are always re-derived from the current spec/clarification facts (the existing
  `rederiveStaleChecklist` behavior, made unconditional); the authored prose sections and
  the refreshed Source Snapshot are preserved. Generated rows are never re-ingested.
- **Tasks**: replace the `markTasksStale` relabel-and-carry path with a full re-derive of
  the graph from current sources, **merging** each re-derived task with the prior file by
  the existing leading-`SourceId` key to carry forward authored `status`/`owner` and the
  stable `T###` id. Orphaned tasks (whose source no longer exists) are dropped. This
  retires the `staleTask`/`TF-001`/`tasks.correctStaleTasks`/`StaleCount` re-run signal
  (unused downstream — verified).
- **Semantics + escape hatch**: document the regeneration semantics in the
  authoring-contracts surface and pin them against drift; the only permitted block on
  re-run remains the existing `unsafe-overwrite` opt-out, whose diagnostic already names
  the file to delete and the command to re-run.

**Change tier**: **Tier 1** (command-output contract + generated-artifact regeneration
behavior; touches the `Checklist`/`Task` parser types only additively). Delivers spec,
plan, tasks, tests, docs, and migration notes. No persisted-schema version bump.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (per constitution).

**Primary Dependencies**: existing repo only — `FS.GG.SDD.Artifacts` (`Checklist`/`Task`
parsers, diagnostics), `FS.GG.SDD.Commands` (`ChecklistPlanAuthoring`, `TaskGraphAuthoring`,
`ViewGeneration`, diagnostic constructors, next-action routing), the `TestSupport` harness
in `FS.GG.SDD.Commands.Tests`, xUnit. No new external dependencies.

**Storage**: files only — `work/<id>/checklist.md`, `work/<id>/tasks.yml`. No database, no
persisted-schema change (`scaffold-provenance` and all `v1` schemas untouched).

**Testing**: xUnit via `TestSupport.runRequest`/`runChecklist`/`runTasks` (in-process MVU
loop). Offline, no network, no Governance runtime (FR-012). Real filesystem fixtures.

**Target Platform**: cross-platform CLI/library + CI (GitHub Actions), same as the repo.

**Project Type**: single project — F# CLI/library with a test suite.

**Performance Goals**: not a hot path. Re-derivation is O(requirements + tasks) per run,
already performed on the stale path today; whole-suite runtime impact negligible. Re-run
budget tests (`tasks create and rerun complete under budget`) must stay green.

**Constraints**: JSON automation contract stays byte-stable except the deliberate,
additive outcome/diagnostic changes for #146/#147; `--text`/`--rich` remain pure
projections; determinism/byte-identical re-run preserved (FR-008, `DeterminismMatrixTests`);
authored content preserved (FR-007); `unsafe-overwrite` opt-out preserved (FR-009); no
second source of truth (Principle VII); no Governance runtime (FR-012).

**Scale/Scope**: 2 command-authoring modules changed (`ChecklistPlanAuthoring.fs`,
`TaskGraphAuthoring.fs`), no `.fsi` in either; parser types (`Checklist.fsi`/`Task.fsi`)
touched additively at most; ~8 directly-relevant command fixtures updated + determinism/
golden suites; 1 authoring-contract doc section + drift-guard.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec→FSI→Tests→Impl | PASS | Behavior lives in `Commands` modules with **no** `.fsi` (module-internal); the only signature surface (`Checklist.fsi`/`Task.fsi`) changes at most additively and gets its `val` before `.fs`. Tests are written to fail-before/pass-after (US1/US2 repros). |
| II. Structured artifacts are the machine contract | PASS | Makes explicit *which content is authoritative*: sources (spec/clarify/plan) own the derived rows; the artifact's authored sections + task `status`/`owner` are the only author-owned state. Re-derivation removes the "prose row wins over source" drift — the principle's core aim. Diagnostic recorded in the command report. |
| III. Visibility in `.fsi` | PASS | `ChecklistPlanAuthoring`/`TaskGraphAuthoring` have no `.fsi`. If `Task.fsi`/`Checklist.fsi` gain a helper (e.g. a status-merge or a `TaskStatus` note), the `val`/type is added in the `.fsi` and `PublicSurface.baseline` updated in lockstep. No `.fs` visibility modifiers. Net surface is expected to be **subtractive-in-behavior, additive-or-flat in symbols**. |
| IV. Idiomatic simplicity | PASS | The change *removes* a branch (the not-stale preserve/append path) and a relabel step, replacing them with a straight re-derive + a plain merge-by-key `Map` lookup. No new abstractions, operators, or CE machinery. |
| V. Elmish/MVU boundary | PASS | No new stateful/I-O workflow — the change is inside the pure `update`-side authoring functions that produce `WriteFile` effects; the existing edge interpreter is unchanged. |
| VI. Test evidence mandatory | PASS | Each defect gets a real-fixture regression test that fails on `main` and passes after (checklist orphaned-row re-ingestion; tasks plan-disposition re-derive). Determinism/golden/preserve suites extended. No mocks. |
| VII. Agent+human share one contract | PASS | The regeneration semantics are documented once in the authoring-contract surface (mirrored `.claude`≡`.codex`≡`.agents`) and pinned by a drift guard; no second source of truth. Skills stay authoring-only. |
| VIII. Observability & safe failure | PASS | Re-run never silently strands a stale artifact (FR-004); the only block is the explicit `unsafe-overwrite` diagnostic that already names file+command (FR-006). Malformed input still fails as user error, not tool defect. |

**Result**: No violations. Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/082-idempotent-gate-artifacts/
├── plan.md              # This file
├── research.md          # Phase 0 — the design decisions (mechanism, merge key, stale retirement)
├── data-model.md        # Phase 1 — authored-vs-generated regions, task merge model
├── quickstart.md        # Phase 1 — how to reproduce #146/#147 and validate the fix
├── contracts/           # Phase 1 — the regeneration-semantics contract + re-run outcome contract
│   ├── regeneration-semantics.md
│   └── rerun-outcomes.md
├── checklists/
│   └── requirements.md  # spec quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Commands/CommandWorkflow/
├── ChecklistPlanAuthoring.fs   # collapse stale/not-stale → always re-derive the 5 machine-derived
│                               #   sections; drop `plannedChecklistReviews (Some existingFacts)`
│                               #   re-ingestion + `appendChecklistReviews`; keep authored sections +
│                               #   Source Snapshot refresh; preserve `unsafe-overwrite` guard.
├── TaskGraphAuthoring.fs       # replace `markTasksStale`+carry with full re-derive; merge authored
│                               #   status/owner + stable T### id by leading-SourceId key; drop
│                               #   orphaned tasks; retire staleTask/TF-001 emission on this path.
├── HandlersEarly.fs            # (only if the write-kind classification needs a note; behavior via above)
└── ViewGeneration.fs           # confirm missingDisposition reads re-derived tasks (no change expected)

src/FS.GG.SDD.Artifacts/LifecycleArtifacts/
├── Checklist.(fs/fsi)          # no shape change expected; touch only if a section helper is surfaced
└── Task.(fs/fsi)               # `TaskStatus.Stale` retained (legacy/authored); additive only if a
                                #   merge helper is surfaced

docs/                            # regeneration-semantics reference (authoring-contract surface)
└── (authoring-contracts doc + drift-guard fixture)

.claude/skills/fs-gg-sdd-authoring-contracts/SKILL.md   # + regeneration-semantics section (mirrored)
.codex/skills/fs-gg-sdd-authoring-contracts/SKILL.md    # byte-identical mirror
.agents/skills/fs-gg-sdd-authoring-contracts/SKILL.md   # byte-identical mirror (+ skill-manifest sha256)

tests/
├── FS.GG.SDD.Commands.Tests/
│   ├── ChecklistCommandTests.fs   # NEW #146 re-ingestion regression; update stale/preserve fixtures
│   ├── TasksCommandTests.fs       # NEW #147 re-derive regression; REPLACE the stale-relabel fixture
│   ├── AnalyzeCommandTests.fs     # confirm disposition clears after tasks re-derive
│   └── PublicSurface.baseline     # updated iff a symbol is added
├── FS.GG.SDD.Artifacts.Tests/     # Checklist/Tasks parser fixtures iff a type changes
└── FS.GG.SDD.Validation.Tests/DeterminismMatrixTests.fs   # byte-identical re-run stays green
```

**Structure Decision**: Single-project layout, unchanged. The behavior change is confined
to the two `CommandWorkflow` authoring modules (both without `.fsi`), so the public surface
is nearly untouched; parser types change additively only if a merge/section helper is
surfaced. Authored surfaces (the authoring-contracts skill) live where they already live.

## Phased delivery (maps to user stories)

Each user story is an independently shippable increment (per spec priorities):

- **Slice A (US1, P1) — checklist stops re-ingesting**: make the machine-derived sections
  always re-derive; delete the not-stale preserve/append re-ingestion; add the orphaned-row
  regression test; reconcile the existing stale/preserve fixtures. Ships #146.
- **Slice B (US2, P1) — tasks regenerates in place**: replace `markTasksStale`+carry with
  re-derive + merge-by-source-key (status/owner/T###-id preserved, orphans dropped); retire
  the stale-relabel re-run signal; add the plan-disposition regression + status-preservation
  test; confirm `analyze` disposition clears. Ships #147.
- **Slice C (US3, P2) — documented semantics**: add the regeneration-semantics section to
  the authoring-contracts surface (mirrored + manifest), pin it with a drift guard, and
  point the relevant diagnostics/next-actions at it.

Slices A and B are independent; C documents the semantics both establish. A and B each
close their child issue (#146, #147); C satisfies the epic's "define + document" criterion.

## Migration / compatibility notes

- **JSON automation contract**: the deliberate, documented changes are (i) checklist no
  longer preserves an orphaned `CHK-###` blocking row across a re-run (a row with no basis
  in current sources disappears), and (ii) tasks re-run emits a regenerated graph rather
  than `staleTask`/`TF-001`/`status: stale`/`StaleCount > 0` on an upstream change. Both are
  the intended fix. `staleTask`/`tasks.correctStaleTasks`/`TaskStatus.Stale` remain in the
  surface (no baseline removal) but are no longer emitted by the source-change path.
- **First re-run churn**: an existing artifact produced by the old carry-forward path may be
  rewritten once on the first post-upgrade re-run (rows re-derived to canonical form), then
  stabilizes to byte-identical `noChange`. Documented in quickstart.
- **No persisted-schema bump**: all `v1` schemas and `skill-manifest` schema v1 unchanged
  (only the authoring-contracts skill body sha256 changes if the doc lands in the skill).
- **Downstream stages**: `analyze`/`verify`/`ship`/`refresh` do not read task `Stale` status
  (verified), so retiring the relabel path changes no downstream verdict; `analyze`'s
  `missingDisposition` now clears correctly because the disposition task is actually derived.
- **Agent surfaces**: `.claude`≡`.codex`≡`.agents` kept byte-identical; `skill-manifest`
  regenerated if the doc section lands in the skill.

## Risks

- **Byte-stability of always-re-derive vs the old not-stale output**: if the re-derived
  checklist bytes differ from what the old append path wrote, the first re-run rewrites
  once. Mitigation: assert idempotence on the *second* re-run (byte-identical `noChange`);
  accept a one-time canonicalization on the first.
- **Status-preservation when a task's leading SourceId changes**: merge keys on the leading
  `SourceId`; if a plan edit changes which source leads a task, that task's authored
  `status`/`owner` may reset to a fresh `Pending`. Mitigation: documented in research.md as
  accepted/rare; the merge falls back cleanly (new task, no crash). Considered matching on
  the full SourceId set — rejected as over-engineered for the observed cases.
- **Dropping orphaned Done tasks**: a task the author marked `Done` whose source later
  disappears is dropped on re-derive. Mitigation: correct by construction (the covered work
  is gone); called out in research.md and quickstart so it isn't a surprise.
- **Golden/determinism fixtures**: the stale-relabel and preserve/append fixtures encode the
  old behavior and must be rewritten, not merely extended. Mitigation: Slice A/B each own
  their fixture reconciliation; `DeterminismMatrixTests` byte-identical matrix is the backstop.
