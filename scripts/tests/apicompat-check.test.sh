#!/usr/bin/env bash
# Unit test for the pure half of the API compatibility gate (FS.GG.SDD#381) — lib/apicompat-classify.sh.
#
# Regression guard for the gate's two defects, both of which rendered as a GREEN required check:
#
#   1. FAIL-OPEN EXIT CODE. Only BREAK exited non-zero. `Indeterminate` (the pack/tool/feed failed —
#      the API was never compared) and `NoBaselineYet` (nothing to compare against) both exited 0.
#      NU1403 made every real run Indeterminate, so the gate reported `success` for months without
#      ever comparing an API. The `verdict-*` cases below FAIL against the pre-#381 `exit 0`.
#
#   2. TRUNCATED CP#### MESSAGE. `sed 's/ \[.*//'` cut at the first " [", but an ApiCompat message
#      CONTAINS " [Baseline]" — so the log lost the `but not on …` clause, hiding which side the
#      member is missing from. The surviving fragment shows only nullable-annotated parameter types,
#      which reads like a toolchain artifact when it is really a removed constructor overload. The
#      `strip-*` cases below FAIL against the pre-#381 sed.
#
# Run:  bash scripts/tests/apicompat-check.test.sh   (no network, no dotnet)
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../lib/apicompat-classify.sh
. "$here/../lib/apicompat-classify.sh"

fail=0

# --- apicompat_strip_project_suffix ------------------------------------------------------------

check_strip() {
  local name="$1" input="$2" expected="$3" got
  got="$(printf '%s\n' "$input" | apicompat_strip_project_suffix)"
  if [ "$got" = "$expected" ]; then
    printf '  ok   %-32s\n' "$name"
  else
    printf '  FAIL %-32s\n       expected: %s\n       got:      %s\n' "$name" "$expected" "$got"
    fail=1
  fi
}

# The REAL line the gate emits for the FS.GG.Contracts break, abridged in the parameter list only.
# Everything that matters is verbatim: the "[Baseline]" marker mid-message, and the trailing fsproj.
real_cp0002="error CP0002: Member 'Fsgg.Provider.ProviderDescriptor.ProviderDescriptor(string, Microsoft.FSharp.Core.FSharpOption<string>?)' exists on [Baseline] lib/net10.0/FS.GG.Contracts.dll but not on lib/net10.0/FS.GG.Contracts.dll [/home/runner/work/FS.GG.SDD/src/FS.GG.Contracts/FS.GG.Contracts.fsproj]"
real_cp0002_kept="error CP0002: Member 'Fsgg.Provider.ProviderDescriptor.ProviderDescriptor(string, Microsoft.FSharp.Core.FSharpOption<string>?)' exists on [Baseline] lib/net10.0/FS.GG.Contracts.dll but not on lib/net10.0/FS.GG.Contracts.dll"

# THE #381 REGRESSION CASE: the old sed truncated this at "exists on", losing "[Baseline] … but not
# on …" — i.e. the direction of the break.
check_strip "keeps-[Baseline]-and-but-not-on" "$real_cp0002" "$real_cp0002_kept"

check_strip "strips-targetframework-variant" \
  "error CP0002: Member 'X' exists on [Baseline] a.dll but not on a.dll [/r/X.fsproj::TargetFramework=net10.0]" \
  "error CP0002: Member 'X' exists on [Baseline] a.dll but not on a.dll"

check_strip "no-trailing-bracket-unchanged" \
  "error CP0008: Cannot change 'X' to 'Y'" \
  "error CP0008: Cannot change 'X' to 'Y'"

# Only the LAST bracket group goes, and only when it ends the line.
check_strip "keeps-interior-brackets" \
  "error CP0002: Member '[weird]' exists on [Baseline] a.dll [/r/X.fsproj]" \
  "error CP0002: Member '[weird]' exists on [Baseline] a.dll"

# --- apicompat_verdict --------------------------------------------------------------------------

check_verdict() {
  local name="$1" broke="$2" indet="$3" nobase="$4" want_reason="$5" want_rc="$6" got_reason got_rc
  got_reason="$(apicompat_verdict "$broke" "$indet" "$nobase")"
  got_rc=$?
  if [ "$got_reason" = "$want_reason" ] && [ "$got_rc" -eq "$want_rc" ]; then
    printf '  ok   %-32s -> %s (exit %s)\n' "$name" "$got_reason" "$got_rc"
  else
    printf '  FAIL %-32s expected %s/exit %s, got %s/exit %s\n' \
      "$name" "$want_reason" "$want_rc" "$got_reason" "$got_rc"
    fail=1
  fi
}

#            name                       broke indet nobase  reason           exit
check_verdict "clean-passes"                0     0     0    "pass"            0
check_verdict "break-fails"                 1     0     0    "break"           1

# THE #381 REGRESSION CASES. Both of these exited 0 before, and the required gate went green.
# Indeterminate earns exit 3, not 1 — it is distinguishable from a break because the tree failed to
# pack rather than the API changing (Rendering's model, adopted; see lib/apicompat-classify.sh).
check_verdict "indeterminate-alone-FAILS"   0     1     0    "indeterminate"   3
check_verdict "nobaseline-alone-FAILS"      0     0     1    "nobaseline"      1

# Severity order: name the worst thing found. A BREAK means the gate RAN and found something, which
# is the stronger signal, so it outranks a pack failure.
check_verdict "break-outranks-indet"        1     1     0    "break"           1
check_verdict "indet-outranks-nobaseline"   0     1     1    "indeterminate"   3
check_verdict "all-three"                   2     3     1    "break"           1

# FeedUnavailable is not an argument at all: the feed not answering is external to the change and
# must not block a merge (ADR-0101). It is reported loudly, never as a pass — but it exits 0, so a
# run whose ONLY non-OK outcome is FeedUnavailable is indistinguishable HERE from a clean one. That
# is deliberate; the "not compared" reporting lives in apicompat-check.sh, not in the verdict.
check_verdict "feedunavailable-not-a-fail"  0     0     0    "pass"            0

# --- apicompat_baseline_optional ----------------------------------------------------------------

# The allowlist is EMPTY on purpose: FS.GG.Contracts has been published since 1.0.0, so a missing
# baseline for it is a gate failure, never a pass. If this case ever goes red, someone has
# allowlisted a package that is in fact published — which re-opens the #381 hole for it.
if apicompat_baseline_optional "FS.GG.Contracts"; then
  printf '  FAIL %-32s FS.GG.Contracts must NOT be baseline-optional\n' "contracts-needs-a-baseline"
  fail=1
else
  printf '  ok   %-32s\n' "contracts-needs-a-baseline"
fi

if [ "$fail" -ne 0 ]; then
  echo "apicompat-check.test.sh: FAILURES" >&2
  exit 1
fi
echo "apicompat-check.test.sh: all passed"
