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
- spec: work/903-scaffold-root-gitignore-composition/spec.md sha256:ecedeaf189c1a22e5b6c6f211c5167921eabb9df3db1d645d5775659647f3c33 schemaVersion:1
- clarifications: work/903-scaffold-root-gitignore-composition/clarifications.md sha256:3b21cffd21f648977c2ef6f3039778a4c0fa2d9faca59fdc9bfb852722863c44 schemaVersion:1
- checklist: work/903-scaffold-root-gitignore-composition/checklist.md sha256:886f8d59bcc2baa925c7167670089d06c2375483d3a7b681a1a8955385f23e7a schemaVersion:1

## Plan Scope
- Work item 903-scaffold-root-gitignore-composition is planned from the current specification, clarification, and checklist facts.
- Requirement count: 1.
- Clarification decision count: 0.
- Checklist result count: 1.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Scaffold will retain the SDD-generated root `.gitignore` as a temporary composition input, run the provider without generic overwrite, then read the provider-authored root raw bytes and atomically write the deterministic UTF-8 SDD lifecycle-ignore prefix followed by those bytes byte-for-byte. The provider suffix retains any UTF-8 preamble plus its original order, comments, blank lines, negations, and final-newline state; composition performs no line de-duplication, decoding, or normalization. Explicit provider `--force` behavior is not inferred or widened by SDD.

## Contract Impact
- PC-001 [PD-001] command report: `fsgg-sdd scaffold` keeps its existing provider invocation and report schema. Its default root-file composition becomes a generic behavior contract: a provider-emitted `.gitignore` is compatible with SDD's seeded root ignore content because its raw bytes are retained exactly as a suffix after the deterministic UTF-8 SDD prefix through an atomic binary write, with no line de-duplication, decoding, or normalization; a provider failure or an attempt to write an SDD-owned subtree remains fail-closed.

## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: Add focused command tests that assert the scaffolded `.gitignore` starts with the deterministic UTF-8 SDD prefix and ends with the provider-emitted raw bytes byte-for-byte, including a UTF-8-BOM plus CRLF producer boundary, that generic `--force` is not forwarded, and that a direct-overwrite mutation is red. Add opt-in real-provider composition acceptance proving the same direct-versus-scaffold output contract through the published provider route; run focused tests, CLI smoke evidence, and the release dry gates before handoff.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] additiveBehavior: Existing providers without a root `.gitignore` keep the current scaffold result. Providers that do emit one gain safe default composition whose provider-authored body is preserved byte-for-byte after the SDD prefix, without registry schema, provenance schema, or command-line changes. No migration or rewrite of existing scaffolded workspaces is required.

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
