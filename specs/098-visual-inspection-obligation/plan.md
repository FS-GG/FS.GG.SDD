# Implementation Plan: The Visual-Inspection Obligation

**Branch**: `item/306-render-and-look`

**Date**: 2026-07-10

**Spec**: `specs/098-visual-inspection-obligation/spec.md`

**Tracks**: FS.GG.SDD#306 (roadmap, P2). Breakout1 `FEEDBACK.md` §4.

## Summary

Add one optional boolean to `.fsgg/project.yml`; when it is set, derive one task, one obligation,
one advisory checklist row, and one blocking evidence gate. Everything else is inherited from
machinery that already exists.

The design deliberately reuses four existing mechanisms rather than adding a parallel one:

| Need | Reused mechanism |
| --- | --- |
| Declare the trigger | `project.implementSkill`'s reader shape (`Config.fs`), on the read effect `checklist`/`tasks`/`evidence` already request |
| Mint the obligation | `plannedTask` stamps `RequiredEvidence = [EV###]` on every derived task |
| "Synthetic never satisfies" | `evidenceDispositions`' existing cascade |
| Mark the obligation | `RequiredSkillOrCapabilityTags = task.RequiredSkills` — the `visual-inspection` tag rides the task into the obligation and into `verify`'s `skills[]` view |

## Technical Context

- **Language/Version**: F# on .NET 10.
- **Primary Dependencies**: none new.
- **Storage**: `.fsgg/project.yml` (authored), `work/<id>/tasks.yml`, `work/<id>/evidence.yml`.
- **Testing**: xUnit, real filesystem fixtures. Goldens for JSON contracts.
- **Project Type**: library + CLI.
- **Constraints**: no new I/O edge (FR-009); no Rendering vocabulary in `src/` (SC-005); zero churn
  in workspaces that do not declare the flag (SC-001).
- **Scale/Scope**: 4 source files, 1 skill, 3 mirrored/pinned artifacts, ~6 test files.

## Constitution Check

| Principle | Verdict | Note |
| --- | --- | --- |
| I. Spec → FSI → Tests → Impl | PASS | `Config.fsi` and `Evidence.fsi` move with their `.fs`; tests written red-first. |
| II. Structured artifacts are the machine contract | PASS | `project.visualSurface` is the authoritative flag; the checklist advisory row is prose *derived from* it, never a second source. Prose never wins: the gate reads the boolean. |
| III. Visibility lives in `.fsi` | PASS | Two additive signature entries (`ProjectLifecycleConfig.VisualSurface`, `Evidence.visualInspectionSkill`); `PublicSurface.baseline` re-captured. |
| IV. Idiomatic simplicity | PASS | A bool, a string constant, one `List.filter`. No new abstraction. |
| V. Elmish/MVU boundary | PASS | Pure `update`-side derivation over an already-requested `ReadFile` effect. No new effect, no new interpreter case. |
| VI. Test evidence is mandatory | PASS | Every FR gets a semantic test through the public surface over a real fixture; the two behavior branches (declared / not declared) are both asserted. |
| VII. Agents and humans share one contract | PASS | `fs-gg-sdd-evidence` updated once and mirrored byte-identically to `.claude`, `.codex`, `.agents`; manifest digest regenerated. |
| VIII. Observability and safe failure | PASS | A malformed flag degrades to `false` (user-input tolerance) rather than blocking every command. The new gate is a `DiagnosticError` with a correction string. |

**Change tier**: **Tier 1** — additive schema field, new diagnostic id, changed task-graph output
under a declared flag, changed agent-skill contract. Migration note required (below).

## Project Structure

### Documentation (this feature)

```
specs/098-visual-inspection-obligation/
├── spec.md
├── plan.md
└── tasks.md
```

### Source Code (repository root)

```
src/FS.GG.SDD.Artifacts/LifecycleArtifacts/
├── Config.fs / Config.fsi              # + ProjectLifecycleConfig.VisualSurface
└── Evidence.fs / Evidence.fsi          # + visualInspectionSkill constant

src/FS.GG.SDD.Commands/
├── CommandReports/DiagnosticConstructors.fs   # + missingVisualInspectionArtifact
├── CommandReports.fs / .fsi                   # re-export
└── CommandWorkflow/
    ├── TaskGraphAuthoring.fs           # + visualSurface flag, + visualInspectionTasks
    ├── ChecklistPlanAuthoring.fs       # + advisory incoherence review
    └── HandlersEvidence.fs             # + the artifact gate (validation + disposition)

.claude/skills/fs-gg-sdd-evidence/SKILL.md   # authored source (embedded resource)
.codex/skills/fs-gg-sdd-evidence/SKILL.md    # byte-identical mirror
.agents/skills/skill-manifest.json           # regenerated digest
docs/reference/authoring-contracts.md        # the flag + the gate
```

**Structure Decision**: the flag is read where the config is already read; nothing crosses an
assembly boundary that did not already. `visualInspectionSkill` lives in `Artifacts/Evidence.fs`
because both the task generator (which stamps it) and the evidence handler (which reads it back off
the obligation) must agree on the literal, and `Artifacts` is the only assembly both see.

## Design Detail

### Why a skill tag, and not a new obligation `Kind`

`EvidenceObligation.Kind` is a string set uniformly to `"taskEvidence"` by the single mint site.
Adding a second kind would fork the mint, and `Kind` is not carried on the authored declaration, so
the gate could not recover it from `evidence.yml` alone. `RequiredSkillOrCapabilityTags` is already
routed from `task.RequiredSkills` onto the obligation, is already surfaced in `verify.json`'s
`skills[]`, and is already unioned correctly when two tasks share an obligation id. The tag is
therefore the marker that costs nothing and is visible at every seam that needs it.

The gate's two call sites recover "is this obligation visual?" differently, because they have
different inputs, and both are exact:

- `evidenceDispositions` reads `obligation.RequiredSkillOrCapabilityTags`.
- `evidenceValidationDiagnostics` has no obligations, only `taskFacts`; it recovers the obligation
  ids from the tasks whose `requiredSkills` carry the tag.

### Why the derived task has no `sourceIds`

`taskValidationDiagnostics.unknownSources` gates every `sourceIds` entry against the union of ids
the lifecycle facts declare. A visual-inspection task descends from **no** `FR`/`DEC`/`PD` — that is
the entire point of the feature — so any id it invented would be rejected as unknown by the tool
that had just generated it. An empty `sourceIds` is therefore not a shortcut; it is the honest
encoding of "this obligation is not in the requirement set."

`maybeTask` dedups on the first `sourceId`, so with none it always allocates. Idempotency (SC-004)
comes from `mergeAuthoredTaskState`, which matches derived to prior tasks **by title** and re-keys
the obligation to the final task number — the same path every other derived task takes.

### Why the checklist row is advisory

`plannedChecklistReviews` re-derives every row from source on every run and never re-ingests prior
rows (082 / #146). A blocking `fail` row would be un-passable: an author who reviewed the
conjunction and flipped it to `pass` would have it overwritten on the next run and dead-end at
`plan`. The row's job is to *prompt*; the blocking job belongs to the obligation, at `evidence`.

## Verification Plan

- `Config` parse: declared `true`/`false`/absent/blank/non-boolean/missing-file → `true`/`false`/
  `false`/`false`/`false`/`false` (FR-001).
- `tasks`: declared → exactly one `Inspect a rendered frame` task with the tag and one obligation;
  undeclared → zero; two consecutive runs → identical bytes, stable `T###` (FR-002, FR-003, SC-004).
- `evidence`: pass+real+artifact → `supported`; pass+real+no artifact → blocking diagnostic +
  `invalid`; pass+synthetic → `synthetic` (unsatisfied); deferral → `deferred`, no artifact gate
  (FR-004, FR-005, FR-006).
- `checklist`: declared + ≥1 requirement → one advisory row referencing every requirement id;
  declared + 0 requirements → none; undeclared → none (FR-007).
- Skill drift guard + manifest digest (FR-008).
- Existing goldens unchanged (SC-001); `src/` grep clean (SC-005).

## Agent-facing behavior

`fs-gg-sdd-evidence` gains a "The visual-inspection obligation" section. Claude, Codex, and the
neutral `.agents` root receive byte-identical bodies from the one embedded resource, as today.

## Governance integration

None. No gate, no route, no profile vocabulary crosses into a produced artifact.

## Migration

Additive and opt-in. `.fsgg/project.yml` schema stays v1. A workspace that does not declare
`project.visualSurface` sees no change to any artifact. A workspace that declares it gains one task
and one obligation; a workspace that later un-declares it must delete the derived task's
`evidence.yml` entry, exactly as for a folded `PD-###` (#310).

## Complexity Tracking

One deliberate deviation from the issue's acceptance criteria, not from the constitution: AC1's
`profile game / sample-pack / View: 'model -> SceneNode` trigger and AC4's `Viewer.runAppEvidence`
recipe are FS.GG.Rendering vocabulary that the constitution's Engineering Constraints forbid in
generic SDD. The boolean and the product-neutral recipe satisfy the ACs' intent. Recorded in the
spec's `## Out of Scope` and on the pull request.

No constitution violations.
