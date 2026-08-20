---
schemaVersion: 1
workId: 886-comment-quality-policy
title: Comment Quality Policy
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/886-comment-quality-policy/spec.md
sourceClarifications: work/886-comment-quality-policy/clarifications.md
sourceChecklist: work/886-comment-quality-policy/checklist.md
publicOrToolFacingImpact: true
---

# Comment Quality Policy Plan

Prose status: planned

## Source Snapshot
- spec: work/886-comment-quality-policy/spec.md sha256:ed7d13830d283b1ac1a1ed0ee5b2157f0c4a8b85cd3892490b11825540cc5fd8 schemaVersion:1
- clarifications: work/886-comment-quality-policy/clarifications.md sha256:877c62e6bcd140f5f67d95dc915b53b4e8787f69917a9f92fcb03b348fbfedec schemaVersion:1
- checklist: work/886-comment-quality-policy/checklist.md sha256:4614b02b661d32e5f939ac6f365b9cd69847c72e84f07350a64bb696e5953fbe schemaVersion:1

## Plan Scope
- Update the embedded constitution seed in `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs`.
- Keep the authoritative content contract in `specs/033-skeleton-constitution/contracts/constitution-content.md` byte-equivalent with the emitted seed.
- Adopt the complete policy in `.fsgg/constitution.md` and `.specify/memory/constitution.md` without changing their distinct repository-specific framing.
- Add the standard Spec Kit feature package under `specs/121-comment-quality-policy` and focused init/golden coverage.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Add one full `Comment quality` section to the embedded constitution seed, using rationale-first obligations and explicit anti-narration/history language.
- PD-002 [AC-002] [FR-002] complete: Mirror the seed section exactly into the constitution-content contract and assert byte equality in `InitCommandTests`; add equivalent complete sections to both producer constitutions.
- PD-003 [AC-003] [FR-003] complete: Preserve the existing `AgentGuidanceTarget` no-clobber write path and re-run its authored-constitution preservation regression without adding migration logic.
- PD-004 [AC-004] [FR-004] complete: State in policy prose that comment semantics need human review and are not fully lintable; introduce no new analyzer or CLI gate.

## Contract Impact
- PC-001 [PD-001] artifactContent: Newly initialized `.fsgg/constitution.md` bytes change; command-report schema and public F# APIs remain unchanged.
- PC-002 [PD-002] sourceParity: The authoritative constitution-content contract and embedded seed remain byte-equivalent.

## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: Add a focused assertion for the complete comment-quality section, mutate/remove a required sentence, and observe the test fail before restoring it.
- VO-002 [PD-002] [PC-002] goldenTest: Run `InitCommandTests` and verify the command-report golden changes only by the expected constitution digest.
- VO-003 [PD-003] [PC-001] semanticTest: Run the existing authored constitution no-clobber regression and the full solution suite.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] noClobber: Existing authored constitutions are not migrated; the new policy is seeded only into new workspaces and adopted explicitly elsewhere.

## Generated View Impact
- GV-001 [PD-001] commandReportGolden: Re-pin the full-shape command-report constitution digest to the new deterministic seed bytes.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 886-comment-quality-policy`.
