# Phase 1 Data Model: Release and Distribution Readiness

The new public surface lives in `FS.GG.SDD.Artifacts.ReleaseContract`. All types
are records/DUs (Constitution IV). Serialization is canonical and deterministic
(Constitution II, FR-008). Nothing here changes an authored-source or existing
generated-view schema (FR-013); it *describes and locks* them.

## Entities

### PackageVersionIdentity
The declared semantic version of the release, shared by all `FS.GG.SDD.*`
packages and the `fsgg-sdd` CLI.

| Field | Type | Notes |
|---|---|---|
| `Version` | `string` (SemVer) | Single source from `Directory.Build.props`. |
| `Channel` | `ReleaseChannel` | `PreRelease` (`0.x`) or `Stable` (`>=1.0`). |
| `PackageIds` | `string list` | `FS.GG.SDD.Artifacts`, `FS.GG.SDD.Commands`, `FS.GG.SDD.Cli`. |
| `CliCommandName` | `string` | `fsgg-sdd`. |

*Validation*: `Version` MUST parse as SemVer; `Channel` MUST be consistent with
`Version` (major `0` ⇒ `PreRelease`). Maps spec entity *Package/CLI version
identity*; satisfies FR-003.

### ChangeClass (DU)
The classification of a public-contract change, driving the bump rule.

`Breaking | Additive | Clarifying`

*Mapping* (FR-001, research R3): `Breaking → major (+ migration note)`,
`Additive → minor`, `Clarifying → patch`. Pre-1.0: `Breaking` is allowed on a
minor bump but still requires a note.

### StabilityClass (DU)
Per-contract / per-field stability.

`Stable | AdditiveOptional | Experimental`

*Validation*: a `Stable` contract changing shape ⇒ `Breaking`; an
`AdditiveOptional` contract may gain optional fields as `Additive`. Satisfies
FR-004 stability classification.

### CompatibilityMatrixEntry
A per-release-line compatibility record.

| Field | Type | Notes |
|---|---|---|
| `SddVersionLine` | `string` | e.g. `0.2.x`. |
| `SpecKitRange` | `string` | Supported Spec Kit version range. |
| `GovernanceContractVersionRange` | `string option` | Supported handoff `contractVersion` range — **optional integration fact** (FR-002, edge case "No Governance installed"). |

*Validation*: `GovernanceContractVersionRange = None` is valid and MUST NOT block
readiness. Maps spec entity *Compatibility matrix entry*.

### ContractKind (DU)
Identifies what kind of public output an entry documents, and its serialization
**format** — because the catalog covers both JSON machine contracts and Markdown
projections (resolves analysis findings U1/U2).

```fsharp
type ContractFormat =
    | Json            // machine contract: field inventory is JSON fields
    | Markdown        // projection (Constitution II authoring surface): inventory is sections

type ContractKind =
    | GeneratedView of GeneratedViewKind * ContractFormat
    | CommandOutput                                   // the `--json` CommandReport (Json)
```

*Rationale*: `summary.md`, and the `agent-commands/` `commands.md`/`skills.md`,
are **Markdown projections**, not machine contracts. They still get a stability
classification and an inventory (FR-004), but the inventory enumerates **document
sections**, not JSON fields, and `SchemaVersion` is the version of the *generator*
that produces them, not a JSON `schemaVersion`. This keeps Constitution II intact
(Markdown stays an authoring surface) while still documenting and locking their
stability.

### SchemaReferenceEntry
The documented shape of **one** public generated artifact (or sub-file) or `--json`
report. The `AgentCommands` view emits three sub-files of **differing** stability,
so it is documented as **one entry per sub-file**, not one bundled row (resolves
U2):

| Sub-file | Format | Stability (starting) |
|---|---|---|
| `agent-commands/<target>/guidance.json` | Json | AdditiveOptional (machine contract) |
| `agent-commands/<target>/commands.md` | Markdown | AdditiveOptional (projection) |
| `agent-commands/<target>/skills.md` | Markdown | AdditiveOptional (projection) |

| Field | Type | Notes |
|---|---|---|
| `Contract` | `string` | e.g. `work-model.json`, `agent-commands/<target>/guidance.json`, `command-report (--json)`. |
| `Kind` | `ContractKind` | Carries the `GeneratedViewKind` (+ format) or `CommandOutput`. |
| `SchemaVersion` | `int` | JSON contract's `schemaVersion`, or the generator version for a Markdown projection. |
| `ContractVersion` | `string option` | Cross-repo `contractVersion` where applicable (handoff). |
| `Stability` | `StabilityClass` | Per R4. |
| `Determinism` | `string` | Determinism guarantee statement. |
| `Inventory` | `InventoryItem list` | JSON: field names; Markdown: section names. Each with per-item stability (FR-004). |
| `SourceArtifact` | `ArtifactRef` | Back-reference to the authoritative structured contract / projection source (FR-005). |
| `BaselinePresent` | `bool` | Whether a locking golden baseline exists (FR-006/FR-012). |

*Validation*: every public output (counting each `agent-commands/` sub-file
separately) MUST have exactly one entry; an entry with no `SourceArtifact` or
`BaselinePresent = false` ⇒ **not-ready** (FR-012, SC-002). Maps spec entity
*Schema reference entry*.

### InventoryItem
`{ Name: string; Kind: InventoryKind; Stability: StabilityClass }` where
`InventoryKind = JsonField | MarkdownSection` — supports per-field / per-section
stability for both formats (FR-004; resolves U1).

### MigrationNoteRef
A per-release record pointer for breaking changes.

| Field | Type | Notes |
|---|---|---|
| `Version` | `string` | Release the note covers. |
| `Path` | `string` | `docs/release/migrations/<version>.md`. |
| `BreakingChanges` | `string list` | Enumerated breaking public-contract changes (FR-010). |

*Validation*: required iff the release contains a `Breaking` change; MUST NOT be
present for additive-only releases (FR-009/SC-006). Maps spec entity *Migration
note*.

### ReleaseReadiness (top-level envelope)
The authoritative machine contract serialized to
`docs/release/release-readiness.json`.

| Field | Type | Notes |
|---|---|---|
| `SchemaVersion` | `int` | This contract's own schema version (starts at `1`). |
| `GeneratorVersion` | `GeneratorVersion` | Provenance (reuses `SchemaVersion.GeneratorVersion`). |
| `Identity` | `PackageVersionIdentity` | FR-003. |
| `Compatibility` | `CompatibilityMatrixEntry list` | FR-002. |
| `Catalog` | `SchemaReferenceEntry list` | FR-004 — one entry per public output. |
| `Migrations` | `MigrationNoteRef list` | FR-009/FR-010. |

*Validation*: `Catalog` MUST cover every `GeneratedViewKind` (counting each
`agent-commands/` sub-file separately) plus the public command-output report
(SC-002). Serialization MUST be byte-stable (FR-008/SC-005).

## Public-contract baseline (test-layer entity)
Not an F# type — a checked-in golden fixture (string `.baseline` or
`.approved.json`) under `tests/**/baselines/`. One per public schema /
representative produced artifact / `--json` report, plus the existing
`PublicSurface.baseline` `.fsi` surface baselines. Drift ⇒ failing test with an
actionable diff (FR-007/SC-004). Maps spec entity *Public-contract baseline*.

## Release-readiness check (pure function)
`evaluate : ReleaseReadiness -> ProducedArtifact list -> Diagnostic list`

where the input is a caller-supplied snapshot of what a real lifecycle run
produced (resolves U3 — the input type is pinned, not left open):

```fsharp
type ProducedArtifact =
    { Contract: string                 // matches SchemaReferenceEntry.Contract
      Source: ArtifactRef              // where it was produced (readiness/<id>/…)
      Inventory: string list }         // observed JSON fields or Markdown sections
```

The check is a pure fold over `(catalog, produced)`; it performs **no** file I/O
itself — the caller (the test suite / conformance harness) reads the artifacts and
passes the snapshots in, preserving the Constitution V pure-validator exemption.

Returns one diagnostic per gap:
- public output with no `Catalog` entry → *not-ready* (FR-012);
- `Catalog` entry with `BaselinePresent = false` or missing `SourceArtifact` →
  *not-ready* (FR-012);
- produced artifact item not in its entry's `Inventory`, or documented item absent
  from the produced artifact → *drift / not-ready* (FR-015, structured wins);
- `Breaking` change with no `MigrationNoteRef` → *not-ready* (FR-009).

Empty diagnostic list ⇒ release-ready. Pure, deterministic, no I/O beyond the
caller-supplied produced-artifact snapshots (Constitution V exemption).

## State / lifecycle
None added. `ReleaseReadiness` is a static repo-level contract regenerated when a
public surface changes; it is **not** a per-work-item lifecycle artifact and does
**not** participate in `charter … ship`, refresh, or agent guidance (FR-013).
