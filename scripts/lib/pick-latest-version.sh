#!/usr/bin/env bash
# pick-latest-version.sh — pure selection of the newest published version from a NuGet
# flat-container `index.json`, factored out of apicompat-check.sh so it can be unit-tested
# without touching the feed (FS.GG.SDD#91, regression guard for #90).
#
# WHY THIS EXISTS
#   The org GitHub Packages feed returns the `versions` array NEWEST-FIRST
#   ({"versions":["1.4.0",...,"1.0.1"]}), so the original positional `tail -1` picked the
#   OLDEST version as the ApiCompat baseline (#90). Pick the max explicitly instead.
#
# CONTRACT
#   stdin  : the raw flat-container index.json (or any text containing quoted "x.y.z" tokens)
#   stdout : the selected version, or nothing when there are none (NoBaselineYet)
#   Prefers the highest STABLE release; falls back to the highest prerelease ONLY when no
#   stable exists — `sort -V` ranks a prerelease ABOVE its base version (1.4.0-preview >
#   1.4.0), the opposite of SemVer precedence, so a naive `sort -V | tail -1` would regress
#   to picking a prerelease over its release on packages that ship `-preview`.
pick_latest_version() {
  local versions stable
  versions="$(grep -oE '"[0-9][^"]*"' | tr -d '"')"
  [ -z "$versions" ] && return
  stable="$(printf '%s\n' "$versions" | grep -v -e '-' | sort -V | tail -1)"
  if [ -n "$stable" ]; then
    printf '%s\n' "$stable"
  else
    printf '%s\n' "$versions" | sort -V | tail -1
  fi
}

# Directly runnable as a filter: `curl … | scripts/lib/pick-latest-version.sh`.
if [ "${BASH_SOURCE[0]}" = "${0}" ]; then
  pick_latest_version
fi
