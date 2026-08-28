# Feature Specification: Risk-Scaled SDD

**Feature Branch**: `item/937-sdd-modernization`

**Created**: 2026-08-28

**Status**: Approved

**Input**: Modernize SDD and CI for AI-assisted delivery: reduce repeated lifecycle ceremony and synthetic-evidence emphasis, make exact-candidate execution and independent critic review primary, and retain strict controls at protected boundaries.

## User Scenarios & Testing

### User Story 1 - Proportionate lifecycle (Priority: P1)

A product worker classifies a change as small, normal, or high risk and receives the smallest workflow that still supports the decisions the change can affect.

**Why this priority**: Repeating the full artifact chain for every change consumes attention without adding confidence.

**Independent Test**: Classify representative documentation-only, ordinary product, and protected-boundary changes and confirm their required evidence and gates differ as specified.

**Acceptance Scenarios**:

1. **Given** a small change that cannot alter runtime or protected policy, **When** its risk is classified, **Then** concise intent, exact-candidate checks, and review are sufficient without regenerating every lifecycle stage.
2. **Given** an ordinary product change, **When** its risk is classified, **Then** specification, focused tests, candidate binding, and independent review are required while duplicated stage hand-offs are omitted.
3. **Given** an authority, release, migration, destructive, security, or public-contract change, **When** its risk is classified, **Then** the full relevant controls remain fail closed.

---

### User Story 2 - Confidence from observed outcomes (Priority: P1)

A reviewer judges confidence from exact-candidate execution, independently authored controls, and critic findings. Synthetic-versus-observed provenance remains inspectable metadata but does not independently determine readiness.

**Why this priority**: Provenance matters, but the current binary classification overstates fixture realism and understates candidate-bound execution and review.

**Independent Test**: Verify that a passing candidate-bound run can satisfy an obligation regardless of synthetic metadata, while stale, fabricated, failed, or unbound execution cannot.

**Acceptance Scenarios**:

1. **Given** a passing run receipt bound to the reviewed candidate, **When** evidence is evaluated, **Then** synthetic metadata is reported without downgrading the pass by itself.
2. **Given** a claimed pass without a valid observed receipt, **When** protected readiness is requested, **Then** readiness remains blocked.
3. **Given** a candidate change after evidence or review, **When** readiness is requested, **Then** stale candidate bindings are rejected.

---

### User Story 3 - Risk-selected CI (Priority: P2)

A contributor receives fast checks for low-impact changes and broader checks only when changed paths or declared impact can affect their subject.

**Why this priority**: The repository currently starts five pull-request workflows and a roughly ten-minute broad gate for changes that may only edit prose.

**Independent Test**: Feed representative changed-path sets to the classifier and exercise the corresponding workflow branches, including conservative fallback for unknown inputs.

**Acceptance Scenarios**:

1. **Given** documentation and lifecycle-authoring changes only, **When** CI runs, **Then** inexpensive integrity checks run and build, full suite, package, API, and formal-model checks do not.
2. **Given** ordinary source or test changes, **When** CI runs, **Then** locked restore, build, focused repository tests, and candidate review checks run.
3. **Given** protected-boundary paths or an indeterminate classification, **When** CI runs, **Then** the full gate runs.

### Edge Cases

- Renames and deletions are classified from both old and new paths.
- A missing comparison base, malformed classifier input, or unknown path fails conservatively to high risk.
- Existing work packages and evidence fields continue to parse without migration before use.
- Documentation that changes authority, release, migration, or CI policy is high risk despite being Markdown.
- Independent critic review remains an orchestration/merge-boundary responsibility; SDD records or consumes it without pretending self-review is independent.

## Requirements

### Functional Requirements

- **FR-001**: The lifecycle MUST define three risk profiles—small, normal, and high—with explicit, deterministic promotion rules.
- **FR-002**: Unknown or indeterminate impact MUST select the high-risk profile.
- **FR-003**: New work MUST be able to use one concise decision-bearing package instead of compulsory charter, clarification, checklist, plan, task, evidence, analysis, verification, and ship regeneration when those transitions add no new decision.
- **FR-004**: Existing lifecycle packages and persisted schema fields MUST remain readable without mandatory migration.
- **FR-005**: Synthetic provenance MUST remain representable and visible, but MUST NOT by itself make a passing, candidate-bound observed execution unsatisfied.
- **FR-006**: A claimed pass without a valid observed execution or durable record receipt MUST remain unsatisfied at protected readiness boundaries.
- **FR-007**: Candidate identity MUST bind execution and critic review; a changed candidate invalidates prior acceptance.
- **FR-008**: Small changes MUST require concise intent, relevant inexpensive checks, exact-candidate identity, and review, but MUST NOT require the full lifecycle artifact chain.
- **FR-009**: Normal changes MUST require specification, focused verification, candidate binding, and independent critic review.
- **FR-010**: Authority, release, migration, destructive, security, public-contract, build-policy, and CI-policy changes MUST use high-risk controls.
- **FR-011**: Pull-request CI MUST preserve stable required context names while selecting expensive work by risk.
- **FR-012**: CI classification MUST be testable locally and fail conservatively on missing, malformed, or unrecognized input.
- **FR-013**: User and agent guidance MUST describe evidence as concise, deduplicated, candidate-bound support for decisions rather than an artifact-production target.
- **FR-014**: The design MUST document measured before/after workflow cost and examples for all three profiles.

### Key Entities

- **Risk Profile**: The selected level (`small`, `normal`, or `high`), reasons, changed paths, and required control families.
- **Candidate Evidence**: An observed execution or durable record bound to a candidate identity, with outcome and provenance metadata.
- **Critic Decision**: An independent review verdict bound to the same candidate identity and invalidated when that identity changes.
- **Protected Boundary**: An operation whose impact requires fail-closed controls, including authority, release, migration, destructive, security, public-contract, and CI-policy changes.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A small change can be described and accepted with at most one authored decision package plus candidate-bound check and review receipts, rather than the current seven authored and seven readiness files measured on a recent work item.
- **SC-002**: Documentation-only pull requests avoid the roughly ten-minute full build/test/formal-model gate while retaining stable required status contexts.
- **SC-003**: Tests cover at least one small, normal, high, and indeterminate classification, with indeterminate input selecting high risk in 100% of cases.
- **SC-004**: Tests prove that synthetic metadata alone causes zero readiness downgrades for otherwise valid candidate-bound observed passes.
- **SC-005**: Tests continue to reject fabricated execution, stale candidate evidence, missing critic authority, and weakened protected-boundary/release controls.
- **SC-006**: Existing committed work packages parse and validate without bulk rewrites.

## Assumptions

- The coordination layer continues to own worker identity, claims, independent critic routing, and protected merge authorization.
- SDD owns lifecycle semantics, evidence representation, generated guidance, and reusable CI classification policy.
- Exact-candidate binding uses the repository's immutable commit identity; stronger provenance may be added without changing the risk model.
- This feature modernizes the default path while preserving compatibility fields; removal of legacy fields can occur only in a separately versioned migration.
