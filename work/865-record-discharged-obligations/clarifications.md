---
schemaVersion: 1
workId: 865-record-discharged-obligations
title: Record Discharged Obligations
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/865-record-discharged-obligations/spec.md
publicOrToolFacingImpact: true
---

# Record Discharged Obligations Clarifications

## Source Specification
- work/865-record-discharged-obligations/spec.md

## Clarification Questions
- CQ-001 [AMB:AMB-001] blocking answered: Should a record receipt be verified live — dereferencing the locator — or only well-formed and re-checkable?
- CQ-002 [AMB:AMB-002] blocking answered: May a record-class obligation also be discharged by an `observedRun`, and a test-class obligation by a `recordReceipt`?
- CQ-003 [AMB:AMB-003] blocking answered: How does an obligation come to be record-class — a new spec classification facet, or the existing authored task capability tag?

## Answers
- CQ-001 → Well-formed and re-checkable, never dereferenced; and where the record is repository-local, additionally byte-bound. The hermeticity cost of the alternative was measured, not assumed: `.github#2545`'s `verification/run-checks.sh` contains no network call of any kind — the property its VO-005 asserts, and the reason its `fixture` CI job needs no token. A live-dereferencing receipt would trade every other obligation's offline reproducibility, in every consuming repository, for one obligation's stronger check. It would also be the weaker trade than it looks: dereferencing proves a locator resolves, not that the resolved artifact says what the receipt claims, so the reader still has to look.
- CQ-002 → No, in both directions. The discharge rule is kind-directed.
- CQ-003 → The existing authored task capability tag, `record-discharge`, carried in `requiredSkills`.

## Decisions
- **DEC-001** [CQ-001] [AMB:AMB-001] [FR-003] [FR-004] [FR-010] [AC-003] [AC-004] [AC-010]: A record receipt is validated for **form and re-checkability, never dereferenced**. `kind`, `locator`, `locatorContract`, `statement` and `recordedAt` are judged structurally; a `decision` receipt — the case where the record is repository-local — is additionally bound to the file's exact bytes by `sha256:` digest and probed for existence through the cited-artifact cascade that already exists. An `issue` or `commit` locator is judged for form only and committed verbatim into `verify.json` and the ship verdict, so a later reader — human, or CI holding a credential SDD does not — can re-check it out of band. SDD gains no network access.
- **DEC-002** [CQ-002] [AMB:AMB-002] [FR-008] [AC-008]: The discharge rule is **kind-directed and fail-closed**. A `recordReceipt` never discharges a test-class obligation, and an `observedRun` never discharges a record-class one. One shared function answers "was this obligation discharged?" for `verify`, `ship`, and the committed verdict, so the three cannot drift — the same discipline `obligationIsObserved` already imposes. Accepting either receipt for either class would let an author attach whichever receipt they happen to hold to whichever obligation is blocking, which is the laundering `obligationIsObserved`'s `forall` exists to prevent.
- **DEC-003** [CQ-003] [AMB:AMB-003] [FR-001] [AC-001]: The class rides the **authored `record-discharge` task capability tag** in `tasks.yml` `requiredSkills`, exactly as `visual-inspection` does. `requiredSkills` is authored state that the task generator unions across regeneration (FS.GG.SDD#310, AC7), so the tag survives a `tasks` re-run. This adds no spec classification facet, does not touch `TaskGraphAuthoring`, and keeps the declaration where the author already declares what a task needs.

## Accepted Deferrals
No accepted deferrals recorded.

## Remaining Ambiguity
- None. AMB-001, AMB-002 and AMB-003 resolved above.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 865-record-discharged-obligations`.
