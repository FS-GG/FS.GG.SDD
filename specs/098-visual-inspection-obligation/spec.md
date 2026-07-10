# Feature Specification: The Visual-Inspection Obligation

**Feature Branch**: `item/306-render-and-look`

**Created**: 2026-07-10

**Status**: Draft

**Input**: FS.GG.SDD#306 — "`implement` has no render-and-look obligation — a green suite over a
self-contradicting spec ships a visual bug." From Breakout1 (external consumer), `FEEDBACK.md` §4.

## Overview

Breakout1 shipped with 55 passing tests, ten green lifecycle stages, 50 real evidence passes, 0
synthetic, and `fsgg-sdd ship` reporting `shipReady` — while the ball was invisible behind the HUD
on every ceiling bounce. The spec put a 16px wall border at `y = 16`, an opaque 48px HUD band over
`y ∈ [0, 48]`, and drew the HUD after the playfield. Follow all three literally and the ball spends
every ceiling bounce occluded.

The physics was exactly as specified. The defect is a **specification incoherence that exists only
as pixels**, living in the *conjunction* of two requirements that no single requirement references.

SDD's obligation graph is closed over requirements: every obligation descends from an `FR-###`, a
`DEC-###`, a `PD-###`, or a contract/migration/view id. A requirements-derived gate cannot reach a
defect that is not in the requirement set. This is not a bug with a fix — it is a hole in the
method, and it is the one finding in the Breakout1 report that names a *class* of defect the rest
of the system is structurally blind to.

This feature closes the hole with the cheapest thing that could possibly work: when a workspace
declares that it has a visual surface, the task graph derives **one obligation that is discharged
by rendering a frame and looking at it**, and the checklist carries **one advisory prompt** for the
between-requirements defect class. The obligation is a first-class member of the existing
obligation graph, so it inherits the satisfaction rule for free: `result: pass ∧ synthetic: false`,
and nothing else, satisfies it.

### The generic-SDD boundary

The issue's acceptance criteria name a `game` / `sample-pack` profile, a `View: 'model -> SceneNode`
type, and the `Viewer.runAppEvidence` recipe. All three are **FS.GG.Rendering** vocabulary. The
constitution's Engineering Constraints are unambiguous: *"No repo-specific knowledge of
FS.GG.Rendering package IDs, templates, or docs URLs belongs in generic SDD code"*, and CLAUDE.md
repeats it. Those names are the consumer's *reasons* to declare a visual surface; they are not
facts generic SDD may know.

So the trigger is a **value-agnostic boolean the workspace declares** — `project.visualSurface` in
`.fsgg/project.yml` — read exactly as the existing `project.implementSkill` and
`project.testFramework` are read. A rendering product sets it because it has a `SceneNode` view; a
TUI product sets it because it draws a terminal frame; SDD never learns why. See FR-001 and
`## Out of Scope`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The obligation exists, and only a real rendered frame discharges it (Priority: P1)

A product author declares `project.visualSurface: true` and runs the lifecycle.

**Why this priority**: this is the whole feature. Without the obligation, `implement` has nothing to
discharge and the green suite still ships the visual bug.

**Acceptance**

1. **Given** `.fsgg/project.yml` declaring `project.visualSurface: true`, **When** `fsgg-sdd tasks`
   runs, **Then** `tasks.yml` carries one derived task titled `Inspect a rendered frame`, whose
   `requiredSkills` include `visual-inspection`, and whose `requiredEvidence` names one obligation.
2. **Given** the same workspace with no such declaration (absent, blank, or `false`), **When**
   `fsgg-sdd tasks` runs, **Then** no such task is derived and the graph is byte-identical to today.
3. **Given** the derived obligation, **When** the author declares it `result: pass`,
   `synthetic: false`, and names a rendered artifact (a non-empty `artifacts:` entry, or a
   `sourceRefs[]` carrying a `path` or `uri`), **Then** the obligation is `supported` and `verify`
   is green.
4. **Given** the derived obligation declared `result: pass`, `synthetic: false`, and **no** rendered
   artifact, **When** `fsgg-sdd evidence` runs, **Then** it blocks with
   `evidence.missingVisualInspectionArtifact` and the disposition is `invalid`.
4a. **Given** the obligation scaffolded under `evidence --from-tests tests/PhysicsTests.fs`, **When**
   the author flips it to `result: pass`, **Then** it still blocks — the test path was never seeded
   onto it, so it names no rendered artifact.
5. **Given** the derived obligation declared `result: pass`, `synthetic: true` with a disclosure,
   **When** `fsgg-sdd verify` runs, **Then** the disposition is `synthetic` and the obligation is
   **not** satisfied — the existing satisfaction rule, inherited unchanged.
6. **Given** the derived obligation declared an accepted deferral with all four deferral fields,
   **When** `fsgg-sdd evidence` runs, **Then** it is a first-class `deferred` disposition and the
   artifact gate does not fire.

### User Story 2 - The checklist prompts for the defect class that lives between requirements (Priority: P2)

**Why this priority**: the obligation catches the pixel; the prompt is what makes an author look for
the *conjunction* before the pixel exists. It is cheap, and it is the only place the method names
the class.

**Acceptance**

1. **Given** a workspace declaring a visual surface and a spec with at least one `FR-###`, **When**
   `fsgg-sdd checklist` runs, **Then** the Review Results section carries one **advisory**
   (non-blocking) row naming the between-requirements defect class — draw order versus geometry,
   overlapping bands, z-order versus collision bounds — and referencing every requirement id.
2. **Given** a workspace that does not declare a visual surface, **When** `fsgg-sdd checklist` runs,
   **Then** no such row appears and the checklist is byte-identical to today.
3. The row is **advisory, never blocking**: the tool cannot know whether the author looked, and the
   checklist re-derives its rows from source on every run, so a blocking row an author "passed"
   would reappear and dead-end the lifecycle. The blocking gate is the US1 obligation, at `evidence`.

### User Story 3 - The evidence skill teaches the obligation (Priority: P3)

**Why this priority**: an obligation an agent does not know how to discharge is an obligation that
gets a synthetic pass.

**Acceptance**

1. `fs-gg-sdd-evidence` documents the visual-inspection obligation: what derives it, what discharges
   it, that a synthetic pass never satisfies it, and the shape of the render-and-look recipe stated
   in **product-neutral** terms (render one representative frame through the product's own
   render-to-image entry point; record the produced image as the evidence artifact).
2. The seeded `.claude` / `.codex` / `.agents` copies stay byte-identical and the skill manifest
   digest is regenerated.

### Edge Cases

- `project.visualSurface` present but malformed (a non-boolean scalar) → reads as `false`. It is an
  optional convenience flag, not a contract; a typo must not block every command in the workspace
  (Principle VIII: distinguish user input from tool defect, and degrade optional integrations).
- `.fsgg/project.yml` absent or unparseable → `false`, matching how `implementSkill` degrades.
- Visual surface declared, spec has **zero** requirements → no checklist advisory row (there is no
  conjunction to review), but the task is still derived (the product still renders).
- The declaration flips `true → false` → the derived task has no live disposition ref and is dropped
  by the existing `mergeAuthoredTaskState` orphan rule; its `evidence.yml` entry must be removed by
  the author, exactly as for a folded `PD-###` task (#310).
- Two runs with the declaration on → the task's `T###` is stable (the merge matches on title).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `.fsgg/project.yml` MUST accept an optional boolean `project.visualSurface`. It MUST
  default to `false` when absent, blank, non-boolean, or when the config is missing/unparseable.
  Generic SDD MUST NOT encode any provider-, profile-, or type-specific value that implies it.
- **FR-002**: When `project.visualSurface` is `true`, `tasks` MUST derive exactly one additional
  task titled `Inspect a rendered frame`, carrying `requiredSkills: [visual-inspection, <implement
  skill>]`, no `sourceIds`, and the primary task dependency. When `false`, it MUST derive none.
- **FR-003**: The derived task MUST carry exactly one `requiredEvidence` obligation, minted by the
  existing per-task mechanism, so the obligation is a first-class member of the obligation graph and
  flows into `evidence.yml` scaffolding, `verify.json` dispositions, and skill visibility.
- **FR-004**: A declaration that satisfies a visual-inspection obligation (`result: pass` ∧
  `synthetic: false`) MUST name at least one rendered artifact — a non-empty `artifacts:` entry, or a
  `sourceRefs[]` entry carrying a `path` or a `uri`. Otherwise `evidence` MUST block with
  `evidence.missingVisualInspectionArtifact` (a `DiagnosticError`) and `verify` MUST record the
  disposition as `invalid`.
- **FR-004a**: `evidence --from-tests <path>` MUST NOT seed its proving-test source onto a
  visual-inspection obligation. A test file is not a rendered frame, and FR-004's check cannot tell
  them apart — seeding one would pre-satisfy the gate with the wrong kind of proof. Every other
  obligation is seeded as before.
- **FR-005**: A `synthetic: true` pass MUST NOT satisfy a visual-inspection obligation. (Inherited
  from the existing satisfaction rule; asserted, not re-implemented.)
- **FR-006**: A deferral of a visual-inspection obligation MUST remain a first-class `deferred`
  disposition, subject only to the existing four-field deferral gate. FR-004 MUST NOT fire on it.
- **FR-007**: When `project.visualSurface` is `true` and the spec declares at least one requirement,
  `checklist` MUST derive one advisory, non-blocking review row naming the between-requirements
  incoherence class and referencing every requirement id. It MUST NOT be derived otherwise.
- **FR-008**: `fs-gg-sdd-evidence` MUST document the obligation, its discharge, and the
  product-neutral render-and-look recipe. The seeded copies across all three agent-skill roots MUST
  stay byte-identical and the skill manifest digest MUST be regenerated.
- **FR-009**: No new I/O edge. `checklist`, `tasks`, and `evidence` already read `.fsgg/project.yml`
  in their read-effect frames; the flag MUST ride those reads.

### Key Entities

- **`project.visualSurface`** — an optional boolean on `ProjectLifecycleConfig`. The only new
  authored field. Schema stays v1 (additive optional).
- **`visualInspectionSkill`** — the string `"visual-inspection"`, the structural marker that makes a
  task's obligation a visual-inspection obligation. It is a skill tag, so it also becomes visible in
  `verify`'s `skills[]` view for free.
- **`evidence.missingVisualInspectionArtifact`** — the new blocking diagnostic.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A workspace with no `visualSurface` declaration produces byte-identical `tasks.yml`,
  `checklist.md`, `evidence.yml`, `analysis.json`, `verify.json`, and `ship.json` to `main`. Every
  existing golden is untouched.
- **SC-002**: A workspace declaring `visualSurface: true` and discharging the obligation with a
  synthetic pass reaches `verify` with the obligation **unsatisfied**.
- **SC-003**: The same workspace discharging with `result: pass`, `synthetic: false`, and no artifact
  path blocks at `evidence` with exit 1 and `evidence.missingVisualInspectionArtifact`.
- **SC-004**: `tasks` is idempotent under the declaration: two consecutive runs produce identical
  bytes and a stable `T###` for the derived task.
- **SC-005**: Grep of `src/` for `game`, `sample-pack`, `SceneNode`, `Viewer`, `runAppEvidence`
  returns no new hits.

## Assumptions

- The consumer decides what "has a visual surface" means. SDD reads a boolean.
- The obligation is per **work item**, not per requirement. One representative frame, looked at, is
  the unit of proof the Breakout1 report describes; N frames would be a count, not a method.

## Out of Scope

- Rendering the frame. SDD owns the obligation, never the renderer. There is no `fsgg-sdd render`.
- Validating that the named artifact **is** an image, exists on disk, or depicts anything in
  particular. `evidence` declares; `verify` validates declared pointers by the existing rules.
- Deriving the flag from a provider, a starter, a template id, or a `View:` type signature (FR-001).
- Naming `Viewer.runAppEvidence` in seeded guidance. It is FS.GG.Rendering's API; the constitution
  forbids it in generic SDD, and the skill states the recipe's *shape* instead. Recorded as a
  deliberate deviation from the issue's AC4 wording, satisfying its intent.

## Deferred

- A per-requirement or per-scenario visual obligation, and any structured "frame inventory".
- Teaching `analyze` to search for between-requirement incoherence directly. That is a real research
  problem; the advisory checklist row is the honest, cheap stand-in.
