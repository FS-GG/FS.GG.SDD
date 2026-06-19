# Tasks: [FEATURE_NAME]

**Feature branch**: `[FEATURE_BRANCH]`
**Spec**: `specs/[FEATURE_ID]/spec.md`
**Plan**: `specs/[FEATURE_ID]/plan.md`

## Status Legend

- `[ ]` — pending
- `[X]` — done with real evidence (or with synthetic evidence disclosed per
  Principle V)
- `[-]` — skipped (with written rationale)

Never mark a failing task `[X]`; never weaken an assertion to green a build.

## Vertical-slice rule (US phases)

A task tagged `[US*]` may only be marked `[X]` when the change is reachable from a
user-facing entry point and that path was actually exercised — an FSI session
against the packed library, a smoke run of the application, a manual walk-through
with transcript, or a screenshot captured under `readiness/`. Domain, model, or
core-layer changes alone do **not** satisfy `[X]` for a `[US*]` task, even if
their unit tests pass green. If the user-reachable surface is missing, stubbed, or
not yet wired, mark `[ ]` (work continues). If the path can only be exercised with
synthetic evidence for now, keep it honest: disclose the synthetic evidence per
Principle V and open a tracking issue for the real wire-up.

For stateful or I/O-bearing stories, `[X]` also requires Elmish/MVU evidence:
the public `Model` / `Msg` / `Effect` or `Cmd<Msg>` contract was exercised,
pure `update` transitions were tested, emitted effects were asserted, and
the effect interpreter was run against real dependencies where safe.

This rule does not apply to Setup, Foundation, Integration, or Polish
phase tasks; those are evaluated against their own phase verification.

## Task Annotations

- **[P]** — parallel-safe (no dependency on another incomplete task in this phase)
- **[US1]**, **[US2]**, … — user-story scope
- **[T1]** / **[T2]** — Tier 1 (contracted) vs Tier 2 (internal) change

Phases run in sequence; tasks within a phase may run in parallel. When a task
depends on a non-obvious earlier task, note it in the task description (e.g.
"after T011"). There is no separate dependency graph to maintain.

---

## Phase 1: Setup

- [ ] T001 Scaffold the feature directory and link spec + plan
- [ ] T002 [P] Add baseline install or adoption documentation for the selected profile
- [ ] T003 [P] Add readiness artifact scaffolding (`specs/[FEATURE_ID]/readiness/`)
- [ ] T004 Record feature Tier, affected layer, public-API impact, Elmish/MVU applicability, and required evidence obligations

---

## Phase 2: Foundation

- [ ] T005 Draft the public surface as `.fsi` signature(s), including `Model`, `Msg`, `Effect` or `Cmd<Msg>`, `init`, `update`, and interpreter boundary for stateful or I/O-bearing features
- [ ] T006 [P] Add or update constitutional guidance that this feature touches
- [ ] T007 [P] Define or update operational workflows, commands, reports, or scripts
- [ ] T008 Exercise the draft `.fsi` from FSI (`scripts/prelude.fsx` or ad-hoc), including representative `init` / `update` paths when MVU applies, and capture the session transcript to `readiness/fsi-session.txt`
- [ ] T009 Record surface-area baselines for the new / changed public modules
- [ ] T010 Record unsupported-scope handling and failure diagnostics

**Checkpoint**: Foundation ready — story implementation may begin in parallel.

---

## Phase 3: User Story 1 (US1)

### Tests First (Principle I, Principle VI)

- [ ] T011 [P] [US1] Add semantic tests that load the packed library (or prelude), exercise the US1 surface, and assert MVU state transitions plus emitted effects when applicable
- [ ] T012 [P] [US1] Add verification for the US1 outcome against the readiness artifact, including real interpreter evidence for effects where safe

### Implementation

- [ ] T013 [P] [US1] Add story-specific contracts, docs, or fixtures
- [ ] T014 [P] [US1] Add any required sample or schema artifacts
- [ ] T015 [US1] Implement the primary user-facing behavior for the story, keeping MVU `update` pure when applicable
- [ ] T016 [US1] Connect the story's effect interpreter to canonical readiness artifacts or workflows
- [ ] T017 [US1] Add validation and actionable failure diagnostics
- [ ] T018 [US1] Document the story's independent validation path

**Checkpoint**: User Story 1 is fully functional and testable independently.

---

## Phase 4: User Story 2 (US2)

### Tests First

- [ ] T019 [P] [US2] Add semantic tests exercising the US2 surface through FSI, including MVU transitions and effects when applicable
- [ ] T020 [P] [US2] Add validation for the US2 readiness outcome, including real interpreter evidence where safe

### Implementation

- [ ] T021 [P] [US2] Add story-specific contracts, docs, or fixtures
- [ ] T022 [US2] Implement the primary user-facing behavior for the story

**Checkpoint**: User Story 2 is fully functional and testable independently.

---

## Phase 5: Integration & Polish

- [ ] T023 Surface-area baseline refresh (Tier 1 only)
- [ ] T024 Run the packed library through the numbered example scripts and confirm none are broken
- [ ] T025 Confirm every synthetic dependency is disclosed per Principle V (use-site comment, `Synthetic` test token, PR description entry)
