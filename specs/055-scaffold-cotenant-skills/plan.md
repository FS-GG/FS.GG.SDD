# Implementation Plan: Scaffold co-tenant skills under the shared skill roots

**Branch**: `055-scaffold-cotenant-skills` | **Date**: 2026-07-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/055-scaffold-cotenant-skills/spec.md`

## Summary

Resolve coordination item **FS-GG/FS.GG.SDD#55**: the `fsgg-sdd scaffold` intrusion guard
claims the **entire** `.claude/skills/` and `.codex/skills/` prefixes as SDD-owned, so a
compliant rendering provider (FS.GG.Rendering Feature 219) that emits its own UI skills
(`fs-gg-elmish`, …) into `.claude/skills/` is rejected as `scaffold.providerWroteSddTree`
(exit 2) — every real `fs-gg-ui` scaffold blocks. This feature **narrows the single
discriminator** (`isSddTree`, `HandlersScaffold.fs:53-62`) so SDD reserves only the
`fs-gg-sdd-*` skill namespace under each shared root; a provider may co-populate the rest as
product.

The change is a **guard narrowing**, not a new capability: no new command, effect, outcome,
exit code, persisted field, or public signature. Because `isSddTree` is the one predicate
driving *both* the intrusion partition and provenance ownership (`:376-377` → `:277`), narrowing
it in one place makes co-tenant skills simultaneously "not an intrusion" (FR-001) and
"recorded as `generatedProduct`" (FR-004) by construction. Seeded `fs-gg-sdd-*` skills stay
excluded from product via the pre-existing `skeletonFiles` subtraction (`:373`), so `init`
stays byte-identical (FR-010) and provenance stays schema **v1** (FR-008). The negative
`skills-intrusion` fixture is re-pointed from the now-legitimate `.claude/skills/leak` to a
*new* reserved path `.claude/skills/fs-gg-sdd-custom/` (FR-009), and a new positive
`skills-cotenant` fixture proves the unblocked path.

See [research.md](./research.md) for the resolved decisions (R1–R8),
[data-model.md](./data-model.md) for the partitioned concepts and truth table, and
[contracts/](./contracts/) for the narrowed discriminator contract (P1–P8).

> **Planning decision resolved (research R2):** namespace reservation is by the `fs-gg-sdd-`
> **name-prefix** under each root (forward-compatible; protects future SDD skill names),
> chosen over exact-collision-with-the-seeded-set. The spec pre-commits to this and the
> assumption is explicit, so **no `/speckit-clarify` is required**; a reviewer preferring
> exact-collision semantics would revisit only R2 and the FR-001/FR-002 wording, and either way
> non-`fs-gg-sdd-*` co-tenant skills are permitted.

> **Correctness note (research R3):** the re-pointed negative fixture must target a **new**
> reserved-namespace path (`fs-gg-sdd-custom`), not an existing seeded name like
> `fs-gg-sdd-plan`. The produced set is a path diff (`afterSet − beforePaths − skeletonFiles`),
> so a write to an already-seeded path is subtracted and would never reach the intrusion
> filter — the guard test would pass while detecting nothing.

## Technical Context

**Language/Version**: F# on .NET, `net10.0`.

**Primary Dependencies**: none new. The change is one `internal let` predicate in
`HandlersScaffold.fs`; the provider is still the existing `dotnet new` at the single MVU
`RunProcess` edge. `System.Text.Json` / Spectre.Console projections are untouched (rich derives
from text).

**Storage**: Files only, unchanged. `.fsgg/scaffold-provenance.json` stays schema **v1** and
gains no field (FR-008). Seeding effects (`SeededSkills`, `initEffects`) are untouched, so
`init` is byte-identical (FR-010).

**Testing**: xUnit; real `dotnet new` provider fixtures under `tests/fixtures/scaffold-provider/`
(no mocks), serialized by `[<Collection("Scaffold")>]`. New/changed: a `skills-cotenant`
positive fixture (writes `.claude/skills/fs-gg-elmish/SKILL.md`); the re-pointed
`skills-intrusion` fixture (writes `.{claude,codex}/skills/fs-gg-sdd-custom/SKILL.md`); a
co-tenant success test + seeded-skill byte-identity assertion in `ScaffoldCommandTests.fs`; the
updated negative test; an `isSddTree`/`isSddOwned` truth-table unit test; a co-tenant produced
path in `ScaffoldParityTests.fs` (json ≡ text ≡ rich); a provenance schema-v1 guard.

**Target Platform**: Cross-platform CLI (`fsgg-sdd`) + libraries.

**Project Type**: Multi-project single solution (CLI + libraries). Not web/mobile.

**Performance Goals**: N/A. Determinism (sorted `producedPaths`/`intrusions`; stable exit-code
taxonomy; byte-stable json/provenance) is the hard constraint.

**Constraints**: change only the intrusion discriminator (Principle V — classification stays in
the pure handler; the `RunProcess` edge is untouched); no new effect/command/outcome/exit code
(research R8); no persisted-schema or public-signature change (R6); `init` byte-identical (R7);
reservation symmetric across both roots (FR-007); the safety property retained — real intrusions
still exit 2 and an incomplete scaffold is never reported complete (FR-011).

**Scale/Scope**: one narrowed predicate + one small extracted helper (`isReservedSkillSubtree`);
one re-pointed fixture (rename two leak dirs → `fs-gg-sdd-custom`, registry comment); one new
fixture (registry + template + two files); ~3 new/updated tests; no `.fsi`, baseline, schema,
or docs-schema change. Agent-surface wording alignment (Claude ⇔ Codex) for the narrowed
ownership scope.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.1.0.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Spec → FSI → Semantic Tests → Impl | **PASS** | No public surface change, so no `.fsi` step; fail-first tests (positive co-tenant, re-pointed negative, truth-table unit, parity) precede the one-line `.fs` narrowing. |
| II. Structured artifacts are the machine contract | **PASS** | The provenance json is the machine contract; it is untouched in shape (schema **v1**, FR-008). Co-tenant paths appear only as additive entries in the existing `producedPaths`/`generatedProduct` arrays. |
| III. Visibility in `.fsi` | **PASS** | `isSddTree` / new `isReservedSkillSubtree` are module-`internal`; no public surface moves; no `PublicSurface.baseline` refresh. |
| IV. Idiomatic simplicity | **PASS** | One predicate narrowed, one helper extracted for the FR-007 symmetry and readability. No new type, effect, or advanced feature. Complexity Tracking empty. |
| V. Elmish/MVU boundary | **PASS** | The classification is pure and stays in `finalizeScaffold`; the `RunProcess` provider edge is unchanged. Exactly the constitutional shape. |
| VI. Test evidence mandatory | **PASS** | Fail-first: co-tenant success is red today (blocks); the re-pointed negative fails until the guard narrows; real `dotnet new` fixtures, no mocks. Synthetic fixture bodies disclosed in fixture comments/test names. |
| VII. Agent & human share one contract | **PASS** | The report/provenance is the single source of truth; agents author nothing new. Both agent surfaces' scaffold/getting-started guidance describe the narrowed ownership scope equivalently (Claude ⇔ Codex). |
| VIII. Observability & safe failure | **PASS** | Genuine intrusions still fail fast (exit 2, `providerWroteSddTree`); user-input errors stay exit 1; the co-tenant success path degrades nothing. Incomplete scaffold never reported complete (FR-011). |

**Change tier**: **Tier 1 (contracted change)** — a behavioral change to the scaffold intrusion
guard, a cross-repo integration surface (the scaffold-provider contract's behavioral coherence,
unblocking FS.GG.Rendering Feature 219). Requires spec, plan, tasks, tests, and a migration note.
**No `.fsi`, no persisted-schema migration** (additive JSON, provenance stays v1).

**Gate result**: PASS — no violations, no justified-complexity entries.

## Project Structure

### Documentation (this feature)

```text
specs/055-scaffold-cotenant-skills/
├── plan.md              # This file
├── research.md          # Phase 0 — R1–R8 (resolves the prefix-reservation crux; no clarify)
├── data-model.md        # Phase 1 — E1–E4, the isSddTree truth table
├── quickstart.md        # Phase 1 — validation scenarios A–E + test map
├── contracts/
│   └── scaffold-intrusion-discriminator.md  # the narrowed reservation rule + properties P1–P8
├── checklists/          # pre-existing (from spec authoring)
└── tasks.md             # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root) — files this feature touches

```text
src/FS.GG.SDD.Commands/CommandWorkflow/
└── HandlersScaffold.fs                 # narrow isSddTree skills clauses (:58-62) → new
                                        #   isReservedSkillSubtree; the ONLY behavioral change.
                                        #   isSddOwned/collisionPaths follow (R5); partition
                                        #   (:376-377) and provenance (:277) inherit the narrowing.

tests/
├── fixtures/scaffold-provider/
│   ├── skills-intrusion/               # RE-POINT (FR-009): rename .claude/skills/leak and
│   │   ├── .claude/skills/fs-gg-sdd-custom/SKILL.md   #   .codex/skills/leak → fs-gg-sdd-custom
│   │   └── .codex/skills/fs-gg-sdd-custom/SKILL.md
│   ├── skills-cotenant/                # NEW positive fixture: writes a non-reserved skill
│   │   ├── .template.config/template.json            #   + declares lifecycle symbol
│   │   ├── .claude/skills/fs-gg-elmish/SKILL.md
│   │   └── app.txt
│   └── registries/
│       ├── skills-intrusion.providers.yml   # comment update (targets fs-gg-sdd-custom now)
│       └── skills-cotenant.providers.yml     # NEW registry (source __FIXTURE__/skills-cotenant)
├── FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs   # + co-tenant success (US1/SC-001) +
│                                        #   seeded byte-identity (SC-002); update the negative
│                                        #   test to fs-gg-sdd-custom; + isSddTree truth-table unit
└── FS.GG.SDD.Cli.Tests/ScaffoldParityTests.fs         # + co-tenant produced path json ≡ text ≡ rich

docs/
└── release/migrations/                 # NEW note — narrowed scaffold guard (additive; no
                                        #   persisted-schema change; init byte-identical)

# Agent surfaces (Claude ⇔ Codex, kept equivalent):
CLAUDE.md / AGENTS.md                    # ownership wording: "the fs-gg-sdd-* skill subtrees are
.claude/skills/fs-gg-sdd-getting-started/SKILL.md   #   SDD-owned" (reserved namespace, not the
.codex/skills/fs-gg-sdd-getting-started/SKILL.md    #   whole root) — only if current wording overclaims
```

**Structure Decision**: The existing multi-project layout is retained. This feature is a
**one-predicate narrowing** in `HandlersScaffold.fs` plus fixtures/tests and a migration note —
no new module, type, `.fsi`, or schema. Everything else (partition, provenance, projections,
outcomes, exit codes) inherits the narrowed classification unchanged, mirroring how produced-path
content already flows as data through the same pipeline.

## Phase 0 — Research

Complete. All planning choices resolved in [research.md](./research.md): R1 (narrow the single
`isSddTree` predicate — required, not just cleanest, for FR-004), R2 (prefix reservation, no
clarify), R3 (re-point the negative fixture to a *new* reserved path — the diff-mechanism trap),
R4 (add a co-tenant success fixture), R5 (collision-exclusion coupling is benign/more-correct,
covered by a truth-table test), R6 (no schema/signature/projection reshape), R7 (`init`
byte-identical), R8 (safety property retained). No `NEEDS CLARIFICATION` remain.

## Phase 1 — Design & Contracts

Complete. Artifacts generated: [data-model.md](./data-model.md), the
[contracts/](./contracts/) discriminator spec, [quickstart.md](./quickstart.md). Agent context
(`CLAUDE.md` SPECKIT block) updated to point at this plan.

## Phase 2 — Task planning approach (for `/speckit-tasks`, not executed here)

Expected task ordering (spec → tests → impl, fail-first):

1. **Fixtures**: re-point `skills-intrusion` (rename the two `leak` dirs → `fs-gg-sdd-custom`,
   update registry comment); add the `skills-cotenant` positive fixture (template.json declaring
   `lifecycle`, `.claude/skills/fs-gg-elmish/SKILL.md`, `app.txt`) + its registry.
2. **Fail-first tests**: co-tenant success (exit 0, `providerSucceeded`, co-tenant path in
   `producedPaths` as `generatedProduct`, seeded skills byte-identical — US1/SC-001/SC-002);
   update the negative test to expect `fs-gg-sdd-custom` rejected on both roots (FR-002/007/009);
   an `isSddTree`/`isSddOwned` truth-table unit test (R1/R5); a `ScaffoldParityTests` co-tenant
   produced-path parity case (FR-004/008); a provenance schema-v1 guard.
3. **Impl**: extract `isReservedSkillSubtree` and narrow the two skills clauses of `isSddTree`
   (`HandlersScaffold.fs:58-62`). No other source edit.
4. **Docs**: migration note (narrowed guard, additive, provenance v1, `init` byte-identical).
5. **Agent surfaces**: if any current wording claims the *whole* skill root as SDD-owned, tighten
   it to "the `fs-gg-sdd-*` skill subtrees" in `CLAUDE.md`/`AGENTS.md` and the getting-started
   skill on both surfaces (Claude ⇔ Codex aligned).

**Cross-repo**: no versioned contract surface change (behavioral coherence only). Recording the
co-tenant ownership model in the coherence set / ADR is a follow-up tracked by
**FS-GG/FS.GG.Templates#47**; close **FS-GG/FS.GG.SDD#55** at merge via
`cross-repo-coordination`. Unblocks **FS.GG.Rendering Feature 219**.

## Complexity Tracking

No entries — no constitution violations and no justified complexity (no new effect, command,
outcome, exit code, type, schema, or advanced F# feature; one predicate narrowed).
