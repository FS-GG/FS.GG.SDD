---
schemaVersion: 1
workId: 942-local-evidence-authority
title: Local Evidence Authority
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/942-local-evidence-authority/spec.md
sourceClarifications: work/942-local-evidence-authority/clarifications.md
sourceChecklist: work/942-local-evidence-authority/checklist.md
publicOrToolFacingImpact: true
---

# Local Evidence Authority Plan

Prose status: planned

## Source Snapshot
- spec: work/942-local-evidence-authority/spec.md sha256:ed33f5e49d6c22cfbfe8482fa0d5e45643e6d85b494d479b9b931cf1c3f189e4 schemaVersion:1
- clarifications: work/942-local-evidence-authority/clarifications.md sha256:ed5c945fb3f230d80a15cacd9641b3d2b3b063729452436c0cf1236a4d538cfa schemaVersion:1
- checklist: work/942-local-evidence-authority/checklist.md sha256:0aad692569e82fc616935e7210a4ef7df8a27c56847df5f29e8ae8242703cec2 schemaVersion:1

## Plan Scope
- Work item 942-local-evidence-authority is planned from the current specification, clarification, and checklist facts.
- Requirement count: 5.
- Clarification decision count: 3.
- Checklist result count: 5.

## Plan Decisions
- PD-001 [AC-001] [AC-002] [FR-001] [DEC-001] complete: Plan one read-only `git cat-file -e HEAD:<path>` effect for each satisfying declaration's cited local path, keyed through the existing MVU effect/result boundary rather than direct filesystem calls in pure handlers; HEAD membership prevents staged-only proof from authorizing the reviewed candidate.
- PD-002 [AC-003] [FR-002] complete: Treat a successful exact-path Git index lookup as durable local authority so existing tracked source, test, docs, specs, readiness, and work-package evidence stays compatible.
- PD-003 [AC-004] [FR-003] [DEC-002] complete: Keep non-empty `sourceRefs[].uri` entries outside local-path enumeration as the explicit external receipt shape; no schema version changes.
- PD-004 [AC-005] [FR-004] [DEC-001] complete: Derive one shared local-authority diagnostic fold for evidence, verify, and ship; missing or failed Git provenance prevents a supported disposition and therefore prevents `shipReady`.
- PD-005 [AC-001] [AC-002] [AC-003] [AC-004] [AC-005] [FR-005] [DEC-003] complete: Add boundary tests with real temporary Git repositories for ignored-existing, untracked-existing, clean-clone omission, tracked work evidence, and external receipt controls, plus verify/ship regression assertions.

## Contract Impact
- PC-001 [PD-001] [PD-002] [PD-003] [PD-004] command report: Add dedicated diagnostic ids for non-durable local evidence authority while retaining evidence schemaVersion 1 and all existing report fields.

## Verification Obligations
- VO-001 [PD-001] [PD-002] [PD-003] [PD-004] [PD-005] [PC-001] semanticTest: Run focused Artifacts and Commands tests plus the full solution suite to a tracked TRX; demonstrate every new refusal turns red on its named diagnostic and both positive controls remain green.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] additiveDiagnostic: Existing evidence.yml remains schemaVersion 1; authors with ignored or untracked local proof move the artifact into tracked source (prefer `work/<id>/evidence/`) or cite a durable external `sourceRefs[].uri` receipt.

## Generated View Impact
- GV-001 [PD-004] verificationAndShip: verification evidence dispositions and ship readiness now include local-authority diagnostics derived from the exact candidate workspace; no new generated-view schema version.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 942-local-evidence-authority`.
