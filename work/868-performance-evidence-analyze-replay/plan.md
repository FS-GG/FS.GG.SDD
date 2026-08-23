---
schemaVersion: 1
workId: 868-performance-evidence-analyze-replay
title: Keep active performance evidence in post-evidence analyze replay
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/868-performance-evidence-analyze-replay/spec.md
sourceClarifications: work/868-performance-evidence-analyze-replay/clarifications.md
sourceChecklist: work/868-performance-evidence-analyze-replay/checklist.md
publicOrToolFacingImpact: true
---

# Keep active performance evidence in post-evidence analyze replay Plan

Prose status: planned

## Source Snapshot
- spec: work/868-performance-evidence-analyze-replay/spec.md sha256:a927cc013c794d54a114ee8578a5a706d6456bf1bff564832daa435b98eb22d2 schemaVersion:1
- clarifications: work/868-performance-evidence-analyze-replay/clarifications.md sha256:d2765337101bdfb2d1a269fb344045e61d5080b9b42873319bb5990e6452fb9a schemaVersion:1
- checklist: work/868-performance-evidence-analyze-replay/checklist.md sha256:611230416b2ce747b7598fca0c3d91bcef91697cc69c1cdf8ed34ccf5ec53c35 schemaVersion:1

## Plan Scope
- Work item 868-performance-evidence-analyze-replay is planned from the current specification, clarification, and checklist facts.
- Requirement count: 1.
- Clarification decision count: 0.
- Checklist result count: 1.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Pass the recovered ordinary-evidence snapshot, rather than the original absent input, to `performanceEvidenceSnapshots` whenever generated views are rebuilt after evidence exists. Preserve the existing source/artifact/measurement set through direct `analyze`, `verify`, and `ship` replay.

## Contract Impact
- PC-001 [PD-001] command report: The unwrapped CLI lifecycle contract changes only by retaining already-authored active performance evidence; no command syntax, public F# signature, or artifact schema changes.

## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: Add a ship-ready command fixture with a typed performance intent, normal and stress workload ids, a valid `performance-evidence-v1` artifact, and observed-run evidence. Run `analyze`, `verify`, and `ship` twice and compare every tracked readiness byte after each command; mutate the recovered-performance fallback to `None` and observe the historical rewrite fail the regression.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] compatibility: Existing work items recover their already-authored active performance evidence on their next lifecycle replay. No migration, schema bump, or destructive rewrite is required.

## Generated View Impact
- GV-001 [PD-001] workModel: `work-model.json` and the generated lifecycle views retain the full performance evidence source set after ordinary evidence is recovered, so their digests stay stable across post-evidence replay.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 868-performance-evidence-analyze-replay`.
