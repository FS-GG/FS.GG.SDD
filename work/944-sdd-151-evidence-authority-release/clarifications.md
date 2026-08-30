---
schemaVersion: 1
workId: 944-sdd-151-evidence-authority-release
title: Sdd 151 Evidence Authority Release
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/944-sdd-151-evidence-authority-release/spec.md
publicOrToolFacingImpact: true
---

# Sdd 151 Evidence Authority Release Clarifications

## Source Specification
- work/944-sdd-151-evidence-authority-release/spec.md

## Clarification Questions
- **CQ-001**: What result is sufficient when the protected-main failure does not reproduce?
- **CQ-002**: Which package identities form the patch coherent set?
- **CQ-003**: Who may publish or create immutable release objects?
- **CQ-004**: What identity crosses the PR, dry-run, tag, feed, and public read-back boundaries?

## Answers
- CQ-001 → Preserve the assertion and bound the anomaly with exact run logs, successful GitHub retry, repeated isolated-process runs, and complete local Debug/Release controls; do not invent a speculative code change.
- CQ-002 → FS.GG.SDD.Cli and FS.GG.SDD.Artifacts move together to 1.5.1; FS.GG.Contracts stays at its separately governed 7.5.2 identity.
- CQ-003 → This implementation lane prepares and verifies exact bytes only. The root release authority retains merge, tag, GitHub Release, and dual-feed publication.
- CQ-004 → PR review qualifies source and the custody mechanism. After merge, one no-push manual dispatch at the exact release commit creates the durable package artifact and manifest. Tag/release publication may consume only that artifact at the same commit; whole-nupkg reproducibility across fresh packs is not assumed.

## Decisions
- **DEC-001** [CQ-001] [FR-001]: Treat non-reproduction as a bounded qualification result only when all independent controls are green and the non-vacuity assertion remains byte-unchanged.
- **DEC-002** [CQ-002] [FR-002] [FR-005]: Use one 1.5.1 version projection for CLI and Artifacts, retain Contracts 7.5.2, and hash the packed nupkgs before handoff.
- **DEC-003** [CQ-003] [FR-003] [FR-004] [FR-005]: Stop at an exact-head reviewed-ready PR with reproducible candidate packages and clean-install evidence; perform no irreversible release action.
- **DEC-004** [CQ-004] [FR-003] [FR-005]: Separate source qualification from archive custody. The reviewed PR proves the build/test/custody mechanism; a post-merge dry-run creates the only publishable archives, and the tag/release path promotes those exact retained bytes after verifying commit-bound manifest identity. Independent repacks are an inversion and must differ without weakening the custody contract.

## Accepted Deferrals
No accepted deferrals recorded.

## Remaining Ambiguity
No blocking ambiguity remains.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 944-sdd-151-evidence-authority-release`.
