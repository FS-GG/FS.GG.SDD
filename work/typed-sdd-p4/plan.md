---
schemaVersion: 1
workId: typed-sdd-p4
title: Typed SDD additive lifecycle backend
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/typed-sdd-p4/spec.md
sourceClarifications: work/typed-sdd-p4/clarifications.md
sourceChecklist: work/typed-sdd-p4/checklist.md
publicOrToolFacingImpact: true
---

# Typed SDD additive lifecycle backend Plan

Prose status: planned

## Source Snapshot
- spec: work/typed-sdd-p4/spec.md sha256:5aae02e7418ccc4429e3b9c47574f1dc436c15cb6b3a992b8b95acd7d2e25cc5 schemaVersion:1
- clarifications: work/typed-sdd-p4/clarifications.md sha256:7be402436cc021c2ad34450d66a0b61fbd6b6e0f8bf0274f54e634d1084da52c schemaVersion:1
- checklist: work/typed-sdd-p4/checklist.md sha256:b5c58ec0fc03c5a45de9145fbfcf534b25bad57f6b4548e8bb9c16e1e5c812be schemaVersion:1

## Plan Scope
- Extend the existing typed specification kernel into an installed-artifact representation backend without changing the lifecycle stage order.
- Add lifecycle/provenance contracts and CLI operations in the producer first, publish an immutable preview, then let provider repositories consume that exact identity.
- Prove migration safety and all failure identities in producer fixtures before any downstream provider rollout.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Add `typed-sdd` as a distinct lifecycle value; retain explicit `none` and `sdd`, recognize `spec-kit` only as a compatibility token, and resolve omission to `sdd` until P5.
- PD-002 [AC-002] [FR-002] complete: Use `work/<id>/specification.fsx` as canonical Typed SDD authority; compile it with package-owned kernel/extension assemblies into deterministic normalized JSON and Markdown projections.
- PD-003 [AC-003] [AC-004] [FR-003] complete: Implement migration as a two-step analyze/accept protocol with `Migrated`, `Ambiguous`, and `Unsupported` classifications, byte-preserving preaccept behavior, semantic diff, and rollback digest.
- PD-004 [AC-006] [FR-004] complete: Give wrong lifecycle, compiler absence, identity mismatch, unsupported extension, stale projection, direct canonical edit, and unavailable authoring agent separate stable diagnostics and corrections.
- PD-005 [AC-002] [AC-005] [FR-005] complete: Keep the existing ordered stage commands and installed stage skills as one corpus; resolve a representation backend from provenance instead of duplicating stage instructions.
- PD-006 [AC-002] [AC-006] [FR-006] complete: Extend scaffold provenance additively with lifecycle/backend identity and a Typed SDD authority manifest containing compiler, package, extension, canonical, normalized, projection, authoring, and rollback identities.
- PD-007 [AC-002] [FR-007] complete: Add CLI `author`, `inspect`, and `migrate` surfaces backed by installed assets; reject Markdown ingestion and fail closed on unsupported extension nodes.
- PD-008 [AC-005] [FR-008] complete: Make refresh, upgrade, doctor, readiness, and ship consult provenance and the authority manifest, preserve authored meaning, and diagnose stale or incompatible identities before write.
- PD-009 [AC-001] [AC-002] [AC-003] [AC-004] [AC-005] [AC-006] [FR-009] complete: Cover the clean installed-package path and each required mutation in focused unit, command, CLI, artifact, and end-to-end fixture tests; release as the next immutable minor preview.

## Contract Impact
- PC-001 [PD-001] lifecycle contract: Lifecycle values and omitted-value behavior are public provider-facing compatibility facts.
- PC-002 [PD-002] [PD-006] authority manifest: Typed SDD provenance and authority identities are additive stable JSON contracts.
- PC-003 [PD-003] migration report: Classification, semantic diff, write decision, and rollback facts are deterministic tool-facing output.
- PC-004 [PD-004] diagnostics: Failure identities and corrections are stable automation-facing contracts.
- PC-005 [PD-005] [PD-007] installed assets: Backend descriptors, compiler assets, and author/inspect/migrate skills ship in the package/tool artifacts.

## Verification Obligations
- VO-001 [PD-001] [PD-005] [PC-001] semanticTest: Prove the lifecycle matrix and shared stage corpus with explicit and omitted selections.
- VO-002 [PD-002] [PD-006] [PD-007] [PC-002] [PC-005] integrationTest: A clean directory using published artifacts only authors, compiles, inspects, projects, refreshes, upgrades, and ships Typed SDD.
- VO-003 [PD-003] [PC-003] mutationTest: Prove all migration classifications and that no bytes change before acceptance.
- VO-004 [PD-004] [PD-008] [PC-004] mutationTest: Prove each required negative control maps to its unique diagnostic through doctor, readiness, and ship.
- VO-005 [PD-009] [PC-001] [PC-002] [PC-003] [PC-004] releaseEvidence: Full build/test/surface/pack gates pass and public artifacts are byte-identical across feeds before downstream pinning.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-003] optIn: Standard SDD remains canonical until migration analysis reports `Migrated` and the operator explicitly accepts the semantic diff.
- PM-002 [PC-002] rollbackCapable: Accepted migration records the Standard SDD source digest and rollback boundary without deleting the original source.

## Generated View Impact
- GV-001 [PD-002] [PD-006] normalizedSpecification: Normalized JSON and Markdown are regenerated views bound to the canonical F# source and package/compiler identity.
- GV-002 [PD-008] workModel: Readiness and ship views include lifecycle/backend authority freshness or report a specific blocking diagnostic.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- P5, not this item, owns the eventual omitted-value default flip.
- Governance remains an optional integration seam and is not required by the Typed SDD backend.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work typed-sdd-p4`.
