---
schemaVersion: 1
workId: 3247-roadmap-acceptance-authoring
title: Roadmap Single-PR Acceptance
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/3247-roadmap-acceptance-authoring/spec.md
sourceClarifications: work/3247-roadmap-acceptance-authoring/clarifications.md
sourceChecklist: work/3247-roadmap-acceptance-authoring/checklist.md
publicOrToolFacingImpact: true
---

# Roadmap Single-PR Acceptance Plan

Prose status: planned

## Source Snapshot
- spec: work/3247-roadmap-acceptance-authoring/spec.md sha256:b97e3def7281adc5514f99802860d091c5b7f2f81425d5fa57e597aa9299a2ef schemaVersion:1
- clarifications: work/3247-roadmap-acceptance-authoring/clarifications.md sha256:49f042aff5dee70a7563a9d483ba7759788cd934c28118e5240148468f5d7ebe schemaVersion:1
- checklist: work/3247-roadmap-acceptance-authoring/checklist.md sha256:02d84f460385bce196dd880a31341d669e9184578ab20bd8b84488f3a38c3901 schemaVersion:1

## Plan Scope
- Work item 3247-roadmap-acceptance-authoring is planned from the current specification, clarification, and checklist facts.
- Requirement count: 5.
- Clarification decision count: 3.
- Checklist result count: 5.

## Plan Decisions
- PD-001 [AC-001] [FR-001] [FR-002] [DEC-001] complete: Model shared-role PR identity explicitly. Equal PR numbers require equal candidates and equal merges; distinct PR numbers retain the existing four-way collision guard. Candidate-to-merge collapse remains forbidden in either mode.
- PD-002 [AC-002] [FR-003] [DEC-002] complete: Resolve both implementation and acceptance repository authority from the applied unit registration, which already binds the unit issue, owner, repository, and lifecycle subject.
- PD-003 [AC-003] [FR-004] [DEC-003] complete: Accept either the literal roadmap unit cycle or a non-empty critique cycle ending in the routed SDD work suffix. Preserve ordinary exact-head validation for literal cycles. For routed work cycles, query the live comparison between the reviewed commit and final candidate, require `ahead` with `ahead_by = 1`, the reviewed commit as exact merge base, and the canonical critique JSON as the sole file with status `modified`; refuse every other relationship.
- PD-004 [AC-004] [FR-005] complete: Exercise exact candidate `8b19392392bc9071bf68335cef8b98ad64304787` against the immutable #304/#305 identities and require the production route to clear all identity and critique refusals before it reaches intentionally omitted operation-output controls.
- PD-005 [AC-001] [AC-002] [AC-003] complete: Retain live pull-request, tree equality, protected-main, structured-review, lifecycle, qualification, and independent SDD observer checks unchanged.

## Contract Impact
- PC-001 [PD-001] [PD-002] [PD-003] command semantics: `roadmap unit accept seal` keeps the existing input schema and command vocabulary while correcting the relationship among existing identity fields.

## Verification Obligations
- VO-001 [PD-001] [PD-002] [PD-003] [PD-004] [PD-005] semanticTest: Run exact candidate Core 969/969 and BoardOps 276/276 suites and the immutable production reproduction; require all identity/critique findings to clear and every inversion to remain red.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] compatibilityPatch: No schema migration is required; already-recorded single-PR unit facts become sealable, while divergent shared identities and unrelated critique cycles remain invalid.

## Generated View Impact
- GV-001 [PD-004] readiness: Generate analyze, work-model, verify, and ship artifacts in FS.GG.SDD for the exact external engine candidate and its durable issue evidence.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- The SDD receiver records the external implementation evidence; it does not own coordination-engine source.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 3247-roadmap-acceptance-authoring`.
