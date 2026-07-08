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
- checklist: work/001-example/checklist.md sha256:7ccd0cebf69a72e5e7215d830908f7cb61c8fda6c62a20c57bb5794a026dca25 schemaVersion:1

## Plan Scope
- Work item 001-example is planned from the current specification, clarification, and checklist facts.
- Requirement count: 2.
- Clarification decision count: 1.
- Checklist result count: 2.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Plan requirement FR-001 (serve-to-loser) through the plan command contract.
- PD-002 [AC-002] [FR-002] complete: Plan requirement FR-002 (rally scoring, no match-end) through the plan command contract.

## Contract Impact
- PC-001 [PD-001] command report: fsgg-sdd plan, work/001-example/plan.md, and command-report JSON are tool-facing and compatibility-preserving.

## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: Run focused command tests, FSI/prelude evidence, and CLI smoke evidence before task generation.

## Migration Posture
- PM-001 [PC-001] diagnoseOnly: Plan schemaVersion 1 is accepted; unsupported plan schemas diagnose before write.

## Generated View Impact
- GV-001 [PD-001] workModel: readiness/001-example/work-model.json refreshes from current plan sources or reports staleGeneratedView.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 001-example`.
