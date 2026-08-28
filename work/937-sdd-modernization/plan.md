---
schemaVersion: 1
workId: 937-sdd-modernization
title: Modernize SDD evidence and CI
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/937-sdd-modernization/spec.md
sourceClarifications: work/937-sdd-modernization/clarifications.md
sourceChecklist: work/937-sdd-modernization/checklist.md
publicOrToolFacingImpact: true
---

# Modernize SDD evidence and CI Plan

Prose status: planned

## Source Snapshot
- spec: work/937-sdd-modernization/spec.md sha256:b5825ab92485b1d958d1ad03cea9e7f928bd1de91521b94ae1e121051523be34 schemaVersion:1
- clarifications: work/937-sdd-modernization/clarifications.md sha256:08d1928a381830d6199393958c53272b66c624cac448e8f0854de875dcbea2a3 schemaVersion:1
- checklist: work/937-sdd-modernization/checklist.md sha256:1724a7fa70cb4dbcd7344c429eec88cf2860b9cca0f7a2c3b835b9faa8c39913 schemaVersion:1

## Plan Scope
- Work item 937-sdd-modernization is planned from the current specification, clarification, and checklist facts.
- Requirement count: 5.
- Clarification decision count: 0.
- Checklist result count: 5.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Make observed candidate-bound execution authoritative; retain `synthetic` only as provenance metadata and continue to fail closed when the observed-run receipt is absent or stale.
- PD-002 [AC-001] [FR-002] complete: Add a deterministic path classifier with Small, Normal, and High profiles; ambiguity, empty input, renames, and deletions promote to High.
- PD-003 [AC-001] [FR-003] complete: Keep formal checks, public API compatibility, build-configuration drift, dependency surface, and the full suite as protected High-profile controls.
- PD-004 [AC-001] [FR-004] complete: Preserve required workflow job names and make inapplicable controls succeed with an explicit profile summary rather than disappear.
- PD-005 [AC-001] [FR-005] complete: Bind High-profile approval to the exact candidate commit and require an independently authored critic verdict before merge.

## Contract Impact
- PC-001 [PD-001] evidence-and-ci: Evidence readiness behavior changes compatibly for recorded provenance; the new `scripts/ci-risk` output and stable workflow job names form the CI selection contract.

## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: Run positive and negative evidence-semantic tests, classifier edge-case tests, the full .NET suite, surface/dependency checks, and exact-candidate independent critic review.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] compatibility: Keep reading existing `synthetic` fields and preserve stable required job names; no artifact migration or consumer flag day is required.

## Generated View Impact
- GV-001 [PD-001] guidance-and-manifest: Regenerate the byte-identical Claude/Codex evidence guidance, skill manifest, agent context pointers, API baselines, and readiness work model from the landed candidate.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 937-sdd-modernization`.
