---
description: "Task list for: Extract a shared JSON view-parser skeleton (total matches)"
---

# Tasks: Extract a shared JSON view-parser skeleton (total matches)

**Input**: Design documents from `/specs/023-extract-json-view-parser/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/parse-json-view.md, quickstart.md

**Tier**: Tier 2 (internal refactor). No public `.fsi` changes, no behavior changes,
byte-identical view output. The existing 437-test suite is the regression gate.

**Tests**: This refactor introduces no new behavior except the previously-unreachable
`(None, Current/Deprecated)` arm — a throw → defined `Error`. Because that is a
behavior change, Constitution Principle VI makes the SC-005 totality assertion a
**required** new test (fails before — raises; passes after — returns the diagnostic
`Error`). It is the only new test the spec permits; all other verification is over the
existing suite, and no existing test may be weakened, skipped, or rewritten (FR-008/SC-002).

**Elmish/MVU**: N/A — these are simple pure parsers, explicitly exempt from MVU
ceremony (Constitution Principle V; plan Constitution Check).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on another incomplete task)
- **[Story]**: US1 / US2 (or none for setup/foundational/polish)
- All paths are relative to the repository root.

---

## Phase 1: Setup (Baseline Capture)

**Purpose**: Record the pre-refactor baseline that every success criterion is measured against.

- [X] T001 Confirm a clean working tree on branch `023-extract-json-view-parser` and that the solution builds, then capture the FS0025 / FS3261 / test baselines: run `dotnet build -c Release --no-incremental 2>&1 | tee /tmp/build-before.log`, record `grep -c FS0025 /tmp/build-before.log` (expect **4**) and `grep -c FS3261 /tmp/build-before.log` (record the number), and run `dotnet test 2>&1 | tee /tmp/test-before.log` (expect **437 passed**). Per quickstart.md "Prerequisites".

---

## Phase 2: Foundational (Blocking Prerequisite)

**Purpose**: Add the single shared skeleton that BOTH user stories depend on. The
total `version, status` match lives here, so this phase is the source of both the
deduplication (US1) and the crash-proof/zero-FS0025 outcome (US2).

**⚠️ CRITICAL**: No parser can be rewired (US1) and no totality assertion can be added
(US2) until this skeleton exists.

- [X] T002 **Deviation noted:** the skeleton takes `(path: string) (text: string)` instead of `(snapshot: FileSnapshot)`. `FileSnapshot` is defined in `LifecycleArtifacts/Core.fs` (fsproj line 22), which compiles **after** `Internal.fs` (line 20), so a `FileSnapshot` parameter is not in scope at the skeleton's position — the plan/contract assumed `FileSnapshot` lived in `GenerationManifest`, which it does not. The two-string signature is internal-only (no `.fsi`, no public surface) and behavior is byte-identical. Add the internal `parseJsonView` skeleton to `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Internal.fs` (the existing `[<AutoOpen>] module internal Internal`, no `.fsi`). Implement it exactly per `contracts/parse-json-view.md` "Behavior": signature `label -> malformedJsonCorrection -> build:(ArtifactRef -> SchemaVersion -> JsonElement -> Result<'view, Diagnostic list>) -> snapshot:FileSnapshot -> Result<'view, Diagnostic list>`; build the `artifact` via `sourceArtifact snapshot.Path` for `GeneratedView`, parse the `JsonDocument` inside a `try/with`, read `schemaVersion`, call `SchemaVersion.classifyRaw`, and branch with the **total** match. The match MUST cover all 10 `(Version ∈ {Some,None}) × (Status: Current|Deprecated|Unsupported|Malformed|Future)` combinations (FR-003): `Some,Current`/`Some,Deprecated` → `build artifact schema root`; `_,Malformed` **and** `None,Current` **and** `None,Deprecated` → `Error [ Diagnostics.malformedSchemaVersion artifact $"{label} is missing or has malformed schemaVersion." ]` (FR-004, the one new arm folded into the malformed arm); `_,Unsupported` → `Error [ Diagnostics.unsupportedSchemaVersion artifact (rawVersion |> Option.defaultValue "") ]`; `_,Future` → `Error [ Diagnostics.futureSchemaVersion artifact (rawVersion |> Option.defaultValue "") ]`; the `with ex` catch → `Error [ Diagnostics.workModelInconsistent artifact $"{label} JSON is malformed: {ex.Message}" malformedJsonCorrection [ snapshot.Path ] ]`. No `| _ ->` catch-all (research.md Decision 3). No fsproj or `open` edits — `Internal.fs` is already compiled at fsproj line 20 ahead of all four parsers (contracts C-1..C-5).

**Checkpoint**: `parseJsonView` compiles inside `Internal`; its own match is exhaustive
(no FS0025 from the skeleton). Parsers not yet rewired.

---

## Phase 3: User Story 1 — One shared view-parser skeleton (Priority: P1) 🎯 MVP

**Goal**: The four public view parsers route through the single `parseJsonView` skeleton;
each supplies only its artifact-specific `build` callback (identity validation + record
construction). The parse → classify → identity → error-arm structure exists once, not four times.

**Independent Test**: The existing view-parser suites (analysis/verify/ship/agent-guidance)
pass unchanged, and the skeleton appears exactly once in source (`grep` per quickstart Scenario 4).

> Each parser keeps its `build` body **verbatim** from today's success arm — same identity
> match, same record fields, same defaults (`viewVersion` `"1.0"`, status defaults), same
> sort orders, same field parsers (data-model.md "Per-parser build callbacks"). Use the exact
> `(label, malformedJsonCorrection)` strings from contracts/parse-json-view.md so output stays
> byte-identical (SC-004). These four tasks touch four different files → all `[P]`.

### Implementation for User Story 1

- [X] T003 [P] [US1] Rewrite `parseAnalysisView` in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Analysis.fs` to delegate to `parseJsonView "Analysis view" "Regenerate readiness/<id>/analysis.json with valid JSON." (fun artifact schema root -> …)`, moving the existing identity match (`createWorkId workId`, `parseStage stage`) + `AnalysisView` record build into the `build` callback verbatim. Remove the now-duplicated parse/`classifyRaw`/schema-error/`try-with` skeleton from this file. `Analysis.fsi` unchanged.
- [X] T004 [P] [US1] Rewrite `parseVerificationView` in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Verify.fs` to delegate to `parseJsonView "Verification view" "Regenerate readiness/<id>/verify.json with valid JSON." (fun artifact schema root -> …)`, moving the existing identity match + `VerificationView` record build into `build` verbatim. Remove the duplicated skeleton. `Verify.fsi` unchanged.
- [X] T005 [P] [US1] Rewrite `parseShipView` in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Ship.fs` to delegate to `parseJsonView "Ship view" "Regenerate readiness/<id>/ship.json with valid JSON." (fun artifact schema root -> …)`, moving the existing identity match + `ShipView` record build into `build` verbatim. Remove the duplicated skeleton. `Ship.fsi` unchanged.
- [X] T006 [P] [US1] Rewrite `parseGeneratedAgentGuidance` in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Guidance.fs` to delegate to `parseJsonView "Generated agent guidance" "Regenerate the generated agent-commands guidance.json with valid JSON." (fun artifact schema root -> …)`, moving the existing identity validation (`createWorkId workId`, `jsonDigest "behaviorModelDigest"`, non-empty `targetId`) + `GeneratedAgentGuidance` record build into `build` verbatim. Remove the duplicated skeleton. `Guidance.fsi` unchanged.

### Verification for User Story 1

- [X] T007 [US1] After T003–T006: run `dotnet test` and confirm **437 passed, 0 failed** with no test-source changes (FR-006/FR-008/SC-002). The analysis/verify/ship/agent-guidance suites passing unchanged proves byte-identical parse results, ordering, and diagnostics (SC-004).
- [X] T008 [US1] Confirm single-source + no copied bodies (quickstart Scenario 4 / SC-003): `grep -rn "parseJsonView" src/FS.GG.SDD.Artifacts/LifecycleArtifacts/` shows **1 definition (Internal.fs) + 4 call sites**; `grep -rn "SchemaVersion.classifyRaw" src/FS.GG.SDD.Artifacts/LifecycleArtifacts/{Analysis,Verify,Ship,Guidance}.fs` shows **no hits** (classify now lives only in the skeleton); net `src` shrinks by ~70 LOC.
- [X] T009 [US1] Confirm no public surface changed (quickstart Scenario 5 / FR-007/SC-004): `git diff --name-only -- 'src/**/*.fsi'` is **empty**.

**Checkpoint**: All four parsers route through one skeleton; suite green; no `.fsi` diff;
no duplicated skeleton bodies remain. US1 complete and independently verified.

---

## Phase 4: User Story 2 — Crash-proof, warning-clean schema handling (Priority: P1)

**Goal**: Zero FS0025 incomplete-match warnings, no parser can raise `MatchFailureException`
from an unhandled `version, status` combination, and the impossible
`(None, Current/Deprecated)` state degrades to a malformed-schema diagnostic `Error`.

**Independent Test**: `dotnet build` reports 0 FS0025; a constructed `(version = None,
status = current)` input returns a malformed-schema-version `Error` rather than raising.

> The total match itself was delivered in Phase 2 (T002); this phase proves and locks the
> two US2 outcomes — zero FS0025 and the crash-proof impossible-state behavior.

### Test for User Story 2 (the one REQUIRED new test — Constitution Principle VI) ⚠️

> **Write this test FIRST and confirm it FAILS before the skeleton's total arm exists**
> (the pre-refactor path raises `MatchFailureException` for the constructed state),
> then confirm it PASSES after T002. This is the one genuinely new behavior, so
> Principle VI makes the test required, not optional.

- [X] T010 [P] [US2] **Reachability note:** the literal `(Version = None, Status = Current/Deprecated)` state is **unreachable by construction** — `SchemaVersion.classifyRaw` only ever pairs `Current`/`Deprecated` with `Some version`, and `parseJsonView` is `module internal` with no `InternalsVisibleTo`, so the exact pairing cannot be injected from a test. The added assertion (`parseAnalysisView missing schemaVersion returns malformed-schema Error and never raises` in `AnalysisViewTests.fs`) therefore drives the **equivalent observable path**: a missing `schemaVersion` classifies as `None/Malformed` and routes through the *same folded arm* that now also absorbs `(None, Current/Deprecated)`. It asserts the exact malformed-schema-version `Error` message `"Analysis view is missing or has malformed schemaVersion."` and that no exception is raised. (Consequence: the "fails before T002" expectation does not hold — pre-refactor the malformed arm already returned this `Error`; the genuine new guarantee, totality, is proven by T011's FS0025 → 0.) Single new test; no existing test modified. Add a single **required** totality assertion (SC-005 / FR-003 / FR-004; quickstart Scenario 6) in the appropriate existing test project alongside the current view-parser suites.

### Verification for User Story 2

- [X] T011 [US2] Zero FS0025, build green (quickstart Scenario 1 / FR-005/SC-001): `dotnet build -c Release --no-incremental 2>&1 | tee /tmp/build-after.log` succeeds and `grep -c FS0025 /tmp/build-after.log` returns **0** (down from the baseline 4 in T001).
- [X] T012 [US2] FS3261 unchanged (quickstart Scenario 2 / FR-009): `grep -c FS3261 /tmp/build-after.log` equals the T001 baseline count — any movement is relocation only, no new or removed nullness sites.

**Checkpoint**: Build is green with 0 FS0025, FS3261 is unchanged, and the impossible
state returns a diagnostic `Error` instead of throwing. US2 complete.

---

## Phase 5: Polish & Done-When Sign-off

**Purpose**: Final cross-cutting confirmation that the whole quickstart checklist passes.

- [X] T013 Walk the quickstart "Done when" checklist end-to-end and confirm every box: Scenario 1 (0 FS0025), Scenario 2 (FS3261 unchanged), Scenario 3 (437 tests, unchanged), Scenario 4 (`parseJsonView` once + 4 call sites, no copied skeletons), Scenario 5 (no `.fsi` diff), Scenario 6 (impossible state → `Error`, never raises). See `quickstart.md`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1, T001)**: No dependencies — run first to capture the baseline.
- **Foundational (Phase 2, T002)**: Depends on T001. **BLOCKS both user stories** — the skeleton must exist before any parser can call it or any totality test can target it.
- **User Story 1 (Phase 3)**: Depends on T002.
- **User Story 2 (Phase 4)**: The crash-proof/zero-FS0025 behavior is delivered by T002; its verifications (T011/T012) are most meaningful after the parsers are rewired (T003–T006) so FS0025 is cleared at every site. The required totality test (T010) depends only on T002 (it targets the skeleton's total arm) and should be authored test-first — failing before T002, passing after.
- **Polish (Phase 5)**: Depends on US1 and US2 being complete.

### Within User Story 1

- T003, T004, T005, T006 are `[P]` — four different files, each independently rewiring one parser to the shared skeleton.
- T007–T009 (verification) run after T003–T006.

### Parallel Opportunities

- T003 / T004 / T005 / T006 (Analysis / Verify / Ship / Guidance) — different files, fully parallel.
- T010 (totality test) is independent of the parser rewrites and can be written in parallel once T002 lands.

---

## Parallel Example: User Story 1 parser rewrites

```text
# After T002 (skeleton exists), launch all four parser rewrites together:
Task: "Rewrite parseAnalysisView in Analysis.fs to call parseJsonView (T003)"
Task: "Rewrite parseVerificationView in Verify.fs to call parseJsonView (T004)"
Task: "Rewrite parseShipView in Ship.fs to call parseJsonView (T005)"
Task: "Rewrite parseGeneratedAgentGuidance in Guidance.fs to call parseJsonView (T006)"
```

---

## Implementation Strategy

### MVP (User Story 1)

1. T001 baseline → T002 skeleton → T003–T006 rewire the four parsers → T007–T009 verify.
2. **STOP and VALIDATE**: 437 tests green, no `.fsi` diff, skeleton single-sourced.

### Full delivery

1. Complete US1 (the deduplication MVP).
2. Add US2: T010 totality test + T011/T012 warning gates.
3. T013 sign off the full quickstart checklist.

---

## Notes

- `[P]` tasks = different files, no dependency on another incomplete task.
- The skeleton (T002) deliberately serves both stories; the total match is what
  simultaneously removes the duplication (US1) and the four FS0025 sites + latent
  `MatchFailureException` (US2).
- Preserve every per-parser string and `build` body verbatim — the binding gate is
  build green + 437 tests green + 0 FS0025, and SC-004 requires byte-identical output.
- Never mark a failing task `[X]`; never weaken an assertion to green the build.
- Commit after US1 and after US2 as logical groups.
