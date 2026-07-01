# Tasks: Orchestrator skill fan-out — union SDD + provider skills into all three agent roots

**Input**: Design documents from `/specs/056-orchestrator-skill-fanout/`

**Prerequisites**: plan.md, spec.md, research.md (R1–R9), data-model.md (E1–E7 + truth table), contracts/skill-fanout.md (P1–P10), quickstart.md (Scenarios A–F)

**Tests**: REQUIRED. Constitution Principles I (Spec → FSI → Semantic Tests → Impl) and VI
(test evidence mandatory, fail-first with real `dotnet new` fixtures, no mocks). Within every
phase, write the tests first and confirm RED before the `.fs` bodies.

**Organization**: Grouped by user story. Phases run in sequence; `[P]` tasks within a phase touch
different files and may run in parallel.

**Tier**: Tier 1 (contracted change). One public-surface change (`ScaffoldProvenance` additive
`MirroredPaths` field + `Mirrored` owner). Additive JSON, provenance stays **schema v1**.

**Implementation status (2026-07-01)**: Complete — **all 33 tasks `[X]`**, full solution green
(Contracts 64, Artifacts 149, Cli 80, Validation 18, Commands 462 — 0 failures). The feature added
zero net new failures; T031 additionally completed the incomplete `0.2.1→0.3.0` republish that had
left 10 pre-existing baseline failures (ReleaseContract/SchemaMigration/GeneratedModelCurrency/
NormalizedWorkModel/ScaffoldCliCoherence), so the whole suite is now green. Note: the CLI-subprocess
smoke tests require a **Release** build of `src/FS.GG.SDD.Cli` (`dotnet build -c Release`) before
`dotnet test`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: different files, no dependency on another incomplete task in this phase
- **[Story]**: `US1` / `US2` / `US3` (or blank for setup/foundational/polish)

---

## Phase 1: Setup (fixtures + baseline)

**Purpose**: A green baseline and the real provider fixtures the story tests drive (no mocks).

- [X] T001 Confirm the offline baseline is green: `dotnet build FS.GG.SDD.sln` and
  `dotnet test tests/FS.GG.SDD.Commands.Tests --filter FullyQualifiedName~Scaffold`. Record the
  pre-change pass count so later RED/GREEN transitions are attributable.
- [X] T002 [P] Author the **new positive** fixture `tests/fixtures/scaffold-provider/skills-agents-cotenant/`
  — a real `dotnet new` template that writes `.agents/skills/fs-gg-elmish/SKILL.md` plus an ordinary
  product file, and declares the `lifecycle` symbol. Disclose the synthetic body in a fixture comment
  (Principle VI). Model it on the existing `lifecycle/` fixture.
- [X] T003 [P] Author the **new negative** fixture `tests/fixtures/scaffold-provider/skills-intrusion-agents/`
  — writes `.agents/skills/fs-gg-sdd-custom/SKILL.md` (reserved-namespace intrusion in the neutral
  root). Model it on the existing `skills-intrusion/` fixture.
- [X] T004 [P] Add the registry entries for the two new fixtures under
  `tests/fixtures/scaffold-provider/registries/` (a `*.providers.yml` per fixture, following the
  existing registry pattern). Also **split** the existing combined `skills-intrusion` fixture (which
  writes both `.claude/skills/leak/` **and** `.codex/skills/leak/`) into two single-root fixtures —
  `skills-intrusion-claude/` (writes only `.claude/skills/leak/`) and `skills-intrusion-codex/`
  (writes only `.codex/skills/leak/`) — each with its own `*.providers.yml`, so US2 Scenario C can
  attribute rejection to each whole-root clause **independently** (plan project-structure line 140).
  Repoint the one existing reference (`ScaffoldCommandTests.fs:~629`,
  `skills-intrusion.providers.yml`) to the new per-root fixtures.

**Checkpoint**: Fixtures + registries resolve; baseline test count recorded.

---

## Phase 2: Foundational (shared contract, seed seam, strict guard clause)

**Purpose**: The public surface, the third-root seeding seam, and the one guard clause that ALL
three stories build on. FSI first, then fail-first tests, then the `.fs` bodies.

**⚠️ CRITICAL**: No user story work begins until this phase is complete.

### Contract surface (FSI first — Principle I/III)

- [X] T005 In `src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fsi`: add the optional
  `MirroredPaths: ScaffoldProducedPath list` field to `ScaffoldProvenanceRecord` (documented as
  additive, schema **v1**, default `[]`), and add the new `Mirrored` case to the `ArtifactOwner`
  union (serialized `"mirrored"`). No other public surface moves. Refresh `PublicSurface.baseline`
  if it enumerates the record (data-model E5/E6, research R4).

### Fail-first tests (write RED before T009–T011)

- [X] T006 [P] In `tests/FS.GG.SDD.Commands.Tests/` add an `isSddTree`/`isSddOwned` **truth-table**
  unit test asserting every row of data-model.md's E-table: `.claude/skills/anything` and
  `.codex/skills/anything` stay whole-root `true`; `.agents/skills/fs-gg-sdd-*/…` is `true` (new
  clause); `.agents/skills/fs-gg-elmish/…` is `false`; bare `.agents/skills/` and `.agents/x` are
  `false`; `.fsgg`/`work`/`readiness` unchanged; `CLAUDE.md`/`AGENTS.md` owned-not-tree. Must FAIL
  on the missing `.agents/skills/fs-gg-sdd-` clause (contracts P1–P4).
- [X] T007 [P] In `tests/FS.GG.SDD.Commands.Tests/SeededSkillsTests.fs` extend the seeding test to
  assert `init` produces the 15 `fs-gg-sdd-*` skills in **all three** roots (`.claude/skills/`,
  `.codex/skills/`, `.agents/skills/`) byte-for-byte identical across roots, and byte-stable across
  two runs (Scenario A, FR-004, SC-003, P5). Must FAIL (only two roots seeded today).
- [X] T008 [P] In `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs` add a
  **provenance schema-v1 additive guard**: a record with `MirroredPaths` round-trips (serialize →
  `tryParse`), `schemaVersion` stays `1`, absent/null `mirroredPaths` parses to `[]`, `mirroredPaths`
  serializes after `producedPaths` sorted by path, and `"mirrored"` never appears in `producedPaths`.
  Must FAIL (field does not exist yet) — data-model §Provenance shape, P10.

### Implementation (make T006–T008 green)

- [X] T009 In `src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fs`: serialize/`tryParse` the additive
  `MirroredPaths` (after `producedPaths`, sorted by path, `tryParse` defaults absent/null to `[]`)
  and the `Mirrored` owner (`"mirrored"`). Keep `serialize` byte-deterministic (canonical key order,
  no clock/abs-path/ANSI). Makes T008 green (research R4, data-model E5).
- [X] T010 In `src/FS.GG.SDD.Commands/CommandWorkflow/SeededSkills.fs`: extend `skillEffects` to emit
  a **third** `WriteFile(".agents/skills/{name}/SKILL.md", body, AgentGuidanceTarget)` per skill from
  the same canonical embedded body. Single seam → `init` and `scaffold` both gain the third root.
  Makes T007 green (research R1, data-model E2, P5).
- [X] T011 In `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs`: add the single
  `isSddTree` clause `p.StartsWith(".agents/skills/fs-gg-sdd-", Ordinal)`. Keep `.claude/skills/`
  and `.codex/skills/` whole-root reserved (do **not** narrow — opposite of reverted 055). Makes
  T006 green (research R5, contracts P1/P2).

**Checkpoint**: Contract surface, third-root seeding, and the strict+symmetric guard are green.
Foundation ready — stories can proceed.

---

## Phase 3: User Story 2 - The intrusion guard stays strict and symmetric (Priority: P1)

**Goal**: A provider write into `.claude/skills/`, `.codex/skills/`, or `.agents/skills/fs-gg-sdd-*`
is rejected as `scaffold.providerWroteSddTree` (exit 2, `providerFailed`), no fan-out performed.

**Independent Test**: Scaffold the intrusion fixtures; confirm exit 2, `providerWroteSddTree`, and
that no intruded path is mirrored or recorded as product.

> US2 sits before US1 because it hardens the guard the mirror stage relies on; the guard clause
> itself already landed in Phase 2 (T011), so this story is end-to-end rejection coverage.

### Tests for User Story 2 (fail-first)

- [X] T012 [P] [US2] In `tests/FS.GG.SDD.Commands.Tests/ScaffoldGuardTests.fs` (or
  `ScaffoldCommandTests.fs`) assert the per-root `skills-intrusion-claude` **and**
  `skills-intrusion-codex` fixtures (T004) each yield exit **2**, `scaffold.providerWroteSddTree`,
  `providerFailed`, no post-instantiation mirror, and the intruded path absent from both
  `producedPaths` and `mirroredPaths` — driving each fixture in its own case so each whole-root clause
  is proven independently (Scenario C, FR-001, SC-002, P1).
- [X] T013 [P] [US2] Add a negative test driving the new `skills-intrusion-agents` fixture
  (`.agents/skills/fs-gg-sdd-custom/`): exit **2**, `scaffold.providerWroteSddTree`, path absent from
  produced/mirrored (Scenario D, FR-002, SC-002, P2).
- [X] T014 [P] [US2] Assert the existing `.fsgg/`·`work/`·`readiness/` intrusion behavior is
  unchanged (regression guard, exit 2) — reuse/confirm the existing `writes-into-fsgg` fixture
  coverage (Scenario/edge, FR unchanged, P4).

### Implementation for User Story 2

- [X] T015 [US2] Verify no impl beyond T011 is needed: the mirror stage (US1) MUST skip reserved
  paths when enumerating `.agents/skills/*`. If T012–T014 reveal a gap (e.g. the terminal
  intrusion outcome running any mirror tick), fix it in `HandlersScaffold.fs` so an intrusion
  finalizes in one tick with **no** mirror/post-instantiation steps (data-model state machine,
  FR-012). Otherwise mark `[-]` with rationale.
  **Verified — no extra impl needed:** intrusions finalize on the terminal `finalizeScaffold`
  path (before any post-instantiation tick), and `providerSkillFiles` reads only the diff's
  non-reserved `.agents/skills/*` (the `fs-gg-sdd-*` namespace is already removed as an
  `isSddTree` intrusion). T012–T014 pass unchanged.

**Checkpoint**: Strict, symmetric guard proven; T012–T014 green.

---

## Phase 4: User Story 1 - Same skills in every agent root after scaffold (Priority: P1) 🎯 MVP

**Goal**: After a compliant `scaffold`, all three roots hold the byte-identical union of seeded
`fs-gg-sdd-*` ∪ provider `fs-gg-*` skills; the mirrored `.claude`/`.codex` copies are recorded in
`mirroredPaths` (owner `mirrored`) and the provider `.agents` canonical stays `generatedProduct`.

**Independent Test**: Scaffold the `skills-agents-cotenant` fixture; confirm exit 0,
`providerSucceeded`, three-root byte-identity of `fs-gg-sdd-*` **and** `fs-gg-elmish`, and the
provenance split above.

### Tests for User Story 1 (fail-first)

- [X] T016 [P] [US1] In `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs` add
  "scaffold fans out the union to all three roots": drive `skills-agents-cotenant`, assert exit 0,
  `providerSucceeded`, **no** `providerWroteSddTree`, and that `.claude/skills/`, `.codex/skills/`,
  `.agents/skills/` each contain byte-identical `fs-gg-sdd-*` **and** `fs-gg-elmish` trees
  (Scenario B, FR-005/006, SC-001, P6). Must FAIL (no mirror stage yet).
- [X] T017 [P] [US1] Add the provenance-attribution assertion: `.agents/skills/fs-gg-elmish/SKILL.md`
  in `producedPaths` (`generatedProduct`); `.claude/.../fs-gg-elmish` + `.codex/.../fs-gg-elmish` in
  `mirroredPaths` (`mirrored`); no `fs-gg-sdd-*` path in either array; `schemaVersion` still `1`
  (Scenario B, FR-007, P7). Must FAIL.
- [X] T018 [P] [US1] Add the **provider-produces-no-skills** case: a fixture (reuse `lifecycle/` or
  `ok/`) that emits no `.agents/skills/*` → all three roots carry the seeded `fs-gg-sdd-*` set
  byte-identically, `mirroredPaths` is `[]`, no provider skill appears (Acceptance US1 #3).
- [X] T019 [P] [US1] In `tests/FS.GG.SDD.Cli.Tests/ScaffoldParityTests.fs` assert the mirrored/produced
  paths appear identically across the json/text/rich projections and the rich path changes no JSON
  byte (Scenario F, SC-005, P10). Must FAIL.

### Implementation for User Story 1

- [X] T020 [US1] In `HandlersScaffold.fs` add the pure MIRROR planning to the post-instantiation
  staged machine (re-derived from the interpreted-effect log, no new model field, Principle V): on
  create-success, plan `EnumerateDirectory ".agents/skills"` → for each **non-reserved** provider
  skill `ReadFile` its `SKILL.md` → `WriteFile` that exact body into `.claude/skills/{name}/` and
  `.codex/skills/{name}/` (no-clobber `AgentGuidanceTarget`); stage the two copies into
  `mirroredPaths` (owner `Mirrored`). Order the MIRROR tick before/alongside the existing `git init`
  probe (research R2/R3, data-model state machine). Makes T016/T017 green.
- [X] T021 [US1] Thread `mirroredPaths` into the provenance write (TICK A) and into all three report
  projections (json/text/rich) so the mirrored facts are carried identically (research R4, P10).
  Makes T019 green.
- [X] T022 [US1] Ensure an incomplete fan-out is never reported complete: a mirror `ReadFile`/`WriteFile`
  failure surfaces the `scaffold.mirrorFailed` diagnostic and finalizes as a **non-success** scaffold
  at **exit 2** — reusing the existing tool-defect class, **no** new outcome or exit code (FR-012, P10;
  data-model §State transitions). This assertion is **required**, not optional: drive it via the
  staged-machine unit path (inject a failing mirror `WriteFile` into the interpreted-effect log) and
  assert the outcome is not `providerSucceeded`, exit is 2, `scaffold.mirrorFailed` is present, and the
  report/provenance record no completed fan-out. Add a fault-injection fixture variant additionally if
  the harness supports it.

**Checkpoint**: MVP complete — compliant scaffold yields `claude ≡ codex ≡ agents = union`,
correctly attributed; T016–T019 green.

---

## Phase 5: User Story 3 - init seeds all three roots; refresh/upgrade bring them to currency (Priority: P2)

**Goal**: `init` seeds three roots (already via T010); `refresh` re-mirrors the union to currency;
`doctor` reports three-root drift read-only; `upgrade` reconciles it no-clobber to zero residual.

**Independent Test**: `init` → three byte-identical roots (T007 covers init). Delete a root's copies
→ `doctor` reports the drift; `upgrade` re-seeds/re-mirrors no-clobber; `refresh` re-mirrors the union.

### Tests for User Story 3 (fail-first)

- [X] T023 [P] [US3] In `tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs` add a re-mirror test:
  after a compliant scaffold, mutate/remove a mirror copy, run `refresh`, assert the union is
  re-mirrored byte-identically across all three roots and `summary.md` reports currency
  (Scenario E, FR-009, P8). Must FAIL.
- [X] T024 [P] [US3] Add a `doctor` three-root drift test (in the Doctor test module): with a product
  missing the `.agents/skills/` seeded copies (simulated pre-056), `doctor` reports the
  missing/divergent root **read-only** (zero writes, exit 0) (Scenario E, FR-010, P9). Must FAIL.
- [X] T025 [P] [US3] Add an `upgrade` reconcile test (in the Upgrade test module): the same drifted
  product → `upgrade` (confirmed) re-seeds the missing root no-clobber + re-mirrors provider copies,
  leaving zero residual drift and preserving author edits (Scenario E, SC-004, P9). Must FAIL.

### Implementation for User Story 3

- [X] T026 [US3] In `src/FS.GG.SDD.Commands/CommandWorkflow/Drift.fs`: extend `expectedArtifactPaths`
  to include `.agents/skills/{name}/SKILL.md` for every seeded name (third root), and add the
  **three-root union** drift check — for each skill in the union (seeded ∪ provenance `mirroredPaths`
  / on-disk `.agents`), all three roots present and byte-identical; missing/divergent = drift
  (research R7, data-model E7). Makes T024's detection green.
- [X] T027 [US3] In `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersRefresh.fs`: re-derive the union
  from on-disk state (canonical embedded seeded bodies + non-reserved `.agents/skills/*`) and
  re-write the `.claude`/`.codex` mirror copies (no-clobber) + re-seed any missing seeded copy in any
  root; report currency in `summary.md` (research R6, FR-009). Makes T023 green.
- [X] T028 [US3] In `HandlersDoctor.fs` and `HandlersUpgrade.fs`: consume the extended `Drift` model
  (no second source of truth). `doctor` projects the three-root drift read-only; `upgrade` reconciles
  via `init`'s `AgentGuidanceTarget` re-seed + the R6 re-mirror, each behind its existing `Confirm`
  (or `--yes`). Makes T024/T025 green (research R7, FR-010).

**Checkpoint**: The invariant holds for every verb (init/scaffold/refresh/doctor/upgrade).

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Agent-surface equivalence, release/version sequencing, docs, and full-suite validation.

- [X] T029 [P] Update Claude ⇔ Codex agent surfaces equivalently to describe the three-root union
  ownership model (Principle VII, FR-013): `.claude/skills/fs-gg-sdd-getting-started/SKILL.md`,
  `.codex/skills/fs-gg-sdd-getting-started/SKILL.md`, and `CLAUDE.md`/`AGENTS.md`. Keep wording
  byte-equivalent across Claude and Codex.
- [X] T030 [P] Add the additive **recording paragraph** to `docs/release/migrations/README.md`
  (no `<version>.md` file): additive `mirroredPaths` + `mirrored` owner, provenance stays v1, and the
  version-gated `init` third-root skeleton growth (research R9, ADR-0008).
- [X] T031 Advance the orchestrator-axis minimum CLI version and sequence the CLI release
  **before** a clean scaffold consumes it (publish-before-flip): bump the version-of-truth per the
  release process (FR-011, research R8). No provider package-id/version literal enters generic SDD.
  **DONE (separate release-alignment commit):** completed the incomplete `036d101` `0.2.1→0.3.0`
  republish that had left the version-of-truth aligned only in `Directory.Build.local.props`. The
  root cause was NOT an unbaked InformationalVersion (baking works — `0.3.0` is baked); it was stale
  hardcodes + goldens: `ReleaseContract.currentRelease` (`0.2.1`/`0.2.x`), the `SchemaVersion`
  fallback, the `release-readiness.json` baseline + docs copy, the `valid-work-item` work-model
  fixture (regenerated via the real generator — clean version/digest/`sourceIds` diff), and the
  `min-behind` coherence fixture + its assertions. Then advanced the SDD version-of-truth to
  **`0.4.0`** for FR-011 (the fan-out's version-gated seeded-surface growth, ADR-0008): bumped
  `Directory.Build.local.props`, `ReleaseContract`/`SchemaVersion`, regenerated the release goldens
  + work-model fixture via the real generators, and re-anchored the coherence fixtures to the new
  installed version (`min-equal`=`0.4.0`, `min-behind`=`0.5.0`). Full solution green at `0.4.0`:
  Contracts 64, Artifacts 149, Cli 80, Validation 18, Commands 462, 0 failures. Remaining is the
  cross-repo **publish-before-flip**: publish the `0.4.0` CLI, then Templates#47 declares
  `minimumFsggSdd: 0.4.0` — a release/coordination step, not a code gate.
- [X] T032 Run `.specify` docs-currency: `fsgg-sdd refresh`/`agents` equivalents are described in
  guidance; confirm no provider-specific package id, template id, path, or docs URL leaked into
  generic SDD (SC-005, contract "Out of scope").
- [X] T033 Full-suite green: `dotnet test FS.GG.SDD.sln` (all Scaffold/SeededSkills/Refresh/Doctor/
  Upgrade/Parity filters), and walk quickstart.md Scenarios A–F manually to confirm SC-001…SC-005.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no dependencies.
- **Phase 2 (Foundational)**: depends on Phase 1 fixtures; **BLOCKS all stories**. The FSI (T005)
  precedes its impl (T009); the fail-first tests (T006–T008) precede T009–T011.
- **Phase 3 (US2)**: depends on Phase 2 (guard clause T011).
- **Phase 4 (US1)**: depends on Phase 2 (seed seam T010, provenance T009). Independent of US2, but
  T020's mirror stage must honor the reserved-namespace skip US2 proves.
- **Phase 5 (US3)**: depends on Phase 2 (T010) and reuses US1's mirror (T020/T027 share the
  re-mirror logic — factor the pure union-mirror helper in T020 so T027 reuses it).
- **Phase 6 (Polish)**: depends on all desired stories complete.

### Within each story

- Tests (fail-first) MUST be RED before the matching `.fs` body. Never mark a failing task `[X]`.
- Pure transition/union planning before the interpreter-edge I/O (Principle V/MVU).

### Parallel opportunities

- Setup: T002/T003/T004 in parallel (different fixture dirs).
- Foundational: T006/T007/T008 in parallel (different test files) after T005.
- US2: T012/T013/T014 in parallel. US1: T016/T017/T018/T019 in parallel. US3: T023/T024/T025 in parallel.
- Polish: T029/T030 in parallel.

### Suggested MVP

**Phase 1 → Phase 2 → Phase 3 (US2) → Phase 4 (US1)** delivers the core: a compliant scaffold
fans out the byte-identical union to all three roots with a strict, symmetric guard (the reason
#55 was filed). US3 (currency for every verb) and Polish follow.

---

## Notes

- `[P]` = different files, no dependency on another incomplete task in this phase.
- Real `dotnet new` fixtures, no mocks; Scaffold tests serialized by `[<Collection("Scaffold")>]`.
- No new effect, command, outcome, or exit code — one guard clause, one reused seeding seam, one
  additive provenance field, a mirror fold over enumerated skills (plan Complexity Tracking: empty).
- Reservation asymmetry is intentional: `.claude`/`.codex` whole-root; `.agents` only the
  `fs-gg-sdd-*` namespace.
- `init` is intentionally NOT byte-identical to pre-056 (third root is version-gated growth, ADR-0008).
- Verify tests fail before implementing; commit after each task or logical group.
