# Tasks: Surface provider output on scaffold failure

**Input**: Design documents from `specs/054-surface-provider-output/`

**Prerequisites**: plan.md, spec.md, research.md (R1–R10), data-model.md (E1–E4), contracts/

**Change tier**: **Tier 1 (contracted change)** — additive report-contract change with public
`.fsi` surface, no persisted-schema migration. Tests are mandatory (Principle VI).

**Elmish/MVU applicability**: The one behavioral change is I/O at the existing `RunProcess`
edge (drain → bounded capture); the pure handler only reads the returned `ProcessRunResult`
and classifies. So this feature emits explicit tasks for the `.fsi` contract, the edge
interpreter, and emitted-result assertions — see Phase 2 and the per-story tests.

**Resolved planning decision (R6)**: captured streams are **single-line-encoded** in the
`--text` projection (embedded newline → literal `\n`, the escaping JSON already applies), so
`--rich` auto-derives from the text `key: value` lines with no per-command rich code and the
json ≡ text ≡ rich parity holds. This is settled in research.md; no `/speckit-clarify` needed.

## Status legend

- `[ ]` pending · `[X]` done with real evidence · `[-]` skipped with rationale

---

## Phase 1: Setup

**Purpose**: Establish the failing baseline before any change.

- [X] T001 Confirm on branch `054-surface-provider-output` and that `dotnet build FS.GG.SDD.sln`
  is green as the pre-change baseline (record the current `PublicSurface.baseline` for
  `FS.GG.SDD.Commands` so its refresh in T004 is a reviewable diff). Branch created off `main`;
  baseline build green (0 errors). Commands `PublicSurface.baseline` captures only module static
  functions, so an added record type/fields will not perturb it (verified in T004).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The `.fsi` contract, the edge capture, and the test fixtures that every user
story depends on. **⚠️ No user-story work can begin until this phase is complete.**

**Constitution I (Spec → FSI → Semantic Tests → Impl)**: the `.fsi` lands first (T002), then
fail-first edge tests (T005) before the `.fs` capture hardens (T006).

- [X] T002 [P] Add the E1 record `ProviderInvocationResult` (fields `CommandLine`,
  `ProcessStarted`, `ExitCode: int option`, `StandardOutput`, `StandardOutputTruncated`,
  `StandardError`, `StandardErrorTruncated`) and extend E2 `ScaffoldSummary` with the additive
  `ProviderInvocation: ProviderInvocationResult option` field, and E3 `ProcessRunResult` with
  `Command`/`StandardOutput`/`StandardOutputTruncated`/`StandardError`/`StandardErrorTruncated`,
  in `src/FS.GG.SDD.Commands/CommandTypes.fsi` (near `ScaffoldSummary`, `:336-357` / `:487-492`).
  Mirror the exact same additions in `src/FS.GG.SDD.Commands/CommandTypes.fs`. Keep existing
  partial-record match patterns (`Some { Started = false }`, `Some { Started = true; ExitCode = 0 }`)
  compiling. Done: `.fsi` + `.fs` mirrored; all `ProcessRunResult`/`ScaffoldSummary` constructors
  threaded (`None` default). NB: the new `ProcessRunResult.Command` field collides with
  `CommandReport.Command`, so `renderText` gained an explicit `report: CommandReport` annotation to
  keep F# record-field inference correct.
- [X] T003 [P] [US1] Author the failing-provider test fixtures under
  `tests/fixtures/scaffold-provider/`: (a) a **fixed-marker** fixture that prints a fixed stderr
  line and exits a fixed non-zero code (for the byte-stable golden, FR-009); (b) a
  `productName`-rejecting `dotnet new` template invoked with `--productName` so the engine prints
  `'--productName' is not a valid option` (SC-001); (c) an **oversize** fixture that prints
  > 65 536 characters to a stream (truncation, SC-005); (d) a **binary-bytes** fixture that emits
  non-UTF-8/binary bytes on a stream (defensive-decode robustness, spec Edge Cases: non-UTF-8 /
  binary bytes). Reuse existing `fails-midway` and `writes-into-fsgg`
  fixtures for Scenarios A and G — do not duplicate them. Done: added `rejects-param` template
  (declares no `productName`) + registry for SC-001. The oversize/binary/fixed-marker cases are
  driven deterministically at the `RunProcess` edge with controlled `/bin/sh` children (a
  `dotnet new` template cannot deterministically emit > 64 KiB or raw binary bytes), reusing
  `fails-midway` (Scenario A) and `writes-into-fsgg` (Scenario G).
- [X] T004 Refresh `tests/FS.GG.Contracts.Tests/PublicSurface.baseline` for the additive
  `FS.GG.SDD.Commands` surface from T002 (reviewable diff vs the T001 snapshot). Depends on T002.
  No-op: the `FS.GG.SDD.Commands` surface baseline (`tests/FS.GG.SDD.Commands.Tests/
  PublicSurface.baseline`, the one that actually covers this assembly) captures only module static
  **functions** — an added record type/fields and the `providerOutputCapChars` property getter
  (special-name) do not appear, so no baseline changed. `SurfaceBaselineTests` stays green.
- [X] T005 [US1] Write fail-first edge tests in `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs`
  asserting the `runProcess` interpreter now returns the executed `Command` line and bounded,
  truncation-flagged `StandardOutput`/`StandardError` (drain retained, content kept), **and** that
  the binary-bytes fixture (T003d) decodes defensively — the interpreter returns without throwing
  and the captured stream is representable in valid JSON (R9; spec Edge Cases: non-UTF-8 / binary).
  These MUST FAIL before T006. Serialize under `[<Collection("Scaffold")>]`. Depends on T002, T003.
  Evidence: fail-first was observed as the intermediate compile-stub state (edge returned empty
  `Command`/`StandardOutput`/`StandardError`); the hardened edge tests now pass against real
  `/bin/sh` children (command line, captured stdout/stderr, defensive binary decode round-trips
  through `System.Text.Json`).
- [X] T006 [US1] Replace the stdout/stderr discard at `src/FS.GG.SDD.Commands/CommandEffects.fs:97-98`
  with a **bounded concurrent** capture (deadlock-safe, both streams read concurrently; R1):
  define `providerOutputCapChars = 65536` near `runProcess`, record the fully-resolved command
  line into `ProcessRunResult.Command`, cap each stream at the bound and set its `…Truncated`
  flag (R2), put the launch-exception message into `StandardError` on the catch (`Started = false`,
  R4), and decode defensively so non-UTF-8/binary bytes cannot crash the report or corrupt JSON
  (R9). Make T005 pass. Depends on T005. Done: bounded concurrent `ReadToEndAsync` on both pipes
  before `WaitForExit`; `providerOutputCapChars = 65536` + `boundCapturedStream`; command line
  recorded; launch exception message → `StandardError` on the catch; non-throwing `UTF8Encoding`
  set as `StandardOutput/ErrorEncoding` for defensive decode. Exposed `providerOutputCapChars` in
  the `.fsi` as a contract constant.

**Checkpoint**: Types exist in `.fsi`, the edge captures bounded output, fixtures are ready —
user stories can proceed.

---

## Phase 3: User Story 1 — Diagnose a provider failure from the report alone (Priority: P1) 🎯 MVP

**Goal**: The scaffold report itself carries the provider's invoked command line, captured
stdout, captured stderr, and exit code on every provider-defect failure (FR-001/002/003).

**Independent Test**: Run scaffold against the fixed-marker fixture; assert the JSON report
contains that provider's command line, the known stderr text, and the exit code, and the
outcome is still `providerFailed` at exit 2.

### Tests for User Story 1 ⚠️ (write first, ensure they FAIL)

- [X] T007 [P] [US1] Scenario A + determinism golden in `ScaffoldCommandTests.fs`: fixed-marker
  fixture ⇒ exit **2**, outcome `providerFailed`, and a **byte-stable** JSON scaffold block whose
  `providerInvocation.commandLine` is the executed `dotnet new …` line, `.exitCode` is the fixed
  int, `.standardOutput`/`.standardError` and their `…Truncated` flags are present (FR-001/002/009,
  SC-002). Also assert the **empty-stderr-but-nonzero-exit** edge (spec Edge Cases): a fixture that
  writes nothing to stderr and exits non-zero yields `.standardError` **present and empty** (field
  emitted, not omitted) alongside the command line and exit code. Realized: the byte-stable
  "golden" is the deterministic-serialization assertion (`serializeReport` twice equal) plus
  structural facts (commandLine contains the resolved `dotnet new fsgg-fixture-fail -o .`, non-zero
  int exitCode, both stream fields + truncation flags present) — the `dotnet new` stream *wording*
  is SDK data (R7) so it is not frozen in a golden. The empty-stderr-nonzero-exit edge is a `/bin/sh
  -c 'exit 7'` edge case (present-and-empty capture).
- [X] T008 [P] [US1] Scenario B (SC-001 repro) in `ScaffoldCommandTests.fs`: `productName`-rejecting
  fixture invoked with `--param productName=Acme` ⇒ report `standardError` **contains**
  `'--productName' is not a valid option` (assert-contains, not golden — engine wording is SDK
  data, R7). Disclose the synthetic-vs-real engine source in the test name. Done via the new
  `rejects-param` template + `_Synthetic` test name.
- [X] T009 [P] [US1] Scenario C (US1-AC3) in `ScaffoldCommandTests.fs`: launch-failure fixture ⇒
  exit **2**, `scaffold.providerUnavailable`, `providerInvocation.processStarted = false`,
  `exitCode = null` (**not** `0`, FR-003), `commandLine` = attempted command, `standardError` =
  the launch error (R4). Split into two tests because a **fully end-to-end** `providerUnavailable`
  is host-infeasible: the create program is `dotnet`, which resolves via the apphost even with an
  empty PATH, so `dotnet new` always launches on a dotnet-equipped host (verified). (a) **real
  edge** launch failure — a nonexistent binary → `Started = false`, command line recorded,
  `StandardError` = the OS launch error (real `Process.Start` failure, R4). (b) **projection**
  over a report with a disclosed synthetic never-launched invocation record (`_Synthetic` test) ⇒
  `processStarted = false`, `exitCode: null`, commandLine + launch error preserved. Real-evidence
  path for the shape is (a); disclosed per Principle V at the code site and in the test name.
- [X] T010 [P] [US1] Scenario H provenance guard (FR-010) in `ScaffoldCommandTests.fs`: after a
  provider failure, `.fsgg/scaffold-provenance.json` still parses as schema **v1** and contains
  **no** captured-output keys (no stdout/stderr/commandLine).

### Implementation for User Story 1

- [X] T011 [US1] In `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs`
  `finalizeScaffold`, build a `ProviderInvocationResult` from the returned `ProcessRunResult`
  (`ExitCode = if Started then Some c else None`) and set `ScaffoldSummary.ProviderInvocation`
  to `Some …` on the three provider-defect terminals only (`:341-347` `providerFailed`,
  `:362-367` / `:368-373` `providerUnavailable` / `providerWroteSddTree`); leave `None` on
  success, empty-success, and dry-run (FR-005/006 gate). Depends on T006.
- [X] T012 [US1] In `src/FS.GG.SDD.Commands/CommandSerialization.fs`, emit the additive
  `providerInvocation` JSON object inside the `scaffold` block with a **fixed key order**
  (`commandLine`, `processStarted`, `exitCode` as int-or-null, `standardOutput`,
  `standardOutputTruncated`, `standardError`, `standardErrorTruncated`), or `"providerInvocation": null`
  when `None`. Makes T007–T010 pass (JSON contract). Depends on T011.
- [X] T013 [US1] In `src/FS.GG.SDD.Artifacts/Diagnostics.fs`, update the remediation **text** of
  `scaffold.providerFailed`, `scaffold.providerUnavailable`, and `scaffold.providerWroteSddTree`
  to point the consumer at the surfaced `providerInvocation` output (FR-008); leave ids,
  severities, and argument vectors unchanged (R8). Depends on T011.

**Checkpoint**: A provider-defect failure is fully diagnosable from the JSON report alone
(SC-001, SC-002) — MVP complete and independently testable.

---

## Phase 4: User Story 2 — Consistent visibility across automation and human surfaces (Priority: P2)

**Goal**: The provider-output facts appear identically in JSON, `--text`, and `--rich`; rich
degrades to zero-ANSI when non-interactive (FR-004, SC-003).

**Independent Test**: Run the same failing scaffold with `--json`, `--text`, `--rich`; assert
all three carry identical provider-output facts and `--rich` == `--text` (zero ANSI) when
redirected.

### Tests for User Story 2 ⚠️ (write first, ensure they FAIL)

- [X] T014 [P] [US2] Scenario D parity in `tests/FS.GG.SDD.Cli.Tests/ScaffoldParityTests.fs`:
  for a provider failure, json ≡ text ≡ rich carry the same four facts (command line, stdout,
  stderr, exit code); rich-redirected output == the text projection with **zero ANSI**; the JSON
  bytes are unchanged by the rich path (FR-004, SC-003).

### Implementation for User Story 2

- [X] T015 [US2] In `src/FS.GG.SDD.Commands/CommandRendering.fs`, add the single-line `--text`
  lines for the provider-defect case — `scaffoldProviderCommandLine:`, `scaffoldProviderExitCode:`
  (or `(not launched)` when `ExitCode = None`), `scaffoldProviderStdout:` + `…Truncated:`,
  `scaffoldProviderStderr:` + `…Truncated:` — **single-line-encoding** embedded newlines as
  literal `\n` (R6); omit all lines when `ProviderInvocation = None`. Depends on T011.
- [X] T016 [US2] Confirm `src/FS.GG.SDD.Cli/Rendering.fs` needs **no** change — rich auto-derives
  the new text `key: value` pairs (`:92-99`); verify solely via the T014 parity test (rich stays
  presentation-only and excluded from goldens). Depends on T015.

**Checkpoint**: US1 and US2 both work; all three projections agree (SC-003).

---

## Phase 5: User Story 3 — No noise on success or pre-invocation errors (Priority: P3)

**Goal**: Provider stdout/stderr are surfaced **only** when the provider was invoked and the
outcome is a provider defect; success and every exit-1 user-input block carry no content
(FR-006, SC-004), and no exit code or outcome string changes (FR-007, SC-006).

**Independent Test**: Run a successful scaffold and a `providerMissing` scaffold; assert
neither report carries provider stdout/stderr content and exit codes are unchanged (0 / 1).

### Tests for User Story 3 ⚠️ (write first, ensure they FAIL/hold)

- [X] T017 [P] [US3] Scenario E success + pre-invocation no-noise in `ScaffoldCommandTests.fs`:
  (a) `ok` fixture ⇒ JSON `scaffold.providerInvocation` is `null`, no stdout/stderr content,
  success JSON byte-stable except the additive null field, exit **0**; (b) no `--provider`
  (`providerMissing`) ⇒ exit **1**, input diagnostic only, no `providerInvocation` content (also
  spot-check `providerUnknown`/`providerVersionUnsupported`/`providerParamMissing`). (FR-006, SC-004)
- [X] T018 [P] [US3] Scenario F truncation in `ScaffoldCommandTests.fs`: oversize fixture ⇒ the
  captured stream is ≤ `providerOutputCapChars` (65 536) and its `…Truncated` flag is `true`
  (FR-005, SC-005).
- [X] T019 [P] [US3] Scenario G in `ScaffoldCommandTests.fs`: `writes-into-fsgg` fixture ⇒ exit
  **2**, `scaffold.providerWroteSddTree` remains the primary diagnostic, **and**
  `providerInvocation` is present for consistency (edge case).
- [X] T020 [P] [US3] Exit-code/outcome invariance (FR-007, SC-006) in `ScaffoldCommandTests.fs`:
  assert success ⇒ 0, provider defect ⇒ 2, user-input ⇒ 1 and the outcome strings are unchanged
  from today across the representative cases.

### Implementation for User Story 3

- [X] T021 [US3] Verify the FR-006 gate holds end-to-end: `ProviderInvocation` is `None` on
  success, empty-success, dry-run, and all pre-invocation user-input blocks (the `None`-elsewhere
  contract from T011), and the JSON emits `"providerInvocation": null` there (T012). Make
  T017–T020 pass; adjust the T011 gate only if a case leaks. Depends on T011, T012.

**Checkpoint**: All three stories independently functional; success/user-input contracts clean
(SC-004) and exit taxonomy unchanged (SC-006).

---

## Phase 6: Polish & Cross-Cutting

**Purpose**: Docs, migration note, agent-surface parity, and full quickstart validation.

- [X] T022 [P] Add a migration note under `docs/release/migrations/` describing the additive
  `providerInvocation` report block and the `providerOutputCapChars` bound, and stating there is
  **no persisted-schema change** (`.fsgg/scaffold-provenance.json` stays v1, FR-010). Realized per
  the repo's own migrations policy: an **additive-only** release **MUST NOT** carry a
  `<version>.md` note (`release-readiness.json` `migrations[]` stays empty). Following the
  established convention (030/050/052/053), the change is recorded as an additive paragraph in
  `docs/release/migrations/README.md` instead — describing the `providerInvocation` block, the
  `providerOutputCapChars` bound, and the no-persisted-schema / no-exit-code / no-outcome-string
  invariants.
- [X] T023 [P] Update the scaffold / getting-started reference docs to note the richer
  provider-failure report (command line + stdout + stderr + exit code), and update **both** agent
  surfaces equivalently (Claude `.claude/skills/fs-gg-sdd-getting-started` &
  `fs-gg-sdd-project`, and the matching Codex `.codex/skills/…`) so Claude ⇔ Codex stay aligned
  (Principle VII).
- [X] T024 Run the full quickstart.md validation (Scenarios A–H + determinism) and
  `dotnet test tests/FS.GG.SDD.Commands.Tests --filter FullyQualifiedName~Scaffold` and
  `dotnet test tests/FS.GG.SDD.Cli.Tests --filter FullyQualifiedName~ScaffoldParity`; confirm all
  green with real fixtures (no mocks). Record evidence per Principle VI. Depends on all prior.
  Evidence: full solution suite green — 786 passed, 0 failed, 3 skipped (network-gated Acceptance
  opt-in only). Scaffold filter: 105 passed; ScaffoldParity: 11 passed. Scenarios A/B/G run real
  `dotnet new`; C/F/binary run real child processes at the `RunProcess` edge; H asserts provenance
  stays v1 with no captured-output keys.
- [X] T025 At merge, close the coordination loop on **FS.GG.SDD#35** via `cross-repo-coordination`,
  noting the `exit 127` root cause was fixed upstream (FS.GG.Rendering Feature 217) and this PR
  delivers the SDD-owned diagnostics half — no versioned cross-repo contract surface added.
  Posted a `## Response` on FS-GG/FS.GG.SDD#35 (comment 4852585245) and advanced its Coordination
  board item (P2 SDD, `scaffold-provider`) to **In review**; the merging PR `Closes #35` (which
  flips the item to Done). Confirmed no registry change is required — additive report contract only.

---

## Dependencies & Execution Order

- **Phase 1 (Setup)** → **Phase 2 (Foundational)** blocks everything. Within Phase 2: T002 →
  T004; T002 + T003 → T005 → T006. T003 is [P] with T002.
- **Phase 3 (US1)** depends on Phase 2. Tests T007–T010 [P] first; then T011 → {T012, T013}.
- **Phase 4 (US2)** depends on T011. Test T014 first; then T015 → T016.
- **Phase 5 (US3)** depends on T011/T012. Tests T017–T020 [P] first; then T021.
- **Phase 6 (Polish)** depends on the stories it documents; T024 depends on all.

### Parallel opportunities

- Phase 2: T002 ∥ T003 (different trees); T007–T010 all [P] (assertions in one file but
  independent cases — split or keep serial if same-file churn is a concern).
- Cross-story: once Phase 2 completes, US1/US2/US3 test-authoring can proceed in parallel;
  implementation converges on `finalizeScaffold` (T011) so sequence T011 before T012/T015/T021.
- Phase 6: T022 ∥ T023.

---

## Implementation Strategy

**MVP = User Story 1**: Setup → Foundational → US1 (T001–T013). At that point a provider-defect
failure is fully diagnosable from the JSON report alone (SC-001/SC-002) — the entire point of
the board item. US2 (projection parity) and US3 (no-noise + invariance) are refinements layered
on the same `finalizeScaffold` attach point.

## Notes

- `[P]` = different files / independent, no dependency on another incomplete task in the phase.
- `[US#]` maps each task to its story for traceability.
- Write each story's tests first and confirm they FAIL before implementing (Principle VI).
- Never mark a failing task `[X]`; never weaken an assertion to green a build.
- Rich output (`--rich`) is presentation-only and excluded from deterministic/golden contracts.
