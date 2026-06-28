# Tasks: Correct capabilities schema version to 2 and republish FS.GG.Contracts 1.0.1

**Input**: Design documents from `/specs/040-correct-capabilities-version/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/declared-constants.md, contracts/delivery.md, quickstart.md

**Tests**: Included — the spec mandates a version-constant verification suite (FR-003, SC-001) and the constitution requires test evidence (Principle VI). Test changes precede the implementation flip per Principle I (assert 2 → fail → flip constant → pass).

**Change tier**: Tier 1 (contracted change) overall. No phase reclassifies, so no per-task `[T1]/[T2]` annotations are emitted.

**Elmish/MVU applicability**: **N/A.** This is a pure declared-constant + package-identity correction. No `Model`/`Msg`/`Effect`/`update`/interpreter is introduced; pack/publish/resolve occur at the `dotnet`/`nuget.config` tooling edge, outside SDD runtime (Principle V PASS/N/A, FR-007). No `.fsi` boundary tasks are required because the public signature is unchanged.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file, no ordering)
- **[Story]**: `[US1]`, `[US2]`, `[US3]` — traceability to the spec's user stories
- Exact file paths are given in each task

---

## Phase 1: Setup — Shared local folder feed infrastructure

**Purpose**: Establish the in-repo delivery substrate (committed `nuget.config` + feed directory) so US2 can pack/publish/resolve and so restore stays clean. Shared infra; no constant or version change yet.

- [X] T001 [P] Create the committed feed directory `.fsgg-local-feed/.gitkeep` (repo root) so the configured local source path exists and `dotnet restore` stays clean while the feed is empty (data-model "Supporting config"; delivery.md).
- [X] T002 Create repo-root `nuget.config` adding the local folder source `<add key="fsgg-local" value="./.fsgg-local-feed" />` **without** `<clear/>`, so nuget.org and inherited sources keep resolving (contracts/delivery.md "Shared local folder feed"). Depends on T001 (the source path must exist).
- [X] T003 Regression check: run `dotnet restore FS.GG.SDD.sln` and confirm no `error` / `unable to load the service index` (quickstart Scenario D). Depends on T001, T002.

**Checkpoint**: Feed source is configured and restore is clean; US2 publish/resolve is unblocked. US1 does not depend on this phase.

---

## Phase 2: User Story 1 — Declared capabilities version matches the Governance reference (Priority: P1) 🎯 MVP

**Goal**: `Fsgg.Schemas.capabilitiesVersion` reads **2** (corrected from 1), the three sibling Governance-owned constants stay **1**, and the version-constant verification asserts 2 and the full suite passes — with no `.fsi`/surface/`entries` drift.

**Independent Test**: `dotnet test tests/FS.GG.Contracts.Tests` is green; the declared `capabilities` version reads 2 and `governance`/`policy`/`tooling` read 1; `PublicSurface.baseline` and the `entries`/owner facts are unchanged (quickstart Scenario A).

### Tests for User Story 1 (write first; must FAIL before implementation) ⚠️

- [X] T004 [US1] In `tests/FS.GG.Contracts.Tests/SchemaVersionConstantTests.fs`, change the assertion in the *"Governance-owned schema versions equal the declared reference values"* fact (line 61) from `Assert.Equal(1, Schemas.capabilitiesVersion)` to `Assert.Equal(2, Schemas.capabilitiesVersion)`; leave the `governance`/`policy`/`tooling` assertions at `1` (contracts/declared-constants.md "Verification contract"). Run `dotnet test tests/FS.GG.Contracts.Tests` and confirm this fact now **FAILS** (constant is still 1).

### Implementation for User Story 1

- [X] T005 [US1] In `src/FS.GG.Contracts/Schemas.fs` (line 164), change `let capabilitiesVersion = 1` to `let capabilitiesVersion = 2`; leave `governanceVersion`/`policyVersion`/`toolingVersion = 1` and every SDD-owned constant untouched (data-model Entity 1; FR-001, FR-002). Do **not** edit `Schemas.fsi` — `val capabilitiesVersion: int` is unchanged (Principle III).
- [X] T006 [US1] Run `dotnet test tests/FS.GG.Contracts.Tests` and confirm green: the T004 assertion now passes, and the unchanged SDD-owned-constants fact, the `entries`/owner fact, and `PublicSurface.baseline` still pass (FR-003, FR-007, SC-001; quickstart Scenario A). Depends on T004, T005.

**Checkpoint**: US1 complete and independently verifiable — the package is a truthful single source of truth (`capabilities` = 2) with zero surface/emission drift. This is the MVP.

---

## Phase 3: User Story 2 — Corrected package republished as 1.0.1 on the shared feed (Priority: P1)

**Goal**: Bump the package identity `1.0.0` → `1.0.1` (new immutable identity, never an in-place mutation), pack it, and publish it to the committed local folder feed so consumers can resolve `1.0.1` carrying `capabilities` = 2.

**Independent Test**: `FS.GG.Contracts.1.0.1.nupkg` packs, resolves from `.fsgg-local-feed/`, and reading its `Fsgg.Schemas.capabilitiesVersion` returns 2; `1.0.0` is unmutated (quickstart Scenarios B & C).

**Dependencies**: requires Phase 1 (feed infra) and US1 (corrected constant must be in the package being packed).

### Implementation for User Story 2

- [X] T007 [US2] In `src/FS.GG.Contracts/FS.GG.Contracts.fsproj` (line 9), bump `<Version>1.0.0</Version>` to `<Version>1.0.1</Version>` (data-model Entity 2; FR-004). Do not re-pack or mutate any existing `1.0.0` artifact.
- [X] T007a [US2] **Internal-coherence coupling (discovered at delivery via the org `contract-coherence` gate).** The gate (`FS-GG/.github` `.github/workflows/contract-coherence.yml`) asserts fsproj `<Version>` == `Fsgg.ContractVersion.value` and uses it as the actual version for the pin-drift check. Bump `src/FS.GG.Contracts/ContractVersion.fs` `value "1.0.0"→"1.0.1"`, `patch 0→1` (major/minor unchanged), and update `tests/FS.GG.Contracts.Tests/ContractVersionTests.fs` to assert `"1.0.1"`/patch `1` (fail-before/pass-after). `ContractVersion.fsi` and `PublicSurface.baseline` unchanged. Without this `1.0.1` is internally incoherent and cannot pass the gate. Depends on T007.
- [X] T008 [US2] Pack the new immutable identity: `TMP="$(mktemp -d)"; dotnet pack src/FS.GG.Contracts/FS.GG.Contracts.fsproj -c Release -o "$TMP/packages" --nologo`, and confirm exactly one `FS.GG.Contracts.1.0.1.nupkg` is produced (quickstart Scenario B; FR-004). The version comes from the T007 fsproj bump, so no `-p:Version` override is needed. Depends on T007.
- [X] T009 [US2] Publish to the local feed (reusing the `$TMP` from T008 in the same shell session): `dotnet nuget push "$TMP/packages/FS.GG.Contracts.1.0.1.nupkg" --source fsgg-local`, then `find .fsgg-local-feed -iname 'FS.GG.Contracts*1.0.1*'` to confirm `1.0.1` is present under the committed feed layout (quickstart Scenario C; FR-005, SC-002). Depends on T003 (feed configured), T008.

**Checkpoint**: US1 AND US2 both hold — a consumer pinning `1.0.1` from the feed obtains `capabilities` = 2 single-sourced with no local literal (SC-005), and `1.0.0` remains distinct and unchanged.

---

## Phase 4: User Story 3 — Org dependency registry pin reflects the corrected package (Priority: P2)

**Goal**: Advance the `fsgg-contracts` pin `1.0.0` → `1.0.1` (`owner: sdd`) in the `FS-GG/.github` org dependency registry, keeping the contract-coherence workflow green.

**Independent Test**: the `fsgg-contracts` entry in the org registry reads `1.0.1` and the contract-coherence workflow passes (quickstart Scenario E).

**Out-of-repo**: this is cross-repo bookkeeping in `FS-GG/.github`, not a code deliverable in this tree (plan "Cross-cutting decisions"; contracts/delivery.md). Sequence **after** US2 so the registry points at a published, resolvable version.

- [ ] T010 [US3] Via the `cross-repo-coordination` protocol, file a coordination issue + PR against `FS-GG/.github` advancing the `fsgg-contracts` pin `1.0.0` → `1.0.1` (`owner: sdd`); sequence it on the org Coordination board (FR-006, contracts/delivery.md). Depends on T009 (1.0.1 must be published first).
- [ ] T011 [US3] Confirm the `fsgg-contracts` entry reads `1.0.1` and the `FS-GG/.github` contract-coherence workflow passes (SC-003; quickstart Scenario E). Depends on T010. Does **not** block this repo's merge.

**Checkpoint**: the cross-repo registry is coherent with the published package; downstream consumers following the registry pin (incl. FS.GG.Governance#14) are unblocked.

---

## Phase 5: Validation & Wrap-up

**Purpose**: Confirm the full quickstart and the no-behaviour-change bar.

- [X] T012 [P] Run the full quickstart (`specs/040-correct-capabilities-version/quickstart.md`) Scenarios A–D and confirm every "Done when" checkbox holds (Scenario E tracked, not blocking). Depends on T006, T009.
- [X] T013 [P] Confirm FR-007 / SC-004: no SDD-emitted artifact schema version changed and no SDD runtime behaviour changed — `git diff` touches only `Schemas.fs:164`, `SchemaVersionConstantTests.fs:61`, `FS.GG.Contracts.fsproj:9`, the new `nuget.config`, and `.fsgg-local-feed/.gitkeep`; `Schemas.fsi` and `PublicSurface.baseline` are unchanged. Confirm FR-008: `.github/workflows/release.yml` is **unchanged** (GitHub Packages path stays deferred).

---

## Dependencies & Execution Order

### Phase order

- **Phase 1 (Setup, feed infra)**: no dependency on the constant change; can start immediately. Prerequisite for US2 publish.
- **Phase 2 (US1)**: independent of Phase 1; the MVP. T004 (test, fail) → T005 (flip constant) → T006 (test, pass).
- **Phase 3 (US2)**: depends on Phase 1 (feed) and US1 (corrected constant in the packed package).
- **Phase 4 (US3)**: depends on US2 (published `1.0.1`); out-of-repo, non-blocking for merge.
- **Phase 5 (Validation)**: depends on US1 + US2.

### Cross-task dependencies (beyond plain phase order)

- T002 after T001; T003 after T001+T002.
- T005 after T004 (assert-fail before flip, Principle I); T006 after T004+T005.
- T008 after T007; T009 after T003+T008.
- T010 after T009; T011 after T010.

### Parallel opportunities

- T001 is `[P]` (independent file). Phase 1 is otherwise serial (T002→T003).
- **US1 (Phase 2) and Phase 1 can run fully in parallel** — different files, no shared dependency — until US2 needs both.
- Within US1, tasks are serial (fail-before/flip/pass ordering); no `[P]`.
- T012 and T013 (`[P]`) are independent validation reads.

---

## Suggested MVP scope

**User Story 1 (Phase 2)** — correcting `capabilitiesVersion` to 2 with the verification suite green — is the substance and the prerequisite the downstream Governance re-type is blocked on. US2 (republish `1.0.1`) is required to actually unblock consumers; US3 is out-of-repo follow-on bookkeeping.

## Task counts

- US1: 3 tasks (T004–T006)
- US2: 3 tasks (T007–T009)
- US3: 2 tasks (T010–T011, out-of-repo)
- Setup: 3 tasks (T001–T003); Validation: 2 tasks (T012–T013)
- **Total: 13**
