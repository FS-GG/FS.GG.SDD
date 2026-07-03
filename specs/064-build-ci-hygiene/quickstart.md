# Quickstart / Validation: Build/CI hygiene (feature 064)

Runnable checks that prove each contract in
[`contracts/ci-hygiene-contract.md`](./contracts/ci-hygiene-contract.md). Run from
the repo root on a clean checkout. Prereqs: .NET SDK `10.0.x`, `git`, and (for the
format check) a pinned Fantomas installed to a tool path.

## C1 — Hermetic restore leaves lockfiles untouched

```bash
git restore '**/packages.lock.json' 2>/dev/null; git checkout -- .
dotnet restore FS.GG.SDD.sln
git diff --exit-code -- '**/packages.lock.json'   # MUST be clean (exit 0)
dotnet restore FS.GG.SDD.sln --locked-mode        # MUST exit 0
# All 11 lockfiles agree on one FSharp.Core:
grep -h -A2 '"FSharp.Core"' src/*/packages.lock.json tests/*/packages.lock.json | grep contentHash | sort -u | wc -l   # -> 1
```

Expected: no lockfile modified; locked restore succeeds; a single FSharp.Core hash.
This is the acceptance for SC-001/002 (run ideally on a machine with unrelated
inherited NuGet sources, where it fails today).

## C2 — CI caching

Inspect (no runtime needed):

```bash
grep -c 'cache: true' .github/workflows/gate.yml .github/workflows/release.yml .github/workflows/composition-acceptance.yml
grep -rn 'cache-dependency-path' .github/workflows/
```

Expected: every `actions/setup-dotnet@v4` step across the three workflows enables
`cache: true` with `cache-dependency-path: '**/packages.lock.json'`. Cache-hit on a
second run is observed in the Actions log (SC-003).

## C3 — Format gate

```bash
# Clean tree passes:
fantomas --check .          # exit 0 after the one-time reformat
# Negative check: mangle a file, confirm the gate fails and names the fix:
cp src/FS.GG.SDD.Cli/Program.fs /tmp/Program.fs.bak
printf '\n\n   let   x=1\n' >> src/FS.GG.SDD.Cli/Program.fs
fantomas --check .          # exit non-zero
cp /tmp/Program.fs.bak src/FS.GG.SDD.Cli/Program.fs
```

Expected: clean tree passes; a mis-formatted file fails with the `fantomas <paths>`
fix hint (SC-004). Then confirm the reformat was layout-only:

```bash
dotnet test FS.GG.SDD.sln -c Debug           # green
# no golden/*.json/.fsi baseline diff attributable to the reformat
```

## C4 — Warning ratchet

```bash
dotnet build FS.GG.SDD.sln -c Debug   # MUST be clean under the widened ratchet
```

Negative check: introduce a warning of a newly-promoted class in a scratch file and
confirm the build now errors, then revert. `Directory.Build.props` must be
unchanged:

```bash
git diff --exit-code -- Directory.Build.props Directory.Packages.props .config/dotnet-tools.json   # clean (SC-005)
```

## C5 — Locked-restore composite

```bash
test -f .github/actions/locked-restore/action.yml            # exists
grep -rc 'uses: ./.github/actions/locked-restore' .github/workflows/   # 5 usages
grep -rn 'dotnet restore .* --locked-mode' .github/workflows/ | grep -v actions/locked-restore   # no inline copies left
```

Expected: one composite action, five callers, zero inline `Restore (locked)` shell
blocks (SC-006).

## C6 — Release smoke + RollForward

```bash
bash scripts/verify-cli-tool.sh          # prints "C6 PASS", exit 0 (offline)
grep -n 'RollForward' src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj
grep -n 'verify-cli-tool.sh' .github/workflows/release.yml   # wired before the push step
```

Expected: the tool smoke passes offline; `RollForward` is declared; the smoke runs
before `dotnet nuget push` in `release.yml` (SC-006).

## C7 — Global invariant (no contract drift)

```bash
dotnet run --project src/FS.GG.SDD.Cli -- validate --json | grep -i overallPassed   # true
git status --porcelain -- '**/*.golden' '**/*.fsi'   # no unexpected contract/baseline churn
```

Expected: `fsgg-sdd validate` reports `overallPassed`; no CLI-output/JSON/golden
change attributable to this feature (SC-007 / FR-014).
