---
schemaVersion: 1
workId: 903-scaffold-root-gitignore-composition
title: Scaffold Root Gitignore Composition
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/903-scaffold-root-gitignore-composition/spec.md
sourceClarifications: work/903-scaffold-root-gitignore-composition/clarifications.md
sourceChecklist: work/903-scaffold-root-gitignore-composition/checklist.md
publicOrToolFacingImpact: true
---

# Scaffold Root Gitignore Composition Plan

Prose status: planned

## Source Snapshot
- spec: work/903-scaffold-root-gitignore-composition/spec.md sha256:d826fc7488a4cdda236fe2d9527ab89fa9ae4542c766f21be1eea61b3fbc0530 schemaVersion:1
- clarifications: work/903-scaffold-root-gitignore-composition/clarifications.md sha256:3b21cffd21f648977c2ef6f3039778a4c0fa2d9faca59fdc9bfb852722863c44 schemaVersion:1
- checklist: work/903-scaffold-root-gitignore-composition/checklist.md sha256:b1cdbf675fd62886bd98ce1710830fa22e1fc2a1626b123edf5af5236914afec schemaVersion:1

## Plan Scope
- Work item 903-scaffold-root-gitignore-composition is planned from the current specification, clarification, and checklist facts.
- Requirement count: 1.
- Clarification decision count: 0.
- Checklist result count: 1.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Scaffold will retain the SDD-generated root `.gitignore` as a temporary composition input, run the provider without generic overwrite, then deterministically merge the pre-existing SDD lines and provider-authored lines into one root file. The merge is line-preserving and de-duplicates exact lines in first-seen order, so neither source is clobbered; explicit provider `--force` behavior is not inferred or widened by SDD.

## Contract Impact
- PC-001 [PD-001] command report: `fsgg-sdd scaffold` keeps its existing provider invocation and report schema. Its default root-file composition becomes a generic behavior contract: a provider-emitted `.gitignore` is compatible with SDD's seeded root ignore content, while a provider failure or an attempt to write an SDD-owned subtree remains fail-closed.

## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: Add focused command tests that assert both SDD and provider entries survive default scaffold, prove a direct-overwrite mutation is red, and prove an existing authored root file is not clobbered. Add opt-in real-provider composition acceptance proving the same output shape through the published provider route; run focused tests, CLI smoke evidence, and the release dry gates before handoff.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] additiveBehavior: Existing providers without a root `.gitignore` keep the current scaffold result. Providers that do emit one gain safe default composition without registry schema, provenance schema, or command-line changes. No migration or rewrite of existing scaffolded workspaces is required.

## Generated View Impact
- GV-001 [PD-001] workModel: Refresh the work model and generated readiness views after the authored plan/tasks/evidence are current; generated views must record the composition contract and report stale input rather than silently treating scaffold evidence as current.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 903-scaffold-root-gitignore-composition`.
