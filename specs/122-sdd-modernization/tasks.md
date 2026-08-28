# Tasks: Risk-Scaled SDD

**Feature branch**: `item/937-sdd-modernization`
**Spec**: `specs/122-sdd-modernization/spec.md`
**Plan**: `specs/122-sdd-modernization/plan.md`

## Status Legend

- `[ ]` pending
- `[X]` done with exercised production behavior
- `[-]` skipped with rationale

## Phase 1: Decision package

- [X] T001 Record measured lifecycle and CI baseline in `specs/122-sdd-modernization/research.md`
- [X] T002 Define risk, evidence-confidence, compatibility, and critic boundaries in `specs/122-sdd-modernization/contracts/`
- [X] T003 Update both managed agent context pointers in `AGENTS.md` and `CLAUDE.md`

## Phase 2: Doctrine and guidance

- [X] T004 [P] [US1] Amend `.specify/memory/constitution.md` so lifecycle artifacts and verification scale with risk
- [X] T005 [P] [US1] Modernize the preset task/implementation doctrine under `.specify/presets/fsharp-opinionated/`
- [X] T006 [US1] Update SDD lifecycle skills in `.claude/skills/fs-gg-sdd-*` and their mirrored Codex projections so concise candidate-bound evidence replaces synthetic-disclosure ceremony
- [X] T007 [US1] Document small, normal, and high workflows plus migration in `docs/sdd-modernization.md`

## Phase 3: Evidence semantics

- [X] T008 [P] [US2] Add semantic tests in `tests/FS.GG.SDD.Commands.Tests/` proving observed synthetic-marked passes satisfy and synthetic metadata remains visible
- [X] T009 [P] [US2] Retain negative tests for unobserved, stale, malformed, failed, and wrong-kind receipts in `tests/FS.GG.SDD.Commands.Tests/`
- [X] T010 [US2] Change shared evidence predicates in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Evidence.fs` so provenance does not override observed outcomes
- [X] T011 [US2] Update verify disposition logic and reports in `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersVerify.fs` while preserving compatibility fields
- [X] T012 [US2] Exercise the public CLI from a real runner report and record the result under `readiness/937-sdd-modernization/`

## Phase 4: Risk-selected CI

- [X] T013 [P] [US3] Add mutation-style small/normal/high/indeterminate classifier tests at `scripts/tests/ci-risk.test.sh`
- [X] T014 [US3] Implement the dependency-free classifier at `scripts/ci-risk`
- [X] T015 [US3] Integrate classification into `.github/workflows/gate.yml` without changing required job names
- [X] T016 [US3] Update `scripts/test.sh` and `DEVELOPING.md` so normal CI uses the fast tier and high-risk CI uses full plus protected controls

## Phase 5: Acceptance and migration

- [X] T017 Run classifier tests, fast suite, full high-risk suite, surface checks, and exact public CLI evidence against one candidate
- [X] T018 Confirm old work packages still parse without rewrite and document compatibility evidence
- [ ] T019 Obtain an independent critic verdict bound to the exact candidate and repair all substantive findings
- [ ] T020 Merge only after required checks settle green; verify exact main and complete FS.GG.SDD#937

## Dependencies

- T004–T007 establish the new doctrine before generated guidance is accepted.
- T008–T009 precede T010–T011.
- T013 precedes T014–T015.
- T017–T020 require all implementation tasks.

## MVP

US1 + US2 is the minimum coherent product improvement. US3 is included in this feature because the user explicitly requested CI modernization and the measured cost is a primary motivation.
