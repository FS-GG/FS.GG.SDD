# Implementation Plan: CLI Version Coherence in Scaffold Provenance

**Branch**: `052-cli-version-coherence` | **Date**: 2026-07-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/052-cli-version-coherence/spec.md`

## Summary

Make the `fsgg-sdd` CLI a first-class, auditable input to a scaffolded product. (1) Record the
provider-declared **minimum coherent CLI version** next to the already-recorded producing CLI
version in `.fsgg/scaffold-provenance.json` (additive, schema stays v1). (2) When the installed
CLI is **strictly behind** that minimum, emit one **non-blocking** `scaffold.cliBehindMinimum`
advisory (naming installed / minimum / gap) carrying a next-action pointer to the re-seed path,
in all three report projections. The minimum is read value-agnostically from the provider
registry; comparison uses one shared `Fsgg.Version` grammar (the existing Registry SemVer engine,
made public). No provider-specific literal enters generic SDD, and the change degrades cleanly
(no minimum / malformed minimum / undeterminable CLI version) so it is independently shippable
ahead of the epic-#85 Templates/registry halves.

See [research.md](./research.md) for the resolved design decisions (D1–D11),
[data-model.md](./data-model.md) for the additive entities, and
[contracts/](./contracts/) for the four contract specs.

> **⚠ Spec correction surfaced during research (D8)**: FR-008/US3 name the remedy as
> "`refresh-agents` / the seeding effects", but `fsgg-sdd refresh` does **not** re-seed — the
> seeding effects run via `fsgg-sdd init` (idempotent/no-clobber, reused by scaffold). The plan
> points the advisory and docs at **"upgrade the CLI, then re-run `fsgg-sdd init`"** (within
> FR-008's "the seeding effects" wording) and explicitly notes `refresh` does not re-seed.

## Technical Context

**Language/Version**: F# on .NET, `net10.0`.

**Primary Dependencies**: `System.Text.Json` (`Utf8JsonWriter`, deterministic hand-ordered
serialization); YamlDotNet-backed `parseYaml` (provider registry); Spectre.Console (rich
projection only). BCL-only version comparison — no new dependency.

**Storage**: Files — `.fsgg/scaffold-provenance.json` (SDD-owned artifact), `.fsgg/providers.yml`
(author/provider-owned registry). No database.

**Testing**: xUnit (`open Xunit`), real filesystem/fixture-provider tests; golden/byte-stable
JSON fixtures; `PublicSurface.baseline` surface-area guards.

**Target Platform**: Cross-platform CLI (`fsgg-sdd`) + libraries.

**Project Type**: Multi-project single solution (CLI + libraries). Not web/mobile.

**Performance Goals**: N/A (a three-integer compare + one extra JSON field). Determinism is the
hard constraint, not throughput.

**Constraints**: Additive & backward-compatible (schema stays v1; existing readers unaffected);
deterministic byte-stable JSON (no clock/absolute path); value-agnostic (no provider-specific
literal in generic SDD); non-blocking advisory (exit code unchanged); rich degrades to zero-ANSI.

**Scale/Scope**: One additive provenance field, one additive provider-descriptor field, one new
shared version module, two new diagnostic codes, one new next-action branch, one new scaffold
summary field, plus docs + fixtures.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.1.0.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Spec → FSI → Semantic Tests → Impl | **PASS** | `.fsi` updated for `ScaffoldProvenance`, `Fsgg.Provider`, new `Fsgg.Version`, `CommandTypes`; tests written to fail-first. |
| II. Structured artifacts are the machine contract | **PASS** | Provenance JSON is authoritative; additive field documented in `schema-reference.md`; malformed provider input reported (never silently dropped). |
| III. Visibility in `.fsi` | **PASS** | New public surface only in `.fsi`; `PublicSurface.baseline` for `FS.GG.Contracts`, `FS.GG.SDD.Artifacts`, `FS.GG.SDD.Commands` refreshed. |
| IV. Idiomatic simplicity | **PASS** | `string option` field + a records-and-functions version module; no advanced-feature justification needed. |
| V. Elmish/MVU boundary | **PASS** | Coherence check is a **pure** function in the scaffold `update`; no new `Effect`/edge I/O — reuses the existing single provenance `WriteFile`. |
| VI. Test evidence mandatory | **PASS** | Golden fixtures + advisory/parity/back-compat tests, real fixture provider (no mocks). |
| VII. Agent & human share one contract | **PASS** | Docs + seeded-skill/getting-started guidance updated for Claude and Codex equivalently; no second source of truth. |
| VIII. Observability & safe failure | **PASS** | Staleness = `Info`; malformed provider minimum = `Warning`; distinguishes provider-input defect from author input; honest degradation when CLI version undeterminable. |

**Change tier**: **Tier 1 (contracted change)** — schema, provider contract read, command
output, agent-skill/docs. Requires spec, plan, tasks, `.fsi`, tests, docs, migration note.

**Gate result**: PASS — no violations, **no Complexity Tracking entries required**.

## Project Structure

### Documentation (this feature)

```text
specs/052-cli-version-coherence/
├── plan.md              # This file
├── research.md          # Phase 0 — D1–D11 decisions
├── data-model.md        # Phase 1 — additive entities E1–E6
├── quickstart.md        # Phase 1 — validation scenarios A–G
├── contracts/           # Phase 1
│   ├── scaffold-provenance-v1-additive.md
│   ├── provider-registry-minimum-cli.md
│   ├── version-compare.md
│   └── advisory.md
├── checklists/          # pre-existing
└── tasks.md             # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root) — files this feature touches

```text
src/
├── FS.GG.Contracts/
│   ├── Version.fs / Version.fsi        # NEW — shared major.minor.patch grammar (D3/E3)
│   ├── Registry.fs                     # refactor private SemVer to delegate to Fsgg.Version
│   ├── Provider.fs / Provider.fsi      # + ProviderDescriptor.MinimumCliVersion (E2)
│   └── Schemas.fs / Schemas.fsi        # optional: mirror the new field (coherence, D10)
├── FS.GG.SDD.Artifacts/
│   ├── ScaffoldProvenance.fs / .fsi    # + RequiredMinimumCliVersion; serialize/tryParse (E1)
│   └── Diagnostics.fs                  # + scaffold.cliBehindMinimum, scaffold.providerMinimumMalformed (E5)
└── FS.GG.SDD.Commands/
    ├── CommandWorkflow/
    │   ├── HandlersScaffold.fs         # parse min → pure cliCoherenceDiagnostics; thread into provenanceWriteEffect (D11)
    │   └── Config.fs                   # parse minimumCliVersion scalar (E2)
    ├── CommandTypes.fs / .fsi          # + ScaffoldSummary.RequiredMinimumCliVersion (E4)
    ├── CommandReports.fs               # NextAction branch reseedSeededSkills (E6)
    ├── CommandSerialization.fs         # emit new scaffold-block field (JSON)
    └── CommandRendering.fs             # emit new scaffoldRequiredMinimumCliVersion text line

tests/
├── FS.GG.Contracts.Tests/             # Fsgg.Version + Registry-delegation regression; baseline
├── FS.GG.SDD.Artifacts.Tests/         # ScaffoldProvenanceTests: serialize/tryParse + back-compat
├── FS.GG.SDD.Commands.Tests/          # ScaffoldCommandTests goldens; new advisory/degradation tests
└── FS.GG.SDD.Cli.Tests/               # ScaffoldParityTests / EarlyStageProjectionTests-style parity

docs/
├── release/schema-reference.md         # document requiredMinimumCliVersion (declared-exception section)
├── release/migrations/                 # NEW note (feature-050 precedent)
└── reference/…                         # re-seed remedy for behind-minimum CLI (FR-010/US3)
```

**Structure Decision**: Existing multi-project layout is retained. The one **new** unit is the
shared `Fsgg.Version` module in `FS.GG.Contracts` (the natural home for a cross-repo-shared
version grammar the Registry already needs); everything else is additive edits to existing files.

## Phase 0 — Research

Complete. All spec-open planning choices resolved in [research.md](./research.md):
D1 (CLI version already recorded), D2 (value-agnostic provider minimum), D3 (one shared
`Fsgg.Version` grammar), D4 ("behind" boundary), D5 (`Info` non-blocking advisory), D6 (malformed
minimum → `Warning`, record null), D7 (undeterminable CLI version → skip), D8 (re-seed remedy is
`init`, **not** `refresh` — spec correction), D9 (three-projection surfacing), D10 (additive v1 /
minor bump / migration note), D11 (pure MVU placement, no new effect).

## Phase 1 — Design & Contracts

Complete. Artifacts generated: [data-model.md](./data-model.md), the four
[contracts/](./contracts/) specs, [quickstart.md](./quickstart.md). Agent context (`CLAUDE.md`
SPECKIT block) updated to point at this plan.

## Phase 2 — Task planning approach (for `/speckit-tasks`, not executed here)

Expected task ordering (spec→fsi→tests→impl, bottom-up by dependency):

1. **`Fsgg.Version`** (`.fsi` → tests → `.fs`); refactor `Registry` private SemVer to delegate;
   `FS.GG.Contracts` baseline.
2. **Provider descriptor** additive field + `Config` parse; registry fixtures (with/without
   `minimumCliVersion`).
3. **Provenance** additive field: `ScaffoldProvenance.fsi`/`.fs` serialize (after `generator`,
   string-or-null) + `tryParse` default `None`; Artifacts back-compat test.
4. **Diagnostics**: two new `scaffold.*` codes.
5. **Scaffold handler**: pure `cliCoherenceDiagnostics`; merge into `model.Diagnostics` on all
   descriptor-resolved paths; thread `requiredMinimumCliVersion` into `provenanceWriteEffect`.
6. **Report projections**: `ScaffoldSummary` field; JSON + text emit; `NextAction` branch;
   verify rich derives automatically.
7. **Tests**: Commands goldens updated; new behind/at-or-above/no-min/malformed/undeterminable
   cases; CLI three-projection parity + exit-code-parity (SC-004); grep-based value-agnostic
   guard (SC-005).
8. **Docs**: `schema-reference.md` field; migration note; re-seed remedy doc + getting-started/
   refresh-agents skill guidance (noting `refresh` does not re-seed), Claude ⇔ Codex aligned.
9. **Optional coherence**: mirror the field in `FS.GG.Contracts` `ScaffoldProvenanceSchema` and
   record the pre-existing `effectiveParameters` mirror drift as a known issue.

Cross-repo: confirm the `minimumCliVersion` key name against the epic-#85 shared contract issue
before merge (`cross-repo-coordination`); the additive schema is a **minor** package bump via the
contracts version-bump checklist (no handoff `contractVersion` involved).

## Complexity Tracking

No constitution violations — no entries.
