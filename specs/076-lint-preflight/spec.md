# Feature Specification: Pre-flight authoring lint

**Feature Branch**: `076-lint-preflight`

**Created**: 2026-07-05

**Status**: Draft

**Input**: User description: "Add `fsgg-sdd lint <artifact>` (and `<stage> --explain`) pre-flight static validation that flags load-bearing authoring-grammar defects BEFORE a stage blocks: mis-formatted checklist coverage lines (the 'counted but uncovered' silent case), missing `[AMB:AMB-###]` decision tags on ambiguity resolutions, incomplete/wrong per-stage front matter, and duplicate ids. Each defect reports a fix hint plus a pointer to the shipped example/grammar section. Exercised on the canonical `docs/examples/lifecycle-artifacts/*` (clean) and on deliberately-broken fixtures (each defect class caught). Source: TD1 Bulwark field-feedback report `FEEDBACK.md` §3.1/§3.6/§4.2/Rec #4; tracked as issue #123 under epic #127."

## Clarifications

### Session 2026-07-05

- Q: What command surface should this feature deliver? → A: Both surfaces now — the standalone
  `fsgg-sdd lint <artifact>` verb **and** the in-stage `<stage> --explain` dry run, running the
  same checks.
- Q: What exit codes when a well-formed artifact has defects vs. a missing/unreadable/unrecognized
  artifact? → A: clean = 0, defects found = 1, unusable input (missing/unreadable/unrecognized) = 2.
- Q: Severity split, or are all findings failures? → A: All findings are errors — any reported
  defect fails the run; there is no advisory/warning severity in this feature.

## User Scenarios & Testing *(mandatory)*

The load-bearing SDD authoring grammars fail **silently or with under-specified errors**: a
defect is only surfaced when the stage itself blocks, after the author has already invested a
full stage run. The TD1 *Bulwark* field run cost 2–4 wasted iterations per grammar because the
failure arrived late and under-explained. This feature lets an author **statically pre-flight an
authored artifact and see every grammar defect — with a fix hint and a pointer to the shipped
grammar — before running the stage that would otherwise block on it.**

### User Story 1 - Pre-flight one authored artifact before running its stage (Priority: P1)

An author has drafted an SDD artifact (e.g. `clarifications.md`, a checklist, `evidence.yml`, or
a front-matter-bearing stage document) and, before running the stage, runs a single command to
statically check it for the known load-bearing grammar defects. Each defect is reported with its
location, a concrete fix hint, and a pointer to the shipped example / grammar section, so the
author fixes it in one pass instead of discovering it through a late, terse stage block.

**Why this priority**: This is the whole value — it converts silent/late blocks into an
up-front, actionable list. Everything else builds on this capability. Shipping only this story
already erases most of §3.1's wasted iterations.

**Independent Test**: Run the command against a deliberately-broken artifact that contains each
of the four defect classes and confirm every defect is reported with a fix hint and a grammar
pointer, without running any lifecycle stage and without mutating any file.

**Acceptance Scenarios**:

1. **Given** a checklist whose coverage line is mis-formatted so it silently fails to bind an FR
   to its AC (the "counted but uncovered" case), **When** the author lints the checklist,
   **Then** the defect is reported with a fix hint showing the correct `- FR-###: … (covers
   AC-###)` form and a pointer to the coverage-line grammar section.
2. **Given** a clarifications artifact where an ambiguity is resolved by prose that lacks the
   `[AMB:AMB-###]` decision tag, **When** the author lints it, **Then** the missing-tag defect is
   reported with the decision-tag resolution grammar pointer.
3. **Given** a stage document whose front matter is missing one or more required per-stage
   fields, **When** the author lints it, **Then** each missing/invalid field is named with a
   pointer to the per-stage front-matter field set.
4. **Given** an artifact that declares the same stable id twice, **When** the author lints it,
   **Then** the duplicate-id defect is reported naming both occurrences.
5. **Given** any artifact with at least one defect, **When** the author lints it, **Then** the
   command exits non-zero and writes/mutates nothing.

---

### User Story 2 - Trust: canonical clean artifacts lint clean (Priority: P2)

An author (or CI) runs the pre-flight over the canonical shipped example artifacts and gets a
zero-defect pass. A linter that cries wolf on known-good input is abandoned; the pre-flight must
have **no false positives** on the artifacts the project itself ships as the grammar of record.

**Why this priority**: Trust/adoption. Without a clean pass on the canonical examples the
feature is not usable as a gate and authors will stop running it.

**Independent Test**: Lint each artifact under `docs/examples/lifecycle-artifacts/*` and confirm
zero defects and a success exit code.

**Acceptance Scenarios**:

1. **Given** the canonical `docs/examples/lifecycle-artifacts/checklist.md`,
   `clarifications.md`, `evidence.yml`, and `tasks.yml`, **When** each is linted, **Then** zero
   defects are reported and the command exits 0.

---

### User Story 3 - Pre-flight in-line while running a stage (Priority: P3)

Rather than remembering a separate command, an author asks a stage to explain what it would
block on without advancing or mutating anything — a non-blocking dry run of the same grammar
checks surfaced during the normal stage flow.

**Why this priority**: Convenience and discoverability. It reuses the same checks as Story 1 on
the stage's own artifact, lowering the chance an author never learns the pre-flight exists. It is
strictly additive over Story 1.

**Independent Test**: Invoke a stage in explain mode against an artifact with defects and confirm
it reports the same defects as the standalone lint, advances no state, and mutates nothing.

**Acceptance Scenarios**:

1. **Given** a stage whose input artifact has grammar defects, **When** the author runs that
   stage in explain mode, **Then** the same defect list is reported, no lifecycle state file is
   written, and the stage does not advance.

---

### Edge Cases

- **Unrecognized / unsupported artifact**: linting a path that isn't a recognized SDD artifact
  reports a clear "cannot determine artifact kind" result (user-input failure), not a crash.
- **Missing file / unreadable path**: reported as a user-input failure with the path, not a stack
  trace.
- **Malformed but parseable vs unparseable**: a document too malformed to parse at all reports a
  single parse-level defect rather than a cascade of misleading grammar defects.
- **Empty / placeholder digests**: `Source Snapshot` `sha256:` placeholder digests are optional
  and MUST NOT be reported as defects (they are not a lint concern — see authoring-contracts).
- **Multiple defect classes in one artifact**: all applicable classes are reported together in
  one run, deterministically ordered, not just the first.
- **Artifact kind with no applicable checks**: linting an artifact for which no grammar checks
  apply reports a clean pass, not an error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a pre-flight check that an author can run against a single
  authored SDD artifact **without running or advancing any lifecycle stage**.
- **FR-002**: The system MUST auto-detect the artifact kind / applicable lifecycle stage from the
  artifact (e.g. its front matter and/or filename) and apply only the grammar checks that pertain
  to that kind.
- **FR-003**: The system MUST detect mis-formatted checklist coverage lines, including the
  **"counted but uncovered" silent case** where a line looks like a coverage line but is
  malformed so it fails to bind an FR to its AC.
- **FR-004**: The system MUST detect ambiguity resolutions that are missing the required
  `[AMB:AMB-###]` decision-tag (the decision-tag resolution grammar).
- **FR-005**: The system MUST detect incomplete or invalid per-stage front matter — each required
  field absent from, or invalid in, the stage's required field set.
- **FR-006**: The system MUST detect duplicate stable ids across the artifact id families it
  already recognizes (clarification, checklist, task, plan, specification, work ids).
- **FR-007**: Each reported defect MUST include (a) a concrete, actionable fix hint and (b) a
  resolvable pointer to the shipped example and/or the authoring-contracts grammar section that
  documents the correct form.
- **FR-008**: The pre-flight MUST be strictly **read-only** — it writes no file, mutates no
  artifact, and emits no lifecycle state / readiness file.
- **FR-009**: The pre-flight MUST NOT be a lifecycle stage: it advances nothing, and its next
  lifecycle command is `None` (cross-cutting, reachable only via its own command surface).
- **FR-010**: The command MUST project its result the same three ways as every other command —
  deterministic JSON (default), plain `--text`, and rich `--rich` — with the established
  precedence, adding/dropping no facts across projections and changing no JSON byte for `--rich`.
- **FR-011**: The command MUST use these exit codes so it is usable as a CI/pre-commit gate:
  **0** when the artifact is clean (zero defects), **1** when one or more defects are found in a
  well-formed artifact, and **2** when the input is unusable (missing, unreadable, or an
  unrecognized artifact kind — the run could not be performed). CI can thus distinguish "found
  defects" from "could not run".
- **FR-012**: Output MUST be deterministic — the same artifact bytes produce a byte-identical JSON
  report across runs, with a stable defect ordering.
- **FR-013**: The canonical shipped example artifacts under `docs/examples/lifecycle-artifacts/*`
  MUST lint with **zero defects** (no false positives on the grammar of record).
- **FR-014**: The system MUST report all applicable defects found in a single run (it does not
  stop at the first defect), each carrying its own location within the artifact.
- **FR-015**: When an artifact is too malformed to parse, the system MUST report a single
  parse-level defect rather than a cascade of misleading downstream grammar defects.
- **FR-016**: The system MUST offer an in-stage "explain" mode (`<stage> --explain`) that runs the
  same grammar checks against the stage's own artifact as a non-blocking dry run — reporting the
  same defects while advancing no state and mutating nothing. Both surfaces (standalone `lint` and
  `<stage> --explain`) are in scope for this feature and share one check implementation.
- **FR-017**: Every reported defect MUST be an **error** — there is no advisory/warning severity in
  this feature; any reported defect fails the run (exit 1). Conditions that are explicitly *not*
  defects (e.g. optional `sha256:` Source-Snapshot digests) are simply not reported.

### Key Entities *(include if feature involves data)*

- **Lint defect**: a single detected grammar problem — its defect class (coverage-line,
  missing-decision-tag, front-matter, duplicate-id, parse), its location within the artifact, a
  human fix hint, and a pointer to the governing grammar section/example. All defects are errors
  (there is no warning severity in this feature).
- **Lint report**: the deterministic result of one pre-flight run over one artifact — the
  detected artifact kind, the ordered set of defects (possibly empty), and the pass/fail outcome
  that drives the exit code; projected as JSON/text/rich like every other command report.
- **Grammar pointer**: a stable reference from a defect to the shipped example and/or the
  authoring-contracts section that documents the correct form (the §3.6 "point at the grammar"
  requirement).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a deliberately-broken fixture containing all four load-bearing defect classes
  (coverage line, missing `[AMB:]` tag, incomplete front matter, duplicate id), a single
  pre-flight run reports **every** class — 4/4 caught — with no stage run required.
- **SC-002**: The canonical `docs/examples/lifecycle-artifacts/*` artifacts produce **0** defects
  and a success exit — zero false positives on the grammar of record.
- **SC-003**: **100%** of reported defects carry both a concrete fix hint and a resolvable pointer
  to a grammar section/example.
- **SC-004**: Each §3.1 friction scenario (clarify `[AMB:]` decision tag, per-stage front matter,
  checklist coverage line) is catchable pre-flight — an author resolves the defect **without
  first running the blocking stage**.
- **SC-005**: The JSON report for a given artifact is **byte-identical** across repeated runs
  (deterministic), and defect ordering is stable.
- **SC-006**: The pre-flight is directly usable as a CI/pre-commit gate: a clean artifact yields
  exit 0, a defect-bearing well-formed artifact yields exit 1, and an unusable input (missing /
  unreadable / unrecognized) yields exit 2 — three distinguishable outcomes.

## Assumptions

- **Both surfaces ship in this feature** (clarified 2026-07-05): the standalone `lint <artifact>`
  verb and the in-stage `<stage> --explain` dry run (FR-016). Both run the **same** checks over the
  same grammars — one capability with two entry points, not two behaviors.
- **Scope is the existing load-bearing grammars**, not new validation. The four defect classes are
  exactly the ones the lifecycle already enforces (and that TD1 hit): coverage lines, decision
  tags, per-stage front matter, duplicate ids. The pre-flight reuses those existing rules; it does
  not invent stricter ones. Governance-owned concerns (evidence freshness, gate enforcement) are
  out of scope.
- **The authoring-contracts document and the shipped examples are the pointer targets** — the same
  grammar sections (`docs/reference/authoring-contracts.md`) and canonical example artifacts that
  §3 identifies as the sole current home of these grammars.
- **`lint` takes a single artifact path** and auto-detects its kind; linting an entire work item
  in one invocation is out of scope for this feature (a possible additive follow-up).
- **`sha256:` Source-Snapshot digests are optional** and are never a lint defect, per the existing
  authoring-contracts guidance.
- This feature is **generic SDD**: it embeds no product-, provider-, or rendering-specific
  knowledge, consistent with the repo boundary.
