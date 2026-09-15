---
schemaVersion: 1
workId: 927-single-quint-lifecycle
title: Single Quint-backed workspace lifecycle
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/927-single-quint-lifecycle/spec.md
sourceClarifications: work/927-single-quint-lifecycle/clarifications.md
sourceChecklist: work/927-single-quint-lifecycle/checklist.md
publicOrToolFacingImpact: true
---

# Single Quint-backed workspace lifecycle Plan

Prose status: planned

## Source Snapshot
- spec: work/927-single-quint-lifecycle/spec.md sha256:bc9271fa0dad56388caade41928dfa07a15b33a1e0367a6a4d30166a395b3eed schemaVersion:1
- clarifications: work/927-single-quint-lifecycle/clarifications.md sha256:76f57836cdfd3dfe60eec538231959bdf5190a35f50c2d79a8687a551734f250 schemaVersion:1
- checklist: work/927-single-quint-lifecycle/checklist.md sha256:bdfaa9763ef49e55873dcbf34f230dc0304b01e1a3b9e0ec600d464d9920792f schemaVersion:1

## Plan Scope
- Define the typed public surface before implementation, then exercise it through semantic unit tests and a runnable literate Quint model.
- Reuse existing SHA-256/canonical JSON conventions and Typed SDD atomic migration effects.

## Plan Decisions
- PD-001 [FR-001] [FR-002] [FR-003] [DEC-002] complete: Add closed records/unions for workspace modules, proposals, dispositions, human acceptance, semantic changes, reconciliation, and migration classifications.
- PD-002 [FR-004] [FR-005] [DEC-004] complete: Implement pure validation, canonical serialization/fingerprinting, readable diff, deterministic three-way reconciliation, and exact-base reduction.
- PD-003 [FR-006] [FR-007] [DEC-005] complete: Implement pure legacy inventory planning with complete classifications, original digests, target identity, decision requirements, and rollback manifest identity.
- PD-004 [FR-008] [DEC-001] complete: Ship one literate Quint authority and test module; typecheck, test, and sample-run witnesses/invariants with pinned Quint 0.32.0.
- PD-005 [FR-009] [FR-010] [DEC-003] complete: Change omitted Typed SDD backend selection to Quint, update equivalent author/migrate guidance, diagnostics, API baselines, and package-only controls.

## Contract Impact
- PC-001 [PD-001] breaking: SDD 2.0.0 adds the single-lifecycle contract and changes omitted Typed SDD backend behavior; explicit legacy behavior remains in the compatibility window.

## Verification Obligations
- VO-001 [PD-001] [PD-002] [PD-003]: Unit and mutation tests cover every disposition, exact-base reduction, semantic conflicts, dangling IDs, stale evidence, and migration class.
- VO-002 [PD-004]: Quint typecheck/test/run proves every major action reachable and all declared invariants stable in sampled traces.
- VO-003 [PD-005]: CLI and package-only tests prove omitted Quint, explicit v1 compatibility, deterministic installed outputs, and unchanged atomic rollback.

## Performance Intent
- Canonicalization, validation, diff, and reduction are deterministic in module/change count; no runtime latency target is introduced.

## Migration Posture
- PM-001 [PC-001] breaking-with-window: publish SDD 2.0.0 before adoption; explicit fsharp-specification-v1 and all legacy lifecycle tokens remain inspectable/migratable for the 2.x window.

## Generated View Impact
- GV-001 [PD-001] [PD-005]: Update `.fsi` baselines, release schema/version projections, seeded skills, and readiness outputs from their authorities.

## Accepted Deferrals
- Templates/wizard default activation remains gated on `OperatingV2` and a separately qualified public package rollout.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- The package major release and downstream activation are sequenced after producer merge.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 927-single-quint-lifecycle`.
