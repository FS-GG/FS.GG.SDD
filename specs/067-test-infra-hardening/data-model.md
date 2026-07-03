# Phase 1 Data Model: Test-infrastructure hardening

This feature is internal test/harness hardening; it introduces **no new
persisted schema, DTO, or wire type**. The "entities" below are the test-support
constructs the plan creates or consolidates. None is serialized; none appears in
any `.fsi` public surface except where noted as unchanged.

## Shared test-support constructs (new/consolidated)

### `TestShared` (new shared file `tests/Shared/TestShared.fs`)

Neutral namespace `FS.GG.SDD.TestShared`, linked into all six test projects.
Single home for previously-duplicated primitives.

| Member | Shape | Notes |
|---|---|---|
| `findRepoRoot` | `DirectoryInfo -> string` | Single definition (was ×4). Walks up to `FS.GG.SDD.sln`. |
| `repoRoot` | `string` | Computed once from `AppContext.BaseDirectory`. |
| `writeRelative` | `string -> string -> string -> unit` | Single definition (was ×2 in tests). |
| `tempDirectory` | `unit -> string` | Now nests under the per-run root (Decision 4); signature unchanged. |
| `runTempRoot` | `string` | Per-run root `GetTempPath()/fsgg-sdd-tests-<runId>/`; deleted at assembly teardown. |
| `SurfaceBaseline.verify` | `baselinePath:string -> capture:(unit -> string[]) -> unit` | Update-or-assert wrapper honoring `FSGG_UPDATE_BASELINE=1`. |
| `evidenceLadder` | `count:int -> (string list)` / evidence text builder | Derives `T001..T00n` once; removes the hardcoded `T001–T006` literal. |

### Per-project `TestSupport` (existing, now delegating)

Each project keeps its `TestSupport.repoRoot` / `writeRelative` / `tempDirectory`
public names for call-site stability, but the bodies delegate to `TestShared`.
Duplicated bodies in Contracts / Artifacts / Acceptance are removed.

## Test-isolation constructs

### `ProcessGlobalEnv` collection (Commands.Tests)

A single xUnit collection name applied to every class that mutates process-global
env or spawns a `PATH`-resolved process. Folds in the former `Scaffold` and
`Console` collections. No fixture state required — the collection's only job is to
serialize its members against each other.

### `runId` (per test run)

An identifier used only to name the per-run temp root. **Must not** use
`Guid.NewGuid`/time in a way that leaks into any deterministic contract — it names
temp paths only, never a compared artifact. (In the product `run`, derive it from
existing run context; in tests, a per-assembly `Guid` computed once at fixture
init is fine — temp paths are never asserted.)

## Harness constructs changed internally (product, contract-neutral)

| Construct | File | Change | Contract impact |
|---|---|---|---|
| `tempDirectory` / `copyDirectory` roots | `ValidationRunner.fs` | Nested under a run root, deleted in `finally` | None (report unchanged) |
| `withPerturbedHost` | `ValidationRunner.fs` | Adds cwd variation to culture/TZ | None (verdicts stay Pass; may catch a real determinism bug) |
| degradation cells | `ValidationRunner.fs` | Actually set `NO_COLOR`/`TERM` for those cells | None (no-ANSI check now genuinely exercised) |

## Invariants preserved (not entities, but the model's hard constraints)

- Every committed `**/*.baseline` file is byte-identical pre/post feature.
- The `validation-report` JSON schema and per-cell verdict structure are
  unchanged; only sensed metadata (already non-deterministic in the contract)
  can differ.
- No `.fsi` file in `src/**` changes.
