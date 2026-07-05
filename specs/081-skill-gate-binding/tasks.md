---
description: "Task breakdown for 081-skill-gate-binding"
---

# Tasks: Bind SDD authoring skills to the CLI gate grammar

**Input**: `specs/081-skill-gate-binding/` — plan.md, spec.md, research.md, data-model.md, contracts/

**Tier**: Tier 1 (agent-skill contract + command-output diagnostic identifier + public surface). Tests are mandatory (Principle VI).

**Legend**: `[ ]` pending · `[X]` done w/ real evidence · `[-]` skipped w/ rationale · `[P]` parallel-safe (no dep on another incomplete task in the phase) · `[US#]` user story.

**Order rule**: phases run in sequence; tasks within a phase may run in parallel where marked `[P]`. Within a change, follow Spec→FSI→Tests→Impl (Principle I). This feature has no new stateful/I-O MVU workflow — the doctest drives the *existing* command MVU loop via `TestSupport`, so no new `Model`/`Msg`/`Effect` tasks (Principle V n/a; noted in T004).

---

## Phase 1: Setup & baseline (Shared)

**Purpose**: Establish ground truth so later phases assert against reality, not assumptions.

- [ ] T001 [P] Record current corpus gate state: for each file in `docs/examples/lifecycle-artifacts/`, run it through its real gate via a scratch xUnit or `fsgg-sdd` invocation and note which (if any) block today. Capture findings in a short comment block at the top of the new `tests/FS.GG.SDD.Commands.Tests/SkillGateDoctestTests.fs` (created empty here).
- [ ] T002 [P] Confirm which child-issue symptoms still reproduce on `main` (specify bold FR, evidence deferral fields, clarify `sourceSpec`, checklist back-ref label) — one-line status each in `specs/081-skill-gate-binding/research.md` "Resolved unknowns" if any differ from the recorded state.
- [ ] T003 [P] Inventory every stage skill's fenced blocks and decide the single runnable example per skill to mark; list `(skill → corpus file, mode)` as a comment in `SkillGateDoctestTests.fs`.
- [ ] T004 Note in this file: Principle V (MVU) is not applicable — no new I/O workflow; the doctest reuses `TestSupport.runRequest`. (Documentation task, no code.)

---

## Phase 2 — [US1]+[US2] (P1, MVP): the durable skill↔gate binding

**Goal**: A gate-passing corpus, run through the *real* gates, with each stage skill's example bound to it — so drift cannot land. Delivers the MVP on its own.

### Corpus (the doctest's input of record)

- [ ] T010 [US1] Repair `docs/examples/lifecycle-artifacts/spec.md` to the gate-accepted coverage form — every FR as a single physical, non-bold line `- FR-###: … (covers AC-###)` — and ensure FR/AC/US ids are coherent for the corpus workId.
- [ ] T011 [US1] Ensure `docs/examples/lifecycle-artifacts/checklist.md` coverage + `[CHK:CHK-###]` back-refs are coherent with the repaired spec (no missing back-refs, no uncovered FRs).
- [ ] T012 [US1] Add at least one `result: deferred` (`kind: deferral`) declaration to `docs/examples/lifecycle-artifacts/evidence.yml` carrying all four `requiredDeferralKeys` (`rationale`, `owner`, `scope`, `laterLifecycleVisibility`), keeping the existing satisfying `result: pass` entries. (Feeds T033/#142.)
- [ ] T013 [US1] Verify the whole corpus (`charter/spec/clarifications/checklist/plan/tasks/evidence`) is internally coherent (shared workId, cross-artifact refs resolve).

### Example-marker contract + doctest

- [ ] T014 [P] [US2] Freeze the marker grammar in `contracts/example-marker.md` as implemented (already drafted) — no code, confirm the extractor design matches it.
- [ ] T015 [US2] Implement the marked-example extractor in `tests/FS.GG.SDD.Commands.Tests/SkillGateDoctestTests.fs`: read each `.claude/skills/fs-gg-sdd-<stage>/SKILL.md`, collect fenced blocks preceded by `<!-- fsgg-sdd:example … -->`, parse `corpus`/`mode`/`counter`.
- [ ] T016 [US1] Implement the gate-run assertion in `SkillGateDoctestTests.fs`: seed a temp project via `TestSupport`, write the corpus files into it, run each stage gate (`runSpecify`/`runClarify`/`runChecklist`/`runEvidence`/…), assert `report.Outcome` is not `Blocked` and no `DiagnosticError` diagnostics. **This test must be RED until T010–T013 land, then GREEN.**
- [ ] T017 [US2] Implement the skill↔corpus consistency assertion in `SkillGateDoctestTests.fs`: for each non-`counter` marked block, assert `mode` (`contains`/`equals`, normalized per contract) against `docs/examples/lifecycle-artifacts/<corpus>`.
- [ ] T018 [US2] Implement the coverage assertion: every stage skill documenting a gated artifact contributes ≥1 non-`counter` marked block; fail naming any skill that does not (FR-004).
- [ ] T019 [US2] Implement counter-example handling: assert each `counter`-marked block, run through its stage gate, *does* block.

### Skill edits (add markers + fix the P1-visible symptoms) — canonical `.claude`, mirrored `.codex`

- [ ] T020 [US1] Correct the `fs-gg-sdd-specify` FR example to the non-bold `- FR-###: … (covers AC-###)` form and add its `<!-- fsgg-sdd:example corpus=spec.md mode=contains -->` marker (#141). Edit `.claude/skills/fs-gg-sdd-specify/SKILL.md`.
- [ ] T021 [P] [US2] Add example markers to the remaining stage skills' runnable example(s), each pointing at its corpus file: `charter/clarify/checklist/plan/tasks/analyze/evidence/verify/ship`. Edit each `.claude/skills/fs-gg-sdd-<stage>/SKILL.md`.
- [ ] T022 [US1] Mirror every Phase-2 `.claude/skills/*` edit byte-identically into `.codex/skills/*` (keeps `SkillMirrorTests` green). Do NOT hand-edit `.agents` (seed-time only).

**Checkpoint (MVP)**: `SkillGateDoctestTests` green; reverting any skill example to a bad form or breaking the corpus turns it red naming the skill/diagnostic (SC-003). US1+US2 shippable here.

---

## Phase 3 — [US3] (P2): field-list truth against the typed contract

**Goal**: Required-field statements in skills are checked against a single typed registry.

- [ ] T030 [US3] Add the required-keys registry signature to `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/RequiredKeys.fsi`: `requiredFrontMatterKeys : LifecycleStage -> string list` and `requiredDeferralKeys : string list`.
- [ ] T031 [US3] Implement `RequiredKeys.fs` with values sourced from the per-parser tuple matches (e.g. clarify `[schemaVersion; workId; stage; sourceSpec]` per `Clarification.fs:121-122`; deferral `[rationale; owner; scope; laterLifecycleVisibility]`). Add both to `FS.GG.SDD.Artifacts.fsproj` in dependency order.
- [ ] T032 [US3] [P] Behavioral parser tests in `tests/FS.GG.SDD.Artifacts.Tests/` proving the gate actually requires each registry key: omit each `requiredFrontMatterKeys` key → parser blocks; omit each `requiredDeferralKeys` key on a deferral → `missingDeferralRationale`. (Keeps the registry honest vs the real gate.)
- [ ] T033 [US3] Document the four deferral fields in `.claude/skills/fs-gg-sdd-evidence/SKILL.md` (a machine-readable field-list region) and confirm the deferral example (T012) is referenced (#142); mirror to `.codex`.
- [ ] T034 [US3] Confirm `fs-gg-sdd-clarify` names `sourceSpec` and every `requiredFrontMatterKeys Clarify` key in a machine-readable region (#143 — likely already present; add region markers if needed); mirror to `.codex`.
- [ ] T035 [US3] Reconcile the `fs-gg-sdd-authoring-contracts` §5 required-fields table (SKILL.md ~lines 113-119) with the registry; mirror to `.codex`.
- [ ] T036 [US3] Implement `tests/FS.GG.SDD.Commands.Tests/RequiredFieldContractTests.fs`: assert set-equality between the registry and (a) each stage skill's field-list region and (b) the §5 table; fail naming the field + surface on any asymmetric difference (FR-009).

**Checkpoint**: adding a required key to the registry without updating a skill/§5 table turns `RequiredFieldContractTests` red naming the field + surface.

---

## Phase 4 — [US4] (P2): split the mislabeled checklist back-reference diagnostic

**Goal**: The missing-`[CHK:CHK-###]` case emits `missingChecklistBackReference`, not `malformedChecklistFrontMatter`. Follow FSI→Tests→Impl.

- [ ] T040 [US4] Add the public `val missingChecklistBackReference` signature to `src/FS.GG.SDD.Commands/CommandReports.fsi` (near line 54, sorted).
- [ ] T041 [US4] [P] Failing test in `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs`: a `CR-###` review line missing `[CHK:CHK-###]` → `report.Diagnostics` contains `missingChecklistBackReference` and NOT `malformedChecklistFrontMatter`.
- [ ] T042 [US4] [P] Failing test in `tests/FS.GG.SDD.Artifacts.Tests/`: `Checklist.checklistReferenceDiagnostics` emits the new id (not `workModelInconsistent`) for a result with `None` item ref.
- [ ] T043 [US4] [P] Retained-behavior test: a genuinely malformed front-matter checklist still emits `malformedChecklistFrontMatter`.
- [ ] T044 [US4] Emit the new diagnostic id at the source: `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Checklist.fs` `checklistReferenceDiagnostics` (~line 244) — replace the `workModelInconsistent` for the `None` item-ref branch with a `missingChecklistBackReference` diagnostic (add helper in the Artifacts `Diagnostics` module + `.fsi` if needed).
- [ ] T045 [US4] Add the `missingChecklistBackReference` constructor in `src/FS.GG.SDD.Commands/CommandReports/DiagnosticConstructors.fs` (near line 319) and register it in the checklist stage-classifier lists (~lines 1033, 1068).
- [ ] T046 [US4] Add the re-export wrapper in `src/FS.GG.SDD.Commands/CommandReports.fs` (near lines 118-119).
- [ ] T047 [US4] Update the remap in `src/FS.GG.SDD.Commands/CommandWorkflow/ChecklistPlanAuthoring.fs` (line 79) so the new source id surfaces as `missingChecklistBackReference`; the `workModelInconsistent` case now maps only genuine front-matter mismatches.
- [ ] T048 [US4] Add the view category case in `src/FS.GG.SDD.Commands/CommandWorkflow/ViewGeneration.fs` (~line 297) for the new id (not the front-matter category).
- [ ] T049 [US4] Add a back-reference remediation pointer in `src/FS.GG.SDD.Commands/CommandReports/RemediationPointers.fs` (near line 75) pointing at the `- CR-### [CHK:CHK-###] …` grammar; ensure `RemediationPointersTests` (every id has a pointer) passes.
- [ ] T050 [US4] Update `fs-gg-sdd-troubleshooting` (and any remediation-pointer prose) to name `missingChecklistBackReference` for the missing-back-ref cause (FR-011); mirror to `.codex`.
- [ ] T051 [US4] Add the sorted symbol `FS.GG.SDD.Commands.CommandReports.missingChecklistBackReference` to `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` and `tests/FS.GG.SDD.Cli.Tests/PublicSurface.baseline`.

**Checkpoint**: T041–T043 green; back-ref names its real cause; front-matter cases unchanged.

---

## Phase 5: Guards, docs, and cross-cutting closeout

- [ ] T060 Regenerate the process skill manifest: `fsgg-sdd registry skill-manifest --write` (or `--check` in CI) so `.agents/skills/skill-manifest.json` matches the edited body sha256s; confirm `ProcessSkillManifestTests`/`SeededSkillsTests` green.
- [ ] T061 [P] Verify `.claude`≡`.codex` for every edited skill (`SkillMirrorTests`) and re-run `SeededSkillsTests`.
- [ ] T062 [P] Verify `.fsgg/early-stage-guidance.md` currency vs the changed specify/clarify/checklist contracts; update if its drift guard requires (Decision 6).
- [ ] T063 [P] Update user-facing docs referencing the diagnostic or evidence deferral fields (e.g. `docs/reference/*`, `fs-gg-sdd-authoring-contracts` cross-links) for consistency.
- [ ] T064 Run `fsgg-sdd analyze` for this work item (cross-artifact readiness) and the full suite `dotnet test FS.GG.SDD.sln`; confirm green. Walk `quickstart.md` steps 1–5 incl. the SC-003 red-branch demonstration.
- [ ] T065 Close child issues #141, #142, #143, #144 with references to the durable checks (doctest / field-check / diagnostic split), not just the edits (SC-005). Epic #140 rolls up via the board.

---

## Dependencies

- Phase 1 → all. Phase 2 corpus (T010–T013) blocks the T016 gate assertion. Phase 2 is the MVP and should land before Phases 3–4 so their skill/corpus edits are caught by the doctest.
- Phase 3: T030→T031→(T032,T036); T033–T035 depend on T031 (registry values) and feed T036.
- Phase 4: T040 before T044–T047 (FSI first); tests T041–T043 authored before impl T044–T048; T045/T046 before T047; all before T051 baseline.
- Phase 5: T060/T061 after any skill edit (Phases 2–4); T064/T065 last.

## Summary

- **US1** (P1): T001–T004(setup), T010–T013, T016, T020, T022 — ~8 core tasks. **MVP.**
- **US2** (P1): T003, T014–T019, T021, T022 — doctest mechanism + markers.
- **US3** (P2): T030–T036 — 7 tasks.
- **US4** (P2): T040–T051 — 12 tasks.
- **Cross-cutting**: T060–T065.
- **Parallel opportunities**: T001–T003; T032/T036 gated on registry; T041–T043 (independent test files); T060–T063; most skill-marker edits (T021) are per-file parallel.
- **Suggested MVP scope**: **Phase 2 (US1+US2)** — the corpus + doctest + markers. It alone makes drift unable to land and delivers the epic's core acceptance; US3 and US4 are independent P2 follow-ons.
