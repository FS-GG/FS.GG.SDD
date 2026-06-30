---
description: "Task breakdown for emitting fs-gg-sdd-* process skills into scaffolded products"
---

# Tasks: Emit fs-gg-sdd-* process skills into scaffolded products

**Input**: Design documents from `specs/051-scaffold-sdd-process-skills/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/seeded-skill-set.md, quickstart.md

**Tests**: Included — the spec, plan, and contract make INV-1..INV-8 load-bearing
(completeness, parity, determinism, no-clobber, single-seam, boundary, drift,
skeleton-shape). Tests precede emission per Constitution Principle VI.

## Status legend

- `[ ]` pending · `[X]` done with real evidence · `[-]` skipped (rationale on the line)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase
- **[Story]**: US1 / US2 / US3 (omitted for shared phases)
- Paths are exact and repo-relative.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Wire the canonical skill bodies into the `FS.GG.SDD.Commands`
assembly as embedded resources (decision D1) so emission reads compiled-in bytes,
never the FS.GG.SDD repo at runtime.

- [X] T001 Add `EmbeddedResource` items in `src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj` linking the 15 in-scope canonical `.claude/skills/fs-gg-sdd-<name>/SKILL.md` sources (charter, specify, clarify, checklist, plan, tasks, analyze, evidence, verify, ship, lifecycle, getting-started, authoring-contracts, refresh-agents, validate), each with a stable deterministic `LogicalName` (e.g. `SeededSkill.fs-gg-sdd-<name>`). Exclude `fs-gg-sdd-project`. Confirm `dotnet build` packs the resources.
- [X] T002 [P] In the same `.fsproj`, register `CommandWorkflow/SeededSkills.fs` in the `Compile` ItemGroup ordered **before** `CommandWorkflow/Foundation.fs` (Foundation consumes it). No NuGet additions.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The declared set + embedded-resource loader + skill→effects
expansion that every user story builds on.

**⚠️ CRITICAL**: No user-story emission or test can pass until this phase is complete.

- [X] T003 Create `src/FS.GG.SDD.Commands/CommandWorkflow/SeededSkills.fs` (internal module, no `.fsi`, matching sibling `CommandWorkflow/*.fs`): declare the sorted membership list of the 15 in-scope skill names (the single in-code source of the set, per contract §1), and a manifest-resource loader that reads each linked `SKILL.md` body from the embedded resources added in T001. Surface a `SeededSkill = { Name: string; Body: string }` sequence.
- [X] T004 In `SeededSkills.fs`, add `skillEffects` — expand each `SeededSkill` into two additive `WriteFile` effects: `.claude/skills/<Name>/SKILL.md` and `.codex/skills/<Name>/SKILL.md`, both carrying `ArtifactWriteKind.AgentGuidanceTarget` (the existing no-clobber, authored-SDD-owned write-kind used by `constitution.md`/`early-stage-guidance.md`). Iterate the sorted declared list so effect order is deterministic (FR-006). Reuse the existing `WriteFile` effect — no new `Effect` case (Principle V).

**Checkpoint**: The declared set is loadable and expands to 30 deterministic `WriteFile` effects, but is not yet wired into seeding.

---

## Phase 3: User Story 1 — A scaffolded product's agent can discover the SDD process (Priority: P1) 🎯 MVP

**Goal**: `init` (and, by reuse, `scaffold`) emits all 15 process skills × 2
surfaces into every seeded product.

**Independent Test**: `fsgg-sdd init` into an empty dir → all 30 `fs-gg-sdd-*/SKILL.md`
files present and non-empty across `.claude/skills/` and `.codex/skills/`; no
`fs-gg-sdd-project` skill seeded.

### Tests for User Story 1 (write FIRST, ensure they FAIL before T007)

- [X] T005 [P] [US1] In `tests/FS.GG.SDD.Commands.Tests/InitCommandTests.fs`, extend the skeleton-shape assertions: after `init` into a temp dir, assert all 30 seeded skill files (15 names × {`.claude`,`.codex`}) exist and are non-empty, and that `.claude/skills/fs-gg-sdd-project/SKILL.md` is **absent** (INV-1, INV-8, SC-001). Must fail pre-emission.

### Implementation for User Story 1

- [X] T006 [US1] In `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs`, extend `initEffects` to append `SeededSkills.skillEffects` after the existing skeleton/constitution/early-stage effects. Keep all prior `init` effects byte-identical (additive only, per constraint).
- [X] T007 [US1] Run `dotnet build` + the InitCommandTests; confirm T005 now passes (30 files present/non-empty, project skill absent). Capture as real evidence.

**Checkpoint**: MVP — a seeded product carries the discoverable process skill set via `init`. `scaffold` inherits it through the shared seam (proven in Phase 6).

---

## Phase 4: User Story 2 — Re-running the seeding command never clobbers author edits (Priority: P2)

**Goal**: Re-running seeding preserves author-edited skill files and refills only
missing ones; `refresh` never rewrites seeded skills.

**Independent Test**: Seed, edit one skill body, delete another, re-run `init` →
edited file preserved verbatim, deleted file refilled, no error.

### Tests for User Story 2 (write FIRST, ensure they FAIL or are unverified before T010)

- [X] T008 [P] [US2] In `tests/FS.GG.SDD.Commands.Tests/SeededSkillsTests.fs` (new file), add the no-clobber test (INV-4, SC-003): seed → append `LOCAL EDIT` to one `.claude/skills/fs-gg-sdd-plan/SKILL.md` and delete `.codex/skills/fs-gg-sdd-tasks/SKILL.md` → re-run seeding → assert the edited file is byte-unchanged and the deleted file is refilled; assert no overwrite of any other present file.
- [X] T009 [P] [US2] Add a refresh-preservation test (INV via FR-005): after seeding, run the `refresh` generator (driver in `CommandWorkflow/HandlersRefresh.fs`) and assert it neither rewrites nor enumerates the seeded skill files (they are not a `refreshCanonicalView` and not a provenance path). Place this in the existing refresh test that owns `refresh` coverage in `tests/FS.GG.SDD.Commands.Tests/` (co-locate with the current refresh-preservation cases for `constitution.md`/`early-stage-guidance.md`), not in the new `SeededSkillsTests.fs`.

### Implementation for User Story 2

- [X] T010 [US2] Verify no-clobber/refresh behavior is satisfied **by construction** via `AgentGuidanceTarget` + `CommandEffects.canOverwrite` (no new code expected). If T008/T009 reveal a gap (e.g. a skill subtree not recognized by an ownership check), make the minimal fix in `SeededSkills.fs`/`Foundation.fs` and re-run. Capture passing evidence.

**Checkpoint**: Seeding is safe to re-run; `refresh` leaves seeded skills untouched.

---

## Phase 5: User Story 3 — Seeding is deterministic and Claude/Codex stay equivalent (Priority: P3)

**Goal**: Byte-identical output across runs; Claude and Codex surfaces equivalent;
seeded content cannot silently drift from the authored source.

**Independent Test**: Seed twice, diff produced skill trees (identical, no
dates); diff `.claude` vs `.codex` per skill (identical); drift guard fails if
declared set ≠ on-disk authored set or embedded bytes ≠ on-disk source.

### Tests for User Story 3 (in `SeededSkillsTests.fs`)

- [X] T011 [P] [US3] Determinism test (INV-3, FR-006/SC-004): seed the same input into two temp dirs, recursively diff the seeded skill trees for byte-identity, and assert no body contains run-varying content (no `20\d\d-\d\d-\d\d` date or `\d\d:\d\d:\d\d` time).
- [X] T012 [P] [US3] Parity test (INV-2, FR-002/SC-004): for every declared skill, assert the `.claude/skills/<name>/SKILL.md` and `.codex/skills/<name>/SKILL.md` bodies are byte-identical.
- [X] T013 [P] [US3] Drift guard (INV-7, FR-010/SC-005): assert the in-code declared membership set equals the on-disk authored `fs-gg-sdd-*` set under `.claude/skills/` (excluding `fs-gg-sdd-project`), equals the set under `.codex/skills/`, and that each embedded resource body equals both on-disk sources. The test must FAIL when a skill is added/removed/edited on disk without updating the declared set — mirroring the FS.GG.Rendering parity guard.

### Implementation for User Story 3

- [X] T014 [US3] Reconcile any drift surfaced by T011–T013 (e.g. align `.codex/skills/` bodies with `.claude/skills/` sources so parity holds, or correct the declared list). Re-run `SeededSkillsTests`; capture passing evidence. No production logic change expected beyond the declared-list/source reconciliation.

**Checkpoint**: All eight invariants (INV-1..INV-8 except the scaffold-seam INV-5/INV-6, covered next) are green.

---

## Phase 6: Cross-Cutting — Scaffold boundary, conformance, docs

**Purpose**: Prove the single-seam + provider-boundary invariants and keep the
skeleton-shape conformance surface authoritative.

- [X] T015 [P] In `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs`, extend `isSddOwned`/`isSddTree` to recognize the `.claude/skills/` and `.codex/skills/` subtrees as SDD-owned, so a provider that writes into them is rejected and the trees are never recorded as `generatedProduct` in `.fsgg/scaffold-provenance.json` (INV-6, FR-008).
- [X] T016 [US1] In `tests/FS.GG.SDD.Acceptance.Tests/CompositionAcceptanceTests.fs`, extend `skeletonPaths` (or the equivalent expected-shape list) with the 30 seeded skill paths and assert they are present after `scaffold` and **absent** from `scaffold-provenance.json` `producedPaths` (INV-5, INV-8). Confirm `init` and `scaffold` deliver the identical set via the shared seam (FR-007). *(Network-gated/opt-in suite — end-to-end coverage; the seam itself is also covered offline by T019.)*
- [X] T019 [P] [US1] Offline single-seam test (INV-5, FR-007) in `tests/FS.GG.SDD.Commands.Tests/` (e.g. `SeededSkillsTests.fs` or an existing scaffold-handler test): assert that the effect list `scaffold` builds includes `SeededSkills.skillEffects` (the 30 `WriteFile` effects), by inspecting the effects/work-model rather than driving a real provider — so the init≡scaffold seam reuse is verified in the **default offline inner loop**, not only behind the network-gated `CompositionAcceptanceTests`. Must fail if scaffold stops reusing `initEffects`.
- [X] T020 [P] [US3] Provider-boundary rejection test (INV-6, FR-008) in `tests/FS.GG.SDD.Commands.Tests/`: assert that when a (stub/fake) provider writes a file into `.claude/skills/` or `.codex/skills/`, scaffold rejects it as `providerWroteSddTree` (exercising the T015 `isSddOwned`/`isSddTree` guard) and the skill subtrees never appear in `scaffold-provenance.json` `producedPaths`. The negative case for the SDD↔provider boundary; pairs with T015.
- [X] T017 [P] Update `CLAUDE.md` Core boundary `init`/`scaffold` bullets to note that the skeleton now seeds the 15 `fs-gg-sdd-*` process skills (no-clobber, refresh-preserved, SDD-owned, excluded from provider routing and provenance), mirroring the constitution/early-stage-guidance wording.
- [X] T018 Run the full `quickstart.md` scenarios 1–5 end-to-end against a freshly built `fsgg-sdd`; record results (BYTE-IDENTICAL / PRESERVED / REFILLED / PARITY) as real evidence for the feature.

---

## Dependencies & Execution Order

### Phase order

1. **Phase 1 Setup** — no deps; start immediately.
2. **Phase 2 Foundational** — depends on Phase 1; **blocks all user stories**.
3. **Phase 3 (US1, P1)** — depends on Phase 2. The MVP.
4. **Phase 4 (US2, P2)** and **Phase 5 (US3, P3)** — depend on Phase 3 emission existing; independent of each other.
5. **Phase 6 Cross-cutting** — depends on US1 emission (T006) and the drift/parity reconciliation (T014).

### Key cross-task dependencies

- T002 before T003/T004 (compile order). T001 before T003 (loader needs the resources).
- T003 before T004 before T006 (module → effects → wired into `initEffects`).
- T005 before T006 (test-first). T006 before T007/T016/T018.
- T013/T014 before T016 (declared set must be coherent before the scaffold-shape assert).
- T006 before T019 (the seam reuse can only be asserted once skill effects are wired). T015 before T020 (the rejection test exercises the T015 guard).

### Parallel opportunities

- T002 ∥ (after T001) — independent fsproj edit vs resource embedding.
- T005, T008, T009, T011, T012, T013, T019, T020 are all `[P]` test authoring in distinct files/cases — write together (T019 after T006, T020 after T015).
- T015 and T017 are independent files (scaffold handler vs CLAUDE.md).

---

## MVP scope

**User Story 1 (Phase 1 → 2 → 3)** is the MVP: it delivers the entire point of
the feature — a seeded product whose agent can discover the SDD process. US2
(safe re-run) and US3 (determinism/parity/drift) are guardrails layered on top
and can ship incrementally after the MVP is validated.

## Task count per user story

- **US1 (P1)**: 4 — T005, T006, T007, T019 (+ shared Setup T001–T002, Foundational T003–T004; scaffold-shape T016).
- **US2 (P2)**: 3 — T008, T009, T010.
- **US3 (P3)**: 5 — T011, T012, T013, T014, T020.
- **Shared / cross-cutting**: 6 — T001, T002, T003, T004, T015, T017, T018.

Total: 20 tasks (T001–T020).

## Notes

- This is an authored checklist (standard Spec Kit). Ordering lives here; there is
  no separate machine-validated task graph.
- Never mark a failing task `[X]`. Verify every test FAILS before its
  implementation task, then passes after.
- Most US2/US3 "implementation" tasks are reconciliation, not new logic —
  no-clobber, refresh-preservation, and provenance-exclusion come for free from
  the `AgentGuidanceTarget` write-kind (Principle IV/V: no new effect, no new schema).

## Implementation evidence (2026-06-30)

All T001–T020 complete with real evidence:

- Full solution `dotnet test` green: 397 (Commands) + 33/3-skip (Acceptance,
  network-gated) + 72 (Cli) + 135 (Artifacts) + 41 (Contracts) + 18 (Validation).
  The 3 skips are the opt-in network-gated composition-acceptance facts.
- Quickstart scenarios 1–5 run end-to-end against a freshly built `fsgg-sdd`:
  S1 15×2 present + no project skill; S2 scaffold (fixture provider) delivers the
  identical set, absent from provenance `producedPaths`; S3 PRESERVED + REFILLED;
  S4 BYTE-IDENTICAL + NO-DATES; S5 PARITY all 15.
- No synthetic evidence relied on for any task; the scaffold seam and the
  provider-boundary rejection are exercised over a real `dotnet new` provider fixture.
