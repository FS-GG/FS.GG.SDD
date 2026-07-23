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
# Run:  bash scripts/tests/apicompat-check.test.sh   (no network; requires the repo's .NET SDK)
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

# --- ambient same-ID/version cache isolation ---------------------------------------------------

# This is deliberately a real NuGet/ApiCompat regression, not an assertion over shell text. Two
# byte-different packages are packed with the same ID/version:
#
#   * the configured feed contains the authoritative baseline, compatible with the candidate;
#   * the ambient global-packages cache is pre-seeded from another feed with a PoisonOnly member.
#
# Without gate-owned NUGET_PACKAGES + NUGET_HTTP_CACHE_PATH, NuGet reuses the poisoned bytes and
# Package Validation reports CP0002. With isolation, the exact production script resolves the
# configured-feed package and reports OK.
check_ambient_cache_isolation() (
  set -euo pipefail

  local fixture_root="$1"
  local package_id="FS.GG.ApiCompat.CacheProbe"
  local baseline_version="1.0.0"
  local authoritative="$fixture_root/authoritative"
  local poison="$fixture_root/poison"
  local candidate="$fixture_root/candidate"
  local consumer="$fixture_root/consumer"
  local authoritative_feed="$fixture_root/authoritative-feed"
  local poison_feed="$fixture_root/poison-feed"
  local ambient_packages="$fixture_root/ambient-packages"
  local ambient_http_cache="$fixture_root/ambient-http-cache"
  local build_packages="$fixture_root/build-packages"

  mkdir -p \
    "$authoritative" "$poison" "$candidate" "$consumer" \
    "$authoritative_feed" "$poison_feed" \
    "$ambient_packages" "$ambient_http_cache" "$build_packages"

  for project in "$authoritative" "$poison" "$candidate"; do
    cat > "$project/Probe.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <PackageId>$package_id</PackageId>
    <AssemblyName>$package_id</AssemblyName>
    <Version>$baseline_version</Version>
  </PropertyGroup>
  <Target Name="CaptureApiCompatCacheEnvironment"
          BeforeTargets="Pack"
          Condition="'\$(APICOMPAT_ENV_PROBE)' != ''">
    <WriteLinesToFile File="\$(APICOMPAT_ENV_PROBE)"
                      Lines="\$(NUGET_PACKAGES)|\$(NUGET_HTTP_CACHE_PATH)"
                      Overwrite="true" />
  </Target>
</Project>
EOF
  done

  cat > "$authoritative/Contract.cs" <<'EOF'
namespace FS.GG.ApiCompat.CacheProbe;
public sealed class Contract
{
    public string Value() => "authoritative";
}
EOF
  cp "$authoritative/Contract.cs" "$candidate/Contract.cs"
  cat > "$poison/Contract.cs" <<'EOF'
namespace FS.GG.ApiCompat.CacheProbe;
public sealed class Contract
{
    public string Value() => "poison";
    public string PoisonOnly() => "ambient";
}
EOF

  NUGET_PACKAGES="$build_packages" NUGET_HTTP_CACHE_PATH="$fixture_root/build-http-cache" \
    dotnet pack "$authoritative/Probe.csproj" -c Release -o "$authoritative_feed" \
      --nologo --verbosity quiet
  NUGET_PACKAGES="$build_packages" NUGET_HTTP_CACHE_PATH="$fixture_root/build-http-cache" \
    dotnet pack "$poison/Probe.csproj" -c Release -o "$poison_feed" \
      --nologo --verbosity quiet

  cat > "$consumer/Consumer.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="$package_id" Version="[$baseline_version]" />
  </ItemGroup>
</Project>
EOF
  cat > "$consumer/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="poison" value="$poison_feed" />
  </packageSources>
</configuration>
EOF

  NUGET_PACKAGES="$ambient_packages" NUGET_HTTP_CACHE_PATH="$ambient_http_cache" \
    dotnet restore "$consumer/Consumer.csproj" --configfile "$consumer/nuget.config" \
      --nologo --verbosity quiet

  test -f \
    "$ambient_packages/fs.gg.apicompat.cacheprobe/$baseline_version/lib/net10.0/$package_id.dll"

  NUGET_PACKAGES="$ambient_packages" \
  NUGET_HTTP_CACHE_PATH="$ambient_http_cache" \
  NUGET_FEED_TOKEN="functional-test-token" \
  APICOMPAT_TEST_PROJECT="$candidate/Probe.csproj" \
  APICOMPAT_TEST_FEED_URL="$authoritative_feed" \
  APICOMPAT_ENV_PROBE="$fixture_root/gate-env.txt" \
    "$here/../apicompat-check.sh" --baseline "$baseline_version" \
      >"$fixture_root/gate.log" 2>&1

  grep -F "FS.GG.ApiCompat.CacheProbe OK" "$fixture_root/gate.log" >/dev/null

  local gate_packages gate_http_cache
  IFS='|' read -r gate_packages gate_http_cache < "$fixture_root/gate-env.txt"
  test "$gate_packages" != "$ambient_packages"
  test "$gate_http_cache" != "$ambient_http_cache"
  test "$(basename "$gate_packages")" = "packages"
  test "$(basename "$gate_http_cache")" = "http-cache"
  test "$(dirname "$gate_packages")" = "$(dirname "$gate_http_cache")"
  test ! -e "$gate_packages"
  test ! -e "$gate_http_cache"
)

cache_fixture="$(mktemp -d)"
if check_ambient_cache_isolation "$cache_fixture"; then
  printf '  ok   %-32s\n' "ambient-cache-isolated"
else
  printf '  FAIL %-32s\n' "ambient-cache-isolated"
  sed 's/^/       /' "$cache_fixture/gate.log" 2>/dev/null || true
  fail=1
fi
rm -rf "$cache_fixture"

if [ "$fail" -ne 0 ]; then
  echo "apicompat-check.test.sh: FAILURES" >&2
  exit 1
fi
echo "apicompat-check.test.sh: all passed"
