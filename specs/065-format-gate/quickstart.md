# Quickstart / Validation: Format gate (feature 065)

Runnable checks that prove the feature end-to-end. Commands assume repo root and
the pinned Fantomas from research Decision 1 (`7.0.5`). See
[contracts/format-gate-contract.md](contracts/format-gate-contract.md) for the
contract each check maps to.

## Setup — install pinned Fantomas out-of-manifest

```bash
# Out-of-manifest install (does NOT touch .config/dotnet-tools.json) — FG-2
dotnet tool install fantomas --version 7.0.5 --tool-path ./.fantomas-tool --allow-roll-forward
FANTOMAS=./.fantomas-tool/fantomas
```

## FG-1 — `.editorconfig` is the Fantomas config

```bash
test -f .editorconfig && grep -q '\[\*\.fs\]' .editorconfig && echo "editorconfig present with [*.fs]"
# There must be no separate fantomas config:
! ls fantomas.json 2>/dev/null && echo "no fantomas.json (correct)"
```

Expected: `.editorconfig` exists with an `[*.fs]` section; no `fantomas.json`.

## FG-3 / SC-001 — clean tree passes, mangled tree fails and names the fix

```bash
# Clean tree passes after the one-time reformat:
"$FANTOMAS" --check .                       # exit 0

# Negative check: mangle a file, confirm non-zero + fix hint, then restore:
cp src/FS.GG.SDD.Cli/Program.fs /tmp/Program.fs.bak
printf '\n\n   let   x=1\n' >> src/FS.GG.SDD.Cli/Program.fs
"$FANTOMAS" --check . ; echo "exit=$?"       # non-zero; output names `fantomas <paths>`
cp /tmp/Program.fs.bak src/FS.GG.SDD.Cli/Program.fs
```

Expected: clean tree → exit 0; mangled tree → non-zero with the `fantomas
<paths>` reformat hint.

## FG-5 / SC-002 — the reformat is layout-only

```bash
# Green suite after reformat:
dotnet test FS.GG.SDD.sln -c Debug           # green

# No golden / .fsi baseline diff attributable to the reformat:
git diff --stat -- '*.json'                  # no golden/deterministic baseline changes
git diff --stat -- '*.fsi'                   # only layout, zero signature/declaration change

# fsgg-sdd validate stays overallPassed:
dotnet run --project src/FS.GG.SDD.Cli -- validate --json | grep -q '"overallPassed":true' && echo "validate overallPassed"
```

Expected: suite green; no golden `.json` changes; `.fsi` diffs (if any) are
whitespace-only with no declaration change; `validate` still `overallPassed`.

## FG-2 / SC-003 — managed manifest untouched

```bash
# Fantomas is NOT in the managed manifest:
! grep -q 'fantomas' .config/dotnet-tools.json && echo "manifest clean (fantomas absent)"
# build-config-drift gate stays green (managed org files byte-identical) — verified in CI.
```

Expected: `.config/dotnet-tools.json` contains no `fantomas` entry; the
`build-config-drift` job stays green.

## FG-6 / SC-004–005 — reproducible + documented

```bash
# The exact install/check/fix commands above are documented for contributors
grep -q 'fantomas' DEVELOPING.md && echo "documented for contributors"
# Fix workflow: reformat in place, re-check:
"$FANTOMAS" .            # reformats the tree
"$FANTOMAS" --check .    # now exit 0
```

Expected: the documented command reformats and makes the gate pass; a
contributor's local verdict matches CI because both pin `7.0.5`.
