# Feature Specification: Extract shared CommandWorkflow seams

**Feature Branch**: `061-extract-shared-seams`

**Created**: 2026-07-02

**Input**: Remediation #7 of the 2026-07-02 code-quality & architecture review (§3.4).
"Quantified duplication across readiness-view writers, blocked-work-model fallbacks,
front-matter identity diagnostics, source-staleness, the rich-console builder, and the
MVU drive loop."

## Context

Resolves **FS-GG/FS.GG.SDD#71** (roadmap item from the 2026-07-02 review, child of the
remediation epic). The review quantified six classes of near-identical code that guarantee
structural drift the moment one copy is edited and the others are not:

1. **Readiness-view writers.** `verifyJson` (`HandlersVerify.fs`), `shipJson`
   (`HandlersShip.fs`), and `analysisJson` (`ViewGeneration.fs`) each open with the same
   preamble (`schemaVersion`…`generator`), a `sources` array differing only by a
   source-kind classifier, and repeat the same `generatedViews` / boundary-facts /
   `diagnostics` / `nextAction` sub-writers (~150 near-identical lines verify vs ship).
2. **Blocked-work-model fallback ×9.** The `(diagnostics, [view], effects)` a stage returns
   when its prerequisites are missing is duplicated verbatim across `HandlersEarly` (×5),
   `HandlersAnalyze`, `HandlersVerify`, `HandlersShip`, `HandlersEvidence`.
3. **Front-matter identity diagnostics ×10.** The three shared identity checks
   (schemaVersion / work-id / expected stage) that every authored artifact runs are copied
   across `ParsingEarly`/`ParsingMid`/`ParsingTasks` (~120 lines) with only the per-artifact
   diagnostic constructors differing.
4. **Source-snapshot staleness ×3.** The digest-comparison `List.exists` is duplicated
   across checklist/plan/tasks re-parsing.
5. **Rich-console builder ×3.** The Ansi/color/width Spectre.Console setup is copied across
   `Rendering.fs` (command + validation) and `RegistryValidate.fs`.
6. **MVU drive loop ×2.** `Program.fs`'s `interpretUntilIdle` + build-report loop is
   duplicated verbatim as `ValidationRunner.runRequest` — divergence changes
   validate-vs-CLI behavior invisibly.
7. **Per-stage read-effect lists.** The pre-work-model stages (charter → tasks) share one
   read-effect frame differing only by their growing set of authored-artifact reads.

This feature is a **behavior-preserving refactor**. No public JSON contract byte changes for
any view, no diagnostic message/id/order changes, no schema-version bump, and no change to
which files any command reads. The whole test suite is the safety net.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Readiness views cannot drift structurally (Priority: P1)

The analyze / verify / ship JSON views are emitted through shared writer helpers, so their
common envelope (preamble, sources, generatedViews, boundary facts, diagnostics, nextAction)
is single-sourced and byte-identical to today's output.

**Why this priority**: The three views are the machine contract other tooling consumes; a
silent structural drift between them is the highest-consequence duplication in the set.

**Independent Test**: The existing analyze/verify/ship JSON golden and determinism tests
pass unchanged.

### User Story 2 - Shared lifecycle helpers are single-sourced (Priority: P2)

The blocked-work-model fallback, the front-matter identity checks, and the source-staleness
test each live in one helper the stages call, so a change lands in one place.

**Why this priority**: These are the highest-count duplications (9 / 10 / 3 sites); a fix or
diagnostic-wording change today requires editing every copy.

**Independent Test**: Every stage's blocked-prerequisite, identity-mismatch, and
stale-source test passes unchanged; diagnostic ids, messages, and ordering are identical.

### User Story 3 - One run loop and one rich-console builder (Priority: P3)

The CLI and the validation harness drive a command to its report through one shared
`driveToReport`; every rich sink builds its console through one `createCappedConsole`.

**Why this priority**: The drive-loop divergence is a correctness risk (validate could
diverge from the CLI invisibly); the rich-console copy is presentation-only.

**Independent Test**: CLI command output, validation-harness output, and rich-degradation
tests pass unchanged.

## Requirements *(mandatory)*

- **FR-001**: The analyze/verify/ship writers MUST emit byte-identical JSON to the current
  output, produced through shared preamble/sources/generatedViews/boundary/diagnostics/
  nextAction writer helpers. (covers AC-001)
- **FR-002**: A single `blockedWorkModelPlan` helper MUST produce the blocked-work-model
  `(diagnostics, [view], effects)` for every stage that computes a work model. (covers AC-002)
- **FR-003**: A single `frontMatterIdentityDiagnostics` helper MUST produce the shared
  schemaVersion/work-id/stage identity diagnostics for every authored artifact, preserving
  each artifact's diagnostic constructors, messages, and ids. (covers AC-003)
- **FR-004**: A single `sourceDigestsStale` helper MUST implement the source-snapshot
  staleness test for checklist/plan/tasks. (covers AC-004)
- **FR-005**: A single `driveToReport` in the Commands project MUST be the run loop used by
  both the CLI entry point and the validation harness. (covers AC-005)
- **FR-006**: A single `createCappedConsole` MUST build every rich Spectre.Console sink. (covers AC-006)
- **FR-007**: The pre-work-model read-effect lists (charter → tasks) MUST be produced by one
  shared frame; the later generators whose read orders genuinely differ are left unchanged. (covers AC-007)

## Acceptance Criteria *(mandatory)*

- **AC-001**: analyze/verify/ship JSON golden + determinism tests pass unchanged.
- **AC-002**: All blocked-prerequisite tests pass; the fallback exists in exactly one place.
- **AC-003**: All identity-mismatch / malformed-front-matter tests pass unchanged.
- **AC-004**: All stale-source tests pass unchanged.
- **AC-005**: CLI and validation-harness command tests pass unchanged.
- **AC-006**: Rich-rendering / degradation tests pass unchanged.
- **AC-007**: charter/clarify/checklist/plan/tasks command tests pass unchanged.

## Out of Scope

- The analyze/evidence/verify/ship/agents/refresh read-effect lists whose read orders
  genuinely differ (not a clean shared frame) — left as-is by design (FR-007).
- The larger architectural items tracked separately (`#72` CommandReports split, `#76`
  de-AutoOpen / DU-ify string states / shared readiness-envelope schema).
