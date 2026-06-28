# Tasks: Composition-Acceptance Consumes the Dispatched Registry

**Input**: Design documents from `/specs/041-composition-acceptance-dispatch/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/registry-dispatch.md, quickstart.md

**Branch**: `041-composition-acceptance-dispatch`

**Tier**: Tier 1 (contracted change) — consumes the versioned cross-repo
`composition-registry-updated` dispatch contract, owned jointly with FS.GG.Templates. No F#
public surface (`.fsi`/baseline unchanged), no new lifecycle artifact, no schema change.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase (different file).
- **[Story]**: which user story the task serves (US1/US2/US3); omit for shared/cross-cutting work.
- Tier annotation omitted throughout — every phase matches the spec's overall Tier 1.
- Each task names an exact file path.

## Elmish/MVU applicability

Principle IV/V (Elmish/MVU boundaries) is **not applicable** to this feature: it adds **no F#
product code**. The only executable artifact is a deterministic POSIX-shell resolver at the
CI process edge (recorded as a deliberate in-scope exception in plan.md Constitution Check V).
F# appears only as the resolver's xUnit test harness. There is no `Model`/`Msg`/`Effect`,
no `update`, no interpreter boundary to contract.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the new directory and contract grounding both user stories build on.

- [X] T001 Create the `scripts/workflows/` directory at the repo root (currently absent) so the
  resolver script and its test fixture have a home, per plan.md Structure Decision.
- [X] T002 Confirm the consumed dispatch contract is fixed before coding: re-read
  `specs/041-composition-acceptance-dispatch/contracts/registry-dispatch.md` and record (in the
  task notes / commit message) the exact `client_payload` field names the resolver will read
  (`registry_content`, `registry_path`, `registry_sha256_12`, `version`) so the script invents no
  field (FR-009). No code change.

**Checkpoint**: directory exists; the field names the resolver depends on are pinned to the contract.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Define the resolver's CLI/env contract and its expected behavior table **before**
either user story implementation, so US1 (the resolver + tests) and US2 (the green run) share one
agreed surface. No behavior is implemented here.

**⚠️ CRITICAL**: No user story work begins until this phase is complete.

- [X] T003 Write the resolver's input/output contract as a header comment block in a stub
  `scripts/workflows/resolve-acceptance-registry.sh` (executable, `#!/usr/bin/env bash`, no logic
  yet — `exit 1` placeholder): document the **exact** env vars it reads — pinned here as the one name
  set quickstart, workflow, and tests all reference (resolves the env-name drift): `REGISTRY_PATH_INPUT`
  (manual `registry_path` input), `FSGG_DISPATCH_REGISTRY_CONTENT` + `FSGG_DISPATCH_REGISTRY_SHA256_12`
  (dispatched `client_payload.registry_content` / `registry_sha256_12`), `REGISTRY_SECRET_CONTENT`
  (scheduled secret content), plus `GITHUB_EVENT_NAME` and `RUNNER_TEMP`; the `--print-env` mode used by
  quickstart Scenario 3; and the deterministic precedence chain (input > dispatch content > secret) with
  the fail-closed rules (empty dispatch content **and** dispatch sha mismatch both fail closed). This is
  the single source of truth the tests assert against (data-model.md "Registry source" + "State → outcome").

**Checkpoint**: the resolver's contract (env names, `--print-env`, precedence, fail-closed) is
written down and matches data-model.md; implementation and tests can now proceed against it.

---

## Phase 3: User Story 1 — Test the live registry, drift-free (Priority: P1) 🎯 MVP

**Goal**: SDD's composition-acceptance accepts the dispatched `composition-registry-updated` event
as a first-class registry source, materializes its content verbatim, points
`FSGG_SDD_ACCEPTANCE_REGISTRY` at it, and runs the unchanged acceptance facts over it — with
deterministic single-source selection and fail-closed on empty dispatch.

**Independent Test**: `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~RegistryResolverTests"`
is green; the resolver, driven through a real shell over real temp files, selects the right source
per precedence, materializes bytes verbatim (matching the advertised sha), and exits non-zero on an
empty dispatch — all without the nightly schedule and without any secret set (quickstart Scenario 1).

### Tests for User Story 1 ⚠️ (write FIRST — must FAIL before T010)

> Tests live in the existing `FS.GG.SDD.Acceptance.Tests` project and reuse its real process edge
> `AcceptanceSupport.runToCompletion`, as plain `[<Fact>]` (offline/deterministic) — **not**
> `RequiresRegistryFact`, so they run in the default inner loop. Gate the whole module to a
> shell-available host (skip on Windows) so the cross-platform inner loop stays green (plan.md
> Target Platform).

- [X] T004 [P] [US1] Create `tests/FS.GG.SDD.Acceptance.Tests/RegistryResolverTests.fs` with a
  `module RegistryResolverTests`, a Windows/no-bash skip guard, and a helper that invokes
  `scripts/workflows/resolve-acceptance-registry.sh` via `runToCompletion` with a controlled env
  and a fresh `RUNNER_TEMP` temp dir. No assertions yet beyond the harness compiling.
- [X] T005 [US1] Register the new test file in
  `tests/FS.GG.SDD.Acceptance.Tests/FS.GG.SDD.Acceptance.Tests.fsproj`: add
  `<Compile Include="RegistryResolverTests.fs" />` (after `AcceptanceSupport.fs`, before/after the
  other offline test files) and add an `<None Include="../../scripts/workflows/resolve-acceptance-registry.sh"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>` item so the script is copied beside the test assembly and is reachable from the test working directory. Use this single mechanism — do **not** also compute a repo-root-relative path in the test (one deterministic script location). Depends on T004.
- [X] T006 [P] [US1] Fact: **dispatch source, verbatim materialization** — given
  `GITHUB_EVENT_NAME=repository_dispatch` and non-empty `registry_content` containing multi-line
  YAML + special characters, the resolver writes the bytes **byte-for-byte** to an ephemeral file,
  prints/exports that path, exits 0, and the file's first-12 sha256 equals the advertised
  `registry_sha256_12` (FR-002/FR-008, data-model.md D4/D5). Add to `RegistryResolverTests.fs`.
- [X] T007 [P] [US1] Fact: **deterministic precedence — manual input overrides** — given both a
  manual `registry_path` input and a secret (and/or a dispatch payload), the resolver selects the
  input path (precedence 1) and exits 0 (FR-004, data-model.md D2). Add to `RegistryResolverTests.fs`.
- [X] T008 [P] [US1] Fact: **secret fallback** — given only the scheduled secret content (no input,
  no dispatch), the resolver materializes the secret content verbatim, exports its path, exits 0
  (FR-004 precedence 3). Add to `RegistryResolverTests.fs`.
- [X] T009 [P] [US1] Fact: **fail closed on empty dispatch and on sha mismatch** — (a) given
  `GITHUB_EVENT_NAME=repository_dispatch` with missing/empty `registry_content`, the resolver exits
  **non-zero**, emits a clear `::error::` diagnostic, prints **no** path, and writes no registry file
  (FR-005, SC-005, data-model.md outcome table); distinct from the no-source-at-all case. (b) given
  `GITHUB_EVENT_NAME=repository_dispatch` with non-empty `registry_content` whose recomputed first-12
  sha256 ≠ the advertised `FSGG_DISPATCH_REGISTRY_SHA256_12`, the resolver also exits **non-zero** with
  an integrity-mismatch `::error::` and prints no path (data-model.md "State → outcome" mismatch row /
  D5). Add both to `RegistryResolverTests.fs`.

### Implementation for User Story 1

- [X] T010 [US1] Implement `scripts/workflows/resolve-acceptance-registry.sh` to satisfy T006–T009:
  `set -euo pipefail`; the deterministic precedence chain (manual input path > dispatch
  `registry_content` > secret content); verbatim write with `printf '%s'` to
  `${RUNNER_TEMP}/fsgg/providers.yml`; recompute the materialized file's sha256 (`sha256sum` /
  `shasum -a 256`, first 12 hex) and, on a dispatch run, verify it matches the advertised
  `registry_sha256_12`, **failing closed (`::error::` + non-zero exit) on a mismatch**; fail closed
  with `::error::` + non-zero exit on a dispatch with empty content; print the resolved path and support `--print-env` (emit
  `FSGG_SDD_ACCEPTANCE_REGISTRY=<path>` for `eval` per quickstart Scenario 3) and the
  `$GITHUB_ENV`/`$GITHUB_STEP_SUMMARY` append used by CI. Carry **no** rendering id/template/path/docs
  token (FR-003/SC-003). Tests T006–T009 turn green here. Depends on T003–T009.
- [X] T011 [US1] Wire the resolver into `.github/workflows/composition-acceptance.yml`: add
  `repository_dispatch: { types: [composition-registry-updated] }` to the `on:` triggers (alongside
  `schedule` and `workflow_dispatch`); add a top-level `concurrency: { group: composition-acceptance,
  cancel-in-progress: true }` so a burst of upstream registry edits supersedes in-flight runs and only
  the latest content is tested (spec Edge Cases: "Burst of upstream registry edits"); replace the
  inline "Materialize the external provider registry" step body with a call to
  `scripts/workflows/resolve-acceptance-registry.sh`, mapping its env to the **pinned names from T003**
  — `REGISTRY_PATH_INPUT` ← the manual input, `REGISTRY_SECRET_CONTENT` ←
  `secrets.FSGG_SDD_ACCEPTANCE_REGISTRY`, and `FSGG_DISPATCH_REGISTRY_CONTENT` /
  `FSGG_DISPATCH_REGISTRY_SHA256_12` ←
  `github.event.client_payload.{registry_content,registry_sha256_12}`. Keep the "Run the composition
  acceptance" step (`dotnet test ... --filter "kind=composition-acceptance"`) byte-identical so the
  acceptance facts run unchanged (FR-006). Depends on T010.

**Checkpoint**: US1 is independently testable — resolver facts pass offline; the workflow accepts
the dispatch and feeds the unchanged acceptance. Drift-free guarantee delivered (SC-001).

---

## Phase 4: User Story 2 — First green nightly across the composed boundary (Priority: P2)

**Goal**: With a live registry resolving the published rendering template and the merged
Rendering root-build wrappers (Rendering#9), the composition-acceptance's build and run facts pass
and the overall verdict is green — and the run records the registry content identity it tested.

**Independent Test**: Run the acceptance with a live registry available (quickstart Scenario 3 /
Scenario 4 manual path) and confirm the build + run facts succeed and the result records the tested
registry's sha (drift signal). Depends on US1 (a real registry source to resolve).

### Implementation for User Story 2

- [X] T012 [US2] Surface the drift signal to the run in
  `.github/workflows/composition-acceptance.yml`: after resolution, append the tested registry
  identity (the 12-char `registry_sha256_12` / `version`, plus the resolver-recomputed sha) to the
  GitHub Step Summary (`$GITHUB_STEP_SUMMARY`) so every dispatch-sourced run is traceable to one
  exact content hash (FR-008, SC-006). The `composition-acceptance-result` v1 document stays
  unmodified — the signal lives at the run layer only. Depends on T011.
- [-] T013 [US2] Validate the green outcome end-to-end against a live registry per quickstart
  Scenario 3 (simulate the dispatch locally: export `GITHUB_EVENT_NAME=repository_dispatch` +
  registry content, `eval "$(scripts/workflows/resolve-acceptance-registry.sh --print-env)"`, then
  `dotnet test FS.GG.SDD.sln --filter "kind=composition-acceptance"`). Confirm the build and run
  facts pass over the composed product and the verdict is **pass** (FR-010, SC-002).
  **SKIPPED — no live registry reachable in this offline environment.** Rationale: the green
  verdict requires resolving the real *published* rendering template (network + package-feed
  access) which is not available in the offline sandbox; with `FSGG_SDD_ACCEPTANCE_REGISTRY`
  unset the `kind=composition-acceptance` facts are discovery-skipped (confirmed in T014). The
  resolver half (source selection, verbatim materialization, integrity check, fail-closed) is
  fully proven offline by `RegistryResolverTests` (T006–T010). The authoritative proof of SC-002
  is the **CI scheduled/manual path** (quickstart Scenario 4): once the org App dispatches (or the
  nightly secret runs) against the live registry with Rendering#9's root-build wrappers merged, the
  build/run facts pass. The wiring that feeds that path (T011/T012) is in place and unit-covered.

**Checkpoint**: a live-registry run reaches a passing verdict and records the content hash it tested.

---

## Phase 5: User Story 3 — Existing sources and the offline inner loop are untouched (Priority: P3)

**Goal**: The new dispatch source is purely additive — the offline inner loop, the manual
`registry_path` path, and the scheduled secret path all behave exactly as before.

**Independent Test**: With no registry env set, `dotnet test FS.GG.SDD.sln` is green with every
`RequiresRegistryFact` composition-acceptance fact **Skipped** and no network touched; the manual
and scheduled paths still resolve as before (quickstart Scenario 2).

### Implementation for User Story 3

- [X] T014 [US3] Verify the offline inner loop is unaffected: run `dotnet test FS.GG.SDD.sln` with
  **no** `FSGG_SDD_ACCEPTANCE_REGISTRY` set and confirm the suite is green, the network-gated
  composition-acceptance facts report Skipped, the new `RegistryResolverTests` run as cheap local
  process spawns, and wall-clock is unchanged versus before this feature (FR-007, SC-004). Record
  the result; no code change expected (this is a regression guard).
- [X] T015 [P] [US3] Confirm source-path back-compat in the resolver/workflow: assert via the
  existing resolver facts (and a workflow read-through) that the manual `registry_path` input and
  the scheduled secret still resolve exactly as today — the dispatch branch only **adds** a source,
  with no behavioral fork downstream (FR-004/FR-006, US3 acceptance scenario 2). If an extra
  targeted fact is needed it belongs in `tests/FS.GG.SDD.Acceptance.Tests/RegistryResolverTests.fs`.

**Checkpoint**: all three sources work; the inner loop is provably unregressed.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Identity-leak guard, cross-repo coordination bookkeeping, and final validation.

- [X] T016 [P] Identity-leak check (SC-003 / FR-003): run the quickstart "Identity-leak check"
  `git grep` over committed SDD source excluding `specs/**` and `docs/**`, and eyeball the resolver
  + workflow + new test file, confirming **zero** rendering package id / template id / path / docs
  URL tokens appear anywhere in SDD source or any produced result document.
- [X] T017 [P] Record the cross-repo coordination obligation (FR-009): note in the PR description /
  Coordination board that `composition-registry-updated` v1 is now **consumed** by SDD (per
  `contracts/registry-dispatch.md`) and that any field/event-type change is a coordinated two-sided
  change with FS.GG.Templates. Update the contract/compatibility registry entry via the
  **cross-repo-coordination** protocol. No SDD source change.
- [X] T018 Run the full quickstart validation (`specs/041-composition-acceptance-dispatch/quickstart.md`
  Scenarios 1–4 to the extent reachable offline) and confirm each expected outcome; capture results
  on this task line.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** — no dependencies; start immediately.
- **Foundational (Phase 2)** — depends on Phase 1; **blocks** all user stories (defines the resolver
  contract every test asserts against).
- **US1 (Phase 3)** — depends on Phase 2. The MVP; delivers the drift-free guarantee.
- **US2 (Phase 4)** — depends on US1 (needs the resolver + dispatch wiring to feed a live registry).
- **US3 (Phase 5)** — depends on US1 existing (it guards that US1's addition didn't regress the
  inner loop / existing sources); independently *testable* once US1 lands.
- **Polish (Phase 6)** — depends on all desired user stories.

### Within US1

- Tests (T004–T009) are written and **must FAIL** before the implementation (T010) makes them pass.
- The test harness (T004) and fsproj registration (T005) precede the individual facts (T006–T009).
- Resolver implementation (T010) precedes workflow wiring (T011).

### Parallel opportunities

- T006, T007, T008, T009 are independent facts in the same new file — author together (one file, so
  coordinate the final merge), each asserting a distinct source/outcome row of data-model.md.
- T016 and T017 (polish) are independent and parallel-safe.
- US2 and US3 both build on US1; once US1 is merged they can proceed in parallel.

### Suggested MVP scope

**User Story 1 (Phase 3)** — the resolver + dispatch trigger that closes the unwired-registry gap
(SC-001). US2 (first green, SC-002) and US3 (no-regression, SC-004) layer on top.

---

## Task count

- **US1 (P1, MVP)**: 8 tasks (T004–T011) — 5 test facts/harness, 1 resolver, 1 workflow wiring, 1 fsproj.
- **US2 (P2)**: 2 tasks (T012–T013).
- **US3 (P3)**: 2 tasks (T014–T015).
- **Setup/Foundational**: 3 tasks (T001–T003).
- **Polish**: 3 tasks (T016–T018).
- **Total**: 18 tasks.

## Notes

- `[P]` = different file, no dependency on another incomplete task in this phase.
- Never mark a failing task `[X]`; never weaken an acceptance fact to green a build — narrow scope
  and document on the task line (`[-]` with rationale).
- T013 may legitimately resolve `[-]` if no live registry is reachable offline — the CI
  scheduled/manual path (quickstart Scenario 4) is then the authoritative proof of SC-002.
- Commit after each task or logical group; the resolver (T010) and its tests (T006–T009) should land
  together so the FAIL→PASS transition is visible in history.
