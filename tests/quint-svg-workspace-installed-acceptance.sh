#!/usr/bin/env bash
set -Eeuo pipefail
ulimit -c 0

# Keep the first failing source location when a redirected package/tool command
# exits under `set -e`. Print only its command name; arguments may contain feed
# locations or credentials supplied by a caller.
trap 'status=$?; if [[ $- == *e* ]]; then printf "SVG-WORKSPACE-QUINT-ERROR: %s:%s command=%s exit=%s\n" "${BASH_SOURCE[0]:-$0}" "$LINENO" "${BASH_COMMAND%% *}" "$status" >&2; exit "$status"; fi' ERR

repo_root="$(git rev-parse --show-toplevel)"
: "${QUINT_BIN:?preseed exact Quint 0.32.0 binary in QUINT_BIN}"
: "${LMT_BIN:?preseed exact lmt binary in LMT_BIN}"
: "${FABLE_BIN:?preseed exact Fable 5.13.0 executable in FABLE_BIN}"
: "${FABLE_SDK_BINDING:?preseed retained SDK provision binding in FABLE_SDK_BINDING}"
: "${SYNC_JAVA_HOME:?preseed exact Temurin JRE root in SYNC_JAVA_HOME}"
: "${SYNC_APALACHE_DIST:?preseed exact Apalache distribution in SYNC_APALACHE_DIST}"

fail() { printf 'SVG-WORKSPACE-QUINT-REFUSAL: %s\n' "$*" >&2; exit 1; }
sha() { sha256sum "$1" | cut -d' ' -f1; }

[[ "$(sha "$QUINT_BIN")" == '939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f' ]] || fail 'wrong Quint object'
[[ "$(sha "$LMT_BIN")" == '37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10' ]] || fail 'wrong lmt object'
fable_identity="$(python3 "$repo_root/scripts/verify-fable-tool.py" --tool "$FABLE_BIN" --sdk-binding "$FABLE_SDK_BINDING")" \
  || fail 'Fable package/SDK launcher identity refused'

version="${SVG_WORKSPACE_PACKAGE_VERSION:-}"
if [[ -z "$version" ]]; then
  version="$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' "$repo_root/Directory.Build.local.props" | head -1)"
fi
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || fail 'coherent stable package version is absent or malformed'
if [[ -n "${SVG_WORKSPACE_PACKAGE_VERSION:-}" && -z "${Q3_PACKAGE_SOURCE:-}" ]]; then
  fail 'explicit retained version requires a package source; packing is forbidden'
fi

if [[ "${SVG_WORKSPACE_OFFLINE_STAGE:-0}" != '1' ]]; then
  scratch="$(mktemp -d /tmp/fsgg-svg-workspace-quint.XXXXXX)"
  trap 'rm -rf -- "$scratch"' EXIT
  if [[ -n "${Q3_PACKAGE_SOURCE:-}" ]]; then
    package_source="$Q3_PACKAGE_SOURCE"
    package_origin='public-package'
  else
    package_source="$scratch/feed"
    package_origin='candidate-package'
    mkdir -p "$package_source"
    for project in FS.GG.Contracts FS.GG.SDD.Artifacts FS.GG.SDD.Commands FS.GG.SDD.Validation FS.GG.SDD.Cli; do
      dotnet pack "$repo_root/src/$project/$project.fsproj" -c Release -o "$package_source" >/dev/null
    done
    provisioning_packages="$scratch/provisioning-packages"
    NUGET_PACKAGES="$provisioning_packages" dotnet restore "$repo_root/src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj" --no-http-cache >/dev/null
    find "$provisioning_packages" -type f -name '*.nupkg' -exec cp -f '{}' "$package_source/" \;
  fi
  printf '%s\n' \
    '<?xml version="1.0" encoding="utf-8"?>' \
    '<configuration><packageSources><clear /><add key="qualified" value="'"$package_source"'" /></packageSources></configuration>' \
    >"$scratch/NuGet.Config"
  export NUGET_PACKAGES="$scratch/packages"
  export NUGET_HTTP_CACHE_PATH="$scratch/http-cache"
  dotnet tool install FS.GG.SDD.Cli --version "$version" --tool-path "$scratch/tool" \
    --configfile "$scratch/NuGet.Config" --no-cache >/dev/null

  # Restore the neutral parity probe before isolation. The offline stage replaces these two
  # placeholder sources with generated bindings but consumes this exact assets graph.
  mkdir -p "$scratch/fable-probe"
  printf '%s\n' \
    '<Project Sdk="Microsoft.NET.Sdk">' \
    '  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>' \
    '  <ItemGroup><Compile Include="Bindings.fs" /><Compile Include="Program.fs" /></ItemGroup>' \
    '</Project>' >"$scratch/fable-probe/Probe.fsproj"
  printf '%s\n' 'module Placeholder' 'let Value = "prepared"' >"$scratch/fable-probe/Bindings.fs"
  printf '%s\n' 'open Placeholder' 'printfn "%s" Value' >"$scratch/fable-probe/Program.fs"
  dotnet restore "$scratch/fable-probe/Probe.fsproj" --configfile "$scratch/NuGet.Config" --no-http-cache >/dev/null

  offline_env=(
    SVG_WORKSPACE_OFFLINE_STAGE=1
    SVG_WORKSPACE_SCRATCH="$scratch"
    NUGET_PACKAGES="$NUGET_PACKAGES"
    NUGET_HTTP_CACHE_PATH="$NUGET_HTTP_CACHE_PATH"
    QUINT_BIN="$QUINT_BIN"
    LMT_BIN="$LMT_BIN"
    FABLE_BIN="$FABLE_BIN"
    FABLE_SDK_BINDING="$FABLE_SDK_BINDING"
    SYNC_JAVA_HOME="$SYNC_JAVA_HOME"
    SYNC_APALACHE_DIST="$SYNC_APALACHE_DIST"
    Q3_PACKAGE_SOURCE="$package_source"
    Q2_JUNIT_OUT="${Q2_JUNIT_OUT:-}"
    Q2_EXACT_IR_JUNIT_OUT="${Q2_EXACT_IR_JUNIT_OUT:-}"
    Q3_JUNIT_OUT="${Q3_JUNIT_OUT:-}"
    Q3_TOOLCHAIN_OUT="${Q3_TOOLCHAIN_OUT:-}"
    SVG_WORKSPACE_PACKAGE_ORIGIN="$package_origin"
    SVG_WORKSPACE_PACKAGE_VERSION="$version"
  )

  if [[ "${SVG_WORKSPACE_SKIP_UNSHARE:-0}" == '1' ]]; then
    env "${offline_env[@]}" SVG_WORKSPACE_NAMESPACE_ISOLATED=0 bash "$0"
  else
    /usr/bin/unshare --user --map-root-user --net -- env "${offline_env[@]}" \
      SVG_WORKSPACE_NAMESPACE_ISOLATED=1 bash "$0"
  fi
  exit $?
fi

scratch="${SVG_WORKSPACE_SCRATCH:?offline scratch root is required}"
trap 'rm -rf -- "$scratch"' EXIT
cli="$scratch/tool/fsgg-sdd"
[[ -x "$cli" ]] || fail 'public CLI was not installed before network isolation'
package_origin="${SVG_WORKSPACE_PACKAGE_ORIGIN:?package origin is required}"

seed_root="$scratch/seeded-skills"
"$cli" init --root "$seed_root" >/dev/null
for skill in fs-gg-sdd-typed-reconcile fs-gg-sdd-typed-correspond; do
  cmp "$repo_root/.claude/skills/$skill/SKILL.md" "$seed_root/.claude/skills/$skill/SKILL.md" >/dev/null \
    || fail "$skill Claude seed bytes drifted"
  cmp "$seed_root/.claude/skills/$skill/SKILL.md" "$seed_root/.agents/skills/$skill/SKILL.md" >/dev/null \
    || fail "$skill agent-root seed bytes differ"
done

profile1="$scratch/profile1"
"$cli" typed-sdd author --root "$profile1" --work legacy --title Legacy \
  --agent acceptance --session retained --backend fsharp-specification-v1 >"$scratch/profile1-author.json"
"$cli" typed-sdd inspect --root "$profile1" --work legacy >"$scratch/profile1-before.json"
find "$profile1" -type f -print0 | sort -z | xargs -0 sha256sum >"$scratch/profile1.before"

printf 'wrong object\n' >"$scratch/wrong-quint"
if "$cli" typed-sdd provision --cache "$scratch/wrong-cache" --quint "$scratch/wrong-quint" --lmt "$LMT_BIN" >"$scratch/wrong-tool.json"; then
  fail 'wrong tool object was accepted'
fi
grep -F 'typedSdd.provision.objectMismatch' "$scratch/wrong-tool.json" >/dev/null || fail 'wrong-tool refusal drifted'
if "$cli" typed-sdd provision --cache "$scratch/wrong-profile" --profile fsgg-quint-profile/1 \
  --quint "$QUINT_BIN" --lmt "$LMT_BIN" >"$scratch/wrong-profile.json"; then
  fail 'unsupported provision profile was accepted'
fi
grep -F 'typedSdd.provision.profileUnsupported' "$scratch/wrong-profile.json" >/dev/null || fail 'wrong-profile refusal drifted'

"$cli" typed-sdd provision --cache "$scratch/cache" --quint "$QUINT_BIN" --lmt "$LMT_BIN" >"$scratch/provision.json"
grep -F '"outcome": "succeeded"' "$scratch/provision.json" >/dev/null || fail 'provisioning failed'
grep -F '"platform": "linux/amd64"' "$scratch/provision.json" >/dev/null || fail 'platform identity is absent'
grep -F '"profile": "fsgg-quint-profile/2"' "$scratch/provision.json" >/dev/null || fail 'profile identity is absent'
grep -F 'github:informalsystems/quint@v0.32.0' "$scratch/provision.json" >/dev/null || fail 'Quint provenance is absent'
grep -F 'github:driusan/lmt@62fe18f2f6a6e11c158ff2b2209e1082a4fcd59c' "$scratch/provision.json" >/dev/null || fail 'lmt provenance is absent'

cache="$scratch/cache/objects"
[[ "$(sha "$cache/939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f")" == '939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f' ]] || fail 'cached Quint drifted'
[[ "$(sha "$cache/37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10")" == '37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10' ]] || fail 'cached lmt drifted'

lifecycle_model="$scratch/workspace-lifecycle-model"
mkdir -p "$lifecycle_model"
cp "$repo_root/src/FS.GG.SDD.Artifacts/TypedSpecifications/QuintAssets/workspace-lifecycle.md" "$lifecycle_model/"
(
  cd "$lifecycle_model"
  "$LMT_BIN" workspace-lifecycle.md
  "$QUINT_BIN" typecheck workspace-lifecycle.qnt >/dev/null
  # Quint 0.32 exits zero even when --main discovers no tests. Bind the seven
  # declared runs explicitly and check their names and positive count.
  test_names=(
    filingDoesNotMutateAcceptedAuthority coherentDeltaCanReachAcceptance
    noSemanticChangeCanReachAcceptance acceptedOpaqueCanReachAcceptance
    ambiguousProposalRemainsNonAccepting contradictoryProposalRemainsNonAccepting
    staleProposalRemainsNonAccepting
  )
  selector="$(IFS='|'; printf '%s' "${test_names[*]}")"
  test_output="$("$QUINT_BIN" test workspace-lifecycle.qnt --main WorkspaceLifecycleTests \
    --match "$selector" --seed=927 --backend typescript)" || fail 'lifecycle Quint scenarios failed'
  grep -Eq '(^|[[:space:]])7 passing \(' <<<"$test_output" || fail 'lifecycle Quint scenarios were not all executed'
  for test_name in "${test_names[@]}"; do
    grep -F "ok $test_name passed 1 test(s)" <<<"$test_output" >/dev/null \
      || fail "lifecycle Quint scenario $test_name was not discovered"
  done
  empty_output="$("$QUINT_BIN" test workspace-lifecycle.qnt --main WorkspaceLifecycleTests \
    --match '^NoSuchWorkspaceLifecycleRun$' --seed=927 --backend typescript)" \
    || fail 'empty-discovery control could not run'
  if grep -Eq '(^|[[:space:]])7 passing \(' <<<"$empty_output"; then
    fail 'empty-discovery control looked like a complete run'
  fi
  "$QUINT_BIN" run workspace-lifecycle.qnt --main WorkspaceLifecycle --max-samples=100 --max-steps=6 --seed=927 \
    --backend typescript \
    --invariants filingIsImmutable acceptedAuthorityIsCoherent acceptedRevisionIsMonotonic \
      acceptedFingerprintTracksRevision acceptanceUsesExactBase staleProposalCannotAccept >/dev/null
)

"$cli" typed-sdd provision --cache "$scratch/cache" --quint "$QUINT_BIN" --lmt "$LMT_BIN" >"$scratch/provision-repeat.json"
cmp "$scratch/provision.json" "$scratch/provision-repeat.json" >/dev/null || fail 'repeat provisioning report drifted'
"$cli" typed-sdd provision --cache "$scratch/concurrent-cache" --quint "$QUINT_BIN" --lmt "$LMT_BIN" >"$scratch/provision-concurrent-a.json" &
provision_a=$!
"$cli" typed-sdd provision --cache "$scratch/concurrent-cache" --quint "$QUINT_BIN" --lmt "$LMT_BIN" >"$scratch/provision-concurrent-b.json" &
provision_b=$!
wait "$provision_a"
wait "$provision_b"
cmp "$scratch/provision-concurrent-a.json" "$scratch/provision-concurrent-b.json" >/dev/null || fail 'concurrent provisioning did not converge'
if "$cli" typed-sdd provision --cache '' --quint "$QUINT_BIN" --lmt "$LMT_BIN" >"$scratch/invalid-cache.json"; then
  fail 'invalid cache path was accepted'
fi
grep -F 'typedSdd.provision.cachePathInvalid' "$scratch/invalid-cache.json" >/dev/null || fail 'invalid-cache refusal drifted'
"$cli" typed-sdd provision --cache "$scratch/cache" \
  --quint "$cache/939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f" \
  --lmt "$cache/37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10" \
  >"$scratch/provision-alias.json" || fail 'content-addressed source alias was not idempotent'
cp -a "$scratch/cache" "$scratch/modified-cache"
printf 'modified\n' >>"$scratch/modified-cache/objects/37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10"
if "$cli" typed-sdd provision --cache "$scratch/modified-cache" --quint "$QUINT_BIN" --lmt "$LMT_BIN" >"$scratch/cache-conflict.json"; then
  fail 'modified cache object was accepted'
fi
grep -F 'typedSdd.provision.cacheConflict' "$scratch/cache-conflict.json" >/dev/null || fail 'cache-conflict refusal drifted'
if "$cli" typed-sdd author --root "$scratch/modified-root" --work demo --agent acceptance --session modified \
  --backend quint-specification-v1 --cache "$scratch/modified-cache" >"$scratch/modified-author.json"; then
  fail 'modified cache authored an authority'
fi
grep -F 'typedSdd.v2.cacheInvalid' "$scratch/modified-author.json" >/dev/null || fail 'modified-cache author refusal drifted'
[[ -z "$(find "$scratch/modified-root" -type f ! -path '*/typed-sdd-transactions/authority.lock' -print -quit 2>/dev/null)" ]] || fail 'modified-cache refusal wrote authority files'
chmod -x "$cache/37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10"
"$cli" typed-sdd provision --cache "$scratch/cache" --quint "$QUINT_BIN" --lmt "$LMT_BIN" >/dev/null
[[ -x "$cache/37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10" ]] || fail 'repeat provisioning did not repair executable mode'

fixture='tests/fixtures/quint-retained-reducer'
general_fixture='tests/fixtures/quint-neutral-authority'
for extraction in a b; do
  extraction_root="$scratch/extraction-$extraction"
  mkdir -p "$extraction_root/$general_fixture"
  cp "$repo_root/$general_fixture/model.md" "$extraction_root/$general_fixture/model.md"
  (cd "$extraction_root/$general_fixture" && "$LMT_BIN" model.md)
  "$QUINT_BIN" typecheck --out="$extraction_root/cooperative.typed.json" "$extraction_root/$general_fixture/cooperative.qnt"
done
cmp "$scratch/extraction-a/$general_fixture/cooperative.qnt" "$scratch/extraction-b/$general_fixture/cooperative.qnt" >/dev/null || fail 'neutral lmt extractions differ'
cmp "$scratch/extraction-a/cooperative.typed.json" "$scratch/extraction-b/cooperative.typed.json" >/dev/null || fail 'neutral Quint typecheck observations differ'

# A neutral consumer model still typechecks, but it cannot claim SDD alignment:
# profile-2 acceptance requires the joined SDD module catalogue.
neutral_root="$scratch/neutral-refusal"
mkdir -p "$neutral_root/$fixture"
cp "$repo_root/$fixture/scene.md" "$repo_root/$fixture/bindings.json" "$neutral_root/$fixture/"
if "$cli" typed-sdd author --root "$neutral_root" --work scene --title 'Neutral model' \
  --agent acceptance --session neutral --backend quint-specification-v1 --cache "$scratch/cache" \
  --profile fsgg-quint-profile/2 --source "$fixture/scene.md" --bindings "$fixture/bindings.json" \
  --java-home "$SYNC_JAVA_HOME" --apalache-dist "$SYNC_APALACHE_DIST" \
  >"$scratch/neutral-refusal.json"; then
  fail 'neutral model claimed SDD alignment without an accepted module catalogue'
fi
grep -F 'SYNC-MODULES-MISSING' "$scratch/neutral-refusal.json" >/dev/null || fail 'neutral model refused for another reason'
[[ ! -f "$neutral_root/readiness/scene/typed-authority.json" ]] || fail 'neutral model wrote accepted SDD authority'

# Retain the package-only exact-IR boundary over the neutral requirements and coordination
# compiler slices. The adapter script independently applies all 17 malformed-IR mutations.
ir_root="$scratch/exact-ir"
mkdir -p "$ir_root"
cp "$repo_root/docs/experiments/quint-q1/slices/requirements-and-evidence.md" "$ir_root/"
cp "$repo_root/docs/experiments/quint-q1/slices/coordination-process.md" "$ir_root/"
(cd "$ir_root" && "$LMT_BIN" requirements-and-evidence.md coordination-process.md)
for module in requirements.qnt coordination.qnt; do
  "$QUINT_BIN" typecheck --out="$ir_root/$module.typed.json" "$ir_root/$module"
done
artifact_assembly="$(find "$scratch/tool" -name FS.GG.SDD.Artifacts.dll -print -quit)"
[[ -f "$artifact_assembly" ]] || fail 'installed Artifacts assembly is absent'
dotnet fsi --reference:"$artifact_assembly" --exec \
  "$repo_root/tests/FS.GG.SDD.Artifacts.Tests/QuintExactIrAdapterTests.fsx" \
  "$ir_root/requirements.qnt.typed.json" \
  >"$scratch/exact-ir.log"
grep -F 'Exact Quint 0.32.0 Q1 IR corpus and 17 fail-closed mutations passed.' \
  "$scratch/exact-ir.log" >/dev/null || fail 'exact IR mutation corpus did not pass'

# Preserve package-only generic profile-2 compilation as a separate Artifacts
# consumer. Its S.I.R. model is not an SDD authority and must not acquire an
# implementation-alignment receipt. The installed assembly promotes the exact
# observed typed effect to a nonempty contract and generated bindings.
sir_root="$scratch/general-sir"
mkdir -p "$sir_root"
cp "$repo_root/tests/fixtures/quint-general-sir/sir-combat.md" "$sir_root/"
(cd "$sir_root" && "$LMT_BIN" sir-combat.md)
"$QUINT_BIN" typecheck --out="$sir_root/sir-combat.qnt.typed.json" "$sir_root/sir-combat.qnt" >/dev/null
dotnet fsi --reference:"$artifact_assembly" --exec \
  "$repo_root/tests/FS.GG.SDD.Artifacts.Tests/QuintGeneralSirAcceptance.fsx" \
  "$sir_root/sir-combat.qnt.typed.json" \
  "$repo_root/tests/fixtures/quint-general-sir/profile-bindings.json" \
  "$repo_root/tests/fixtures/quint-general-sir/sir-combat.md" \
  "$sir_root/sir-combat.qnt" \
  "$scratch/profile2-general" >"$scratch/profile2-general.log"
grep -F 'PROFILE-2-SIR-ACCEPTED: rules=16 properties=7 relationships=14 bounds=4 verifications=3 impacts=1 compatibility=1 actions=5' \
  "$scratch/profile2-general.log" >/dev/null || fail 'installed generic profile-2 compilation did not promote the declared catalogue'
general_contract="$scratch/profile2-general/contract.json"
for section in relationships verificationProfiles bounds impacts compatibility; do
  grep -F '"'"$section"'":[{' "$general_contract" >/dev/null || fail "generic installed contract lacks nonempty $section"
done
grep -F 'ACT-Step' "$scratch/profile2-general/bindings.fs" >/dev/null || fail 'generic installed action binding is absent'

for run in a b; do
  root="$scratch/author-$run"
  mkdir -p "$root"
  cp "$repo_root/tests/fixtures/typed-sdd-sync/specification.md" "$root/specification.md"
  cp "$repo_root/tests/fixtures/typed-sdd-sync/bindings.json" "$root/bindings.json"
  "$cli" typed-sdd author --root "$root" --work scene --title 'Accepted SDD reducer' \
    --agent acceptance --session exact --backend quint-specification-v1 --cache "$scratch/cache" \
    --profile fsgg-quint-profile/2 --source specification.md --bindings bindings.json \
    --java-home "$SYNC_JAVA_HOME" --apalache-dist "$SYNC_APALACHE_DIST" \
    >"$scratch/author-$run.json"
  "$cli" typed-sdd inspect --root "$root" --work scene >"$scratch/inspect-$run.json"
  grep -F '"outcome": "succeeded"' "$scratch/author-$run.json" >/dev/null || fail "author $run failed"
  grep -F '"outcome": "succeeded"' "$scratch/inspect-$run.json" >/dev/null || fail "inspect $run failed"
done
# Accepted authority bytes must be deterministic. Fresh sync receipts include
# verifier timestamps, temporary output paths and sampled run timings, so
# compare their stable authority/obligation fields separately.
diff -ru --exclude=sync-report.json "$scratch/author-a" "$scratch/author-b" >/dev/null || fail 'isolated accepted authority roots differ'
cmp "$scratch/author-a.json" "$scratch/author-b.json" >/dev/null || fail 'isolated author reports differ'
python3 - "$scratch/author-a/readiness/scene/quint/sync-report.json" \
  "$scratch/author-b/readiness/scene/quint/sync-report.json" <<'PY' || fail 'installed sync receipts disagree on authority or obligations'
import json, sys
with open(sys.argv[1], encoding='utf-8-sig') as source:
    first = json.load(source)
with open(sys.argv[2], encoding='utf-8-sig') as source:
    second = json.load(source)
def require(condition, message):
    if not condition:
        raise SystemExit(message)
stable = ('schema', 'operation', 'outcome', 'acceptedSha256', 'contractSha256',
          'generatedSha256', 'toolSha256', 'hostToolSha256', 'verifierToolSha256',
          'implementationSha256', 'adapterSha256', 'packageIdentity',
          'acceptedRevision', 'seed', 'maxSamples', 'maxSteps', 'scenarioCount',
          'actionCount')
require(all(key in first and key in second for key in stable), 'stable receipt field missing')
require(all(first.get(key) == second.get(key) for key in stable), 'stable receipt fields differ')
for report in (first, second):
    require(report['outcome'] == 'satisfied', 'alignment was not satisfied')
    require(report['acceptedRevision'] == 0, 'initial revision drifted')
    require(report['scenarioCount'] == 7 and report['actionCount'] == 3, 'model/reducer coverage drifted')
    require(report['correspondenceSha256'] and report['runSha256'], 'correspondence/run evidence missing')
    require([step['name'] for step in report['steps']] == ['typecheck', 'test', 'run', 'verify'], 'model steps drifted')
    require(all(step['exitCode'] == 0 for step in report['steps']), 'model step failed')
PY
grep -F 'PROD-001' "$scratch/author-a/readiness/scene/quint/bindings.fs" >/dev/null || fail 'accepted module identity is absent'
grep -F 'sandbox-contract' "$scratch/author-a/readiness/scene/quint/contract.json" >/dev/null || fail 'contract digest closure is absent'
grep -F 'SYNC-MAX-STEPS' "$scratch/author-a/readiness/scene/quint/bindings.fs" >/dev/null || fail 'accepted bound binding is absent'

probe="$scratch/fable-probe"
mkdir -p "$probe"
cp "$scratch/profile2-general/bindings.fs" "$probe/Bindings.fs"
printf '%s\n' \
  '<Project Sdk="Microsoft.NET.Sdk">' \
  '  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>' \
  '  <ItemGroup><Compile Include="Bindings.fs" /><Compile Include="Program.fs" /></ItemGroup>' \
  '</Project>' >"$probe/Probe.fsproj"
printf '%s\n' \
  'open SirCombatGenerated' \
  'printfn "%s" ContractFingerprint' \
  'Catalogue |> List.filter (fun row -> row.ExportId = "EXPORT-Rules") |> List.map _.Id |> String.concat "," |> printfn "%s"' \
  'printfn "%s" CanonicalContractJson' >"$probe/Program.fs"
dotnet run --project "$probe/Probe.fsproj" --no-restore >"$scratch/native.txt"
cmp "$scratch/profile2-general/native.txt" "$scratch/native.txt" >/dev/null || fail 'generic compiled native binding drifted'
"$FABLE_BIN" "$probe/Probe.fsproj" --outDir "$scratch/fable" --noRestore --noCache --silent
node "$scratch/fable/Program.js" >"$scratch/fable.txt"
cmp "$scratch/native.txt" "$scratch/fable.txt" >/dev/null || fail '.NET/Fable projection parity failed'
sed -i '0,/let ContractFingerprint = "/s//let ContractFingerprint = "mutated-/' "$probe/Bindings.fs"
"$FABLE_BIN" "$probe/Probe.fsproj" --outDir "$scratch/fable-mutated" --noRestore --noCache --silent
node "$scratch/fable-mutated/Program.js" >"$scratch/fable-mutated.txt"
if cmp "$scratch/fable.txt" "$scratch/fable-mutated.txt" >/dev/null; then
  fail 'independent Fable binding mutation was not observed'
fi

printf 'edited\n' >"$scratch/author-a/readiness/scene/quint/contract.json"
if "$cli" typed-sdd inspect --root "$scratch/author-a" --work scene >"$scratch/edited.json"; then
  fail 'edited authority inspected green'
fi
grep -F 'typedSdd.v2.artifactMismatch' "$scratch/edited.json" >/dev/null || fail 'freshness refusal drifted'
if "$cli" typed-sdd author --root "$scratch/missing" --work demo --agent acceptance --session missing \
  --backend quint-specification-v1 --cache "$scratch/no-cache" >"$scratch/missing-cache.json"; then
  fail 'missing cache authored an authority'
fi
grep -F 'typedSdd.v2.cacheMissing' "$scratch/missing-cache.json" >/dev/null || fail 'missing-cache refusal drifted'
[[ -z "$(find "$scratch/missing" -mindepth 1 -type f ! -path '*/typed-sdd-transactions/authority.lock' -print -quit 2>/dev/null)" ]] || fail 'missing-cache refusal wrote authority files'

typed_effect="$scratch/author-b/readiness/scene/quint/typed-effect.json"
typed_manifest="$scratch/author-b/readiness/scene/typed-authority.json"
old_typed_sha="$(sha "$typed_effect")"
printf '{"forged":true}\n' >"$typed_effect"
new_typed_sha="$(sha "$typed_effect")"
sed -i "s/$old_typed_sha/$new_typed_sha/" "$typed_manifest"
if "$cli" typed-sdd inspect --root "$scratch/author-b" --work scene >"$scratch/forged-effect.json"; then
  fail 'forged typed-effect closure inspected green'
fi
grep -F 'typedSdd.v2.typedEffectClosure' "$scratch/forged-effect.json" >/dev/null || fail 'typed-effect semantic closure drifted'

for boundary in $(seq 1 10); do
  crash_root="$scratch/crash-author-$boundary"
  mkdir -p "$crash_root"
  if FSGG_TYPED_SDD_TEST_CRASH_AFTER_MOVE="$boundary" "$cli" typed-sdd author \
    --root "$crash_root" --work demo --title Demo --agent acceptance --session "crash-$boundary" \
    --backend quint-specification-v1 --cache "$scratch/cache" >/dev/null 2>&1; then
    fail "injected author crash boundary $boundary unexpectedly succeeded"
  fi
  if "$cli" typed-sdd inspect --root "$crash_root" --work demo >"$scratch/crash-inspect-$boundary.json"; then
    fail "crash boundary $boundary exposed an authority"
  fi
  grep -F 'typedSdd.authorityMissing' "$scratch/crash-inspect-$boundary.json" >/dev/null || fail "crash boundary $boundary did not recover"
  [[ -z "$(find "$crash_root" -type f ! -path '*/typed-sdd-transactions/authority.lock' -print -quit)" ]] || fail "crash boundary $boundary left partial bytes"
done

concurrent_root="$scratch/concurrent-author"
mkdir -p "$concurrent_root"
FSGG_TYPED_SDD_TEST_PAUSE_AFTER_PREPARE_MS=1000 "$cli" typed-sdd author \
  --root "$concurrent_root" --work demo --title Demo --agent acceptance --session concurrent \
  --backend quint-specification-v1 --cache "$scratch/cache" >"$scratch/concurrent-author.json" &
author_pid=$!
for _ in $(seq 1 100); do
  [[ -n "$(find "$concurrent_root/.fsgg/typed-sdd-transactions" -name journal.json -print -quit 2>/dev/null)" ]] && break
  sleep 0.02
done
"$cli" typed-sdd inspect --root "$concurrent_root" --work demo >"$scratch/concurrent-inspect.json"
wait "$author_pid"
grep -F '"outcome": "succeeded"' "$scratch/concurrent-inspect.json" >/dev/null || fail 'concurrent inspect observed a partial authority'

"$cli" typed-sdd inspect --root "$profile1" --work legacy >"$scratch/profile1-after.json"
find "$profile1" -type f -print0 | sort -z | xargs -0 sha256sum >"$scratch/profile1.after"
cmp "$scratch/profile1.before" "$scratch/profile1.after" >/dev/null || fail 'profile-1 bytes changed'
cmp "$scratch/profile1-before.json" "$scratch/profile1-after.json" >/dev/null || fail 'profile-1 inspection changed'

migration="$scratch/migration"
mkdir -p "$migration"
"$cli" typed-sdd author --root "$migration" --work demo --title 'Neutral legacy authority' --agent acceptance --session v1 --backend fsharp-specification-v1 >/dev/null
find "$migration" -type f -print0 | sort -z | xargs -0 sha256sum >"$scratch/v1.before"
"$cli" typed-sdd migrate --root "$migration" --work demo --source work/demo/spec.md \
  --backend quint-specification-v1 --cache "$scratch/cache" --agent acceptance --session migration >"$scratch/migrate-preflight.json"
grep -F '"classification": "Migrated"' "$scratch/migrate-preflight.json" >/dev/null || fail 'migration preflight classification drifted'
find "$migration" -type f -print0 | sort -z | xargs -0 sha256sum >"$scratch/v1.preflight"
cmp "$scratch/v1.before" "$scratch/v1.preflight" >/dev/null || fail 'migration preflight wrote bytes'
"$cli" typed-sdd migrate --root "$migration" --work demo --source work/demo/spec.md --accept \
  --backend quint-specification-v1 --cache "$scratch/cache" --agent acceptance --session migration >"$scratch/migrate.json"
"$cli" typed-sdd inspect --root "$migration" --work demo >/dev/null || fail 'migrated authority did not inspect'

race_root="$scratch/replacement-rollback-race"
cp -a "$migration" "$race_root"
FSGG_TYPED_SDD_TEST_PAUSE_AFTER_PREPARE_MS=1000 "$cli" typed-sdd author \
  --root "$race_root" --work demo --title 'Concurrent replacement' --agent acceptance --session replacement \
  --backend quint-specification-v1 --cache "$scratch/cache" --accept >"$scratch/race-author.json" &
race_author_pid=$!
for _ in $(seq 1 100); do
  [[ -n "$(find "$race_root/.fsgg/typed-sdd-transactions" -name journal.json -print -quit 2>/dev/null)" ]] && break
  sleep 0.02
done
set +e
"$cli" typed-sdd rollback --root "$race_root" --work demo --accept >"$scratch/race-rollback.json"
race_rollback_status=$?
wait "$race_author_pid"
race_author_status=$?
set -e
[[ $race_author_status -eq 0 && $race_rollback_status -ne 0 ]] || fail 'replacement and rollback did not serialize to one commit'
grep -F 'typedSdd.v2.rollbackMissing' "$scratch/race-rollback.json" >/dev/null || fail 'serialized rollback refusal drifted'
"$cli" typed-sdd inspect --root "$race_root" --work demo >/dev/null || fail 'replacement/rollback race left invalid authority'

set +e
FSGG_TYPED_SDD_TEST_CRASH_AFTER_MOVE=5 "$cli" typed-sdd rollback --root "$migration" --work demo --accept >/dev/null 2>&1
rollback_crash=$?
set -e
[[ $rollback_crash -ne 0 ]] || fail 'injected rollback crash succeeded'
"$cli" typed-sdd inspect --root "$migration" --work demo >/dev/null || fail 'rollback crash did not recover the complete v2 pre-state'
"$cli" typed-sdd rollback --root "$migration" --work demo --accept >"$scratch/rollback.json"
find "$migration" -type f -print0 | sort -z | xargs -0 sha256sum >"$scratch/v1.after"
cmp "$scratch/v1.before" "$scratch/v1.after" >/dev/null || fail 'rollback did not restore exact v1 bytes'

[[ "${SVG_WORKSPACE_NAMESPACE_ISOLATED:-0}" == '1' ]] \
  || fail 'offline acceptance requires a real user and network namespace'
mkdir -p "$(dirname "${Q2_JUNIT_OUT:-$scratch/q2.xml}")" "$(dirname "${Q3_JUNIT_OUT:-$scratch/q3.xml}")"
cat >"${Q2_JUNIT_OUT:-$scratch/q2.xml}" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<testsuite name="FS.GG.SDD.SvgWorkspaceQuintQ2" tests="16" failures="0">
  <testcase classname="SvgWorkspaceQuintQ2" name="PACKAGE_ORIGIN-installed-before-isolation" />
  <testcase classname="SvgWorkspaceQuintQ2" name="real-network-namespace-isolation" />
  <testcase classname="SvgWorkspaceQuintQ2" name="exact-content-addressed-tools" />
  <testcase classname="SvgWorkspaceQuintQ2" name="neutral-model-lmt-extraction" />
  <testcase classname="SvgWorkspaceQuintQ2" name="neutral-model-quint-typecheck" />
  <testcase classname="SvgWorkspaceQuintQ2" name="two-isolated-author-roots" />
  <testcase classname="SvgWorkspaceQuintQ2" name="profile2-author-inspect" />
  <testcase classname="SvgWorkspaceQuintQ2" name="neutral-general-authority-facts" />
  <testcase classname="SvgWorkspaceQuintQ2" name="action-effect-correspondence" />
  <testcase classname="SvgWorkspaceQuintQ2" name="canonical-receipt-parity" />
  <testcase classname="SvgWorkspaceQuintQ2" name="dotnet-fable-parity" />
  <testcase classname="SvgWorkspaceQuintQ2" name="fable-independent-mutation" />
  <testcase classname="SvgWorkspaceQuintQ2" name="contract-digest-closure" />
  <testcase classname="SvgWorkspaceQuintQ2" name="workspace-lifecycle-lmt-extraction-and-typecheck" />
  <testcase classname="SvgWorkspaceQuintQ2" name="workspace-lifecycle-behavioral-witnesses" />
  <testcase classname="SvgWorkspaceQuintQ2" name="workspace-lifecycle-sampled-invariants" />
</testsuite>
EOF
sed -i "s/PACKAGE_ORIGIN/$package_origin/" "${Q2_JUNIT_OUT:-$scratch/q2.xml}"
cat >"${Q2_EXACT_IR_JUNIT_OUT:-$scratch/q2-exact-ir.xml}" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<testsuite name="FS.GG.SDD.SvgWorkspaceQuintQ2ExactIr" tests="20" failures="0">
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="requirements-exact-quint-0.32-ir" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="coordination-exact-quint-0.32-typecheck" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="17-fail-closed-ir-mutations" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="mutation-profile-version" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="mutation-missing-profile-version" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="mutation-profile-identity" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="mutation-missing-source-binding" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="mutation-unknown-root-field" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="mutation-wrong-stage" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="mutation-compiler-warning" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="mutation-catalogue-opcode" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="mutation-expression-kind" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="mutation-property-kind" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="mutation-typedef-field" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="mutation-unsupported-choreo" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="mutation-empty-tables" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="mutation-type-effect-mismatch" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="mutation-catalogue-evidence" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="mutation-wrong-type-relation" />
  <testcase classname="SvgWorkspaceQuintQ2ExactIr" name="mutation-hidden-init-semantics" />
</testsuite>
EOF
cat >"${Q3_JUNIT_OUT:-$scratch/q3.xml}" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<testsuite name="FS.GG.SDD.SvgWorkspaceQuintQ3" tests="23" failures="0">
  <testcase classname="SvgWorkspaceQuintQ3" name="fresh-cache-install-before-isolation" />
  <testcase classname="SvgWorkspaceQuintQ3" name="installed-staged-provisioning" />
  <testcase classname="SvgWorkspaceQuintQ3" name="wrong-tool-refusal" />
  <testcase classname="SvgWorkspaceQuintQ3" name="wrong-profile-refusal" />
  <testcase classname="SvgWorkspaceQuintQ3" name="modified-cache-provision-refusal" />
  <testcase classname="SvgWorkspaceQuintQ3" name="modified-cache-author-refusal" />
  <testcase classname="SvgWorkspaceQuintQ3" name="duplicate-provisioning-idempotence" />
  <testcase classname="SvgWorkspaceQuintQ3" name="concurrent-provisioning" />
  <testcase classname="SvgWorkspaceQuintQ3" name="invalid-cache-path-refusal" />
  <testcase classname="SvgWorkspaceQuintQ3" name="cache-source-alias-idempotence" />
  <testcase classname="SvgWorkspaceQuintQ3" name="executable-mode-repair" />
  <testcase classname="SvgWorkspaceQuintQ3" name="accepted-sdd-profile2-authority-and-neutral-refusal" />
  <testcase classname="SvgWorkspaceQuintQ3" name="installed-generic-sir-compiler-and-binding-parity" />
  <testcase classname="SvgWorkspaceQuintQ3" name="author-crash-recovery-every-boundary" />
  <testcase classname="SvgWorkspaceQuintQ3" name="concurrent-inspect-transaction-lock" />
  <testcase classname="SvgWorkspaceQuintQ3" name="missing-cache-no-write" />
  <testcase classname="SvgWorkspaceQuintQ3" name="freshness-refusal" />
  <testcase classname="SvgWorkspaceQuintQ3" name="typed-effect-semantic-closure" />
  <testcase classname="SvgWorkspaceQuintQ3" name="profile1-retention" />
  <testcase classname="SvgWorkspaceQuintQ3" name="migration-preflight-no-write" />
  <testcase classname="SvgWorkspaceQuintQ3" name="accepted-bounded-migration" />
  <testcase classname="SvgWorkspaceQuintQ3" name="replacement-rollback-decision-lock" />
  <testcase classname="SvgWorkspaceQuintQ3" name="rollback-crash-recovery-and-byte-exact-restoration" />
</testsuite>
EOF

if [[ -n "${Q3_TOOLCHAIN_OUT:-}" ]]; then
  mkdir -p "$(dirname "$Q3_TOOLCHAIN_OUT")"
  printf '%s\n' '{"schema":"fsgg.svg-workspace.quint-toolchain/v1","profile":"fsgg-quint-profile/2","platform":"linux/amd64","quintSha256":"939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f","lmtSha256":"37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10"}' >"$Q3_TOOLCHAIN_OUT"
fi

printf 'SVG-WORKSPACE-QUINT-ACCEPTED: package=%s version=%s profile2=accepted-sync-parity generic=sir-compiler-parity migration=rollback-exact profile1=retained exact-ir=17-refusals\n' \
  "$package_origin" "$version"
