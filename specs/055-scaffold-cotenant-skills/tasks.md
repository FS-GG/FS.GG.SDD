# Tasks: Scaffold co-tenant skills under the shared skill roots

**Input**: Design documents from `/specs/055-scaffold-cotenant-skills/`

**Prerequisites**: plan.md, spec.md, research.md (R1–R8), data-model.md (E1–E4 + truth
table), contracts/scaffold-intrusion-discriminator.md (P1–P8), quickstart.md (A–E).

**Tests**: Required. This is a **Tier 1 (contracted change)** to the scaffold intrusion guard
(a cross-repo integration surface). Per the constitution (Principle I/VI) tests are authored
**fail-first** and precede the one-line `.fs` narrowing. Scaffold tests drive real `dotnet new`
providers under `tests/fixtures/scaffold-provider/` (no mocks), serialized by
`[<Collection("Scaffold")>]`.

**Organization**: Grouped by user story. Phases run in sequence; `[P]` tasks within a phase
touch different files and may run in parallel.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different files)
- **[Story]**: `US1` / `US2` / `US3` (omitted for shared/foundational/polish work)
- All tasks are **Tier 1** (matches the spec tier); no `[T2]` annotations needed.
- Elmish/MVU note: the change lives entirely in the pure classifier `finalizeScaffold`; the
  `RunProcess` provider edge is untouched (Principle V). There is **no** `.fsi`, `Model`,
  `Msg`, `Effect`, schema, or public-signature change (plan §Constitution Check), so no MVU
  contract tasks are emitted — the applicable evidence is the pure truth-table + fixture tests.

---

## Phase 1: Setup / Fixtures (Shared Infrastructure)

**Purpose**: Establish the real `dotnet new` fixtures the fail-first tests depend on. No source
or test edits yet. Both fixture changes are prerequisites for Phase 2/3 tests.

- [X] T001 [P] Add the **new positive** `skills-cotenant` fixture at
  `tests/fixtures/scaffold-provider/skills-cotenant/`: `.template.config/template.json`
  (identity `FsggSdd.Fixture.SkillsCotenant`, shortName `fsgg-fixture-skills-cotenant`,
  declaring the `lifecycle` string symbol so the forwarded `--param lifecycle` is accepted, as
  in `skills-intrusion/.template.config/template.json`); `.claude/skills/fs-gg-elmish/SKILL.md`
  (a non-reserved co-tenant skill body); and an ordinary product file `app.txt`. (Scenario A;
  FR-001)
- [X] T002 [P] Add the registry
  `tests/fixtures/scaffold-provider/registries/skills-cotenant.providers.yml` (schemaVersion 1,
  one provider `fixture`, contractVersion `1.0.0`, templateId
  `fsgg-fixture-skills-cotenant`, `source: __FIXTURE__/skills-cotenant`, parameter
  `lifecycle` required:false), modeled on `skills-intrusion.providers.yml`. (Scenario A)
- [X] T003 [P] **Re-point** the negative `skills-intrusion` fixture (FR-009, research R3):
  rename `tests/fixtures/scaffold-provider/skills-intrusion/.claude/skills/leak/` →
  `.claude/skills/fs-gg-sdd-custom/` and `.codex/skills/leak/` →
  `.codex/skills/fs-gg-sdd-custom/` (keep `SKILL.md` under each; keep `app.txt` and
  `.template.config/template.json`). The new path must be a **new** reserved-namespace name
  (`fs-gg-sdd-custom`), never an already-seeded name like `fs-gg-sdd-plan` — a write to a seeded
  path is subtracted by `skeletonFiles` and would never reach the guard (plan Correctness note).
- [X] T004 Update the comment in
  `tests/fixtures/scaffold-provider/registries/skills-intrusion.providers.yml` to describe the
  re-pointed target (`.{claude,codex}/skills/fs-gg-sdd-custom/`, this feature 055) instead of
  the old whole-root `leak` shape. (Depends on T003; same fixture family.)

**Checkpoint**: Fixtures present. `skills-cotenant` currently makes scaffold **block** (the
guard over-matches); `skills-intrusion` now targets a reserved-namespace path.

---

## Phase 2: Foundational — Fail-first truth-table oracle

**Purpose**: Pin the narrowed classifier's oracle (data-model E3 truth table) as a pure unit
test **before** touching any fixture-driven behavior. This test is the R1/R5 correctness gate
and blocks nothing else, but is authored first so the discriminator semantics are fixed.

**⚠️ CRITICAL**: Write this test to FAIL against the current whole-root `isSddTree`, then it
goes green with the Phase 5 narrowing.

- [X] T005 [US1][US2] Add an `isSddTree` / `isSddOwned` reserved-namespace **truth-table unit
  test** in `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs` (or a focused sibling
  module), asserting every row of data-model.md E3: `.fsgg/x`·`work/x`·`readiness/x` → SDD-owned;
  `.claude/skills/fs-gg-sdd-plan/…`, `.claude/skills/fs-gg-sdd-custom/…`,
  `.codex/skills/fs-gg-sdd-anything/…` → reserved (true); `.claude/skills/fs-gg-elmish/…` and
  bare `.claude/skills/leak` → **false** (product); `AGENTS.md`/`CLAUDE.md` → skeleton
  (`isSddTree=false`, `isSddOwned=true`, unchanged). **Access**: `HandlersScaffold.isSddTree`/
  `isSddOwned` are members of `[<AutoOpen>] module internal HandlersScaffold` and are reachable
  from this test project via the **existing** `InternalsVisibleTo("FS.GG.SDD.Commands.Tests")` in
  `FS.GG.SDD.Commands.fsproj` — no new seam or `.fsi` needed. Covers both the US1 co-tenant rows
  (elmish/leak → product, FR-001) and the US2 reserved rows (FR-002). Must FAIL now (the
  elmish/leak rows classify as SDD tree today). (contracts P1–P4; research R1/R5)

**Checkpoint**: The discriminator oracle is red and unambiguous.

---

## Phase 3: User Story 1 — Compliant provider scaffold completes with co-tenant skills (P1) 🎯 MVP

**Goal**: A scaffold whose only shared-skill-root writes are non-reserved co-tenant skills
completes (exit 0, `providerSucceeded`), lands the provider skill as `generatedProduct`, and
leaves the seeded `fs-gg-sdd-*` skills byte-identical.

**Independent Test**: Run scaffold against the `skills-cotenant` fixture; confirm exit 0, no
`scaffold.providerWroteSddTree`, `.claude/skills/fs-gg-elmish/SKILL.md` present and listed as
produced product, seeded `fs-gg-sdd-*` skills present and absent from produced paths.

### Tests for User Story 1 (fail-first) ⚠️

- [X] T006 [US1] Add the co-tenant **success test** `scaffold provider writing a co-tenant skill
  succeeds` in `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs` (uses the T001/T002
  `skills-cotenant` fixture): assert exit **0**, outcome `providerSucceeded`, **no**
  `scaffold.providerWroteSddTree` diagnostic, `.claude/skills/fs-gg-elmish/SKILL.md` on disk and
  present in `producedPaths` with `Owner = generatedProduct`, and that the seeded
  `fs-gg-sdd-*` skill paths are **absent** from `producedPaths`. Must FAIL now (currently
  blocks). One non-reserved co-tenant skill (`fs-gg-elmish`) is the **representative** for the
  class — it proves SC-001's win without needing to reproduce the real provider's literal 8
  leaked paths. (Scenario A; FR-001/004/006, SC-001)
- [X] T007 [P] [US1] Add the **seeded byte-identity** assertion (Scenario B) — after a
  `skills-cotenant` scaffold, each seeded `.claude/skills/fs-gg-sdd-*/SKILL.md` and
  `.codex/skills/fs-gg-sdd-*/SKILL.md` is byte-identical to what `fsgg-sdd init` seeds, and the
  only extra tree in the scaffolded root is `fs-gg-elmish`. Place alongside T006 in
  `ScaffoldCommandTests.fs`. (FR-005/010, SC-002)
- [X] T008 [P] [US1] Add a **co-tenant produced-path parity** case in
  `tests/FS.GG.SDD.Cli.Tests/ScaffoldParityTests.fs`: the `.claude/skills/fs-gg-elmish/SKILL.md`
  produced path renders identically across json ≡ text ≡ rich (listed under produced product,
  never as SDD-owned). (Scenario E; FR-004/008, SC-004)
  NOTE: this is a **projection** parity guard over a constructed report; the three projections
  do not consult `isSddTree`, so it passes structurally (green) rather than fail-first-red — the
  fail-first behavioral evidence for the narrowing is T005/T006 (real fixtures). Verified green.

**Checkpoint**: US1 is fully red and pins success + byte-identity + parity. This is the MVP —
once the Phase 5 narrowing lands, US1 goes green on its own.

---

## Phase 4: User Story 2 — Genuine SDD-tree intrusion is still rejected (P1)

**Goal**: Narrowing the guard must not open a hole — reserved-namespace and other-SDD-tree
writes still fail at exit 2, symmetric across both roots.

**Independent Test**: Run scaffold against the re-pointed `skills-intrusion` fixture and the
existing `lifecycle-intrusion` fixture; confirm each is rejected (`providerWroteSddTree`, exit
2, `providerFailed`) and intruded paths never appear as produced product.

### Tests for User Story 2 (fail-first) ⚠️

- [X] T009 [US2] **Update the negative test** (the existing `scaffold provider writing into the
  seeded skill trees is a provider defect` in
  `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs`) to drive the re-pointed
  `skills-intrusion` fixture and expect `.claude/skills/fs-gg-sdd-custom/SKILL.md` **and**
  `.codex/skills/fs-gg-sdd-custom/SKILL.md` rejected as `scaffold.providerWroteSddTree` (exit 2,
  outcome `providerFailed`) on **both** roots, with those paths absent from `producedPaths`.
  Depends on the T003/T004 fixture re-point. This test must stay/return red until the guard
  narrows and must not accidentally pass by matching the whole root. (Scenario C; FR-002/007/009/011,
  SC-003)
- [X] T010 [P] [US2] Confirm/keep the `.fsgg/`·`work/`·`readiness/` rejection coverage green —
  verify the existing `lifecycle-intrusion` test (`scaffold provider writing SDD trees under
  lifecycle=sdd is a provider defect`) still asserts exit 2 + `providerWroteSddTree` and needs
  **no** change under the narrowed guard (regression guard for FR-003). (Scenario D; SC-003)

**Checkpoint**: The safety property is pinned; both the reserved-namespace and other-tree
intrusion paths are covered and unchanged-by-content.

---

## Phase 5: Implementation — narrow the discriminator (the ONLY source change)

**Purpose**: Make Phases 2–4 go green with a single-predicate narrowing. Do this only after the
fail-first tests exist and fail.

- [X] T011 In `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs` extract the helper
  `isReservedSkillSubtree p` as a plain module-internal `let` (a **sibling of `isSddTree`** in
  `[<AutoOpen>] module internal HandlersScaffold` — **not** `let private`, so it shares
  `isSddTree`'s visibility; data-model E1: `p.StartsWith(".claude/skills/fs-gg-sdd-", Ordinal) || p.StartsWith(".codex/skills/fs-gg-sdd-", Ordinal)`) and **replace** the two
  whole-root skills clauses of `isSddTree` (currently `HandlersScaffold.fs:58-62`,
  `.claude/skills/` / `.codex/skills/`) with `|| isReservedSkillSubtree p`. Update the adjacent
  051 comment to state the reservation is now the `fs-gg-sdd-*` namespace, not the whole root.
  Leave the `.fsgg/`·`work/`·`readiness/` arms, `isSddOwned`, `collisionPaths`, the partition
  (`:376-377`), and the provenance stamp (`:277`) untouched — they inherit the narrowing (R5/R8).
  This is the **only** behavioral change in the feature.
- [X] T012 Run the feature test suites and confirm green:
  `dotnet test tests/FS.GG.SDD.Commands.Tests --filter FullyQualifiedName~Scaffold` and
  `dotnet test tests/FS.GG.SDD.Cli.Tests --filter FullyQualifiedName~ScaffoldParity`
  (T005–T010 now pass). Then run the full `dotnet build FS.GG.SDD.sln` + targeted suites to
  confirm no regression (no `.fsi`/baseline/schema drift). (Depends on T011.)

**Checkpoint**: US1 (success/byte-identity/parity) and US2 (both intrusion paths) green off one
predicate change.

---

## Phase 6: User Story 3 — Auditable, unchanged report/provenance contract (P2)

**Goal**: Prove the change is additive — provenance stays schema v1, json contract gains only
produced-path entries, no removed/reshaped fields.

**Independent Test**: Diff json report + provenance for an equivalent scaffold before/after;
only additive produced-path entries; `schemaVersion` still `1`.

- [X] T013 [US3] Add a **provenance schema-v1 guard** for the co-tenant scaffold — assert
  `.fsgg/scaffold-provenance.json` `schemaVersion == 1`, the `fs-gg-elmish` co-tenant path is
  recorded with `Owner = generatedProduct`, and no seeded `fs-gg-sdd-*` path appears in
  provenance. Place in `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs` (or the existing
  provenance test module). (contracts P5/P6/P7; FR-004/008, SC-004)
- [X] T014 [P] [US3] Verify the additive-only property against the golden/pre-change output for
  an equivalent scaffold — the only json/provenance differences are the additional co-tenant
  produced-path entries; no field removed, renamed, or reshaped. Extend the existing scaffold
  golden/parity coverage rather than adding a new schema. (Scenario E; FR-008, SC-004)
  DONE via the same `ScaffoldParityTests` case as T008 (asserts the rich path changes **no JSON
  byte** and the co-tenant path flows through the existing `producedPaths` projection with no new
  or reshaped field) plus T013's provenance schema-v1 guard. No new schema added.

**Checkpoint**: The versioned cross-repo surface is proven additive; downstream consumers need
no re-pin.

---

## Phase 7: Polish — docs & aligned agent surfaces

**Purpose**: Migration note and Claude ⇔ Codex ownership-wording alignment. No behavioral code.

- [X] T015 [P] Add a migration note under `docs/release/migrations/`:
  narrowed scaffold intrusion guard — providers may now co-populate `.claude/skills/` /
  `.codex/skills/` outside the reserved `fs-gg-sdd-*` namespace; **additive only**, provenance
  stays v1, `init` byte-identical, exit-code taxonomy unchanged. (FR-008/010)
  NOTE: implemented as a **recording paragraph in `docs/release/migrations/README.md`** (the
  established pattern for 050/052/053/054), **not** a `TEMPLATE.md`-derived `<version>.md` file.
  The repo's migration-note obligation is explicit that an **additive-only** release MUST NOT
  carry a `<version>.md` note (its `release-readiness.json` `migrations[]` stays empty); a
  spurious version file would itself violate the policy. Feature 055 is additive, so the README
  paragraph is the policy-correct home.
- [X] T016 [P] Align the **ownership wording** across both agent surfaces so it says the
  `fs-gg-sdd-*` skill **subtrees** are SDD-owned (a reserved namespace), not the whole
  `.claude/skills/`·`.codex/skills/` root — **only where current wording overclaims**:
  `CLAUDE.md`, `AGENTS.md`, and `.claude/skills/fs-gg-sdd-getting-started/SKILL.md` +
  `.codex/skills/fs-gg-sdd-getting-started/SKILL.md` (kept equivalent Claude ⇔ Codex).
  (Principle VII; FR-002)
- [X] T017 Run `quickstart.md` scenarios A–E end to end as the final acceptance pass; confirm
  the mapped tests (quickstart "Mapped tests" table) all pass and the exit-code/diagnostic
  expectations hold. (Depends on Phase 5/6.)

---

## Dependencies & Execution Order

### Phase order (sequential)

1. **Phase 1 (Fixtures)** — no code deps; T001/T002/T003 are `[P]`; T004 depends on T003.
2. **Phase 2 (truth-table)** — pure unit oracle; independent of fixtures, authored fail-first.
3. **Phase 3 (US1 tests)** — depend on T001/T002 (positive fixture).
4. **Phase 4 (US2 tests)** — T009 depends on T003/T004 (re-pointed fixture); T010 is a
   no-change regression guard.
5. **Phase 5 (impl T011)** — the single narrowing; requires Phases 2–4 red. T012 verifies green.
6. **Phase 6 (US3)** — provenance/additive proofs; depend on T011.
7. **Phase 7 (polish)** — docs/agent wording; T017 final acceptance after Phases 5–6.

### Critical path

T001/T002/T003 → T004 → (T005, T006, T007, T008, T009, T010 authored red) → **T011** → T012 →
(T013, T014) → (T015, T016) → T017.

### Parallel opportunities

- Phase 1: T001, T002, T003 in parallel (different files); T004 after T003.
- Phase 3: T007, T008 parallel with each other; T006 is the primary US1 test they build on.
- Phase 4: T010 parallel with T009.
- Phase 6: T014 parallel with T013.
- Phase 7: T015, T016 parallel; T017 last.

---

## Implementation Strategy

### MVP (User Story 1)

Phase 1 fixtures → Phase 2 oracle → Phase 3 US1 tests (red) → Phase 5 narrowing (T011) →
US1 green. That alone unblocks the compliant `fs-gg-ui` scaffold (SC-001) end to end.

### Full delivery

Add US2 (safety, Phase 4) and US3 (additive contract, Phase 6) around the same single T011
change — all three stories are satisfied by one predicate narrowing plus their tests — then
land the docs + agent-surface alignment (Phase 7).

---

## Notes

- **One behavioral change**: T011 only. Everything else is fixtures, tests, docs, and wording.
- Fail-first is real here: T005/T006/T008 must be red against today's whole-root guard; T009
  must reject the *new* reserved path (not a subtracted seeded path — plan Correctness note).
- **Out of scope (no task):** an in-place rewrite of an *already-seeded* `fs-gg-sdd-*` skill at
  its existing path is not diff-visible (subtracted by `skeletonFiles` before the partition), so
  the intrusion guard does not surface it — the seeded skills' clobber protection is the
  `AgentGuidanceTarget` no-clobber write-kind, unchanged here (spec Edge Cases; contract
  Out-of-scope). This is precisely why T003/T009 target a *new* `fs-gg-sdd-custom` path.
- No `.fsi`, `PublicSurface.baseline`, persisted-schema, or provenance-schema change; provenance
  stays **v1**; `init` byte-identical (verified by T007).
- **Cross-repo**: no versioned contract-surface change (behavioral coherence only). Recording
  the co-tenant ownership model in the coherence set / ADR is the follow-up tracked by
  **FS-GG/FS.GG.Templates#47**; close **FS-GG/FS.GG.SDD#55** at merge via
  `cross-repo-coordination`. Unblocks **FS.GG.Rendering Feature 219**.
- Never mark a failing task `[X]`; never weaken an assertion to green a build.
- **Pre-existing, out-of-feature failures (disclosed):** two `ScaffoldCliCoherenceTests`
  (`behind minimum emits exactly one cliBehindMinimum…` and `…reseedSeededSkills next-action…`)
  fail on a clean tree independent of this feature — an artifact of the `0.2.1 → 0.3.0` CLI
  version bump (installed == declared minimum, so no behind-minimum advisory), and 17
  `… CLI … smoke` tests (analyze/evidence/ship/agents/verify/refresh) fail because they need the
  packed CLI tool absent from this environment. Confirmed by stashing all feature changes. Feature
  055 introduces **zero** new failures; all its own suites (Scaffold, ScaffoldParity, SeededSkills,
  Drift, SurfaceBaseline, PublicSurface) are green.
