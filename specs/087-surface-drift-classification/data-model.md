# Phase 1 Data Model: Surface Drift Classification

All shapes are **in-memory command-report** facts. No on-disk artifact schema changes (FR-014).

## `ClassifiedEntry`

One classified drifted `.fsi`.

| Field | Type | Notes |
|---|---|---|
| `Path` | `string` | The drifted **source**-relative `.fsi` path (e.g. `src/Foo/Bar.fsi`). |
| `Classification` | `string` | `additive` \| `breaking` \| `cosmetic`. |
| `RecommendedBump` | `string` | Per-file bump: `major` \| `minor` \| `none`. |
| `AddedMembers` | `string list` | Member tokens present in source, absent in baseline. Sorted. |
| `RemovedOrChangedMembers` | `string list` | Member tokens present in baseline, absent in source. Sorted. |
| `UnparseableFallback` | `bool` | True when the source yielded no member tokens and was classified `breaking` conservatively (FR-011). |

**Invariants**: exactly one of the three classifications; `breaking` ⇔ `RemovedOrChangedMembers`
non-empty **or** `UnparseableFallback`; `additive` ⇔ `RemovedOrChangedMembers` empty and
`AddedMembers` non-empty; `cosmetic` ⇔ both member lists empty (bytes differ but member set equal).

## `SurfaceClassification`

The run-level classification fact.

| Field | Type | Notes |
|---|---|---|
| `Verdict` | `string` | Most-severe entry: `breaking` \| `additive` \| `cosmetic` \| `none` (none ⇔ no drifted files). |
| `RecommendedBump` | `string` | Mapped from `Verdict`: breaking→`major`, additive→`minor`, cosmetic→`none`, none→`none`. |
| `Entries` | `ClassifiedEntry list` | One per drifted file, sorted by `Path`. Empty when no drift. |

**Severity order** (for `Verdict`): `breaking` ≻ `additive` ≻ `cosmetic` ≻ `none` (FR-006).

## `SurfaceSummary` (extended — additive field)

Feature 086's record gains one field:

| Field | Type | Notes |
|---|---|---|
| … existing 086 fields … | | unchanged |
| `Classification` | `SurfaceClassification` | Always present. Empty (`none`/`none`/`[]`) when no file drifted. |

## JSON projection (additive `classification` object inside `surface`)

```json
"surface": {
  "sourceRoot": "src",
  "baselineRoot": "docs/api-surface",
  "mode": "check",
  "checkedCount": 2,
  "missingBaselinePaths": ["docs/api-surface/Foo/Extra.fsi"],
  "driftedSourcePaths": ["src/Foo/Bar.fsi"],
  "orphanBaselinePaths": [],
  "updatedBaselinePaths": [],
  "isCoherent": false,
  "classification": {
    "verdict": "breaking",
    "recommendedBump": "major",
    "entries": [
      {
        "path": "src/Foo/Bar.fsi",
        "classification": "breaking",
        "recommendedBump": "major",
        "addedMembers": ["val baz: int -> string"],
        "removedOrChangedMembers": ["val baz: int -> int"],
        "unparseableFallback": false
      }
    ]
  }
}
```

The `classification` object is **always** written (verdict `none`, bump `none`, empty `entries`
when nothing drifted) so the automation contract has a stable shape. All lists sorted →
deterministic (FR-012).

## Text projection (rich auto-derives from these lines)

```text
surfaceClassificationVerdict: breaking
surfaceClassificationBump: major
surfaceClassified: 1
surfaceClassified: src/Foo/Bar.fsi=breaking (major)
```

Each `key: value` line becomes a rich table row (FR-013); no bespoke rich block.
