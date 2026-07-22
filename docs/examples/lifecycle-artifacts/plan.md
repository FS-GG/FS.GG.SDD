---
schemaVersion: 1
workId: 001-example
title: Example Work Item
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/001-example/spec.md
sourceClarifications: work/001-example/clarifications.md
sourceChecklist: work/001-example/checklist.md
publicOrToolFacingImpact: true
---

# Example Work Item Plan

<!--
A complete, valid `plan.md` you can copy-adapt. Validated on every build by the live
plan parser (ExampleArtifactsContractTests via Plan.parsePlanFacts). `status: planned`
and the Source Snapshot digests are written by `fsgg-sdd plan`; do not hand-edit them.
See the per-stage front-matter and stable-id grammars in
docs/reference/authoring-contracts.md and [[fs-gg-sdd-plan]].
-->

Prose status: planned

## Source Snapshot
- spec: work/001-example/spec.md sha256:780f0fe89e3ef54473c37ac69c3225ee4765474ad9991f641ecb8908a6924300 schemaVersion:1
- clarifications: work/001-example/clarifications.md sha256:c4a046356d2826d3b006ee63fe10207f715211d66681c08cfbb5cf642539fa53 schemaVersion:1
- checklist: work/001-example/checklist.md sha256:f2abd1d517f05641ae270ec9c9fce04054b7cea1a1cd1ca231c7ca1be3995f84 schemaVersion:1

## Plan Scope
- Work item 001-example is planned from the current specification, clarification, and checklist facts.
- Requirement count: 2.
- Clarification decision count: 1.
- Checklist result count: 2.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Serve direction is derived from the finished rally's loser at rally end and stored on the match, not recomputed at serve time — so a replayed match and a live one cannot disagree.
- PD-002 [AC-002] [FR-002] complete: The scoreboard accumulates points and exposes no terminal state, because the clarified rally is endless; a win condition would be a new requirement, not a tweak.
- PD-003 [DEC-002] acceptedDeferral: Accepted deferral DEC-002 remains visible to task generation.
- PD-004 [CR-003] acceptedDeferral: Accepted deferral CR-003 remains visible to task generation.

## Contract Impact
- PC-001 [PD-001] command report: `nextServer` is returned as a value rather than applied as a mutation, so a caller cannot desync the scoreboard from the serve.

## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: Win two consecutive rallies with the same player and assert the serve goes to the same opponent twice — the failure leg of the serve rule: the loser serves, it does not alternate.

## Migration Posture
- PM-001 [PC-001] diagnoseOnly: No persisted match state changes shape, so there is nothing to migrate; a stored match from before this change still loads.

## Generated View Impact
- GV-001 [PD-001] workModel: The work model gains no new node kinds — only PD-001's prose moves — so a stale view is a regenerate, not a migration.

## Accepted Deferrals
- DEC-002 acceptedDeferral: No win condition this cycle — carried into tasks and evidence so it is deferred in the open, not dropped.
- CR-003 acceptedDeferral: Scoring-limit review waits on the win-condition question; it stays visible so the next cycle inherits the question rather than rediscovering it.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 001-example`.
