#!/usr/bin/env bash
#
# test.sh — tiered local test runner for the inner loop (FS.GG.SDD#207).
#
# WHY THIS EXISTS
#   `dotnet test FS.GG.SDD.sln` is the only obvious move, and it takes minutes. The cost is
#   NOT diffuse: FS.GG.SDD.Commands.Tests alone is ~74% of the wall clock, because ~12 of its
#   files spawn real `dotnet`/CLI subprocesses (scaffold, init, the CLI-raw smokes). The pure
#   layer that most edits actually touch — Contracts + Artifacts (parsers, codec, work model,
#   serializers) — runs in ~1s, but nothing pointed anyone at it.
#
#   So: pick a tier matched to what you changed, and let the PR gate be the backstop.
#
# TIERS (cheapest project first, so a failure surfaces fast)
#   fast       Contracts + Artifacts       354 tests, ~3s     — parser / model / codec / serialization
#   component  + Validation + Cli          510 tests, ~30s    — CLI surface, report projections
#   full       + Commands + Acceptance   1,288 tests, ~2m20s  — everything; parity with the PR gate
#
# THIS REMOVES NO CI COVERAGE. `.github/workflows/gate.yml` still runs the FULL suite on every
# PR and is still required, so a Commands-layer regression is caught there even if you only ran
# `fast` locally. Tiering speeds the local loop; it does not weaken the gate.
#
# USAGE
#   scripts/test.sh                 # full (safe default — same coverage as the gate), ~2m20s
#   scripts/test.sh fast            # ~3s   pure layer
#   scripts/test.sh component       # ~30s  + CLI/validation
#   scripts/test.sh fast --no-build # skip the build when only the test assertions changed
#   scripts/test.sh fast -- --filter 'FullyQualifiedName~Codec'   # pass args through to dotnet test
#
# OPTIONS
#   --no-build      reuse existing binaries (implies --no-restore)
#   -c <config>     build configuration (default: Debug — the config the gate tests, and the one
#                   the Commands.Tests CLI smokes auto-detect and invoke)
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
FAST_PROJECTS=(
  tests/FS.GG.Contracts.Tests
  tests/FS.GG.SDD.Artifacts.Tests
)
COMPONENT_PROJECTS=(
  "${FAST_PROJECTS[@]}"
  tests/FS.GG.SDD.Validation.Tests
  tests/FS.GG.SDD.Cli.Tests
)
FULL_PROJECTS=(
  "${COMPONENT_PROJECTS[@]}"
  tests/FS.GG.SDD.Commands.Tests
  tests/FS.GG.SDD.Acceptance.Tests
)

tier="full"
config="Debug"
build=1
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
      echo "test.sh: unknown argument '$1' (expected: fast|component|full, --no-build, -c <config>, -- <dotnet test args>)" >&2
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

for project in "${projects[@]}"; do
  echo "── $project"
  start=$SECONDS
  if dotnet test "$project" "${test_args[@]}" "${passthrough[@]+"${passthrough[@]}"}"; then
    status="ok"
  else
    status="FAILED"
    failed=1
  fi
  timings+=("$(printf '%6ss  %-8s %s' "$((SECONDS - start))" "$status" "$project")")
  echo
done

echo "── summary (tier: $tier)"
printf '%s\n' "${timings[@]}"
printf '%6ss  total\n' "$((SECONDS - overall_start))"

exit "$failed"
