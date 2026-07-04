# Implementation Plan: Clarify/checklist silent-failure grammars

**Branch**: `070-clarify-checklist-grammars` | **Date**: 2026-07-04 | **Spec**: [spec.md](./spec.md)

## Summary

Close #105 with the empirically-correct scope (all three traps reproduced against
`HEAD`): fix the one genuine parser bug (Trap 1), make the two blocking
diagnostics name their grammar (the issue's preferred ask), and publish +
drift-guard + regression-lock the two empty-section contracts (Traps 2 & 3, which
are already correct). Tier-2 behavior change confined to one artifact parser and
two diagnostic constructors; no `.fsi`, schema, or JSON-shape change.

## Technical Context

- **Language**: F# on .NET 10 (`net10.0`); warnings-as-errors on (baseline build
  = 0 warnings / 0 errors).
- **Blast radius**:
  - `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Clarification.fs` —
    `parseRemainingAmbiguity` (the one behavior change).
  - `src/FS.GG.SDD.Commands/CommandReports/DiagnosticConstructors.fs` — the
    `correction` strings for `unresolvedBlockingAmbiguity` and
    `failedChecklistPrerequisite`.
  - `docs/reference/authoring-contracts.md` + `tests/FS.GG.SDD.Commands.Tests/AuthoringDocsContractTests.fs`
    — new drift-guarded clarify/checklist empty-section contracts.
  - The `fs-gg-sdd-clarify` / `fs-gg-sdd-checklist` skills across every maintained
    agent-skill root **and** the authored seeded source (see Skill fan-out below).
  - Regression tests under `tests/FS.GG.SDD.Artifacts.Tests` (or the Commands
    suite) for Traps 2 & 3.
- **No `.fsi` touched**: `parseRemainingAmbiguity` is an internal helper; the
  public `Clarification.parseClarificationFacts` signature is unchanged.
  Diagnostic `correction` text is not part of any golden/baseline contract.

## Approach

### FR-001 — Trap-1 parser fix (the real bug)

`Internal` is `[<AutoOpen>] module internal`, so `isNoOutstandingSentinel` is
already in scope in `Clarification.fs`. In `parseRemainingAmbiguity`
(`Clarification.fs:262`), drop any line for which `isNoOutstandingSentinel line`
is true **before** classifying it — mirroring `parseNonEmptySectionLines`
(`Internal.fs:249`) which already does this for `## Blocking Findings`. A
disclaimer line (`- None. AMB-… resolved`) is thereby excluded from the
remaining-ambiguity set and contributes 0 to `BlockingAmbiguityCount`; a genuine
`- AMB-001: … unclear.` line still passes the filter and is classified `blocking`
exactly as today. This is the minimal, consistent fix — the same disciplined
`none`/`No <noun>` distinction already trusted for findings.

### FR-002 / FR-003 — grammar-actionable diagnostics

In `DiagnosticConstructors.fs`, enrich the `correction` (the field the report
serializes; there is no separate `advice` field):

- `unresolvedBlockingAmbiguity` (`~L271`): correction names the recognized
  `## Remaining Ambiguity` grammar — that `AMB-###` ids under that heading count
  as unresolved unless the line is a `None…`/`No …` disclaimer or marked
  `deferred`/`non-blocking`.
- `failedChecklistPrerequisite` (`~L328`): correction states a clean
  `fsgg-sdd checklist` review writes `status: checklistReady` automatically and
  directs the author to clear blocking findings and re-run `checklist`, not
  hand-edit the status.

These are message-only changes (no id, severity, or field-shape change). Any
snapshot/golden test that pins the old correction text is updated in the same
commit.

### FR-004 — drift-guarded doc contracts

Extend `docs/reference/authoring-contracts.md` with two new sections and tagged
fenced examples following the existing `coverage:accepted`/`coverage:rejected`
pattern:

- Clarify `## Remaining Ambiguity`: `remaining-ambiguity:disclaimer` (accepted,
  non-blocking) and `remaining-ambiguity:blocking` (rejected, still blocks).
- Checklist `## Blocking Findings`: `blocking-findings:disclaimer` (dropped) and
  `blocking-findings:finding` (real, blocks).

Add drift-guard facts to `AuthoringDocsContractTests.fs` mirroring the coverage
tests: parse each tagged clarify example through `Clarification.parseClarificationFacts`
and assert `BlockingAmbiguityCount = 0` for disclaimers / `> 0` for genuine
blockers; parse each checklist example through the live `Checklist` parser and
assert the finding count is 0 for disclaimers / `> 0` for genuine findings. This
also serves as the Trap-2 regression lock.

### FR-005 — skill fan-out

Per CLAUDE.md the `fs-gg-sdd-*` skills are seeded byte-identically into three
agent-skill roots (`.claude/skills`, `.codex/skills`, `.agents/skills`) and are
pinned to an authored seeded source by a drift guard. Update the `fs-gg-sdd-clarify`
and `fs-gg-sdd-checklist` skills at the **authored seeded source**, then propagate
byte-identically to every root the seeded-skill drift guard checks. Discover the
exact source-of-truth path and the guard before editing (do not hand-sync roots
blind); run the guard to confirm parity. Content: the stage's empty-section rule,
and for checklist the auto-write of `checklistReady`.

### Trap-3 regression lock

Add a test asserting a clean `checklist` run writes `status: checklistReady`
(guarding against reintroduction of a `checklisted`-style intermediate), plus a
test that a non-`checklistReady` status yields the enriched
`failedChecklistPrerequisite` correction.

## Constitution Check

- **Structured artifacts are the machine contract** (II): the only behavior change
  is which lines `parseRemainingAmbiguity` counts; `BlockingAmbiguityCount` remains
  the authoritative signal. Prose disclaimers are explicitly non-authoritative —
  documented and drift-guarded. PASS.
- **Spec → FSI → tests → impl** (I): no public surface change; semantic tests
  through the public `parseClarificationFacts` / command path precede the fix. PASS.
- **Real evidence**: verification is the real `clarify → checklist → plan` command
  path on the reproduced fixture going from blocked to clean, a real full test run,
  and the real drift guards. PASS.
- **Claude/Codex parity**: skills updated equivalently across all roots. PASS.

## Risks

- **Over-broad sentinel**: `isNoOutstandingSentinel` could in principle drop a
  genuinely-blocking line that happens to start `No …`. Mitigated — the regex
  requires a lifecycle-outstanding noun, and AC-2 + the `remaining-ambiguity:blocking`
  drift example lock the genuine-blocker path. If any real fixture regresses, stop.
- **Hidden golden pinning the old correction text**: mitigated by a full
  `dotnet test` run; update any pinned snapshot in the same commit.
- **Skill root drift**: mitigated by editing the authored source and running the
  seeded-skill drift guard rather than hand-editing roots.
