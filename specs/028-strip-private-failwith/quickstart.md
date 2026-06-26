# Quickstart: Verifying R7 (redundant `private` + `failwith` context)

This is a Tier 2 cleanup: the proof is that **everything observable stays byte-identical**
while the noise is gone. Run from repo root.

## Prerequisites

- .NET SDK for `net10.0`; `dotnet`, `git`, `grep` on PATH.
- Clean working tree on `028-strip-private-failwith`; know the merge base (`git merge-base HEAD main`).

## 1. Capture the baseline before changes (optional but recommended)

```bash
mkdir -p /tmp/r7-baseline
for cmd in charter analyze refresh; do
  dotnet run --project src/FS.GG.SDD.Cli -- "$cmd" --json > "/tmp/r7-baseline/$cmd.json" 2>/dev/null || true
  dotnet run --project src/FS.GG.SDD.Cli -- "$cmd" --text > "/tmp/r7-baseline/$cmd.txt"  2>/dev/null || true
done
```
(Use whatever representative invocation each command needs; the point is a stable pre-image.)

## 2. Build is green, no new warning category (FR-007 / SC-006)

```bash
dotnet build -c Release FS.GG.SDD.sln
```
**Expected**: build succeeds, 0 errors. No FS3261 / FS0025 emitted (ratchet still 0).
`Directory.Build.props` is unchanged; no `#nowarn` was added:
```bash
git diff --exit-code Directory.Build.props          # expect: no output (unchanged)
! git grep -n '#nowarn' -- 'src/**/*.fs'             # expect: no new nowarn introduced
```

## 3. Full suite passes unchanged (FR-006 / SC-003)

```bash
dotnet test FS.GG.SDD.sln
```
**Expected**: all 437 tests pass, none removed or skipped. Any newly-total path that
previously threw is covered by a no-throw assertion (US2 Scenario 3).

## 4. Public contract is byte-stable (FR-003 / SC-004)

```bash
BASE=$(git merge-base HEAD main)
git diff --stat "$BASE" -- '**/*.fsi' '**/PublicSurface.baseline'
```
**Expected**: empty output — no `.fsi` and no `PublicSurface.baseline` changed.

## 5. Deterministic output is byte-stable (FR-006 / SC-005)

```bash
for cmd in charter analyze refresh; do
  dotnet run --project src/FS.GG.SDD.Cli -- "$cmd" --json | diff - "/tmp/r7-baseline/$cmd.json"
  dotnet run --project src/FS.GG.SDD.Cli -- "$cmd" --text | diff - "/tmp/r7-baseline/$cmd.txt"
done
```
**Expected**: no diffs for any command in either format.

## 6. The cleanup actually happened (SC-001 / SC-002)

```bash
# Redundant private gone from .fsi-guarded modules (only justified retentions remain):
grep -rn -E '\b(let|type|module) +private\b' --include='*.fs' src
# Expected: only sites with a recorded retentionReason (e.g. HandlersShip.fs if gate-retained).

# No bare inner-error-string throws remain (old-form absence check):
grep -rn -E 'failwith message|defaultWith failwith|failwith "report not built"' --include='*.fs' src
# Expected: empty — every escape now names its id/path/value + inner error.

# Positive audit — every surviving throw must name a context id/path/value + inner error:
grep -rn -E 'failwith\b|failwithf\b' --include='*.fs' src
# Expected: only context-bearing forms (failwithf/invalidOp naming the constructed id/path/value);
# no bare `failwith "<inner error string>"` remains.
```

## 7. Roadmap reflects completion (FR-008 / SC-007)

```bash
grep -n -E 'R7|7 / 7 complete' docs/reports/2026-06-26-074428-refactor-analysis.md
```
**Expected**: the R7 row and status detail show ✅ with landed evidence, and the aggregate
reads `7 / 7 complete · 0 in progress · 0 not started`.

## Done when

Steps 2–7 all meet their expected outcome: green Release build with no new warnings,
full suite green, empty `.fsi`/baseline diff, empty output diff, no redundant `private` /
no bare-string throw remaining, and the roadmap at `7 / 7 complete`.
