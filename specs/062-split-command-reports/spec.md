# Feature Specification: Split CommandReports and type the defect/summary contracts

**Feature Branch**: `062-split-command-reports`

**Created**: 2026-07-03

**Status**: Draft

**Input**: FS.GG.SDD issue #72 — 2026-07-02 code-quality & architecture review (§1.4 + §3.2 + §3.5 / remediation #8, MEDIUM). Repo-local, not cross-repo.

**Change Tier**: Tier 1 (contracted change). The `Diagnostic` public record gains a typed field and the `Diagnostics` module gains two functions — a deliberate, minimal public-type change (the typed classification of FR-001/FR-004). Every user-/tool-visible *output* contract (JSON bytes, exit codes, stream routing, text/rich projections, persisted schemas, and the `CommandReports` module surface) is held invariant. `.fsi` files are updated before `.fs` bodies per Principles I/III.

## Overview

`CommandReports.fs` has grown to ~1,524 lines holding three separable
responsibilities, and two pieces of report policy are carried by
*stringly-typed* mechanisms that fail silently when a maintainer forgets to keep
them in sync:

- Exit-code escalation to the tool-defect class (exit 2) is decided by matching a
  diagnostic's id against a hand-maintained `providerDefectIds` string set. A
  defect diagnostic whose id is not listed is silently demoted to exit 1.
- Agent-refresh classifies a diagnostic as "stale" by substring-matching its id
  (`diagnostic.Id.IndexOf("stale")`), which couples routing to id spelling.
- The lifecycle-command driver threads a positional 12-tuple of per-stage summary
  options through every command arm, so a change to one stage's summary shape
  touches every arm and mis-ordering is invisible to the compiler.

This feature makes the failure-prone contracts **typed** so the compiler enforces
them, and separates `CommandReports.fs` into cohesive units — while preserving the
external CLI contract (JSON automation output, exit codes, and the text/rich
projections) byte-for-byte.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A new defect diagnostic escalates the exit code without a second registration (Priority: P1)

A maintainer adds a diagnostic that represents a tool/provider defect (not
malformed user input). Today they must also remember to add its id to the
`providerDefectIds` set, or the command that emits it silently exits 1 instead of
the tool-defect exit 2 — a defect that automation cannot detect.

After this feature, the defect class is a typed property carried by the
diagnostic itself, established where the diagnostic is constructed. Exit-code
escalation reads that property. Adding a defect diagnostic escalates correctly
with no separate list to maintain.

**Why this priority**: This is the correctness win — it removes a class of
silent-demotion bugs on the machine-consumed exit-code contract, which downstream
automation and CI rely on to distinguish "your input was wrong" (exit 1) from
"the tool/provider broke" (exit 2).

**Independent Test**: Add or simulate a defect-class diagnostic that is not (and
was not) in any hand-maintained id list, emit it from a blocked command, and
confirm the process exits 2; emit a malformed-user-input diagnostic and confirm
exit 1. No test needs to reference an id-string allowlist.

**Acceptance Scenarios**:

1. **Given** a blocked command whose diagnostics include one marked as a
   tool/provider defect, **When** the exit code is computed, **Then** the process
   exits 2.
2. **Given** a blocked command whose diagnostics are all marked as user-input
   failures, **When** the exit code is computed, **Then** the process exits 1.
3. **Given** a maintainer adds a new defect-class diagnostic constructor, **When**
   they do not edit any separate id registry, **Then** the diagnostic still
   escalates to exit 2.
4. **Given** the full set of diagnostics that escalate today (`toolDefect`,
   `scaffold.providerFailed`, `scaffold.providerUnavailable`,
   `scaffold.providerWroteSddTree`, `scaffold.mirrorFailed`,
   `upgrade.selfUpdateFailed`, `upgrade.stepFailed`), **When** each is emitted
   from its command, **Then** every one still exits 2 exactly as before.

---

### User Story 2 - The external CLI contract is unchanged by the refactor (Priority: P1)

A downstream consumer (automation, CI, Governance handoff, golden tests) depends
on the exact JSON bytes, exit codes, and stdout/stderr routing that `fsgg-sdd`
emits today, plus the text and rich projections. The restructuring and typing
changes must not alter any of these observable outputs.

**Why this priority**: The refactor has no user-facing value if it changes the
contract; contract preservation is the safety net that makes the whole change
adoptable. It is the pass/fail boundary for the feature.

**Independent Test**: Run the full existing golden/determinism/projection test
suite against the refactored code with no baseline edits and confirm it is green;
diff representative JSON, text, and rich outputs across every command against the
pre-refactor baseline and confirm zero byte differences (rich excluded from
byte-golden per project rule, but its facts unchanged).

**Acceptance Scenarios**:

1. **Given** any command × output projection × representative state, **When** it
   is run before and after the refactor, **Then** the default/`--json` output is
   byte-identical.
2. **Given** any command, **When** it is run before and after the refactor,
   **Then** the exit code and the stdout/stderr stream routing are identical.
3. **Given** the `--text` projection for every command, **When** run before and
   after, **Then** the plain-text output is byte-identical.
4. **Given** the existing test suite and golden baselines, **When** the refactor
   lands, **Then** they pass without baseline modification.

---

### User Story 3 - Agent-refresh staleness routing does not depend on id spelling (Priority: P2)

The agent-refresh path decides whether embedded diagnostics indicate a stale
generated view. Today it does so by searching for the substring `"stale"` inside
each diagnostic id. Renaming or adding a stale-signalling diagnostic can silently
change routing.

After this feature, staleness is a typed property of the diagnostic (the same
typed classification introduced for the defect bit, or a sibling of it), and the
refresh path reads that property instead of the id text.

**Why this priority**: Same failure mode as US1 (stringly-typed policy that fails
silently) but on a narrower, non-exit-code surface, so it is P2 rather than P1.

**Independent Test**: Emit a stale-signalling diagnostic whose id does not contain
the literal substring `stale`, run the refresh classification, and confirm it is
still classified as stale; confirm a non-stale diagnostic whose id happens to
contain `stale` is not misclassified.

**Acceptance Scenarios**:

1. **Given** a diagnostic typed as stale-signalling with an id that omits the word
   "stale", **When** refresh classifies embedded diagnostics, **Then** it is
   treated as stale.
2. **Given** the current stale-signalling diagnostics, **When** refresh runs after
   the change, **Then** its output is unchanged from before.

---

### User Story 4 - Per-stage summaries thread through one typed shape, not a positional 12-tuple (Priority: P3)

A maintainer changing one lifecycle stage's summary (e.g. adding a field to the
verify summary) today must edit a 12-position tuple that is constructed
identically in every command arm, padding the other 11 positions with `None`. A
mis-ordered position compiles but produces a wrong report.

After this feature, the per-stage summaries are carried in a named partial record
where each stage sets only its own field, so a wrong field cannot be silently
swapped for another and one stage's change touches only its own arm.

**Why this priority**: Maintainability/robustness improvement with no user-facing
behaviour change; lowest priority because it does not fix an observed
silent-failure on a shipped contract, only prevents a future one.

**Independent Test**: Confirm the driver produces the same report/model for every
command as before, and that adding a field to one stage's summary requires editing
only that stage's arm.

**Acceptance Scenarios**:

1. **Given** each lifecycle command, **When** its plan is computed, **Then** the
   resulting report is identical to the pre-refactor report.
2. **Given** the summary-threading construct, **When** a maintainer inspects it,
   **Then** each command arm sets only the fields relevant to its stage.

---

### User Story 5 - Report responsibilities live in cohesive units (Priority: P3)

A maintainer looking for "how a diagnostic is constructed", "how the next-action /
correction is routed", or "how the report and exit code are assembled" today reads
one ~1,500-line file mixing all three. After this feature these responsibilities
are separated into cohesive units, each independently navigable.

**Why this priority**: Pure structural clarity; enables the other stories and the
follow-up hotspot splits without changing behaviour, so it is P3.

**Independent Test**: Confirm the three responsibilities (diagnostic
construction, correction/next-action routing policy, report + exit-code assembly)
are in separate compilation units with no responsibility straddling two, and the
build and full suite are green.

**Acceptance Scenarios**:

1. **Given** the split, **When** the project builds, **Then** it compiles with no
   new public surface leaked and no behaviour change.
2. **Given** a maintainer needs to change next-action routing, **When** they open
   the routing unit, **Then** they do not also encounter diagnostic constructors
   or report assembly in the same file.

---

### Edge Cases

- A diagnostic that is both a warning severity and typed as a defect: severity and
  defect-class are independent; exit-code escalation depends on the defect bit and
  the blocked outcome, not on severity. Behaviour must match today's set-membership
  result for every currently-listed id.
- A command that emits multiple diagnostics of mixed classes while blocked: exit 2
  if *any* diagnostic is a defect, else exit 1 — identical to today's
  `List.exists` over the id set.
- Non-blocked outcomes (Succeeded / SucceededWithWarnings / NoChange) always exit
  0 regardless of the defect bit — unchanged.
- The help report path (no diagnostics, no changes → exit 0) is unaffected.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The tool-defect class that escalates a blocked command to exit 2
  MUST be carried as a typed property established at diagnostic construction, not
  decided by matching the diagnostic id against a separately-maintained string
  set.
- **FR-002**: Adding a new defect-class diagnostic MUST cause it to escalate to
  exit 2 with no edit to any separate id registry; the hand-maintained
  `providerDefectIds` set MUST be removed (or reduced to a compiler-checked
  derivation with no free-form id literals).
- **FR-003**: Every diagnostic that escalates to exit 2 today MUST continue to do
  so, and every diagnostic that resolves at exit 1 today MUST continue to do so;
  the exit-code outcome MUST be identical for all current diagnostics.
- **FR-004**: Agent-refresh staleness classification MUST read a typed property of
  the diagnostic rather than substring-matching the diagnostic id, and MUST
  produce the same classification for every diagnostic as it does today.
- **FR-005**: The per-stage lifecycle summaries threaded through the command
  driver MUST be carried in a named record (or equivalently type-safe structure)
  where each command arm sets only its own stage's field(s), replacing the
  positional 12-tuple; the resulting report MUST be unchanged for every command.
- **FR-006**: `CommandReports.fs` MUST be separated so that diagnostic
  construction, correction/next-action routing policy, and report + exit-code
  assembly are distinct cohesive units, with no single unit spanning more than one
  of these responsibilities.
- **FR-007**: The default/`--json` output of every command MUST be byte-identical
  before and after this feature for every representative state.
- **FR-008**: The `--text` projection of every command MUST be byte-identical, and
  the `--rich` projection MUST carry the same facts (adding/dropping none), before
  and after this feature.
- **FR-009**: Exit codes and stdout/stderr stream routing MUST be identical before
  and after this feature for every command and outcome.
- **FR-010**: The existing test suite and all **golden output baselines** (JSON,
  text, serialization) MUST pass without modification, as MUST the
  `FS.GG.SDD.Commands` public-surface baseline. The **only** sanctioned baseline
  change is the additive `FS.GG.SDD.Artifacts` public-surface delta introduced by
  the two new typed functions (FR-011). Where a test currently asserts against the
  `providerDefectIds` id set or the `"stale"` substring, it MUST be re-expressed
  against the typed property without weakening coverage.
- **FR-011**: No new public API surface MAY be introduced beyond what the typed
  defect/staleness classification and the summary record require; internal-only
  helpers MUST stay internal, and any new public surface MUST be sketched as
  `.fsi` before implementation per the constitution.
- **FR-012**: The Governance-handoff compatibility facts and the release-readiness
  catalog MUST be unchanged by this feature (no schema-versioned artifact changes;
  this is an internal restructuring).

### Key Entities

- **Diagnostic**: The unit of machine-readable feedback (`Id`, `Severity`,
  `Artifact`, `Location`, `Message`, `Correction`, `RelatedIds`). This feature
  adds a typed classification distinguishing tool/provider *defects* (exit-2
  escalating) from user-input failures, and a typed marker for stale-signalling
  diagnostics — replacing today's id-string membership and substring tests.
- **Per-stage summaries carrier**: The structure the command driver uses to pass
  each lifecycle stage's optional summary (specification, clarification,
  checklist, plan, tasks, analysis, evidence, verification, ship, plus diagnostics
  / generated-views / planned-effects) from the stage's plan computation to report
  assembly. Replaces the positional 12-tuple with a named partial record.
- **Report policy units**: The separated responsibilities carved out of
  `CommandReports.fs` — diagnostic constructors, correction/next-action routing,
  and report + exit-code assembly.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the diagnostics that escalate to exit 2 today still exit 2,
  and 100% that exit 1 today still exit 1, verified by test with zero baseline
  edits.
- **SC-002**: Zero byte differences in default/`--json` and `--text` output across
  every command × representative state, before vs after.
- **SC-003**: Adding a new defect-class diagnostic requires editing exactly zero
  separate id-registry locations (down from one today) for correct exit-2
  escalation.
- **SC-004**: Zero free-form diagnostic-id string literals remain in the exit-code
  escalation and refresh-staleness decision paths (down from the `providerDefectIds`
  set of 7 ids and the `"stale"` substring test).
- **SC-005**: The three responsibilities currently in `CommandReports.fs` reside
  in separate compilation units, none exceeding a single responsibility; the full
  existing test suite is green.
- **SC-006**: `fsgg-sdd validate` reports the same determinism, degradation,
  release baseline-conformance, and Governance-handoff-compatibility results as
  before this feature.

## Assumptions

- The exit-code contract is exactly: blocked + any defect-class diagnostic → 2;
  blocked + no defect-class diagnostic → 1; all non-blocked outcomes → 0. This
  matches the current `exitCodeForReport`.
- The current `providerDefectIds` membership (`toolDefect`,
  `scaffold.providerFailed`, `scaffold.providerUnavailable`,
  `scaffold.providerWroteSddTree`, `scaffold.mirrorFailed`,
  `upgrade.selfUpdateFailed`, `upgrade.stepFailed`) is the authoritative present
  behaviour to preserve; the typed bit must reproduce exactly this set.
- The follow-up complexity-hotspot splits called out in issue #72
  (`computeRefreshPlan`, `computeVerifyPlan`/`computeShipPlan` and their Some-tuple
  matches) are **out of scope** for this feature except where the 12-tuple
  replacement (FR-005) naturally touches the Some-tuple match; deeper splits are a
  separate work item.
- This is a repo-local change with no cross-repo contract impact; no coordination
  with Governance/Rendering/Templates is required.
- All diagnostic constructors already centralize id + message + correction, so
  attaching a defect/staleness classification at those constructors is sufficient
  to cover every producer.
