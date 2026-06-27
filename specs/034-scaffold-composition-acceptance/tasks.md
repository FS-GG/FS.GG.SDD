# Tasks: Scaffold Composition Acceptance (real rendering provider)

**Input**: Design documents from `specs/034-scaffold-composition-acceptance/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tier**: Tier 1 (contracted change) — a new schema-versioned `composition-acceptance-result`
v1 document and a new opt-in test surface. **Zero** lifecycle-artifact, provider, provenance,
`validate`, or `release-readiness.json` change; **zero** `.fsi`/`PublicSurface.baseline` edit.

> **This feature is verification.** Unlike a typical Spec Kit feature where tests are optional,
> the deliverable *is* an opt-in, network-gated xUnit acceptance plus one result contract. The
> "real composition path" (scaffold MVU, provider wrapper, provenance partition, refresh
> exclusion, post-instantiation git+chmod) already exists and is proven against the fixture
> provider; these tasks add the harness that drives the **real** provider and asserts the facts.
> No production `update`/effect/`.fsi` is added (Principle V: the build/run/refresh probes live
> at the **test edge**, never inside a lifecycle `update`).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase (different file)
- **[Story]**: US1 / US2 / US3 (or none for setup/foundational/polish)
- Phases run in sequence; tasks within a phase marked `[P]` may run in parallel
- All work concentrates in `tests/FS.GG.SDD.Acceptance.Tests/` plus a few extend-in-place files

---

## Phase 1: Setup (new opt-in test project)

**Purpose**: Stand up the isolated, network-gated acceptance project so it builds offline.

- [X] T001 Create `tests/FS.GG.SDD.Acceptance.Tests/FS.GG.SDD.Acceptance.Tests.fsproj` targeting
  `net10.0`, referencing `src/FS.GG.SDD.Commands` and `src/FS.GG.SDD.Artifacts` (to drive the
  MVU loop and parse provenance), with the pinned `xUnit 2.9.3` + `Microsoft.NET.Test.Sdk`
  `17.14.1` package refs (from `Directory.Packages.props`). `WarningsAsErrors` stays at 0; no
  `#nowarn`. Files included in compile order: `AcceptanceSupport.fs`, `CompositionResult.fs`,
  `CompositionAcceptanceTests.fs`.
- [X] T002 Add the new project to `FS.GG.SDD.sln` under the `tests` solution folder
  (`{0AB3BF05-...}`), alongside the existing four test projects, so `dotnet test FS.GG.SDD.sln`
  builds it. Confirm `dotnet build FS.GG.SDD.sln` succeeds offline.

**Checkpoint**: The empty acceptance project compiles in the solution with no network.

---

## Phase 2: Foundational (shared harness + result contract)

**Purpose**: The env-gating, the dotnet/process shell, the temp-dir + registry-copy helpers, and
the deterministic result-document serializer — every user-story fact depends on these.

**⚠️ CRITICAL**: No user-story acceptance fact can be written until this phase is complete.

- [X] T003 [P] Implement env gating + run scaffolding in
  `tests/FS.GG.SDD.Acceptance.Tests/AcceptanceSupport.fs`: read `FSGG_SDD_ACCEPTANCE_REGISTRY`;
  expose a guard that calls `Assert.Skip` when it is unset/empty (so the offline inner loop is
  green and honest, contracts/acceptance-protocol.md §Gating); create a fresh empty temp product
  root; copy the registry file into `<root>/.fsgg/providers.yml`. No rendering identifier appears
  anywhere in this file (FR-009).
- [X] T004 [P] Add the in-process driver to `AcceptanceSupport.fs` mirroring
  `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs` (`request`/`runRequest`/`interpretAll`): build a
  `Scaffold` request with `Provider = Some "rendering"` and `Parameters = ["lifecycle","sdd"]`,
  run the `init`→…→`Scaffold` MVU loop over the real provider, and return the `--json`
  `CommandReport` for outcome reading. (Depends on T003 — same file; sequence after.)
- [X] T005 [P] Add the `dotnet`/`git` process-shell helpers to `AcceptanceSupport.fs` used by the
  build/run/refresh probes at the test edge: `dotnet build` (**300 s** timeout), a **headless,
  bounded** run smoke (no display server — a `--help`/`--version`-style probe, else a headless
  launch that must survive a **10 s grace window** without a non-zero exit; **60 s** overall
  timeout, so a hung app fails rather than hangs — research D6, contracts/acceptance-protocol.md
  §run probe), and a refresh invocation; each captures exit code + surfaced diagnostic for
  `failure.diagnostic`. (Same file; sequence after T003/T004.)
- [X] T006 [P] Implement the result document in
  `tests/FS.GG.SDD.Acceptance.Tests/CompositionResult.fs`: the
  `composition-acceptance-result` v1 record (`schemaVersion`, `generator` with a **hard-coded**
  `version = "1.0.0"` constant — never derived from build/date/random, FR-011/SC-005, finding F8 —,
  `verdict`, `inputs`, `scaffoldOutcome`, `scaffoldDiagnostic`, `facts.*` 9 bools, `failure`,
  `sensed`) and a deterministic serializer with stable key order, no timestamps/randomness in the
  body, UTF-8 — the same `Utf8JsonWriter` style the repo uses for `scaffold-provenance.json`
  (contracts/composition-acceptance-result.md).
- [X] T007 [P] In `CompositionResult.fs`, implement the `sensed`-block normalization (null out
  `resolvedTemplateVersion`/`providerAvailable`/`host`/`timestamp` before any byte-comparison),
  mirroring the `ValidationContracts.fs` INV-5 pattern (research D8, FR-011/SC-005). (Same file;
  sequence after T006.)
- [X] T008 [P] In `CompositionResult.fs`, implement the `Verdict = Pass | Fail of FailReason |
  SkipUnavailable` DU and the verdict-resolution function keyed on the **`(scaffoldOutcome,
  diagnostic code)` pair** (data-model.md state-transition table; contracts/acceptance-protocol.md
  mapping — finding F1): `providerSucceeded`+all-facts→`pass`; `providerSucceededEmpty`
  (`scaffold.providerEmpty`)→`fail`(incomplete); **`providerFailed`+`scaffold.providerUnavailable`
  →`skip-unavailable`**; `providerFailed`+`scaffold.providerWroteSddTree`/`scaffold.providerFailed`
  →`fail`(defect); `providerNotRun`→`fail`(config error). The outcome alone is insufficient —
  `providerFailed` covers both unavailable (SKIP) and defect (FAIL), so resolving on the outcome
  without the diagnostic would collapse SKIP into FAIL and break SC-004. `failure` carries the
  first failing fact + diagnostic; the driving code is recorded in `scaffoldDiagnostic`. Add a
  **purely offline unit test** (no env, no network — finding F3) that drives this function with
  synthetic `(outcome, diagnostic)` pairs and asserts each branch: unavailable→skip,
  wrote-SDD-tree→fail(defect), non-zero-exit→fail(defect), `providerNotRun`→fail(config),
  empty→fail(incomplete), success+all-facts→pass. (Same file; sequence after T006.)
- [X] T009 Add a shape/golden test for the result schema in
  `CompositionAcceptanceTests.fs` (or a sibling `CompositionResultTests.fs`): assert a
  fully-populated result serializes to the byte-exact expected JSON with the `sensed` block
  normalized to null, and assert two synthetic same-input bodies are byte-identical (SC-005).
  This baselines the new contract (Principle III: the new contract is the result schema, fixed by
  its own golden test, not a signature). Runs **offline** — no env, no network.

**Checkpoint**: The harness can drive the real provider and serialize a deterministic verdict;
the result schema is baselined offline.

---

## Phase 3: User Story 1 — Prove the real composition path is coherent end to end (Priority: P1) 🎯 MVP

**Goal**: One green end-to-end acceptance against the real provider: a single invocation yields
the runnable app **and** the SDD skeleton + authored constitution, the app builds and runs, the
post-instantiation git+chmod steps ran, and the run is reported complete only if every part
succeeded.

**Independent Test**: With `FSGG_SDD_ACCEPTANCE_REGISTRY` pointed at the real rendering registry,
`dotnet test FS.GG.SDD.sln --filter kind=composition-acceptance` yields verdict **pass** and a
result document with all P1 facts true.

> All US1 facts live in `tests/FS.GG.SDD.Acceptance.Tests/CompositionAcceptanceTests.fs`, tagged
> `[<Trait("kind","composition-acceptance")>]`, each self-skipping via the T003 guard when the
> registry env is unset. They share one file → write sequentially (not `[P]`), all after Phase 2.

- [X] T010 [US1] Add the orchestrating acceptance fact in `CompositionAcceptanceTests.fs`: from an
  empty dir, run the driver (T004), read the `--json` `outcome` **and its diagnostic code**, branch
  via the verdict-resolution function (T008), and on a success outcome emit the result document
  (default `<root>/composition-acceptance.json`, overridable via `FSGG_SDD_ACCEPTANCE_RESULT_PATH`
  — finding F7) — failing the test iff `Fail`, skipping iff `SkipUnavailable`. This is the spine
  every fact hangs off (Acceptance Scenario 1).
- [X] T011 [US1] Assert `skeletonPresent` + `constitutionPresent` (FR-002): the reused `init`
  effects exist in the product and `.fsgg/constitution.md` is present (it is **not** provider
  output). Acceptance Scenario 1.
- [X] T012 [US1] Assert `appBuilds` + `appRuns` (FR-003): `dotnet build` (300 s) over the produced
  app succeeds and the **headless** run smoke (T005 — `--help`/`--version` probe or a headless
  launch surviving the 10 s grace window; no display server, so it is CI-reproducible) starts the
  app without crashing — distinguishing "files produced" from "working product". Acceptance
  Scenario 2; Edge Case "app fails to build/run" → `fail` with the build/run diagnostic surfaced.
- [X] T013 [US1] Assert `gitInitialized` + `scriptsExecutable` (FR-004): a git repo exists at the
  product root **or** init was explicitly skipped-non-fatal (Edge Case: git absent / pre-existing
  work tree), and every produced `.sh` has the executable bit (or none were produced). Both
  outcomes appear in the report. Acceptance Scenario 3.
- [X] T014 [US1] Assert `reportedComplete` (FR-007): the scaffold `--json` `outcome` is the
  success outcome marked complete; an incomplete/empty scaffold (`providerSucceededEmpty`) is
  never read as complete (→ `fail`(incomplete)). Acceptance Scenario 1; Edge Case "incomplete is
  never complete".
- [X] T015 [US1] Evidence-obligations note (Principles IV/V/VI): record in the task/PR notes that
  the acceptance adds **no** I/O to any lifecycle `update` — build/run/git probes are at the test
  edge; the evidence is real filesystem + real `dotnet new`/`build`/`run`/`git` over the real
  provider (no mocks), and an unavailable provider SKIPs rather than fabricating a pass.

**Checkpoint**: US1 alone delivers the epic's green end-to-end PASS — the MVP that closes P2
confidence. Stop and validate against the real registry before proceeding.

---

## Phase 4: User Story 2 — Provenance is partitioned correctly (Priority: P2)

**Goal**: Make the provider↔SDD provenance boundary an asserted fact: provider paths are
externally-owned `generatedProduct`; no `init`-skeleton/constitution path is `generatedProduct`;
and `refresh` excludes the externally-owned paths while regenerating only SDD-owned views.

**Independent Test**: After the US1 scaffold, the result's `facts.provenancePartitioned` and
`facts.refreshExcludes` are both true; reading `.fsgg/scaffold-provenance.json` and running
`refresh` confirms the app code is byte-unchanged.

> Same shared file `CompositionAcceptanceTests.fs`; depends on Phase 3 producing a tree to
> inspect. Sequential (not `[P]`).

- [X] T016 [US2] Assert `provenancePartitioned` (FR-005): parse `.fsgg/scaffold-provenance.json`
  via `ScaffoldProvenance.tryParse`, confirm every provider-produced path's `Owner` is
  `GeneratedProduct`, and confirm no skeleton/constitution path is marked `generatedProduct`.
  Acceptance Scenario 1 of US2.
- [X] T017 [US2] Assert `refreshExcludes` (FR-006): run the refresh probe (T005) on the product,
  then assert the provider/app paths are **byte-unchanged** (hash before/after) while SDD-owned
  views regenerate. Acceptance Scenario 2 of US2.

**Checkpoint**: US1 + US2 prove the composition is both coherent and correctly partitioned for a
later safe `refresh`.

---

## Phase 5: User Story 3 — Opt-in and provider-neutral (Priority: P3)

**Goal**: The acceptance coexists with the cheap offline inner loop without contaminating it: the
default run stays offline + rendering-free and skips the acceptance; the real-provider path runs
only when explicitly requested; unavailable and misconfigured runs map to honest SKIP/FAIL.

**Independent Test**: `dotnet test FS.GG.SDD.sln` with the env unset passes offline with the
acceptance facts Skipped and zero rendering identifiers in SDD source or the acceptance project;
the extended leak-scan guard is green.

- [X] T018 [US3] Assert the env-unset SKIP path is honest in `CompositionAcceptanceTests.fs`:
  with `FSGG_SDD_ACCEPTANCE_REGISTRY` unset every fact reports **Skipped** (via the T003 guard),
  no network is touched, no result document is written (SC-003, contracts/acceptance-protocol.md
  §Gating). Acceptance Scenario 1 of US3.
- [X] T019 [US3] Assert unavailable → `skip-unavailable` (data-model.md transition; FR-008/SC-004,
  finding F1): a run whose feed/version is unreachable surfaces `outcome = providerFailed` with
  diagnostic `scaffold.providerUnavailable`; the acceptance reads the **diagnostic code** (not the
  outcome alone, which it shares with provider defects) and returns the SKIP verdict (test
  Skipped) — never a false PASS and never a FAIL of SDD. Edge Case "Provider unavailable".
  Acceptance Scenario 3 of US3. (The offline branch coverage for this mapping lives in T008's unit
  test; this fact exercises the real-provider path.)
- [X] T020 [US3] Assert the config-error FAIL path (Edge Case "Registry omitted/misconfigured"):
  a missing/misconfigured registry maps to `providerNotRun` → `fail`(config error) and never
  silently falls back to a fixture or embedded identifier (FR-009).
- [X] T021 [US3] Extend the leak-scan deny-list in
  `tests/FS.GG.SDD.Commands.Tests/ScaffoldGuardTests.fs`: add the
  `tests/FS.GG.SDD.Acceptance.Tests/**` tree (`.fs` files) to the `forbiddenTokens`
  (`fs-gg-ui`, `FS.GG.Rendering`) scan so the acceptance project is proven free of any rendering
  package id / template id / path / docs URL (FR-009/SC-003). Acceptance Scenario 2 of US3.
- [X] T021a [US3] Guard FR-012 (no Governance — finding F5): assert the acceptance project carries
  **no Governance reference** — neither a `FS.GG.Governance*` project/package reference in
  `FS.GG.SDD.Acceptance.Tests.fsproj` nor a `Governance` symbol in its `.fs` sources (a small
  static scan, the same mechanism as T021). This proves the acceptance requires no Governance
  runtime and computes no Governance verdict. (Negative invariant; offline, no network.)

**Checkpoint**: All three stories independently functional; the offline inner loop is provably
network-free and rendering-free, and the real path is reached only through the external registry.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Wire the schedule, declare the contract exception, align agent surfaces, and prove
determinism end to end.

- [X] T022 [P] Extend `docs/release/schema-reference.md`: add the `composition-acceptance-result`
  as a **second declared release-catalog exception** (after the `validation-report` exception at
  §"Declared exception"), explaining it is sensed harness output — not a produced lifecycle
  artifact — so `release-readiness.json` and its golden baseline stay unchanged (plan D5).
- [X] T023 [P] Add `.github/workflows/composition-acceptance.yml`: `schedule` +
  `workflow_dispatch`, setting `FSGG_SDD_ACCEPTANCE_REGISTRY` from a workflow secret/path and
  running `dotnet test FS.GG.SDD.sln --filter kind=composition-acceptance` — separate from the
  offline inner loop (FR-010). (`.github/workflows/` is currently empty.)
- [X] T024 [P] Add the determinism two-run check (SC-005): run the real acceptance twice with the
  same inputs and an available provider, normalize the `sensed` block to null, and assert the two
  result-document bodies are **byte-identical**. (Network-gated like the other real-provider
  facts; offline it skips.)
- [X] T025 [P] Align the four agent surfaces with one aligned line each — "the real-provider
  composition acceptance is opt-in and network-gated, out of the default inner loop": `CLAUDE.md`,
  `AGENTS.md`, `.claude/skills/fs-gg-sdd-project/SKILL.md`,
  `.codex/skills/fs-gg-sdd-project/SKILL.md` (Principle VII; keep Claude and Codex aligned).
- [X] T026 Run `quickstart.md` end to end: confirm step 1 (offline SKIP, inner loop green, guard
  passes), and — where a real registry is available — step 2 (PASS), step 4 (unavailable→SKIP),
  and step 5 (determinism). Record the verdicts.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Phase 1; **blocks all user stories** (every fact uses
  the harness + result serializer).
- **User stories (Phases 3–5)**: all depend on Phase 2. US1 is the MVP; US2 depends on US1
  producing a tree to inspect; US3's facts are independent but live in the same shared file.
- **Polish (Phase 6)**: T022/T023/T025 (`[P]`) can land any time after Phase 1; T024/T026 depend
  on the user-story facts being implemented.

### Within the shared acceptance file

`AcceptanceSupport.fs`, `CompositionResult.fs`, and `CompositionAcceptanceTests.fs` are each a
single file — tasks touching the same file are **sequential** even where conceptually parallel.
The `[P]` markers in Phase 2 apply across the **two different** files (`AcceptanceSupport.fs` vs
`CompositionResult.fs`); within one file, follow the stated order.

### Parallel opportunities

- Phase 2: the `AcceptanceSupport.fs` chain (T003→T004→T005) runs in parallel with the
  `CompositionResult.fs` chain (T006→{T007,T008}); T009 needs T006–T008.
- Phase 6: T022, T023, T025 are independent files → fully parallel.
- The leak-scan extension (T021) touches `ScaffoldGuardTests.fs` (a different project) and can
  proceed in parallel with the US1/US2 acceptance facts once the acceptance project directory
  exists (after Phase 1).

---

## Summary

| User story | Priority | Tasks | Count |
|---|---|---|---|
| Setup | — | T001–T002 | 2 |
| Foundational | — | T003–T009 | 7 |
| US1 — coherent composition (MVP) | P1 | T010–T015 | 6 |
| US2 — provenance partition | P2 | T016–T017 | 2 |
| US3 — opt-in / provider-neutral | P3 | T018–T021, T021a | 5 |
| Polish | — | T022–T026 | 5 |
| **Total** | | | **27** |

> **Implementation status (2026-06-28).** All 27 tasks complete; full offline solution suite green
> (`dotnet test FS.GG.SDD.sln` → 519 passed, 3 composition-acceptance facts gated-Skipped, 0 failed).
> The result-schema byte-exact golden, the verdict-mapping unit tests, the env-unset gate proof, the
> config-error mapping, the no-Governance guard, and the extended FR-009 leak scan all pass offline.
>
> **Real end-to-end exercise + Principle V disclosure.** The orchestration spine (T010–T017), the
> two-run determinism check (T024), and the best-effort unavailable check (T019) were exercised
> end-to-end with **real** `dotnet new install`/`dotnet new`/`dotnet build`/`dotnet run --no-build`/
> `git init` + in-process `refresh` (no mocks), yielding verdict=`pass` with all nine facts true
> (`readiness/composition-acceptance.fixture-pass.json`, `readiness/EVIDENCE.md`). **SYNTHETIC:** the
> registry in that run named the provider `rendering` but resolved it to the neutral in-repo
> `fsgg-fixture-lifecycle` template, **not** the real published rendering template (external per
> FR-009, reached only via `FSGG_SDD_ACCEPTANCE_REGISTRY`). Every other layer is real. The
> real-published-template PASS is the scheduled workflow's job (T023); it was not run in the
> implementation environment (no published registry available). T019's `Started=false` trigger is
> environment-dependent (this host resolves `dotnet` without PATH), so the
> `(providerFailed, scaffold.providerUnavailable) → skip-unavailable` mapping is proven
> deterministically offline by T008 and best-effort on the real path. No I/O was added to any
> lifecycle `update` — the build/run/git probes live at the test edge (Principles IV/V/VI).
>
> **Post-`/speckit-analyze` remediation (applied):** F1 (CRITICAL) — verdict resolution is now
> keyed on the `(outcome, diagnostic code)` pair across spec/data-model/contracts and T008/T010/T019,
> because `providerFailed` covers both unavailable (SKIP) and defect (FAIL). F3 — an offline
> mapping unit test is folded into T008. F5 — the FR-012 no-Governance guard is T021a. F2/F4 —
> headless run probe + concrete timeouts (300 s build / 60 s run / 10 s grace) in T005/T012. F7 —
> result-path override `FSGG_SDD_ACCEPTANCE_RESULT_PATH` in T010. F8 — pinned `generator.version`
> constant in T006.

**Suggested MVP scope**: Phases 1–3 (Setup + Foundational + **US1**) — a single green PASS verdict
for the real `rendering` + `lifecycle=sdd` composition. That alone delivers the epic's deliverable
(confirm the composition path through SDD's provider wrapper) and the confidence that closes the
P2 SDD phase; US2 and US3 refine what the acceptance asserts and protect the architecture.

**Parallel opportunities**: the two foundational files (harness vs result contract) build in
parallel; the leak-scan extension and the three polish docs/CI tasks are independent of the
acceptance facts. The real-provider facts are inherently sequential within the one test file.
