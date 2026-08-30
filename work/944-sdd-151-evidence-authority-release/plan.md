---
schemaVersion: 1
workId: 944-sdd-151-evidence-authority-release
title: Sdd 151 Evidence Authority Release
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/944-sdd-151-evidence-authority-release/spec.md
sourceClarifications: work/944-sdd-151-evidence-authority-release/clarifications.md
sourceChecklist: work/944-sdd-151-evidence-authority-release/checklist.md
publicOrToolFacingImpact: true
---

# Sdd 151 Evidence Authority Release Plan

Prose status: planned

## Source Snapshot
- spec: work/944-sdd-151-evidence-authority-release/spec.md sha256:6f6c5391ff6be94bc39bcae73a8698136ea6f613a78b93ae18208f6f595628aa schemaVersion:1
- clarifications: work/944-sdd-151-evidence-authority-release/clarifications.md sha256:23f75d12d8ab4493a2f4c3d612d678dffbb959a2bb8eb076286124562cd07d94 schemaVersion:1
- checklist: work/944-sdd-151-evidence-authority-release/checklist.md sha256:40292c9d14b4453bb90b3ccfa0ead216815125e4c384dcf86a42cf40a0caa8cc schemaVersion:1

## Plan Scope
- Work item 944-sdd-151-evidence-authority-release is planned from the current specification, clarification, and checklist facts.
- Requirement count: 5.
- Clarification decision count: 3.
- Checklist result count: 5.

## Plan Decisions
- PD-001 [AC-001] [FR-001] [DEC-001] complete: Download exact protected-main run/job logs, compare retry state, and run the unchanged RefreshEvidenceDeadlockTests repeatedly in fresh Debug processes plus complete Debug/Release suites; change the test only if a reproducible root cause is established.
- PD-002 [AC-002] [FR-002] [DEC-002] complete: Mechanically inventory every exact 1.5.0 version occurrence under declared paths, classify it as CLI/Artifacts release projection or unrelated history, update the coherent set to 1.5.1, and assert Contracts remains 7.5.2.
- PD-003 [AC-003] [FR-003] complete: Run locked restore/build/test gates in Debug and Release, focused release-contract tests, exhaustive validation, API compatibility, package inspection, release dry-run, SDD verify/ship, and invert one exact-version release expectation to prove rejection.
- PD-004 [AC-004] [FR-004] complete: Pack once, install the CLI from an isolated local source/tool path, assert version 1.5.1, and execute the candidate-owned ignored/untracked refusal with tracked and external-receipt positive controls.
- PD-005 [AC-005] [FR-005] [DEC-003] complete: Hash and enumerate the exact nupkgs, record candidate/base/tree bindings, and hand the reviewed-ready PR to the root release authority without publishing, tagging, merging, or repacking.

## Contract Impact
- PC-001 [PD-002] [PD-005] package release: FS.GG.SDD.Cli and FS.GG.SDD.Artifacts package metadata and release projections advance compatibly to stable 1.5.1; FS.GG.Contracts remains 7.5.2.

## Verification Obligations
- VO-001 [PD-001] [PD-002] [PD-003] [PD-004] [PD-005] [PC-001] releaseQualification: Preserve and repeatedly exercise seeding non-vacuity; pass full Debug/Release, pack/API/validation/dry-run, exact-version inversion, and clean installed-tool behavior; record tracked exact-candidate evidence.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] patchRelease: No schema or public API migration; consumers update the stable CLI pin from 1.5.0 to 1.5.1 to receive the evidence-authority repair.

## Generated View Impact
- GV-001 [PD-002] releaseProjection: Release-readiness docs, compatibility/install/versioning projections, fixtures, and this work package must agree on 1.5.1 while generated lifecycle views remain exact-candidate current.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 944-sdd-151-evidence-authority-release`.
