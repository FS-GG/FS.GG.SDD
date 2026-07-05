---
description: "Task breakdown for 082-idempotent-gate-artifacts"
---

# Tasks: Idempotent Generated Gate Artifacts

**Input**: `specs/082-idempotent-gate-artifacts/` — plan.md, spec.md, research.md, data-model.md, contracts/

**Tier**: Tier 1 (command-output contract + generated-artifact regeneration behavior; parser types touched additively at most). Tests are mandatory (Principle VI). No persisted-schema bump.

**Legend**: `[ ]` pending · `[X]` done w/ real evidence · `[-]` skipped w/ rationale · `[P]` parallel-safe (no dep on another incomplete task in the phase) · `[US#]` user story.

**Order rule**: phases run in sequence; tasks within a phase may run in parallel where marked `[P]`. Within a change, follow Spec→FSI→Tests→Impl (Principle I). Principle V (MVU) is **not applicable**: the change is inside the pure `update`-side authoring functions (`ChecklistPlanAuthoring`/`TaskGraphAuthoring`) that already emit `WriteFile` effects; no new `Model`/`Msg`/`Effect` or interpreter edge is introduced (noted in T004).

**Slices** (independently shippable, per plan): Phase 2 = Slice A / US1 / #146 (MVP) · Phase 3 = Slice B / US2 / #147 · Phase 4 = Slice C / US3 (epic "document" criterion).

---

## Phase 1: Setup & baseline (Shared)

**Purpose**: Pin current behavior so later phases assert against reality, and prove each defect reproduces on `main` before touching code.

- [X] T001 [P] Reproduce #146 on `main`: add a scratch/xfail test in `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs` that builds a `checklist.md` on the **not-stale** path (Source Snapshot digests match current sources) containing a tool-injected `CHK-###` blocking "missing acceptance coverage" row for an `FR` that the current `spec.md` **does** cover, re-runs `checklist`, and asserts the row is gone. Confirm it **fails** on `main`. Leave it as the seed for T020.
- [X] T002 [P] Reproduce #147 on `main`: add a scratch/xfail test in `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs` that generates `tasks.yml`, edits `plan.md` to add a `DEC-002` disposition, re-runs `tasks`, and asserts a task referencing `DEC-002` exists and the outcome is not `status: stale`. Confirm it **fails** on `main`. Seed for T030.
- [X] T003 [P] Inventory the fixtures that encode the *old* behavior and must be rewritten (not just extended): `ChecklistCommandTests.fs` (`checklist stale rerun purges…`, `checklist rerun preserves…`), `TasksCommandTests.fs` (`tasks marks existing tasks stale when source snapshots change`, `tasks rerun preserves existing authored task state`), and any golden rows in `ReadinessViewGoldenTests.fs` / `DeterminismMatrixTests.fs`. Record the list as a comment block at the top of `tasks.md`'s companion or in `research.md` "Fixture reconciliation".
- [X] T004 Confirm scope notes in this file: Principle V (MVU) n/a (no new I/O workflow); `ChecklistPlanAuthoring.fs`/`TaskGraphAuthoring.fs` have **no `.fsi`** (module-internal), so no signature change there — only `Checklist.fsi`/`Task.fsi` change if a helper is surfaced (T041). Documentation task, no code.

---

## Phase 2 — [US1] (P1, MVP): checklist stops re-ingesting its own rows (#146)

**Goal**: The machine-derived checklist sections are re-derived from current sources on every run; a tool-injected row with no basis in current sources disappears; authored sections are preserved. Delivers #146 on its own.

### Tests first (Principle I/VI)

- [X] T010 [US1] Promote T001 into the real regression test `checklist re-run re-derives machine sections and drops orphaned injected rows` in `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs`: not-stale path, orphaned `CHK-###` blocking row for a now-covered FR → after re-run the row is gone, `FailedBlockingCount` for that FR is 0, stage advances, no file deleted. (SC-001, SC-003.)
- [X] T011 [P] [US1] Add `checklist re-run preserves authored sections` to `ChecklistCommandTests.fs`: authored text in *Advisory Notes* / *Lifecycle Notes* is byte-untouched after a re-derive. (SC-004, FR-007.)
- [X] T012 [P] [US1] Add `checklist re-run is idempotent and byte-identical` to `ChecklistCommandTests.fs`: second re-run with unchanged sources → `noChange`, byte-identical. (SC-006, FR-008.)

### Implementation

- [X] T013 [US1] In `src/FS.GG.SDD.Commands/CommandWorkflow/ChecklistPlanAuthoring.fs`, make re-derivation unconditional: in `checklistDiagnosticsTextAndSummary`, always take the `rederiveStaleChecklist`-equivalent path (re-derive the five machine-derived sections from current spec/clarification facts + refresh Source Snapshot), and **delete** the not-stale branch that calls `plannedChecklistReviews … (Some existingFacts)` + `appendChecklistReviews`. Keep the `unsafe-overwrite` sentinel guard at the top intact (FR-009).
- [X] T014 [US1] Remove now-dead re-ingestion helpers/params in `ChecklistPlanAuthoring.fs`: the `existingFacts`-seeded dedup in `plannedChecklistReviews` (the `existingSourceIds` skip) and `appendChecklistReviews` if no longer referenced. Keep id-continuation only if needed for determinism; prefer deterministic fresh allocation by requirement order (verify byte-stability via T012).
- [X] T015 [US1] Reconcile the existing fixtures flagged in T003 for checklist: rewrite `checklist stale rerun purges…` and `checklist rerun preserves…` to assert the unconditional-re-derive behavior (they should largely still hold; adjust any assertion that depended on the not-stale append path).

**Checkpoint**: `dotnet test` green for `ChecklistCommandTests`; T010 now passes; #146 fixed. Slice A shippable.

---

## Phase 3 — [US2] (P1): tasks regenerate in place (#147)

**Goal**: `tasks` re-derives the full graph from current sources on every run, merging authored `status`/`owner` and the stable `T###` id by leading-`SourceId` key, dropping orphaned tasks, and retiring the stale-relabel re-run signal. Delivers #147.

### Tests first

- [X] T020 [US2] Promote T002 into `tasks re-run re-derives graph so a new plan disposition appears` in `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`: after adding `DEC-002` to `plan.md` and re-running, a task with `DEC-002` in its `SourceIds`/`Decisions` exists; outcome is not `status: stale`; no `staleTask`/`TF-001`; `StaleCount = 0`. (SC-002.)
- [X] T021 [P] [US2] Add `tasks re-run preserves authored status and owner` to `TasksCommandTests.fs`: a task marked `Done` (and `Skipped: <reason>`) with a set `owner`, after an unrelated `plan.md` edit, keeps its `status`/`owner` and its `T###` id on the re-derived task; structural fields recomputed. (SC-004, FR-007.)
- [X] T022 [P] [US2] Add `tasks re-run drops a task whose source was removed` to `TasksCommandTests.fs`: delete a task's source from `plan.md`, re-run → the task is absent (not relabeled `Stale`). (Documented behavior, research Decision 3.)
- [X] T023 [P] [US2] Add `tasks re-run is idempotent and byte-identical` to `TasksCommandTests.fs`: unchanged sources → second re-run `noChange`, byte-identical. (SC-006.)
- [X] T024 [P] [US2] Add cross-check to `tests/FS.GG.SDD.Commands.Tests/AnalyzeCommandTests.fs`: after T020's re-derive, `fsgg-sdd analyze` clears `missingDisposition` for `DEC-002`.

### Implementation

- [X] T025 [US2] In `src/FS.GG.SDD.Commands/CommandWorkflow/TaskGraphAuthoring.fs` `tasksDiagnosticsTextAndSummary`: replace the `taskSourceSnapshotStale` → `markTasksStale` + carry-forward path with a full re-derive of the graph from current sources (as a from-scratch `plannedTasks … None`).
- [X] T026 [US2] Implement the merge in `TaskGraphAuthoring.fs`: index prior tasks by leading `SourceId`; for each re-derived task, if a prior match exists carry forward `status`, `owner`, and the prior `T###` id; else allocate the next `T###` and default `Pending`. Prior tasks with no derived match are dropped. Keep file-level `advisoryNotes`/`lifecycleNotes` preserved.
- [X] T027 [US2] Retire the stale re-run signal in `TaskGraphAuthoring.fs`: stop emitting `markTasksStale`, the `TF-001` finding, and the `staleTask` diagnostic on the source-change path. Do **not** remove `TaskStatus.Stale`, the `staleTask` constructor, its remediation pointer, or the `tasks.correctStaleTasks` next-action (kept per research Decision 4; no `PublicSurface.baseline` removal).
- [X] T028 [US2] Reconcile the fixtures flagged in T003 for tasks: **replace** `tasks marks existing tasks stale when source snapshots change` with the re-derive behavior; update `tasks rerun preserves existing authored task state` to the merge semantics (status/owner + `T###` preserved).

**Checkpoint**: `dotnet test` green for `TasksCommandTests` + `AnalyzeCommandTests`; T020 passes; #147 fixed. Slice B shippable.

---

## Phase 4 — [US3] (P2): documented regeneration semantics + drift guard

**Goal**: The regeneration semantics are documented once in the authoring-contract surface, mirrored across agent roots, and pinned to live behavior. Satisfies the epic's "define + document" criterion.

- [X] T030 [US3] Add a "Regeneration semantics" section to `.claude/skills/fs-gg-sdd-authoring-contracts/SKILL.md` stating the four rules from `contracts/regeneration-semantics.md` (re-derive-not-re-ingest; authored content = checklist authored sections + task `status`/`owner`; coverage authored in `spec.md`; only block is `unsafe-overwrite`).
- [X] T031 [US3] Mirror the edited skill byte-identically to `.codex/skills/fs-gg-sdd-authoring-contracts/SKILL.md` and `.agents/skills/fs-gg-sdd-authoring-contracts/SKILL.md`; regenerate `.agents/skills/skill-manifest.json` (`fsgg-sdd registry skill-manifest --write`) so the body `sha256` matches. Keep `.claude`≡`.codex`≡`.agents`.
- [X] T032 [P] [US3] Point remediation at the doc: ensure any residual stale/blocked next-action (e.g. `unsafeOverwrite` remediation) and the authoring-contracts references resolve to the new section. Update `RemediationPointers`/docs pointer if a section anchor changed.
- [X] T033 [US3] Add/extend a drift-guard test (alongside the seeded-skill/mirror guards in `tests/FS.GG.Contracts.Tests/SkillMirrorTests.fs` and the manifest guard) asserting the new section is present and the three roots stay byte-identical with a fresh manifest. Confirm `SkillMirrorTests` + manifest guard green.

**Checkpoint**: epic #145 "document regeneration semantics" satisfied; mirror + manifest guards green.

---

## Phase 5: Polish & full-suite validation (Shared)

**Purpose**: Prove the whole matrix stays coherent and the contract changes are the only intended deltas.

- [X] T040 [P] Run `tests/FS.GG.SDD.Validation.Tests/DeterminismMatrixTests.fs` (byte-identical re-run matrix) and reconcile any golden rows in `ReadinessViewGoldenTests.fs` that rendered the retired `stale` task signal. Golden diffs must be exactly the intended #146/#147 deltas.
- [X] T041 If any helper was surfaced in `Checklist.fsi`/`Task.fsi` (e.g. a status-merge or section helper), add its `val`/type to the `.fsi` **before** the `.fs` body and update `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` (+ `FS.GG.SDD.Cli.Tests` baseline) in lockstep, sorted. If nothing was surfaced, record "no surface change" here.
- [X] T042 [P] Verify the JSON automation contract deltas are exactly those in `contracts/rerun-outcomes.md` and additive/intended elsewhere: diff `--json` for `checklist`/`tasks` on the unchanged-source path (must be byte-identical) and on the fixed paths (must match the contract). `--text`/`--rich` remain pure projections.
- [X] T043 [P] Confirm no downstream verdict changed: `verify`/`ship`/`refresh` suites green; `analyze` only changes by clearing the now-covered disposition (T024).
- [X] T044 Run the full `dotnet test` suite; confirm all green. Update `specs/082-idempotent-gate-artifacts/quickstart.md` if any observed command output wording differs from what the contract docs assert.
- [X] T045 Migration note check: confirm the "first re-run may rewrite once, then stabilizes" behavior holds (assert idempotence on the *second* re-run, not the first) and is reflected in the plan/quickstart.

---

## Dependencies

- **Phase 1** before all. T001/T002 must be shown failing on `main` before their promotions (T010/T020).
- **Phase 2 (A)** and **Phase 3 (B)** are independent and may proceed in parallel by different people; each closes its own child issue (#146 / #147).
- **Phase 4 (C)** documents semantics both slices establish; land after A+B behavior is final (so the doc matches behavior). T031 depends on T030; T033 depends on T031.
- **Phase 5** after A+B+C; T041 gates any `.fsi`/baseline change; T040/T042 are the contract backstops.

## Parallel opportunities

- T001/T002/T003 (Phase 1) — independent files/reads.
- T011/T012 within US1; T021/T022/T023/T024 within US2.
- Slice A (Phase 2) ∥ Slice B (Phase 3) entirely (different modules/tests).
- T042/T043 (Phase 5) — independent verification reads.

## Suggested MVP

**Phase 2 (US1 / Slice A / #146)** — the most common footgun (a blocking checklist row that won't clear) fixed on its own, shippable without Slice B or C.

## Evidence obligations

Behavior-changing tasks (T010–T015, T020–T028) each land with a real-fixture xUnit test that fails before and passes after (Principle VI). Documentation tasks (T004, T030–T033) are evidenced by the mirror/manifest/drift guards. Principle IV (MVU) is not applicable (T004). No synthetic evidence anticipated; disclose per Principle V at implement time if any arises.
