# Phase 1 Data Model: Surface provider output on scaffold failure

**Feature**: `054-surface-provider-output` | **Date**: 2026-07-01

Two entities change and one new record is added. No persisted schema changes
(`.fsgg/scaffold-provenance.json` stays v1, FR-010). All additions are transient
report/runtime data.

## E1 — `ProviderInvocationResult` (NEW report entity)

The spec Key Entity "Provider invocation result": the diagnostic facts of a
provider-defect invocation, surfaced in the scaffold report only.

`src/FS.GG.SDD.Commands/CommandTypes.fsi` (new record, near `ScaffoldSummary`):

```fsharp
/// The diagnostic runtime facts of a provider-defect scaffold invocation, surfaced
/// in the scaffold report (FR-001/002/003/005) and never persisted (FR-010). Present
/// only on the three provider-defect outcomes; absent (`None`) on success, dry-run,
/// and every pre-invocation user-input block (FR-006).
type ProviderInvocationResult =
    { /// Fully-resolved invoked command line, program + args as executed (FR-001).
      CommandLine: string
      /// Whether the provider process actually started (FR-003 discriminator).
      ProcessStarted: bool
      /// The provider exit code; `None` when the process never launched (FR-003) —
      /// distinct from a real `0`. Projected as int-or-null in json.
      ExitCode: int option
      /// Captured standard output, bounded to the per-stream cap (FR-002/005).
      StandardOutput: string
      StandardOutputTruncated: bool
      /// Captured standard error — carries the engine's own rejection text
      /// (FR-002/005). On a launch failure this holds the launch error (R4).
      StandardError: string
      StandardErrorTruncated: bool }
```

| Field | Source | Notes |
|-------|--------|-------|
| `CommandLine` | edge `ProcessRunResult.Command` | `dotnet new <templateId> -o . <params> [--force]` as executed |
| `ProcessStarted` | edge `ProcessRunResult.Started` | `false` ⇒ `providerUnavailable` |
| `ExitCode` | `Some ExitCode` iff `Started`, else `None` | FR-003 absence-vs-`0` |
| `StandardOutput` / `…Truncated` | edge capture (R1/R2) | ≤ 65 536 chars |
| `StandardError` / `…Truncated` | edge capture (R1/R2); launch error on unavailable | ≤ 65 536 chars |

**Determinism**: presence, ordering, and shape are fixed; only the textual content is
execution data (FR-009).

## E2 — `ScaffoldSummary` (EXTENDED)

`src/FS.GG.SDD.Commands/CommandTypes.fsi:336-357` — one additive field:

```fsharp
type ScaffoldSummary =
    { // …existing 13 fields unchanged…
      NextActionHint: string
      /// Provider-defect diagnostic facts (FR-006 gate): `Some` only on
      /// `providerFailed` / `providerUnavailable` / `providerWroteSddTree`;
      /// `None` on success, empty-success, dry-run, and user-input blocks.
      ProviderInvocation: ProviderInvocationResult option }
```

**Populated** in `HandlersScaffold.finalizeScaffold` on the three provider-defect
terminals only (`:341-347`, `:362-367`, `:368-373`); `None` everywhere else.

## E3 — `ProcessRunResult` (EXTENDED, internal edge type)

`src/FS.GG.SDD.Commands/CommandTypes.fsi:487-492` — carry the capture forward instead of
discarding it. Existing partial-field record patterns (`Some { Started = false }`,
`Some { Started = true; ExitCode = 0 }`) keep matching.

```fsharp
/// Captured outcome of a `RunProcess` effect at the edge. `Started = false` means the
/// process could not be launched (engine/command absent); its exit code is meaningless
/// and surfaced as `ExitCode = None` in the report (FR-003).
type ProcessRunResult =
    { Started: bool
      ExitCode: int
      /// The fully-resolved command line as executed (program + args) — FR-001.
      Command: string
      /// Captured stdout/stderr, each bounded to `providerOutputCapChars` with a
      /// truncation flag (FR-002/005). On a launch failure `StandardError` holds the
      /// launch exception message (R4).
      StandardOutput: string
      StandardOutputTruncated: bool
      StandardError: string
      StandardErrorTruncated: bool }
```

## E4 — Bound constant (NEW)

`providerOutputCapChars = 65536` (64 KiB), near `runProcess` in
`src/FS.GG.SDD.Commands/CommandEffects.fs`. Enforced only at the edge; documented in the
scaffold report contract and the migration note (FR-005 / SC-005).

## Unchanged (explicitly)

- `ScaffoldProvenanceRecord` — schema **v1**, no captured-output fields (FR-010); a guard
  test asserts the provenance JSON carries no stdout/stderr keys.
- `CommandOutcome`, `ScaffoldOutcome`, exit-code mapping (`CommandReports.providerDefectIds`,
  `exitCodeForReport`) — unchanged (FR-007): defect ⇒ 2, user-input ⇒ 1, success ⇒ 0.
- Diagnostic ids, severities, and argument vectors for `scaffold.providerFailed` /
  `providerUnavailable` / `providerWroteSddTree` — unchanged; only remediation *text* updated
  (FR-008, R8).

## Projection summary

| Fact | JSON (`scaffold.providerInvocation`) | Text (single-line, R6) | Rich |
|------|--------------------------------------|------------------------|------|
| command line | `commandLine` | `scaffoldProviderCommandLine:` | derived |
| started | `processStarted` | (implicit in exit code) | derived |
| exit code | `exitCode` (int\|null) | `scaffoldProviderExitCode:` / `(not launched)` | derived |
| stdout | `standardOutput` + `standardOutputTruncated` | `scaffoldProviderStdout:` + `…Truncated:` | derived |
| stderr | `standardError` + `standardErrorTruncated` | `scaffoldProviderStderr:` + `…Truncated:` | derived |
| (no defect) | `"providerInvocation": null` | lines omitted | omitted |
