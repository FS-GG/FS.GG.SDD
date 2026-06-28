---
description: "Task list for Publish FS.GG.Contracts to the org package feed on release"
---

# Tasks: Publish FS.GG.Contracts to the org package feed on release

**Input**: Design documents from `/specs/039-publish-contracts-package/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/release-workflow.md, quickstart.md

**Tier**: Tier 2 (release-engineering; no product contract/schema/CLI surface change, FR-010).
Per the plan's Constitution Check, **no new F# tests are owed** — the artifact is a single
YAML workflow, verified by its own `workflow_dispatch` dry run plus a real feed query
(quickstart C0–C6), gating on the existing `FS.GG.Contracts.Tests`.

**Elmish/MVU (Principle V)**: **Not applicable.** No F# product code, no lifecycle
command/generator/validator, `nextLifecycleCommand` unaffected. The only I/O lives in GitHub
Actions, exactly as the sibling workflows (`gate.yml`, `composition-acceptance.yml`). No
`.fsi` contract, pure-transition tests, or interpreter evidence are owed (plan Principle V,
justified PASS).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different file, no dependency on another incomplete task).
- **[Story]**: Which user story the task serves (US1–US4).
- **Single-file caveat**: Phases 2–5 all edit one file — `.github/workflows/release.yml`.
  Edits to that file are **not** mutually `[P]`; `[P]` is reserved for tasks touching a
  different file (local preflight, the cross-repo registry record, docs). Within the
  workflow, phases run in sequence and each task lands a distinct, self-contained block.

## Path Conventions

- New workflow: `.github/workflows/release.yml` (the entire product of this feature).
- Reference (unchanged): `src/FS.GG.Contracts/FS.GG.Contracts.fsproj` (version source),
  `tests/FS.GG.Contracts.Tests/FS.GG.Contracts.Tests.fsproj` (publish gate),
  `.github/workflows/gate.yml` (locked-restore pattern to mirror).
- Cross-repo (outside this repo): `FS-GG/.github` `registry/dependencies.yml` (FR-011).

---

## Phase 1: Setup & local preflight (Shared)

**Purpose**: Confirm the facts the workflow is built on, before authoring it. All tasks touch
no repo files (read-only / scratch), so all are `[P]`.

- [X] T001 [P] Confirm the effective Contracts version is `1.0.0` (the project override of the
  `0.2.0` SDD product line): run
  `dotnet msbuild src/FS.GG.Contracts/FS.GG.Contracts.fsproj -getProperty:Version`
  and verify it prints `1.0.0` (quickstart C0; research Decision 1). This is the version
  source the workflow reads — confirming it here de-risks the resolution step.
  → Verified: `-getProperty:Version` printed `1.0.0`.
- [X] T002 [P] Confirm single-package pack scope: run
  `dotnet pack src/FS.GG.Contracts/FS.GG.Contracts.fsproj -c Release -o /tmp/fsgg-pack` and
  verify exactly one `FS.GG.Contracts.1.0.0.nupkg` is produced (quickstart C0; research
  Decision 4). Confirms `IsPackable=true` (feature 036) and that this feature re-scopes nothing.
  → Verified: exactly one `FS.GG.Contracts.1.0.0.nupkg` produced.
- [X] T003 [P] Confirm preconditions for a net-new workflow: `git tag` is empty (no existing
  release tags), `.github/workflows/` contains only `gate.yml` and `composition-acceptance.yml`,
  and `src/FS.GG.Contracts/packages.lock.json` + the test project's lockfile both exist
  (research Decision 7 & Resolved unknowns). Confirms the locked-restore path and that
  `release.yml` is additive.
  → Verified: `git tag` empty; only `gate.yml` + `composition-acceptance.yml` present; both lockfiles exist.

**Checkpoint**: Version source, pack scope, and lockfiles confirmed — safe to author the workflow.

---

## Phase 2: Foundational — workflow spine & gating (Blocking Prerequisites)

**Purpose**: Create `.github/workflows/release.yml` with the triggers, least-privilege
permissions, canonical-repo guard, and the `contracts-tests` gate job + `publish` job
skeleton that every user story builds on. **No user-story work can begin until this exists**
— the stories all add steps inside this file.

⚠️ All Phase 2 tasks edit the same new file; do them in order (T004 creates it).

- [X] T004 [US2/US3] Create `.github/workflows/release.yml` with the trigger block exactly per
  `contracts/release-workflow.md`: `on: { release: { types: [published] }, push: { tags:
  ['v*'] }, workflow_dispatch: { inputs: { version: { description: "Explicit version to
  pack+publish. Omit for a pack-only dry run.", type: string, required: false } } } }`. The
  `workflow_dispatch.version` input serves US2 (FR-002/FR-003 override + dry run); the trigger
  block itself is US3 gating surface — hence the joint `[US2/US3]` tag. Add a top-of-file comment
  pointing at the contract doc (so the YAML is the contract's implementation, not folklore —
  plan Principle II).
- [X] T005 [US3] Add `name: release`, top-level `permissions: { contents: read }`, and a
  `concurrency` group to `.github/workflows/release.yml`. A single tagged release fires **both**
  the `release: published` and `push: tags v*` triggers; key the group on the version/tag (e.g.
  `release-${{ github.event.release.tag_name || github.ref_name }}`) with `cancel-in-progress:
  false` so the paired runs **serialize** (the second waits and no-ops via `--skip-duplicate`)
  rather than race or cancel a push mid-flight — NOT `gate.yml`'s PR-number/ref key with
  `cancel-in-progress: true` (least-privilege baseline, FR-007; dual-trigger note in
  `contracts/release-workflow.md`).
- [X] T006 [US3] Add the `contracts-tests` job to `.github/workflows/release.yml`:
  `runs-on: ubuntu-latest`, `if: github.repository == 'FS-GG/FS.GG.SDD'` (fork no-op, FR-006),
  steps = `actions/checkout@v4`, `actions/setup-dotnet@v4` (`dotnet-version: "10.0.x"`), a
  **locked** restore (`--locked-mode`, mirroring `gate.yml`'s error message + regenerate hint,
  research Decision 7), then `dotnet test tests/FS.GG.Contracts.Tests/FS.GG.Contracts.Tests.fsproj
  -c Release --no-restore` (FR-005 gate).
- [X] T007 [US3] Add the `publish` job **skeleton** to `.github/workflows/release.yml`:
  `needs: [contracts-tests]`, `runs-on: ubuntu-latest`, the same
  `if: github.repository == 'FS-GG/FS.GG.SDD'` guard, job-level `permissions: { contents: read,
  packages: write }`, and the lead-in steps `actions/checkout@v4`, `actions/setup-dotnet@v4`,
  locked restore (`--locked-mode`). Leave the resolve/pack/push steps for Phases 3–4
  (placeholder comment marking where T011/T008 land).

**Checkpoint**: A valid, runnable workflow exists that gates on the contracts tests and never
runs on a fork — but packs/pushes nothing yet. (Verify YAML parses, e.g. `gh workflow view`
after merge, or a local YAML lint.)

---

## Phase 3: User Story 1 — A released package is obtainable from the org feed (Priority: P1) 🎯 MVP

**Goal**: The `publish` job actually packs `FS.GG.Contracts` and pushes it to
`https://nuget.pkg.github.com/FS-GG/index.json`, idempotently.

**Independent Test**: Cut a release (or `workflow_dispatch` with `version: 1.0.0`), then query
the feed for `fs.gg.contracts` — the version is listed (not 404) and a clean-environment
consumer restores it from the org feed alone (quickstart C2/C5).

**Dependency**: The pack/push steps consume `${{ steps.ver.outputs.version }}` and
`${{ steps.ver.outputs.push }}` produced by US2 (T011). Author T011 first, or land it in the
same change — the P1 MVP is US1+US2 together (a correct version is inseparable from a correct
publish). T008–T010 wire the steps; T011 supplies their inputs.

- [X] T008 [US1] In `.github/workflows/release.yml` `publish` job, add the **pack** step:
  `dotnet pack src/FS.GG.Contracts/FS.GG.Contracts.fsproj -c Release
  -p:Version=${{ steps.ver.outputs.version }} --no-restore -o artifacts/packages`
  (single explicit project — single-package scope, FR-001 / research Decision 4; `--no-restore`
  reuses T007's locked restore).
- [X] T009 [US1] Add the **"packed nothing" assertion** step after pack: fail loudly if
  `artifacts/packages/*.nupkg` is empty (`set -euo pipefail`; glob/`ls` check). A green test +
  empty pack MUST NOT report success (FR-009 / data-model invariant I2 / spec edge "tests pass
  but pack produces no package").
- [X] T010 [US1] Add the **push** step, guarded by `if: steps.ver.outputs.push == 'true'`:
  `dotnet nuget push "artifacts/packages/*.nupkg" --source
  https://nuget.pkg.github.com/FS-GG/index.json --api-key ${{ secrets.GITHUB_TOKEN }}
  --skip-duplicate`. `--skip-duplicate` ⇒ idempotent re-publish (FR-004 / SC-004, US1
  acceptance #2, invariant I3); any non-duplicate push failure fails the run (FR-009). The
  `if` makes a dry run skip the push entirely (FR-003 — set by US2).

**Checkpoint**: With a version supplied, the workflow packs and pushes a real package to the
org feed and a duplicate re-push is a no-op success — US1 is functional (given T011).

---

## Phase 4: User Story 2 — The published version is release-derived, not hand-set (Priority: P1)

**Goal**: A single version-resolution step computes `version` + `push` deterministically from
the event, with loud failures on drift/defect and a benign dry run on an intentional
no-version manual run.

**Independent Test**: Trigger from a release tag matching the fsproj (`v1.0.0`) → publishes
`1.0.0`; `workflow_dispatch` with `version: 2.3.4` → publishes `2.3.4`; `workflow_dispatch`
with no version → dry run, pushes nothing; release tagged `v2.0.0` while fsproj is `1.0.0` →
fails loudly (quickstart C1/C2/C2-negative; spec US2 acceptance #1–3).

**Note**: T011 is the input to US1's T008/T010 (`steps.ver.outputs.*`). Land it with the P1
slice.

- [X] T011 [US2] Add the version-resolution step `id: ver` (outputs `version`, `push`) to the
  `publish` job in `.github/workflows/release.yml`, implementing the state machine in
  `data-model.md` / the contract table exactly:
  - read `fsproj_version` via
    `dotnet msbuild src/FS.GG.Contracts/FS.GG.Contracts.fsproj -getProperty:Version`
    (evaluated value, **not** a text grep — research Decision 1);
  - `workflow_dispatch` + non-empty `inputs.version` → `version = strip-v(input)`, `push=true`;
  - `workflow_dispatch` + empty input → `version = fsproj_version`, **`push=false`** (intentional
    dry run, FR-003 / Decision 3);
  - `release: published` / `push: tags v*` → `version = fsproj_version`, `push=true`;
  - `strip-v(x)` removes one leading `v` (`v1.0.0`→`1.0.0`), consistent with the rendering
    sibling.
  Emit a clear **dry-run notice** on the `push=false` path so an intentional no-version run is
  visibly benign (Principle VIII).
- [X] T012 [US2] Add the **drift guard** inside `id: ver` for `release`/`push(tag)` events: if
  the triggering tag is version-bearing and `strip-v(tag) != fsproj_version`, **fail loudly**
  (tag vs. package line drifted — FR-002/FR-008, invariant I1, spec edge "malformed/mismatched
  tag"). A non-version-bearing tag is acceptable (fsproj is the authority); only a *mismatched*
  version-bearing tag fails.
- [X] T013 [US2] Add the **unreadable-version guard** inside `id: ver`: on a `release`/`push(tag)`
  event, if `fsproj_version` is empty/unreadable, **fail the run** — never silently degrade a
  release event to a dry run (FR-009 / Decision 3, distinguishing defect from the benign manual
  no-version path). `set -euo pipefail` throughout the step.

**Checkpoint**: Version is sourced from the fsproj for every real publish, manual override and
dry run behave per spec, and tag drift / unreadable version fail loudly — US1+US2 P1 MVP
complete and end-to-end publishable.

---

## Phase 5: User Story 3 — Publishing is gated, safe, canonical-only (Priority: P2)

**Goal**: Verify the safety guards authored in Phase 2 actually hold, and that the push uses
least-privilege run-scoped credentials (no PAT).

**Independent Test**: A fork release/dispatch event does not run the jobs; failing
`FS.GG.Contracts.Tests` means `publish` (which `needs` them) never runs; the push authenticates
with `secrets.GITHUB_TOKEN` + `packages: write`, not a personal token (quickstart C4; spec US3
acceptance #1–3, invariants I4).

> The guards themselves were authored in T005–T007/T010 (foundational + push wiring). This
> phase confirms them — keeping US3 independently verifiable.

- [X] T014 [US3] Review `.github/workflows/release.yml` to confirm **both** jobs carry
  `if: github.repository == 'FS-GG/FS.GG.SDD'` (fork no-op, FR-006/SC-005) and that `publish`
  has `needs: [contracts-tests]` so a red test run never reaches the push (FR-005). Confirm
  no path can push when tests fail or on a fork.
- [X] T015 [US3] Confirm least-privilege credentials in `.github/workflows/release.yml`:
  top-level `permissions: contents: read`, the `publish` job (and only it) adds
  `packages: write`, and the push uses `--api-key ${{ secrets.GITHUB_TOKEN }}` with **no**
  personal access token anywhere (FR-007/SC-005). Grep the file for any `PAT`/`secrets.*TOKEN`
  other than `GITHUB_TOKEN`.

**Checkpoint**: Forks and red builds provably cannot publish; the push is least-privilege —
US3 hardening verified.

---

## Phase 6: User Story 4 — Registry coherence reflects the real feed (Priority: P3)

**Goal**: After a package lands on the feed, the cross-repo `fsgg-contracts` registry record no
longer describes the feed as empty / the package as 404.

**Independent Test**: After publish, the org coherence gate (FS-GG/.github#18) asserting
`declared fsgg-contracts == actual FS.GG.Contracts version == feed` passes, and the registry
note describes a real published package (quickstart C6; spec US4 acceptance #1, SC-006).

> **Outside this repository's product code.** Lands in `FS-GG/.github` via the cross-repo
> coordination protocol (research Decision 8, FR-011). Sequenced *after* the first real publish
> (Phases 3–4) — it is only meaningful once `1.0.0` is actually on the feed.

- [-] T016 [P] [US4] After the first successful publish, file/track the `fsgg-contracts`
  coherence-record update in `FS-GG/.github` `registry/dependencies.yml` per the cross-repo
  coordination protocol: update the coherence note from "empty feed / 404" to "published", and
  confirm the #18 declared-vs-feed gate passes (FR-011 / SC-006). Use the
  `cross-repo-coordination` skill to file/sequence this on the Coordination board. *(Not a
  change in this repo.)*
  → DEFERRED to post-publish: the registry coherence flip is only meaningful once `1.0.0` is
  actually on the feed (T018). The Coordination board item for this feature is updated now to
  record the producer-half landing and sequence the post-publish registry update.

**Checkpoint**: The registry stops asserting an incoherent state — the loop the feature was
filed to fix is closed.

---

## Phase 7: Polish & verification

**Purpose**: Prove the producer path end-to-end against the quickstart and keep the contract
doc and YAML in agreement.

- [-] T017 Run the **manual dry run** (quickstart C1): `workflow_dispatch` with no `version`
  on the canonical repo → confirm the `publish` job logs the dry-run notice, lists one packed
  `.nupkg`, and **skips** the push; a subsequent feed query is unchanged (FR-003 / SC contract).
  → DEFERRED to post-merge: requires the workflow to exist on `FS-GG/FS.GG.SDD` default branch
  before `workflow_dispatch` is selectable. The version-resolution + dry-run path it exercises
  was verified locally (the `id: ver` script simulated for the no-version dispatch case →
  `push=false` with the dry-run notice; pack + "packed nothing" assertion run against the real
  fsproj). Run as the first live check once merged.
- [-] T018 Run the **real publish + idempotent re-run** (quickstart C2/C3): release at
  `v1.0.0` (fsproj `1.0.0`) → `contracts-tests` passes, `publish` packs and pushes `1.0.0`;
  then re-run → succeeds with `--skip-duplicate` reporting the duplicate skipped, no second
  copy (SC-001/SC-004). Run the **mismatch negative** (C2-negative): a `v2.0.0` tag with fsproj
  `1.0.0` fails loudly in `id: ver`, pushing nothing.
  → DEFERRED to post-merge: requires a live release/tag event + the live org feed. The
  resolution branches were verified locally (release/push-tag matching → `push=true`; `v2.0.0`
  vs fsproj `1.0.0` → fails loudly, pushing nothing). The live push is the documented
  first-release verification step.
- [-] T019 [P] Run the **consumer restore** (quickstart C5): feed query for `fs.gg.contracts`
  returns `1.0.0` (not 404), and a clean-environment F# project restores `FS.GG.Contracts
  1.0.0` from the org feed alone (SC-002).
  → DEFERRED to post-merge: depends on T018 having published `1.0.0` to the live feed.
- [X] T020 [P] Cross-check `.github/workflows/release.yml` against
  `contracts/release-workflow.md` (triggers, gating table, version-resolution table, pack/push
  block) — the YAML is the contract's implementation; reconcile any divergence (plan Principle
  II). Confirm FR-010 holds: no `.fsgg` schema, contract surface, contract version, or CLI
  behavior changed by this branch (`git diff --stat` touches only `.github/workflows/release.yml`
  plus this spec dir).

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — read-only preflight, all `[P]`.
- **Phase 2 (Foundational)**: depends on Phase 1; **blocks all stories** — creates the one file
  every story edits. T004 → T005 → T006 → T007 in order (same file).
- **Phase 3 (US1) + Phase 4 (US2)**: the combined **P1 MVP**. Same file; US1's pack/push
  (T008/T010) consume US2's `ver` outputs (T011) → land T011 with or before T008/T010. Treat
  Phases 3–4 as one change set.
- **Phase 5 (US3, P2)**: verifies guards authored in Phase 2 + the push wiring; do after the
  publish path exists.
- **Phase 6 (US4, P3)**: cross-repo, after the first real publish (Phases 3–4 verified in
  Phase 7's T018). `[P]` — different repo.
- **Phase 7 (Polish)**: after the workflow is merged to the canonical repo (CI events require it).

### Parallel opportunities

- **Phase 1**: T001, T002, T003 all `[P]` (read-only).
- **Phases 2–5**: single file `.github/workflows/release.yml` → **not** parallel; sequential
  edits, distinct blocks.
- **Phase 6**: T016 `[P]` (FS-GG/.github, different repo).
- **Phase 7**: T019 and T020 `[P]` (feed query / doc cross-check are independent of T017/T018,
  which exercise live CI and should run in sequence).

---

## Implementation strategy

### MVP (Phases 1–4)

1. Phase 1 preflight → confirm version source, pack scope, lockfiles.
2. Phase 2 → land the workflow spine (triggers, permissions, gate job, publish skeleton).
3. Phases 3+4 together → version-resolution + pack + assert + idempotent push. **This is the
   shippable MVP**: it closes the empty-feed gap (US1) with a correct, drift-proof version (US2).
4. **STOP and VALIDATE** via Phase 7 T017 (dry run) then T018 (real publish).

### Incremental delivery

- MVP (US1+US2) → dry run + real publish verified → the fabric's producer half is live.
- US3 (Phase 5) → confirm fork/red-build/least-privilege guards.
- US4 (Phase 6) → close the cross-repo registry coherence loop in FS-GG/.github.
- Phase 7 → full quickstart C1–C6 + contract/YAML reconciliation.

---

## Notes

- The entire product is one file: `.github/workflows/release.yml`. `[P]` is therefore rare —
  reserved for preflight, the cross-repo record, the feed query, and the doc cross-check.
- No F# tests are added (Tier 2, FR-010): verification is the workflow's own dry run + a real
  feed query, gating on the existing `FS.GG.Contracts.Tests` (plan Principle VI, justified).
- Elmish/MVU is N/A here — no F# product code, no lifecycle command (plan Principle V).
- Never mark a task `[X]` on a red run; never weaken a guard (drift/defect/fork) to green CI —
  the loud failures are the point (Principle VIII).
