# Tasks: The Visual-Inspection Obligation

**Input**: `specs/098-visual-inspection-obligation/{spec,plan}.md`

**Tier**: Tier 1 (additive schema field, new diagnostic id, agent-skill contract change)

**Tracks**: FS.GG.SDD#306

**Sequencing**: no `Blocked by`. Touch-set is disjoint from anything else in flight.

## Format

`[ID] [P?] [Story] Description` — `[X]` done, `[ ]` open, `[-]` dropped. `[P]` = parallelizable.

## Phase 1: Foundational — the declaration and the marker

- [X] T001 [US1] `ProjectLifecycleConfig.VisualSurface: bool`, read via `boolAt` so a non-boolean
  degrades to `false` (`src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fs` + `.fsi`) — FR-001
- [X] T002 [P] [US1] `Evidence.visualInspectionSkill` / `isVisualInspectionTagged` /
  `namesRenderedArtifact` in `Artifacts`, the one assembly both `Commands` modules see
  (`LifecycleArtifacts/Evidence.fs` + `.fsi`) — FR-004
- [X] T003 [P] `CommandReports.missingVisualInspectionArtifact` (`DiagnosticConstructors.fs`,
  `CommandReports.fs` + `.fsi`) — FR-004

## Phase 2: US1 — the obligation

### Tests first (red)

- [X] T004 `project.visualSurface` parses `true`/`True`/`false`/absent/non-boolean/blank
  (`tests/FS.GG.SDD.Artifacts.Tests/SchemaContractTests.fs`) — FR-001
- [X] T005 [P] `isVisualInspectionTagged` / `namesRenderedArtifact` unit coverage, including the
  `artifacts`-only, `sourceRefs`-only, and names-nothing cases
  (`tests/FS.GG.SDD.Artifacts.Tests/EvidenceArtifactTests.fs`) — FR-004
- [X] T006 [P] `tasks` derives / does not derive / re-derives idempotently / drops on withdrawal
  (`tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`) — FR-002, FR-003, SC-004
- [X] T007 [P] `evidence` blocks a pass with no artifact; accepts an `artifacts:` pass; accepts a
  `sourceRefs[]` pass; records a synthetic pass as unsatisfying-not-invalid; lets a deferral through
  (`tests/FS.GG.SDD.Commands.Tests/EvidenceCommandTests.fs`) — FR-004, FR-005, FR-006, SC-002, SC-003

### Implementation

- [X] T008 `derivedVisualSurface` + `visualInspectionTaskTitle` + the `visualInspectionTasks` family,
  emitted last with empty `sourceIds` (`CommandWorkflow/TaskGraphAuthoring.fs`) — FR-002, FR-003
- [X] T009 Thread the flag off the already-requested `.fsgg/project.yml` read at the `tasks` call
  site; no new effect (`TaskGraphAuthoring.fs`) — FR-009
- [X] T010 The artifact gate, twice: the pre-write `evidenceValidationDiagnostics` (recovering the
  obligation ids from tagged tasks) and the `evidenceDispositions` cascade (reading the tag off the
  obligation) (`CommandWorkflow/HandlersEvidence.fs`) — FR-004, FR-006
- [X] T011 Mirror the cascade in `verifyTestDispositionViews` so `TD-` and `ED-` agree
  (`CommandWorkflow/HandlersVerify.fs`) — FR-004

- [X] T011a Self-review finding: `--from-tests` seeded a proving-test path onto the visual
  obligation, pre-satisfying FR-004 with the wrong kind of proof. `skeletonEvidenceDeclaration` now
  leaves a tagged obligation unseeded, with a red-first test (`HandlersEvidence.fs`) — FR-004a
- [X] T011b Self-review finding: the artifact rule was spelled three times. Collapsed to one
  `Evidence.passesWithoutRenderedArtifact`, read by the gate, the `ED-` cascade, and the `TD-` mirror

## Phase 3: US2 — the checklist prompt

- [X] T012 `checklist prompts for between-requirement incoherence` / `derives no prompt when
  undeclared` / `re-derives idempotently`
  (`tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs`) — FR-007
- [X] T013 The advisory `incoherenceReviews` row over the whole requirement set, and the
  `visualSurface` read at the checklist handler (`CommandWorkflow/ChecklistPlanAuthoring.fs`) —
  FR-007, FR-009

## Phase 4: US3 — agent guidance

- [X] T014 `fs-gg-sdd-evidence` gains "The visual-inspection obligation": what derives it, the
  four-step render-and-look recipe in product-neutral terms, both enforced rules, and the
  defer-don't-synthesize instruction — FR-008
- [X] T015 Mirror to `.codex/skills/fs-gg-sdd-evidence/SKILL.md`; regenerate
  `.agents/skills/skill-manifest.json` (`registry skill-manifest --write`) and the
  `FullShapeGoldenTests` `command-report.json` golden, which pins the seeded body digest — FR-008
- [X] T016 `docs/reference/authoring-contracts.md` § "The visual-inspection obligation
  (`project.visualSurface`)", including the migration note

## Phase: Verification and polish

- [X] T017 Re-capture `PublicSurface.baseline` for `Artifacts` and `Commands` (three additive entries)
- [X] T018 Drive the **built CLI** end-to-end over a real `init`-seeded workspace: declare the flag,
  walk charter→verify, and exercise all four dispositions (block / synthetic / real / deferral).
  Confirm `tasks` reports `noChange` on the second run and `analyze` accepts the sourceId-less task.
- [X] T019 Mutation check: deleting the gate's emission turns
  `evidence blocks a visual-inspection pass that names no rendered artifact` red — the test is
  load-bearing, not incidental (Principle VI)
- [X] T020 SC-005 grep: no `game` / `sample-pack` / `SceneNode` / `Viewer` / `runAppEvidence` token
  in `src/` or in the seeded skill
- [X] T021 Full suite green (`scripts/test.sh`), Fantomas 7.0.5 clean on every changed `.fs`/`.fsi`

## Summary

- US1: 8 tasks (MVP 🎯 — the obligation is the feature)
- US2: 2 tasks
- US3: 3 tasks
- Foundational + polish: 8 tasks
- Parallel opportunities: T002/T003 with T001; T005/T006/T007 with each other.
- Blocked-by: none.
