# Feature Specification: An Unobserved Pass Fails Closed

**Feature Branch**: `item/350-unobserved-disposition`

**Created**: 2026-07-14

**Status**: Draft

**Input**: FS.GG.SDD#350 — "`result: pass` is a self-attestation — no stage ever observes a test run,
so `ship` certifies paperwork, not work." Child of `.github` epic #266 ("Coherence gates that fail
open"), instance (j). Governed by **ADR-0035** (`observed-run-receipts`). Follows spec 101 (disclose,
#398) and spec 102 (record, #415).

## Overview

ADR-0035 stages the fix for #350 in three, and names them:

1. **Disclose** — `evidenceSelfAttested: N` beside `shipEvidenceSupported: N`. Landed (#398 / spec 101).
2. **Record** — `evidence --from-test-report` parses a runner-produced TRX/JUnit report and records an
   `observedRun` receipt, so `observed` stops being structurally zero. Landed (#415 / spec 102).
3. **Fail closed** — a `result: pass` carrying no receipt **stops satisfying**. This feature.

Stage 3 is the one #350's acceptance actually demands:

> `result: pass` on a test obligation cannot reach `"satisfied"` without an artifact the tool
> **observed** rather than was **told about**.

### Why this ships opt-in, and why that is not a dodge

ADR-0035 gates the flip on two conditions, and one of them is **false today**:

> **Fail closed** — `unobserved` stops satisfying. Flipped **once the fleet is green**, on a schema
> major.

The fleet is not green. **No `evidence.yml` in this repository carries a receipt** — not one — and the
same holds across the org. Defaulting the gate on would turn every ship-ready work item in every FS-GG
repo not-ship-ready *simultaneously*, with the remedy (record a receipt) not yet performed anywhere.
That is "stopping the org dead", which is precisely what ADR-0035's staged migration exists to avoid.

So this feature lands the **mechanism** and its **proof**, behind `--require-observed`, default off:

- With the flag absent, behavior is **byte-for-byte** what it was. The new disposition is unreachable.
- With the flag present, the gate is complete and fails closed — and #266's demanded failure-leg test
  proves it on a fabricated lifecycle.

Flipping the default then becomes a one-line change a **human** makes, on a schema major, once
receipts are actually being recorded — with the whole decision, and a test that goes deliberately red,
written down beside it. **An agent must not make that call**, and the irony of an agent unilaterally
deciding the merge-boundary policy in the very issue about agents being the source of truth for the
merge-boundary verdict is not lost on this spec.

### What this buys, stated honestly

An agent can still fabricate a TRX. **This does not make evidence unforgeable and must not be sold as
if it did** (ADR-0035, "What this does not claim"). It moves the bar from an assertion to an artifact:
forging it is now a deliberate act rather than the path of least resistance.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A fabricated lifecycle cannot reach `shipReady` (Priority: P1)

The #350 probe: a lifecycle walked on scaffolding, `result: pass` / `synthetic: false` on every
obligation, nothing ever run. Under `--require-observed` it must be **refused** — at `verify`, and at
the merge boundary that actually matters.

**Why this priority**: it is the issue's acceptance, and #266's standing note is that *a fix whose
failure leg is untested is how this class of defect survives*.

### User Story 2 - Work that DID run its suite still ships (Priority: P1)

An obligation carrying a passing `observedRun` receipt reaches `satisfied` and `shipReady` exactly as
before. A gate that blocks everything is not a gate, it is an outage.

### User Story 3 - The default is unchanged, and provably so (Priority: P1)

The same fabricated work item that US1 refuses sails through when nobody passes the flag. This is
uncomfortable and deliberate: it is the ADR's staged migration, and an undeclared default flip would
be exactly the breakage it staged around.

### User Story 4 - A stale green record cannot launder an unobserved pass (Priority: P1)

A blocked `verify` writes nothing — an incomplete run never reports complete — so it leaves the
*previous*, green, still-digest-current `verify.json` on disk. A `ship` that trusted
`verificationReady` alone would certify a lifecycle `verify` had **just refused**.

**Why this priority**: this is #266's own rule (*compare against reality, not a record of reality*)
failing **inside the fix for #266**. It was invisible to a fully green test suite and was caught by
driving the real CLI. It is the reason the flag is on *both* stages.

### User Story 5 - An honest deferral is not punished (Priority: P2)

The gate says "no run was observed", **not** "you are lying". A deferral claims no pass, and a
disclosed `synthetic` already discloses that nothing proved it. Neither asserts a run; neither may be
caught failing to evidence one.

## Requirements *(mandatory)*

- **FR-001**: `verify` MUST accept `--require-observed`. Default false.
- **FR-002**: Under the flag, a test obligation whose `result: pass` is not backed by a passing
  `observedRun` receipt MUST reach a new, non-satisfying disposition `unobserved` rather than
  `satisfied`, and MUST raise the blocking `verify.unobservedRequiredTest`.
- **FR-003**: `unobserved` MUST carry severity `blocking`, so it drives
  `readiness: needsVerificationCorrection`. A warning that still satisfies is the disclosure already
  shipped in #398, not a gate.
- **FR-004**: The observation rule MUST be the shared `Evidence.obligationIsObserved` — consumed, not
  restated — so `ED-`, `TD-`, `ship`, and the committed verdict cannot drift on what "observed" means.
  Its `forall`-over-real-passes reading is inherited deliberately: one observed run beside one
  hand-asserted pass is **not** observed.
- **FR-005**: `ship` MUST accept `--require-observed` and, under it, refuse to certify obligations the
  verify record reports as supported-but-unobserved (`ship.unobservedEvidence`, blocking). It gates on
  the record `verify` wrote; it never re-derives the rule.
- **FR-006**: With the flag absent on a command, that command's behavior — JSON bytes, exit code,
  readiness, counters — MUST be identical to its pre-feature behavior.
- **FR-007**: A disclosed `synthetic` pass and an honest deferral MUST NOT reach `unobserved`.
- **FR-008**: The `unobserved` disposition value is **additive** to the `governance-handoff` surface
  and obliges a **minor** bump of the `governance-handoff` contract in the org registry
  (`registry/dependencies.yml`, owned by `.github`), then a `docs/architecture.md` §5 reconcile —
  per ADR-0035's registry obligation. Filed cross-repo; it must land before the default flips.

## Acceptance Criteria

- **AC-001** (US1, FR-002/003/005): A work item with five obligations claiming `result: pass` /
  `synthetic: false` and no receipt, run under `--require-observed`, reports
  `needsVerificationCorrection`, `testSatisfied: 0`, raises `verify.unobservedRequiredTest`, and does
  **not** reach `shipReady`.
- **AC-002** (US2): The same work item, after `evidence --from-test-report` records a passing receipt,
  reports `verificationReady` and `shipReady` under the flag.
- **AC-003** (US3, FR-006): Without the flag, that same fabricated work item still reports
  `verificationReady` and `shipReady`, with `observed: 0`.
- **AC-004** (US4, FR-005): A green `verify.json` written by an earlier unflagged run does **not**
  allow `ship --require-observed` to reach `shipReady`.
- **AC-005** (US5, FR-007): An honest, fully-fielded deferral is not named by
  `verify.unobservedRequiredTest`.

## Out of Scope

- **Flipping the default.** A human does that, on a schema major, once the fleet records receipts.
  This feature deliberately leaves the defect reachable by default and says so out loud.
- **SDD running a suite.** ADR-0035 rejected it: toolchain knowledge must not enter generic SDD.
- **Deciding what an unobserved obligation *costs* at a merge boundary.** That is Governance's
  (ADR-0035 §3). SDD ships the fact and the switch; it never enforces.
