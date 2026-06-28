# Phase 1 Data Model: Typed Registry Validator

The typed model of `registry/dependencies.yml`, added to `Fsgg.Registry` in
`FS.GG.Contracts` (BCL-only). Shapes mirror the real file (see
[contracts/registry-document.md](./contracts/registry-document.md)). The legacy
`RegistryComponent`/`DependencyEdge`/`RegistryModel`/`validate` are unchanged and out of
scope here.

## Entities

### RegistryDocument (root)

| Field | Type | Notes |
|---|---|---|
| `SchemaVersion` | `int` | Top-level `schemaVersion`. Must be an integer. |
| `Repos` | `RegistryRepo list` | From the `repos:` mapping; must be non-empty. |
| `Contracts` | `ContractEntry list` | From `contracts:`; must be non-empty. |
| `Dependencies` | `DependencyEdge2 list` | From `dependencies:`; may be empty. |
| `Coherence` | `CoherenceEntry list` | From `coherence:`; may be empty. |

> Prose/informational keys (`updated`, `surface` folded bodies, `notes`, `summary`,
> `parameters`, `behavior-break`, …) are **not** modeled beyond presence/non-blank checks
> where the authority requires them. Only declared scalar fields are authoritative
> (constitution II).

### RegistryRepo

| Field | Type | Notes |
|---|---|---|
| `Id` | `string` | The mapping key (`sdd`, `rendering`, `governance`, `templates`). |
| `Name` | `string` | `name:`; must be non-blank. |
| `Role` | `string` | `role:`; must be non-blank. |

### ContractEntry

| Field | Type | Notes |
|---|---|---|
| `Id` | `string` | `id:`; non-blank; unique across contracts. |
| `Version` | `string` | `version:`; non-blank (absent/blank → `MissingField`); when present, valid per the version grammar (R3). |
| `Owner` | `string` | `owner:`; non-blank; ∈ repo ids ∪ {`github`}. |
| `Surface` | `string` | `surface:`; non-blank (scalar or folded scalar). |
| `Consumers` | `string list` | `consumers:`; each ∈ repo ids. |
| `PackageVersion` | `string option` | `package-version:`; if present, valid version. |
| `Range` | `string option` | `range:`; if present, well-formed range (e.g. `1.x`). |

### DependencyEdge2 (real repo→repo edge)

| Field | Type | Notes |
|---|---|---|
| `From` | `string` | `from:`; non-blank; ∈ repo ids. |
| `To` | `string` | `to:`; non-blank; ∈ repo ids. |
| `Via` | `string` | `via:`; **free-text, not contract-checked** (R4). |

### CoherenceEntry

| Field | Type | Notes |
|---|---|---|
| `Id` | `string` | `id:`; non-blank. |
| `Coherent` | `bool` | `coherent:`; boolean. |

> Other coherence keys (`owner`, `summary`, `tracking`, …) are not validated here.

## Diagnostics & rules

Reuses `RegistryDiagnostic { Entry; Rule; Message }` and `ValidationResult = Valid |
Invalid of RegistryDiagnostic list`. `RegistryRule` is **extended additively**:

| Rule | Existing? | Fires when |
|---|---|---|
| `MissingField of fieldName` | yes | A required scalar/list field is absent or blank. |
| `UnknownComponent` | yes | `owner`/`consumers`/edge `from`/`to` references a non-repo id (owner also allows `github`). |
| `MalformedVersion` | yes | A `version`/`package-version`/`range`/`schemaVersion` value violates the grammar (R3). |
| `IncompatibleVersion` | yes | (unused by `validateDocument` on the canonical file; retained for the legacy validator.) |
| `DuplicateComponent` | **NEW** | Two contracts share an `id`. |
| `MalformedDocument` | **NEW** | The document or a node is not the expected shape (e.g. root not a mapping, contract not a mapping, unparseable file). |

## Validation order (deterministic — R5)

`root → repos (file order) → contracts (file order) → dependencies (file order) →
coherence (file order)`, diagnostics appended in encounter order. `Valid` iff the list is
empty.

## Version grammar (R3)

- Valid `version`/`package-version`: SemVer `major.minor.patch` + optional
  `-prerelease`/`+build` (`1.0.0`, `0.1.52-preview.1`) **or** bare integer (`1`, `2`).
- Valid `range`: permissive (`1.x`, comparator sets).
- `schemaVersion`: integer.

## Validation-rule → requirement / success-criteria map

| Rule / behavior | Spec ref |
|---|---|
| `load` file → model or parse failure | FR-001, US1 |
| Model understands `contracts[]`/`dependencies[]`/`coherence[]` | FR-002, US2 |
| repo→repo edges do not trip `UnknownComponent` | FR-003, US2-S1, SC-002 |
| bare-integer versions accepted | FR-004, US3-S1, SC-002 |
| `1.x` ranges accepted | FR-005, US3-S2, SC-002 |
| prerelease versions accepted (`0.1.52-preview.1`) | FR-005 (extended), SC-001 |
| genuine defects still reported | FR-006, US2-S3 (relaxed per R4), US3-S3, SC-002 |
| deterministic verdict | FR-007, SC-004 |
| canonical file → Valid, zero diagnostics | FR-008, SC-001 |
| additive, no major bump | FR-009 |
| sufficient to retire the Python stand-in | FR-010, SC-003, SC-005 |
