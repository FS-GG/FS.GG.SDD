---
schemaVersion: 1
workId: 892-rendering-skills-v2-consumer
title: Rendering Skills V2 Consumer
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/892-rendering-skills-v2-consumer/spec.md
sourceClarifications: work/892-rendering-skills-v2-consumer/clarifications.md
sourceChecklist: work/892-rendering-skills-v2-consumer/checklist.md
publicOrToolFacingImpact: true
---

# Rendering Skills V2 Consumer Plan

Prose status: planned

## Source Snapshot
- spec: work/892-rendering-skills-v2-consumer/spec.md sha256:3656d9c80a82b82b3eab46add8165cb1e1275a62f18bb6ce325f4c51c2f5a5a2 schemaVersion:1
- clarifications: work/892-rendering-skills-v2-consumer/clarifications.md sha256:b683a03db399f83d9070dd67c94003c58f691a5e00ab8fddb3acdbc030f00e70 schemaVersion:1
- checklist: work/892-rendering-skills-v2-consumer/checklist.md sha256:ecd1ef3c8bee242e7b29d103055a4e0710beb704f4ab3ec7af0a53a096c9017c schemaVersion:1

## Plan Scope
- Work item 892-rendering-skills-v2-consumer is planned from the current specification, clarification, and checklist facts.
- Requirement count: 1.
- Clarification decision count: 0.
- Checklist result count: 1.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Parse the Rendering owner manifest with the shared product-manifest codec; retain v1's canonical body behavior and make v2 a closed per-file transport that verifies all selected bytes before any write.

## Contract Impact
- PC-001 [PD-001] schema v2: FS.GG.Rendering owns `skill-manifest.json`; a v2 row supplies `files[]` with canonical text SHA-256 for SKILL.md and every sidecar, while SDD rejects missing, altered, duplicate, escaping, or undeclared package files.

## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: Exercise schema-v2 complete materialization plus missing and undeclared sidecar mutations, retain v1 regression coverage, then run focused command and release-readiness tests.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] compatibility: Existing Rendering schema-v1 packages remain canonical-body-only and retain the advisory for undeclared sidecars; the producer publishes schema-v2 before consumers rely on sidecars.

## Generated View Impact
- GV-001 [PD-001] workModel: Refresh the work model and downstream readiness after the authored v2 contract plan, preserving source digests and surfacing any stale generated view.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 892-rendering-skills-v2-consumer`.
