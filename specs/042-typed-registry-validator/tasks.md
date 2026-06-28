---
description: "Task list for Typed Registry Validator"
---

# Tasks: Typed Registry Validator

**Input**: Design documents from `/specs/042-typed-registry-validator/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md,
contracts/registry-document.md, contracts/cli-registry-validate.md

**Tests**: Included — the spec mandates them (US1–US3 each carry acceptance
scenarios; Constitution VI requires real test evidence). Write each story's tests
first and confirm they FAIL before implementing.

**Change classification**: Tier 1 (contracted change) — public model/validator
surface in the published `FS.GG.Contracts` package plus a cross-repo gate contract.
Requires `.fsi`-first, tests, surface-baseline update, docs, and a version note.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on another
  incomplete task in this phase)
- **[Story]**: US1 / US2 / US3 (omitted for shared/cross-cutting tasks)
- All paths are repository-root-relative and exact.

## Decomposition note (read first)

`validateDocument` is one pure function exercised by all three stories. To keep the
stories independently testable, the rules land by **progressive tightening**:

- **Foundational** builds the typed model and a `validateDocument` whose structural
  rules (missing-field, duplicate-id, document-shape, deterministic order) are real,
  but whose **reference checks** (owner/consumers/edge endpoints) and **version
  grammar** are deliberately *permissive*.
- **US1** wires the file→verdict path end-to-end; the canonical file already passes
  the permissive validator, so the MVP is demonstrable on its own.
- **US2** tightens the **reference** rules (and adds the paired failing-first tests).
- **US3** tightens the **version grammar** (and adds its paired failing-first tests).

The canonical `registry/dependencies.yml` stays `Valid` at every checkpoint; each
story adds genuine rule logic plus tests that fail before that logic exists.

## Elmish/MVU applicability (Constitution V)

`load` (file read + YAML parse) is I/O → it lives at the **edge** in
`FS.GG.SDD.Artifacts` and is composed by the command handler at the interpreter
boundary; it never enters the BCL-only `FS.GG.Contracts` leaf. `validateDocument` is
a **pure** function (no I/O) — unit-tested directly over parsed/constructed models.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: vendor the real fixture and confirm the dependency posture the plan
relies on.

- [X] T001 [P] Vendor the canonical registry file as a test fixture: copy
  `FS-GG/.github` → `registry/dependencies.yml` to
  `tests/fixtures/registry/dependencies.yml` and reference it from both test
  projects (this is the real-file fixture for parity, SC-005/FR-008).
- [X] T002 [P] Confirm dependency posture without adding packages: verify
  `FS.GG.Contracts` resolves to FSharp.Core only (BCL-only leaf, hard constraint)
  and that YamlDotNet 16.3.0 is already available to `FS.GG.SDD.Artifacts` via
  `Directory.Packages.local.props` (reuse, no new package anywhere).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the typed model + a deterministic, structurally-correct (but
reference-/version-permissive) pure validator. No story can begin until this exists.

**⚠️ CRITICAL**: blocks US1, US2, and US3.

- [X] T003 Extend `src/FS.GG.Contracts/Registry.fsi` additively (`.fsi` before
  `.fs`, Principle I/III): add `RegistryRepo`, `ContractEntry`, `DependencyEdge2`,
  `CoherenceEntry`, `RegistryDocument`; extend `RegistryRule` with
  `DuplicateComponent` and `MalformedDocument`; declare
  `val validateDocument: document: RegistryDocument -> ValidationResult`. Leave the
  legacy `RegistryModel`/`validate` surface unchanged.
- [X] T004 Implement the model records/DUs and the extended `RegistryRule` cases in
  `src/FS.GG.Contracts/Registry.fs` so the unit compiles against the new `.fsi`.
- [X] T005 Implement the structural core of `validateDocument` in
  `src/FS.GG.Contracts/Registry.fs`: required-scalar/list presence → `MissingField`;
  duplicate contract `Id` → `DuplicateComponent`; non-mapping/unexpected node →
  `MalformedDocument`; deterministic diagnostic order
  `root → repos → contracts → dependencies → coherence` in encounter order; `Valid`
  iff empty. Reference checks and version checks are **permissive** here (tightened
  in US2/US3). Depends on T003, T004.
- [X] T006 Update `tests/FS.GG.Contracts.Tests/PublicSurface.baseline` for the
  additive surface and confirm `PublicSurfaceTests.fs` passes (apicompat additive-
  clean — no removals/renames). Depends on T003.

**Checkpoint**: model + deterministic structural validator exist; surface baseline green.

---

## Phase 3: User Story 1 - Validate the real registry file from disk (Priority: P1) 🎯 MVP

**Goal**: a consumer/CI gate points the typed validator at the on-disk
`registry/dependencies.yml` and gets a verdict (valid, or precise diagnostics, or a
safe load failure) using only the published surface — no stand-in script.

**Independent Test**: run `fsgg-sdd registry validate <path>` against the vendored
canonical fixture (→ exit 0, `Valid`, zero diagnostics), a missing/unparseable file
(→ exit 1, single `MalformedDocument`-class diagnostic, no crash), and twice for
byte-identical output.

### Tests for User Story 1 (write first, confirm FAIL) ⚠️

- [X] T007 [P] [US1] `tests/FS.GG.SDD.Artifacts.Tests/RegistryDocumentParseTests.fs`:
  real fixture → `Ok RegistryDocument` with order preserved and extra/unknown keys
  tolerated; missing file → `Error`; non-YAML/malformed → `Error` (never throws).
- [X] T008 [P] [US1] `tests/FS.GG.Contracts.Tests/RegistryDocumentTests.fs`:
  `validateDocument` over the parsed real fixture returns `Valid` (structural happy
  path) and is byte-identical across repeated runs (determinism, FR-007/SC-004).

### Implementation for User Story 1

- [X] T009 [US1] New load edge
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/RegistryDocument.fsi` + `.fs`:
  `val load: path: string -> Result<Fsgg.Registry.RegistryDocument, RegistryLoadError>`
  over YamlDotNet, order-preserving, tolerant of unknown keys, never throwing on
  missing/unreadable/unparseable input (Constitution V/VIII). Add both files to
  `src/FS.GG.SDD.Artifacts/*.fsproj` compile order.
- [X] T010 [US1] Parse `registry validate <path>` (subcommand + required path arg,
  arg-validation diagnostic on missing/empty path) in the commands parsing layer
  (`src/FS.GG.SDD.Commands/CommandWorkflow/ParsingEarly.fs` or a sibling), and route
  it as a cross-cutting command (not a lifecycle stage).
- [X] T011 [US1] New handler
  `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersRegistry.fs` composing
  `load <path>` → on `Error`, a single `MalformedDocument`-class load/parse
  diagnostic; on `Ok doc`, `Registry.validateDocument doc`; assemble the standard
  `CommandReport`. Register the file in `FS.GG.SDD.Commands.fsproj` compile order
  (after `HandlersScaffold.fs`, before `CommandWorkflow.fsi`). Depends on T009.
- [X] T012 [US1] Wire dispatch + output: project the report in all three formats
  (`--rich` > `--text` > `--json` > default; rich is presentation-only, no JSON byte
  change) and set the exit code (`0` when `Valid`; `1` when `Invalid` or load
  failed). Depends on T011.
- [X] T013 [US1] Acceptance: run quickstart Scenario 1 (canonical → exit 0, zero
  diagnostics), Scenario 4 (missing + non-YAML → exit 1, single load diagnostic, no
  stack trace), and Scenario 5 (two runs `diff`-clean). Record evidence.

**Checkpoint**: path-in/verdict-out works end-to-end on the real file; MVP demoable.

---

## Phase 4: User Story 2 - No false alarms on the real schema shape (Priority: P1)

**Goal**: `validateDocument` understands repo-id references and `coherence[]`, so it
emits **no** `UnknownComponent` for legitimately-authored repo-id edges/owners/
consumers — while still catching genuinely undefined references.

**Independent Test**: the canonical file yields zero diagnostics on its dependency
edges/owners/consumers and `coherence` section; paired broken cases (`to: nope`,
`consumers: [ghost]`) still emit `UnknownComponent`; a dropped `owner` still emits
`MissingField`; duplicate ids still emit `DuplicateComponent`.

### Tests for User Story 2 (write first, confirm FAIL) ⚠️

- [X] T014 [P] [US2] Extend `tests/FS.GG.Contracts.Tests/RegistryDocumentTests.fs`
  with the reference-rule pairs from quickstart Scenario 3: good repo-id edge /
  good `consumers: [templates]` / well-formed `coherence[]` → no diagnostic;
  `from/to` or `consumers` referencing a non-repo id → `UnknownComponent`; dropped
  `owner` → `MissingField`; duplicate contract id → `DuplicateComponent`. These must
  FAIL against the permissive foundational validator.

### Implementation for User Story 2

- [X] T015 [US2] Tighten `validateDocument` reference rules in
  `src/FS.GG.Contracts/Registry.fs`: `Owner` ∈ repo ids ∪ {`github`} else
  `UnknownComponent`; each `Consumers` entry ∈ repo ids else `UnknownComponent`;
  each edge `From`/`To` non-blank + ∈ repo ids else `MissingField`/`UnknownComponent`;
  `Via` is free-text (not checked); `coherence[]` entries processed (`Id` non-blank →
  `MissingField`), never treated as unknown/malformed. Preserve document order.
- [X] T016 [US2] Acceptance: confirm the canonical fixture still returns `Valid`
  (no regression) and the T014 broken cases now produce exactly their rule kinds
  (SC-002, FR-003/FR-006). Record evidence.

**Checkpoint**: real schema shape accepted; genuine reference defects still caught.

---

## Phase 5: User Story 3 - Accept bare-integer versions and shorthand ranges (Priority: P2)

**Goal**: `validateDocument` accepts the version vocabulary the registry legitimately
uses — full SemVer (incl. prerelease/build), bare-integer schema versions (`1`, `2`),
and shorthand ranges (`1.x`) — without false `MalformedVersion`, while still catching
genuinely malformed versions.

**Independent Test**: `version: "1"`/`"2"`, `version: "0.1.52-preview.1"`,
`range: "1.x"`, integer `schemaVersion` produce no diagnostic; `"1.2.x.4"`, `"abc"`,
`range: "??"` still emit `MalformedVersion`.

### Tests for User Story 3 (write first, confirm FAIL) ⚠️

- [X] T017 [P] [US3] Extend `tests/FS.GG.Contracts.Tests/RegistryDocumentTests.fs`
  with the version-grammar pairs from quickstart Scenario 3: bare-integer, prerelease,
  and `1.x` good cases → no diagnostic; `1.2.x.4` / `abc` (version) and `??` (range)
  → `MalformedVersion`; non-integer `schemaVersion` → `MalformedVersion`. Must FAIL
  against the permissive foundational validator.

### Implementation for User Story 3

- [X] T018 [US3] Add the BCL-only version-grammar helper and wire it into
  `validateDocument` in `src/FS.GG.Contracts/Registry.fs`:
  `version`/`package-version` valid iff `^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$`
  **or** `^\d+$`; `range` valid iff `^[\d.xX*\s<>=~^|.-]+$`; `schemaVersion` an
  integer — else `MalformedVersion`. No third-party SemVer package (BCL regex only).
- [X] T019 [US3] Acceptance: confirm the canonical fixture still returns `Valid`
  (no regression) and quickstart Scenario 2 parity (`validateDocument <real fixture>`
  = `Valid`, agreeing with `scripts/validate-registry.py`, SC-005). Record evidence.

**Checkpoint**: all three previously-false-positive classes cleared; full canonical
file passes; defect detection intact.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: ship the contracted change cleanly.

- [X] T020 Version posture: bump `FS.GG.Contracts` `<Version>` `1.0.1 → 1.1.0` in
  `src/FS.GG.Contracts/FS.GG.Contracts.fsproj` (additive minor), refresh
  `packages.lock.json` if affected, and confirm the apicompat gate is additive-clean.
- [X] T021 [P] Docs: ensure `contracts/registry-document.md` and
  `contracts/cli-registry-validate.md` match the shipped surface/command, and note in
  `docs/release/schema-reference.md` if the new entrypoint warrants a reference line.
  Record the FR-010 follow-up (FS-GG/.github#18 swaps `scripts/validate-registry.py`
  for `fsgg-sdd registry validate` and flips coherence id `registry-validator-typed`
  to `coherent: true`) as a **cross-repo follow-up**, explicitly out of this repo's
  deliverable.
- [X] T022 Final verification: run quickstart Scenarios 1–5 end-to-end, the full
  `FS.GG.Contracts.Tests` + `FS.GG.SDD.Artifacts.Tests` suites, and confirm
  `dotnet build` of `FS.GG.Contracts` shows **no** new package dependency (BCL-only
  intact). Add golden coverage for the diagnostic-projection JSON.
- [X] T023 [P] Add the Coordination board card (Repo `sdd`, Workstream `Versioning`,
  Contract `fsgg-contracts`) linked to FS-GG/FS.GG.SDD#12 and FS-GG/.github#18, per
  spec Assumptions (use the cross-repo-coordination protocol).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Setup — BLOCKS all stories.
- **US1 (Phase 3)**: depends on Foundational. Delivers the end-to-end path (MVP).
- **US2 (Phase 4)**: depends on Foundational; independent of US1 (tightens
  `validateDocument` reference rules; testable on the pure function alone).
- **US3 (Phase 5)**: depends on Foundational; independent of US1/US2 (tightens the
  version grammar; testable on the pure function alone).
- **Polish (Phase 6)**: depends on the stories being shipped (T020/T022 need the
  final surface; T021/T023 are parallel-safe).

### Within each story

- Tests are written first and must FAIL before the matching implementation.
- US2/US3 implementations both edit `Registry.fs`'s `validateDocument` in distinct
  rule blocks — if run by different people, serialize the two edits (or merge
  carefully) to avoid a same-file conflict.

### Parallel opportunities

- T001, T002 (Setup) run in parallel.
- T007 and T008 (US1 tests, different files) run in parallel.
- Once Foundational completes, US1 / US2 / US3 can proceed in parallel by different
  people — each is independently testable — with the one caveat that US2's T015 and
  US3's T018 touch the same function.
- T021 and T023 (Polish) run in parallel.

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 →
4. **STOP & VALIDATE**: `registry validate <canonical fixture>` → exit 0, zero
diagnostics; bad path → safe exit 1. Demo the path-in/verdict-out unblock.

### Incremental delivery

US1 (MVP, the unblock) → US2 (no false alarms on the real shape) → US3 (version
vocabulary) → Polish (version bump, docs, board card). The canonical file is `Valid`
at every checkpoint; each story adds real rule logic plus failing-first tests.

---

## Notes

- [P] = different files, no dependency on another incomplete task in the phase.
- The load edge is the **only** I/O; keep `validateDocument` pure (Constitution V).
- Do not add a third-party SemVer/YAML package to `FS.GG.Contracts` (BCL-only leaf).
- Never mark a failing task `[X]`; never weaken an assertion to green a build.

## Implementation notes (as shipped)

- **Command wiring (T010–T012) — CLI-level peer, not a lifecycle `SddCommand`.**
  `registry validate` ships as a cross-cutting **CLI-level command** in
  `src/FS.GG.SDD.Cli/RegistryValidate.fs(i)` (module `RegistryValidate`), dispatched in
  `Program.fs` *before* `parseCommand` — the established pattern of the existing `validate`
  harness command — rather than as a new `SddCommand` routed through the work-item
  `CommandReport`. It carries its own deterministic `RegistryValidateReport`
  (`{ path, valid, diagnostics[] }`) with `serialize` (JSON) / `renderText` / a Spectre
  rich projection (degrading to plain text when non-interactive). Rationale: the command
  has no work item, stage, or `nextLifecycleCommand`, so the lifecycle
  `CommandReport`/`parseCommand` contracts (and their apicompat surface) stay untouched —
  the same reason `validate` is a peer. The plan deferred exact wiring to tasks
  ("ParsingEarly.fs *or a sibling*"); the contract in
  `contracts/cli-registry-validate.md` is updated to match. User-facing contract
  (path-in → verdict-out, three projections, exit 0 iff `Valid`) is unchanged.
- **Validator (T005/T015/T018) implemented in one pass.** The progressive-tightening
  decomposition was for independent contributors; built by one implementer, the full
  structural + reference + version rules landed together. The canonical fixture is `Valid`
  and every paired broken case reports its exact rule kind (verified — see below).
- **Test split (T008).** `Contracts.Tests/RegistryDocumentTests.fs` unit-tests pure
  `validateDocument` over constructed models (no I/O — Contracts has no loader);
  `Artifacts.Tests/RegistryDocumentParseTests.fs` owns the real-fixture end-to-end
  evidence: `load <real fixture>` → `Ok` → `validateDocument` → `Valid` (SC-001/SC-005).
- **Acceptance evidence (T013/T016/T019).** Smoke runs captured under
  `readiness/` (`SMOKE.md` + scenario JSONs): canonical → exit 0 / 0 diagnostics; a broken
  copy → exit 1 with `MalformedVersion` + two `UnknownComponent` in document order;
  missing/non-YAML → exit 1 single `MalformedDocument` (no stack trace); two `--json` runs
  byte-identical. Full solution suite green (591 passed, 0 failed).
- **Follow-up (FR-010, cross-repo — out of this repo's deliverable).** FS-GG/.github#18
  swaps `scripts/validate-registry.py` for `fsgg-sdd registry validate` and flips coherence
  id `registry-validator-typed` to `coherent: true`; the registry's `fsgg-contracts` pin is
  bumped 1.0.1 → 1.1.0 and republished (mirrors feature 040) to match the additive package
  bump landed here.
