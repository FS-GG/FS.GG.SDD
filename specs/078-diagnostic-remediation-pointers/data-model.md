# Phase 1 Data Model: Diagnostic remediation pointers

No persisted schema changes. This models the **in-memory registry** and the **pointer mapping**
that drive construction and the guard. Nothing here is serialized to disk.

## Entity: `RemediationPointer`

An in-memory record (module `RemediationPointers`, internal to `FS.GG.SDD.Commands`) describing
where a covered diagnostic's correction should send the author.

| Field | Type | Notes |
|-------|------|-------|
| `Example` | `string option` | Repo-relative POSIX path under `docs/examples/lifecycle-artifacts/`, e.g. `docs/examples/lifecycle-artifacts/clarifications.md`. `None` only if the stage genuinely has no example (none after FR-004 lands). |
| `Anchor` | `string option` | `docs/reference/authoring-contracts.md#<slug>` where `<slug>` is the GitHub slug of a real heading. `None` only if no grammar section states the rule. |

**Invariant (guard-enforced)**: at least one of `Example`/`Anchor` is `Some` for every covered id;
when both are `Some`, the rendered correction cites both (FR-002, clarify Q2).

## Entity: covered-set registry

`RemediationPointers.registry : Map<string, RemediationPointer>` — keyed by diagnostic **id**
(the same id string passed to the constructor). Its key set **is** the enumerated authoring-grammar
covered set (FR-001). The guard iterates this map.

`RemediationPointers.suffixFor : string -> string` — returns the deterministic pointer sentence for
a covered id (see `contracts/remediation-pointer.md`), or `""` for an id not in the registry (so a
non-covered constructor that accidentally calls it is a no-op, protecting FR-008).

## Stage → pointer targets

Anchors resolve against the current `docs/reference/authoring-contracts.md` headings:

| Stage | Example path | Front-matter anchor | Grammar anchor(s) for the stage's content rules |
|-------|--------------|--------------------|--------------------------------------------------|
| charter | `…/charter.md` *(new)* | `#per-stage-front-matter` | `#per-stage-front-matter` (identity/scope) |
| specify | `…/spec.md` *(new)* | `#per-stage-front-matter` | `#specify---input-intent-facts`, `#stable-id-declarations` |
| clarify | `…/clarifications.md` | `#per-stage-front-matter` | `#clarify-decision-tag-resolution`, `#stable-id-declarations` |
| checklist | `…/checklist.md` | `#per-stage-front-matter` | `#acceptance-coverage-line`, `#stable-id-declarations` |
| plan | `…/plan.md` *(new)* | `#per-stage-front-matter` | `#stable-id-declarations` |
| tasks | `…/tasks.yml` | `#per-stage-front-matter` | `#stable-id-declarations` |
| evidence | `…/evidence.yml` | `#per-stage-front-matter` | `#evidenceyml-declarations` |

## Per-diagnostic mapping (authoritative for `/speckit-tasks`)

`…/` abbreviates `docs/examples/lifecycle-artifacts/`. Anchor column omits the
`docs/reference/authoring-contracts.md` prefix. `*(agg)*` = grammar-rooted aggregate block.

| Diagnostic id | Example | Anchor |
|---------------|---------|--------|
| `malformedCharterFrontMatter` | `…/charter.md` | `#per-stage-front-matter` |
| `charterIdentityMismatch` | `…/charter.md` | `#per-stage-front-matter` |
| `missingSpecificationIntent` | `…/spec.md` | `#specify---input-intent-facts` |
| `malformedSpecificationFrontMatter` | `…/spec.md` | `#per-stage-front-matter` |
| `malformedSpecificationFacts` | `…/spec.md` | `#stable-id-declarations` |
| `duplicateSpecificationId` | `…/spec.md` | `#stable-id-declarations` |
| `missingSpecificationId` | `…/spec.md` | `#stable-id-declarations` |
| `unknownSpecificationReference` | `…/spec.md` | `#stable-id-declarations` |
| `specificationIdentityMismatch` | `…/spec.md` | `#per-stage-front-matter` |
| `missingClarificationAnswer` | `…/clarifications.md` | `#clarify-decision-tag-resolution` |
| `unresolvedBlockingAmbiguity` | `…/clarifications.md` | `#clarify-decision-tag-resolution` |
| `malformedClarificationFrontMatter` | `…/clarifications.md` | `#per-stage-front-matter` |
| `duplicateClarificationId` | `…/clarifications.md` | `#stable-id-declarations` |
| `unknownClarificationReference` | `…/clarifications.md` | `#stable-id-declarations` |
| `unsafeDecisionChange` | `…/clarifications.md` | `#clarify-decision-tag-resolution` |
| `clarificationIdentityMismatch` | `…/clarifications.md` | `#per-stage-front-matter` |
| `malformedChecklistFrontMatter` | `…/checklist.md` | `#per-stage-front-matter` |
| `duplicateChecklistId` | `…/checklist.md` | `#stable-id-declarations` |
| `unknownChecklistSourceReference` | `…/checklist.md` | `#stable-id-declarations` |
| `failedChecklistPrerequisite` *(agg)* | `…/checklist.md` | `#acceptance-coverage-line` |
| `checklistIdentityMismatch` | `…/checklist.md` | `#per-stage-front-matter` |
| `malformedPlanFrontMatter` | `…/plan.md` | `#per-stage-front-matter` |
| `duplicatePlanId` | `…/plan.md` | `#stable-id-declarations` |
| `unknownPlanSourceReference` | `…/plan.md` | `#stable-id-declarations` |
| `failedPlanPrerequisite` *(agg)* | `…/plan.md` | `#stable-id-declarations` |
| `planIdentityMismatch` | `…/plan.md` | `#per-stage-front-matter` |
| `malformedTasksArtifact` | `…/tasks.yml` | `#per-stage-front-matter` |
| `duplicateTaskId` | `…/tasks.yml` | `#stable-id-declarations` |
| `unknownTaskSourceReference` | `…/tasks.yml` | `#stable-id-declarations` |
| `unknownTaskDependency` | `…/tasks.yml` | `#stable-id-declarations` |
| `taskDependencyCycle` | `…/tasks.yml` | `#stable-id-declarations` |
| `doneTaskMissingEvidence` | `…/tasks.yml` | `#stable-id-declarations` |
| `skippedTaskMissingRationale` | `…/tasks.yml` | `#stable-id-declarations` |
| `failedTasksPrerequisite` *(agg)* | `…/tasks.yml` | `#stable-id-declarations` |
| `tasksIdentityMismatch` | `…/tasks.yml` | `#per-stage-front-matter` |
| `evidence.malformedEvidenceArtifact` | `…/evidence.yml` | `#evidenceyml-declarations` |
| `evidence.duplicateEvidenceId` | `…/evidence.yml` | `#evidenceyml-declarations` |
| `evidence.unknownReference` | `…/evidence.yml` | `#evidenceyml-declarations` |
| `evidence.missingRequiredEvidence` *(agg)* | `…/evidence.yml` | `#evidenceyml-declarations` |
| `evidence.undisclosedSyntheticEvidence` | `…/evidence.yml` | `#evidenceyml-declarations` |
| `evidence.missingDeferralRationale` | `…/evidence.yml` | `#evidenceyml-declarations` |
| `evidence.missingRequiredSkill` *(agg)* | `…/evidence.yml` | `#evidenceyml-declarations` |
| `evidence.unsupportedResultState` | `…/evidence.yml` | `#evidenceyml-declarations` |
| `evidence.unsafeUpdate` | `…/evidence.yml` | `#evidenceyml-declarations` |
| `evidence.identityMismatch` | `…/evidence.yml` | `#per-stage-front-matter` |
| `verify.missingRequiredTest` *(agg)* | `…/evidence.yml` | `#evidenceyml-declarations` |

**Notes**
- **Warning-severity `stale*` diagnostics are excluded** (`staleChecklistResult`, `stalePlanDecision`,
  `staleTask`, `verify.staleRequiredTest`, `evidence.staleEvidence*`): the spec scopes the pointer
  requirement to **error-severity** blocking diagnostics only. The registry contains 46 error ids.
- Every row cites **both** an example and an anchor (all seven stages have an example after
  FR-004), so the both-when-both-exist rule (clarify Q2) applies uniformly.
- The `*IdentityMismatch` rows are retained in-set and pointed at the per-stage front-matter
  grammar (the fix is a front-matter/location correction the example demonstrates), per research
  Decision 3's open item — resolved here as **include**.
- Anchors used: `#per-stage-front-matter`, `#specify---input-intent-facts`,
  `#stable-id-declarations`, `#clarify-decision-tag-resolution`, `#acceptance-coverage-line`,
  `#evidenceyml-declarations`. All exist in the current `authoring-contracts.md`; the guard verifies
  this (a task must confirm the GitHub slug of "## `evidence.yml` declarations" is
  `evidenceyml-declarations` and of "## `specify --input` intent facts" is
  `specify---input-intent-facts`, adjusting the constant if the slugifier differs).

## New shipped example artifacts (FR-004)

| File | Validated by | Front matter `stage` / terminal `status` | Must contain |
|------|--------------|------------------------------------------|--------------|
| `charter.md` | Commands charter front-matter parser (`Commands.Tests`) | `charter` / authored charter status | schemaVersion, workId, title, stage, changeTier, status front matter; scope/policy sections |
| `spec.md` | `Specification.parseSpecificationFacts` (`Artifacts.Tests`) | `specify` / spec-ready status | required sections; stable US/FR/AC ids; `#specify---input`-shaped intent facts; zero blocking diagnostics |
| `plan.md` | `Plan.parsePlanFacts` (`Artifacts.Tests`) | `plan` / plan-ready status | plan front matter incl. `sourceSpec`/`sourceClarifications`/`sourceChecklist`; valid PD/PC/source ids; zero blocking diagnostics |

Each new example carries the established header comment linking back to
`docs/reference/authoring-contracts.md` and the relevant `fs-gg-sdd-*` stage skill, matching the
four existing examples.

## State transitions

None. Pure construction-time mapping; no lifecycle state.
