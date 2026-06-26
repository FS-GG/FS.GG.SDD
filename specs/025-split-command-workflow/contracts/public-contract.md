# Contract: Public surface & byte-stable behavior

The "interface" this refactor exposes is the **unchanged** public contract of
`FS.GG.SDD.Commands`. R2's binding gate is that this contract — and every
command's deterministic output — is held exactly fixed. This document states the
invariants and the mechanical check that proves each.

## C-1 — Public signature is byte-identical

The only public surface of the workflow is, and remains:

```fsharp
namespace FS.GG.SDD.Commands

open FS.GG.SDD.Commands.CommandTypes

module CommandWorkflow =
    val init: request: CommandRequest -> CommandModel * CommandEffect list
    val update: msg: CommandMsg -> model: CommandModel -> CommandModel * CommandEffect list
```

**Check**: `git diff --exit-code "$BASE" -- src/FS.GG.SDD.Commands/CommandWorkflow.fsi`
produces no output and exits 0, where `BASE=$(git merge-base main HEAD)` is the
immutable pre-refactor baseline (not the moving `main` ref). (FR-002, SC-002.)

## C-2 — Deterministic JSON is byte-for-byte identical

For every command and every representative input, the default/`--json`
projection, exit code, and stdout/stderr routing are unchanged. No golden,
surface-baseline, or `release-readiness` fixture may be regenerated.

**Check**: `dotnet test` passes the full deterministic/golden suite (438 tests),
and `git status --porcelain` shows no modified fixture/golden/baseline files
after the run. (FR-003, SC-003, SC-004.)

## C-3 — Build is clean with no new warning categories

`dotnet build -c Release` succeeds with no new errors and no new warning
categories. The existing FS3261 (nullness) unique-site count in `src` (~290 per
the R2 baseline in `docs/reports/2026-06-26-074428-refactor-analysis.md`) must
not increase as a result of the reorganization.

**Check**: `dotnet build -c Release --no-incremental` is clean; the deduplicated
FS3261 site count is ≤ the recorded baseline. (FR-007, SC-006.)

## C-4 — Layering and compile order preserved

`FS.GG.SDD.Commands` still references only `FS.GG.SDD.Artifacts` (plus
FSharp.Core). No new project reference, no cycle, valid `.fsproj` order
(`Artifacts → Commands → Cli`/`Validation` intact).

**Check**: the `.fsproj` `ProjectReference` set is unchanged; the solution builds
in dependency order. (FR-008.)

## C-5 — Behavior preserved, including intentional divergence

No observable change to diagnostics, effects, artifact contents, or control flow
for any command. `computeRefreshPlan` keeps its own guard and does **not** route
through `runHandler`; the R1 design exceptions are preserved verbatim, not
"cleaned up". (FR-006.)

**Check**: covered transitively by C-2 (the suite exercises every command,
including refresh) plus a code-review confirmation that `computeRefreshPlan`'s
guard shape is unchanged.

## Out of scope

No new public values, no new diagnostics, no new effects, no output-format
changes. Any such addition would violate the Tier-2 classification and this
contract.
