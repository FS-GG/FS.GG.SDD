---
schemaVersion: 1
workId: 865-record-discharged-obligations
title: Record Discharged Obligations
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Record Discharged Obligations Specification

Prose status: specified

## User Value

A work item whose obligations are discharged by durable records — a decision recorded, a row filed, a
routing performed — can reach `ship`, instead of blocking at `verify` forever because no test run can
ever observe a record.

`verify` can only observe an obligation discharged by a **test run**: `EvidenceDisposition.Observed` has
exactly one true-maker, an `observedRun` receipt parsed from a runner's report. An obligation discharged
by a durable record has no runner report to parse, so `Observed` is `false` by construction,
`verify.unobservedRequiredTest` fires forever, and `ship` is unreachable. The only exits left to a work
item are to fabricate a receipt or to merge with `verify` blocked — and two items have now taken the
second, both disclosing it honestly.

FS.GG.SDD#350 correctly closed the self-attestation hole for tests. It did not create a second, equally
observable channel for records. This specification creates that channel without reopening the first hole.

### The two measured occurrences

| item | posture | unreceipted obligations |
|---|---|---|
| `.github#2380` | `verify` **BLOCKED — `verify.unobservedRequiredTest`**, `ship` not reached, closed `COMPLETED` by merged PR #2550 | 10 of 14 — `EV001`, `EV002`, `EV005`-`EV010`, `EV013`, `EV014`, all documentation and routing claims, typed `kind: review` |
| `.github#2545` | same posture, `ship` not reached | 5 of 24 — `EV007`, `EV009`, `EV011`, `EV013`, `EV024`; four are decisions or routings, one names filed rows |

Both packages rejected the two cheap escapes explicitly — relabelling record obligations `kind:
verification` so an unrelated receipt would attach to them, and widening the check script with legs that
assert *X is recorded* by grepping for X's name. Both are the anti-patterns this repository has measured
repeatedly. The gap is in the gate's vocabulary, and that is what this work closes.

## Scope

- SB-001: Give an evidence obligation a declared **discharge class** — `test` or `record` — give a
  record-class obligation a re-checkable `recordReceipt` channel to `Observed: true`, and narrow
  `verify.unobservedRequiredTest` to the obligations a test run could actually discharge.
- SB-002: Mirror the split at the merge boundary so `ship` reports a record-class shortfall as a record
  shortfall, and re-check the two measured occurrences against the change.

## Non-Goals

- SB-003: Do not dereference a record locator. No command gains network access; the form of a remote
  locator is judged, its content is not fetched.
- SB-004: Do not implement Governance enforcement. SDD reports whether an obligation was discharged and
  by what class of evidence; whether that suffices to cross a merge boundary stays Governance's question
  (ADR-0035 §3).
- SB-005: Do not move a persisted `schemaVersion`, and do not change any existing package's verdict.
- SB-006: Do not add a spec-level requirement classification facet and do not change the task generator.
  The discharge class rides the authored task capability tag the generator already preserves.
- SB-007: Do not add a CLI flag. The record channel is authored in `evidence.yml`, because a record is
  authored by nature — there is no runner to read it from.

## User Stories

- US-001 (P1): As a lifecycle author whose obligation is discharged by a durable record, I can declare that class and name the record, so `verify` and `ship` judge my work against evidence that can actually exist.
- US-002 (P2): As a reviewer, I can tell from the committed `verify`/`ship` views which obligations rest on an observed run and which rest on a durable record, and re-check the named record myself.
- US-003 (P2): As the maintainer of a package authored before this channel existed, my committed evidence keeps parsing and keeps the verdict it had.

## Acceptance Scenarios

- AC-001 [US-001] [FR-001]: Given a task tagged with the `record-discharge` capability, when `verify` runs, then the obligation minted from it carries discharge class `record` and both the evidence and required-test dispositions it writes record that class.
- AC-002 [US-001] [FR-002]: Given a record-class obligation whose every real pass carries a coherent `recordReceipt`, when `verify` runs, then its evidence disposition is `supported` with `observed: true` and its required-test disposition is `satisfied`.
- AC-003 [US-001] [FR-003]: Given a `recordReceipt` whose `kind` is unrecognized, whose `locator` is ill-formed for its kind, whose `locatorContract` is not `durable-locator-v1`, or whose `statement`/`recordedAt` is blank or unparseable, when `verify` runs, then the obligation is `invalid` and `evidence.recordReceiptInvalid` names it.
- AC-004 [US-001] [FR-004]: Given a `decision` receipt naming a repository-relative path, when that file's bytes no longer hash to the recorded digest, then the obligation is `invalid` and `evidence.recordReceiptStale` names it; and when the file is absent, the existing `evidence.artifactNotFound` cascade names it.
- AC-005 [US-002] [FR-005]: Given a record-class obligation resting on `result: pass` with no receipt at all, when `verify` runs, then it does not satisfy — the author's word alone remains insufficient.
- AC-006 [US-002] [FR-006]: Given a record-class obligation lacking its record and a test-class obligation lacking its run in the same package, when `verify` runs, then `verify.unrecordedRequiredRecord` names only the first and `verify.unobservedRequiredTest` names only the second.
- AC-007 [US-002] [FR-007]: Given a `verify.json` in which a record-class obligation is supported but unobserved, when `ship` runs, then `ship.unrecordedEvidence` names it rather than `ship.unobservedEvidence`.
- AC-008 [US-002] [FR-008]: Given a record-class obligation whose only receipt is an `observedRun`, and a test-class obligation whose only receipt is a `recordReceipt`, when `verify` runs, then neither is discharged.
- AC-009 [US-003] [FR-009]: Given an `evidence.yml` and a `verify.json` authored before this channel existed, when they are parsed and re-verified, then they parse without diagnostics, keep their prior verdict, and no persisted `schemaVersion` has moved.
- AC-010 [US-003] [FR-010]: Given any command run against a package carrying `issue` and `commit` receipts, when it runs with no network available, then it completes with the same result it has with one.
- AC-011 [US-003] [FR-011]: Given `docs/release/schema-reference.md`, when a reader consults it for how an obligation is discharged, then it describes the record channel and states the actual default for requiring an observed run.

## Functional Requirements

- FR-001: An evidence obligation carries a declared discharge class — `test` (the default) or `record`, taken from the authored `record-discharge` task capability tag — and `verify` records that class on both the evidence and required-test dispositions it writes. (Stories: US-001; Acceptance: AC-001)
- FR-002: A record-class obligation whose matching real passes each carry a coherent `recordReceipt` reaches `Observed: true`, on the same footing an `observedRun` receipt gives a test obligation, and therefore reaches `satisfied` at `verify` and passes `ship`. (Stories: US-001; Acceptance: AC-002)
- FR-003: A `recordReceipt` is coherent only when its `kind` is one of `decision`, `issue`, `commit`; its `locator` is well-formed for that kind; its `locatorContract` is exactly `durable-locator-v1`; and it carries a non-blank `statement` and a parseable ISO-8601 `recordedAt`. An incoherent receipt makes its obligation `invalid`, never merely unobserved. (Stories: US-001; Acceptance: AC-003)
- FR-004: A `decision` receipt names a lexically contained, repository-relative path and binds that file's exact bytes with a `sha256:` digest; the path is a cited artifact, so a deleted record turns the obligation `invalid` through the existing cascade and an edited record turns the receipt stale. (Stories: US-001; Acceptance: AC-004)
- FR-005: A record-class obligation resting on `result: pass` with no receipt does not satisfy; the record channel adds a second observable footing, not a second place to type a pass. (Stories: US-002; Acceptance: AC-005)
- FR-006: `verify.unobservedRequiredTest` is raised only for test-class obligations; a record-class obligation lacking its record reaches the non-satisfying disposition `unrecorded` and raises `verify.unrecordedRequiredRecord`, which names the obligations and what is missing. (Stories: US-002; Acceptance: AC-006)
- FR-007: `ship` mirrors the split at the merge boundary over the record `verify` wrote: `ship.unrecordedEvidence` for record-class obligations, `ship.unobservedEvidence` for test-class ones. (Stories: US-002; Acceptance: AC-007)
- FR-008: The discharge rule is kind-directed and stated once for `verify`, `ship`, and the committed verdict: a `recordReceipt` never discharges a test-class obligation and an `observedRun` never discharges a record-class one. (Stories: US-002; Acceptance: AC-008)
- FR-009: The channel is additive. An `evidence.yml`, `verify.json`, or `ship-verdict.json` written before it parses unchanged, keeps its verdict, and no persisted `schemaVersion` moves. (Stories: US-003; Acceptance: AC-009)
- FR-010: No command dereferences a record locator, and the record channel introduces no network access anywhere in SDD. (Stories: US-003; Acceptance: AC-010)
- FR-011: `docs/release/schema-reference.md` documents the record channel and states the actual, 0.14.0-inverted default for requiring an observed run, replacing the stale description of it as opt-in. (Stories: US-003; Acceptance: AC-011)

## Ambiguities

- AMB-001: Should a record receipt be verified **live** — dereferencing the locator — or only **well-formed and re-checkable**? The item leaves this open and names the hermeticity cost concretely. Decided at clarify.
- AMB-002: May a record-class obligation also be discharged by an `observedRun`, and a test-class one by a `recordReceipt`? Decided at clarify.
- AMB-003: How does an obligation come to be record-class — a new spec classification facet, or the existing authored task capability tag? Decided at clarify.

## Public Or Tool-Facing Impact

- This specification is an SDD lifecycle artifact and command-report contract input.
- `EvidenceDeclaration` gains an optional `recordReceipt`, and `EvidenceObligation` a `DischargeClass`.
  Both are additive public surface on `FS.GG.SDD.Artifacts`, requiring committed `docs/api-surface`
  baselines and a minor version bump under the versioning policy.
- `verify.json` and the `ship` views gain an additive `recordRequirement` boolean per disposition,
  absent-reads-`false`, with no `schemaVersion` movement.
- Four new diagnostics enter the command-report contract: `verify.unrecordedRequiredRecord`,
  `ship.unrecordedEvidence`, `evidence.recordReceiptInvalid`, `evidence.recordReceiptStale`.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 865-record-discharged-obligations`.
