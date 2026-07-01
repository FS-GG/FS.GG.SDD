# Phase 1 Data Model: Scaffold co-tenant skills under the shared skill roots

**Feature**: `055-scaffold-cotenant-skills` | **Date**: 2026-07-01

This feature adds **no new type, record, field, or persisted schema**. It narrows the
predicate that classifies provider-produced paths. The "entities" below are the spec's
Key Entities expressed as the concepts the narrowed predicate partitions; there is no
`.fsi` surface change and no `PublicSurface.baseline` refresh (research R6).

## E1 — Reserved SDD skill namespace (concept; realized by a predicate)

The `fs-gg-sdd-*` skill subtrees under each shared skill root that SDD seeds and owns. No
provider may write into them (FR-002/FR-011). Realized, after narrowing, as a new helper that
is a **sibling of `isSddTree`** inside `[<AutoOpen>] module internal HandlersScaffold` — a plain
module-internal `let` (not `let private`), so it shares `isSddTree`'s visibility and is reachable
from the test project via the existing `InternalsVisibleTo("FS.GG.SDD.Commands.Tests")`:

```fsharp
/// A skill subtree under either shared skill root whose top-level skill name is in the
/// SDD-reserved `fs-gg-sdd-*` namespace. Reservation is by name-prefix (research R2), so it
/// protects future `fs-gg-sdd-*` names too, and is symmetric across the two roots (FR-007).
let isReservedSkillSubtree (p: string) =
    p.StartsWith(".claude/skills/fs-gg-sdd-", StringComparison.Ordinal)
    || p.StartsWith(".codex/skills/fs-gg-sdd-", StringComparison.Ordinal)
```

| Property | Value | Notes |
|----------|-------|-------|
| Roots | `.claude/skills/`, `.codex/skills/` | Treated identically (FR-007) |
| Reserved prefix (per root) | `fs-gg-sdd-` | Name-prefix, not exact-seeded-set (R2) |
| Owner | SDD skeleton (`AgentGuidanceTarget`) | Seeded by `init`, excluded from product via `skeletonFiles` |
| Classification | intrusion when provider-written | `scaffold.providerWroteSddTree`, exit 2 |

## E2 — Provider co-tenant skill (concept)

A provider-produced skill directory under a shared skill root whose top-level name is
**outside** the reserved namespace (e.g. `.claude/skills/fs-gg-elmish/…`). Classified as
provider product.

| Property | Value | Notes |
|----------|-------|-------|
| Predicate | `isSddTree = false` and under a skills root | Falls through to `producedPaths` at `HandlersScaffold.fs:377` |
| Provenance owner | `GeneratedProduct` | `provenanceWriteEffect:277` — same array as any other product path (FR-004) |
| Report | listed under `producedPaths` (json/text/rich) | Additive entry only; no new key (FR-004/FR-008) |
| Clobber | never clobbers seeded `fs-gg-sdd-*`; never clobbered by them | Disjoint namespaces; `init` byte-identical (FR-005/FR-010) |

## E3 — Scaffold intrusion discriminator (`isSddTree`, MODIFIED)

The rule that partitions provider-produced paths into intrusions (rejected) vs product
(recorded). This feature changes only its two skill-root clauses.

`src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs:53-62`:

| Clause | Before | After |
|--------|--------|-------|
| `.fsgg/`, `work/`, `readiness/` | SDD tree | **unchanged** — SDD tree |
| `.claude/skills/**` | *entire root* SDD tree | only `.claude/skills/fs-gg-sdd-*` (via `isReservedSkillSubtree`) |
| `.codex/skills/**` | *entire root* SDD tree | only `.codex/skills/fs-gg-sdd-*` (via `isReservedSkillSubtree`) |

Truth table (the unit-test oracle for R1/R5):

| Path | `isSddTree` | `isSddOwned` | Classification |
|------|:-----------:|:------------:|----------------|
| `.fsgg/x`, `work/x`, `readiness/x` | true | true | intrusion / SDD-owned |
| `.claude/skills/fs-gg-sdd-plan/SKILL.md` | true | true | reserved (intrusion if provider-new) |
| `.claude/skills/fs-gg-sdd-custom/SKILL.md` | true | true | reserved-namespace intrusion (R2) |
| `.codex/skills/fs-gg-sdd-anything/SKILL.md` | true | true | reserved (symmetric, FR-007) |
| `.claude/skills/fs-gg-elmish/SKILL.md` | **false** | **false** | provider product (FR-001) |
| `.claude/skills/leak` (bare file) | **false** | **false** | provider product (edge case) |
| `AGENTS.md`, `CLAUDE.md` | false | true | SDD-owned skeleton (unchanged) |

## E4 — Scaffold provenance record (UNCHANGED shape)

`.fsgg/scaffold-provenance.json`, `ScaffoldProvenanceRecord` with `SchemaVersion = 1`
(`ScaffoldProvenance.fs`; stamped at `HandlersScaffold.fs:270`). Gains **no field and no schema
bump** (FR-008); a co-tenant scaffold merely adds entries to the existing `producedPaths`
array, each `Owner = GeneratedProduct` — indistinguishable in shape from any other produced
path.

## State / flow (unchanged except the partition)

`resolveScaffold → invoke provider (RunProcess edge) → finalizeScaffold` (`:294-394`):
`produced = (afterSet − beforePaths − skeletonFiles) − provenancePath`, then partitioned by the
**narrowed** `isSddTree` into `intrusions` / `producedPaths`. Outcome, diagnostics, and
`exitCodeForReport` (provider-defect id + `Blocked` → 2) are untouched (research R8).
