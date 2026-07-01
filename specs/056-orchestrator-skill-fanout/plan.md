# Implementation Plan: Orchestrator skill fan-out — union SDD + provider skills into all three agent roots

**Branch**: `056-orchestrator-skill-fanout` | **Date**: 2026-07-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/056-orchestrator-skill-fanout/spec.md`

## Summary

Implement the **FS-GG/FS.GG.SDD#55** decision (Option A — orchestrator-owned fan-out; ADR-0011,
owned separately/pending): keep the scaffold intrusion guard **strict** and make `fsgg-sdd` the
**sole mirror authority** that populates every agent-skill root with the same skills. SDD seeds
its `fs-gg-sdd-*` process skills into a *third* root `.agents/skills/` (in addition to `.claude`
and `.codex`), providers write their `fs-gg-*` UI skills only into the neutral `.agents/skills/`
root, and SDD fans out the **byte-identical union** (seeded ∪ provider) into all three roots so
the Claude, Codex, and neutral runtimes are interchangeable (`claude ≡ codex ≡ agents = union`).

The guard stays strict and gains one clause — the `fs-gg-sdd-*` namespace under `.agents/skills/`
is reserved too (a provider may write *other* `.agents` skills, never SDD's). The change reuses
the existing single seeding seam (`SeededSkills.skillEffects` → `initEffects`), the existing
`ReadFile`/`EnumerateDirectory`/`WriteFile` effects, and the existing no-clobber
`AgentGuidanceTarget` semantics — **no new effect, command, or exit code**. `scaffold`,
`refresh`, and `doctor`/`upgrade` all carry the invariant (full fan-out across every verb).

This is the deliberate replacement for the reverted feature 055 (guard-narrowing). See
[research.md](./research.md) (R1–R9), [data-model.md](./data-model.md) (entities + the strict, one-clause-extended
`isSddTree` truth table + provenance shape), and [contracts/](./contracts/) (the fan-out +
strict-guard contract, P1–P10).

> **Cross-repo posture.** This is behavioral coherence on the `scaffold-provider` contract plus
> the orchestrator version axis (ADR-0008). **ADR-0011** (the formal decision record) is owned
> and written **separately by the user** in `FS-GG/.github`; this plan references it as pending
> and treats the #55 comment as the decision of record. The provider side — Rendering emitting
> `.agents/skills/` and no longer `.claude/` — is **FS-GG/FS.GG.Templates#47** (still open). #55
> stays closed; this feature *implements* its decision.

## Technical Context

**Language/Version**: F# on .NET, `net10.0`.

**Primary Dependencies**: none new. The mirror is built from the existing MVU effects
(`EnumerateDirectory`, `ReadFile`, `WriteFile` with `AgentGuidanceTarget`) interpreted at the
edge; `System.Text.Json`/Spectre.Console projections are unchanged (rich derives from text).

**Storage**: Files only. `init`/`scaffold` now materialize a **third** agent-skill root
`.agents/skills/`. `.fsgg/scaffold-provenance.json` records the mirrored files **additively**
(schema **stays v1**; a new optional `mirroredPaths` array defaulting to `[]` — R4).

**Testing**: xUnit; real `dotnet new` provider fixtures under `tests/fixtures/scaffold-provider/`
(no mocks), serialized by `[<Collection("Scaffold")>]`. New: a `skills-agents-cotenant` positive
fixture (writes `.agents/skills/fs-gg-elmish/SKILL.md`); negative fixtures for `.claude/skills/`,
`.codex/skills/`, and `.agents/skills/fs-gg-sdd-*` intrusions; three-root byte-identity assertions
for `init` and `scaffold`; a drift-guard extension test; a refresh re-mirror test; a doctor/upgrade
three-root drift test; an `isSddTree` truth-table unit test; a provenance schema-v1 additive guard;
parity for the mirrored produced/mirrored paths.

**Target Platform**: Cross-platform CLI (`fsgg-sdd`) + libraries.

**Project Type**: Multi-project single solution (CLI + libraries). Not web/mobile.

**Performance Goals**: N/A. Determinism (sorted paths; byte-stable json/provenance; byte-identical
mirror across roots) is the hard constraint.

**Constraints**: classification/mirroring stays pure in the handlers, real I/O at the interpreter
edge (Principle V); no new effect/command/outcome/exit code; reservation asymmetry is intentional —
`.claude/skills/` and `.codex/skills/` stay **whole-root** reserved, `.agents/skills/` is reserved
only in the `fs-gg-sdd-*` **namespace**; `init` is intentionally **not** byte-identical to pre-056
(the third root is a deliberate, version-gated skeleton growth — ADR-0008); an incomplete fan-out
is never reported complete (FR-012).

**Scale/Scope**: extend `SeededSkills.skillEffects` (+1 root); a new post-instantiation mirror
stage in scaffold + a refresh re-mirror step + a doctor/upgrade drift arm; one `isSddTree` clause;
one additive provenance field (`.fsi` + serializer + `tryParse` default); the drift `expectedArtifactPaths`
gains the third root; new/changed fixtures + ~8–10 tests; agent-surface wording. One `.fsi` change
(`ScaffoldProvenance`), matching the 050/052 additive-field precedent.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.1.0.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Spec → FSI → Semantic Tests → Impl | **PASS** | One public-surface change (`ScaffoldProvenance` gains an optional `mirroredPaths` field) → `.fsi` sketched first; fail-first tests (three-root byte-identity, strict-guard negatives, truth table, drift, refresh, provenance) precede the `.fs` bodies. |
| II. Structured artifacts are the machine contract | **PASS** | The provenance json is the machine contract; the mirror record is an **additive** array (schema **v1**, R4). Byte-identity across roots is the tool-checkable invariant, pinned by the drift guard (FR-008). |
| III. Visibility in `.fsi` | **PASS** | Only `ScaffoldProvenance.fsi` moves (additive field, `PublicSurface.baseline` refreshed if it lists the record). `isSddTree`, `SeededSkills.*`, the mirror/refresh/drift helpers are module-`internal` (no `.fsi`). |
| IV. Idiomatic simplicity | **PASS** | Reuses the single seeding seam, existing effects, and no-clobber write-kind. No new effect/type framework; the mirror is a fold over enumerated skills. Complexity Tracking empty. |
| V. Elmish/MVU boundary | **PASS** | Union computation + mirror planning are pure in the handlers; the edge interpreter performs the real `EnumerateDirectory`/`ReadFile`/`WriteFile`. Staged like the existing post-instantiation `git init`/`chmod` machine (re-derived from the interpreted-effect log). |
| VI. Test evidence mandatory | **PASS** | Fail-first with real `dotnet new` fixtures (no mocks); three-root byte-identity, strict-guard negatives, and drift are red before the change. Synthetic fixture bodies disclosed in fixture comments/test names. |
| VII. Agent & human share one contract | **PASS** | The report/provenance is the single source of truth; agents author nothing new. Both surfaces' scaffold/getting-started guidance describe the three-root union model equivalently (Claude ⇔ Codex). |
| VIII. Observability & safe failure | **PASS** | Strict guard still fails fast (exit 2, `providerWroteSddTree`); a mirror failure surfaces a diagnostic and a non-success outcome (FR-012); user-input errors stay exit 1. |

**Change tier**: **Tier 1 (contracted change)** — a behavioral + provenance-shape change to the
scaffold/init/refresh/doctor/upgrade agent-skill contract and the `scaffold-provider` cross-repo
integration surface (unblocking FS.GG.Rendering Feature 219). Requires spec, plan, tasks, `.fsi`
(ScaffoldProvenance additive field), tests, and a migration-note decision. **Additive JSON,
provenance stays v1**; `init`'s seeded set grows by the third root (a declared, version-gated
skeleton change, not a schema migration).

**Gate result**: PASS — no violations, no justified-complexity entries.

## Project Structure

### Documentation (this feature)

```text
specs/056-orchestrator-skill-fanout/
├── plan.md              # This file
├── research.md          # Phase 0 — R1–R9 (the third-root, mirror, ownership, provenance decisions)
├── data-model.md        # Phase 1 — entities, the strict isSddTree truth table, provenance shape
├── quickstart.md        # Phase 1 — validation scenarios + test map
├── contracts/
│   └── skill-fanout.md  # the fan-out + strict-guard contract, properties P1–P10
└── tasks.md             # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root) — files this feature touches

```text
src/FS.GG.SDD.Commands/CommandWorkflow/
├── SeededSkills.fs            # skillEffects: emit a THIRD WriteFile per skill for
│                              #   .agents/skills/{name}/SKILL.md (byte-identical body).
├── HandlersScaffold.fs        # isSddTree: + ".agents/skills/fs-gg-sdd-" clause (namespace-only,
│                              #   keep .claude/.codex whole-root strict). New post-instantiation
│                              #   MIRROR stage: enumerate provider .agents/skills/*, read bodies,
│                              #   write union byte-identical into all three roots; record mirrored.
├── HandlersRefresh.fs         # re-mirror the union to currency across all three roots (FR-009).
├── Drift.fs                   # expectedArtifactPaths + .agents third root; three-root union drift
│                              #   for doctor/upgrade (FR-010).
├── Foundation.fs              # initEffects unchanged in shape — inherits the third root via
│                              #   SeededSkills.skillEffects (single seam).
└── HandlersDoctor.fs /        # consume the extended Drift model (no second source of truth).
    HandlersUpgrade.fs

src/FS.GG.SDD.Artifacts/
├── ScaffoldProvenance.fsi     # + optional `MirroredPaths` field (schema v1; tryParse defaults []).
└── ScaffoldProvenance.fs      # serialize/tryParse the additive field, canonical key order.

tests/
├── fixtures/scaffold-provider/
│   ├── skills-agents-cotenant/            # NEW positive: writes .agents/skills/fs-gg-elmish/
│   ├── skills-intrusion-claude/ ·-codex/  # negative: .claude/.codex whole-root (reuse/rename existing)
│   ├── skills-intrusion-agents/           # NEW negative: .agents/skills/fs-gg-sdd-custom/
│   └── registries/*.providers.yml
├── FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs   # three-root byte-identity, mirror, negatives,
│                              #   truth table, provenance v1 additive guard
├── FS.GG.SDD.Commands.Tests/SeededSkillsTests.fs      # init seeds three roots byte-identically
├── FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs    # refresh re-mirror
├── FS.GG.SDD.Commands.Tests/{Doctor,Upgrade}*.fs      # three-root drift detect + reconcile
└── FS.GG.SDD.Cli.Tests/ScaffoldParityTests.fs         # mirrored produced/mirrored paths parity

docs/
└── release/migrations/README.md   # recording paragraph (additive provenance; init third-root
                                    #   growth is version-gated per ADR-0008) — see R9.

# Agent surfaces (Claude ⇔ Codex, kept equivalent):
CLAUDE.md / AGENTS.md
.claude/skills/fs-gg-sdd-getting-started/SKILL.md
.codex/skills/fs-gg-sdd-getting-started/SKILL.md   # three-root union ownership model
```

**Structure Decision**: The existing multi-project layout is retained. The change concentrates in
`SeededSkills.fs` (third root), `HandlersScaffold.fs` (guard clause + mirror stage), the
`refresh`/`doctor`/`upgrade` handlers (carry the invariant), and one additive provenance field.
Everything else (partition, projections, outcomes, exit codes, effect set) is unchanged — the
mirror flows through the same staged post-instantiation machine the `git init`/`chmod` steps
already use.

## Complexity Tracking

No entries — no constitution violations and no justified complexity. No new effect, command,
outcome, exit code, or advanced F# feature; one guard clause, one reused seeding seam, one additive
provenance field, and a mirror fold over enumerated skills.
