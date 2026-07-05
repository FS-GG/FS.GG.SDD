# Feature Specification: fs-gg-sdd-evidence skill — honest partial/at-scale evidence, bulk authoring, deferrals-first-class

**Feature Branch**: `079-evidence-at-scale-guidance`

**Created**: 2026-07-05

**Status**: Draft

**Input**: User description: "Improve the fs-gg-sdd-evidence process skill (SKILL.md) to cover honest partial/at-scale evidence: how to mark real-pass vs deferral across an auto-expanded task graph (e.g. 18 authored tasks expanding to 85 obligations), that deferrals are first-class and not failures (reinforcing the result:pass ∧ synthetic:false honesty model), and a documented bulk-authoring path/pattern for large obligation sets. Source: TD1/Bulwark field-feedback §3.5/§4.1. Issue FS-GG/FS.GG.SDD#126 under epic #127."

## User Scenarios & Testing *(mandatory)*

In the TD1 *Bulwark: Tower Defense* field run — a full charter→ship SDD pass on `fsgg-sdd`
0.6.0 that ended **honest**: 77 real passes, 8 genuine deferrals, **0 synthetic** evidence —
the honesty model worked, but its at-scale *guidance* had to be reverse-engineered. `tasks`
auto-expanded 18 authored tasks into **85** (per-FR + per-plan-decision + per-contract +
keep-deferral-visible tasks), and `evidence` scaffolded **85 obligations**. The author hit two
gaps the `fs-gg-sdd-evidence` skill did not cover:

- **No at-scale classification guidance.** Across 85 obligations, how to decide — and honestly
  record — which are real passes and which are legitimate deferrals was left implicit. The skill
  documented the satisfaction rule but not how to apply it *sweepingly* over an auto-expanded
  graph, nor that leaving some obligations as deferrals is an expected, honest outcome rather
  than a partial failure.
- **No bulk-authoring path.** Filling 85 honest entries by hand is error-prone; the author wrote
  a throwaway Python transform to do it. The skill named no supported pattern for authoring a
  large obligation set, so each author reinvents one.

This feature closes those gaps by **improving the `fs-gg-sdd-evidence` process skill body**
(`SKILL.md`). The skill is one of the 16 SDD-owned `fs-gg-sdd-*` process skills seeded into
every scaffolded workspace; its canonical body lives at `.claude/skills/fs-gg-sdd-evidence/
SKILL.md`, is mirrored byte-identically to `.codex/skills/fs-gg-sdd-evidence/SKILL.md`, is
compiled into `FS.GG.SDD.Commands` as an embedded resource that `init`/`scaffold` seed, and is
pinned by a `sha256` in the process `skill-manifest.json`. The change is **documentation only**:
it adds guidance to the skill body and re-pins the three derived surfaces (the `.codex` mirror,
the embedded resource, and the manifest `sha256`). It introduces no CLI behavior, schema, field,
flag, output stream, or exit-code change.

## Clarifications

### Session 2026-07-05

- Q: Should this feature add or change any `fsgg-sdd` runtime behavior (a `--bulk` flag, a
  scaffold-time template, a new evidence field) to support bulk authoring? → A: No. The scope is
  documentation only — the skill describes a *pattern* an author applies with existing tooling
  (the already-shipped `evidence --from-tests`, `requirementRefs`/`planDecisionRefs` linkage
  from #124/feature 077, and ordinary editor/scripting affordances). No CLI, schema, or field
  change.
- Q: Which agent surfaces must the improved body reach? → A: All the surfaces the existing
  seeding already fans out to — the canonical `.claude` body and its byte-identical `.codex`
  mirror in this repo, carried transitively into scaffolded workspaces' three roots (`.claude`,
  `.codex`, `.agents`) by the unchanged seeding path. This feature edits the two in-repo copies
  and re-pins the manifest; it does not change the seeding mechanism.

### User Story 1 - An author honestly classifies an auto-expanded obligation graph (Priority: P1)

An author finishes implementation on a work item whose `tasks` expanded into dozens of
obligations. Opening the `fs-gg-sdd-evidence` skill, they find explicit guidance for the
at-scale case: how to sweep the obligation set and mark each obligation as either a **real pass**
(`result: pass`, `synthetic: false`, pointing at the actual test/proof) or a **deferral**, using
the `requirementRefs`/`planDecisionRefs` each scaffolded obligation now carries so classification
needs no join back to `tasks.yml` by title. They classify all obligations honestly and reach
`verify` without guessing at the intended workflow.

**Why this priority**: This is the core of the issue — the honesty model already works, but the
at-scale *how* was undocumented. It delivers value on its own: an author facing 85 obligations
gets a described method instead of reverse-engineering one.

**Independent Test**: Read the improved skill body and confirm it contains a step-by-step
at-scale classification workflow that (a) distinguishes real pass from deferral, (b) references
the per-obligation origin refs as the classification key, and (c) matches the shipped evidence
example and the `evidence.yml` grammar in `authoring-contracts.md`.

**Acceptance Scenarios**:

1. **Given** the improved `fs-gg-sdd-evidence/SKILL.md`, **When** an author reads it, **Then** it
   documents a workflow for classifying a large auto-expanded obligation set into real passes vs
   deferrals, explicitly tied to the `result: pass ∧ synthetic: false` satisfaction rule.
2. **Given** the improved body, **When** an author looks for how to tell obligations apart,
   **Then** it names the `requirementRefs`/`planDecisionRefs` origin refs (from feature 077) as
   the per-obligation classification key, with no `tasks.yml` title-join required.

---

### User Story 2 - Deferrals read as first-class, not as failure (Priority: P1)

An author has obligations they legitimately cannot satisfy in this cut (blocked upstream,
out-of-scope-for-now, awaiting a dependency). The skill states plainly that recording these as
deferrals is an **honest, expected, first-class outcome** — not a partial failure and not
something to disguise as a synthetic pass — and that a work item that ships with declared
deferrals is coherent. The author records the deferrals without fear that doing so "looks worse"
than a synthetic stand-in (it is strictly more honest).

**Why this priority**: The reverse-engineered lesson of TD1 was exactly this framing. Getting it
wrong pushes authors toward synthetic passes to avoid "failing," which is the dishonesty the
model exists to prevent. Ships independently of the bulk-authoring guidance.

**Independent Test**: Confirm the skill body contains an explicit "deferrals are first-class,
not failures" statement that contrasts a deferral against a synthetic pass and confirms a
shippable work item may carry deferrals.

**Acceptance Scenarios**:

1. **Given** the improved body, **When** an author reads the deferral guidance, **Then** it
   states that a deferral (`result: deferred` / `kind: deferral`) is a first-class, honest
   disposition — accepted, not a failure — and is preferable to a synthetic pass.
2. **Given** the improved body, **When** an author weighs deferral vs synthetic, **Then** the
   skill reinforces that only `result: pass ∧ synthetic: false` satisfies, so a synthetic pass
   never counts and a deferral is the honest way to leave an obligation unsatisfied.

---

### User Story 3 - A documented bulk-authoring pattern for large obligation sets (Priority: P2)

An author facing dozens of scaffolded obligations finds, in the skill, a described **bulk-authoring
pattern** for filling them honestly — instead of hand-editing each entry or inventing a one-off
script as TD1 did. The pattern uses only already-shipped affordances (`evidence --from-tests` to
pre-map obligations to a proving test file, the carried origin refs to drive per-obligation
classification, and ordinary scripted/editor edits), and is explicit that bulk authoring must
still produce honest per-obligation classifications (never a blanket `pass`).

**Why this priority**: High-value but secondary to the classification/deferral framing — an
author can classify honestly by hand once US1/US2 are documented; the bulk pattern removes the
toil that drove the throwaway transform. Depends on no new tooling.

**Independent Test**: Confirm the skill body describes a bulk-authoring path grounded in existing
tooling (notably `--from-tests`) with an explicit honesty caveat, and that it does not promise any
CLI flag or feature that `fsgg-sdd` does not already ship.

**Acceptance Scenarios**:

1. **Given** the improved body, **When** an author looks for how to fill a large obligation set,
   **Then** it documents a bulk-authoring pattern built on existing affordances (including
   `evidence --from-tests`) and warns that every obligation still needs an honest individual
   classification.
2. **Given** the improved body, **When** an author reads the bulk pattern, **Then** it references
   no unshipped CLI flag, field, or behavior — only affordances present in the current `fsgg-sdd`.

---

### User Story 4 - The improved skill stays coherent across surfaces and guards (Priority: P1)

The edited skill body remains byte-identical between its `.claude` canonical copy and its
`.codex` mirror, the embedded resource compiled into `FS.GG.SDD.Commands` matches the on-disk
body, and the process `skill-manifest.json` `sha256` for `fs-gg-sdd-evidence` is re-pinned to the
new body. All existing drift guards (agent-surface parity, skill mirror, process skill manifest)
stay green, so a scaffolded workspace seeds the improved body identically on every agent surface.

**Why this priority**: A skill edit that desyncs a mirror or leaves a stale manifest `sha256`
breaks the build and ships divergent guidance to Claude vs Codex vs neutral `.agents` runtimes.
The guards are what make the improved body trustworthy as *the* seeded contract.

**Independent Test**: After the edit, `diff` the `.claude` and `.codex` bodies (identical), and
run the drift-guard suite (agent-surface parity, skill mirror, process skill manifest) to
confirm green, including the regenerated manifest `sha256`.

**Acceptance Scenarios**:

1. **Given** the edited canonical body, **When** the `.codex` mirror is compared, **Then** the
   two files are byte-identical.
2. **Given** the edited canonical body, **When** the process skill manifest is regenerated
   (`fsgg-sdd registry skill-manifest --write`) and checked (`--check`), **Then** the
   `fs-gg-sdd-evidence` `sha256` equals `sha256sum` of the on-disk body and the check passes.
3. **Given** the edited canonical body and re-pinned manifest, **When** the full test suite runs,
   **Then** the agent-surface-drift, skill-mirror, and process-skill-manifest guards all pass and
   no unrelated golden fixture changes.

### Edge Cases

- **Frontmatter `description` field**: the skill's YAML `description` is projected into the org
  registry and help surfaces. If it is edited, it must stay a single line within the established
  length/format conventions; a body-only edit leaves it untouched. Either way the manifest
  `sha256` (computed over the whole file) is re-pinned.
- **No new lifecycle facts**: the guidance must describe only already-true behavior of
  `fsgg-sdd` (the satisfaction rule, the `kind`/`result` vocabularies, `--from-tests`, carried
  origin refs). It must not document a flag, field, or disposition that does not exist, or the
  skill would mislead rather than guide.
- **Cross-skill coherence**: the deferral/satisfaction framing must not contradict
  `fs-gg-sdd-authoring-contracts` (the full grammar + drift guard) or `fs-gg-sdd-verify`; where
  they overlap, the evidence skill links rather than restating divergently.
- **Seeding path unchanged**: this feature adds no skill and removes none, so `SeededSkills.
  skillNames`, the embedded-resource set, and the manifest's row set are unchanged — only the
  `fs-gg-sdd-evidence` body bytes and its `sha256` change.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The `fs-gg-sdd-evidence` skill body (`SKILL.md`) MUST document an at-scale
  classification workflow for an auto-expanded obligation graph — how to sweep a large obligation
  set and mark each obligation as a real pass (`result: pass`, `synthetic: false`) or a deferral,
  grounded in the load-bearing `result: pass ∧ synthetic: false` satisfaction rule.
- **FR-002**: The body MUST name the per-obligation origin refs (`requirementRefs` /
  `planDecisionRefs`, populated on scaffolded obligations by feature 077) as the classification
  key that lets an author decide each obligation's disposition from its own `evidence.yml` entry,
  without joining back to `tasks.yml` by task title.
- **FR-003**: The body MUST state explicitly that a **deferral is first-class** — an honest,
  accepted disposition, not a failure — that a shippable work item may carry declared deferrals,
  and that a deferral is preferable to a synthetic pass (which never satisfies).
- **FR-004**: The body MUST document a **bulk-authoring pattern** for large obligation sets that
  relies only on already-shipped affordances (including `evidence --from-tests` and the carried
  origin refs) and MUST caution that bulk authoring still requires an honest, individual
  classification per obligation (never a blanket `pass`).
- **FR-005**: The guidance MUST describe only behavior that `fsgg-sdd` already ships; it MUST NOT
  introduce or imply any new CLI flag, evidence field, `kind`/`result` value, output stream, or
  exit code.
- **FR-006**: The canonical body (`.claude/skills/fs-gg-sdd-evidence/SKILL.md`) and its mirror
  (`.codex/skills/fs-gg-sdd-evidence/SKILL.md`) MUST remain **byte-identical** after the edit.
- **FR-007**: The process `skill-manifest.json` `sha256` for `fs-gg-sdd-evidence` MUST be
  re-pinned to the edited body (equal to `sha256sum` of the on-disk `SKILL.md`), regenerated via
  the manifest generator (`fsgg-sdd registry skill-manifest --write`) and passing its `--check`.
- **FR-008**: All existing drift guards MUST stay green after the edit — the agent-surface-drift
  guard, the skill-mirror (`.claude`≡`.codex`) guard, and the process-skill-manifest guard — and
  the seeded-skill embedded resource MUST match the on-disk canonical body (so `init`/`scaffold`
  seed the improved guidance).
- **FR-009**: The edit MUST NOT change the skill *set* — no skill is added or removed, and
  `SeededSkills.skillNames`, the embedded-resource set, and the manifest's row set are unchanged;
  only the `fs-gg-sdd-evidence` body and its `sha256` change.
- **FR-010**: The new guidance MUST stay coherent with the sibling skills it overlaps
  (`fs-gg-sdd-authoring-contracts` for the full grammar, `fs-gg-sdd-verify` for downstream
  evaluation, `fs-gg-sdd-tasks` for where obligations originate), linking rather than restating
  divergently.

### Key Entities *(include if data involved)*

- **`fs-gg-sdd-evidence` skill body**: the authored `SKILL.md` (frontmatter + Markdown) that is
  the canonical, SDD-owned guidance for the evidence stage; the single artifact whose content
  this feature changes.
- **Canonical copy / `.codex` mirror**: the two in-repo byte-identical roots
  (`.claude/skills/...`, `.codex/skills/...`) the drift guard pins together.
- **Embedded seeded-skill resource**: the compiled-in copy of the canonical body in
  `FS.GG.SDD.Commands` (`LogicalName SeededSkill.fs-gg-sdd-evidence`) that `init`/`scaffold` seed.
- **Process skill manifest entry**: the `fs-gg-sdd-evidence` row in `.agents/skills/
  skill-manifest.json` — `scope: process`, `materializes-when: always`, and the body `sha256`
  this feature re-pins.
- **Obligation graph**: the auto-expanded `tasks`→`evidence` obligation set (the 18→85 case) the
  guidance teaches an author to classify honestly at scale.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An author facing an auto-expanded obligation graph can, using only the improved
  skill body, classify every obligation as a real pass or a deferral without reverse-engineering
  the workflow — demonstrated for the canonical TD1 18→85 case (real-pass vs deferral marking
  keyed on the carried origin refs).
- **SC-002**: The improved body explicitly frames deferrals as first-class and not failures, and
  states a shippable work item may carry deferrals — verifiable by inspection of the body.
- **SC-003**: The improved body documents a bulk-authoring pattern grounded only in already-shipped
  affordances, with an explicit honesty caveat — and cites no unshipped flag/field/behavior.
- **SC-004**: The `.claude` and `.codex` bodies are byte-identical and the process
  skill-manifest `sha256` for `fs-gg-sdd-evidence` equals `sha256sum` of the on-disk body
  (`--check` passes).
- **SC-005**: The full test suite is green — the agent-surface-drift, skill-mirror, and
  process-skill-manifest guards all pass — with no CLI behavior, schema, field, or exit-code
  change and no unrelated golden-fixture churn.

## Assumptions

- The change is **documentation only**: it edits the `fs-gg-sdd-evidence` skill body and re-pins
  the derived surfaces (mirror, embedded resource, manifest `sha256`). No `fsgg-sdd` command,
  schema, field, flag, output stream, or exit code changes (persisted schemas stay at their
  current versions).
- The canonical body is `.claude/skills/fs-gg-sdd-evidence/SKILL.md`; `.codex` is its
  byte-identical mirror; the embedded resource and the manifest `sha256` derive from it. The
  `.agents` fan-out into scaffolded workspaces is produced by the unchanged seeding path and is
  not separately edited in this repo.
- The origin-ref linkage (`requirementRefs`/`planDecisionRefs` on scaffolded obligations) and
  `evidence --from-tests` are already shipped (feature 077 / #124, merged), so the guidance
  documents existing behavior rather than requesting new behavior.
- The manifest `sha256` is a plain `sha256sum` of the on-disk `SKILL.md` (verified: the current
  manifest value equals `sha256sum` of the current body); regeneration uses `fsgg-sdd registry
  skill-manifest --write`.
- The precise wording, section placement within the body, and whether the frontmatter
  `description` is touched are refined during `/speckit-plan`; this spec fixes the contract (the
  four guidance obligations FR-001..FR-004, honesty-only FR-005, and the coherence/guard
  obligations FR-006..FR-010), not the final prose.
- Pairs with the already-merged siblings under epic #127 (evidence-obligation-refs / feature 077,
  diagnostic-remediation-pointers / feature 078); it is the last remaining child of #127.
