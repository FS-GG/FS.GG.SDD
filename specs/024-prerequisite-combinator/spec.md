# Feature Specification: Extract a prerequisite combinator and shared handler shell

**Feature Branch**: `024-prerequisite-combinator`

**Created**: 2026-06-26

**Status**: Draft

**Change Tier**: Tier 2 (internal change) — implementation cleanup with no
user-visible or tool-visible contract change. The public `.fsi` (`init` /
`update`) and surface-area baselines remain unchanged; requires spec and tests
only. The roadmap holds the deterministic JSON output byte-stable for this item.

**Input**: User description: "next item in docs/reports/2026-06-26-074428-refactor-analysis.md" → roadmap item **R1** (§5.1): *Extract prerequisite combinator + `runHandler` shell.*

## Context

`CommandWorkflow.fs` defines twelve per-command planning handlers
(`computeCharterPlan` … `computeRefreshPlan`). Every one of them hand-rolls the
same skeleton:

1. **Guard on `WorkId`** — `match model.Request.WorkId with | None -> <default
   tuple> | Some workId -> …` (e.g. `CommandWorkflow.fs:3715`, `:4003`).
2. **Accumulate diagnostics** — `projectDiagnostics model` +
   `duplicateWorkIdDiagnostics workId model` + a **prerequisite cascade** that
   parses each upstream artifact and threads its parsed *facts* forward to the
   next check, blanking the remainder once any prerequisite is absent
   (`CommandWorkflow.fs:4008-4034`; the cascade is hand-rolled 5-deep in
   `computeAnalyzePlan` and 6-deep in `computeEvidencePlan` at `:4625-4656`).
3. **Sort + detect blocking** — `commandDiagnostics |> DiagnosticsModule.sort`
   then `hasBlocking = diagnostics |> List.exists (fun d -> d.Severity =
   DiagnosticError)` (repeated verbatim at `:3724`, `:3757`, `:3804`, `:3853`,
   `:3913`, `:3980`, `:4064`, `:4697`, …).
4. **Gate effects on `hasBlocking`** — `if hasBlocking then [] else <write
   effects> @ <generated effects>` (e.g. `:3726-3730`, `:4086-4090`).
5. **Return a per-stage tuple** — `(diagnostics, …summaries…, generatedViews,
   effects)` whose arity grows with the lifecycle position (4-tuple for charter,
   widening to the full lifecycle tuple for verify, plus the two cross-cutting
   shapes for agents/refresh).

The result is ~2,300 lines of handler code (the longest, `computeVerifyPlan`, is
544 lines) in which the genuinely stage-specific logic — *which artifacts are
prerequisites, how to build this stage's artifact, and how to render its view* —
is buried in copy-pasted guard / cascade / `hasBlocking` / effect-gating
boilerplate. The prerequisite cascade in particular is re-expressed as a bespoke
nested `match` per stage, so adding a lifecycle stage or changing the
short-circuit policy means editing every handler.

This refactor extracts that shared structure into (a) an ordered **prerequisite
combinator** that folds a stage's declared prerequisites — threading parsed facts
forward and short-circuiting once one is missing — and (b) a **handler shell**
that owns the `WorkId` guard, diagnostic sort, `hasBlocking` computation, and
effect-gating, leaving each stage to supply only its prerequisite list, its
artifact builder, and its view renderer. It is the highest-leverage row in the
roadmap because it both shrinks the god module (§1.1 / R2) and removes the
per-stage copy-paste in one move.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One prerequisite combinator, not twelve hand-rolled cascades (Priority: P1)

A maintainer adding a lifecycle stage, reordering prerequisites, or changing how
a missing upstream artifact short-circuits downstream parsing edits **one**
combinator and the affected stage's declared prerequisite list — not a bespoke
nested `match` inside every handler. Each stage declares its ordered
prerequisites (project, duplicate, then the upstream artifact chain), and the
combinator folds them, accumulating each successfully-parsed artifact's facts for
the next check and blanking the remaining checks once one prerequisite is absent.

**Why this priority**: The hand-rolled cascades (5- and 6-deep nested matches)
are the most error-prone duplication in the file — they encode the lifecycle
ordering invariant in twelve places. Single-sourcing them is the core of R1 and
the prerequisite for the handler-shell extraction.

**Independent Test**: With the combinator extracted, the existing command-handler
test suites (charter through refresh) pass unchanged, and the prerequisite
short-circuit logic — "if artifact N is missing, artifacts N+1… are not parsed
and contribute no diagnostics" — appears once in the combinator rather than being
re-expressed per stage.

**Acceptance Scenarios**:

1. **Given** a work item missing an upstream prerequisite (e.g. no plan when
   running analyze), **When** the stage runs, **Then** it emits exactly the same
   diagnostics, in the same sorted order, as before the refactor, and does not
   attempt to parse the artifacts downstream of the missing one.
2. **Given** a work item with all prerequisites present, **When** the stage runs,
   **Then** the parsed facts thread forward identically and the produced
   artifact, generated views, and effects are unchanged.
3. **Given** the twelve handlers, **When** the source is inspected, **Then** the
   ordered prerequisite fold with its short-circuit/fact-threading policy is
   defined once and referenced by each stage, with no copied cascade bodies
   remaining.

---

### User Story 2 - One handler shell owns the guard / blocking / gating boilerplate (Priority: P1)

A maintainer changing how handlers detect blocking diagnostics, gate effects, or
guard the missing-`WorkId` path edits **one** shell, not twelve handler bodies.
The shell owns the `WorkId` guard, the diagnostic sort, the `hasBlocking`
computation, and the "emit effects only when not blocking" gate; each stage
supplies only its artifact-build and view-render functions and its return shape.

**Why this priority**: The guard / `hasBlocking` / effect-gating lines are
repeated verbatim across all twelve handlers; centralizing them removes the
largest volume of copy-paste and makes the blocking/gating policy single-sourced.
It is independently valuable even apart from the combinator.

**Independent Test**: With the shell extracted, every handler routes its
guard/blocking/gating through the shared shell; the existing per-command tests
pass unchanged, and the `hasBlocking`/effect-gating expressions appear once in
the shell rather than once per handler.

**Acceptance Scenarios**:

1. **Given** a handler whose computed diagnostics include a blocking error,
   **When** it runs through the shell, **Then** no write/generated effects are
   emitted — identical to the pre-refactor `if hasBlocking then []` behavior.
2. **Given** a request with no `WorkId`, **When** any stage runs, **Then** the
   shell returns that stage's documented empty/default result, identical to the
   pre-refactor `| None ->` arm.
3. **Given** the twelve handlers, **When** the source is inspected, **Then** the
   guard, diagnostic sort, `hasBlocking` computation, and effect gate are defined
   once in the shell and not duplicated per handler.

---

### Edge Cases

- **Missing `WorkId`**: handled once by the shell's guard; each stage still
  yields its own documented empty/default tuple shape (the arity differs per
  stage), with no behavior change.
- **First prerequisite missing vs. a later one missing**: the combinator
  short-circuits at the first absent prerequisite — downstream artifacts are not
  parsed and emit no diagnostics — reproducing today's nested-match behavior
  exactly for every position in the chain.
- **Blocking diagnostic from a generated-view failure (not a prerequisite)**:
  still flows into `hasBlocking` and suppresses effects via the shell, unchanged.
- **Cross-cutting handlers (`computeAgentsPlan`, `computeRefreshPlan`)**: these
  are not lifecycle stages and have a different prerequisite shape and return
  type; they MUST still share the guard / `hasBlocking` / effect-gating shell
  even where their prerequisite list does not fit the linear lifecycle cascade.
- **Per-stage return arity**: the shell must accommodate the differing tuple
  arities (charter's 4-tuple through verify's full lifecycle tuple, plus the
  agents/refresh shapes) without flattening or changing any of them.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repeated prerequisite cascade — the ordered, fact-threading,
  short-circuiting parse of each stage's upstream artifacts — MUST be expressed
  once as a reusable combinator, and each of the twelve `compute*Plan` handlers
  MUST obtain its prerequisite diagnostics and parsed facts through that
  combinator rather than a hand-rolled nested `match`.
- **FR-002**: The combinator MUST preserve the existing short-circuit semantics:
  once a prerequisite artifact is absent (or fails to parse into facts), the
  checks downstream of it MUST NOT be evaluated and MUST contribute no
  diagnostics — identical to the current per-stage cascade.
- **FR-003**: The combinator MUST thread each successfully-parsed prerequisite's
  facts forward to subsequent checks (e.g. spec facts feeding clarification,
  checklist, plan, and task checks) exactly as the current cascade does.
- **FR-004**: The repeated handler boilerplate — the missing-`WorkId` guard, the
  `DiagnosticsModule.sort`, the `hasBlocking` computation, and the
  "emit effects only when not blocking" gate — MUST be expressed once in a shared
  handler shell, with each stage supplying only its artifact-build and
  view-render functions (and its return shape).
- **FR-005**: All twelve handlers (charter, specify, clarify, checklist, plan,
  tasks, analyze, evidence, verify, ship, agents, refresh) MUST route their
  guard / blocking-detection / effect-gating through the shared shell; no handler
  may keep a private copy of that boilerplate.
- **FR-006**: Each handler MUST preserve its existing behavior for every input
  the test suite exercises — identical diagnostics (same ids, severities, and
  sort order), identical produced artifacts and summaries, identical generated
  views, and identical emitted effects (including the blocked-effects-suppressed
  case).
- **FR-007**: The change MUST NOT alter any public `.fsi` contract — `init` and
  `update` keep their names and signatures — and MUST NOT change the surface-area
  baseline.
- **FR-008**: The change MUST NOT introduce new logic duplication: the
  combinator, its short-circuit/fact-threading policy, and the shell's
  guard/blocking/gating each exist exactly once, not copied.
- **FR-009**: The existing test suite (438 tests) MUST pass unchanged; no test
  may be weakened, skipped, or rewritten to accommodate the refactor beyond
  mechanical call-site updates.
- **FR-010**: The refactor MUST NOT change FS3261 (nullness) or FS0025
  (incomplete-match) warning counts as a side effect (FS3261 is R5 scope; FS0025
  was cleared by R4 and MUST stay at zero); any FS3261 movement MUST be
  relocation only, not new or removed sites.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All **438** existing tests pass — equal to the post-R4 baseline —
  with no test-source changes beyond mechanical call-site updates.
- **SC-002**: The prerequisite cascade (ordered fold with short-circuit and
  fact-threading) is defined **once**; no `compute*Plan` handler contains a
  hand-rolled nested prerequisite `match`, and the previously 5-/6-deep cascades
  in `computeAnalyzePlan` / `computeEvidencePlan` are gone.
- **SC-003**: The guard / `DiagnosticsModule.sort` / `hasBlocking` /
  effect-gating expressions are defined **once** in the shell; a source search
  finds the `hasBlocking = … List.exists … DiagnosticError` expression a single
  time rather than the current ~10+ occurrences.
- **SC-004**: No public `.fsi` signature changes and no surface-area baseline
  changes; the deterministic JSON output for every existing view fixture is
  **byte-identical** before and after the refactor.
- **SC-005**: A clean `dotnet build -c Release` is green with **0 FS0025**
  warnings (unchanged from R4) and an FS3261 count equal to the pre-refactor
  baseline (no new or removed nullness sites).
- **SC-006**: The handler section of `CommandWorkflow.fs` shrinks measurably
  (the twelve `compute*Plan` bindings net fewer lines than before), confirming
  the boilerplate was removed rather than relocated.

## Assumptions

- The twelve `compute*Plan` handlers are **internal** to `CommandWorkflow.fs`
  with no external consumers — only `init`/`update` are public (the 7-line
  `.fsi`), so the combinator, the shell, and any change to the handlers' internal
  signatures are a pure internal reorganization with near-zero blast radius.
- The exact F# mechanism for the combinator and shell (a fold over a list of
  `WorkModel -> Diagnostic list * 'facts` checks, a higher-order `runHandler`
  taking build/render callbacks, the precise placement within the module or a new
  internal module, and how the differing return arities are handled — e.g. the
  shell returning diagnostics+effects with each stage assembling its own tuple)
  are **planning/implementation details**, not part of this contract.
- The cross-cutting `computeAgentsPlan` and `computeRefreshPlan` handlers share
  the **shell** (guard/blocking/gating) but not necessarily the linear lifecycle
  **prerequisite cascade**, since their prerequisite shape differs; the spec
  requires them to use the shell and forbids them keeping a private copy of the
  guard/blocking/gating boilerplate, without forcing their prerequisites into the
  linear combinator if that would change behavior.
- Byte-stable JSON output **is** required for this item (R1 holds the public
  `.fsi` contract and deterministic output byte-stable per the roadmap). The
  binding gate is: build green + the 438-test suite green + zero FS0025 +
  unchanged FS3261 + byte-identical view output + unchanged `.fsi`/surface
  baseline.
- No new behavior is introduced for any input the existing suite already
  exercises; the refactor is behavior-preserving, so **no new fixtures or tests
  are required** and the existing 438-test suite is the regression gate. (Unlike
  R4, this item introduces no genuinely new arm, so it adds no new required
  assertion.) No existing test may be weakened, skipped, or rewritten.
- This item is sequenced **before** R2 (the `CommandWorkflow.fs` file split):
  extracting the combinator and shell first gives the later module split a
  cleaner, smaller handler layer to relocate.
