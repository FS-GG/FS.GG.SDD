#!/usr/bin/env bash
# apicompat-check.sh — breaking-change (ApiCompat / Package Validation) detector for the SDD-owned
# cross-repo contract package FS.GG.Contracts. H3 / FS-GG/.github#20, epic #16 Pillar 5.
# A break found here BLOCKS the merge in this repo — see ENFORCING below before calling it advisory.
#
# WHAT IT DOES
#   Packs FS.GG.Contracts Release with the .NET SDK's Package Validation enabled and compares the
#   freshly-packed assembly against its published BASELINE on the org GitHub Packages feed. A removed
#   or changed public member surfaces as a CP#### error — i.e. a public-API break that, under the
#   registry's version ranges (registry coherence id `fsgg-contracts`), must force a SemVer major.
#
# SCOPE
#   FS.GG.Contracts only. It is the single SDD-owned cross-repo *contract* package consumers pin
#   (Governance/Templates/SDD re-type onto it). The other IsPackable projects (the fsgg-sdd CLI +
#   its internal libs) are not feed-published contract surfaces, so they are out of scope here.
#
# WHY THIS SHAPE (not the shared FsggApiGate knob)
#   FS.GG.Contracts is F#. Microsoft.CodeAnalysis.PublicApiAnalyzers (the C# half of the org
#   shared-build-config api-breaking-change-gate) is a Roslyn/C# source analyzer and does NOT
#   analyze F# — so the operative detector is the language-agnostic SDK ApiCompat / Package
#   Validation (assembly + package level). Mechanism recorded in FS-GG/.github registry coherence
#   id `apicompat-publicapi-gate` (Governance spec 088 research D1). The source-level public-surface
#   record stays the committed .fsi signatures.
#
# ENFORCING — a break here HARD-BLOCKS the merge (do not call this advisory; FS.GG.SDD#384)
#   The advisory → required ratchet has ALREADY HAPPENED (FS-GG/.github#20 resolved). The
#   `api-compatibility-gate` job in .github/workflows/gate.yml carries no `continue-on-error`, and
#   `API compatibility gate (breaking-change → SemVer major)` is a REQUIRED status check on `main`
#   (#287, enforce_admins=true). A non-zero exit from this script therefore blocks the merge button
#   for everyone, admins included.
#
#   This header used to describe the script as advisory while exiting 1 into that required gate. The
#   contradiction is not cosmetic: it tells whoever is staring at a red gate that they are looking at
#   a soft signal they can move past, when in fact nothing merges until the break is resolved — which
#   is exactly the afternoon it cost in FS.GG.SDD#384. If the gate is ever ratcheted back down, change
#   the CI job and this comment together.
#
#   The advisory/required choice lives ENTIRELY in the CI job + branch protection, never here: this
#   script always exits non-zero on a real break and needs no edit to move either way. It also runs as
#   a SEPARATE job and never reddens the normal build/release pack (Package Validation is left OFF
#   there), so `gate` / `build-config-drift` are unaffected by it.
#
#   Fail-safe: it exits non-zero ONLY on a genuine CP#### break. No baseline on the feed is reported
#   NoBaselineYet and a pack/tool failure unrelated to API is reported Indeterminate — neither is a
#   silent pass, and neither reddens the gate. The one deliberate fail-OPEN is the no-token path (see
#   AUTH): it exits 0 WITHOUT checking, so that fork PRs — which get no token — can still merge.
#
# AUTH
#   Needs read access to https://nuget.pkg.github.com/FS-GG. Provide a token via NUGET_FEED_TOKEN
#   (CI: secrets.GITHUB_TOKEN with `packages: read`; locally: a PAT or `gh auth token`). CPM requires
#   package source mapping, so we write a throwaway, source-mapped nuget.config (via --configfile)
#   that serves only FS.GG.* from the feed (everything else from nuget.org).
#
# USAGE
#   scripts/apicompat-check.sh [--baseline <version>]
#     --baseline <version>  force the baseline version (default: the package's latest on the feed).
set -uo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

# Packable contract projects in scope (PackageId is read from the fsproj).
PROJECTS=("src/FS.GG.Contracts/FS.GG.Contracts.fsproj")

FEED_URL="https://nuget.pkg.github.com/FS-GG/index.json"
FEED_DL="https://nuget.pkg.github.com/FS-GG/download"
FORCE_BASELINE=""
while [ $# -gt 0 ]; do
  case "$1" in
    --baseline) FORCE_BASELINE="${2:-}"; shift 2 ;;
    *) echo "unknown arg: $1" >&2; exit 2 ;;
  esac
done

token="${NUGET_FEED_TOKEN:-${GH_TOKEN:-${GITHUB_TOKEN:-}}}"
if [ -z "$token" ]; then
  echo "::warning::apicompat-check: no feed token (NUGET_FEED_TOKEN / GH_TOKEN / GITHUB_TOKEN) — cannot read baselines. SKIPPING the check and exiting 0 WITHOUT verifying the API surface (deliberate fail-open: a fork PR gets no token, and this gate is required, so failing here would block every fork)." >&2
  exit 0
fi
feed_user="${NUGET_FEED_USER:-${GITHUB_ACTOR:-x-access-token}}"

workdir="$(mktemp -d)"
trap 'rm -rf "$workdir"' EXIT
cfg="$workdir/nuget.config"
cat > "$cfg" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="fsgg" value="$FEED_URL" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
    <packageSource key="fsgg"><package pattern="FS.GG.*" /></packageSource>
  </packageSourceMapping>
  <packageSourceCredentials>
    <fsgg>
      <add key="Username" value="$feed_user" />
      <add key="ClearTextPassword" value="$token" />
    </fsgg>
  </packageSourceCredentials>
</configuration>
EOF

# The version-selection is factored into scripts/lib/pick-latest-version.sh so it can be
# unit-tested without the feed (scripts/tests/pick-latest-version.test.sh, #91).
# shellcheck source=lib/pick-latest-version.sh
. "$(dirname "${BASH_SOURCE[0]}")/lib/pick-latest-version.sh"

# Latest published version of a package id on the feed, or empty if none (NoBaselineYet).
# The flat-container `versions` array is NOT guaranteed sorted — GitHub Packages returns it
# newest-first — so pick_latest_version chooses the max explicitly rather than by position.
latest_version() {
  local id_lower; id_lower="$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')"
  curl -fsSL -H "Authorization: Bearer $token" "$FEED_DL/$id_lower/index.json" 2>/dev/null \
    | pick_latest_version
}

# A check version strictly greater than the baseline that PRESERVES prerelease-ness (so a package
# with prerelease dependencies doesn't trip NU5104). For a prerelease baseline append `.apicheck`
# (more prerelease fields sort higher); a stable baseline bumps its patch. ApiCompat still reports
# real breaks regardless of the version number; additions — which are not breaks — never error.
check_version() {
  local b="$1"
  if [[ "$b" == *-* ]]; then printf '%s.apicheck' "$b"; return; fi
  local major minor patch; IFS='.' read -r major minor patch <<<"$b"
  printf '%s.%s.%s' "${major:-0}" "${minor:-0}" "$(( ${patch:-0} + 1 ))"
}

echo "apicompat-check — ApiCompat/Package Validation vs the org feed baseline"
echo "REQUIRED gate: a BREAK below exits non-zero and blocks the merge (this is not an advisory run)."
echo "feed: $FEED_URL   projects: ${#PROJECTS[@]}"
echo

ok=0; broke=0; nobaseline=0; indeterminate=0
declare -a break_lines

for proj in "${PROJECTS[@]}"; do
  pkgid="$(grep -oE '<PackageId>[^<]+</PackageId>' "$proj" | sed -E 's/<\/?PackageId>//g' | head -1)"
  [ -z "$pkgid" ] && pkgid="$(basename "$proj" .fsproj)"

  baseline="$FORCE_BASELINE"
  [ -z "$baseline" ] && baseline="$(latest_version "$pkgid")"
  if [ -z "$baseline" ]; then
    printf '  %-22s NoBaselineYet (not on feed)\n' "$pkgid"
    nobaseline=$((nobaseline+1)); continue
  fi

  cv="$(check_version "$baseline")"
  log="$workdir/${pkgid}.log"
  if dotnet pack "$proj" -c Release --configfile "$cfg" \
        -p:Version="$cv" \
        -p:EnablePackageValidation=true \
        -p:PackageValidationBaselineVersion="$baseline" \
        -o "$workdir/out" >"$log" 2>&1; then
    printf '  %-22s OK            (compatible with %s)\n' "$pkgid" "$baseline"
    ok=$((ok+1))
  else
    if grep -qE 'error CP[0-9]' "$log"; then
      printf '  %-22s BREAK         (vs %s)\n' "$pkgid" "$baseline"
      broke=$((broke+1))
      while IFS= read -r l; do break_lines+=("    $pkgid: $l"); done \
        < <(grep -oE 'error CP[0-9]+: .*' "$log" | sed -E 's/ \[.*//' | sort -u)
      echo "::warning title=ApiCompat break in $pkgid::public-API break vs baseline $baseline (see job log)"
    else
      printf '  %-22s Indeterminate (pack/tool failure — not a clean pass; see log)\n' "$pkgid"
      indeterminate=$((indeterminate+1))
      tail -3 "$log" | sed 's/^/      /'
    fi
  fi
done

echo
echo "summary: OK=$ok  BREAK=$broke  NoBaselineYet=$nobaseline  Indeterminate=$indeterminate  (total ${#PROJECTS[@]})"
if [ "$broke" -gt 0 ]; then
  echo
  echo "breaking changes (force a SemVer major, or suppress deliberately with ApiCompatGenerateSuppressionFile):"
  printf '%s\n' "${break_lines[@]}"
  exit 1
fi
exit 0
