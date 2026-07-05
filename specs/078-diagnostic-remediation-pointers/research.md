# Phase 0 Research: Diagnostic remediation pointers

All Technical-Context unknowns are resolved below. No `NEEDS CLARIFICATION` remain.

## Decision 1 — Pointer lives in the existing `Correction` field (no new field)

- **Decision**: Append the remediation pointer to the diagnostic's existing `Correction` string.
- **Rationale**: The `Diagnostic` record already carries `Correction` as the "how to fix" channel;
  it flows through every projection (`--json`/`--text`/`--rich`) and through `fsgg-sdd
  lint`/`--explain` with zero new plumbing (FR-003, FR-009). Adding a `RemediationPointer` field
  would bump the JSON automation contract shape (SC-005 forbids), require serialization + baseline
  changes, and gain nothing an author-visible string can't carry.
- **Alternatives considered**: (a) a new structured `pointer` field — rejected (contract churn,
  no consumer needs it structured yet); (b) a `RelatedIds` entry — rejected (`RelatedIds` is for
  artifact ids, not doc anchors, and isn't rendered as guidance).

## Decision 2 — Anchor resolution: live-slugify the heading, guard-verified

- **Decision**: A cited grammar anchor is the GitHub-style slug of a real `##`/`###` heading in
  `docs/reference/authoring-contracts.md` (lowercase, spaces→`-`, punctuation dropped, e.g.
  "## Clarify decision-tag resolution" → `clarify-decision-tag-resolution`). The pointer strings
  are embedded as **constants** in `RemediationPointers`; the **guard test** recomputes the slug
  set from the live doc headings and asserts every cited anchor is in it.
- **Rationale**: Keeps product code I/O-free (constants only, Principle V) while the test is the
  drift guard (FR-006 / US3): rename a heading and the guard goes red until the citation or the
  heading moves. Mirrors the existing `ExampleArtifactsContractTests` philosophy (validate against
  the live artifact so references can't rot).
- **Alternatives considered**: (a) resolve anchors at runtime from the doc — rejected (filesystem
  I/O in the pure diagnostic path, and the doc may not ship next to the CLI); (b) a hand-maintained
  anchor allow-list separate from the doc — rejected (a second source of truth that itself rots).

## Decision 3 — Covered set: enumerated in the registry, includes grammar-rooted aggregates

- **Decision**: `RemediationPointers` enumerates the covered diagnostic ids explicitly. The set is
  the authoring-grammar diagnostics **plus** the grammar-rooted aggregate readiness blocks (clarify
  Q1). Pure sequencing/config/tool-defect/scaffold/doctor blocks are excluded (FR-008). The
  enumerated set below is authoritative for `/speckit-tasks`; each row's `(example, anchor)` is
  finalized in `data-model.md`.
- **Covered set (from `DiagnosticConstructors.fs`), by stage:**
  - **charter**: `malformedCharterFrontMatter`, `charterIdentityMismatch`
  - **specify**: `missingSpecificationIntent`, `malformedSpecificationFrontMatter`,
    `malformedSpecificationFacts`, `duplicateSpecificationId`, `missingSpecificationId`,
    `unknownSpecificationReference`, `specificationIdentityMismatch`
  - **clarify**: `missingClarificationAnswer`, `unresolvedBlockingAmbiguity`,
    `malformedClarificationFrontMatter`, `duplicateClarificationId`, `unknownClarificationReference`,
    `unsafeDecisionChange`, `clarificationIdentityMismatch`
  - **checklist**: `malformedChecklistFrontMatter`, `duplicateChecklistId`,
    `unknownChecklistSourceReference`, `staleChecklistResult`, `failedChecklistPrerequisite` *(agg)*,
    `checklistIdentityMismatch`
  - **plan**: `malformedPlanFrontMatter`, `duplicatePlanId`, `unknownPlanSourceReference`,
    `stalePlanDecision`, `failedPlanPrerequisite` *(agg)*, `planIdentityMismatch`
  - **tasks**: `malformedTasksArtifact`, `duplicateTaskId`, `unknownTaskSourceReference`,
    `unknownTaskDependency`, `taskDependencyCycle`, `staleTask`, `doneTaskMissingEvidence`,
    `skippedTaskMissingRationale`, `tasksIdentityMismatch`
  - **evidence**: `evidence.malformedEvidenceArtifact`, `evidence.duplicateEvidenceId`,
    `evidence.unknownReference`, `evidence.missingRequiredEvidence` *(agg)*,
    `evidence.undisclosedSyntheticEvidence`, `evidence.missingDeferralRationale`,
    `evidence.missingRequiredSkill` *(agg)*, `evidence.unsupportedResultState`,
    `evidence.unsafeUpdate`, `evidence.identityMismatch`
  - **verify**: `verify.missingRequiredTest` *(agg)*, `verify.staleRequiredTest`
- **Rationale**: An explicit list makes FR-001's "enumerated set" a real object the guard iterates,
  and makes coverage auditable. The `/speckit-clarify` boundary rule (pure `missing*Prerequisite`
  = out; `failed*Prerequisite`/`missingRequired*` = in) is applied above.
- **Alternatives considered**: infer the set by severity+substring heuristic — rejected (fragile,
  and FR-008 needs a precise out-set so untouched corrections are provably unchanged).
- **Open for `/speckit-plan` data-model refinement**: the `*IdentityMismatch` rows are "wrong work
  id / wrong location" blocks — arguably closer to config than grammar. Data-model Decision records
  whether each identity-mismatch row cites the per-stage front-matter grammar (recommended: yes,
  since the fix is a front-matter/location correction the example demonstrates) or is excluded.

## Decision 4 — `RemediationPointers` is internal to `FS.GG.SDD.Commands`

- **Decision**: New module `RemediationPointers` under `CommandReports/`, `internal`, with an
  `.fsi`. Tests reach it via the existing `InternalsVisibleTo("FS.GG.SDD.Commands.Tests")`.
- **Rationale**: It is a construction-time wiring detail for `DiagnosticConstructors` (itself
  `internal`), not a consumer-facing surface. Keeping it internal avoids a `FS.GG.Contracts`
  public-surface baseline change (Principle III) while still honoring the `.fsi`-first rule.
- **Alternatives considered**: put the map inline in `DiagnosticConstructors` — rejected (the guard
  test needs to iterate the covered set independently of constructing every diagnostic, and one
  registry keeps example/anchor edits in a single reviewable place).

## Decision 5 — Charter example validated in `Commands.Tests`; spec/plan in `Artifacts.Tests`

- **Decision**: `spec.md` and `plan.md` examples are validated by the public
  `Specification.parseSpecificationFacts` / `Plan.parsePlanFacts` inside the existing
  `ExampleArtifactsContractTests` (FS.GG.SDD.Artifacts.Tests). `charter.md` is validated by the
  charter front-matter parser, which lives in `FS.GG.SDD.Commands` — so its contract case is a new
  test in `FS.GG.SDD.Commands.Tests`.
- **Rationale**: There is no `Charter` module in `FS.GG.SDD.Artifacts` (charter is identity-only,
  parsed in `EarlyStageAuthoring`); `Artifacts.Tests` cannot reference `Commands`. Charter's example
  is therefore thinner — front matter + scope/policy sections — and validated where its parser lives.
- **Alternatives considered**: lift a charter parser into Artifacts to co-locate the test — rejected
  (out of scope; would be a larger refactor for no product benefit).

## Decision 6 — Deterministic pointer-suffix format

- **Decision**: One deterministic sentence appended after the existing correction, e.g.:
  `See the shipped example docs/examples/lifecycle-artifacts/clarifications.md and the grammar at
  docs/reference/authoring-contracts.md#clarify-decision-tag-resolution.` Repo-relative POSIX
  paths, no leading `./`, single space separators, `and` when both parts present, terminal period.
  Exact template fixed in `contracts/remediation-pointer.md`.
- **Rationale**: FR-007 determinism — no timestamps/absolute paths/env content, so goldens and the
  `validate` determinism matrix stay reproducible across OSes. A single template keeps the guard's
  extraction regex simple and the golden diffs uniform.
- **Alternatives considered**: markdown links `[example](path)` — rejected (noise in plain-text/JSON
  automation consumers; the raw path is more copy-pasteable in a terminal).

## Decision 7 — Golden / assertion update strategy

- **Decision**: Treat the covered corrections' new suffix as an expected golden delta. Update the
  ≤8 existing test files that assert covered correction substrings to expect the appended pointer;
  regenerate any JSON goldens that embed a covered correction. Non-covered corrections' goldens must
  be byte-unchanged (a diff on them is a regression signal for FR-008).
- **Rationale**: The change is intentional and bounded; the guard + per-stage correction tests
  pin the new text so the goldens are re-derived from one source of truth.
- **Alternatives considered**: assert only "contains a pointer" loosely everywhere — rejected for
  the representative per-stage tests (we want exact text pinned once), but acceptable as the guard's
  cross-cutting assertion shape.
