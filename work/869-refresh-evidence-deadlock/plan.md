---
schemaVersion: 1
workId: 869-refresh-evidence-deadlock
title: Refresh Evidence Deadlock
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/869-refresh-evidence-deadlock/spec.md
sourceClarifications: work/869-refresh-evidence-deadlock/clarifications.md
sourceChecklist: work/869-refresh-evidence-deadlock/checklist.md
publicOrToolFacingImpact: true
---

# Refresh Evidence Deadlock Plan

Prose status: planned

## Source Snapshot
- spec: work/869-refresh-evidence-deadlock/spec.md sha256:9531f3dee6f5366a1569f018ccd5e7b725f816dc0a3ac662f4e8c893a363d1ca schemaVersion:1
- clarifications: work/869-refresh-evidence-deadlock/clarifications.md sha256:597461b55eeff51d53442e820c7a419b435650218f8b13a1d26f0094725417af schemaVersion:1
- checklist: work/869-refresh-evidence-deadlock/checklist.md sha256:32a2955ae3522960c62bb7ddc8371f9aa34fef0f3c952e4a762bf2c6c33cefd9 schemaVersion:1

## Plan Scope
- Work item 869-refresh-evidence-deadlock is planned from the current specification, clarification, and checklist facts.
- Requirement count: 8.
- Clarification decision count: 4.
- Checklist result count: 8.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: In `WorkModel.referenceDiagnostics`, replace the `unknown` call on the `task.RequiredEvidence` edge with a single warning-severity `undeclaredEvidenceObligation`, whose artifact is `work/<id>/evidence.yml` and whose relatedIds carry the undeclared evidence id and the citing `tasks.yml`. The other three edges keep calling `unknown` untouched.
- PD-002 [AC-002] [FR-002] complete: Leave `Diagnostics.unknownReference` and `Diagnostics.workModelInconsistent` byte-identical — same id, same `DiagnosticError`, same message — so the change is provably confined to which edge calls them.
- PD-003 [AC-003] [FR-003] complete: Add no compensating gate. `evidence.missingRequiredEvidence` and the `verify`/`ship` unmet-obligation checks already refuse an undeclared obligation by id; the coverage asserts this positively rather than assuming it, because DEC-001 is a relocation and an unwitnessed relocation is a deletion.
- PD-004 [AC-004] [FR-004] complete: Widen `ViewGeneration.generatedViewPlan` to return a fourth element — the distinct, sorted `Artifact.Path` values of the blocking work-model diagnostics — and have `HandlersRefresh` pass it to `refreshMalformedSource` in place of `specPath workId`. The nine other call sites bind and discard it.
- PD-005 [AC-005] [FR-005] complete: Add `refresh.unattributedBlockedView`, emitted when the blocking diagnostics name no declared source, so the empty case is reported as "could not attribute" rather than collapsing onto an arbitrary artifact.
- PD-006 [AC-006] [FR-006] complete: Add a fixture that drives a package to `shipReady`, appends one requirement, and then runs only the documented sequence, asserting `implementationReady` and a scaffolded declaration with no hand-edit and no deletion.
- PD-007 [AC-007] [FR-007] complete: Record the PD-006 fixture's behaviour against the pre-change code by inverting the subject — restoring the blocking edge — and capturing the exact red, so the fixture is shown to be capable of failing.
- PD-008 [AC-008] [FR-008] complete: Document the post-amendment step in `docs/reference/authoring-contracts.md` and make the new diagnostic's correction name the command that closes the gap.
- PD-009 [AC-009] [FR-009] complete: Give `mergeEvidenceArtifacts` a `withSeededObligations` step that appends a `skeletonEvidenceDeclaration` for every obligation the existing artifact matches nothing for — the same seeder the fresh-file path already uses — and report the seeded ids as the non-blocking `evidence.seededObligations`. Match on id or `obligationRefs`, mirroring the disposition rule, so the merge and the disposition cannot disagree; touch no authored declaration.

## Contract Impact
- PC-001 [PD-001] [PD-002] public surface: `FS.GG.SDD.Artifacts` gains one public diagnostic constructor, `undeclaredEvidenceObligation`. Additive; `Diagnostics.fsi`, its `docs/api-surface` baseline and the reflection `PublicSurface.baseline` all move together.
- PC-002 [PD-005] public surface: `FS.GG.SDD.Commands` gains one public refresh diagnostic constructor for the unattributed case, mirrored in `CommandReports.fsi` and its baseline.
- PC-003 [PD-001] [PD-004] persisted artifact: `readiness/<id>/work-model.json` may now carry a warning entry in its existing `diagnostics` array, and may now be written in a state where it previously was not. No schema major; every committed package keeps parsing.
- PC-005 [PD-009] command report: `fsgg-sdd evidence` gains the non-blocking `evidence.seededObligations` report line and, on this one path, appends to an artifact the author owns. Additive; no persisted schema change.
- PC-004 [PD-004] command report: `fsgg-sdd refresh`'s `refresh.malformedSource` changes which artifact its `relatedIds` name. The id, severity and message are unchanged; only the accusation becomes true.


## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: A task requiring an undeclared evidence id derives a work model and reports exactly one warning naming `evidence.yml` and the id.
- VO-002 [PD-002] [PC-001] semanticTest: Unresolved requirement, decision and dependency references still block with `unknownReference`, proving the boundary is the edge's direction and not a blanket demotion.
- VO-003 [PD-003] [PC-003] semanticTest: An obligation that is never declared still stops `evidence`, `verify` and `ship`.
- VO-004 [PD-004] [PD-005] [PC-002] [PC-004] semanticTest: A blocked work model attributes to the source its blocking diagnostics name, and reports the unattributed case when they name none.
- VO-005 [PD-006] [PD-007] semanticTest: The documented sequence recovers a package that gained a requirement, and the same fixture is shown red against the restored blocking edge.
- VO-006 [PD-008] documentationReview: The authoring contract states the post-amendment step and the diagnostic correction points at it.
- VO-007 [PD-009] [PC-005] semanticTest: `evidence` seeds the missing declaration into an authored `evidence.yml`, names the seeded ids, and the seeded declaration is still `result: missing` so `verify` keeps refusing.


## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] [PC-003] diagnoseOnly: The two new diagnostic ids are additive, never replacements. A package generated before this change keeps parsing and keeps its verdict; nothing re-derives differently except the case that previously could not derive at all.

## Generated View Impact
- GV-001 [PD-001] [PD-004] workModel: `readiness/<id>/work-model.json` becomes derivable in a state where it previously refused, and carries the new warning in its `diagnostics` array.
- GV-002 [PD-001] verifyView: `readiness/<id>/analysis.json` reaches `implementationReady` for a package whose only outstanding fact is an undeclared obligation, which is what unblocks `evidence`.


## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 869-refresh-evidence-deadlock`.
