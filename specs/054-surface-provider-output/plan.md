# Implementation Plan: Surface provider output on scaffold failure

**Branch**: `054-surface-provider-output` | **Date**: 2026-07-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/054-surface-provider-output/spec.md`

## Summary

Close the SDD-owned observability gap behind coordination item **FS.GG.SDD#35**: on a
provider-defect scaffold failure, `fsgg-sdd scaffold` reports only the bare exit code and
**discards** the provider's stdout/stderr (`CommandEffects.fs:97-98`), so a downstream
consumer cannot see *why* the provider failed without inserting a `PATH` logging shim and
re-running. This feature carries the provider's **fully-resolved invoked command line, its
captured stdout, its captured stderr, and its exit code** forward into the scaffold
`CommandReport`, projected the same three ways as every other scaffold fact (json contract /
text summary / rich presentation).

The change is a **diagnostic enrichment**, not a new capability: no new command, effect,
outcome, or exit code. Capture happens at the one existing MVU `RunProcess` edge (the drain
is retained for deadlock-safety but the content is kept, bounded, instead of dropped); the
pure handler attaches the facts on the three provider-defect terminals only (FR-006); the
success path, dry-run, and every pre-invocation user-input block carry nothing new but an
additive empty field. Provenance stays schema **v1** (FR-010); outcome strings and exit codes
(2/1/0) are unchanged (FR-007).

See [research.md](./research.md) for the resolved decisions (R1–R10),
[data-model.md](./data-model.md) for the entities, and [contracts/](./contracts/) for the
report-block and edge-capture contracts.

> **Planning decision resolved (research R6):** the **text single-line encoding** of captured
> streams. The rich projection is auto-derived by parsing the plain-text `key: value` lines
> (`Cli/Rendering.fs:92-99`), so a multi-line captured stream in the text projection would break
> the k/v parse and the json ≡ text ≡ rich parity tests. **Resolution (settled in research.md R6 —
> no `/speckit-clarify` needed):** single-line-encode captured streams in the text projection
> (embedded newline → literal `\n`, the same escaping JSON already applies), keeping all three
> projections parity-safe with no per-command rich code. The alternative (a bespoke multi-line rich
> block) was rejected as heavier and divergent.

## Technical Context

**Language/Version**: F# on .NET, `net10.0`.

**Primary Dependencies**: `System.Diagnostics.Process` at the existing `RunProcess` edge
(now bounded-capturing stdout/stderr concurrently); `System.Text.Json` (deterministic
hand-ordered serialization, existing pattern) for the additive `providerInvocation` json
block; Spectre.Console (rich projection only, auto-derived from text). **No new third-party
dependency.**

**Storage**: Files only, all reads/writes unchanged. The captured provider output lives only
in the transient in-memory `CommandReport`; nothing new is persisted.
`.fsgg/scaffold-provenance.json` stays schema **v1** and gains no field (FR-010).

**Testing**: xUnit (`open Xunit`); real `dotnet new` provider fixtures under
`tests/fixtures/scaffold-provider/` (no mocks), serialized by `[<Collection("Scaffold")>]`.
New/extended: a controlled fixture that emits a fixed stderr marker + fixed non-zero exit for
a **byte-stable** golden; the SC-001 real-engine repro asserted *contains*
`'--productName' is not a valid option`; a truncation fixture (> 65 536 characters); parity tests in
`ScaffoldParityTests.fs` (json ≡ text ≡ rich, rich-redirected ≡ text); a provenance guard test
(no captured-output keys); `PublicSurface.baseline` refresh for `FS.GG.SDD.Commands`.

**Target Platform**: Cross-platform CLI (`fsgg-sdd`) + libraries.

**Project Type**: Multi-project single solution (CLI + libraries). Not web/mobile.

**Performance Goals**: N/A. Determinism (byte-stable json/text across projections; stable
exit-code taxonomy) is the hard constraint; the per-stream 65 536-character bound caps report size.

**Constraints**: capture only at the existing `RunProcess` edge (Principle V); bounded per
stream (`providerOutputCapChars = 65 536`) with a truncation flag; present **iff** provider
invoked AND outcome is a provider defect (FR-006); no reclassification, no exit-code change
(FR-007); no persisted-schema change (FR-010); json is the single source of truth and
text/rich add/drop no facts; rich degrades to zero-ANSI and is excluded from goldens; defensive
UTF-8 decode so binary bytes cannot crash the report.

**Scale/Scope**: One new `ProviderInvocationResult` record; one additive `ScaffoldSummary`
field; five new fields on the internal `ProcessRunResult`; a bounded concurrent capture in
`runProcess`; the additive json block + text lines (rich auto-derives); updated remediation
text on three diagnostics (ids/severity/args unchanged); fixtures, goldens, parity/guard
tests, `.fsi` + baseline refresh, one migration note.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.1.0.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Spec → FSI → Semantic Tests → Impl | **PASS** | `.fsi` first for `ProviderInvocationResult`, the extended `ProcessRunResult`, and `ScaffoldSummary.ProviderInvocation`; fail-first goldens/parity/guard tests before the `.fs` capture hardens. |
| II. Structured artifacts are the machine contract | **PASS** | The report json is the machine contract; the new facts are transient diagnostic runtime data (spec + plan say report-only). No new persisted schema — `scaffold-provenance.json` stays v1 (FR-010). |
| III. Visibility in `.fsi` | **PASS** | All new/changed public surface lands in `.fsi`; `FS.GG.SDD.Commands` `PublicSurface.baseline` refreshed. |
| IV. Idiomatic simplicity | **PASS** | Records + options + pure classification; a bounded concurrent read at the edge. No new effect, no advanced-feature machinery. Complexity Tracking empty. |
| V. Elmish/MVU boundary | **PASS** | Capture is I/O and stays at the existing `RunProcess` edge interpreter; the pure `update`/handler only reads the returned `ProcessRunResult` and classifies — exactly the constitutional shape. |
| VI. Test evidence mandatory | **PASS** | Fail-first: byte-stable golden over a controlled fixture, real-engine SC-001 repro, truncation fixture, three-projection parity, provenance guard. Synthetic fixture output disclosed in test names. |
| VII. Agent & human share one contract | **PASS** | The report is the single source of truth; agents author nothing new. Both agent surfaces' scaffold/getting-started guidance note the richer failure report equivalently (Claude ⇔ Codex). |
| VIII. Observability & safe failure | **PASS** | Directly strengthens observability: user-input errors (exit 1, no provider output) stay distinct from provider defects (exit 2, output surfaced); launch failure surfaces the attempted command + launch error with `exitCode = null`; binary bytes decode defensively. |

**Change tier**: **Tier 1 (contracted change)** — additive report-contract change (`ScaffoldSummary`
field + json `providerInvocation` block + text lines) with public `.fsi` surface. Requires spec,
plan, tasks, `.fsi`, tests, docs, and a migration note. **No persisted-schema migration.**

**Gate result**: PASS — no violations, no justified-complexity entries.

## Project Structure

### Documentation (this feature)

```text
specs/054-surface-provider-output/
├── plan.md              # This file
├── research.md          # Phase 0 — R1–R10 (resolves all planning choices; flags the R6 clarify item)
├── data-model.md        # Phase 1 — E1 new record, E2/E3 extended, E4 bound constant
├── quickstart.md        # Phase 1 — validation scenarios A–H
├── contracts/           # Phase 1
│   ├── scaffold-report-provider-output.md   # the additive providerInvocation block + presence gate + 3 projections
│   └── provider-invocation-capture.md       # the bounded concurrent capture protocol at the RunProcess edge
└── tasks.md             # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root) — files this feature touches

```text
src/
├── FS.GG.SDD.Artifacts/
│   └── Diagnostics.fs                     # remediation text of providerFailed/Unavailable/WroteSddTree points at
│                                          #   the surfaced providerInvocation (FR-008); ids/severity/args unchanged
└── FS.GG.SDD.Commands/
    ├── CommandTypes.fs / .fsi             # + ProviderInvocationResult; extend ProcessRunResult (Command/std*/*Truncated);
    │                                      #   + ScaffoldSummary.ProviderInvocation field
    ├── CommandEffects.fs                  # runProcess: bounded concurrent capture (replace :97-98 discard); providerOutputCapChars;
    │                                      #   launch-error into StandardError (R4); defensive UTF-8 decode
    ├── CommandWorkflow/HandlersScaffold.fs# finalizeScaffold: attach ProviderInvocation on the 3 defect terminals
    │                                      #   (:341-347/:362-367/:368-373); None elsewhere; ExitCode int option from Started
    ├── CommandSerialization.fs            # + providerInvocation json object (or null) in the scaffold block (fixed key order)
    └── CommandRendering.fs                # + single-line scaffoldProvider{CommandLine,ExitCode,Stdout,Stderr,*Truncated} text lines
src/FS.GG.SDD.Cli/
└── Rendering.fs                           # no change — rich auto-derives the new text k/v pairs (verify via parity test)

tests/
├── FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs   # provider-defect surfacing, SC-001 repro, truncation, provenance guard
├── FS.GG.SDD.Cli.Tests/ScaffoldParityTests.fs         # json ≡ text ≡ rich for the provider-output facts; rich-redirected ≡ text
├── FS.GG.Contracts.Tests/PublicSurface.baseline       # refreshed for FS.GG.SDD.Commands additive surface
└── fixtures/scaffold-provider/                         # + fixed-marker failing fixture; + oversize (truncation) fixture;
                                                        #   reuse fails-midway / writes-into-fsgg; a productName-rejecting template

docs/
└── release/migrations/                    # NEW note — additive providerInvocation report block (no persisted-schema change)
```

**Structure Decision**: The existing multi-project layout is retained. This feature is
entirely **additive edits** to existing scaffold files plus fixtures/tests — no new module.
The one behavioral change is at the `runProcess` edge (drain→bounded capture); everything else
threads one new record field through the pure handler and the two deterministic projections
(rich follows for free). This mirrors how produced-path content already flows as data through
the same pipeline.

## Phase 0 — Research

Complete. All planning choices resolved in [research.md](./research.md): R1 (capture at the
existing `RunProcess` edge, bounded, deadlock-safe concurrent read), R2 (65 536-character per-stream cap
+ truncation flag), R3 (exit-code absence as `int option` in the report entity), R4 (launch
error surfaced on `providerUnavailable`), R5 (attachment points + FR-006 gate), R6
(three-projection shape + the **rich-derives-from-text** constraint → text single-line encoding,
**routed to `/speckit-clarify`**), R7 (determinism: controlled-fixture golden + real-engine
contains-assert), R8 (diagnostic remediation points at the output, ids unchanged), R9
(provenance untouched + defensive decode), R10 (public surface + additive migration posture).

## Phase 1 — Design & Contracts

Complete. Artifacts generated: [data-model.md](./data-model.md), the two
[contracts/](./contracts/) specs, [quickstart.md](./quickstart.md). Agent context (`CLAUDE.md`
SPECKIT block) updated to point at this plan.

## Phase 2 — Task planning approach (for `/speckit-tasks`, not executed here)

Expected task ordering (spec → fsi → tests → impl, bottom-up by dependency):

1. **Types (`.fsi` first)**: add `ProviderInvocationResult`; extend `ProcessRunResult` with
   `Command`/`StandardOutput`/`StandardOutputTruncated`/`StandardError`/`StandardErrorTruncated`;
   add `ScaffoldSummary.ProviderInvocation`. Refresh `PublicSurface.baseline`.
2. **Edge capture** (`CommandEffects.fs`): replace the discard (`:97-98`) with a bounded
   concurrent capture; define `providerOutputCapChars = 65 536`; record the resolved command
   line; put the launch error into `StandardError` on the catch (R4); defensive UTF-8 decode.
   Fail-first edge/truncation tests.
3. **Handler attach** (`HandlersScaffold.finalizeScaffold`): build `ProviderInvocationResult`
   (`ExitCode = if Started then Some c else None`) and set `ProviderInvocation` on the three
   defect terminals; `None` on success/empty/dry-run.
4. **Projections**: json `providerInvocation` object-or-null in the scaffold block
   (`CommandSerialization.fs`); single-line text lines (`CommandRendering.fs`); confirm rich
   auto-derives (`ScaffoldParityTests.fs`).
5. **Diagnostics** (`Diagnostics.fs`): update remediation text of the three provider-defect
   diagnostics to point at `providerInvocation` (FR-008); ids/severity/args unchanged.
6. **Fixtures + tests**: fixed-marker failing fixture (golden), `productName`-rejecting template
   (SC-001 contains-assert), oversize fixture (truncation, SC-005), provenance guard (FR-010),
   success/user-input no-output (SC-004), parity (SC-003), exit-code unchanged (SC-006).
7. **Docs**: migration note (additive report block, no persisted-schema change); scaffold /
   getting-started reference + agent-surface guidance noting the richer failure report
   (Claude ⇔ Codex aligned).

**Cross-repo**: none required — the `exit 127` root cause is already fixed upstream
(FS.GG.Rendering Feature 217); this is the SDD-owned diagnostics half and adds **no** versioned
cross-repo contract surface. Close the loop on FS.GG.SDD#35 at merge via
`cross-repo-coordination`.

## Complexity Tracking

No entries — no constitution violations and no justified complexity (no new effect, command,
outcome, exit code, or advanced F# feature).
