---
description: "Task list for feature 080 — scaffold name → valid F# identifier"
---

# Tasks: Guarantee a freshly scaffolded product compiles (name → valid F# identifier)

**Input**: Design documents in `specs/080-scaffold-name-sanitization/`
(spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md)

**Overall tier**: Tier 1 (contracted change). Tasks inherit `[T1]` unless annotated.

**Legend**: `[ ]` pending · `[X]` done w/ real evidence · `[-]` skipped w/ rationale ·
`[P]` parallel-safe within its phase · `[USn]` user story. Phases run in sequence; tasks
within a phase may run in parallel. Constitution I (Spec→FSI→tests→impl) and VI (fail-before/
pass-after, real fixtures) are followed per unit.

---

## Phase 1: Contract signatures (`.fsi` first — Principle I/III)

**Purpose**: Pin the public surface before any `.fs` body. Blocking prerequisite for all stories.

- [X] T001 [P] Add `IdentifierParameter: string option` to `ProviderDescriptor` in
  `src/FS.GG.Contracts/Provider.fsi` (additive; doc comment per
  `contracts/provider-descriptor-identifier-parameter.md`). Field only — no new helper.
- [X] T002 [P] Create `src/FS.GG.SDD.Artifacts/FsharpIdentifier.fsi` with `DerivationError` and
  `val deriveNamespace: name: string -> Result<string, DerivationError>`, matching
  `contracts/fsharp-identifier-derivation.md`.
- [X] T003 [P] Add `val scaffoldNameUnrepresentable: name: string -> Diagnostic` to
  `src/FS.GG.SDD.Artifacts/Diagnostics.fsi` (id `scaffold.nameUnrepresentable`).

---

## Phase 2: Foundational — derivation + contract field + parse (blocking prerequisites)

**Purpose**: Everything the user stories build on. Complete before Phase 3+.

### Derivation module (pure — Principle IV; MVU not applicable, it is a pure transform)

- [X] T004 [US1] Add golden + property tests in
  `tests/FS.GG.SDD.Artifacts.Tests/FsharpIdentifierTests.fs` from the behavior table in
  `contracts/fsharp-identifier-derivation.md` (hyphen-drop, dots-preserved, leading-digit,
  reserved-keyword, `---`/`""` → `Unrepresentable`; idempotence, no-op-on-valid, determinism).
  Tests MUST fail before T005. Pin the `Acme..Foo` empty-interior-segment choice here.
- [X] T005 [US1] Implement `src/FS.GG.SDD.Artifacts/FsharpIdentifier.fs` (per-segment, ordinal,
  culture-invariant) so T004 passes. Reserved-keyword list is language-level, local to the module.

### Provider-descriptor field + registry parse (Principle II — structured contract)

- [X] T006 Implement the record field added in T001 in `src/FS.GG.Contracts/Provider.fs`
  (`IdentifierParameter: string option`); keep `defaultNameParameter`/`resolveNameParameter`
  unchanged.
- [X] T007 [P] Parse optional `identifierParameter:` scalar in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fs` `parseProviderRegistry` → `None` when
  absent/blank; does not affect entry-drop (still gated on name/contractVersion/templateId/source).
- [X] T008 [P] [US4] Implement `scaffoldNameUnrepresentable` in
  `src/FS.GG.SDD.Artifacts/Diagnostics.fs` (`DiagnosticError`, ref `.fsgg/providers.yml`,
  `NextAction`: choose a name with at least one identifier character, evidence `[name]`).
- [X] T009 Add a fixture registry
  `tests/fixtures/scaffold-provider/registries/identifier-declaring.providers.yml` declaring both
  `nameParameter` and `identifierParameter` (+ required name param) for handler tests.
- [X] T010 [P] Tests: `IdentifierParameter` default `None`/roundtrip in
  `tests/FS.GG.Contracts.Tests/ProviderDescriptorTests.fs`; `identifierParameter:` parse (present/
  absent/blank) in `tests/FS.GG.SDD.Artifacts.Tests/ProviderRegistryParseTests.fs`. Fail before
  T006/T007.
- [X] T011 Update public-surface baselines for `FS.GG.Contracts` and `FS.GG.SDD.Artifacts`
  (the API-compat gate) to reflect T001–T003 additions.

**Checkpoint**: derivation, contract field, parse, and diagnostic exist and are unit-tested.

---

## Phase 3: [US1] Hyphenated/misspelled name still builds — the derive+forward wiring (P1, MVP)

**Purpose**: The core guarantee. Pure step in the scaffold MVU `update` (`resolveScaffold`);
requests no new effect.

- [X] T012 [US1] Tests in `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs` (using the
  T009 fixture): scaffolding `Roquelike-DungeonCrawler` forwards **both** `--<name>
  Roquelike-DungeonCrawler` (verbatim) and `--<identifier> RoquelikeDungeonCrawler` in the
  planned/`RunProcess` `dotnet new` args; a name already valid forwards the identifier unchanged
  (no-op). Fail before T013.
- [X] T013 [US1] Wire derivation into `resolveScaffold` in
  `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs` per `data-model.md` flow: when
  `descriptor.IdentifierParameter = Some k` and the sink is not author-set and the source name is
  present, inject `deriveNamespace`'s `Ok` value at key `k` into the effective map; `None`/no-source
  ⇒ unchanged behavior.

**Checkpoint**: US1 independently testable — derived identifier reaches the forwarded command.

---

## Phase 4: [US2] Raw name preserved where it belongs (P1)

**Purpose**: The raw name is never mutated in what is forwarded/recorded.

- [X] T014 [US2] Assert in `ScaffoldCommandTests.fs` that the `NameParameter` value is byte-
  identical to the author input in both the forwarded args and the recorded `EffectiveParameters`,
  and that the derived sink is a *separate* entry (raw name not overwritten). Extend the existing
  verbatim-forwarding assertion.
- [X] T015 [US2] Confirm provenance/report record both entries with schema unchanged: update the
  provenance/report snapshot fixtures in `tests/FS.GG.SDD.Commands.Tests` /
  `tests/FS.GG.SDD.Artifacts.Tests` (whichever holds the scaffold snapshot) to include the injected
  sink row; assert `schemaVersion` stays `1` (research D6). No code change expected in
  `ScaffoldProvenance.fs`.

**Checkpoint**: US2 independently testable — raw name intact, derived identifier distinct, schema v1.

---

## Phase 5: [US4] Auditability + unrepresentable-name guard (P2)

**Purpose**: Author override precedence, and safe failure on a name with no identifier character.

- [X] T016 [US4] Tests in `ScaffoldCommandTests.fs`: author `--param <identifier>=Explicit`
  forwards `Explicit` (no derivation — FR-008 precedence); name `---` blocks with
  `scaffold.nameUnrepresentable`, exit class 1, not-run summary, no success provenance. Fail before
  T017.
- [X] T017 [US4] In `resolveScaffold` (`HandlersScaffold.fs`): honor author sink override (skip
  derivation when the sink key is in `request.Parameters`); on `deriveNamespace = Error`, return
  `ScaffoldBlocked [ scaffoldNameUnrepresentable name ] (notRunSummary …)` with the FR-009 hint.
- [X] T018 [P] [US4] Report projections surface both effective parameters and the block-path
  diagnostic. The derived sink is an *ordinary* `effectiveParameters` entry — no new projection
  shape — so it rides the already-golden json/text/rich `effectiveParameters` projection tests;
  the T014 real-run test asserts both entries in the json provenance, and the T016 block test
  asserts `scaffold.nameUnrepresentable` in the report diagnostics. No new projection code needed.

**Checkpoint**: US4 independently testable — precedence + block path proven; reports coherent.

---

## Phase 6: [US3] CI build+test smoke against the real provider (P1, network-gated)

**Purpose**: Regression guard so a non-compiling scaffold can never silently ship.

- [ ] T019 [US3] Extend `tests/FS.GG.SDD.Acceptance.Tests/CompositionAcceptanceTests.fs`: scaffold
  the real `rendering` provider with a **hyphenated/misspelled** product-name param and add a
  `dotnet test` probe alongside the existing build (and run) probes; assert **both** build and test
  exit 0. Keep it under `kind=composition-acceptance` (self-skips when
  `FSGG_SDD_ACCEPTANCE_REGISTRY` is unset).
- [ ] T020 [P] [US3] Confirm `.github/workflows/composition-acceptance.yml` runs the extended filter
  unchanged (no new always-on job); add a test-matrix/registry row only if the acceptance harness
  requires one for the new probe. Note in the task line if no workflow edit is needed.

> **Blocked on cross-repo adoption (T021).** T019/T020 assert the real `rendering` provider
> declares `identifierParameter` and consumes the sink symbol; until FS.GG.Rendering ships that
> (T021 request), the smoke would be red in the gated lane. Sequenced *after* the cross-repo
> request lands, so the test is added when it can go green — not committed known-red.

**Checkpoint**: US3 — offline gate green (self-skip); the network-gated lane asserts build+test
(goes green once Rendering adopts — Phase 7).

---

## Phase 7: Cross-repo, agent surfaces, docs, and final gate

- [X] T021 [US3] Filed **FS-GG/FS.GG.Rendering#142** (`cross-repo`/`cross-repo:request`/
  `contract-change`); added to the Coordination board (Status: Ready, Phase: P1 Rendering,
  Contract: scaffold-provider). SDD epic #148 commented with the dependency. (research D8.)
- [ ] T022 [US3] Add the versioned additive contract entry to `FS-GG/.github`
  `registry/dependencies.yml` + prepend `registry/CHANGELOG.md` + update
  `docs/registry/compatibility.md`; validate with `fsgg-sdd registry validate
  registry/dependencies.yml` → `"valid": true`. (Coordinated with T021; may land via the
  cross-repo PR.)
- [-] T023 Not applicable: no seeded agent skill or `agent-commands` text documents the scaffold
  `.fsgg/providers.yml` param mechanics (`nameParameter`/`identifierParameter`) — verified by grep
  across `.claude`/`.codex`/`.agents` skills (zero hits). There is no agent-surface prose to mirror,
  so the dual-surface rule (FR-012) is satisfied vacuously. The living contract surface is the
  `Provider.fsi` + the schema doc (T024). Revisit if scaffold param docs are ever added to a skill.
- [X] T024 [P] Added `nameParameter` (source) and `identifierParameter` (sink) rows + the derivation
  precedence rule to `specs/030-scaffold-template-provider/contracts/providers-descriptor.schema.md`
  (the living `.fsgg/providers.yml` schema doc); value-agnostic wording, `030` FR-002 intact. The
  field-level contract is in `contracts/provider-descriptor-identifier-parameter.md`.
- [X] T025 (offline gate) Full `dotnet test FS.GG.SDD.sln -c Debug` green — Contracts 87 · Artifacts
  211 · Validation 18 · Acceptance 33 (+3 network-skipped) · Cli 99 · Commands 555, 0 failures; the
  composition-acceptance facts self-skip offline (SC-006). Fantomas `--check` clean on changed files.
  `/speckit-analyze` before merge still recommended (not yet run).

---

## Dependencies (beyond phase ordering)

- T005 after T004; T006 after T001; T007/T008 after their `.fsi` (T002 is standalone).
- T013 after T005+T006+T007 (needs derivation + field + parse). T017 after T008+T013.
- T015 after T013 (snapshot reflects injected sink). T019 after T013 (real forward path).
- T022 coordinated with T021. T025 last.

## Parallel opportunities

- Phase 1: T001/T002/T003 fully parallel (distinct `.fsi`).
- Phase 2: T007/T008/T010 parallel after their signatures; T004/T005 (derivation) parallel to the
  contract-field track.
- Phase 7: T023/T024 parallel; T021/T022 are the cross-repo track.

## MVP scope

**User Story 1** (Phases 1–3): a hyphenated name yields a forwarded valid identifier — the core
"it compiles" guarantee, independently testable via handler tests without the network-gated lane.
US2 (raw-name preservation) and US4 (guardrails) harden it; US3 (CI smoke) + the cross-repo
adoption prove it end-to-end against the real provider.

## Task count per user story

- US1: T004, T005, T012, T013 (4)
- US2: T014, T015 (2)
- US3: T019, T020, T021, T022 (4)
- US4: T008, T016, T017, T018 (4)
- Shared/foundational/polish: T001, T002, T003, T006, T007, T009, T010, T011, T023, T024, T025 (11)

Total: 25 tasks.
