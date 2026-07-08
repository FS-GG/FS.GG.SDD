# Feature Specification: The Source-Id Union Belongs at the Consumer

**Feature Branch**: `item/189-sdd-reconcile-tasks-yml-sourceids-vs-dec`

**Created**: 2026-07-08

**Status**: Draft

**Input**: FS-GG/FS.GG.SDD#189, deferred out of FS-GG/FS.GG.SDD#164 during review of PR #187 (spec 093, User Story 5 — "DEFERRED, not implemented"). The defect was real and unfixed; only the chosen fix was withdrawn. Grounding the issue against the running CLI before implementation corrected its diagnosis and split three unrelated defects out to FS-GG/FS.GG.SDD#191, FS-GG/FS.GG.SDD#192, FS-GG/FS.GG.SDD#193.

## Overview

A task in `tasks.yml` carries three reference fields. Two are authored and typed —
`requirements: [FR-###]` and `decisions: [DEC-###]`. One is authored and untyped —
`sourceIds: [...]`, the escape hatch for the ids the typed fields cannot express
(`AC-`, `CR-`, `GV-`, `PC-`, `PD-`, `PM-`, `SB-`, `VO-`). The parser reads all three
verbatim; it derives nothing (`Task.fs:277`).

Consumers disagree about which field is canonical. Spec 093 §3 named four of them and
concluded that `evidence` and `verify` were both blind. **That conclusion is wrong**, and
this feature's first job is to correct it. Grounded against the running CLI on `bd64f02`:

| consumer | what it actually reads | site | blind? |
|---|---|---|---|
| `analyze` | `SourceIds ∪ Requirements ∪ Decisions` | `TaskGraphAuthoring.fs:725-731` | no |
| `evidence` | `LinkedSourceIds ∪ LinkedRequirementIds ∪ LinkedDecisionIds` | `HandlersEvidence.fs:276` | **no** |
| `verify` | hard-coded `[]` | `HandlersVerify.fs:154` | **yes — vacuously** |
| agent guidance | `Requirements @ Decisions` | `WorkModel.fs:888` | **yes** |

Two corrections to the inherited diagnosis:

1. **`evidence` is not blind.** `HandlersEvidence.fs:212` assigns `LinkedSourceIds = task.SourceIds`,
   but that field has exactly one consumer in the tree — `:276` — and it already unions with the
   typed fields. Verified end-to-end: a `T001` authored with **no `sourceIds:` line** scaffolds to
   `requirementRefs: ["FR-001"]`. The fix prescribed in #189's original body ("`LinkedSourceIds`
   should read `task.SourceIds ∪ requirements ∪ decisions`") is a **no-op**. Making it would
   change no byte of output.

2. **`verify` is blind, but not for the reason the union story predicts.**
   `VerifyEvidenceDispositionView.SourceIds` is the literal `[]` at `HandlersVerify.fs:154`, so
   `verify.json`'s `affectedSourceIds` is **unconditionally empty** — for a task with eleven
   `sourceIds` exactly as much as for a task with none. This is not a union defect; it is an
   unimplemented field. The current golden (`tests/FS.GG.SDD.Commands.Tests/goldens/readiness/verify.json`)
   asserts `"affectedSourceIds": []` at every one of its occurrences, which is why the gap
   survived: **the golden enshrines the bug.**

So the union defect has exactly **one** real consumer (agent guidance), and beside it sits an
unrelated unimplemented field on the same subject. Both are fixed here because both answer the
same question — *which ids does this task touch?* — and a reader who fixes one and not the other
leaves the codebase saying two different things.

### Why the parser is the wrong home (inherited, and confirmed)

Deriving `SourceIds = sourceIds ∪ requirements ∪ decisions` at `Task.fs` looks like a strict
widening. It is not. Each field already answers to its own gate, against its own known-id set:
`sourceIds` to `unknownTaskSourceReference` in the `tasks` stage
(`taskValidationDiagnostics.unknownSources`, `TaskGraphAuthoring.fs:777-785`), and
`requirements:`/`decisions:` to `unknownReference` at work-model generation
(`WorkModel.referenceDiagnostics`, `:192-206`, folded in at `:523`). Unioning on parse folds the
typed fields into `SourceIds` and so subjects them to the *tasks* gate **as well** — a second
validation, against a different id set, that they have never faced:

> An existing workspace whose hand-authored `requirements: [FR-007]` names an id since dropped
> from `spec.md` is green today. After upgrading, the same untouched `tasks.yml` exits 1 with
> `unknownTaskSourceReference`. `schemaVersion` deliberately stays `1`, so nothing explains the
> change to the author.

Confirmed at the gate. `TaskGraphAuthoring.fs:272-279` records the same lesson in a comment — a
prior attempt to thread FR ids from a decision line into `requirements:` caused `unknownTaskSourceReference`
to block `tasks` on output the tool had just generated.

The union therefore belongs at each consumer that wants it. `analyze` and `evidence` already do it
that way. This feature makes the remaining two consistent with them, and **does not touch the parser**.

Two comments in the tree currently assert the reverted parser union as present-tense fact and must
be corrected, or the next reader re-derives the withdrawn fix:

- `docs/examples/lifecycle-artifacts/tasks.yml:7-9` — *"`sourceIds` is the untyped superset the tool
  DERIVES on parse … it is what `evidence` and `verify` read, so a task authored this way is fully
  visible to both."* False for `verify`, vacuous for `evidence`, and the parser derives nothing.
  **Owned by FS-GG/FS.GG.SDD#192**, which rewrites that example; out of scope here.
- `TaskGraphAuthoring.fs:275` — *"whence `Task.fs` unions it into `SourceIds`"*. In scope here.

**Change tier: Tier 1** (generated-view content change to `readiness/<id>/agent-commands/<target>/guidance.json`
and command-output-contract content change to `readiness/<id>/verify.json`). No `.fsi` changes:
`deriveGuidanceModel: model: WorkModel -> NormalizedGuidanceModel` (`WorkModel.fsi:123`) keeps its
signature, and `HandlersVerify`/`TaskGraphAuthoring` have no `.fsi` (internal modules). No public
API surface baseline moves. No persisted schema version changes; both fields already exist and are
already emitted — they change from *under-populated* to *populated*.

## Clarifications

### Session 2026-07-08

- Q: Should `HandlersEvidence.fs:212` be changed to union, per #189's original prescription?
  → A: **No.** Verified no-op: `:276`, its only consumer, already unions. Changing `:212` would
  widen `LinkedSourceIds` for a field nothing else reads. Instead, add a comment at `:212` recording
  why the union is already satisfied, so the withdrawn fix is not re-proposed a third time.

- Q: What *should* `verify.json`'s `affectedSourceIds` contain?
  → A: The union of the source-id lineage of every task the obligation links —
  `⋃ over draft.TaskIds of (task.SourceIds ∪ task.Requirements ∪ task.Decisions)`, distinct and
  sorted. This mirrors `verifyTestDispositionViews` (`HandlersVerify.fs:164`), which already takes
  `taskFacts` and derives `RequirementIds` from the linked tasks. `taskFacts` is already in scope at
  the `verifyEvidenceDispositionViews` call site (`:494`), so no plumbing is added.

- Q: Does widening these two fields flip any existing workspace from green to red?
  → A: **`affectedSourceIds`, no. `relatedIds`, YES — and it is a merge blocker.** An earlier draft of
  this spec asserted "neither field is validated … widening cannot produce a diagnostic." That is false
  for `relatedIds`, and adversarial review caught it.

  `relatedIds` feeds `purpose`, and both are hashed into `behaviorModelDigest`
  (`WorkModel.fs:945-965`). `agents` compares the digest recorded in an existing `guidance.json`
  against the recomputed one (`HandlersAgents.fs:361-368`); a mismatch raises
  `agents.behaviorDivergence`, an `errorDiagnostic` (`DiagnosticConstructors.fs:899`), and
  `HandlersAgents.fs:435` then sets `effects = []`. Reproduced: a workspace generated by the pre-095
  CLI, **left completely untouched**, runs `agents` on the post-095 CLI and blocks with 4 diagnostics.
  Its `fix:` tells the author to regenerate the guidance — which `agents` has just refused to do.

  This is exactly the green→red-on-upgrade hazard this spec cites as the argument *against* the
  parser union, arriving through a different door. `affectedSourceIds` is unaffected: nothing digests
  `verify.json`'s disposition arrays.

  **Root cause is pre-existing and not this feature's.** `divergent = equivalenceRequired && not
  behaviorMatches` never compares `claude` against `codex`; it compares recorded-vs-recomputed for one
  target. So an ordinary `tasks.yml` retitle already blocks `agents` on `main` — verified. This
  feature does not create the guard; it makes every existing workspace trip it once, with zero
  authored edits. Tracked as FS-GG/FS.GG.SDD#197; this feature is sequenced behind it.

- Q: Is `affectedSourceIds` a Governance compatibility obligation, and does this need a migration note?
  → A: No, to both. `docs/release/schema-reference.md:183` names `evidenceDispositions` but not its
  sub-fields; `affectedSourceIds` appears nowhere under `docs/release/` and no Governance-handoff
  fixture reads it. **Initially this spec asserted a migration note was required.** Checking
  `versioning-policy.md:42-46` shows that is wrong: Breaking means *remove/rename/retype a field,
  change an output shape, remove/rename a command or flag, or change an exit-code contract*. Filling
  an existing `string[]` is none of those. `migrations/README.md` then forbids the note outright —
  "an additive-only release MUST NOT carry a migration note." FR-009 was rewritten to say so, and to
  say why, so the next author does not re-derive the wrong obligation. This also keeps the change out
  of `docs/release/`, which is FS-GG/FS.GG.SDD#190's in-flight touch-set.

- Q: Should the emitter stop writing a `sourceIds:` line that merely restates the typed fields?
  → A: **Out of scope.** That is a `tasks.yml` authored-layout change owing its own normalization
  note, and it collides with FS-GG/FS.GG.SDD#192's rewrite of the shipped example. Deferred.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A scope boundary reaches the agent that must respect it (Priority: P1)

An author writes a task whose only reference to a scope boundary is `sourceIds: [SB-002]` —
the typed fields cannot express `SB-###`. They run `verify`, then `agents`.

**Why this priority**: This is the defect. An id the author deliberately attached to a task is
dropped on the floor before the agent that acts on the task ever sees it. The author has no
signal that it was dropped.

**Acceptance**:

- **AC-001**: Given a task with `sourceIds: [SB-002]`, `requirements: [FR-001]`, and
  `decisions: [DEC-001]`, when agent guidance is derived, then that task's `relatedIds` contains
  `DEC-001`, `FR-001`, and `SB-002`.
- **AC-002**: Given a task with `requirements: [FR-001]` and no `sourceIds:`, when agent guidance
  is derived, then its `relatedIds` is exactly `[FR-001]` — the union adds nothing that was not
  authored, and no existing guidance loses an id.
- **AC-003**: `relatedIds` is distinct and sorted, so a task whose `DEC-001` appears in both
  `decisions:` and `sourceIds:` (the duplication `clarificationDecisionTasks` emits) yields one
  `DEC-001`, not two.

### User Story 2 - `verify.json` reports which sources an obligation actually touches (Priority: P1)

A Governance integrator (or an author reading `verify.json`) wants to know which lifecycle facts a
failing evidence obligation affects.

**Why this priority**: `affectedSourceIds` has shipped as a permanently empty array. Any consumer
that trusted it has been reading a constant. This is a correctness fix to a published output field.

**Acceptance**:

- **AC-004**: Given an obligation linked to a task carrying `sourceIds: [VO-001]`,
  `requirements: [FR-002]`, `decisions: [DEC-001, DEC-002]`, when `verify` runs, then that
  obligation's `affectedSourceIds` is `["DEC-001", "DEC-002", "FR-002", "VO-001"]`.
- **AC-005**: Given an obligation linked to **two** tasks, its `affectedSourceIds` is the distinct,
  sorted union of both tasks' lineages.
- **AC-006**: Given an obligation whose linked task carries no references at all,
  `affectedSourceIds` is `[]` — the previous value, now earned rather than hard-coded.
- **AC-007**: `verify`'s exit code, `outcome`, and diagnostics are byte-for-byte unchanged for every
  existing fixture. Only `affectedSourceIds` content moves.
- **AC-011**: No file is added under `docs/release/migrations/`, and `release-readiness.json`'s
  `migrations[]` stays empty — the change is not Breaking under `versioning-policy.md:44`.

### User Story 3 - The next reader does not re-propose the withdrawn parser fix (Priority: P2)

A contributor reads `TaskGraphAuthoring.fs` or `HandlersEvidence.fs` and reasons about where the
union lives.

**Why this priority**: The withdrawn fix has now been proposed twice (once landed and reverted in
#187, once prescribed in #189's body). Both times the code's own comments encouraged it.

**Acceptance**:

- **AC-008**: `TaskGraphAuthoring.fs:275`'s claim that "`Task.fs` unions it into `SourceIds`" is
  corrected to describe what the parser does (reads `sourceIds` verbatim) and why the union is a
  consumer concern.
- **AC-009**: `HandlersEvidence.fs:212` carries a comment recording that the union is already
  satisfied at `:276`, so widening `:212` is a no-op.
- **AC-010**: `docs/reference/authoring-contracts.md` states, for the `tasks.yml` reference fields,
  that the parser reads all three verbatim and that each consumer unions them as it needs — with the
  `unknownSources` gate named as the reason the parser must not.

### Edge Cases

- A task listing the same id in `decisions:` and `sourceIds:` (which `clarificationDecisionTasks`
  emits by construction) must not double it in either output. Covered by AC-003.
- Case: `Task.fs:277` upper-cases and sorts `sourceIds`, while `Requirements`/`Decisions` retain the
  authored casing of their typed ids. The union must not emit `FR-001` and `fr-001` as two ids. The
  id grammars (`Identifiers.create*`) already normalize the typed fields, so the union compares
  equal-cased values; a test pins this rather than assuming it.
- An obligation with **no** linked tasks yields `affectedSourceIds: []` and must not throw.
- A `draft.TaskIds` entry naming a task absent from `taskFacts` (not currently reachable — drafts are
  built from `taskFacts` — must degrade to skipping that id, never throw. Principle VIII.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `WorkModel.deriveGuidanceModel` MUST compute a task's `relatedIds` as the distinct,
  sorted union of `task.Requirements`, `task.Decisions`, and `task.SourceIds`. (covers AC-001, AC-002, AC-003)
- **FR-002**: `HandlersVerify.verifyEvidenceDispositionViews` MUST populate
  `VerifyEvidenceDispositionView.SourceIds` from the linked tasks' lineage — the distinct, sorted
  union over `draft.TaskIds` of `task.SourceIds ∪ task.Requirements ∪ task.Decisions` — replacing the
  hard-coded `[]`. It MUST take `taskFacts` as a parameter, mirroring `verifyTestDispositionViews`. (covers AC-004, AC-005, AC-006)
- **FR-003**: `verify`'s `outcome`, exit code, and diagnostic set MUST be unchanged for every existing
  fixture; only `affectedSourceIds` content changes. (covers AC-007)
- **FR-004**: The parser (`Task.fs`) MUST NOT be changed. `taskValidationDiagnostics.unknownSources`
  MUST continue to gate on the authored `sourceIds` alone — never on `requirements:`/`decisions:`,
  which answer to `WorkModel.referenceDiagnostics` instead — so no existing workspace moves from
  green to blocked. (covers AC-007)
- **FR-005**: `HandlersEvidence.fs:212` MUST NOT be changed to union; it MUST carry a comment
  explaining that its only consumer (`:276`) already does. (covers AC-009)
- **FR-006**: `TaskGraphAuthoring.fs:275`'s stale parser-union claim MUST be corrected. (covers AC-008)
- **FR-007**: `docs/reference/authoring-contracts.md` MUST document the verbatim-parse /
  union-at-consumer model for the three `tasks.yml` reference fields. (covers AC-010)
- **FR-008**: Both changed outputs MUST remain deterministic — identical inputs produce identical
  bytes across runs — and idempotent under re-run. (covers AC-003, AC-005)
- **FR-009**: This release MUST NOT ship a migration note. `docs/release/versioning-policy.md:44`
  classes a change Breaking only when it *removes, renames, or retypes a public field, changes an
  output shape, removes/renames a command or flag, or changes an exit-code contract*. Populating an
  existing `string[]` that previously held a constant `[]` is none of those: the field's name, type,
  and shape are unchanged, and `schemaVersion` stays `1`. `docs/release/migrations/README.md` is
  explicit that "an **additive-only** release **MUST NOT** carry a migration note; its absence is
  consistent with the policy, not an omission." The change is therefore recorded in this spec, in the
  code comments (FR-005/006), and in `authoring-contracts.md` (FR-007) — **not** in
  `docs/release/`. (covers AC-007, AC-011)
- **FR-010**: The value-semantics change MUST be stated where an author looks for the contract:
  `docs/reference/authoring-contracts.md` says which consumers union the three reference fields, so
  a reader learns that `affectedSourceIds` and `relatedIds` now carry the union. (covers AC-010)

### Key Entities

- **`WorkTask.SourceIds`** — authored, untyped, upper-cased and sorted at parse. The escape hatch
  for ids with no typed field. Gated by `unknownSources`. Unchanged by this feature.
- **`WorkTask.Requirements` / `WorkTask.Decisions`** — authored, typed (`RequirementId` / `DecisionId`).
  Never gated by `unknownSources`. Unchanged by this feature.
- **`NormalizedGuidanceModel.RelatedIds`** — generated. Widens from `Requirements @ Decisions` to the
  three-way union.
- **`VerifyEvidenceDispositionView.SourceIds`** → serialized as `verify.json`'s `affectedSourceIds`.
  Changes from the literal `[]` to the linked tasks' lineage union.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A task whose only `SB-###` reference is in `sourceIds:` has that id present in its
  generated agent-guidance `relatedIds`. Today it is absent; a test fails before and passes after.
- **SC-002**: `verify.json`'s `affectedSourceIds` is non-empty for at least one obligation in the
  updated golden. Today every occurrence in that golden is `[]`.
- **SC-003**: Zero existing workspaces move from a passing to a blocking `tasks`/`analyze` outcome:
  the parser and `unknownSources` are untouched, proven by an unchanged `Task.fs` and an unchanged
  `TasksArtifactTests` suite.
- **SC-004**: The full test suite passes with no regression against the recorded baseline
  (1236 passing, 4 skipped — the 4 are the network-gated composition-acceptance tests).
- **SC-005**: Re-running `verify` and `agents` twice over the same sources produces byte-identical
  outputs (determinism + idempotence).
- **SC-006**: This grep returns nothing across `src/` and `docs/reference/` — no present-tense claim
  that the parser unions `sourceIds` survives (the one historical mention in `TaskGraphAuthoring.fs`
  is past-tense and immediately corrected):

  ```sh
  grep -rniE "unions it into|Task\.fs unions|derives on parse|untyped superset the tool derives" src/ docs/reference/
  ```

  The claim survives in `docs/examples/lifecycle-artifacts/tasks.yml`, which FS-GG/FS.GG.SDD#192 owns
  and rewrites; correcting it here would collide with that item's touch-set.

## Assumptions

- `taskFacts` is in scope at `HandlersVerify.fs:494` (verified: the adjacent line already passes it
  to `verifyTestDispositionViews`), so FR-002 adds a parameter, not a plumbing change.
- `LinkedSourceIds` has exactly one consumer, `HandlersEvidence.fs:276` (verified by grep across
  `src/` and `tests/`; there are no test readers).
- `affectedSourceIds` carries no Governance compatibility obligation (verified: absent from `docs/release/`).
- Both authored reference surfaces are existence-validated, by *different* gates at *different*
  stages — `sourceIds` by `unknownTaskSourceReference` (tasks stage), `requirements:`/`decisions:` by
  `unknownReference` (work-model generation, a `DiagnosticError`). An early draft of this spec claimed
  the typed fields were "never validated"; that was a grep that only covered `TaskGraphAuthoring.fs`.
  The parser-union hazard is unchanged by the correction — it is about adding a *second* gate.
- The measured local baseline on `bd64f02` is 1236 passing / 4 skipped. This sandbox needs
  the `RestoreForceEvaluate` NuGet workaround; CI does not.

## Dependencies

- None on other in-flight work. `fsgg-coord overlap` reports FS.GG.SDD#189 DISJOINT from the three
  in-flight items (#188, #190, #192-when-claimed) after the touch-set narrowing recorded on the issue.
- FS-GG/FS.GG.SDD#191 and FS-GG/FS.GG.SDD#183 OVERLAP this item and are sequenced behind it via the
  board's `Blocked by`.

## Out of Scope

- **The parser union.** Explicitly rejected; see Overview.
- **`HandlersEvidence.fs:212`.** Comment only (FR-005).
- **FS-GG/FS.GG.SDD#191** — `verify`/`ship` report success but never write `work-model.json`. Found
  while grounding this item. It means FR-001's effect is not observable through the `agents` command
  end-to-end until #191 lands; FR-001 is therefore verified at `deriveGuidanceModel`'s own seam
  (`AgentGuidanceViewTests`), which is where its contract lives.
- **FS-GG/FS.GG.SDD#192** — the shipped example fails its own `analyze`, and owns the correction of
  `docs/examples/lifecycle-artifacts/tasks.yml`'s stale comment.
- **FS-GG/FS.GG.SDD#193** — `analyze` emits `missingDisposition` twice.
- **Emitter suppression of redundant `sourceIds:` lines.** Deferred; see Clarifications.
- **`ArtifactOperation.NoChange` vs `CommandOutcome.NoChange` naming confusion** — FS-GG/FS.GG.SDD#183.
