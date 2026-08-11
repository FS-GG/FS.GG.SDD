---
schemaVersion: 1
workId: 857-post-evidence-analyze-replay
title: Post Evidence Analyze Replay
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/857-post-evidence-analyze-replay/spec.md
sourceClarifications: work/857-post-evidence-analyze-replay/clarifications.md
sourceChecklist: work/857-post-evidence-analyze-replay/checklist.md
publicOrToolFacingImpact: true
---

# Post Evidence Analyze Replay Plan

Prose status: planned

## Source Snapshot
- spec: work/857-post-evidence-analyze-replay/spec.md sha256:b0d3efd9a4a6e5ed17f1e139a20395c4edc67c56368b1799f312d3f3bd5b77db schemaVersion:1
- clarifications: work/857-post-evidence-analyze-replay/clarifications.md sha256:bf7527567b30665e92d7e0a1b85d60da926c5bc6816013a4130747a7c819295f schemaVersion:1
- checklist: work/857-post-evidence-analyze-replay/checklist.md sha256:9ee7953280f07d0b0971c75e6410246524e7c568eef00109ae28fa71f5720b87 schemaVersion:1

## Plan Scope
- Work item 857-post-evidence-analyze-replay is planned from the current specification, clarification, and checklist facts.
- Requirement count: 1.
- Clarification decision count: 0.
- Checklist result count: 1.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Feed existing evidence into analyze's work-model generation and canonicalize only `sourceSnapshots` in both source generation and currency checks; evidence validation continues to compare the full recorded snapshots, so stale sources remain blocking.

## Contract Impact
- PC-001 [PD-001] command report: The behavior is internal to generated work-model source identity; no CLI argument, public F# signature, or persisted schema changes, and command reports retain their current shapes.

## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: Drive a ship-ready fixture through the exact six-command replay twice, assert the second pass is noChange and readiness/guidance bytes are unchanged, then run the Commands suite.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] diagnoseOnly: Existing evidence files migrate lazily on their next replay; sourceSnapshots stay serialized unchanged, so no schema migration or destructive rewrite is required.

## Generated View Impact
- GV-001 [PD-001] workModel: Work-model generation and currency checking canonicalize the same tool-owned evidence snapshot payload, preventing recursive analysis/evidence digests while retaining all semantic evidence sources.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 857-post-evidence-analyze-replay`.
