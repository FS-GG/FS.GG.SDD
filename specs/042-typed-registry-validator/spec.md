# Feature Specification: Typed Registry Validator

**Feature Branch**: `042-typed-registry-validator`

**Created**: 2026-06-28

**Status**: Draft

**Input**: User description: "start the next unblocked sdd item on the coordination board." — resolved to the open, unblocked cross-repo request FS-GG/FS.GG.SDD#12 ("[cross-repo] Fsgg.Registry cannot validate registry/dependencies.yml (model + version grammar gap)").

## Context

`Fsgg.Registry` is the SDD-owned, typed validator shipped in the `FS.GG.Contracts`
package. It is meant to be the authority that the reusable cross-repo
`contract-coherence` gate (FS-GG/.github#18) uses to validate the canonical
`registry/dependencies.yml` against the FS.GG.Contracts registry schema.

Today it cannot do that, for two reasons:

1. **No way in.** The validator is pure over an already-built typed model; there
   is no entrypoint that turns the on-disk YAML file into that model. A gate or
   consumer has no way to feed the real file to the validator.
2. **The model and version grammar do not match the real file.** The typed model
   describes `Components {Id, Version}` + `Edges {Consumer, Provider,
   CompatibleRange}`, but the on-disk file is `contracts[] {id, version, owner,
   surface, consumers}` + `dependencies[] {from, to, via}` + `coherence[]`.
   Dependency edges legitimately reference **repo** ids (sdd, rendering, …) that
   are not contracts, and the file legitimately declares **bare-integer schema
   versions** (e.g. `1`, `2`) and shorthand ranges (e.g. `1.x`). Run against the
   real file, the current validator would emit a false `UnknownComponent` for
   every dependency edge and a false `MalformedVersion` for every bare-integer
   version.

Because of this, FS-GG/.github#18 shipped a **stand-in Python validator**
(`scripts/validate-registry.py`) that mirrors the validator's rule *kinds*
(MissingField / UnknownComponent / MalformedVersion). The registry tracks this as
coherence id `registry-validator-typed` with `coherent: false`. This feature
closes that gap so the typed validator becomes the single authority and the Python
stand-in can be retired.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Validate the real registry file from disk (Priority: P1)

A maintainer or CI gate points the typed registry validator at the canonical
on-disk `registry/dependencies.yml` and receives a clear verdict — either "valid"
or a precise list of diagnostics — without needing any stand-in tool.

**Why this priority**: This is the core unblock. Without a load-and-validate
entrypoint, nothing downstream (the coherence gate, a maintainer running it
locally) can use the typed validator at all. It is the minimum viable slice that
delivers value on its own.

**Independent Test**: Provide the actual canonical registry file and confirm the
validator loads it and returns a verdict. Provide a deliberately broken copy
(missing required field, undefined contract reference, malformed version) and
confirm it returns the corresponding diagnostics.

**Acceptance Scenarios**:

1. **Given** the canonical, well-formed `registry/dependencies.yml`, **When** the
   validator is run against the file path, **Then** it returns a "valid" verdict
   with no diagnostics.
2. **Given** a registry file with a contract entry missing its version, **When**
   the validator runs, **Then** it returns a missing-field diagnostic naming that
   entry and field.
3. **Given** a file path that does not exist or cannot be read, **When** the
   validator runs, **Then** it returns a clear, deterministic parse/load failure
   rather than crashing.

---

### User Story 2 - No false alarms on the real schema shape (Priority: P1)

The validator understands the canonical registry schema as authored —
`contracts[]`, `dependencies[]` referencing repo ids, and `coherence[]` — and does
not flag legitimate, intentionally-authored content as errors.

**Why this priority**: A validator that rejects the real file on every dependency
edge is worse than no validator — it cannot become the authority and would block
every gate run. Eliminating false positives is required for the feature to be
usable, so it is co-equal P1 with Story 1.

**Independent Test**: Run the validator over the unmodified canonical file and
confirm zero diagnostics are produced for dependency edges whose endpoints are
repo ids (sdd, rendering, governance, templates, .github) and for the `coherence`
section.

**Acceptance Scenarios**:

1. **Given** dependency edges whose `from`/`to` reference repo ids that are not
   contract ids, **When** the validator runs, **Then** it does **not** emit
   `UnknownComponent` for those edges.
2. **Given** a `coherence[]` section in the file, **When** the validator runs,
   **Then** it processes the file without treating `coherence` entries as unknown
   or malformed.
3. **Given** a contract whose `owner` or a `consumers[]` entry references a
   genuinely undefined repo id, **When** the validator runs, **Then** it still
   reports that genuine reference error (real defects are not masked by removing
   the false positives). *(The `dependencies[].via` field is intentionally
   free-text and is **not** contract-checked — matching the Python authority this
   replaces, SC-005; genuine contract-reference defects surface structurally via
   the `owner`/`consumers`/duplicate-id/version rules rather than by parsing
   `via`. See research R4.)*

---

### User Story 3 - Accept bare-integer schema versions and shorthand ranges (Priority: P2)

The validator accepts the version forms the registry legitimately uses —
bare-integer schema versions (`1`, `2`) and shorthand ranges (`1.x`) — alongside
full `major.minor.patch` SemVer, without falsely flagging them as malformed.

**Why this priority**: Required for a clean pass over the real file, but it builds
on Stories 1 and 2 being in place. Without it the validator cannot go "green" on
the canonical file, but it is a narrower grammar concern than the structural model
gap.

**Independent Test**: Run the validator over the canonical file containing
governance-policy `1`, governance-capabilities `2`, governance-tooling/-descriptor
`1`, and a `1.x` range, and confirm none are reported as malformed; then confirm a
genuinely malformed version (e.g. `1.2.x.4`, `abc`) is still reported.

**Acceptance Scenarios**:

1. **Given** a contract declaring a bare-integer schema version such as `1` or
   `2`, **When** the validator runs, **Then** it does not emit `MalformedVersion`
   for that entry.
2. **Given** a compatibility range expressed as `1.x`, **When** the validator
   runs, **Then** it accepts the range without a malformed-version diagnostic.
3. **Given** a version string that is genuinely malformed, **When** the validator
   runs, **Then** it still emits `MalformedVersion` for that entry.

---

### Edge Cases

- **Unreadable / missing file**: returns a deterministic load failure, never an
  unhandled crash.
- **Malformed YAML** (not parseable): returns a single, clear parse diagnostic
  rather than a cascade of misleading content diagnostics.
- **Unknown / extra fields** in the file (e.g. a future additive field): handled
  tolerantly — additive evolution of the registry must not break the validator.
- **Mixed version vocabularies** in one file: full SemVer, bare-integer schema
  versions, and `N.x` ranges coexisting are all handled correctly in a single run.
- **Empty sections**: an empty `dependencies[]` or `coherence[]` is valid, not an
  error.
- **Genuine defects still surface**: removing false positives must not silence
  real missing-field, unknown-repo-id (`UnknownComponent`), duplicate-id, or
  malformed-version errors.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The validator MUST provide a documented entrypoint that takes a path
  to an on-disk registry file and produces either a typed model/verdict or a clear
  load/parse failure — closing the "no way in" gap.
- **FR-002**: The validator MUST understand the canonical registry schema as
  authored: a top-level integer `schemaVersion`; a non-empty `repos` mapping (each
  `{name, role}`, keyed by repo id); `contracts[] {id, version, owner, surface,
  consumers}` (with optional `package-version` and `range`); `dependencies[] {from,
  to, via}`; and `coherence[]`. The `repos` ids are the reference set that
  `owner`/`consumers`/edge endpoints resolve against.
- **FR-003**: The validator MUST NOT emit `UnknownComponent` (or equivalent) for
  dependency edges whose endpoints are repo ids that are not contract ids.
- **FR-004**: The validator MUST accept bare-integer schema versions (e.g. `1`,
  `2`) and MUST NOT flag them as malformed.
- **FR-005**: The validator MUST accept shorthand compatibility ranges (e.g.
  `1.x`) used in the canonical file and MUST NOT flag them as malformed.
- **FR-006**: The validator MUST continue to report genuine defects — missing
  required fields, **duplicate contract ids**, references to genuinely undefined
  **repo ids** (via `owner`, `consumers`, or edge `from`/`to`), and genuinely
  malformed versions — i.e. removing false positives MUST NOT mask real errors. The
  `dependencies[].via` field is intentionally free-text and is NOT contract-checked
  (parity with the Python authority, SC-005); genuine contract-reference defects are
  caught structurally by the rules above rather than by parsing `via`.
- **FR-007**: The validator MUST return a deterministic verdict for the same input
  (same diagnostics, same order) so it can back a reproducible CI gate.
- **FR-008**: Running the validator over the current canonical
  `registry/dependencies.yml` MUST produce a "valid" verdict with zero false
  diagnostics.
- **FR-009**: The change MUST be additive to the published contract surface — no
  **breaking (major)** `FS.GG.Contracts` bump may be incurred. An additive **minor**
  package version bump (new public types/functions, legacy surface retained) is
  expected and acceptable; a breaking change is not, and were one to prove
  unavoidable it MUST be called out.
- **FR-010**: The outcome MUST be sufficient for FS-GG/.github#18 to retire the
  Python stand-in (`scripts/validate-registry.py`) and adopt the typed validator
  as the authority, flipping coherence id `registry-validator-typed` to
  `coherent: true`.

### Key Entities

- **Registry file**: the canonical on-disk `registry/dependencies.yml` owned by
  FS-GG/.github; the input to validation. Carries a top-level integer
  `schemaVersion` and a `repos` mapping.
- **Repo entry**: an item under `repos` (keyed by repo id, e.g. `sdd`, `rendering`,
  `governance`, `templates`) with `name` and `role`. The repo ids are the reference
  set for `owner`/`consumers`/edge-endpoint checks.
- **Contract entry**: an item under `contracts[]` with `id`, `version`, `owner`,
  `surface`, `consumers`, and optional `package-version`/`range`.
- **Dependency edge**: an item under `dependencies[]` with `from`, `to`, `via`;
  `from`/`to` are repo ids (not contract ids) and `via` is free-text.
- **Coherence entry**: an item under `coherence[]` recording cross-repo coherence
  state (e.g. id, coherent flag, tracking reference).
- **Diagnostic**: a single validation finding (entry, rule kind, message). Rule
  kinds emitted by `validateDocument`: `MissingField`, `UnknownComponent`,
  `DuplicateComponent`, `MalformedVersion`, and `MalformedDocument` (the load/parse
  failure). (`IncompatibleVersion` exists on the rule type but is retained for the
  legacy `validate` only and is not emitted by `validateDocument` on the canonical
  file.)
- **Verdict**: "valid" or a deterministic list of diagnostics.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The typed validator, run against the unmodified canonical
  `registry/dependencies.yml`, returns "valid" with **zero** diagnostics.
- **SC-002**: For each of the three previously-false-positive classes (repo-id
  dependency edges, bare-integer schema versions, `N.x` ranges), a representative
  case produces **no** diagnostic, while a paired genuinely-broken case still
  produces the correct diagnostic — demonstrating no regression in defect
  detection.
- **SC-003**: A consumer can validate the registry file end-to-end (path in,
  verdict out) using only the shipped SDD entrypoint — `fsgg-sdd registry validate
  <path>`, which composes the `FS.GG.SDD.Artifacts` YAML loader edge with the
  published `FS.GG.Contracts` `validateDocument` — with **no** stand-in script
  required. (The BCL-only `FS.GG.Contracts` leaf cannot read YAML itself; the loader
  lives at an edge in `FS.GG.SDD.Artifacts` per Constitution Principle V.)
- **SC-004**: The validator's verdict for a given file is byte-for-byte identical
  across repeated runs (determinism), suitable for a CI `--exit-code`-style gate.
- **SC-005**: FS-GG/.github#18 can replace `scripts/validate-registry.py` with the
  typed validator and flip coherence id `registry-validator-typed` to coherent,
  with no behavioral disagreement between the two on the canonical file.

## Assumptions

- The canonical schema of `registry/dependencies.yml` is as described in request
  FS-GG/FS.GG.SDD#12 (`contracts[]` / `dependencies[]` / `coherence[]`); the live
  file in FS-GG/.github is the source of truth and will be consulted during
  planning.
- The choice between **converging the typed model to the real schema** versus
  **publishing a documented projection plus a load function** is an implementation
  decision deferred to `/speckit-plan`; either satisfies this spec so long as the
  outcomes above hold.
- The validator remains BCL-only (no new third-party SemVer dependency),
  consistent with the existing `Fsgg.Registry` design, unless planning shows a
  YAML loader dependency is unavoidable — in which case it is called out in the
  plan.
- This is SDD-owned work in `FS.GG.Contracts`; FS-GG/.github#18 is the downstream
  consumer and is tracked separately. This feature is "done" when the typed
  validator is the capable authority, even if the .github swap lands as a
  follow-up.
- This item is the next unblocked SDD work but is not yet a card on the
  Coordination board; it should be added (Repo `sdd`, Workstream `Versioning`,
  Contract `fsgg-contracts`) and linked to FS-GG/FS.GG.SDD#12 and FS-GG/.github#18.
