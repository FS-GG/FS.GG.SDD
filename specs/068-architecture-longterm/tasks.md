---
description: "Task list for feature 068 — Architecture longer-term cleanups"
---

# Tasks: Architecture longer-term cleanups

**Input**: Design documents from `/specs/068-architecture-longterm/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/preserved-contracts.md

**Change tier**: Tier 2 (internal). The governing contract is
[preserved-contracts.md](./contracts/preserved-contracts.md): **every machine
surface stays byte-identical.** Every implementation task ends at the same gate —
`git diff` over `**/*.baseline`, `src/**/*.fsi`, and the readiness/JSON golden
fixtures is empty, and `dotnet test FS.GG.SDD.sln` is green with no new warnings.

**Format**: `[ID] [P?] [Story] Description` — `[P]` = parallel-safe (different
files, no dependency on another incomplete task in this phase).

## MVU applicability

This feature makes **no** behavior change and adds no stateful workflow. The MVU
interpreter and effect edge are untouched; Decision 5 *strengthens* the pure core
(removes ambient cwd + static-init throw). So Principle V yields no new
`Model`/`Msg`/`Effect` tasks; the existing suite is the fail-before/pass-after
evidence for the refactors, and the one new behavior (docs byte-identity) gets its
own guard (T031).

---

## Delivery status (reconciled 2026-07-03)

This branch **merges two parallel 068 efforts** (same author, two sessions): the
original `8bf3bb1` (US1 envelope + these lifecycle docs) and a second effort that
implemented US2/US4/US5/US6. They had **no source-code overlap** and combine
cleanly; the full suite is green and the readiness JSON stays byte-identical (my
byte-goldens were captured against the US1 wrapper's output and pass — so they now
*guard* the shared frame).

| Story | State | Notes |
|---|---|---|
| **US1** envelope | ✅ Done | `8bf3bb1` wrapper + byte-goldens (`ReadinessViewGoldenTests`) now guarding it |
| **US2** DU states | ✅ Done | **Superset** of the plan: `ViewCurrencyClass` (8 cases) + `ReconciliationOutcome` (5) + `ReconciliationStepId` (3), `RefreshDisposition.EarlyStage`; `…Value` maps pinned; +2 lines `PublicSurface.baseline` (bounded additive `.fsi`) |
| **US6** docs identity | ✅ Done | `AGENTS.md ≡ CLAUDE.md` + `AgentSurfaceDriftTests` (Contracts.Tests) |
| **US4** Parsing renames | ✅ Done | Final names `EarlyStageAuthoring`/`ChecklistPlanAuthoring`/`TaskGraphAuthoring` (differ from the candidate names below) |
| **US5** purity | ◑ Partial | SeededSkills legible-failure ✅. `projectIdFromRoot`: the "no ambient cwd" premise was verified **FALSE** (`DirectoryInfo(".").Name` returns the cwd leaf — load-bearing); coupling made explicit + pinned, full edge-removal **deferred** (behavior risk). `RegistryDocument.load` left **out of scope** (public `.fsi` / documented Constitution-V edge) rather than commented. |
| **US3** de-AutoOpen | ⏭️ Deferred | Spike found ~200-site refactor with pervasive same-named helpers across modules; ambiguous under warnings-as-errors. Its own follow-up PR. |

The per-task checkboxes below retain the original plan; treat this table as the
authoritative delivered state.

---

## Phase 1: Setup & baseline capture (Shared)

**Purpose**: Establish the byte-identity gate before any edit, so every later task
can prove it changed no contract.

- [X] T001 Clean baseline captured: build succeeds with **2 benign FS3262** warnings; full suite **877 passed / 0 failed / 3 skipped** (Artifacts 175, Commands 478, Cli 87, Contracts 86, Acceptance 33+3gated, Validation 18). This is the pre-feature reference.
- [X] T002 [P] Pinned surfaces snapshot = git HEAD (5 `PublicSurface.baseline` + 46 `src/**/*.fsi`, all clean vs HEAD). Gate = empty `git diff` over those paths. Readiness JSON asserted by `AnalysisViewTests`/`VerificationViewTests`/`ShipViewTests` (Artifacts) + `VerifyCommandTests`/`GovernanceHandoffTests`/`ReleaseConformanceTests` (Commands).
- [X] T003 [P] Wire tokens recorded (data-model tables): refresh = `blocked`/`refreshed`/`stale`/`already-current`/`current`/`na`; upgrade = `wouldApply`/`applied`/`failed`/`skipped`. `toToken` must reproduce these exactly.

**Checkpoint**: Baseline captured; the gate is defined.

---

## Phase 2: User Story 1 — Readiness envelope (Priority: P1) 🎯 MVP

**Goal**: One `writeReadinessEnvelope` frame produces `analysis`/`verify`/`ship`
so they cannot drift structurally; output stays byte-identical.

**Independent Test**: Regenerate the three views; the golden fixtures diff is
empty, and one frame function has three thin callers.

- [X] T004 [US1] Added `writeReadinessEnvelope` to `ViewGeneration.fs:537` — owns the `MemoryStream`/`Utf8JsonWriter(Indented=true)` lifecycle, opening object + `writeViewPreamble` + `writeSourcesArray`, the `writeBody` callback, and the terminal `WriteEndObject` + flush + UTF-8 decode.
- [X] T005 [US1] Added `writeGovernanceReadinessTail` to `ViewGeneration.fs` — the identical verify/ship tail `writeReadinessFindings` → `writeBoundaryFacts "governanceCompatibility"` → `writeViewDiagnostics` → `readiness` scalar. (Scope corrected vs the task line: `generatedViews` is *not* in the shared tail — ship interposes its `disposition` object between generatedViews and findings, so each body emits generatedViews itself.)
- [X] T006 [US1] `analysisJson` rewritten as a thin `writeReadinessEnvelope` caller supplying its ordered body (sourceRelationships, readiness object, findings before generatedViews, `optionalBoundaryFacts`, diagnostics, nextAction). Byte order preserved.
- [X] T007 [US1] `verifyJson` rewritten as a thin caller: body = lifecycleReadiness + taskGraph + evidence/test/skill dispositions + generatedViews + `writeGovernanceReadinessTail` + nextAction.
- [X] T008 [US1] `shipJson` rewritten as a thin caller: body = lifecycleReadiness + verificationReadiness + evidenceDispositions + generatedViews + disposition + `writeGovernanceReadinessTail` + nextAction.
- [X] T009 [US1] **Gate PASSED**: full suite **877/0/3 — identical to T001 baseline**; targeted view+JSON-contract tests 112/0; `git diff` over `**/*.baseline` + `src/**/*.fsi` empty; grep = 1 `writeReadinessEnvelope` def + 3 callers (SC-001/SC-002 met). Byte-identity confirmed end-to-end.

**Checkpoint**: US1 complete — three views share one frame, zero byte change.

---

## Phase 3: User Story 2 — DU-typed working state (Priority: P2)

**Goal**: View-currency and upgrade-outcome states are DUs matched exhaustively;
wire tokens emitted at one projection point each.

**Independent Test**: No raw-string comparison of those concepts remains; existing
refresh/upgrade tests pass; contract diff empty.

- [ ] T010 [P] [US2] Define `RefreshViewState` DU + `toToken` in the Commands.Internal namespace (data-model table: `Blocked→"blocked"`, `Refreshed→"refreshed"`, `Stale→"stale"`, `AlreadyCurrent→"already-current"`, `Current→"current"`, `NotApplicable→"na"`). Place it where `HandlersRefresh` can consume it (its own module or `Foundation`, respecting compile order before `HandlersRefresh`).
- [ ] T011 [P] [US2] Define `UpgradeStepOutcome` DU + `toToken` (`WouldApply→"wouldApply"`, `Applied→"applied"`, `Failed→"failed"`, `Skipped→"skipped"`) placed before `Drift`/`HandlersUpgrade` in compile order.
- [ ] T012 [US2] Replace the ~30 raw-string view-currency sites in `HandlersRefresh.fs` (lines per research Decision 2) with `RefreshViewState` matches; map into the existing `GeneratedViewCurrency` DU at its boundary (`:682`) instead of duplicating. Emit tokens only via `toToken`. (after T010)
- [ ] T013 [US2] Change `Drift.Step.Outcome` to `UpgradeStepOutcome` in `Drift.fs` (`:199,216,227`) and update `HandlersUpgrade.fs` (`:88,105,120,165-166,266`) to DU matches; emit tokens only via `toToken`. (after T011)
- [ ] T014 [US2] **Gate**: `grep -nE '"(refreshed|blocked|stale|already-current|wouldApply|applied|skipped)"'` over the three files returns hits **only** inside the two `toToken` functions; run `--filter "FullyQualifiedName~Refresh|FullyQualifiedName~Upgrade|FullyQualifiedName~Drift"`; contract diff empty (SC-003).

**Checkpoint**: US2 complete — states typed, wire bytes unchanged.

---

## Phase 4: User Story 6 — CLAUDE.md ↔ AGENTS.md identical + guard (Priority: P2)

**Goal**: The two agent surfaces carry one canonical content; a guard fails on any
byte divergence. Fully independent of the code phases.

**Independent Test**: `diff CLAUDE.md AGENTS.md` is empty; perturbing one byte
fails the guard.

- [ ] T015 [P] [US6] Reconcile `AGENTS.md` to byte-identical `CLAUDE.md` content (CLAUDE.md is the authored source; bring AGENTS.md up to the full doctrine, including the SPECKIT plan-reference block AGENTS.md currently lacks). Verify `diff CLAUDE.md AGENTS.md` is empty.
- [ ] T016 [US6] Add `tests/FS.GG.Contracts.Tests/AgentSurfaceDriftTests.fs` — a fact asserting `File.ReadAllText "CLAUDE.md" = File.ReadAllText "AGENTS.md"`, resolved via the repo-root helper, failing with an actionable message naming the divergence. Register it in `FS.GG.Contracts.Tests.fsproj` (alongside `AuthoringDocsContractTests.fs` / `EarlyStageGuidanceContractTests.fs`).
- [ ] T017 [US6] **Gate**: run `--filter "FullyQualifiedName~AgentSurface"` (passes); temporarily append a byte to `AGENTS.md`, confirm the guard fails, revert (SC-007).

**Checkpoint**: US6 complete — `claude ≡ codex` for the context docs, guarded.

---

## Phase 5: User Story 5 — Purity soft spots (Priority: P3)

**Goal**: Two §1.5 leaks fixed (non-observably), the third documented.

**Independent Test**: A missing seeded-skill resource yields a diagnostic (not
`TypeInitializationException`); `projectIdFromRoot` has no ambient-cwd dependency;
`RegistryDocument.load` carries an intentional-edge comment; suite green.

- [ ] T018 [P] [US5] `SeededSkills.fs` (`:52-58`): convert `seededSkills` from an eager module `let` to `lazy` (or a `loadSeededSkills ()` function) evaluated at a defined call site; make the missing-embedded-resource case raise a dedicated, actionable message naming the resource instead of a static-init `failwithf`. Re-point every consumer (`HandlersScaffold`/`init` effects) to the new accessor.
- [ ] T019 [US5] Update `tests/FS.GG.SDD.Commands.Tests/SeededSkillsTests.fs` and any `FS.GG.Contracts.Tests` counterpart to consume the new `seededSkills` accessor; the pinned on-disk set and the 45-effect fan-out tripwire stay byte-identical. (after T018)
- [ ] T020 [P] [US5] `Foundation.fs` (`projectIdFromRoot`, `:34-40`): ensure the root is resolved to an absolute path at the effect edge before it reaches the planner, so the pure function never resolves `"."` against ambient cwd. Verify the emitted project id is unchanged for concrete roots.
- [ ] T021 [P] [US5] `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/RegistryDocument.fs` (`load`, `:92`): add a comment documenting the file-IO read as an intentional, justified artifact-load edge co-located with the registry model (relocation to host deferred as Tier-1). Mirror the purity ledger note in data-model.md.
- [ ] T022 [US5] **Gate**: `grep failwithf SeededSkills.fs` gone/replaced; `--filter "FullyQualifiedName~SeededSkill"` green; contract diff empty (SC-006).

**Checkpoint**: US5 complete.

---

## Phase 6: User Story 4 — Rename Parsing slabs by responsibility (Priority: P3)

**Goal**: The three parser modules named for what they parse, not compile position.
Name-only; compile order preserved.

**Independent Test**: `grep ParsingEarly|ParsingMid|ParsingTasks` in `src/`+`tests/`
returns 0; build green.

- [ ] T023 [US4] Rename `ParsingEarly.fs` → module + file for charter/spec/clarify parsing (candidate `CharterSpecClarifyParsing`; final name confirmed against contents). Update the `.fsproj` `<Compile Include>` entry and all references.
- [ ] T024 [US4] Rename `ParsingMid.fs` → checklist/plan parsing (candidate `ChecklistPlanParsing`). Update `.fsproj` + references. (same file as T023's `.fsproj` edit — sequence after T023)
- [ ] T025 [US4] Rename `ParsingTasks.fs` → tasks/evidence parsing (candidate `TaskEvidenceParsing`). Update `.fsproj` + references. (after T024)
- [ ] T026 [US4] **Gate**: `grep -rn 'ParsingEarly\|ParsingMid\|ParsingTasks' src/ tests/` returns 0; `dotnet build` green; compile order in `FS.GG.SDD.Commands.fsproj` unchanged in sequence; contract diff empty (SC-005).

**Checkpoint**: US4 complete.

---

## Phase 7: User Story 3 — Drop the flat `[<AutoOpen>]` (Priority: P2, sequenced last)

**Goal**: Remove the blanket `[<AutoOpen>] module internal` across the 15 modules
that still carry it; restore call-site provenance via explicit `open` / qualified
access. Highest churn — done last so it rebases over settled names/symbols.

**Independent Test**: `grep AutoOpen` in `CommandWorkflow/` shows only
individually-justified survivors; build green, no new warnings; contract diff empty.

**Depends on**: Phases 2–6 complete (envelope, DUs, renames, purity all landed).

- [ ] T027 [US3] Remove `[<AutoOpen>]` from the 15 `CommandWorkflow/*.fs` modules that carry it (all except `Drift`, `SeededSkills`, which already model the target). Do it in compile order.
- [ ] T028 [US3] Fix every resulting resolution error by adding an explicit `open <Module>` at the top of each consuming file (preferred for ubiquitous modules like `Foundation`) or qualifying the call site. Any surviving `[<AutoOpen>]` must carry a one-line justification comment. (after T027)
- [ ] T029 [US3] **Gate**: `dotnet build FS.GG.SDD.sln` green with warning count **not** increased vs T001; `grep -rn AutoOpen src/FS.GG.SDD.Commands/CommandWorkflow/` shows only justified survivors; contract diff empty (SC-004).

**Checkpoint**: US3 complete — flat scope gone, provenance visible.

---

## Phase 8: Polish & full validation

- [ ] T030 Run the full `quickstart.md` — every SC command behaves as annotated.
- [ ] T031 **Final gate (SC-008)**: `git diff --stat -- '**/*.baseline' 'src/**/*.fsi'` empty; readiness/JSON golden fixtures diff empty; `dotnet test FS.GG.SDD.sln` fully green including the new `AgentSurfaceDriftTests` (836/0/3), warning count == T001.
- [ ] T032 [P] Update `docs/reports/…` cross-links if needed and confirm `specs/068-architecture-longterm/` artifacts reference the delivered names (reconcile the candidate Parsing names in data-model.md/plan.md with the final chosen names from T023-T025).
- [ ] T033 Run `/speckit-analyze` for cross-artifact consistency, then prepare the PR (`Closes FS-GG/FS.GG.SDD#76`); flip board item #76 to *In review*.

---

## Dependencies & execution order

- **Phase 1 (Setup)** blocks everything (defines the gate).
- **Phases 2–6** are mutually independent after Phase 1 and may run in parallel /
  any order. Recommended priority order: US1 → US2 → US6 → US5 → US4 (per plan
  delivery order).
- **Phase 7 (US3 de-AutoOpen)** MUST run after Phases 2–6 (rebases over settled
  module names + new symbols).
- **Phase 8** runs after all stories.

### True cross-task dependencies

- T012 after T010; T013 after T011 (DU defined before use).
- T019 after T018 (tests follow the accessor change).
- T024 after T023, T025 after T024 (serialized `.fsproj` edits).
- T027→T028→T029 (de-AutoOpen sequence); whole phase after Phases 2–6.

### Parallel opportunities

- T002 ∥ T003 (Setup).
- T010 ∥ T011 (two DUs, different insertion points).
- Whole stories US1 ∥ US2 ∥ US6 ∥ US5 ∥ US4 if staffed — each is an independent slice.
- T018 ∥ T020 ∥ T021 within US5 (different files).

---

## Task counts

| Story | Tasks | Priority |
|---|---|---|
| Setup | T001–T003 (3) | — |
| US1 — readiness envelope | T004–T009 (6) | **P1 / MVP** |
| US2 — DU states | T010–T014 (5) | P2 |
| US6 — docs identity + guard | T015–T017 (3) | P2 |
| US5 — purity | T018–T022 (5) | P3 |
| US4 — Parsing renames | T023–T026 (4) | P3 |
| US3 — de-AutoOpen | T027–T029 (3) | P2 (last) |
| Polish | T030–T033 (4) | — |

**Total**: 33 tasks. **MVP scope**: Phase 1 + US1 (readiness envelope) — the P1
slice that removes the highest-leverage drift risk on its own.

**Suggested first increment**: US1, then US2 and US6 in parallel; save US3
(de-AutoOpen) for last per the plan's de-risking order.
