---
description: "Task list for Governance Readiness Handoff Contract"
---

# Tasks: Governance Readiness Handoff Contract

## 📊 Progress — ✅ 23 / 23 complete (100%)

> Legend: ✅ done · 🟡 in progress · ⬜ not started · ⏭️ skipped
> Validated 2026-06-20: full suite **336 passing / 0 failed** (83 Artifacts + 253 Commands;
> +18 new `GovernanceHandoffTests`). Evidence under `readiness/`.

| Phase | Tasks | Status |
|---|---|---|
| 1 · Setup & grounding | T001–T002 | ✅✅ |
| 2 · Foundational (FSI surface + projection skeleton + manifest kind) | T003–T007 | ✅✅✅✅✅ |
| 3 · US1 root handoff + ship emission (P1, MVP) | T008–T010 | ✅✅✅ |
| 4 · US2 declared evidence nodes + edges (P1) | T011–T012 | ✅✅ |
| 5 · US3 governed references + config presence (P2) | T013–T014 | ✅✅ |
| 6 · US4 merge-boundary readiness facts (P2) | T015–T016 | ✅✅ |
| 7 · US5 currency + refresh (P3) | T017–T018 | ✅✅ |
| 8 · Polish, supersession & validation | T019–T023 | ✅✅✅✅✅ |

### Implementation deviations (forced by the real codebase; boundary unchanged)

- **T003 signature** — the contract's
  `fromWorkModel : WorkModel -> ShipSummary -> VerificationSummary -> …` cannot live in
  `FS.GG.SDD.Artifacts` because `ShipSummary`/`VerificationSummary` live in
  `FS.GG.SDD.Commands`, which depends on Artifacts (a circular reference). Resolved by
  having Commands pre-extract Artifacts-native `ReadinessFacts` / `GovernanceConfigPresence`
  (built by parsing the SDD-owned `ship.json`) and pass them to a pure
  `fromWorkModel : WorkModel -> SourceIdentity list -> GovernanceConfigPresence -> ReadinessFacts -> GeneratorVersion -> GovernanceHandoff`.
- **T007 baseline** — the module surface lands in the **Artifacts** public-surface baseline
  (`tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline`), which is where the reflection
  test covers the `FS.GG.SDD.Artifacts` namespace; the Commands baseline is unaffected
  (all new Commands helpers stay private behind `CommandWorkflow.fsi`).
- **T019 supersession** — done as a documented **pointer** at the `.fsi` definitions of
  `GovernanceCompatibility` and `GovernanceCompatibilityFact` (the handoff is now the single
  Governance-facing source). Structural removal was deliberately deferred to avoid
  destabilizing the report/serialization contract, which is governed by separate specs.

**Input**: Design documents from `specs/017-governance-handoff/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md,
contracts/integration-requirements.md, contracts/governance-handoff.md, quickstart.md

**Change Tier**: Tier 1 (contracted change: introduces the first explicit,
versioned, optional SDD-owned contract *consumed by* FS.GG.Governance). All
phases match the spec's overall tier; no per-task `[T1]`/`[T2]` annotations are
needed.

**Nature of this feature**: This feature **adds a new public F# surface** (one
pure projection module `FS.GG.SDD.Artifacts/GovernanceHandoff.fsi/.fs`), a new
generated-view schema (`readiness/<id>/governance-handoff.json`), and a new
`GenerationManifest.GeneratedViewKind` case, and **supersedes** the advisory
`GovernanceCompatibility` placeholders. It adds **no** new lifecycle stage,
**no** new command, and changes **no** authored-source schema. The projection is
a pure total fold over the existing normalized `WorkModel` + verify/ship
readiness; emission reuses the existing `ship`/`refresh` MVU command workflow and
effect interpreter.

**Tests**: Test tasks here are first-class spec deliverables (Constitution VI;
FR-004/005/009/012, SC-002..SC-007 are *defined* by assertions —
determinism, declared-states-only, boundary-exclusion, stale/refresh,
byte-identity). They are core implementation, not optional scaffolding.

**Elmish/MVU applicability (Principle IV/V)**: The projection is **pure** (records
+ a total fold over `WorkModel` → canonical JSON) — no new MVU. The handoff is
emitted/regenerated through the **existing** `ship`/`refresh` `Model`/`Msg`/
`Effect`/`update` boundary and effect interpreter; no new stateful or I/O
workflow is introduced. No separate MVU-contract task is needed beyond the
additive emission wiring (T009, T017).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on another
  incomplete task in this phase)
- **[Story]**: `US1`..`US5`; unlabeled tasks are shared/cross-cutting
- Paths are repository-relative from `/home/developer/projects/FS.GG.SDD`

---

## Phase 1: Setup & Grounding (Shared Infrastructure)

**Purpose**: Confirm the regression baseline and pin the exact producer/consumer
shapes the projection folds over, so the field mapping matches real types rather
than the contract's prose.

- [X] T001 Confirm the full test suite is green at baseline by running
  `dotnet test FS.GG.SDD.sln` and recording the pass count; this is the
  regression baseline the new module and tests must preserve. Note the current
  `PublicSurface.baseline` is clean so any later diff is attributable to the new
  surface only.
- [X] T002 [P] Ground the projection inputs against the real signatures and
  record a short grounding appendix in `specs/017-governance-handoff/research.md`
  (scratch grounding, not a shipped artifact): read
  `src/FS.GG.SDD.Artifacts/WorkModel.fsi` (`EvidenceEntry` — `Id`, `Result`,
  `Synthetic`, `Rationale`, `TaskRefs`; `TaskEntry` — `Id`, `Status`,
  `Dependencies`, `RequiredEvidence`; `GovernanceBoundaryEntry` — `Path`,
  `Owner`, `Relationship`), `src/FS.GG.SDD.Artifacts/GenerationManifest.fsi`
  (`SourceIdentity`, `GeneratedViewKind`, `viewKindValue`, `isStale`,
  `expectedSummaryOutputPath`, manifest build/serialize),
  `src/FS.GG.SDD.Commands/CommandTypes.fsi` (`ShipSummary` —
  `Disposition`/`VerificationReadiness`/counts/blocking ids,
  `VerificationSummary`, `GovernanceCompatibilityFact`, `ArtifactChange`), and the
  canonical `src/FS.GG.SDD.Artifacts/Serialization.fsi` writer. Capture the
  **exact `EvidenceEntry.Result` token vocabulary** actually produced by the
  evidence command, and reconcile it against the D2 mapping table in
  `contracts/integration-requirements.md`; flag any token the table does not
  cover so T011's mapping stays total.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish the FSI-first public surface, the new generated-view kind,
and a compiling projection skeleton that every story extends and asserts over.

**⚠️ CRITICAL**: No user story can be implemented until the `.fsi` contract
(T003), the manifest view kind (T004), the compiling projection skeleton (T005),
and the test scaffold (T006) exist — every story fills the same
`GovernanceHandoff.fs` fold and the same `GovernanceHandoffTests.fs` file.

- [X] T003 Author `src/FS.GG.SDD.Artifacts/GovernanceHandoff.fsi` (FSI-first,
  Principle I/III) with the full public surface per `data-model.md`: the
  `GovernanceHandoff` root record (`SchemaVersion`, `ContractVersion`,
  `GeneratorVersion`, `WorkId`, `Sources`, `Evidence`, `GovernedReferences`,
  `GovernanceConfig`, `Readiness`, `Diagnostics`); `EvidenceProjection`
  (`Nodes`/`Dependencies`); `EvidenceNode` (`Id`, `State`, `Rationale`);
  the closed `DeclaredEvidenceState` DU of the **five declared** states only
  (`Pending`/`Real`/`Synthetic`/`Failed`/`Skipped` — **no** `AutoSynthetic`);
  `EvidenceEdge` (`Dependent`/`Dependency`); `GovernedReference` (`Path`, `Owner`,
  `Relationship`, `Kind`, `Operation`); `GovernanceConfigPresence`;
  `ReadinessFacts`; and the signatures
  `fromWorkModel : WorkModel -> ShipSummary -> VerificationSummary -> GovernanceConfigPresence -> GeneratorVersion -> GovernanceHandoff`,
  `toJson : GovernanceHandoff -> string`, and the total evidence-state mapping
  `val` it exposes for tests. Register `GovernanceHandoff.fsi`/`.fs` in
  `src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj` **after** `WorkModel.fs`
  and `GenerationManifest.fs` (it depends on both) and before
  `LifecycleRuleContracts`.
- [X] T004 Add the new generated-view kind in
  `src/FS.GG.SDD.Artifacts/GenerationManifest.fsi`/`.fs`: a `GovernanceHandoff`
  case on `GeneratedViewKind`, its `viewKindValue` token
  (`"governance-handoff"`), and
  `expectedGovernanceHandoffOutputPath : workId:string -> string` →
  `readiness/<id>/governance-handoff.json`. Reuse the existing `SourceIdentity`
  digest comparison and `isStale` unchanged (no new currency logic). Update the
  manifest serialization to round-trip the new case (in `GenerationManifest.fs`
  and any `CommandSerialization` manifest writer that enumerates kinds).
- [X] T005 Implement a compiling skeleton `src/FS.GG.SDD.Artifacts/GovernanceHandoff.fs`
  satisfying the `.fsi`: define the records and the `DeclaredEvidenceState` DU,
  and a **total** `fromWorkModel` that builds the envelope
  (`SchemaVersion = 1`, `ContractVersion = "1.0.0"`, `GeneratorVersion`, `WorkId`,
  `Sources`) with empty-but-valid `Evidence`/`GovernedReferences`/`Readiness`/
  `Diagnostics` and the passed-in `GovernanceConfig`, plus `toJson` via the canonical
  `Serialization` module (fixed key order, no clocks/durations/host paths/ANSI).
  The build must be green; later story tasks fill the empty slices.
- [X] T006 Scaffold `tests/FS.GG.SDD.Commands.Tests/GovernanceHandoffTests.fs`
  (module header, `open` of xUnit + `TestSupport`) and register it in
  `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj` immediately
  before `SurfaceBaselineTests.fs`. Reuse the existing `TestSupport`
  `runShip`/`runRefresh` and disposable-project helpers unchanged.
- [X] T007 Update the public surface baseline
  `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` for the new
  `GovernanceHandoff` module surface and the `GovernanceHandoff`
  `GeneratedViewKind` case, with **no unrelated baseline churn**; capture an FSI
  public-surface transcript that loads the module and exercises
  `fromWorkModel`/`toJson` over a tiny work model, saved as
  `specs/017-governance-handoff/readiness/fsi-surface.txt` (Constitution I/III
  evidence). Confirm `SurfaceBaselineTests` is green.

**Checkpoint**: The handoff type contract, view kind, and a deterministic empty
projection compile and serialize; all stories can now extend one fold and assert
over one file.

---

## Phase 3: User Story 1 - Project SDD Readiness Into a Versioned Governance Handoff (Priority: P1) 🎯 MVP

**Goal**: `fsgg-sdd ship` emits a deterministic, schema-versioned
`readiness/<id>/governance-handoff.json` envelope — schema/contract/generator
versions, contributing sources + digests — produced with no Governance runtime
and leaving authored sources byte-identical.

**Independent Test**: Advance a disposable work item through `ship` and confirm
the handoff file exists with `schemaVersion: 1`, `contractVersion: "1.0.0"`, a
`generatorVersion`, and `sources[]` with digests; that producing it required no
Governance runtime; and that it is byte-identical across two productions and
leaves every authored source unchanged (AC1–AC3, SC-002, SC-003, SC-007).

- [X] T008 [US1] Implement the envelope projection in
  `src/FS.GG.SDD.Artifacts/GovernanceHandoff.fs`: populate `Sources` from
  `GenerationManifest.SourceIdentity` for the work model and verify/ship readiness
  views (path + digest + schema version), set the constant schema/contract
  versions and `WorkId`, and default `GovernanceConfig` to all-`false` with
  omitted pointers (FR-011 baseline; US3 enriches presence). Carry the work
  model's existing diagnostics into the root `Diagnostics` verbatim,
  deterministically ordered by id (the prose/structured-conflict and
  evidence-cycle edge cases are surfaced, not resolved; data-model invariant 7).
  Keep ordering deterministic (FR-004).
- [X] T009 [US1] Wire additive ship emission in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`: in the `ship` path, after
  merge-boundary readiness is computed, project the handoff and write
  `readiness/<id>/governance-handoff.json` through the **existing** effect
  interpreter, recording a `GenerationManifest` of kind `GovernanceHandoff`
  (mirroring how `work-model.json`/`summary.md` are emitted). Surface a handoff
  summary in `CommandReport` via `CommandTypes.fsi`/`.fs`, `CommandReports.fs`,
  and `CommandSerialization.fs` (deterministic). Emission MUST NOT change command
  behavior or output when `.fsgg` Governance config is absent (FR-010).
- [X] T010 [US1] Add the envelope, no-Governance, determinism, and byte-identity
  assertions to `tests/FS.GG.SDD.Commands.Tests/GovernanceHandoffTests.fs`: after
  `runShip`, the file exists and carries `schemaVersion`/`contractVersion`/
  `generatorVersion`/`sources[]`+digests (AC1); a run with no
  `.fsgg/policy.yml`/`capabilities.yml`/`tooling.yml` succeeds with
  `governanceConfig` all-`false` and no pointers (AC3, SC-002); two productions
  over an identical source tree are byte-identical after temp-root path
  normalization (AC2, SC-003); and all authored sources (`.fsgg/*`,
  `work/<id>/*`) are byte-identical before/after `ship` (SC-007).

**Checkpoint**: `ship` emits a deterministic, declared-only handoff envelope with
no Governance present — the shippable MVP seam.

---

## Phase 4: User Story 2 - Carry Declared Evidence States and Dependency Edges, Not Computed Taint (Priority: P1)

**Goal**: The handoff carries each declared evidence state verbatim and the
evidence/task dependency edges in the `Kernel.Evidence.build` shape, computing
**no** effective/tainted state.

**Independent Test**: Build a work item whose declared evidence mixes real,
synthetic, pending, failed, skipped, and deferred entries with dependency edges;
confirm each declared state and edge is carried verbatim, no
effective/`autoSynthetic` state appears, and every edge endpoint is present in
`nodes` (AC1–AC3, SC-004, SC-005).

> Edits the same `GovernanceHandoff.fs` fold (T011) and the same test file (T012)
> as US1, so it runs **after** US1 in this phase ordering, not in parallel with it.

- [X] T011 [US2] Implement the evidence projection in
  `src/FS.GG.SDD.Artifacts/GovernanceHandoff.fs`: the **total** evidence-state
  mapping per the D2 table in `contracts/integration-requirements.md`
  (`Synthetic = true` dominates → `synthetic`; supported/passed/real/verified →
  `real`; deferred/accepted-deferral → `skipped` with `Rationale` carried;
  missing/none/not-started → `pending`; failed/invalid → `failed`; `stale` → base
  state **plus** a `staleEvidence` diagnostic appended to the root `Diagnostics`
  (alongside the carried-through work-model diagnostics from T008) — never an
  `AutoSynthetic` token);
  build the unified node set (`evidence:<EvidenceId>`, `task:<TaskId>`) with task
  state from `TaskEntry.Status` (done→`real`, blocked/failed→`failed`,
  todo/in-progress→`pending`); derive edges `evidence:<e>→task:<t>` from
  `EvidenceEntry.TaskRefs`, `task:<t>→task:<d>` from `TaskEntry.Dependencies`, and
  `task:<t>→evidence:<e>` from `TaskEntry.RequiredEvidence`; emit nodes/edges in
  deterministic id order with every edge endpoint guaranteed present in `nodes`.
- [X] T012 [US2] Add the evidence assertions to `GovernanceHandoffTests.fs`:
  mixed declared states are carried verbatim with stable namespaced identity
  (AC1); dependency edges are projected as directed `(dependent, dependency)`
  pairs (AC2); a synthetic declaration with a real dependent reports the dependent
  as its declared `real` state and **does not** taint it (AC3); the mapping table
  is exhaustively covered (every SDD `Result`/`Synthetic` combination from the
  T002 grounding maps to exactly one declared token, SC-004); no `"autoSynthetic"`
  token appears (SC-005); every edge endpoint resolves to a node (invariant 6);
  and a work item whose normalized model carries a prose/structured-conflict
  diagnostic and one whose declared evidence forms a dependency cycle each surface
  that existing diagnostic verbatim in the handoff `diagnostics[]` — the edges are
  carried as-is and SDD does **not** pre-reject the cycle (spec edge cases;
  data-model invariant 7).

**Checkpoint**: The single highest-value Governance input — declared evidence
nodes + edges — is projected verbatim with the taint boundary held on the SDD
side.

---

## Phase 5: User Story 3 - Project Governed-Boundary and Routing References Without Deciding Routes (Priority: P2)

**Goal**: The handoff references the work item's changed/governed artifacts and
the project's `.fsgg` policy/capability/tooling pointers, selecting **no** route,
profile, or gate.

**Independent Test**: Produce a handoff for a work item with changed artifacts and
declared governed-boundary references; confirm they are listed as routing
references with stable identity, `.fsgg` pointers are referenced when present and
reported absent when not, and no route/profile/gate/severity/enforcement appears
(AC1–AC3, SC-005).

- [X] T013 [US3] Implement `GovernedReferences` and `GovernanceConfig` presence in
  `src/FS.GG.SDD.Artifacts/GovernanceHandoff.fs`: project
  `WorkModel.GovernanceBoundaries` (+ `CommandTypes.ArtifactChange` `Kind`/
  `Operation` when available) into `GovernedReference` (`Path` normalized
  forward-slash repo-relative, `Owner`, `Relationship`, optional `Kind`/
  `Operation`), deterministically ordered by path; detect `.fsgg/policy.yml`,
  `.fsgg/capabilities.yml`, `.fsgg/tooling.yml` presence and set each
  `*Present`/`*Pointer` (pointer omitted when absent, FR-011). Presence detection
  is a path check only — SDD never parses Governance config semantics.
- [X] T014 [US3] Add the routing-reference and boundary-exclusion assertions to
  `GovernanceHandoffTests.fs`: changed artifacts are listed as references with
  stable identity (AC1); `.fsgg` pointers are referenced when the files are
  present and their absence is reported without failure when not (AC2); and the
  handoff contains **no** selected route, profile, gate id, matched glob,
  capability verdict, severity, or enforcement decision (AC3, SC-005 —
  boundary-exclusion over the serialized JSON).

**Checkpoint**: Governance can route from explicit, work-item-scoped references
without SDD ever selecting a route.

---

## Phase 6: User Story 4 - Carry Merge-Boundary Readiness as Advisory Facts, Not a Verdict (Priority: P2)

**Goal**: The handoff summarizes the SDD-owned merge-boundary readiness — ship
disposition, advisory/warning/blocking counts, per-view currency, and blocking
diagnostic ids — as declared advisory facts, asserting **no** pass/fail verdict.

**Independent Test**: Produce a handoff for both a ship-ready and a ship-blocked
work item and confirm each carries the SDD disposition and blocking diagnostic ids
as advisory facts, neither refuses production, and neither asserts an enforcement
verdict (AC1–AC3).

- [X] T015 [US4] Implement `ReadinessFacts` in
  `src/FS.GG.SDD.Artifacts/GovernanceHandoff.fs` from `ShipSummary` /
  `VerificationSummary`: `ShipDisposition`, `VerificationReadiness`,
  advisory/warning/blocking `counts`, `BlockingDiagnosticIds`, and `PerViewState`
  — `(view, currency)` for each contributing generated view (`work-model.json`,
  `verify.json`, `ship.json`) from the standard `SourceIdentity` digest comparison,
  ordered by view name. These compose already-computed ship readiness;
  add **no** new readiness logic and emit no severity/pass-fail token.
- [X] T016 [US4] Add the readiness assertions to `GovernanceHandoffTests.fs`: a
  ship-ready item carries its disposition and zero blocking ids (AC1); a
  ship-blocked item carries the blocking diagnostic ids and disposition **without
  refusing to produce the handoff** and without an enforcement verdict (AC2; this
  is also the "work item not yet at ship readiness" edge-case path — the
  disposition reflects the incomplete stage and never asserts unreached ship
  readiness);
  readiness fields are present as advisory facts and no pass/fail/severity verdict
  token appears (AC3, boundary-exclusion).

**Checkpoint**: SDD's merge-boundary readiness is a structured advisory input a
Governance fence can act on — never a verdict SDD claims to have enforced.

---

## Phase 7: User Story 5 - Detect Stale and Currency-Report the Handoff View (Priority: P3)

**Goal**: The handoff is a generated view whose currency comes from regeneration —
reported stale when its sources change, restored by `refresh`, and reported
absent/stale when missing rather than presumed current.

**Independent Test**: Produce a handoff, modify a contributing source so digests
change, confirm it is reported stale; `refresh` and confirm it is reported current
with updated source digests; a missing handoff for a ship-ready item is reported
stale/absent (AC1–AC3, SC-006).

> Reuses the established `GenerationManifest` currency + `refresh` machinery; the
> view kind and digest comparison come from T004. Edits `CommandWorkflow.fs`
> after T009.

- [X] T017 [US5] Wire additive refresh regeneration in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`: in the `refresh` path, regenerate
  `readiness/<id>/governance-handoff.json` and report its currency
  (`current`/`missing`/`stale`/`malformed`) via the standard `SourceIdentity`
  digest comparison for kind `GovernanceHandoff`, alongside the other generated
  views. `refresh` MUST only write generated views, never an authored source
  (FR-015).
- [X] T018 [US5] Add the currency assertions to `GovernanceHandoffTests.fs`:
  after a contributing source changes, the handoff is reported `stale` against the
  changed digest (AC1); `runRefresh` regenerates it to `current` with updated
  `sources[]` digests (AC2); a missing handoff for a ship-ready item is reported
  stale/absent, not silently current (AC3, SC-006); and authored sources stay
  byte-identical across `refresh` (SC-007).

**Checkpoint**: A present handoff file is never mistaken for a current one; the
refresh path keeps it accurate.

---

## Phase 8: Polish, Supersession & Validation (Cross-Cutting)

**Purpose**: Retire the advisory placeholders the handoff replaces, capture
executable-path evidence, and run final validation. These touch shared
contract/report files and run after the per-story projection slices exist.

- [X] T019 Supersede the advisory Governance-compatibility placeholders (FR-013,
  research D7): reduce or repoint `GovernanceCompatibility`
  (`RouteAware`/`ProfileAware`/`FreshnessAware`/`EnforceableBySdd`) in
  `src/FS.GG.SDD.Artifacts/LifecycleRuleContracts.fsi`/`.fs` and the per-command
  `GovernanceCompatibilityFact` in `src/FS.GG.SDD.Commands/CommandTypes.fsi`/`.fs`,
  `CommandReports.fs`, and `CommandSerialization.fs` so the concrete handoff is
  the single Governance-facing source (Constitution VII), keeping any retained
  surface as a pointer to the handoff rather than a parallel approximation. Update
  the `.fsi` signatures and `PublicSurface.baseline` accordingly; keep dependent
  command tests green.
- [X] T020 [P] Capture CLI process evidence (Constitution VI): run the shipped
  `FS.GG.SDD.Cli` executable `ship` then `refresh` over a disposable directory
  with JSON output, and save the transcript plus the produced
  `governance-handoff.json` as
  `specs/017-governance-handoff/readiness/cli-smoke.txt` to prove the real
  executable path (not just in-process assertions).
- [X] T021 [P] Confirm the final `PublicSurface.baseline` reflects exactly the new
  `GovernanceHandoff` module, the `GeneratedViewKind` case, the `CommandReport`
  handoff-summary additions, and the T019 supersession — with **no** unrelated
  surface churn — and that `SurfaceBaselineTests` is green.
- [X] T022 Run the `quickstart.md` Scenarios 1–5 end-to-end (ship emits handoff
  with no Governance; determinism; evidence mapping + edges; stale + refresh;
  authored sources preserved) and record the result as
  `specs/017-governance-handoff/readiness/quickstart-validation.md`, confirming
  SC-001..SC-007.
- [X] T023 Final regression: run `dotnet test FS.GG.SDD.sln` and confirm the full
  suite is green with the new `GovernanceHandoffTests` included and the surface
  baseline updated (Constitution VI green-suite requirement). Capture the run
  output as `specs/017-governance-handoff/readiness/full-suite.txt`.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately. T002 [P] runs
  alongside T001.
- **Foundational (Phase 2)**: depends on Setup; **blocks** every user story. Order
  within the phase: T003 (`.fsi`) → T004 (manifest kind) → T005 (compiling
  skeleton) → T006 (test scaffold) → T007 (baseline + FSI transcript).
- **US1 (Phase 3)**: depends on Foundational. It is the MVP and the prerequisite
  for the other stories' *independent tests*, because they all assert over the
  handoff that `ship` (T009) emits.
- **US2–US4 (Phases 4–6)**: depend on US1's emission (T009) for their independent
  tests and extend the same `GovernanceHandoff.fs` fold — they run **sequentially**
  in priority order (US2 P1 → US3 P2 → US4 P2), not in parallel, because they edit
  the same projection file and the same test file.
- **US5 (Phase 7)**: depends on US1 emission (T009) and the manifest currency
  machinery (T004); adds the `refresh` regeneration (T017).
- **Polish (Phase 8)**: T019 supersession depends on the handoff existing
  (Phases 3–6); T020–T023 are final evidence/validation after all stories.

### Within the projection and test files (single-file serialization)

- `src/FS.GG.SDD.Artifacts/GovernanceHandoff.fs` is extended by T005 → T008 →
  T011 → T013 → T015 in that order (each adds a slice of the same total fold);
  these are **not** `[P]`.
- `tests/FS.GG.SDD.Commands.Tests/GovernanceHandoffTests.fs` is extended by
  T006 → T010 → T012 → T014 → T016 → T018 in that order; these are **not** `[P]`.
- `src/FS.GG.SDD.Commands/CommandWorkflow.fs` is edited by T009 (ship) then T017
  (refresh); sequential.

### Parallel opportunities

- T002 [P] runs alongside T001.
- In Polish, T020 (CLI evidence capture) and T021 (baseline confirmation) are
  `[P]` (different artifacts); T022/T023 run last.
- Cross-story parallelism is intentionally limited: because the projection is one
  pure fold in one file and all assertions live in one test file, the stories are
  sequenced rather than parallelized. The independence guarantee holds at the
  **test** level (each story's facet is asserted on its own), not at the file
  level.

---

## Implementation Strategy

### MVP (P1: US1, built on Foundational)

1. Phase 1 Setup → Phase 2 Foundational (FSI surface, view kind, compiling
   skeleton, test scaffold, baseline).
2. US1: `ship` emits the deterministic, declared-only handoff envelope with no
   Governance present.
3. **STOP and VALIDATE**: `dotnet test` green with the envelope + determinism +
   no-Governance + byte-identity assertions. This is the shippable seam — the
   first SDD output Governance can consume.

### Incremental delivery

1. MVP (US1) → versioned handoff envelope emitted at `ship`.
2. US2 → declared evidence nodes + edges (the highest-value Governance input).
3. US3 → governed/routing references + `.fsgg` presence (boundary held: no route).
4. US4 → merge-boundary readiness as advisory facts (boundary held: no verdict).
5. US5 → currency + `refresh` regeneration.
6. Polish → retire the advisory placeholders, capture CLI evidence, validate.

---

## Notes

- **Declared facts only**: SDD MUST NOT compute effective/`autoSynthetic`
  evidence states, select routes/profiles/gates, or compute freshness (FR-005,
  FR-009, SC-004, SC-005). The boundary-exclusion assertions (T012, T014, T016)
  are load-bearing — never weaken them to green a build; narrow scope and document
  with `[-]` + rationale instead.
- **No FS.GG.Governance package reference** in SDD code. The cross-repo shape is
  versioned in `contracts/integration-requirements.md`; the Governance types it
  names are validation *targets*, not compile-time dependencies (research D6).
- **Markdown is an authoring surface** (Constitution II); `governance-handoff.json`
  is the machine contract. Any human-readable rendering is a projection over the
  same structured facts (FR-013).
- **Generated-view currency comes from regeneration**, never file presence
  (FR-012) — a present-but-stale handoff is never reported current (T018).
- Never mark a failing task `[X]`. Mark `[-]` with written rationale if a task is
  skipped. Update the progress table and the per-task checkboxes as work lands.
</content>
</invoke>
