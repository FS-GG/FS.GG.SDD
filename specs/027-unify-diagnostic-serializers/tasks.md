---
description: "Task list for 027 — Collapse Diagnostic Builder + Unify JSON Serializers"
---

# Tasks: Collapse Diagnostic Builder + Unify JSON Serializers

**Input**: Design documents from `/specs/027-unify-diagnostic-serializers/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md,
contracts/diagnostic-builder.md, contracts/shared-json-writers.fsi.md,
quickstart.md

**Change Tier**: Tier 2 (internal change). Behavior-preserving refactor. The
binding gate is **byte-identical** output (`--json` for every command + the
serialized work-model JSON) plus byte-stable public `.fsi` and surface-area
baselines for `CommandReports`, `CommandSerialization`, `Serialization`.

**Tests**: This is a behavior-preserving refactor, so the test discipline is
*negative*: the existing golden / determinism / surface-baseline suites are the
gate and must stay green and byte-identical. The captured pre-change baseline is
the fixture. New assertions are added only where a coverage gap is found
(research D5). No new failing-first tests are written for behavior that does not
change.

**Organization**: Phase 1 (setup/baseline) → Phase 2 (foundational, none
blocking) → Phase 3 User Story 1 (diagnostic builder collapse, P1) → Phase 4
User Story 2 (serializer unification, P2) → Phase 5 polish. Stories are
sequenced US1 → US2 per the spec assumption (Story 2 is verified against a
Story-1-stable baseline).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase
- **[Story]**: `US1` / `US2`; omitted for setup / cross-cutting tasks
- Tier annotations omitted — every phase is Tier 2, matching the spec

---

## Phase 1: Setup & Pre-Change Baseline (Shared Infrastructure)

**Purpose**: Capture the immutable byte-for-byte baseline that every later gate
diffs against. This MUST be done on the unmodified `027-…` tree before any edit
(quickstart §1).

- [X] T001 Confirm a clean starting point: run `dotnet test` from repo root and — **Done.** Baseline green at **438** tests (104+18+265+51).
  record the green baseline test count (spec says 438; re-measure and note the
  actual). Abort the feature if the tree is not green before any edit.
- [X] T002 [P] Snapshot the surface guards to a scratch dir: copy — **Done.** Snapshotted to `/tmp/r6/` (2 baselines + 3 `.fsi`).
  `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline`,
  `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline`,
  `src/FS.GG.SDD.Artifacts/Serialization.fsi`,
  `src/FS.GG.SDD.Commands/CommandSerialization.fsi`, and
  `src/FS.GG.SDD.Commands/CommandReports.fsi` to `/tmp/r6/` (quickstart §1).
- [X] T003 [P] Snapshot representative `--json` output to `/tmp/r6/<cmd>.before.json` — **Done.** Byte-identical gate carried by the in-suite golden/determinism tests, which drive charter/analyze/refresh + the `duplicate-work-id` fixture (non-empty `relatedIds`) and the work-model JSON.
  for the SC-004 set — at minimum **charter**, **analyze**, **refresh**, and the
  pinned **diagnostic-emitting failure path** `tests/fixtures/lifecycle-commands/duplicate-work-id`
  (a duplicate-id diagnostic with non-empty `relatedIds`, so it exercises the
  `relatedIds`-ordering risk of research D2) — plus the serialized **work-model
  JSON**, driven from the existing fixtures under
  `tests/fixtures/lifecycle-commands/*` (quickstart §1).
- [X] T004 [P] Re-measure and record the baseline numbers the spec/contract — **Done.** Re-measured: 99 `DiagnosticError`, 14 `DiagnosticWarning` grep hits (13 warning constructors + 1 severity comparison at line 1415); 1477/455/357 LOC confirmed.
  depend on, from the unmodified tree:
  `grep -c "DiagnosticError" src/FS.GG.SDD.Commands/CommandReports.fs` (expect 99),
  `grep -c "DiagnosticWarning" src/FS.GG.SDD.Commands/CommandReports.fs` (expect 14),
  and `wc -l` of `CommandReports.fs` / `CommandSerialization.fs` /
  `Serialization.fs` (1477 / 455 / 357) for the SC-006 net-LOC check. Record the
  enumerated list of the 14 warning constructors (cross-check against
  contracts/diagnostic-builder.md) — this list is the FR-002 no-flip checklist.

**Checkpoint**: `/tmp/r6/` holds the frozen baseline (surface, JSON, counts).
Every later gate diffs against it.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: None. This refactor introduces no shared infrastructure that both
stories need before either can start — US1 lives entirely in `CommandReports.fs`
and US2 introduces its own new module. Story sequencing (US1 before US2) is a
verification-ordering choice, not a build dependency.

- [X] T005 (No foundational work.) Confirm the two stories share no common — **Done.** Confirmed: US1 is confined to `CommandReports.fs`; US2 adds `Json/JsonWriters` + edits the two serializers. Disjoint files; ordering is verification-only.
  prerequisite beyond the Phase-1 baseline; record that US1 and US2 touch
  disjoint files (`CommandReports.fs` vs the serializer modules) so the only
  ordering constraint is the spec's "verify US1 stable before US2" assumption.

**Checkpoint**: Foundation trivially ready — US1 may begin.

---

## Phase 3: User Story 1 — One way to build a command diagnostic (Priority: P1) 🎯 MVP

**Goal**: Every command diagnostic in `CommandReports.fs` is built through the
single shared builder. The ~99 hand-spelled `DiagnosticError` literals collapse
to a builder default; the 14 warning constructors keep `DiagnosticWarning`; the
structurally-identical families share their skeleton — all with byte-identical
emitted diagnostics and a byte-identical `CommandReports.fsi`.

**Independent Test**: A grep confirms every diagnostic routes through
`commandDiagnostic` (no inline severity/path/sort); a diagnostic-emitting command
yields a `diagnostics` array byte-identical to the `/tmp/r6` baseline; and
`git diff` of `CommandReports.fsi` is empty.

**Scope**: Single file `src/FS.GG.SDD.Commands/CommandReports.fs`. Public
`CommandReports.fsi` and the Commands `PublicSurface.baseline` are held
byte-identical (FR-007, SC-005). All new builders are internal-only — absent from
the `.fsi` (contracts/diagnostic-builder.md).

### Tests / coverage check for User Story 1

- [X] T006 [US1] Coverage gap check (research D5 open item a/b): confirm the — **Done.** Coverage already sufficed: per-command golden + `ReleaseDeterminismTests` + the `duplicate-work-id` fixture pin the representative set. No new assertion added.
  SC-004 representative set — charter, analyze, refresh, and the pinned
  `duplicate-work-id` failure path — already has golden `--json` coverage in
  `tests/FS.GG.SDD.Commands.Tests/*CommandTests.fs` /
  `ReleaseDeterminismTests.fs`. If a representative diagnostic family or the
  `duplicate-work-id` failure path is not pinned, add one thin golden assertion
  for it (and only it). Note in the task line whether a test was added or
  coverage already sufficed.

### Implementation for User Story 1

- [X] T007 [US1] In `src/FS.GG.SDD.Commands/CommandReports.fs`, add the — **Done.** Added internal `errorDiagnostic`/`warningDiagnostic` over `commandDiagnostic`; absent from `.fsi`.
  internal-only `errorDiagnostic id path message correction relatedIds`
  (fixes `severity = DiagnosticSeverity.DiagnosticError`) and
  `warningDiagnostic …` (fixes `DiagnosticWarning`) helpers layered over the
  existing public `commandDiagnostic` (contracts/diagnostic-builder.md). Do NOT
  add them to `CommandReports.fsi`. (After T004's warning enumeration.)
- [X] T008 [US1] Repoint the ~99 error-severity named constructors in — **Done.** Repointed **97** error constructors; hand-spelled `DiagnosticError` literals collapse to the `errorDiagnostic` default (remaining tokens: helper default + 2 severity comparisons).
  `CommandReports.fs` onto `errorDiagnostic`, removing the hand-spelled
  `DiagnosticSeverity.DiagnosticError` literal at each call site. Every named
  function keeps its exact name and `.fsi` signature (FR-003, FR-007). Verify
  `grep -c "DiagnosticError" CommandReports.fs` drops toward 1 (the single
  default in `errorDiagnostic`).
- [X] T009 [US1] Repoint the 14 warning-severity named constructors (the T004 — **Done.** Repointed **13** warning constructors onto `warningDiagnostic`; no severity flip (remaining `DiagnosticWarning` tokens: helper default + the line-1415 comparison).
  enumerated list) onto `warningDiagnostic`. Confirm each of the 14 still
  resolves to `DiagnosticWarning` — no flip in either direction (FR-002, "severity
  is not uniform" edge case). `grep -c "DiagnosticWarning" CommandReports.fs`
  drops toward 1.
- [X] T010 [US1] Introduce the per-family helpers (`missing*`, `malformed*`, — **Done.** Added family-shape helpers `errorForPath`/`warningForPath` (relatedIds `[path]`, 32+3 sites) and `errorForRef` (relatedIds `[id]`, 24 sites); genuinely-varying-arity families (bulk id-lists, identity pairs, special path+relatedIds) route through the severity-default base. Helpers internal-only.
  `duplicate*`, `unknown*`, `stale*`, `unsafe*`/`failed*`) that capture each
  family's shared `id`/`path`/`message`/`correction` skeleton with the varying
  pieces as parameters, and route each family's named constructors through its
  helper (FR-003, data-model E1). Keep helpers internal (not in `.fsi`). Note the
  `stale*` family spans both severities — route each member through the correct
  default. Do not merge or rename any named function.

### Verification for User Story 1 (gate against the Phase-1 baseline)

- [X] T011 [US1] SC-001 structural check: grep `CommandReports.fs` to confirm no — **Done** (inspection). Every constructor routes through `commandDiagnostic` via a default/family helper; no inline severity/path resolution remains. Left as inspection step (no structural test added).
  named diagnostic constructor still re-implements severity/path/sort inline —
  every one routes through `commandDiagnostic` via a default/family helper. Decide
  per research D5(b) whether to leave this as an inspection step or add a small
  structural assertion; record the choice on the task line.
- [X] T012 [US1] Byte-identical `.fsi`/surface gate (FR-007, SC-005): — **Done.** `git diff --exit-code` over `CommandReports.fsi` + Commands `PublicSurface.baseline` prints nothing; `SurfaceBaselineTests` green.
  `git diff --exit-code -- src/FS.GG.SDD.Commands/CommandReports.fsi
  tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` MUST print nothing.
  `SurfaceBaselineTests` for Commands MUST stay green.
- [X] T013 [US1] Byte-identical output gate (FR-006, FR-009, SC-004): re-emit the — **Done.** 265 Commands tests green incl. golden `--json` + determinism; no golden churn.
  representative `--json` set and diff against `/tmp/r6/<cmd>.before.json` — all
  diffs MUST be empty (ids, severities, paths, messages, corrections, relatedIds,
  order). Run `dotnet test` — all green, no golden churn.

**Checkpoint**: US1 complete and independently verified. `CommandReports.fs` has
one builder convention; the diagnostic contract and surface are byte-identical.
This is a shippable MVP increment on its own.

---

## Phase 4: User Story 2 — One shared set of JSON writer primitives (Priority: P2)

**Goal**: The low-level JSON writers duplicated across `CommandSerialization.fs`
(Commands) and `Serialization.fs` (Artifacts) — `writeDiagnostic`,
`writeOutputDigest`, and the `writeStringList` / digest / location variants — exist
once in a new shared module and are consumed by both serializers, with string-list
ordering and option/bare-digest handling parameterized. Output stays
byte-identical.

**Independent Test**: A grep confirms each previously-duplicated writer body
exists in exactly one location (in `JsonWriters`); both serializers consume it;
the work-model JSON and command `--json` are byte-identical to the (now
Story-1-stable) baseline; and the two serializer `.fsi` files are unchanged.

**Scope**: New module `src/FS.GG.SDD.Artifacts/Json/JsonWriters.fs` + `.fsi`
under `namespace FS.GG.SDD.Artifacts.Json` (research D1 — the sub-namespace keeps
it out of the reflection-based `FS.GG.SDD.Artifacts` surface baseline). Edits to
`Serialization.fs` (Artifacts) and `CommandSerialization.fs` (Commands). The
three named entry-point `.fsi` files stay byte-identical (FR-007, FR-008). Starts
after US1 is verified stable (T013).

### Foundational for User Story 2 (the shared module)

- [X] T014 [US2] Author `src/FS.GG.SDD.Artifacts/Json/JsonWriters.fsi` exactly per — **Done.** Authored `Json/JsonWriters.fsi` per contract (`StringListOrder`, 5 `val`s).
  contracts/shared-json-writers.fsi.md: `namespace FS.GG.SDD.Artifacts.Json`,
  `module JsonWriters`, `type StringListOrder = SourceOrder | Sorted`, and `val`s
  for `writeStringList`, `writeSourceDigest`, `writeOutputDigest`, `writeLocation`,
  `writeDiagnostic`. (Constitution I — `.fsi` first.)
- [X] T015 [US2] Implement `src/FS.GG.SDD.Artifacts/Json/JsonWriters.fs` against — **Done.** Implemented `JsonWriters.fs`; `severityValue` called unqualified (sub-namespace can't qualify the opened `Diagnostics` module).
  the behavioral contract: `writeStringList` applies `List.sort` only for
  `Sorted`; `writeSourceDigest`/`writeOutputDigest` are the option-aware superset
  (`Some` → `{algorithm,value}` object, `None` → `WriteNull name`);
  `writeLocation` takes `name` and renders `{line,column}` number-or-null /
  `WriteNull name`; `writeDiagnostic` writes fields in **exactly** the order
  `id, severity (severityValue), artifact, location (name "location"), message,
  correction, relatedIds (via writeStringList with relatedIdsOrder)`
  (contracts/shared-json-writers.fsi.md, research D2/D3).
- [X] T016 [US2] Register the new module in — **Done.** Registered `Json/JsonWriters.fsi`/`.fs` after `Diagnostics.fs` in the Artifacts fsproj.
  `src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj` — add `Json/JsonWriters.fsi`
  then `Json/JsonWriters.fs` to `<Compile>` order **after** `Diagnostics.fs`
  (line 17) and **before** `Serialization.fsi` (line 59), since `Serialization.fs`
  will consume it. (After T014/T015.)

### Implementation for User Story 2 (repoint both serializers)

- [X] T017 [US2] Edit `src/FS.GG.SDD.Artifacts/Serialization.fs` to consume — **Done.** `Serialization.fs` consumes `JsonWriters`; deleted 6 local bodies; `SourceOrder` for lists; bare digests wrapped `Some`; option-aware `writeOutputDigest` collapses the `None` match. `.fsi` byte-stable.
  `FS.GG.SDD.Artifacts.Json.JsonWriters`, deleting the local `writeStringList`,
  `writeDigest`, `writeOutputDigest`, `writeLocation`/`writeSourceLocation`, and
  `writeDiagnostic` bodies. Pass `SourceOrder` for string lists and the
  `writeDiagnostic` `relatedIds`; pass `Some digest` at the bare-digest call sites;
  pass the per-call location name (`"location"` / `"sourceLocation"`). Handle the
  inline `None` digest sites (e.g. `Serialization.fs:42-44,128-130,146-148`) via
  the option-aware primitive. `Serialization.fsi` stays byte-identical. (After
  T016; depends on the Story-1-stable tree, T013.)
- [X] T018 [US2] Edit `src/FS.GG.SDD.Commands/CommandSerialization.fs` to consume — **Done.** `CommandSerialization.fs` consumes `JsonWriters`; deleted 5 local bodies; `Sorted` for lists + diagnostic relatedIds. `.fsi` byte-stable.
  `JsonWriters`, deleting the local `writeStringList`, `writeSourceDigest`,
  `writeOutputDigest`, `writeLocation`, and `writeDiagnostic` bodies. Pass
  `Sorted` for string lists and the `writeDiagnostic` `relatedIds`; location name
  is the fixed `"location"`. `CommandSerialization.fsi` stays byte-identical.
  (After T016; independent of T017 — different file, [P] with T017.)

### Verification for User Story 2 (gate against the baseline)

- [X] T019 [US2] SC-002 duplication check (quickstart §6): — **Done.** `writeDiagnostic`/`writeOutputDigest` + string-list/digest/location each defined exactly once, in `JsonWriters.fs`.
  `grep -rn "let writeDiagnostic\b" src/` and
  `grep -rn "let writeOutputDigest\b" src/` each return **exactly one** definition
  (in `JsonWriters.fs`); likewise no second body of the string-list/digest/location
  writers remains across the two assemblies.
- [X] T020 [US2] Layering + `.fsi` gate (FR-008, SC-005): — **Done.** No `FS.GG.SDD.Commands` in Artifacts fsproj; `git diff --exit-code` over both serializer `.fsi` + both baselines prints nothing; surface tests green.
  `grep -n "FS.GG.SDD.Commands" src/FS.GG.SDD.Artifacts/*.fsproj` is empty
  (one-way layering preserved); `git diff --exit-code` over
  `Serialization.fsi`, `CommandSerialization.fsi`, and both
  `PublicSurface.baseline` files prints nothing; the Artifacts and Commands
  `SurfaceBaselineTests` stay green (the new module under
  `FS.GG.SDD.Artifacts.Json` is not captured by the exact-namespace reflection —
  research D1).
- [X] T021 [US2] Byte-identical output gate (FR-006, SC-004): re-emit the — **Done.** Full suite green, no golden/determinism churn (work-model JSON + command `--json` byte-identical, incl. `relatedIds` order, digests, nulls, locations).
  representative `--json` set and the work-model JSON and diff against
  `/tmp/r6/*.before.json` — all empty, with particular attention to `relatedIds`
  ordering, digest objects, null fields, and locations (research D2). `dotnet test`
  green, no golden churn.

**Checkpoint**: Both stories complete. Each duplicated writer exists once; both
serializers consume it; all output and surface baselines byte-identical.

---

## Phase 5: Polish & Cross-Cutting Verification

**Purpose**: Whole-feature gates that span both stories (quickstart §3–6,
SC-003/006/007).

- [X] T022 [P] SC-006 net-LOC check: `git diff --stat -- src/` shows a net — **Done.** Net `src` LOC **2146 vs 2289 = −143** (exceeds ~90 est.).
  reduction in `src` line count (analysis est. ≈90 LOC — ≈50 from the diagnostic
  collapse, ≈40 from the serializer unification). Record the actual delta; if the
  net is not negative, investigate residual scaffolding before closing.
- [X] T023 [P] SC-007 Release gate: `dotnet build -c Release` is green with no new — **Done.** `dotnet build -c Release` green, **0 warnings** (R5 FS3261/FS0025 gate intact).
  warning category (the R5 FS3261/FS0025 `WarningsAsErrors` gate still passes).
- [X] T024 SC-003 full-suite regression: `dotnet test` across — **Done.** `dotnet test` green at the 438 baseline across all four suites; no churn.
  `FS.GG.SDD.Artifacts.Tests`, `FS.GG.SDD.Commands.Tests`, `FS.GG.SDD.Cli.Tests`,
  and `FS.GG.SDD.Validation.Tests` is green at the T001 baseline count, no golden
  or determinism churn.
- [X] T025 Walk the quickstart "Done When" checklist end-to-end and confirm every — **Done.** Quickstart 'Done When' all satisfied: empty JSON diffs, unchanged `.fsi`/baselines, green test + Release, each writer in one place, net LOC down.
  box: empty JSON diffs, unchanged `.fsi`/baseline files, green `dotnet test` and
  `dotnet build -c Release`, each unified writer body in exactly one place, net
  `src` LOC down.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup/Baseline)**: no dependencies — must run first on the
  unmodified tree (the baseline is immutable input for every later gate).
- **Phase 2 (Foundational)**: trivial; no blocking work.
- **Phase 3 (US1)**: depends only on Phase 1. Self-contained in `CommandReports.fs`.
- **Phase 4 (US2)**: depends on Phase 1 and is **sequenced after US1 is verified
  stable (T013)** per the spec assumption — the serializer work is checked against
  an already-stable baseline. (US2 touches different files, so this is a
  verification ordering, not a compile dependency.)
- **Phase 5 (Polish)**: depends on both stories complete.

### Within each story

- US1: T007 (helpers) → T008/T009 (repoint error/warning call sites) → T010
  (family helpers) → T011–T013 (gates). T006 coverage check runs first/in parallel.
- US2: T014 (`.fsi`) → T015 (impl) → T016 (fsproj order) → T017 / T018 (repoint
  each serializer, parallel to each other) → T019–T021 (gates).

### Parallel opportunities

- T002, T003, T004 (Phase 1 snapshots) are independent — all `[P]`.
- T017 and T018 edit different files (Artifacts vs Commands serializer) — `[P]`.
- T022 and T023 (LOC + Release gate) are independent `[P]`.

---

## Implementation Strategy

### MVP (User Story 1 only)

1. Phase 1 baseline → Phase 3 US1 → stop and validate against `/tmp/r6` (T011–T013).
2. US1 is independently shippable: one diagnostic builder convention, byte-identical
   contract and surface. Demo/merge if desired before US2.

### Incremental delivery

1. Baseline (Phase 1) → US1 (diagnostic collapse) → verify byte-identical → ship.
2. US2 (serializer unification) on the stable baseline → verify byte-identical → ship.
3. Polish gates (Phase 5) close the feature.

---

## Notes

- This is behavior-preserving: the evidence is that **nothing the contract cares
  about moves** — byte-identical JSON, byte-stable `.fsi` + surface baselines,
  green tests, green Release gate. Never weaken a golden assertion to absorb a
  diff; a non-empty diff is a real regression to fix.
- Elmish/MVU (Principle IV/V): N/A — pure functions only (diagnostic construction,
  JSON writing); no state/I-O boundary is touched (plan Constitution Check).
- All new builders (US1) and the new writer module's consumption (US2) add **no**
  public surface to the three named `.fsi` files; the only new `.fsi` is
  `Json/JsonWriters.fsi` under a sub-namespace deliberately outside the guarded
  baseline.
- `[P]` = different files, no dependency on another incomplete task in the phase.
- Commit after each task or logical group; stop at the US1 checkpoint to validate
  independently.
