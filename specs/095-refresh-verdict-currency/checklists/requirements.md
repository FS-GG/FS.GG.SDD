# Specification Quality Checklist: `refresh` Reports True Facts About the Committed Ship Verdict

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-07-08
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

## Notes

- Items marked incomplete require spec updates before `/speckit-clarify` or `/speckit-plan`.

### Validation record (iteration 1)

**"No implementation details" — judged PASS with a documented exception.** This is a
defect-correction feature in a CLI whose *output contract* is the user-facing surface. The spec cites
file/line anchors (`HandlersRefresh.fs:527`) and names existing identifiers (`parseShipView`,
`ViewCurrencyClass`, the three `refresh.*` diagnostic ids). These are not implementation *choices* —
they are the **observable contract** (diagnostic ids and `generatedViews[].currency` values are the
machine-readable API that FR-001..FR-015 constrain) and the **evidence** that each defect is real.
Suppressing them would make the requirements unverifiable and the defect claims unfalsifiable. This
matches the house style of `specs/094-surface-version-bump/spec.md`. The line anchors were verified
against `39fa3e5`; two were corrected during authoring
(`DiagnosticConstructors.fs:946`, `HandlersRefresh.fs:631`).

**Success criteria technology-agnostic — PASS.** SC-001..SC-007 are stated over artifact states and
reported words, not over functions or types. SC-001's 10-cell matrix is the state space, not a test
framework.

**Clarifications resolved without user round-trip.** Four ambiguities (AMB-001..AMB-004) were
resolved from the code itself and recorded in §Clarifications. None met the bar for a
[NEEDS CLARIFICATION] marker: each had exactly one defensible answer once the source was read
(notably AMB-002, where the existing `| _, Some _ -> Blocked` arm already encodes the right meaning,
and AMB-004, where F# match totality forecloses deletion).

**Scope bounded — PASS.** The §Out of Scope section explicitly excludes the three adjacent temptations
(schema-validating `analysis.json`/`verify.json`, the redundant `List.sort`, the gitignore depth-1
gap), each with the reason it is excluded, so the plan cannot silently widen the touch-set declared on
FS.GG.SDD#188.

**Result: 16/16 pass. Ready for `/speckit-plan`.**
