---
schemaVersion: 1
workId: 869-refresh-evidence-deadlock
title: Refresh Evidence Deadlock
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Refresh Evidence Deadlock Specification

Prose status: specified

## User Value

An author who adds a requirement to a work package that already has an `evidence.yml` can bring
that package back to currency through the documented commands alone.

Today they cannot. Adding one requirement mints one new evidence obligation, and the package enters
a state no documented command can leave:

```
fsgg-sdd refresh  --work <id>   -> blocked   (refresh.malformedSource, 4x refresh.blockedUpstreamView)
fsgg-sdd analyze  --work <id>   -> needsGeneratedViewRefresh (staleSourceCount 1, generatedViewFindingCount 1)
fsgg-sdd evidence --work <id>   -> blocked   (evidence.analysisNotReady)
```

The cycle is closed and total. `refresh` cannot derive `work-model.json` because `tasks.yml`
requires an evidence id that `evidence.yml` does not declare; `analyze` cannot reach
`implementationReady` while that view is blocked and stale; and `evidence` — the only command that
writes an evidence declaration — refuses while `analyze` is not `implementationReady`.

The refusal is the more galling for being informed: in the same report in which it refuses,
`evidence` already names the exact declaration that is missing (`evidence.missingRequiredEvidence`,
relatedIds `[EV031]`). The tool computes the fact the author needs, and then declines to act on it.

The only escapes are undocumented and destructive: hand-seed a `result: missing` declaration into
`evidence.yml`, or delete `evidence.yml` outright — after which the lifecycle proceeds normally,
because an ABSENT `evidence.yml` is already carved out and a merely INCOMPLETE one is not. That
asymmetry is the defect: the state with thirty of thirty-one declarations is treated worse than the
state with none.

## Scope

- SB-001: The work model's reference diagnostics for the `tasks.yml` -> `evidence.yml` edge — the
  one reference in `tasks.yml` that points at an artifact a LATER lifecycle stage authors.
- SB-002: `refresh`'s attribution of a blocked work model to a declared source, which is today a
  hard-coded placeholder rather than the source at fault.
- SB-003: The authoring documentation and diagnostic corrections that describe both, so the tool
  says the authoring step to the author rather than leaving them to invent it.

## Non-Goals

- SB-004: Do not weaken any gate that decides whether an obligation was actually discharged.
  `evidence`, `verify` and `ship` must still refuse an obligation that is never declared.
- SB-005: Do not change the three reference edges in `tasks.yml` that point UPSTREAM — to
  requirements, decisions and other tasks. Those name artifacts that already exist, and an
  unresolved reference there is a genuine inconsistency with no later stage to close it.
- SB-006: Do not introduce a persisted schema major, and do not change any committed package's
  current verdict.
- SB-007: Do not add a new flag, mode or opt-out. The remedy is the documented command sequence.

## User Stories

- US-001 (P1): As a lifecycle author who has added a requirement to a package that already has `evidence.yml`, I can run the documented commands in the documented order and reach `implementationReady`, so a routine amendment is not a dead end.
- US-002 (P2): As an author reading a blocked `refresh`, I am told which declared source is actually at fault, so I do not re-lint a clean artifact.
- US-003 (P3): As a consumer of the lifecycle in another repository, I keep every guarantee about undischarged obligations that I have today, so nothing I depend on is traded away for the fix.

## Acceptance Scenarios

- AC-001 [US-001] [FR-001]: Given a green package whose `tasks.yml` requires an evidence id `evidence.yml` does not declare, when the work model is derived, then the derivation succeeds and reports a non-blocking diagnostic naming `evidence.yml` and the undeclared id.
- AC-002 [US-003] [FR-002]: Given a `tasks.yml` reference to a requirement, decision or task id that does not resolve, when the work model is derived, then the derivation still blocks with `unknownReference` exactly as it does today.
- AC-003 [US-003] [FR-003]: Given an evidence obligation that is never declared, and a CONTROL fixture identical but for that one fact whose `evidence`, `verify` and `ship` are all green, then `evidence` seeds the obligation and names it, `verify` refuses and names it, and `ship` refuses behind the verification view — so no enforcement is lost by AC-001.
- AC-004 [US-002] [FR-004]: Given a work model that cannot be derived, when `refresh` reports it, then `refresh.malformedSource` names the source the blocking diagnostics actually name, and never a hard-coded `spec.md`.
- AC-005 [US-002] [FR-005]: Given a blocked work model whose blocking diagnostics name no source at all, when `refresh` reports it, then it says it could not identify one rather than accusing an arbitrary artifact.
- AC-006 [US-001] [FR-006]: Given a package brought to `shipReady` and then given one added requirement, when the documented sequence is run, then it reaches `implementationReady` and `evidence` scaffolds the new declaration, with no hand-editing and no deletion of `evidence.yml`.
- AC-007 [US-001] [FR-007]: Given the pre-change code, when the AC-006 fixture runs against it, then it fails at the deadlock, so the fixture is shown to test the fix rather than to pass either way.
- AC-008 [US-001] [FR-008]: Given the authoring documentation, when an author consults it for what to do after adding a requirement, then it states the step and the diagnostics point at it.
- AC-009 [US-001] [FR-009]: Given an already-authored `evidence.yml` that declares nothing for a newly minted obligation, when `evidence` runs, then it seeds a `result: missing` declaration for that obligation, names the ids it seeded, and modifies no authored declaration.

## Functional Requirements

- FR-001: A `tasks.yml` task requiring an evidence id that `evidence.yml` does not declare produces one non-blocking diagnostic that names `evidence.yml` as the artifact to change and carries the undeclared id, instead of the blocking `unknownReference` and `workModelInconsistent` pair it produces today. (Stories: US-001; Acceptance: AC-001)
- FR-002: The task-to-requirement, task-to-decision and task-to-dependency reference edges keep emitting blocking `unknownReference` and `workModelInconsistent`, unchanged in id, severity and message. (Stories: US-003; Acceptance: AC-002)
- FR-003: An evidence obligation that carries no declaration and no accepted deferral is still refused downstream by id — `verify` blocks naming it and `ship` blocks behind the verification view it did not produce — while `evidence` seeds it rather than refusing, and that seeding cannot satisfy it. The gate carries a control leg, because a fixture that blocks for an unrelated reason blocks identically. (Stories: US-003; Acceptance: AC-003)
- FR-004: `refresh.malformedSource` names a source recovered from the blocking work-model diagnostics rather than a hard-coded argument, so the artifact it accuses is one those diagnostics actually name. (Stories: US-002; Acceptance: AC-004)
- FR-005: Where the blocking work-model diagnostics name no declared source, `refresh` reports that it could not attribute the blockage instead of naming an arbitrary artifact. (Stories: US-002; Acceptance: AC-005)
- FR-006: A package that gains a requirement after `evidence.yml` exists returns to `implementationReady` and through `evidence` using only the documented commands, with no hand-editing of `evidence.yml` and no deletion of an authored artifact. (Stories: US-001; Acceptance: AC-006)
- FR-007: The FR-006 fixture is shown to fail against the pre-change behaviour, so it is evidence of the fix rather than a test that passes either way. (Stories: US-001; Acceptance: AC-007)
- FR-008: The authoring documentation records the post-amendment step, and the new diagnostic's correction points the author at it. (Stories: US-001; Acceptance: AC-008)
- FR-009: `evidence` seeds a `result: missing` skeleton into an already-authored `evidence.yml` for every obligation it declares nothing for, reports which ids it seeded, and rewrites no authored declaration. (Stories: US-001; Acceptance: AC-009)

## Ambiguities

- AMB-001: The reported defect was filed as admitting three MUTUALLY EXCLUSIVE outcomes. Either
  `evidence` may scaffold a missing declaration while analysis is blocked on that very fact, or the
  work model may be derived from an `evidence.yml` that is merely incomplete, or neither command
  changes and the deadlock becomes a documented authoring step. Whether they are in fact exclusive
  is itself the question. Decided at clarify.
- AMB-002: If the work model may be derived from an incomplete `evidence.yml`, the demoted
  diagnostic could keep its existing id at a lower severity or take a new id of its own. Decided at
  clarify.
- AMB-003: `refresh` can recover the at-fault source only if the generator surfaces it. Whether to
  widen the generator's return, or to smuggle the paths through an existing diagnostic's
  `relatedIds`, is undecided. Decided at clarify.

## Public Or Tool-Facing Impact

- This specification is an SDD lifecycle artifact and command-report contract input.
- `FS.GG.SDD.Artifacts` gains one public diagnostic constructor. This is an additive public-surface
  change with committed `.fsi` and reflection baselines, and it is the reason this item is routed
  `sdd-required` rather than `lightweight`.
- The `fsgg-sdd refresh`, `analyze`, `evidence`, `verify` and `ship` commands are the lifecycle that
  every `sdd-required` item in every FS-GG repository runs. A change to when `refresh` may derive
  changes what an unrelated worker in another repository observes at its `analyze` gate, which is
  why SB-004 forbids trading any enforcement away for the fix.
- `readiness/<id>/work-model.json` gains the possibility of a warning-severity entry in its existing
  `diagnostics` array. The array already exists and already carries diagnostics; no schema major.

## Lifecycle Notes

- Next lifecycle action: `fsgg-sdd clarify --work 869-refresh-evidence-deadlock`.
