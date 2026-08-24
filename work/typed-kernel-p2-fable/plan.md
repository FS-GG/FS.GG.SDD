---
schemaVersion: 1
workId: typed-kernel-p2-fable
title: Fable-Consumable Typed Kernel Package
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/typed-kernel-p2-fable/spec.md
sourceClarifications: work/typed-kernel-p2-fable/clarifications.md
sourceChecklist: work/typed-kernel-p2-fable/checklist.md
publicOrToolFacingImpact: true
---

# Fable-Consumable Typed Kernel Package Plan

Prose status: planned

## Source Snapshot
- spec: work/typed-kernel-p2-fable/spec.md sha256:793eb033c8a37abe954efb879140b546e3e378326e98b2a09a3e7d6d7fa1d0a9 schemaVersion:1
- clarifications: work/typed-kernel-p2-fable/clarifications.md sha256:61228784359c84673c87bf0c13a8c0774a5974194f9bd4cd42bd24a284f2db5a schemaVersion:1
- checklist: work/typed-kernel-p2-fable/checklist.md sha256:34c31d987317ab6d80c5e90db685f78e03133591fa57fdca43c329e2ec3cd18b schemaVersion:1

## Plan Scope
- Work item typed-kernel-p2-fable is planned from the current specification, clarification, and checklist facts.
- Requirement count: 5.
- Clarification decision count: 0.
- Checklist result count: 5.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Pack an authoritative `fable/FS.GG.SDD.Artifacts.fsproj` and portable kernel source whose Fable-facing extension contract excludes `System.Text.Json` callbacks while retaining identity, envelope, validation, normalization, fingerprint, compile, and semantic diff.
- PD-002 [AC-001] [FR-002] complete: Implement portable framing and SHA-256 in the package-owned Fable source, then compare exact normalized bytes and lowercase fingerprints with the net10.0 implementation.
- PD-003 [AC-001] [FR-003] complete: Leave the net10.0 implementation and signatures unchanged and rerun all typed-kernel codec, projection, evidence, migration, and package tests.
- PD-004 [AC-001] [FR-004] complete: Inspect packed paths and compile a clean external Fable consumer; reject any consumer/gameplay source, undeclared dependency, or missing package-owned Fable file.
- PD-005 [AC-001] [FR-005] complete: Bump the coherent SDD package set to preview.3, publish through the existing release workflow, and verify a fresh nuget.org-only locked consumer restore.

## Contract Impact
- PC-001 [PD-001] package layout: `FS.GG.SDD.Artifacts` adds the conventional `fable/FS.GG.SDD.Artifacts.fsproj` source projection without changing its net10.0 public API.

## Verification Obligations
- VO-001 [PD-001] [PD-002] [PD-003] [PD-004] [PD-005] [PC-001] semanticTest: Run producer unit/package tests, pack inspection, gate inversion, clean .NET/Fable cross-runtime consumer equality, full build, release workflow, and public-feed locked restore.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] additive: preview.3 adds portable package metadata; preview.2 remains immutable and consumers advance explicitly.

## Generated View Impact
- GV-001 [PD-001] packageView: Refresh SDD work-model/evidence plus deterministic packed-path and cross-runtime receipts for the preview.3 artifact.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work typed-kernel-p2-fable`.
