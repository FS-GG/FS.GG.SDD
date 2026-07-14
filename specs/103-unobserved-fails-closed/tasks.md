# Tasks: An Unobserved Pass Fails Closed

**Spec**: `specs/103-unobserved-fails-closed/spec.md` · **Plan**: `plan.md` · **Issue**: FS.GG.SDD#350

## Phase 1 — Request surface

- **T001** `CommandTypes`: add `CommandRequest.RequireObserved: bool` (+ `.fsi`), default `false` at
  every construction site. — FR-001, FR-006
- **T002** `Options`: register `flag "--require-observed"` for `Verify` **and** `Ship`; `Program`:
  read it with `hasFlag`. — FR-001, FR-005
- **T003** `CommandHelp`: advertise the flag on both commands (the `UnknownOptionTests` mirror test
  goes red if help advertises what `Options` does not recognize). — FR-001

## Phase 2 — The verify gate

- **T004** `DiagnosticConstructors` + `CommandReports` (+ `.fsi`): `unobservedRequiredTest` (error).
  — FR-002
- **T005** `HandlersVerify.dispositionSeverity`: `"unobserved" -> "blocking"`. — FR-003
- **T006** `HandlersVerify.verifyTestDispositionViews`: take `requireObserved`; insert the `unobserved`
  arm **immediately above** `satisfied` and **below** `synthetic`/deferral; consume the shared
  `obligationIsObserved`, negated. — FR-002, FR-004, FR-007
- **T007** `HandlersVerify`: raise `verify.unobservedRequiredTest` over the `unobserved` views. — FR-002

## Phase 3 — The ship gate (the fail-open the CLI caught)

- **T008** `DiagnosticConstructors` + `CommandReports` (+ `.fsi`): `unobservedShipEvidence` (error).
  — FR-005
- **T009** `HandlersShip.shipVerificationPrerequisite`: take `requireObserved`; when set and the verify
  record reports `selfAttested > 0`, add the blocking diagnostic. Gate on the record; never re-derive.
  — FR-005
- **T010** `RemediationPointers`: register `verify.unobservedRequiredTest`. — FR-002

## Phase 4 — Evidence (tests + drive)

- **T011** Failure leg: a fabricated lifecycle cannot reach `shipReady` under the flag. **Mutation-check
  it**: disable the `unobserved` arm and confirm the test goes red. — AC-001
- **T012** Stale-record leg: a green `verify.json` from an earlier unflagged run does not launder an
  unobserved pass past `ship`. **Mutation-check it** against removal of the ship gate. — AC-004
- **T013** Non-brick leg: a recorded receipt still reaches `shipReady` under the flag. — AC-002
- **T014** Opt-in leg: without the flag, the fabricated item still reports `verificationReady` /
  `shipReady`, `observed: 0`. — AC-003
- **T015** No-punishment leg: an honest, fully-fielded deferral is not named by the diagnostic. — AC-005
- **T016** Drive the real CLI end to end: walk the lifecycle, reproduce `shipReady` / `observed: 0`,
  refuse it under the flag at both stages, then ship green against a real TRX. **Not optional** — the
  T012 fail-open was invisible to a fully green suite.
- **T017** Re-capture `PublicSurface.baseline` (two new public functions) and confirm no golden moves.
  — FR-006

## Phase 5 — Docs + cross-repo

- **T018** `docs/release/schema-reference.md`: correct the now-false *"a pass with no receipt still
  satisfies"* paragraph; document the flag, both diagnostics, and why it is passed to both stages.
- **T019** File the cross-repo `governance-handoff` minor bump (`registry/dependencies.yml`, then
  `docs/architecture.md` §5) against `FS-GG/.github`. Must land before the default flips. — FR-008
