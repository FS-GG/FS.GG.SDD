# Tasks: An Unauthored Decision Is a Missing Decision

**Input**: `specs/100-unauthored-scaffold-content/{spec,plan}.md`

**Tier**: Tier 1 (new blocking diagnostic id `unauthoredScaffoldContent`; new blocking behaviour in
the `analyze` command contract)

**Tracks**: FS.GG.SDD#351

**Sequencing**: no `Blocked by`. Touch-set declared on the item and widened once, mid-flight, onto
`VerifyCommandTests.fs` (DISJOINT — no collision with the one other in-flight claim).

## Format

`[ID] [P?] [Story] Description` — `[X]` done, `[ ]` open, `[-]` dropped. `[P]` = parallelizable.

## Phase 1: Foundational — the rule, stated once, beside the generator

- [X] T001 [US1] `ChecklistPlanAuthoring.unauthoredPlanLines` — re-derive `plannedPlanEntries` from a
  blank slate and return the ids of every entry whose refs-and-prose the plan still carries verbatim.
  Compares the line **minus its id** (plan numbers incrementally; we derive from zero — see plan.md
  "Why the comparison ignores the entry id") — FR-001, FR-004
- [X] T002 [P] `CommandReports.unauthoredScaffoldContent` — `errorDiagnostic`, id
  `unauthoredScaffoldContent`, naming each unauthored entry
  (`CommandReports/DiagnosticConstructors.fs`) — FR-001, Constitution VIII

## Phase 2: US1 — `analyze` refuses (the failing test comes first)

- [X] T003 [US1] `analyze blocks a plan that still holds the prose the scaffold wrote` — asserts the
  **diagnostic id**, not an exit code (#266), and that every scaffolded family is named
  (`PD`/`PC`/`VO`/`PM`/`GV`) — SC-001, FR-001
- [X] T004 [US1] Wire the detector into `HandlersAnalyze` so one `DiagnosticError` suppresses
  `analysis.json` — FR-003
- [X] T005 [US1] `a lifecycle on untouched scaffold cannot reach ship` — no `analysis.json`,
  `evidence` blocked, `ship` blocked. This is the acceptance criterion in full — SC-001, FR-003

## Phase 3: US2 — the green path is unaffected

- [X] T006 [US2] `authoring the plan prose clears the block` — and, since the id/refs/kind token are
  untouched, it also proves the gate keys on the *prose*, not the machine contract — FR-002
- [X] T007 [US2] `a partially authored plan blocks only on the entries still untouched` — the
  conservatism, asserted — FR-004
- [X] T008 [US2] `a renumbered entry is still scaffold — the gate does not key on the id` — the
  fail-open leg from plan.md: same prose, different id, still blocked — FR-004

## Phase 4: Fixtures — the behaviour change, absorbed honestly

- [X] T009 `TestSupport.authorPlanProse` — keep the id, refs, and kind token; replace only the prose.
  Chained from every `initialize*Project`, which is why ~169 tests went green from one edit
- [X] T010 `ValidationRunner.authorPlanProse` — the harness drives the real lifecycle, so without
  this it **skips** (not fails) the six analyzed-or-later cells: silent coverage loss
- [X] T011 The four hand-rolled lifecycles that cannot use the shared helper —
  `LifecycleSmokeTests` (×2), `TasksCommandTests` (post-`acceptUpstream` re-plan),
  `EvidenceCommandTests`, `SkillGateDoctestTests`
- [X] T012 `VerifyCommandTests` — the fifth hand-rolled lifecycle (it must declare the test framework
  before `charter`, so it cannot route through `initializePlanReadyProject`). Missed by the first
  sweep; it died on `KeyNotFoundException` rather than an assertion
- [X] T013 The shipped example (`docs/examples/lifecycle-artifacts/plan.md`) now carries genuinely
  authored prose — it must, to pass its own gate — and the goldens re-digest accordingly
