---
description: "Task list for 031-scaffold-lifecycle-passthrough"
---

# Tasks: Scaffold lifecycle-parameter pass-through & app-only provenance

**Input**: Design documents from `/specs/031-scaffold-lifecycle-passthrough/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: This is a **verification-only** feature — the deliverable *is* tests +
fixtures (FR-009). Every implementation task below is a test or fixture task; no
`src/` change is planned (FR-010). If a verification surfaces a genuine defect,
the corrective change stays inside the existing scaffold contract and is called
out in `research.md` before any `src/` edit. **This contingency fired**: T009/T010
surfaced a real forwarding defect (`dotnet new` has no `-p:k=v` passthrough), fixed
within the scaffold contract as the `--k v` wire form — see **research Decision 8**.
No public surface, `.fsi`, schema, projection, or baseline changed.

**Organization**: Phase 2 builds the shared fixture machinery that every story
needs. Phases 3–5 map to the three P1 user stories; Phase 6 covers the FR-008
edges that reuse the variant fixtures. Phases run in sequence; `[P]` tasks within
a phase touch different files and may run in parallel.

## Status legend

- `[ ]` pending · `[X]` done with real evidence · `[-]` skipped (rationale on line)

## MVU/Elmish applicability

No production MVU code is added. Verifications drive the **existing** scaffold MVU
loop through the public surface exactly as `ScaffoldCommandTests.fs` does:
plan-level assertions inspect the real planned `RunProcess` create-arg vector
(not a mocked stage), and end-to-end assertions read the recording fixture's
echoed app file after a real `dotnet new` run (Principle V/VI).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the baseline before adding anything.

- [X] T001 Confirm a clean baseline: run `dotnet test FS.GG.SDD.sln` and record
  it green; capture the four `tests/**/PublicSurface.baseline` snapshots and the
  scaffold golden outputs as the pre-change reference for the SC-007 no-drift
  check (T026). No file change in this task. **Evidence**: baseline green (468
  tests) once the Release CLI is built — the 17 initial "CLI smoke" failures were
  purely environmental (those tests run `dotnet run -c Release --no-build`, so they
  need a prior `dotnet build -c Release`), not real regressions. The four baseline
  snapshots were copied to scratch as the T026 reference.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build the rendering-agnostic recording fixture, its variants, and
the registry files that every user-story scenario consumes.

**⚠️ CRITICAL**: No user-story verification can run until these fixtures exist.

**Neutrality gate (FR-001 / SC-005)**: every file in this phase uses only neutral
identifiers — template short names `fsgg-fixture-lifecycle*`, registry provider
name `fixture`, source via the `__FIXTURE__` absolute-path token. **No** real
FS.GG.Rendering package id, template id, provider name, or docs URL anywhere.
(The leak scan in T020/T021 enforces this on the committed tree.)

- [X] T002 [P] Create the recording template `template.json` at
  `tests/fixtures/scaffold-provider/lifecycle/.template.config/template.json`
  declaring two symbols per `contracts/recording-fixture-provider.md`:
  `productName` (string, `replaces` `PRODUCT_NAME`, default `Product`) and
  `lifecycle` (string, `replaces` `LIFECYCLE_VALUE`, no default). Short name
  `fsgg-fixture-lifecycle`.
- [X] T003 [P] Create the app stubs
  `tests/fixtures/scaffold-provider/lifecycle/App.fsproj` and
  `tests/fixtures/scaffold-provider/lifecycle/Program.fs` (substitute
  `PRODUCT_NAME`), mirroring the existing `ok/` fixture shape.
- [X] T004 [P] Create the recording channel
  `tests/fixtures/scaffold-provider/lifecycle/scaffold-manifest.txt` containing
  at least `lifecycle=LIFECYCLE_VALUE` and `productName=PRODUCT_NAME` so the
  forwarded value can be read back verbatim (backs F1 / FR-002).
- [X] T005 [P] Create `tests/fixtures/scaffold-provider/registries/lifecycle.providers.yml`
  — provider name `fixture`, template `fsgg-fixture-lifecycle` → `lifecycle/` via
  `__FIXTURE__`, `lifecycle` **not** required. Drives US1, US2, determinism,
  value-agnosticism.
- [X] T006 [P] Create `tests/fixtures/scaffold-provider/registries/lifecycle-required.providers.yml`
  — same template, `lifecycle` marked **required** (FR-008 required-but-missing edge).
- [X] T007 [P] Create the empty-product variant: a `lifecycle-empty/` fixture (or
  a new registry pointing at the existing `empty/` fixture if `dotnet new`
  tolerates an undeclared `-p:lifecycle`, per the resolved note in
  `contracts/recording-fixture-provider.md`) plus
  `tests/fixtures/scaffold-provider/registries/lifecycle-empty.providers.yml`
  (`fsgg-fixture-lifecycle-empty`). Decide the reuse-vs-new question here and note
  the choice in the registry comment. **Decision: NEW fixture.** Reuse is
  impossible — `dotnet new` rejects an undeclared `-p:lifecycle` (`'…' is not a
  valid option`, exit 127), proven out-of-band. The `lifecycle-empty/` template
  declares the `lifecycle` symbol and produces no files; noted in the registry
  comment.
- [X] T008 [P] Create the SDD-tree-intrusion variant: a `lifecycle-intrusion/`
  fixture that declares `lifecycle` and writes into `.fsgg/`/`work/`/`readiness/`
  (or a new registry pointing at the existing `writes-into-fsgg/` fixture under
  the same reuse rule as T007) plus
  `tests/fixtures/scaffold-provider/registries/lifecycle-intrusion.providers.yml`
  (`fsgg-fixture-lifecycle-intrusion`). **Decision: NEW fixture** (same
  reuse-impossible reason as T007); declares `lifecycle` and writes `app.txt`,
  `work/leak.txt`, `readiness/leak.txt`.
- [X] T009 Smoke-verify the recording fixture out-of-band: in a throwaway temp
  dir run `dotnet new fsgg-fixture-lifecycle -p:lifecycle=probe -p:productName=X`
  and confirm `scaffold-manifest.txt` reads `lifecycle=probe`. This proves the
  fixture's substitution guarantee (recording-fixture contract behavior #1)
  before any SDD-side assertion depends on it. After T002–T008. **Evidence + defect
  surfaced**: the smoke run revealed `dotnet new` SDK 10 has **no** `-p:k=v`
  passthrough — `-p` is only the auto-alias of the first parameter, so
  `-p:lifecycle=probe` mis-binds and two `-p:` args error (exit 127). The correct
  form `--lifecycle probe --productName X` substitutes correctly. This is the
  forwarding defect corrected per **research Decision 8** (`HandlersScaffold.fs`).

**Checkpoint**: Fixtures + registries exist and substitute correctly — user
stories can begin.

---

## Phase 3: User Story 1 — Lifecycle parameter forwarded verbatim (Priority: P1) 🎯 MVP

**Goal**: Prove SDD forwards `--param lifecycle=sdd` to the provider invocation
verbatim and as an opaque key=value — the forwarded set equals `defaults ⊕
--param` with nothing added, dropped, renamed, or reordered.

**Independent Test**: Run scaffold against the `fixture` provider with `--param
lifecycle=sdd`; assert the recording fixture's `scaffold-manifest.txt` contains
`lifecycle=sdd` and the planned create-arg vector equals the overlay set.

**Reference**: `contracts/forwarding-invariant.md` (F1–F4). All tasks add facts to
`tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs`. Write each to fail
before the assertion exists, then confirm green against current behavior.

- [X] T010 [US1] Verbatim-arrival fact (F1 / FR-002 / US1.1): real run with
  `--param lifecycle=sdd`; assert the produced `scaffold-manifest.txt` contains
  exactly `lifecycle=sdd`. **This fact is what surfaced the forwarding defect**
  (Decision 8): it fails under the old `-p:k=v` form and passes after the
  `--k v` corrective fix. Test: `scaffold forwards lifecycle=sdd … verbatim`.
- [X] T011 [P] [US1] Success-indicator fact (US1.2): same run asserts outcome is
  the success outcome and `ProviderInvoked = true`.
- [X] T012 [P] [US1] Forwarded-set equality fact (F2 / FR-003 / SC-001 / US1.3):
  dry-run/plan-level inspection of the planned `RunProcess` create-arg vector;
  assert the `--key value` set equals `defaults ⊕ author --param` exactly — no
  added, dropped, renamed, or reinterpreted key/value (reuse the MVU surface at
  `ScaffoldCommandTests.fs:74-78`). Wire form is `--k v` per Decision 8 (was
  `-p:k=v` in the draft).
- [X] T013 [P] [US1] Order-independence fact (F3 / FR-008): supply `--param` in
  two different orders → identical create-arg vector.
- [X] T014 [P] [US1] Value-agnosticism fact (F4 / FR-007 / US3.2 companion C4):
  run with an arbitrary nonce `lifecycle=<nonce>`; assert outcome, create-arg
  vector, and provenance shape are identical to the `lifecycle=sdd` run modulo the
  echoed value. (This is the behavioral half of the US3 guard; lives here per
  `contracts/leak-invariant-scan.md` C4.)

**Checkpoint**: US1 forwarding fully verified and independently runnable.

---

## Phase 4: User Story 2 — Provenance records only app-only paths (Priority: P1)

**Goal**: Prove `.fsgg/scaffold-provenance.json` records exactly the provider's
app-only tree (all `generatedProduct`), with no SDD skeleton path, the skeleton
byte-identical to `init`, and deterministic output.

**Independent Test**: Run the `lifecycle=sdd` scaffold; read provenance and assert
producedPaths == the provider's files, all `generatedProduct`, disjoint from the
skeleton.

**Reference**: `contracts/app-only-provenance.md` (P1–P7). Tasks T015–T018 add
facts to `ScaffoldCommandTests.fs`; T019 lands in `ScaffoldParityTests.fs`.

- [X] T015 [US2] App-only precision/recall fact (P1, P2 / FR-004 / SC-002,003 /
  US2.1,2.3): diff the post-run target against the skeleton; assert
  `provenance.producedPaths` equals that app-file set and every entry has
  `owner == "generatedProduct"`.
- [X] T016 [P] [US2] No-skeleton-leak + init byte-identity fact (P3, P4 / FR-005 /
  SC-002 / US2.2): assert `producedPaths ∩ skeleton == ∅`, and that each skeleton
  file a `lifecycle=sdd` scaffold writes is byte-identical to the same file from a
  standalone `init` run into a sibling temp dir.
- [X] T017 [P] [US2] Determinism fact (P5, P6 / FR-006 / SC-004): two
  `lifecycle=sdd` runs into clean targets → byte-identical provenance files and
  byte-identical `--json` report; assert provenance has sorted paths and no clock /
  no absolute path.
- [X] T018 [P] [US2] Refresh-exclusion fact (P7 / 030-FR-007, re-asserted; not
  this feature's FR-007 leak scan):
  after a `lifecycle=sdd` scaffold, run `refresh` and assert the app-only produced
  paths are absent from the refreshed/blocked view ids (externally owned).
- [X] T019 [P] [US2] Three-projection parity fact (FR-006 / US2.4) in
  `tests/FS.GG.SDD.Cli.Tests/ScaffoldParityTests.fs`: JSON, text, and rich
  projections present the same app-only produced-path facts for a `lifecycle=sdd`
  run (rich adds/drops no facts).

**Checkpoint**: US2 provenance invariants fully verified.

---

## Phase 5: User Story 3 — No rendering knowledge leaks into generic SDD (Priority: P1)

**Goal**: Enforce on every build that no rendering-specific identifier and no
`lifecycle`-value special-casing exists in generic SDD source, and prove the scan
actually bites a planted violation.

**Independent Test**: Run the guard against the shipped tree (clean) and against a
synthetic source string carrying a planted identifier and a planted `lifecycle`
literal (caught + located).

**Reference**: `contracts/leak-invariant-scan.md` (C1–C3; C4 done in T014). All
tasks extend `tests/FS.GG.SDD.Commands.Tests/ScaffoldGuardTests.fs`.

- [X] T020 [US3] Identifier deny-list scan (C1 / FR-007 / US3.1): extend the
  existing deny-list scan (`ScaffoldGuardTests.fs:12-57`) to cover
  `src/**/*.{fs,fsi}` and the three generic-contract test files across their
  projects — `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs`,
  `tests/FS.GG.SDD.Cli.Tests/ScaffoldParityTests.fs`, and
  `tests/FS.GG.SDD.Artifacts.Tests/ScaffoldProvenanceTests.fs`;
  case-insensitive; fails with `"{path}: {token}"`. Assert clean on the shipped tree.
- [X] T021 [P] [US3] Scoped lifecycle-value scan (C2 / FR-007 / US3.2): assert the
  collision-free lifecycle-**value** token `spec-kit` does **not** appear in the
  curated scaffold-source **union** only —
  `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs` plus
  `src/FS.GG.SDD.Commands/CommandSerialization.fs` /
  `src/FS.GG.SDD.Commands/CommandRendering.fs` /
  `src/FS.GG.SDD.Commands/CommandReports.fs` /
  `src/FS.GG.SDD.Cli/Rendering.fs`. The rich renderer (`Cli/Rendering.fs`) is
  **included** so a lifecycle special-case planted in any scaffold projection path
  is caught. Scope is the curated file list, **not** the repo. **Refined per
  research Decision 9**: the literal `lifecycle` is generic SDD vocabulary even in
  the scaffold source (`nextLifecycleEffects`, the "begin the lifecycle at charter"
  hint, `lifecycleStageReadiness`), so a literal-token scan false-positives on the
  clean tree; the only collision-free value token is `spec-kit` (`"sdd"`/`"none"`
  collide with `Ownership = "sdd"` / `None`-rendering). The comprehensive "no
  branching on any value" guarantee is the behavioral C4 (T014). Assert clean.
- [X] T022 [P] [US3] Planted-violation proof (C3 / FR-007 / SC-005 / US3.3): a
  unit fact that the offender-detector returns a non-empty, **located** list for a
  synthetic in-memory source string containing (a) a planted rendering identifier
  and (b) a planted `spec-kit` lifecycle-value literal (Decision 9) — i.e. the scan
  would not silently miss a real violation. The **same** detector guards the tree
  (C1/C2 use it), and a manual plant of `spec-kit` into real `HandlersScaffold.fs`
  source was confirmed to fail the C2 fact and name the file (quickstart §4).

**Checkpoint**: US3 leak guard enforced and self-proving.

---

## Phase 6: FR-008 edge cases (cross-cutting, reuse variant fixtures)

**Purpose**: Exercise the existing diagnostics/outcomes under `lifecycle=sdd`
using the T006–T008 variant fixtures. Each asserts behavior that already exists;
none changes `src/`. All land in `ScaffoldCommandTests.fs`.

- [X] T023 [P] Required-but-missing edge (FR-008 / SC-006): with
  `lifecycle-required.providers.yml`, omit `lifecycle`; assert SDD blocks with
  `scaffold.providerParamMissing` at exit 1 **before** provider invocation and
  writes **no** provenance. Depends on T006.
- [X] T024 [P] Empty-product edge (FR-008 / SC-006): with
  `lifecycle-empty.providers.yml` and `--param lifecycle=sdd`, assert the
  empty-success outcome (`ProviderSucceededEmpty`/`providerEmpty`), exit 0, and
  `producedPaths = []`. Depends on T007.
- [X] T025 [P] SDD-tree-intrusion edge (FR-008 / SC-006): with
  `lifecycle-intrusion.providers.yml` and `--param lifecycle=sdd`, assert the
  provider-defect outcome `scaffold.providerWroteSddTree` at exit 2, the scaffold
  reported incomplete, and the intruded SDD paths are **never** laundered into
  provenance as app-only. Depends on T008.

**Checkpoint**: All four edge cases verified under `lifecycle=sdd`.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T026 No-public-surface-drift check (FR-010 / SC-007): confirm the four
  `PublicSurface.baseline` snapshots and all scaffold golden outputs are unchanged
  vs the T001 reference; `git status --porcelain` shows changes only under
  `specs/031-*`, `tests/fixtures/scaffold-provider/lifecycle*`, and the three
  scaffold test modules. **Evidence**: all four baselines byte-identical to the
  T001 reference (diff clean). `git status` shows exactly the planned surfaces
  **plus** the two documented exceptions: the one Decision-8 forwarding fix in
  `HandlersScaffold.fs` (no public-surface/`.fsi`/schema/projection change — the
  baselines prove it) and the routine 030→031 feature pointer
  (`.specify/feature.json`, `CLAUDE.md`). FR-010's "no public-surface change" holds.
- [X] T027 Run the full suite green: `dotnet test FS.GG.SDD.sln`; confirm
  `WarningsAsErrors` ratchet stays at 0 and no `#nowarn` was introduced.
  **Evidence**: full suite green — Artifacts/Validation/Cli (55)/Commands (300),
  0 failed. The only build warnings are the 2 pre-existing `FS3262` in
  `HandlersScaffold.fs` (lines 41/63, untouched by this feature); no new warning,
  no `#nowarn`.
- [X] T028 Execute `quickstart.md` end-to-end (full suite, targeted filter, and
  the manual planted-violation sanity check in §4) and confirm each documented
  expectation holds; fix any drift between the guide and reality. **Evidence**:
  full suite + `~Scaffold` filter green; manual `spec-kit` plant into real
  `HandlersScaffold.fs` source failed the C2 fact and named the file, then reverted.
  **Drift fixed** per Decisions 8 & 9: §1/§3 "lifecycle-literal scan" → "lifecycle-
  value (`spec-kit`) scan", §3 "`-p:` vector" → "`--key value` vector", and §5
  git-status note now accounts for the Decision-8 src fix and the feature pointer.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: after Phase 1 — **blocks all user stories** (every
  scenario consumes a fixture/registry).
- **User Stories (Phases 3–5)**: after Phase 2; independent of each other and may
  proceed in parallel (different test modules — `ScaffoldCommandTests.fs` for
  US1/US2, `ScaffoldGuardTests.fs` for US3, `ScaffoldParityTests.fs` for T019).
- **Edge cases (Phase 6)**: after Phase 2 (need T006–T008); independent of the
  story phases.
- **Polish (Phase 7)**: after all desired verification phases complete.

### Cross-task dependencies (beyond phase ordering)

- T009 after T002–T008 (smoke-checks the assembled fixture).
- T014 (value-agnosticism) is the behavioral companion to US3 but lives in US1's
  module; it depends only on Phase 2.
- T023→T006, T024→T007, T025→T008.
- T026 compares against the T001 reference snapshot.

### Parallel opportunities

- Phase 2: T002–T008 are all `[P]` (distinct files); T009 gates on them.
- Phase 3: T011–T014 `[P]` after T010 establishes the run harness.
- Phase 4: T016–T019 `[P]` after T015.
- Phase 5: T021–T022 `[P]` after T020.
- Phase 6: T023–T025 all `[P]`.
- Across phases: once Phase 2 lands, US1, US2, US3, and the edges can be staffed
  concurrently.

---

## Implementation Strategy

### MVP (User Story 1)

1. Phase 1 (baseline) → Phase 2 (fixtures, CRITICAL) → Phase 3 (US1 forwarding).
2. **STOP and VALIDATE**: forwarding proven verbatim/opaque end-to-end and at the
   plan level — the core board claim.

### Incremental delivery

Phase 2 → US1 (MVP, forwarding) → US2 (provenance) → US3 (leak guard) → Phase 6
(edges) → Phase 7 (no-drift + quickstart). Each story is independently runnable
via `dotnet test … --filter "FullyQualifiedName~Scaffold"`.

---

## Notes

- `[P]` = different files, no dependency on another incomplete task in the phase.
- TDD discipline (constitution Principle I/VI): author each verification to **fail
  before it exists**, then confirm it passes against current behavior. Never mark a
  failing task `[X]`; never weaken an assertion to green a build.
- If any verification surfaces a genuine defect, **stop**: record it in
  `research.md`, make the corrective change within the existing scaffold contract,
  and update the affected `.fsi`/baseline/golden as a Tier 1 follow-through
  (none anticipated — the behavior already exists).
- Real fixtures only (FR-009): no mocks of internal stages; disclose any
  unavoidable synthetic stand-in in the test/fixture name.
