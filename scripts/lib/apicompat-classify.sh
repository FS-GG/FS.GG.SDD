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
#   $1 broke          — packages with a real CP#### break                              -> exit 1
#   $2 indeterminate  — packages whose PACK/TOOL failed (a fact about the tree)        -> exit 3
#   $3 nobaseline     — packages with no baseline that are NOT allowlisted             -> exit 1
#
# FeedUnavailable is deliberately NOT an argument: it does not fail. See below.
#
# THE FIVE STATES, and the exit code each earns. This mirrors FS.GG.Rendering's
# scripts/apicompat-check.sh, which reached this model first (their #186/#216) — the mechanism is
# ONE org-registered gate (`apicompat-publicapi-gate`, Governance spec 088 D1), so it gets one
# model, not two divergent ones.
#
#   OK               packed, and ApiCompat found no break vs the baseline.            -> 0
#   BREAK            packed, and ApiCompat reported a CP#### error.                   -> 1
#   NoBaselineYet    the feed ANSWERED, and this package has no published version.    -> 1 unless
#                    Nothing to compare against.                                         allowlisted
#   Indeterminate    the pack or the tool failed. The comparison did NOT happen, and  -> 3
#                    the cause is a fact about THE TREE UNDER TEST, not the network.
#   FeedUnavailable  the feed did not answer (transport error, 5xx, 401/403, no       -> 0, ::error::
#                    token). The comparison did not happen for a reason EXTERNAL to
#                    the change.
#
# WHY FeedUnavailable EXITS 0 (ADR-0101, adopted from Rendering)
#   Requiring this check already takes a dependency on feed availability. A GitHub Packages outage
#   must INFORM a merge, not block every merge in the org behind an external service. It is a loud
#   `::error::` and an explicit "not a pass" line — never a silent green. The split is on WHO FAILED
#   TO ANSWER, and it is drawn BEFORE packing, at the feed read (see feed_latest_version). Pack logs
#   are never pattern-matched for "looks like a network problem", because NU1403 looked exactly like
#   one and was not — it was the lock bug, a fact about the tree, and treating it as a feed blip is
#   how this gate went blind in the first place.
#
# WHERE SDD IS DELIBERATELY STRICTER THAN RENDERING
#   NoBaselineYet is allowlist-gated here (FS.GG.SDD#381 asks for it explicitly: "an explicit
#   allowlist, not a fallthrough"). Rendering exits 0 on it unconditionally, which is reasonable for
#   17 packables that gain new members often — but it leaves a hole: GitHub Packages answers 404 for
#   an UNAUTHORIZED read as readily as for an absent package, so a token that quietly loses
#   `packages: read` reports NoBaselineYet and passes. SDD has exactly one packable, published since
#   1.0.0, so for SDD a missing baseline can only mean something is wrong.
apicompat_verdict() {
  local broke="${1:-0}" indeterminate="${2:-0}" nobaseline="${3:-0}"
  if [ "$broke" -gt 0 ]; then printf 'break\n'; return 1; fi
  if [ "$indeterminate" -gt 0 ]; then printf 'indeterminate\n'; return 3; fi
  if [ "$nobaseline" -gt 0 ]; then printf 'nobaseline\n'; return 1; fi
  printf 'pass\n'
  return 0
}
