---
schemaVersion: 1
workId: 865-record-discharged-obligations
title: Record Discharged Obligations
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/865-record-discharged-obligations/spec.md
sourceClarifications: work/865-record-discharged-obligations/clarifications.md
sourceChecklist: work/865-record-discharged-obligations/checklist.md
publicOrToolFacingImpact: true
---

# Record Discharged Obligations Plan

Prose status: planned

## Source Snapshot
- spec: work/865-record-discharged-obligations/spec.md sha256:21d019efa96537b148f56ff75c938544a0604459ef56bd4a1a1d2a84d466acd6 schemaVersion:1
- clarifications: work/865-record-discharged-obligations/clarifications.md sha256:78ecbbb052f7126f8b2e5b29833e544eefa3c3d1f0877067b3b9a18e52733d2d schemaVersion:1
- checklist: work/865-record-discharged-obligations/checklist.md sha256:b53c682c5f858ee157088cead28f8a80cd24f80d44931f0f22208cbb25735ce8 schemaVersion:1

## Plan Scope
- Work item 865-record-discharged-obligations is planned from the current specification, clarification, and checklist facts.
- Requirement count: 11.
- Clarification decision count: 3.
- Checklist result count: 11.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Add `EvidenceObligation.DischargeClass` (`test` | `record`), set from the `record-discharge` capability tag in `EvidenceDomain.obligations`, and carry it as an additive `RecordRequirement` boolean on `EvidenceDisposition` and `RequiredTestDisposition`. This mirrors how `ClassifiedRequirement`/`JourneyRequirement` were added, so `ship` and the Governance handoff read the class off the committed view instead of re-deriving it.
- PD-002 [AC-002] [FR-002] complete: Compute the disposition `Observed` flag through one kind-directed rule, `Evidence.obligationDischarged`, rather than calling `obligationIsObserved` directly at each site. The `ED-` ladder, the `TD-` ladder, `ship`, and the committed verdict all consume that one function.
- PD-003 [AC-003] [FR-003] complete: State the receipt's coherence once as `Evidence.recordReceiptInconsistency`, returning the reason or `None` — the exact shape of `observedRunInconsistency`. An incoherent receipt on a real pass makes its obligation `invalid` (`evidence.recordReceiptInvalid`), never merely unobserved, so a malformed receipt cannot be quietly demoted to "no receipt".
- PD-004 [AC-004] [FR-004] complete: Include a `decision`-kind receipt's locator in `Evidence.citedArtifactPaths`, so a deleted record turns the obligation `invalid` through the existing `evidence.artifactNotFound` cascade with no new gate; and add `recordReceiptIsCurrent` beside `observedRunIsCurrent` so an edited record turns the receipt stale (`evidence.recordReceiptStale`).
- PD-005 [AC-005] [FR-005] complete: `Evidence.isRecorded` is true only for a declaration carrying a coherent receipt, and `obligationIsRecorded` is `forall` over the declarations claiming a real pass — inheriting `obligationIsObserved`'s anti-laundering reading rather than restating it. A pass with no receipt is therefore never recorded.
- PD-006 [AC-006] [FR-006] complete: Split the `TD-` ladder's fail-closed arm in two, each guarded by the obligation's class, placed immediately above `satisfied` where the existing `unobserved` arm sits. A record-class shortfall reaches the new `unrecorded` state and raises `verify.unrecordedRequiredRecord`; a test-class shortfall keeps `unobserved` and `verify.unobservedRequiredTest`.
- PD-007 [AC-007] [FR-007] complete: Partition `ship`'s supported-but-unobserved obligation ids by the `recordRequirement` flag `verify` wrote and raise `ship.unrecordedEvidence` for the record half. `ship` re-asserts the record `verify` wrote rather than re-deriving from `evidence.yml`, which is the existing merge-boundary discipline.
- PD-008 [AC-008] [FR-008] complete: Keep the kind-directed rule in `FS.GG.SDD.Artifacts` and consume it from `Commands`. `isObserved` keeps its current body unchanged, so a `recordReceipt` cannot discharge a test obligation; `isRecorded` is its record twin, so an `observedRun` cannot discharge a record obligation.
- PD-009 [AC-009] [FR-009] complete: Read every new persisted field tolerantly — `jsonBool … |> Option.defaultValue false` for `recordRequirement`, a null-aware lift for `recordReceipt` — and move no `schemaVersion`. A view or evidence file written before this channel parses to exactly the values it already meant.
- PD-010 [AC-010] [FR-010] complete: Add no new effect. Receipt validation is pure over strings; existence and byte-currency reuse the already-injected `artifactExists` / `artifactBytes` probes resolved at the effect edge, exactly as `missingCitedArtifacts` and `observedRunIsCurrent` already do.
- PD-011 [AC-011] [FR-011] complete: Extend `docs/release/schema-reference.md` with the record channel beside the `observedRun` channel, and correct its stale claim that requiring an observed run is opt-in and off by default — `0.14.0` inverted that default and the paragraph was never updated.

## Contract Impact
- PC-001 [PD-001] [PD-011] command report: The `verify` and `ship` command reports gain no new block and no new counter — the record channel is reported through the existing `evidenceObservedCount` / `evidenceSelfAttestedCount` pair and the existing disposition arrays, so every consumer of the JSON contract keeps working unchanged. What does change is which obligations can appear in which state, and that is documented in `docs/release/schema-reference.md` rather than encoded in a new field.
- PC-002 [PD-002] [PD-005] [PD-008] public surface: `FS.GG.SDD.Artifacts` gains the `RecordReceipt` type, `EvidenceDeclaration.RecordReceipt`, `EvidenceObligation.DischargeClass`, and the `recordDischargeCapability` / `isRecordDischargeTagged` / `recordReceiptInconsistency` / `isRecorded` / `obligationIsRecorded` / `obligationDischarged` rules. Additive only: committed `docs/api-surface` baselines must be refreshed and the change is a minor bump under the versioning policy.
- PC-003 [PD-009] persisted artifact: `readiness/<id>/verify.json`, `ship.json` and `ship-verdict.json` gain an additive `recordRequirement` boolean per disposition, and `evidence.yml` an optional `recordReceipt` mapping. No persisted `schemaVersion` moves and no existing field changes meaning.
- PC-004 [PD-003] [PD-006] [PD-007] diagnostics: four ids enter the command-report diagnostic contract — `verify.unrecordedRequiredRecord`, `ship.unrecordedEvidence`, `evidence.recordReceiptInvalid`, `evidence.recordReceiptStale` — each with a remediation pointer.

## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: The obligation minted from a `record-discharge`-tagged task carries discharge class `record`, and both `verify.json` disposition arrays record `recordRequirement: true` for it — asserted over the written view, not over the in-memory draft, because the committed view is what `ship` and the Governance handoff read.
- VO-002 [PD-002] [PC-002] semanticTest: A record-class obligation whose pass carries a coherent receipt reaches `supported` with `observed: true`, its `TD-` mirror reaches `satisfied`, and `ship` certifies it — the positive fixture acceptance criterion 4 requires.
- VO-003 [PD-003] [PC-004] semanticTest: Each coherence branch is exercised by its own case — unrecognized `kind`, ill-formed `locator` per kind, wrong `locatorContract`, blank `statement`, unparseable `recordedAt` — and each is refused as `invalid` rather than silently unobserved.
- VO-004 [PD-004] [PC-002] semanticTest: Mutating the record file turns the receipt stale and the obligation `invalid`; deleting it raises `evidence.artifactNotFound`. Both are the negative fixture that proves the gate can fail.
- VO-005 [PD-006] [PD-007] [PC-004] semanticTest: With one record-class and one test-class obligation both unmet in the same package, `verify.unrecordedRequiredRecord` names only the record obligation and `verify.unobservedRequiredTest` only the test obligation; `ship` partitions the same way.
- VO-006 [PD-009] [PC-003] semanticTest: A pre-channel `evidence.yml` and a pre-channel `verify.json` parse without diagnostics, read `recordRequirement` as `false`, and keep the verdict they had.
- VO-007 [PD-010] [PD-011] [PC-001] documentationReview: The change adds no effect type and no network call, and `docs/release/schema-reference.md` states the record channel and the actual observed-run default.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] [PC-004] diagnoseOnly: The four new diagnostics are additive ids, not replacements. `verify.unobservedRequiredTest` and `ship.unobservedEvidence` keep their ids, their severities and their exact wording for the test-class obligations they always described, so a consumer matching on either id sees no behaviour change for the population it already matched. Nothing renames, so no consumer migration is owed.
- PM-002 [PC-003] diagnoseOnly: A view or evidence file written before the record channel is read, not migrated — an absent `recordRequirement` is `false` and an absent `recordReceipt` is `None`, which is what each already meant. No re-sync command is introduced and no committed package needs one.

## Generated View Impact
- GV-001 [PD-001] [PD-003] workModel: The normalized work model gains nothing. Discharge class is derived at obligation-minting time from `requiredSkills`, which the work model already carries verbatim, so no generated view acquires a new field and none of the committed work models in this repository go stale on this change.
- GV-002 [PD-009] verifyView: readiness/865-record-discharged-obligations/verify.json and ship.json are regenerated by this work item's own back half, and are themselves the demonstration that a record-discharged package reaches ship.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.
- This package dogfoods its own change: several of its obligations are record-discharged, so its own `verify`/`ship` run is evidence for FR-002 as well as a lifecycle gate.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 865-record-discharged-obligations`.
