# Feature Specification: Accept 4-Segment Versions in the Registry Validator

**Feature Branch**: `045-validator-4-segment-versions`

**Created**: 2026-06-29

**Status**: Planned

**Input**: User description: "start item #32 on the coordination board." — resolved to the open cross-repo request FS-GG/FS.GG.SDD#32 ("[cross-repo] Fsgg.Registry validator rejects 4-segment versions (1.2.1.1) — blocks the .github#49 typed-CLI gate swap").

## Context

`Fsgg.Registry` is the SDD-owned, typed registry validator shipped in the
`FS.GG.Contracts` package and exposed to consumers as `fsgg-sdd registry validate`.
Feature 042 made it the capable authority over the canonical
`registry/dependencies.yml`: it accepts full SemVer (`1.0.0`), bare-integer schema
versions (`1`, `2`), prereleases (`0.1.52-preview.1`), and shorthand ranges (`1.x`),
and was deliberately built to mirror the Python stand-in
(`scripts/validate-registry.py`, owned by FS-GG/.github) "so the two cannot
disagree."

They do disagree on one case. The validator's version grammar accepts **exactly
three** numeric segments, so it rejects the legitimate **4-segment**
`major.minor.patch.revision` form (NuGet / `System.Version`). The contract
`governance-reference-gate-set` is legitimately versioned `1.2.1.1` — ADR-0007
derives that version from its four contained `schemaVersion`s
(`{gov}.{caps}.{policy}.{tooling}`), and it is the real published
`FS.GG.Governance.ReferenceGateSet` package version. The Python authority accepts
it (its regex carries an optional 4th segment); the typed validator emits a false
`MalformedVersion` on both `version` and `package-version`:

```
$ fsgg-sdd registry validate registry/dependencies.yml --text
registry validate: registry/dependencies.yml → invalid (2 diagnostics)
  - MalformedVersion [governance-reference-gate-set]: ... malformed 'version': '1.2.1.1'.
  - MalformedVersion [governance-reference-gate-set]: ... malformed 'package-version': '1.2.1.1'.
$ echo $?   # 1
```

So the SDD#26 convergence claim that the typed validator's rule kinds *mirror*
`validate-registry.py` is **incomplete**: the 4-segment case diverges. This blocks
FS-GG/.github#49 (swap the reusable `contract-coherence` gate from the Python
stand-in to the typed CLI). Swapping as-is would turn `contract-coherence` **red on
every FS-GG repo's CI** — the registry is valid; the validator is wrong — and keeps
coherence id `registry-validator-typed` at `coherent: false`. This feature closes
the grammar gap and republishes the validator so the gate swap can land.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The validator accepts a legitimate 4-segment version (Priority: P1)

A maintainer or CI gate runs the typed registry validator against the canonical
`registry/dependencies.yml`, which contains the legitimately 4-segment
`governance-reference-gate-set` version `1.2.1.1`, and receives a "valid" verdict —
the same answer the Python authority gives.

**Why this priority**: This is the entire unblock. While the validator falsely
rejects `1.2.1.1`, it cannot become the coherence-gate authority, and FS-GG/.github#49
stays blocked. It is the minimum viable slice and delivers the value on its own.

**Independent Test**: Run the validator over the canonical registry file (or a
fixture carrying a `1.2.1.1` `version` and `package-version`) and confirm it returns
"valid" with zero diagnostics; run it over a contract whose version is a genuinely
malformed 4-ish string (e.g. `1.2.x.4`) and confirm it still reports
`MalformedVersion`.

**Acceptance Scenarios**:

1. **Given** a contract declaring `version: "1.2.1.1"`, **When** the validator runs,
   **Then** it does **not** emit `MalformedVersion` for that contract's `version`.
2. **Given** a contract declaring `package-version: "1.2.1.1"`, **When** the validator
   runs, **Then** it does **not** emit `MalformedVersion` for that contract's
   `package-version`.
3. **Given** the unmodified canonical `registry/dependencies.yml` (which contains the
   4-segment `governance-reference-gate-set`), **When** the validator runs, **Then**
   it returns a "valid" verdict with zero diagnostics.

---

### User Story 2 - Genuine version defects are still caught (Priority: P1)

The grammar widens to admit one more legitimate numeric shape and nothing else:
previously-accepted forms keep working, and genuinely malformed versions are still
reported. Removing the false positive must not open a hole.

**Why this priority**: A validator that, while fixing `1.2.1.1`, also started passing
`1.2.x.4`, `abc`, or a 5-segment string would be worse than the current one — it
would silently mask real defects in a gate every FS-GG repo depends on. Preserving
defect detection is co-equal P1 with Story 1.

**Independent Test**: Re-run the existing version corpus (bare integers `1`/`2`,
full SemVer, prereleases, `1.x` ranges) and confirm all still pass; run the existing
malformed cases (`1.2.x.4`, `abc`) and a new over-long numeric case and confirm each
still produces `MalformedVersion`.

**Acceptance Scenarios**:

1. **Given** the previously-accepted forms (`1`, `2`, `1.0.0`, `0.1.52-preview.1`, and
   a `1.x` range), **When** the validator runs, **Then** none are flagged as malformed
   (no regression).
2. **Given** a genuinely malformed version such as `1.2.x.4` or `abc`, **When** the
   validator runs, **Then** it still emits `MalformedVersion` for that entry.
3. **Given** a numeric version with more than four segments (e.g. `1.2.3.4.5`),
   **When** the validator runs, **Then** it is reported as `MalformedVersion` (the
   widening admits a 4th segment only, not unbounded segments).

---

### User Story 3 - Downstream gate can adopt the fixed validator (Priority: P2)

The fix is delivered as a published artifact so FS-GG/.github#49 can pin a CLI/package
version that agrees with the registry, retire the Python stand-in, and flip coherence
id `registry-validator-typed` to `coherent: true`.

**Why this priority**: The grammar fix in source is necessary but not sufficient for
the cross-repo unblock — the consumer pins a *published* version. It builds on Stories
1–2 being correct, so it is P2, but it is what actually closes the request.

**Independent Test**: From a clean consumer environment, install/restore the published
artifact at the new version and run `registry validate` against the canonical file,
confirming a "valid" verdict end-to-end with no source build of this repo.

**Acceptance Scenarios**:

1. **Given** the grammar fix is merged, **When** the validator is published to the org
   feed, **Then** a new consumable version exists that accepts `1.2.1.1`, and the
   version is reported back on FS-GG/FS.GG.SDD#32.
2. **Given** the published version, **When** FS-GG/.github#49 pins it in the
   `contract-coherence` gate, **Then** the typed validator and the Python stand-in
   agree on the canonical file (both "valid"), enabling the stand-in's retirement.

---

### Edge Cases

- **Exactly four numeric segments** (`1.2.1.1`): accepted.
- **Four segments with a prerelease/build suffix** (e.g. `1.2.1.1-preview.1`): accepted
  on the same terms as 3-segment SemVer, so the optional 4th segment composes with the
  existing prerelease/build grammar rather than being a special case.
- **Non-numeric 4th-ish segment** (`1.2.x.4`): still rejected (the widening is numeric
  only).
- **More than four segments** (`1.2.3.4.5`): still rejected.
- **Range field unaffected**: the permissive `range` grammar (`1.x`, comparators) is
  out of scope and unchanged; only the `version` / `package-version` grammar widens.
- **Parity drift**: if the Python authority's 4-segment grammar differs in detail from
  the chosen widening, the divergence is surfaced and reconciled rather than silently
  left — the stated goal is that the two cannot disagree on the canonical file.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The validator MUST accept a `version` value of the form
  `major.minor.patch.revision` where every segment is numeric (e.g. `1.2.1.1`) and MUST
  NOT emit `MalformedVersion` for it.
- **FR-002**: The validator MUST apply the same acceptance to the optional
  `package-version` field, so a contract carrying both `version: "1.2.1.1"` and
  `package-version: "1.2.1.1"` produces no diagnostics for either.
- **FR-003**: The 4th numeric segment MUST be **optional** — all forms accepted today
  (bare integers `1`/`2`, full SemVer `1.0.0`, prereleases `0.1.52-preview.1`) MUST
  continue to be accepted unchanged.
- **FR-004**: The widening MUST be **numeric-only and bounded to one extra segment**:
  versions with a non-numeric segment (e.g. `1.2.x.4`), non-version text (`abc`), or
  more than four numeric segments (e.g. `1.2.3.4.5`) MUST still be reported as
  `MalformedVersion`.
- **FR-005**: Running the validator over the current canonical
  `registry/dependencies.yml` (which contains the 4-segment
  `governance-reference-gate-set`) MUST produce a "valid" verdict with **zero**
  diagnostics and exit success.
- **FR-006**: The validator's grammar MUST regain parity with the Python authority
  (`scripts/validate-registry.py`) on the 4-segment case, so the two do not disagree on
  the canonical file; the existing "mirrors the stand-in" invariant MUST be restored,
  not further diverged.
- **FR-007**: A `1.2.1.1` case MUST be added to the validator's test corpus (an accepted
  4-segment version), alongside a still-rejected over-long/non-numeric case, so the new
  behavior and its boundary are both pinned by tests.
- **FR-008**: The change MUST be additive to the published contract surface — no
  **breaking (major)** `FS.GG.Contracts` bump may be incurred. (A behavior-only grammar
  widening is expected; if any version constant bumps, it is a non-breaking patch/minor,
  and a breaking change — were one unavoidable — MUST be called out.)
- **FR-009**: The fixed validator MUST be **published** to the org feed (the
  `FS.GG.SDD.Cli` tool, plus `FS.GG.Contracts` if its version constant bumps), and the
  new consumable version MUST be reported back on FS-GG/FS.GG.SDD#32 so FS-GG/.github#49
  can pin it.
- **FR-010**: The outcome MUST be sufficient for FS-GG/.github#49 to swap the
  `contract-coherence` gate from the Python stand-in to the typed CLI and flip coherence
  id `registry-validator-typed` toward `coherent: true`, with no behavioral disagreement
  between the two on the canonical file.

### Key Entities

- **Version string**: a contract's `version` (and optional `package-version`) value in
  the registry. The accepted vocabulary is bare integer, 3-segment SemVer (with optional
  prerelease/build), and — newly — 4-segment numeric (`major.minor.patch.revision`, with
  optional prerelease/build).
- **`governance-reference-gate-set` contract**: the registry contract legitimately
  versioned `1.2.1.1` (per ADR-0007, four contained `schemaVersion`s); the concrete case
  this feature must stop rejecting.
- **Python authority**: `scripts/validate-registry.py` in FS-GG/.github, the reference
  grammar the typed validator mirrors; the source of the "cannot disagree" invariant.
- **Diagnostic**: a single validation finding; the relevant rule kind here is
  `MalformedVersion`, which must no longer fire on `1.2.1.1` but must still fire on
  genuinely malformed versions.
- **Verdict**: "valid" or a deterministic list of diagnostics, backing the CI gate.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The typed validator, run against the unmodified canonical
  `registry/dependencies.yml`, returns "valid" with **zero** diagnostics and exit
  success (today it returns 2 diagnostics and exit 1).
- **SC-002**: For the 4-segment class, a representative accepted case (`1.2.1.1`)
  produces **no** diagnostic while a paired genuinely-broken case (`1.2.x.4` and a
  5-segment `1.2.3.4.5`) still produces `MalformedVersion` — demonstrating the widening
  added one shape and no defect-detection regression.
- **SC-003**: The full pre-existing version corpus (bare integers, 3-segment SemVer,
  prereleases, `1.x` ranges) continues to pass unchanged — zero regressions.
- **SC-004**: A consumer can validate the registry file end-to-end (path in, "valid"
  out) using only the **published** SDD artifact at its new version, with no stand-in
  script and no source build of this repo.
- **SC-005**: The typed validator and `scripts/validate-registry.py` produce the **same**
  verdict on the canonical file (both "valid"), so FS-GG/.github#49 can retire the
  stand-in with no behavioral disagreement.

## Assumptions

- The canonical schema and the legitimacy of `governance-reference-gate-set`'s `1.2.1.1`
  version are as described in FS-GG/FS.GG.SDD#32 and ADR-0007; the live registry in
  FS-GG/.github is the source of truth and will be consulted during planning.
- The Python authority's regex already admits the optional 4th numeric segment (per the
  issue body); the typed grammar is the side that diverged, so reconciliation means
  widening the typed grammar to match — not changing the Python authority. If planning
  finds the two differ in further detail, that divergence is surfaced in the plan.
- The exact grammar expression (extending the existing `version` regex with an optional
  `(\.\d+)?` segment vs. an equivalent parser change) is an implementation decision
  deferred to `/speckit-plan`; any approach satisfying the requirements above is
  acceptable, and the BCL-only, no-new-dependency constraint of `Fsgg.Registry` holds.
- This is SDD-owned work in `FS.GG.Contracts` / `FS.GG.SDD.Cli`. FS-GG/.github#49 is the
  downstream consumer tracked separately; this feature is "done" when the fixed validator
  is published and the new version is reported back on #32, even if the .github gate swap
  lands as a follow-up.
- This item is item #32 on the Coordination board (Repo `sdd`, Workstream `Versioning`,
  Contract `fsgg-contracts`); its card should reflect `In progress` and remain linked to
  FS-GG/FS.GG.SDD#32 and FS-GG/.github#49.
