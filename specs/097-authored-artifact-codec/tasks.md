# Tasks: Authored-Artifact Codec and Round-Trip Property

**Input**: `specs/097-authored-artifact-codec/` (spec.md, plan.md, research.md, data-model.md, contracts/artifact-codec.md, quickstart.md)

**Tier**: 1 (the #180 gate now fires — behavior change + migration note; new test-only FsCheck dependency).

**Tracks**: FS.GG.SDD#201 (Gap A) · ADR-0002 invariant 1 · Closes #180, #181, #182.

**Sequencing**: The **Phase 0 authoring slice is DISJOINT** (`specs/096/**`, `docs/decisions/0002`) and lands independently. **Phases 1+ are `Blocked by` FS.GG.SDD#189** — `fsgg-coord overlap FS.GG.SDD#201 FS.GG.SDD#189` = OVERLAP on `HandlersEvidence.fs`, `HandlersVerify.fs`, `TaskGraphAuthoring.fs`, `tests/FS.GG.SDD.Artifacts.Tests`. Re-run `overlap` before starting Phase 1 (ADR-0021).

## Format

`[ID] [P?] [Story] Description` — `[P]` = no dependency on another incomplete task in this phase. `[X]` done, `[ ]` pending, `[-]` skipped with rationale.

---

## Phase 0: Authoring (this slice — disjoint, lands independently)

- [X] T001 Write `spec.md` — FR-001..009, SC-001..004, four P1 user stories, edge cases.
- [X] T002 Write `research.md` — R1..R7, each verified against `main` (`f09c239`), file:line grounded.
- [X] T003 Write `data-model.md` — `FieldCodec<'M>`, the authored/tool-owned partition tables for `evidence.yml` and `tasks.yml`, invariants 1..6.
- [X] T004 Write `contracts/artifact-codec.md` — the `ArtifactCodec` `.fsi` sketch and contract obligations C-1..C-5.
- [X] T005 Write `plan.md` — constitution check (8 principles PASS), structure decision, sequencing.
- [X] T006 Write `quickstart.md` — six validation scenarios mapped to user stories/issues.
- [X] T007 Write this task list. Author `docs/decisions/0002-*` (ADR umbrella). File grouped issues #201..#204.

---

## Phase 1: Setup (after #189 merges — rebase and re-baseline)

- [ ] T008 Rebase `item/201-*` onto `main` after FS.GG.SDD#189 lands. Re-run `scripts/fsgg-coord overlap FS.GG.SDD#201 FS.GG.SDD#189` — it must be moot (item Done). Confirm the shared files (`HandlersEvidence.fs`, `HandlersVerify.fs`, `TaskGraphAuthoring.fs`) landed as expected, and that #189's consumer-side `SourceIds ∪ Requirements ∪ Decisions` union is in place so this feature does **not** re-touch that logic (data-model.md note).
- [ ] T009 Green baseline: `dotnet build FS.GG.SDD.sln -c Debug`, then `dotnet test tests/FS.GG.SDD.Artifacts.Tests tests/FS.GG.SDD.Commands.Tests`. Record the pre-change pass count. (Memory: on NU1403 for FSharp.Core, force-evaluate restore then revert the lock churn; measure the baseline, don't inherit it.)
- [ ] T010 [P] Add FsCheck to the test dependency set — `Directory.Packages.props` (pin the version) + `PackageReference` in `tests/FS.GG.SDD.Artifacts.Tests/*.fsproj`. Confirm `Directory.Packages.local.props` lock churn is handled per the NU1403 memory. Test-only; no `src/` reference (FR: research R5).
- [ ] T011 Pin the pre-change contract: capture `evidence --json` and `tasks --json` for a coherent fixture (byte + exit-code baseline for FR-009), and capture a fixture that reproduces #181 (fully-populated `sourceRef`) and #180 (bare-null disclosure) as **red** characterization inputs for Phase 2.

---

## Phase 2: Foundational — the codec (Principle I: `.fsi` before `.fs`; BLOCKS all stories)

> **Built ahead of #189 (option B, 2026-07-08).** This phase is new files + additive `.fsproj`/baseline edits — genuinely disjoint from #189 — so it landed early to de-risk the abstraction. Phases 3+ (the parser/emitter refactor) still wait on #189. FsCheck (T010) is **not** needed here: the Phase-2 tests are plain xUnit facts; FsCheck arrives with the artifact round-trip properties in Phase 6.

- [X] T012 Authored `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/ArtifactCodec.fsi` — opaque `FieldCodec<'M>` (keeps YamlDotNet out of the public surface), `optionalScalar`/`requiredScalar`/`inlineList`/`scalarBlock`, `keys`, `render`, `decode`. `.fsi` before `.fs`.
- [X] T013 [P] `tests/FS.GG.SDD.Artifacts.Tests/ArtifactCodecTests.fs` — 12 facts over a toy model: full round-trip, omission (C-2), null-as-absence incl. quoted-`"null"` distinction (C-3, the #180 case), determinism/order (C-4), missing-required Error, `keys` order, and the FR-007 record↔codec coupling mechanism (reflection, test-only). All green.
- [X] T014 Implemented `ArtifactCodec.fs` — `render` maps writers, `decode` folds readers over one shared `fields` list; `optionalScalar` reads via `tryScalarNonNullAt` (reuses `Internal.isPlainNullScalar`), writes omit-when-`None`; minimal round-trip-safe scalar quoter (a `"null"` string is quoted so it can't be misread as absence). No reflection/SRTP in `src/` (C-5).
- [X] T015 [P] Registered `ArtifactCodec.fsi`/`.fs` after `Internal.fs` in `FS.GG.SDD.Artifacts.fsproj` and `ArtifactCodecTests.fs` in the test `.fsproj`; regenerated `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` (+7 functions; the opaque `FieldCodec` record is correctly absent). Full solution builds 0/0; all 267 Artifacts tests green.

---

## Phase 3: User Story 1 — a re-render preserves everything authored (P1) 🎯 MVP  (#181 + unfiled)

**Goal**: authored `sourceRef` provenance, `lifecycleNotes`, `tasks.yml` `title`/`publicOrToolFacingImpact` survive a re-run.

### Tests first

- [ ] T016 [P] [US1] `tests/FS.GG.SDD.Commands.Tests/EvidenceCommandTests.fs`: a fully-populated `sourceRef` (`id`/`kind`/`path`/`uri`/`digest`/`relatedSourceId`/`result`) + authored `lifecycleNotes` survives a re-render byte-for-byte. MUST fail today (proves #181/clobber).
- [ ] T017 [P] [US1] `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`: a custom front-matter `title` and `publicOrToolFacingImpact: false` survive a `tasks` re-run. MUST fail today (proves the unfiled revert/flip).

### Implementation

- [ ] T018 [US1] Define the **evidence `fields` list** in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Evidence.fs` per data-model.md Partition A (authored fields incl. all 7 `sourceRef` fields + `lifecycleNotes`). Refactor `parseEvidenceArtifact` to `ArtifactCodec.parse fields`.
- [ ] T019 [US1] Refactor `renderEvidence*` in `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersEvidence.fs` to `ArtifactCodec.render fields` over the **same** list. Remove the hardcoded `lifecycleNotes` canned line (`:806`) and the 4-field `renderEvidenceSourceRefs` (`:689-714`); tool-owned snapshots stay computed (Partition A tool-owned).
- [ ] T020 [US1] Define the **tasks `fields` list** in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Task.fs` per Partition B; refactor `parseTask*` onto `ArtifactCodec.parse`.
- [ ] T021 [US1] Refactor the `tasks.yml` emitter in `src/FS.GG.SDD.Commands/CommandWorkflow/TaskGraphAuthoring.fs` onto `ArtifactCodec.render`. Read the parsed `title` (drop the `requestTitle` override at `:519,524`) and the parsed `publicOrToolFacingImpact` (drop the hardcoded `true` at `:531`). T016/T017 go green.

**Checkpoint**: US1 is the MVP — the data-loss defect is closed. Independently testable.

---

## Phase 4: User Story 2 — absence stays absence (P1)  (#182)

- [ ] T022 [P] [US2] `EvidenceArtifactTests.fs`: a source snapshot with `Digest = None`/`SchemaVersion = None` renders no `digest:`/`schemaVersion:` line (no `""`, no invented `1`, no trailing-whitespace line). MUST fail today.
- [ ] T023 [US2] Ensure the snapshot renderer uses `optionalScalar` (omit-when-`None`), removing the `Option.defaultValue ""`/`"1"` at `HandlersEvidence.fs:680-687`. T022 green. (Snapshots remain tool-computed; this fixes the *render* of a parsed snapshot per Partition A.)
- [ ] T023b [P] [US2] Point the snapshot `digest` **read** (`Evidence.fs:137`, which gates `evidenceSourceSnapshotStale`) at the codec's null-aware reader, and add a guard test that the count of null-unaware reads at gate-bearing sites is zero (SC-003) — the audit named `syntheticDisclosure` (T027) *and* this snapshot digest read as the two gate-bearing null-unaware sites.

---

## Phase 5: User Story 3 — a bare-null scalar is absence, not "null" (P1)  (#180)

- [ ] T024 [P] [US3] `EvidenceArtifactTests.fs`: `parseSyntheticDisclosure` on bare-null `standsInFor`/`reason` → `None` (reader test, all of `null`/`Null`/`NULL`/`~`/empty). MUST fail today.
- [ ] T025 [P] [US3] `EvidenceCommandTests.fs`: a `synthetic: true, result: pass` declaration with bare-null disclosure blocks with `evidence.undisclosedSyntheticEvidence` (exit 1). MUST fail today (gate bypass).
- [ ] T026 [P] [US3] `tests/FS.GG.SDD.Commands.Tests/VerifyCommandTests.fs`: the same declaration blocks at `verify` too (parity, `HandlersVerify.fs:184`). MUST fail today.
- [ ] T027 [US3] Point `parseSyntheticDisclosure` (`Evidence.fs:169-181`) at the null-aware read via the codec's `optionalScalar`. T024/T025/T026 green. Confirm a quoted `"null"` still round-trips as the string.
- [ ] T028 [US3] Consider a `RequiredKeys` registry row + `RequiredFieldContractTests` entry for `standsInFor`/`reason`, mirroring `requiredDeferralKeys`, so the evidence skill documents them. Decide in-task; skip with rationale if the codec's presence check suffices.

---

## Phase 6: User Story 4 — any field combination survives (P1)  (FR-005/FR-007)

- [ ] T029 [P] [US4] `EvidenceArtifactTests.fs`: FsCheck property `parse(render(m)) = m` over a generator of well-formed evidence models ranging every optional present/absent (authored partition only; tool-owned fields fixed). Disclose the generator in the test name.
- [ ] T030 [P] [US4] `TaskArtifactTests.fs`: the same round-trip property for `tasks.yml`.
- [ ] T031 [US4] `ArtifactCodecTests.fs`: the **coupling** test (FR-007) — the codec `Key` set equals the authored-record label set per artifact (reflection in test code only). Adding an authored field with no `FieldCodec` entry fails it (SC-004).
- [ ] T032 [US4] Negative control (SC-002): a disabled test (or documented spike) proving that deleting one `FieldCodec.Write` reddens the property — evidence the property actually bites.

---

## Phase 7: Docs, migration, and polish

- [ ] T033 Migration note in `docs/release/migrations/` (per the versioning policy): a workspace carrying `syntheticDisclosure: { standsInFor: null, reason: null }` now blocks at `evidence`/`verify` (the correct outcome). No `schemaVersion` bump; the YAML shape is unchanged. Reference #180.
- [ ] T034 [P] Update `docs/reference/authoring-contracts.md` / the `fs-gg-sdd-evidence` skill if `standsInFor`/`reason` documentation changes (from T028).
- [ ] T035 Byte-idempotence regression (FR-008): re-render an unchanged coherent fixture twice; assert byte-identical, and diff against the T011 pre-change contract to confirm no unintended `--json`/exit change (FR-009).
- [ ] T036 Full-suite green: `dotnet test FS.GG.SDD.sln -c Debug`. Compare pass count to T009. Close #180/#181/#182 on merge; update #201 with the residual (the Gap-A findings this feature does not cover, if any).

---

## Summary

- **Tasks per story**: US1 = 6 (T016–T021), US2 = 2, US3 = 5, US4 = 4; foundational codec = 4 (T012–T015); setup = 4; authoring = 7 (done); docs/polish = 4.
- **Parallel opportunities**: within each phase the `[P]` tests are file-disjoint and run together; T010 (FsCheck dep) parallels T008/T009.
- **MVP**: Phase 0 → 1 → 2 → **Phase 3 (US1)** — the field-loss defect (#181) closed via the codec. US2/US3/US4 layer on the same foundation.
- **Blocked by**: FS.GG.SDD#189 for everything from Phase 1. Phase 0 is done and independent.
