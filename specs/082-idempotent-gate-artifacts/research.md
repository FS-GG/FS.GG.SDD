# Phase 0 Research: Idempotent Generated Gate Artifacts

This phase resolved the one design question the spec deferred — **how** to distinguish
tool-injected content from authored content — plus the task merge model and the fate of the
`stale` signal. All findings are grounded in the current code (file:line references below).

## Ground truth (current behavior)

- **All `CHK-###`/`CR-###` checklist rows are tool-derived** from spec/clarify facts
  (`ChecklistPlanAuthoring.fs` `plannedChecklistReviews`, `allocate`). No human authors a
  review row. The FR→AC coverage verdict is computed from **`spec.md`** requirement
  references (`Specification.fs requirementReferences`), *not* from `checklist.md`.
- **Checklist re-run has two branches**: stale (source digest changed) →
  `rederiveStaleChecklist` purges + re-derives the machine sections; not-stale →
  `plannedChecklistReviews (Some existingFacts)` + `appendChecklistReviews`, which **dedups
  on existing rows' `SourceId`s and preserves the rows verbatim** — this is the #146
  re-ingestion.
- **Checklist sections**: machine-derived (rewritten by `rederiveStaleChecklist`): *Source
  Snapshot, Checklist Items, Review Results, Accepted Deferrals, Blocking Findings*.
  Authored (never rewritten): *Source Specification, Source Clarifications, Advisory Notes,
  Lifecycle Notes*.
- **Task rows are USUALLY tool-derived but CAN be hand-authored** (revised — see Decision 3):
  stable id `T###`. Author-mutable fields include `status` (incl. `Skipped of string`
  rationale), `owner`, AND hand-added disposition refs (`requirements`/`decisions`/`sourceIds`)
  — the corpus covers `DEC-001` this way. On upstream change, `markTasksStale` relabels
  `Pending`/`InProgress`→`Stale` and carries rows verbatim; it **never re-derives** — this is
  #147.
- **Nothing downstream reads task `Stale`**: `analyze`/`missingDisposition` collects
  dispositions from *all* tasks regardless of status (`ViewGeneration.fs`); `verify`/
  `refresh`/`ship` "stale" refer to evidence/generated-view staleness, not task status.
  Only `staleTask`, `TF-001`, `tasks.correctStaleTasks`, and `StaleCount` consume it.

## Decision 1 — Mechanism: always re-derive, don't mark provenance

**Decision**: Re-derive the machine-owned regions from current sources on **every** run
(stale or not), rather than adding per-row provenance markers to segregate tool-injected
from authored rows.

**Rationale**: Provenance markers are only necessary if authored and generated rows are
*interleaved* in the same region. They are not: every `CHK`/task row is tool-derived, and
the author-owned content is cleanly separable — whole authored *sections* in `checklist.md`,
and two *fields* (`status`/`owner`) on a task keyed by a stable id. So the simplest correct
mechanism is to treat the machine regions as fully derived output (recompute them) and the
authored content as inputs to preserve/merge. This *removes* code (a branch + a relabel
step) rather than adding a marker vocabulary and a parser for it.

**Alternatives considered**:
- *Per-row provenance markers / managed-region sentinels* — rejected: adds a marker grammar,
  a parser, and a drift surface to solve interleaving that doesn't exist. Heavier and more
  error-prone than re-deriving.
- *Keep the digest-stale gate, just also re-derive on the not-stale path when a row is
  orphaned* — rejected: that is "always re-derive" with extra conditional bookkeeping; the
  unconditional form is simpler and strictly covers it.

## Decision 2 — Checklist: make `rederiveStaleChecklist` unconditional

**Decision**: Collapse the stale/not-stale branch. Always re-derive the five machine-derived
sections from current spec/clarification facts (the existing `rederiveStaleChecklist` path),
always refresh the Source Snapshot, and preserve the authored sections untouched. Delete the
`plannedChecklistReviews (Some existingFacts)` re-ingestion and `appendChecklistReviews`.

**Rationale**: The stale gate exists only to decide whether to re-derive; since re-derivation
is deterministic and cheap and the authored sections are preserved either way, the gate adds
only the #146 bug. Unconditional re-derive makes FR-002/003 hold by construction and keeps
FR-008 (unchanged sources → identical bytes → `noChange`).

**Consequence**: `CHK`/`CR` ids are reallocated deterministically each run (by requirement
order), so they stay byte-stable across runs but may differ once from a file produced by the
old append path (one-time canonicalization).

## Decision 3 — Tasks: re-derive + four-way merge (matched / new / kept-authored / dropped)

**Revised during implementation** — the corpus fixture disproved the "all task rows are
tool-derived" premise (see Decision 1 note): `tasks.yml` legitimately MIXES tool-derived
tasks with hand-authored ones (custom titles, hand-added `requirements`/`decisions`), and
disposition coverage for a plain clarification decision (`DEC-001`) exists in the corpus
ONLY as a hand-authored `decisions: [DEC-001]` (the derivation and `runPlan` never propagate
a non-deferral decision id into a task). Confirmed with the user: **preserve live authored
tags**. So the merge is four-way, keyed on the deterministic task **Title**:

1. **Matched** (a derived task's title equals a prior task's title): the derived task inherits
   the prior task's authored `status`/`owner`, its stable `T###` id, and its still-**live**
   authored disposition refs (`requirements`/`decisions`/`sourceIds` unioned with the derived
   set, filtered to ids present in the current sources).
2. **New** (derived, no prior match): a fresh `T###` above every prior id; because
   `plannedTasks` embeds fresh ids in `Dependencies`, those references are remapped fresh→final.
3. **Kept authored** (prior, no derived match) — kept **iff** it uniquely covers a live
   disposition the derived graph does *not* already cover (a hand-authored task the tool cannot
   derive); its refs are filtered to live ids.
4. **Dropped** (prior, no derived match, no *uniquely-covered* live disposition): a redundant
   orphan whose source is gone.

`markTasksStale` and the `staleTask`/`TF-001` emission on the source-change path are removed.

**Rationale**: fixes #147 (a new plan disposition derives a task and appears; the run never
reports stale-and-unchanged) while preserving genuinely-authored tasks and disposition
coverage the derivation misses. The "uniquely covers an unmet live disposition" rule is what
distinguishes an authored task worth keeping from an orphaned derived leftover — without a
provenance marker (still avoided). Title keying is exact for real generated files (byte-stable
re-runs); a hand-renamed derived task falls into case 3/4 by coverage, which is acceptable.

**Accepted limitations** (documented, low-impact):
- A hand-renamed derived task is treated as authored (kept if it uniquely covers a live
  disposition, else dropped) rather than matched — its `status`/`owner` are preserved only if
  it still covers something unmet. Rare; the corpus is the motivating case.
- Positional `PD-###` ids can be reused across plan regenerations; the coverage rule keys on
  the disposition set, not the volatile id, so a reused id does not falsely retain an orphan.

**Alternatives considered**:
- *Per-task provenance markers* — still rejected (Decision 1): the coverage rule separates
  authored from orphaned without a marker vocabulary.
- *Preserve only `status`/`owner` (the original clarify Q2 answer)* — rejected after the
  corpus disproved it: it silently drops hand-authored disposition coverage. The user reopened
  the decision to "preserve live authored tags."
- *Keep `markTasksStale` and additionally re-derive* — rejected: the confusing dual signal and
  the dead `tasks.correctStaleTasks` loop.

## Decision 4 — Retire the task `stale` re-run signal, keep the symbols

**Decision**: Stop emitting `staleTask`, `TF-001`, `status: stale`, and `StaleCount > 0`
from the source-change re-run path. Keep `TaskStatus.Stale`, the `staleTask` diagnostic
constructor, its remediation pointer, and the `tasks.correctStaleTasks` next-action in the
public surface (no baseline removal).

**Rationale**: Downstream consumers don't read task `Stale` (verified), so retiring the
emission is safe and is exactly what fixes #147's "reported stale, did nothing." Keeping the
symbols avoids a subtractive `PublicSurface.baseline` change and lets the parser still accept
a legacy/authored `Stale` value. If a later feature proves them fully dead, remove them then
with a proper baseline update.

**Alternatives considered**: *Delete the `Stale` case and all its plumbing now* — rejected
for this feature: larger surface change (Task.fsi DU, StaleCount, constructor, routing,
rendering) for no functional gain here; deferred.

## Decision 5 — Escape hatch = the existing `unsafe-overwrite` opt-out only

**Decision**: The only re-run outcome that blocks instead of regenerating is the existing
`<!-- fsgg-sdd: unsafe-overwrite -->` opt-out (FR-009), whose diagnostic already names the
file and remediation. No new delete-and-regenerate diagnostic is introduced; FR-006 is
satisfied by that existing, file-and-command-naming diagnostic.

**Rationale**: With always-re-derive, the "regenerate cleanly" branch of the epic's AC is the
default; the "fail with explicit delete-and-regenerate instruction" branch is served by the
pre-existing sentinel path, so no author is ever left inferring `rm`.

## Decision 6 — Document the semantics in the authoring-contract surface

**Decision**: Add a "regeneration semantics" section to the authoring-contracts surface
(`fs-gg-sdd-authoring-contracts` skill, mirrored `.claude`≡`.codex`≡`.agents`, manifest
sha256 refreshed) stating: (1) tool-injected rows are re-derived every run and never
re-ingested; (2) authored content is the authored `checklist.md` sections and task
`status`/`owner`; (3) coverage is authored upstream in `spec.md`; (4) the only block is the
`unsafe-overwrite` opt-out. Pin it against the live behavior with a drift-guard test.

**Rationale**: Satisfies the epic's root-cause criterion (define **and document**) and US3;
Principle VII keeps it a single mirrored source pinned to behavior, not a second truth.

## Open questions

None. The spec's deferred mechanism question is resolved (Decision 1); all clarify decisions
map to Decisions 2–5.
