---
schemaVersion: 1
workId: 922-quint-first-q1
title: Quint First Q1
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/922-quint-first-q1/spec.md
sourceClarifications: work/922-quint-first-q1/clarifications.md
sourceChecklist: work/922-quint-first-q1/checklist.md
publicOrToolFacingImpact: true
---

# Quint First Q1 Plan

Prose status: planned

## Source Snapshot
- spec: work/922-quint-first-q1/spec.md sha256:164bee87dcb9ca4f3dca46d6e934b3b5b3844e6c6b9f621be2d897cd38440861 schemaVersion:1
- clarifications: work/922-quint-first-q1/clarifications.md sha256:41e85a6df143d173a34a18af5e13e1651a568251e53e29a469b0a25ee6361d58 schemaVersion:1
- checklist: work/922-quint-first-q1/checklist.md sha256:4c9aa7576ee2c221b0bab16f1f2df71ea730076dfc83befca99dfd72b656d128 schemaVersion:1

## Plan Scope
- Work item 922-quint-first-q1 is planned from the current specification, clarification, and checklist facts.
- Requirement count: 12.
- Clarification decision count: 4.
- Checklist result count: 12.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Author three canonical literate documents under `docs/experiments/quint-q1/slices/`; extract into a temporary directory and run exact Quint typecheck/test/run controls over every module.
- PD-002 [AC-002] [FR-002] complete: Add `tests/quint-q1/qualify.sh` as the deterministic orchestrator. It binds tool hashes and fence manifest, runs `lmt` only in an isolated temporary copy, compares two extractions, and executes missing/order/duplicate/stale/edited mutations.
- PD-003 [AC-003] [FR-003] complete: Request two fresh critics after the candidate commit: one domain/readability critic and one architecture/tooling critic. Their exact-head records are acceptance inputs, never authored report scores.
- PD-004 [AC-004] [FR-004] complete: Emit a producer candidate manifest and separately bind the response from `EHotwagner/S.I.R.#353`; absent or mismatched consumer evidence leaves the producer verdict refused.
- PD-005 [AC-001] [FR-005] complete: Put every requirement, action, invariant, evidence obligation, and implementation binding in explicit typed catalogue values within the embedded Quint; prose may explain only identities present there.
- PD-006 [AC-005] [FR-006] complete: Select `quint-co/quint-llm-kit@cc75369f741af7d490936f82002c2d28e3b3d78d`, hash its complete tracked tree plus each evaluated skill/workflow, preserve Apache-2.0 attribution, and execute no installer.
- PD-007 [AC-005] [FR-007] complete: Publish a line-item adopted/adapted/rejected matrix for the standalone skills, witnesses, trace explanation, transition/type/listener coverage, Choreo, execution tiers, and implementation/refactor workflows.
- PD-008 [AC-006] [FR-008] complete: Give the coordination slice explicit retry/stale/lost-update/double-apply/order/deadlock/safety/liveness/convergence controls and the S.I.R. slice a stable replay vocabulary plus deliberately wrong witness.
- PD-009 [AC-003] [FR-009] complete: Record machine-readable exact tool/source/tree digests and repeatable timing/size measurements; keep subjective readability solely in independent review evidence.
- PD-010 [AC-001] [AC-002] [FR-010] complete: Draft `fsgg-quint-profile/1` as a closed demonstrated feature list, explicit-catalogue identity rule, finite-bound separation, deterministic collection/extraction rules, and fail-closed unsupported-construct list.
- PD-011 [AC-001] [FR-011] complete: Draft a small JSON Schema for the compiled contract containing identities, locations, relations, verification profiles, bounds, and digests but no expression/AST slot; validate positive and forbidden-expression fixtures.
- PD-012 [AC-004] [FR-012] complete: Publish an explicit provisional verdict and fingerprint manifest. Acceptance changes no backend and authorizes only the post-Q1 ADR-0077 amendment; Q2 remains separately gated.

## Contract Impact
- PC-001 [PD-002] test-only tool contract: `tests/quint-q1/qualify.sh` accepts exact `LMT_BIN` and `QUINT_BIN` paths, refuses wrong bytes/version, and writes results only to a caller-owned temporary/output directory.
- PC-002 [PD-010] proposed profile: `fsgg-quint-profile/1` is an experiment proposal, not a published contract.
- PC-003 [PD-011] proposed compiled contract: schema v1 is validated as a Q1 candidate and carries no production authority or arbitrary Quint expressions.
- PC-004 [PD-004] cross-repository replay request: the candidate manifest is the immutable input to `EHotwagner/S.I.R.#353`; its response must bind the same fingerprint set.

## Verification Obligations
- VO-001 [PD-001] [PD-002] [PC-001] semanticTest: Run the complete pinned qualification harness twice from clean temporary directories and compare manifests byte-for-byte.
- VO-002 [PD-002] mutationTest: Invert every extraction/source-binding gate and every safety-critical model property; require a non-zero result naming the injected defect.
- VO-003 [PD-006] supplyChain: Recompute upstream commit/tree/file/license digests and prove a moving HEAD/latest-tool substitution is refused.
- VO-004 [PD-011] contractTest: Validate the compiled-contract example and reject missing IDs, duplicate IDs, wrong digests, compiler-node-derived IDs, and any expression/AST field.
- VO-005 [PD-003] independentReview: Obtain distinct domain/readability and architecture/tooling reviews over the exact candidate head.
- VO-006 [PD-004] crossRepoCorrespondence: Require the S.I.R. child to pass real-interpreter replay plus mapping and implementation mutations against the exact producer manifest.

## Performance Intent
- PI-001: Measure cold/warm extraction, typecheck, test, run, and bounded verify wall time independently; Q1 sets observations, not production budgets.
- PI-002: Report canonical Markdown, generated module, IR/ITF, dependency archive, and committed-witness byte counts.

## Migration Posture
- PM-001 [PC-002] diagnoseOnly: Q1 is qualification only. No production manifest, backend, source, or consumer is migrated.
- PM-002 [PC-004] rollback: Refusal or consumer mismatch deletes no historical evidence and leaves `fsharp-specification-v1` authoritative.

## Generated View Impact
- GV-001 [PD-001] workModel: `readiness/922-quint-first-q1/work-model.json` is regenerated from authored sources and must reach `implementationReady` before experiment files are changed.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 922-quint-first-q1`.
