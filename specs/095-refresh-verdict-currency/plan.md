# Implementation Plan: `refresh` Reports True Facts About the Committed Ship Verdict

**Branch**: `item/188-sdd-refresh-ship-verdict-currency-report` | **Date**: 2026-07-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/095-refresh-verdict-currency/spec.md`

## Summary

Three currency words that `fsgg-sdd refresh` reports about `ship.json` and the **committed**
`ship-verdict.json` are not true of the artifacts they label. The root cause is one line:
`downstreamClass` (`HandlersRefresh.fs:444`) validates `ship.json` with `parsesAsJson` where the
artifact's contract is a *schema*. Everything else follows.

**Approach**: pass a per-artifact validator into the (file-local) `downstreamClass`, validating
`ship.json` with the existing `ShipModule.parseShipView` oracle and leaving `analysis.json` /
`verify.json` on `parsesAsJson`. Once `shClass` is honest, the verdict's existing
`| _, Some _ -> Blocked` arm already produces the right word, and the mis-attributing `Malformed`
stamp at `:527` becomes unreachable. Separately, split `verdictDiags`'s `Missing` arm on the source's
class so a *stale* source emits a warning whether or not the verdict is present. Finally, comment the
dead totality branch at `:528`.

**The key property, proven in [research.md](./research.md) R3**: `verdictClass` is already a member of
`structuredClasses`, so every state this feature re-words is *already* non-clean and already emits the
`refresh.unrenderableSummary` error. **Exit codes do not move.** This is a re-attribution of words, not
a change of behavior — and FR-007/SC-004 enforce that with a table test rather than trusting the
argument.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (constitution, Engineering Constraints)

**Primary Dependencies**: None added. Consumes `ShipModule.parseShipView`, already exported at
`src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Ship.fsi:55`.

**Storage**: Filesystem lifecycle artifacts. No persisted schema version changes (FR-014).

**Testing**: xUnit. `tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs`, using the existing
`TestSupport.shippedProject` / `runRefresh` / `refreshViewState` real-filesystem harness — no mocks
(constitution VI).

**Target Platform**: Linux/macOS/Windows CLI (`fsgg-sdd`)

**Project Type**: CLI tool + libraries (single project tree)

**Performance Goals**: N/A. The added validation is one `parseShipView` call per `refresh` over a
`FileSnapshot` already resident in memory; no I/O is added.

**Constraints**: Deterministic byte-identical output across runs (SC-007). Exit-code invariance across
the full state matrix (FR-007, SC-004).

**Scale/Scope**: Two files. ~25 lines of source change in one handler; ~4 test edits and 2 new tests.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Justification |
|---|---|---|
| **I. Spec → FSI → Semantic Tests → Implementation** | **PASS** | Spec authored and committed first (`de2d1e7`). No `.fsi` step applies: `HandlersRefresh` has no signature file and is internal to `FS.GG.SDD.Commands` (research R2). The public surface consumed (`parseShipView`) is already declared in `Ship.fsi`. Semantic tests precede the `.fs` edit (Phase 2 ordering below). |
| **II. Structured Artifacts Are the Machine Contract** | **PASS** | This feature exists *because* the structured contract (`generatedViews[].currency`) disagreed with the prose diagnostic. It rules for the structured artifact: every currency word must be true of its artifact. The prose was already right; the machine fact is corrected to match. |
| **III. Visibility Lives in `.fsi`** | **PASS (not triggered)** | No public surface changes. `downstreamClass` is a `let` inside the handler body (`:438`), not a module export. `PublicSurface.baseline` unchanged. |
| **IV. Idiomatic Simplicity** | **PASS** | The change is a function parameter (`isValid: FileSnapshot -> bool`) on an existing local closure. No custom operators, no SRTP, no reflection, no active patterns. Rejected alternative — branching on `path = shipPath workId` inside `downstreamClass` — was *less* simple, coupling a generic helper to one artifact's identity (research R2). |
| **V. Elmish/MVU Is the Boundary** | **PASS** | Untouched. The edit is inside the pure classification logic of the refresh handler; it plans no new effect and interprets no I/O. FR-006 asserts the effect set is unchanged (no `WriteFile` for the verdict). |
| **VI. Test Evidence Is Mandatory** | **PASS** | Behavior-changing (report-contract) code. Two new tests fail before and pass after; four existing tests are pinned as regression evidence (research R5). Real-filesystem fixtures via `TestSupport`, no mocks, no synthetic data. |
| **VII. Agent And Human Workflows Share One Contract** | **PASS (not triggered)** | No agent skill or command surface changes. `--json`/`--text`/`--rich` continue to project the same facts (FR-015). |
| **VIII. Observability And Safe Failure** | **PASS — this is the principle the feature serves** | "Failures must distinguish malformed user input from tool defects." Today a malformed *input* (`ship.json`) is reported as a corrupt *committed output* (`ship-verdict.json`). FR-003/FR-004/FR-005 restore the distinction and point the operator at the file needing repair. |
| **Change Classification** | **Tier 1** | Command output-contract change: two `generatedViews[].currency` values, one summary bucket assignment, one diagnostic severity. Requires spec, plan, tasks, tests, docs. No `.fsi` (none exists), no schema version bump, no migration notes (no persisted artifact changes; FR-014). |

**Engineering constraints**: no rendering/provider literals introduced; no Governance dependency; no
new package. `refresh` remains useful without Governance installed.

**Gate result: PASS — no violations. Complexity Tracking section omitted (nothing to justify).**

### Post-Phase-1 re-check

Re-evaluated after `data-model.md` and `contracts/` were written. **Still PASS.** Phase 1 surfaced one
addition — the `AlreadyCurrentViewIds`/`BlockedViewIds` bucket move (research R3a, FR-003a) — which
*strengthens* Principle II (the second projection of the same fact is corrected too) and introduces no
new complexity: it is a consequence of `classifyToBucket`'s existing `| _ ->` arm, requiring zero code
beyond the `shClass` correction. No new gate is implicated.

### Post-implementation re-check

**Still PASS**, with two FRs added *during* implementation because the evidence demanded them:

- **FR-016** (deprecated schema stays `current`) — adopting `parseShipView` adopts `parseJsonView`'s
  compatibility policy verbatim. Discovered by reading `classifyRaw`; pinned by a test so a later
  tightening cannot silently reclassify a working workspace.
- **FR-017** (`governance-handoff` never inherits `malformed`) — discovered by **Principle VI in
  action**. Thirty-six in-process tests were green while the real CLI reported
  `governance-handoff: malformed` about a well-formed file: the fix had reintroduced its own defect one
  artifact over, via `inheritShip`'s verbatim propagation of `shClass`. Real process evidence beat
  transitive coverage, exactly as the constitution says it should.

Both *strengthen* Principle VIII (malformed input is distinguished from a sound artifact) and neither
adds complexity: FR-017 is a three-line `Malformed → Blocked` map on an existing closure. Tier stays 1;
still no `.fsi`, no schema version, no new diagnostic id.

**Scope note.** FR-017 also corrects matrix cells 3/4, where `governance-handoff` was reported
`malformed` *before* this feature. That is a pre-existing falsehood of the same class, in the same
report, one line from the code being changed. Fixing the verdict's false `malformed` while leaving the
handoff's in the same output would be incoherent.

## Project Structure

### Documentation (this feature)

```text
specs/095-refresh-verdict-currency/
├── spec.md                       # authored, committed de2d1e7
├── plan.md                       # this file
├── research.md                   # Phase 0 — R1..R7
├── data-model.md                 # Phase 1 — the currency state machine
├── quickstart.md                 # Phase 1 — manual validation of the 10-cell matrix
├── contracts/
│   └── refresh-currency-matrix.md   # Phase 1 — the output contract, before/after
├── checklists/
│   └── requirements.md           # spec quality gate (16/16)
└── tasks.md                      # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Commands/
├── CommandWorkflow/
│   └── HandlersRefresh.fs        # THE ONLY SOURCE FILE TOUCHED
│                                 #   :350  parsesAsJson        — kept, rewrapped for FileSnapshot
│                                 #   :438  downstreamClass     — gains an `isValid` parameter
│                                 #   :451-453 call sites       — ship gets parsesAsShipView
│                                 #   :495  inheritShip         — maps Malformed → Blocked (FR-017)
│                                 #   :514-539 verdictClass     — :527 becomes unreachable
│                                 #   :528  dead branch         — gains the invariant comment (FR-013)
│                                 #   :607-618 verdictDiags     — Missing arm splits on shClass
└── CommandReports/
    └── DiagnosticConstructors.fs # READ-ONLY — reuses refreshStaleView (:931) as-is

src/FS.GG.SDD.Artifacts/
└── LifecycleArtifacts/
    └── Ship.fs / Ship.fsi        # READ-ONLY — parseShipView consumed, not modified (research R7)

tests/FS.GG.SDD.Commands.Tests/
└── RefreshCommandTests.fs        # 1 characterization test corrected (:88-113)
                                  # 2 new tests (B's untested state; the exit-code matrix)
                                  # 4 tests pinned unchanged as regression evidence
```

**Structure Decision**: Single project tree, unchanged. The realized touch-set is
`HandlersRefresh.fs` + `RefreshCommandTests.fs` — **strictly narrower** than the `Paths:` line declared
on FS.GG.SDD#188 (which additionally names `Ship.fs`, excluded by research R7). A narrower touch-set
cannot introduce an ADR-0021 overlap that the declared one did not already have, so the DISJOINT
verdicts against the in-flight #164 and #171 stand without re-checking.

## Implementation Phases

Ordered to satisfy Constitution I (semantic tests before the `.fs` body hardens) and to keep every
intermediate commit green.

**Phase 2 — Tests first (red).**
1. Correct the characterization test `RefreshCommandTests.fs:88-113` to assert the *true* facts
   (`ship: malformed`, `ship-verdict: blocked`, bucket move). It now **fails**.
2. Add the missing test for B's state — stale source, verdict absent — asserting
   `refresh.staleView` (warning), currency `missing`. It **fails**.
3. Add the exit-code invariance table test over the 10-cell matrix (SC-004), capturing today's exit
   codes as the expected values. It **passes** now and must keep passing.

**Phase 3 — US1 (P1): the `malformed` re-attribution.**
4. Rewrap `parsesAsJson` to take a `FileSnapshot` (or add a thin `parsesAsJsonSnap`), so both
   validators share one shape.
5. Add the `isValid` parameter to `downstreamClass`; pass `parsesAsJsonSnap` at the analysis and
   verify call sites (FR-002) and `parsesAsShipView` (= `parseShipView >> Result.isOk`) at the ship
   call site (FR-001).
6. Tests 1 and 3 go green. FR-003, FR-003a, FR-004, FR-005, FR-006 satisfied; FR-007 held by test 3.

**Phase 4 — US2 (P2): the severity symmetry.**
7. In `verdictDiags` (`:607-618`), split the `Missing` arm: source `Stale` → `refreshStaleView`
   (FR-009); otherwise → `refreshBlockedUpstreamView` (FR-011). `Blocked` keeps
   `refreshBlockedUpstreamView`.
8. Test 2 goes green. FR-010 (currency stays `missing`) and FR-012 (present-verdict path unchanged)
   held by the pinned tests.

**Phase 5 — US3 (P3): the dead branch.**
9. Comment `:528` with the invariant (`shClass = AlreadyCurrent` ⇒ `snapshot` returned `Some` ⇒
   `textOf` cannot be `None`, established at `:442-444`), noting that after Phase 3 the arm is doubly
   unreachable. Retain for match totality (FR-013).

**Phase 6 — Verification.**
10. Full test sweep; assert the four pinned regression tests (research R5) never changed.
11. `fsgg-sdd refresh` against a real fixture per [quickstart.md](./quickstart.md), all 10 cells.
12. Confirm no golden under `tests/FS.GG.SDD.Commands.Tests/goldens/readiness` moved (research R6) —
    which also keeps this branch disjoint from the in-flight FS.GG.SDD#164.

## Complexity Tracking

> Constitution Check passed with no violations. Nothing to justify.
