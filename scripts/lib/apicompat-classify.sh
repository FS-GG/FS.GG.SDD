#!/usr/bin/env bash
# apicompat-classify.sh — the pure half of apicompat-check.sh: how a per-package outcome maps to the
# gate's exit code, and how a raw MSBuild CP#### line is rendered into the log. Factored out (like
# lib/pick-latest-version.sh) so it is unit-testable without the feed or the SDK
# (scripts/tests/apicompat-check.test.sh, FS.GG.SDD#381).
#
# WHY THIS EXISTS
#   The gate was fail-OPEN. Only BREAK exited non-zero, so `Indeterminate` (the tool never ran) and
#   `NoBaselineYet` (nothing to compare against) BOTH exited 0 and the REQUIRED check went green.
#   The classification was always correct; the exit code threw it away — the gate said the right
#   thing in its log and `success` in its status, and only the status is load-bearing.
#
#   That was not hypothetical. NU1403 broke this script's pack on every run for as long as the
#   pre-ADR-0032 lock bug existed, so every run was Indeterminate and this repo had NO API
#   compatibility checking at all — and nobody could tell, because "compared and clean" and "never
#   compared" rendered identically (FS.GG.SDD#381, epic FS-GG/.github#266). A real binary break
#   shipped through the blind gate in the meantime; see the BASELINE RATCHET note in
#   apicompat-check.sh for why it can never re-report it.
#
#   The rule this file encodes: A GATE THAT COULD NOT RUN HAS NOT PASSED. Every non-OK outcome
#   exits non-zero.

# Package ids allowed to resolve NO published baseline — i.e. genuinely never released yet. A package
# NOT listed here that resolves no baseline FAILS the gate.
#
# This is an ALLOWLIST, not a fallthrough (FS.GG.SDD#381). "Not on the feed" is one HTTP status away
# from "the feed stopped answering", and GitHub Packages answers 404 for unauthorized reads as
# readily as for absent ones — so treating a missing baseline as a pass hands anyone who breaks the
# feed credentials a green required check.
#
# EMPTY ON PURPOSE: FS.GG.Contracts has been published since 1.0.0 and must always have a baseline.
# Add an id here ONLY while introducing a genuinely new package, and remove it once the first version
# reaches the feed.
APICOMPAT_NO_BASELINE_ALLOWED=()

# True when $1 is permitted to have no baseline.
apicompat_baseline_optional() {
  local id="$1" allowed
  for allowed in ${APICOMPAT_NO_BASELINE_ALLOWED[@]+"${APICOMPAT_NO_BASELINE_ALLOWED[@]}"}; do
    [ "$allowed" = "$id" ] && return 0
  done
  return 1
}

# Strip MSBuild's trailing " [/path/to/Foo.fsproj]" (or "…fsproj::TargetFramework=net10.0]") from a
# diagnostic line — and nothing else. stdin → stdout, line-oriented.
#
# THE BUG THIS REPLACES: `sed 's/ \[.*//'` cut at the FIRST " [". An ApiCompat CP#### message
# *contains* one:
#
#   error CP0002: Member 'X..ctor(…)' exists on [Baseline] lib/net10.0/X.dll
#                                     but not on lib/net10.0/X.dll [/repo/src/X/X.fsproj]
#                               ^^^^^^^^^^^^ cut here
#
# so the log lost the `but not on …` clause, and the single fact that matters — WHICH SIDE the member
# is missing from, i.e. whether it was removed or added — was truncated away mid-sentence. It cost a
# misdiagnosis: the surviving fragment shows only the nullable-annotated parameter types, which reads
# like a toolchain nullability artifact when it is in fact a removed constructor overload.
# Anchoring at end-of-line strips the project suffix and keeps `[Baseline]` intact.
apicompat_strip_project_suffix() {
  sed -E 's/ \[[^][]*\]$//'
}

# The gate verdict. Prints a one-word reason on stdout; RETURNS the script's exit code.
#   $1 broke          — packages with a real CP#### break
#   $2 indeterminate  — packages whose pack / tool / feed read FAILED (the check did not run)
#   $3 nobaseline     — packages with no baseline that are NOT allowlisted
#
# Any non-zero count fails: a break is a break, and every other counted outcome means the gate could
# not prove there wasn't one. Reported in severity order so the summary names the worst thing found.
apicompat_verdict() {
  local broke="${1:-0}" indeterminate="${2:-0}" nobaseline="${3:-0}"
  if [ "$broke" -gt 0 ]; then printf 'break\n'; return 1; fi
  if [ "$indeterminate" -gt 0 ]; then printf 'indeterminate\n'; return 1; fi
  if [ "$nobaseline" -gt 0 ]; then printf 'nobaseline\n'; return 1; fi
  printf 'pass\n'
  return 0
}
