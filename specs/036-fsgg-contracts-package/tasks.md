---
description: "Task list for FS.GG.Contracts package — shared schema, provider & registry contracts"
---

# Tasks: FS.GG.Contracts Package — Shared Schema, Provider & Registry Contracts

**Input**: Design documents from `/specs/036-fsgg-contracts-package/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md, contracts/

**Tests**: Included — Principle VI (Test Evidence Is Mandatory) is constitutional here,
and plan.md/quickstart.md define explicit test tasks and success-criteria scenarios.

**Tier**: Tier 1 (contracted change) per plan.md Constitution Check. Spec, plan, tasks,
`.fsi`, tests, docs, and the cross-repo registry registration are all required.

**Organization**: Tasks are grouped by user story. Phases run in sequence; tasks within
a phase marked `[P]` may run in parallel. `.fsi` is authored and compiled before the
paired `.fs` (Principle I, repo compile-ordering convention).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on another incomplete task in this phase)
- **[Story]**: US1 / US2 / US3, or none for shared setup/foundational/polish
- Tier (`[T1]`/`[T2]`) omitted — all tasks match the spec's overall Tier 1

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the new leaf library and test project skeletons and wire them into
the solution. No SDD project may reference the new package (FR-001/010).

- [X] T001 Create `src/FS.GG.Contracts/FS.GG.Contracts.fsproj` targeting `net10.0`, `IsPackable=true`, `PackageId=FS.GG.Contracts`, `Version=1.0.0`, with **no** `ProjectReference` and a single `PackageReference Include="FSharp.Core"` (BCL-only, FR-001/002/003/011). Mirror conventions from `src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj` (central version/package management; no `RootNamespace`/`AssemblyName` override beyond `PackageId`). Leave `<Compile>` ordering placeholders for the modules added in later phases.
- [X] T002 Create `tests/FS.GG.Contracts.Tests/FS.GG.Contracts.Tests.fsproj` — xunit (2.9.3, central), `IsPackable=false`, single `ProjectReference` to `src/FS.GG.Contracts/FS.GG.Contracts.fsproj`. Mirror an existing SDD test fsproj for xunit wiring.
- [X] T003 Add both new projects to `FS.GG.SDD.sln` (`dotnet sln FS.GG.SDD.sln add ...`). Confirm `dotnet restore FS.GG.SDD.sln` succeeds and the solution still builds with no other project modified.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The self-describing contract version is the lowest-layer member every other
module and consumer references; it compiles first and is shared across all stories.

**⚠️ CRITICAL**: User-story phases (3–5) depend on this phase. No US work begins until complete.

- [X] T004 Author `src/FS.GG.Contracts/ContractVersion.fsi` defining module `Fsgg.ContractVersion` with `val value: string`, `val major/minor/patch: int` (per `contracts/contract-version.fsi`, FR-012). Add it as the first `<Compile>` entry in the fsproj.
- [X] T005 Implement `src/FS.GG.Contracts/ContractVersion.fs`: `value = "1.0.0"`, `major = 1`, `minor = 0`, `patch = 0`. Confirm `dotnet build src/FS.GG.Contracts` is clean.

**Checkpoint**: Library compiles with its version self-report. User stories can now proceed (in parallel if staffed — they touch disjoint module files).

---

## Phase 3: User Story 1 - One typed source of truth for every `.fsgg` schema and its version (Priority: P1) 🎯 MVP

**Goal**: A single `Fsgg.Schemas` module names every `.fsgg` schema with a typed record
and a named version constant; each SDD-owned constant equals the value SDD emits today.

**Independent Test**: Build the package in isolation, enumerate `Fsgg.Schemas.entries`,
confirm all 10 named schemas are present each with a record and version constant, and
that each SDD-owned constant equals today's emitted value.

### Tests for User Story 1 ⚠️ (write first; ensure they FAIL before T009/T010)

- [X] T006 [P] [US1] In `tests/FS.GG.Contracts.Tests/SchemaVersionConstantTests.fs`: assert `Fsgg.Schemas.entries` has exactly 10 members and that the set of `Name`s equals { providers, project, sdd, agents, scaffold-provenance, governance-handoff, governance, policy, capabilities, tooling } (SC-001, quickstart Scenario B).
- [X] T007 [P] [US1] In `SchemaVersionConstantTests.fs`: assert each **SDD-owned** version constant equals today's emitted value — `providersVersion=projectVersion=sddVersion=agentsVersion=scaffoldProvenanceVersion=governanceHandoffVersion=1`, `governanceHandoffContractVersion="1.0.0"` — grounding each against its in-repo source (`src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fs`, `GovernanceHandoff.fs`, the `LifecycleArtifacts/Config` parsers), citing the source line in a code comment per assertion (FR-005, SC-002). For the **Governance-owned** constants (`governance/policy/capabilities/toolingVersion=1`), assert the value the package *declares to the Governance published reference* — label these tests explicitly as declared-reference values, **not** SDD-emitted (spec Assumptions; data-model "Governance-owned" provenance). Do not assert them against any SDD output.

### Implementation for User Story 1

- [X] T008 [US1] Author `src/FS.GG.Contracts/Schemas.fsi` (per `contracts/schemas.fsi`): module `Fsgg.Schemas` with `SchemaOwner` DU (`Sdd | Governance`), `SchemaContractEntry` record, the 11 named version `val`s, `val entries: SchemaContractEntry list`, and the 10 typed schema record type names (`ProvidersSchema`, `ProjectSchema`, `SddSchema`, `AgentsSchema`, `ScaffoldProvenanceSchema`, `GovernanceHandoffSchema`, `GovernanceSchema`, `PolicySchema`, `CapabilitiesSchema`, `ToolingSchema`). Add `.fsi` then `.fs` to the fsproj after `ContractVersion`.
- [X] T009 [US1] Implement `src/FS.GG.Contracts/Schemas.fs` using BCL/`FSharp.Core` types only (FR-002/014 — generic shapes only, no provider-/rendering-/Governance-runtime identity). For the **6 SDD-owned** schemas, define each record's full field set mirroring the corresponding `src/FS.GG.SDD.Artifacts` records (project/sdd/agents/providers configs, `ScaffoldProvenanceRecord`, `GovernanceHandoff`). For the **4 Governance-owned** schemas (`governance`, `policy`, `capabilities`, `tooling`), define a minimal, explicitly-provisional record sourced from the Governance published reference (a documented placeholder, **not** an invented field set), each marked with a `// SOURCE: Governance published reference (TBD-link)` comment per data-model "Governance-owned" provenance — the full field set is deferred to the Governance counterpart item. Define every version constant and the `entries` list (one `SchemaContractEntry` per schema with `Owner` set correctly). Make T006/T007 pass (after T008).

**Checkpoint**: US1 fully functional — every schema represented, constants verified against today's values. This is the MVP.

---

## Phase 4: User Story 2 - Extended provider descriptor with declared build/test/run/verify and a canonical name (Priority: P2)

**Goal**: A `Fsgg.Provider` module exposes the extended descriptor — the five preserved
SDD fields plus optional `Build`/`Test`/`Run`/`Verify` commands and a `NameParameter`
defaulting to `name` — with helpers for name resolution and malformed-command detection.

**Independent Test**: A descriptor with no command fields resolves to today's defaults
and `resolveNameParameter = "name"`; a descriptor declaring commands and a non-default
name exposes each as authored; a blank `Executable` is reported malformed.

### Tests for User Story 2 ⚠️ (write first; ensure they FAIL before T012/T013)

- [X] T010 [P] [US2] In `tests/FS.GG.Contracts.Tests/ProviderDescriptorTests.fs`: assert a descriptor with `Build=Test=Run=Verify=None` exposes them absent (SC-003); assert a descriptor declaring each `DeclaredCommand` exposes `Executable`/`Arguments` exactly as authored; assert the five preserved fields match SDD's current `ProviderDescriptor` shape (FR-006 Scenario 4, quickstart Scenario D).
- [X] T011 [P] [US2] In `ProviderDescriptorTests.fs`: assert `Provider.defaultNameParameter = "name"`, `Provider.resolveNameParameter` returns `"name"` for an absent/blank declaration and the authored value otherwise (FR-007, Scenarios 3), and `Provider.isMalformed` is `true` for a `DeclaredCommand` with blank/whitespace `Executable` and `false` otherwise (Edge Case, Principle VIII).

### Implementation for User Story 2

- [X] T012 [US2] Author `src/FS.GG.Contracts/Provider.fsi` (per `contracts/provider.fsi`): module `Fsgg.Provider` with `DeclaredCommand`, `ProviderParameterSpec` (preserved unchanged), the extended `ProviderDescriptor` record (five preserved fields first, then the four `option` commands and `NameParameter`), and `val defaultNameParameter`, `val resolveNameParameter`, `val isMalformed`. Add `.fsi`/`.fs` to the fsproj.
- [X] T013 [US2] Implement `src/FS.GG.Contracts/Provider.fs`: `defaultNameParameter = "name"`; `resolveNameParameter` falls back to it when `NameParameter` is blank/whitespace; `isMalformed` is whitespace check on `Executable`. Records are additive over SDD's current descriptor (preserved fields byte-identical shape, FR-006). Make T010/T011 pass.

**Checkpoint**: US1 and US2 both work independently.

---

## Phase 5: User Story 3 - Typed, validated cross-repo dependency registry (Priority: P3)

**Goal**: A `Fsgg.Registry` module models `registry/dependencies.yml` as typed records and
exposes a pure `validate` that returns diagnostics naming the offending entry and the
violated coherence/completeness rule.

**Independent Test**: A coherent model validates with no diagnostics; an incoherent model
(range excludes referenced version) and an incomplete model (missing required field) each
return a diagnostic naming the entry and the rule.

### Tests for User Story 3 ⚠️ (write first; ensure they FAIL before T015/T016)

- [X] T014 [P] [US3] In `tests/FS.GG.Contracts.Tests/RegistryValidatorTests.fs`: assert a coherent `RegistryModel` ⇒ `Valid`; an incoherent model (edge `CompatibleRange` excludes the provider's declared `Version`) ⇒ `Invalid [ IncompatibleVersion ... ]`; an incomplete entry (missing required field) ⇒ `Invalid [ MissingField "<field>" ... ]`; an edge to an absent component ⇒ `Invalid [ UnknownComponent ... ]`; a non-SemVer version/range ⇒ `Invalid [ MalformedVersion ... ]`. Assert each diagnostic's `Entry` names the offending component/edge (FR-009, SC-007, quickstart Scenario E).

### Implementation for User Story 3

- [X] T015 [US3] Author `src/FS.GG.Contracts/Registry.fsi` (per `contracts/registry.fsi`): module `Fsgg.Registry` with `RegistryComponent`, `DependencyEdge`, `RegistryModel`, `RegistryRule` DU (`MissingField of string | UnknownComponent | IncompatibleVersion | MalformedVersion`), `RegistryDiagnostic`, `ValidationResult` DU (`Valid | Invalid of RegistryDiagnostic list`), and `val validate: RegistryModel -> ValidationResult`. Add `.fsi`/`.fs` to the fsproj.
- [X] T016 [US3] Implement `src/FS.GG.Contracts/Registry.fs`: a small BCL-only SemVer parse/compare/range helper (no third-party package, research R5) plus the pure `validate` evaluating the four rule families (non-blank `Id`/`Version` + parseable version; non-blank edge fields; edge endpoints exist in `Components`; `CompatibleRange` includes the provider's declared `Version`). Each diagnostic names the entry, rule, and a human-surfacable `Message` (FR-008/009). Make T014 pass.

**Checkpoint**: All three user stories independently functional.

---

## Phase 6: Cross-Cutting Verification, Surface Baseline & Packaging

**Purpose**: Prove the package-wide guarantees that span all stories — BCL-only closure,
public-surface baseline, local-feed pack/consume, and the SDD additive guarantee.

- [X] T017 [P] In `tests/FS.GG.Contracts.Tests/DependencyClosureTests.fs`: assert the package dependency closure contains only `FSharp.Core` (allowlist = `{FSharp.Core}`) — no `YamlDotNet`, `System.Text.Json`, `Spectre.Console`, Governance, or rendering packages (SC-004, FR-002, quickstart Scenario A). Read the generated `.deps.json` / inspect the resolved closure.
- [X] T018 Generate `tests/FS.GG.Contracts.Tests/PublicSurface.baseline` capturing the package's exported surface and assert against it (Principle III, quickstart Scenario I). Establish the initial baseline as the deliberate first capture.
- [X] T019 Self-describing contract version test in `FS.GG.Contracts.Tests`: assert `Fsgg.ContractVersion.value = "1.0.0"`, `major=1`, `minor=0`, `patch=0` (FR-012, quickstart Scenario F).
- [X] T020 Pack to a local folder feed: `dotnet pack src/FS.GG.Contracts/FS.GG.Contracts.fsproj -c Release -o ./artifacts/local-feed`. Confirm `FS.GG.Contracts.1.0.0.nupkg` is produced; from a **throwaway probe project created outside the repo work tree** (e.g. under the session scratch dir, not committed) add the folder as a NuGet source, restore, and resolve `Fsgg.Schemas`/`Fsgg.Provider`/`Fsgg.Registry` (SC-005, FR-011, quickstart Scenario G). Ensure `artifacts/` is git-ignored (add it to `.gitignore` if absent) so the feed output never appears in the working tree; the probe project lives outside the repo so it cannot contradict T021.
- [X] T021 Additive-guarantee regression: run `dotnet test FS.GG.SDD.sln` and confirm the entire existing SDD suite passes unchanged, no file under `src/FS.GG.SDD.*` is modified, no existing golden/baseline changes, and SDD adds **no** reference to `FS.GG.Contracts` (FR-010, SC-006, quickstart Scenario H). `git status` (excluding git-ignored build outputs such as `bin/`/`obj/`/`artifacts/`/`*.nupkg`) must show only new `src/FS.GG.Contracts`, `tests/FS.GG.Contracts.Tests`, the `.gitignore` `artifacts/` addition, the constitution/spec/plan/data-model edits for this feature, and spec files — never an edit to an existing `src/FS.GG.SDD.*` source or fixture.

**Checkpoint**: Package-wide guarantees verified; distributable artifact produced.

---

## Phase 7: Cross-Repo Registry Registration & Docs (Contract-Change)

**Purpose**: This is a contract-change; register the new package surface in the cross-repo
dependency registry per the coordination protocol / ADR-0001 (FR-013, SC-008).

- [X] T022 File a cross-repo PR against `FS-GG/.github` updating `registry/dependencies.yml` and `docs/registry/compatibility.md` to record the `FS.GG.Contracts` 1.0.0 surface, linking FS-GG/FS.GG.SDD#8 as the tracking reference (quickstart Scenario J). Use the cross-repo-coordination skill/protocol; this is a separate repo PR, not a local build artifact.
- [X] T023 [P] Run the quickstart validation end-to-end (Scenarios A–J) and record results; reconcile any drift between quickstart and the implemented surface.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories.
- **User Stories (Phases 3–5)**: Depend on Foundational. They touch disjoint module files
  (`Schemas.*`, `Provider.*`, `Registry.*`) and disjoint test files, so they may run in
  parallel; or sequentially in priority order P1 → P2 → P3.
- **Verification/Packaging (Phase 6)**: Depends on all user stories whose surface it
  asserts (T017/T018/T020 need all three modules; T021 is independent of module content
  but should run last to confirm the additive guarantee).
- **Registry/Docs (Phase 7)**: Depends on the package surface being final (Phase 6).

### Within Each User Story

- Tests written first and FAIL before implementation (Principle VI).
- `.fsi` authored and added to the fsproj before the paired `.fs` (Principle I/III).
- Story complete and independently testable before moving to the next priority.

### Parallel Opportunities

- Phase 1: T001 and T002 are largely independent (different fsproj files); T003 depends on both.
- Phase 3/4/5 are mutually parallel after Phase 2 — each owns its own `.fsi/.fs` + test file.
- Within a story, the two test tasks marked `[P]` (e.g. T006/T007, T010/T011) edit the same
  test file only when noted; where they share a file, serialize them.
- Phase 6: T017 is `[P]`; T018/T019/T020 can overlap; T021 runs last.

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1: Setup → 2. Phase 2: Foundational → 3. Phase 3: US1 →
4. **STOP and VALIDATE**: enumerate `Fsgg.Schemas.entries`, confirm 10 schemas + constants
   against today's emitted values. This alone is the coherence backbone the item exists for.

### Incremental Delivery

1. Setup + Foundational → library compiles with version self-report.
2. US1 → schemas + constants (MVP, the H3/H4 unblocker).
3. US2 → extended provider descriptor (unblocks the #9 probe re-type).
4. US3 → registry types + validator (the H3 coherence-workflow input).
5. Phase 6 → BCL-only/baseline/pack guarantees. 6. Phase 7 → cross-repo registration.

---

## Notes

- **Elmish/MVU (Principle V)**: N/A — the package is pure records, DUs, module `let`
  constants, and one pure validator. The plan documents this exemption ("simple pure
  parsers, data models, and validators"); no MVU `Model`/`Msg`/`Effect`/interpreter tasks.
- **BCL-only (FR-002/SC-004)**: the package owns typed models + a pure validator only;
  YAML/JSON (de)serialization stays at each consumer's edge. The only allowed closure
  member is `FSharp.Core` (documented Complexity-Tracking deviation).
- **No external identity (FR-014)**: never embed a concrete provider/rendering/Governance
  package id, template id, path, command, or docs URL — generic contract shapes only.
- **Additive (FR-010)**: no existing `src/FS.GG.SDD.*` file is modified; SDD does not
  reference the package in this item (the re-type is FS-GG/FS.GG.SDD#9).
- **Principle I (step 3)**: each story authors its `.fsi` (T008/T012/T015), then writes
  tests through that public surface *before* the `.fs` hardens (T006/T007, T010/T011, T014) —
  the test-first tasks are the "exercise the public API before implementation" step; an
  ad-hoc FSI/prelude smoke against the `.fsi` is encouraged but optional.
- Never mark a failing task `[X]`; never weaken an assertion to green a build.
