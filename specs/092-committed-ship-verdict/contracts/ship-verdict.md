# Contract — `readiness/<id>/ship-verdict.json` (schema v1)

**Owner**: FS.GG.SDD · **Class**: durable generated (ADR-0026, extends ADR-0018) · **Stability**:
`AdditiveOptional` · **Cross-repo contract**: **no** (no `contractVersion`; absent from
`registry/dependencies.yml`)

The committed, compact projection of `readiness/<id>/ship.json`. It answers *"what was true at
merge?"* — the one lifecycle fact regeneration cannot reconstruct. It **drops inventory, never
facts**.

## Producers

| Command | When | Gate |
|---|---|---|
| `fsgg-sdd ship` | whenever it writes `ship.json` | the existing `not hasBlocking` write gate |
| `fsgg-sdd refresh` | re-projected from `ship.json` | **iff** `ship.json`'s currency class is already-current |

Both call **one** pure projection over the same `ship.json` text, so their bytes are identical by
construction. Nothing else may write this file; it is never hand-authored.

## Fields

All present, in this order. No additional field may appear.

| Field | Type | Source in `ship.json` |
|---|---|---|
| `schemaVersion` | integer, **Stable** | `schemaVersion` |
| `viewVersion` | string | `viewVersion` |
| `workId` | string | `workId` |
| `stage` | string (`"ship"`) | `stage` |
| `status` | string | `status` |
| `generator` | string `<id>/<version>` | `generator` |
| `sourcesDigest` | `{ algorithm, value }` | aggregate over `sources[]` (below) |
| `verificationReadiness.status` | string | `verificationReadiness.status` |
| `disposition.state` | string | `disposition.state` |
| `disposition.blockingFindingIds` | string[] | `disposition.blockingFindingIds` |
| `readiness` | string | `readiness` |

## `sourcesDigest`

One SHA-256 over a canonical pre-image of `ship.json`'s `sources[]`, in canonical (path-sorted)
order, one line per source, joined with `\n` and **no trailing newline**:

```
<path>|<algorithm>:<value>
```

It binds each source's **path** to its digest, so a reader can prove the committed verdict
corresponds to the committed sources. An empty `sources[]` yields the SHA-256 of the empty string
(`e3b0c442…b855`) — a defined value, never an omitted field. A non-`sha256` algorithm appears
verbatim in the pre-image, so a future algorithm change alters the aggregate rather than colliding
with it.

## Determinism

`byte-stable; canonical key order; no clock/path/ANSI`. Two runs on unchanged inputs produce
byte-identical output; an unchanged `refresh` produces no git diff.

## Size

At most **20 lines** when `disposition.blockingFindingIds` is empty (the exact rendered size), plus
exactly one line per blocking finding id. Compare `ship.json`: 279 lines, ~59% inventory.

## Example (ship-ready)

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

## Git disposition

Committed. The seeded `.gitignore` excludes `readiness/*/*` and re-includes
`!readiness/*/ship-verdict.json`. The **contents** rule is load-bearing: git never descends into an
excluded *directory*, so the negation under `readiness/*/` would be silently inert. Nested
`agent-commands/<target>/…` views stay ignored because `readiness/*/*` matches that directory itself.

Adoption is additive and no-clobber: a repository that has not copied the amended fragment keeps
ADR-0018's behavior — the verdict is written to disk and ignored — and nothing breaks.

## Non-goals

`verify.json` and `governance-handoff.json` are **not** committed (ADR-0026 §1). `ship.json` is
unchanged: still regenerable, still ignored. No history rewrite; no cleanup of already-ignored files.
