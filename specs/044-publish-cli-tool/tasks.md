---
description: "Task list for Publish the fsgg-sdd CLI as a dotnet tool to the org feed"
---

# Tasks: Publish the `fsgg-sdd` CLI as a dotnet tool

**Input**: Design documents from `/specs/044-publish-cli-tool/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md,
contracts/release-workflow.md, quickstart.md

**Tier**: **Tier 1 (contracted / cross-repo integration)** — adds a new published cross-repo
artifact (the `fsgg-sdd` dotnet tool consumed by FS-GG/.github#49) and extends the
release-engineering contract. The authoritative producer contract is
`contracts/release-workflow.md` (authored in the plan phase); the `release.yml` edit is its
implementation. **No `.fsi`/public-F#-surface change** — the CLI is already
`PackAsTool`/`ToolCommandName=fsgg-sdd`, so no signature/baseline edits are owed.

**Tests / evidence**: The CLI publish is gated on the existing `FS.GG.SDD.Cli.Tests`. The one
genuinely new behavioral property — the packed tool's **runtime self-containment** — is proven by
a deterministic **offline pack→install→run smoke** over real fixtures (quickstart C6;
Constitution VI, real fixtures over mocks). No new unit test is owed for the packaging/publish
YAML wiring itself (mirrors features 039/043).

**Elmish/MVU (Principle V)**: **Not applicable.** No F# product code; the only I/O is the GitHub
Actions publish + `dotnet` invocations — not an SDD lifecycle command/generator/validator
(`nextLifecycleCommand` unaffected). No `.fsi` contract, pure-transition tests, or interpreter
evidence owed (plan Principle V, justified PASS).

**Design decision in force**: This implements research **Decision 2** — the version-bearing-tag
guard is generalized to "tag must match **at least one** of the two evaluated versions" (the FR-014
reconciliation). If the maintainer prefers the alternative (keep feature-039 C2 strict and split
triggers instead), revise T004/T005 before merging — flagged in the plan completion note.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different surface, no dependency on another incomplete task).
- **[Story]**: Which user story the task serves (US1 consumer-install P1, US2 producer P2,
  US3 dev-install P3).
- **Surface split**: The committed in-repo work is the `release.yml` edit + the offline smoke +
  the contract-doc pointer (Phase 2). Publishing (Phase 3), feed visibility + consumer install
  (Phase 4/US1), and the `.github#49` wiring (Phase 6) are operational/cross-repo — no repo-file
  change. US1's *outcome* is the MVP, but it is enabled by the Phase-2 producer change.

## Path Conventions

- Edited (the committed in-repo change): `.github/workflows/release.yml`.
- New: `scripts/verify-cli-tool.sh` (the offline self-containment smoke, quickstart C6) —
  may instead be inlined as a CI step (T008 decides; default is the script).
- Reference (unchanged): `src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj` (already
  `PackAsTool`/`ToolCommandName=fsgg-sdd`, version source `Directory.Build.local.props` `0.2.0`),
  `src/FS.GG.Contracts/FS.GG.Contracts.fsproj` (`1.1.0`, publish behavior preserved),
  `tests/FS.GG.SDD.Cli.Tests/` (CLI publish gate), `tests/fixtures/registry/dependencies.yml`
  (well-formed validate fixture for the smoke).
- Cross-repo / operational (outside this repo): the org feed
  (`nuget.pkg.github.com/FS-GG`), the `FS.GG.SDD.Cli` package visibility setting,
  FS-GG/.github#49 (the coherence-gate consumer), and FS-GG/FS.GG.SDD#31 + its Coordination board
  item.

---

## Phase 1: Setup & preflight (Shared)

**Purpose**: Confirm the facts the producer change is built on, before editing anything. All tasks
are read-only (no repo files change), so all are `[P]`.

- [X] T001 [P] Confirm the CLI already packs as the tool: `grep -E 'PackAsTool|ToolCommandName' src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj` (expect `PackAsTool=true`, `ToolCommandName=fsgg-sdd`) and `dotnet msbuild src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -getProperty:Version` (expect `0.2.0`, the SDD product line — data-model "Published package"). No `.fsproj` edit is required. — VERIFIED: `PackAsTool=true`, `ToolCommandName=fsgg-sdd`, Version `0.2.0`.
- [X] T002 [P] Confirm the YAML loader is in the CLI's project-reference closure (the gap-#2 evidence): `grep -rn RegistryDocument src/FS.GG.SDD.Artifacts/LifecycleArtifacts/RegistryDocument.fsi` and `grep YamlDotNet src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj`, and that `FS.GG.SDD.Cli.fsproj` references `FS.GG.SDD.Artifacts` (research Decision 4). — VERIFIED: `RegistryDocument.fsi` present; `YamlDotNet` referenced by Artifacts; CLI ProjectReferences Artifacts.
- [X] T003 [P] Confirm the feed does **not** yet serve the CLI (the gap this closes): `gh api /orgs/FS-GG/packages?package_type=nuget --jq '.[].name'` lists `FS.GG.Contracts` and `FS.GG.UI.*` but **not** `FS.GG.SDD.Cli` (issue #31; quickstart C5 will flip this). — VERIFIED: feed lists `FS.GG.Contracts` + `FS.GG.UI.*`, no `FS.GG.SDD.Cli`.

**Checkpoint**: CLI is already packable as `fsgg-sdd@0.2.0`, its loader is in-closure, feed lacks it — safe to extend the producer.

---

## Phase 2: Foundational — extend the producer + prove self-containment (the committed in-repo change) 🎯

**Purpose**: The load-bearing committed work. Edits `release.yml` to publish the CLI alongside
Contracts (US2 implementation) and adds the offline self-containment smoke (the US1/US3 evidence).
**Blocks** any real publish and the consumer-install outcome.

**⚠️ CRITICAL**: Phases 3–5 depend on this phase being merged and live on the canonical repo.

- [X] T004 [US2] In `.github/workflows/release.yml`, add a `resolve-versions` job (canonical-repo guarded) that evaluates **both** `<Version>`s via `dotnet msbuild <proj> -getProperty:Version` (Contracts `src/FS.GG.Contracts/...`, CLI `src/FS.GG.SDD.Cli/...`), applies the **at-least-one-line tag guard** (a version-bearing `v<semver>` tag must equal `contracts_version` **or** `cli_version`, else fail; non-version-bearing tag is fine), and outputs `contracts_version`, `cli_version`, `push`. Implement the version-resolution table exactly as in `contracts/release-workflow.md` (dispatch `version` input stays Contracts-scoped; empty input ⇒ `push=false` dry run; unreadable either version on a real event ⇒ fail). (FR-005, FR-006; research Decisions 2–3, 7.)
- [X] T005 [US2] In `.github/workflows/release.yml`, refactor the existing `publish` (Contracts) job into `publish-contracts` consuming `resolve-versions` outputs (`needs: [resolve-versions, contracts-tests]`); preserve its locked restore, `dotnet pack src/FS.GG.Contracts/... -p:Version=${{ needs.resolve-versions.outputs.contracts_version }}`, non-empty-pack assertion, `nuget push --skip-duplicate`, `packages: write`, and canonical-repo guard **unchanged** except for sourcing the version from `resolve-versions` (FR-014 — the only Contracts delta is the relaxed tag guard from T004; data-model "Publish decision").
- [X] T006 [US2] In `.github/workflows/release.yml`, add a `cli-tests` job (canonical-repo guarded): locked restore of `tests/FS.GG.SDD.Cli.Tests/FS.GG.SDD.Cli.Tests.fsproj` then `dotnet test tests/FS.GG.SDD.Cli.Tests/FS.GG.SDD.Cli.Tests.fsproj -c Release --no-restore`, with the same locked-restore error message pattern as the other jobs (the CLI publish gate; research Decision 6).
- [X] T007 [US2] In `.github/workflows/release.yml`, add a `publish-cli` job (`needs: [resolve-versions, cli-tests]`, canonical-repo guarded, `permissions: { contents: read, packages: write }`): locked restore of `src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj`, `dotnet pack src/FS.GG.SDD.Cli/... -c Release -p:Version=${{ needs.resolve-versions.outputs.cli_version }} --no-restore -o artifacts/packages`, assert a `FS.GG.SDD.Cli.*.nupkg` was produced (FR-007), and `if push=='true'` push with `--skip-duplicate` to `https://nuget.pkg.github.com/FS-GG/index.json` using `${{ secrets.GITHUB_TOKEN }}` (FR-001/FR-002/FR-008). A non-duplicate push failure fails the run (FR-012).
- [X] T008 [US2] [P] Add the offline self-containment smoke as `scripts/verify-cli-tool.sh` (the runnable form of quickstart **C6**): pack `src/FS.GG.SDD.Cli` to a temp dir, `dotnet tool install FS.GG.SDD.Cli --tool-path <tmp> --add-source <tmp> --version <evaluated>` (no org feed), then assert `fsgg-sdd registry validate <well-formed fixture> --text` exits 0 and `registry validate <malformed.yml>` exits non-zero. `set -euo pipefail`; print `C6 PASS` on success. Make it executable (FR-010; Constitution VI). **Precondition (U7)**: first confirm the success-leg fixture validates clean — `dotnet run --project src/FS.GG.SDD.Cli -- registry validate tests/fixtures/registry/dependencies.yml` exits 0 (it is feature 042's real-registry input, so it should). If that fixture is *not* a passing case, author/point at a known-valid registry fixture for the exit-0 leg rather than weakening the assertion.
- [X] T009 [US2] Update the `release.yml` header comment to cite `specs/044-publish-cli-tool/contracts/release-workflow.md` as the authoritative two-package producer contract (it currently cites only feature 039), so the YAML stays the implementation of a written contract (FR-013).
- [X] T010 [US2] Validate the edited workflow locally: parse `release.yml` (e.g. `python3 -c "import yaml,sys; yaml.safe_load(open('.github/workflows/release.yml'))"`; run `actionlint` if available) and run `bash scripts/verify-cli-tool.sh` — **C6 must PASS** before this phase is considered done (do not mark `[X]` on a failing smoke).

**Checkpoint**: `release.yml` publishes both packages; the packed CLI tool runs `registry validate` standalone offline. The committed in-repo change is complete and self-verified.

---

## Phase 3: User Story 2 — the producer packs + pushes the CLI on a real run (Priority: P2)

**Goal**: Confirm the extended producer actually publishes the CLI to the org feed alongside
Contracts, on the same triggers, gated, idempotent, dry-run-capable.

**Independent Test**: A no-input dispatch packs both and pushes nothing (C1); a real publish pushes
both `FS.GG.Contracts` and `FS.GG.SDD.Cli`; a re-run pushes no duplicate (C3).

**Dependency**: After Phase 2 is merged and `release.yml` is live on `FS-GG/FS.GG.SDD`. Operational
— no repo-file change.

- [X] T011 [US2] Dry run (quickstart **C1**): `gh workflow run release.yml -R FS-GG/FS.GG.SDD` (no `version` input). Confirm `resolve-versions` sets `push=false`, both `publish-contracts` and `publish-cli` pack (logs show `FS.GG.Contracts.*.nupkg` and `FS.GG.SDD.Cli.*.nupkg`) and **neither** pushes; the feed is unchanged. — DONE: dry run 28336462461 all-green, push=false, both packed, nothing pushed (C1).
- [X] T012 [US2] Real publish (quickstart **C2/C3**): `gh workflow run release.yml -R FS-GG/FS.GG.SDD -f version=1.1.0` (the feature-043 path — also publishes the CLI at its evaluated `0.2.0`). Confirm `cli-tests` passes, `publish-cli` pushes `FS.GG.SDD.Cli 0.2.0`, and `publish-contracts` pushes `FS.GG.Contracts 1.1.0`. Re-running the same command is an idempotent green no-op (`--skip-duplicate`, FR-008). — DONE: run 28336512847 all-green; publish-cli pushed FS.GG.SDD.Cli 0.2.0; Contracts 1.1.0 idempotent 'already pushed' no-op (C2/C3).
- [X] T013 [US2] [P] Feed verification (quickstart **C5**): `gh api /orgs/FS-GG/packages?package_type=nuget --jq '.[].name'` now includes `FS.GG.SDD.Cli`, and `gh api '/orgs/FS-GG/packages/nuget/FS.GG.SDD.Cli/versions' --jq '.[].name'` lists `0.2.0` (SC-002). — DONE: feed now lists FS.GG.SDD.Cli; versions=[0.2.0] (SC-002).

**Checkpoint**: `FS.GG.SDD.Cli 0.2.0` is on the org feed next to `FS.GG.Contracts`.

---

## Phase 4: User Story 1 — consumer CI installs + runs the validator from the feed (Priority: P1) 🎯 MVP outcome

**Goal**: Make the published tool usable by a consumer with only the org feed + a run-scoped token,
and prove the `.github#49` shape works. This is the outcome that unblocks the typed coherence gate.

**Independent Test**: From a clean environment (no SDD checkout), `dotnet tool install --global
FS.GG.SDD.Cli --add-source <org feed>` then `fsgg-sdd registry validate registry/dependencies.yml`
succeeds (well-formed) / exits non-zero (malformed) (SC-001/SC-003).

**Dependency**: After Phase 3 publishes the package.

- [X] T014 [US1] **One-time operational step (FR-011)**: set the `FS.GG.SDD.Cli` org package visibility to **Public** (GitHub → `FS-GG` org → Packages → `FS.GG.SDD.Cli` → Package settings → Change visibility → Public, and/or link it to the `FS.GG.SDD` repo), mirroring `FS.GG.Contracts`. Until this lands, other repos' CI cannot restore it with their run-scoped token (quickstart "make the feed package public"). — DONE (auto): package published already Public, linked to FS-GG/FS.GG.SDD (inherited public-repo visibility); no manual flip needed.
- [X] T015 [US1] Clean consumer install + run (quickstart **C5** consumer half; SC-001/SC-003): from a directory with **no** FS.GG.SDD source, `dotnet tool install --global FS.GG.SDD.Cli --version 0.2.0 --add-source https://nuget.pkg.github.com/FS-GG/index.json` then `fsgg-sdd registry validate <a real dependencies.yml> --text` → success; repeat against a malformed file → non-zero exit. Confirms self-containment over the *feed* package (not just the local pack from T010). — DONE: clean install from feed (tool-path, no SDD source); well-formed exit 0, malformed exit 1 (SC-001/SC-003).

**Checkpoint**: A consumer with only the feed + a run-scoped token can install and run the typed validator — US1 delivered (MVP outcome); FS-GG/.github#49 is technically unblocked.

---

## Phase 5: User Story 3 — any FS-GG developer installs the tool for lifecycle commands (Priority: P3)

**Goal**: Confirm the published tool runs general `fsgg-sdd` lifecycle/cross-cutting commands, not
just `registry validate`.

**Independent Test**: After install, `fsgg-sdd --help` and a representative command behave as a
source-built CLI would.

**Dependency**: After Phase 4 (tool installable from the feed).

- [X] T016 [P] [US3] With the tool installed from the feed, run `fsgg-sdd --help` and one representative command (e.g. `fsgg-sdd registry validate ... --json` to confirm the default automation contract, and a `--rich` invocation to confirm Spectre rendering is bundled and degrades correctly) — output matches a source-built CLI (SC — US3 acceptance). — DONE: feed-installed tool: --json automation contract + --rich degrades to plain text non-interactively; matches source build (US3).

**Checkpoint**: The published tool is a full `fsgg-sdd`, usable by any FS-GG developer.

---

## Phase 6: Polish, cross-repo handoff & no-drift

**Purpose**: Prove no Contracts/golden drift, hand off to the consumer, and record the work on the
coordination layer.

- [X] T017 [P] No-drift proof (FR-014): run `dotnet test FS.GG.SDD.sln -c Release` (green, incl. the 042 registry-validator goldens and the CLI tests) and confirm `git status --porcelain` shows the only committed in-repo changes are `.github/workflows/release.yml`, `scripts/verify-cli-tool.sh`, and this `specs/044-*` dir — `src/`, `tests/fixtures/`, and the CLI `.fsproj` untouched.
- [ ] T018 [US1] Hand off to the consumer: comment on **FS-GG/.github#49** that `FS.GG.SDD.Cli 0.2.0` is live and public on the org feed with the install + `registry validate` invocation, so the gate can swap the Python stand-in (`scripts/validate-registry.py`) for `fsgg-sdd registry validate`. Use the `cross-repo-coordination` skill (`## Response` comment). The actual gate-wiring is owned by #49, not this repo.
- [ ] T019 [P] Update the coordination layer: comment the publish outcome on FS-GG/FS.GG.SDD#31, and move its Coordination board item Ready→In progress→Done as the publish + visibility land (use the `cross-repo-coordination` skill). Note that `.github#49` (Blocked by #31) can now proceed.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies — read-only preflight, all `[P]`.
- **Phase 2 (Foundational, in-repo)**: after Phase 1. T004→T005 (resolve feeds publish-contracts), T004→T007 (resolve feeds publish-cli), T006→T007 (cli-tests gates publish-cli), T008→T010 (smoke must exist to run). T009 is `[P]`. **Blocks Phases 3–5.**
- **Phase 3 (US2 publish)**: after Phase 2 is **merged + live** on the canonical repo. T011→T012→T013.
- **Phase 4 (US1, P1 MVP outcome)**: after Phase 3. T014 (visibility) before T015 (consumer install from feed).
- **Phase 5 (US3)**: after Phase 4 (tool installable).
- **Phase 6 (Polish)**: T017 after Phase 2 (diff to assert); T018 after T015 (consumer proven); T019 after T012/T014.

### Parallel opportunities

- **Phase 1**: T001–T003 all `[P]` (read-only).
- **Phase 2**: T008 (smoke script) and T009 (header comment) are `[P]` against the `release.yml` job edits; the four job edits (T004–T007) touch the same file and are sequential.
- **Phase 6**: T017 and T019 are `[P]` (test/diff vs. board update).

---

## Implementation strategy

### MVP (Phases 2 → 3 → 4)

The shippable increment that delivers value is: extend the producer (Phase 2) → publish the CLI
(Phase 3) → make it public + prove a clean consumer install (Phase 4). That is US1's outcome and
unblocks FS-GG/.github#49. **STOP and VALIDATE** at the Phase-4 checkpoint (consumer install +
`registry validate` from the feed).

### Incremental delivery

1. Phase 1 preflight → CLI packable, loader in-closure, feed lacks it.
2. Phase 2 → the committed `release.yml` edit + offline smoke (C6 PASS). **The PR for this repo.**
3. Phase 3 → publish both packages; feed lists `FS.GG.SDD.Cli`.
4. Phase 4 → make public + clean consumer install. **MVP outcome / #49 unblocked.**
5. Phase 5 → general lifecycle commands from the tool.
6. Phase 6 → no-drift proof + cross-repo handoff + board update.

---

## Notes

- **FR-009 / SC-006 fork-and-failing-tests guard is verified _by construction_, not by a runtime
  test (G4)**: the `github.repository == 'FS-GG/FS.GG.SDD'` guard on every job (T004–T007) plus the
  `needs: [..., cli-tests]` / `needs: [..., contracts-tests]` gating mean a fork event or a red test
  run can never reach a push. Fork events and red runs can't be triggered on demand, so there is no
  dedicated verification task — review the guards in the `release.yml` diff (T010) as the evidence.
- The only committed in-repo files are `.github/workflows/release.yml` (edited) and
  `scripts/verify-cli-tool.sh` (new). Publishing, package visibility, and the #49 gate-wiring are
  operational/cross-repo.
- The CLI `.fsproj` is **not** edited — it is already `PackAsTool`/`ToolCommandName=fsgg-sdd`.
- Contracts publish behavior is preserved except the relaxed tag guard (research Decision 2 / FR-014);
  never widen the scope of that delta to green a release.
- Self-containment (FR-010) is the load-bearing new property — never mark T010/T015 `[X]` on a
  failing `registry validate`; narrow and document instead.
- Elmish/MVU is N/A — no F# product code, no lifecycle command (plan Principle V).
- Never mark a task `[X]` on a red run; never weaken an assertion to green CI.
