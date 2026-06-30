# Feature Specification: Lifecycle/CLI Semantics Papercuts

**Feature Branch**: `048-lifecycle-cli-papercuts`

**Created**: 2026-06-30

**Status**: Draft

**Input**: User description: "start the next sdd owned item on the coordination board." → resolved to the next SDD-owned Coordination board item: **FS-GG/FS.GG.SDD#39 — Lifecycle/CLI semantics papercuts (§3.1–§3.5)**, a child of the TestSpec-tutorial framework-feedback epic FS-GG/.github#74.

## Context

A consumer agent drove a real product (the Pong TestSpec) end-to-end through the SDD
lifecycle (`charter → ship`) using `fsgg-sdd` 0.2.1 and reached green, but five
lifecycle/CLI behaviors surprised the first-run author and cost real time. Each one
makes a correct run *look* wrong, or makes a stale run *look* fixed, or blocks on an
authoring affordance that should be benign. The unifying problem is **trust in what a
stage reports**: an author must be able to fix an input, re-run, and have the report
truthfully reflect the new state — and must be able to discover how to drive the CLI
without trial and error.

This feature is scoped to the five reported papercuts. It changes lifecycle stage
re-evaluation semantics, ambiguity detection, the self-inflicted staleness advisory, and
adds `--help`. It does not change the artifact contracts those stages produce beyond what
is required to stop reporting a misleading state.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Re-running a stage after fixing its inputs reflects the fix truthfully (Priority: P1)

After an author corrects an authored source (e.g. fixes coverage, or edits `spec.md`)
and re-runs the same stage, the stage's reported result and its rendered artifact reflect
the corrected source — never a stale prior verdict that makes broken work look done or
done work look broken. (Covers §3.1 stale `checklist` results, §3.2 `specify` no-op.)

**Why this priority**: This is the most dangerous class — §3.1 makes a still-failing run
look "mostly fixed" (`succeededWithWarnings`) while `checklist.md` retains the old `fail`
rows; an author can ship believing a gate passed. §3.2 silently ignores edited spec
content, costing a wrong experiment. Both directly undermine the author's trust that "fix
and re-run" works, which is the core inner loop.

**Independent Test**: Author a work item to a passing/failing checklist state; change the
underlying source so the verdict should flip; re-run the stage; assert the reported result
and the rendered artifact both reflect the new source (no rows/verdicts derived from the
superseded snapshot remain).

**Acceptance Scenarios**:

1. **Given** a work item whose `checklist` previously recorded `fail` rows, **When** the
   author fixes the offending source and re-runs `checklist`, **Then** the stage purges
   results that were reviewed against the superseded source snapshot and re-derives them,
   so no stale `fail` row survives and the reported result matches the current sources
   (no manual deletion of `checklist.md` required).
2. **Given** a spec at `status: specified`, **When** the author edits `spec.md` and re-runs
   `specify`, **Then** the change is not silently dropped: either the edited content is
   re-ingested, or the report unambiguously states that `specify` only promotes the first
   draft and that downstream stages read the live file (so the author is never left
   believing an edit was ingested when it was not).
3. **Given** an unchanged source, **When** the author re-runs the stage, **Then** the stage
   still reports `noChange` and produces byte-identical output (determinism preserved).

---

### User Story 2 - A correct, complete run ends clean (Priority: P2)

When an author completes a stage correctly, the stage does not flag staleness that the
stage itself just caused. A fully-correct run reads as clean, not "not-quite-clean."
(Covers §3.4 `staleGeneratedView` re-introduced by `verify`/`ship`.)

**Why this priority**: `verify` and `ship` each write a readiness file, which immediately
marks `work-model.json` stale again, so those stages always end `succeededWithWarnings`
with a `staleGeneratedView` advisory that only a trailing `refresh` clears. It is advisory,
not blocking — hence P2 — but persistent false noise trains authors to ignore warnings and
hides real ones.

**Independent Test**: Drive a work item to a state where `verify`/`ship` should succeed
cleanly; run the stage; assert the result is clean (no `staleGeneratedView` advisory whose
sole cause is the stage writing its own readiness output).

**Acceptance Scenarios**:

1. **Given** a verification-ready work item with a current work model, **When** the author
   runs `verify`, **Then** the result does not carry a `staleGeneratedView` advisory caused
   solely by `verify` writing its own `readiness/<id>/verify.json`.
2. **Given** a ship-ready work item with a current work model, **When** the author runs
   `ship`, **Then** the result does not carry a `staleGeneratedView` advisory caused solely
   by `ship` writing its own `readiness/<id>/ship.json`.
3. **Given** a work model that is genuinely stale for an unrelated reason (an upstream
   authored source changed), **When** the author runs `verify`/`ship`, **Then** the
   `staleGeneratedView` advisory is still reported (real staleness is not suppressed).

---

### User Story 3 - "No open questions" is expressible without blocking the lifecycle (Priority: P3)

An author who wants to record that there are no outstanding ambiguities can do so in the
natural way without accidentally creating a blocking ambiguity. (Covers §3.3 any bullet
under `## Ambiguities` becomes a blocking item.)

**Why this priority**: Writing `- None outstanding…` under `## Ambiguities` created a
blocking `AMB-001` that halted `clarify`. It is a low-severity papercut with an existing
workaround (use a non-bulleted paragraph), so P3, but it punishes the obvious authoring
gesture and is undiscoverable.

**Independent Test**: Author a spec whose `## Ambiguities` section states "no open
questions" in list form; run `clarify`; assert no blocking ambiguity is raised and the
stage proceeds.

**Acceptance Scenarios**:

1. **Given** a spec whose `## Ambiguities` section expresses "none outstanding" (including
   as a bullet), **When** the author runs `clarify`, **Then** no blocking ambiguity is
   created and the stage treats the section as empty of real questions.
2. **Given** a spec with a genuine outstanding ambiguity bullet, **When** the author runs
   `clarify`, **Then** that ambiguity is still detected and blocks as before (real
   ambiguities are not lost).

---

### User Story 4 - CLI flags and commands are discoverable via `--help` (Priority: P3)

An author can ask the CLI how to use it — at the top level and per stage — and get usage
text instead of an error. (Covers §3.5 `--help` returns `unknownCommand`.)

**Why this priority**: `--help`/`-h` (and `<stage> --help`) currently return
`unknownCommand` JSON, forcing trial-and-error flag discovery. Important for first-run
ergonomics but not a correctness risk, so P3.

**Independent Test**: Invoke `fsgg-sdd --help` and `fsgg-sdd <stage> --help`; assert each
returns usage/flag listing content (not an `unknownCommand` error) and exit 0.

**Acceptance Scenarios**:

1. **Given** the CLI, **When** the author runs `fsgg-sdd --help` or `fsgg-sdd -h`, **Then**
   it returns top-level usage (available commands and global flags) and exits 0.
2. **Given** any lifecycle/cross-cutting command, **When** the author runs
   `fsgg-sdd <command> --help`, **Then** it returns that command's flag listing and exits 0.
3. **Given** help output, **When** rendered, **Then** it follows the existing three-way
   projection rules (default/`--json`, `--text`, `--rich`) and `--help` itself is not
   reported as an unknown command.

---

### Edge Cases

- **Partial source change**: an author fixes one of several `checklist` failures and leaves
  others — re-run must re-derive all rows against the current snapshot, keeping the
  still-failing ones as `fail` and clearing only the now-passing/superseded ones.
- **`specify` re-ingest vs. live-read divergence**: if the chosen resolution for §3.2 is to
  document rather than re-ingest, the report wording must hold for every downstream stage
  that reads the live file, so an author is never told one thing while a stage does another.
- **Ambiguity section with mixed content**: a "none outstanding" disclaimer plus a real
  ambiguity bullet — the real one must still block; only the disclaimer is ignored.
- **`--help` combined with other flags/subcommands** (e.g. `fsgg-sdd --help --json`,
  `fsgg-sdd verify --help`): help takes precedence and never falls through to command
  execution or to `unknownCommand`.
- **Genuinely unknown command** (e.g. `fsgg-sdd frobnicate`): must still report
  `unknownCommand` — adding `--help` must not weaken real unknown-command detection.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: On re-run, `checklist` MUST purge any review result that was derived against a
  superseded source snapshot and re-derive it from the current sources, so no stale `fail`
  (or `pass`) row survives in `checklist.md` after the underlying source is corrected. The
  reported result MUST reflect the current sources without the author manually deleting
  `checklist.md`.
- **FR-002**: After an author edits `spec.md` for a work item already at `status:
  specified`, re-running `specify` MUST NOT silently discard the edit. The system MUST
  either (a) re-ingest the changed file, or (b) return a report that unambiguously states
  `specify` only promotes the first draft and that downstream stages read the live file —
  so the author always knows whether the edit was ingested. (See Assumptions for the chosen
  default.)
- **FR-003**: `clarify` MUST NOT convert a "no outstanding ambiguities" disclaimer under
  `## Ambiguities` into a blocking ambiguity, regardless of whether it is written as prose
  or as a bullet. An author MUST have a supported way to express "no open questions" that
  does not block.
- **FR-004**: `clarify` MUST continue to detect and block on genuine outstanding
  ambiguities, including when a "none outstanding" disclaimer appears alongside a real
  ambiguity.
- **FR-005**: `verify` MUST NOT emit a `staleGeneratedView` advisory whose sole cause is
  `verify` writing its own `readiness/<id>/verify.json` during the same invocation.
- **FR-006**: `ship` MUST NOT emit a `staleGeneratedView` advisory whose sole cause is
  `ship` writing its own `readiness/<id>/ship.json` during the same invocation.
- **FR-007**: `verify` and `ship` MUST still surface `staleGeneratedView` when the
  generated view is stale for any reason other than the stage writing its own readiness
  output (e.g. an upstream authored source changed). Real staleness MUST NOT be suppressed.
- **FR-008**: The CLI MUST treat `--help` and `-h` as a help request at the top level,
  returning available commands and global flags and exiting 0 — never `unknownCommand`.
- **FR-009**: The CLI MUST support `<command> --help`/`-h` for every command (lifecycle and
  cross-cutting), returning that command's flag listing and exiting 0.
- **FR-010**: Help output MUST be rendered through the same `CommandReport` three-way
  projection (default/`--json`, `--text`, `--rich`) as all other commands, adding and
  dropping no facts across projections and changing no JSON byte relative to the projection
  contract; `--rich` degrades to zero-ANSI when non-interactive or color-disabled.
- **FR-011**: Adding `--help` MUST NOT weaken detection of genuinely unknown commands or
  unknown flags; those MUST still report `unknownCommand` (or the existing equivalent) at
  the existing exit code.
- **FR-012**: All re-evaluation, ambiguity, staleness, and help behaviors MUST remain
  deterministic — identical inputs produce byte-identical default/`--json` and `--text`
  output, and `noChange` is still reported when inputs are unchanged.
- **FR-013**: The Governance-owned effective-evidence-freshness and gate-enforcement
  boundary MUST be unaffected — these are SDD-owned reporting-clarity fixes and introduce
  no new Governance dependency or contract change.

### Key Entities *(include if data involved)*

- **Checklist result row**: a per-item review verdict in `checklist.md`, associated with
  the source snapshot it was reviewed against; the basis for purge-and-re-derive on re-run.
- **Source snapshot reference**: the digest/identity of the authored sources a generated
  result was derived from; used to detect "this result is stale because the source it was
  reviewed against was superseded."
- **Ambiguity item**: a detected open question (e.g. `AMB-001`) parsed from the spec's
  `## Ambiguities` section; must distinguish a real question from a "none outstanding"
  disclaimer.
- **`staleGeneratedView` advisory**: the non-blocking signal that a generated view
  (`work-model.json`) is older than its sources; must distinguish self-inflicted staleness
  from genuine upstream staleness.
- **Help report**: the `CommandReport` describing available commands/flags, projected the
  same three ways as every other command report.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After correcting a source that previously failed `checklist`, a single re-run
  of `checklist` yields a report and a `checklist.md` that contain zero rows derived from
  the superseded snapshot — with no manual file deletion. (Today this requires deleting and
  regenerating the file.)
- **SC-002**: Editing `spec.md` and re-running `specify` never leaves the author unable to
  tell whether the edit was ingested: in 100% of cases the report either reflects the new
  content or explicitly states the edit was not ingested and where it is read live.
- **SC-003**: A "no outstanding ambiguities" note under `## Ambiguities`, written either as
  prose or as a bullet, results in zero blocking ambiguities from `clarify`, while a spec
  containing a real ambiguity still blocks.
- **SC-004**: A fully-correct `verify` run and a fully-correct `ship` run each end in a
  clean result with zero `staleGeneratedView` advisories attributable solely to the stage
  writing its own readiness file — so the author no longer needs a trailing `refresh` to
  reach a clean state. Genuine upstream staleness is still reported in 100% of cases.
- **SC-005**: `fsgg-sdd --help`, `fsgg-sdd -h`, and `fsgg-sdd <command> --help` return
  usage/flag content and exit 0 for every command, with zero `unknownCommand` responses to
  a help request; genuinely unknown commands still return `unknownCommand`.
- **SC-006**: A first-run author can discover every command's flags without reading source
  or decompiling the CLI — help output lists them.

## Assumptions

- **Scope is exactly the five §3.1–§3.5 papercuts** from FS-GG/FS.GG.SDD#39; the broader
  epic FS-GG/.github#74 (skills vendoring, headless evidence, docs) is tracked by sibling
  issues and is out of scope here.
- **§3.2 default resolution**: the spec's opening preference was to re-ingest `spec.md` on
  `specify` when the file content has changed (option (a) in FR-002), because it removes the
  surprise rather than documenting around it and matches how `clarify`/`checklist` already
  read the live file. **Resolved (plan §3.2 / research D2): option (b), document-by-reporting**
  — re-ingest would violate the "specify promotes only the first draft" invariant, so the
  chosen behavior leaves authored bytes untouched and adds a deterministic report statement
  that `specify` promotes only the first draft and that downstream stages read the live file.
  Either way FR-002's "author always knows" outcome holds; the implemented path is (b).
- **§3.3 affordance**: an explicit "no ambiguities" affordance (recognized disclaimer
  and/or a sanctioned empty form) is acceptable; the exact recognized form is a plan-level
  detail, provided the obvious bullet form no longer blocks.
- **Determinism and the JSON automation contract are invariants**: these fixes are
  presentation/semantics clarity changes and must not alter the JSON contract beyond
  removing the misleading advisory/stale rows; `--rich` remains presentation-only and
  excluded from golden contracts.
- **No Governance change**: this work is SDD-owned and introduces no cross-repo contract
  change; the Coordination board item stays `Repo Scope: sdd`, `Phase: P2 SDD`,
  `Workstream: Lifecycle`.
- **CLI version baseline**: behavior is described relative to the reported `fsgg-sdd` 0.2.1
  run; the fixes land in a subsequent release.
