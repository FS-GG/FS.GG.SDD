---
description: "Task list for 094 — prompt the coherent-set version bump on a classified shipped-surface mutation"
---

# Tasks: Prompt the Coherent-Set Version Bump on a Classified Shipped-Surface Mutation

**Input**: Design documents from `specs/094-surface-version-bump/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tier**: Tier 1 (command output contract + a new diagnostic id + two `--param` keys). Tests are REQUIRED (Constitution VI).

**Organization**: Phases run in sequence; `[P]` tasks within a phase touch different files and may run in parallel. Stories: `[US1]` the prompt, `[US2]` `--update` prompts too, `[US3]` honest degradation, `[US4]` workspace-declared axis.

**`.fsi` note**: Per Principle I/III, `CommandTypes.fsi` and `Diagnostics.fsi` are authored **before** their `.fs`. `HandlersSurface` is an `internal` module with no signature file (matching 086/087), so `readAxisText`/`applyBump`/`versionBumpPrompt` stay `private` and add no public surface. The one new public `val` is `Diagnostics.surfaceVersionBumpRequired`, which *does* move `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` (the reflection baseline captures public static module functions — records and fields are not captured, which is why 087 touched no baseline).

**MVU note**: Per Principle V, the axis read is a `ReadFile` **effect** appended to `Foundation.surfaceReadEffects` (the existing first wave). `computeSummary` stays pure over interpreted snapshots — no `File.ReadAllText` enters the handler. No new `Model`/`Msg`/`Effect` kind is introduced.

> **⚠ Touch-set (ADR-0021): NOT disjoint. Implementation is `Blocked by` FS.GG.SDD#164 and FS.GG.SDD#177.**
>
> - **#164** holds `CommandTypes.fs`/`.fsi`, `CommandSerialization.fs`, `CommandRendering.fs`, `SurfaceCommandTests.fs`.
> - **#177** holds `Foundation.fs` and `docs/release/`.
>
> Phases 0–1 below (this spec directory) are disjoint and land now. **Do not start Phase 2** until both
> are merged; then rebase onto `main` and re-run `scripts/fsgg-coord overlap` before touching a shared
> file. Design decision AMB-005 already removed `ReleaseContract.fs`/`.fsi` from the touch-set, dropping
> one of #177's collisions — do not reintroduce it.

---

## Phase 0: Authoring (this slice — disjoint, lands independently)

- [X] T001 Write `specs/094-surface-version-bump/spec.md` — six clarifications (AMB-001..AMB-006), FR-001..FR-017, four user stories, seven success criteria.
- [X] T002 Write `specs/094-surface-version-bump/research.md` — nine findings (R1..R9), each verified against the tree at `d1c6e20`, not inferred.
- [X] T003 Write `specs/094-surface-version-bump/data-model.md` — `VersionBumpPrompt`, the axis state table, the bump algebra, the three projections, invariants I1..I5.
- [X] T004 Write `specs/094-surface-version-bump/plan.md` — constitution check, the five ruled-out files, design detail, the 24-row verification plan.
- [X] T005 Write `specs/094-surface-version-bump/quickstart.md`.
- [X] T006 Write this task list.

**Checkpoint**: the authored slice is complete and mergeable on its own. Everything below waits on #164 + #177.

---

## Phase 1: Setup (after rebase onto merged #164 + #177)

- [ ] T007 Rebase onto `main`. Re-run `scripts/fsgg-coord overlap FS.GG.SDD#171 FS.GG.SDD#164` and `… #177`; both must now be moot (items Done). Confirm the five shared files landed as expected and that `ReportAssembly.fs:ReportVersion` is still `1.3.0` (if another feature bumped it first, take the next minor).
- [ ] T008 Confirm the build baseline is green before changes: `dotnet build FS.GG.SDD.sln`, then `dotnet test tests/FS.GG.SDD.Artifacts.Tests tests/FS.GG.SDD.Commands.Tests tests/FS.GG.SDD.Cli.Tests`. Record the pre-change pass count for regression comparison. (Memory: if `dotnet build` fails NU1403 on FSharp.Core, force-evaluate restore then revert the lock churn.)
- [ ] T009 Pin the pre-change contract: capture `surface --json` and `surface --text` for a drifted fixture and for a coherent tree. These are the exit-code and byte-output baselines FR-013 / SC-004 / SC-006 are measured against.

---

## Phase 2: Foundational — prove the two load-bearing research claims (BLOCKS all stories)

**Purpose**: The whole design rests on R1 (`--update` classifies *before* it writes) and R2 (a missing file interprets to `Snapshot = None`, not a failure). Prove both against the **unchanged** code. If either fails, the design is invalid: US2 would need a handler restructure, and US3 would need an `Exists` probe.

- [ ] T010 [P] In `tests/FS.GG.SDD.Commands.Tests/SurfaceCommandTests.fs`, add a test that runs `surface --update` against a fixture with one drifted `.fsi` and asserts `summary.Classification.Verdict` is the **pre-write** verdict (`additive`), not `none`. MUST pass against the current, unmodified handler (characterizes R1).
- [ ] T011 [P] In the same file, add a test that plans a `ReadFile` for a **nonexistent** path, interprets the effect set, and asserts `hasInterpreted key model = true` while `snapshot path model = None`. MUST pass unmodified (characterizes R2).

**Checkpoint**: R1 and R2 are tests, not assumptions. Both pass before any production change.

---

## Phase 3: The signature surface (Principle I — `.fsi` before `.fs`)

- [ ] T012 In `src/FS.GG.SDD.Commands/CommandTypes.fsi`, declare `VersionBumpPrompt` (six fields, documented per data-model.md) and append `VersionBump: VersionBumpPrompt` to `SurfaceSummary`. Not an `option` — the automation contract keeps a stable shape.
- [ ] T013 In `src/FS.GG.SDD.Artifacts/Diagnostics.fsi`, declare `val surfaceVersionBumpRequired` with the seven-parameter signature from data-model.md.
- [ ] T014 Mirror both into `CommandTypes.fs` / `Diagnostics.fs` as the minimum needed to compile. The diagnostic is a `DiagnosticWarning`, id `surface.versionBumpRequired` (FR-008).

---

## Phase 4: User Story 1 — the operator is told what the mutation costs (Priority: P1) 🎯 MVP

**Goal**: `surface --check` reports the axis, its value, the required bump, and the implied version, and warns.

**Independent test**: V1 in plan.md § Verification Plan — additive drift + `<Version>0.8.0</Version>` ⇒ `minor` / `0.9.0` / one warning.

### Tests first

- [ ] T015 [P] [US1] `SurfaceCommandTests.fs`: V1 (additive ⇒ `minor`, `0.8.0` → `0.9.0`, one warning), V2 (breaking ⇒ `major`, `1.0.0`), V3 (cosmetic ⇒ `none`, `suggested = current`, **no** warning — I3, I4), V4 (coherent ⇒ `none`, no warning).
- [ ] T016 [P] [US1] `SurfaceCommandTests.fs`: V5 — for each of V1–V4, under both `--check` and `--update`, the exit code is identical to the T009 baseline (FR-013, SC-004).
- [ ] T017 [P] [US1] `tests/FS.GG.SDD.Artifacts.Tests/DiagnosticTests.fs`: V22 — id `surface.versionBumpRequired`, severity `DiagnosticWarning`.

### Implementation

- [ ] T018 [US1] `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs`: add `versionAxisFile` (default `Directory.Build.props`) and `versionAxisProperty` (default `Version`) via the existing `surfaceParam`; add the `escapesRoot` predicate (FR-017); append the guarded `ReadFile` to `surfaceReadEffects` (first wave).
- [ ] T019 [US1] `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersSurface.fs`: add private `readAxisText` (XDocument, `LocalName` match, `.Value.Trim()`, catch `XmlException` ⇒ `None`), private `applyBump`, and `versionBumpPrompt` folding snapshot + classification into a `VersionBumpPrompt`. Populate `SurfaceSummary.VersionBump` in `computeSummary`.
- [ ] T020 [US1] `HandlersSurface.fs`: in `computeSurfaceNext`, append `versionDiagnostics` beside `driftDiagnostics`/`orphanDiagnostics`, emitting the warning iff `RequiredBump ∈ {major, minor}` (FR-008, I4). **Not** gated on the mode — see US2.
- [ ] T021 [US1] `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersSurface.fs`: add the FR-015 comment on `SurfaceClassify.bumpFor` naming `ReleaseContract.bumpRule` and stating why they differ. Add the reciprocal comment on `bumpRule` in `src/FS.GG.SDD.Artifacts/ReleaseContract.fs`. **Comment only — no code change to `ReleaseContract`, and `.fsi` untouched** (AMB-005, R5).
- [ ] T022 [US1] `src/FS.GG.SDD.Commands/CommandReports/ReportAssembly.fs`: bump `ReportVersion` `1.3.0 → 1.4.0` (line 79) and add the changelog comment line. **That is the whole task.** `helpReport` sets `Surface = None` (`:156`) and constructs no `SurfaceSummary`, so it needs no change. No test asserts the `"1.3.0"` literal. The one record literal that *will* fail to compile is `tests/FS.GG.SDD.Cli.Tests/SurfaceProjectionTests.fs:23-42` — fix it in T034.

**Checkpoint**: US1 is demonstrable end-to-end on the convention default. This alone delivers the feature's value.

---

## Phase 5: User Story 2 — `--update` does not silently consume the event (Priority: P1)

**Goal**: the run that erases the drift is the run that reports its cost.

**Independent test**: V6 — `--update` on breaking drift ⇒ baselines rewritten, exit 0, warning + `major`/`1.0.0`.

- [ ] T023 [P] [US2] `SurfaceCommandTests.fs`: V6 (`--update` prompts, exit 0, baselines refreshed), V7 (second `--update` ⇒ verdict `none`, no warning — idempotent).
- [ ] T024 [US2] `SurfaceCommandTests.fs`: V8 — assert **on the planned effect set** that no effect targets the `versionAxisFile` (FR-012, SC-005, I5). Asserting on file mtimes is not sufficient.
- [ ] T025 [US2] Verify T020 emits under both modes (no `not model.Request.SurfaceUpdate` guard on `versionDiagnostics`). If T020 was written with the guard, remove it — this task exists because the guard is the natural thing to copy from `driftDiagnostics`.

**Checkpoint**: the governed event survives the normal PR workflow (SC-002).

---

## Phase 6: User Story 3 — an unresolvable axis degrades honestly (Priority: P2)

**Goal**: every axis failure is named, `requiredBump` still lands, exit code untouched.

**Independent test**: V9/V10/V11 — absent file / no such property / unparseable text.

- [ ] T026 [P] [US3] `SurfaceCommandTests.fs`: V9 (file absent ⇒ `undeterminable`, `null`/`null`, diagnostic names `--param versionAxisFile`), V10 (no `<Version>` ⇒ `undeterminable`, diagnostic names `--param versionAxisProperty`), V11 (`not-a-version` ⇒ `unparseable`, `currentVersion` is `null` — the bad text is **not** echoed).
- [ ] T027 [P] [US3] `SurfaceCommandTests.fs`: V12 (malformed XML ⇒ `undeterminable`, no exception escapes), V13 (`1.2.3-beta` ⇒ `unparseable`), V14 (`\n 0.8.0 <!--x-->` ⇒ `resolved`, `0.8.0` — trim is load-bearing, comments are ignored by `XElement.Value`), V15 (duplicate element ⇒ first in document order).
- [ ] T028 [US3] `SurfaceCommandTests.fs`: V19a — `--param versionAxisFile=../outside.props` ⇒ `undeterminable`, **and no `ReadFile` is planned for it** (FR-017). Assert on the planned effect set.
- [ ] T028b [US3] `SurfaceCommandTests.fs`: V19b — `--param versionAxisFile=/etc/passwd` ⇒ same. **This row is not redundant with V19a.** `normalizeRelativePath` ends in `.TrimStart('/')`, so a guard that normalizes before testing `Path.IsPathRooted` passes V19a and *still opens `/etc/passwd`*. Test the raw param (plan.md §1). Assert on the planned effect set, not on the report alone.
- [ ] T029 [US3] `HandlersSurface.fs` / `Diagnostics.fs`: implement the unresolved branch of the diagnostic message — name the `--param` override that would resolve the axis (FR-010, SC-007).

**Checkpoint**: the feature never dead-ends and never asserts a version it did not read.

---

## Phase 7: User Story 4 — a consumer declares a non-default axis (Priority: P2)

**Goal**: FS.GG.Audio / FS.GG.Game point at their own axis; generic SDD learns neither name.

**Independent test**: V16 — `--param versionAxisProperty=FsGgAudioVersion`, `2.3.1` + breaking ⇒ `3.0.0`.

- [ ] T030 [P] [US4] `SurfaceCommandTests.fs`: V16 (non-default property), V17 (non-default file).
- [ ] T031 [US4] `SurfaceCommandTests.fs`: V18 — the constitutional guard. Assert `grep -rE 'FsGg[A-Za-z]+Version' src/` yields **zero** matches (FR-003, SC-003). A source-tree assertion, not a behavior test; it is what makes this feature safe to generalize. Note the fixture *test* files legitimately contain `FsGgAudioVersion` — scope the assertion to `src/` only.

---

## Phase 8: Projections and determinism

- [ ] T032 [US1] `src/FS.GG.SDD.Commands/CommandSerialization.fs`: append the `versionBump` object to `writeSurface`. Stable key set; `currentVersion`/`suggestedVersion` as explicit `null` when unresolved (R6, FR-007).
- [ ] T033 [US1] `src/FS.GG.SDD.Commands/CommandRendering.fs`: append the five flat `surfaceVersion*` `key: value` lines, `defaultArg … "(none)"`, always emitted (R6). Keep them flat scalars so `--rich` auto-derives.
- [ ] T034 [P] `tests/FS.GG.SDD.Cli.Tests/SurfaceProjectionTests.fs`: V20 (json object shape, five text lines, rich degrades to the same text) and V21 (two runs ⇒ byte-identical `--json` and `--text`) (FR-014, SC-006).
- [ ] T035 Confirm **no** change is needed in `src/FS.GG.SDD.Cli/Rendering.fs` (rich auto-derives — R6). If one turns out to be needed, stop: that contradicts R6 and the touch-set must be re-declared and re-overlapped.

---

## Phase 9: Docs, help, and baselines

- [ ] T036 [P] `src/FS.GG.SDD.Commands/CommandHelp.fs`: document `--param versionAxisFile` and `--param versionAxisProperty` with their defaults (FR-016).
- [ ] T037 [P] `tests/FS.GG.SDD.Commands.Tests/HelpCommandTests.fs`: V23 — assert both keys and defaults appear in `surface --help`.
- [ ] T038 [P] `docs/release/schema-reference.md`: describe the `surface.versionBump` block (this is exactly what 087 did for `classification`). Confirm `docs/release/release-readiness.json` and its test baseline stay **untouched** — the `commandReport` inventory names only top-level report blocks (R9).
- [ ] T038b **Do not touch `CLAUDE.md` or `AGENTS.md` unless you touch both.** `tests/FS.GG.Contracts.Tests/AgentSurfaceDriftTests.fs` asserts they are **byte-identical** (CLAUDE.md is the authored source; AGENTS.md is its mirror). Both document `surface` at lines 156-159. This feature adds no command and no vocabulary, so 087's precedent says leave them alone — but if the doctrine paragraph is edited, mirror it or the suite goes red.
- [ ] T039 Re-capture `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` with `FSGG_UPDATE_BASELINE=1` (one new public `val`). Diff it: exactly one line should be added. Any other delta means something leaked into a public surface.
- [ ] T040 **No remediation pointer is required — and adding one breaks the build.** Verified: `RemediationPointersTests.fs` never enumerates the diagnostic catalog; every invariant iterates `for KeyValue(id, _) in RemediationPointers.registry` (`src/FS.GG.SDD.Commands/CommandReports/RemediationPointers.fs`), a hand-curated authoring-grammar subset that deliberately excludes even the blocking `surface.drift` (see its comment at `:52-55`). A new diagnostic of *any* severity cannot break it. Do **not** add `surface.versionBumpRequired` to the registry: `RemediationPointersTests.fs:122-132` asserts every registry key appears as a quoted literal in `CommandReports/DiagnosticConstructors.fs`, but `surface.*` ids live in `FS.GG.SDD.Artifacts/Diagnostics.fs` — the assertion would fail. This task is a **no-op confirmation**; tick it after re-reading `RemediationPointers.fs:52-55`. (`RemediationSupport.fs`, named in an earlier draft, is the doctor/upgrade fixture builder and is irrelevant here.)

---

## Phase 10: Verification and close-out

- [ ] T041 `dotnet build FS.GG.SDD.sln` clean under the full warning ratchet (`TreatWarningsAsErrors=true`, no carve-out).
- [ ] T042 Full suite green: `dotnet test FS.GG.SDD.sln`. Compare against T008's pass count — the delta must be exactly the tests added here, with **zero** pre-existing `SurfaceCommandTests` modified beyond additive assertions (SC-004).
- [ ] T043 `fsgg-sdd validate --markdown` — regression check only. **`surface` is explicitly excluded from validate's command matrices** (`src/FS.GG.SDD.Validation/ValidationRunner.fs:626-628`: *"a cross-cutting API-surface baseline verb covered by its own semantic suite"*), so this proves nothing about the feature. Its job is to confirm the `ReportVersion` bump did not disturb an unrelated cell. Do not treat a green validate as coverage.
- [ ] T044 Dogfood: `fsgg-sdd surface --check --text --param versionAxisFile=Directory.Build.local.props` **in this repo**. Confirm `surfaceVersionAxisState: resolved` and `surfaceVersionCurrent: 0.8.0`. **Expect exit 1, not 0.** This repo has no `docs/api-surface/` tree (it keeps a reflection baseline instead), so all 53 authored `.fsi` report as `missingBaselinePaths` → `isCoherent: false` → the pre-existing `surface.drift` error fires. That is feature 086 behavior, not a regression. Classification is still `none` (087 classifies only *drifted* files — baseline present **and** differing — never `missing-baseline`), so `requiredBump` is `none` and **no** version warning fires. The exit 1 comes entirely from `surface.drift`. Note also that the *default* axis (`Directory.Build.props`) has no `<Version>` element in this repo — hence `--param versionAxisFile=…local.props`; running without it correctly yields `undeterminable` (US3-2).
- [ ] T045 `/speckit-analyze` — cross-artifact consistency across spec/plan/tasks before merge (Constitution, Development Workflow).
- [ ] T046 Open the PR against `main`, referencing FS.GG.SDD#171 and FS-GG/.github ADR-0025. Then `scripts/fsgg-coord done FS.GG.SDD#171 --flip`.

---

## Dependencies

```
Phase 0 (authored slice)  ──────────────► merges independently, NOW
                                             │
        #164 merged ─┐                       │
        #177 merged ─┴──► Phase 1 (rebase) ──┴──► Phase 2 (prove R1, R2)
                                                     │
                                                     ▼
                                              Phase 3 (.fsi first)
                                                     │
                              ┌──────────────────────┼──────────────────────┐
                              ▼                      ▼                      ▼
                        Phase 4 [US1] 🎯      Phase 6 [US3]          Phase 7 [US4]
                              │                      │                      │
                              ▼                      │                      │
                        Phase 5 [US2]                │                      │
                              └──────────────────────┴──────────────────────┘
                                                     ▼
                                            Phase 8 (projections)
                                                     ▼
                                            Phase 9 (docs, baselines)
                                                     ▼
                                            Phase 10 (verify, ship)
```

US1 is the MVP and gates nothing but US2 (which depends on T020's diagnostic wiring). US3 and US4 are
independent of both once Phase 3 lands — all three consume `VersionBumpPrompt`.

## Parallel opportunities

- **T010 ‖ T011** — different characterization tests, same file, disjoint regions.
- **T015 ‖ T016 ‖ T017** — T017 is a different project (`Artifacts.Tests`).
- **T026 ‖ T027**, **T030** — independent fixtures.
- **T036 ‖ T037 ‖ T038** — help source, help test, docs.
- Phases **4, 6, 7** can run concurrently once Phase 3 lands, but they all edit `SurfaceCommandTests.fs`
  and `HandlersSurface.fs` — one worker, or accept the merge. This is intra-*file* contention, below the
  granularity of the ADR-0021 touch-set check.

## Traceability

| FR | Tasks | Verification |
|---|---|---|
| FR-001 axis params | T018 | V16, V17 |
| FR-002 no MSBuild eval | T019 | V14, V15 |
| FR-003 no provider literal | — (constraint) | **V18** |
| FR-004 bump from verdict | T019 | V1, V2, V3, V4 |
| FR-005 applyBump | T019 | V1, V2, V16 |
| FR-006 three axis states | T019, T029 | V9–V13, V19 |
| FR-007 unresolved ⇒ null | T032, T033 | V9, V11, V20 |
| FR-008 warning iff major\|minor | T020 | V1, V3, V4, V22 |
| FR-009 message content | T029 | V1, V9 |
| FR-010 names the override | T029 | V9, V10 |
| FR-011 both modes | T020, T025 | V6, V7 |
| FR-012 zero axis writes | (structural) | **V8** |
| FR-013 exit code unchanged | — | **V5** |
| FR-014 three projections | T032, T033 | V20, V21 |
| FR-015 bumpFor ≠ bumpRule | T021 | V24 |
| FR-016 help | T036 | V23 |
| FR-017 root containment | T018 | V19 |

FR-003, FR-012, and FR-013 have **no implementation task** — they are constraints the design satisfies
structurally (plan.md §5) and that V18 / V8 / V5 exist to keep true. FR-015's V24 is a source-inspection
assertion paired with T021.
