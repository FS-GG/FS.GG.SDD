# Tasks: Document Load-Bearing Authoring Contracts & Self-Correcting Diagnostics

**Input**: Design documents from `/specs/046-document-authoring-contracts/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tier**: Tier 1 (command-output contract — diagnostic string values in JSON / `checklist.md`
change). Phase classification matches the spec's overall tier, so no per-task `[T1]`/`[T2]`
annotations are emitted.

**Tests**: Tests ARE in scope for this feature (Principle VI: golden fixtures for each enriched
diagnostic, plus the SC-005 drift guard). Test tasks precede the string/code edits and MUST be
red before the implementation task that greens them.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on another incomplete task in this phase)
- **[Story]**: `[US1]`/`[US2]`/`[US3]` — which user story the task serves
- Each task names an exact file path

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the verified source facts are still accurate before any string is edited
(plan.md requires re-verifying line references against `main` before changing diagnostic text).

- [X] T001 [P] Re-verify the strict coverage regex (`^\s*-\s*(FR-\d{3,})\s*:\s*(.+)$` + same-line `AC-`/`US-` scan) in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Specification.fs` (`requirementReferences`) and its consumer `requirementCoverage`/`hasCoverage` in `src/FS.GG.SDD.Commands/CommandWorkflow/ParsingMid.fs`; note the confirmed line numbers in research.md R1 if they have drifted.
- [X] T002 [P] Re-verify the evidence vocabulary and satisfaction ladder: `EvidenceKind`/`parseEvidenceKind` (silent fallback to `Verification`) in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Evidence.fs`, `normalizedEvidenceResult` + `evidenceObligations` correction in `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersEvidence.fs`, and the non-synthetic-`pass` disposition in `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersVerify.fs`; update research.md R2 line refs if drifted.
- [X] T003 [P] Re-verify the intent fact computation (`missing` list) in `src/FS.GG.SDD.Commands/CommandWorkflow/ParsingEarly.fs` and the `missingSpecificationIntent` correction site (~line 151) in `src/FS.GG.SDD.Commands/CommandReports.fs`; confirm the **Message already names the missing facts** (R3) so only the Correction text needs enriching.

**Checkpoint**: All three diagnostic sites and three parser sources confirmed against `main`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Wire up the one new code artifact and record the MVU/idiomatic-simplicity stance.

- [X] T004 Register the new test file `tests/FS.GG.SDD.Commands.Tests/AuthoringDocsContractTests.fs` in `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj` (add an empty/compiling module in correct `<Compile Include=...>` order) so US1's drift-guard test can build before its body is written.
- [X] T005 Record Elmish/MVU applicability: this feature adds **no** new stateful or I/O-bearing workflow — all edits are localized string changes in existing report/handler builders plus authored Markdown. Principle V (Elmish/MVU boundary) and Principle IV (no new abstraction) are **not applicable**; note this on the evidence-obligations line of the eventual `evidence.yml` for this work item.

**Checkpoint**: Test project compiles with the new (empty) test module; no `.fsi` surface changed.

---

## Phase 3: User Story 1 - Author finds the coverage and evidence contracts without decompiling (Priority: P1) 🎯 MVP

**Goal**: Publish a durable authoring reference (and quickstart mirrors) that states the exact
accepted/rejected coverage forms and the full `evidence.yml` vocabulary + satisfaction rule, and
keep it co-verified against the live parsers so it can never silently drift.

**Independent Test**: Hand the published reference (and quickstart) to someone who has never seen
the CLI internals; they author a passing coverage line and a satisfying `evidence.yml` on the
first attempt using only the docs (SC-001).

### Tests for User Story 1 (write FIRST — must FAIL before docs exist)

- [X] T006 [US1] Implement the SC-005 drift guard in `tests/FS.GG.SDD.Commands.Tests/AuthoringDocsContractTests.fs`: read `docs/reference/authoring-contracts.md` via a stable repo-relative path; extract the tagged fenced blocks (`coverage:accepted`, `coverage:rejected`, `evidence:satisfied`, `evidence:unsatisfied`). Drive the **public** parse surface (the raw `requirementReferences`/`parseEvidenceKind` functions are restricted by `Specification.fsi`/`Evidence.fsi` and there is no `InternalsVisibleTo`, so they are not callable from the test assembly — using them would force a Tier-1 surface change, which this feature does not take): wrap each `coverage:accepted`/`coverage:rejected` line in a minimal spec `FileSnapshot`, run `Specification.parseSpecificationFacts`, and assert the line contributes ≥1 (accepted) or 0 (rejected) entries with an acceptance-scenario id to `.RequirementReferences`; wrap each `evidence:*` block in an evidence `FileSnapshot`, run `Evidence.parseEvidence`, and assert the parsed `EvidenceDeclaration` (real `kind`/`result`/`synthetic` vocabulary) satisfies iff `normalizedEvidenceResult result = "pass" && not synthetic` — the same non-synthetic-`pass` rule the verify/evidence disposition ladders apply (re-expressed in one line in the test because that rule is not exposed as a public predicate; T002 keeps it in sync with the handler ladder). (Red until T007.)

### Implementation for User Story 1

- [X] T007 [US1] Create `docs/reference/authoring-contracts.md` with the three required sections from contracts/authoring-reference.md: (1) the accepted `- FR-###:` coverage form with a copyable example and the explicit "does not establish coverage" list (bold `**FR-###**`, bracketed-only AC tags, colon-less lines) with the loose-scan-vs-strict-scan reason; (2) the `evidence.yml` `kind` vocabulary (incl. the silent `verification` fallback), `result` vocabulary, the non-synthetic-`pass` satisfaction rule, and a copyable satisfying example; (3) the `specify --input` labeled-line intent facts. Include the machine-checkable tagged fenced example blocks the T006 guard reads. (Greens T006.)
- [X] T008 [P] [US1] Mirror the happy path in `docs/quickstart.md` (FR-005): add a valid coverage-line example at the `checklist` stage and a satisfying `evidence.yml` snippet at the `evidence` stage of the per-stage lifecycle table, and link `reference/authoring-contracts.md`.
- [X] T009 [P] [US1] Link the new reference from `docs/index.md` so it is discoverable from the docs index.
- [X] T010 [US1] Verify FR-006/SC-005 end to end: run `dotnet test tests/FS.GG.SDD.Commands.Tests --filter AuthoringDocsContractTests` and confirm green (every documented accepted form accepted, every rejected form rejected by the live parsers).

**Checkpoint**: The authoring contracts are published, discoverable, and co-verified. The
"forced decompilation" defect is removed by documentation alone (US1 is independently shippable).

---

## Phase 4: User Story 2 - A failing gate tells the author the exact form to write (Priority: P2)

**Goal**: Enrich the `Correction` string of the three in-scope diagnostics so a failure shows the
exact expected form inline — while keeping codes, severities, blocking, exit codes, routing, the
JSON field set, and pass/fail outcomes invariant (FR-010).

**Independent Test**: Author each failure (uncovered requirement, unsatisfied evidence obligation,
intent string missing one fact), run the command, and confirm the diagnostic names the offending
item and shows the exact expected form such that following it verbatim resolves the failure (SC-003).

### Tests for User Story 2 (update goldens FIRST — must FAIL against current strings)

- [X] T011 [P] [US2] Update the checklist missing-coverage golden(s) in `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs` (and any `checklist.md`/work-model golden that embeds the correction) to the enriched form from contracts/diagnostic-corrections.md Site 1 — naming the `FR-###` and showing `- {FR}: <text> (covers AC-###)` and that bold/colon-less forms are not recognized. (Red until T014.)
- [X] T012 [P] [US2] Update the evidence unsatisfied-obligation golden(s) in `tests/FS.GG.SDD.Commands.Tests/EvidenceCommandTests.fs` and `tests/FS.GG.SDD.Commands.Tests/VerifyCommandTests.fs` (+ any work-model golden surfacing this correction) to Site 2's enriched form — a matching **non-synthetic** declaration with `result: pass` (synthetic pass does not satisfy) or an accepted deferral. (Red until T015.)
- [X] T013 [P] [US2] Update the `missingSpecificationIntent` golden(s) in `tests/FS.GG.SDD.Commands.Tests/SpecifyCommandTests.fs` (+ `CommandReportJsonTests.fs`/`TextProjectionTests.fs` if they assert this correction) to Site 3's enriched form showing the labeled-line `value:` / `scope:` / `requirement:` input, keeping the Message unchanged. (Red until T016.)

### Implementation for User Story 2

- [X] T014 [US2] Enrich the checklist missing-coverage `Correction` (FR-007) in the `fail` branch of `plannedChecklistReviews` in `src/FS.GG.SDD.Commands/CommandWorkflow/ParsingMid.fs` (current site `ParsingMid.fs:163` — re-confirm via T001); keep status `fail`, `Blocking = true`, the named `FR-###`, and CHK/CR id allocation unchanged. (Greens T011.)
- [X] T015 [US2] Enrich the unsatisfied-obligation `Correction` (FR-008) in `evidenceObligations` in `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersEvidence.fs`; keep obligation id/kind/linkage and the `missing` disposition unchanged. (Greens T012.)
- [X] T016 [US2] Enrich the `missingSpecificationIntent` `Correction` (FR-009) in `src/FS.GG.SDD.Commands/CommandReports.fs` to show the labeled-line form; do **not** touch `ParsingEarly.fs` fact computation (already correct) and do not change the Message. (Greens T013.)
- [X] T017 [US2] Run `dotnet test tests/FS.GG.SDD.Commands.Tests` and confirm all three golden groups are green; diff the regenerated goldens to confirm the **only** JSON changes are the three `correction` strings — no field added/removed/renamed, no code/severity/blocking/exit/routing/outcome change (FR-010 / SC-004).

**Checkpoint**: Each gate failure self-corrects from the terminal; the automation contract is
provably unchanged except for the three intended correction strings.

---

## Phase 5: User Story 3 - "Evidence" is unambiguous in the SDD authoring surface (Priority: P3)

**Goal**: Make clear, by name, that SDD's lifecycle `evidence.yml` is distinct from any unrelated
"evidence" document a scaffolded product may ship — entirely within SDD-owned docs/diagnostics.

**Independent Test**: Search the SDD authoring docs for "evidence"; the lifecycle concept is
consistently named `evidence.yml` and a note warns a scaffolded product may carry a separate,
unrelated "evidence" document — with no external provider package/path/URL referenced.

### Implementation for User Story 3

- [X] T018 [US3] Add the disambiguation note (FR-011) to the `evidence.yml` section of `docs/reference/authoring-contracts.md`: name SDD's concept as the lifecycle `evidence.yml` contract and warn that a scaffolded product may ship a separate, unrelated "evidence" document that does not describe this contract. (Extends T007; same file.)
- [X] T019 [US3] Confirm FR-011/SC-006 boundary: grep `docs/reference/authoring-contracts.md` and `docs/quickstart.md` to assert the disambiguation references **no** external provider package id, template id, file path, or docs URL — disambiguation stays in SDD-owned text.

**Checkpoint**: "Evidence" is unambiguous in SDD's authoring surface without naming any external
provider.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Keep the agent surfaces aligned, enforce the no-grammar-change / no-provider-identity
invariants, and run the quickstart validation end to end.

- [X] T020 [P] Add an equivalent "authoring contracts" subsection (coverage line + `evidence.yml` vocabulary/satisfaction rule) to `.claude/skills/fs-gg-sdd-project/SKILL.md` (FR-013), summarizing the reference doc.
- [X] T021 [P] Add the **same** subsection to `.codex/skills/fs-gg-sdd-project/SKILL.md` (FR-013), kept byte-for-byte equivalent in facts to the Claude surface (Principle VII).
- [X] T022 Enforce FR-012/SC-006 across the whole diff: `git diff` confirms no change to the coverage/evidence parsing grammars (`Specification.fs` `requirementReferences`, `Evidence.fs` `parseEvidenceKind`) and no external provider package id / template id / path / docs URL introduced anywhere this feature touches.
- [X] T023 Run the quickstart.md validation Scenarios A–E from repo root (`dotnet build`, `dotnet test tests/FS.GG.SDD.Commands.Tests`, the three diagnostic scenarios, the docs-self-sufficiency scenario, and the contract-invariants diff/grep); record results.
- [-] T024 Skipped — `/speckit-analyze` not run as a separate pass. Cross-artifact consistency is enforced mechanically by the SC-005 drift guard (docs ↔ live parsers, 4/4 green) and the FR-010/FR-012 invariant checks (no parser-grammar diff, no provider identity in added doc lines, public-surface baseline unchanged); no drift found.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately; T001–T003 are read-only and parallel.
- **Foundational (Phase 2)**: After Setup. T004 (test-file registration) blocks US1's T006.
- **US1 (Phase 3)**: After Foundational. T006 (test) before T007 (doc); T008/T009 parallel after T007; T010 verifies.
- **US2 (Phase 4)**: After Foundational. Independent of US1 (different files). Tests T011–T013 before impl T014–T016; T017 verifies.
- **US3 (Phase 5)**: After US1's T007 (extends the same reference doc). Small.
- **Polish (Phase 6)**: After US1 and US2 content is final (agent surfaces summarize both); T022–T024 last.

### User Story Dependencies

- **US1 (P1)**: Independent — documentation + drift guard. The MVP; removes the core defect alone.
- **US2 (P2)**: Independent of US1 (diagnostic strings live in source/goldens, not docs). Builds conceptually on the contracts US1 documents but does not require the doc files.
- **US3 (P3)**: Depends on US1's reference doc existing (T007), since it adds a note to that file.

### Within Each User Story

- Tests are written/updated to red FIRST, then the implementation greens them.
- US2 site edits (T014/T015/T016) are in three different files and may run in parallel once their goldens (T011/T012/T013) are red.

### Parallel Opportunities

- Setup: T001, T002, T003 all `[P]`.
- US1: T008 and T009 `[P]` after T007.
- US2: T011, T012, T013 `[P]` (different test files); T014, T015, T016 `[P]` (different source files) once goldens are red.
- US3 is small and sequential.
- Polish: T020 and T021 `[P]` (different skill files).

---

## Parallel Example: User Story 2

```bash
# After Foundational, set the three goldens red together (different files):
Task: "Update checklist coverage golden in ChecklistCommandTests.fs"        # T011
Task: "Update evidence obligation golden in EvidenceCommandTests.fs/VerifyCommandTests.fs"  # T012
Task: "Update intent golden in SpecifyCommandTests.fs (+ JSON/Text projections)"            # T013

# Then green them with the three localized source edits together (different files):
Task: "Enrich coverage Correction in ParsingMid.fs"                          # T014
Task: "Enrich obligation Correction in HandlersEvidence.fs"                  # T015
Task: "Enrich missingSpecificationIntent Correction in CommandReports.fs"    # T016
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → Phase 2 Foundational → Phase 3 US1.
2. **STOP and VALIDATE**: the published reference + drift guard remove the forced-decompilation
   defect on their own (SC-001/SC-002/SC-005). Ship if desired.

### Incremental Delivery

1. US1 (docs + drift guard) → MVP.
2. US2 (self-correcting diagnostics) → tests red → source edits → goldens green (SC-003/SC-004).
3. US3 (evidence disambiguation note) → small addition to the reference.
4. Polish: agent parity (FR-013), no-grammar/no-provider-identity guard (FR-012/SC-006),
   quickstart validation, analyze.

---

## Notes

- `[P]` = different files, no dependency on another incomplete task in the phase.
- Never mark a failing task `[X]`; never weaken a golden assertion to green a build — update the
  golden deliberately to the intended enriched string (FR-010) and document it.
- The only intended JSON/`checklist.md` deltas are the three `correction` strings; any other
  golden regression is a defect, not an update.
- No parsing grammar changes and no external provider identity anywhere (FR-012 / SC-006).
