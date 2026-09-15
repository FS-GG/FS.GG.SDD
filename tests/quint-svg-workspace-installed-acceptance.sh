#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0

repo_root="$(git rev-parse --show-toplevel)"
: "${QUINT_BIN:?preseed exact Quint 0.32.0 binary in QUINT_BIN}"
: "${LMT_BIN:?preseed exact lmt binary in LMT_BIN}"
: "${FABLE_BIN:?preseed exact Fable 5.13.0 executable in FABLE_BIN}"

fail() { printf 'SVG-WORKSPACE-QUINT-REFUSAL: %s\n' "$*" >&2; exit 1; }
sha() { sha256sum "$1" | cut -d' ' -f1; }

[[ "$(sha "$QUINT_BIN")" == '939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f' ]] || fail 'wrong Quint object'
[[ "$(sha "$LMT_BIN")" == '37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10' ]] || fail 'wrong lmt object'
[[ "$(sha "$FABLE_BIN")" == '28f7d8bd23ca801cd3c3d86dbdd053fbdebeacb6f1c7ceffe903b3af69f75451' ]] || fail 'wrong Fable launcher'

version="$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' "$repo_root/Directory.Build.local.props" | head -1)"
[[ -n "$version" ]] || fail 'coherent package version is absent'

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
    Q3_PACKAGE_SOURCE="$package_source"
    Q2_JUNIT_OUT="${Q2_JUNIT_OUT:-}"
    Q2_EXACT_IR_JUNIT_OUT="${Q2_EXACT_IR_JUNIT_OUT:-}"
    Q3_JUNIT_OUT="${Q3_JUNIT_OUT:-}"
    Q3_TOOLCHAIN_OUT="${Q3_TOOLCHAIN_OUT:-}"
    SVG_WORKSPACE_PACKAGE_ORIGIN="$package_origin"
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
  "$QUINT_BIN" test workspace-lifecycle.qnt --main WorkspaceLifecycleTests --seed=927 >/dev/null
  "$QUINT_BIN" run workspace-lifecycle.qnt --main WorkspaceLifecycle --max-samples=100 --max-steps=6 --seed=927 \
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

for run in a b; do
  root="$scratch/author-$run"
  mkdir -p "$root/$fixture"
  cp "$repo_root/$fixture/scene.md" "$root/$fixture/scene.md"
  cp "$repo_root/$fixture/bindings.json" "$root/$fixture/bindings.json"
  "$cli" typed-sdd author --root "$root" --work scene --title 'Neutral retained reducer' \
    --agent acceptance --session exact --backend quint-specification-v1 --cache "$scratch/cache" \
    --profile fsgg-quint-profile/2 --source "$fixture/scene.md" --bindings "$fixture/bindings.json" \
    >"$scratch/author-$run.json"
  "$cli" typed-sdd inspect --root "$root" --work scene >"$scratch/inspect-$run.json"
  grep -F '"outcome": "succeeded"' "$scratch/author-$run.json" >/dev/null || fail "author $run failed"
  grep -F '"outcome": "succeeded"' "$scratch/inspect-$run.json" >/dev/null || fail "inspect $run failed"
done
diff -ru "$scratch/author-a" "$scratch/author-b" >/dev/null || fail 'isolated author roots differ'
cmp "$scratch/author-a.json" "$scratch/author-b.json" >/dev/null || fail 'isolated author reports differ'
grep -F 'ACT-Choose' "$scratch/author-a/readiness/scene/quint/bindings.fs" >/dev/null || fail 'action correspondence is absent'
grep -F 'sandbox-contract' "$scratch/author-a/readiness/scene/quint/contract.json" >/dev/null || fail 'contract digest closure is absent'

general_root="$scratch/general-authority"
mkdir -p "$general_root/$general_fixture"
cp "$repo_root/$general_fixture/model.md" "$general_root/$general_fixture/model.md"
cp "$repo_root/$general_fixture/bindings.json" "$general_root/$general_fixture/bindings.json"
"$cli" typed-sdd author --root "$general_root" --work cooperative --title 'Neutral cooperative authority' \
  --agent acceptance --session general --backend quint-specification-v1 --cache "$scratch/cache" \
  --profile fsgg-quint-profile/2 --source "$general_fixture/model.md" --bindings "$general_fixture/bindings.json" \
  >"$scratch/general-author.json"
"$cli" typed-sdd inspect --root "$general_root" --work cooperative >"$scratch/general-inspect.json"
general_contract="$general_root/readiness/cooperative/quint/contract.json"
for section in relationships verificationProfiles bounds impacts compatibility; do
  grep -F '"'"$section"'":[{' "$general_contract" >/dev/null || fail "neutral general authority lacks $section"
done
grep -F 'ACT-Advance' "$general_root/readiness/cooperative/quint/bindings.fs" >/dev/null || fail 'neutral general action binding is absent'

probe="$scratch/fable-probe"
mkdir -p "$probe"
cp "$general_root/readiness/cooperative/quint/bindings.fs" "$probe/Bindings.fs"
printf '%s\n' \
  '<Project Sdk="Microsoft.NET.Sdk">' \
  '  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>' \
  '  <ItemGroup><Compile Include="Bindings.fs" /><Compile Include="Program.fs" /></ItemGroup>' \
  '</Project>' >"$probe/Probe.fsproj"
printf '%s\n' \
  'open CooperativeTurnGenerated' \
  'printfn "%s" ContractFingerprint' \
  'printfn "%s" CanonicalContractJson' >"$probe/Program.fs"
dotnet run --project "$probe/Probe.fsproj" --no-restore >"$scratch/native.txt"
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
<testsuite name="FS.GG.SDD.SvgWorkspaceQuintQ3" tests="22" failures="0">
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
  <testcase classname="SvgWorkspaceQuintQ3" name="neutral-general-profile-authority" />
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

printf 'SVG-WORKSPACE-QUINT-ACCEPTED: package=%s version=%s profile2=author-inspect-parity migration=rollback-exact profile1=retained exact-ir=17-refusals\n' \
  "$package_origin" "$version"
