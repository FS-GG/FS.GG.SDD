# Phase 1 Data Model: FS.GG.Contracts Package

All types are pure (no I/O, no mutation, no third-party dependency). Records and DUs
only. F# shapes shown for clarity; the authoritative public surface is `contracts/*.fsi`.

## Module `ContractVersion` (FR-012)

| Member | Type | Meaning |
|--------|------|---------|
| `value` | `string` | The package contract SemVer, `"1.0.0"`. |
| `major` / `minor` / `patch` | `int` | Structured components for compatibility checks. |

Self-report only; carries no behaviour. Single authoritative value (no second place can
disagree — spec Edge Case "future version bump").

## Module `Schemas` — typed shape + version constant per `.fsgg` schema (FR-004/005)

### `SchemaContractEntry`

The unit of "one fact in one place": a schema's name paired with its version constant.

| Field | Type | Notes |
|-------|------|-------|
| `Name` | `string` | Contract name, e.g. `"providers"`, `"governance-handoff"`. |
| `SchemaVersion` | `int` | The integer schema version SDD emits (all `1` today). |
| `ContractVersion` | `string option` | Present where the artifact also carries a string contract version (`governance-handoff` → `"1.0.0"`). |
| `Owner` | `SchemaOwner` | `Sdd` or `Governance`. |

### `SchemaOwner` (DU)

`Sdd | Governance` — distinguishes SDD-emitted schemas (version must equal today's
output) from Governance-owned schemas declared to the published reference.

### Typed record per schema + named version constant

For each of the **10 named schemas** the module exposes (a) a typed record mirroring the
artifact shape and (b) a named version constant, using BCL/`FSharp.Core` types only.
Record-shape provenance differs by owner:

- **SDD-owned** (`providers`, `project`, `sdd`, `agents`, `scaffold-provenance`,
  `governance-handoff`): the record reproduces today's field set, mirrored from the
  in-repo source of truth — the SDD `LifecycleArtifacts/*` configs and the
  `ScaffoldProvenanceRecord`/`GovernanceHandoff` records in `src/FS.GG.SDD.Artifacts`.
  These are fully specified here because their source is in-repo.
- **Governance-owned** (`governance`, `policy`, `capabilities`, `tooling`): their
  authoritative field sets live in the **Governance published schema reference**, a
  cross-repo source not pinned in this repo. This create-only item therefore declares
  each as a **minimal, explicitly-provisional record sourced from that reference**
  (a documented placeholder shape, not an invented field set), carrying its
  declared version constant and an `Owner = Governance` entry. The full field set is
  filled in when the Governance published reference is pinned (tracked to the
  Governance counterpart item); the package never fabricates Governance field shapes
  that could drift from the reference. A `// SOURCE: Governance published reference (TBD-link)`
  comment marks each such record.

| Schema | Record (shape) | Version constant | Owner | Value |
|--------|----------------|------------------|-------|-------|
| `providers` | `ProvidersSchema` | `providersVersion` | Sdd | `1` |
| `project` | `ProjectSchema` | `projectVersion` | Sdd | `1` |
| `sdd` | `SddSchema` | `sddVersion` | Sdd | `1` |
| `agents` | `AgentsSchema` | `agentsVersion` | Sdd | `1` |
| `scaffold-provenance` | `ScaffoldProvenanceSchema` | `scaffoldProvenanceVersion` | Sdd | `1` |
| `governance-handoff` | `GovernanceHandoffSchema` | `governanceHandoffVersion` (+ `governanceHandoffContractVersion = "1.0.0"`) | Sdd | `1` |
| `governance` | `GovernanceSchema` | `governanceVersion` | Governance | `1` (published ref) |
| `policy` | `PolicySchema` | `policyVersion` | Governance | `1` (published ref) |
| `capabilities` | `CapabilitiesSchema` | `capabilitiesVersion` | Governance | `1` (published ref) |
| `tooling` | `ToolingSchema` | `toolingVersion` | Governance | `1` (published ref) |

**Validation rules**: each SDD-owned constant MUST equal the value SDD emits today
(asserted by test, FR-005/SC-002). `entries : SchemaContractEntry list` enumerates all
10 for the "represented?" check (SC-001).

## Module `Provider` — extended descriptor (FR-006/007)

### `DeclaredCommand`

| Field | Type | Notes |
|-------|------|-------|
| `Executable` | `string` | Blank/whitespace ⇒ malformed declaration (not "absent"). |
| `Arguments` | `string list` | Passed verbatim, in order. |

Mirrors the Feature 035 H1 shape for a 1:1 forward-compatible read.

### `ProviderParameterSpec` (preserved from SDD, unchanged)

| Field | Type |
|-------|------|
| `Key` | `string` |
| `Required` | `bool` |
| `Default` | `string option` |

### `ProviderDescriptor` (extended — additive over SDD's current record)

| Field | Type | Status | Notes |
|-------|------|--------|-------|
| `Name` | `string` | preserved | provider identity |
| `ContractVersion` | `string` | preserved | provider contract SemVer, e.g. `"1.0.0"` |
| `TemplateId` | `string` | preserved | opaque to SDD |
| `Source` | `string` | preserved | opaque to SDD |
| `Parameters` | `ProviderParameterSpec list` | preserved | declared `--param`s |
| `Build` | `DeclaredCommand option` | **new** | absent ⇒ platform default |
| `Test` | `DeclaredCommand option` | **new** | absent ⇒ platform default |
| `Run` | `DeclaredCommand option` | **new** | absent ⇒ platform default |
| `Verify` | `DeclaredCommand option` | **new** | absent ⇒ platform default |
| `NameParameter` | `string` | **new** | canonical product-name input; defaults to `"name"` |

**Validation / default rules**:
- All four command fields default to `None`; consuming behaviour then equals today's
  defaults (FR-006 Scenario 1, SC-003).
- `defaultNameParameter : string = "name"`; a helper resolves a descriptor with no
  declared name parameter to `"name"` (FR-007, Scenario 3).
- A declared command with blank `Executable` is **malformed**, distinct from absent
  (spec Edge Case); a predicate exposes this so consumers surface it (Principle VIII).
- The five preserved fields are byte-for-byte the current SDD `ProviderDescriptor`
  (Scenario 4 — additive).

## Module `Registry` — dependency-registry model + validator (FR-008/009)

### `RegistryComponent`

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `string` | repo/package identity, e.g. `"FS.GG.Contracts"`. |
| `Version` | `string` | declared SemVer of this component. |

### `DependencyEdge`

| Field | Type | Notes |
|-------|------|-------|
| `Consumer` | `string` | component id that depends. |
| `Provider` | `string` | component id depended upon. |
| `CompatibleRange` | `string` | declared compatible version range over the provider. |

### `RegistryModel`

| Field | Type |
|-------|------|
| `Components` | `RegistryComponent list` |
| `Edges` | `DependencyEdge list` |

### `RegistryDiagnostic`

| Field | Type | Notes |
|-------|------|-------|
| `Entry` | `string` | names the offending component id or edge. |
| `Rule` | `RegistryRule` | the coherence/completeness rule violated. |
| `Message` | `string` | human-surfacable description. |

### `RegistryRule` (DU)

| Case | Meaning |
|------|---------|
| `MissingField of fieldName: string` | a required field is absent/blank (incomplete entry). |
| `UnknownComponent` | an edge references a component id not in `Components`. |
| `IncompatibleVersion` | an edge's `CompatibleRange` excludes the referenced provider's declared `Version`. |
| `MalformedVersion` | a version/range string is not parseable SemVer. |

### `ValidationResult` (DU)

`Valid | Invalid of RegistryDiagnostic list` — success has no diagnostics (SC-007).

### `validate : RegistryModel -> ValidationResult`

Pure. Rules evaluated:
1. Every component has non-blank `Id` and parseable `Version` (`MissingField` /
   `MalformedVersion`).
2. Every edge has non-blank `Consumer`/`Provider`/`CompatibleRange` (`MissingField`).
3. Every edge's `Consumer` and `Provider` exist in `Components` (`UnknownComponent`).
4. Each edge's `CompatibleRange` includes the provider component's declared `Version`
   (`IncompatibleVersion`).

Diagnostics name the entry and rule (FR-009). SemVer parse/compare is a small BCL-only
helper (R5) — no third-party SemVer package.

## Cross-entity invariants

- **One-fact-in-one-place**: a schema version exists in exactly one place — its
  `Schemas` constant. No consumer re-encodes it (US1).
- **Additive**: every `Provider` extension field is optional or defaulted so an
  unextended descriptor behaves as today (US2, SC-003).
- **Purity**: nothing in the package reads/writes files or calls a third party
  (SC-004); validation is more than shape-checking — it includes semantic coherence
  (spec Edge Case).
