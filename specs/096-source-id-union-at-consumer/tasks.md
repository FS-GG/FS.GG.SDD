# Tasks: The Source-Id Union Belongs at the Consumer

**Spec**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md) · **Issue**: FS-GG/FS.GG.SDD#189

Order is load-bearing. Per Constitution VI, every behavior test is observed **failing** before its
implementation task runs. The golden is regenerated last (plan.md → "Why the golden is regenerated
last"), so it records the fix rather than defining it.

| # | Task | Requirements | Depends on |
|---|---|---|---|
| T001 | **Measure** the pre-change baseline (do not inherit it): `dotnet test` ⇒ 1236 passed / 4 skipped on `bd64f02` | — | — |
| T002 | Pin the current (defective) behavior with two characterization asserts, to prove the tests can see it | FR-001, FR-002 | T001 |
| T003 | Write `AgentGuidanceViewTests` for `relatedIds`: `sourceIds`-only id present; typed-only unchanged; dedup+sort; absent-task safety | FR-001 | T002 |
| T004 | Implement FR-001 — `WorkModel.fs:888` three-way union | FR-001 | T003 (failing) |
| T005 | Write `VerifyCommandTests` for `affectedSourceIds` (the producer is internal to Commands; `VerificationViewTests` only parses): single task, two tasks, no refs, no linked tasks | FR-002, FR-003 | T004 |
| T006 | Implement FR-002 — `HandlersVerify` takes `taskFacts`, derives `SourceIds`; update `:494` call site | FR-002, FR-003 | T005 (failing) |
| T007 | Regenerate the 4 affected `goldens/readiness/*` (verify/ship/ship-verdict/summary — the last three are digest-only); inspect the diff by eye — every changed array must be a lineage the fixture's tasks declare | FR-002, FR-003 | T006 |
| T008 | Assert FR-004: `git diff --exit-code -- src/…/Task.fs`; `TasksArtifactTests` untouched and green | FR-004 | T006 |
| T009 | Comment FR-005 at `HandlersEvidence.fs:212` (why `:276` already unions — do not "fix" this) | FR-005 | — |
| T010 | Comment FR-006 at `TaskGraphAuthoring.fs:275` (parser reads verbatim; union is a consumer concern) | FR-006 | — |
| T011 | Document FR-007 in `docs/reference/authoring-contracts.md` | FR-007 | T004, T006 |
| T012 | Determinism/idempotence FR-008: run `verify` twice ⇒ byte-identical `verify.json` | FR-008 | T006 |
| T013 | FR-009: confirm **no** migration note is owed (`versioning-policy.md:44` — not Breaking) and that `docs/release/` stays untouched | FR-009 | T006 |
| T014 | SC-006: the precise grep finds no present-tense parser-union claim in `src/` or `docs/reference/` | FR-005, FR-006 | T009, T010 |
| T015 | Full suite green vs the **measured** baseline (1236 passed / 4 skipped on `bd64f02`); no regression | all | T007, T008, T011 |

## Coverage

- FR-001: T003, T004
- FR-002: T005, T006, T007
- FR-003: T005, T007, T015
- FR-004: T008
- FR-005: T009, T014
- FR-006: T010, T014
- FR-007: T011
- FR-008: T012
- FR-009: T013
- FR-010: T011

Every FR has at least one task that fails before it (T003, T005, T009, T010, T011) or guards a
regression (T008, T012, T013, T015).

## Parallelism

T009, T010 are comment-only and touch files no other task edits — safe to do at any point.
T003/T004 (Artifacts) and T005/T006 (Commands) are separate assemblies but T005 asserts through the
command surface that depends on T004's assembly; keep them sequential as listed.
