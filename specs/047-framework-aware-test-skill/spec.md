# Feature Specification: Framework-aware required test skill

**Feature Branch**: `047-framework-aware-test-skill`

**Created**: 2026-06-30

**Status**: Draft

**Input**: User description: "start the next sdd owned item on the coordination board." → Coordination board item **FS-GG/FS.GG.SDD#42** (Ready, P2 SDD, Lifecycle): *Generated task metadata declares required test skill "xunit" the product doesn't use (Expecto).*

## Context

SDD's task-generation tooling stamps every generated verification-obligation task with a
hard-coded required test skill of `xunit`. A product whose test project uses a different
framework (the `rendering` scaffold uses **Expecto**) is therefore pointed at the wrong
test framework by its own generated task metadata, and its verification-readiness skill
obligation (`evidence.missingRequiredSkill`) is keyed to a skill that does not describe the
product. The fix belongs to SDD because the offending value is generated downstream by
SDD's task tooling, not by any product source (confirmed by the routing in #42).

SDD is the **generic** lifecycle product: it must not assume — or hard-code — any single
test framework. The required test skill on generated tasks must reflect the product's
actual test framework, and when that framework is not declared it must fall back to a
framework-neutral skill rather than misdirecting the author to one specific framework.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Author of a non-xUnit product gets the right test skill (Priority: P1)

An author works an SDD-managed product whose test project uses Expecto. They run the
lifecycle through task generation and inspect the generated task metadata for a
verification-obligation task. The required test skill names a capability consistent with
their product's actual test framework — never `xunit` — so following the obligation leads
them to the framework they actually use.

**Why this priority**: This is the reported defect (#42) and the core value: authors are no
longer pointed at a test framework their product does not use. Without it, every non-xUnit
SDD product carries misleading verification guidance.

**Independent Test**: Generate tasks for a product whose declared test framework is Expecto
and assert the verification-obligation task's `requiredSkills` contains the Expecto-matched
skill and contains no `xunit` token.

**Acceptance Scenarios**:

1. **Given** a product that declares its test framework as Expecto, **When** task generation
   runs, **Then** each verification-obligation task's required test skill corresponds to the
   declared framework and no generated task metadata contains `xunit`.
2. **Given** the same product, **When** verification readiness is evaluated, **Then** the
   `evidence.missingRequiredSkill` obligation is keyed to the framework-matched skill (so an
   author who supplies the matching skill satisfies it).

---

### User Story 2 - Undeclared framework yields a neutral, non-misleading skill (Priority: P2)

An author works a product that has not declared any test framework. Task generation still
produces a satisfiable verification-obligation task, but its required test skill is
framework-neutral — it names no specific framework, so the author is never misdirected to
xUnit (or any other framework) they may not use.

**Why this priority**: Most products will not have an explicit framework declaration at
first. The safe default must never reintroduce the #42 defect by assuming a framework.

**Independent Test**: Generate tasks for a product with no declared test framework and assert
the verification-obligation task's required test skill is the framework-neutral skill and
contains no framework-specific token (`xunit`, `expecto`, etc.).

**Acceptance Scenarios**:

1. **Given** a product with no declared test framework, **When** task generation runs,
   **Then** the verification-obligation task carries a framework-neutral required test skill
   and no framework-specific token appears in the generated task metadata.

---

### User Story 3 - No regression to non-test task skills or determinism (Priority: P3)

An author relies on the rest of the generated work model being unchanged. Only the
test-framework-specific skill changes; the SDD-process skills on the other six generated
task categories (implementation, contract, migration, generated-view, and deferral — whose
skill is `traceability`) are untouched, and re-running generation on identical inputs still
produces byte-identical metadata.

**Why this priority**: SDD's generated artifacts are machine contracts under deterministic
and golden tests; the fix must be surgical and reproducible.

**Independent Test**: Diff the generated work model before and after the change for the same
inputs — the only differences are in the verification-obligation tasks' test skill; all other
task categories' `requiredSkills` are identical; a second generation run is byte-identical.

**Acceptance Scenarios**:

1. **Given** identical lifecycle inputs, **When** task generation runs twice, **Then** the
   generated task metadata is byte-identical across runs.
2. **Given** a product, **When** task generation runs, **Then** the `requiredSkills` of
   requirement, plan-decision, contract, migration, generated-view, and deferral tasks are
   unchanged from prior behavior.

---

### Edge Cases

- **Declared framework value is itself a recognizable test framework** (e.g. `expecto`,
  `nunit`): the required test skill reflects that declared framework.
- **Declared framework value is unrecognized/custom**: SDD trusts the author's declaration
  and emits a skill derived from the declared value rather than inventing or validating a
  framework — SDD does not maintain a closed list of "approved" frameworks.
- **No declaration present**: framework-neutral skill (User Story 2), never `xunit`.
- **Empty/blank declaration**: treated as no declaration (framework-neutral skill), not as a
  framework named "".
- **Existing readiness/work models generated before this change**: regenerating (refresh /
  re-running task generation) updates the skill; the feature does not silently rewrite
  already-emitted artifacts outside a generation run.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The required test skill on a generated verification-obligation task MUST reflect
  the product's actual test framework and MUST NOT be a hard-coded framework the product does
  not use.
- **FR-002**: When the product's test framework is declared through SDD-owned configuration,
  the system MUST emit a required test skill that corresponds to that declared framework.
- **FR-003**: When no test framework is declared, the system MUST emit a single
  framework-neutral required test skill (one that names no specific framework) instead of
  defaulting to `xunit` or any other framework.
- **FR-004**: The system MUST NOT emit `xunit` — or any other specific framework token — as a
  required skill for a product that does not declare/use it.
- **FR-005**: The change MUST be confined to the test-framework-specific skill; the non-test
  SDD-process skills emitted on the other generated task categories (implementation, contract,
  migration, generated-view, and deferral — the last carrying the `traceability` skill) MUST
  remain unchanged.
- **FR-006**: Generated task metadata MUST remain deterministic — stable ordering and
  byte-stable output for a given set of inputs and a given declared framework — preserving the
  JSON/work-model machine contract and golden fixtures (which MUST be updated to the new
  expected values as part of this work).
- **FR-007**: The declared-test-framework signal MUST be SDD-owned and generic; it MUST NOT
  introduce any rendering-, provider-, or template-specific package id, template id, path, or
  docs URL into generic SDD behavior.
- **FR-008**: Verification-readiness evaluation (the `evidence.missingRequiredSkill`
  obligation) MUST evaluate against the framework-matched (or neutral) skill, so an author who
  supplies the skill matching their actual framework satisfies the obligation.
- **FR-009**: The behavior MUST be observable consistently across the report projections
  (`--json`/default, `--text`, `--rich`) without changing the JSON contract bytes beyond the
  intended skill value.

### Key Entities *(include if data involved)*

- **Verification-obligation task**: a generated work-model task created per plan verification
  obligation; the sole task category that today carries the hard-coded `xunit` test skill.
- **Required test skill**: the skill/capability tag in a task's `requiredSkills` that names the
  test capability an author must have; the subject of this change.
- **Declared test framework**: a generic, SDD-owned signal of the product's test framework,
  read by task generation to choose the required test skill; absent for products that have not
  declared one.
- **Work model / tasks metadata**: the generated artifacts (`readiness/<id>/work-model.json`
  and the tasks artifact) that carry `requiredSkills`; machine contracts under golden tests.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a product whose declared test framework is Expecto, 100% of generated
  verification-obligation tasks declare the Expecto-matched required test skill, and the count
  of `xunit` tokens in the generated task metadata is **0**.
- **SC-002**: For a product with no declared test framework, 100% of generated
  verification-obligation tasks carry the framework-neutral required test skill and **0**
  framework-specific tokens.
- **SC-003**: For a product that declares any test framework, the required test skill in every
  verification-obligation task equals the skill derived from that declared framework.
- **SC-004**: The `requiredSkills` of all non-test task categories are unchanged versus prior
  behavior (verified by golden comparison), i.e. **0** unintended skill diffs outside
  verification-obligation tasks.
- **SC-005**: Re-running task generation on identical inputs produces byte-identical task
  metadata (determinism preserved across runs).
- **SC-006**: The scenario in FS-GG/FS.GG.SDD#42 (the `rendering`/Expecto scaffold) no longer
  points an author at xUnit through generated task metadata.

## Assumptions

- The product's test framework is declared (or will be declarable) through SDD-owned
  configuration consistent with `.fsgg/` slot ownership (ADR-0005) — `.fsgg/project.yml` is the
  expected home. The exact field name and schema-version handling are a planning concern, not
  fixed by this spec.
- When a framework is undeclared, a framework-neutral test skill (e.g. a generic
  "automated tests" capability tag) keeps the verification obligation meaningful without
  misdirecting the author; the precise neutral token is settled in planning.
- Only the verification-obligation task category currently hard-codes a test framework
  (`xunit`, at the task-generation seam); all other generated task skills are SDD-process
  skills and are out of scope.
- This work item is the generic SDD half of the cross-repo ask; FS.GG.Rendering's Feature 219
  FR-008 is already satisfied by routing the fix here, so no Rendering-side change is required
  by this spec. The related `.github#75` tutorial-docs xUnit-vs-Expecto papercut is a distinct
  artifact and out of scope.
- No existing test asserts the generated verification-obligation task's `requiredSkills`
  today, so this work adds those assertions (and any work-model golden carrying them) rather
  than rewriting pre-existing obligation-skill goldens. The only `xunit` tokens currently in
  the test tree are *parser-input* fixtures at
  `tests/FS.GG.SDD.Artifacts.Tests/TasksArtifactTests.fs` and
  `tests/FS.GG.SDD.Artifacts.Tests/VerificationViewTests.fs`; those are swapped to an
  SDD-neutral token as part of implementation. This is expected churn, not a regression.
