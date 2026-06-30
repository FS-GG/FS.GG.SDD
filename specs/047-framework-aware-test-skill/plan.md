# Implementation Plan: Framework-aware required test skill

**Branch**: `047-framework-aware-test-skill` | **Date**: 2026-06-30 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/047-framework-aware-test-skill/spec.md`

## Summary

SDD's task generator hard-codes `xunit` as the required test skill on every
generated verification-obligation task (`ParsingTasks.fs:244`,
`[ "xunit"; "readiness-evidence" ]`). For any product whose test project uses a
different framework (the `rendering` scaffold uses Expecto, per
FS-GG/FS.GG.SDD#42) this misdirects the author and mis-keys the
`evidence.missingRequiredSkill` obligation.

The fix makes the required test skill **framework-aware** while keeping SDD
generic:

1. Add an optional, additive `project.testFramework` scalar to the SDD-owned
   `.fsgg/project.yml` schema (still `schemaVersion: 1`), parsed onto
   `ProjectLifecycleConfig.TestFramework: string option`.
2. Add a pure resolver `resolveTestSkill : string option -> string`:
   declared framework → normalized token (e.g. `expecto`); absent/blank →
   neutral token `automated-tests`. SDD keeps no closed list of "approved"
   frameworks; unrecognized declared values are trusted and normalized.
3. Thread the already-loaded project-config snapshot from `computeTasksPlan`
   into `tasksDiagnosticsTextAndSummary` → `plannedTasks`, so `obligationTasks`
   emits `[ resolveTestSkill declared; "readiness-evidence" ]` instead of the
   literal `xunit`. No other task category changes; no new I/O edge is added.

The verify side needs no logic change: `evidence.missingRequiredSkill` is keyed
by each task's `requiredSkills` string and satisfied via task-linked evidence
dispositions, so emitting the new token automatically re-keys the obligation
(FR-008). `init`'s authored `project.yml` is **not** changed — it declares no
framework, so by-default products get the neutral skill, and no
rendering/provider specifics enter generic SDD (FR-007).

**Change tier**: Tier 1 (contracted) — adds a config schema field, changes
generated work-model/tasks content, and updates golden fixtures.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (per constitution Engineering Constraints).

**Primary Dependencies**: Existing in-repo only — `FS.GG.SDD.Artifacts`
(lifecycle artifacts, work model, schema versioning) and `FS.GG.SDD.Commands`
(command workflow / MVU handlers). YAML parsing via the existing
`Internal.fs` helpers. No new packages.

**Storage**: Files only — `.fsgg/project.yml` (authored config) and generated
`readiness/<id>/work-model.json` + tasks artifact.

**Testing**: xUnit 2.9.3 with hand-written `Assert.*`; inline triple-quoted
golden strings plus on-disk fixture trees under `tests/fixtures/**`. No
snapshot/approval library.

**Target Platform**: Cross-platform CLI (`fsgg-sdd`).

**Project Type**: Single project — CLI lifecycle product (library + CLI).

**Performance Goals**: N/A (deterministic, offline generation; not a hot path).

**Constraints**: Deterministic, byte-stable generated output (FR-006); no
provider/rendering/template-specific identity in generic SDD (FR-007); JSON
contract bytes unchanged beyond the intended skill value (FR-009).

**Scale/Scope**: Surgical — one config field, one pure resolver, one threading
change at the task-generation seam, plus fixtures/tests.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation**: Public surface change is
  the additive `TestFramework: string option` field on `ProjectLifecycleConfig`
  (update `Config.fsi`). Tests precede the `.fs` change (parser test + generation
  tests fail on `xunit` before, pass on the new token after). **PASS**.
- **II. Structured Artifacts Are the Machine Contract**: The authoritative source
  for the test skill is the structured `project.testFramework`; prose never
  overrides it. The generated work-model/tasks JSON is the machine contract and
  is updated as goldens. **PASS**.
- **III. Visibility Lives in `.fsi`**: `Config.fsi` updated for the new field;
  `ParsingTasks` is `module internal` with no `.fsi` and exposes nothing public,
  so the resolver stays internal. Public-surface baseline
  (`PublicSurface.baseline`) re-checked — no new public symbols expected. **PASS**.
- **IV. Idiomatic Simplicity**: One pure function over `string option`, plain
  normalization (trim/lower/slugify); no clever abstractions. **PASS**.
- **V. Elmish/MVU Boundary**: No new I/O. The `.fsgg/project.yml` read effect
  already exists in `tasksReadEffects`; the resolver is a pure transition over
  the existing snapshot. **PASS**.
- **VI. Test Evidence Mandatory**: New failing-first tests for declared,
  undeclared, and custom-framework generation, determinism, non-test-category
  invariance, and the re-keyed verify obligation; real fixture trees over mocks.
  **PASS**.
- **VII. Agent + Human Share One Contract**: Skill value flows through the same
  `requiredSkills` field consumed by CLI, agents, and verify; no second source.
  **PASS**.
- **VIII. Observability And Safe Failure**: Undeclared/blank framework degrades
  explicitly to the neutral skill (never `xunit`); malformed `project.yml` keeps
  its existing schema diagnostics. **PASS**.

Engineering-constraint checks: stays `net10.0`, package namespace unchanged, no
FS.GG.Rendering package IDs/templates/docs URLs introduced (FR-007), SDD remains
useful without Governance. **No violations → Complexity Tracking omitted.**

## Project Structure

### Documentation (this feature)

```text
specs/047-framework-aware-test-skill/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── project-config-test-framework.md
│   └── verification-obligation-skill.md
├── checklists/          # (pre-existing)
├── spec.md
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Artifacts/
└── LifecycleArtifacts/
    ├── Config.fs                 # parseProjectConfig: read optional project.testFramework
    └── Config.fsi                # add TestFramework: string option to ProjectLifecycleConfig

src/FS.GG.SDD.Commands/
└── CommandWorkflow/
    ├── ParsingTasks.fs           # add resolveTestSkill + neutralTestSkill; obligationTasks
    │                             #   uses [ resolvedTestSkill; "readiness-evidence" ];
    │                             #   thread declared framework into plannedTasks /
    │                             #   tasksDiagnosticsTextAndSummary
    └── HandlersEarly.fs          # computeTasksPlan: extract TestFramework from the parsed
                                  #   project snapshot and pass the resolved skill downward

tests/FS.GG.SDD.Commands.Tests/
└── TasksCommandTests.fs          # generation assertions: declared→expecto, none→automated-tests,
                                  #   custom→derived; no xunit token; non-test categories unchanged;
                                  #   determinism (byte-identical re-run)

tests/FS.GG.SDD.Artifacts.Tests/
├── TasksArtifactTests.fs         # replace stray `requiredSkills: [xunit]` parser-input fixture
└── VerificationViewTests.fs      # replace stray `"skill": "xunit"` parser-input fixture

tests/fixtures/**                 # add/adjust a fixture project declaring testFramework: expecto
                                  #   and confirm work-model.json goldens carry the new token
```

**Structure Decision**: Single-project layout (existing). The change spans the
`FS.GG.SDD.Artifacts` config schema and the `FS.GG.SDD.Commands` task-generation
workflow — the two real directories above. No new project or module is created;
the resolver lives inside the existing internal `ParsingTasks` module.

## Phase 0 — Research

See [research.md](./research.md). All NEEDS CLARIFICATION resolved:

- Config field location and name: **`project.testFramework`** (user-confirmed),
  optional scalar, additive, `schemaVersion` stays `1`.
- Neutral token: **`automated-tests`** (user-confirmed).
- Skill derivation for declared frameworks: normalize (trim → lowercase →
  slugify whitespace); trust unrecognized values; no closed framework list.
- Migration posture: additive optional field, no migration; absent ⇒ neutral;
  already-emitted artifacts are not rewritten outside a generation run.

## Phase 1 — Design & Contracts

- [data-model.md](./data-model.md) — `ProjectLifecycleConfig.TestFramework`,
  the resolver contract, and the verification-obligation task shape.
- [contracts/project-config-test-framework.md](./contracts/project-config-test-framework.md)
  — the `.fsgg/project.yml` v1 additive field.
- [contracts/verification-obligation-skill.md](./contracts/verification-obligation-skill.md)
  — the generated `requiredSkills` resolution contract.
- [quickstart.md](./quickstart.md) — runnable validation scenarios proving US1–US3.
- Agent context: `CLAUDE.md` SPECKIT marker repointed to this plan.

**Post-design Constitution re-check**: still PASS — design adds no new public
symbols beyond the additive config field, no new I/O edge, and no provider
specifics.
