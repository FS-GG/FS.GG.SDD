# Feature Specification: Rich Spectre.Console CLI Rendering

**Feature Branch**: `019-spectre-rendering`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User description: "start the next item on the implementation plan."

> Resolved from `docs/initial-implementation-plan.md`: all SDD-owned lifecycle
> phases (artifact model, normalized work model, lifecycle commands, evidence,
> verify, ship, refresh, agent guidance, bootstrap/migration, Governance handoff,
> and the Phase 13 release/distribution SDD slice) are shipped and merged
> (001–018). The one remaining unchecked item with an SDD owner is Phase 13's
> *"Add Spectre.Console projections backed by the same report objects used for
> JSON,"* which feature 018 explicitly deferred as a presentational follow-on
> (`specs/018-release-readiness/spec.md` Out-of-scope). This feature delivers that
> SDD-owned slice. Governance-owned release rules, gate schemas, route/profile
> enforcement, and freshness remain out of scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read lifecycle command output at a glance in a terminal (Priority: P1)

A maintainer or coding agent runs an `fsgg-sdd` lifecycle command (init, charter,
specify, clarify, checklist, plan, tasks, analyze, evidence, verify, ship,
refresh, agents) in an interactive terminal. Today they get either dense
deterministic JSON or flat plain text. They want a richer human rendering that
makes the command outcome, the work item, any blocking diagnostics, generated-view
currency, and the recommended next lifecycle command immediately scannable —
without having to read JSON or count lines of plain text.

**Why this priority**: The lifecycle surface is complete and stable; the gap is
purely human ergonomics. Making the existing report objects easy to read in a
terminal is the entire value of this feature, and every other story builds on the
rich projection existing at all.

**Independent Test**: Can be fully tested by running any one lifecycle command in
an initialized SDD project with the rich format selected, in an interactive
terminal, and confirming the rendered output clearly presents the command
outcome, the affected work item, diagnostics (if any), and the next recommended
command — derived entirely from the same report object the JSON projection uses.

**Acceptance Scenarios**:

1. **Given** an initialized SDD project and an evidence-ready work item, **When**
   a user runs a lifecycle command with the rich human format selected in an
   interactive terminal, **Then** the output visually distinguishes the outcome
   state (e.g. ready vs blocked), lists any diagnostics grouped by severity, and
   states the recommended next lifecycle command.
2. **Given** a command that produces blocking diagnostics, **When** it is run with
   the rich format, **Then** the blocking reasons are visually emphasized and the
   process still exits with the same blocked exit code and routes the rendered
   output to the same stream as the other formats.
3. **Given** any lifecycle command, **When** it is run with the rich format,
   **Then** every fact present in the command report (outcome, work item,
   per-stage summary, diagnostics, generated-view currency, next command) is
   represented in the rendering and no fact absent from the report is invented.

---

### User Story 2 - Safe, clean output in non-interactive contexts (Priority: P1)

A user pipes a lifecycle command's output to a file, another tool, or a CI log,
or runs it on a terminal that does not support color. They must never get ANSI
color/control sequences corrupting captured output, and automation must keep
getting the deterministic JSON contract it already depends on.

**Why this priority**: This repository's central discipline is that automation
truth never changes and deterministic output must not depend on terminal wrapping
or ANSI. A rich renderer that leaks escape codes into logs or alters the default
machine output would be a regression, so safe degradation is as critical as the
rich rendering itself.

**Independent Test**: Can be fully tested by running a lifecycle command with
output redirected to a file (non-interactive) and confirming the captured bytes
contain no ANSI/color control sequences, and by confirming the default output
format and its bytes are unchanged from before this feature for the same inputs.

**Acceptance Scenarios**:

1. **Given** a lifecycle command run with output redirected to a file or pipe,
   **When** the rich format would otherwise apply, **Then** the captured output
   contains no ANSI/color escape sequences.
2. **Given** an environment that disables color (for example a `NO_COLOR`
   indicator or a non-interactive/dumb terminal), **When** a lifecycle command is
   run, **Then** color and rich control sequences are suppressed while the
   information content is preserved.
3. **Given** any lifecycle command and the same inputs, **When** the default
   output format is used, **Then** the produced bytes and exit code are identical
   to the behavior before this feature.

---

### User Story 3 - Choose the right format for the task (Priority: P2)

A user (human or agent) wants to deliberately choose between machine-readable
JSON, portable plain text, and rich terminal output depending on whether they are
scripting, logging, or reading interactively.

**Why this priority**: The first two stories deliver and protect the rich
projection; explicit, predictable format selection makes it usable across the
human/agent/CI mix the product serves. It is valuable but depends on the rich and
fallback behaviors existing first.

**Independent Test**: Can be fully tested by invoking the same lifecycle command
three ways — requesting JSON, plain text, and rich output — and confirming each
returns the corresponding projection of the same report with consistent outcome
and exit code.

**Acceptance Scenarios**:

1. **Given** a lifecycle command, **When** the user explicitly requests the JSON
   format, **Then** they receive the deterministic JSON projection unchanged.
2. **Given** a lifecycle command, **When** the user explicitly requests plain
   text, **Then** they receive the existing portable plain-text projection.
3. **Given** a lifecycle command, **When** the user explicitly requests the rich
   format, **Then** they receive the rich projection in interactive terminals and
   a clean plain-text fallback when output is non-interactive or color is
   disabled.

---

### Edge Cases

- What happens when the rich format is requested but stdout is redirected, piped,
  or attached to a non-interactive/dumb terminal? Output falls back to plain text
  with no ANSI sequences.
- How does the renderer handle a blocked outcome? The rendered output goes to the
  same stream and exit code as the other formats for that outcome.
- How does the renderer handle an unknown command or an error report? It still
  presents the diagnostics and preserves the existing error stream and exit code.
- How does the renderer behave when the terminal width is unknown or very narrow?
  Content remains readable and never corrupts; rich layout adapts or degrades
  rather than failing.
- How is a report with no diagnostics shown versus one with many? Both are
  represented clearly without inventing or dropping facts.
- What happens for commands that are not lifecycle stages (e.g. agents, refresh)?
  Their reports are rendered by the same projection without implying a next
  lifecycle stage they do not have.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The SDD CLI MUST provide a rich, human-oriented terminal rendering
  of every command report produced by a parsed lifecycle, agents, or refresh
  invocation (including blocked/error outcomes for those commands). The renderer
  MUST be able to render any `CommandReport` shape, but the pre-parse bootstrap
  paths — an unparseable/unknown command and the no-arguments invocation — remain
  on the deterministic JSON-to-stderr contract and are not subject to format
  selection (see Assumptions and the output-format-selection contract).
- **FR-002**: The rich rendering MUST be a pure projection over the same command
  report object used by the JSON and plain-text projections; it MUST NOT add facts
  absent from the report. It MUST represent every human-relevant populated field of
  the report (outcome, work item, changed artifacts, the populated per-stage
  summary, generated-view currency, diagnostics, governance-compatibility facts,
  and next command). Report envelope metadata that exists only for the machine
  contract — `SchemaVersion`, `ReportVersion`, `ProjectRoot`, `OutputFormat`, and
  `OverwritePolicy` — is intentionally not surfaced in the rich projection and is
  not counted as an omitted fact.
- **FR-003**: JSON MUST remain the deterministic automation contract; selecting or
  enabling rich rendering MUST NOT change the JSON bytes, the report object, or any
  exit code for the same inputs.
- **FR-004**: Users MUST be able to explicitly select among JSON, plain-text, and
  rich output for any lifecycle command.
- **FR-005**: When output is non-interactive (redirected, piped, or a
  non-interactive/dumb terminal) or color is disabled, the CLI MUST NOT emit ANSI
  or color control sequences and MUST fall back to the plain-text rendering while
  preserving information content.
- **FR-006**: Blocked or error outcomes MUST continue to use the same output
  stream routing and exit-code behavior under the rich format as under the
  existing formats.
- **FR-007**: The rich rendering MUST visually distinguish the command outcome
  state, diagnostics grouped by severity, generated-view currency, and the
  recommended next lifecycle command.
- **FR-008**: The rich rendering MUST NOT be part of any deterministic golden or
  snapshot contract; determinism and byte-stability guarantees apply to the JSON
  projection only, and the existing plain-text projection MUST remain available
  and unchanged in meaning.
- **FR-009**: This feature MUST NOT introduce a new authored-source schema, new
  report fields, or a new lifecycle stage; it adds a presentation projection only
  and leaves the `charter … ship` lifecycle ordering unchanged.
- **FR-010**: Claude and Codex agent guidance and product documentation MUST be
  kept consistent with the available output formats so both agent surfaces
  describe rendering behavior equivalently.

### Key Entities *(include if feature involves data)*

- **Command report (existing)**: The structured per-command result object that
  already backs the JSON and plain-text projections (outcome, work item,
  per-stage summaries, diagnostics, generated-view currency, next command). This
  feature consumes it unchanged and adds no fields.
- **Output format selection (existing concept, extended)**: The user-facing notion
  of which projection to emit. This feature extends the existing format choice
  with a rich option while preserving JSON as the default automation contract and
  plain text as the portable fallback.
- **Rich rendering projection (new, presentation-only)**: A non-deterministic,
  terminal-targeted view computed from the command report. It is not an authored
  source, not a generated machine artifact, and not part of any digest or golden
  contract.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For 100% of supported commands, a human can identify the outcome
  state, the top blocking reason (if any), and the recommended next command from
  the rich output alone, without reading JSON or source. Verified objectively by
  asserting the rendered output contains the outcome badge, the first
  `Error`-severity diagnostic (when any), and the `NextAction` command.
- **SC-002**: For the same inputs, the default (automation) output bytes and the
  process exit code are identical before and after this feature; all existing
  deterministic JSON fixtures and exit-code expectations continue to pass.
- **SC-003**: When command output is redirected to a file or piped, zero ANSI or
  color control sequences appear in the captured output across all supported
  commands.
- **SC-004**: 100% of the human-relevant populated report fields (per FR-002 —
  excluding the machine-contract envelope metadata `SchemaVersion`,
  `ReportVersion`, `ProjectRoot`, `OutputFormat`, `OverwritePolicy`) are
  represented in the rich rendering, and zero facts absent from the report appear
  in it, verified by a projection-completeness check.
- **SC-005**: Selecting the rich format never changes which stream (stdout vs
  stderr) carries the output for a given outcome compared with the existing
  formats.

## Assumptions

- JSON remains the default output format and the sole deterministic/automation
  contract; rich rendering is opt-in and presentation-only, so existing CLI smoke
  evidence and JSON fixtures remain valid without change.
- Spectre.Console is the rich-rendering technology, as named by the implementation
  plan's ground rule that "Plain text and Spectre.Console output are projections
  over the same report objects."
- The existing plain-text projection is the fallback for non-interactive or
  color-disabled contexts; no separate degraded format is introduced.
- No new authored-source schema, report fields, or lifecycle stage are added; this
  is a cross-cutting presentation projection and does not alter
  `nextLifecycleCommand` ordering.
- Interactive-terminal detection and color suppression follow common conventions
  (TTY detection and a `NO_COLOR`-style signal); exact detection mechanics are an
  implementation detail for the plan.
- The pre-parse bootstrap paths — an unparseable/unknown command and the
  no-arguments invocation — remain on the deterministic JSON-to-stderr contract and
  are not routed through format selection; the rich renderer is still capable of
  rendering any `CommandReport` shape, but those paths are not routed through it.
- Governance-owned release/gate/route/profile/freshness concerns and the broader
  release publish pipeline remain out of scope.
