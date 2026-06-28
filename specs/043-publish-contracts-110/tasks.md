---
description: "Task list for Publish FS.GG.Contracts 1.1.0 to the org feed and make source/feed/registry coherence durable"
---

# Tasks: Publish FS.GG.Contracts 1.1.0 to the org feed and make source/feed/registry coherence durable

**Input**: Design documents from `/specs/043-publish-contracts-110/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md,
contracts/contracts-version-coherence.md, quickstart.md

**Tier**: Tier 2 (release-engineering / process; no `.fsgg` schema, contract surface, contract
version, or CLI behavior change — `1.1.0` was already shipped by feature 042). Per the plan's
Constitution Check, **no new F# tests are owed**: the only committed in-repo artifact is one
Markdown runbook; the publish reuses feature 039's `release.yml` unchanged and gates on the
existing `FS.GG.Contracts.Tests`. Verification is the publish run + a real feed query + the doc
presence check (quickstart C0–C7).

**Elmish/MVU (Principle V)**: **Not applicable.** No F# product code, no lifecycle
command/generator/validator, `nextLifecycleCommand` unaffected. The only I/O is the existing
GitHub Actions publish + a cross-repo registry edit. No `.fsi` contract, pure-transition tests,
or interpreter evidence are owed (plan Principle V, justified PASS).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different surface, no dependency on another incomplete task).
- **[Story]**: Which user story the task serves (US1–US3).
- **Surface split**: US1 (publish) and US2 (registry sync) touch **no repo files** — they invoke
  the existing `release.yml` and edit the cross-repo `.github` registry. US3 is the **only**
  committed in-repo change (one new doc). So US3 is `[P]` against US1/US2 — it can land at any
  time, independent of the publish.

## Path Conventions

- New (the only committed in-repo change): `docs/release/contracts-version-bump-checklist.md`.
- Reference (unchanged): `.github/workflows/release.yml` (feature 039 — invoked, not edited),
  `src/FS.GG.Contracts/FS.GG.Contracts.fsproj` + `ContractVersion.fs` (version source, already
  `1.1.0`), `tests/FS.GG.Contracts.Tests/` (publish gate),
  `tests/fixtures/registry/dependencies.yml` (042 validator input — **frozen**, not edited).
- Cross-repo (outside this repo): `FS-GG/.github` `registry/dependencies.yml` + FS-GG/.github#42
  (registry `package-version` advance), and the Coordination board item / FS-GG/FS.GG.SDD#27.

---

## Phase 1: Setup & preflight (Shared)

**Purpose**: Confirm the facts the publish is built on, before invoking anything. All tasks are
read-only (no repo files change), so all are `[P]`.

- [X] T001 [P] Confirm the source is at `1.1.0`: run
  `dotnet msbuild src/FS.GG.Contracts/FS.GG.Contracts.fsproj -getProperty:Version` (expect
  `1.1.0`) and `grep 'let value' src/FS.GG.Contracts/ContractVersion.fs` (expect
  `let value = "1.1.0"`). This is the version `release.yml` will publish (quickstart C0; data-model
  "Contract source version").
- [X] T002 [P] Confirm the feed is a version behind: run
  `gh api /orgs/FS-GG/packages/nuget/FS.GG.Contracts/versions --jq '.[].name'` and verify it lists
  only `1.0.1` (the gap this feature closes; quickstart C1).
- [X] T003 [P] Confirm the publish path is present and dispatchable: `.github/workflows/release.yml`
  exists on the canonical repo with a `workflow_dispatch` `version` input (feature 039), and the
  current account can dispatch it. No edit is made — research Decision 1 (the workflow is invoked,
  not changed).

**Checkpoint**: Source = `1.1.0`, feed = `1.0.1` only, publish workflow present — safe to publish.

---

## Phase 2: User Story 1 — FS.GG.Contracts 1.1.0 is obtainable from the org feed (Priority: P1) 🎯 MVP

**Goal**: Land `FS.GG.Contracts 1.1.0` on the org feed via the existing publish workflow, so any
authorized consumer can resolve it.

**Independent Test**: Invoke `release.yml` resolving to `1.1.0`, then query the feed — `1.1.0` is
listed (not 404) alongside `1.0.1`, and a clean authorized consumer restores exactly `1.1.0`
(quickstart C3–C4).

**Note**: Operational only — no repo files change. Recommended trigger is `workflow_dispatch` with
`version=1.1.0` (the "publish exactly what was asked" path), **not** a `v1.1.0` git tag/release
(the SDD product line is `0.2.0`; a `v1.1.0` tag would misrepresent it — research Decision 1).

- [X] T004 [US1] (Optional pre-flight) Dry-run the publish: `gh workflow run release.yml --repo
  FS-GG/FS.GG.SDD` (no `version` input). Confirm the `publish` job logs the dry-run notice, lists
  `Packed: FS.GG.Contracts.1.1.0.nupkg`, and **skips** the push; the feed is unchanged (quickstart
  C2; inherited feature-039 behavior).
- [X] T005 [US1] Publish `1.1.0`: `gh workflow run release.yml --repo FS-GG/FS.GG.SDD -f
  version=1.1.0`. Confirm `FS.GG.Contracts.Tests` passes (the publish gate, FR-002), pack produces
  one `.nupkg`, and `nuget push --skip-duplicate` lands `1.1.0` on
  `https://nuget.pkg.github.com/FS-GG/index.json` (FR-001; quickstart C3). Re-running is an
  idempotent success (FR-003).
- [X] T006 [US1] Verify the feed: `gh api /orgs/FS-GG/packages/nuget/FS.GG.Contracts/versions --jq
  '.[].name'` now lists `1.1.0` (and `1.0.1`), not 404 (SC-001; quickstart C4). Then confirm a clean
  authorized consumer `dotnet restore`s `FS.GG.Contracts 1.1.0` from the org feed alone (a throwaway
  project referencing `FS.GG.Contracts 1.1.0` against the org `nuget.config` source) — this restore
  check is required to fully evidence SC-001, not optional.

**Checkpoint**: `1.1.0` is obtainable from the org feed — the core ask of #27 is delivered (MVP).

---

## Phase 3: User Story 2 — Registry package-version advances so source, feed, and registry agree (Priority: P2)

**Goal**: Advance the org registry `fsgg-contracts.package-version` `1.0.1 → 1.1.0` so all three
authorities (source, feed, registry) record `1.1.0` and the compatibility projection is accurate.

**Independent Test**: After the feed shows `1.1.0`, `FS-GG/.github`
`registry/dependencies.yml` records `package-version: 1.1.0`, its compatibility projection agrees,
and the `contract-coherence` gate stays green (quickstart C5; SC-002/SC-003).

**Dependency**: Strictly **after** Phase 2 T006 confirms `1.1.0` is live — `package-version` MUST
NOT advance ahead of the feed (FR-007). Cross-repo: handled in `FS-GG/.github`, not this repo
(research Decision 4 / ADR-0001).

- [X] T007 [US2] Notify the registry coordinator that `1.1.0` is live and request the advance:
  `gh issue comment 42 --repo FS-GG/.github --body "## Response — FS.GG.Contracts 1.1.0 is live on
  the org feed (versions API confirms). Please advance registry fsgg-contracts.package-version
  1.0.1→1.1.0 (the version pin is already 1.1.0). Source/feed now coherent at 1.1.0."` — this is the
  `.github`-side registry issue **FS-GG/.github#42** (NOT the SDD issue #27, and NOT the unrelated
  Done `.github#27`); use its successor if #42 is closed. Use the `cross-repo-coordination` skill to
  file/sequence if no open tracking issue exists (FR-004).
- [ ] T008 [US2] *(Blocked on FS-GG/.github#47 — request filed 2026-06-28; the `package-version` 1.0.1→1.1.0 advance is the coordinator's merge. `registry.version` is already 1.1.0; `package-version` still 1.0.1 until #47 lands.)* Confirm coherence once `.github` merges the advance: `registry/dependencies.yml`
  `fsgg-contracts` shows `version: 1.1.0` **and** `package-version: 1.1.0`, the
  `docs/registry/compatibility.md` projection agrees, and the `contract-coherence` gate is green on
  `.github` PRs and `main` (SC-002/SC-003; quickstart C5). *(Verification of a cross-repo change;
  not an edit in this repo.)*

**Checkpoint**: Source, feed, and registry all record `1.1.0` — the last incoherence is cleared.

---

## Phase 4: User Story 3 — A source contract bump can no longer silently outrun the feed and registry (Priority: P3)

**Goal**: Add the durable, in-repo runbook so a future `FS.GG.Contracts` source bump always
publishes and advances the registry in the same change — the root-cause fix. This is the **only
committed in-repo artifact** and is independent of the publish (`[P]` against US1/US2).

**Independent Test**: `docs/release/contracts-version-bump-checklist.md` exists and names the three
same-change actions (bump source · publish to feed · update `.github` registry `version` +
`package-version`), citing the `contract-coherence` gate / ADR-0001 (quickstart C6; SC-004).

- [X] T009 [P] [US3] Create `docs/release/contracts-version-bump-checklist.md` — a human-facing
  runbook projecting `contracts/contracts-version-coherence.md` "Durable bump protocol". It MUST:
  (a) state the four-value coherence invariant (source == feed(newest) == registry.version ==
  registry.package-version); (b) give a numbered same-change checklist — **(1)** bump fsproj
  `<Version>` **and** `Fsgg.ContractVersion.value` together, **(2)** publish the new version to the
  org feed via `.github/workflows/release.yml` (`workflow_dispatch -f version=<new>`), **(3)**
  advance `FS-GG/.github` `registry/dependencies.yml` `fsgg-contracts.version` and — only after the
  feed confirms (FR-007) — `package-version`; (c) cite the `contract-coherence` gate and **ADR-0001**
  (a version bump must update the `.github` registry in the same coordinated change); (d) name no
  rendering-/Governance-/provider-specific identity (constitution Engineering Constraints). Match the
  front-matter style of the other `docs/release/*.md` docs.
- [X] T010 [P] [US3] Verify the runbook per quickstart C6: it exists and `grep` confirms it names
  `publish`, `package-version`, and `ADR-0001`/`contract-coherence`. A maintainer following it
  produces a coherent source/feed/registry set on the next bump (SC-004).

**Checkpoint**: The durable guardrail is in the repo — the 042 failure mode (source bumped, feed +
registry not) cannot recur unnoticed.

---

## Phase 5: Polish & verification

**Purpose**: Prove no contract/CLI/golden drift, and record the work on the coordination layer.

- [X] T011 [P] Confirm no surface drift (SC-005): run `dotnet test FS.GG.SDD.sln -c Release` (full
  suite incl. the 042 registry-validator goldens) — green — and `git status --porcelain` shows the
  only committed in-repo change is `docs/release/contracts-version-bump-checklist.md` (plus this
  `specs/043-*` dir). Confirm `tests/fixtures/registry/dependencies.yml`, `src/`, and
  `.github/workflows/` are untouched (quickstart C7; research Decision 5).
- [X] T012 [P] Update the coordination layer: comment the publish + checklist outcome on
  FS-GG/FS.GG.SDD#27, and move its Coordination board item toward Done once Phase 3's registry
  advance lands (use the `cross-repo-coordination` skill). Record that US1/US2 are operational/
  cross-repo and US3 is the committed in-repo deliverable.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — read-only preflight, all `[P]`.
- **Phase 2 (US1, P1 MVP)**: after Phase 1. Operational (invoke `release.yml`). T004 → T005 → T006
  in order (dry run → publish → verify).
- **Phase 3 (US2, P2)**: strictly **after** Phase 2 T006 (FR-007 — never advance ahead of the
  feed). Cross-repo in `FS-GG/.github`.
- **Phase 4 (US3, P3)**: **independent** of Phases 2–3 (the only repo-file change). Can be authored
  and merged at any time, before or after the publish. T009 → T010.
- **Phase 5 (Polish)**: T011 after T009 (doc exists for the `git status` assertion); T012 after the
  publish (T006) and ideally the registry advance (T008).

### Parallel opportunities

- **Phase 1**: T001–T003 all `[P]` (read-only).
- **Across phases**: US3 (T009–T010) is `[P]` against US1/US2 — a different surface (a repo doc vs.
  workflow invocation + cross-repo registry). The doc PR and the publish are fully independent.
- **Phase 5**: T011 and T012 are `[P]` (test/diff check vs. coordination-board update).

---

## Implementation strategy

### MVP (Phase 2)

US1 alone — publish `1.1.0` to the feed — is the shippable MVP: it closes the feed-behind-source
gap that #27 was filed for. Then **STOP and VALIDATE** via quickstart C4 (feed query).

### Incremental delivery

1. Phase 1 preflight → confirm source `1.1.0`, feed `1.0.1`, workflow present.
2. Phase 2 (US1) → publish `1.1.0`; verify the feed. **MVP done.**
3. Phase 3 (US2) → advance the `.github` registry `package-version`; full coherence.
4. Phase 4 (US3) → the durable checklist doc (the committed PR for this repo; can land first or in
   parallel).
5. Phase 5 → no-drift proof + coordination-board update.

---

## Notes

- The only committed in-repo file is `docs/release/contracts-version-bump-checklist.md`. US1/US2 are
  operational (invoke feature-039 `release.yml`) and cross-repo (advance the `.github` registry).
- No F# tests are added (Tier 2): verification is the publish run + feed query + doc presence,
  gating on the existing `FS.GG.Contracts.Tests` (plan Principle VI, justified).
- Elmish/MVU is N/A — no F# product code, no lifecycle command (plan Principle V).
- The 042 registry **fixture** is frozen — never edit `tests/fixtures/registry/dependencies.yml`
  here (would change 042 goldens; SC-005 / research Decision 5).
- `package-version` advances only **after** the feed confirms `1.1.0` (FR-007); never speculatively.
- Never mark a task `[X]` on a red run; never weaken the no-drift / no-fixture-edit boundary to
  green CI.
