---

description: "Task breakdown for Honor Provider-Declared Default Starter Selection in Scaffold"
---

# Tasks: Honor Provider-Declared Default Starter Selection in Scaffold

**Input**: Design documents from `/specs/050-scaffold-default-starter/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included. Constitution VI (Test Evidence Mandatory) and the plan both
require failing-first tests over real fixtures for every behavior here. This is a
*lock-and-prove* feature — the selection mechanism already exists, so the test
tasks (regression coverage + goldens + grep guard) are the load-bearing
deliverables, not optional.

**Tier**: Tier 1 (contracted) per plan — one additive field
(`effectiveParameters`) on `scaffold-provenance.json` (schema v1) and the scaffold
report, across json/text/rich; regression + golden updates; one network-gated
acceptance assertion; docs + migration note; a cross-repo redirect (FR-009).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase.
- **[Story]**: `US1`/`US2`/`US3` (cross-cutting tasks omit it).
- Phases run in sequence; tasks within a phase may run in parallel where marked.

## Elmish/MVU applicability

No new I/O edge (Constitution V, PASS in plan). The change rides the existing
scaffold MVU finalize path (`finalizeScaffold` → `provenanceWriteEffect` / summary
constructors); the effective-parameters value is computed in the **pure**
transition (`effectiveParameters`, `HandlersScaffold.fs:85-92`, UNCHANGED) and the
provenance write stays a single `WriteFile` effect. So there is no new `.fsi` MVU
contract — the only public-surface deltas are the additive record fields:
`EffectiveParameters` on `ScaffoldProvenanceRecord` (`ScaffoldProvenance.fsi`, T003)
and on `ScaffoldSummary` (`CommandTypes.fsi`, T004). Emitted-effect coverage is the
existing scaffold provenance `WriteFile` assertion extended by the new golden
(T005); pure-transition coverage is the default-apply / override precedence tests
(T008, T013) over the unchanged `effectiveParameters` fold.

---

## Phase 1: Setup & Shared Contract Surface (Blocking Prerequisites)

**Purpose**: a green baseline, the value-agnostic test fixture both stories drive,
and the additive `.fsi` field declarations. Per Constitution I (Spec → FSI →
Semantic Tests → Implementation), the `.fsi` declarations land before any test or
body that consumes them.

- [X] T001 Confirm a clean baseline: `dotnet build FS.GG.SDD.sln` and
  `dotnet test FS.GG.SDD.sln` both green before any change (records the pre-feature
  state the FR-008 byte-identical guard in T009/T014 and the final gate compare
  against).
- [X] T002 [P] Add the generic, value-agnostic fixture registry
  `tests/fixtures/scaffold-provider/registries/default-declaring.providers.yml`
  per data-model.md §"Test data model": one provider, `schemaVersion: 1`,
  `contractVersion: "1.0.0"`, a **required** `productName`, and a **non-required**
  parameter with a declared `default` (abstract key/value — `variant` /
  `default: alpha`). **No** `game`/`app`/rendering package id/template id/path/docs
  URL (FR-004 — must pass the T020 grep guard). Drives US1, US2, and the edge cases.
- [X] T003 Declare the additive field on the produced artifact in
  `src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fsi`:
  `EffectiveParameters : (string * string) list` on `ScaffoldProvenanceRecord`
  (`.fsi:10-21`). `serialize`/`tryParse` signatures stay unchanged (Constitution
  III). Signature only — no body yet. BLOCKS T005/T007.
- [X] T004 Declare the additive field on the report in
  `src/FS.GG.SDD.Commands/CommandTypes.fsi`:
  `EffectiveParameters : (string * string) list` on `ScaffoldSummary`
  (`.fsi:328-339`). Signature only — no body yet. BLOCKS the US1 impl (T010–T012).

**Checkpoint**: solution builds with the two additive `.fsi` fields stubbed; the
fixture exists. Foundational recording (Phase 2) can begin.

---

## Phase 2: Foundational — Effective-Parameters Recording Substrate (Blocking)

**Purpose**: the FR-003 recording substrate both user stories assert on — the
provenance serialize/parse round-trip and the registry-default parse regression.
**⚠️ No user story can be observed end-to-end until this phase is complete.**

### Tests (write first; must FAIL before T007)

- [X] T005 [P] In `tests/FS.GG.SDD.Artifacts.Tests/`, add a failing
  `ScaffoldProvenance` test set per `contracts/scaffold-provenance-effective-parameters.md`:
  (a) `serialize >> tryParse` round-trips `EffectiveParameters` preserving order and
  content; (b) a v1 document **without** `effectiveParameters` parses to `[]`
  (backward compatibility, D3); (c) a byte-exact `scaffold-provenance.json` golden
  including the field, sorted ascending by key, emitted **after** `producedPaths`,
  `[]` when empty (FR-003).
- [X] T006 [P] In `tests/FS.GG.SDD.Artifacts.Tests/`, add a failing
  `ProviderRegistryParseTests` case asserting `parseProviderRegistry` reads
  `parameters[].default` from the T002 fixture into `ProviderParameterSpec.Default`
  (`Config.fs:189`) — the substrate for FR-001, locked as a regression.
- [X] T007b [P] In `tests/FS.GG.Contracts.Tests/`, add a failing assertion that the
  provenance schema version stays `1` (additive posture, D3) — guards against an
  unintended major bump for a purely additive optional field.

### Implementation

- [X] T007 In `src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fs`, implement the T003
  field: add `EffectiveParameters` to `ScaffoldProvenanceRecord` (`:15-22`);
  `serialize` (`:33-60`) writes `effectiveParameters` as an array of `{key,value}`
  objects **sorted ascending by key**, after `producedPaths`, always emitted (`[]`
  when empty) — same discipline as `producedPaths` (`:50`); `tryParse` (`:62-101`)
  reads it, **defaulting to `[]` when the key is absent**. Run T005/T006/T007b green.

**Checkpoint**: provenance round-trips the field, registry defaults parse, schema
stays v1. The recording substrate is ready; US1 can make it observable end-to-end.

---

## Phase 3: User Story 1 — Author gets the provider's default starter without naming it (Priority: P1) 🎯 MVP

**Goal**: when the author omits a parameter that has a provider-declared `default`,
scaffold forwards that declared default verbatim to the provider and records it as
the effective value in the scaffold report (json/text) and
`.fsgg/scaffold-provenance.json` — so the author lands on the provider's intended
default with no extra flags, reproducibly.

**Independent Test**: with the T002 default-declaring fixture, run scaffold omitting
`variant`; assert the provider is invoked with `--variant alpha` and that the
scaffold JSON `scaffold.effectiveParameters` and the provenance `effectiveParameters`
both record `variant=alpha`, sorted by key (quickstart Scenario 1).

### Tests for User Story 1 (write first; must FAIL before T010–T012)

- [X] T008 [P] [US1] In `tests/FS.GG.SDD.Commands.Tests/` (ScaffoldCommandTests), add
  a failing `DefaultApplied` test: scaffold against the T002 fixture **omitting**
  `variant` invokes the provider with the declared default (`--variant alpha`), and
  the scaffold report summary + provenance both record `variant=alpha` as effective,
  sorted by key (FR-001/FR-003, SC-001).
- [X] T009 [P] [US1] In ScaffoldCommandTests, add failing **json + text projection
  goldens** for the default-applied run per
  `contracts/scaffold-report-effective-parameters.md`: json emits
  `effectiveParameters` (array of `{key,value}`) **after** `producedPaths`, sorted,
  `[]` when empty; text emits one sorted `scaffoldEffectiveParam: <key>=<value>` line
  per entry (header omitted when empty). Assert **no other key, key order, stream, or
  exit code** changes and that a representative **non-scaffold** command golden is
  byte-identical (FR-008).
- [X] T010b [P] [US1] In ScaffoldCommandTests, add the **edge-case** set (quickstart
  Scenario 5, data-model precedence rules): (a) required `productName` omitted still
  surfaces `scaffold.providerParamMissing` and is **absent** from `effectiveParameters`
  — a declared default never makes a required param optional; (b) a blank/whitespace
  `default` is surfaced as a blank declaration, never a silently invented value;
  (c) the value is forwarded verbatim, never interpreted; (d) a provider/parameter that
  declares **no** `default` and is omitted forwards no key for it and records `[]` (or
  omits that key) in `effectiveParameters` — the "provider declares no default" edge
  case (spec.md Edge Cases), behavior unchanged.

### Implementation for User Story 1

- [X] T010 [US1] In `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs`,
  populate the T004 `ScaffoldSummary.EffectiveParameters`: `notRunSummary`
  (`:111-122`) → empty list; the success path / `terminalSummary` (`:259-270`) → the
  `effectiveParameters` map (`:85-92`, UNCHANGED), sorted by key; pass the same map
  into the `ScaffoldProvenanceRecord` constructor for `provenanceWriteEffect`
  (`:232-242`). No change to the precedence fold.
- [X] T011 [US1] In `src/FS.GG.SDD.Commands/CommandSerialization.fs` `writeScaffold`
  (`:291-314`), emit `effectiveParameters` (array of `{key,value}`, sorted by key,
  always present) **after** `producedPaths` — the json automation contract. Run
  T008/T009 (json) green.
- [X] T012 [US1] In `src/FS.GG.SDD.Commands/CommandRendering.fs` scaffold block
  (`:196-213`), emit one sorted `scaffoldEffectiveParam: <key>=<value>` line per
  entry after the `scaffoldProducedPath:` lines (omitted when empty); `--rich` reuses
  these plain key/value lines (presentation only, excluded from goldens, degrades to
  zero-ANSI). Run T009 (text) + T010b green.

**Checkpoint**: MVP — the provider's declared default is forwarded and durably
recorded in all three projections + provenance. STOP and validate quickstart
Scenarios 1, 3 (data-only registry edit changes the forwarded value with zero
generic-SDD code change), and 4.

---

## Phase 4: User Story 2 — Author overrides the default starter explicitly (Priority: P1)

**Goal**: an explicit `--param <key>=<value>` always wins over a provider-declared
`default`; the override value (not the default) is forwarded and recorded as
effective. Reuses the US1 recording substrate — confirms the precedence the flip
must never remove.

**Independent Test**: with the T002 fixture, run scaffold passing
`--param variant=beta`; assert the provider is invoked with `--variant beta` (not the
declared default), and provenance + report record `variant=beta` as effective
(quickstart Scenario 2).

### Tests for User Story 2 (write first; must FAIL or already pass via US1 wiring)

- [X] T013 [P] [US2] In `tests/FS.GG.SDD.Commands.Tests/` (ScaffoldCommandTests), add
  a failing `Override` test: `--param variant=beta` against the T002 fixture invokes
  the provider with `beta`, the declared default `alpha` is **not** applied, and the
  report summary + provenance record `variant=beta` as effective (FR-002/FR-003,
  SC-002).
- [X] T014 [P] [US2] In ScaffoldCommandTests, add the json + text projection golden
  for the override run (effective value = the override, not the default), and
  re-assert non-scaffold goldens byte-identical (FR-008).

### Implementation for User Story 2

- [X] T015 [US2] Verify the override path needs **no new production code**: the
  `effectiveParameters` fold (`HandlersScaffold.fs:85-92`) already overlays
  `request.Parameters` over the declared defaults with `Map.add` so the author value
  wins (research D1). If T013/T014 are green from the US1 wiring alone, record that on
  this task line as the confirmation that FR-002 is satisfied by the existing
  mechanism; if any gap surfaces, fix it in `HandlersScaffold.fs` and document the
  narrowing here. Do **not** mark `[X]` on a failing assertion.
  **CONFIRMED**: the US2 Override tests (T013/T014) and the host-binary smoke
  (`--param variant=beta` overrides the declared `alpha`; manifest shows `variant=beta`)
  pass with **no new production code** — the existing `effectiveParameters` `Map.add`
  overlay already makes the author value win. FR-002 holds via the existing mechanism.

**Checkpoint**: US1 + US2 both pass independently — declared default applied when
omitted, explicit override always wins, both recorded. Validate quickstart
Scenario 2.

---

## Phase 5: User Story 3 — Default starter proven against the real provider (Priority: P2)

**Goal**: the network-gated composition-acceptance drives the fixed real-provider
scaffold with **no** explicit starter parameter against the real published rendering
registry at `0.1.54-preview.1`, asserting the produced product builds and the verdict
is `pass` (GREEN) — exercising the registry's declared default **by reference, never
by name**. Offline (gating env unset) it reports Skipped and touches no network.

**Independent Test**: with `FSGG_SDD_ACCEPTANCE_REGISTRY` set, run the composition
acceptance with the starter param omitted from `scaffoldRequest`; assert build +
`pass`. With it unset, assert Skipped and no network (quickstart Scenario 7).

**Depends on**: the recording substrate (Phase 2) only for provenance shape; the
acceptance harness, lane, and gating already exist (feature 041).

### Tests for User Story 3

- [X] T016 [US3] In `tests/FS.GG.SDD.Acceptance.Tests/AcceptanceSupport.fs`, ensure
  `scaffoldRequest` (`:125-128`) drives the default-starter path by carrying **no**
  explicit starter parameter (today `Parameters = ["lifecycle","sdd"]` — already omits
  a starter; confirm and annotate that omission is the by-reference default-starter
  exercise of FR-006). Do not name any starter value.
- [X] T017 [US3] In `tests/FS.GG.SDD.Acceptance.Tests/` (CompositionAcceptanceTests),
  assert the gated run: the fixed composition scaffold with no explicit starter
  produces a product that **builds** and yields verdict `pass`
  (`CompositionResult.fs:28-31`), via `RequiresRegistryFactAttribute`
  (`AcceptanceSupport.fs:63-69`); and assert the offline path reports Skipped with no
  network (FR-006/FR-007, SC-004). Update the byte-exact result golden
  (`CompositionAcceptanceTests.fs:407-441`) and `inputs.params`
  (`CompositionResult.fs:169-174`) in lockstep — the params doc carries **no** starter
  key when omitted.

**Checkpoint**: all three stories independently functional; the generic mechanism is
proven end-to-end against the real provider in the opt-in lane while the offline inner
loop stays green and offline. Validate quickstart Scenario 7 (Skipped offline).

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T018 [P] Update `tests/FS.GG.SDD.Artifacts.Tests/` and
  `tests/FS.GG.SDD.Commands.Tests/` PublicSurface baselines for the two additive
  record fields (`ScaffoldProvenanceRecord.EffectiveParameters`,
  `ScaffoldSummary.EffectiveParameters`); add a CLI-level test in
  `tests/FS.GG.SDD.Cli.Tests/` that the scaffold report projects the effective
  parameters consistently through default/`--json`, `--text`, and `--rich` (rich
  adds/drops no facts, degrades to zero-ANSI when non-interactive/`NO_COLOR`/`TERM=dumb`).
  **NARROWED**: the PublicSurface baselines capture only static module functions
  (modules = abstract-sealed classes), **not** record instance fields — so the two
  additive record fields do not appear in any `PublicSurface.baseline` and the surface
  tests stay green with no baseline edit (verified: full suite green). The CLI-level
  projection parity test was added to `ScaffoldParityTests.fs`.
- [X] T019 [P] Document the value-agnostic default-starter selection contract per D6:
  in `docs/release/schema-reference.md` record the provenance `effectiveParameters`
  field and the `.fsgg/providers.yml` `parameters[].default` semantics; in
  `docs/reference/authoring-contracts.md` add how a provider author declares and
  changes a default starter and how `--param` overrides it (mirroring
  `contracts/provider-default-starter-selection.md`), so a provider author can follow
  it without reading source (FR-005, SC-005). No provider-specific value.
- [X] T020 [P] Add the boundary grep-guard test (FR-004/SC-003, D5): a repository-wide
  assertion of **zero** occurrences of `game`, `app`-as-starter, rendering package
  ids, template ids, paths, or docs URLs in generic SDD source and generic-contract
  tests/fixtures (the new T002 fixture included). Place it alongside the existing
  feature-030 boundary guard.
- [X] T021 [P] Add the Tier-1 migration note under `docs/release/migrations/`
  recording the additive `effectiveParameters` field on `scaffold-provenance.json`
  (schema stays v1; `tryParse` defaults to `[]`; backward/forward compatible per D3).
  **NARROWED to the repo's migration policy**: `docs/release/migrations/README.md`
  states an **additive-only** release MUST NOT carry a `<version>.md` note (notes are
  for breaking changes; the `release-readiness.json` `migrations[]` array stays empty).
  This change is additive, so — exactly as feature 030 did — the change is recorded as
  an explanatory paragraph in the migrations index (`README.md`), not a version note.
  Manufacturing a breaking-change note would violate the policy and the empty-`migrations[]`
  invariant (Principle III).
- [X] T022 [P] Update `CLAUDE.md` and `AGENTS.md` in lockstep (Constitution VII): note
  that scaffold records the effective forwarded parameters (declared defaults overlaid
  by `--param` overrides) in `.fsgg/scaffold-provenance.json` and the scaffold report,
  value-agnostically — no provider-specific starter value.
- [X] T023 Post the cross-repo response on FS-GG/FS.GG.SDD#44 via the
  `cross-repo-coordination` protocol (FR-009, D7): state SDD's in-boundary half is
  delivered (mechanism locked + proven + documented) and redirect the literal
  `app → game` default flip to FS.GG.Templates as a data edit in
  `providers/rendering.providers.yml` for the `fs-gg-ui-template` contract at
  `0.1.54-preview.1`. The literal flip is out of scope for this repo.
- [X] T024 Run the full quickstart (`specs/050-scaffold-default-starter/quickstart.md`
  Scenarios 1–6 offline, Scenario 7 Skipped offline) and record evidence; confirm
  SC-001..SC-005 all hold.
- [X] T025 Final gate: `dotnet build FS.GG.SDD.sln` + `dotnet test FS.GG.SDD.sln`
  green; run `fsgg-sdd validate` to confirm no broad-matrix/determinism/baseline
  regression; confirm all non-scaffold command goldens are byte-identical (FR-008).

---

## Dependencies & Execution Order

### Phase order

- **Phase 1 (Setup & `.fsi`)** → **Phase 2 (Foundational recording)** →
  **Phase 3 (US1)** → **Phase 4 (US2)** → **Phase 5 (US3)** → **Phase 6 (Polish)**.
  Phases are sequential.
- T003 (provenance `.fsi`) blocks T005/T007. T004 (report `.fsi`) blocks US1 impl
  (T010–T012).
- Phase 2 (T005–T007) is the FR-003 substrate both US1 and US2 assert on; it must be
  green before US1 impl.

### Within stories

- Tests before implementation (T005/T006/T007b → T007; T008/T009/T010b →
  T010/T011/T012; T013/T014 → T015 confirmation).
- T010 (summary/provenance population) before T011 (json) and T012 (text).
- US2 (Phase 4) reuses the US1 recording wiring; it adds tests + goldens and a
  confirmation that FR-002 holds via the existing precedence fold (T015).
- T020 (grep guard) can run as soon as the T002 fixture lands, but is grouped in
  polish so it guards the whole final tree.

### Parallel opportunities

- T005/T006/T007b [P] (distinct test projects).
- T008/T009/T010b [P] within ScaffoldCommandTests (distinct tests — coordinate edits
  to the same file).
- T013/T014 [P] (US2 tests).
- T018/T019/T020/T021/T022 [P] (independent test/docs/surfaces).

## Story summary

- **US1 (P1, MVP)**: 6 tasks (T008, T009, T010b, T010, T011, T012) — declared default
  applied when omitted, forwarded + recorded in json/text/provenance.
- **US2 (P1)**: 3 tasks (T013, T014, T015) — explicit `--param` override always wins,
  recorded; reuses US1 wiring.
- **US3 (P2)**: 2 tasks (T016, T017) — real-provider default-starter composition
  acceptance, opt-in/network-gated.
- **Shared/Foundational**: T001–T004 (baseline, fixture, `.fsi`), T005–T007/T007b
  (recording substrate) + T018–T025 (polish/cross-cutting).

**Suggested MVP scope**: Phase 1 + Phase 2 + User Story 1 — the provider's declared
default is honored and durably recorded, which is the exact capability #44's
Templates-side `app → game` flip depends on. US2 (override) and US3 (real-provider
proof) harden and verify it.

## Notes

- Never mark a failing task `[X]`; never weaken an assertion to green a build —
  narrow scope and document it on the task line.
- `[-]` = skipped with written rationale.
- Real fixture/golden evidence is required (Constitution VI); disclose any synthetic
  evidence per Principle V when marking `[X]`.
- FR-004/SC-003 boundary: no `game`/`app`-as-starter, rendering package/template id,
  path, or docs URL may enter generic SDD source or generic-contract tests/fixtures —
  the T002 fixture and every example stay value-agnostic.
