# Implementation Plan: Emit fs-gg-sdd-* process skills into scaffolded products

**Branch**: `051-scaffold-sdd-process-skills` | **Date**: 2026-06-30 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/051-scaffold-sdd-process-skills/spec.md`

## Summary

Today a `lifecycle=sdd` product seeded by `fsgg-sdd init` (and `scaffold`) gets
the `.fsgg/` skeleton, constitution, early-stage guidance, and root agent
guidance — but **zero** `fs-gg-sdd-*` process skills, so the scaffolded product's
agent cannot discover the lifecycle it is meant to follow. This feature emits the
15 consumer-relevant `fs-gg-sdd-*` process skills (10 stage + 5 cross-cutting,
excluding `fs-gg-sdd-project`) into both agent surfaces of every seeded product,
through the single shared `initEffects` seam that `scaffold` already reuses.

Technical approach (see [research.md](research.md)): compile the canonical
`.claude/skills/fs-gg-sdd-<name>/SKILL.md` bodies into the
`FS.GG.SDD.Commands` assembly as **embedded resources** (linking the existing
authored sources — no transcription), and emit them as additive
`WriteFile(<path>, <body>, AgentGuidanceTarget)` effects in `initEffects`. The
`AgentGuidanceTarget` write-kind gives no-clobber and authored-SDD-owned
semantics for free — the exact precedent set by `constitution.md` /
`early-stage-guidance.md`. A new membership-driven drift guard, extended
skeleton-shape conformance, and the scaffold boundary trees complete the slice.
No new schema, no new effect type, no `release-readiness.json` catalog change.

## Technical Context

**Language/Version**: F# on .NET, `net10.0`

**Primary Dependencies**: FSharp.Core; existing `FS.GG.SDD.Commands` workflow
(`Foundation.initEffects`, `CommandEffects.interpret`), `FS.GG.SDD.Artifacts`.
New: MSBuild `EmbeddedResource` items (no NuGet additions).

**Storage**: Filesystem only — authored Markdown skeleton files under the seeded
product's `.claude/skills/` and `.codex/skills/`.

**Testing**: xUnit-style F# test projects (`FS.GG.SDD.Commands.Tests`,
`FS.GG.SDD.Acceptance.Tests`), real-filesystem temp-dir fixtures.

**Target Platform**: Linux/macOS/Windows CLI.

**Project Type**: Single project — CLI lifecycle product (`fsgg-sdd`).

**Performance Goals**: N/A (one-shot seeding; ~57 KB embedded content).

**Constraints**: Deterministic, byte-identical output (no dates/randomness);
no-clobber re-run; `init` byte-identical aside from the additive skill files; no
provider-/rendering-specific identifiers in generic SDD; no runtime read of the
FS.GG.SDD repo (content is compiled-in).

**Scale/Scope**: 15 skills × 2 surfaces = 30 additive seeded files; ~1 new
internal module, 1 fsproj edit, 1 scaffold-boundary edit, 1 new test + 2 extended
tests.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

**Change tier**: **Tier 1** (artifact-layout / skeleton-shape / agent-skill
contract change). Requires spec, plan, tasks, tests, docs, and migration notes —
all present in this plan.

| Principle | Status | How satisfied |
|-----------|--------|---------------|
| I. Spec → FSI → tests → impl | PASS | Spec done; declared skill set sketched as the surface; tests precede emission; internal `CommandWorkflow` modules have no `.fsi` (existing pattern), public `.fsi` unchanged. |
| II. Structured artifacts are the contract | PASS | The **declared membership list** + drift guard are the machine contract; skill bodies stay authoring surface (like the constitution). [contracts/seeded-skill-set.md](contracts/seeded-skill-set.md) names the authoritative source. |
| III. Visibility in `.fsi` | PASS | No public surface change; `CommandTypes.fsi`/`CommandEffects.fsi` untouched (no new effect/write-kind). New module is internal, no `.fsi` (matches sibling `CommandWorkflow/*.fs`). |
| IV. Idiomatic simplicity | PASS | A sorted list of names → embedded-resource lookup → existing `WriteFile` effects. No frameworks, no new abstractions. Embedding (vs 57 KB of inline literals) is the *simpler* maintainable choice at 15-file scale. |
| V. Elmish/MVU boundary | PASS | Reuses the existing `WriteFile` effect and `CommandEffects.interpret` edge; no new I/O edge, no new `Effect` case. |
| VI. Test evidence mandatory | PASS | Real-FS temp-dir fixtures; tests fail before emission, pass after; determinism (seed twice + diff), no-clobber (edit + re-run), parity, drift guard. |
| VII. One contract for agents & humans | PASS | Single canonical set written to both Claude and Codex surfaces → equivalent by construction (FR-002). |
| VIII. Observability & safe failure | PASS | No-clobber preserves author edits; re-run is safe and idempotent; partial directories fill missing files only. No new failure mode introduced. |

**Engineering constraints**: `net10.0` ✓; `FS.GG.SDD.*` namespace ✓; no
Rendering package/template/URL leaks (the skill bodies are generic SDD process
guidance) ✓; useful without Governance ✓.

**Result**: PASS — no violations, **Complexity Tracking not required**.

## Project Structure

### Documentation (this feature)

```text
specs/051-scaffold-sdd-process-skills/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions D1–D7
├── data-model.md        # Phase 1 — SeededSkill / SeededSkillFile
├── contracts/
│   └── seeded-skill-set.md   # Phase 1 — declared set + INV-1..8
├── quickstart.md        # Phase 1 — runnable validation scenarios
└── tasks.md             # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Commands/
├── FS.GG.SDD.Commands.fsproj          # + EmbeddedResource items (15 canonical SKILL.md), + Compile SeededSkills.fs
└── CommandWorkflow/
    ├── SeededSkills.fs                 # NEW internal module: declared list + manifest-resource loader + skill→effects expansion
    ├── Foundation.fs                   # initEffects extended: append SeededSkills.skillEffects
    └── HandlersScaffold.fs             # isSddOwned/isSddTree: recognize .claude/skills & .codex/skills subtrees

# Canonical source of truth (already in repo, now also embedded — unchanged):
.claude/skills/fs-gg-sdd-<name>/SKILL.md   # 15 in-scope skills (the linked embedded sources)

tests/
├── FS.GG.SDD.Commands.Tests/
│   ├── InitCommandTests.fs             # extend skeleton assertions: 30 seeded skill files present + non-empty
│   └── SeededSkillsTests.fs            # NEW: INV-2/3/4/7 (parity, determinism, no-clobber, drift guard)
└── FS.GG.SDD.Acceptance.Tests/
    └── CompositionAcceptanceTests.fs   # extend skeletonPaths with seeded skill paths (INV-5/INV-8)
```

**Structure Decision**: Single-project CLI. All emission lives in the existing
`src/FS.GG.SDD.Commands/CommandWorkflow/` seam; one new internal module
(`SeededSkills.fs`, compiled before `Foundation.fs`) holds the declared set and
the embedded-resource loader so `Foundation.fs` stays lean. The canonical bodies
remain the repo's existing `.claude/skills/fs-gg-sdd-*/SKILL.md` files, now also
embedded by link.

## Key edges and how the requirements map

| Requirement | Where it is satisfied |
|-------------|-----------------------|
| FR-001 emit set | `SeededSkills.fs` + `Foundation.initEffects` |
| FR-002 both surfaces, equivalent | one canonical body → both `.claude`/`.codex` paths |
| FR-003 authored SDD-owned, not generatedProduct | `AgentGuidanceTarget` write-kind; absent from provenance |
| FR-004 no-clobber | `CommandEffects.canOverwrite` (existing, via write-kind) |
| FR-005 refresh preserves | neither a `refreshCanonicalView` nor a provenance path — by construction |
| FR-006 deterministic, no run-varying content | static embedded bytes; iterate sorted declared list |
| FR-007 single seam, init ≡ scaffold | `scaffold` reuses `initEffects` unchanged |
| FR-008 SDD-produced, never provider | `isSddOwned`/`isSddTree` cover skill subtrees |
| FR-009 skeleton-shape conformance | `InitCommandTests` + `CompositionAcceptanceTests.skeletonPaths` |
| FR-010 drift guard | new `SeededSkillsTests` membership/parity/embedded identity |

## Phase 0 — Research

Complete. All NEEDS CLARIFICATION resolved in [research.md](research.md):
D1 embedding mechanism, D2 membership (15, project excluded), D3 target paths,
D4 scaffold boundary, D5 drift-guard design, D6 conformance surface (skeleton,
not catalog), D7 schema/migration (no bump; refresh untouched).

## Phase 1 — Design & Contracts

Complete: [data-model.md](data-model.md), [contracts/seeded-skill-set.md](contracts/seeded-skill-set.md),
[quickstart.md](quickstart.md), and the CLAUDE.md `SPECKIT` plan reference
updated to this plan.

## Complexity Tracking

No constitution violations — table intentionally empty.
