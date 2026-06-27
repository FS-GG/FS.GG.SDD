# Implementation Plan: SDD skeleton emits the lifecycle constitution at `.fsgg/constitution.md`

**Branch**: `033-skeleton-constitution` | **Date**: 2026-06-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/033-skeleton-constitution/spec.md`

## Summary

`fsgg-sdd init` lays the SDD skeleton — today `.fsgg/project.yml`, `.fsgg/sdd.yml`,
`.fsgg/agents.yml`, `CLAUDE.md`, `AGENTS.md`, and the `work/`/`readiness/` roots — and
`fsgg-sdd scaffold` reuses those effects **unchanged** before invoking an app-only provider.
The skeleton emits **no constitution today**. **[ADR-0004](https://github.com/FS-GG/.github/blob/main/docs/adr/0004-constitution-ownership-for-lifecycle-sdd-products.md)**
resolved the P0 gate left open by ADR-0002 Decision 4 in favour of **SDD**: SDD owns and
ships the F# lifecycle constitution for `lifecycle=sdd` products, at **`.fsgg/constitution.md`**,
in the existing SDD skeleton namespace.

This feature adds **exactly one** new authored skeleton file to `initEffects`
(`Foundation.fs:81-91`): a `WriteFile(".fsgg/constitution.md", constitutionText, AgentGuidanceTarget)`
effect. Everything else falls out **for free** from the existing architecture, which the
grounded inventory below proves:

1. **Scaffold delivers it with zero scaffold-specific logic (FR-004).** `scaffold` already
   lays the skeleton by replaying `initEffects`, so the constitution is written on the
   scaffold path automatically.
2. **It is excluded from app-only provenance with no provenance change (FR-005).** Scaffold's
   `generatedProduct` set is computed as `after − before − skeletonFiles − provenance`, and
   `skeletonFiles` is **derived** from `initEffects` (`HandlersScaffold.fs:77-82`). Adding the
   constitution to `initEffects` adds it to the subtracted skeleton set in the same step —
   it can never appear in `generatedProduct`.
3. **`refresh` leaves it untouched (FR-009).** `refresh` only regenerates a fixed set of
   `readiness/<id>/*` and configured-agent-guidance paths; it has no generator targeting any
   `.fsgg/` root file, so the constitution is never regenerated nor flagged stale.
4. **It is no-clobber by construction (FR-008).** `canOverwrite` (`CommandEffects.fs:42-48`)
   permits overwriting a differing existing file **only** for `AuthoredSource` and
   `GeneratedView`; `AgentGuidanceTarget` (and `StructuredSource`) **refuse**. Choosing
   `AgentGuidanceTarget` — the kind CLAUDE.md/AGENTS.md already use — gives the constitution
   the identical no-clobber behaviour US3 names as its analog.

The only real authoring work is **the generic constitution body** (FR-002/FR-003): a
populated, placeholder-free, deterministic F#-SDD-product constitution containing no
FS.GG.SDD-repo-, provider-, template-, or rendering-specific names, paths, or URLs. The
authoritative seed body is fixed in
[contracts/constitution-content.md](./contracts/constitution-content.md) so implementation
transcribes it verbatim (determinism, FR-007).

Decisions locked in [research.md](./research.md):

1. **`AgentGuidanceTarget`, not `AuthoredSource`.** The spec's Key-Entity hint listed
   "`AuthoredSource` / `AgentGuidanceTarget`", but `canOverwrite` makes `AuthoredSource`
   *overwrite-allowed* on differing content — it would silently clobber an author-ratified
   constitution and **violate FR-008**. `AgentGuidanceTarget` is the established no-clobber
   kind for root authored markdown skeleton files (CLAUDE.md/AGENTS.md), which US3 explicitly
   names as the behavioral model. It also yields `Ownership = "authored"` in the report
   (`CommandReports.fs:934`, `if kind = GeneratedView then "generated" else "authored"`),
   satisfying FR-010. `StructuredSource` (the `.fsgg/*.yml` kind) is the close runner-up —
   also no-clobber, also "authored" — rejected only because the constitution is prose
   markdown, not structured config, and US3 anchors on the markdown analog.
2. **One `initEffects` line, zero other production edits.** The skeleton set, the
   scaffold-provenance exclusion, the refresh exclusion, and the report attribution are all
   *derived* from `initEffects`. Adding the single `WriteFile` is sufficient for FR-001/004/
   005/008/009/010. No new `ArtifactWriteKind`, no `.fsi` change, no schema change, no
   scaffold/refresh/provenance code change.
3. **Generic body is content, fixed by contract; determinism is structural.** The body is a
   string literal beside `agentGuidance`/`sddConfigText` in `Foundation.fs`, carries no
   timestamp/date/randomness, and is identical across runs and machines (FR-007/SC-003) by
   the same mechanism as every other skeleton string.
4. **Re-baseline is automatic; the assertions are dynamic.** The init/scaffold disjointness,
   byte-identity, and determinism tests enumerate the skeleton **dynamically**
   (`relativeFiles initRoot`), so they self-adjust to include the new file and keep passing;
   the hardcoded app-only produced set (`["App.fsproj"; "Program.fs"; "scaffold-manifest.txt"]`)
   is unaffected. New positive tests are *added* for US1/US2/US3; no golden file regeneration
   is forced unless `release-readiness.json` is shown to enumerate the skeleton (it does not —
   verified in Phase 1).
5. **The constitution is not a release-catalog artifact.** It is authored skeleton content,
   not a produced lifecycle artifact; `docs/release/` and `release-readiness.json` catalog
   lifecycle artifacts/commands and make no mention of skeleton files, so the release contract
   and its golden baseline are unchanged.

**Change tier**: **Tier 1 (contracted change)** — implements the SDD-side obligation of
**ADR-0004** and **re-baselines the `init` byte-identical invariant** by adding one observable
skeleton artifact. It changes **no** provider contract, **no** provider invocation protocol,
and **no** `scaffold-provenance.json` schema. Because the only new public-surface touch is one
additional emitted file (not a type/signature change), `.fsi` files and `PublicSurface.baseline`
snapshots are **unchanged**; the contract surface that moves is the *skeleton set*, re-baselined
via the dynamic skeleton tests plus new US1/US2/US3 coverage.

## Technical Context

**Language/Version**: F# on .NET, target `net10.0` (constitution Engineering Constraints).

**Primary Dependencies**: Standard library only. The emission reuses the existing
`WriteFile` effect (`CommandTypes` `CommandEffect`) and its edge interpreter
(`CommandEffects.fs:135-146`) **unchanged**. No new package, no new effect case, no new
external tool.

**Storage**: Filesystem only. One new authored skeleton file `.fsgg/constitution.md`. No new
artifact *type*. `.fsgg/scaffold-provenance.json` (schema v1) and `.fsgg/providers.yml` (v1)
are **byte-unaffected**. No release-catalog change.

**Testing**: `dotnet test FS.GG.SDD.sln` (xUnit; Artifacts, Validation, Cli, Commands). New
coverage lands in `InitCommandTests.fs` (US1: existence, populated, placeholder-free, generic,
determinism, no-clobber re-run), `ScaffoldCommandTests.fs` (US2: present in product, absent
from `generatedProduct`), and `RefreshCommandTests.fs` (US3: refresh leaves it untouched and
never flags it stale/generated/external). All assertions run over **real** filesystem fixtures
through the public command surface (constitution VI); no mocks.

**Target Platform**: Linux/cross-platform CLI. The emission is a pure `File.WriteAllText` at
the existing edge; no platform-sensed behavior, no process, no permission bit.

**Performance Goals**: N/A. One additional small file write per `init`/`scaffold`.

**Project Type**: Single solution, CLI + libraries (`src/FS.GG.SDD.*`). The reference provider
(a runnable UI app) lives in FS.GG.Rendering and is **not** a dependency here; the constitution
is generic SDD skeleton content verified by in-repo fixtures.

**Constraints**:
- **Determinism (FR-007/SC-003)**: the constitution body is a constant string literal with no
  date/timestamp/randomness/environment-derived content; two `init` runs on identical inputs
  produce byte-identical `.fsgg/constitution.md`, exactly as the other skeleton strings do.
- **`init` baseline moves once (FR-006)**: adding the constitution deliberately re-baselines
  the "`init` stays byte-identical across releases" invariant; the new output (now including
  `.fsgg/constitution.md`) becomes the baseline, after which byte-identical stability resumes.
  `scaffold` inherits the new skeleton with **no** scaffold-specific change.
- **No-clobber (FR-008)**: `AgentGuidanceTarget` → `canOverwrite` returns `false` for a
  differing existing file (`CommandEffects.fs:48`) → the write is refused and the existing
  author edits are preserved (`SafeWriteDecision = "refused"`/`"preserveExisting"`), identical
  to CLAUDE.md/AGENTS.md.
- **Generic content (FR-003/SC-006)**: zero FS.GG.SDD-repo-, provider-, template-, or
  rendering-specific strings. The repo-wide **C1** leak scan in `ScaffoldGuardTests.fs` over
  `src/**/*.fs(i)` already covers the new string literal in `Foundation.fs` for provider/
  rendering identifiers; a dedicated US1 generic-content assertion covers the broader
  repo-/template-specific check.
- **`--rich` is a pure projection**: the new file appears in the changed-artifacts list of the
  same `CommandReport` the text/rich views already render; no JSON byte, stream, or exit-code
  change. Rich stays excluded from deterministic/golden contracts.
- `WarningsAsErrors` ratchet stays at 0; no `#nowarn` introduced.

**Scale/Scope**: +1 `WriteFile` effect in `initEffects`; +1 constant string literal
(`constitutionText`) in `Foundation.fs`; 0 new `CommandEffect` cases; 0 new `ArtifactWriteKind`
cases; 0 `.fsi` edits; 0 schema/provenance/release-catalog changes; 0 new commands. Tests:
+US1/US2/US3 scenarios across three existing test files; no forced golden regeneration. Agent
surfaces (CLAUDE.md/AGENTS.md + 2× SKILL.md) gain a one-line "the skeleton seeds
`.fsgg/constitution.md`" note (no workflow-shape change).

### Grounded inventory (current tree, verified 2026-06-27 @ `50b086e`)

| Concern | Anchor | Disposition (this feature) |
|---|---|---|
| Skeleton effect list | `Foundation.fs:81-91` (`initEffects`) | **extend**: add `WriteFile(".fsgg/constitution.md", constitutionText, AgentGuidanceTarget)` |
| Skeleton content strings | `Foundation.fs:34-79` (`projectConfigText`/`sddConfigText`/`agentsConfigText`/`agentGuidance`) | **add** a sibling `constitutionText` constant (the contract body) |
| Effect interpreter (write) | `CommandEffects.fs:135-146` (`WriteFile` arm) | **reuse unchanged** — writes the file; refuses unsafe overwrite |
| No-clobber policy | `CommandEffects.fs:42-48` (`canOverwrite`) | **reuse unchanged** — `AgentGuidanceTarget` ⇒ refuse on differing existing file (FR-008) |
| Report attribution | `CommandReports.fs:914-944` (`changeFromEffectResult` `WriteFile` arm) | **reuse unchanged** — emits `Kind="agentGuidance"`, `Ownership="authored"` (FR-010) |
| Scaffold skeleton subtraction | `HandlersScaffold.fs:77-82` (`skeletonFiles`) + `:308-310` (`produced`) | **reuse unchanged** — `skeletonFiles` is derived from `initEffects`, so the new path is auto-excluded from `generatedProduct` (FR-005) |
| Refresh classification | `HandlersRefresh.fs:113-123` (`authoredPreserved`, informational) + regeneration targets (`:178-295`) | **reuse unchanged** — no generator targets `.fsgg/` root files (FR-009); optionally add `.fsgg/constitution.md` to the informational `authoredPreserved` list for report symmetry |
| Skeleton-set tests (dynamic) | `ScaffoldCommandTests.fs:442-469` / `:474-492` / `:498-509` | **no edit** — enumerate skeleton via `relativeFiles initRoot`; self-adjust and keep passing |
| Init existence test | `InitCommandTests.fs:21-33` | **extend**: assert `.fsgg/constitution.md` exists, is populated, placeholder-free, generic, deterministic, no-clobber (US1) |
| Init plan test | `CommandWorkflowTests.fs:11-24` (`Assert.Contains`) | **extend** (clarity): add a `Contains` assertion for the constitution `WriteFile` effect |
| Release contract golden | `tests/FS.GG.SDD.Artifacts.Tests/baselines/release-readiness.json` (`ReleaseContractTests.fs:112-119`) | **no change** — catalog lists lifecycle artifacts/commands, not skeleton files (verified) |
| Public-surface baselines | `tests/**/PublicSurface.baseline` (×4) | **no change** — no type/signature surface change |
| Agent surfaces | `CLAUDE.md`, `AGENTS.md`, `.claude/skills/fs-gg-sdd-project/SKILL.md`, `.codex/skills/fs-gg-sdd-project/SKILL.md` | **one-line note**: the skeleton seeds an authored `.fsgg/constitution.md`, kept aligned across Claude & Codex (constitution VII) |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec → FSI → FSI-exercise → Tests → Impl | ✅ | No new public *signature* (the change is one emitted file using existing types), so there is no `.fsi` to sketch. The new behavior is exercised through the public command surface (the `init`/`scaffold` MVU loop) in tests authored to fail first (a skeleton with no `.fsgg/constitution.md`) and pass after. |
| II. Structured artifacts are the contract | ✅ | The constitution is **authored markdown**, deliberately *not* a generated view or a structured machine contract — it carries no source digests and drives no gate. The authoritative produced-path contract (`scaffold-provenance.json`, v1) is byte-unchanged and continues to exclude it. Prose↔structured conflict: none — the report's changed-artifacts entry (`Kind`/`Ownership`) is the single structured record of the emission and adds no fact the file lacks. |
| III. Visibility lives in `.fsi` | ✅ | No public module surface changes; `.fsi` files and `PublicSurface.baseline` snapshots are unchanged. The moved contract is the *skeleton set*, re-baselined via the dynamic skeleton tests + new US1/US2/US3 coverage, not a signature. |
| IV. Idiomatic simplicity | ✅ | One `WriteFile` list entry and one `string` constant in `Foundation.fs`, reusing the existing effect, interpreter, and no-clobber policy. No new abstraction, type, reflection, or framework. |
| V. Elmish/MVU boundary | ✅ | The emission is an **effect** (`WriteFile`) produced by the pure `initEffects` planner and performed only at the edge interpreter (`CommandEffects.fs`). No I/O is added to any `update`; the planner stays a pure value. |
| VI. Test evidence | ✅ | Real-filesystem fixtures through the public surface: US1 asserts a real, populated, placeholder-free, generic `.fsgg/constitution.md` after `init` + byte-identical re-run; US2 a real scaffolded product with the file present and absent from `generatedProduct`; US3 a real author-edit surviving `init` re-run and `refresh`. No mocks; tests fail before the `initEffects` line exists. |
| VII. One contract for agents + humans | ✅ | The constitution is one artifact for CLI users, CI, Claude, and Codex; the CLAUDE/AGENTS/2× SKILL surfaces gain a single aligned line noting the skeleton seeds it. It is authored content, not a second source of truth, and not agent-generated. |
| VIII. Observability & safe failure | ✅ | The emission is reported as a changed artifact attributed to the SDD skeleton (FR-010); a no-clobber refusal surfaces the existing safe-write decision (`refused`/`preserveExisting`) rather than silently overwriting (constitution VIII / FR-008). No new failure mode; nothing escalates a skeleton write to a tool defect. |

**Change tier**: **Tier 1 (contracted change)** — see Summary. The moved contract (the `init`
skeleton set / byte-identical baseline) is justified by the Accepted ADR-0004 obligation.
**Complexity Tracking is empty** — there is no constitution violation; the single `WriteFile`
is the idiomatic MVU expression of the required skeleton emission.

**Lifecycle-feature plan checklist** (constitution §Development Workflow):
- *Authored artifacts*: **one new** — `.fsgg/constitution.md`, seeded once by the skeleton,
  thereafter author-owned (no-clobber). Classified `AgentGuidanceTarget` (no-clobber, authored
  ownership), like CLAUDE.md/AGENTS.md.
- *Structured machine contracts*: none new; `scaffold-provenance.json` (v1) and `providers.yml`
  (v1) are byte-unchanged. The report's changed-artifacts entry is the only structured record.
- *Generated views*: none; the constitution is **not** a generated view. `refresh` continues to
  exclude it and never flags it stale.
- *Schema version & migration*: no schema touched → no migration. Release catalog unchanged.
- *Agent behavior (Claude & Codex)*: one aligned one-line note added to both surfaces + both
  SKILLs.
- *Optional Governance integration*: none. Governance-owned effective-evidence freshness and
  gate enforcement remain optional downstream concerns; the seeded constitution does not change
  the handoff.
- *Tests/fixtures for stale/conflicting artifacts*: no-clobber on `init` re-run, `refresh`
  leaves it untouched (not stale/generated/external), scaffold `generatedProduct` exclusion,
  determinism across runs, and the generic-content guard are all exercised (Edge Cases).

## Project Structure

### Documentation (this feature)

```text
specs/033-skeleton-constitution/
├── plan.md              # This file
├── research.md          # Phase 0 — AgentGuidanceTarget vs AuthoredSource (canOverwrite/FR-008);
│                         #   one-line emission; derived skeleton-set exclusions; generic body;
│                         #   dynamic re-baseline; release-catalog exclusion
├── data-model.md        # Phase 1 — the constitution artifact; ArtifactWriteKind/canOverwrite
│                         #   semantics; report changed-artifact shape; provenance/refresh exclusion
├── quickstart.md        # Phase 1 — run the US1/US2/US3 suite; expected outcomes; how to
│                         #   reproduce determinism, no-clobber, and the generatedProduct exclusion
├── contracts/           # Phase 1 — behavior + content contracts (no new external interface)
│   ├── constitution-content.md   # the authoritative generic seed body (transcribe verbatim)
│   ├── init-emission.md          # init writes .fsgg/constitution.md; kind; report attribution; determinism
│   ├── scaffold-provenance.md    # delivered via reused init effects; excluded from generatedProduct
│   └── lifecycle-exclusion.md    # no-clobber on re-run (FR-008); refresh leaves it untouched (FR-009)
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Commands/CommandWorkflow/
└── Foundation.fs
    ├── constitutionText            # NEW constant: the generic seed body (contracts/constitution-content.md)
    └── initEffects                 # + WriteFile(".fsgg/constitution.md", constitutionText, AgentGuidanceTarget)

# Reused UNCHANGED (proof the rest is free):
#   CommandEffects.fs:42-48,135-146   canOverwrite + WriteFile interpreter (no-clobber, FR-008)
#   CommandReports.fs:914-944         changed-artifact attribution (Kind/Ownership, FR-010)
#   HandlersScaffold.fs:77-82,308-310 skeletonFiles (derived) + produced diff (FR-004/005)
#   HandlersRefresh.fs:113-295        refresh targets exclude .fsgg/ root files (FR-009)

tests/FS.GG.SDD.Commands.Tests/
├── InitCommandTests.fs             # + US1: exists, populated, placeholder-free, generic,
│                                   #   deterministic re-run, no-clobber author edit
├── ScaffoldCommandTests.fs         # + US2: present in product, absent from generatedProduct
├── RefreshCommandTests.fs          # + US3: refresh leaves author edits untouched; not stale/generated/external
└── CommandWorkflowTests.fs         # + (clarity) Contains assertion for the constitution WriteFile effect

# Agent surfaces (one aligned line each):
#   CLAUDE.md, AGENTS.md, .claude/skills/fs-gg-sdd-project/SKILL.md, .codex/skills/fs-gg-sdd-project/SKILL.md
```

**Structure Decision**: Single-solution F# layout retained. Unlike feature 032 (new scaffold
behavior + a new effect case), this feature adds **no** public signature: it is one skeleton
`WriteFile` plus one content constant, and every downstream property (scaffold delivery,
provenance exclusion, refresh exclusion, report attribution, no-clobber) is *derived* from the
existing architecture. The work concentrates in the constitution body (the content contract)
and in US1/US2/US3 verification, not in production code.

## Complexity Tracking

No Constitution Check violations — the single `WriteFile` skeleton effect is the idiomatic MVU
expression of the ADR-0004 emission obligation, and reusing the existing no-clobber/provenance/
refresh machinery is strictly simpler than any alternative. This section is intentionally empty.
