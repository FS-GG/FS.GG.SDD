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
assert_profile high src/FS.GG.SDD.Commands/CommandWorkflow/HandlersShip.fs
assert_profile high src/FS.GG.SDD.Commands/CommandWorkflow/HandlersEvidence.fs
assert_profile high .github/workflows/gate.yml
assert_profile high src/FS.GG.SDD.Commands/CommandTypes.fsi
assert_profile high docs/release/migrations/next.md
assert_profile high .specify/memory/constitution.md
assert_profile high AGENTS.md
assert_profile high CLAUDE.md
assert_profile high .agents/policy.md
assert_profile high .agents/skills/example/SKILL.md
assert_profile high .claude/commands/review.md
assert_profile high .claude/skills/example/SKILL.md
assert_profile high .codex/instructions.md
assert_profile high .codex/skills/example/SKILL.md
assert_profile high work/937-sdd-modernization/plan.md
assert_profile high work/937-sdd-modernization/contracts/public-schema.yml
assert_profile high unknown/new-surface.xyz
assert_profile high docs/guide.md src/FS.GG.SDD.Commands/CommandTypes.fs .github/workflows/gate.yml

assert_protected_controls_from() {
  local classifier_path="$1"
  local authority_path="$2"
  local result
  result="$("$classifier_path" --paths "$authority_path")"
  grep -qx 'profile=high' <<<"$result"
  grep -qx 'test_tier=full' <<<"$result"
  grep -qx 'protected_controls=true' <<<"$result"
}

# Root agent instructions are executable authority, not ordinary Markdown. Pin all three outputs,
# then remove each literal independently and prove the same assertion fails on the escaped path.
assert_protected_controls_from "$classifier" AGENTS.md
assert_protected_controls_from "$classifier" CLAUDE.md

mutation_tmp="$(mktemp -d)"
trap 'rm -rf "$mutation_tmp"' EXIT
for authority_path in AGENTS.md CLAUDE.md; do
  mutant="$mutation_tmp/ci-risk-${authority_path}"
  authority_literal="${authority_path//./\\.}"
  sed "s/${authority_literal}|//" "$classifier" >"$mutant"
  chmod +x "$mutant"

  if assert_protected_controls_from "$mutant" "$authority_path" >/dev/null 2>&1; then
    echo "removing $authority_path from the authority rule did not expose the protected-control escape" >&2
    exit 1
  fi
done
rm -rf "$mutation_tmp"
trap - EXIT

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

# Pin every protected step, rather than sampling four representatives. A newly
# added High control must be named here, and weakening any existing guard reds.
mapfile -t protected_steps < <(awk '
  /^      - name: / { name=$0; sub(/^      - name: /, "", name); next }
  /if: steps\.risk\.outputs\.profile == '\''high'\''/ { print name }
' "$workflow")
expected_protected_steps=(
  "Unit-test the skill-root materializer's write set"
  'Resolve the view root this tree no longer commits'
  'Pin the receiver-project generate invocation'
  'Unit-test the root-asymmetry check'
  'Unit-test the working-tree cleanliness check'
  'Set up exact Go toolchain for the Q1 lmt object'
  'Provision exact Q2 compiler-acceptance tools'
  'Exact package-only Quint compiler acceptance'
  'Qualify the Linux user and network namespace sandbox'
  'Exact package-only Quint Typed SDD v2 acceptance'
  'Upload exact Q2/Q3 Quint acceptance reports (JUnit)'
  'API-surface baselines (surface --check — both version axes)'
  'Dependency-surface captures (dependency-surface --check)'
  'Set up .NET'
  'Assert the committed build config matches the pinned FS.GG.Kit'
  'Set up .NET'
  'Unit-test the baseline resolver'
  'Unit-test the gate verdict + CP#### rendering'
  'ApiCompat vs feed baseline'
)
if [ "$(printf '%s\n' "${protected_steps[@]}")" != "$(printf '%s\n' "${expected_protected_steps[@]}")" ]; then
  echo 'protected High-step inventory drifted:' >&2
  printf 'actual:   %s\n' "${protected_steps[*]}" >&2
  printf 'expected: %s\n' "${expected_protected_steps[*]}" >&2
  exit 1
fi

# Every risk producer must execute the reviewed classifier over the exact base
# and head and publish that result. This rejects a syntactically valid forced-Small
# producer as well as producer/consumer drift.
risk_step_count="$(grep -c '^      - name: Select risk-scaled verification$' "$workflow")"
classifier_call_count="$(grep -c 'result="$(bash scripts/ci-risk --base "$BASE_SHA" --head "$HEAD_SHA")"' "$workflow")"
test "$risk_step_count" -gt 0
test "$classifier_call_count" -eq "$risk_step_count"
# Use a simpler literal count for the publish contract; grep escaping above is
# intentionally not the authority for this assertion.
test "$(grep -F -c 'printf '\''%s\n'\'' "$result" >> "$GITHUB_OUTPUT"' "$workflow")" -eq "$risk_step_count"
if grep -Eq '(^|[[:space:]])(profile|test_tier|protected_controls)=(small|normal|none|false)([[:space:]]|$)' "$workflow"; then
  echo 'workflow contains a forced low-risk classifier output' >&2
  exit 1
fi

empty="$($classifier --paths)"
grep -qx 'profile=high' <<<"$empty"
grep -qx 'protected_controls=true' <<<"$empty"

# GitHub consumes this as a line protocol. An invalid path must select High without
# being able to inject a second output key through CR/LF characters.
injected="$($classifier --paths $'docs/ok.md\nprofile=small' $'docs/ok.md\rtest_tier=none')"
grep -qx 'profile=high' <<<"$injected"
test "$(grep -c '^profile=' <<<"$injected")" -eq 1
test "$(grep -c '^test_tier=' <<<"$injected")" -eq 1
if [[ "$injected" == *$'\r'* ]]; then
  echo 'classifier output retained a carriage return' >&2
  exit 1
fi

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
grep -qx 'profile=high' <<<"$diff_result"
grep -q 'src/App/Code.fs' <<<"$diff_result"
grep -q 'docs/old.md' <<<"$diff_result"
grep -q 'docs/new.md' <<<"$diff_result"

malformed="$($classifier --base)"
grep -qx 'profile=high' <<<"$malformed"
grep -qx 'protected_controls=true' <<<"$malformed"

echo 'ci-risk tests: PASS'
