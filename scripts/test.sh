#!/usr/bin/env bash
#
# test.sh — tiered local test runner for the inner loop (FS.GG.SDD#207).
#
# WHY THIS EXISTS
#   `dotnet test FS.GG.SDD.sln` is the only obvious move, and it takes minutes. The cost is
#   NOT diffuse: it is concentrated in the ~150 tests that spawn a real `dotnet`/CLI/git
#   subprocess (scaffold's real `dotnet new`, the CLI-raw smokes, the apphost help/validate/lint
#   smokes, the git-backed gitignore checks). The other ~1,600 tests — the parsers, codec, work
#   model, serializers, command handlers, and report projections — run in-process in seconds.
#   Project granularity alone could not separate them: ~1,050 of the cheap ones live inside the
#   Commands + Cli test projects, alongside their subprocess-spawning siblings. So those siblings
#   carry the `tier=slow` trait (FS.GG.SDD#209) and the cheap tiers filter them out with
#   `--filter tier!=slow`, admitting the in-process majority without the subprocess tail.
#
#   So: pick a tier matched to what you changed, and let the PR gate be the backstop.
#
# TIERS (cheapest first, so a failure surfaces fast). The `tier=slow` trait (FS.GG.SDD#209) marks
# the ~150 tests that spawn a real dotnet/CLI/git subprocess; `tier!=slow` lets the cheap tiers
# reach the ~1,050 in-process Commands+Cli tests without paying for their subprocess-spawning siblings.
#   fast       pure + in-process Commands/Cli   1,576 tests, ~20s   — no subprocess: parser / model / codec / handlers / reports
#   component  + Validation + full Cli          1,637 tests, ~35s   — adds validation + the CLI process smokes
#   full       every project, unfiltered      ~1,787 tests, ~2-3m   — everything, no filter; parity with the PR gate
#
# THIS REMOVES NO CI COVERAGE. `.github/workflows/gate.yml` still runs the FULL suite on every
# PR and is still required, so a Commands-layer regression is caught there even if you only ran
# `fast` locally. Tiering speeds the local loop; it does not weaken the gate.
#
# USAGE
#   scripts/test.sh                 # full (safe default — same coverage as the gate), ~2-3m
#   scripts/test.sh fast            # ~14s  pure + in-process Commands/Cli (no subprocess)
#   scripts/test.sh component       # ~25s  + Validation + the CLI process smokes
#   scripts/test.sh fast --no-build # skip the build when only the test assertions changed
#   scripts/test.sh fast -- --filter 'FullyQualifiedName~Codec'   # narrow further (a user --filter wins)
#   scripts/test.sh --results-dir artifacts/test-results   # emit one TRX per project (see below)
#
# OPTIONS
#   --no-build      reuse existing binaries (implies --no-restore)
#   -c <config>     build configuration (default: Debug — the config the gate tests, and the one
#                   the Commands.Tests CLI smokes auto-detect and invoke)
#   --results-dir <dir>
#                   emit a TRX report per project into <dir>, named for the project
#                   (FS.GG.SDD.Commands.Tests.trx, …). This is the runner-produced report an SDD
#                   work item records an ADR-0035 observation receipt FROM, via
#                   `fsgg-sdd evidence --from-test-report <dir>/<project>.trx` (see DEVELOPING.md).
#                   The filename is per-project ON PURPOSE: a single shared `LogFileName` would be
#                   overwritten by each looped `dotnet test`, leaving only the last project's run —
#                   a green-looking report missing five sixths of the suite. The reports are written
#                   IN ADDITION to any `--logger` you pass after `--`; they change no exit code.
#   --              everything after this is forwarded verbatim to each `dotnet test`
#
# WHY PER-PROJECT AND NOT `dotnet test FS.GG.SDD.sln`
#   A solution-wide run spawns every test host at once; on a memory-constrained machine (or one
#   with sibling worktrees building) that dies with `Failed to create CoreCLR, HRESULT: 0x80070008`
#   or a 90s protocol-negotiation timeout — resource exhaustion misread as a red suite. Looping the
#   projects covers exactly the same tests (these six ARE the solution's test projects) and buys a
#   per-project timing table, which is the whole point: make the cost visible.

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# The six test projects of FS.GG.SDD.sln, cheapest first. Acceptance is network-gated and
# self-skips when FSGG_SDD_ACCEPTANCE_REGISTRY is unset, so `full` stays offline by default.
#
# A `project::filter` entry runs that project with `dotnet test --filter <filter>`. The trait
# `tier=slow` marks the ~150 tests that spawn a real `dotnet`/CLI/git subprocess (FS.GG.SDD#209),
# so `tier!=slow` admits the ~1,050 in-process Commands+Cli tests into the cheap tiers WITHOUT their
# subprocess-spawning siblings. `full` runs every project unfiltered, so it stays the gate's peer:
# the union of the tiers is the whole suite — no test is reachable only under a filter.
PURE_PROJECTS=(
  tests/FS.GG.Contracts.Tests
  tests/FS.GG.SDD.Artifacts.Tests
)
FAST_PROJECTS=(
  "${PURE_PROJECTS[@]}"
  "tests/FS.GG.SDD.Commands.Tests::tier!=slow"
  "tests/FS.GG.SDD.Cli.Tests::tier!=slow"
)
COMPONENT_PROJECTS=(
  "${PURE_PROJECTS[@]}"
  tests/FS.GG.SDD.Validation.Tests
  tests/FS.GG.SDD.Cli.Tests
  "tests/FS.GG.SDD.Commands.Tests::tier!=slow"
)
FULL_PROJECTS=(
  "${PURE_PROJECTS[@]}"
  tests/FS.GG.SDD.Validation.Tests
  tests/FS.GG.SDD.Cli.Tests
  tests/FS.GG.SDD.Commands.Tests
  tests/FS.GG.SDD.Acceptance.Tests
)

tier="full"
config="Debug"
build=1
results_dir=""
passthrough=()

while [ $# -gt 0 ]; do
  case "$1" in
    fast | component | full)
      tier="$1"
      shift
      ;;
    --no-build)
      build=0
      shift
      ;;
    -c | --configuration)
      config="${2:?-c requires a configuration}"
      shift 2
      ;;
    --results-dir)
      results_dir="${2:?--results-dir requires a directory}"
      shift 2
      ;;
    --)
      shift
      passthrough=("$@")
      break
      ;;
    -h | --help)
      # The header comment IS the help text: everything from the title line down to the
      # implementation-notes section. Matched on markers, not line numbers, so editing the
      # header can't silently truncate `--help`.
      awk 'NR >= 3 { if (/^# WHY PER-PROJECT/) exit; print }' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *)
      echo "test.sh: unknown argument '$1' (expected: fast|component|full, --no-build, -c <config>, --results-dir <dir>, -- <dotnet test args>)" >&2
      exit 2
      ;;
  esac
done

case "$tier" in
  fast) projects=("${FAST_PROJECTS[@]}") ;;
  component) projects=("${COMPONENT_PROJECTS[@]}") ;;
  full) projects=("${FULL_PROJECTS[@]}") ;;
esac

cd "$repo_root"

# Resolve --results-dir to an absolute path and create it. dotnet's `--results-directory` is
# interpreted relative to each `dotnet test`'s cwd (repo_root here); an absolute path removes all
# doubt and lets a caller pass a workspace-relative dir without it landing per-project.
if [ -n "$results_dir" ]; then
  mkdir -p "$results_dir"
  results_dir="$(cd "$results_dir" && pwd)"
fi

test_args=(-c "$config")
if [ "$build" -eq 0 ]; then
  test_args+=(--no-build --no-restore)
fi

echo "tier: $tier  (${#projects[@]} projects, $config)"
if [ "$tier" != "full" ]; then
  echo "note: the PR gate still runs the FULL suite — this tier is the local inner loop only."
fi
echo

# Integer seconds via bash's SECONDS. Deliberately no `bc`/`date +%s.%N` arithmetic: `bc` is not
# installed everywhere (it is absent from several of our dev images) and a missing binary must not
# turn a green suite red.
declare -a timings=()
overall_start=$SECONDS
failed=0

for entry in "${projects[@]}"; do
  # A tier entry is `path` or `path::<trait-filter>`. The filter, when present, is passed as
  # `--filter` so a tier can admit a project's cheap in-process subset (`tier!=slow`) without its
  # subprocess-spawning tests. A user `--filter` after `--` is emitted last and wins (vstest keeps
  # the last --filter), which is what an explicit `test.sh fast -- --filter Foo` narrowing wants.
  project="${entry%%::*}"
  filter_args=()
  if [ "$entry" != "$project" ]; then
    filter_args=(--filter "${entry#*::}")
    label="$project  [${entry#*::}]"
  else
    label="$project"
  fi

  # One TRX per project when --results-dir is set, named for the project so no run overwrites
  # another (a shared LogFileName loses five sixths of the suite — the whole reason this is not just
  # `-- --logger trx`). Emitted BEFORE the passthrough so a user `-- --logger …` is additive, not a
  # replacement. `${project##*/}` is the project directory's basename, e.g. FS.GG.SDD.Commands.Tests.
  results_args=()
  if [ -n "$results_dir" ]; then
    results_args=(--logger "trx;LogFileName=${project##*/}.trx" --results-directory "$results_dir")
  fi

  echo "── $label"
  start=$SECONDS
  if dotnet test "$project" "${test_args[@]}" "${filter_args[@]+"${filter_args[@]}"}" "${results_args[@]+"${results_args[@]}"}" "${passthrough[@]+"${passthrough[@]}"}"; then
    status="ok"
  else
    status="FAILED"
    failed=1
  fi
  timings+=("$(printf '%6ss  %-8s %s' "$((SECONDS - start))" "$status" "$label")")
  echo
done

echo "── summary (tier: $tier)"
printf '%s\n' "${timings[@]}"
printf '%6ss  total\n' "$((SECONDS - overall_start))"

if [ -n "$results_dir" ]; then
  echo
  echo "TRX reports written to: $results_dir"
fi

exit "$failed"
