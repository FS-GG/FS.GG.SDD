# Implementation Plan: Bootstrap and Migration Experience

**Branch**: `016-bootstrap-migration` | **Date**: 2026-06-20 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/016-bootstrap-migration/spec.md`

**Note**: Spec Kit setup returned an empty script branch because this repository
is currently on Git branch `main`; the active feature context is persisted in
`.specify/feature.json` as `specs/016-bootstrap-migration`.

## Summary

Deliver the Phase 9 bootstrap and migration experience that makes FS.GG.SDD
usable for new products and existing Spec Kit projects **without changing the
lifecycle command surface**. This feature ships three consumer documentation
surfaces plus one automated end-to-end verification harness over the already
complete `fsgg-sdd` command family:

1. A consumer **quickstart** (`docs/quickstart.md`) that walks a new user from
   `fsgg-sdd init` through `fsgg-sdd ship` for one work item with no Governance
   gate runtime installed, naming for each stage the authored source written and
   the generated readiness view refreshed or reported, and showing where the
   cross-cutting `fsgg-sdd agents` and `fsgg-sdd refresh` generators bring agent
   guidance and the `summary.md` projection to currency.
2. A **migration guide** (`docs/migration-from-spec-kit.md`) that maps an
   existing standard Spec Kit project's `specs/` and `.specify/` artifacts onto
   native SDD `.fsgg` and `work/<id>` sources additively, preserving standard
   Spec Kit as a valid workflow and never instructing destructive removal.
3. An **optional Governance adoption note** (`docs/adopting-governance.md`)
   documenting how `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, and
   `.fsgg/tooling.yml` are added after `fsgg-sdd init` as an additive,
   compatibility-only layer that never changes SDD command usability.
4. An automated **lifecycle smoke** (`tests/FS.GG.SDD.Commands.Tests/LifecycleSmokeTests.fs`)
   that creates a disposable SDD project, drives `init` → `charter` → `specify`
   → `clarify` → `checklist` → `plan` → `tasks` → `analyze` → `evidence` →
   `verify` → `ship` plus `agents` and `refresh` in-process over the existing
   command workflow, asserts every stage's authored source and generated
   readiness view, asserts no Governance policy/capability/tooling files are
   required, asserts the emitted next-action chain the quickstart documents
   (FR-014), and asserts determinism across two runs. A real CLI process smoke is
   captured as readiness evidence to prove the shipped executable path.

This feature **introduces no new lifecycle stage, no new `fsgg-sdd` command, and
no new authored-source or generated-view schema**. The only F# change is a new
test file that exercises existing public surfaces through the existing
`TestSupport` run helpers (`initializeProject`, `runCharter` … `runShip`,
`runAgents`, `runRefresh`); no `.fsi` signature, public API baseline, or
structured contract changes. The deliverables document and verify the existing
command surface and `.fsgg` + `work/<id>` artifact layout.

Runtime product templates and FS.GG.Rendering template-provider delegation for
generating product runtime code are **optional and out of scope** (the SDD/
Rendering ownership boundary; `init` already creates the lifecycle skeleton).
Governance-owned routing, effective-evidence freshness, profiles, gates, audit,
and release behavior remain out of scope; Governance references in the docs are
advisory compatibility facts only.

## Technical Context

**Language/Version**: F# on .NET SDK targeting `net10.0` (the new smoke test);
Markdown for the three shipped consumer documentation surfaces.

**Primary Dependencies**: Existing `FS.GG.SDD.Artifacts`, `FS.GG.SDD.Commands`,
and `FS.GG.SDD.Cli` projects, exercised unchanged. The smoke reuses the existing
`FS.GG.SDD.Commands.Tests` harness (`TestSupport` with `findRepoRoot`,
`tempDirectory`, `runRequest`, and the per-stage `run*` helpers including
`runAgents` and `runRefresh`) and xUnit. The CLI process smoke runs the existing
`FS.GG.SDD.Cli` executable. No new package dependency.

**Storage**: Filesystem only. The smoke creates a disposable temp project under
the OS temp directory (via the existing `tempDirectory()` helper) and asserts
over the authored sources under `.fsgg/` and `work/<id>/` and the generated views
under `readiness/<id>/` (`work-model.json`, `analysis.json`, `verify.json`,
`ship.json`, `summary.md`, and `agent-commands/<target>/`). It writes no
repository files and depends on no surrounding-repository Governance state.

**Schema/Migration**: No new schema. The feature adds no structured contract and
changes none; the smoke asserts over the schema versions the existing generators
already emit. Documentation is an authoring surface (Constitution II). The
migration guide describes an additive, diagnose-nothing posture: it never
rewrites or removes existing `specs/` or `.specify/` content and is safe to
re-apply. The shipped docs use FsDocs-style frontmatter consistent with
`docs/initial-implementation-plan.md`.

**Testing**: `dotnet test` with xUnit. New `LifecycleSmokeTests.fs` covering:
full lifecycle init→ship plus `agents` and `refresh` over a disposable project;
per-stage authored-source and generated-view assertions; no-Governance assertion
(no `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, or `.fsgg/tooling.yml` created
or required); next-action chain assertion that matches the documented quickstart
ordering and pointers (FR-014); determinism assertion comparing machine-readable
readiness across two runs over identical inputs (FR-006); a Governance-files-
present-but-incomplete variant asserting every command stays usable (FR-011); a
no-Rendering / no-monorepo assertion that the run needs nothing beyond the SDD
projects (FR-013). A real `FS.GG.SDD.Cli` process smoke (init→ship) is captured
as readiness evidence. The full suite must remain green.

**Target Platform**: Cross-platform .NET test and console executable on
Linux/macOS/Windows; the consumer docs are platform-neutral Markdown.

**Project Type**: Documentation deliverables plus an automated verification test
over the existing F# command workflow library and console executable. No new
project, no new source module.

**Performance Goals**: The in-process lifecycle smoke (init→ship + agents +
refresh) over the disposable project completes in under 10 seconds in the command
test harness on the local development machine; two determinism runs produce
byte-identical machine-readable readiness outputs.

**Constraints**: Markdown remains an authoring surface and the existing schema-
versioned structured artifacts remain the machine contract (Constitution II);
the docs and smoke must not drift from the commands' emitted canonical stage
order and next-action pointers (FR-014, enforced by smoke assertions); the
migration guide is additive and non-destructive and never removes authored
Spec Kit content (FR-008/FR-009); the smoke and docs assume no Governance gate
runtime, no FS.GG.Rendering package, no monorepo checkout, and no runtime product
templates (FR-005/FR-013); generated readiness views are framed as outputs whose
currency comes from `refresh`, not file presence (FR-015); the feature
introduces no Governance routing, freshness, profile, gate, audit, or release
behavior (FR-016); no new public F# surface, so `.fsi` signatures and public API
baselines are unchanged (the existing surface-baseline tests must still pass);
the smoke's outputs and assertions exclude implicit clocks, durations, terminal
width, ANSI styling, directory enumeration order, host path separators, random
values, and absolute host paths.

**Scale/Scope**: Three shipped consumer docs, one updated `docs/index.md` and
`README.md` cross-link, one new automated smoke test file, and CLI process smoke
readiness evidence. One disposable SDD project and one work item exercised per
smoke run.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Spec -> FSI -> Semantic Tests -> Implementation | The feature adds no new public F# surface; the only code is a semantic test that exercises existing public command surfaces through the established harness. FSI-first / `.fsi`-before-`.fs` does not apply because no signature changes; the existing public surface is the contract the smoke validates. | PASS (no new public API) |
| II. Structured Artifacts Are the Machine Contract | No new structured contract is introduced or changed. Markdown docs are authoring surfaces; the existing schema-versioned readiness views remain the machine contract the smoke asserts over. The docs frame generated views as outputs whose currency comes from `refresh`. | PASS |
| III. Visibility Lives in `.fsi`, Not in `.fs` | No public API change; `.fsi` files and public surface baselines are unchanged, and `SurfaceBaselineTests` must still pass unmodified. | PASS |
| IV. Idiomatic Simplicity Is the Default | The smoke reuses existing records, unions, modules, and the existing `TestSupport` run helpers and command workflow; no framework, reflection, process-orchestration complexity, or new abstractions are introduced. | PASS |
| V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows | The feature adds no new stateful workflow; it drives the existing MVU command workflow through the existing interpreter harness. The CLI process smoke uses the shipped executable boundary. | PASS |
| VI. Test Evidence Is Mandatory | The plan requires a real in-process lifecycle smoke over a disposable project, a real CLI process smoke captured as evidence, determinism assertions, no-Governance assertions, and a green full suite — real evidence, not synthetic. | PASS |
| VII. Agent And Human Workflows Must Share One Contract | The quickstart, migration guide, and adoption note describe the same lifecycle commands and generated views humans, Claude, Codex, CLI, and CI use; the smoke asserts the documented next-action chain so docs cannot become a second source of truth that drifts from command behavior. | PASS |
| VIII. Observability And Safe Failure | The smoke asserts the lifecycle's existing diagnostics and next-actions remain correct under bootstrap and no-Governance conditions, and the migration guide is additive/non-destructive so adopters cannot lose authored content. | PASS |

No constitution violations are present. Complexity tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/016-bootstrap-migration/
|-- plan.md
|-- research.md
|-- data-model.md
|-- contracts/
|   |-- consumer-docs.md          # quickstart + migration + adoption doc contracts
|   |-- lifecycle-smoke.md        # automated end-to-end smoke contract
|   `-- bootstrap-assertions.md   # no-Governance / determinism / FR-014 drift checks
|-- quickstart.md                 # Phase 1 validation guide for THIS feature
`-- tasks.md                      # Created by /speckit-tasks, not this command
```

### Source Code (repository root)

```text
FS.GG.SDD.sln                       # unchanged
Directory.Build.props               # unchanged
Directory.Packages.props            # unchanged

docs/
|-- quickstart.md                   # NEW: consumer init->ship walkthrough, no Governance
|-- migration-from-spec-kit.md      # NEW: additive Spec Kit -> native SDD migration guide
|-- adopting-governance.md          # NEW: optional Governance-after-init adoption note
`-- index.md                        # UPDATED: link the three new docs

README.md                           # UPDATED: link quickstart + migration in Workflow section

src/                                # UNCHANGED (no new command, schema, or .fsi surface)
|-- FS.GG.SDD.Artifacts/
|-- FS.GG.SDD.Commands/
`-- FS.GG.SDD.Cli/

tests/
`-- FS.GG.SDD.Commands.Tests/
    |-- LifecycleSmokeTests.fs      # NEW: in-process init->ship + agents + refresh smoke
    `-- TestSupport.fs              # REUSED unchanged (existing run* helpers)

specs/016-bootstrap-migration/readiness/   # CLI process smoke + full-suite + review evidence
```

**Structure Decision**: Add documentation under `docs/` and one verification test
under the existing `FS.GG.SDD.Commands.Tests` project; change no `src/` module.
The bootstrap experience is a documentation-and-verification feature over an
already complete command surface, so the correct shape is consumer docs plus an
automated smoke that pins the documented behavior to the commands' real output.
The smoke lives in the existing command test project because that project already
hosts the `TestSupport` harness with every per-stage run helper and the temp-
project machinery; reusing it keeps the smoke deterministic and avoids new
process-orchestration code (a fast in-process drive), while a separately captured
real CLI process smoke supplies executable-path evidence per Constitution VI.

## Complexity Tracking

No complexity exceptions are introduced.

## Phase 0 Research

Research is recorded in [research.md](research.md). All planning unknowns are
resolved; no clarification markers remain.

## Phase 1 Design

Design artifacts generated by this plan:

- [data-model.md](data-model.md)
- [contracts/consumer-docs.md](contracts/consumer-docs.md)
- [contracts/lifecycle-smoke.md](contracts/lifecycle-smoke.md)
- [contracts/bootstrap-assertions.md](contracts/bootstrap-assertions.md)
- [quickstart.md](quickstart.md)

## Post-Design Constitution Check

| Gate | Result |
|---|---|
| FSI-first public surface | PASS (N/A): no new public F# API; `.fsi` files and public surface baselines are unchanged and must still pass. |
| Structured machine contract | PASS: no structured contract added or changed; docs are authoring surfaces; the smoke asserts over existing schema-versioned readiness views. |
| Public API baseline | PASS: `SurfaceBaselineTests` remain unmodified and green; no baseline regeneration is planned. |
| MVU boundary | PASS: the smoke drives the existing MVU command workflow; no new stateful workflow is introduced. |
| Evidence | PASS: contracts define the in-process lifecycle smoke, the CLI process smoke evidence, no-Governance, determinism, FR-014 drift, FR-011 Governance-incomplete, and FR-013 no-Rendering assertions, plus a green full suite. |
| Agent contract | PASS: the three docs and the smoke describe and pin the one shared lifecycle contract; agent context (`CLAUDE.md` SPECKIT marker) points at this plan. |
| Safe failure | PASS: the migration guide is additive and non-destructive; the smoke confirms lifecycle diagnostics and next-actions remain correct under bootstrap and no-Governance conditions. |

No new complexity exceptions were introduced by Phase 1 design.
