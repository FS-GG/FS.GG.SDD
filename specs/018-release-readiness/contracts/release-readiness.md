# Contract: `release-readiness.json` (SDD-owned release contract)

**Status**: SDD-owned machine contract. **`schemaVersion`: 1.** Authoritative
copy lives at `docs/release/release-readiness.json`; the versioning policy,
compatibility matrix, and schema reference docs are **projections** of it
(FR-005). On any disagreement between this artifact and a produced lifecycle
artifact or the real public surface, the **produced/structured artifact is
authoritative** and the discrepancy is a detectable failure (FR-015).

This contract introduces **no** lifecycle stage and changes **no** authored-source
schema (FR-013). It is **entirely SDD-owned**: it carries no Governance gate,
route, profile, freshness, or publish/provenance data (FR-014); Governance
compatibility appears only as an optional string range.

## Top-level shape

```jsonc
{
  "schemaVersion": 1,
  "generatorVersion": { "id": "FS.GG.SDD.Artifacts", "version": "0.2.0" },
  "identity": {
    "version": "0.2.0",
    "channel": "preRelease",            // preRelease (0.x) | stable (>=1.0)
    "packageIds": [
      "FS.GG.SDD.Artifacts",
      "FS.GG.SDD.Commands",
      "FS.GG.SDD.Cli"
    ],
    "cliCommandName": "fsgg-sdd"
  },
  "compatibility": [
    {
      "sddVersionLine": "0.2.x",
      "specKitRange": ">=<pinned>",
      "governanceContractVersionRange": "1.x"   // OR null when not integrated
    }
  ],
  "catalog": [
    {
      "contract": "work-model.json",
      "kind": { "generatedView": "WorkModel" },  // or { "commandOutput": true }
      "schemaVersion": 1,
      "contractVersion": null,                    // non-null only for cross-repo contracts
      "stability": "additiveOptional",            // stable | additiveOptional | experimental
      "determinism": "byte-stable; canonical key order; no clock/path/ANSI",
      "fields": [ { "name": "schemaVersion", "stability": "stable" } ],
      "sourceArtifact": { "kind": "generatedView", "path": "readiness/<id>/work-model.json" },
      "baselinePresent": true
    }
  ],
  "migrations": [
    {
      "version": "1.0.0",
      "path": "docs/release/migrations/1.0.0.md",
      "breakingChanges": [ "…" ]
    }
  ]
}
```

## Catalog coverage requirement (SC-002)

`catalog[]` MUST contain exactly one entry for each public output:

| Output | `kind` | `contractVersion`? |
|---|---|---|
| `work-model.json` | `generatedView: WorkModel` | no |
| `analysis.json` | `generatedView: Analysis` | no |
| `verify.json` | `generatedView: Verify` | no |
| `ship.json` | `generatedView: Ship` | no |
| `governance-handoff.json` | `generatedView: GovernanceHandoff` | **yes** (cross-repo) |
| `summary.md` *(Markdown projection)* | `generatedView: Summary` | no |
| `agent-commands/<target>/guidance.json` | `generatedView: AgentCommands` | no |
| `agent-commands/<target>/commands.md` *(Markdown projection)* | `generatedView: AgentCommands` | no |
| `agent-commands/<target>/skills.md` *(Markdown projection)* | `generatedView: AgentCommands` | no |
| `<command> --json` report | `commandOutput` | no |

The `AgentCommands` view is documented as **one entry per sub-file** (the `.json`
is a machine contract; the two `.md` are projections). For Markdown projections,
`schemaVersion` carries the generator version and the inventory enumerates
document sections, not JSON fields (see [schema-reference.md](schema-reference.md)).

An output with no entry, an entry with `baselinePresent = false`, or an entry with
no `sourceArtifact` MUST be reported **not-ready** (FR-012). The check fails by
*absence*, never passes by it.

## Field semantics

- `identity.version` — single SemVer source (`Directory.Build.props`); equals
  every package's `<Version>` and is consistent with `generatorVersion.version`
  (FR-003, research R1).
- `channel` — derived from `version`: major `0` ⇒ `preRelease`.
- `compatibility[].specKitRange` — supported Spec Kit version range (FR-002).
- `compatibility[].governanceContractVersionRange` — supported handoff
  `contractVersion` range, or `null`; **optional integration fact** that MUST NOT
  block readiness (FR-002, edge case "No Governance installed").
- `catalog[].stability` / `fields[].stability` — `stable | additiveOptional |
  experimental` (FR-004, research R4).
- `catalog[].contractVersion` — present only where a cross-repo `contractVersion`
  exists (currently `governance-handoff.json`); moves independently of
  `schemaVersion` (edge case "Schema version vs contract version divergence").
- `migrations[]` — present iff a release contains a breaking change; absent for
  additive-only releases (FR-009).

## Determinism (FR-008 / SC-005)

Serialized through the canonical `Serialization` module: stable key ordering,
UTF-8, trailing-newline normalized, **no** clock, duration, host path, ordering
nondeterminism, or ANSI styling. Producing the file twice over identical inputs
yields byte-identical output.

## Versioning of this contract

Additive field additions bump this contract's `schemaVersion` **minor**; a
breaking shape change bumps `schemaVersion` **major** and requires its own
migration note — the contract obeys the policy it documents.
