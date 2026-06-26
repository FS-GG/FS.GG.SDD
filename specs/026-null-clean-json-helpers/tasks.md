---
description: "Task list for 026-null-clean-json-helpers"
---

# Tasks: Null-Clean JSON Access + Warnings-as-Errors Gate

**Input**: Design documents from `/specs/026-null-clean-json-helpers/`

**Prerequisites**: plan.md, spec.md, research.md (D1–D7), data-model.md (null-boundary map + INV-1…INV-5), contracts/warnings-gate.md (C1–C3), quickstart.md

**Change Tier**: Tier 2 (internal change + build-config). No public `.fsi`, schema, generated-view, command, or artifact-layout change. Phase tiers match the spec's overall tier, so no per-task `[T1]/[T2]` annotation is needed.

**Tests**: This is a behavior-preserving refactor — the **existing** 438-test suite plus byte-identical `--json` output are the regression gates (FR-003 / SC-004). No new test-first tasks are required (Principle VI is satisfied by the existing suite staying green). One optional null-coalescing unit test (T020) adds direct SC-005 evidence; the gate-bites check (T024) is a build-level check, not a runtime unit test.

**Organization**: Tasks are grouped by user story. Phase 2 (the JSON-access boundary) is foundational for US1 because the `Internal.fs` fix also clears the downstream propagation in the four parser `build` callbacks (research D3); the parser-family residual tasks depend on it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — different file, no dependency on another incomplete task in this phase.
- **[Story]**: `US1` (null cleanup) or `US2` (enable gate).
- All cleanup follows the C2 null-handling convention; every null→default substitution MUST be behavior-preserving (INV-1: `Option.ofObj x |> Option.defaultValue ""` ≡ `if isNull x then "" else x`). No file/project-scope `#nowarn`; per-site suppression only for an enumerated intractable site (FR-009).

---

## Phase 1: Setup — Baseline capture

**Purpose**: Re-measure ground truth on the current branch and capture the determinism baseline before any edit (research "Baseline measurement"; quickstart Step 0).

- [X] T001 Re-measure the warning baseline on the current `main` merge state: `dotnet build -c Release --no-incremental 2>&1 | grep -oE "warning FS[0-9]+" | sort | uniq -c`. Record the unique FS3261 site count and confirm FS3261 is the **only** category emitted (expected ~283 unique / 952 raw, 0 FS0025, 0 other — confirms SC-006 holds by construction). Also record the **per-assembly** FS3261 distribution, explicitly including `src/FS.GG.SDD.Cli` (Program.fs/Rendering.fs) — expected **0**; if it is non-zero, add a dedicated Cli-src cleanup task before T018 (the "four assemblies" wording in research.md otherwise leaves Cli-src coverage to the T018 catch-all). Save the grouped output under `specs/026-null-clean-json-helpers/` notes for diffing.
- [X] T002 [P] Capture deterministic `--json` baselines for the representative commands on a fixture: run charter, analyze, and refresh through `dotnet run --project src/FS.GG.SDD.Cli -- <cmd> --json <fixture-args>` and save stdout to `/tmp/<cmd>.before.json` (one per command). These are the byte-identical reference for T019 (SC-004 / V-3).

**Checkpoint**: Baseline counts and `--json` references captured — cleanup can begin.

---

## Phase 2: Foundational — JSON-access boundary (Priority: P1, blocks parser-family cleanup) 🎯

**Purpose**: Centralize `JsonElement.GetString()` null-coalescing once at the shared `Internal` helper layer. This is the highest-leverage edit (research D3): it clears the helper sites **and** the "compatible nullability" propagation flowing into the `Analysis`/`Verify`/`Ship`/`Guidance` `build` callbacks. The parser-family residual tasks (T005–T008) depend on this.

**⚠️ CRITICAL**: Complete T003 before T005–T008; those tasks fix only the residue left after centralization.

- [X] T003 [US1] Centralize `string | null` coalescing in the JSON-access helpers of `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Internal.fs` — wrap `JsonElement.GetString()` with `Option.ofObj` in `jsonString`, `jsonRequiredString`, `jsonInt`, `jsonStringList`, `parseJsonDigest`, and `parseJsonView` so each yields a clean `string option`/`string` at the boundary (C2 table; D3). Each coalesced default MUST equal the prior `if isNull v then "" else v` value exactly (INV-1). Stay `internal`; add no `.fsi` and no public module (INV-3).
- [X] T004 [US1] Rebuild Artifacts and confirm the `Internal.fs` sites (8) and the bulk of the downstream parser-family sites have dropped, isolating the residual per-callback matches for T005–T008. (`dotnet build src/FS.GG.SDD.Artifacts -c Release --no-incremental 2>&1 | grep -c "warning FS3261"`.)

**Checkpoint**: JSON boundary is null-clean — parser-family residuals and the remaining clusters can be cleaned in parallel.

---

## Phase 3: User Story 1 — Null-clean the JSON access boundary (Priority: P1)

**Goal**: Drive the FS3261 unique-site count across `src` **and** tests to 0 with behavior-preserving idioms, leaving the warning signal clean and trustworthy (independently shippable even if the gate is never added).

**Independent Test**: Clean Release build emits 0 FS3261 + 0 FS0025; full suite passes; `--json` for charter/analyze/refresh is byte-identical to the T002 baseline.

### Parser-family residuals (Artifacts — depend on T003)

- [X] T005 [P] [US1] Clear residual FS3261 sites in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Analysis.fs` (44 baseline; most cleared by T003) with local null pattern-matches per C2.
- [X] T006 [P] [US1] Clear residual FS3261 sites in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Verify.fs` (43 baseline) per C2.
- [X] T007 [P] [US1] Clear residual FS3261 sites in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Ship.fs` (19 baseline) per C2.
- [X] T008 [P] [US1] Clear residual FS3261 sites in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Guidance.fs` (17 baseline) per C2.

### Remaining Artifacts clusters

- [X] T009 [P] [US1] Clear FS3261 sites in `src/FS.GG.SDD.Artifacts/WorkModel.fs` (53 baseline) using `Option.ofObj` / `String.IsNullOrEmpty`; this file compiles after `Internal.fs` and may reuse the same idioms (D3).
- [X] T010 [P] [US1] Clear FS3261 sites in `src/FS.GG.SDD.Artifacts/ReleaseContract.fs` (25 baseline, includes a few non-`string` `string | null` sites) with inline idioms per C2 / D5.
- [X] T011 [P] [US1] Clear FS3261 sites in `src/FS.GG.SDD.Artifacts/GenerationManifest.fs` (10 baseline) — fix **in place**; this file compiles before `Internal.fs` and cannot reach the shared helper (D4).
- [X] T012 [P] [US1] Clear FS3261 sites in `src/FS.GG.SDD.Artifacts/SchemaVersion.fs` (8 baseline) — fix **in place**, compiles before `Internal.fs` (D4).
- [X] T013 [P] [US1] Clear the Artifacts long-tail FS3261 sites (1–4 each) across `LifecycleArtifacts/Core.fs`, `Evidence.fs`, `Task.fs`, `Plan.fs`, `Specification.fs`, `Clarification.fs`, `Checklist.fs`, `RequirementModel.fs`, plus `ArtifactRef.fs` and `Identifiers.fs`, with inline idioms per C2.

### Commands assembly (~17 baseline)

- [X] T014 [P] [US1] Clear FS3261 sites in the Commands assembly with inline idioms per C2 (cannot see Artifacts internals — no cross-assembly plumbing, D4): `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs`, `ParsingEarly.fs`, `ParsingMid.fs`, `ParsingTasks.fs`, `HandlersShip.fs`, `HandlersEvidence.fs`, `HandlersAgents.fs`.

### Validation assembly (~17 baseline)

- [X] T015 [P] [US1] Clear FS3261 sites in `src/FS.GG.SDD.Validation/ValidationContracts.fs` (14 baseline) with inline idioms; an `[<AutoOpen>] module internal` null helper local to this assembly is permitted for the dense cluster but optional (D4).
- [X] T016 [P] [US1] Clear FS3261 sites in `src/FS.GG.SDD.Validation/ValidationRunner.fs` (3 baseline, includes `Process`/path) with pattern-match / `Option.ofObj` per D5.

### Test projects (8 baseline — included per FR-007 / D2)

- [X] T017 [P] [US1] Clear FS3261 sites in the test projects (BCL nullable returns in setup, incl. `Process | null` / `DirectoryInfo | null`, D5): `tests/FS.GG.SDD.Cli.Tests/ValidateCommandTests.fs` (4), `tests/FS.GG.SDD.Artifacts.Tests/TestSupport.fs` (2), `tests/FS.GG.SDD.Commands.Tests/LifecycleSmokeTests.fs` (1), `tests/FS.GG.SDD.Validation.Tests/IsolationTests.fs` (1). Re-measure if the 8 sites land in slightly different files than the baseline estimate.

### Story-1 verification (after all cleanup tasks above)

- [X] T018 [US1] Clean Release build over the whole solution emits **0** FS3261 and **0** FS0025 (SC-001/SC-002, V-1): `dotnet build -c Release --no-incremental 2>&1 | grep -E "warning FS3261|warning FS0025" | wc -l` → 0. Iterate on any straggler files until 0.
- [X] T019 [US1] Run the full suite — `dotnet test` — and confirm all 438 tests pass with no behavior change (SC-004, V-2). Then diff `--json` output for charter/analyze/refresh against the T002 baselines (`diff /tmp/<cmd>.before.json <(… --json …)`); every diff MUST be empty (SC-004, V-3, INV-1).
- [X] T019a [US1] Confirm no public surface moved (FR-008 / INV-3, Tier 2): the `SurfaceBaselineTests` in every project (`tests/FS.GG.SDD.{Artifacts,Cli,Commands,Validation}.Tests/SurfaceBaselineTests.fs`) pass unchanged, and no `.fsi` file was added or modified by the cleanup (`git diff --name-only -- '*.fsi'` is empty).
- [-] T020 [P] [US1] (Optional, SC-005 evidence — SKIPPED: `Internal` helpers are `module internal` with no `InternalsVisibleTo`, so a direct unit test would require a surface change; coalescing behavior is covered by the full suite + byte-identical `--json`) Add one null-coalescing unit test in `tests/FS.GG.SDD.Artifacts.Tests/` asserting an `Internal` helper returns the identical value (e.g. `""`/`None`) for a missing/null JSON field as before — direct evidence that null handling is observable only at the centralized boundary.
- [X] T021 [US1] Confirm `data-model.md` "Enumerated suppressions (FR-009)" is still **empty**, or — only if an intractable BCL site was found — list it there with `file:line` and justification before merge (FR-009 / D6). Confirm no file/project-scope `#nowarn "3261"` / `#nowarn "25"` was introduced (INV-5).

**Checkpoint**: Solution is null-clean (count 0), tests green, output byte-identical. US1 is independently shippable. The gate (US2) can now be flipped on safely.

---

## Phase 4: User Story 2 — Enable the regression gate (Priority: P2)

**Goal**: Add the scoped `WarningsAsErrors=FS3261;FS0025` gate so the clean state cannot silently re-accumulate.

**Independent Test**: With count at 0, the gate-on build succeeds; an injected nullness defect fails the build with FS3261 as an error; reverting returns it to green; no off-scope category is promoted.

**⚠️ Depends on US1**: T022 MUST land only after T018 reports 0 — enabling the gate while any FS3261 site remains breaks the build (research D7).

- [X] T022 [US2] Add the scoped escalation to `Directory.Build.props`: keep `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>` and add `<WarningsAsErrors>FS3261;FS0025</WarningsAsErrors>` (contract C1). Inherited by all `src` and test projects; do not touch `<Nullable>enable</Nullable>`.
- [X] T023 [US2] Clean rebuild with the gate on succeeds — `dotnet build -c Release --no-incremental` → "Build succeeded, 0 warnings, 0 errors" (SC-001 still holds, V-4).
- [X] T024 [US2] Prove the gate bites (SC-003 / V-5): temporarily revert one coalesced helper in `Internal.fs` to `Some(value.GetString())`, rebuild, confirm **Build FAILED** with `error FS3261` at that line; `git checkout -- src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Internal.fs`; rebuild and confirm green again. (Build-level check, not a committed test.)
- [X] T025 [US2] Confirm gate scope (SC-006 / V-6): `dotnet build -c Release --no-incremental 2>&1 | grep -E "error FS" | grep -vE "FS3261|FS0025" | wc -l` → 0. No warning category outside FS3261/FS0025 fails the build.

**Checkpoint**: Gate is enabled and demonstrably effective; scope is confirmed. Both user stories complete.

---

## Phase 5: Polish & validation

**Purpose**: End-to-end confirmation and record-keeping.

- [X] T026 Run the full `quickstart.md` "Done when" checklist top to bottom (Steps 0–2) and confirm every box (counts 0; tests pass; `--json` diffs empty; gate added; injected defect fails; scope clean; suppressions list empty/justified).
- [X] T027 [P] Update the roadmap status for R5 in `docs/reports/2026-06-26-074428-refactor-analysis.md` (and any report index) to reflect the shipped null-clean + gated state, noting the final measured unique-site count cleared.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** — no dependencies; run first to capture baselines.
- **Phase 2 (Foundational JSON boundary)** — depends on Phase 1; **blocks** the parser-family residual tasks T005–T008.
- **Phase 3 (US1)** — T005–T008 depend on T003; T009–T017 are independent of the boundary fix but conceptually part of the same null-clean pass; T018–T021 (verification) run after all cleanup tasks.
- **Phase 4 (US2)** — depends on T018 reporting **0** (research D7 sequencing).
- **Phase 5 (Polish)** — after US2.

### Within US1

- T003 (boundary) before T005–T008 (parser residuals).
- T009–T017 each touch a distinct file and have no ordering dependency among themselves.
- All of T005–T017 before the T018 zero-count gate.

### Parallel opportunities

- T005–T008 (parser residuals) run in parallel once T003 lands.
- T009–T017 are all `[P]` — distinct files across Artifacts / Commands / Validation / tests; run concurrently.
- T020 (optional unit test) is independent and `[P]`.

### Parallel example (after T003)

```text
# Parser residuals + remaining clusters together (distinct files):
Task: T005 Analysis.fs   Task: T006 Verify.fs   Task: T007 Ship.fs   Task: T008 Guidance.fs
Task: T009 WorkModel.fs  Task: T010 ReleaseContract.fs  Task: T011 GenerationManifest.fs
Task: T012 SchemaVersion.fs  Task: T013 Artifacts long tail
Task: T014 Commands assembly  Task: T015 ValidationContracts.fs  Task: T016 ValidationRunner.fs
Task: T017 test projects
```

---

## Implementation Strategy

### MVP (User Story 1 only)

1. Phase 1: capture baselines.
2. Phase 2: centralize the JSON boundary (T003–T004).
3. Phase 3: clean the remaining clusters to 0 and verify (T005–T021).
4. **STOP and VALIDATE**: count 0, tests green, `--json` byte-identical. Shippable on its own — a clean warning signal with zero behavior change.

### Incremental delivery

- Ship US1 (clean signal) → then US2 (the durable ratchet). The gate must come second so the build never breaks mid-refactor (D7).

---

## Notes

- `[P]` = different file, no dependency on another incomplete task in this phase.
- Every cleanup task is behavior-preserving (INV-1); the existing suite + byte-identical `--json` are the only behavioral evidence required (no test-first tasks for this Tier 2 refactor).
- Baseline per-file counts are estimates from the 2026-06-26 analysis; re-measure against current `main` (T001) and follow the actual site distribution — the target (0) is unaffected by minor drift.
- No public `.fsi` or surface baseline moves (INV-3 / FR-008); verified explicitly by T019a (surface-baseline tests green + no `.fsi` diff).
- Commit after each task or logical group; never mark a task `[X]` while FS3261 sites remain in its file.
