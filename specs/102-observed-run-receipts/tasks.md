# Tasks: Observed Run Receipts

**Spec**: `specs/102-observed-run-receipts/spec.md` · **Plan**: `plan.md` · **Issue**: FS.GG.SDD#350

## Phase 1 — Codec primitive

- **T001** `ArtifactCodec`: add `intScalar key fallback get set` (+ `.fsi`). The codec has no integer
  field type and the receipt carries three counts. Reads a bare int, falls back on a malformed token
  (mirroring `boolScalar`); always writes. — FR-002

## Phase 2 — Model + rules (Artifacts, pure)

- **T002** `Evidence`: `type ObservedRun = { Source; Digest; Outcome; Passed; Failed; Skipped }`, and
  `EvidenceDeclaration.ObservedRun: ObservedRun option` (+ `.fsi`). — FR-001
- **T003** `EvidenceCodec`: `observedRunFields` + `ObservedRunDraft`, wired into `declarationFields`
  via `optionalNestedVia` so read and write are declared together. — FR-002
- **T004** `Evidence.isObserved`: read the receipt — `outcome = passed && failed = 0`. Delete the
  `false`-constant body and the "today this is false" doc; keep it total and I/O-free. — FR-006
- **T005** `Evidence.observedRunInconsistency: ObservedRun -> string option` — negative count,
  `passed` with `failed > 0`, unknown outcome, malformed digest. — FR-005
- **T006** `Evidence.citedArtifactPaths` += `observedRun.source`, so the #349 cascade probes it. — FR-009

## Phase 3 — Parse (Artifacts, pure)

- **T007** New `LifecycleArtifacts/TestReport.fs`/`.fsi`: `parse: source -> text -> Result<ObservedRun, string>`.
  TRX (`<TestRun>`/`Counters`) and JUnit (`<testsuites>`/`<testsuite>`). Counts derived; digest via
  `SchemaVersion.sha256Text`. Total — returns `Error`, never raises, on any malformed input. — FR-003, FR-004, FR-005

## Phase 4 — Record (Commands)

- **T008** Diagnostics: `evidence.testReportUnparseable`, `evidence.observedRunInconsistent`,
  `evidence.testReportPathEscape` (all blocking). — FR-008
- **T009** `HandlersEvidence.testReportReadEffects`: plan `ReadFile` for `--from-tests` in the first
  wave; refuse an uncontained path *before* planning (no effect at all). — FR-008
- **T010** `HandlersEvidence`: parse the interpreted snapshot, stamp `observedRun` on real-pass
  `verification` declarations, emit the diagnostics. Absent report blocks. — FR-007, FR-008
- **T011** `evidenceValidationDiagnostics`: enforce `observedRunInconsistency` on the merged artifact
  (an authored receipt is user input). — FR-005

## Phase 5 — Verification

- **T012** Artifacts tests: TRX/JUnit parse, unparseable, inconsistent, CRLF≡LF digest, codec
  round-trip, `isObserved` truth table, `obligationIsObserved` no-laundering. — AC-001..003
- **T013** Command tests: receipts recorded + idempotent + only real-pass `verification`;
  absent/escaping/unparseable each block and record nothing. — AC-004, AC-005
- **T014** Invariant + non-regression: `supported == selfAttested + observed` in `verify.json` /
  `ship.json`; `readiness` byte-unchanged vs the receipt-free fixture. **This is the FR-010 guard —
  it is what proves stage 2 gates nothing.** — AC-007
- **T015** `docs/release/schema-reference.md`: document `observedRun`. Update `.fsi` surface.
