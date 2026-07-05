# Feature Specification: Idempotent Generated Gate Artifacts

**Feature Branch**: `082-idempotent-gate-artifacts`

**Created**: 2026-07-05

**Status**: Draft

**Input**: FS.GG framework development-feedback report — *Hollow Depths* build (`001-hollow-depths`), 2026-07-05. Epic FS.GG.SDD#145 (children #146 §2.2b, #147 §2.2d).

## Overview

The FS.GG SDD lifecycle generates two **gate artifacts** that authors then read and
partially author against: `work/<id>/checklist.md` (requirements-quality review with
FR→AC coverage) and `work/<id>/tasks.yml` (the typed task graph). Today both files mix
**tool-injected content** (rows the CLI writes — e.g. a `CHK-### blocking: … missing
acceptance coverage` item, or a derived task row) with **authored content** (the authored
prose sections of `checklist.md`, and per-task state the author advances such as
`Done`/`Skipped`/`owner`). Both files are also written and re-read as authored input.

Because the tool cannot tell its own injected content apart from the author's, a re-run
**re-ingests the tool's prior output as if the author had written it**. Two concrete
failures result:

1. **Checklist never clears (#146).** When an FR lacks acceptance coverage, `checklist`
   writes a `CHK-###` blocking row into `checklist.md`. On a re-run where the recorded
   source snapshot still matches (the "not stale" path), the tool **re-parses its own
   prior `CHK-###` rows as authored input** — it dedups on their `SourceId`s (suppressing
   re-derivation) and preserves the rows verbatim, so a blocking row with no basis in the
   current sources is re-counted as blocking and never clears. The only escape found was
   an undocumented `rm work/<id>/checklist.md`.

2. **Tasks won't regenerate (#147).** After editing `plan.md` to add a decision
   disposition (to clear `analyze`'s `missingDisposition` for `DEC-002`), re-running
   `tasks` reported `tasks.yml` **stale** but preserved the old rows verbatim and never
   recomputed the graph from the changed plan. The new disposition never appeared. The
   only escape found was an undocumented `rm work/<id>/tasks.yml`.

A generated gate that poisons its own re-run, gives no guidance to recover, and can only
be unstuck by an inferred `rm`, is a footgun. This feature defines and enforces
**regeneration semantics** so that re-running a gate stage after upstream edits either
regenerates cleanly or blocks with an explicit, actionable delete-and-regenerate
instruction — never leaves the author silently stale.

## Clarifications

### Session 2026-07-05

- Q: When `tasks` is re-run after an upstream source change, what is the default
  behavior? → A: **Regenerate in place** — recompute the graph from current sources every
  run, preserving authored task status; never report stale-and-unchanged. Blocking is
  reserved for the existing `unsafe-overwrite` sentinel (FR-009).
- Q: Which author-owned state in `tasks.yml` must survive regeneration? → A: **Task status
  advances only** (`Done`/`Skipped`/`InProgress`); everything else (rows, dependencies,
  requirement/decision refs, required skills/evidence) is re-derived from sources.
- Q: If an author hand-edits a row the tool owns (a generated `CHK-###` or task row), what
  does regeneration do? → A: **Reclaim (overwrite)** — the tool owns generated rows and
  overwrites the edit with the freshly derived version; authors edit authored lines, not
  generated rows.
- Q (reopened during implementation): The example corpus proved `tasks.yml` also holds
  **hand-authored tasks** and hand-added disposition tags (e.g. `decisions: [DEC-001]`) that
  the derivation cannot reproduce — should re-derivation drop them? → A: **Preserve live
  authored tags.** The merge keeps a prior task's still-live authored disposition refs, and
  keeps a whole authored task when it uniquely covers a live disposition the derived graph
  misses; only refs/tasks whose sources are gone (or already covered by derivation) are
  dropped. This supersedes the narrower "status/owner only" answer above for `tasks.yml`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Fixing coverage clears the checklist on re-run (Priority: P1)

An author runs `fsgg-sdd checklist`, sees a `CHK-###` blocking row for a requirement that
lacks acceptance coverage, resolves the coverage (the `(covers AC-###)` declaration lives
in `spec.md`'s requirement reference), and re-runs `fsgg-sdd checklist`. The now-resolved
blocking row clears on that single re-run, without the author deleting any file — because
the tool re-derives its review rows from the current sources rather than re-ingesting its
own prior output.

**Why this priority**: This is defect #146 and the most common footgun — a tool-injected
blocking row that outlives the condition that produced it, because the "not stale" re-run
path re-parses the row as authored input and re-counts it. Fixing it removes the
undocumented `rm` from the everyday path.

**Independent Test**: On a checklist whose recorded source snapshot matches the current
sources (the "not stale" path), where the file contains a tool-injected `CHK-###` blocking
row for a requirement that the current `spec.md` *does* cover, re-run `checklist` and
confirm the orphaned blocking row is dropped (re-derived away), the stage no longer blocks,
and no file was deleted.

**Acceptance Scenarios**:

1. **Given** a `checklist.md` on the "not stale" path containing a tool-injected `CHK-###`
   "missing acceptance coverage" blocking row for `FR-002`, and a current `spec.md` in
   which `FR-002` *is* covered, **When** the author re-runs `checklist`, **Then** the
   `CHK-###` row for `FR-002` is re-derived away (no longer present), the blocking count is
   zero, and the stage advances — with no manual file deletion.
2. **Given** the same checklist where `FR-002` is still **not** covered in `spec.md`,
   **When** `checklist` is re-run, **Then** the requirement is re-reported as blocking, but
   as a freshly re-derived verdict — not an accumulated duplicate of the prior run's row.
3. **Given** an author has hand-written notes in the authored sections of `checklist.md`
   (e.g. Advisory Notes, Lifecycle Notes), **When** `checklist` regenerates the
   machine-derived sections, **Then** those authored sections are preserved untouched.

---

### User Story 2 - Editing the plan regenerates the task graph on re-run (Priority: P1)

An author edits `plan.md` (e.g. adds a decision disposition) and re-runs `fsgg-sdd tasks`.
The task graph is regenerated in place from the changed sources — the new disposition
appears — while the author's advanced task status (`Done`/`Skipped`/`InProgress`) is
preserved. The author is never left with a graph reported "stale" and silently unchanged.

**Why this priority**: This is defect #147. A task graph that reports stale-and-does-
nothing is worse than a hard failure, because the author reasonably believes the re-run
picked up their edit. It silently corrupts downstream `analyze`/evidence freshness.

**Independent Test**: On a work item with a generated `tasks.yml`, edit `plan.md` to add
a decision disposition, re-run `tasks`, and confirm the run regenerates the graph in place
so the change is reflected — never reports stale while leaving the file unchanged.

**Acceptance Scenarios**:

1. **Given** a generated `tasks.yml` and a `plan.md` edited to add a new decision
   disposition, **When** the author re-runs `tasks`, **Then** the graph is regenerated in
   place and its rows reflect the changed plan (the new disposition appears) — with no
   manual file deletion and no stale-and-unchanged outcome.
2. **Given** an author has advanced task status (`Done`/`Skipped`/`InProgress`), **When**
   the task graph regenerates, **Then** that status is preserved on the corresponding
   re-derived tasks, while rows, dependencies, and refs are recomputed from sources.
3. **Given** upstream sources are unchanged, **When** `tasks` is re-run, **Then** the
   result is stable and no spurious regeneration or duplicate rows occur (idempotence).

---

### User Story 3 - Regeneration semantics are documented and discoverable (Priority: P2)

An author (or agent) who hits a stale or blocked gate can read the documented
regeneration semantics — how tool-injected content is distinguished from authored
content, and what re-running a stage does after upstream edits — and follow the
diagnostic's next action without inferring an `rm`.

**Why this priority**: The epic's root-cause acceptance criterion requires the semantics
to be *defined and documented*, not just fixed in code. Discoverability is what prevents
the next author from re-deriving the workaround by trial and error.

**Independent Test**: Locate the documented regeneration-semantics reference from the
diagnostic/next-action of a stale gate run, and confirm it states the injected-vs-
authored distinction and the regenerate-or-delete-and-regenerate rule.

**Acceptance Scenarios**:

1. **Given** a stale or blocking gate re-run, **When** the author reads its diagnostic
   and next action, **Then** they are pointed to documented regeneration semantics with
   an actionable step, not left to infer a manual `rm`.
2. **Given** the regeneration-semantics documentation, **When** it is compared to the
   live CLI behavior, **Then** they agree (the doc is not stale relative to the tool).

---

### Edge Cases

- **`unsafe-overwrite` opt-out is preserved**: a `checklist.md`/`tasks.yml` carrying the
  `<!-- fsgg-sdd: unsafe-overwrite -->` sentinel must still block overwrite as it does
  today; regeneration must not silently bypass that guard.
- **Partial authored + injected mix**: a file with authored sections (or advanced task
  status) *and* stale tool-injected rows must regenerate the injected rows while preserving
  the authored content — not all-or-nothing.
- **Author edits a tool-injected row by hand**: the tool **reclaims** it — regeneration
  overwrites the hand-edited generated row with the freshly derived version. A mutated
  generated row is never persisted as authoritative (this is the poison being killed).
- **Idempotent no-op**: re-running a gate with no upstream change and no authored edits
  must produce a byte-stable file and a `noChange` outcome (no accumulation).
- **Delete-and-regenerate path**: when the tool cannot safely regenerate in place, the
  emitted instruction must name the exact file path and the exact command, and following
  it must yield a clean artifact.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST distinguish tool-injected content from authored content
  within the gate artifacts `work/<id>/checklist.md` and `work/<id>/tasks.yml`, such that
  the distinction survives a write/read round-trip.
- **FR-002**: On any re-run of a gate stage, the system MUST NOT re-ingest its own
  tool-injected content as authored input. Tool-injected content MUST be re-derived from
  the current authoritative sources each run, not carried forward from the prior run's
  output.
- **FR-003**: When the current authoritative sources no longer justify a tool-injected
  blocking item (e.g. the FR is now covered in `spec.md`), a single re-run of that gate
  stage MUST clear the item without requiring the author to delete any file — including on
  the "not stale" path where the recorded source snapshot still matches. A tool-injected
  row's presence in the prior file MUST NOT keep it alive.
- **FR-004**: Re-running a gate stage after upstream authoritative sources change MUST
  regenerate the affected tool-injected content in place to reflect the current sources.
  It MUST NOT report the artifact "stale" while leaving it unchanged. The only permitted
  block on re-run is the `unsafe-overwrite` opt-out (FR-009), whose diagnostic already
  names the file and remediation.
- **FR-005**: `tasks` MUST regenerate the task graph in place from the changed sources on
  re-run (so an added plan decision disposition appears); it MUST NOT report
  stale-and-unregenerated. When regenerating, it MUST reclaim tool-owned rows (FR-002)
  rather than carry forward the prior run's derived rows.
- **FR-006**: Any diagnostic that requires an unavoidable manual delete-and-regenerate
  step MUST name the exact artifact file path to delete and the exact command to re-run.
  No supported recovery path may depend on the author inferring `rm`.
- **FR-007**: Regeneration MUST preserve genuinely-authored content and state — the
  authored sections of `checklist.md` (e.g. Advisory Notes, Lifecycle Notes, Source
  Specification/Clarifications) and, in `tasks.yml`, advanced task **status**
  (`Done`/`Skipped`/`InProgress`), the author-editable `owner`, each matched task's
  still-**live** hand-added disposition refs (`requirements`/`decisions`/`sourceIds`), and any
  whole hand-authored task that uniquely covers a live disposition the derivation does not —
  while recomputing all other tool-owned/derived content (checklist review rows; task
  structure, dependencies, required skills/evidence) from current sources. Authored refs/tasks
  whose sources are gone (or already covered by derivation) are dropped, so no stale content is
  re-ingested.
- **FR-008**: Re-running a gate stage with no upstream source change and no authored edit
  MUST be idempotent: a byte-stable artifact and a `noChange` outcome, with no
  accumulation or duplication of rows across runs.
- **FR-009**: The system MUST preserve the existing `<!-- fsgg-sdd: unsafe-overwrite -->`
  opt-out: regeneration MUST NOT overwrite an artifact carrying that sentinel, and MUST
  continue to surface the existing overwrite-blocked diagnostic.
- **FR-010**: The regeneration semantics (the injected-vs-authored distinction and the
  regenerate-or-delete-and-regenerate rule) MUST be documented in the authoring-contract
  surface, and that documentation MUST be kept in agreement with the live CLI behavior
  (pinned against drift).
- **FR-011**: The JSON automation contract, exit codes, and determinism of the affected
  commands MUST be preserved except where a command's outcome/diagnostic changes to
  satisfy FR-002–FR-006; any such change MUST be additive and deterministic, and
  `--text`/`--rich` remain pure projections.
- **FR-012**: The change MUST remain within SDD's ownership boundary — no Governance
  runtime, verdict, or rule evaluation is introduced; gate artifacts remain SDD-owned
  lifecycle artifacts.

### Key Entities

- **Gate artifact**: an SDD-owned lifecycle file that the tool generates *and* the author
  edits against — here `work/<id>/checklist.md` and `work/<id>/tasks.yml`.
- **Tool-injected content**: rows/sections the CLI writes into a gate artifact (e.g. a
  `CHK-###` missing-coverage blocking item, a derived task row) — owned by the generator,
  meant to be re-derived, not authored.
- **Authored content**: content the human owns within the same file — the authored prose
  sections of `checklist.md` (Advisory/Lifecycle Notes, etc.), and per-task advanced
  `status`/`owner` in `tasks.yml`. (The `(covers AC-###)` coverage declaration is authored
  upstream in `spec.md`, not in these gate artifacts.)
- **Source snapshot / digest**: the recorded fingerprint of the upstream authoritative
  sources (spec/clarifications/checklist/plan) used to detect upstream change.
- **Regeneration**: recomputing tool-injected content from current sources while
  preserving authored content, or, when that is not safe in place, blocking with an
  explicit delete-and-regenerate instruction.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After correcting FR→AC coverage, a single `checklist` re-run clears every
  resolved missing-coverage blocking item — zero manual file deletions required
  (previously: recovery was impossible without `rm work/<id>/checklist.md`).
- **SC-002**: After an upstream `plan.md` change, a single `tasks` re-run reflects the
  change in the regenerated task graph in 100% of cases; no run reports
  stale-and-unchanged, and no manual file deletion is required (except when the author has
  opted out via the `unsafe-overwrite` sentinel, whose diagnostic names the file).
- **SC-003**: 100% of tool-injected blocking items are re-derived from current sources on
  every re-run; none survives from a prior run's injection once its cause is resolved.
- **SC-004**: Genuinely-authored content — advanced task status (`Done`/`Skipped`) and
  `owner`, plus the authored prose sections of `checklist.md` — is preserved across
  regeneration in 100% of tested cases.
- **SC-005**: Every stale or blocked gate re-run yields an actionable next step
  (regenerate, or delete-and-regenerate with the exact path and command); no diagnostic
  leaves the author to infer `rm` unaided.
- **SC-006**: Re-running any affected gate stage with unchanged sources and no authored
  edits is byte-stable and returns `noChange` (idempotence verified).

## Assumptions

- Scope is the two named gate artifacts that mix generated and authored content —
  `checklist.md` and `tasks.yml`. Purely-generated views already written as
  `GeneratedView` (e.g. `work-model.json`, `analysis.json`) are out of scope; they are
  regenerated wholesale and carry no authored content.
- "Resolving coverage" refers to the author adding the `(covers AC-###)` declaration to the
  requirement reference in `spec.md` (authored at the specify stage; the checklist stage
  *computes* FR→AC coverage from it). The checklist defect is the "not stale" re-run path
  re-ingesting its own prior `CHK-###` rows, independent of where coverage is authored.
- The existing `<!-- fsgg-sdd: unsafe-overwrite -->` opt-out semantics are retained
  unchanged; this feature does not redesign that guard.
- Governance ownership is unchanged: SDD reports readiness and owns these lifecycle
  artifacts; no Governance runtime, freshness verdict, or gate enforcement is introduced.
- The JSON output remains the automation contract; any diagnostic or outcome additions are
  additive and deterministic, and `--text`/`--rich` remain pure projections.
- Whether the two defects are fixed by segregating generated content (provenance markers /
  managed regions) or by always re-deriving generated content on re-run is an
  implementation decision deferred to the plan; this spec constrains the observable
  regeneration semantics, not the mechanism.
