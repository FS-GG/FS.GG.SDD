---
description: "Task list for feature 033 — SDD skeleton emits the lifecycle constitution at .fsgg/constitution.md"
---

# Tasks: SDD skeleton emits the lifecycle constitution at `.fsgg/constitution.md`

**Input**: Design documents from `specs/033-skeleton-constitution/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md,
contracts/ (`constitution-content.md`, `init-emission.md`,
`scaffold-provenance.md`, `lifecycle-exclusion.md`)

**Tests**: Included and required. Constitution Principle VI mandates real-evidence
tests, and all four contracts specify verification over the public command surface.
Per Principle I/VI, the US1 tests are authored to **fail first** against the
pre-change tree, then pass after the single production edit lands.

**Tier**: Tier 1 (contracted change) — re-baselines the `init` skeleton set by
adding exactly one authored skeleton artifact. No `.fsi`/signature change, no
schema change, no provider-contract or `scaffold-provenance.json` change.

**Elmish/MVU note**: the emission is an existing `WriteFile` **effect** produced
by the pure `initEffects` planner and performed only at the edge interpreter
(`CommandEffects.fs`). No new `.fsi` contract, no new `Model`/`Msg`/`Effect` case,
and no I/O added to any `update` — so no MVU-boundary scaffolding tasks are needed
beyond the existing-effect reuse (plan Constitution Check V). Verification runs
through the public `init`/`scaffold`/`refresh` surface on real filesystem fixtures.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different file, no dependency on another
  incomplete task in this phase)
- **[Story]**: `US1`/`US2`/`US3` traceability
- Tier matches the spec (Tier 1) throughout; no per-task tier override needed

---

## Phase 1: Setup

**Purpose**: Confirm scope and stage the authoritative content. No new project
structure or dependencies (single `WriteFile` + one `string` constant; `net10.0`,
standard library only — plan Technical Context).

- [X] T001 [P] Confirm no new project structure, package, `CommandEffect` case, or
  `ArtifactWriteKind` case is required; the change is one `initEffects` line plus one
  `Foundation.fs` constant reusing the existing `WriteFile` effect/interpreter
  (plan §Scale/Scope, §Grounded inventory).
- [X] T002 Stage the authoritative seed body from
  `specs/033-skeleton-constitution/contracts/constitution-content.md` for verbatim
  transcription, and confirm its invariants up front: placeholder-free (no
  `[BRACKET]`/`TODO`/`FIXME`), generic (none of `FS.GG.SDD`, `FS.GG.Rendering`,
  `FS.GG.Governance`, provider/template ids, docs URLs), and no date/timestamp/
  randomness (FR-002/FR-003/FR-007).

---

## Phase 2: Foundational (Blocking Prerequisites)

**N/A for this feature.** There is no foundational work separable from the MVP.
The single shared production change (the `constitutionText` constant + the
`WriteFile` line in `initEffects`) lives in Phase 3 (US1) because US1 is the MVP it
delivers. Per the plan's central thesis, US2 and US3 fall out **for free** from that
one edit, so they are verification-only and each depend on the US1 production task
(T006). This cross-story dependency is stated explicitly below.

---

## Phase 3: User Story 1 - `init` lays down a ready-to-use lifecycle constitution (Priority: P1) 🎯 MVP

**Goal**: `fsgg-sdd init` writes a populated, valid, generic, deterministic
`.fsgg/constitution.md` as part of the SDD skeleton, reported as an authored
skeleton artifact.

**Independent Test**: Run `fsgg-sdd init` in a temp empty dir; confirm
`.fsgg/constitution.md` exists, is non-empty, structurally valid, placeholder-free,
generic, and byte-identical across a re-run.

### Tests for User Story 1 (write FIRST — must FAIL before T005/T006) ⚠️

- [X] T003 [US1] Add the US1 init suite to
  `tests/FS.GG.SDD.Commands.Tests/InitCommandTests.fs`, all over a real temp-dir
  fixture through the public `init` surface (contract `init-emission.md`):
  (a) **AC1** `.fsgg/constitution.md` exists, non-empty, has a recognizable
  constitution title/principles (FR-001/FR-002);
  (b) **AC1 report** the command report lists it as a created authored skeleton
  artifact with `Kind = "agentGuidance"`, `Ownership = "authored"`, operation
  `Create` (FR-010);
  (c) **AC3 generic** the content contains none of the forbidden token set
  (`FS.GG.SDD`, `FS.GG.Rendering`, `FS.GG.Governance`, provider/template ids, docs
  URLs) and no `[`…`]` placeholder (FR-003/SC-006);
  (d) **AC2 determinism** two `init` runs on identical inputs yield byte-identical
  `.fsgg/constitution.md` (FR-007/SC-003).
  All four must fail against the current tree (no `.fsgg/constitution.md` emitted).
- [X] T004 [P] [US1] Add a plan-level clarity assertion to
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`: `Assert.Contains` that
  `initEffects` plans `WriteFile(".fsgg/constitution.md", _, AgentGuidanceTarget)`
  (different file from T003 → parallel-safe). Must fail first.

### Implementation for User Story 1

- [X] T005 [US1] Add the `constitutionText` constant to
  `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs`, beside
  `projectConfigText`/`sddConfigText`/`agentsConfigText`/`agentGuidance` (lines
  ~34-79), transcribing `contracts/constitution-content.md` verbatim as a
  triple-quoted F# literal (no interpolation, no date/randomness) — FR-002/FR-007.
- [X] T006 [US1] Add `WriteFile(".fsgg/constitution.md", constitutionText,
  AgentGuidanceTarget)` to `initEffects` in
  `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs` (lines ~81-91), alongside
  the other skeleton `WriteFile` effects (depends on T005). **This is the single
  shared production change** that delivers FR-001/FR-004/FR-005/FR-008/FR-009/FR-010
  — US2 (T008) and US3 (T010) depend on it.
- [X] T007 [US1] Run `dotnet test` for the Commands tests and confirm the
  previously-failing T003/T004 assertions now pass; no other Init/Workflow test
  regresses.

**Checkpoint**: `init` emits a real, populated, generic, deterministic
`.fsgg/constitution.md` — MVP complete and independently testable.

---

## Phase 4: User Story 2 - Scaffold delivers the constitution without polluting app-only provenance (Priority: P2)

**Goal**: `scaffold` delivers `.fsgg/constitution.md` via the reused `init` effects
while keeping it out of the `generatedProduct` set in `scaffold-provenance.json`.

**Independent Test**: Scaffold with a test provider + `lifecycle=sdd` into a temp
dir; confirm `.fsgg/constitution.md` is present and absent from `generatedProduct`.

### Tests for User Story 2 (verification-only; depends on T006) ⚠️

- [X] T008 [P] [US2] Add the US2 suite to
  `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs` over a real scaffold
  fixture with a test provider (contract `scaffold-provenance.md`):
  (a) **AC1** after `scaffold --provider <name> --param lifecycle=sdd`,
  `.fsgg/constitution.md` is present in the product and the report attributes it to
  the SDD skeleton, not the provider (FR-004);
  (b) **AC2** the `generatedProduct` paths read from the resulting
  `.fsgg/scaffold-provenance.json` do **not** include `.fsgg/constitution.md`
  (FR-005/SC-002);
  (c) **Edge case (spec Edge Cases #2)** scaffold with a **non-`sdd`** `lifecycle`
  param (e.g. `--param lifecycle=spec-kit`) still produces `.fsgg/constitution.md`,
  confirming the emission rides the always-run `init` effects and is **not** gated
  on the provider's `lifecycle` parameter (FR-004).
  Different file from US1/US3 tests → parallel-safe. Depends on T006.

### Implementation for User Story 2

- [X] T009 [US2] No production change (delivered for free by T006). Run
  `ScaffoldCommandTests.fs` and confirm T008 passes **and** the existing dynamic
  skeleton-enumeration / byte-identity / determinism tests
  (`ScaffoldCommandTests.fs:442-469`/`:474-492`/`:498-509`) self-adjust and keep
  passing with the new file (FR-006 re-baseline) — the unchanged hardcoded app-only
  produced set is the FR-005 proof. Also add an explicit **set-delta** assertion
  (in `ScaffoldCommandTests.fs` or `InitCommandTests.fs`) that the skeleton path set
  grew by **exactly one** entry — `.fsgg/constitution.md` — relative to the prior
  skeleton (e.g. assert the new skeleton set minus the constitution equals the
  established skeleton set), tying the re-baseline directly to **SC-005** ("only
  skeleton-set delta being the single new artifact").

**Checkpoint**: Constitution is delivered on the scaffold path and provably
excluded from app-only provenance.

---

## Phase 5: User Story 3 - Re-running and refreshing never clobbers an authored constitution (Priority: P3)

**Goal**: An author-edited `.fsgg/constitution.md` survives a re-`init`
(no-clobber) and `refresh` (never regenerated, never flagged stale/generated/
external).

**Independent Test**: `init`, edit the constitution, re-run `init` and `refresh`;
confirm the edited bytes are preserved with zero unintended modification.

### Tests for User Story 3 (verification-only; depends on T006) ⚠️

- [X] T010 [P] [US3] Add the US3 suite to
  `tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs` over real fixtures
  (contract `lifecycle-exclusion.md`):
  (a) **AC1 no-clobber** after `init`, edit `.fsgg/constitution.md`, re-run `init`
  ⇒ the edited bytes are preserved and the report records the constitution as
  `Operation = Refuse` / `SafeWriteDecision = "refused"`, not overwritten (FR-008);
  (b) **AC2 refresh** with the author-modified constitution present, run `refresh`
  ⇒ the file is byte-unchanged and is **not** reported as a generated view, a stale
  view, or a `generatedProduct` path (FR-009/SC-004).
  Different file from US1/US2 tests → parallel-safe. Depends on T006.

### Implementation for User Story 3

- [-] T011 [US3] **Skipped** — report-symmetry only (research D3). Adding `.fsgg/constitution.md`
  to `authoredPreserved` also requires reading it in `refreshReadEffects` (the list is
  snapshot-filtered), expanding the deliberately minimal footprint. T010 AC2 passes without
  it: refresh never targets `.fsgg/` root files, so protection holds. *(original task)* Add
  `.fsgg/constitution.md` to the informational `authoredPreserved` list in
  `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersRefresh.fs` (~lines 113-123) so
  the refresh summary reports it as preserved-authored, symmetric with the
  `.fsgg/*.yml` configs. Skip if T010 passes without it — protection holds either
  way (refresh never targets `.fsgg/` root files).
- [X] T012 [US3] No required production change for protection (delivered for free by
  T006's `AgentGuidanceTarget`/`canOverwrite` and the refresh target set). Run
  `RefreshCommandTests.fs` and confirm T010 passes.

**Checkpoint**: Author edits to the constitution are durable across `init` re-run
and `refresh`.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Aligned agent-surface notes and whole-solution / baseline integrity.

- [X] T013 [P] Add one aligned line to `CLAUDE.md` and `AGENTS.md` noting the SDD
  skeleton seeds an authored `.fsgg/constitution.md` (no workflow-shape change).
- [X] T014 [P] Add the same aligned line to
  `.claude/skills/fs-gg-sdd-project/SKILL.md` and
  `.codex/skills/fs-gg-sdd-project/SKILL.md`, keeping Claude and Codex surfaces in
  sync (Constitution VII).
- [X] T015 Run `dotnet test FS.GG.SDD.sln` (Artifacts, Validation, Cli, Commands)
  and confirm: all suites green; `WarningsAsErrors` ratchet stays at 0 with no
  `#nowarn` added; the four `PublicSurface.baseline` snapshots and
  `tests/FS.GG.SDD.Artifacts.Tests/baselines/release-readiness.json` are unchanged
  (no signature/release-catalog delta — plan §Grounded inventory).
- [X] T016 Run the `quickstart.md` US1/US2/US3 reproduction and confirm the
  determinism, no-clobber, and `generatedProduct`-exclusion outcomes match the
  documented expectations (SC-001…SC-006).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: N/A (empty). The shared production change is T006 in
  Phase 3.
- **US1 (Phase 3)**: the MVP; owns the single production edit (T005 → T006).
- **US2 (Phase 4)** and **US3 (Phase 5)**: verification-only, each depends on
  **T006**. Once T006 lands they can proceed in parallel.
- **Polish (Phase 6)**: agent-surface notes (T013/T014) can start any time;
  T015/T016 run after US1–US3 tests pass.

### Within User Story 1

- T003/T004 (tests) authored first and observed **failing** → T005 (constant) →
  T006 (effect line, depends on T005) → T007 (green).

### Cross-story dependency (explicit)

- T008 (US2) and T010 (US3) depend on **T006**; their "implementation" tasks
  (T009/T012) are verification because the behavior is derived from T006 for free.

### Parallel Opportunities

- T001 (Setup) is `[P]`.
- T004 is `[P]` with T003 (different files).
- After T006: T008 (US2) and T010 (US3) are `[P]` (different test files); T013 and
  T014 (agent surfaces) are `[P]`.

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 Setup (T001–T002).
2. Phase 3 US1: write failing tests (T003/T004) → add constant (T005) → add effect
   line (T006) → green (T007).
3. **STOP and VALIDATE**: `init` emits a real, generic, deterministic, no-clobber
   constitution — the full ADR-0004 obligation and the closeable P2 SDD item.

### Incremental Delivery

1. US1 (MVP) → US2 (provenance exclusion) → US3 (no-clobber/refresh durability) —
   each independently testable, all riding the single T006 production edit.
2. Polish: aligned agent-surface notes, full-solution + baseline-integrity run,
   quickstart validation.

---

## Summary

- **Task count by story**: US1 = 5 (T003–T007), US2 = 2 (T008–T009),
  US3 = 3 (T010–T012). Setup = 2, Polish = 4. **Total = 16.**
- **Parallel opportunities**: T003∥T004; after T006, US2∥US3 (T008∥T010) and the
  agent-surface notes T013∥T014.
- **Suggested MVP scope**: **User Story 1** — `init` emits
  `.fsgg/constitution.md`; the lone production change (T005+T006) also delivers
  US2/US3 behavior for free.
- **Production footprint**: 1 `WriteFile` line + 1 `string` constant in
  `Foundation.fs` (+ optional informational list entry in `HandlersRefresh.fs`);
  0 `.fsi`/schema/provenance/release-catalog changes.
</content>
</invoke>
