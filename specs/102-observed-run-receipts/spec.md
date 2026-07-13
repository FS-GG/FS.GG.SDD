# Feature Specification: Observed Run Receipts

**Feature Branch**: `item/350-observed-run-receipts`

**Created**: 2026-07-13

**Status**: Draft

**Input**: FS.GG.SDD#350 — "`result: pass` is a self-attestation — no stage ever observes a test run,
so `ship` certifies paperwork, not work." Child of `.github` epic #266 ("Coherence gates that fail
open"), instance (j), via #417. Governed by **ADR-0035** (`observed-run-receipts`). Origin: the
TankSim1 field report, §3.1.

## Overview

For the single most consequential fact in the lifecycle — **did this actually pass?** — the authoring
agent is the source of truth, and nothing cross-checks it. Evidence reaches `supported` (and the
`TD-` mirror reaches `satisfied`) solely because a human or an agent typed `result: pass` into a file
it also authored. SDD invokes no test runner, and no evidence field carries a run receipt.

ADR-0035 settles the shape:

> **SDD ingests an observed run receipt. SDD never runs a test.**

This feature is the **record** half of that decision — the half spec 101 (feature #398) explicitly
built its seam for:

> when #350's observed-receipt model lands, `observed` rises, `selfAttested` falls, the invariant
> holds throughout, and **no schema, projection, or consumer changes.**

`Evidence.isObserved` was, until this feature, a function that returned the constant `false` —
documented as "the one place that learns to say `true`". This feature teaches it, by giving it a
receipt to read.

### What "observed" buys, stated honestly

An agent can fabricate a TRX file. **This feature does not make evidence unforgeable, and must not be
sold as if it did** — that would be the same overclaim in a new place (ADR-0035, "What this does not
claim").

It moves the bar from **assertion** to **artifact**: from a word typed in a file the agent authored,
to a structured report, of a declared format, whose bytes are hashed, whose counts must be internally
consistent, and whose file must exist on disk (feature 099 / #349 already enforces the last).
Forging that is a deliberate act rather than the path of least resistance — which is exactly what
`result: pass` is today.

Whether to trust a receipt's *provenance* is CI's job, and whether an unobserved obligation may cross
a merge boundary is **Governance's** (ADR-0035 §3). SDD makes the receipt exist, parse, and be
checked. It reports the fact; it does not enforce a verdict.

### What is *not* in scope — and why the ladder is untouched

ADR-0035 stages its own migration, deliberately, so the org is not stopped dead:

1. **Disclose** — `evidenceSelfAttested: N` beside `shipEvidenceSupported: N`. **Landed** (#398/#399).
2. **Warn** — a receipt can be recorded, so `observed` becomes a real number. **This feature.**
3. **Fail closed** — a real pass with no receipt stops satisfying. **Not this feature.**

So this feature changes **no disposition, no severity, no exit code, and no gate**. A real pass with
no receipt still reaches `supported`/`satisfied`, exactly as today; it is simply now *counted* as
self-attested against a number that can finally differ. `shipReady` for existing work items is
byte-for-byte unchanged.

That restraint is the point. Stage 3 turns every work item that reports ship-ready today into one
that does not — it is a breaking change to the evidence contract, gated by ADR-0035 on a schema major
and "once the fleet is green". A fleet cannot *get* green until receipts can be recorded, which is
what this feature delivers. Landing 3 before 2 would stop the org dead with no remedy available.

The failure-leg test #350 and #266 demand — a lifecycle walked on scaffolding with fabricated evidence
must not reach `shipReady` — is therefore stage 3's acceptance, not this feature's. This feature's
failure legs are the ones it *can* honestly assert: an unparseable report, a self-inconsistent report,
and a report whose file is not there are all refused.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A recorded receipt is read from a report, not typed by an agent (Priority: P1)

An author (or agent) runs the suite, producing a TRX or JUnit XML report. They point the lifecycle at
it: `fsgg-sdd evidence --work <id> --from-test-report artifacts/test-results.trx`. SDD **opens the file**,
parses it, hashes its bytes, and records an `observedRun` receipt on the test obligations it
discharges.

**Why this priority**: this is the feature. Without it, `Evidence.isObserved` is constantly `false`
and every counter #398 built is a disclosure of a gap nobody can close.

**Acceptance**

- Given a TRX report with 1630 passed / 0 failed, when `evidence --from-test-report` runs, then each
  `verification` declaration claiming a real pass carries `observedRun` with `source`, a
  `sha256:` `digest` of the report's bytes, `outcome: passed`, and the counts.
- The `digest` is computed by SDD over the report's bytes — it is not read from any authored field
  and cannot be supplied by the author.
- Given the same report, re-running `evidence --from-test-report` is idempotent: byte-identical output.
- A JUnit XML report (`<testsuites>` or a bare `<testsuite>`) records an equivalent receipt.

### User Story 2 - `observed` becomes a real number, and nothing else moves (Priority: P1)

The obligations discharged by that run now report `observed`. The ones with no receipt report
`selfAttested`. `verify.json`, `ship.json`, and the committed `ship-verdict.json` all show it —
without a schema, projection, or consumer changing, because #398 built them to derive rather than
assert.

**Why this priority**: spec 101 US3 made this a standing obligation on *this* feature — "the day #350
lands, `observed` must rise **without this feature being touched**." It is the test of whether the
#398 disclosure was real or decorative.

**Acceptance**

- Given 5 supported obligations of which 3 carry a passing receipt, `verify` reports
  `verifyEvidenceSupported: 5`, `verifyEvidenceObserved: 3`, `verifyEvidenceSelfAttested: 2`.
- The invariant `supported == selfAttested + observed` holds in `verify.json`, `ship.json`, and the
  committed ship verdict.
- `readiness` / `shipReady` is unchanged from the same fixture without receipts (stage 2 does not
  gate).
- No file under `src/FS.GG.SDD.Commands/CommandSerialization.fs` or the ship-verdict writer needs a
  new field for `observed` to rise.

### User Story 3 - A report that cannot be believed is refused, not recorded (Priority: P1)

The receipt is only worth what its parse is worth. A report SDD cannot parse, or one that contradicts
itself, must be a **blocking diagnostic** — never a silently-absent receipt, and never a recorded one.

**Why this priority**: a gate that degrades to "no receipt" on a malformed report fails open in a new
place. #266's rule is that a gate fails *closed* when its subject is unreachable.

**Acceptance**

- An unparseable / non-XML / unrecognised-root report → `evidence.testReportUnparseable`, blocking,
  naming the path. No receipt is recorded.
- A self-inconsistent report (a recorded `outcome: passed` alongside `failed > 0`, a negative count,
  or a malformed digest) → `evidence.observedRunInconsistent`, blocking, naming the declaration.
- A `--from-test-report` path that is not on disk → blocking (`evidence.testReportNotFound`); the
  run is not silently treated as absent.
- A `--from-test-report` path that escapes the repository (absolute, or carrying `..`) → blocking
  (`evidence.testReportPathEscape`), and **no read effect is planned at all** — the lexical
  containment rule already carried by `Evidence.citedPathIsContained` (#359/#365, and `surface`'s
  `rootEscape`).

### User Story 4 - The receipt's source is itself compared against reality (Priority: P2)

A receipt names a report path. Feature 099 (#349) already refuses a satisfying declaration whose
cited artifact is not on disk. The receipt's `source` is such a path, so a receipt whose report was
deleted after recording is caught at `verify` — the merge boundary — not merely at authoring time.

**Acceptance**

- `Evidence.citedArtifactPaths` includes `observedRun.source`, so `missingCitedArtifacts` probes it
  and the existing `evidence.artifactNotFound` cascade fires for a deleted report.
- This requires no new gate: `ED-` and `TD-` inherit it through the shared rule object.

## Requirements *(mandatory)*

- **FR-001**: `EvidenceDeclaration` gains an optional `ObservedRun` record — `source`, `digest`,
  `outcome`, `passed`, `failed`, `skipped`. Additive; an evidence.yml without it parses and renders
  exactly as today. (covers AC-001)
- **FR-002**: The `observedRun` field is driven by the **shared `EvidenceCodec` field list**
  (ADR-0002 invariant 1), so it cannot be read without being written or vice versa. (covers AC-001)
- **FR-003**: SDD parses **TRX** and **JUnit XML** — the formats the org's runners already emit
  (ADR-0035 open question, resolved to its stated leaning). Parsing is **pure** and operates on the
  report's *text*: the read is a `ReadFile` effect interpreted at the edge, and `Artifacts` performs
  no I/O (Constitution V). (covers AC-002)
- **FR-004**: The receipt's `digest` is `sha256:<hex>` computed by SDD over the report text via the
  existing `SchemaVersion.sha256Text` (which normalises CRLF→LF, keeping the digest platform-stable).
  It is never authored. (covers AC-002)
- **FR-005**: `outcome` is **derived from the parsed counts** (`failed = failures + errors`;
  `passed` iff `failed = 0`), so a recorded receipt cannot be self-inconsistent by construction. The
  consistency rule is nonetheless enforced on *read*, because an authored evidence.yml can carry a
  hand-written receipt. (covers AC-005)
- **FR-006**: `Evidence.isObserved` returns `true` for a declaration carrying an `observedRun` with
  `outcome: passed` and `failed = 0`, and `false` otherwise. It remains a total, I/O-free function
  over the declaration — the single rule object `verify`, `ship`, and the committed verdict all read.
  (covers AC-003)
- **FR-007**: `evidence --from-test-report <path>` records the receipt on every declaration that is
  kind `verification` **and** claims a real pass (`result: pass`, `synthetic: false`). Other kinds
  (`implementation`, `review`, `deferral`, `synthetic`, `note`) stay authored: judgement is not
  observable, and pretending otherwise re-creates the ceremony problem #351 names. (covers AC-004)
- **FR-008**: An unparseable report emits blocking `evidence.testReportUnparseable`; an absent one
  `evidence.testReportNotFound`; a self-inconsistent receipt `evidence.observedRunInconsistent`; a
  path escaping the repository `evidence.testReportPathEscape` (and plans **no** read effect at all).
  No receipt is recorded in any of these cases. (covers AC-005)
- **FR-011a**: A report recording **no executed tests** (`passed + failed = 0`) is refused, and an
  authored receipt claiming one is blocked. A run in which nothing executed proves nothing, and
  deriving `outcome: passed` from `failed = 0` would otherwise hand `observed` status to an
  empty TRX (every test filtered out), a JUnit root whose children carry no counts, or an
  all-skipped suite — rebuilding the fail-open one level down. `skipped` is not execution.
  (covers AC-010)
- **FR-011**: A report that **parses and records failures**, while an obligation claims `result:
  pass`, emits blocking `evidence.observedRunFailed` and records nothing. The artifact and the claim
  contradict each other, and the artifact is the one nobody typed. This gates nothing that exists
  today — no evidence.yml in the fleet carries a receipt — so it is a new refusal that cannot
  regress an existing work item. (covers AC-008)
- **FR-012**: The receipt takes its **own flag**, `--from-test-report`. `--from-tests` (feature 077)
  names *where the tests live* — a project path seeded onto scaffolded obligations, and committed
  tests pass it a **directory**. ADR-0035 proposed reusing it, having read `HandlersEvidence`'s
  "stamps the path string into `sourceRefs`" as evidence that it already took a report path; it does
  not. Overloading one flag with both meanings would turn a documented feature-077 invocation into a
  blocking `testReportUnparseable`. (covers AC-009)
- **FR-009**: `Evidence.citedArtifactPaths` includes `observedRun.source`, so the feature-099
  existence cascade probes the report and refuses a receipt whose report has been deleted. (covers
  AC-006)
- **FR-010**: **No disposition state, severity, readiness value, or exit code changes.** A real pass
  with no receipt still reaches `supported`/`satisfied`. The invariant
  `supported == selfAttested + observed` continues to hold. (covers AC-007)

## Acceptance Criteria

- **AC-001**: An `observedRun` round-trips through the codec byte-identically; an evidence.yml
  without one is unchanged.
- **AC-002**: A TRX and a JUnit report each parse to the correct counts, with a `sha256:` digest SDD
  computed over the bytes.
- **AC-003**: `isObserved` is `true` exactly for a passing receipt, and `obligationIsObserved`
  refuses to let one observed declaration launder a hand-asserted pass beside it.
- **AC-004**: `evidence --from-test-report` stamps receipts on real-pass `verification` declarations and on
  nothing else; the run is idempotent.
- **AC-005**: Unparseable, self-inconsistent, escaping, and absent reports each block, and record
  nothing.
- **AC-006**: A deleted report makes its obligation `invalid` at `verify` via the existing
  `evidence.artifactNotFound` cascade.
- **AC-007**: `verifyEvidenceObserved` rises and `verifyEvidenceSelfAttested` falls on a fixture with
  receipts, with `supported == selfAttested + observed` holding, and `readiness` unchanged.
- **AC-008**: A report recording failures, beside an obligation claiming a pass, blocks and records
  nothing.
- **AC-009**: `--from-tests` continues to accept a test *directory* and record no receipt;
  `--from-test-report` is accepted by the real CLI and records one.
- **AC-010**: A report with zero executed tests is refused; an authored zero-run receipt blocks; a
  real run that merely skips most of its suite is still recorded.

## Out of Scope

- Running a test suite. SDD never invokes a runner (ADR-0035, rejected alternative 1).
- The `unobserved` **non-satisfying** disposition, the fail-closed flip, the schema major, and the
  fabricated-lifecycle failure-leg test — all stage 3.
- The `governance-handoff` registry bump. ADR-0035 ties that to the disposition change ("gains the
  `unobserved` disposition"); this feature adds no disposition, so the contract surface is unchanged.
- Governance's enforcement policy and receipt freshness (ADR-0035 §3 — explicitly Governance-owned).
- Receipts for non-test obligations (`visual-inspection`, contract impact) — ADR-0035 open question,
  left authored.
