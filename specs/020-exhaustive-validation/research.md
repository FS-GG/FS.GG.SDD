# Phase 0 Research: Scheduled Exhaustive Validation of Broad Matrices

All decisions below resolve the Technical Context with no remaining
`NEEDS CLARIFICATION`.

## Decision 1 — Surface name lock: `fsgg-sdd validate`

**Decision**: The exhaustive validation is invoked as `fsgg-sdd validate`.

**Rationale**: The spec defers the exact name to this plan (Assumptions / "CLI
family name"). `validate` matches the feature's own language ("exhaustive
validation"), reads as a single idiomatic verb like every other command, and is
distinct from the per-work-item lifecycle stage `verify` (which evaluates one work
item's verification readiness; `validate` is a cross-cutting sweep over the whole
public surface). The two never collide in a sentence: "verify *this work item*"
vs. "validate *the product surface*."

**Alternatives considered**: `matrix`, `exhaustive`, `sweep`, `conformance` — all
either jargony or narrower than the report (which also covers degradation and
compatibility). `validate` won on plainness and alignment with the spec text.

## Decision 2 — Placement: new library + CLI-level command, not a `SddCommand`

**Decision**: Add a new `FS.GG.SDD.Validation` library and dispatch `validate` at
the **CLI level** (a peer of `--version`/`version` in `Program.fs`), **not** as a
new `SddCommand` DU case.

**Rationale**: FR-011 forbids a new lifecycle stage and any change to the
per-command `CommandReport` output contract. Adding a `SddCommand` case would
change `CommandTypes.fsi`, `parseCommand`, and the `command-report` catalog shape
— exactly what FR-011 protects. A separate library that **depends on** `Commands`
(to drive the real workflow) and `Artifacts` (to reuse `ReleaseContract.evaluate`)
keeps the dependency one-directional: nothing in the lifecycle surface depends on
the harness, so command contracts cannot drift to satisfy it. This mirrors
feature 018's `ReleaseContract`, which documents/locks existing contracts without
adding a stage, and the CLAUDE.md boundary that `agents`/`refresh` are
non-lifecycle (`nextLifecycleCommand = None`).

**Alternatives considered**: (a) a `SddCommand` case — rejected, violates FR-011's
contract-stability clause; (b) a test-only harness with no CLI — rejected, FR-008
requires on-demand/scheduled invocation, which needs a real command CI can call;
(c) a module inside `Commands` — rejected, would make the lifecycle library carry
harness-only code and risk a dependency cycle when it drives its own workflow.

## Decision 3 — The four declared matrices and their representative dimensions

**Decision**: Declare exactly the four matrices the spec enumerates, each an
inspectable named record (FR-001):

1. **lifecycle-output** — `command ∈ {init, charter, specify, clarify, checklist,
   plan, tasks, analyze, evidence, verify, ship, agents, refresh}` × `projection ∈
   {Json, Text, Rich}` × `state ∈` the representative enumerated work-item states
   below.
2. **determinism** — `output ∈` {the 9 generated views + `command-report (--json)`
   from the release catalog} × `environment ∈ {ColorDisabled, TermDumb,
   NonInteractiveRedirected, Interactive, PerturbedHostEnvironment}`. The last class
   covers the spec Edge Case that determinism MUST hold across host-environment
   differences not part of any contract (locale, time zone, working directory,
   ordering).
3. **baseline-conformance** — `contract ∈` every `release-readiness.json` catalog
   entry × {baseline present, produced artifact conforms}.
4. **compatibility** — `entry ∈` every `compatibility[]` record × {produced handoff
   `contractVersion` conforms, Spec Kit range represented}.

**Representative work-item states** (lifecycle matrix's third dimension): the
existing `TestSupport` ladder already names the representative set —
`fresh` (init only), `specified`, `planReady`, `tasksReady`, `analyzed`,
`evidenced`, `verified`/`shipped`, plus at least one `blocked` state. `specified` is
included as a mid-spec rung because US1 specifically targets regressions that surface
only at mid-lifecycle states. The harness reconstructs these by driving the real
`CommandWorkflow` (Decision 4); the runner owns its own state-builders (it cannot
reference the test project). A cell where a command is invalid for a state (e.g.
`ship` on a `fresh` project) is **skipped-with-reason**, not a gap (FR-009, Edge
Cases).

**Rationale**: These four are the spec's named default matrices (Assumptions /
"Matrix dimension set"). Additional matrices can be declared later without changing
the harness contract because a matrix is just data (a named dimensions + cell
enumeration), so the report shape is matrix-agnostic.

## Decision 4 — State construction: drive the real `CommandWorkflow`

**Decision**: The runner builds each work-item state by driving the real
`FS.GG.SDD.Commands` `CommandWorkflow` (`init`/`update`) over a disposable temp
directory and interpreting effects with the existing `Commands` effect interpreter
— the same path the CLI's `interpretUntilIdle` and `TestSupport` use — then invokes
the command-under-test and captures its `CommandReport` per projection.

**Rationale**: Constitution VI mandates real fixtures over mocks; FR-012 mandates
the **real produced surface** is authoritative. Re-deriving states from real runs
guarantees the matrix exercises the actual contracts, not a synthetic stand-in. The
effect interpreter is already factored out and reused by the CLI, so the harness
adds no parallel I/O path.

## Decision 5 — Baseline matrix reuses `ReleaseContract.evaluate`

**Decision**: The baseline-conformance matrix is implemented by snapshotting the
produced artifacts of a real shipped project and calling the existing pure
`ReleaseContract.evaluate release produced`; each returned diagnostic becomes a
failing/`notValidated` cell, an empty result is all-pass.

**Rationale**: Feature 018 already owns the catalog-vs-produced conformance check as
a pure function (no file I/O, caller supplies the snapshot — Constitution V). Reusing
it keeps a single source of truth for "what conforms," satisfies FR-004's
"not-validated rather than passing by absence" (a missing baseline/source already
yields a diagnostic there), and means the baseline matrix cannot drift from the
release contract. The harness adds the per-cell *projection* of those diagnostics; it
does not re-implement conformance.

## Decision 6 — Report determinism, sensed-metadata fence, projection scope

**Decision**: The `validation-report` JSON is canonically serialized (stable key
order, no clock/duration/host-path/ANSI) exactly like the existing
`ReleaseContract.serialize` and generated views. Operational triage facts
(wall-clock start, duration, host) live **only** under an explicit `sensed` object
that is excluded from the deterministic comparison (FR-007). The report's
deterministic projection is **`--json` (the contract) plus `--text`**; a `--rich`
projection of the report is **deferred** from MVP scope.

**Rationale**: FR-007 requires byte-stability and an explicit sensed fence;
mirroring the proven `ReleaseContract` serializer reuses a known-deterministic
approach. Rich is deferred because (a) the report's value is machine-readability for
CI, (b) the spec mandates only "deterministic machine-readable" for the report
itself, and (c) `--rich` validation in this feature is about the *lifecycle
commands under test* (the determinism/degradation matrix), not the report's own
rendering. `--text` is included for human triage parity with every other command;
`--rich` can be added later as a pure projection without a contract change.

The `validation-report` is a new public JSON contract but is **deliberately excluded**
from the 018 release schema-reference catalog (it carries sensed metadata and is
harness output, not a produced lifecycle artifact). To avoid the very silent-omission
this feature guards against, the exclusion is **documented as a declared exception** in
`docs/release/schema-reference.md` so it is recorded rather than absent (see
validation-report C-4 and task T026a).

## Decision 7 — Status taxonomy distinguishes four visible outcomes

**Decision**: `CellStatus = Pass | Fail of diagnostic | SkippedWithReason of string
| CoverageGap of surface | NotValidated of reason`. A `coverageGap` and a stale
matrix entry (a declared cell whose surface no longer exists) both render the report
**non-passing** (FR-009/FR-012). An interrupted/partial run leaves untouched cells
`NotValidated` (FR-007 / Edge Cases) — never an implicit pass.

**Rationale**: The spec's edge cases require intentional N/A (`skipped`), missing
coverage (`coverageGap`), and incomplete/unproven (`notValidated`) to be *distinct,
visible* states so a sample never reads as a clean pass. Modeling them as DU cases
makes "not covered" and "not finished" impossible to silently omit.

## Decision 8 — No-Governance posture

**Decision**: The harness runs to completion and emits its report with no Governance
runtime present. The compatibility matrix records produced-handoff `contractVersion`
conformance as an **optional integration fact**; it never computes or enforces a
Governance route/profile/freshness/gate/release verdict (FR-005/FR-010). Governance
absence is a clean run, not a failure.

**Rationale**: Constitution Engineering Constraints ("SDD must remain useful without
Governance installed") and FR-010. The `governance-handoff.json` artifact is produced
by SDD itself (feature 017), so its `contractVersion` is checkable without any
Governance package; the *interpretation* of that version is Governance-owned and
stays out of scope.

## Open questions

None. All Technical Context fields are resolved; no `NEEDS CLARIFICATION` remain.
