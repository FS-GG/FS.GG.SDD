# Feature Specification: Early-Stage Agent Guidance Bootstrap

**Feature Branch**: `049-early-stage-agent-guidance`

**Created**: 2026-06-30

**Status**: Draft

**Change Tier**: Tier 1 (contracted) — adds one authored skeleton artifact
(`.fsgg/early-stage-guidance.md`), changes the `agents`/`refresh` command-output
semantics for the missing-work-model case (severity → advisory, exit 1 → 0, new
`NextAction`), adds two advisory diagnostics, and updates both agent surfaces and
the release schema-reference. (Detailed rationale in
[plan.md](./plan.md); per constitution *Change Classification*, the tier is
declared here in the spec.)

**Input**: User description: "start the next sdd owned item on the coordination board." → Coordination board (FS-GG Projects v2 #1) next actionable SDD-owned item: **FS-GG/FS.GG.SDD#40 — [cross-repo] Early-stage agent guidance can't bootstrap (agents/refresh blocked pre-checklist)** (parent epic FS-GG/.github#74, §2.2).

## Context *(why this feature exists)*

A consumer agent drove the TestSpec tutorial end to end and reported that the one
piece of SDD-generated authoring guidance — the per-work-item `commands.md` /
`skills.md` produced by `fsgg-sdd agents` and `fsgg-sdd refresh` — is **unavailable
during exactly the stages that are hardest to author**. Both generators derive from
`readiness/<id>/work-model.json`, which is only produced once the lifecycle has
advanced (the `agents.missingWorkModel` remedy points authors at `verify`/`ship`).
So at `charter`, `specify`, `clarify`, and `checklist`, the author asking for agent
guidance hits a hard block (`agents.missingWorkModel` /
`refresh.blockedUpstreamView`) and gets nothing — a chicken-and-egg gap. This
compounds the separately-tracked gap that the static framework skills are gated off
on the SDD scaffold path (FS-GG/FS.GG.Rendering#30), leaving an agent with no
authoring help during the window it most needs it.

This feature closes that early-stage guidance gap **without** weakening the existing
invariant that the per-work-item generated views are a faithful, digest-backed
projection of the normalized work model and never a second source of truth.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Authoring help exists from an empty/early work item (Priority: P1)

An author (or their coding agent) starting a brand-new work item — before any work
model can exist — can obtain authoritative, self-contained guidance covering the
pre-work-model lifecycle stages: which `fsgg-sdd` command runs at each stage
(`charter` → `specify` → `clarify` → `checklist`), the required section headings each
stage's artifact must contain, the stable-id formats those artifacts use, and the two
load-bearing authoring contracts that previously forced decompilation — the §1.1
coverage line and the §1.2 evidence rule.

**Why this priority**: This is the core of the reported gap. Without it the platform's
thesis ("shrink the agent's surface to the genuinely novel part") fails at precisely
the early stages, and the value of every later generator is gated behind a stage an
agent cannot reliably reach unaided. Delivering only this story already removes the
chicken-and-egg dead end.

**Independent Test**: From a freshly-initialized SDD skeleton with no readiness work
model, an author can retrieve the early-stage authoring guidance and follow it to
produce a `charter` artifact that passes the next lifecycle command — with no
decompilation and no dependence on a built work model.

**Acceptance Scenarios**:

1. **Given** an SDD work item at the `charter` stage with no
   `readiness/<id>/work-model.json`, **When** the author requests agent/authoring
   guidance, **Then** they receive guidance that names the per-stage commands,
   required section headings, and stable-id formats for `charter` through `checklist`,
   **and** the guidance is not an error/blocked result.
2. **Given** the early-stage guidance, **When** an author reads the authoring-contract
   section, **Then** it states the §1.1 coverage-line rule and the §1.2 evidence rule
   explicitly, consistent with the now-published authoring contracts (FS-GG/FS.GG.SDD#38),
   with no reference resolving to a path or artifact that does not exist.

### User Story 2 - `agents` / `refresh` stop being an early-stage dead end (Priority: P2)

An author who runs `fsgg-sdd agents` or `fsgg-sdd refresh` before the work model is
buildable no longer hits a bare hard block. Instead the command produces a useful,
clearly-bounded result: best-effort early-stage guidance derived from whatever
lifecycle artifacts already exist, plus an actionable pointer to the static
early-stage authoring guidance — never silently presenting partial output as complete.

**Why this priority**: The reported friction is specifically that these two commands
fail in the early window. Even if Story 1 ships the guidance, an agent's natural
move is to run `agents`/`refresh`; those entry points must lead somewhere useful
rather than to `agents.missingWorkModel` / `refresh.blockedUpstreamView` with no path
forward.

**Independent Test**: Run `fsgg-sdd agents` (and `fsgg-sdd refresh`) against a work
item that has only early-stage artifacts; confirm the command exits with an
actionable, non-dead-end result that routes the author to early-stage guidance, and
that any partial guidance it emits is explicitly marked as early-stage / incomplete.

**Acceptance Scenarios**:

1. **Given** a work item with early-stage artifacts but no buildable work model,
   **When** the author runs `fsgg-sdd agents`, **Then** the command result is
   actionable (it either emits clearly-labeled best-effort early-stage guidance or
   points to the static early-stage authoring guidance) rather than only reporting
   `agents.missingWorkModel` with no usable next step.
2. **Given** the same work item, **When** the author runs `fsgg-sdd refresh`, **Then**
   the refresh result reports the early-stage situation as a recognized, navigable
   state rather than only `refresh.blockedUpstreamView`.
3. **Given** any early-stage guidance the commands emit, **When** the author or a
   downstream consumer reads it, **Then** it is unambiguously distinguished from the
   full work-model-derived guidance (clearly labeled partial/early-stage) so an
   incomplete result is never mistaken for complete.

### User Story 3 - Early guidance is deterministic and self-consistent (Priority: P3)

The early-stage guidance is deterministic, lifecycle-generic, and free of dangling
references, so it can be trusted and (if shipped as part of the skeleton) regenerated
or re-seeded without clobbering author edits.

**Why this priority**: Guidance that drifts, varies run-to-run, or points at
artifacts that were never produced would reproduce the original "dangling skill
reference" failure mode and erode trust. It is P3 because it hardens Stories 1–2
rather than delivering the capability itself.

**Independent Test**: Produce the early-stage guidance twice under identical inputs
and confirm byte-identical output; scan every reference it makes and confirm each
resolves to an artifact/command/heading that genuinely exists in the SDD contract.

**Acceptance Scenarios**:

1. **Given** identical inputs, **When** the early-stage guidance is produced twice,
   **Then** the two outputs are byte-identical (deterministic).
2. **Given** the early-stage guidance, **When** every command, path, heading, and
   stable-id format it references is checked, **Then** all references resolve and none
   dangle.
3. **Given** a skeleton that already carries author-touched early-stage guidance,
   **When** the seeding/regeneration step runs again, **Then** authored content is not
   clobbered (no-clobber, consistent with the skeleton constitution / `CLAUDE.md`
   policy) — *applies only if the guidance is delivered as a seeded skeleton file.*

### Edge Cases

- **Empty work item (charter only):** guidance must still be obtainable with zero
  prior artifacts — the worst-case early window.
- **Partially-authored stage:** when some but not all early artifacts exist, best-effort
  guidance must reflect what exists without fabricating facts about what does not.
- **Work model now exists:** once the lifecycle reaches the stage where the work model
  is buildable, the existing full work-model-derived guidance remains the source of
  truth; early-stage guidance must not shadow, duplicate, or contradict it.
- **SDD vs. spec-kit lifecycle paths:** early-stage guidance must be available on the
  recommended `lifecycle=sdd` scaffold path, not only the `spec-kit` path (the gap the
  framework-skills issue exposed).
- **Claude and Codex parity:** any early-stage guidance surfaced per agent target must
  describe aligned behavior across Claude and Codex (no behavior divergence).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST make authoring guidance for the pre-work-model lifecycle
  stages (`charter`, `specify`, `clarify`, `checklist`) available to an author/agent
  working a work item that has no `readiness/<id>/work-model.json`.
- **FR-002**: The early-stage guidance MUST cover, for each pre-work-model stage, the
  `fsgg-sdd` command to run, the required section headings the stage's artifact must
  contain, and the stable-id formats those artifacts use.
- **FR-003**: The early-stage guidance MUST state the §1.1 acceptance coverage-line
  rule and the §1.2 `evidence.yml` rule explicitly, consistent with the published
  authoring contracts
  (FS-GG/FS.GG.SDD#38), so authors never need to decompile to recover them.
- **FR-004**: `fsgg-sdd agents` MUST NOT leave an author at a pre-work-model stage with
  only a non-actionable block; it MUST instead emit clearly-labeled best-effort
  early-stage guidance from existing artifacts (FR-010b) and route the author to the
  static early-stage guidance (FR-010a).
- **FR-005**: `fsgg-sdd refresh` MUST treat the pre-work-model early-stage situation as
  a recognized, navigable state with an actionable next step, rather than only emitting
  `refresh.blockedUpstreamView`.
- **FR-006**: Any best-effort/partial early-stage guidance the system emits MUST be
  unambiguously labeled as early-stage/incomplete so it is never mistaken for the full
  work-model-derived guidance (an incomplete result is never reported as complete).
- **FR-007**: The early-stage guidance MUST be deterministic — identical inputs produce
  byte-identical output — and MUST contain no dangling references (every command, path,
  heading, and stable-id format it names resolves to something that exists).
- **FR-008**: The feature MUST NOT weaken the existing invariant that the per-work-item
  generated `agents` views are a faithful, digest-backed projection of the normalized
  work model and not a second source of truth; once the work model is buildable, the
  existing generated guidance remains authoritative and early-stage guidance MUST NOT
  shadow or contradict it.
- **FR-009**: Early-stage guidance MUST be available on the recommended `lifecycle=sdd`
  path (not only the `spec-kit` path) and MUST describe aligned Claude/Codex behavior
  (no agent-target behavior divergence).
- **FR-010**: The system MUST deliver early-stage guidance through **both** channels:
  (a) static, lifecycle-generic authoring guidance available from stage zero (with no
  work item or only a `charter`), and (b) generated best-effort *partial* guidance
  emitted by `fsgg-sdd agents` / `fsgg-sdd refresh` from whatever early lifecycle
  artifacts already exist for the work item.
- **FR-011**: The generated best-effort partial guidance (FR-010b) MUST be explicitly
  labeled early-stage/partial (refining the general FR-006 labeling obligation with the
  digest-stamping constraint below) and MUST be derived only from artifacts that actually
  exist — it MUST NOT fabricate facts about absent artifacts, and MUST NOT be marked or
  digest-stamped as if it were the full work-model-derived projection (preserving the
  FR-008 invariant that the full generated views are the sole work-model source of
  truth once the model is buildable).

### Key Entities *(include if feature involves data)*

- **Early-stage authoring guidance**: A lifecycle-generic, deterministic body of
  guidance covering the pre-work-model stages — per-stage commands, required headings,
  stable-id formats, and the §1.1/§1.2 authoring contracts. Independent of any
  particular work item's work model.
- **Per-work-item generated guidance** (existing): The work-model-derived
  `commands.md` / `skills.md` under `readiness/<id>/agent-commands/<target>/`. Remains
  the source of truth once the work model is buildable; this feature must not
  compromise it.
- **Early-stage command result**: The actionable, clearly-bounded `agents` / `refresh`
  outcome at a pre-work-model stage, replacing the current bare block.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From a freshly-initialized SDD skeleton with no work model, an author can
  obtain early-stage authoring guidance for all four pre-work-model stages in a single
  step, with zero decompilation required.
- **SC-002**: Running `fsgg-sdd agents` or `fsgg-sdd refresh` at a pre-work-model stage
  yields an actionable next step in 100% of cases (no run ends at a non-actionable
  block).
- **SC-003**: 100% of references in the early-stage guidance resolve (zero dangling
  commands, paths, headings, or stable-id formats).
- **SC-004**: The early-stage guidance is byte-identical across repeated production
  under identical inputs (deterministic).
- **SC-005**: The two authoring contracts that previously forced decompilation (§1.1
  acceptance coverage line, §1.2 `evidence.yml` rule) are recoverable by an author
  entirely from the
  early-stage guidance, without reading source or decompiled output.
- **SC-006**: No regression to existing work-model-derived guidance: once the work
  model is buildable, the generated `agents` views remain byte-identical to their
  pre-feature output for the same work model (early-stage guidance does not alter the
  full-guidance contract).

## Assumptions

- The next SDD-owned item on the Coordination board is FS-GG/FS.GG.SDD#40; #44 is
  `Blocked` and every other SDD item is `Done`, so #40 is the unambiguous next start.
- The §1.1 coverage-line rule and §1.2 evidence rule referenced here are the authoring
  contracts published under FS-GG/FS.GG.SDD#38; this feature consumes them as the
  authoritative source and does not redefine them.
- "Pre-work-model stages" are `charter`, `specify`, `clarify`, and `checklist` — the
  stages before `readiness/<id>/work-model.json` is buildable (the model is produced
  on the `verify`/`ship` path per the current `agents.missingWorkModel` remedy).
- Early-stage guidance is lifecycle-generic and deterministic (same discipline as the
  authored skeleton constitution); if it is delivered as a seeded skeleton file it
  follows the established no-clobber-on-re-run policy.
- The framework product-skills gating on the `lifecycle=sdd` scaffold path
  (FS-GG/FS.GG.Rendering#30) is tracked separately; this feature targets SDD-owned
  authoring guidance and does not depend on that fix landing first.
- This is generic SDD behavior: no rendering-specific package names, templates, paths,
  or docs URLs are introduced (per the project boundary in `CLAUDE.md`).
