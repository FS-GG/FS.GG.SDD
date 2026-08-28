#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
classifier="$repo_root/scripts/ci-risk"

profile() {
  "$classifier" --paths "$@" | sed -n 's/^profile=//p'
}

assert_profile() {
  local expected="$1"
  shift
  local actual
  actual="$(profile "$@")"
  if [ "$actual" != "$expected" ]; then
    echo "expected profile=$expected, got profile=$actual for: $*" >&2
    exit 1
  fi
}

assert_profile small docs/guide.md specs/122-sdd-modernization/spec.md work/937/spec.md
assert_profile normal src/FS.GG.SDD.Commands/CommandTypes.fs tests/FS.GG.SDD.Commands.Tests/Foo.fs
assert_profile high .github/workflows/gate.yml
assert_profile high src/FS.GG.SDD.Commands/CommandTypes.fsi
assert_profile high docs/release/migrations/next.md
assert_profile high .specify/memory/constitution.md
assert_profile high work/937-sdd-modernization/plan.md
assert_profile high work/937-sdd-modernization/contracts/public-schema.yml
assert_profile high unknown/new-surface.xyz
assert_profile high docs/guide.md src/FS.GG.SDD.Commands/CommandTypes.fs .github/workflows/gate.yml

workflow="$repo_root/.github/workflows/gate.yml"
assert_guard() {
  local step="$1"
  local expected="$2"
  local guard
  guard="$(awk -v step="$step" '
    index($0, "- name: " step) { getline; sub(/^[[:space:]]*/, ""); print; exit }
  ' "$workflow")"
  if [ "$guard" != "$expected" ]; then
    echo "expected '$step' guard '$expected', got '$guard'" >&2
    exit 1
  fi
}

# Required context names remain present; protected controls are reachable only on high.
grep -q '^  gate:$' "$workflow"
grep -q '^  build-config-drift:$' "$workflow"
grep -q '^  api-compatibility-gate:$' "$workflow"
assert_guard 'Exact package-only Quint compiler acceptance' "if: steps.risk.outputs.profile == 'high'"
assert_guard 'API-surface baselines (surface --check — both version axes)' "if: steps.risk.outputs.profile == 'high'"
assert_guard 'Assert the committed build config matches the pinned FS.GG.Kit' "if: steps.risk.outputs.profile == 'high'"
assert_guard 'ApiCompat vs feed baseline' "if: steps.risk.outputs.profile == 'high'"

empty="$($classifier --paths)"
grep -qx 'profile=high' <<<"$empty"
grep -qx 'protected_controls=true' <<<"$empty"

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
git -C "$tmp" init -q
git -C "$tmp" config user.email ci-risk@example.invalid
git -C "$tmp" config user.name ci-risk
mkdir -p "$tmp/docs" "$tmp/src/App"
printf 'old\n' >"$tmp/docs/old.md"
printf 'source\n' >"$tmp/src/App/Code.fs"
git -C "$tmp" add .
git -C "$tmp" commit -qm base
base="$(git -C "$tmp" rev-parse HEAD)"
git -C "$tmp" mv docs/old.md docs/new.md
git -C "$tmp" rm -q src/App/Code.fs
git -C "$tmp" commit -qm changed
head="$(git -C "$tmp" rev-parse HEAD)"

diff_result="$(cd "$tmp" && "$classifier" --base "$base" --head "$head")"
grep -qx 'profile=normal' <<<"$diff_result"
grep -q 'src/App/Code.fs' <<<"$diff_result"
grep -q 'docs/old.md' <<<"$diff_result"
grep -q 'docs/new.md' <<<"$diff_result"

echo 'ci-risk tests: PASS'
