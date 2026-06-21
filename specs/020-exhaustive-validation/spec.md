# Feature Specification: Scheduled Exhaustive Validation of Broad Matrices

**Feature Branch**: `020-exhaustive-validation`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User description: "start the next item on the implementation plan."

> **Next-item resolution**: The SDD-owned roadmap in
> `docs/initial-implementation-plan.md` is delivered through Phase 9 (commands,
> bootstrap/migration, agent guidance, refresh) and the SDD slice of Phase 13
> (release/versioning/compatibility/schema-reference/baselines in
> `018-release-readiness`, rich rendering in `019-spectre-rendering`). The single
> remaining SDD-owned roadmap item is Phase 13's **"Add scheduled exhaustive
> validation for broad matrices"**, which `018-release-readiness` explicitly
> deferred ("scheduled exhaustive CI validation matrices are not delivered by this
> feature ... the CI matrix is an operational follow-on"). This feature delivers
> that slice. Governance-owned release rules, gate schemas, and enforcement remain
> out of scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Catch Combinatorial Regressions Before Release (Priority: P1)

A maintainer or release owner runs an exhaustive validation that exercises the
full cross-product of SDD's broad matrices — every public command across every
output projection over a representative set of work-item states, every public
generated view and command-output for determinism, and every documented public
contract for baseline conformance — instead of the representative samples the
cheap inner loop runs. They receive a single deterministic report that names
exactly which matrix and which cell (command × projection × state, or
contract × environment) failed, so a regression that only appears in an
uncommon combination is caught before a release rather than by a consumer.

**Why this priority**: The inner loop and per-change checks deliberately sample
representative cases to stay cheap (the `016` smoke runs one happy path; `018`
locks representative shapes). Combinatorial regressions — a projection that
diverges only for a mid-lifecycle work state, a view that is nondeterministic
only under a particular locale — slip past sampling. Exhaustive scheduled
coverage is the backstop that makes the broad surface trustworthy at release.

**Independent Test**: Run the exhaustive validation against the repository,
seed a deliberate regression in one command/projection/state cell, and confirm
the report fails that exact cell with an actionable diagnostic naming the
matrix, cell coordinates, and the affected contract/artifact, while all other
cells pass.

**Acceptance Scenarios**:

1. **Given** a clean repository, **When** the exhaustive validation runs over
   all declared matrices, **Then** it produces a deterministic report with a
   recorded pass/fail/skipped status for every cell of every matrix and exits
   non-zero if any cell failed.
2. **Given** a public generated readiness view that is regenerated twice over
   identical source inputs, **When** the determinism matrix runs, **Then** the
   two outputs are byte-identical and the cell passes; if they differ, the cell
   fails with a diff naming the view.
3. **Given** a rich-rendered command output produced in a non-interactive or
   `NO_COLOR`/`TERM=dumb` environment, **When** the determinism/degradation
   matrix runs, **Then** the validation confirms zero ANSI styling and that the
   deterministic JSON bytes, stream routing, and exit code are unchanged from the
   default projection.
4. **Given** a public contract documented in the release schema reference,
   **When** the baseline-conformance matrix runs, **Then** the validation
   confirms a locking baseline exists and a real produced artifact conforms, and
   reports the contract as not-validated (never a silent pass) if either is
   missing.

---

### User Story 2 - No Public Surface Escapes Coverage (Priority: P2)

A maintainer adds or changes a public command, generated view, or output
contract. The exhaustive validation detects any public surface that no declared
matrix covers and reports it as a coverage gap, so a new surface cannot silently
escape validation by simply not being listed.

**Why this priority**: Exhaustive coverage is only trustworthy if "not covered"
is a visible finding rather than an omission that reads as a pass. This guards
the matrices themselves against drift as the lifecycle surface evolves.

**Independent Test**: Add a public command or generated view that no matrix
references, run the exhaustive validation, and confirm it reports a coverage-gap
finding identifying the uncovered surface rather than passing by absence.

**Acceptance Scenarios**:

1. **Given** a public command/view/contract that no matrix cell covers, **When**
   the exhaustive validation runs, **Then** it emits a coverage-gap finding
   naming the uncovered surface and the report does not pass.
2. **Given** a matrix that names a contract or command that no longer exists in
   the real public surface, **When** the validation reconciles declared coverage
   against the real surface, **Then** the stale matrix entry is reported as a
   detectable failure and the real produced surface is treated as authoritative.

---

### User Story 3 - Keep the Inner Loop Cheap (Priority: P3)

A contributor authoring lifecycle artifacts locally is never required to run the
exhaustive validation as part of the fast loop. The exhaustive validation runs
on a schedule or on demand and is separate from the per-change checks, so
adopting it does not slow local authoring or the existing fast lifecycle
commands.

**Why this priority**: A central project constraint is that local authoring stays
cheap while protected coverage stays broad. The value of exhaustive validation
must not come at the cost of the inner loop.

**Independent Test**: Run the existing fast lifecycle commands and the per-change
checks and confirm their behavior and runtime are unchanged and that none of
them require or trigger the exhaustive validation; confirm the exhaustive
validation can be invoked separately on demand and on a schedule.

**Acceptance Scenarios**:

1. **Given** the existing inner-loop and per-change checks, **When** this feature
   is adopted, **Then** those checks gain no required exhaustive step and their
   behavior and runtime are unchanged.
2. **Given** the exhaustive validation, **When** it is invoked on demand or by a
   schedule, **Then** it runs to completion and produces its deterministic report
   independently of the inner loop.

---

### Edge Cases

- A matrix cell that is intentionally not applicable (e.g., a work-item state
  invalid for a given command) MUST be reported as **skipped with a reason**,
  distinct from a **coverage gap** (a cell that should run but is missing) so
  intentional N/A is never confused with missing coverage.
- A run that is interrupted or only partially completes MUST mark unfinished
  cells as not-validated (never pass), so an incomplete run cannot read as a
  clean pass.
- Determinism checks MUST hold across host-environment differences that are not
  part of a contract (locale, time zone, working directory, ordering) — these
  must not change deterministic output.
- An intentional, accepted baseline change MUST still yield a deterministic
  report; the change surfaces as a baseline drift the maintainer acknowledges,
  not as nondeterminism.
- The exhaustive validation MUST run and produce a report when the Governance
  gate runtime is absent.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The product MUST define a declared, enumerable set of broad
  validation matrices, where each matrix names its dimensions and the
  cross-product of cells it covers, so the intended coverage is inspectable
  rather than implicit.
- **FR-002**: The product MUST exhaustively exercise the **lifecycle-output
  matrix** — every public lifecycle command (`init`, `charter`, `specify`,
  `clarify`, `checklist`, `plan`, `tasks`, `analyze`, `evidence`, `verify`,
  `ship`) and every public cross-cutting command (`agents`, `refresh`), across
  every public output projection (deterministic `--json`/default, `--text`, and
  rich), over a representative enumerated set of work-item lifecycle states — and
  record a pass/fail/skipped result for each resulting cell.
- **FR-003**: The product MUST exhaustively validate **output determinism**:
  every public generated readiness view and every public deterministic
  (`--json`) command-output regenerated or re-rendered over identical source
  inputs MUST be byte-identical; and the documented degradation rules MUST hold —
  rich output produces zero ANSI when color is disabled (`NO_COLOR`), `TERM=dumb`,
  or output is non-interactive/redirected, and rich output changes no JSON byte,
  stream routing, or exit code relative to the default projection.
- **FR-004**: The product MUST exhaustively validate **baseline conformance**:
  every public contract documented in the release schema reference MUST have a
  locking baseline and a real produced artifact that conforms to it; any public
  contract lacking a baseline or schema-reference entry MUST be reported as
  not-validated rather than passing by absence.
- **FR-005**: The product MUST exhaustively validate each published
  **compatibility-matrix entry** by confirming that the actually produced
  Governance handoff artifact conforms to the declared `contractVersion`
  (recorded as an optional integration fact) and that the supported Spec Kit
  range for the release line is represented, without computing or enforcing any
  Governance-owned verdict.
- **FR-006**: The product MUST emit a single deterministic, machine-readable
  exhaustive-validation report enumerating every matrix, every cell, each cell's
  pass/fail/skipped status, and — for each failure — an actionable diagnostic
  identifying the matrix, the cell coordinates, and the affected
  contract/artifact.
- **FR-007**: The exhaustive-validation report MUST be byte-stable for identical
  source inputs and MUST exclude implicit clocks, durations, host paths, ordering
  nondeterminism, and ANSI styling from its deterministic contract. Any
  wall-clock timestamp, duration, or host-environment fact included for
  operational triage MUST be explicitly marked as sensed/non-deterministic
  metadata and excluded from the deterministic comparison.
- **FR-008**: The exhaustive validation MUST be runnable on a schedule and on
  demand, and MUST be separate from the cheap inner-loop and per-change checks, so
  that adopting it adds no required step to local authoring and does not change
  the behavior or runtime of the existing fast lifecycle commands.
- **FR-009**: The product MUST distinguish, per cell, **skipped-with-reason**
  (intentionally not applicable) from **coverage gap** (a public surface or
  declared dimension value that should be exercised but is not), and MUST report a
  coverage gap as a visible failure rather than silently omitting the cell.
- **FR-010**: The exhaustive validation MUST run, and produce its deterministic
  report, without the Governance gate runtime installed, and MUST NOT define,
  compute, or enforce Governance-owned route selection, profiles, evidence
  freshness, gate verdicts, or release-gate logic.
- **FR-011**: The feature MUST NOT introduce a new lifecycle stage and MUST NOT
  change the existing `charter … ship` chain, authored-source schemas, agent
  guidance, or the per-command public output contracts; it is a cross-cutting
  validation harness over existing public contracts (consistent with `agents`
  and `refresh` being non-lifecycle and with `018-release-readiness` FR-013).
- **FR-012**: When the exhaustive validation's declared coverage disagrees with
  the real public surface — a public command, view, or contract exists that no
  matrix covers, or a matrix names a surface that no longer exists — the real
  produced surface MUST be authoritative and the discrepancy MUST be a detectable
  failure.

### Key Entities *(include if feature involves data)*

- **Validation matrix**: A named broad-coverage check defined by its dimensions
  and the cross-product of cells it covers (e.g., command × projection × work
  state; or contract × environment class).
- **Matrix cell**: One coordinate in a matrix's cross-product, with a recorded
  status of pass, fail, or skipped-with-reason.
- **Environment class**: A determinism/degradation dimension value (color
  disabled, `TERM=dumb`, non-interactive/redirected, interactive) under which
  output behavior is validated.
- **Exhaustive-validation report**: The deterministic machine-readable record of
  every matrix, every cell, each cell's status, and per-failure diagnostics.
- **Coverage gap finding**: A detectable failure indicating a public surface or
  declared dimension value that should be exercised but is not covered by any
  cell.
- **Sensed metadata**: Operational fields (wall-clock timestamp, duration, host
  facts) recorded for triage and explicitly excluded from the deterministic
  comparison.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of public lifecycle and cross-cutting commands, across 100%
  of public output projections, over the enumerated representative work-item
  states, are present as cells in the report with a recorded status — none
  omitted.
- **SC-002**: Every public generated readiness view and every public `--json`
  command-output is validated for byte-identical reproduction over identical
  source inputs; zero nondeterministic public contracts pass the determinism
  matrix.
- **SC-003**: 100% of public contracts in the release schema reference are
  validated to have a locking baseline and a conforming produced artifact, or are
  reported as not-validated; none pass by absence.
- **SC-004**: Running the exhaustive validation twice over identical source
  inputs yields a byte-identical deterministic report (excluding fields marked as
  sensed/non-deterministic metadata).
- **SC-005**: A public command, view, or contract that no matrix covers is
  detected as a coverage gap in 100% of seeded cases; no uncovered public surface
  passes by omission.
- **SC-006**: The exhaustive validation runs to completion and produces its
  report in an environment with no Governance gate runtime installed, and no
  Governance-owned route/profile/freshness/gate/release computation appears in any
  artifact it produces.
- **SC-007**: Adopting the exhaustive validation adds no required step to the
  inner authoring loop; the existing fast lifecycle commands' behavior and runtime
  are unchanged (the exhaustive validation is scheduled/on-demand only).
- **SC-008**: Each documented compatibility-matrix entry is exercised against a
  real produced Governance handoff artifact and its declared `contractVersion`,
  with Governance compatibility recorded only as an optional integration fact.

## Assumptions

- **Next-item resolution**: "The next item on the implementation plan" is the
  Phase 13 SDD-owned item "Add scheduled exhaustive validation for broad
  matrices," explicitly deferred by `018-release-readiness`; all earlier
  SDD-owned phases and the `017` Governance handoff contract are shipped.
- **Reuses existing contracts**: The broad matrices are built over surfaces that
  already exist — the lifecycle/cross-cutting commands and their three output
  projections, the public generated readiness views, the `018` release schema
  reference, locking baselines, and compatibility matrix, and the `016` no-
  Governance lifecycle smoke. This feature exhaustively exercises those existing
  contracts; it does not define new public authored-source schemas.
- **Matrix dimension set**: The default broad matrices are (1) lifecycle/cross-
  cutting command × output projection × representative work-item state, (2) public
  view / `--json` output × environment class for determinism and degradation,
  (3) public schema-reference contract × baseline conformance, and (4) published
  compatibility-matrix entry × produced-artifact conformance. Additional matrices
  may be declared later without changing the harness contract.
- **Scheduling boundary**: The capability is runnable on demand and is intended
  to be triggered on a schedule; the concrete CI schedule wiring (cron cadence,
  runner, notifications) is operational configuration and out of scope here — this
  feature owns the declared matrices, the exhaustive runner, and the deterministic
  report contract.
- **Sensed metadata posture**: A scheduled run inherently observes wall-clock
  time and durations; these are recorded as sensed/non-deterministic metadata for
  triage and excluded from the deterministic report contract, consistent with the
  constitution's determinism and observability principles.
- **CLI family name**: The reserved `fsgg-sdd` CLI family name is used; no rename
  is in scope. The exact subcommand/surface name for invoking the exhaustive
  validation is locked by this feature's plan, consistent with prior lifecycle
  features.
- **Out of scope / deferred**: Governance-owned release rules, `fsgg release`/
  `fsgg verify` gate schemas and exit codes, publish/trusted-publishing/
  provenance enforcement, route/profile/freshness/gate computation, and the
  concrete CI schedule configuration are not delivered by this feature.
