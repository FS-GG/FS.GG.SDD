# Feature Specification: Skill-manifest contract types — SkillManifest, AGENT_SKILL_ROOTS, provenance per-skill sha256

**Feature Branch**: `057-skill-manifest-contract`

**Created**: 2026-07-02

**Status**: Draft

**Input**: User description: "P0.D0.2 (ADR-0014): define the contract types the
consolidated skill-mirror needs in FS.GG.Contracts — a per-producer skill manifest, the
AGENT_SKILL_ROOTS constant, and a per-skill sha256 on scaffold-provenance (additive minor)."

## Context

Resolves **FS-GG/FS.GG.SDD#60** (Phase **P0.D0.2** of the skill-vendoring robustness epic
**FS-GG/.github#110**, decided in **ADR-0014**, extending ADR-0011). ADR-0014 replaces four
hand-maintained "materialize union → 3 roots" mirror implementations with **one**
content-addressed algorithm, driven by a declarative per-producer **skill manifest** and a
single `mirror`/`verify` library in `FS.GG.Contracts`, parameterized over one declared root-set
constant `AGENT_SKILL_ROOTS`.

This feature is the **contract-shape prerequisite** (roadmap §3 P0.D0.2) for that work. It is
**types only** — it defines the machine-readable shapes the library (P1) and the provider
manifest (P2) will consume, and it makes `scaffold-provenance` able to carry a per-skill digest.
It deliberately does **not** implement `mirror`/`verify`, does not route any command through a
new code path, and does not compute or populate any digest during scaffold — those are P1.

Every change here is **additive**: new public types and one new optional field, so a consumer
compiled against the prior contract keeps working and provenance written before this change
still parses.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A producer can declare its skills as a versioned manifest (Priority: P1)

A skill producer (SDD's process skills; a provider's product skills) needs a single
machine-readable declaration of the skills it emits — an `id`, a `scope` (process vs product),
the `sha256` of the canonical body, and the body itself (or an in-package path resolving to it) —
instead of ad-hoc directory scans or per-source `template.json` strings.

**Why this priority**: The manifest is the contract the whole ADR-0014 consolidation reads. P1
(the library) and P2 (the provider manifest) both depend on this type existing and being
published in `FS.GG.Contracts`.

**Independent Test**: Construct a `SkillManifest` value with process and product entries in the
`FS.GG.Contracts.Tests` project; assert the type is public, `scope` distinguishes process from
product, and each entry carries a digest and exactly one body source.

### User Story 2 - The agent-skill root set is one declared constant (Priority: P1)

Every fan-out and verify path must derive its target roots (`.claude`, `.codex`, `.agents`) from
one declared constant, so adding or renaming a runtime root is a one-line contract change rather
than an N-place edit across `scaffold`/`refresh`/`doctor`/the template.

**Why this priority**: ADR-0014 §Decision 5. Without the shared constant, the four current
hardcoded root lists (`SeededSkills.fs`, `Drift.fs`, `HandlersRefresh.fs`, `HandlersScaffold.fs`)
stay divergent and the consolidation has nothing single to point at.

**Independent Test**: Assert `Fsgg.Schemas.agentSkillRoots = [".claude"; ".codex"; ".agents"]` and
that it is a public value in the package surface.

### User Story 3 - scaffold-provenance can carry a per-skill sha256, additively (Priority: P1)

A produced/mirrored path recorded in `.fsgg/scaffold-provenance.json` can carry the `sha256` of
its content, so a later content-equality guard (P1) can assert byte-identity across roots. A
provenance file written before this field — with no digest — must still parse unchanged, and the
current emitter (which does not yet compute digests) must produce **byte-identical** output.

**Why this priority**: ADR-0014 §Decision 3 makes provenance content-addressed. The additive
field is the on-disk half of that; the contract type is the published half.

**Independent Test**: Round-trip a provenance record whose paths carry a `sha256` and assert it
survives; parse a provenance document with no `sha256` and assert the field defaults to absent;
serialize a record with no digest and assert the JSON is unchanged from today (no `sha256` key
emitted).

### Edge Cases

- A manifest entry that declares **both** a `body` and a `resolvablePath`, or **neither** — the
  type permits both to be optional; the resolving policy (exactly one) is enforced by P1's
  library, not by this type. This feature only guarantees the shape can express either.
- A provenance path with a `sha256` present but empty/whitespace — treated the same as absent by
  the additive-tolerant parser (no digest).
- A provenance document authored under a **future** unsupported schema version — unchanged
  behavior: rejected as today (this feature does not touch schema-version gating).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `FS.GG.Contracts` MUST publish a `SkillScope` type distinguishing a `process`
  (lifecycle) skill from a `product` (provider) skill.
- **FR-002**: `FS.GG.Contracts` MUST publish a `SkillManifestEntry` type with `Id`, `Scope`,
  `Sha256`, and an optional `Body` and optional `ResolvablePath` (the body itself, or a resolvable
  in-package path to it).
- **FR-003**: `FS.GG.Contracts` MUST publish a `SkillManifest` type — a versioned list of entries
  (a `SchemaVersion` plus the `Skills`) — the per-producer declaration ADR-0014 §Decision 1 calls
  the contract.
- **FR-004**: `FS.GG.Contracts` MUST publish `agentSkillRoots`, the single declared
  `AGENT_SKILL_ROOTS` constant `[".claude"; ".codex"; ".agents"]`.
- **FR-005**: `FS.GG.Contracts` MUST register `skill-manifest` in the schema `entries` registry
  with its own version constant `skillManifestVersion`, owner SDD.
- **FR-006**: The `scaffold-provenance` contract type MUST gain an additive optional per-path
  `Sha256`, and the runtime provenance record/serializer/parser MUST round-trip it while remaining
  additive-tolerant (absent ⇒ no digest) and byte-identical when no digest is present.
- **FR-007**: The `scaffold-provenance` in-code schema version MUST stay `1` (additive change), and
  the `scaffold-provenance` **contract version** MUST be registered as a **minor** bump
  (`1.0.0` → `1.1.0`) in the `FS-GG/.github` registry, with its `compatibility.md` projection
  updated. The typed registry validator MUST report the updated document valid.
- **FR-008**: The `FS.GG.Contracts` package contract version MUST take an additive **minor** bump
  (`1.2.0` → `1.3.0`) reflecting the new public surface, and the public-surface golden baseline MUST
  be updated to the new additive-only surface.
- **FR-009**: This feature MUST NOT implement `mirror`/`verify`, route any command through a new
  path, or populate any digest during scaffold/refresh (all P1).

### Key Entities

- **SkillManifest** — a producer's declarative skill set: `{ SchemaVersion; Skills }`.
- **SkillManifestEntry** — one skill: `{ Id; Scope; Sha256; Body?; ResolvablePath? }`.
- **SkillScope** — `Process | Product`.
- **agentSkillRoots** — the declared `[".claude"; ".codex"; ".agents"]` root set.
- **ScaffoldProducedPathEntry / ScaffoldProducedPath** — gains an optional `Sha256`.

## Success Criteria *(mandatory)*

- **SC-001**: `SkillScope`, `SkillManifestEntry`, `SkillManifest`, `agentSkillRoots`,
  `skillManifestVersion`, and the `Sha256` member appear in the `FS.GG.Contracts` public-surface
  baseline; the surface delta is additive only.
- **SC-002**: `Schemas.entries` enumerates `skill-manifest` (owner SDD) alongside the prior
  schemas; the count moves 10 → 11 and every name remains unique.
- **SC-003**: A provenance record whose paths carry a `sha256` round-trips through
  serialize/parse; a document with no `sha256` parses (digest absent); serialization of a
  digest-free record is byte-identical to today (no `sha256` key emitted).
- **SC-004**: `scaffoldProvenanceVersion` stays `1`; the `.github` registry records
  `scaffold-provenance` at `1.1.0` and validates.
- **SC-005**: The full build and test suite is green.

## Assumptions

- The manifest is a **library contract type**, not (yet) a persisted `.fsgg/` product artifact; it
  is registered in the in-code `entries` schema registry for versioning/discoverability, but this
  feature adds no cross-repo `skill-manifest` contract entry to the `.github` registry (only the
  provenance minor bump, per #60's Done criteria).
- Publish-before-flip: this feature only defines shapes in-repo; the coherent `FS.GG.Contracts` /
  CLI release that publishes them is P1. Registering the provenance schema minor bump is safe
  before that release because the field is optional — current emitter output remains valid under
  `1.1.0`.
