# Tasks: Lifecycle Authoring Papercuts

**Input**: [plan.md](./plan.md), [data-model.md](./data-model.md), [research.md](./research.md)

Ordered by dependency. `[P]` = parallelizable with its siblings. Each phase ends green
(`dotnet build` at minimum) so a failure localizes.

Constitution §I ordering is enforced *within* each phase: `.fsi` → tests → `.fs`.

---

## Phase 1 — US1: clarify title (FR-001)

Smallest, fully independent. Lands first so a bisect has a clean floor.

- [ ] **T001** Add failing tests to `tests/FS.GG.SDD.Commands.Tests/ClarifyCommandTests.fs`:
  spec front-matter title wins over the humanized work id; `--title` still beats the spec; blank
  front-matter title falls back to the work id; the 089 blocked-seed path carries the spec's title.
- [ ] **T002** `EarlyStageAuthoring.fs` `clarificationTemplate` (`:1107`): resolve the title as
  `request.Title → specFacts.FrontMatter.Title → titleFromWorkId workId`, guarding the middle rung with
  `IsNullOrWhiteSpace`. No `.fsi` (module has none).
- [ ] **T003** `dotnet test --filter ClarifyCommandTests` green.

---

## Phase 2 — US2: atomic write (FR-005..010)

Independent of every other phase. One source change, one new test file.

- [ ] **T004** Create `tests/FS.GG.SDD.Commands.Tests/CommandEffectsTests.fs` and add its compile entry
  to `FS.GG.SDD.Commands.Tests.fsproj` **before** `TestSupport.fs`'s consumers (F# compile order).
  Tests: create-new; overwrite-existing; identical content ⇒ destination bytes unchanged; `dryRun`
  writes nothing; `unsafeOverwrite` refuses and leaves no residue; fault injection (parent directory made
  unwritable) ⇒ prior bytes intact, no `.tmp` residue, `toolDefect` diagnostic surfaces; no successful or
  failed write leaves a temp sibling.
- [ ] **T005** `CommandEffects.fs`: add `writeFileAtomic` (private, `try/finally`, temp sibling
  `.{name}.{guid:N}.tmp`, `File.Move(temp, absolute, overwrite = true)`), and call it from the
  `WriteFile` case in place of `File.WriteAllText`. `snapshotIfExists`/`canOverwrite`/`dryRun` untouched.
  No `.fsi` change.
- [ ] **T006** Structural assertion: no `File.WriteAllText` targeting a destination path remains in
  `CommandEffects.fs`.
- [ ] **T007** `dotnet test --filter CommandEffectsTests` green; `dotnet test` still green.

---

## Phase 3 — US3: ambiguity counters (FR-002..004)

Depends on nothing; touches `.fsi`, so signature-first.

- [ ] **T008** `.fsi` first: drop `UnresolvedAmbiguityCount` from `Specification.fsi`
  `SpecificationFacts`, and from `CommandTypes.fsi` `SpecificationSummary`.
- [ ] **T009 [P]** Update tests to the new surface (they will not compile until T010):
  `TextProjectionTests` (no `unresolvedAmbiguities` key), `CommandReportJsonTests` (no
  `unresolvedAmbiguityCount` key), `RichRenderingTests:76` (record construction),
  `SpecificationArtifactTests`, and remove the `SpecifyCommandTests:454` comment that flags the
  incoherence — asserting the resolved state instead.
- [ ] **T010** Implementation: delete the `unresolvedAmbiguityCount` computation
  (`Specification.fs:245-250`) and the record field; drop `SpecificationSummary.UnresolvedAmbiguityCount`
  (`CommandTypes.fs`); drop the construction at `EarlyStageAuthoring.fs:581`; drop the JSON key
  (`CommandSerialization.fs:37`) and the text key (`CommandRendering.fs:26`).
- [ ] **T011** Assert FR-003: `remainingAmbiguityCount`/`blockingAmbiguityCount` and the
  `unresolvedBlockingAmbiguity` gate are unchanged. `dotnet test` green.

---

## Phase 4 — US4: decision refs (FR-011..015)

`.fsi`-first. T012–T014 are the type changes; T015–T017 thread them.

- [ ] **T012** `.fsi` first — `Clarification.fsi`: `RemainingAmbiguity.AmbiguityId: AmbiguityId option`
  → `AmbiguityIds: AmbiguityId list` (D2); delete `RelatedRequirementIds`/`RelatedStoryIds`/
  `RelatedAcceptanceScenarioIds` from `ClarificationQuestion`; delete `RelatedStoryIds`/
  `RelatedAcceptanceScenarioIds` from `ClarificationDecisionFact` (keep `RelatedRequirementIds` — T016
  is its first read site) (D4).
- [ ] **T013** `.fsi` first — `RequirementModel.fsi`: `Decision` gains `RequirementRefs: RequirementId list`,
  `StoryRefs: UserStoryId list`, `AcceptanceRefs: AcceptanceScenarioId list` (D3).
- [ ] **T014** `.fsi` first — `WorkModel.fsi`: `DecisionEntry` gains the three ref lists (D3).
- [ ] **T015 [P]** Failing tests: `ClarificationArtifactTests` (a line naming two AMB ids records both,
  FR-012); `WorkModelTests` (`DEC-003` naming `FR-007`,`FR-001`,`AC-005` yields all three, sorted, FR-011);
  `ClarifyCommandTests` (a multi-ref line containing `FR-999` still blocks with
  `unknownClarificationReference`, FR-013); `TasksCommandTests` (derived task's `requirements:` is
  `[FR-001, FR-007]`, not `[]`, FR-014).
- [ ] **T016** Implementation:
  - `Clarification.fs`: `parseRemainingAmbiguity` drops `List.tryHead` (`:265`); the two parse sites stop
    populating the removed fields (`:202-204`, `:256-258`).
  - `EarlyStageAuthoring.fs:1543,:1674`: `List.choose (item.AmbiguityId |> Option.map _.Value)` →
    `List.collect (item.AmbiguityIds |> List.map _.Value)`.
  - `RequirementModel.fs` `parseDecisions`: extract `FR`/`US`/`AC` ids from the matched line, sorted +
    deduplicated, onto the new `Decision` fields.
  - `WorkModel.fs`: `DecisionEntry` projection + JSON re-parse carry the three lists.
  - `Serialization.fs` `writeDecision`: emit `requirementRefs`/`storyRefs`/`acceptanceRefs`.
  - `TaskGraphAuthoring.fs` `clarificationDecisionTasks` (`:271-278`): pass
    `decision.RelatedRequirementIds` as `requirements` instead of `[]` (FR-014 — and the read site that
    discharges FR-015).
- [ ] **T017** Assert FR-015 by grep: `RelatedRequirementIds` has a read site; `RelatedStoryIds` /
  `RelatedAcceptanceScenarioIds` are gone. `dotnet test` green.

---

## Phase 5 — US5: task refs (FR-016..022)

Widest blast radius. Last, so the four smaller fixes are already proven.

- [ ] **T018** `.fsi` first — `Task.fsi`: document `SourceIds` as derived (no shape change; the field
  stays `string list`).
- [ ] **T019 [P]** Failing tests: `TasksArtifactTests` (typed-refs-only task derives
  `SourceIds = [DEC-001; FR-001]`, FR-016; an explicit `sourceIds: [SB-002]` is retained in the union,
  FR-017; `allTaskDispositionIds` set identical for every fixture, FR-022); `TasksCommandTests`
  (residual-only emission + two runs byte-identical, FR-018; a `DEC-###` task does not carry the id in
  both fields, FR-019); `WorkModelTests` (`relatedIds` includes a `sourceIds`-only id, FR-020);
  `ExampleArtifactsContractTests` (the shipped example's tasks resolve in `evidence` + `verify`, FR-021).
- [ ] **T020** `Task.fs` parse (`:276-279`): `SourceIds` = sorted, deduplicated, upper-cased union of the
  authored `sourceIds:` list, `requirements`, and `decisions` (D5).
- [ ] **T021** `TaskGraphAuthoring.fs`: emitter writes the **residual** `sourceIds:` only (ids not
  recoverable from the typed fields), omitting the key when empty. This alone delivers FR-019 — do **not**
  change what `clarificationDecisionTasks` passes as `sourceIds`, because `maybeTask` reuses that argument
  as the re-gen dedupe key and `[]` would duplicate the task on every re-run.
- [ ] **T022** `WorkModel.fs:879`: `relatedIds = task.SourceIds` (already the sorted, distinct superset).
- [ ] **T023** Verify FR-021 needs no change at `HandlersEvidence.fs:212` / `HandlersVerify.fs:37,154,344`
  — they read `SourceIds`, which now contains the typed refs. Assert, do not edit.
- [ ] **T024** `dotnet test` green.

---

## Phase 6 — Docs, goldens, surface

- [ ] **T025 [P]** `docs/examples/lifecycle-artifacts/tasks.yml` and
  `docs/reference/authoring-contracts.md`: describe the reconciled field semantics (typed refs are
  authored and canonical; `sourceIds:` is derived and carries only the residual). Keep
  `AuthoringDocsContractTests` / `ExampleArtifactsContractTests` passing (FR-025).
- [ ] **T026** Regenerate goldens and fixtures (`FSGG_UPDATE_BASELINE=1 dotnet test`). Review the diff
  as a deliverable (FR-024): digest-only where no semantics moved; a reviewed content diff for
  `relatedIds` (FR-020), the `work-model.json` decision refs (FR-011), and the `tasks.yml` normalization
  (FR-018). **Do not accept an unreviewed golden diff.**
- [ ] **T027** Re-capture `PublicSurface.baseline` for `Commands` and `Cli` if and only if the six
  declared `.fsi` changes moved them (`FSGG_UPDATE_BASELINE=1`). Confirm nothing else moved.
- [ ] **T028** ~~`surface --check` exits 0~~ — **not applicable.** `surface` gates a scaffolded
  workspace's `docs/api-surface/**`; this repo has none and uses the internal reflection
  `PublicSurface.baseline` test (CLAUDE.md). It reports the same 53 missing baselines on `main`.
- [ ] **T029** Full `dotnet test` green. `git diff --stat` reviewed against the declared `Paths:`
  touch-set — nothing outside it (ADR-0021).

---

## Phase 7 — Integrate

- [ ] **T030** Re-run `scripts/fsgg-coord overlap FS.GG.SDD#164 FS.GG.SDD#177` — still DISJOINT.
- [ ] **T031** Commit, push, open PR into `main`. Body: the five defects, the three sharpened diagnoses,
  the golden-diff review, the split-out of #183.
- [ ] **T032** `/code-review` on the diff; address findings.
- [ ] **T033** Merge; `scripts/fsgg-coord done FS.GG.SDD#164 --flip`; `git worktree remove`.

---

## Dependency graph

```
Phase 1 (US1) ─┐
Phase 2 (US2) ─┤
Phase 3 (US3) ─┼─→ Phase 6 (docs/goldens/surface) ─→ Phase 7 (integrate)
Phase 4 (US4) ─┤        ▲
Phase 5 (US5) ─┘────────┘   (Phase 5 depends on Phase 4: clarificationDecisionTasks
                             is edited by both — T016 then T021)
```

Phases 1–3 are mutually independent and independent of 4–5. **Phase 5 depends on Phase 4**: both edit
`TaskGraphAuthoring.clarificationDecisionTasks`, and T021's `sourceIds` change must land on top of
T016's `requirements` change.
