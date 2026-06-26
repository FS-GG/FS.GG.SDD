# Contract: `.fsgg/scaffold-provenance.json` (schema v1)

**schemaVersion**: `1` · **Owner**: SDD writes it; it marks files as externally owned ·
**Determinism**: byte-stable, canonical key order, sorted `producedPaths`, no
clock/absolute-path/ANSI.

Project-level artifact recording who produced runtime files during `fsgg-sdd scaffold`,
the provider contract version, the produced paths, and that ongoing ownership lies
**outside** SDD (FR-006). Consumed by `fsgg-sdd refresh` to exclude these paths from
generated-view currency (FR-007 / SC-007).

## Shape

```json
{
  "schemaVersion": 1,
  "generator": { "id": "FS.GG.SDD.Artifacts", "version": "<assembly-version>" },
  "providerName": "<descriptor name>",
  "providerContractVersion": "1.0.0",
  "templateRef": "<opaque template id echoed from descriptor>",
  "outcome": "providerSucceeded",
  "producedPaths": [
    { "path": "src/Product/Product.fsproj", "owner": "generatedProduct" },
    { "path": "src/Product/Program.fs",     "owner": "generatedProduct" }
  ]
}
```

## Field rules

| Field | Type | Rule |
|---|---|---|
| `schemaVersion` | int | `1`; unsupported major → `unsupportedSchemaVersion` on read |
| `generator` | object | `{ id, version }`, same convention as other generated artifacts |
| `providerName` | string | the resolved descriptor name |
| `providerContractVersion` | string | semver the provider declared |
| `templateRef` | string | opaque echo of the descriptor `templateId` (no SDD interpretation) |
| `outcome` | string | `outcomeValue` of `ScaffoldOutcome` (`providerSucceeded` / `providerSucceededEmpty` / `providerFailed`) — a persisted record is only written when the provider actually ran |
| `producedPaths` | array | sorted by `path`; each `{ path (relative, project-root), owner }` |
| `producedPaths[].owner` | string | always `"generatedProduct"` (`ArtifactOwner.GeneratedProduct`) |

## Determinism & serialization

- Written via the existing `Json/JsonWriters.fs` conventions (`Utf8JsonWriter`, indented).
- `producedPaths` sorted with `Sorted` ordering by `path` before write.
- No timestamps; paths are project-root-relative (never absolute) — matches the report's
  `ProjectRoot = "."` rule.

## Malformed / absent handling

- **Absent**: not an error. `refresh` behaves exactly as before this feature (additive).
- **Malformed**: `scaffold.provenanceMalformed` (user-input class). Readers treat the file
  as absent for safety — nothing is silently regenerated — and surface the diagnostic.

## Release catalog posture

Cataloged in `docs/release/release-readiness.json` as a produced lifecycle artifact
(`owner: sdd`, `requiredBySdd: false`) with `stability: additiveOptional`. Unlike
generated views it represents *externally-owned output references*; the schema-reference
note states it is the authority for refresh exclusion. (It is a real produced artifact,
so — unlike the `validation-report` — it **is** added to the catalog.)
