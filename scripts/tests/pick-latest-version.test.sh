#!/usr/bin/env bash
# Unit test for pick_latest_version (FS.GG.SDD#91) — the ApiCompat baseline resolver.
# Regression guard for #90: the org feed returns `versions` newest-first, so the original
# positional `tail -1` picked the OLDEST version as the baseline. Each case feeds a real
# flat-container index.json payload and asserts the selected version. Case "descending
# (real GH shape)" is the one that FAILS against the pre-#90 `tail -1` implementation.
#
# Run:  bash scripts/tests/pick-latest-version.test.sh   (no network, no dotnet)
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../lib/pick-latest-version.sh
. "$here/../lib/pick-latest-version.sh"

fail=0
check() {
  local name="$1" input="$2" expected="$3" got
  got="$(printf '%s' "$input" | pick_latest_version)"
  if [ "$got" = "$expected" ]; then
    printf '  ok   %-28s -> %s\n' "$name" "${got:-<empty>}"
  else
    printf '  FAIL %-28s expected %s, got %s\n' "$name" "${expected:-<empty>}" "${got:-<empty>}"
    fail=1
  fi
}

# newest-first, the real GitHub Packages shape — the #90 regression case
check "descending-real-gh-shape" '{"versions":["1.4.0","1.2.0","1.1.1","1.1.0","1.0.1"]}' "1.4.0"
check "unsorted"                 '{"versions":["1.2.0","1.10.0","1.4.0"]}'                 "1.10.0"
check "stable-over-prerelease"   '{"versions":["1.4.0","1.5.0-preview.1","1.4.0"]}'        "1.4.0"
check "prerelease-fallback"      '{"versions":["0.2.0-preview.1","0.2.0-preview.2"]}'      "0.2.0-preview.2"
check "empty"                    '{"versions":[]}'                                         ""

if [ "$fail" -ne 0 ]; then
  echo "pick-latest-version.test.sh: FAILURES" >&2
  exit 1
fi
echo "pick-latest-version.test.sh: all passed"
