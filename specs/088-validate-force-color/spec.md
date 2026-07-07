# Feature Specification: Force-Color Override + Capture-Safe Markdown Report Card

**Feature Branch**: `088-validate-force-color`

**Created**: 2026-07-07

**Status**: Draft

**Input**: User description: "fsgg-sdd validate --rich: honor FORCE_COLOR and add a --force-color flag to bypass TTY detection when the caller explicitly wants ANSI (while continuing to honor NO_COLOR), and add a capture-safe Markdown \"report card\" projection of the validation-report so agents and logs get a first-class human artifact without ANSI. Implements FS.GG.SDD#172 (origin FS.GG.Audio workflow-feedback §3.6, re-routed from FS.GG.Rendering#157)."

## Overview

`fsgg-sdd` renders every command's human-oriented `--rich` output only when it senses a live, color-capable terminal: rich degrades to zero-ANSI plain text whenever stdout is redirected, `NO_COLOR` is set, or `TERM=dumb`. That degrade-to-plain rule is correct for pipes and logs in general, but it makes the human report **second-class under an agent harness**: an agent that captures stdout (a pipe, not a TTY) can never see the intended rich report, and even a PTY shim yields raw escape codes rather than a rendered artifact. There is today no way for a caller to say "I know you think this isn't a terminal — give me the color anyway," and no capture-safe human projection that is simultaneously readable and free of ANSI noise.

This feature closes that gap two ways, mirroring the two remedies proposed in FS.GG.SDD#172:

1. **A force-color override.** Honor the ecosystem-standard `FORCE_COLOR` environment variable and add an equivalent `--force-color` flag. Either one bypasses the interactivity/terminal-capability sensing so that `--rich` emits real ANSI even when stdout is redirected or `TERM=dumb`. `NO_COLOR` remains the safety valve and continues to win: when it is set, output stays plain regardless of any force-color request.

2. **A capture-safe Markdown "report card" projection** of the `validate` command's `validation-report`. This is a fourth output projection, selected by `--markdown`, that renders the same report facts as a deterministic, ANSI-free Markdown document — human-readable in a log, a PR comment, or an agent transcript, and safe to capture into a file. It gives automated and logged contexts a first-class human artifact without depending on a terminal at all.

Both changes are additive projections/overrides over the existing report facts. No JSON contract byte, exit code, stream-routing rule, or persisted schema changes.

## Clarifications

### Session 2026-07-07

- Q: When both `NO_COLOR` and a force-color signal (`FORCE_COLOR` or `--force-color`) are present, which wins? → A: **`NO_COLOR` wins** — output stays plain. `NO_COLOR` is the user's explicit opt-out and the issue directs us to "continue to honor `NO_COLOR`." Force-color only overrides *capability sensing* (redirection / `TERM=dumb`), never an explicit opt-out.
- Q: What does force-color override — just the redirected/non-interactive check, or also `TERM=dumb`? → A: **Both.** Force-color asserts "emit ANSI regardless of what you sensed," so it overrides the non-interactive/redirected gate **and** `TERM=dumb` (a capability proxy). It does **not** override `NO_COLOR`.
- Q: Is the force-color override scoped to `validate`, or does it apply to every command's `--rich` rendering? → A: **Every command.** The color/TTY gate is a single shared decision (`detectCapabilities` + the `resolve*` degrade branch), so `--force-color`/`FORCE_COLOR` uniformly re-enable rich ANSI for any command that supports `--rich`. Scoping it to `validate` alone would be an inconsistent surface.
- Q: Is the Markdown "report card" a `validate`-only projection of the `validation-report`, or a fourth general `CommandReport` projection for every command? → A: **`validate`-only**, of the `validation-report`, for this feature. The issue frames it as a report card of the `validation-report` (the harness output). A general fourth `CommandReport` projection is a larger surface change and is an explicit non-goal here.
- Q: How is `FORCE_COLOR` interpreted as a value — presence like `NO_COLOR`, or boolean-ish? → A: **Boolean-ish**, matching the `supports-color`/`chalk` ecosystem: unset, empty, or the literal `0` means "do not force"; any other value means "force." This avoids a spurious empty-`FORCE_COLOR=` leftover in an environment silently forcing color. (`NO_COLOR` keeps its own standard "present with any value, including empty" semantics — unchanged.)
- Q: Does `--markdown` participate in the format-selection precedence, and where? → A: Yes. Precedence becomes `--rich > --markdown > --text > --json > default(json)`. The flags are mutually-exclusive intents; precedence only decides the winner if a caller passes more than one.
- Q: Is the Markdown projection deterministic / part of golden contracts (unlike `--rich`)? → A: **Yes, deterministic.** Unlike `--rich` (presentation-only, excluded from golden contracts), the Markdown report card carries no ANSI, no width sensing, and no wall-clock/sensed data, so it is a stable text contract suitable for golden tests and for `--out` persistence.
- Q: When force-color is active and a report is routed to stderr (the Blocked path routes to stderr, other reports to stdout), does force-color apply to whichever sink is used? → A: **Yes** — force-color acts on the color/TTY gate for the sink the report is actually written to (stdout or stderr), uniformly. It re-enables ANSI on that sink; it never changes which sink a report routes to.
- Q: Does the new flag get a short alias (e.g. `--md`, `-F`)? → A: **No.** The surface is exactly the long flags `--markdown` and `--force-color`, matching the existing `--rich`/`--text`/`--json` long-flag convention. No aliases in this feature.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See rich color in captured/redirected output on request (Priority: P1)

An agent harness (or a CI log viewer, or a `tee`-to-file pipeline) captures `fsgg-sdd validate --rich` stdout through a pipe. Because the pipe is not a live TTY, rich normally degrades to plain text and the human loses the intended color report. The caller now sets `FORCE_COLOR=1` (or passes `--force-color`) to declare "emit ANSI regardless of what you sensed." The command renders the real rich, colored report to the captured stream. If the caller instead sets `NO_COLOR`, the output stays plain even with force-color present — the explicit opt-out always wins.

**Why this priority**: This is the core remedy in the issue — making the human `--rich` report first-class in automated/logged contexts. Without it, the rich report remains unreachable whenever stdout is not a terminal.

**Independent Test**: Run `validate --rich` with stdout redirected and `FORCE_COLOR=1` set; confirm the output contains ANSI escape sequences (rich rendering). Repeat with `--force-color` instead of the env var and confirm the same. Then add `NO_COLOR=1` alongside force-color and confirm the output is byte-identical to the plain `--text`/degraded projection (zero ANSI).

**Acceptance Scenarios**:

1. **Given** stdout is redirected (non-interactive) and no color signals are set, **When** the author runs `validate --rich`, **Then** output degrades to zero-ANSI plain text (unchanged baseline behavior).
2. **Given** stdout is redirected, **When** the author runs `validate --rich` with `FORCE_COLOR=1` (or `--force-color`), **Then** the command emits the rich projection with real ANSI escape sequences.
3. **Given** `TERM=dumb` with stdout redirected, **When** the author runs `validate --rich --force-color`, **Then** rich ANSI is still emitted (force-color overrides the `TERM=dumb` capability proxy).
4. **Given** both `NO_COLOR` and `FORCE_COLOR=1` (or `--force-color`) are set, **When** the author runs `validate --rich`, **Then** output stays plain text with zero ANSI (`NO_COLOR` wins).
5. **Given** `FORCE_COLOR=0` (or empty `FORCE_COLOR=`) with stdout redirected, **When** the author runs `validate --rich`, **Then** output degrades to plain text (that value does not force color).

---

### User Story 2 - Get a capture-safe human report card without a terminal (Priority: P1)

An agent, a CI job, or an author wants the human-readable content of the `validation-report` in a form that is readable in a log or a PR comment and safe to write to a file — without ANSI escape codes and without depending on a TTY. They run `fsgg-sdd validate --markdown`. The command emits a Markdown document that presents the same facts the rich report shows — the overall verdict, the five summary counts, a per-matrix rollup, and the specific non-passing cells — as headings, tables, and lists. The output is deterministic (no wall-clock, width, or ANSI), so it renders identically in any context and can be captured, committed, or diffed.

**Why this priority**: This is the second remedy in the issue and stands on its own: even without force-color, agents and logs get a first-class human artifact. It is the projection most useful for the reported agent-workflow pain (report cards in transcripts and PR bodies).

**Independent Test**: Run `validate --markdown` for a report with a mix of passing and non-passing matrices; confirm the output is valid Markdown containing the verdict, all five counts, a matrices table, and each non-passing cell — with zero ANSI escape sequences — and that two runs over the same report are byte-identical.

**Acceptance Scenarios**:

1. **Given** a `validation-report` with all cells passing, **When** the author runs `validate --markdown`, **Then** the output is a Markdown document stating the passed verdict and the five summary counts, contains zero ANSI escape sequences, and enumerates no non-passing cells.
2. **Given** a report with at least one failing / coverage-gap / not-validated / skipped cell, **When** the author runs `validate --markdown`, **Then** the Markdown surfaces each non-passing cell with its matrix coordinates and status, and passing cells are summarized rather than enumerated (parity with the rich projection's fact set).
3. **Given** the same report rendered twice, **When** the author runs `validate --markdown` each time, **Then** the two outputs are byte-identical (deterministic; no sensed/wall-clock/width data leaks in).
4. **Given** `validate --markdown --out <file>`, **When** the command runs, **Then** the Markdown projection is persisted to `<file>` (the deterministic projection is persisted, mirroring how `--out` persists JSON/plain today), and the exit code reflects only the report verdict.

---

### User Story 3 - Predictable projection selection and unchanged contracts (Priority: P2)

An author or automation relies on stable, predictable output selection. The four projection flags are mutually-exclusive intents with a defined precedence (`--rich > --markdown > --text > --json > default`), so passing more than one has a well-defined winner. The JSON automation contract, the exit codes, and the stdout/stderr routing are unchanged by any of the new surface: `--markdown` and force-color add and drop no report facts, and the default (no flag) output remains byte-identical JSON.

**Why this priority**: The value of the new surface depends on it not perturbing the existing deterministic contracts that automation already depends on. Important, but subordinate to the two capabilities above.

**Independent Test**: Confirm `validate` with no format flag still emits byte-identical default JSON; confirm `--rich --markdown` selects rich, `--markdown --text` selects Markdown; confirm the JSON bytes, exit code, and stream routing are identical whether or not `--force-color`/`FORCE_COLOR` is present.

**Acceptance Scenarios**:

1. **Given** multiple format flags on one invocation, **When** the author runs `validate --markdown --text --json`, **Then** the Markdown projection is selected (precedence `--rich > --markdown > --text > --json`).
2. **Given** any format selection, **When** force-color signals are present or absent, **Then** the JSON bytes, the exit code, and the stdout/stderr routing are unchanged (force-color affects only whether the rich projection emits ANSI).
3. **Given** the default invocation `validate`, **When** it runs with any combination of `FORCE_COLOR`/`--force-color`, **Then** the default JSON output is byte-identical to today's (force-color has no effect on non-rich projections).

---

### Edge Cases

- **Both signals set**: `NO_COLOR` present together with `FORCE_COLOR`/`--force-color` → plain output (`NO_COLOR` wins). This is the safety-valve invariant and is explicitly tested.
- **`FORCE_COLOR` non-forcing values**: unset, empty (`FORCE_COLOR=`), or `0` are treated as "do not force"; any other value forces. An accidental empty leftover in an environment does not silently force color.
- **Force-color on an already-interactive terminal**: a no-op relative to today (rich already renders); force-color must not double-apply or change interactive output.
- **`--force-color` on a non-`--rich` projection** (e.g. `validate --markdown --force-color`, or `validate --json --force-color`): force-color only governs the rich ANSI gate; it does not inject ANSI into Markdown, plain, or JSON output. Markdown and JSON stay zero-ANSI regardless.
- **`--markdown` when the report is empty / all-pass**: still emits a well-formed Markdown document (verdict + counts + "all cells pass" summary), never an empty file.
- **`--markdown --rich` together**: rich wins by precedence; the Markdown flag is inert on that invocation.
- **Missing optional report fields** (`schemaVersion`/`generatorVersion` absent): the Markdown projection omits them gracefully and is not a completeness failure, matching the rich projection's tolerance.

## Requirements *(mandatory)*

### Functional Requirements

#### Force-color override

- **FR-001**: The system MUST honor a `FORCE_COLOR` environment variable and an equivalent `--force-color` CLI flag as a request to emit rich ANSI output regardless of terminal-capability sensing (redirected/non-interactive stdout or `TERM=dumb`).
- **FR-002**: A force-color signal MUST override the non-interactive/redirected gate AND `TERM=dumb`, so that `--rich` emits real ANSI escape sequences in those cases.
- **FR-003**: A force-color signal MUST NOT override `NO_COLOR`: when `NO_COLOR` is set, output MUST remain zero-ANSI plain text even if `FORCE_COLOR`/`--force-color` is present. Precedence is `NO_COLOR > force-color > capability sensing`.
- **FR-004**: The system MUST interpret `FORCE_COLOR` boolean-ish: unset, empty, or the literal `0` do NOT force color; any other value forces color. (`NO_COLOR`'s existing "present with any value" semantics are unchanged.)
- **FR-005**: The force-color override MUST apply uniformly to every command that supports `--rich` (it acts on the shared color/TTY gate), not to `validate` alone.
- **FR-006**: Force-color MUST affect only whether the rich projection emits ANSI. It MUST NOT change the JSON bytes, plain-text bytes, Markdown bytes, exit code, or stdout/stderr routing of any projection.

#### Capture-safe Markdown report card

- **FR-007**: The `validate` command MUST offer a `--markdown` projection that renders the `validation-report` as a Markdown document.
- **FR-008**: The Markdown projection MUST present the same fact set the rich projection presents: the overall verdict, the five summary counts (passed / failed / skipped / coverage-gaps / not-validated), a per-matrix rollup, and every non-passing cell with its matrix coordinates and status; passing cells MUST be summarized, not enumerated.
- **FR-009**: The Markdown projection MUST be free of ANSI escape sequences and MUST NOT depend on terminal interactivity, window width, or wall-clock/sensed metadata — it MUST be deterministic and byte-identical across repeated runs over the same report.
- **FR-010**: The Markdown projection MUST invent no fact absent from the `validation-report` and MUST omit absent optional fields (e.g. `schemaVersion`/`generatorVersion`) without treating their absence as a failure.
- **FR-011**: `--markdown` MUST take part in the format-selection precedence, ordered `--rich > --markdown > --text > --json > default(json)`.
- **FR-012**: When `--markdown` is selected with `--out <file>`, the system MUST persist the Markdown projection to the file (consistent with `--out` persisting a deterministic projection today), and the exit code MUST continue to reflect only the report verdict.

#### Invariants

- **FR-013**: The default projection (no format flag) and the `--json`/`--text` projections MUST remain byte-identical to their current output; this feature adds projections and an override only.
- **FR-014**: The `validation-report` JSON contract and persisted schema MUST NOT change; the Markdown report card is a projection, not a new lifecycle artifact, and remains outside the `release-readiness.json` catalog (the existing declared exception).
- **FR-015**: Exit codes and stream routing MUST be unchanged: `validate` exits 0 iff the report's overall verdict passes, 1 otherwise, independent of the selected projection or force-color.

### Key Entities

- **Force-color signal**: the resolved boolean derived from the `FORCE_COLOR` environment variable (boolean-ish) OR the `--force-color` flag; an input to the color/TTY gate that re-enables ANSI unless `NO_COLOR` is set.
- **Terminal-capability decision**: the existing sensed state (interactive?, color-enabled?, width) plus the new force-color signal, jointly deciding whether `--rich` renders ANSI or degrades to plain.
- **Markdown report card**: a deterministic, ANSI-free Markdown projection of the `validation-report` — headings + verdict + summary counts + a matrices table + non-passing-cell lists — selected by `--markdown`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With stdout redirected, `validate --rich` emits zero-ANSI plain text by default and real ANSI when `FORCE_COLOR=1` or `--force-color` is set — a caller can obtain the colored human report from a captured (non-TTY) stream in exactly one added signal.
- **SC-002**: With both `NO_COLOR` and a force-color signal set, 100% of `--rich` invocations produce zero-ANSI output (the `NO_COLOR` safety valve is never overridden).
- **SC-003**: `validate --markdown` produces byte-identical output across repeated runs over the same report (deterministic), and that output contains zero ANSI escape sequences in every environment (interactive or not).
- **SC-004**: The Markdown report card surfaces the verdict, all five summary counts, and every non-passing cell — an author or agent can read the full pass/fail picture from the Markdown alone without consulting the JSON.
- **SC-005**: The default, `--json`, and `--text` outputs are byte-identical before and after this feature, and the exit code and stream routing are unaffected by force-color or `--markdown` in 100% of cases.
- **SC-006**: The force-color override behaves identically across every `--rich`-capable command (uniform gate), verified for at least one non-`validate` command in addition to `validate`.

## Assumptions

- The color/TTY gate is a single shared decision point (capability sensing plus the rich-degrade branch), so adding the force-color signal there makes the override uniform across commands without per-command wiring.
- `NO_COLOR` retains its ecosystem-standard "present with any value (including empty) disables color" semantics; only `FORCE_COLOR` gets the boolean-ish "empty/`0` = off" interpretation, matching the `supports-color`/`chalk` convention callers already expect.
- The Markdown report card is scoped to the `validate` command's `validation-report` in this feature; a general fourth `CommandReport` projection for all commands is out of scope (a possible later feature).
- The rich projection remains presentation-only and excluded from golden/deterministic contracts; the new Markdown projection, being ANSI-free and width-independent, is deterministic and IS suitable for golden tests and `--out` persistence.
- No Governance runtime, network, or new persisted artifact is required; this is a CLI-local projection/override change with no cross-repo contract impact beyond satisfying the FS.GG.SDD#172 request.
- The reported agent-harness pain (FS.GG.Audio workflow-feedback §3.6) is addressed by either remedy independently, so the two user stories are independently shippable; delivering both fully resolves the issue.
