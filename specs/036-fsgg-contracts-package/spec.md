# Feature Specification: FS.GG.Contracts Package — Shared Schema, Provider & Registry Contracts

**Feature Branch**: `036-fsgg-contracts-package`

**Created**: 2026-06-28

**Status**: Draft

**Input**: User description: "next non blocked sdd item on the project coordination board" → resolved to Coordination board item FS-GG/FS.GG.SDD#8 (`H2 · sdd — Create FS.GG.Contracts package`), part of FS-GG/.github#16 Pillar 2 (the coherence backbone). **contract-change** touching the `scaffold-provider` contract and all `.fsgg` schemas.

## Context

The four FS-GG repos (SDD, Governance, Templates, Rendering) are coupled at the edges by convention, hand-sync, and duplicated facts: `.fsgg` schema versions live only in F# code, each repo re-encodes the same schema shapes, and the scaffold-provider contract declares no build/run/test/verify command. This feature creates a single **versioned, BCL-only `FS.GG.Contracts` package, owned by SDD**, that becomes the one typed source of truth for every `.fsgg` schema (with its version constant), the extended provider descriptor, and the cross-repo dependency-registry types. Consumer repos re-type onto it in later items (FS-GG/FS.GG.SDD#9 and the Governance/Templates counterparts); this item only **creates and publishes** the package. It must be purely additive — nothing in SDD changes behaviour, and SDD's existing artifacts remain byte-identical.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One typed source of truth for every `.fsgg` schema and its version (Priority: P1)

A maintainer of any FS-GG repo references a single published package that defines, for every `.fsgg` artifact in the contract, both its typed shape and its schema version as a named constant. The same schema fact (shape + version) is never re-encoded in two repos again.

**Why this priority**: This is the coherence backbone and the core deliverable of the item. Without the canonical typed schemas + version constants, none of the downstream re-type, drift-gate, or auto-update work (H3/H4) can begin. It is the minimum that makes the package worth publishing.

**Independent Test**: Build the package in isolation and enumerate its exported schema types and version constants. Confirm every `.fsgg` schema named in the contract (providers, project, sdd, agents, governance, policy, capabilities, tooling, scaffold-provenance, governance-handoff) is represented, and that each SDD-owned schema's version constant equals the version SDD emits today.

**Acceptance Scenarios**:

1. **Given** the package source, **When** it is built, **Then** it produces a single BCL-only library exposing a schema module that names every `.fsgg` schema in the contract with a typed record and a version constant.
2. **Given** an SDD-owned schema (e.g. scaffold-provenance, governance-handoff, project, sdd, agents), **When** its version constant is read from the package, **Then** the value equals the schema version SDD currently writes into that artifact.
3. **Given** a consumer that re-types its read/write of an SDD-owned `.fsgg` artifact onto the package's records, **When** it produces that artifact for a fixed input, **Then** the output is byte-identical to today's (zero diff). *(The consumer re-type is item FS-GG/FS.GG.SDD#9, where this byte-identity is measured; this item delivers the records and constants that make it hold and asserts their equality with today's emitted values — see SC-002.)*

---

### User Story 2 - Extended provider descriptor with declared build / test / run / verify and a canonical name (Priority: P2)

A template provider author declares, in the provider contract, optional commands for how the produced product is built, tested, run, and verified, plus the canonical input parameter that carries the product name (defaulting to `name`). Consumers (the scaffold acceptance probes, future scaffold orchestration) read these declared commands instead of assuming a fixed `dotnet` invocation.

**Why this priority**: The extended descriptor is the contract half of the H1 declared-or-default probe work (FS-GG/FS.GG.SDD#7) and unblocks the probe re-type in #9. It is high value but depends on the descriptor type existing in the package, so it follows P1.

**Independent Test**: Construct a descriptor with no command fields declared and confirm consumers resolve to the same platform defaults as today; construct a descriptor that declares build/test/run/verify commands and a non-default name parameter and confirm each declared value is exposed exactly as authored.

**Acceptance Scenarios**:

1. **Given** the provider descriptor type from the package, **When** a provider declares none of the optional build/test/run/verify commands, **Then** the descriptor exposes them as absent and consuming behaviour is identical to today's defaults.
2. **Given** a provider declares a build, test, run, and/or verify command, **When** the descriptor is read, **Then** each declared command is exposed exactly as authored.
3. **Given** a provider declares no name parameter, **When** the descriptor is read, **Then** the canonical name parameter resolves to the default value `name`.
4. **Given** the existing provider fields (name, contract version, template id, source, parameters), **When** the extended descriptor is used, **Then** those fields are preserved unchanged (the extension is additive).

---

### User Story 3 - Typed, validated cross-repo dependency registry (Priority: P3)

A coherence check (today convention-only) reads the cross-repo dependency registry (`registry/dependencies.yml`) as a typed model and runs a validator that reports actionable diagnostics when the registry is incoherent or malformed, instead of relying on hand inspection.

**Why this priority**: Typed registry + validator is required for the H3 coherence workflow and drift gates, but those consumers do not exist yet, so it is the lowest-priority of the three deliverables. It still ships in this item because the registry types are part of the package contract.

**Independent Test**: Provide a coherent registry model to the validator and confirm it passes with no diagnostics; provide an incoherent or incomplete model and confirm the validator returns diagnostics that name the offending entry and the coherence rule violated.

**Acceptance Scenarios**:

1. **Given** the registry types from the package, **When** a coherent registry model is validated, **Then** the validator returns success with no diagnostics.
2. **Given** an incoherent registry model (e.g. a dependency edge whose declared compatible version range excludes the referenced version), **When** it is validated, **Then** the validator returns a diagnostic identifying the entry and the violated coherence rule.
3. **Given** an incomplete registry entry (missing a required field), **When** it is validated, **Then** the validator returns a diagnostic naming the missing field.

---

### Edge Cases

- **Schema named in the contract but not yet emitted by SDD** (governance, policy, capabilities, tooling are Governance-owned): the package still declares the typed shape and version constant to the published schema reference so Governance can re-type onto it; it does not require SDD to produce that artifact.
- **Provider descriptor declares an empty command** (declared but blank): treated as a malformed declaration, distinct from "absent / use default" — surfaced rather than silently defaulted.
- **Registry validator given a structurally valid but semantically incoherent document**: still reports a coherence diagnostic (validation is more than shape-checking).
- **Future schema version bump**: a consumer reading a version constant must get the single value owned by the package; there is no second place that could disagree.
- **BCL-only constraint vs. YAML**: the package owns the typed model and a pure validator over that model; raw YAML/JSON (de)serialization stays at the consumer edge so the package carries no third-party package dependency.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a new standalone library project, `FS.GG.Contracts`, owned by SDD, that carries no dependency on any other SDD project (it is a leaf/lowest-level contract library).
- **FR-002**: `FS.GG.Contracts` MUST be BCL-only — it MUST NOT take a dependency on any third-party package (no YAML/JSON parser, no Governance runtime, no rendering library).
- **FR-003**: The package MUST be versioned with an explicit SemVer of `1.0.0`, and its initial contract MUST be additive (it introduces only new surface; it removes or breaks nothing in any consumer).
- **FR-004**: The package MUST expose a schema module that, for every `.fsgg` schema named in the contract — providers, project, sdd, agents, governance, policy, capabilities, tooling, scaffold-provenance, governance-handoff — defines a typed record for the artifact shape and a named version constant.
- **FR-005**: For every SDD-owned schema, the version constant exposed by the package MUST equal the schema version SDD currently emits for that artifact.
- **FR-006**: The package MUST expose a provider-descriptor type that preserves the existing provider fields (name, contract version, template id, source, parameters) and adds optional `Build`, `Test`, `Run`, and `Verify` command declarations.
- **FR-007**: The provider descriptor MUST expose a canonical name parameter that defaults to `name` when the provider declares none.
- **FR-008**: The package MUST expose dependency-registry types modeling `registry/dependencies.yml` plus a pure validator over those types that returns diagnostics for incoherent or incomplete registry models.
- **FR-009**: Registry-validator diagnostics MUST identify the offending entry and the violated rule (missing required field, or coherence violation) in a form a consumer can surface to a user.
- **FR-010**: Creating the package MUST NOT change any existing SDD behaviour: SDD MUST NOT yet reference the package, and every existing SDD artifact and test output MUST remain byte-identical (the re-type of SDD's `parseProviderRegistry`/`ProviderDescriptor` is a separate item, FS-GG/FS.GG.SDD#9).
- **FR-011**: The package MUST be packable into a distributable artifact and publishable to a feed; until the org feed exists (H4), publishing to a local feed MUST satisfy this requirement.
- **FR-012**: The package MUST be self-describing about its own contract version so a consumer can detect which contract version it is compiled against.
- **FR-013**: This work being a contract-change, the cross-repo dependency registry and its compatibility projection MUST be updated to register the `FS.GG.Contracts` package surface, linking this item as the tracking reference (per the coordination protocol and ADR-0001).
- **FR-014**: The package MUST NOT embed any provider-specific, rendering-specific, or Governance-runtime identity (no concrete package id, template id, path, command, or docs URL) — it defines generic contract shapes only.

### Key Entities

- **Schema contract entry**: a single `.fsgg` artifact's typed shape paired with its named schema version constant; the unit of "one fact in one place."
- **Provider descriptor**: the typed model of a `.fsgg/providers.yml` entry — existing identity/parameter fields plus optional build/test/run/verify commands and a canonical name parameter.
- **Dependency registry model**: the typed representation of `registry/dependencies.yml` — the cross-repo dependency edges and version-compatibility facts that the coherence backbone enforces.
- **Registry validator result**: success, or a set of diagnostics each naming an entry and a violated coherence/completeness rule.
- **Contract version**: the SemVer identity of the published package surface, used by consumers to detect what they compiled against.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the `.fsgg` schemas named in the contract (10 named schemas) are represented in the package, each with a typed record and a named version constant.
- **SC-002**: For every SDD-owned schema, the package's version constant and record shape equal the values/fields SDD emits today (verified in this item). The downstream guarantee that a consumer re-typing an SDD-owned `.fsgg` artifact onto these records produces **zero byte difference** from today's artifact is a property of the re-type item FS-GG/FS.GG.SDD#9 and is measured there; this item asserts only the constant/shape equality that makes that guarantee achievable.
- **SC-003**: A provider descriptor that declares no build/test/run/verify commands yields behaviour identical to today's defaults (no observable change for existing providers, which declare none).
- **SC-004**: The package carries zero third-party package dependencies (BCL-only verified from its dependency closure).
- **SC-005**: The package builds and produces a distributable artifact that is installable and consumable from a local feed.
- **SC-006**: The entire existing SDD test suite passes unchanged after the package is added, with no modification to any existing SDD artifact output (additive guarantee).
- **SC-007**: The registry validator accepts at least one representative coherent registry model with no diagnostics and rejects at least one incoherent and one incomplete model, each with a diagnostic naming the offending entry.
- **SC-008**: The cross-repo dependency registry and its compatibility projection record the new package surface and link this item as the tracking reference.

## Assumptions

- **Create-only scope**: this item creates and publishes the package; re-typing SDD's `parseProviderRegistry`/`ProviderDescriptor` and the acceptance probes onto it is the separate, blocked item FS-GG/FS.GG.SDD#9, and the Governance/Templates re-types are their own items. The package is therefore the canonical definition that consumers migrate to later, not an immediate rewiring of SDD.
- **All named schemas included**: the contract explicitly enumerates Governance-owned schemas (governance, policy, capabilities, tooling) alongside SDD-owned ones; all are declared in the package so every consumer re-types onto one source. SDD-owned versions match today's emitted values (the in-repo source of truth: `src/FS.GG.SDD.Artifacts`). Governance-owned shapes/versions are **declared to the Governance published schema reference**, which is the authoritative external source — they are not SDD-emitted and are therefore not asserted against any SDD output. Tests for the Governance-owned constants verify the value the package *declares to that reference* (currently `1`), not an SDD-emitted value, and must label the source accordingly; if the Governance published reference is not yet pinned in-repo, the declared value is provisional and tracked to the Governance counterpart item rather than treated as a verified SDD fact.
- **BCL-only means no third-party packages**: the package owns typed models and pure validators; YAML/JSON (de)serialization remains at each consumer's existing edge (e.g. SDD.Artifacts' YAML reader), so the contract library stays dependency-free.
- **Local feed for now**: "publish to the feed" is satisfied by a local (folder-based) package feed until the org feed lands in H4; no real published feed is in scope here.
- **Module organization**: the package groups its surface into a schemas area (typed records + version constants), a provider area (extended descriptor), and a registry area (types + validator), per the item's stated module breakdown (`Fsgg.Schemas` / `Fsgg.Provider` / `Fsgg.Registry`).
- **Target framework / language**: the package targets the repo's standard `net10.0` and is authored in F#, consistent with the rest of SDD, and sits at or below SDD.Artifacts in project layering.
- **Defaults preserve behaviour**: every default in the extended provider descriptor (absent commands, `name` parameter default) reproduces current behaviour so existing providers and the offline acceptance loop are observably unchanged.

## Dependencies

- **FS-GG/.github#16** — Pillar 2 of the homogeneous build · contracts · auto-update initiative (parent epic).
- **FS-GG/FS.GG.SDD#7** (H1, delivered) — declared-or-default acceptance probes; the extended descriptor's build/run fields are shaped to be a 1:1 forward-compatible read of that work.
- **Unblocks** FS-GG/FS.GG.SDD#9 (re-type SDD onto the package) and the Governance/Templates re-type items.
- **Cross-repo registry** (`FS-GG/.github` → `registry/dependencies.yml` + `docs/registry/compatibility.md`) must be updated as part of resolution (contract-change).
