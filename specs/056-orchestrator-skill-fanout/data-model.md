# Data Model: Orchestrator skill fan-out

**Feature**: `056-orchestrator-skill-fanout` | Phase 1. Entities, the strict `isSddTree` truth
table, the skill-union relation, and the additive provenance shape.

## Entities

### E1 — Agent-skill root

One of the three directories that must carry the union skill set:

| Root | Written by | Reservation |
|------|-----------|-------------|
| `.claude/skills/` | SDD only (seed + mirror) | **whole root** reserved (`isSddTree`) |
| `.codex/skills/` | SDD only (seed + mirror) | **whole root** reserved (`isSddTree`) |
| `.agents/skills/` | **provider** (non-reserved) + SDD (seed) | only `fs-gg-sdd-*` **namespace** reserved |

The asymmetry is intentional (research R5): `.agents/skills/` is the neutral provider root; the
other two are SDD-exclusive and receive provider skills only via SDD's mirror.

### E2 — Seeded process skill

A `fs-gg-sdd-*` skill whose canonical body is an embedded resource (`SeededSkills.loadBody`).
`SeededSkills.skillNames` is the single in-code source of the set (15 skills). Each seeds to **all
three roots**, byte-identical, no-clobber `AgentGuidanceTarget`.

### E3 — Provider skill (co-tenant)

A non-`fs-gg-sdd-*` skill (e.g. `fs-gg-elmish`) the provider writes into `.agents/skills/`. Its
canonical body lives at `.agents/skills/{name}/SKILL.md` (owner `generatedProduct`). SDD mirrors it
byte-identically into `.claude/skills/{name}/` and `.codex/skills/{name}/` (owner `mirrored`).

### E4 — Skill union

`union = { seeded fs-gg-sdd-* } ∪ { provider .agents/skills/* (non-reserved) }`. The invariant:

```
for every skill s in union:
  bytes(.claude/skills/s/SKILL.md) == bytes(.codex/skills/s/SKILL.md) == bytes(.agents/skills/s/SKILL.md)
```

(`claude ≡ codex ≡ agents = union`). Enforced by construction (one canonical body → N writes) and
pinned by the drift guard (E7).

### E5 — Mirror record (provenance)

Additive `mirroredPaths: ScaffoldProducedPath list` on `ScaffoldProvenanceRecord`. Each entry is a
mirrored `.claude`/`.codex` copy of a provider skill, owner `Mirrored`. `producedPaths` is
unchanged (provider product, incl. the canonical `.agents` skill). See §Provenance shape.

### E6 — Ownership vocabulary

| Owner (serialized) | Applies to | refresh regenerates? | no-clobber? |
|---|---|---|---|
| `authored` (`AgentGuidanceTarget`) | seeded `fs-gg-sdd-*` in all 3 roots | re-seed missing | yes |
| `generatedProduct` | provider product incl. `.agents/skills/*` canonical | **excluded** | n/a (provider-owned) |
| `mirrored` **(new)** | `.claude`/`.codex` mirror copies of provider skills | **yes (re-mirror)** | yes |

### E7 — Three-root drift (doctor/upgrade)

A finding that a product violates E4: a seeded/provider skill missing from a root, or the three
copies not byte-identical. `Drift.expectedArtifactPaths` extends to the third root; the union check
compares the provenance `mirroredPaths` + on-disk `.agents` skills across all three roots.

## The strict `isSddTree` truth table (E1 reservation)

`isSddTree p` (over the normalized relative path) — **strict**; the guard is NOT narrowed:

| Path | isSddTree | isSddOwned | Note |
|------|-----------|------------|------|
| `.fsgg/x`, `work/x`, `readiness/x` | true | true | unchanged lifecycle trees |
| `.claude/skills/anything` | **true** | true | whole root reserved (unchanged) |
| `.codex/skills/anything` | **true** | true | whole root reserved (unchanged) |
| `.agents/skills/fs-gg-sdd-plan/…` | **true** | true | reserved namespace (**new clause**) |
| `.agents/skills/fs-gg-sdd-custom/…` | **true** | true | reserved namespace (**new clause**) |
| `.agents/skills/fs-gg-elmish/…` | **false** | false | provider co-tenant (product) |
| `.agents/skills/` (bare) / `.agents/x` | false | false | not reserved |
| `AGENTS.md` / `CLAUDE.md` | false | true | skeleton agent-guidance (unchanged) |

The only change vs today is the single `.agents/skills/fs-gg-sdd-` clause. `.claude/skills/` and
`.codex/skills/` remain whole-root `true` (this is the opposite of the reverted 055).

## Provenance shape (schema v1, additive — research R4)

`ScaffoldProvenanceRecord` (unchanged fields elided):

```
{ schemaVersion: 1                         // UNCHANGED
  generator, requiredMinimumCliVersion,    // UNCHANGED
  providerName, providerContractVersion, templateRef, outcome,
  producedPaths: [ { path, owner } ]       // UNCHANGED — provider product (incl. .agents/skills/* canonical)
  mirroredPaths: [ { path, owner } ]       // NEW, optional, sorted by path, default []
                                           //   entries owner = "mirrored"; the .claude/.codex mirror copies
  effectiveParameters: [ { key, value } ] }// UNCHANGED
```

- Serialized after `producedPaths`; `tryParse` defaults absent/null `mirroredPaths` to `[]`.
- `owner` gains one value `"mirrored"`, appearing **only** inside `mirroredPaths`.
- `serialize` stays byte-deterministic (canonical key order, sorted paths, no clock/abs-path/ANSI).

## State transitions (scaffold post-instantiation, extended)

The staged machine, re-derived from the interpreted-effect log each tick:

```
create interpreted (success)
  → TICK MIRROR: EnumerateDirectory .agents/skills → ReadFile each provider skill
                 → WriteFile union into .claude & .codex (no-clobber) ; stage mirroredPaths
  → TICK A: provenance write (producedPaths + mirroredPaths) ; git rev-parse probe ; chmod .sh
  → TICK B: git init (if not in a work tree)
  → TICK C: terminal success summary
```

Terminal outcomes (dry-run, provider unavailable/failed, **any intrusion incl.
`.agents/skills/fs-gg-sdd-*`**) finalize in one tick and run **no** mirror/post-instantiation steps
(FR-012 — an incomplete fan-out is never reported complete).

A fault **inside** the MIRROR tick (a `ReadFile`/`WriteFile` failure while writing the union into
`.claude`/`.codex`) finalizes as a **non-success** scaffold: it emits the `scaffold.mirrorFailed`
diagnostic at **exit 2** (the existing tool-defect class — no new outcome or exit code), the outcome
is never `providerSucceeded`, and neither the report nor `scaffold-provenance.json` records the
fan-out as complete (FR-012). The `scaffold.mirrorFailed` id is additive observability only.
