# Research: Risk-Scaled SDD

## Decision 1: Confidence hierarchy

**Decision**: Rank evidence by decision relevance: exact candidate identity, observed execution outcome, independently authored controls, and independent critic verdict. Keep synthetic provenance as descriptive metadata.

**Rationale**: A fixture may be synthetic while the test execution and candidate binding are real. Conversely, `synthetic: false` is author-controlled and proves nothing by itself. The current CLI already defaults to requiring observed runner receipts, which is the stronger boundary.

**Alternatives considered**: Remove provenance entirely (loses useful context); preserve synthetic as a blocking state (keeps the misleading proxy); trust authored pass declarations (permits fabrication).

## Decision 2: Three risk profiles

**Decision**: Use `small`, `normal`, and `high`. Promote on the highest-impact changed path; unknown classification is high.

**Rationale**: Three levels are enough to separate prose/metadata changes, ordinary product work, and protected boundaries without recreating a policy language. The selection is explainable and locally testable.

**Alternatives considered**: Per-check scoring (opaque and easy to game); two levels (forces ordinary source work into either docs-light or release-heavy); user-declared risk alone (self-classification can miss impact).

## Decision 3: Stable contexts, conditional work

**Decision**: Keep existing required job names and make the job report a successful, inspectable small-profile decision while skipping irrelevant expensive steps.

**Rationale**: GitHub branch protection waits forever when a required context is not produced. Job-level path filters are therefore unsafe for required workflows.

**Alternatives considered**: Workflow path filters (missing required context); removing required contexts (weakens the boundary); separate optional workflows only (does not reduce the existing required gate).

## Decision 4: Compatibility before deletion

**Decision**: Retain legacy artifact fields, dispositions, and parsers in this release. New semantics ignore synthetic status as a readiness override when valid observation exists. Removal requires a later schema-major migration.

**Rationale**: Existing repositories and Governance consumers read these fields. Additive behavioral modernization can land without a fleet-wide atomic migration.

**Alternatives considered**: Delete evidence fields and lifecycle commands now (breaking and cross-repo); bulk rewrite existing work packages (cost with no user value).

## Measured baseline

- A recent complete work item contains seven authored files and seven readiness files.
- The public lifecycle exposes ten sequential stages plus cross-cutting commands.
- Five workflows start on a typical pull request; `gate.yml` contains five jobs.
- Recent full gate runs settle in roughly 9–10 minutes.
- The full local tier covers about 1,787 tests and is documented as 2–3 minutes; the fast tier covers about 1,576 in-process tests in roughly 20 seconds.
- 117 source, test, documentation, and generated-skill files mention synthetic provenance, demonstrating that the proxy has spread well beyond one flag.
