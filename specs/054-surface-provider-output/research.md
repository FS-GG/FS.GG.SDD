# Phase 0 Research: Surface provider output on scaffold failure

**Feature**: `054-surface-provider-output` | **Date**: 2026-07-01

All spec-open planning choices are resolved below (R1–R10). This feature is a
**diagnostic enrichment** of the existing `fsgg-sdd scaffold` provider-failure path;
it adds no new command, effect, outcome, or exit code. Every decision preserves the
existing outcome taxonomy, exit-code mapping (0/2/1), and the schema-v1 provenance
record (FR-007 / FR-010).

## Anchoring facts (existing code)

- The provider is invoked at the MVU `RunProcess` edge interpreter,
  `src/FS.GG.SDD.Commands/CommandEffects.fs:70-115` (`runProcess`). Today it sets
  `RedirectStandardOutput/Error = true`, then **drains and discards** both streams
  (`proc.StandardOutput.ReadToEnd() |> ignore`, `…StandardError.ReadToEnd() |> ignore`,
  lines 97-98) and returns `ProcessRunResult { Started; ExitCode }` — the streams
  are explicitly "excluded from the contract" (comment line 69).
- `ProcessRunResult` is defined at `src/FS.GG.SDD.Commands/CommandTypes.fsi:487-492`
  (`.fs:447`): `{ Started: bool; ExitCode: int }`. On launch failure the edge catch
  (`.fs:107-115`) returns `Started = false; ExitCode = -1` and **discards the
  exception**.
- The pure handler classifies the outcome from the interpreted-effect log in
  `finalizeScaffold`, `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs:293-377`:
  the three provider-defect terminals are the **unavailable** branch (341-347,
  `Started = false`), the **SDD-tree intrusion** branch (362-367), and the
  **non-zero exit** branch (368-373). Success/empty (374-377) and dry-run (315-336)
  are the non-defect paths.
- `ScaffoldSummary` (the scaffold `CommandReport` block) is
  `src/FS.GG.SDD.Commands/CommandTypes.fsi:336-357`; JSON at
  `CommandSerialization.fs:294-329`; text at `CommandRendering.fs:196-217`; rich derives
  from the same report.
- Diagnostics: `scaffoldProviderFailed` / `scaffoldProviderUnavailable` /
  `scaffoldProviderWroteSddTree` at `src/FS.GG.SDD.Artifacts/Diagnostics.fs:210-241`.
- Fixtures: `tests/fixtures/scaffold-provider/` with committed registries
  (`fails-midway.providers.yml`, `writes-into-fsgg.providers.yml`, `ok.providers.yml`)
  resolved through a real `dotnet new` provider (no mocks). `fails-midway` runs a
  `postAction` executing `false` (non-zero exit) after materializing `partial.txt`.
  Command tests: `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs`; three-projection
  parity: `tests/FS.GG.SDD.Cli.Tests/ScaffoldParityTests.fs`.

---

## R1 — Capture point: bounded read at the existing `RunProcess` edge

**Decision**: Capture both streams where they are already drained — the `runProcess`
edge interpreter — replacing the discard with a **bounded** capture that retains the
first *N* characters per stream and continues draining (discarding) the remainder so
the child's pipes never block. The retained text, a per-stream `truncated` flag, the
exit code, the `Started` flag, and the **resolved command line** flow back inside the
existing `ProcessRunResult` (extended, R3).

**Rationale**: The spec Assumption "Capture at the process edge" mandates exactly this;
the drain is *retained* (deadlock-safe) but the content is carried forward instead of
dropped. Keeping the capture at the single existing edge honors Principle V (I/O only at
the edge, the pure handler classifies) with zero new effect.

**Concurrency**: The existing code reads stdout fully then stderr fully **sequentially**,
which is a latent deadlock if the child fills the stderr pipe while the parent blocks on
stdout. Because we now read both under a size bound, read the two streams **concurrently**
(one `Task` per stream) and then `WaitForExit`. This removes the latent deadlock and is
required for a bounded reader.

**Alternatives rejected**: (a) a new dedicated `CaptureProcess` effect — unjustified
duplication of `RunProcess`; (b) capturing in the pure handler — impossible, the handler
never touches the process; (c) an external logging shim — this is precisely the workaround
the board item exists to eliminate.

## R2 — Size bound and truncation marker (FR-005 / SC-005)

**Decision**: Bound each stream to **65 536 characters** (64 KiB, `providerOutputCapChars`).
When a stream exceeds the cap, retain the first 65 536 characters verbatim and set that
stream's boolean `…Truncated` flag to `true`. The **flag** is the machine-contract
truncation indicator; no ellipsis or marker byte is injected into the captured content
(content stays faithful for diffing/greps). Text/rich may render a derived "(truncated)"
note from the flag; JSON carries the flag.

**Rationale**: 64 KiB is generous for real provider diagnostics (the SC-001 repro
`'--productName' is not a valid option` is a few dozen bytes) while bounding a runaway
provider's report. A char cap (not a byte cap) matches the `TextReader` capture unit and
is deterministic across platforms. Documenting a single per-stream constant satisfies "a
documented maximum size per stream."

**Alternatives rejected**: a line cap (less predictable memory bound); appending a marker
into the content (corrupts faithful capture and complicates goldens); no bound (violates
FR-005/SC-005).

## R3 — Exit-code absence represented distinctly (FR-003)

**Decision**: Two-layer representation.
- The **internal edge type** `ProcessRunResult` keeps `Started: bool` and `ExitCode: int`
  (a launch failure stays `Started = false; ExitCode = -1`) and gains the capture fields
  (`Command`, `StandardOutput`, `StandardOutputTruncated`, `StandardError`,
  `StandardErrorTruncated`). Existing record patterns like `Some { Started = false }` and
  `Some { Started = true; ExitCode = 0 }` continue to match unchanged (F# partial-field
  record patterns).
- The **report entity** `ProviderInvocationResult` carries `ExitCode: int option` —
  `Some code` when the process started, `None` when it never launched — computed in the
  handler from `Started`. JSON projects it as an integer-or-`null`; text as `exitCode:
  (not launched)` when `None`.

**Rationale**: The option lives in the *report* fact (the contract surface FR-003 governs),
so absence-vs-real-`0` is unambiguous without churning the many internal `ProcessRunResult`
pattern matches (repo-init probe, etc.). No misleading `0` ever reaches the report.

**Alternatives rejected**: making the internal `ExitCode` an `int option` (ripples through
the repo-init probe matches for no report benefit); reusing the `-1` sentinel in the report
(indistinguishable from a real `-1`, violates FR-003).

## R4 — Launch-error surfacing (US1-AC3, "Failed to launch")

**Decision**: On the edge catch (launch failure), set `Started = false`, `Command` = the
attempted command line, and put the caught exception's `.Message` into the captured
`StandardError` (with empty `StandardOutput`). The unavailable-branch handler then attaches
a `ProviderInvocationResult` with `ProcessStarted = false`, `ExitCode = None`, the attempted
command line, and that launch-error text.

**Rationale**: US1-AC3 requires the report to "surface the attempted command line and the
launch error"; the exception message (e.g. the engine binary not found) is the only launch
diagnostic available, and it belongs on the error stream conceptually.

## R5 — Where the invocation result is attached, and the FR-006 gate

**Decision**: `ScaffoldSummary` gains `ProviderInvocation: ProviderInvocationResult option`,
populated **only** on the three provider-defect terminals in `finalizeScaffold`:
unavailable (341-347), SDD-tree intrusion (362-367), and non-zero exit (368-373). It is
`None` on success, empty-success, dry-run, and every pre-invocation user-input block
(`providerMissing` / `providerUnknown` / `providerVersionUnsupported` / `providerParamMissing`),
which never reach the create path. The resolved command line comes from the interpreted
create-effect's captured `Command` (the exact `dotnet new … -o . <params> [--force]` line as
executed, FR-001).

**Rationale**: This is the FR-006 gate expressed structurally — output is present iff the
provider was invoked and the outcome is a provider defect. `None` on the success path keeps
the deterministic success contract clean (SC-004): JSON gains only an additive
`"providerInvocation": null`.

## R6 — Three-projection shape (FR-004 / SC-003), with the rich-derives-from-text constraint

**Load-bearing constraint discovered in code**: the **rich** projection is *not*
hand-authored per command — `src/FS.GG.SDD.Cli/Rendering.fs:92-99` parses the plain-**text**
projection into a `key: value` table and renders that. So every text line must stay a single
`key: value` pair, and `ScaffoldParityTests.fs` asserts json ≡ text ≡ rich facts and
rich-redirected ≡ text. A multi-line captured stream emitted raw into the text projection
would break the k/v parse and the parity tests.

**Decision**:
- **JSON** (contract, single source of truth): a nested `providerInvocation` object appended
  to the `scaffold` block (or `null`) with fixed key order:
  `commandLine`, `processStarted`, `exitCode` (int|null), `standardOutput`,
  `standardOutputTruncated`, `standardError`, `standardErrorTruncated`. The
  `System.Text.Json` writer escapes embedded newlines as `\n` — the faithful machine
  contract.
- **Text**: additive **single-line** labeled pairs after the existing scaffold lines, emitted
  only when `ProviderInvocation` is `Some` —
  `scaffoldProviderCommandLine:`, `scaffoldProviderExitCode:` (`(not launched)` when `None`),
  `scaffoldProviderStdout:`, `scaffoldProviderStdoutTruncated:`, `scaffoldProviderStderr:`,
  `scaffoldProviderStderrTruncated:`. The captured streams are **single-line-encoded** — each
  embedded newline rendered as a literal `\n` (and other control chars escaped) — so each fact
  stays one `key: value` pair and rich derives cleanly. This is a presentation *encoding* of
  the same fact JSON already escapes as `\n`, so no fact is added or dropped (projection rule
  preserved).
- **Rich**: derived automatically from the text k/v pairs (no scaffold-specific rich code),
  presentation-only, degrading to zero-ANSI when non-interactive or color-disabled — excluded
  from golden contracts (FR-009).

**Rationale**: Single-line encoding is the only shape that satisfies *all three* standing
constraints simultaneously — JSON is the faithful contract, text stays machine-scannable, and
rich auto-derives from text without new per-command rich code — while the parity tests keep
json ≡ text ≡ rich honest. All three carry the identical four facts (command line, stdout,
stderr, exit code) → SC-003.

> **Resolved (no `/speckit-clarify` needed):** the **text single-line encoding** of captured
> streams (literal `\n`) is adopted over the alternative that teaches the rich renderer a
> scaffold-specific multi-line block. Single-line encoding (above) is the minimal, parity-safe
> change and adds no per-command rich code; the alternative buys prettier human stderr at the cost
> of diverging the text/rich derivation contract and new rich code excluded from goldens. This
> decision is settled here and carried into tasks.md; no clarification round is required.

## R7 — Determinism and golden strategy (FR-009)

**Decision**: Structure is deterministic; content is data. Two test tiers:
- **Byte-stable golden** over a **controlled** fixture provider whose captured output is
  SDK-independent (a `postAction` that emits a fixed marker line and a fixed non-zero exit,
  plus a truncation fixture that emits > 65 536 characters). The golden asserts the full JSON/text
  scaffold block including the fixed content and the truncation flags.
- **Real-engine repro (SC-001)** asserts *contains* `'--productName' is not a valid option`
  from the actual `dotnet new` engine (a template that omits `productName` invoked with
  `--productName`), because the engine's exact wording is SDK-version data, not a byte
  contract — the same way real produced-path content is treated as data today.

**Rationale**: Matches the spec Determinism model exactly and avoids a golden that would go
red on an SDK bump while still proving the board-item repro is diagnosable from the report.

## R8 — Diagnostic remediation points at the surfaced output (FR-008)

**Decision**: Update the remediation strings of `scaffoldProviderFailed`,
`scaffoldProviderUnavailable`, and `scaffoldProviderWroteSddTree`
(`Diagnostics.fs:210-241`) to point the reader at the surfaced fields, e.g. "See the
provider's captured stderr and command line in the scaffold report (`providerInvocation`)."
The `scaffold.providerFailed` **message** (`Provider '{name}' exited {exitCode}.`) is
unchanged; the exit code is now *also* a structured fact (FR-003), not only interpolated.

**Rationale**: FR-008 requires the report be self-describing about where to read the cause.
The diagnostic ids, severities, and `[name; exitCode]` arg vectors are unchanged (stable
diagnostic contract).

## R9 — Provenance untouched (FR-010) and defensive decoding

**Decision**: `ScaffoldProvenanceRecord` stays schema v1; `provenanceWriteEffect`
(`HandlersScaffold.fs:267-281`) is not extended with any stdout/stderr — the invocation
result lives only in the transient report. Add a guard test asserting the provenance JSON
contains no captured-output keys. Captured content is decoded through the process
`StreamReader` (UTF-8 with replacement), so non-UTF-8 / binary bytes become replacement
characters and can never crash the report or corrupt the JSON (edge case).

**Rationale**: FR-010 — transient diagnostic runtime data is not a durable provenance fact.

## R10 — Public surface & migration posture

**Decision**: Tier 1 contracted change. `.fsi` updates: new `ProviderInvocationResult`
record, the extended `ProcessRunResult`, and the new `ScaffoldSummary.ProviderInvocation`
field; refresh the `FS.GG.SDD.Commands` `PublicSurface.baseline`. Additive report change →
a `docs/release/migrations/` note (new nested `providerInvocation` object / null in the
scaffold block; additive text lines). No `release-readiness.json` catalog change (no new
produced artifact; provenance schema unchanged).

**Rationale**: Consistent with the repo's Tier-1 discipline for a report-contract change with
no persisted-schema migration.

---

## Cross-repo

None required. The board item's `exit 127` root cause (the `--productName` parameter
surface) is already fixed upstream in FS.GG.Rendering (Feature 217); this feature is the
SDD-owned observability half and adds **no** versioned cross-repo contract surface. The
reference provider stays in FS.GG.Rendering; generic SDD gains no provider-specific
identity. A courtesy note on the coordination item (FS.GG.SDD#35) that the diagnostics gap
is closed is the only cross-repo touch, handled at merge via `cross-repo-coordination`.
