# Quickstart: Surface Drift Classification

## What it does

`fsgg-sdd surface --check` already fails CI when a committed `docs/api-surface/<Pkg>/<Name>.fsi`
baseline drifts from its authored `src/<Pkg>/<Name>.fsi`. This feature adds a **classification** of
each drifted baseline so you know whether the shipped-surface change is additive or breaking, and
which coherent-set version bump to make.

## Run it

```sh
# Default (check): detects drift AND classifies each drifted baseline.
fsgg-sdd surface

# Machine-readable (default projection) — the automation contract:
fsgg-sdd surface --json | jq '.surface.classification'

# Human projections:
fsgg-sdd surface --text
fsgg-sdd surface --rich
```

## Read the verdict

- **additive** → members only added; every prior signature still present → **minor** bump.
- **breaking** → a member removed, renamed, or its signature changed → **major** bump.
- **cosmetic** → only comments / blank lines / member ordering changed → **no** bump.
- **none** (run-level) → nothing drifted, or the only drift is a *new* surface (`missing-baseline`),
  which is a fresh registration, not a shipped-surface mutation.

```jsonc
// fsgg-sdd surface --json | jq .surface.classification
{
  "verdict": "breaking",          // most-severe drifted file
  "recommendedBump": "major",     // major | minor | none
  "entries": [
    { "path": "src/Foo/Bar.fsi", "classification": "breaking", "recommendedBump": "major",
      "addedMembers": ["val baz: int -> string"],
      "removedOrChangedMembers": ["val baz: int -> int"],
      "unparseableFallback": false }
  ]
}
```

## What it does NOT do

- It does **not** change the exit code: a drifted tree still exits 1 under `--check` (feature 086);
  classification is advisory. Reconcile with `fsgg-sdd surface --update`, then commit.
- It does **not** classify a `missing-baseline` (new surface), a `matched` file, or an `orphan`
  baseline — only already-shipped (`drifted`) surfaces.
- It does **not** perform the version bump or touch the registry — those are the downstream
  publishing prompt (#171) and the `.github` reconcile (ADR-0025).
