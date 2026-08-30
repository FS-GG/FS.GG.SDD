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
- spec: work/944-sdd-151-evidence-authority-release/spec.md sha256:d3d789b2acece7026f05849cca01a6a810bcd6497c94ae9015ff053ae9e580a4 schemaVersion:1
- clarifications: work/944-sdd-151-evidence-authority-release/clarifications.md sha256:2dd17e96eb47fcc3857b8740d27fac631987cc8ae6e10f6caeb388b6fefc82a9 schemaVersion:1
- checklist: work/944-sdd-151-evidence-authority-release/checklist.md sha256:13f38e105426f4e81c3c16dc9c9fe8f93e9d0abebcb0ca28b71f3da2f2460502 schemaVersion:1

## Plan Scope
- Work item 944-sdd-151-evidence-authority-release is planned from the current specification, clarification, and checklist facts.
- Requirement count: 5.
- Clarification decision count: 4.
- Checklist result count: 6.

## Plan Decisions
- PD-001 [AC-001] [FR-001] [DEC-001] complete: Download exact protected-main run/job logs, compare retry state, and run the unchanged RefreshEvidenceDeadlockTests repeatedly in fresh Debug processes plus complete Debug/Release suites; change the test only if a reproducible root cause is established.
- PD-002 [AC-002] [FR-002] [DEC-002] complete: Mechanically inventory every exact 1.5.0 version occurrence under declared paths, classify it as CLI/Artifacts release projection or unrelated history, update the coherent set to 1.5.1, and assert Contracts remains 7.5.2.
- PD-003 [AC-003] [FR-003] complete: Run locked restore/build/test gates in Debug and Release, focused release-contract tests, exhaustive validation, API compatibility, package inspection, release dry-run, SDD verify/ship, and invert exact-version plus every ApiCompat isolation property. Execute shell subjects as ordinary commands with explicit status capture so `set -e` suppression inside conditionals cannot make a failed assertion green.
- PD-004 [AC-004] [FR-004] complete: Pack once, install the CLI from an isolated local source/tool path, assert version 1.5.1, and execute the candidate-owned ignored/untracked refusal with tracked and external-receipt positive controls.
- PD-005 [AC-005] [FR-005] [DEC-003] [DEC-004] complete: Treat independent nupkg packs as non-identical containers. Qualify the source/custody mechanism in PR; after merge, create packages once in a no-push dispatch, retain packages plus a commit-bound inventory/hash manifest, and make tag/release publication download and verify only that exact prior artifact. Add back-to-back pack and substituted-handoff inversions. Hand the reviewed-ready PR to the root release authority without publishing, tagging, or merging.

## Contract Impact
- PC-001 [PD-002] [PD-005] package release: FS.GG.SDD.Cli and FS.GG.SDD.Artifacts package metadata and release projections advance compatibly to stable 1.5.1; FS.GG.Contracts remains 7.5.2.

## Verification Obligations
- VO-001 [PD-001] [PD-002] [PD-003] [PD-004] [PD-005] [PC-001] releaseQualification: Preserve and repeatedly exercise seeding non-vacuity; pass full Debug/Release, pack/API/validation/dry-run, exact-version and isolation mutations, back-to-back non-reproducible-pack control, exact-artifact custody/handoff inversions, and clean installed-tool behavior; record tracked exact-candidate evidence.

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
