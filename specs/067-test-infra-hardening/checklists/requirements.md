# Specification Quality Checklist: Test-infrastructure hardening

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-07-03
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Implementation notes (post-build, T035)

- **Scope widened during planning** (recorded in `research.md`): two clusters live in
  product code `src/FS.GG.SDD.Validation/ValidationRunner.fs` (temp cleanup + genuine
  env cells), not just tests. Kept Tier-2/contract-neutral — all `.fs`-internal, no
  `.fsi`/baseline/`validation-report`-schema change (diff-gate confirms empty).
- **FR-009 narrowed**: "make library Rich differ from plain text" would reverse research
  Decision 6 (add a Spectre dependency to the validation library = Tier-1). Preserved the
  intentional `Rich→text` degradation; the Rich ANSI guarantee stays covered by
  `ValidateCommandTests`. Delivered: color-disabling cells now genuinely set `NO_COLOR`/
  `TERM=dumb`; `withPerturbedHost` now varies cwd.
- **SC-004 refined**: strict "0 after a single run" is unreachable in xUnit v2 (VSTest kills
  process-exit before a big delete completes). Delivered a self-healing pid-tagged startup
  sweep → residue bounded to ≤1 run, never accumulating (pre-feature: 567 orphaned dirs).
- **US2 outcome**: all 106 orphan manifests were pure unread documentation (redundant with
  the Validation harness) → deleted; `deterministic-report` retained (used dir) + a guard.
- Final state: full suite **877 passed / 3 skipped** (registry-gated), Release build
  **0 warnings** under the ratchet, baselines/`.fsi`/golden fixtures byte-identical.

## Notes

- This is a Tier-2 internal test-infrastructure feature; the "users" are developers
  and CI. Named artifacts (`PATH`, `FSGG_UPDATE_BASELINE`, fixture-manifest paths)
  appear because they *are* the subject matter of the work, not as implementation
  choices — they identify the concrete defects being remediated. Test-mechanism
  choices (e.g. exact xUnit collection grouping) are deferred to the plan and noted
  as such in Assumptions.
- FR-006 / FR-012 / SC-007 encode the hard invariant: no observable product
  contract, baseline, or golden fixture changes. This is the primary guardrail for
  the plan and the analyze stage.
- One narrow judgment call is deliberately left to planning rather than raised as a
  clarification: per-manifest wire-in-vs-delete of the 106 orphans. The spec fixes
  the *rule* (no real coverage lost, no dead files kept); the per-file decision is
  a plan/tasks concern, not a spec ambiguity.
