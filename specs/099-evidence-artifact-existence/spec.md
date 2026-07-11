# Feature Specification: Cited Evidence Artifacts Must Exist

**Feature Branch**: `item/349-evidence-artifact-existence`

**Created**: 2026-07-11

**Status**: Draft

**Input**: FS.GG.SDD#349 — "evidence: `artifacts:` paths are never existence-checked — a green
`ship` can cite a file that does not exist." Child of `.github` epic #266 ("Coherence gates that
fail open"), instance (j). Origin: the TankSim1 field report, §3.1.

## Overview

Nothing in the lifecycle ever touches a path named by an evidence declaration. An entry with
`result: pass`, `synthetic: false`, and `artifacts: [tests/ThisFileDoesNotExist.fs]` passes
`evidence`, `verify`, and `ship` with zero diagnostics. Each stage declines the check by pointing at
the other: `HandlersEvidence.fs:255-259` says on-disk existence "is a verify-stage concern"; verify
reads only `Result`/`Synthetic`/`SyntheticDisclosure` and never looks. The handoff is documented and
lands nowhere.

The feature is not merely unchecked — it is **vacuous**. A census of every artifact citation in this
repository at `d21774d` found **29 cited paths, of which 29 do not exist**. Zero. That census
includes `docs/examples/lifecycle-artifacts/evidence.yml`, the corpus this product *publishes to
teach authors how to declare evidence*, and two committed, passing tests
(`EvidenceCommandTests.fs:1184` and `:1206`) whose fixture never writes the `.png` they cite. The
probe the field report ran by hand was already green in our own suite.

Epic #266 ratified the rule this violates:

> A coherence gate must fail closed when its subject is absent, stale, or unreachable. **Compare
> against reality (the feed, the tag, the file on disk), not against a record of reality.**

A cited artifact path *is* the file on disk — the epic's own example. This feature makes the gate
compare against reality.

### Both buckets, or the hole just moves

`Evidence.namesRenderedArtifact` (`Evidence.fs:152-159`, feature 098) discharges an obligation from
**either** of two path-bearing buckets: an `artifacts:` entry, **or** a `sourceRefs[]` entry carrying
a `path`. Existence-checking only `artifacts:` would leave the identical hole one field to the left —
an author (or an agent) who writes the non-existent path into `sourceRefs` passes exactly as before.
Our own `EvidenceCommandTests.fs:1206` proves it: same fake `.png`, `sourceRefs` bucket, green.

So the unit of enforcement is **every locally-resolvable path a declaration cites**: `artifacts[]`
plus `sourceRefs[].path`. A `sourceRefs[].uri` is deliberately **excluded** — it is not a local file
and this feature performs no network access.

### What is *not* in scope

`result: pass` remains a self-attestation: nothing observes the test run that the artifact is
evidence *of*. That is the expensive half, filed separately as FS.GG.SDD#350, and it is the one that
makes `ship` mean something. This feature is the cheap half — the file is *there* — and it is worth
landing alone because a verdict that cites a path nobody checked is worse once ADR-0026 records those
verdicts permanently in git history.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A pass that cites a missing file is refused (Priority: P1)

An author (or agent) declares an obligation satisfied and cites the artifact that proves it. The file
is not there — mistyped, never produced, deleted, or invented by an agent optimising for a green
stage. The lifecycle refuses the declaration and names the path.

**Why this priority**: this is the whole feature. Without it, every downstream verdict —
`verify.json`, `ship.json`, and the ADR-0026 committed ship verdict — may certify a claim whose only
support is a string.

**Acceptance**

- Given an evidence declaration with `result: pass`, `synthetic: false`, and `artifacts:` naming a
  path that does not exist under the workspace root, when `fsgg-sdd evidence` runs, then it emits a
  **blocking** `evidence.artifactNotFound` diagnostic naming the missing path, and writes no
  `evidence.yml` (H-4: no handler writes on a blocking run).
- The same declaration citing the missing path via `sourceRefs[].path` is refused identically.
- Given the file exists, the declaration passes as before, with no new diagnostic.
- A `sourceRefs[].uri` is never probed and never blocks.

### User Story 2 - The refusal survives to the merge boundary (Priority: P1)

An artifact exists when evidence is authored and is deleted (or renamed) before merge. Evidence is
not re-run. `verify` must still catch it, or the deletion is invisible at exactly the moment it
matters.

**Why this priority**: the `evidence` stage declares; `verify` is the merge-boundary gate. A check
that only fires at authoring time is a check that a stale artifact walks straight past.

**Acceptance**

- Given a work item whose `evidence.yml` cites a path that no longer exists, when `fsgg-sdd verify`
  runs, then the obligation's `TD-` disposition is `invalid`, carries
  `evidence.artifactNotFound`, its severity is `blocking`, and readiness is
  `needsVerificationCorrection`.
- Given the same work item, `fsgg-sdd ship` reports not ready (it aggregates verify's blocking
  findings; no ship change is required).

### User Story 3 - Only satisfying declarations are held to it (Priority: P2)

An obligation that is `deferred`, `missing`, or disclosed-`synthetic` may legitimately cite an
artifact that does not exist yet — that is what deferral *means*.

**Why this priority**: a gate that blocks honest deferral teaches authors to stop deferring, which is
the failure mode #266 is trying to cure, not cause.

**Acceptance**

- Existence is enforced **only** where `normalizedEvidenceResult = "pass"` and `Synthetic = false` —
  precisely the satisfaction rule (`result: pass ∧ synthetic: false`, and nothing else, satisfies).
- A `deferred`/`missing`/`stale`/`advisory`/`blocked` declaration citing an absent path is not
  blocked by this feature.
- A `pass` + `synthetic: true` declaration citing an absent path is not blocked by this feature (it
  is already disclosed and does not satisfy).

### User Story 4 - The corpus we publish is honest (Priority: P2)

`docs/examples/lifecycle-artifacts/` is copied verbatim into a real initialized workspace and driven
through the live gates by `ExampleLifecycleContractTests` and the skill doctests. It cites six test
files that do not exist.

**Why this priority**: the example is the product's own teaching artifact. Shipping an example that
the product's own gate rejects is the same defect one level up — and it is what the doctest would
now, correctly, go red on.

**Acceptance**

- Every path cited by `docs/examples/lifecycle-artifacts/evidence.yml` exists in that corpus.
- The example corpus passes `evidence` and `verify` with the existence check live.

### Edge Cases

- **Path escapes the workspace** (`../../etc/passwd`): already refused lexically by
  `ArtifactRef.create` (non-blank, no `..`). This feature adds no new escape surface: it probes only
  paths that already survived that guard, resolved relative to the workspace root. Containment stays
  lexical, per ADR-0002 / FS.GG.SDD#185.
- **A directory at the cited path**: treated as absent (an artifact is a file). `ReadFile`'s
  interpreter snapshots files only.
- **Blank / whitespace-only entries**: already stripped by the codec's scalar-list parse; they were
  never `ArtifactRef`s and are not probed.
- **`--dry-run`**: probes still run (reads are permitted in dry-run; only writes are suppressed), so
  a dry run reports the same diagnostics as a real run.
- **Case sensitivity**: existence is decided by the filesystem, not normalized by SDD. A workspace on
  a case-insensitive filesystem will accept a differently-cased path; this is not corrected here.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The evidence stage MUST resolve every locally-resolvable path cited by a *satisfying*
  declaration (`result: pass` ∧ `synthetic: false`) against the workspace and MUST emit a blocking
  `evidence.artifactNotFound` diagnostic naming each path that does not exist.
- **FR-002**: The set of locally-resolvable cited paths MUST be `artifacts[]` ∪ `sourceRefs[].path`.
  `sourceRefs[].uri` MUST NOT be probed and MUST NOT block.
- **FR-003**: The existence fact MUST be produced by an interpreted `CommandEffect` at the edge and
  consumed by the pure `update` — no `System.IO` call may appear in a handler or in `Artifacts`
  (Constitution V).
- **FR-004**: `verify` MUST mirror the refusal: the affected obligation's `TD-` disposition state is
  `invalid`, carrying `evidence.artifactNotFound`, with `blocking` severity.
- **FR-005**: `ship` MUST report not-ready when verify carries a blocking `artifactNotFound` finding.
  This requires no change to `HandlersShip` — it already aggregates blocking findings.
- **FR-006**: A declaration that does not satisfy (`deferred`/`missing`/`stale`/`advisory`/`blocked`,
  or disclosed-synthetic) MUST NOT be blocked by this feature.
- **FR-007**: The rule MUST be expressed **once** in `Artifacts` and consumed by its callers (the
  evidence gate, the `ED-` cascade, the `TD-` mirror), following the `passesWithoutRenderedArtifact`
  precedent, so the rule cannot drift between stages.
- **FR-008**: The published example corpus MUST cite only paths that exist within it.
- **FR-009**: The failure leg MUST be asserted on the diagnostic id / reason string, not on a bare
  exit code (per #266's own open note: a fix whose failure leg is untested is how this class
  survives).

### Key Entities

- **cited path** — a workspace-relative path named by a declaration in `artifacts[]` or
  `sourceRefs[].path`. Not `uri`.
- **satisfying declaration** — `result: pass` ∧ `synthetic: false`. The existing satisfaction rule;
  this feature adds no new vocabulary.
- **`evidence.artifactNotFound`** — new blocking diagnostic id (`DiagnosticError`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The exact probe from the field report — a `pass`/`non-synthetic` entry citing
  `tests/ThisFileDoesNotExist.fs` — is refused at `evidence`, at `verify`, and reported not-ready at
  `ship`. Today it is green at all three.
- **SC-002**: The repository's artifact-citation census goes from **29 missing / 29 cited** to
  **0 missing**.
- **SC-003**: Both committed tests that today pass while citing a non-existent `.png`
  (`EvidenceCommandTests.fs:1184`, `:1206`) either write the file they cite or assert the refusal.
- **SC-004**: A failure-leg test asserts `evidence.artifactNotFound` by id, for both the `artifacts:`
  bucket and the `sourceRefs[].path` bucket.
- **SC-005**: No `System.IO` reference is added to `FS.GG.SDD.Artifacts` or to any handler.

## Assumptions

- Cited artifacts are small (screenshots, logs, test sources). The probe reuses `ReadFile`, which
  snapshots file *text*; see `plan.md` for why this is acceptable and what would change if it stops
  being true.
- The workspace root is the process's project root, as for every other lifecycle read.

## Out of Scope

- Observing that a cited test actually *ran* or passed — `result: pass` remains a self-attestation
  (FS.GG.SDD#350).
- Freshness/digest verification of a cited artifact (`sourceRefs[].digest` is parsed, not verified).
- Remote `uri` resolution.
- Rewriting the ~20 `tests/fixtures/**/evidence.yml` corpora: they are read by the pure parser
  (`Artifacts.Tests`), never driven through the Commands evidence gate, so they do not block. Verified
  empirically, not assumed.

## Deferred

- A dedicated `FileExists` `CommandEffect` case. `ReadFile` already yields the existence fact
  (`Succeeded = true`, `Snapshot = None` ⇒ absent) and adding a public union case is a breaking
  change against the API-compatibility gate. Revisit if cited artifacts become large binaries.
