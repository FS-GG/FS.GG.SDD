# Tasks: Clear the FS3218 / FS3262 warning debt and drop the ratchet exemption

**Feature**: `066-fs3218-warning-cleanup` | **Spec**: `spec.md` | **Plan**: `plan.md`

Dependency-ordered. `[P]` marks tasks that may run in parallel (independent files).

## Phase 1 — Cleanup (US2, behaviour-preserving)

- [ ] **T001 [P]** Align `src/FS.GG.SDD.Commands/CommandReports.fs` forwarding
  parameter names to `src/FS.GG.SDD.Commands/CommandReports.fsi`. For each `let f a0
  a1 … = Inner.f a0 a1 …`, rename `a0 a1 …` to the signature's argument names in
  order. Do not touch the `.fsi`. Covers FR-001, FR-002. (AC US2 §1)
- [ ] **T002 [P]** Replace the two `Option.ofObj v |> Option.defaultValue ""` guards
  in `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs` (lines ~42 `version`,
  ~76 `text`) with direct use of the non-null value. Covers FR-003. (AC US2 §2)
- [ ] **T003** Incremental check: `dotnet build -t:Rebuild` still under the *old*
  exemption emits **zero** FS3218 and **zero** FS3262 (proves T001/T002 cleared every
  occurrence before the exemption is removed).

## Phase 2 — Drop the exemption (US1)

- [ ] **T004** Remove `FS3218;FS3262` from the first `WarningsNotAsErrors` append in
  `Directory.Build.local.props` and rewrite the preceding comment to record the classes
  as cleared in feature 066 (not deferred debt). Leave the NU16xx / RS#### / NU190x
  exemptions and the `WarningsAsErrors` FS3261/FS0025 floor untouched. Covers FR-004.
  (AC US1 §1)

## Phase 3 — Verify (US1 + US2)

- [ ] **T005** `dotnet build -t:Rebuild` → green, 0 warnings / 0 errors with both
  classes now promoted to errors. (SC-001, FR-005, AC US1 §2)
- [ ] **T006** `git diff --stat`: confirm `CommandReports.fsi` unchanged and no golden
  `.json` baseline touched. (FR-002, SC-003)
- [ ] **T007** Full test suite green (`dotnet test`); `fsgg-sdd validate` stays
  `overallPassed`. (FR-005, AC US2 §3)
- [ ] **T008** Ratchet spot-check: temporarily reintroduce one FS3218, confirm the
  build now **fails**, then revert. (SC-004, AC US1 §3)
- [ ] **T009** Confirm the managed org build files are byte-identical to `FS-GG/.github`
  `dist/dotnet/` (the `build-config-drift` gate stays green). (SC-005, FR-006)

## Phase 4 — Land

- [ ] **T010** Commit, push `066-fs3218-warning-cleanup`, open a PR that closes the §2
  (warning-debt) half of issue #85.
