# Implementation Plan: Split `LifecycleArtifacts.fs` per artifact family

**Branch**: `022-split-lifecycle-artifacts` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/022-split-lifecycle-artifacts/spec.md`

## Summary

Reorganize the 3,161-line single-module `LifecycleArtifacts.fs` (and its 722-line
`.fsi`) into one source file per artifact family under a `LifecycleArtifacts/`
folder, plus a small shared core. Implements roadmap item **R3** from
`docs/reports/2026-06-26-074428-refactor-analysis.md`.

**Stakeholder decisions captured during planning (these override the spec's
original FR-002/FR-003/FR-005 wording):**

1. There are **no external consumers yet**, so preserving the literal
   `LifecycleArtifacts.<member>` qualified name and the exact `.fsi` shape is
   **not** required. In-repo consumers and tests **may be edited mechanically**.
2. **Byte-identical** artifact output is **not** separately required. The single
   binding behavioral gate is: **the existing test suite passes** (build green +
   all tests green). Whatever the tests assert is the contract.

These decisions resolve a hard F# constraint discovered during planning (see
[research.md](./research.md)): a true per-family split and zero-consumer-edit
contract preservation are mutually exclusive in F#, because `open`-ing a
re-export facade cannot bring record labels / DU cases back into scope. With the
relaxations above, we take the clean approach: **each family becomes its own
`[<AutoOpen>]` module under namespace `FS.GG.SDD.Artifacts`, the monolithic
`LifecycleArtifacts` module is retired, and consumers are updated to
`open FS.GG.SDD.Artifacts`.**

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (.NET SDK 10.0.301 per baseline)

**Primary Dependencies**: FSharp.Core, YamlDotNet, `System.Text.Json` (BCL)

**Storage**: Filesystem lifecycle artifacts (Markdown authoring surface +
JSON/YAML structured contracts). No DB.

**Testing**: Existing solution test suite — `FS.GG.SDD.Artifacts.Tests`,
`FS.GG.SDD.Commands.Tests`, `FS.GG.SDD.Cli.Tests`, `FS.GG.SDD.Validation.Tests`
(437 tests at baseline). `dotnet test` is the regression harness.

**Target Platform**: Linux/cross-platform CLI library.

**Project Type**: Single F# class-library refactor inside an existing multi-project
solution. No new project.

**Performance Goals**: None changed. Pure structural move; no hot-path edits.

**Constraints**:
- F# forbids one named module spanning multiple files (the core constraint).
- No new logic duplication (FR-006): shared helpers relocate, never copy.
- Compiler warnings must be relocated, not fixed/added/suppressed (FR-008);
  FS3261/FS0025 are R4/R5 scope.
- The build's declared compile order must keep forward references resolvable
  (FR-007).

**Scale/Scope**: ~3,161 `.fs` + 722 `.fsi` lines, ~11 artifact families, ~25
public parse entrypoints, ~26 explicit qualified reference sites + 18
`open`-site files to update.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Spec → FSI → Tests → Impl | PASS | Pure refactor; each new public module gets its `.fsi` authored before/with its `.fs`. Behavior is pinned by the existing test suite (the regression guard), not new tests. |
| II. Structured artifacts are the machine contract | PASS | No schema, field, or generated-view change. Output behavior is whatever the existing tests assert. |
| III. Visibility lives in `.fsi` | PASS *(with documented exception)* | **Central task**: every new public family module gets a corresponding `.fsi`. The one `Internal` helper module is `module internal` with no `.fsi`. See **Principle III exception** below — Principle III's "modifiers not used as visibility policy" clause is read as governing visibility *within otherwise-public* modules; a genuinely non-public helper module carries no public surface to declare. Surface baselines **are** regenerated (see Tier note + tasks.md baseline task). |
| IV. Idiomatic simplicity | PASS | Only plain modules + `[<AutoOpen>]`. No custom operators, SRTP, reflection, or CEs introduced. |
| V. Elmish/MVU boundary | N/A | These are pure parsers and data models — explicitly exempt ("Simple pure parsers, data models, and validators do not need MVU ceremony"). |
| VI. Test evidence mandatory | PASS (refactor posture) | No behavior change ⇒ no new failing-then-passing test. The existing 437-test suite is the evidence; "fail before / pass after" does not apply to a no-op-behavior refactor. Documented as the verification plan. |
| VII. Agent & human one contract | N/A | No agent-surface or workflow change. |
| VIII. Observability & safe failure | PASS | Diagnostics relocate unchanged. |

**Change Classification**: **Tier 2 (internal change)** — implementation cleanup
with no user-visible or tool-visible contract change (no external consumers; output
behavior preserved as asserted by tests). The `.fsi`/module-name reshape is purely
internal because no external package depends on it.

**Tier-boundary note (analysis finding C2).** The constitution's Tier 2 definition
says "signatures and baselines remain unchanged," and this refactor *does* change
the F# module qualifiers and therefore the `FS.GG.SDD.Artifacts.Tests`
public-surface baseline. We retain **Tier 2** deliberately: the taxonomy's intent
is contract *blast radius*, and with **zero external consumers** the reshape is
invisible outside this repo. The in-repo signature/baseline change is treated as a
mechanical consequence of the move (the aggregate member set is preserved — FR-002
amended), not a contracted-surface change. The public-surface baseline is
regenerated as part of the work (see tasks.md Phase 4 baseline task); this is
called out explicitly rather than left as a silent Tier-2 assumption.

**Principle III exception (analysis finding C1).** Constitution III states
top-level `internal` modifiers "are not used as visibility policy." The single
`LifecycleArtifacts.Internal` helper module is declared `module internal` with no
`.fsi`. We read III's clause as forbidding modifier-based visibility carving
*inside otherwise-public modules* (so public surface is declared in `.fsi`, not
hidden in `.fs`), not as forbidding a wholly non-public helper module that has no
public surface to declare. Every **public** family module still gets its `.fsi`.
This narrow reading is recorded here so the choice is explicit, not silent; if the
constitution's authors intend the literal blanket reading, the resolution is a
PATCH-level constitution clarification, tracked outside this feature.

No constitution violations (with the two documented readings above) ⇒ Complexity
Tracking left empty.

## Project Structure

### Documentation (this feature)

```text
specs/022-split-lifecycle-artifacts/
├── plan.md              # This file
├── research.md          # Phase 0 — F# split-mechanism decision (empirically grounded)
├── data-model.md        # Phase 1 — family decomposition, shared core, compile order
├── contracts/
│   └── module-decomposition.md   # Phase 1 — per-module public surface map
├── quickstart.md        # Phase 1 — how to verify the refactor
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source Code (repository root)

Refactor is confined to `src/FS.GG.SDD.Artifacts/`, with mechanical `open`/
qualifier updates in dependent files.

```text
src/FS.GG.SDD.Artifacts/
├── Identifiers.fs(i)            # unchanged (earlier in compile order)
├── SchemaVersion.fs(i)         # unchanged
├── ArtifactRef.fs(i)           # unchanged
├── Diagnostics.fs(i)           # unchanged
├── GenerationManifest.fs(i)    # unchanged
├── LifecycleArtifacts/         # NEW folder replacing LifecycleArtifacts.fs(i)
│   ├── Internal.fs             # module internal — shared YAML/JSON/Markdown/util helpers (no .fsi)
│   ├── Core.fs(i)              # FileSnapshot, LifecycleArtifactContract, cross-family shared types
│   ├── Config.fs(i)           # Project / Sdd / Agents config family
│   ├── WorkItemMetadata.fs(i) # metadata + front-matter family
│   ├── Specification.fs(i)
│   ├── Clarification.fs(i)
│   ├── Checklist.fs(i)
│   ├── Plan.fs(i)
│   ├── RequirementModel.fs(i) # Requirement/Decision/MarkdownRequirementMention
│   ├── Task.fs(i)
│   ├── Analysis.fs(i)
│   ├── Evidence.fs(i)
│   ├── Verify.fs(i)           # incl. EvidenceDisposition/Test/Skill disposition types
│   ├── Ship.fs(i)
│   ├── Guidance.fs(i)
│   └── WorkItem.fs(i)         # ParsedWorkItem + loadWorkItemFromSnapshots (aggregator, LAST)
├── LifecycleRuleContracts.fs(i) # unchanged (after the new files)
├── WorkModel.fs(i)              # consumer — update opens/qualifiers
├── GovernanceHandoff.fs(i)      # unchanged unless it references moved types
├── ReleaseContract.fs(i)        # unchanged
└── Serialization.fs(i)          # consumer — update opens/qualifiers
```

Dependent files outside this folder that need mechanical `open`/qualifier edits
(no logic change): `Serialization.fs(i)`, `WorkModel.fs(i)` in Artifacts;
`CommandTypes.fs(i)`, `CommandEffects.fs`, `CommandWorkflow.fs` in Commands; and
the Artifacts/Commands test files that `open ...LifecycleArtifacts` or reference
`LifecycleArtifacts.<member>` (≈ the 18 `open`-site files + the test files with
explicit qualifiers).

**Structure Decision**: Single existing project, internal reorganization only.
Each artifact family is a self-contained `[<AutoOpen>]` module in namespace
`FS.GG.SDD.Artifacts`, ordered after a shared `Internal` helper module and a
shared `Core` types module, with the `WorkItem` aggregator last. The fsproj
`<Compile Include>` list is the compile-order manifest and is updated to the
dependency-respecting order in [data-model.md](./data-model.md).

## Complexity Tracking

No constitution violations — section intentionally empty.
