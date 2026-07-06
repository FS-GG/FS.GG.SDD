# Feature Specification: Lifecycle-Status Footer

**Feature Branch**: `084-lifecycle-status-footer`

**Created**: 2026-07-06

**Status**: Draft

**Input**: User description: "Standardized lifecycle-status footer on every fsgg-sdd command. Every command's output ends with a single standardized status display showing full SDD lifecycle progress (charter→…→ship): which stages are done, which is current, what's next, and any blocked stage. Any other report content must render BEFORE this footer. Per-stage marks are SENSED from artifacts on disk (work/<id>, readiness/<id>). Must be an additive structured fact on CommandReport (schema bump, additive-only) so all three projections share the same facts: --json carries it, --text prints a portable footer, --rich renders an elaborate Spectre panel that degrades byte-identically to text when non-interactive. Applies to every command including the cross-cutting verbs (marked 'not a lifecycle stage'). Early-stage commands with no work model still render a coherent footer. No new Governance dependency."

## Clarifications

### Session 2026-07-06

- Q: Where do the footer's failure explanation and options come from? → A: Reuse the facts the report already carries (Option A). On a blocked/failed outcome the footer surfaces the blocking diagnostic's message as the quick explanation and the existing remediation pointer(s) plus the next-action command as the options. No new contract field is added beyond the lifecycle-status fact; the rich projection may color/format these, but they remain the same facts already present in every projection.
- Q: How should the additive report contract be versioned (FR-006/SC-005)? → A: [Reconciled during `/speckit-plan`.] The command-report contract is classed additive-optional with a **stable** output schema version; the two prior additive-field features added fields without moving it. So the lifecycle-status field is additive, the stable schema version is held, the field is recorded in the report's field inventory, and the **semantic report version is bumped one minor** to signal the additive change. This supersedes the original FR-006/SC-005 wording ("increment the schema version"), preserving its traceability intent without breaking the additive-optional policy.
- Q: Should the footer be color-coded? → A: Yes. The rich projection color-codes each stage by its semantic state (done / current / next / pending / blocked) and gives a blocked or failed stage a distinct emphasis. Color is presentation-only: it adds no facts, is excluded from deterministic/golden contracts, and degrades to zero color/box control sequences when non-interactive/redirected/color-disabled.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See where I am in the lifecycle after every command (Priority: P1)

An author or agent driving the SDD lifecycle runs any `fsgg-sdd` command and, at the very end of the output, sees one standardized status display that shows the full ordered lifecycle (charter → specify → clarify → checklist → plan → tasks → analyze → evidence → verify → ship) with every stage marked as done, current, next, pending, or blocked, plus the work id, an "N of M" position, the current command's outcome, and the next command to run. The author never has to reconstruct progress from scattered fields or remember the stage order.

**Why this priority**: This is the whole feature — a single, always-present, at-a-glance progress display is the value. Without it there is nothing to ship.

**Independent Test**: Run any lifecycle-stage command in a work item and confirm the last element of the output is the status display, listing all ten stages with exactly one marked current, the correct set marked done, and a correct next-stage pointer.

**Acceptance Scenarios**:

1. **Given** a work item whose charter, spec, and clarifications exist on disk, **When** the author runs the `clarify` stage command, **Then** the output ends with a status display marking charter/specify/clarify as done through current, `checklist` as next, the remaining stages pending, showing the work id and a stage position (e.g. "3 of 10").
2. **Given** the same work item, **When** the author runs the command with the plain-text projection and again with the rich projection, **Then** both end with a status display carrying the identical set of stage states, work id, position, outcome, and next command — differing only in visual presentation.
3. **Given** any command run, **When** the author reads the output top to bottom, **Then** all other report content (changed artifacts, diagnostics, governance facts, next-action detail) appears strictly before the status display, and nothing is emitted after it.

---

### User Story 2 - Trust the display in scripts and CI (Priority: P1)

An automation consumer runs commands with the default machine-readable projection, or with human projections in a redirected/non-interactive context (CI logs, piped output, `NO_COLOR`). The lifecycle-status information is available as a structured, versioned fact in the machine-readable output, and the human projections degrade to plain, deterministic, color-free text that carries exactly the same facts — never richer, never poorer.

**Why this priority**: The lifecycle status is only trustworthy if it is a first-class contract fact rather than decoration. Parity across projections and safe degradation are non-negotiable in this product; a status display that only some projections carry, or that adds facts in the rich view, is a defect.

**Independent Test**: Capture a command's machine-readable output and confirm it contains the structured lifecycle-status fact; capture the same command's rich output once interactive and once redirected/color-disabled, and confirm the redirected form is byte-identical to the plain-text projection and contains no color/box control sequences, while both agree fact-for-fact with the machine-readable output.

**Acceptance Scenarios**:

1. **Given** the default machine-readable projection, **When** any command runs, **Then** the output includes a structured lifecycle-status fact enumerating every stage with its state, the work id, position, outcome, and next command, under an incremented output schema version, added without removing or renaming any existing field.
2. **Given** the rich projection with output redirected or color disabled, **When** any command runs, **Then** the status display is emitted with zero color/box control sequences and is byte-identical to the plain-text projection's footer.
3. **Given** any two projections of the same command run, **When** their lifecycle-status content is compared, **Then** neither shows a stage state, count, or pointer the other lacks.

---

### User Story 3 - Coherent footer everywhere, including early-stage and cross-cutting commands (Priority: P2)

The status display appears and is meaningful even when the work item is brand new (no work model or readiness artifacts yet) and even when the command is a cross-cutting verb that is not itself a lifecycle stage (the generators and remediation/validation verbs). In the early-stage case the display senses whatever authored artifacts exist and marks the rest pending; in the cross-cutting case it still shows the lifecycle rail but clearly flags that the command just run is not a lifecycle stage, so the "current" mark is not misattributed.

**Why this priority**: Consistency is the point of "standardized." A footer that only works for the ten stages, or that breaks/omits for a fresh work item or a `refresh`/`scaffold`/`doctor` run, would violate the "every command" promise — but these paths are secondary to the core lifecycle-stage experience.

**Independent Test**: Run a stage command in a work item that has only a charter authored, and separately run a cross-cutting command, and confirm each ends with a coherent status display — the first marking only sensed stages done and the rest pending, the second showing the rail with the run flagged as not-a-lifecycle-stage.

**Acceptance Scenarios**:

1. **Given** a work item with only a charter authored and no work model or readiness artifacts, **When** an early-stage command runs, **Then** the status display marks the sensed authored stages appropriately, marks the rest pending, and does not present a false "done" for stages whose artifacts are absent.
2. **Given** any cross-cutting command (a generator, a remediation verb, or the validation harness when it flows through the standard report), **When** it runs against a work item, **Then** the status display shows the full lifecycle rail sensed from disk and flags the command as not a lifecycle stage rather than marking one of the ten stages "current".
3. **Given** a work item that has progressed to a later stage, **When** a stage's artifact on disk is missing while a later stage's artifact is present, **Then** the display reflects the actual on-disk state rather than assuming contiguous completion.

### Edge Cases

- **No work id resolvable** (command run outside any work item, or ambiguous): the display still renders the lifecycle rail with all stages pending and clearly indicates that no work item is in scope, rather than erroring or omitting the footer.
- **Blocked outcome**: when the command is blocked by diagnostics, the display marks the relevant stage as blocked and still renders as the final element; it surfaces the blocking diagnostic's message as a quick explanation and the existing remediation pointer(s) plus next-action command as options (FR-017). If the report is routed to the error stream, the footer travels with it and still degrades to plain text.
- **Blocked outcome with no remediation pointer available**: the footer still shows the blocking explanation and at least the next-action command as an option; it never fabricates options beyond the facts the report carries.
- **A later-stage artifact exists but an earlier-stage artifact is missing** (non-contiguous progress): the display shows the true per-stage sensed state and does not fabricate completion of the skipped stage.
- **Stale or malformed stage artifact on disk**: presence-sensing marks the stage according to the defined rule; the footer does not attempt to re-validate stage artifacts or compute freshness (that remains a separate, out-of-scope concern) and does not crash on a malformed artifact.
- **Terminal stage reached** (`ship`): the display marks all stages done through the terminal stage and shows no next lifecycle command.
- **Very narrow or non-standard terminal width**: the rich display remains readable (wraps or scrolls within its own bounds) and never forces horizontal overflow of the surrounding output; the plain-text footer is width-independent.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Every `fsgg-sdd` command that produces the standard command report MUST include a single lifecycle-status display, and it MUST be the final element of the rendered human output (all other report content renders before it).
- **FR-002**: The lifecycle-status MUST enumerate the full canonical lifecycle in canonical order (charter → specify → clarify → checklist → plan → tasks → analyze → evidence → verify → ship), each stage carrying exactly one state from a defined vocabulary (at minimum: done, current, next, pending, blocked).
- **FR-003**: The lifecycle-status MUST include the resolved work id (or an explicit indication that none is in scope), the current stage position expressed as "N of M", the current command's outcome, and the next lifecycle command to run (or an explicit indication that there is none).
- **FR-004**: Each stage's done/pending state MUST be sensed from the presence of that stage's artifacts on disk under the work item's authored (`work/<id>`) and generated (`readiness/<id>`) locations, reflecting actual on-disk state including non-contiguous progress — not inferred solely from which command was invoked.
- **FR-005**: The lifecycle-status MUST exist as a structured, machine-readable fact on the command report, carried by the default machine-readable projection.
- **FR-006**: Adding the lifecycle-status fact MUST be additive to the report contract: it MUST be recorded once in the report's published field inventory, the report's semantic version MUST be bumped (a minor increment) to signal the additive change, and no existing field may be removed, renamed, or repurposed. The report's stable output schema version MUST remain unchanged, consistent with the product's additive-optional contract policy (a schema-version change is reserved for consumer-visible envelope changes). [Reconciled during planning — see Clarifications 2026-07-06.]
- **FR-007**: The plain-text projection MUST render the lifecycle-status as a deterministic, portable, color-free footer carrying the same facts as the machine-readable fact.
- **FR-008**: The rich projection MUST render the lifecycle-status as an elaborate visual display (a stage rail marked done/current/next/pending/blocked, with work id, "N of M" position, outcome, and next command) as the final rendered element.
- **FR-009**: The rich lifecycle-status display MUST degrade to zero color/box control sequences and be byte-identical to the plain-text footer whenever output is non-interactive, redirected, or color is disabled.
- **FR-010**: Across every projection of a given command run, the lifecycle-status MUST carry identical facts (stage states, work id, position, outcome, next command); no projection may add or drop a fact relative to the machine-readable contract.
- **FR-011**: The lifecycle-status MUST be produced for every command, including the cross-cutting verbs that are not themselves lifecycle stages; for those the display MUST render the full lifecycle rail and flag the command as not a lifecycle stage rather than marking one of the ten stages current.
- **FR-012**: The lifecycle-status MUST render coherently for an early-stage work item that has no work model or readiness artifacts yet, sensing whatever authored artifacts exist and marking the remaining stages pending, without erroring.
- **FR-013**: When a command is blocked, the lifecycle-status MUST mark the affected stage as blocked and still render as the final element, traveling with the report regardless of which output stream carries it; on a blocked or failed outcome it MUST additionally surface a quick failure explanation and options (see FR-017).
- **FR-014**: The lifecycle-status MUST NOT introduce any dependency on the Governance runtime or any artifact SDD does not itself own; it senses only SDD-owned artifacts.
- **FR-015**: Sensing and rendering the lifecycle-status MUST be deterministic for a given on-disk state, so the same work item in the same state yields the same footer facts on repeated runs.
- **FR-016**: The rich projection of the lifecycle-status MUST be color-coded, distinguishing each stage by its semantic state (done / current / next / pending / blocked) and giving a blocked or failed stage a distinct emphasis. Color is presentation-only: it MUST add no facts beyond the shared contract, MUST be excluded from deterministic/golden contracts, and MUST degrade to zero color/box control sequences under the conditions in FR-009.
- **FR-017**: On a blocked or failed outcome, the lifecycle-status footer MUST present a quick failure explanation and one or more options, sourced entirely from facts the report already carries — the blocking diagnostic's message as the explanation and the existing remediation pointer(s) plus the next-action command as the options. It MUST NOT introduce a new footer-specific explanation/options field or a second source of truth, and because the facts already exist in every projection, the explanation and options MUST appear (as the same facts) in the machine-readable, plain-text, and rich projections alike.

### Key Entities *(include if feature involves data)*

- **Lifecycle Status**: The new structured fact carried on the command report. Represents the whole lifecycle at a point in time: the ordered list of stage entries, the resolved work id (or its absence), the current position ("N of M"), the current command's outcome, the next lifecycle command (or its absence), and whether the just-run command is itself a lifecycle stage. On a blocked/failed outcome, the footer's failure explanation and options are **not** new fields on this entity — they are a compact presentation of facts the report already carries (the blocking diagnostic and the next-action/remediation pointers).
- **Stage Entry**: One element of the lifecycle status — a lifecycle stage's identity, its ordinal position, and its sensed state (done / current / next / pending / blocked).
- **Sensed Artifact Set**: The on-disk artifacts (under the work item's authored and generated locations) whose presence determines each stage's done/pending state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of commands that emit the standard command report end their human output with the lifecycle-status display as the final element, verified across the full command matrix.
- **SC-002**: For 100% of command runs, the lifecycle-status facts are identical across the machine-readable, plain-text, and rich projections (no fact added or dropped by any projection).
- **SC-003**: For 100% of rich-projection runs in a non-interactive/redirected/color-disabled context, the emitted status display contains zero color/box control sequences and is byte-identical to the plain-text footer.
- **SC-004**: An author can determine the current stage, the set of completed stages, and the next command for a work item from the footer alone, without consulting any other output or remembering the stage order — confirmed for both an early-stage work item and a mid-lifecycle work item.
- **SC-005**: The report contract change is additive-only: existing machine-readable consumers that ignore the new field continue to parse every prior field unchanged, the change is recorded exactly once in the report's field inventory, and the report's semantic version is bumped once (a minor increment) while the stable output schema version is unchanged.
- **SC-006**: The footer reflects true on-disk state, including a deliberately non-contiguous progress arrangement (a later-stage artifact present while an earlier-stage artifact is absent), rather than assuming contiguous completion.
- **SC-007**: On 100% of blocked/failed command runs, the footer shows a quick explanation of the failure and at least one actionable option, and every explanation/option shown is traceable to an existing report fact (a diagnostic, a remediation pointer, or the next-action command) — none is invented by the footer.
- **SC-008**: In the rich projection, each of the five stage states is visually distinguishable by color, and a blocked/failed stage is distinctly emphasized; the same run redirected/color-disabled shows the identical facts with zero color/box control sequences.

## Assumptions

- **Stage "done" means the stage's artifact is present on disk.** A stage is sensed done when its canonical authored/generated artifact(s) exist under `work/<id>`/`readiness/<id>`; the footer does not re-validate content or compute freshness/staleness (that remains the separate, out-of-scope concern it is today). "Current" is the stage of the command just run (for a lifecycle-stage command) or the furthest sensed-done stage's successor otherwise; "next" follows the canonical successor order; "blocked" is applied when the run is blocked at a stage.
- **The canonical lifecycle and its order already exist in the product** (the charter→…→ship sequence and the successor relation); this feature surfaces and senses against them rather than redefining them.
- **The status display attaches to the standard command report.** Outputs that do not flow through that report (e.g. the standalone validation report) are out of scope unless and until they adopt the report; the validation harness is included only if/where it already emits the standard report.
- **Work-item resolution reuses the existing mechanism** the CLI already uses to determine the work id in scope; no new resolution rules are introduced.
- **"Elaborate" for the rich display means a bounded, presentation-only panel** (stage rail + summary line) consistent with the product's existing rich-rendering conventions; it introduces no facts beyond the shared contract and is excluded from deterministic/golden contracts, as rich output is elsewhere.
- **Color coding is semantic, not fixed to an exact palette in the spec.** FR-016 requires each stage state to be visually distinguishable and a blocked/failed stage emphasized; the concrete color choices (and their alignment with the product's existing rich palette) are a presentation detail for planning, not a contract fact.
- **No persisted-artifact schema changes.** Only the in-report output contract gains an additive field and a version bump; no on-disk artifact schema (work model, readiness, provenance) changes.
