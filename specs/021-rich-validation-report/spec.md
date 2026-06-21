# Feature Specification: Rich Spectre.Console Rendering of the `validation-report`

**Feature Branch**: `021-rich-validation-report`

**Created**: 2026-06-21

**Status**: Ready

**Change Tier**: Tier 1 (contracted change: adds two public functions to the CLI
`Rendering` module surface — `renderValidationRichTo` and `resolveValidation` —
and flips `fsgg-sdd validate --rich` from degrade-to-text to render-rich; no
`validation-report` schema, field, matrix, lifecycle stage, or `CommandReport`
change — FR-009).

**Input**: User description: "start the next item on the implementation plan."

> Resolved from `docs/initial-implementation-plan.md` and feature 020. All
> SDD-owned roadmap items through Phase 13 are shipped and merged (001–020),
> including feature `019-spectre-rendering` (rich projection of the per-command
> `CommandReport`) and feature `020-exhaustive-validation` (the `fsgg-sdd validate`
> harness and its deterministic `validation-report`). The one concrete SDD-owned
> deferral that remains is that feature 020 **deliberately scoped out the `--rich`
> projection of the `validation-report`** (research Decision 6; the
> `validation-report` is a contract distinct from `CommandReport`, so 020 shipped
> only `--json` + `--text` and made `validate --rich` degrade to `--text`). 020's
> own note records that `--rich` "can be added later as a pure projection without a
> contract change." This feature delivers that follow-on for the `validation-report`,
> mirroring how feature 019 added `--rich` for the `CommandReport`. Governance-owned
> release rules, gate schemas, route/profile enforcement, and freshness remain out
> of scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Triage an exhaustive validation run at a glance (Priority: P1)

A maintainer or coding agent runs `fsgg-sdd validate` (the on-demand / scheduled
exhaustive harness) in an interactive terminal. Today the harness emits either the
large deterministic `validation-report` JSON or a flat plain-text projection that
enumerates every matrix and every cell. With four matrices spanning dozens of
cells, the human-relevant question — *did the run pass, and if not, which matrix
and which cells failed?* — is buried. They want a rich terminal rendering that
makes the overall verdict, a per-matrix rollup, and the specific failing /
coverage-gap / not-validated cells (with their diagnostics) immediately scannable,
without reading JSON or counting plain-text lines.

**Why this priority**: The validation harness is complete and stable; the gap is
purely human ergonomics over a deliberately large cross-product. Making the
existing `validation-report` easy to triage in a terminal is the entire value of
this feature, and every other story builds on the rich projection existing at all.

**Independent Test**: Can be fully tested by running `fsgg-sdd validate` with the
rich format selected, in an interactive terminal, against a fixture that produces a
mix of passing and failing cells, and confirming the rendered output clearly
presents the overall verdict, the per-matrix status rollup, and each failing /
coverage-gap / not-validated cell with its coordinates and diagnostic — derived
entirely from the same `validation-report` object the JSON projection uses.

**Acceptance Scenarios**:

1. **Given** a validation run with at least one failing cell, **When** a user runs
   `fsgg-sdd validate` with the rich human format in an interactive terminal,
   **Then** the output visually distinguishes the overall verdict (passed vs not
   passed), shows the summary counts (passed / failed / skipped / coverage-gaps /
   not-validated), and lists each failing cell with its matrix name, cell
   coordinates, and failure diagnostic.
2. **Given** a validation run whose only non-passing cells are `coverageGap` or
   `notValidated`, **When** it is run with the rich format, **Then** those cells
   are visually emphasized as failing the run (because they drive the non-zero
   exit), distinct from `skippedWithReason` cells which do not fail the run, and
   the process exits with the same exit code as the other formats.
3. **Given** any validation run, **When** it is run with the rich format, **Then**
   every human-relevant fact present in the `validation-report` (overall verdict,
   summary counts, each matrix's name and dimensions, each non-passing cell's
   coordinates and status, failure diagnostics identifying matrix / coordinates /
   affected contract) is represented in the rendering and no fact absent from the
   report is invented.

---

### User Story 2 - Safe, clean output in non-interactive contexts (Priority: P1)

A user redirects a `fsgg-sdd validate` run to a file, pipes it to another tool, or
captures it in a scheduled CI log, or runs it on a terminal that does not support
color. They must never get ANSI color/control sequences corrupting captured
output, and automation must keep getting the byte-stable `validation-report` JSON
contract it already depends on — including the sensed-metadata fence that keeps the
deterministic comparison clock-, duration-, and host-free.

**Why this priority**: This repository's central discipline is that automation
truth never changes and deterministic output must not depend on terminal wrapping
or ANSI. The `validation-report` is explicitly byte-stable over identical inputs; a
rich renderer that leaked escape codes into a scheduled validation log or altered
the default machine output would be a direct regression, so safe degradation is as
critical as the rich rendering itself.

**Independent Test**: Can be fully tested by running `fsgg-sdd validate` with
output redirected to a file (non-interactive) and confirming the captured bytes
contain no ANSI/color control sequences, and by confirming the default (`--json`)
output bytes — including the normalized `sensed` fence — are unchanged from before
this feature for the same inputs.

**Acceptance Scenarios**:

1. **Given** `fsgg-sdd validate` run with output redirected to a file or pipe,
   **When** the rich format would otherwise apply, **Then** the captured output
   contains no ANSI/color escape sequences and falls back to the plain-text
   projection.
2. **Given** an environment that disables color (for example a `NO_COLOR`
   indicator or a non-interactive/dumb terminal), **When** `fsgg-sdd validate` is
   run, **Then** color and rich control sequences are suppressed while the
   information content is preserved.
3. **Given** the same source inputs, **When** the default (`--json`) format is
   used, **Then** the produced `validation-report` bytes (with `sensed` normalized
   as today) and the exit code are identical to the behavior before this feature.

---

### User Story 3 - Choose the right format for the task (Priority: P2)

A user (human or agent) wants to deliberately choose between the machine-readable
`validation-report` JSON, the portable plain-text projection, and rich terminal
output, depending on whether they are feeding automation, capturing a scheduled
log, or reading a run interactively. Today `--rich` on `validate` silently degrades
to `--text` even in an interactive terminal; this feature makes `--rich` actually
render richly when the terminal supports it.

**Why this priority**: The first two stories deliver and protect the rich
projection; explicit, predictable format selection makes it usable across the
human/agent/CI mix the harness serves. It is valuable but depends on the rich and
fallback behaviors existing first.

**Independent Test**: Can be fully tested by invoking `fsgg-sdd validate` three
ways — requesting JSON, plain text, and rich output — and confirming each returns
the corresponding projection of the same `validation-report` with a consistent
overall verdict and exit code, and that `--rich` produces rich output in an
interactive terminal rather than the plain-text fallback.

**Acceptance Scenarios**:

1. **Given** `fsgg-sdd validate`, **When** the user explicitly requests the JSON
   format (or passes no format flag), **Then** they receive the deterministic
   `validation-report` JSON projection unchanged.
2. **Given** `fsgg-sdd validate`, **When** the user explicitly requests plain text,
   **Then** they receive the existing portable plain-text projection unchanged.
3. **Given** `fsgg-sdd validate`, **When** the user explicitly requests the rich
   format, **Then** they receive the rich projection in interactive terminals and a
   clean plain-text fallback when output is non-interactive or color is disabled.

---

### Edge Cases

- What happens when `--rich` is requested but stdout is redirected, piped, or
  attached to a non-interactive/dumb terminal? Output falls back to the plain-text
  projection with no ANSI sequences.
- How does the renderer handle a very large cross-product (many matrices, dozens of
  cells)? The rich layout summarizes by matrix and by status (emphasizing the
  failing categories that drive the exit code) so the output stays scannable and
  never corrupts; it adapts or degrades rather than failing on narrow or
  unknown-width terminals.
- How does the renderer handle an all-pass run (zero failures)? It clearly presents
  a passing verdict and the per-matrix rollup without inventing diagnostics.
- How does the renderer handle a partial/interrupted run whose untouched cells are
  `notValidated`? Those cells are shown as failing the run (never as passing),
  consistent with the JSON contract.
- How are the distinct non-passing statuses differentiated? `fail`, `coverageGap`,
  and `notValidated` (which fail the run) are visually distinct from
  `skippedWithReason` (which does not), so a reader is not misled about what blocks.
- May the rich rendering show the `sensed` block (wall-clock start, duration, host)
  that the JSON contract fences out of its deterministic comparison? Yes — the rich
  projection is non-deterministic presentation outside any golden contract, so it
  may surface sensed metadata for human context; doing so must not change the JSON
  or plain-text bytes or the sensed fence.
- What happens when `--matrix <name>` narrows the run and `--rich` is also
  requested? The rich rendering presents exactly the matrices the report contains,
  with no implication of matrices that were not run.
- What happens when `--out <path>` is combined with `--rich`? The persisted file
  remains a deterministic projection (JSON or plain text), because rich is
  presentation-only and not a persisted artifact; rich affects interactive stdout
  display only.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The SDD CLI MUST provide a rich, human-oriented terminal rendering of
  the `validation-report` produced by `fsgg-sdd validate`, including runs with
  failing, coverage-gap, and not-validated cells.
- **FR-002**: The rich rendering MUST be a pure projection over the same
  `validation-report` object used by the JSON and plain-text projections; it MUST
  NOT add facts absent from the report. It MUST represent every human-relevant
  populated field of the report: the overall verdict (`overallPassed`), the summary
  counts (passed / failed / skipped / coverage-gaps / not-validated), each matrix's
  name and dimensions, every non-passing cell's coordinates and status, and each
  failure diagnostic (identifying the matrix, the cell coordinates, and the affected
  contract/artifact). Machine-contract envelope metadata that exists only for the
  JSON contract — `schemaVersion` and `generatorVersion` — is intentionally not
  required in the rich projection and is not counted as an omitted fact. The
  non-deterministic `sensed` block MAY be surfaced in the rich projection (it is
  outside any deterministic/golden contract) but MUST NOT be required.
- **FR-003**: The `validation-report` JSON MUST remain the deterministic automation
  contract; selecting or enabling rich rendering MUST NOT change the JSON bytes, the
  report object, the `sensed` fence, or any exit code for the same source inputs.
- **FR-004**: Users MUST be able to explicitly select among JSON, plain-text, and
  rich output for `fsgg-sdd validate`. Selecting rich MUST produce the rich
  projection in an interactive, color-capable terminal rather than silently
  degrading to plain text as it does today.
- **FR-005**: When output is non-interactive (redirected, piped, or a
  non-interactive/dumb terminal) or color is disabled (for example a `NO_COLOR`
  signal), the CLI MUST NOT emit ANSI or color control sequences and MUST fall back
  to the plain-text rendering while preserving information content.
- **FR-006**: The `validation-report` exit-code rule (non-zero iff any cell is
  `fail`, `coverageGap`, or `notValidated`; `skipped` and `pass` do not fail the
  run) and the output stream routing MUST be identical under the rich format and
  the existing formats.
- **FR-007**: The rich rendering MUST visually distinguish the overall verdict, a
  per-matrix status rollup, and the distinct cell statuses, emphasizing the failing
  categories (`fail`, `coverageGap`, `notValidated`) that drive a non-zero exit
  separately from `skippedWithReason` and `pass`.
- **FR-008**: The rich rendering MUST NOT be part of any deterministic golden or
  snapshot contract; determinism and byte-stability guarantees apply to the JSON
  projection only (with `sensed` normalized as today), and the existing plain-text
  projection MUST remain available and unchanged in meaning.
- **FR-009**: This feature MUST NOT introduce a new contract, a new
  `validation-report` field, a new matrix, or a new lifecycle stage; it adds a
  presentation projection only. `fsgg-sdd validate` remains a cross-cutting,
  CLI-level command (not a `SddCommand`), so the per-command `CommandReport`
  contract and the lifecycle command surface stay unchanged.
- **FR-010**: When the report is persisted to a caller-named path (`--out`), the
  persisted bytes MUST remain a deterministic projection (JSON or plain text); the
  rich projection is presentation-only for interactive stdout and is not written as
  a persisted artifact.
- **FR-011**: Claude and Codex agent guidance and product documentation — including
  the `validate` command contract note that currently records `--rich` as deferred
  — MUST be updated so both agent surfaces and the docs describe the now-available
  rich format for `fsgg-sdd validate` equivalently.

### Key Entities *(include if feature involves data)*

- **`validation-report` (existing)**: The deterministic JSON artifact emitted by
  `fsgg-sdd validate` — `schemaVersion`, `generatorVersion`, the four matrices each
  with `name` / `dimensions` / `cells`, every cell's `coordinates` and tagged
  `status` (`pass` / `fail` / `skippedWithReason` / `coverageGap` / `notValidated`),
  the `summary` counts plus `overallPassed`, and the fenced `sensed` block. This
  feature consumes it unchanged and adds no fields.
- **Output format selection for `validate` (existing concept, extended)**: The
  user-facing choice of which projection to emit. Today it offers `--json` (default,
  deterministic contract) and `--text`, with `--rich` accepted but degraded to
  `--text`. This feature extends it so `--rich` produces a real rich projection in
  interactive terminals while preserving JSON as the default automation contract and
  plain text as the portable fallback.
- **Rich validation rendering (new, presentation-only)**: A non-deterministic,
  terminal-targeted view computed from the `validation-report`. It is not an
  authored source, not a generated machine artifact, not a persisted output, and not
  part of any digest or golden contract.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a validation run with failures, a human can identify the overall
  verdict, the count of failing cells, and at least the first failing cell's matrix
  and coordinates from the rich output alone, without reading JSON. Verified
  objectively by asserting the rendered output contains the verdict indicator, the
  summary counts, and the first non-passing cell's matrix name and coordinates.
- **SC-002**: For the same source inputs, the default (`--json`) `validation-report`
  bytes (with `sensed` normalized as today) and the process exit code are identical
  before and after this feature; all existing deterministic `validation-report`
  byte-stability fixtures and exit-code expectations continue to pass.
- **SC-003**: When `fsgg-sdd validate` output is redirected to a file or piped, zero
  ANSI or color control sequences appear in the captured output.
- **SC-004**: 100% of the human-relevant populated report facts (per FR-002 —
  overall verdict, summary counts, each matrix's name and dimensions, every
  non-passing cell's coordinates and status, and failure diagnostics; excluding the
  machine-contract envelope metadata `schemaVersion`/`generatorVersion`) are
  represented in the rich rendering, and zero facts absent from the report appear in
  it, verified by a projection-completeness check.
- **SC-005**: Selecting the rich format never changes which stream (stdout vs
  stderr) carries the output for a given verdict compared with the JSON and
  plain-text formats.

## Assumptions

- Spectre.Console is the rich-rendering technology, consistent with feature 019 and
  the implementation plan's ground rule that "Plain text and Spectre.Console output
  are projections over the same report objects." Note: feature 020 deliberately gave
  the new `FS.GG.SDD.Validation` library no Spectre dependency; this feature adds the
  rich projection at the CLI/presentation edge (as feature 019 did) so the
  validation library and its `validation-report` contract stay dependency-free and
  byte-for-byte unchanged.
- JSON remains the default output format and the sole deterministic/automation
  contract for the `validation-report`; rich rendering is opt-in and
  presentation-only, so existing CLI smoke evidence, byte-stability fixtures, and the
  `sensed` fence remain valid without change.
- The existing plain-text projection is the fallback for non-interactive or
  color-disabled contexts; no separate degraded format is introduced. This replaces
  only the current interactive-terminal behavior where `--rich` degrades to `--text`.
- The rich projection is non-deterministic and outside every golden/snapshot
  contract; it may therefore surface the `sensed` block (wall-clock start, duration,
  host) for human context, which the JSON contract intentionally normalizes out of
  its deterministic comparison.
- No new contract, `validation-report` field, matrix, or lifecycle stage is added,
  and the per-command `CommandReport` contract is untouched; `fsgg-sdd validate`
  stays a cross-cutting CLI-level command, not a `SddCommand`.
- Interactive-terminal detection and color suppression follow the same conventions
  already used for the lifecycle commands' rich projection (TTY detection and a
  `NO_COLOR`-style signal); exact detection mechanics are an implementation detail
  for the plan.
- Governance-owned release/gate/route/profile/freshness concerns and the broader
  release publish pipeline remain out of scope.
