# Implementation Plan: Early-Stage Authoring Seeds

**Branch**: `089-early-stage-authoring-seeds` | **Date**: 2026-07-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/089-early-stage-authoring-seeds/spec.md`

## Summary

Close the two early-stage authoring gaps from FS-GG/FS.GG.SDD#174. `specify` seeds a
feature-shaped `US-001`/`AC-001` derived from the invocation's user value instead of two
sentences about the SDD process. `clarify`, when it blocks on unanswered ambiguities, writes a
truthful `clarifications.md` skeleton (`status: needsAnswers`, every unanswered ambiguity listed
as blocking) instead of leaving an empty work directory.

Phase 0 research turned up three facts that reshape the work relative to the issue text:

1. A blocked command writes **nothing at all**, because `runHandler` discards every effect when
   any diagnostic is blocking (`Prerequisites.fs:139`). The skeleton needs a deliberate,
   single-line carve-out in that gate — not a change to what text `clarify` computes (research D3).
2. Reusing the existing template on the blocked path would write `status: clarified` and
   "No blocking ambiguity remains." while the command blocks — a false artifact (research D4).
3. A skeleton that correctly lists its ambiguities as blocking is **unresolvable**: `clarify` only
   ever appends to Remaining Ambiguity, so answering every ambiguity leaves the blocking lines
   standing, `clarify` reports `succeeded` with `blockingAmbiguities: 2`, and `checklist` blocks at
   exit 1. Verified against the running CLI. Retiring a resolved ambiguity's line (FR-018) is both
   necessary and sufficient (research D4).

So the plan is: reshape the seed (small), carve out one seed-on-blocked effect channel (surgical),
make the template truthful (small), and retire resolved Remaining Ambiguity lines plus stale
empty-state placeholders (the correctness work that makes the skeleton usable).

**Change tier**: Tier 2 (internal). No `.fsi` exists for the touched modules
(`EarlyStageAuthoring.fs`, `HandlersEarly.fs`, `Prerequisites.fs` are all `internal`, signature-less),
no persisted-artifact schema moves, no `changedArtifacts` write-kind vocabulary changes, no new
diagnostic code, no registry entry. The observable deltas are the *content* of two authored
artifacts and the fact that one blocked command now writes one file.

## Technical Context

**Language/Version**: F# on .NET, `net10.0`

**Primary Dependencies**: FSharp.Core; Spectre.Console (presentation edge only — untouched here)

**Storage**: Filesystem lifecycle artifacts under `work/<id>/` and `readiness/<id>/`

**Testing**: xUnit via `tests/FS.GG.SDD.Commands.Tests` (command behavior, real-filesystem
fixtures) and `tests/FS.GG.SDD.Artifacts.Tests` (parser behavior)

**Target Platform**: Linux/macOS/Windows CLI (`fsgg-sdd`)

**Project Type**: CLI tool over a pure MVU core with an effect interpreter at the edge

**Performance Goals**: N/A — per-invocation authoring, no hot path

**Constraints**: Deterministic byte-identical output on unchanged inputs; blocked runs must not
change outcome or exit code; the seed-on-blocked carve-out must not let any other write escape the
H-4 gate (in particular no `work-model.json` on a blocked `clarify`)

**Scale/Scope**: Three source files, ~120 lines net; two new artifact contracts documented

**Local build note**: this sandbox cannot restore `FSharp.Core` against the committed
`packages.lock.json` (research E1 — an environment artifact; CI's `--locked-mode` gate is green on
`main`). Build and test locally with
`-p:RestorePackagesWithLockFile=false -p:NuGetLockFilePath=<scratch>/nolock.json`. **Never** commit
a regenerated lock file.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|---|---|
| **I. Spec → FSI → Semantic tests → Implementation** | Followed. Spec written first; the touched modules are `internal` with no `.fsi`, so step 2 is vacuous; semantic tests through the CLI/command surface precede the `.fs` change (Phase 2 orders tests before implementation). |
| **II. Structured artifacts are the machine contract** | Honored. The skeleton is Markdown *authoring surface*; the machine contract is the parsed `ClarificationFacts` (blocking count, question ids) and the `CommandReport`. Where prose and structure could disagree — a skeleton claiming `clarified` while the command blocks — the plan makes the structure win (FR-007/FR-008). Contracts documented in `contracts/`. |
| **III. Visibility lives in `.fsi`** | No public surface change. `EarlyStageAuthoring`, `HandlersEarly`, and `Prerequisites` are `internal` modules with no signature files; `Specification.fsi`/`Clarification.fsi` are deliberately **not** touched (research D5 chose a generic remaining-line explanation precisely to avoid widening `SpecificationFacts`). No `docs/api-surface/` baseline moves. |
| **IV. Idiomatic simplicity** | Plain F#: two rendering functions, one set-membership filter, one list-of-lines transform. No new abstraction, no reflection, no CE. The one structural change — a fifth continuation channel on `runHandler` — is justified below and is simpler than the alternatives (research D3). |
| **V. Elmish/MVU is the boundary** | Preserved and load-bearing. All new logic is in the pure `update`-side plan computation; the only new I/O is one additional `CommandEffect` (`WriteFile`) returned from a handler and interpreted at the existing edge. No I/O moves into pure code. |
| **VI. Test evidence is mandatory** | Every FR gets a test that fails before and passes after; real filesystem fixtures, no mocks. The pre-change failure modes are captured verbatim in `research.md` §D-Baseline and re-asserted as regression tests (notably: `clarify` reporting `succeeded` while `checklist` blocks). |
| **VII. Agent and human workflows share one contract** | Unchanged. The skeleton is the same artifact an agent or human authors; the seeded story is the same `spec.md` both read. No second source of truth introduced. `.fsgg/early-stage-guidance.md` is a read-only mirror of the live contract, pinned by a drift-guard test — checked in Phase 2 for whether the seed/skeleton wording it documents must move with this change. |
| **VIII. Observability and safe failure** | Strengthened. No new diagnostic code (FR-016); the blocking diagnostics, outcome, and exit code are unchanged (FR-010). The feature *removes* a silent failure: today `clarify` reports `succeeded` while leaving blocking ambiguities for `checklist` to trip over. |

**Gate result: PASS.** One deviation requires justification and is recorded in Complexity Tracking:
the H-4 effect-gate carve-out weakens a repo-wide invariant ("blocked ⇒ zero writes").

### Re-check after Phase 1 design

Still **PASS**. The Phase 1 design keeps the carve-out to a single named channel that exactly one
handler populates, and keeps `generatedEffects` inside the gate so a blocked `clarify` still writes
no `work-model.json`. No `.fsi`, schema, diagnostic-code, or registry change appeared during design.
Tier 2 classification holds.

## Project Structure

### Documentation (this feature)

```text
specs/089-early-stage-authoring-seeds/
├── plan.md                              # This file
├── spec.md                              # Feature specification
├── research.md                          # Phase 0 — D1..D8, verified baseline transcript, E1
├── data-model.md                        # Phase 1 — the two artifacts + the effect channel
├── quickstart.md                        # Phase 1 — runnable before/after validation
├── contracts/
│   ├── specification-seed.md            # Seeded US-001/AC-001 line grammar
│   └── clarification-skeleton.md        # Blocked-clarify skeleton shape + retirement rules
├── checklists/
│   └── requirements.md                  # Spec quality checklist
└── tasks.md                             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Commands/CommandWorkflow/
├── Prerequisites.fs          # runHandler — the H-4 effect gate; add the blockedSeed channel
├── EarlyStageAuthoring.fs    # specificationTemplate (seed), clarificationTemplate (truthful
│                             #   status + remaining lines), appendClarificationAnswers
│                             #   (retire resolved lines + placeholders),
│                             #   clarificationDiagnosticsTextAndSummary (emit seedText)
└── HandlersEarly.fs          # computeClarifyPlan — thread seedText into the seed effect channel

tests/FS.GG.SDD.Commands.Tests/
├── EarlyStageSeedTests.fs    # NEW — §WD7 seed shape, §WD5 skeleton, retirement, idempotence
└── (existing clarify/specify command tests — golden text updates)

tests/FS.GG.SDD.Artifacts.Tests/
└── ClarificationArtifactTests.fs   # skeleton parses; blocking count == unanswered count
```

**Structure Decision**: No new project or module. The change lives entirely in the existing
`CommandWorkflow` internal modules of `FS.GG.SDD.Commands`, which already own early-stage authoring.
A new test file isolates this feature's semantic tests; existing command tests are updated where
they assert the old meta-seed text or the old `changedArtifacts: 0` blocked outcome (those updates
*are* the evidence the behavior changed — spec Assumptions).

## Phase 1 Design Notes

### The carve-out (research D3)

`runHandler`'s continuation gains a fifth element, `blockedSeedEffects`, and the gate becomes:

```fsharp
let effects =
    if hasBlocking then blockedSeedEffects   // H-4 carve-out (feature 089)
    else writeEffects @ generatedEffects
```

`runHandler` is preserved as a thin wrapper over a generalized `runHandlerWithBlockedSeed` that
supplies `[]`, so the other eight handlers are untouched and the carve-out is one reviewable line.
`generatedEffects` deliberately stay *inside* the gate: a blocked `clarify` writes the skeleton and
nothing else.

### Threading the seed text (research D8)

`clarificationDiagnosticsTextAndSummary` returns a fourth result, `seedText: string option`,
populated only on the file-absent path and only when the rendered skeleton parses. The existing
`text` result — which feeds `generatedViewPlan` — is left exactly as it is today, so a blocked run's
reported `GeneratedViewState` does not change. `seedText` is `None` whenever a `clarifications.md`
already exists, which is what makes FR-011/FR-014 true by construction (research D7).

### Truthful template (research D4/D5)

`clarificationTemplate` derives `status` and the Remaining Ambiguity body from the set of declared
ambiguities that carry **no** concrete decision or accepted deferral — not from the presence of a
`stillOpen` answer. Where a `stillOpen` answer exists its text is still used, so the existing
rendering is a strict superset and no `stillOpen` golden moves.

### Retirement rules (research D4/D6)

`appendClarificationAnswers` gains two post-append passes:
- **FR-018** — drop each Remaining Ambiguity line whose `AMB-###` id now carries a decision or
  accepted deferral; if the section is left empty, insert the `No blocking ambiguity remains.`
  sentinel. Lines for still-unresolved ambiguities, including operator prose, are untouched.
- **FR-019** — drop a section's empty-state placeholder once that section holds a real entry.
  Applies to the Questions/Answers/Decisions/Accepted-Deferrals placeholders only; the
  `No blocking ambiguity remains.` sentinel is meaningful and is never treated as a placeholder.

## Complexity Tracking

> Filled because the Constitution Check records one deviation.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Carve-out in the "blocked ⇒ zero writes" invariant (`runHandler`'s H-4 effect gate) | FR-006 requires a blocked `clarify` to write its skeleton. The invariant is otherwise absolute, so there is no way to satisfy FR-006 without a deliberate exception. Scoped to one named channel that exactly one handler populates; `generatedEffects` stay gated, so no generated view escapes. | *Downgrade `missingClarificationAnswer` to a warning* — would unblock the stage and let `checklist` run against unanswered ambiguities (violates FR-010, breaks the lifecycle). *Bypass `runHandler` in `clarify`* — duplicates the missing-WorkId guard, the diagnostics sort, and the single `hasBlocking` computation the shell exists to single-source. *A new `ArtifactWriteKind` that survives blocking* — `writeKindValue` is serialized into the `changedArtifacts` JSON automation contract, so a new case is an observable vocabulary change (FR-016), and it would let any handler opt in by accident. |
| FR-018/FR-019 exceed the literal issue text | Without FR-018 the feature ships a trap: the skeleton's blocking lines outlive the decisions that answer them, `clarify` reports `succeeded` with `blockingAmbiguities: 2`, and `checklist` blocks two stages later (verified, research D4). Without FR-019 every skeleton this feature ships accumulates an empty-state placeholder beside real content on the operator's first `clarify --input` (verified, research D6). | *Ship FR-006 alone and file the rest* — would replace "the operator hand-authors the file" with "the operator must delete lines from a section after a command told them it succeeded," which is strictly worse and makes the originating issue's goal (less hand-authoring) false. |
