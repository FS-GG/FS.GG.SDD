# Tasks: Cited Evidence Artifacts Must Exist

**Input**: `specs/099-evidence-artifact-existence/{spec,plan}.md`

**Tier**: Tier 1 (new blocking diagnostic id; new blocking behaviour in `evidence` + `verify`;
additive public functions in `FS.GG.SDD.Artifacts`)

**Tracks**: FS.GG.SDD#349

**Sequencing**: no `Blocked by`. Touch-set narrowed to an exact file list, confirmed DISJOINT from
the three other in-flight claims (#352 teal-5a8w, #353 osprey-7c3).

## Format

`[ID] [P?] [Story] Description` — `[X]` done, `[ ]` open, `[-]` dropped. `[P]` = parallelizable.

## Phase 1: Foundational — the rule, stated once

- [ ] T001 [US1] `Evidence.citedArtifactPaths` (`artifacts[]` ∪ `sourceRefs[].path`, never `uri`;
  blanks dropped, deduped, sorted) and `Evidence.missingCitedArtifacts (exists: string -> bool)`
  which returns `[]` unless `result: pass ∧ synthetic: false`
  (`src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Evidence.fs` + `.fsi`) — FR-002, FR-006, FR-007
- [ ] T002 [P] `CommandReports.evidenceArtifactNotFound` — `errorDiagnostic`, id
  `evidence.artifactNotFound` (`CommandReports/DiagnosticConstructors.fs`) + remediation pointer
  (`CommandReports/RemediationPointers.fs`) — FR-001
- [ ] T003 [P] Regenerate `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline`
  (`FSGG_UPDATE_BASELINE=1`) for the two additive functions — Constitution III

## Phase 2: US1 — the evidence stage refuses (the failing test comes first)

- [ ] T004 [US1] **Failure leg, `artifacts:` bucket**: `evidence` over a workspace whose satisfying
  declaration cites a missing path emits blocking `evidence.artifactNotFound` naming the path, and
  writes no `evidence.yml` — assert on the **diagnostic id**, not the exit code
  (`tests/FS.GG.SDD.Commands.Tests/EvidenceCommandTests.fs`) — FR-009, SC-004
- [ ] T005 [US1] **Failure leg, `sourceRefs[].path` bucket**: same refusal via the other bucket —
  the evasion route (`EvidenceCommandTests.fs`) — FR-002, SC-004
- [ ] T006 [P] [US1] **Green leg**: the same declaration with the file present passes, no new
  diagnostic; and a `sourceRefs[].uri`-only declaration never blocks (`EvidenceCommandTests.fs`) — FR-002
- [ ] T007 [US1] Second-wave reads: `HandlersEvidence.citedArtifactReadEffects workId model` →
  `ReadFile` per cited path of a satisfying declaration, deduped against already-planned reads; wired
  at the existing second-wave site in `CommandWorkflow.fs`, gated to `Evidence`/`Verify`/`Ship` — FR-003
- [ ] T008 [US1] Evidence gate: thread `artifactExists` (from the interpreted log) into
  `evidenceValidationDiagnostics`; emit `evidenceArtifactNotFound` for the union of missing cited
  paths (`HandlersEvidence.fs`) — FR-001
- [ ] T009 [US1] `ED-` cascade: an `invalid` arm carrying `evidence.artifactNotFound`, placed beside
  the #306 `missingVisualInspectionArtifact` arm (`HandlersEvidence.evidenceDispositions`) — FR-001

## Phase 3: US2 — the refusal survives to the merge boundary

- [ ] T010 [US2] `TD-` mirror in `verifyTestDispositionViews`: state `invalid`, diagnostic
  `evidence.artifactNotFound`, severity `blocking` (`HandlersVerify.fs`) — FR-004
- [ ] T011 [US2] `verify` test: readiness `needsVerificationCorrection`, `TD-` disposition `invalid`
  and `blocking` (`tests/FS.GG.SDD.Commands.Tests/VerifyCommandTests.fs`) — FR-004
- [ ] T012 [US2] Confirm `ship` reports not-ready with **no `HandlersShip` change** (it already
  aggregates blocking findings) — FR-005

## Phase 4: US3 — only satisfying declarations are held to it

- [ ] T013 [US3] Test: a `deferred` declaration citing an absent path is **not** blocked; a
  `pass` + disclosed-`synthetic` declaration citing an absent path is **not** blocked
  (`EvidenceCommandTests.fs`) — FR-006

## Phase 5: US4 — the published corpus is made honest

- [ ] T014 [US4] Repair `docs/examples/lifecycle-artifacts/`: every path cited by its `evidence.yml`
  must exist in the corpus (6 `tests/ExampleApp.Tests/*.fs` files today cite nothing) — FR-008, SC-002
- [ ] T015 [US4] Census test: every cited path in the example corpus resolves on disk, so the corpus
  cannot rot back to fiction (`tests/FS.GG.SDD.Artifacts.Tests/EvidenceArtifactTests.fs`) — SC-002

## Phase 6: Verification

- [ ] T016 Full offline suite green (`dotnet test FS.GG.SDD.sln -c Debug`), ≥ 1,629 baseline passing
- [ ] T017 Confirm the `tests/fixtures/**` corpora do **not** block (read by the pure parser, never
  driven through the Commands evidence gate) — verified empirically, per spec **Out of Scope**
