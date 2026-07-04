# Feature Specification: Clarify/checklist silent-failure grammars — real fix, actionable diagnostics, published contracts

**Feature ID**: 070-clarify-checklist-grammars
**Branch**: `070-clarify-checklist-grammars`
**Date**: 2026-07-04
**Roadmap**: closes [#105](https://github.com/FS-GG/FS.GG.SDD/issues/105) (cross-repo request from `FS-GG/.github`; epic `.github#165`)
**Source of truth**: Space Invaders consumer feedback §1.1–§1.2, filed as a
`[Blocker]` cross-repo request.

## Context

Issue #105 reports three load-bearing authoring grammars that **silently block**
a lifecycle stage while the artifact "looks" correct — the consumer lost hours
and resorted to `strings`-dumping the CLI to infer the grammar. The report was
written against a **published** CLI; before scoping we reproduced all three
against `HEAD` (build `Debug/net10.0`) and the current source. Ground truth:

| # | Reported trap | Behavior on `HEAD` | Action |
|---|---|---|---|
| 1 | `## Remaining Ambiguity` counting a `None. AMB-… resolved` disclaimer as blocking | **Reproduces.** `- None. AMB-001, AMB-002 resolved above.` → `blockingAmbiguities: 1`; `clarify` reports `diagnostics: 0` and points forward, then `checklist`/`plan` block. | **Fix parser + diagnostics** |
| 2 | `## Blocking Findings` counting a `- None.` bullet as a finding | **Already resolved.** `parseNonEmptySectionLines`/`isNoOutstandingSentinel` drop `- None.`; `plan` succeeds. | **Document + regression-lock** |
| 3 | `checklist` writes `status: checklisted`; no command transitions it to `checklistReady` | **No such state.** The token `checklisted` exists nowhere in `src/`; `checklist` writes `status: checklistReady` directly on a clean review (`needsCorrection` on failures). | **Improve diagnostic + document** |

The root cause of Trap 1: `Clarification.parseRemainingAmbiguity`
(`src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Clarification.fs`) keeps any line
carrying an `AMB-###`/`CQ-###` token and marks it `blocking` unless the line
contains `deferred`/`non-blocking` — it does **not** apply the
`Internal.isNoOutstandingSentinel` "no-outstanding disclaimer" filter that the
sibling `parseNonEmptySectionLines` (used for `## Blocking Findings`) already
applies. Traps 2 and 3 are the same class of problem already solved elsewhere;
the residual gap for them is that the accepted/rejected forms and the
auto-promotion behavior are **undocumented**, and the blocking diagnostics do not
name the recognized grammar, so an author still cannot self-clear the gate.

This feature closes the one genuine behavior bug, makes the two blocking
diagnostics grammar-actionable (the issue's *preferred* ask), and publishes the
two empty-section contracts as drift-guarded documentation + regression tests so
none of the three can silently recur. It deliberately does **not** introduce a
net-new `checklisted` intermediate state or a `--sign-off` verb: the current
design already auto-promotes a clean review to `checklistReady`, and adding that
machinery would re-introduce complexity to fix a bug that no longer exists.

## User Stories

**US1 (P1)** — As an SDD author, when I write a `## Remaining Ambiguity` section
whose body is a "None. AMB-001…AMB-005 resolved" disclaimer, I want that section
read as **zero** blocking ambiguities so `clarify`/`checklist`/`plan` do not block
on ambiguities I have already resolved — while a genuinely unresolved ambiguity
line still blocks exactly as today.

**US2 (P1)** — As an SDD author blocked at `checklist`/`plan`, I want the blocking
diagnostic to name the **recognized grammar** and the concrete route out (for a
blocking ambiguity: that `AMB-###` ids under `## Remaining Ambiguity` count as
unresolved unless the line is a `None…`/`No …` disclaimer or marked
`deferred`/`non-blocking`; for a non-ready checklist status: that a clean
`fsgg-sdd checklist` review writes `checklistReady` automatically and the remedy
is to clear blocking findings and re-run `checklist`, not hand-edit the status),
so I clear the gate in seconds instead of decompiling the CLI.

**US3 (P2)** — As an SDD author (or agent), I want the clarify
`## Remaining Ambiguity` empty-section rule and the checklist `## Blocking
Findings` empty-section rule published in `docs/reference/authoring-contracts.md`
with copyable accepted/rejected examples that are run through the **live parser**
by a drift guard, and restated in the `fs-gg-sdd-clarify` / `fs-gg-sdd-checklist`
skills, so the grammar is discoverable without reading source and cannot drift
from behavior.

## Requirements

- **FR-001**: `Clarification.parseRemainingAmbiguity` MUST exempt lines that
  satisfy the shared `isNoOutstandingSentinel` "no-outstanding disclaimer" rule
  (a bullet whose stripped text is empty, is the whole word `none` optionally
  qualified, or is a `No …` disclaimer naming a lifecycle-outstanding noun) from
  the remaining-ambiguity set, so such a line contributes **zero** to
  `BlockingAmbiguityCount` even when it names one or more `AMB-###`/`CQ-###`
  tokens. A line that is not a disclaimer and carries an `AMB-###`/`CQ-###` token
  MUST remain classified exactly as today (`acceptedDeferral`/`nonBlocking`/
  `blocking`).
- **FR-002**: The `unresolvedBlockingAmbiguity` diagnostic MUST carry a
  `correction` that names the recognized `## Remaining Ambiguity` grammar — that
  `AMB-###` ids under that heading count as unresolved unless the line is a
  `None…`/`No …` disclaimer or marked `deferred`/`non-blocking` — so the author
  can see why a line blocks and how to write it to not block.
- **FR-003**: The `failedChecklistPrerequisite` diagnostic for a non-`checklistReady`
  status MUST carry a `correction` that states a clean `fsgg-sdd checklist` review
  writes `status: checklistReady` automatically and directs the author to clear
  blocking findings and re-run `checklist` rather than hand-edit the status.
- **FR-004**: `docs/reference/authoring-contracts.md` MUST publish (a) the clarify
  `## Remaining Ambiguity` empty-section rule and (b) the checklist `## Blocking
  Findings` empty-section rule, each with copyable **accepted** (disclaimer →
  non-blocking) and **rejected** (genuine finding/ambiguity → blocking) examples,
  tagged so the `AuthoringDocsContractTests` drift guard runs every example
  through the live `Clarification`/`Checklist` parser and fails the build if the
  doc and the tool disagree.
- **FR-005**: The `fs-gg-sdd-clarify` and `fs-gg-sdd-checklist` skills MUST state
  the empty-section rule for their stage and, for checklist, that a clean review
  auto-writes `checklistReady` (no manual transition). The update MUST be applied
  byte-equivalently across every agent-skill root the repo maintains and the
  authored seeded source, so the drift guard over the seeded skills stays green.

## Acceptance Criteria

- **AC-1** (FR-001): Given `## Remaining Ambiguity` with body
  `- None. AMB-001, AMB-002, AMB-003 resolved above.`, when `clarify` parses it,
  then `blockingAmbiguities` is `0` and `checklist`/`plan` do not block on
  ambiguities.
- **AC-2** (FR-001): Given `## Remaining Ambiguity` with body
  `- AMB-001: The scoring rule is unclear.`, when `clarify` parses it, then that
  line is still counted as one `blocking` ambiguity (no regression).
- **AC-3** (FR-002/FR-003): Given a blocking ambiguity or a non-`checklistReady`
  status, when the stage blocks, then the emitted diagnostic's `correction` names
  the recognized grammar / the auto-promotion route as specified.
- **AC-4** (FR-004): Given the published doc examples, when the build runs, then
  the `AuthoringDocsContractTests` drift guard exercises each tagged clarify and
  checklist example through the live parser and passes; a doc example that
  contradicts the parser fails the build.
- **AC-5** (FR-005): Given the skills across every maintained root and the seeded
  source, when the seeded-skill drift guard runs, then all roots are byte-equal
  and green.
- **AC-6**: Given the pre-existing `- None.` `## Blocking Findings` behavior
  (Trap 2) and the `checklist` auto-write of `checklistReady` (Trap 3), when the
  suite runs, then regression tests assert both remain correct, so neither can
  silently regress.

## Success Criteria

- **SC-001**: The Trap-1 disclaimer form no longer blocks any lifecycle stage,
  verified end-to-end through the `clarify → checklist → plan` command path.
- **SC-002**: Both blocking diagnostics name the recognized grammar; an author
  can clear each gate from the diagnostic alone without reading source.
- **SC-003**: The clarify and checklist empty-section contracts are published and
  drift-guarded; the full suite passes with no new warnings.

## Scope / Non-goals

- **In scope**: the `parseRemainingAmbiguity` sentinel exemption; `correction`
  text for `unresolvedBlockingAmbiguity` and `failedChecklistPrerequisite`;
  drift-guarded doc contracts for the two empty-section rules; skill updates
  across all roots + seeded source; regression tests locking Traps 2 and 3.
- **Out of scope / explicitly rejected** (reconciles the issue against `HEAD`):
  a net-new `checklisted` intermediate state, a validated status enum/DU, a
  `fsgg-sdd checklist --sign-off` verb, and any `## Blocking Findings` parser
  change — Trap 2 is already correct and Trap 3's premise does not exist in
  current source. Governance gate enforcement and effective-evidence freshness
  remain out of scope.
