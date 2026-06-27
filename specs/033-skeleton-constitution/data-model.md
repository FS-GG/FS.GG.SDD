# Phase 1 Data Model: `.fsgg/constitution.md`

This feature introduces **no new type** and **no new schema**. The "data model" is the
classification of one new authored artifact within the existing `CommandEffect` /
`ArtifactWriteKind` / changed-artifact model.

## Entity: Lifecycle constitution (`.fsgg/constitution.md`)

| Property | Value | Source / Rationale |
|---|---|---|
| Path (fixed, relative) | `.fsgg/constitution.md` | FR-001; ADR-0004 namespace reuse |
| Content | Generic F#-SDD-product constitution (populated, placeholder-free) | FR-002/FR-003; body in [contracts/constitution-content.md](./contracts/constitution-content.md) |
| Producer | `initEffects` (`Foundation.fs:81-91`) as a `WriteFile` effect | FR-001; reused on the scaffold path (FR-004) |
| `ArtifactWriteKind` | `AgentGuidanceTarget` | research D1 — no-clobber + authored ownership; CLAUDE.md/AGENTS.md analog |
| Overwrite behavior | No-clobber: refuse to overwrite differing existing content | `canOverwrite` arm `Some _, _ -> false` (`CommandEffects.fs:48`); FR-008 |
| Determinism | Byte-identical across runs/machines (constant literal, no date/randomness) | FR-007/SC-003; research D4 |
| Ownership (reported) | `"authored"` | `CommandReports.fs:934` (non-`GeneratedView` ⇒ authored); FR-010 |
| Report `Kind` (reported) | `"agentGuidance"` | `writeKindValue AgentGuidanceTarget` (`CommandTypes.fs`); accepted label imprecision (research D1) |
| Generated-view status | **Not** a generated view (no source digests, no generator version) | FR-009; refresh never targets it |
| Provenance status | **Not** a `generatedProduct` path | FR-005; excluded via derived `skeletonFiles` (`HandlersScaffold.fs:77-82`) |
| Release-catalog status | Not a catalogued lifecycle artifact | research D5; authored skeleton content |
| Lifecycle position | None — skeleton content, seeded by `init`/`scaffold`, not a stage output | CLAUDE.md skeleton boundary |

## Reused types (unchanged)

### `ArtifactWriteKind` (`CommandTypes.fsi:33-37`)
```fsharp
type ArtifactWriteKind =
    | AuthoredSource        // command-authored work files; overwrite-allowed (re-authored)
    | StructuredSource      // .fsgg/*.yml configs; no-clobber
    | GeneratedView         // regenerable readiness views; refreshable
    | AgentGuidanceTarget   // root authored markdown (CLAUDE.md/AGENTS.md); no-clobber  ← constitution uses this
```
No case added. The constitution selects `AgentGuidanceTarget`.

### `canOverwrite` decision table (`CommandEffects.fs:42-48`) — unchanged
| existing? | kind | result | applies to constitution? |
|---|---|---|---|
| absent | any | overwrite (create) | first `init` ⇒ **create** |
| present, identical text | any | overwrite (NoChange) | unmodified re-`init` ⇒ **no-op** |
| present, differing | `AuthoredSource` / `GeneratedView` | overwrite | n/a (not chosen) |
| present, differing | `StructuredSource` / `AgentGuidanceTarget` | **refuse** | author-edited re-`init` ⇒ **preserved** (FR-008) |

### Changed-artifact record (reported) — produced unchanged by `changeFromEffectResult`
For the constitution `WriteFile`, the existing arm (`CommandReports.fs:914-944`) yields:
- `Path = ".fsgg/constitution.md"`
- `Kind = "agentGuidance"`, `Ownership = "authored"`
- `Operation`: `Create` (fresh), `NoChange` (identical re-run), or `Refuse` (author-edited re-run)
- `SafeWriteDecision`: `"safe"` (create) / `"preserveExisting"` (NoChange) / `"refused"`
  (author-edited differing content) / `"dryRunOnly"` (dry run)
- `BeforeDigest`/`AfterDigest`: sha256 of prior/written text per the existing logic

## State transitions (constitution file lifecycle)

```
absent ──init/scaffold──▶ seeded (authored, no-clobber)
seeded ──author edits──▶ ratified/customized (author-owned)
ratified ──re-init (identical)──▶ NoChange (preserveExisting)
ratified ──re-init (differing)──▶ REFUSED (edits preserved)        [FR-008]
ratified ──refresh────────────▶ untouched (never targeted)         [FR-009]
ratified ──scaffold (provenance)─▶ excluded from generatedProduct  [FR-005]
```

## Non-entities (explicitly out of model)

- No `scaffold-provenance.json` schema field (v1 unchanged; FR-005).
- No `providers.yml` change (v1 unchanged).
- No `release-readiness.json` catalog entry (research D5).
- No generated-view source/digest record (the constitution is authored, not generated).
