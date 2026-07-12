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
#   (#287, enforce_admins=true). A non-zero exit from this script blocks the merge button for
#   everyone, admins included.
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
# FAIL-CLOSED — a gate that could not run has NOT passed (FS.GG.SDD#381, epic FS-GG/.github#266)
#   Every non-OK outcome exits non-zero: a real CP#### BREAK, an Indeterminate (the pack, the tool,
#   or the feed read failed — the comparison never happened), and a NoBaselineYet for a package that
#   is not explicitly allowlisted in lib/apicompat-classify.sh.
#
#   It did not used to. Only BREAK exited 1; Indeterminate and NoBaselineYet exited 0 and the
#   required check went green. NU1403 then made the pack fail on EVERY run for as long as the
#   pre-ADR-0032 lock bug existed — so this gate spent its whole life Indeterminate, reporting
#   `success`, having never once compared an API. "Compared and clean" and "never compared" rendered
#   identically, which is the only reason it took months to notice.
#
# BASELINE RATCHET — this gate cannot catch a break that already SHIPPED
#   The baseline is whatever is LATEST on the feed, so publishing a break makes it the new baseline
#   and the gate goes green against it forever after. The window in which a break is catchable is
#   exactly "introduced but not yet published" — i.e. the PR that introduces it. That is the correct
#   design (catch it BEFORE release), but it has a sharp edge: a green run here says nothing about
#   whether the CURRENT baseline was itself a legitimate release. It was not, once already —
#   FS.GG.Contracts 1.4.1 shipped a removed-constructor break as a PATCH while this gate was blind,
#   and the gate has been green against it ever since (FS.GG.SDD#381; the SemVer resolution is a
#   cross-repo release event, tracked separately).
#
# AUTH
#   Needs read access to https://nuget.pkg.github.com/FS-GG. Provide a token via NUGET_FEED_TOKEN
#   (CI: secrets.GITHUB_TOKEN with `packages: read`; locally: a PAT or `gh auth token`). CPM requires
#   package source mapping, so we write a throwaway, source-mapped nuget.config (via --configfile)
#   that serves only FS.GG.* from the feed (everything else from nuget.org).
#
#   NO TOKEN IS A HARD FAILURE, not a skip. It used to exit 0 "advisory-clean" to keep fork PRs
#   mergeable — but a fork PR is *given* a GITHUB_TOKEN, it simply cannot read the FS-GG feed with
#   it, so that path never fired on a fork anyway: forks fell through the NoBaselineYet hole instead
#   and were waved through unverified. This repo has taken 0 fork PRs in 212 and has 0 forks, so no
#   real case is being served by exiting 0 without checking. If fork PRs ever matter, make it a
#   VISIBLE decision in the CI job (a conditional `continue-on-error` on
#   `github.event.pull_request.head.repo.fork`) rather than a silent fallthrough here.
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
  echo "::error title=ApiCompat gate could not run::no feed token (NUGET_FEED_TOKEN / GH_TOKEN / GITHUB_TOKEN) — the baseline is unreadable, so no API comparison was performed. A gate that could not run has not passed (FS.GG.SDD#381). In CI, grant the job \`packages: read\`; locally, export NUGET_FEED_TOKEN=\$(gh auth token)." >&2
  exit 1
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

# The verdict/allowlist/log-rendering logic is factored into scripts/lib/apicompat-classify.sh so it
# can be unit-tested without the feed or the SDK (scripts/tests/apicompat-check.test.sh, #381).
# shellcheck source=lib/apicompat-classify.sh
. "$(dirname "${BASH_SOURCE[0]}")/lib/apicompat-classify.sh"

# Resolve the latest published version of package id $1 on the feed.
#   sets FEED_STATUS  = ok | absent | error
#        FEED_VERSION = the selected baseline (only when ok)
#        FEED_DETAIL  = why, for the log (when absent/error)
#
# The HTTP status is load-bearing and must NOT be thrown away. This used to be `curl -fsSL … 2>/dev/null`
# piped straight into the version picker, which rendered EVERY failure — 401, 403, a 5xx, a DNS
# blip — as an empty string, i.e. as NoBaselineYet, i.e. (before #381) as a silent pass. Anyone who
# broke the feed credentials got a green REQUIRED gate. Separate "the feed says this package has no
# versions" from "the feed did not answer": only the first is NoBaselineYet, and the second is an
# Indeterminate that must redden the gate.
#
# NOTE GitHub Packages answers 404 for an unauthorized read as readily as for a genuinely absent
# package, so `absent` is not proof of absence either — which is why an unallowlisted NoBaselineYet
# also fails. The allowlist is the only thing that turns a missing baseline green.
#
# The flat-container `versions` array is NOT guaranteed sorted — GitHub Packages returns it
# newest-first — so pick_latest_version chooses the max explicitly rather than by position (#90).
feed_latest_version() {
  local id_lower body errf code rc
  id_lower="$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')"
  body="$workdir/feed-$id_lower.json"
  errf="$workdir/feed-$id_lower.err"
  FEED_STATUS="error"; FEED_VERSION=""; FEED_DETAIL=""

  code="$(curl -sSL --retry 2 --max-time 30 -o "$body" -w '%{http_code}' \
            -H "Authorization: Bearer $token" \
            "$FEED_DL/$id_lower/index.json" 2>"$errf")"
  rc=$?
  if [ "$rc" -ne 0 ]; then
    FEED_DETAIL="curl failed (exit $rc): $(tr '\n' ' ' < "$errf" | cut -c1-160)"
    return
  fi

  case "$code" in
    200)
      FEED_VERSION="$(pick_latest_version < "$body")"
      if [ -n "$FEED_VERSION" ]; then
        FEED_STATUS="ok"
      else
        FEED_STATUS="absent"; FEED_DETAIL="feed returned no versions for this package"
      fi
      ;;
    404) FEED_STATUS="absent"; FEED_DETAIL="HTTP 404 — not on the feed (or the token cannot see it)" ;;
    401 | 403) FEED_DETAIL="HTTP $code — the feed rejected the token (needs \`packages: read\` on FS-GG)" ;;
    *) FEED_DETAIL="HTTP $code from the feed" ;;
  esac
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
echo "REQUIRED gate: any outcome below other than OK exits non-zero and blocks the merge."
echo "feed: $FEED_URL   projects: ${#PROJECTS[@]}"
echo

# Every project lands in exactly one bucket, and they sum to ${#PROJECTS[@]} — so the summary can
# never quietly lose one (`allowlisted` is the only bucket that is both non-OK and non-blocking).
ok=0; broke=0; nobaseline=0; indeterminate=0; allowlisted=0

# Assign the arrays rather than `declare -a` them: under `set -u`, ${#arr[@]} on a declared-but-never-
# assigned array is an *unbound variable* error, so the clean path would blow up on its own summary.
break_lines=()
blocked_lines=()

for proj in "${PROJECTS[@]}"; do
  pkgid="$(grep -oE '<PackageId>[^<]+</PackageId>' "$proj" | sed -E 's/<\/?PackageId>//g' | head -1)"
  [ -z "$pkgid" ] && pkgid="$(basename "$proj" .fsproj)"

  baseline="$FORCE_BASELINE"
  if [ -z "$baseline" ]; then
    feed_latest_version "$pkgid"
    case "$FEED_STATUS" in
      ok)
        baseline="$FEED_VERSION"
        ;;
      absent)
        # No baseline. Green ONLY for a package explicitly allowlisted as never-yet-published;
        # otherwise the gate has nothing to compare against and must not claim it compared (#381).
        if apicompat_baseline_optional "$pkgid"; then
          printf '  %-22s NoBaselineYet (allowlisted — never published)\n' "$pkgid"
          allowlisted=$((allowlisted + 1))
        else
          printf '  %-22s NoBaselineYet (NOT allowlisted — %s)\n' "$pkgid" "$FEED_DETAIL"
          nobaseline=$((nobaseline + 1))
          blocked_lines+=("    $pkgid: no baseline to compare against — $FEED_DETAIL")
          echo "::error title=ApiCompat has no baseline for $pkgid::$FEED_DETAIL — nothing was compared. Allowlist it in scripts/lib/apicompat-classify.sh only if it is genuinely unpublished."
        fi
        continue
        ;;
      *)
        printf '  %-22s Indeterminate (feed read failed — %s)\n' "$pkgid" "$FEED_DETAIL"
        indeterminate=$((indeterminate + 1))
        blocked_lines+=("    $pkgid: feed read failed — $FEED_DETAIL")
        echo "::error title=ApiCompat could not read the baseline for $pkgid::$FEED_DETAIL — nothing was compared."
        continue
        ;;
    esac
  fi

  cv="$(check_version "$baseline")"
  log="$workdir/${pkgid}.log"
  if dotnet pack "$proj" -c Release --configfile "$cfg" \
        -p:Version="$cv" \
        -p:EnablePackageValidation=true \
        -p:PackageValidationBaselineVersion="$baseline" \
        -o "$workdir/out" >"$log" 2>&1; then
    printf '  %-22s OK            (compatible with %s)\n' "$pkgid" "$baseline"
    ok=$((ok + 1))
  else
    if grep -qE 'error CP[0-9]' "$log"; then
      printf '  %-22s BREAK         (vs %s)\n' "$pkgid" "$baseline"
      broke=$((broke + 1))
      # Keep the WHOLE CP#### message: `[Baseline]` sits inside it, and the `but not on …` clause
      # after it is the only thing that says which side the member is missing from (#381).
      while IFS= read -r l; do break_lines+=("    $pkgid: $l"); done \
        < <(grep -oE 'error CP[0-9]+: .*' "$log" | apicompat_strip_project_suffix | sort -u)
      echo "::error title=ApiCompat break in $pkgid::public-API break vs baseline $baseline (see job log)"
    else
      # The pack/tool failed, so the comparison NEVER HAPPENED. This is not a clean run; it is an
      # absence of a run, and it exits non-zero (#381). NU1403 lived here for months.
      printf '  %-22s Indeterminate (pack/tool failure — the API was never compared)\n' "$pkgid"
      indeterminate=$((indeterminate + 1))
      blocked_lines+=("    $pkgid: pack/tool failure vs baseline $baseline — see the tail below")
      echo "::error title=ApiCompat could not run for $pkgid::pack/tool failure — the API was never compared (baseline $baseline)."
      # Show the diagnostics, and fall back to the raw tail rather than printing nothing: an
      # Indeterminate with no visible cause is how this defect stayed invisible in the first place.
      diag="$(grep -iE 'error|warning NU|MSB[0-9]+' "$log" | tail -12)"
      [ -n "$diag" ] || diag="$(tail -12 "$log")"
      [ -n "$diag" ] || diag="(the pack produced no output at all)"
      printf '%s\n' "$diag" | sed 's/^/      /'
    fi
  fi
done

echo
echo "summary: OK=$ok  BREAK=$broke  NoBaselineYet=$nobaseline  Indeterminate=$indeterminate  Allowlisted=$allowlisted  (total ${#PROJECTS[@]})"

# The buckets must account for every project. If they don't, a package fell through the loop without
# being classified — which is precisely the shape of the defect this gate is being fixed for, so it
# is a failure rather than a rounding error.
counted=$((ok + broke + nobaseline + indeterminate + allowlisted))
if [ "$counted" -ne "${#PROJECTS[@]}" ]; then
  echo "::error title=ApiCompat accounting is wrong::classified $counted of ${#PROJECTS[@]} projects — one was never classified. Refusing to report a verdict."
  exit 1
fi

verdict="$(apicompat_verdict "$broke" "$indeterminate" "$nobaseline")"
rc=$?

if [ "${#break_lines[@]}" -gt 0 ]; then
  echo
  echo "breaking changes (force a SemVer major, or suppress deliberately with ApiCompatGenerateSuppressionFile):"
  printf '%s\n' "${break_lines[@]}"
fi

if [ "${#blocked_lines[@]}" -gt 0 ]; then
  echo
  echo "the gate could not compare these — that is a FAILURE, not a pass (#381):"
  printf '%s\n' "${blocked_lines[@]}"
fi

if [ "$rc" -ne 0 ]; then
  echo
  echo "verdict: $verdict — exiting $rc (a gate that could not run has not passed)"
fi
exit "$rc"
