# Quickstart / Validation Guide: Test-infrastructure hardening

How to prove the feature works end-to-end. Each scenario maps to a success
criterion. No implementation code here — see `tasks.md` for the work items.

## Prerequisites

- .NET 10 SDK; `git` and `dotnet` on `PATH`.
- Repo built: `dotnet build -c Debug`.

## Scenario 1 — Deterministic, parallel-safe suite (SC-001, US1)

```sh
# Offline inner loop, repeated — expect identical pass/fail each time
for i in 1 2 3 4 5; do dotnet test -c Debug 2>&1 | tail -1; done

# Scheduled-CI condition: acceptance registry set (use a reachable feed or the
# acceptance harness's documented value). Expect no env-ordering flakes.
FSGG_SDD_ACCEPTANCE_REGISTRY=<registry> dotnet test tests/FS.GG.SDD.Acceptance.Tests
```

**Expected**: same result all five offline runs; the `ProcessGlobalEnv` meta-test
passes (every process-spawning class carries the collection); `Acceptance.Tests`
runs serialized. A green→red flip across identical runs is a failure.

## Scenario 2 — No orphaned fixtures (SC-002, US2)

```sh
dotnet test --filter "FullyQualifiedName~FixtureManifestGuard"
```

**Expected**: the guard test passes — every remaining
`tests/fixtures/lifecycle-commands/*/manifest.yml` is referenced by an executing
test. Add a stray manifest → the guard fails.

## Scenario 3 — Unified baseline regeneration (SC-003, US3)

```sh
# Regenerate every baseline via the single switch; expect NO diff (surface unchanged)
FSGG_UPDATE_BASELINE=1 dotnet test --filter "FullyQualifiedName~SurfaceBaseline|FullyQualifiedName~PublicSurface"
git diff --stat -- '**/PublicSurface.baseline'    # expect empty

# Without the switch, all five baseline tests assert
dotnet test --filter "FullyQualifiedName~SurfaceBaseline|FullyQualifiedName~PublicSurface"
```

**Expected**: all five baseline tests honor the switch; regeneration on the
unchanged surface produces an empty diff.

## Scenario 4 — Self-cleaning temp artifacts (SC-004, US4)

```sh
BEFORE=$(find "${TMPDIR:-/tmp}" -maxdepth 1 -name 'fsgg-sdd-*' | wc -l)
dotnet test -c Debug >/dev/null 2>&1
dotnet run --project src/FS.GG.SDD.Cli -- validate --json >/dev/null 2>&1
AFTER=$(find "${TMPDIR:-/tmp}" -maxdepth 1 -name 'fsgg-sdd-*' | wc -l)
echo "leaked: $((AFTER - BEFORE))"
```

**Expected (refined — see spec SC-004)**: temp dirs do not *accumulate*. Under the
VSTest host, a process-exit handler is killed before a large delete finishes, so a
single run leaves **one** pid-tagged root; the next run's startup sweep reclaims any
root whose owning process has exited. Verify the self-healing bound across two runs:

```sh
dotnet test tests/FS.GG.SDD.Commands.Tests >/dev/null 2>&1
A=$(find "${TMPDIR:-/tmp}" -maxdepth 1 -name 'fsgg-sdd-tests-*' | wc -l)   # ~1
dotnet test tests/FS.GG.SDD.Commands.Tests >/dev/null 2>&1
B=$(find "${TMPDIR:-/tmp}" -maxdepth 1 -name 'fsgg-sdd-tests-*' | wc -l)   # still ~1, not 2
echo "run1=$A run2=$B  (bounded, not accumulating)"
```

The product `validate` harness leaks **0** (its `finally` runs with the process
alive). Cleanup is failure-safe (per-child, read-only-tolerant).

## Scenario 5 — Genuinely distinct validation cells (SC-005, US5)

```sh
dotnet test tests/FS.GG.SDD.Validation.Tests
```

**Expected**: the determinism/degradation matrices pass with the perturbed-host
cell varying cwd and the degradation cells applying a real `NO_COLOR`/`TERM`
condition. The library's intentional `Rich→text` degradation is preserved; the
Rich ANSI guarantee stays covered by `ValidateCommandTests`.

## Scenario 6 — De-duplicated helpers (SC-006, US6)

```sh
grep -rl "let rec findRepoRoot" tests/ | wc -l   # expect 1 (the shared file)
grep -rl "let writeRelative"    tests/ | wc -l   # expect 1 (the shared file)
```

**Expected**: each shared helper is defined once in `tests/Shared/TestShared.fs`;
the former copies are gone; the whole suite still builds and passes.

## Overall gate (SC-007 / FR-012)

```sh
git diff --stat -- '**/*.baseline' 'tests/fixtures' 'src/**/*.fsi'   # expect empty for baselines & .fsi
dotnet test    # entire suite green
```
