# Implementation Plan: Strip redundant `private` + give `failwith` escapes context

**Branch**: `028-strip-private-failwith` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/028-strip-private-failwith/spec.md`

## Summary

R7 — the refactor roadmap's last open row — is a Tier 2 (internal) cleanup with two
behavior-preserving sweeps:

1. **Redundant `private` removal (US1, ~80 sites/8 files).** In every `.fsi`-guarded
   module the signature file is already the sole arbiter of public surface, so a
   top-level `let/type/module private` on a binding the `.fsi` omits adds no visibility
   decision — it only misleads the reader (constitution Principle III). Remove it
   wherever the build/test gate proves it is not load-bearing; retain the rare site
   where removal changes intra-assembly resolution (collision/shadow/AutoOpen exposure).
2. **`failwith` context (US2, 9 sites/5 files).** Each partial-function escape converts
   a `Result.Error`/`None` into a bare-inner-string throw inside otherwise-total code.
   All nine are can't-happen-by-construction invariants today (inputs are internal
   `sprintf "EV%03d"`/`"T%03d"` strings, fixed artifact paths, or pre-validated work
   ids). Rewrite each to a context-bearing form whose message names the constructed
   id/path/value **and** the underlying error; only thread a `Result`→diagnostic where a
   site is genuinely reachable on malformed external input **and** the conversion does
   not change tool-visible output (none are expected to qualify this row).

**Technical approach**: mechanical, gated by the existing 437-test suite plus
byte-identical `.fsi`, `PublicSurface.baseline`, and deterministic `--json`/`--text`
output. No `.fsi` edits, no `#nowarn`, no new warning category, FS3261/FS0025 ratchet
stays at 0. Finish by flipping the R7 row + status detail to ✅ and the aggregate to
`7 / 7 complete` in the refactor analysis report.

## Technical Context

**Language/Version**: F# on .NET, target `net10.0` (constitution Engineering Constraints).

**Primary Dependencies**: standard library only for this row; Spectre.Console is touched
indirectly via `Cli/Rendering.fs` (`private` removal only, no behavior).

**Storage**: N/A — source-only edits.

**Testing**: `dotnet test FS.GG.SDD.sln` (xUnit-style F# tests across 4 test projects:
Artifacts, Validation, Cli, Commands), each carrying a `PublicSurface.baseline` snapshot.
Baseline count: 437 tests (per report).

**Target Platform**: Linux/cross-platform CLI (`fsgg-sdd`).

**Project Type**: Single solution, CLI + libraries (`src/FS.GG.SDD.*`).

**Performance Goals**: N/A — no runtime path changes on the happy path; output must stay
byte-identical.

**Constraints**: Tier 2 — every public `.fsi` and every `PublicSurface.baseline` stays
byte-identical; deterministic `--json`/`--text` stays byte-identical; Release build green;
no new warning category; `WarningsAsErrors=FS3261;FS0025` ratchet stays at 0; no `#nowarn`
introduced. (Verified in `Directory.Build.props:19`.)

**Scale/Scope**: 81 `(let|type|module) private` sites across 9 `.fs` files + 9 `failwith`
escapes across 5 `.fs` files. Verified against `main` on 2026-06-26 (inventory below).

### Grounded inventory (current tree, verified 2026-06-26)

`private` sites per file (81 total) — `.fsi` backing checked:

| File | Sites | `.fsi`? | Story-1 treatment |
|------|------:|:-------:|-------------------|
| `src/FS.GG.SDD.Validation/ValidationRunner.fs` | 33 | yes | remove redundant |
| `src/FS.GG.SDD.Artifacts/ReleaseContract.fs` | 20 | yes | remove redundant |
| `src/FS.GG.SDD.Cli/Rendering.fs` | 8 | yes | remove redundant |
| `src/FS.GG.SDD.Artifacts/WorkModel.fs` | 7 | yes | remove redundant |
| `src/FS.GG.SDD.Artifacts/GovernanceHandoff.fs` | 5 | yes | remove redundant |
| `src/FS.GG.SDD.Validation/ValidationHarness.fs` | 3 | yes | remove redundant |
| `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Verify.fs` | 3 | yes | remove redundant |
| `src/FS.GG.SDD.Artifacts/SchemaVersion.fs` | 1 | yes | remove redundant |
| `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersShip.fs` | 1 | **no** | edge case — keep unless build proves redundant |

`failwith` escapes (9 total): `ParsingTasks.fs:91,96,101` (`sprintf "EV%03d"` /
fixed tasks path / `sprintf "T%03d"`), `HandlersEvidence.fs:220` (fixed evidence path),
`HandlersEvidence.fs:259` (`Result.defaultWith failwith` over pre-validated workId),
`ReleaseContract.fs:266` (fixed `CommandSerialization.fs` artifact path),
`ReleaseContract.fs:451` (re-parse of own just-serialized inventory),
`SchemaVersion.fs:166` (`Result.defaultWith failwith` over self-built generator version),
`ValidationRunner.fs:642` (`Option.defaultWith (fun () -> failwith "report not built")`
after the model just built it). All FR-004(a); FR-004(b) is a contingency, not planned.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Spec → FSI → Tests → Impl | ✅ PASS | No new surface. `.fsi` files are frozen (Tier 2). Tests are the existing suite that must stay green; US2 adds a no-throw assertion only where a path becomes provably total. |
| II. Structured artifacts are the contract | ✅ PASS | No schema/artifact change. `PublicSurface.baseline` and deterministic JSON are the machine contracts and must stay byte-identical (SC-004/SC-005). |
| III. Visibility lives in `.fsi` | ✅ PASS (the point) | This row enforces III by deleting `.fs` visibility noise; the `.fsi` remains the sole policy. Retained `private` is the justified exception (FR-002). |
| IV. Idiomatic simplicity | ✅ PASS | Removes modifiers and replaces bare throws with `failwithf`/`invalidOp`-style context. No new abstractions, operators, or CE machinery. |
| V. Elmish/MVU boundary | ✅ PASS | No state/I-O boundary changes. `ValidationRunner` MVU `update`/`init` shapes are untouched; only the post-`update` `Option.defaultWith` escape gains context. |
| VI. Test evidence mandatory | ✅ PASS | Gate = full suite green + byte-identical baselines/output. New assertion only for any newly-total path (US2 Scenario 3); otherwise behavior is unchanged so existing tests are the evidence. |
| VII. One contract for agents + humans | ✅ PASS | No agent-command/skill/generated-view change; `fsgg-sdd agents`/`refresh` output unaffected. |
| VIII. Observability & safe failure | ✅ PASS (improves) | Context-bearing throws make a violated invariant diagnosable (names id/path/value + inner error) instead of emitting a bare inner string. |

**Result**: No violations. Complexity Tracking not required. Change Classification: **Tier
2** as declared in the spec (no `.fsi`/baseline edits).

## Project Structure

### Documentation (this feature)

```text
specs/028-strip-private-failwith/
├── plan.md              # This file (/speckit-plan)
├── research.md          # Phase 0 — redundancy proof, failwith treatment, gating
├── data-model.md        # Phase 1 — site taxonomy + per-site decision record
├── quickstart.md        # Phase 1 — runnable verification (build/test/baseline/output diff)
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

No `contracts/` directory: this is a Tier 2 internal cleanup whose explicit goal is
byte-identical external contracts. There is no new or changed interface to specify; the
frozen `.fsi` files and `PublicSurface.baseline` snapshots are the contract under test
(covered by the quickstart's baseline-diff step).

### Source Code (repository root)

```text
src/
├── FS.GG.SDD.Artifacts/
│   ├── ReleaseContract.fs            # 20 private; failwith :266, :451
│   ├── WorkModel.fs                  # 7 private
│   ├── GovernanceHandoff.fs          # 5 private
│   ├── SchemaVersion.fs              # 1 private; failwith :166
│   └── LifecycleArtifacts/Verify.fs  # 3 private
├── FS.GG.SDD.Validation/
│   ├── ValidationRunner.fs           # 33 private; failwith :642
│   └── ValidationHarness.fs          # 3 private
├── FS.GG.SDD.Cli/
│   └── Rendering.fs                  # 8 private
└── FS.GG.SDD.Commands/
    └── CommandWorkflow/
        ├── HandlersShip.fs           # 1 private (no .fsi — edge case)
        ├── ParsingTasks.fs           # failwith :91, :96, :101
        └── HandlersEvidence.fs       # failwith :220, :259

tests/
├── FS.GG.SDD.Artifacts.Tests/   (PublicSurface.baseline)
├── FS.GG.SDD.Validation.Tests/  (PublicSurface.baseline)
├── FS.GG.SDD.Cli.Tests/         (PublicSurface.baseline)
└── FS.GG.SDD.Commands.Tests/    (PublicSurface.baseline)

docs/reports/2026-06-26-074428-refactor-analysis.md   # R7 row + status detail + aggregate
Directory.Build.props                                  # FS3261;FS0025 ratchet (do not touch)
```

**Structure Decision**: Single existing solution (`FS.GG.SDD.sln`). No files added or
moved — edits are confined to the 12 `.fs` files above plus the one report. The four
`.fsi` files behind the `private`-bearing modules and all four `PublicSurface.baseline`
files are read-only invariants for this row.

## Complexity Tracking

No constitution violations — section intentionally empty.
