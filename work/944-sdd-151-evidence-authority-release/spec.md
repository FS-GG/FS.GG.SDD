---
schemaVersion: 1
workId: 944-sdd-151-evidence-authority-release
title: Sdd 151 Evidence Authority Release
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Sdd 151 Evidence Authority Release Specification

Prose status: specified

## User Value
Ship the merged evidence-authority repair to installed consumers as a coherent stable 1.5.1 release candidate.

## Scope
- SB-001: Diagnose the protected-main seeding anomaly; preserve non-vacuity; bump CLI and Artifacts projections, baselines, fixtures, and release docs while retaining Contracts 7.5.2; qualify exact package bytes and clean installed-tool behavior.

## Non-Goals
- SB-002: Do not publish feeds, create tags/releases, or merge before independent review and release authorization.
- SB-003: Do not change FS.GG.Contracts 7.5.2 or weaken the seeding non-vacuity assertion.

## User Stories
- US-001 (P1): As an installed-tool consumer, I receive the candidate-owned local-evidence authority repair in stable 1.5.1.
- US-002 (P1): As a release authority, I can distinguish a real deterministic seeding defect from a one-off runtime anomaly before publishing.

## Acceptance Scenarios
- AC-001 [US-002] [FR-001]: Given protected-main run 33299790063 failed one seeding non-vacuity assertion, when the exact job and rerun are inspected and the focused test is repeated in isolated processes, then the cause is repaired if reproducible or explicitly bounded with the assertion unchanged.
- AC-002 [US-001] [FR-002]: Given current source is 1.5.0, when the release candidate is prepared, then CLI and Artifacts package/version projections all report stable 1.5.1 while Contracts remains exactly 7.5.2.
- AC-003 [US-001] [FR-003]: Given the exact candidate, when Debug, Release, package, API, validation, and release dry-run gates execute, then every required gate passes, shell assertions retain their status outside conditional-errexit contexts, and each isolation property is proven red when removed.
- AC-004 [US-001] [FR-004]: Given candidate packages, when installed into a clean isolated tool path and exercised against ignored/untracked versus tracked/external evidence, then installed 1.5.1 exhibits the candidate-owned refusal and positive behavior.
- AC-005 [US-001] [FR-005]: Given a successful post-merge dry-run at the exact release commit, when a later tag/release requests publication, then it discovers and downloads that durable run artifact, verifies its source/head/inventory/hash manifest, and pushes those exact archives to both feeds without rebuilding or repacking.

## Functional Requirements
- FR-001: Qualification MUST preserve the existing RefreshEvidenceDeadlockTests non-vacuity assertion and MUST diagnose the protected-main failure from exact logs, retry outcome, repeated isolated processes, and full-suite controls. (covers AC-001)
- FR-002: FS.GG.SDD.Cli and FS.GG.SDD.Artifacts MUST form one stable 1.5.1 coherent set across authoritative properties, release contract, docs, baselines, and fixtures; FS.GG.Contracts MUST remain 7.5.2. (covers AC-002)
- FR-003: The exact candidate MUST pass repeated focused Debug, complete Debug and Release suites, SDD verify/ship, package/API/validation/release dry-run gates, and release-coherence inversions. Shell test harnesses MUST capture explicit command statuses outside `if`/`!` conditional-errexit contexts, aggregate failures, and prove removal of each ApiCompat isolation property fails. (covers AC-003)
- FR-004: A clean local install from candidate packages MUST report 1.5.1 and exercise the candidate-owned local evidence refusal plus tracked/external positive controls. (covers AC-004)
- FR-005: Whole-nupkg bytes MUST NOT be claimed reproducible across independent packs. A no-push workflow dispatch at the exact release commit MUST build and qualify the coherent set once, upload the packages plus a source/head/inventory/SHA-256 manifest as a durable artifact, and a later tag/release workflow MUST fail closed unless it can download and verify that exact artifact before pushing the same archives to both feeds without rebuilding or repacking. (covers AC-005)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- Patch release of the existing CLI/Artifacts contract; no schema or public API removal. Version projections and install guidance move to 1.5.1; Contracts stays 7.5.2.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 944-sdd-151-evidence-authority-release`.
