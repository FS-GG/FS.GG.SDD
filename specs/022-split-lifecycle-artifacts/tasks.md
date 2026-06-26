---
description: "Task breakdown for splitting LifecycleArtifacts.fs per artifact family"
---

# Tasks: Split `LifecycleArtifacts.fs` per artifact family

**Input**: Design documents from `/specs/022-split-lifecycle-artifacts/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md,
contracts/module-decomposition.md, quickstart.md

**Tier**: Tier 2 (internal change). No new behavior; the existing test suite is
the contract enforcer (plan §Constitution Check).

**Tests**: No new tests are written. Per the stakeholder decisions in plan.md,
the **existing 437-test suite is the only behavioral gate** ("fail before / pass
after" does not apply to a no-op-behavior refactor — Principle VI refactor
posture). All `[X]` task evidence is real: a green `dotnet build` + `dotnet
test`, plus the supplementary quickstart checks.

**Completion evidence (2026-06-26)**: `dotnet build` succeeds; `dotnet test`
green at **437 passing** (103 Artifacts + 18 Validation + 265 Commands + 51 Cli),
equal to baseline. Monolith `LifecycleArtifacts.fs(i)` removed; 16 module files
under `LifecycleArtifacts/` (largest `Plan.fs` = 389 lines, was 3,161). Public
member set preserved — `PublicSurface.baseline` regenerated for the
module-qualifier rename only (139 entries, member names unchanged). Clean-rebuild
FS3261/FS0025 **unique-site counts identical before/after** (286 FS3261 + 4
FS0025), relocated into `Analysis.fs`/`Verify.fs`/`Ship.fs`/`Guidance.fs`. Each
shared helper defined exactly once (`Internal`/`Core`). The split uses one
`[<AutoOpen>]` module per family under `namespace FS.GG.SDD.Artifacts`; the
`Internal` helper module is `[<AutoOpen>] module internal` (no `.fsi`); two
cross-family helpers stayed off the public surface as `val internal` slices
(`Core.frontMatter`, `RequirementModel.parseMarkdownRequirementMentions`) and the
shared JSON→`Core` parsers are `val internal` in `Analysis.fsi`.

## Key F# reality (drives ordering)

F# forbids two files declaring `module LifecycleArtifacts`, and an `[<AutoOpen>]`
family module cannot coexist with the monolith defining the same types. So this
is a **big-bang cutover**, not an incrementally-green family-by-family build:
carve every family file first (Phases 2–3), then flip the fsproj + delete the
monolith + update consumers in one atomic step (Phase 4) and iterate to green.
Final type homes (e.g. `EvidenceObligation`) are compiler-confirmed
move-until-it-compiles (data-model.md †).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: carves a different, not-yet-active file — safe to do in parallel.
- **[Story]**: US1 (per-family split), US2 (consumers/contract), US3 (warnings).

---

## Phase 1: Setup & Baseline

**Purpose**: Capture the regression references the refactor is measured against,
and create the target folder.

- [X] T001 Capture the test baseline: run `dotnet test --nologo` from repo root
  and record the pass count (expected 437) to `/tmp/tests-before.txt`
  (quickstart §0). This is the FR-004/SC-003 reference.
- [X] T002 [P] Capture the warning baseline two ways (quickstart §0; SC-005 is a
  **unique-site** metric — see T028/I5). (a) Inventory: `dotnet build
  -clp:NoSummary 2>&1 | grep -oE "warning FS[0-9]+" | sort | uniq -c | tee
  /tmp/warn-before.txt`. (b) Unique-site total for the relocation gate: `dotnet
  build -clp:NoSummary 2>&1 | grep -oE "[^ ]+\.fs\([0-9]+,[0-9]+\): warning
  FS(3261|0025)" | sort -u | wc -l | tee /tmp/warn-sites-before.txt` (expect the
  290/4 unique-site figures). This is the FR-008/SC-005 reference.
- [X] T003 [P] Create the target folder `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/`
  (replaces the `LifecycleArtifacts.fs(i)` monolith; the old files stay until the
  Phase 4 cutover).

**Checkpoint**: Baselines recorded; build is green on `main`.

---

## Phase 2: Foundational — shared core (Blocking Prerequisites)

**Purpose**: Extract the cross-family shared definitions that every family file
depends on. **No family file can compile until these exist and precede it**
(data-model §Dependency rules). These are carved out but not wired into the
build until the Phase 4 cutover.

⚠️ **CRITICAL**: All family extraction (Phase 3) targets these modules.

- [X] T004 [US1] Carve `module internal FS.GG.SDD.Artifacts.LifecycleArtifacts.Internal`
  into `LifecycleArtifacts/Internal.fs` (no `.fsi` — `internal`, per Principle
  III). Move **verbatim** the family-agnostic helpers from
  `LifecycleArtifacts.fs` (YAML: `parseYaml`, `tryMapping`, `tryScalar`,
  `tryChild`, `scalarList`, `schemaVersion`, `requiredScalar`, `combine`,
  `normalizePath`, `artifact`, `sourceArtifact`; Markdown: `frontMatter`,
  `proseStatus`, `sourceLocation`, `hasHeading`, `sectionLines`, scoped-ID
  helpers; JSON: `tryJsonProperty`, `jsonString`, `jsonInt`, `jsonBool`,
  `jsonArray`, `jsonStringList`, `parseJsonDigest`, `jsonDigest`,
  `diagnosticSeverityFromJson`, `artifactFromJsonPath`). No copies — relocate
  once (FR-006). (data-model row `Internal`; research Decision 2)
- [X] T005 [US1] Carve `[<AutoOpen>] module FS.GG.SDD.Artifacts.Core` into
  `LifecycleArtifacts/Core.fs` + `Core.fsi`. Public types `FileSnapshot`,
  `LifecycleArtifactContract`, `AnalysisSourceRecord`,
  `AnalysisGeneratedViewRecord`, `AnalysisOptionalBoundaryFact`; value
  `standardArtifactContracts`. Copy the matching signatures verbatim from the old
  `LifecycleArtifacts.fsi` into `Core.fsi`. (data-model row `Core`)

**Checkpoint**: `Internal` and `Core` files exist with their `.fsi`, content
moved verbatim out of the monolith (the monolith is not yet edited/removed).

---

## Phase 3: User Story 1 — Per-family split (Priority: P1) 🎯 MVP

**Goal**: Every artifact family's types + parsers live co-located in their own
`[<AutoOpen>] module FS.GG.SDD.Artifacts.<Name>` file with a verbatim-sliced
`.fsi`, under `LifecycleArtifacts/`. (FR-001, FR-002, FR-006, FR-009)

**Independent Test**: After Phase 4 makes the build green, a single
`ls src/FS.GG.SDD.Artifacts/LifecycleArtifacts/` shows one file per family and
`wc -l` shows the largest `.fs` ≤ ~700 lines (quickstart §3–4).

Each task below carves one family: move its records/DUs and `parse*` values
**verbatim** (no field/case reorder — FR-005/FR-008) into `<Name>.fs`, and copy
its slice of the old 722-line `.fsi` into `<Name>.fsi`. Each family `open`s
`Internal` (and `Core` where needed). These are `[P]` — distinct, not-yet-active
files — but all converge on the T021 cutover.

- [X] T006 [P] [US1] `Config` → `LifecycleArtifacts/Config.fs(i)`:
  `ProjectLifecycleConfig`, `SddLifecyclePolicy`, `AgentGuidanceTarget`,
  `AgentGuidanceConfig`; `parseProjectConfig`, `parseSddLifecyclePolicy`,
  `parseAgentGuidanceConfig`. (data-model row `Config`)
- [X] T007 [P] [US1] `WorkItemMetadata` → `LifecycleArtifacts/WorkItemMetadata.fs(i)`:
  `WorkItemMetadata`; `parseWorkItemMetadata`.
- [X] T008 [P] [US1] `Specification` → `LifecycleArtifacts/Specification.fs(i)`:
  `SpecificationFrontMatter`, `SpecificationRequirementReference`,
  `SpecificationFacts`; `specificationStandardSections`,
  `parseSpecificationFacts`.
- [X] T009 [P] [US1] `Clarification` → `LifecycleArtifacts/Clarification.fs(i)`:
  `ClarificationDecisionKind`, `ClarificationAnswerKind`,
  `ClarificationFrontMatter`, `ClarificationQuestion`, `ClarificationAnswer`,
  `ClarificationDecisionFact`, `RemainingAmbiguity`, `ClarificationFacts`;
  `clarificationStandardSections`, `parseClarificationFacts`.
- [X] T010 [P] [US1] `Checklist` → `LifecycleArtifacts/Checklist.fs(i)`:
  `ChecklistFrontMatter`, `ChecklistSourceSnapshot`, `ChecklistItem`,
  `ChecklistReviewResult`, `ChecklistFacts`; `checklistStandardSections`,
  `parseChecklistFacts`.
- [X] T011 [P] [US1] `Plan` → `LifecycleArtifacts/Plan.fs(i)`: `PlanFrontMatter`,
  `PlanSourceSnapshot`, `PlanDecision`, `PlanContractReference`,
  `VerificationObligation`, `PlanMigrationNote`, `GeneratedViewImpact`,
  `AcceptedPlanDeferral`, `PlanFacts`; `planStandardSections`, `parsePlanFacts`.
  (largest cluster ≈ 300 lines — data-model §Size)
- [X] T012 [P] [US1] `RequirementModel` → `LifecycleArtifacts/RequirementModel.fs(i)`:
  `Requirement`, `Decision`, `MarkdownRequirementMention`; `parseRequirements`,
  `parseDecisions`, `parseMarkdownRequirementMentions`.
- [X] T013 [P] [US1] `Task` → `LifecycleArtifacts/Task.fs(i)`: `TaskFrontMatter`,
  `TaskSourceSnapshot`, `TaskGraphFinding`, `TaskStatus`, `WorkTask`,
  `TaskFacts`; `parseTaskFacts`, `parseTasks`.
- [X] T014 [P] [US1] `Analysis` → `LifecycleArtifacts/Analysis.fs(i)`:
  `AnalysisSourceRelationship`, `AnalysisFinding`, `AnalysisReadiness`,
  `AnalysisNextAction`, `AnalysisView`; `parseAnalysisView`. (depends on `Core`
  analysis records; FS3261/FS0025 view-parser site — US3)
- [X] T015 [P] [US1] `Evidence` → `LifecycleArtifacts/Evidence.fs(i)`:
  `EvidenceKind`, `EvidenceSubject`, `EvidenceSourceSnapshot`,
  `EvidenceSourceReference`, `SyntheticDisclosure`, `EvidenceDeclaration`,
  `EvidenceObligation`†, `EvidenceArtifact`; `parseEvidenceArtifact`,
  `parseEvidence`. († `EvidenceObligation` home compiler-confirmed Evidence vs
  Verify — data-model)
- [X] T016 [P] [US1] `Verify` → `LifecycleArtifacts/Verify.fs(i)`:
  `EvidenceDispositionState`, `EvidenceDisposition`,
  `RequiredTestDispositionState`, `RequiredTestDisposition`,
  `SkillVisibilityState`, `SkillVisibilityFact`, `VerificationFinding`,
  `VerificationStageReadiness`, `VerificationLifecycleReadiness`,
  `VerificationTaskGraphReadiness`, `VerificationView`; `parseVerificationView`.
  Disposition types relocate **here** (not Evidence) to break the Evidence↔Verify
  apparent coupling (research Decision 3). (FS3261/FS0025 view-parser site — US3)
- [X] T017 [P] [US1] `Ship` → `LifecycleArtifacts/Ship.fs(i)`:
  `ShipReadinessFinding`, `ShipLifecycleStageReadiness`,
  `ShipVerificationReadinessSummary`, `ShipView`; `parseShipView`.
  (FS3261/FS0025 view-parser site — US3)
- [X] T018 [P] [US1] `Guidance` → `LifecycleArtifacts/Guidance.fs(i)`:
  `GuidanceCommandEntry`, `GuidanceSkillEntry`, `GeneratedGuidanceFileRef`,
  `GeneratedAgentGuidance`; `parseGeneratedAgentGuidance`.
- [X] T019 [US1] `WorkItem` → `LifecycleArtifacts/WorkItem.fs(i)`:
  `ParsedWorkItem`; `loadWorkItemFromSnapshots`. **Must be last** in compile order
  — aggregates `ProjectLifecycleConfig`, `SddLifecyclePolicy`,
  `AgentGuidanceConfig`, `WorkItemMetadata`, `Requirement`, `Decision`,
  `WorkTask`, `EvidenceDeclaration`, `MarkdownRequirementMention`, … (depends on
  all preceding families — data-model).

**Checkpoint**: All 16 files — 15 public modules (the ≈11 artifact families plus
`Core`, `WorkItemMetadata`, `RequirementModel`, `WorkItem`) and the `Internal`
helper — carved verbatim; the monolith content is fully accounted for with nothing
left behind.

---

## Phase 4: User Story 2 — Cutover: consumers & contract unaffected (Priority: P1)

**Goal**: Flip the build to the new files and update in-repo consumers/tests so
the full suite passes — the binding gate (FR-003, FR-004, FR-007; SC-002,
SC-003). This is the atomic big-bang step.

**Independent Test**: `dotnet build` + `dotnet test` green with the same pass
count as `/tmp/tests-before.txt`; only mechanical `open`/qualifier edits in
consumers/tests.

- [X] T020 [US2] Update the compile-order manifest in
  `src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj`: replace the
  `LifecycleArtifacts.fsi`/`LifecycleArtifacts.fs` pair (lines 20–21) with the
  16-module ordered list from data-model §Compile-order manifest (`Internal.fs`,
  then `<Name>.fsi`+`.fs` for each family, `WorkItem` last), still ordered before
  `LifecycleRuleContracts.fsi`. (FR-007)
- [X] T021 [US2] Delete the monolith `LifecycleArtifacts.fs` and
  `LifecycleArtifacts.fsi`, then `dotnet build` the Artifacts project. Resolve
  forward-reference/type-home errors by moving definitions per the compiler
  (move-until-it-compiles; data-model †). Build the Artifacts project green
  before touching consumers.
- [X] T022 [P] [US2] Update Artifacts-internal consumers to
  `open FS.GG.SDD.Artifacts` (drop `open ...LifecycleArtifacts` /
  `LifecycleArtifacts.` qualifiers): `Serialization.fs(i)`, `WorkModel.fs(i)`.
  Mechanical only.
- [X] T023 [P] [US2] Update Commands consumers: `CommandTypes.fs(i)`,
  `CommandEffects.fs`, `CommandWorkflow.fs` in `src/FS.GG.SDD.Commands/`.
  Mechanical `open`/qualifier updates only — no logic change (FR-003).
- [X] T024 [P] [US2] Update Artifacts test files (`open`/qualifier only):
  `tests/FS.GG.SDD.Artifacts.Tests/` — `TestSupport.fs`, `SchemaContractTests.fs`,
  `SpecificationArtifactTests.fs`, `ClarificationArtifactTests.fs`,
  `ChecklistArtifactTests.fs`, `PlanArtifactTests.fs`, `TasksArtifactTests.fs`,
  `AnalysisViewTests.fs`, `EvidenceArtifactTests.fs`, `VerificationViewTests.fs`,
  `ShipViewTests.fs`, `AgentGuidanceViewTests.fs`. No test-logic edits (FR-004).
- [X] T025 [P] [US2] Update Commands test files (`open`/qualifier only):
  `tests/FS.GG.SDD.Commands.Tests/` — `AnalyzeCommandTests.fs`,
  `EvidenceCommandTests.fs`, `VerifyCommandTests.fs`, `ShipCommandTests.fs`,
  `AgentsCommandTests.fs`. No test-logic edits.
- [X] T026 [US2] Regenerate the public-surface baseline (analysis finding G1).
  Retiring `module LifecycleArtifacts` for per-family `[<AutoOpen>]` modules
  changes the dumped qualifiers, so `SurfaceBaselineTests` will fail until the
  snapshot is updated. Regenerate `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline`
  (the project's `SurfaceBaselineTests.fs` records the regen mechanism — typically
  a `*_UPDATE_BASELINES`/env-gated run), and check whether
  `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` also shifts (it does iff
  a Commands public signature names a moved Artifacts type). Review the baseline
  diff to confirm it is **only** module-qualifier renames with the member set
  preserved (FR-002 amended) — no member added/removed. This regeneration is the
  expected, permitted baseline edit (not a contract change); do not hand-edit
  member entries to force a pass.
- [X] T027 [US2] Run the full suite: `dotnet build` then `dotnet test --nologo`.
  Confirm the pass count equals `/tmp/tests-before.txt` (SC-003) and that the
  only consumer/test edits were mechanical `open`/qualifier lines plus the T026
  baseline regen (SC-002 amended). If any family type fails to resolve, fix its
  home (T021) — never weaken a test.

**Checkpoint**: Build + full suite green (incl. regenerated surface baseline); the
refactor is behaviorally invisible.

---

## Phase 5: User Story 3 — Warning localization verified (Priority: P3)

**Goal**: Confirm the split **relocated** FS3261/FS0025 warnings to the view
parser family files without changing their counts (FR-008, SC-005). Setup
benefit for R4/R5 — not a code change.

**Independent Test**: warning counts diff clean; FS3261/FS0025 view-parser sites
resolve to `Analysis.fs` / `Verify.fs` / `Ship.fs`.

- [X] T028 [US3] Verify warnings by **unique-site** count, the SC-005 metric
  (analysis finding I5). SC-005's 290 FS3261 / 4 FS0025 are *unique source-site*
  counts, **not** raw per-project warning lines (research.md Decision 5), so
  comparing `uniq -c` line tallies is the wrong basis. Capture unique sites
  before and after — e.g. `dotnet build -clp:NoSummary 2>&1 | grep -oE
  "[^ ]+\.fs\([0-9]+,[0-9]+\): warning FS(3261|0025)" | sort -u | wc -l` — and
  confirm the unique-site total is unchanged from baseline. (Re-baseline the
  "before" with the same command on `main` if T002's tally used raw counts.) A
  changed total means a definition was altered, not just moved — fix to verbatim.
- [X] T029 [US3] Confirm view-parser warnings localized:
  `dotnet build -clp:NoSummary 2>&1 | grep -E "warning (FS3261|FS0025)" |
  grep -oE "LifecycleArtifacts/[A-Za-z]+\.fs" | sort | uniq -c` — expect sites
  concentrated in `Analysis.fs`, `Verify.fs`, `Ship.fs` (SC-005).

**Checkpoint**: Warnings relocated, not changed.

---

## Phase 6: Polish & Cross-Cutting

- [X] T030 [P] Verify size & presence outcomes (FR-001/FR-009; SC-001/SC-006):
  `ls src/FS.GG.SDD.Artifacts/LifecycleArtifacts/` shows all 16 module files
  (the ~11 artifact families plus `Core`, `WorkItemMetadata`, `RequirementModel`,
  `WorkItem`, and the `Internal` helper), the monolith is gone (`test ! -f
  .../LifecycleArtifacts.fs`), and `wc -l .../LifecycleArtifacts/*.fs | sort -n |
  tail -3` shows the largest `.fs` ≤ ~700 lines (quickstart §3–4).
- [X] T031 [P] No-duplication review (FR-006): diff/inspect to confirm each shared
  helper body exists exactly once in `Internal.fs`/`Core.fs` and was not copied
  into a family file (quickstart §6).
- [X] T032 Flip the R3 row in
  `docs/reports/2026-06-26-074428-refactor-analysis.md` from 🔴 to complete, with
  a link to this feature's readiness/evidence (FR-010; quickstart §7).
- [X] T033 Run the full quickstart.md walkthrough end-to-end as the final
  acceptance pass (steps 1–7) and record the result as this feature's evidence.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: start immediately; T001 before any post-change comparison.
- **Phase 2 (Foundational: Internal, Core)**: blocks all of Phase 3 — every
  family references `Internal`/`Core`.
- **Phase 3 (US1 family carving)**: T006–T018 are mutually parallel `[P]` (distinct
  files); T019 (`WorkItem`) is conceptually last but can be carved any time — it
  just must be ordered last in the fsproj.
- **Phase 4 (US2 cutover)**: depends on Phases 2–3 complete. T020→T021 first
  (build Artifacts green), then T022–T025 `[P]`, then T026 (baseline regen), then
  T027 gate.
- **Phase 5 (US3)**: depends on a green Phase 4 build.
- **Phase 6 (Polish)**: depends on Phase 4 (T030/T031 after green; T032/T033 last).

### Critical path

T004/T005 (shared core) → all family carving (T006–T019) → T020 (fsproj) → T021
(delete monolith + build green) → T026 (baseline regen) → T027 (full suite green)
→ T032 (report).

### Parallel opportunities

- T002, T003 in parallel with T001.
- All family carves T006–T018 in parallel (each writes its own new file).
- Consumer/test updates T022–T025 in parallel once T021 is green.
- T030, T031 in parallel.

---

## Notes

- **No new tests** — the existing 437-test suite is the contract enforcer
  (plan §Constitution Check VI; contracts/module-decomposition.md §Acceptance).
- **Verbatim moves only** — no field/case/parser-body edits, preserving
  determinism (FR-005 amended) and warning posture (FR-008). The mechanism is
  fixed by research.md (`[<AutoOpen>]` family modules under `FS.GG.SDD.Artifacts`);
  do not reintroduce a facade.
- **Public-surface baseline regen (T026) is expected**, not a contract breach —
  the module qualifiers change while the member set is preserved (FR-002 amended,
  plan §Tier-boundary note).
- Never mark a task `[X]` on a red build; never weaken a test to green it —
  narrow the type home and document it instead.
- Commit after each green checkpoint (after T027 especially).

---

## Summary

- **Task count**: 33 (US1: 16 carve tasks T004–T019; US2: 8 cutover tasks
  T020–T027 incl. surface-baseline regen T026; US3: 2 verify tasks T028–T029;
  Setup 3, Polish 4).
- **Parallel opportunities**: 13 family carves (T006–T018) + 4
  consumer/test-update tasks (T022–T025) + several verification tasks.
- **Suggested MVP**: Phases 1–4 (US1 split + US2 cutover) — a green build/test
  suite (incl. regenerated surface baseline) with the monolith replaced by
  per-family files is the shippable deliverable. US3 (warnings) and Phase 6 polish
  confirm and document it.
