---

description: "Task list for feature 092 — Committed Compact Ship Verdict"
---

# Tasks: Committed Compact Ship Verdict

**Input**: Design documents from `/specs/092-committed-ship-verdict/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`

**Tests**: Required. Constitution Principle VI — behavior-changing code MUST include automated tests
that fail before the change and pass after. Real filesystem fixtures, no mocks. The two load-bearing
tests run **real `git`** (research D2: no string assertion can prove the negation fires).

**Tier**: Tier 1 (contracted change) — new generated view + schema, new `GeneratedViewKind` case,
additive fields on `ShipView` and `SchemaReferenceEntry`, seeded artifact-layout change. `.fsi` and
`PublicSurface.baseline` move. No cross-repo contract, no `contractVersion`, no registry entry.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase
- **[Story]**: `US1` (verdict in history), `US2` (the negation fires), `US3` (refresh currency),
  `US4` (taxonomy stays catalog-derived)

## Local build

This sandbox cannot restore against the committed lock file (research E1 — environment artifact, CI
is green). Append to every `dotnet build` / `dotnet test`:

```
-p:RestorePackagesWithLockFile=false -p:NuGetLockFilePath="$SCRATCH/nolock.json"
```

**Never** commit a regenerated `packages.lock.json`. Verify with
`git status --short -- '*packages.lock.json'` before every commit.

---

## Phase 1: Baseline capture (Shared)

**Purpose**: pin the pre-change behavior so every later assertion is known to invert something real.

- [X] T001 Build and test on the untouched branch; confirm green and that the lock file is untouched.
- [X] T002 Reproduce research D1 in a scratch git repository: with `readiness/*/` +
      `!readiness/*/ship-verdict.json`, `git add -A -n` stages **nothing** under `readiness/`; with
      `readiness/*/*` it stages exactly `ship-verdict.json`. Record the transcript. This is the
      regression Phase 5 encodes.
- [X] T003 [P] Reproduce research D2: confirm `ArtifactTaxonomyTests.fs:68`'s
      `Assert.Contains("readiness/*/", seeded)` still passes when the seed constant is edited to
      `readiness/*/*` with **no** negation line — i.e. the guard is vacuous under the change.
- [X] T004 [P] Inventory what must move: every enumeration of the generated-view set —
      `refreshCanonicalViews`, `ValidationHarness.determinismOutputs`, `ValidationRunner.classifyOutput`,
      `Core.standardArtifactContracts`, `ReleaseBoundaryTests` T024's `known`,
      `ReleaseConformanceTests.producedArtifacts`, `DeterminismMatrixTests.DeterminismOutputs`. Each is
      a place a new view can be silently omitted.
- [X] T005 [P] Record the golden `ship.json`'s expected `sourcesDigest`
      (`78a32b33a4bb370f169ad4a44307d7f4c0fafc7741bea0d5a82f1a1d5ad5b117`, research D8) so T012's
      independent recomputation has a fixed target.

---

## Phase 2: Foundational — the artifact and its digest (Blocking prerequisite)

**Purpose**: nothing can be emitted, catalogued, or ignored until the projection exists. Signatures
precede bodies (Constitution I/III).

- [X] T006 [US1] Extend `ShipView` with `DispositionBlockingFindingIds: string list` in
      `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Ship.fsi`, then parse it in `Ship.fs` beside the
      existing `disposition.state` read (research D7), sorted for determinism.
- [X] T007 [US1] Add `GeneratedViewKind.ShipVerdict` to
      `src/FS.GG.SDD.Artifacts/GenerationManifest.fsi`/`.fs`, with `viewKindValue ShipVerdict =
      "shipVerdict"` and a sibling `expectedShipVerdictOutputPath`.
- [X] T008 [US1] Author `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/ShipVerdict.fsi` — exactly
      `ShipVerdict`, `sourcesDigest`, `fromShipView`, `toJson` (data-model §1). Register
      `ShipVerdict.fsi`/`.fs` in `FS.GG.SDD.Artifacts.fsproj` immediately after `Ship.fs`.
- [X] T009 [US1] Implement `ShipVerdict.fs`: `sourcesDigest` as the path-sorted
      `<path>|<algorithm>:<value>` fold joined `\n` into `SchemaVersion.sha256Text` (research D8);
      `fromShipView` as a pure field copy; `toJson` via `Utf8JsonWriter(Indented = true)` in the
      canonical order of data-model §1.

**Checkpoint**: `FS.GG.SDD.Artifacts` compiles; the verdict can be produced from a parsed `ShipView`.

---

## Phase 3: US1 — the verdict exists, compact and faithful (Priority: P1)

**Goal**: `fsgg-sdd ship` writes a 20-line verdict beside `ship.json`, carrying exactly the projected
facts. **Independent test**: run `ship` on a fixture; assert existence, field set, line count, digest.

### Tests first

- [X] T010 [P] [US1] `tests/FS.GG.SDD.Artifacts.Tests/ShipVerdictTests.fs` — NEW. `fromShipView` copies
      every field of a parsed golden `ship.json`; the serialized object's property set is **exactly**
      the eleven of FR-002 (assert both directions: no missing, no extra).
- [X] T011 [P] [US1] Same file — the ship-ready shape is exactly 20 lines (FR-004). **Corrected during
      implementation**: the writer expands a non-empty `blockingFindingIds` over its own bracket lines,
      so a verdict with *n* ≥ 1 ids is `21 + n` lines, not `20 + n`. Pinned at 20/22/23/24 for 0/1/2/3.
- [X] T012 [P] [US1] Same file — `sourcesDigest` equals the value recomputed independently in the test
      from the fixture's `sources[]` (T005); mutating any one source's path *or* digest changes the
      aggregate; an empty `sources[]` yields the empty-string SHA-256 (SC-003, spec Edge Cases).
- [X] T013 [P] [US1] `tests/FS.GG.SDD.Artifacts.Tests/ShipViewTests.fs` — `parseShipView` now surfaces
      `disposition.blockingFindingIds`, sorted; a `ship.json` without the field parses to `[]`.
- [X] T014 [US1] `tests/FS.GG.SDD.Commands.Tests/ShipCommandTests.fs` — `ship` writes
      `readiness/<id>/ship-verdict.json` and reports it in `changedArtifacts` **and** `generatedViews`
      (FR-001); a run that blocks on a diagnostic error writes **neither** `ship.json` nor the verdict
      (FR-005 / SC-008); two consecutive runs are byte-identical and contain no timestamp, absolute
      path, or ANSI (FR-008 / SC-007).
- [X] T015 [US1] `tests/FS.GG.SDD.Commands.Tests/goldens/readiness/ship-verdict.json` — NEW golden, and
      a `ReadinessViewGoldenTests.fs` case asserting the emitted bytes match it.

### Implementation

- [X] T016 [US1] `Foundation.fs` — add `shipVerdictPath` beside `shipPath`; add
      `ReadFile(shipVerdictPath workId)` to `refreshReadEffects` so `refresh` can snapshot it.
- [X] T017 [US1] `HandlersShip.fs` — add the pure `shipVerdictEmission workId generator shipText`
      (plan, Phase 1 Design Notes), returning `(GeneratedViewState option * CommandEffect list * string
      option)` and parsing via `ShipModule.parseShipView`.
- [X] T018 [US1] `HandlersShip.fs` — append its effects and view state beside the existing `ship.json`
      write, **inside** the current `not hasBlocking` gate, so FR-005 holds with no new branch.
- [X] T019 [P] [US1] `LifecycleArtifacts/Core.fs` — add the `ship-verdict.json` artifact-inventory row
      after the `ship.json` row.

**Checkpoint**: US1 is independently demonstrable — `ship` emits the verdict; the audit fact exists on
disk. It is not yet *committed* (US2) nor kept current (US3).

---

## Phase 4: US3 — `refresh` re-projects it, byte-identically (Priority: P1)

**Goal**: `refresh` brings the verdict to currency from an already-current `ship.json`, never from a
stale one, through the **same** pure projection. **Independent test**: `ship`, capture bytes,
`refresh`, assert unchanged + already-current.

### Tests first

- [X] T020 [P] [US3] `tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs` — after `ship`, an
      unchanged `refresh` leaves the verdict byte-identical and reports it among
      `AlreadyCurrentViewIds` (FR-006 / SC-006).
- [X] T021 [P] [US3] Same file — a deleted verdict with a current `ship.json` is restored by `refresh`
      and reported `Refreshed`.
- [X] T022 [P] [US3] Same file — with `ship.json` stale / missing / malformed / blocked, the verdict
      **inherits** that class and is not rewritten (FR-006, second half).
- [X] T023 [P] [US3] Same file — the bytes `ship` writes and the bytes `refresh` writes are identical
      for the same inputs (FR-007 / SC-006).

### Implementation

- [X] T024 [US3] `HandlersRefresh.fs` — re-project the verdict in the `governance-handoff` slot, gated
      on `shClass = AlreadyCurrent`, comparing against the on-disk `snapshot` to choose
      `AlreadyCurrent` vs `Refreshed`; inherit `shClass` otherwise (research D5). Reuse
      `shipVerdictEmission` verbatim — do **not** write a second projection (research D6).
- [X] T025 [US3] `HandlersRefresh.fs` — add `"ship-verdict"` to `refreshCanonicalViews` and to every
      per-view bucket list (`perViewState`, `classifyToBucket`, the blocked-path `perViewState`), so it
      is reported like its siblings.
- [X] T026 [P] [US3] `ValidationHarness.fs` — add `"ship-verdict.json"` to `determinismOutputs` and fix
      the "the nine generated views" comment. `ValidationRunner.fs` — add the basename to
      `classifyOutput` (FR-017; research D10 — omission surfaces as a `CoverageGap`, so this is
      enforced, not decorative).
- [X] T027 [P] [US3] `tests/FS.GG.SDD.Validation.Tests/DeterminismMatrixTests.fs` — **no change needed**:
      it builds a deliberately *focused* plan (`verify.json`/`ship.json`/`command-report`), not the full
      set. `reconcileSurface` walks the real `readiness/` tree against `determinismOutputs`, so T026 is
      what makes the view covered; verified by the green Validation suite.

**Checkpoint**: US1 + US3 — the verdict exists and stays current. It is still gitignored.

---

## Phase 5: US2 — the negation fires (Priority: P1)

**Goal**: exactly one file per work item enters the index. **Independent test**: real `git add -A` in a
seeded temporary repository. ⚠️ Nothing in this phase may be verified by a string assertion (research
D2).

### Tests first

- [X] T028 [US2] `tests/FS.GG.SDD.Commands.Tests/GitignoreNegationTests.fs` — NEW. In a temp dir:
      `git init`, `initializeProject`, materialize
      `readiness/<id>/{work-model,analysis,verify,ship,ship-verdict}.json`, `summary.md`, and
      `agent-commands/claude/guidance.json`; run `git add -A`; assert the staged set beneath
      `readiness/` is **exactly** `{readiness/<id>/ship-verdict.json}` (FR-014 / SC-004).
- [X] T029 [US2] Same file — the **regression**: with the pre-feature `readiness/*/` rule written into
      the same repository, `git add -A` stages **nothing** beneath `readiness/`. This is what makes the
      contents rule provably load-bearing rather than cosmetic.
- [X] T030 [P] [US2] Same file — `git check-ignore` reports every nested
      `agent-commands/<target>/…` path ignored, and `ship-verdict.json` not ignored (SC-004).
- [X] T031 [P] [US2] Same file — this repository's own rule: in a temp repo carrying the repo's
      `.gitignore`, only `specs/<feature>/readiness/<id>/ship-verdict.json` stages, and a root
      `readiness/<id>/<proof>` path is **not** ignored (FR-015 / SC-005).
- [X] T032 [P] [US2] `ArtifactTaxonomyTests.fs` — strengthen the vacuous assert (T003): assert the
      seeded text contains **both** `readiness/*/*` and `!readiness/*/ship-verdict.json`, and that it
      does **not** contain a bare `readiness/*/` line (FR-011).

### Implementation

- [X] T033 [US2] `Foundation.fs` `gitignoreSeedText` — `readiness/*/` → `readiness/*/*` +
      `!readiness/*/ship-verdict.json`, with the one-line ADR-0026 exception comment (data-model §5).
      Whole-file no-clobber `AgentGuidanceTarget` write kind is unchanged (FR-009).
- [X] T034 [US2] The repository's own `.gitignore` — `specs/*/readiness/` →
      `specs/*/readiness/*/*` + `!specs/*/readiness/*/ship-verdict.json`. Root `readiness/<id>/`
      pinned proofs stay matched by no rule (FR-010).
- [X] T035 [US2] Confirm `Drift.fs` needs **no** change (presence-only, research D2) and that
      `InitCommandTests` / `ScaffoldCommandTests` / `RemediationCommandTests` still pass — the seeded
      skeleton set and no-clobber behavior are unchanged.

**Checkpoint**: the audit question is answerable from git alone. US1 + US2 + US3 deliver the feature.

---

## Phase 6: US4 — the catalog and the catalog-derived doc (Priority: P2)

**Goal**: the verdict is a catalogued, drift-guarded generated view, and the taxonomy doc's two tables
remain projections of the catalog.

### Tests first

- [X] T036 [P] [US4] `ArtifactTaxonomyTests.fs` — re-partition the guard: regenerable block ≡
      `generatedView ∧ ¬durableGenerated`; new durable-generated table ≡ `generatedView ∧
      durableGenerated`; both set-equal to their doc projections (FR-013 / SC-009). Assert set-equality
      in both directions — never `⊇` (research D3).
- [X] T037 [P] [US4] `ReleaseBoundaryTests.fs` — amend T024's `known` set to admit `ShipVerdict`, and
      rename/comment it to record that feature 092 introduced the kind (FR-016).
- [X] T038 [P] [US4] `tests/FS.GG.SDD.Artifacts.Tests/ReleaseContractTests.fs` — `durableGenerated`
      round-trips through `serialize`/`parse`; it is `true` for the verdict and `false` for every other
      entry (FR-012 / SC-009).
- [X] T039 [P] [US4] `ReleaseConformanceTests.fs` — add `ship-verdict.json` to `producedArtifacts`;
      T015's catalog ≡ produced-set equality holds exactly.
- [X] T040 [P] [US4] `ReleaseReadinessCheckTests.fs` — T019 (catalog covers every enumerable kind) still
      passes, and fails if the catalog entry is removed while the kind exists (SC-010).

### Implementation

- [X] T041 [US4] `ReleaseContract.fsi`/`.fs` — add `SchemaReferenceEntry.DurableGenerated: bool`;
      serialize it beside `baselinePresent`; parse it with the same `GetBoolean()` shape. `jsonViewEntry`
      keeps `DurableGenerated = false`; add a `durableJsonViewEntry` (or an explicit override) for the
      one durable entry (data-model §3).
- [X] T042 [US4] `ReleaseContract.fs` `currentRelease()` — add the `ship-verdict.json` catalog entry
      after `ship`, with the field inventory of data-model §3 (`schemaVersion` Stable, rest
      `AdditiveOptional`), `contractVersion = None`, `durableGenerated = true`.
- [X] T043 [US4] Regenerate `docs/release/release-readiness.json` and
      `tests/FS.GG.SDD.Artifacts.Tests/baselines/release-readiness.json` (byte-locked). Verify no
      `schemaVersion` bump (the flag is `AdditiveOptional`).
- [X] T044 [US4] `docs/reference/artifact-taxonomy.md` — add the **durable generated** table (with
      `readiness/<id>/ship-verdict.json`), keep it out of the regenerable block, update the fenced seed
      fragment to the amended bytes, and note the one sanctioned exception to "ignore by role, never
      re-include" beside the ADR-0026 link.
- [X] T045 [P] [US4] `docs/release/schema-reference.md` — add the catalog table row and the JSON field
      inventory for `ship-verdict.json` (T016-enforced by `ReleaseContractTests`).

---

## Phase 7: Surface, docs, and polish

- [X] T046 Regenerate `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` with
      `FSGG_UPDATE_BASELINE=1`; **read the diff** — it must contain only `ShipVerdict.*`, the new
      `GeneratedViewKind` case, `expectedShipVerdictOutputPath`, `ShipView.DispositionBlockingFindingIds`,
      and `SchemaReferenceEntry.DurableGenerated`. Anything else is scope creep.
- [X] T047 [P] Update the three `fs-gg-sdd-ship` skill mirrors (`.claude/`, `.codex/`, `.agents/`) — the
      ship skill documents `ship.json` as the sole output. All three roots must stay **byte-identical**
      (`SkillMirror` / drift guard). Regenerate `.agents/skills/skill-manifest.json` if its body hash
      moves (`fsgg-sdd registry skill-manifest --write`).
- [X] T048 [P] Run `fsgg-sdd validate --text` end-to-end; confirm `ship-verdict.json` is enumerated and
      no `CoverageGap` is reported (FR-017 / SC-011).
- [X] T049 Confirm `ship.json` is byte-identical to `main`'s on the same fixture, no
      `registry/dependencies.yml` entry was added, and no `contractVersion` introduced (FR-018 / SC-012).
- [X] T050 Full `dotnet build` + `dotnet test` green; `git status --short -- '*packages.lock.json'`
      empty; walk `quickstart.md` §0–§11 top to bottom and confirm each stated proof.

---

## Dependencies

```
Phase 1 (baseline) ─┬─> Phase 2 (artifact + digest)  ── blocking for everything
                    │
Phase 2 ────────────┼─> Phase 3 (US1: ship emits)
                    │        │
                    │        └─> Phase 4 (US3: refresh re-projects)   [needs shipVerdictEmission]
                    │
                    ├─> Phase 5 (US2: gitignore)      [independent of 3/4 — only needs the filename]
                    │
                    └─> Phase 6 (US4: catalog + doc)  [needs GeneratedViewKind from T007]

Phase 3,4,5,6 ─────────> Phase 7 (surface, docs, polish)
```

- **T007 blocks T037/T041/T042** (the kind must exist before the catalog admits it).
- **T017 blocks T018 and T024** (one shared projection — FR-007).
- **T033 blocks T028/T032**; **T034 blocks T031**.
- **T041/T042 block T036/T043** (the guard partitions on a field that must exist).
- Phase 5 is genuinely parallel with Phases 3–4: it depends only on the *name*
  `ship-verdict.json`, not on the emission.
