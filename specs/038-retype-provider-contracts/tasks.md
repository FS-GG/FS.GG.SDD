---
description: "Task list for feature 038 — Re-type Provider Registry onto FS.GG.Contracts & Honor Declared Probe Commands"
---

# Tasks: Re-type Provider Registry onto FS.GG.Contracts & Honor Declared Probe Commands

**Input**: Design documents from `/specs/038-retype-provider-contracts/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [data-model.md](./data-model.md), [research.md](./research.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tier**: Tier 1 — `scaffold-provider` contract surface. Adopts the already-published
`FS.GG.Contracts` 1.0.0 provider types; no new public schema version (FR-012).

**Tests**: Included — Principle VI (test evidence is mandatory) applies, and the spec
ties SC-002/003/004/005/006 to concrete tests. New tests MUST fail before implementation
and pass after.

**Elmish/MVU applicability (Principle IV/V)**: `parseProviderRegistry` is a **pure
parser** (`FileSnapshot -> Result<ProviderDescriptor list, Diagnostic list>`) — no MVU
boundary. The scaffold handler is already MVU; the acceptance probes already own their
process-spawning edge interpreter (feature 035). **No new I/O boundary is introduced**,
so no new `.fsi` MVU contract/transition-test obligations arise beyond the `Config.fsi`
surface edit. This is recorded in the evidence-obligations task (T004).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase
- **[Story]**: US1 / US2 / US3 (traceability to spec user stories)
- Phases run in sequence; tasks within a phase may run in parallel where marked `[P]`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Wire the canonical contracts package into the one owner of provider parsing.
This is the shared prerequisite for US1 and US2.

- [X] T001 Add `<ProjectReference Include="..\FS.GG.Contracts\FS.GG.Contracts.fsproj" />` to
  `src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj` (clean leaf dependency — Contracts
  is BCL-only). Match the in-repo `ProjectReference` form already used by
  `FS.GG.Contracts.Tests`, not a versioned `PackageReference`.
- [X] T002 Restore with locked mode and confirm no package drift:
  `dotnet restore FS.GG.SDD.sln --locked-mode`. Because `FS.GG.Contracts` is BCL-only,
  `src/FS.GG.SDD.Artifacts/packages.lock.json` MUST be unchanged; if restore reports the
  lock is stale, regenerate it and verify the diff adds no new package — only the project
  edge.
  > Outcome: `--locked-mode` reported NU1004 (a new `fs.gg.contracts` project edge). Regenerated
  > with `--force-evaluate`; the only lock change across all 9 `packages.lock.json` files is the
  > `"type": "Project"` edge to `fs.gg.contracts` (whose sole dependency, FSharp.Core, was already
  > present). No new third-party package — exactly the project edge.

**Checkpoint**: `FS.GG.SDD.Artifacts` can `open Fsgg.Provider`; solution still builds
against the *old* local types (no source change yet).

---

## Phase 2: User Story 1 - One canonical provider type (Priority: P1) 🎯 MVP

**Goal**: A single authoritative `ProviderDescriptor` / `ProviderParameterSpec` (in
`FS.GG.Contracts`). `parseProviderRegistry` returns the canonical type; the local
re-encoding in `Config.fs` is deleted; every scaffold-path consumer recompiles with **no
behavior change** for any registry expressible today.

**Independent Test**: Build the solution after removing the local types; run the existing
scaffold/provenance matrix and confirm byte-identical `CommandReport` / scaffold summary /
provenance / diagnostics; a repo search finds exactly one `ProviderDescriptor` and one
`ProviderParameterSpec`.

### Implementation for User Story 1

- [X] T003 [US1] **FSI first** (Principle I/III) — edit
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fsi`: remove the local
  `ProviderDescriptor` and `ProviderParameterSpec` declarations and re-type
  `parseProviderRegistry`'s return to `Fsgg.Provider.ProviderDescriptor list`
  (within the existing `Result<_, Diagnostic list>`). Add the `Fsgg.Provider` namespace
  visibility as needed. (FR-001)
- [X] T004 [US1] Record evidence obligations as a one-line note in `tasks.md`/PR: confirm
  Principle V is **N/A** (pure parser; no new MVU boundary — see header) and enumerate the
  Principle VI tests this feature adds (US1 T008/T009, US2 T010–T012, US3 T019/T020/T021).
  Disclose the **SC-004 limit**: the reference-provider verdict-identical outcome is exercised
  only on the network-gated path (T024) and is structurally backed offline by the
  `declared=None` probe branch (T020) and the resolve-and-bind glue test (T019); no
  deterministic CI test reproduces the full end-to-end verdict. No code in this task.
- [X] T005 [US1] Edit `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fs`:
  `open Fsgg.Provider`; delete the local `ProviderDescriptor` / `ProviderParameterSpec`
  records; map the existing parse output into the canonical record's five preserved fields
  (`Name/ContractVersion/TemplateId/Source/Parameters`) with the new fields defaulted
  (`Build=Test=Run=Verify=None`, `NameParameter=defaultNameParameter`). Preserve the
  drop-incomplete and schema-version gate logic exactly (FR-006, FR-007). Extended-field
  *reads* are deferred to US2 (T013).
- [X] T006 [US1] Recompile the scaffold-path consumers against the canonical descriptor.
  `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs` is the **sole** consumer file
  (verified: it holds `effectiveParameters`, `missingRequiredParameters`, the contract-version
  support check, `ScaffoldProceed`, `scaffoldInvocationEffects`, `provenanceWriteEffect`, and
  reaches the parser via its local alias `module ConfigModule = FS.GG.SDD.Artifacts.Config`);
  no other `src` file references `ProviderDescriptor`/`parseProviderRegistry`. The five
  preserved field names are identical, so edits should be limited to namespace resolution;
  make no behavioral change. (FR-002)
- [X] T007 [US1] Build the whole solution: `dotnet build FS.GG.SDD.sln -c Release`. Fix any
  remaining bind errors from the removed local types so every consumer compiles against
  `Fsgg.Provider`.

### Tests for User Story 1

- [X] T008 [P] [US1] **Regression (must stay green)** — run the existing scaffold/provenance
  matrix (`tests/FS.GG.SDD.Artifacts.Tests/ScaffoldProvenanceTests.fs`,
  `FS.GG.SDD.Commands.Tests`, `FS.GG.SDD.Cli.Tests`) and confirm byte-identical
  `CommandReport` / scaffold summary / provenance and diagnostics
  (`scaffold.providerUnknown`, `scaffold.providerMissing`, unsupported-contract,
  missing-required-parameter). Zero golden changes. (SC-002, FR-006/FR-007)
- [X] T009 [P] [US1] Add a single-definition assertion (SC-001): a repo search proving
  exactly one `ProviderDescriptor` and one `ProviderParameterSpec` definition remain (both
  in `FS.GG.Contracts`) and none in `FS.GG.SDD.Artifacts`. Implement as a test in
  `tests/FS.GG.SDD.Artifacts.Tests/` or document the search in the PR if a test is
  impractical.

**Checkpoint**: MVP — one canonical type, the local re-encoding gone, zero regression. The
declared-command fields exist on the descriptor (all `None`) but are not yet read from YAML.

---

## Phase 3: User Story 2 - Registry parsing reads the extended contract fields (Priority: P2)

**Goal**: `parseProviderRegistry` reads optional `build`/`test`/`run`/`verify` declared
commands and `nameParameter` from `.fsgg/providers.yml` into the canonical descriptor, with
behavior-preserving defaults (blank executable ⇒ `None`; absent/blank `nameParameter` ⇒
`"name"`).

**Depends on**: US1 (T005 establishes the canonical mapping this story extends).

**Independent Test**: Parse a synthetic registry declaring `build`, `run`, and
`nameParameter` and confirm the descriptor carries those values; parse one declaring none
and confirm all command fields `None` and `NameParameter = "name"`; parse a blank-executable
declaration and confirm `None`.

### Tests for User Story 2 (write FIRST — must fail before T013)

- [X] T010 [P] [US2] Add a parse test in
  `tests/FS.GG.SDD.Artifacts.Tests/ScaffoldProvenanceTests.fs` (or a new
  `ProviderRegistryParseTests.fs` in the same project): given a registry entry declaring
  `build` and `run` (executable + arguments) and a `nameParameter`, assert
  `Build = Some {Executable=...; Arguments=[...]}`, `Run = Some {...}`, `Test = None`,
  `Verify = None`, `NameParameter = "<declared>"`. (SC-003, FR-003/FR-004)
- [X] T011 [P] [US2] Add a defaults test: a today-shape entry (no extended keys) parses to
  `Build/Test/Run/Verify = None` and `NameParameter = "name"`. (FR-006)
- [X] T012 [P] [US2] Add a blank-executable test: `build: { executable: "   ", arguments: [x] }`
  parses to `Build = None` (treated as "not declared" via `isMalformed`, never a launchable
  empty executable). Cover `nameParameter:` blank ⇒ `"name"` too. (FR-005)

### Implementation for User Story 2

- [X] T013 [US2] Extend the parse in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fs` per
  [data-model.md](./data-model.md) §"Registry → descriptor mapping": for each of
  `build`/`test`/`run`/`verify`, read `executable` (scalar) + `arguments` (sequence,
  default `[]`) into a candidate `DeclaredCommand`, mapping to `None` when
  `Fsgg.Provider.isMalformed` (blank executable) else `Some`; read optional `nameParameter`
  scalar into `NameParameter` (stored value resolves to `"name"` via `resolveNameParameter`
  when blank). Reuse the existing YamlDotNet helpers; do not re-implement the helpers from
  `FS.GG.Contracts`. (FR-003/FR-004/FR-005)
- [X] T014 [P] [US2] Add a fixture
  `tests/fixtures/scaffold-provider/registries/with-declared-commands.providers.yml`
  declaring `build`, `run`, and `nameParameter` (matching the contract's v1 encoding in
  [contracts/provider-registry-encoding.md](./contracts/provider-registry-encoding.md)),
  for use by T010 and the quickstart Scenario B.
- [X] T015 [US2] Run the US2 parse tests (T010–T012) and confirm they now pass; confirm the
  US1 regression matrix (T008) still green. (SC-003)

**Checkpoint**: The canonical descriptor is now *populated* from the registry — declared
commands are readable on the scaffold path. US1 + US2 both work; no regression.

---

## Phase 4: User Story 3 - Acceptance probes honor the declared build/run commands (Priority: P3)

**Goal**: The opt-in composition acceptance harness flows the **resolved descriptor's**
declared `Build` and `Run` commands into the build/run probes (declared-or-default ready
since feature 035), and retires the harness's local `DeclaredCommand` copy for the canonical
one. The reference provider (declares none) falls through to today's `dotnet` defaults — an
observably unchanged verdict.

**Depends on**: US1 + US2 (the descriptor must carry declared commands).

**Independent Test**: With a synthetic descriptor declaring no build/run command, the probes
invoke the `dotnet` defaults and yield the same facts as today; with one declaring a trivial
build/run command, the probes invoke the declared command and **never** start a `dotnet`
process for the declared case — all offline, no real provider.

### Setup for User Story 3

- [X] T016 [US3] Add `<ProjectReference Include="...\FS.GG.Contracts\FS.GG.Contracts.fsproj" />`
  to `tests/FS.GG.SDD.Acceptance.Tests/FS.GG.SDD.Acceptance.Tests.fsproj`; restore locked
  and confirm `tests/FS.GG.SDD.Acceptance.Tests/packages.lock.json` gains no new package
  (BCL-only Contracts), only the project edge.

### Implementation for User Story 3

- [X] T017 [US3] In `tests/FS.GG.SDD.Acceptance.Tests/AcceptanceSupport.fs`: delete the local
  `DeclaredCommand` record and `open Fsgg.Provider`; re-type the `buildProbe` / `runProbe`
  declared-command parameter to `Fsgg.Provider.DeclaredCommand option`. Bounded-execution
  semantics (build 300 s timeout; run 10 s grace / 60 s overall; cannot-start / non-zero /
  timeout ⇒ diagnosed non-zero) are unchanged. (FR-010, retires the 035 local copy)
- [X] T018 [US3] In `tests/FS.GG.SDD.Acceptance.Tests/CompositionAcceptanceTests.fs`: resolve
  the provider descriptor and flow `descriptor.Build` into the build probe and
  `descriptor.Run` into the run probe (previously always `None`). `Test`/`Verify` remain
  unwired (no probe consumes them). (FR-008)

### Tests for User Story 3 (write/extend FIRST — must fail before T017/T018)

- [X] T019 [P] [US3] **Offline coverage for the FR-008 harness glue** — add a deterministic
  (non-network) test exercising the descriptor resolve-and-bind step that T018 adds to
  `CompositionAcceptanceTests.fs` (currently this glue runs only on the network-gated path).
  Over a **synthetic** registry written to a temp `root` (`.fsgg/providers.yml`), drive the
  same `parseProviderRegistry` → `Result.toOption` → `List.tryFind (d.Name = providerName)` →
  `Option.bind (fun d -> d.Build)` / `d.Run` resolution the harness uses, and assert the
  selected `DeclaredCommand option` matches the declared registry entry (and `None` for the
  today-shape/missing-provider cases). Extract the resolution into a small testable helper in
  `AcceptanceSupport.fs` if needed so it is callable without a real provider. (FR-008)
- [X] T020 [P] [US3] Extend `tests/FS.GG.SDD.Acceptance.Tests/ProbeResolutionTests.fs` with a
  **synthetic** `Fsgg.Provider.ProviderDescriptor`: `buildProbe None root` resolves to the
  `dotnet build` default (unchanged facts); `buildProbe (Some {Executable="<trivial-exe>";
  Arguments=[...]}) root` invokes the trivial command and asserts — by the trivial command's
  deterministic exit — that **no** `dotnet` process is started for the declared case. Mirror
  for `runProbe` under the grace/overall window. (SC-005, FR-009/FR-010)
- [X] T021 [P] [US3] Confirm the provider-agnostic invariant test (T021a — "acceptance
  project carries no Governance reference / no provider-specific package id, template id,
  path, command string, or docs URL") still passes after adding the `FS.GG.Contracts`
  reference and `Fsgg.Provider` usage. (FR-011, SC-006)

**Checkpoint**: The harness is provider-honoring. All three stories complete.

---

## Phase 5: Polish & Validation

- [X] T022 [P] Run the full offline inner loop: `dotnet build FS.GG.SDD.sln -c Release` then
  `dotnet test FS.GG.SDD.sln -c Release`. Confirm all tests pass and the network-gated
  composition tests report **Skipped** (no `FSGG_SDD_ACCEPTANCE_REGISTRY`).
- [X] T023 [P] Walk [quickstart.md](./quickstart.md) Scenarios A–D and check off its
  "Done when" list (build with re-encoding removed; byte-identical matrix; new parse tests;
  probe-honors-declared offline without starting `dotnet`; T021a passes).
- [X] T024 [P] (network-gated, optional in CI) When `FSGG_SDD_ACCEPTANCE_REGISTRY` is
  available, run the composition acceptance suite against the reference provider and confirm
  the `composition-acceptance-result` verdict is identical in pass/fail to the pre-change
  harness — the end-to-end demonstration of **SC-004** that the offline loop cannot reproduce
  (see T004 disclosure). Then refresh the agent-context plan pointer if needed (CLAUDE.md
  already points to `specs/038-retype-provider-contracts/plan.md`); no schema-reference or
  provenance change is required — adopt-only, no new artifact (FR-012). Confirm no docs drift.
  > Outcome: the network-gated suite was **not** run — no `FSGG_SDD_ACCEPTANCE_REGISTRY` in this
  > environment, so its 3 composition facts report **Skipped** (the disclosed SC-004 offline limit,
  > T004), structurally backed by the offline T019 resolve-and-bind glue and T020 probe-honors tests.
  > CLAUDE.md already points to this plan; no schema-reference/provenance/docs change is required
  > (adopt-only). No docs drift.

---

## Dependencies & Execution Order

### Phase / story order

- **Phase 1 Setup (T001–T002)** — no dependencies; start immediately. Shared prerequisite for US1/US2.
- **Phase 2 US1 (P1, MVP)** — after Setup. The structural re-type; unblocks US2 and US3.
- **Phase 3 US2 (P2)** — after US1 (extends the canonical mapping in `Config.fs`).
- **Phase 4 US3 (P3)** — after US1 + US2 (probes consume the descriptor's declared commands).
- **Phase 5 Polish** — after all desired stories.

### Cross-task dependencies (beyond plain phase order)

- T005 depends on T003 (FSI before FS).
- T006/T007 depend on T005 (consumers recompile against the new type).
- T013 depends on T005 (extends the same mapping) and is gated red by T010–T012.
- T017/T018 depend on T016 (project reference) and on US2 (descriptor carries commands);
  gated red by the US3 tests — T019 (resolve-and-bind glue, for T018) and T020 (probe honors,
  for T017/T018).
- T024 (SC-004 end-to-end) is network-gated and runs only with `FSGG_SDD_ACCEPTANCE_REGISTRY`;
  it is optional in CI and does not block merge of the offline-verified change.

### Within each story

- Tests first where the story adds them (US2: T010–T012 before T013; US3: T019/T020 before T017/T018).
- `.fsi` before `.fs` (T003 before T005).
- US1's "tests" (T008/T009) are a regression gate + single-definition assertion, run after T007.

### Parallel opportunities

- T001/T002 are sequential (restore follows the edit).
- US2 test tasks T010/T011/T012 are `[P]` (independent assertions); T014 fixture is `[P]`.
- US3 test tasks T019/T020/T021 are `[P]`.
- Polish T022/T023/T024 are `[P]`.
- Stories themselves are **not** parallel here: US2 and US3 edit/consume the same descriptor
  US1 introduces, so run them in priority order P1 → P2 → P3.

---

## Summary

| Story | Priority | Tasks | Count |
|-------|----------|-------|-------|
| Setup | — | T001–T002 | 2 |
| **US1** (one canonical type) | **P1 / MVP** | T003–T009 | 7 |
| US2 (read extended fields) | P2 | T010–T015 | 6 |
| US3 (probes honor declared) | P3 | T016–T021 | 6 |
| Polish & validation | — | T022–T024 | 3 |
| **Total** | | | **24** |

**Suggested MVP scope**: Setup + **User Story 1** (T001–T009) — retires the local
re-encoding with zero regression. US2 then makes the canonical type *useful* on the scaffold
path; US3 delivers the user-facing payoff (provider-honoring probes), which for the only
provider that exists today produces zero observable change.

**Parallel opportunities**: within-story test/fixture fan-out (T010–T012, T019/T020) and the
polish pass (T021–T023). Stories run sequentially by priority because US2/US3 build on the
single descriptor US1 introduces.
