# Feature Specification: The Committed Verdict Discloses Its Attestation Basis

**Feature Branch**: `item/398-evidence-attestation-disclosure`

**Created**: 2026-07-12

**Status**: Draft

**Input**: FS.GG.SDD#398 — "`ship` certifies 5 supported obligations without disclosing that 0 of them
were observed." Child of FS.GG.SDD#350 (`result: pass` is a self-attestation), under `.github` epic
#417 ("The SDD lifecycle fails open — `ship` certifies paperwork, not work"), which extends #266
("Coherence gates that fail open").

## Overview

`ship` reports `shipEvidenceSupported: 5` and commits `"readiness": "shipReady"`. Both are read — by
humans, and by the agents this product exists to drive — as *"5 obligations were proven."* They mean
*"5 obligations were **asserted** by whoever authored `evidence.yml`."*

The gap is total, not partial. SDD invokes **no test runner**: `Process.Start` occurs once in `src/`
(`CommandEffects.fs:213`) and serves only `scaffold`'s provider and `upgrade`'s self-update. An
obligation reaches `"supported"` (`HandlersEvidence.fs:756-758`) and `"satisfied"`
(`HandlersVerify.fs:259-261`) on exactly one condition — the author wrote `result: pass` and did not
disclose it as `synthetic`. Zero supported obligations have ever been observed, in any repo, ever.

This feature does not fix that. It makes it **impossible to misread**.

### Why the disclosure has to be in the *committed* verdict

`.gitignore:28-29` is the load-bearing fact:

```
specs/*/readiness/*/*
!specs/*/readiness/*/ship-verdict.json
```

`verify.json` and `ship.json` are regenerable output and are **git-ignored** (ADR-0018). The one
readiness artifact that survives into git is `ship-verdict.json` — the ADR-0026 compact verdict — and
it carries **no evidence counts at all**. The green that a future reader will find in history is the
bare string `shipReady`.

So a disclosure added only to `ship.json` and the console projections would be *discarded before it
reached git*, missing precisely the artifact whose durability is the entire argument for urgency:

> **ADR-0026 makes the ship verdict durable in git history.** … we are about to permanently record,
> in every repo, a green verdict whose epistemic content is far weaker than any future reader will
> assume. — `.github`#417

A reader in 2027 has no way to recover that context from a repository. The verdict must carry it.

This fits the verdict's own contract rather than straining it. `ShipVerdict.fsi` states that the
projection *"drops **inventory** and no **facts**."* The attestation basis is a fact, not inventory —
and it is the most consequential fact the verdict has.

### Why two counters, not one

#350's option 3 asks for `evidenceSelfAttested: N` beside `shipEvidenceSupported: N`. Taken
literally, those two numbers are **identical today, by construction** — every supported obligation is
self-attested, because nothing is ever observed. A lone counter that always equals its neighbour
reads as redundant, and a reviewer who cannot see why it differs will eventually delete it.

So report the pair, and let an invariant carry the meaning:

```
supported == selfAttested + observed
```

- **`evidenceObservedCount`** — supported obligations resting on a run the tool *observed*.
  Structurally `0` today. **That zero is the disclosure.**
- **`evidenceSelfAttestedCount`** — supported obligations resting on the author's word. Today, all.

Stated this way the numbers explain themselves (`supported: 5, selfAttested: 5, observed: 0`), the
invariant is testable, and when #350's observed-receipt model lands `observed` rises, `selfAttested`
falls, the invariant holds throughout, and **no schema, projection, or consumer changes.**

### What is *not* in scope

`result: pass` remains a self-attestation. This feature observes nothing, runs nothing, and **blocks
nothing**: no disposition, verdict, exit code, or gate changes, and a green `ship` stays green. It
discloses.

Making `ship` mean *"this works"* is FS.GG.SDD#350, it needs the ADR that issue asks for, and that
ADR settles an SDD↔Governance ownership question this feature deliberately does not pre-empt.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The committed verdict says what it is worth (Priority: P1)

A reader — a reviewer at the merge boundary, an auditor, or an agent in a year's time — opens a
committed `ship-verdict.json`. It records `shipReady`. They can now also see that **0 of its
supported obligations were observed**, without knowing anything about SDD's internals.

**Why this priority**: this is the whole feature. Every other surface here is regenerable and
git-ignored; this is the only one that outlives the branch.

**Acceptance**: a `ship-verdict.json` produced by a lifecycle walked on hand-authored evidence
carries `evidenceObservedCount: 0` alongside its `shipReady`.

### User Story 2 - The operator sees it at the merge boundary (Priority: P1)

An author runs `fsgg-sdd ship`. The report tells them, in all three projections, that the green they
are about to act on rests entirely on their own attestation.

**Acceptance**: `--json`, `--text`, and `--rich` each surface all three counters. `--rich` adds and
drops no facts (CLAUDE.md projection rule).

### User Story 3 - The counters are derived, not asserted (Priority: P1)

The day #350 lands, `observed` must rise **without this feature being touched**. If `observed` is a
hardcoded `0`, or `selfAttested` a copy of `supported`, the disclosure becomes a lie the moment it
stops being true — which is worse than not having it.

**Acceptance**: the observation fact travels per-obligation from the declaration through
`verify.json` → `ship.json` → `ship-verdict.json`. Exactly one function in `Artifacts` decides
whether a declaration was observed; every counter is computed from it.

### Edge Cases

- **Deferred / synthetic / stale / invalid / blocking obligations** are not supported, so they are
  neither observed nor self-attested. They contribute to neither counter, and the invariant still
  holds: `supported == selfAttested + observed`.
- **A disclosed synthetic pass** (`result: pass`, `synthetic: true`) reaches `"synthetic"`, not
  `"supported"`. It is already disclosed as unsatisfying and is out of both counters by construction.
- **An old `ship.json` / `verify.json` without the fields** parses to `0` via the existing tolerant
  parse (`Option.defaultValue`), so no schema-version bump is required.
- **Zero obligations**: all three counters are `0`; the invariant holds trivially.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Whether a declaration rests on an **observed** run MUST be decided by exactly one
  function in `FS.GG.SDD.Artifacts`, consumed by every caller — the same "state the rule once"
  discipline FR-007 of feature 099 imposed on the #349 rule, so `evidence`, `verify`, and `ship`
  cannot drift on what "observed" means.
- **FR-002**: That function MUST today return `false` for every declaration, because SDD observes no
  run. It MUST be a function over the declaration, **not** a constant, so that #350's receipt model
  changes one call site and nothing else.
- **FR-003**: The observation fact MUST travel per-obligation on the evidence disposition, from
  `verify.json` (`evidenceDispositions[].observed`) through `ship.json` to `ship-verdict.json` — so
  no consumer re-derives it and no consumer can hardcode it.
- **FR-004**: `verify.json` and `ship.json` MUST carry `evidenceSelfAttestedCount` and
  `evidenceObservedCount` beside the existing `evidenceSupportedCount`. Additive; the tolerant parse
  keeps prior views readable, so `schemaVersion` does **not** change.
- **FR-005**: **`ship-verdict.json` MUST carry all three counts.** This is the only committed
  readiness artifact and the only one a future reader will find in git.
- **FR-006**: All three report projections (`--json`, `--text`, `--rich`) MUST surface the counters
  for both `verify` and `ship`. `--rich` remains a pure projection: it adds and drops no facts and
  changes no JSON byte, exit code, or stream routing.
- **FR-011**: The **`TD-` required-test dispositions MUST be disclosed too**, by the same rule object.
  `verifyTestSatisfied` is the single most misleading number the lifecycle prints — its *name* asserts
  a test was satisfied, and `TD-` reaches `"satisfied"` from the identical `result: pass` check as
  `ED-` (`.github`#417: *"despite the name, it observes no test"*). Disclosing the evidence counters
  while leaving `verifyTestSatisfied` bare would print an honest line directly above a dishonest one,
  which is worse than disclosing neither.
- **FR-007**: The invariant `supported == selfAttested + observed` MUST hold for every produced
  view, and MUST be asserted by a test.
- **FR-008**: This feature MUST change **no** disposition state, verdict state, diagnostic, exit
  code, or blocking behaviour. A lifecycle that shipped green before this change ships green after
  it, with the same `disposition.state`.
- **FR-009**: A test MUST assert that a lifecycle walked on hand-authored evidence reports
  `evidenceObservedCount: 0` — the standing proof that nothing is observed. It is expected to start
  failing, correctly, on the day #350 lands.
- **FR-010**: The new fields MUST be added to the `ReleaseContract` inventory, so
  `ReleaseConformanceTests` ("no undocumented public field, no documented field absent") stays green
  and the published release contract stays honest.

### Key Entities

- **`Evidence.isObserved`** — the one rule (FR-001/FR-002). Today: `false`.
- **`Evidence.obligationIsObserved`** — the rule applied to an obligation's matched declarations, and
  the single function `ED-` and `TD-` both consume (FR-001). Consults only the declarations claiming a
  real pass, and requires **all** of them to be observed: one observed run must not launder a
  hand-asserted pass beside it — a disclosure that *under*-reports self-attestation fails open, which
  is the defect class this sits in. Both choices are moot while `isObserved` is constantly `false`,
  and both are what #350 inherits.
- **`EvidenceDisposition.Observed` / `RequiredTestDisposition.Observed`** — the per-obligation fact,
  carried in `verify.json`/`ship.json`.
- **`ShipVerificationReadinessSummary`** — gains `EvidenceSelfAttestedCount`, `EvidenceObservedCount`.
- **`ShipVerdict.VerificationReadiness`** — the committed disclosure (FR-005).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A `ship-verdict.json` committed by this repo's own dogfooded lifecycle states that 0 of
  its supported obligations were observed.
- **SC-002**: `supported == selfAttested + observed` holds across every golden and every matrix cell.
- **SC-003**: `observed == 0` across the entire repository today — asserted, not assumed.
- **SC-004**: No `disposition.state`, exit code, or diagnostic changes on any existing golden.
- **SC-005**: When #350 lands, making `observed` non-zero requires editing exactly one function.

## Assumptions

- The tolerant `Option.defaultValue` parse on the readiness views means an additive field is
  backward-compatible without a `schemaVersion` bump — the same judgement CLAUDE.md records for
  scaffold provenance's additive `effectiveParameters` ("schema stays v1").
- `ship-verdict.json`'s line-count contract (`ShipVerdict.fsi`: "exactly 20 lines when
  `DispositionBlockingFindingIds` is empty") is a documented property of the current shape, updated
  deliberately here, not an invariant to preserve.

## Out of Scope

- Running a test suite, ingesting a trx/junit, or recording a run receipt (**FS.GG.SDD#350**).
- Any change to Governance's effective-evidence-freshness or gate enforcement.
- Blocking, failing, or downgrading a verdict on the basis of self-attestation. This feature
  discloses; #350 decides what to do about it.
