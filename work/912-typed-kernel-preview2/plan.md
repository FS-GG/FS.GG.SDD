---
schemaVersion: 1
workId: 912-typed-kernel-preview2
title: Correct typed-kernel preview dependency identity
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/912-typed-kernel-preview2/spec.md
sourceClarifications: work/912-typed-kernel-preview2/clarifications.md
sourceChecklist: work/912-typed-kernel-preview2/checklist.md
publicOrToolFacingImpact: true
---

# Correct typed-kernel preview dependency identity Plan

Prose status: planned

## Source Snapshot
- spec: work/912-typed-kernel-preview2/spec.md sha256:62c29bbb55140e309d55c2f11a148bd9837df3e587cb651451715c19be448da8 schemaVersion:1
- clarifications: work/912-typed-kernel-preview2/clarifications.md sha256:bfa963274a64b4db34b13057b2aef4af94aeaad7f461ac94035148e22f4e9006 schemaVersion:1
- checklist: work/912-typed-kernel-preview2/checklist.md sha256:1e23a072a4f38da724643b725ed11011c6733033e52ca7b00c0b0b62b962deed schemaVersion:1

## Plan Scope
- Correct the producer-side package identity defect before any P3 consumer pin changes.
- Preserve the typed protocol public API and the independent `FS.GG.Contracts` 7.5.2 version line.
- Publish a new immutable coherent preview because preview.1 cannot be replaced on either feed.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: The Artifacts release lane consumes the already-resolved coherent source version and rejects both `Version` and `PackageVersion` command-line overrides.
- PD-002 [AC-001] [FR-001] complete: Avoid global identity properties because NuGet's separate project-reference version evaluation inherits them and would replace the independently versioned Contracts producer's 7.5.2 identity.
- PD-003 [AC-001] [FR-001] complete: A real release-equivalent pack test opens the nupkg and asserts the exact Contracts dependency; the static workflow test supplies the mutation control.
- PD-004 [AC-001] [FR-002] complete: Bump the SDD coherent line to immutable `1.3.0-preview.2`, update release baselines and consumer fixtures, and publish only from merged main through the existing dual-feed workflow.
- PD-005 [AC-001] [FR-002] [FR-003] complete: Verify both feed archives by unsigned-entry identity and run a clean nuget.org consumer with warnings captured; preview.1 remains documented but ineligible.

## Contract Impact
- PC-001 [PD-001] [PD-002] release workflow: Artifacts pack identity comes from the source-evaluated coherent line and the Contracts dependency identity remains producer-owned.
- PC-002 [PD-003] package metadata: `FS.GG.SDD.Artifacts` 1.3.0-preview.2 depends exactly on `FS.GG.Contracts` 7.5.2.
- PC-003 [PD-004] release identity: SDD package/tool generator projections advance coherently to 1.3.0-preview.2 with no public API delta.

## Verification Obligations
- VO-001 [PD-001] [PD-002] [PD-003] [PC-001] [PC-002] semanticTest: Static workflow mutation guard and real nupkg/nuspec dependency inspection pass.
- VO-002 [PD-004] [PC-003] integrationTest: Full build, test, surface, clean-consumer, and protected release dry-run gates pass at the accepted head.
- VO-003 [PD-005] [FR-002] releaseEvidence: Merged-main release publishes preview.2 to both feeds; unsigned payload entries match and a public-only clean consumer restores/runs without NU1603.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-002] forwardOnly: Published preview.1 is immutable and remains visible as defective release history; downstream pins move directly to preview.2.

## Generated View Impact
- GV-001 [PD-004] workModel: Generator-version-bearing release baselines and SDD readiness views refresh coherently to preview.2.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Both `Version` and `PackageVersion` are unsafe command-line globals for this pack graph because NuGet's dependency-version target inherits them; source-resolved packing is the dependency-identity boundary.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 912-typed-kernel-preview2`.
