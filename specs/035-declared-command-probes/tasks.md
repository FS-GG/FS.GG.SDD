---
description: "Task list for Declared-or-Default Acceptance Build/Run Probes"
---

# Tasks: Declared-or-Default Acceptance Build/Run Probes

**Input**: Design documents from `/specs/035-declared-command-probes/`

**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓,
contracts/probe-command.md ✓

**Tests**: Included and mandatory. This is a test-harness feature whose entire
value is probe behavior; constitution Principle VI requires fail-before /
pass-after evidence, and SC-002 is *literally* a pure-resolver test that spawns
no process. Write each story's tests first and confirm they FAIL before
implementing.

**Tier**: Whole feature is **Tier 1** (`scaffold-provider` contract surface,
H2-forward-compatible). Every phase matches that tier, so per-task `[T1]` tags
are omitted (skill: omit when it matches).

**Scope**: All work is confined to `tests/FS.GG.SDD.Acceptance.Tests/` plus one
shared-contract doc update. No `src/` change, no public `FS.GG.SDD.*` package
surface, no `.fsi` (the harness declares none — data-model.md's `val` signatures
are the advisory contract). No scaffold/provider-schema/provenance/lifecycle
artifact change (spec Scope boundary).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (parallel-safe)
- **[Story]**: `[US1]`/`[US2]`/`[US3]` — traceability to the spec's user stories

## Elmish/MVU applicability

I/O-bearing (process edge) but **no new MVU loop** — justified in plan
Constitution Check V. The change keeps I/O at the existing edge
(`startProcess` / `runToCompletion` / the run-probe grace loop) and extracts the
decision into a **pure resolver** (`declared-or-default → ProbeCommand`), which
is the testable transition. Principle V exempts simple selectors/validators from
MVU ceremony; data-model.md's function signatures stand in for the absent `.fsi`.
See **T019**.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish a green baseline and the new test file before any change.

- [X] T001 Confirm the pre-change baseline is green: run `dotnet test FS.GG.SDD.sln`
  (offline, `FSGG_SDD_ACCEPTANCE_REGISTRY` unset) and record that all offline
  tests pass and the `composition-acceptance` facts report **Skipped**. This is
  the FR-005 / SC-001 reference state.
- [X] T002 [P] Create `tests/FS.GG.SDD.Acceptance.Tests/ProbeResolutionTests.fs`
  with a `module FS.GG.SDD.Acceptance.Tests.ProbeResolutionTests` stub (an `open`
  of `AcceptanceSupport` + `Xunit`, no facts yet) and register it in
  `tests/FS.GG.SDD.Acceptance.Tests/FS.GG.SDD.Acceptance.Tests.fsproj` as a
  `<Compile Include="ProbeResolutionTests.fs" />` **after** `AcceptanceSupport.fs`
  (the resolver lives there) and before `CompositionAcceptanceTests.fs`. Confirm
  the solution still builds.

**Checkpoint**: Solution builds with the new (empty) offline test file registered.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared harness types used by every story. ⚠️ Blocks US1 and US2.

- [X] T003 Add the two harness records to `AcceptanceSupport.fs` next to the
  existing `ProbeResult` (≈ line 180), per data-model.md:
  - `DeclaredCommand = { Executable: string; Arguments: string list }` — the
    optional provider-declared command, consumed as `DeclaredCommand option`; the
    1:1 forward-compatible read of the H2 `ProviderDescriptor` build/run fields
    (FR-004). No working-directory field (FR-003 fixes it at the product root).
  - `ProbeCommand = { Executable: string; Arguments: string list; WorkingDirectory: string }`
    — the resolved command handed to the existing edge.

**Checkpoint**: Shared types compile; US1 and US2 can proceed (in parallel if staffed).

---

## Phase 3: User Story 1 - Default probes preserve today's green behavior (Priority: P1) 🎯 MVP

**Goal**: With no declared command (the only state reachable today), the build
and run probes produce the same facts and the same
`composition-acceptance-result` pass/fail as the pre-change harness — the run
default normalized to `dotnet run --project <discovered>` (FR-001/FR-002/FR-005,
SC-001).

**Independent Test**: Offline, assert `resolveBuildCommand None` /
`resolveRunCommand None` / `discoverRunnableProject` produce the default
invocations, then run the full offline suite and confirm composition facts are
unchanged (Skipped offline) — no provider required.

### Tests for User Story 1 (write FIRST, confirm they FAIL) ⚠️

- [X] T004 [P] [US1] In `ProbeResolutionTests.fs`, add failing pure-resolver
  default-branch facts (spawn **no** process): `resolveBuildCommand None root`
  ⇒ `{ Executable="dotnet"; Arguments=["build"]; WorkingDirectory=root }`;
  `resolveRunCommand None root` over a temp product containing one `*.fsproj`
  ⇒ `Some { Executable="dotnet"; Arguments=["run";"--project";<discovered>]; WorkingDirectory=root }`.
- [X] T005 [P] [US1] In `ProbeResolutionTests.fs`, add failing determinism +
  empty-product facts for `discoverRunnableProject` (FR-008): repeated calls over
  the same multi-project temp product return the **same** ordinal-sorted-first
  relative path; an empty product ⇒ `None`, and `resolveRunCommand None` over it
  ⇒ `None` (so the probe can emit a diagnosed not-started outcome).

### Implementation for User Story 1

- [X] T006 [US1] Implement `discoverRunnableProject : root:string -> string option`
  in `AcceptanceSupport.fs`: enumerate `*.fsproj` and `*.csproj` under `root`,
  map to forward-slash relative paths, **ordinal-sort**, take the first; `None`
  when none exist (research D4 / FR-008). Reference only generic tooling.
- [X] T007 [US1] Implement the pure resolvers in `AcceptanceSupport.fs`:
  `resolveBuildCommand : DeclaredCommand option -> root:string -> ProbeCommand`
  and `resolveRunCommand : DeclaredCommand option -> root:string -> ProbeCommand option`.
  Default branch only is gated by US1 tests: build ⇒ `dotnet build`@root; run ⇒
  `discoverRunnableProject root` mapped to `dotnet run --project <p>`@root, else
  `None`. The `Some`/declared arm is completed under **T015** (US2). (Same file
  as T015 — sequence T007 → T015.)
- [X] T008 [US1] Wire `buildProbe`/`runProbe` in `AcceptanceSupport.fs` to take an
  **explicit** declared command (`declared: DeclaredCommand option` curried before
  `root` — F# optional parameters `?p` are member-only and these are `let`-bound
  functions, so use a plain `option` parameter, not `?declared`), resolve it
  to a `ProbeCommand`, and route that through the **existing** edge unchanged
  (`runToCompletion` 300 s for build; the 10 s grace + 60 s overall loop for run).
  `resolveRunCommand … = None` (no runnable project) ⇒ return
  `{ Started=false; ExitCode=-1; Diagnostic="no runnable project discovered." }`
  (FR-007). Drop the old hard-coded `dotnet run --no-build`; the resolved
  `ProbeCommand` is the only invocation source.
- [X] T009 [US1] Update the two probe call sites in
  `CompositionAcceptanceTests.fs` (`buildProbe root` ≈ line 79, `runProbe root` ≈
  line 84) to pass the new explicit declared argument as `None`
  (`buildProbe None root` / `runProbe None root`) — `declared = None` is today's
  behavior, so no observable change. Leave the existing `AppBuilds`/`AppRuns` fact
  logic in `CompositionAcceptanceTests.fs` (≈ line 130, authored under feature 034)
  intact.
- [X] T010 [US1] Verify FR-005 / SC-001: run `dotnet test FS.GG.SDD.sln` offline;
  confirm the new default-branch resolver facts pass, the full suite is green, and
  the `composition-acceptance` facts still report **Skipped** with no
  `composition-acceptance.json` written — identical to the T001 baseline.

**Checkpoint**: MVP — default path is byte-for-verdict unchanged; the optional
declared parameter exists and resolves to the default. Shippable independently.

---

## Phase 4: User Story 2 - Probes honor a provider-declared command (Priority: P2)

**Goal**: A supplied declared command is invoked (its executable, arguments, and
product-rooted working directory) instead of the `dotnet` default, under the same
bounded execution semantics (FR-003/FR-004/FR-006/FR-010, SC-002/SC-004/SC-005).

**Independent Test**: Offline, with a synthetic declared command (generic tooling
only, no provider identity): the pure resolver returns the declared command never
`dotnet` (SC-002), and the real edge invokes it and returns a `ProbeResult`
reflecting its exit — no real provider involved.

### Tests for User Story 2 (write FIRST, confirm they FAIL) ⚠️

- [X] T011 [P] [US2] In `ProbeResolutionTests.fs`, add failing declared-branch
  facts (spawn **no** process, SC-002): `resolveBuildCommand (Some { Executable="mybuild"; Arguments=["--fast"] }) root`
  ⇒ `{ Executable="mybuild"; Arguments=["--fast"]; WorkingDirectory=root }` and is
  **never** `dotnet`; the analogous `resolveRunCommand (Some …)` ⇒ `Some` of the
  declared command at `root`. Assert the read shape is the 1:1 `{ Executable; Arguments }`
  H2 forward-compat form (FR-004 / SC-004).
- [X] T012 [P] [US2] In `ProbeResolutionTests.fs`, add failing FR-010 facts: a
  `Some` whose `Executable` is empty or whitespace resolves to the **default**
  (same `ProbeCommand` as `None`) for both build and run — not an attempt to
  launch a blank executable.
- [X] T013 [US2] In `ProbeResolutionTests.fs`, add a failing **synthetic**-command
  execution fact (name discloses it is synthetic per constitution VI): a trivial
  deterministic declared command using only generic, platform-standard tooling
  (no provider/template/package/docs token) driven through
  `buildProbe (Some declared) root` (and `runProbe (Some declared) root`) returns a
  `ProbeResult` reflecting that command's exit — proving FR-003/FR-006 end-to-end
  through the real edge.
- [X] T014 [US2] In `ProbeResolutionTests.fs`, add failing failure-mode facts for
  a declared command (FR-007 / SC-005): missing executable ⇒
  `{ Started=false; ExitCode=-1; Diagnostic="could not start `<exe>`." }`; a
  non-zero exit ⇒ `{ Started=true; ExitCode<>0; Diagnostic=<surfaced> }`; a hanging
  command ⇒ killed at its bound with a timeout diagnostic. Three distinct
  diagnosed non-zero outcomes; none hangs.

### Implementation for User Story 2

- [X] T015 [US2] Complete the `Some`/declared arm of `resolveBuildCommand` /
  `resolveRunCommand` in `AcceptanceSupport.fs` (depends on T007, same functions):
  a non-blank declared command ⇒ `{ Executable=declared.Executable; Arguments=declared.Arguments; WorkingDirectory=root }`
  for both probes; a blank `Executable` (null/empty/whitespace) ⇒ fall through to
  the default (FR-010). Confirm `buildProbe`/`runProbe` route the declared
  `ProbeCommand` through the **same** bounded edge as the default — no second
  timeout path (FR-006). Make T011–T014 pass.

**Checkpoint**: US1 and US2 both pass; declared and default share one invocation
shape and one set of bounds.

---

## Phase 5: User Story 3 - Harness stays provider-agnostic (Priority: P3)

**Goal**: No Governance/rendering identity and no provider-specific id, template,
path, command, or docs URL enters the harness or the probe defaults (FR-009 /
SC-003, invariant T021a).

**Independent Test**: The existing "acceptance project carries no Governance
reference" fact still passes after the resolver is added to `AcceptanceSupport.fs`,
and the defaults reference only `dotnet`.

- [X] T016 [US3] Run the standing invariant fact
  (`--filter "FullyQualifiedName~acceptance project carries no Governance reference"`)
  and confirm it still passes — the resolver/discovery/defaults added to
  `AcceptanceSupport.fs` introduce no `Governance` token (the fact already scans
  that file). No edit expected; this is a guard check.
- [X] T017 [P] [US3] In `ProbeResolutionTests.fs`, add an offline fact asserting
  the default `ProbeCommand` tokens are exactly the generic set
  (`dotnet` + `build` / `run` + `--project`) — no provider, template, package,
  path, or docs-URL string in the defaults (FR-009 / SC-003).

**Checkpoint**: All three stories independently green; provider-agnostic invariant
proven post-change.

---

## Phase 6: Polish & Cross-Cutting

- [X] T018 Update the shared behavioral contract
  `specs/034-scaffold-composition-acceptance/contracts/acceptance-protocol.md`
  §"The build/run probe" to describe **declared-or-default** resolution: the run
  default normalized to `dotnet run --project <discovered>`, deterministic
  ordinal-first project discovery, the three diagnosed failure modes, and the
  optional declared command — cross-linking
  `specs/035-declared-command-probes/contracts/probe-command.md`. No
  result-document shape change.
- [X] T019 [P] Record the evidence-obligation / Principle V justification in the
  feature (a short note in the feature folder or PR description): the probes stay
  at the test edge, the pure resolver is the tested transition, no new MVU loop
  and no `.fsi` is warranted (harness has none); data-model.md's `val` signatures
  are the advisory contract. Principle IV (idiomatic simplicity) holds — one
  record per entity, one pure function per probe, reusing the existing edge.
- [X] T020 Run the quickstart.md offline scenarios 1–6 and confirm the documented
  expected outcomes (default unchanged, declared-beats-default, deterministic
  discovery, synthetic execution, diagnosed failure modes, provider-agnostic).
  Scenario 7 (real provider, network-gated) is left for the scheduled
  registry-set run — out of the offline inner loop.

---

## Dependencies & Execution Order

- **Phase 1 (Setup)** → **Phase 2 (Foundational)** are sequential and block everything.
- **US1 (P1)** and **US2 (P2)** both depend on Phase 2. They share
  `AcceptanceSupport.fs`: the resolver functions are introduced in **T007 (US1)**
  and completed in **T015 (US2)**, so **T007 precedes T015**. Their *tests* live
  in distinct facts within `ProbeResolutionTests.fs` and are independently
  authorable.
- **US3 (P3)** is a guard over the finished resolver code; run after US1/US2 land
  (T016 verifies no token regressed; T017 is parallel-safe).
- **Phase 6 (Polish)** depends on the resolved behavior being final (after US2).

### Within each story

- Tests are authored and **fail** before implementation (constitution VI).
- T006 (discovery) before T007 (resolver uses it) before T008 (probe wiring) before T009 (call sites).
- T007 (default arm) before T015 (declared arm) — same functions.

### Parallel opportunities

- T002 is `[P]` in Setup.
- US1 test tasks **T004**, **T005** are `[P]` (distinct facts, no process spawn).
- US2 test tasks **T011**, **T012** are `[P]`; **T013**/**T014** spawn processes
  (still distinct facts, but sequence after the pure-resolver facts compile).
- US3 **T017** and Polish **T019** are `[P]`.
- With staff, US1 and US2 test-authoring can proceed concurrently after Phase 2;
  the resolver implementation serializes on `AcceptanceSupport.fs` (T007 → T015).

## Task counts per user story

- **US1 (P1, MVP)**: 7 tasks (T004–T010) — 2 tests, 5 implementation/verify.
- **US2 (P2)**: 5 tasks (T011–T015) — 4 tests, 1 implementation.
- **US3 (P3)**: 2 tasks (T016–T017).
- **Setup/Foundational/Polish (cross-cutting)**: 6 tasks (T001–T003, T018–T020).
- **Total**: 20 tasks.

## Suggested MVP scope

**User Story 1 (Phase 3, T001–T010)**. It delivers the entire forward-compatible
seam — the optional declared parameter exists and resolves to the normalized
`dotnet build` / `dotnet run --project <discovered>` default — while proving zero
observable change (FR-005 / SC-001). US2 then layers declared-command support and
US3 re-proves the provider-agnostic invariant, each independently testable.
