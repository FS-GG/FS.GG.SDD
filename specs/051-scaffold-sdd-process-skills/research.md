# Phase 0 Research: Emit fs-gg-sdd-* process skills into scaffolded products

**Feature**: 051-scaffold-sdd-process-skills
**Date**: 2026-06-30

This document resolves every NEEDS CLARIFICATION from the Technical Context and
records the load-bearing design decisions, each with rationale and the
alternatives rejected.

## D1. Where the skill bodies live and how they are emitted

**Decision**: Compile the canonical skill bodies into the
`FS.GG.SDD.Commands` assembly as **embedded resources** that *link the existing
authored source files* `.claude/skills/fs-gg-sdd-<name>/SKILL.md`, and emit them
through the existing skeleton seam (`initEffects` →
`WriteFile(path, content, AgentGuidanceTarget)` → `CommandEffects.interpret`).
A new internal module `CommandWorkflow/SeededSkills.fs` holds an explicit,
sorted **declared membership list** and a manifest-resource loader; `initEffects`
expands that list into one `WriteFile` per (skill, agent surface).

**Rationale**:
- The constitution/early-stage precedent already writes authored skeleton files
  with the `AgentGuidanceTarget` write-kind, which is the no-clobber kind
  (`CommandEffects.canOverwrite`). Reusing the same kind gives FR-004 (no-clobber)
  and FR-003 (authored, SDD-owned, not `generatedProduct`) for free.
- Linking the *same bytes* that are the repo's source of truth makes content
  drift between source and emission **structurally impossible** — the strongest
  possible answer to FR-006 (byte-identical) and FR-010 (no silent staleness).
- Compiled-in resources are self-contained and deterministic; nothing is read
  from the repo at runtime (satisfies the spec's "Authoring-as-contract"
  assumption: static, self-contained, not runtime-repo-read).
- A single canonical set written to both surfaces makes Claude/Codex parity
  (FR-002, SC-004) hold *by construction* rather than by transcription
  discipline — the `.claude` and `.codex` sources are already byte-identical for
  all 15 in-scope skills (verified with `cmp`).

**Alternatives rejected**:
- *Inline triple-quoted string literals in `Foundation.fs`* (the literal
  constitution/early-stage mechanism): faithful to precedent but adds ~57 KB of
  hand-transcribed literals (the lifecycle skill alone is 11 KB), reintroduces
  exactly the transcription-drift risk FR-010 exists to catch, and violates
  Principle IV (idiomatic simplicity) at 15-file scale. The precedent's literal
  works because it is *one* file per artifact; this is fifteen.
- *Read `.claude/skills/**` from disk at runtime*: rejected by the spec
  assumption ("rather than read at runtime from the FS.GG.SDD repo") and not
  self-contained in a shipped product.
- *Generate an F# source file from the skills at build time*: more machinery
  than embedded resources for no additional guarantee.

**Determinism note**: `GetManifestResourceNames()` ordering is not guaranteed, so
the emission iterates the **explicit sorted declared list**, never manifest order
(FR-006).

## D2. In-scope skill set (membership)

**Decision**: Seed the **15** consumer-relevant `fs-gg-sdd-*` skills and exclude
`fs-gg-sdd-project`:

Lifecycle-stage (10): `fs-gg-sdd-charter`, `fs-gg-sdd-specify`,
`fs-gg-sdd-clarify`, `fs-gg-sdd-checklist`, `fs-gg-sdd-plan`, `fs-gg-sdd-tasks`,
`fs-gg-sdd-analyze`, `fs-gg-sdd-evidence`, `fs-gg-sdd-verify`, `fs-gg-sdd-ship`.

Cross-cutting (5): `fs-gg-sdd-lifecycle`, `fs-gg-sdd-getting-started`,
`fs-gg-sdd-authoring-contracts`, `fs-gg-sdd-refresh-agents`,
`fs-gg-sdd-validate`.

**Rationale**: Matches the spec's Skill-set scope assumption verbatim.
`fs-gg-sdd-project` is about developing the FS.GG.SDD product itself, not using
SDD inside a consumer product, so it has no place in a seeded product.

**Alternatives rejected**: Including `fs-gg-sdd-project` (leaks product-internal
guidance into consumer products); seeding only stage skills (drops the lifecycle
map/getting-started entry points the agent needs to *discover* the process).

**Open for clarify**: The spec flags exact membership as a `/speckit-clarify`
candidate. The drift guard (D5) is membership-driven, so the declared list is the
single place to adjust if clarify changes it — no other code changes.

## D3. Target paths in the seeded product

**Decision**: Mirror the repo convention exactly — for each in-scope skill,
write to both:
- `.claude/skills/fs-gg-sdd-<name>/SKILL.md`
- `.codex/skills/fs-gg-sdd-<name>/SKILL.md`

**Rationale**: The repo already uses `.claude/skills/<name>/SKILL.md` and
`.codex/skills/<name>/SKILL.md`; agents discover skills there. `init` currently
seeds the *root* agent guidance (`CLAUDE.md`, `AGENTS.md`) but no per-agent skill
directory, so this establishes the per-agent skill directory in seeded products
following the established repo layout (spec "Codex skill location" assumption).

**Alternatives rejected**: A single shared skill dir (breaks the per-agent
surface convention and the agents.yml mapping claude→CLAUDE.md, codex→AGENTS.md).

## D4. SDD↔provider boundary (scaffold) coverage

**Decision**: Extend `HandlersScaffold.isSddOwned`/`isSddTree` so the seeded
skill paths under `.claude/skills/` and `.codex/skills/` are recognized as
SDD-owned trees.

**Rationale**: Because `scaffold` reuses `initEffects` unchanged, the skills are
emitted on the scaffold path for free (FR-007). But `skeletonFiles` subtracts the
skeleton from the provider diff and `collisionPaths`/`isSddTree` guard the
SDD-owned trees the provider must never write. Today those trees are only
`.fsgg/`, `work/`, `readiness/`, `AGENTS.md`, `CLAUDE.md`. Without adding the
skill dirs, a provider that wrote into `.claude/skills/` could collide with — or
appear to "own" — an SDD-process skill, violating FR-008 and the
boundary-preservation edge case.

**Alternatives rejected**: Marking the whole `.claude/`/`.codex/` trees SDD-owned
(too broad — a provider may legitimately ship *other* agent assets); scope is the
skill subtree only.

## D5. Parity/drift guard design (FR-010 / SC-005)

**Decision**: Introduce one new test that asserts a three-way identity:
1. the **explicit declared membership list** (D2) equals the set of
   `fs-gg-sdd-*` skill dirs on disk under `.claude/skills/` (minus
   `fs-gg-sdd-project`), and the same set under `.codex/skills/`;
2. for every in-scope skill, the `.claude` source, the `.codex` source, and the
   **embedded resource bytes** are byte-identical;
3. the bytes the seeding command actually emits equal the canonical source bytes.

**Rationale**: Because content is embedded-by-link (D1), content drift is
impossible — so the guard's job is to catch **membership** drift (a new
`fs-gg-sdd-*` skill added to the repo but not declared/seeded, or removed) and
**cross-surface source** drift (`.codex` silently diverging from `.claude`).
That makes SC-005 a real, failing signal "before release rather than shipped
silently." Modeled on the existing `ScaffoldParityTests` /
`ReleaseContractTests` / `EarlyStageGuidanceContractTests` drift-guard style.

**Alternatives rejected**: A content-diff-only guard (degenerate under
embed-by-link); no guard (spec explicitly requires one).

## D6. Release / skeleton-shape conformance surface (FR-009)

**Decision**: Account for the new skill files in the **skeleton-shape**
conformance surfaces, **not** the `release-readiness.json` catalog:
- extend `InitCommandTests` skeleton assertions to require the seeded skill files
  (both surfaces, non-empty);
- extend `CompositionAcceptanceTests.skeletonPaths` to include representative
  seeded skill paths.

**Rationale**: The `fs-gg-sdd-*` SKILL.md files are authored skeleton seeds, not
catalogued *produced lifecycle artifacts*; the `release-readiness.json` catalog
enumerates lifecycle contracts/generated views (e.g. `agent-commands/.../
skills.md`), and the seeded `SKILL.md` files are intentionally not in it — the
skeleton file set is enumerated separately in the init/composition tests. This
keeps the catalog focused and the skeleton shape authoritative and verified.

**Alternatives rejected**: Adding the 30 skill files to `release-readiness.json`
(+ both golden copies + `schema-reference.md`) — miscategorizes authored seeds as
produced lifecycle artifacts and inflates the catalog.

## D7. Schema / migration posture

**Decision**: No schema version changes. `scaffold-provenance.json` stays v1; the
seeded skills are **not** added to `producedPaths` (they are not
`GeneratedProduct`). `refresh` requires no change: the skills are neither a
`refreshCanonicalView` nor a provenance path, so refresh preserves them by
construction (same posture as constitution.md / early-stage-guidance.md). FR-005
is satisfied without touching `HandlersRefresh`.

**Rationale**: Additive authored skeleton files; existing products that re-run
`init`/`scaffold` gain the skills additively under no-clobber. No consumer of any
existing schema changes shape.

**Migration note**: Pre-existing seeded products simply re-run the seeding
command to receive the new skill files; any author edits or name collisions are
preserved (no-clobber).

## Constitution alignment summary

- Principle II (structured artifacts are the contract): the **declared
  membership list** is the machine contract; the drift guard (D5) enforces it;
  prose skill bodies remain authoring surface.
- Principle V (MVU boundary): no new effect or I/O edge — reuse `WriteFile` +
  `CommandEffects.interpret`. No new `.fsi` for the internal `CommandWorkflow`
  modules; public `CommandTypes.fsi`/`CommandEffects.fsi` are unchanged.
- Principle VI (test evidence): real-filesystem fixtures (seed into temp dirs),
  determinism (seed twice, diff), no-clobber (edit then re-run), parity/drift.
- Principle VII (one contract for agents and humans): single canonical set →
  both surfaces, equivalent by construction.
