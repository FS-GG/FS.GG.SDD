# Phase 1 Data Model — Committed Compact Ship Verdict

Feature: `092-committed-ship-verdict`

Two persisted shapes move: the **new** `readiness/<id>/ship-verdict.json` view, and one **additive
field** on the release-catalog entry. One in-memory parse record gains one field.

---

## 1. `ShipVerdict` (new) — `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/ShipVerdict.fs(i)`

The compact, committed projection of `ship.json`. Every field is a fact **copied** from the
`ShipView` it projects; nothing is recomputed, except `SourcesDigest`, which *summarizes* the
`sources[]` inventory it replaces.

```fsharp
type ShipVerdict =
    { SchemaVersion: SchemaVersion          // = ship.json schemaVersion
      ViewVersion: string                   // = ship.json viewVersion
      WorkId: string
      Stage: string                         // "ship"
      Status: string                        // e.g. "shipReady"
      Generator: string                     // "<id>/<version>"
      SourcesDigest: SourceDigest           // { Algorithm = "sha256"; Value = … }
      VerificationReadinessStatus: string   // = ship.json verificationReadiness.status
      DispositionState: string              // = ship.json disposition.state
      DispositionBlockingFindingIds: string list
      Readiness: string }
```

Public surface: `fromShipView : ShipView -> ShipVerdict`, `toJson : ShipVerdict -> string`, and
`sourcesDigest : AnalysisSourceRecord list -> SourceDigest` (exposed so tests can recompute it
independently — SC-003).

### Serialized shape (canonical field order)

Field order mirrors `ship.json`'s own top-level order, with `sources` replaced **in place** by
`sourcesDigest`, so the verdict reads as an order-preserving projection. Rendered by
`Utf8JsonWriter(Indented = true)` — exactly **20 lines** with an empty `blockingFindingIds`.

```json
{
  "schemaVersion": 1,
  "viewVersion": "1.0",
  "workId": "068-readiness-golden",
  "stage": "ship",
  "status": "shipReady",
  "generator": "FS.GG.SDD.Artifacts/0.8.0",
  "sourcesDigest": {
    "algorithm": "sha256",
    "value": "78a32b33a4bb370f169ad4a44307d7f4c0fafc7741bea0d5a82f1a1d5ad5b117"
  },
  "verificationReadiness": {
    "status": "verificationReady"
  },
  "disposition": {
    "state": "shipReady",
    "blockingFindingIds": []
  },
  "readiness": "shipReady"
}
```

**Invariants**

| # | Invariant | Enforced by |
|---|---|---|
| V1 | Every field equals the corresponding `ship.json` fact | FR-002 / SC-002 |
| V2 | No field outside the list above | FR-002 / SC-002 |
| V3 | ≤ 20 lines when `blockingFindingIds` is empty; +1 line per blocking id otherwise | FR-004 / SC-001 |
| V4 | Byte-stable — no clock, no absolute path, no ANSI | FR-008 / SC-007 |
| V5 | Written **iff** `ship.json` is written (the `not hasBlocking` gate) | FR-005 / SC-008 |
| V6 | `ship` and `refresh` emit identical bytes (one shared pure projection) | FR-007 / SC-006 |

### `sourcesDigest` — canonical pre-image

`sources[]` in canonical (path-sorted) order, one line per source, joined with `\n`:

```
<path>|<algorithm>:<value>
```

hashed by the existing `SchemaVersion.sha256Text`, which returns the `SourceDigest =
{ Algorithm; Value }` the field serializes. Binding the **path** to its digest is what lets a later
reader prove the committed verdict corresponds to the committed sources (research D8). An empty
`sources[]` yields the well-known empty-string SHA-256 — a defined value, never an omitted field.

---

## 2. `ShipView` (existing) — one additive field

`Ship.parseShipView` currently flattens `disposition` to its `state` string and discards
`blockingFindingIds` (research D7). It gains:

```fsharp
    { …
      Disposition: string                        // unchanged — disposition.state
      DispositionBlockingFindingIds: string list // NEW — disposition.blockingFindingIds, sorted
      … }
```

Additive to a parse-only record. Moves `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline`.
`Disposition` keeps its name and meaning so no existing reader moves.

---

## 3. `SchemaReferenceEntry` (existing) — one additive field

`src/FS.GG.SDD.Artifacts/ReleaseContract.fsi`:

```fsharp
    type SchemaReferenceEntry =
        { …
          SourceArtifact: ArtifactRef
          BaselinePresent: bool
          DurableGenerated: bool }   // NEW — ADR-0026 §4; true only for ship-verdict.json
```

Serialized beside `baselinePresent` as `"durableGenerated": <bool>`; parsed with the same
`GetBoolean()` shape. The release-readiness `schemaVersion` does **not** move: a new optional field
is `AdditiveOptional` by the catalog's own stability rules.

`durableGenerated: true` is the marker from which the taxonomy doc's **durable** table is derived; it
partitions the `generatedView` catalog (research D3):

- regenerable block ≡ `sourceArtifact.kind == generatedView && !durableGenerated`
- durable-generated table ≡ `sourceArtifact.kind == generatedView && durableGenerated`

### The catalog entry

```
contract:        ship-verdict.json
kind:            GeneratedViewContract(ShipVerdict, Json)
schemaVersion:   1
contractVersion: (none — not a cross-repo contract, FR-018)
stability:       AdditiveOptional
determinism:     byte-stable; canonical key order; no clock/path/ANSI
inventory:       schemaVersion (Stable); viewVersion, workId, stage, status, generator,
                 sourcesDigest, verificationReadiness, disposition, readiness (AdditiveOptional)
sourceArtifact:  readiness/<id>/ship-verdict.json · kind generatedView · owner Sdd
baselinePresent: true
durableGenerated:true
```

---

## 4. `GeneratedViewKind` (existing) — one new case

```fsharp
type GeneratedViewKind =
    | WorkModel | Analysis | Verify | Ship
    | ShipVerdict            // NEW (feature 092 / ADR-0026)
    | Summary | AgentCommands | GovernanceHandoff
    | Other of string
```

`viewKindValue ShipVerdict = "shipVerdict"`. Forces the amendment of `ReleaseBoundaryTests`' T024
`known` set; `ReleaseReadinessCheckTests`' T019 then requires the catalog entry to exist (research
D4, FR-016).

---

## 5. `.gitignore` — two adoptions of one decision

**Seeded consumer fragment** (`Foundation.gitignoreSeedText`, whole-file no-clobber
`AgentGuidanceTarget`):

```gitignore
# FS.GG.SDD — regenerable lifecycle output (ADR-0018).
# Per-work-item readiness views are generated by fsgg-sdd and regenerated by
# `fsgg-sdd refresh`. They are transient: ignore by role, never commit, and do
# not re-include per feature. See docs/reference/artifact-taxonomy.md.
# Exception: the compact merge-boundary verdict is durable-generated (ADR-0026).
readiness/*/*
!readiness/*/ship-verdict.json
```

**This repository's dogfood rule** (`.gitignore`; SDD's readiness views land under
`specs/<feature>/readiness/<work-id>/`):

```gitignore
specs/*/readiness/*/*
!specs/*/readiness/*/ship-verdict.json
```

Root `readiness/<id>/` hand-pinned durable proofs are matched by **no** rule and stay committed.
`readiness/*/*` excludes directory *contents*, keeping the parent traversable so the negation fires;
`agent-commands/` stays ignored because the directory itself matches (research D1).

---

## 6. Command report — no schema change

`ship-verdict.json` appears in the existing `changedArtifacts` and `generatedViews` collections of
the `ship` and `refresh` reports, as `ship.json` and `governance-handoff.json` already do. No new
`CommandReport` block, no new field, no new diagnostic code.

`refresh` gains `"ship-verdict"` in `refreshCanonicalViews` and in its per-view currency buckets, so
it is reported blocked / refreshed / already-current exactly like its siblings.
