# Quickstart / Validation: Unify generated-view-state construction

This is a Tier 2 (internal) refactor. "Working" means: the build is green, the full test
suite passes, and **every** deterministic output byte and public-surface byte is identical
to `main`. The steps below prove that.

## Prerequisites

- .NET SDK 10.0.x; repo at branch `029-unify-view-state`.
- A clean `main` checkout (or `git worktree`) to diff golden output against.

## 1. Capture the pre-change baseline (from `main`)

Before implementing, record deterministic output for a work item exercising each view kind
(charter/workModel, analyze, verify, ship/governance-handoff, agents, refresh):

```sh
# from a main checkout, against a representative fixture work item
for cmd in charter specify analyze verify ship agents refresh; do
  fsgg-sdd "$cmd" --json  > /tmp/base.$cmd.json
  fsgg-sdd "$cmd" --text  > /tmp/base.$cmd.txt
done
```

(Or reuse the existing golden fixtures under `tests/FS.GG.SDD.Commands.Tests/` — they
already pin these outputs.)

## 2. Build and test after the change

```sh
dotnet build -c Release --no-incremental    # 0 errors, 0 warnings (FS3261/FS0025 ratchet at 0)
dotnet test  FS.GG.SDD.sln                  # all existing tests green (434 attributes)
```

Expected: Release build clean with **no new warning category**; all tests pass.

## 3. Verify byte-identical deterministic output

```sh
for cmd in charter specify analyze verify ship agents refresh; do
  diff <(fsgg-sdd "$cmd" --json) /tmp/base.$cmd.json || echo "DRIFT: $cmd json"
  diff <(fsgg-sdd "$cmd" --text) /tmp/base.$cmd.txt  || echo "DRIFT: $cmd text"
done
```

Expected: **no** `DRIFT` lines. `--rich` is presentation-only and excluded.

## 4. Verify public surface is untouched

```sh
git diff --stat main -- '**/*.fsi' '**/PublicSurface.baseline'
```

Expected: **empty** — no `.fsi` and no `PublicSurface.baseline` changes (all edits are
`internal`).

## 5. Verify the refactor actually landed (the SC counters)

```sh
cd src/FS.GG.SDD.Commands/CommandWorkflow

# SC-001: exactly one constructor definition remains
grep -rcE 'let (analysis|verify|ship)?[Gg]eneratedViewState[ (]' . | awk -F: '{s+=$2} END{print s" constructor defs (expect 1)"}'

# SC-002: zero inline Error-filter -> .Id occurrences (all via blockingDiagnosticIds)
grep -rnB1 'List.map _.Id' . | grep -c 'DiagnosticError'   # expect 0

# SC-003: zero inline blocked-workModel constructions (all via blockedWorkModelView)
grep -rcE 'GeneratorVersion \[\] None GeneratedViewCurrency\.Blocked' . | awk -F: '{s+=$2} END{print s" inline blocked (expect 0)"}'

# no leftover local-name shadow
grep -rn 'let generatedViewState =' .   # expect: none (renamed to generatedViewStateLabel)
```

Expected: 1 constructor def; 0 inline blocking-id projections; 0 inline blocked
constructions; 0 local `generatedViewState` shadows.

## 6. Net LOC check (SC-004)

```sh
git diff --shortstat main -- src/FS.GG.SDD.Commands/CommandWorkflow/
```

Expected: net deletion ≥ 60 lines (`SC-004`), with zero behavior change.

## Done When

- Build green, suite green, **no** output/`.fsi`/baseline drift (steps 2–4).
- Counters in step 5 all at their expected values.
- Refactor analysis report updated with an R8 row (status detail + aggregate).
