#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
: "${QUINT_BIN:?preseed exact Quint 0.32.0 binary in QUINT_BIN}"
: "${LMT_BIN:?preseed exact lmt binary in LMT_BIN}"
: "${FABLE_BIN:?preseed exact Fable 5.13.0 executable in FABLE_BIN}"

fail() { printf 'Q2-ACCEPTANCE-REFUSAL: %s\n' "$*" >&2; exit 1; }
sha() { sha256sum "$1" | cut -d' ' -f1; }

[[ -f "$QUINT_BIN" && -x "$QUINT_BIN" ]] || fail 'QUINT_BIN is not an executable local file'
[[ -f "$LMT_BIN" && -x "$LMT_BIN" ]] || fail 'LMT_BIN is not an executable local file'
[[ -f "$FABLE_BIN" && -x "$FABLE_BIN" ]] || fail 'FABLE_BIN is not an executable local file'
[[ "$(sha "$QUINT_BIN")" == '939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f' ]] \
  || fail 'Quint binary is not the Q1-qualified object'
[[ "$(sha "$LMT_BIN")" == '37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10' ]] \
  || fail 'lmt binary is not the Q1-qualified object'
[[ "$($QUINT_BIN --version)" == '0.32.0' ]] || fail 'Quint version drifted'
"$FABLE_BIN" --version 2>&1 | grep -F '5.13.0' >/dev/null || fail 'Fable version drifted'

scratch="$(mktemp -d /tmp/fsgg-quint-q2.XXXXXX)"
trap 'rm -rf -- "$scratch"' EXIT
feed="$scratch/feed"
consumer="$scratch/consumer"
mkdir -p "$feed"
cp -R "$repo_root/tests/fixtures/quint-compiler-consumer" "$consumer"

# Provision the complete package closure into a local feed. Release verification may select a
# public source; normal source-tree verification packs the candidate. The proof below always starts
# from a different, empty global-packages folder after network access is removed.
if [[ -z "${Q2_PACKAGE_SOURCE:-}" ]]; then
  dotnet pack "$repo_root/src/FS.GG.Contracts/FS.GG.Contracts.fsproj" -c Release -o "$feed" >/dev/null
  dotnet pack "$repo_root/src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj" -c Release -o "$feed" >/dev/null
  provisioning_config="$consumer/Provisioning.NuGet.Config"
else
  provisioning_config="$scratch/Public.NuGet.Config"
  printf '%s\n' \
    '<?xml version="1.0" encoding="utf-8"?>' \
    '<configuration><packageSources><clear /><add key="public" value="'"$Q2_PACKAGE_SOURCE"'" /></packageSources>' \
    '<packageSourceMapping><clear /><packageSource key="public"><package pattern="*" /></packageSource></packageSourceMapping></configuration>' \
    >"$provisioning_config"
fi

fable_probe="$scratch/fable-probe"
mkdir -p "$fable_probe"
printf '%s\n' \
  '<Project Sdk="Microsoft.NET.Sdk">' \
  '  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>' \
  '  <ItemGroup><Compile Include="Bindings.fs" /><Compile Include="Program.fs" /></ItemGroup>' \
  '</Project>' >"$fable_probe/FableProbe.fsproj"
printf '%s\n' 'module Placeholder' 'let Value = "prepared"' >"$fable_probe/Bindings.fs"
printf '%s\n' 'open Placeholder' 'printfn "%s" Value' >"$fable_probe/Program.fs"

provisioning_packages="$scratch/provisioning-packages"
NUGET_PACKAGES="$provisioning_packages" dotnet restore "$consumer/Consumer.fsproj" \
  --configfile "$provisioning_config" --no-http-cache >/dev/null
NUGET_PACKAGES="$provisioning_packages" dotnet restore "$fable_probe/FableProbe.fsproj" \
  --configfile "$provisioning_config" --no-http-cache >/dev/null
find "$provisioning_packages" -type f -name '*.nupkg' -exec cp -f '{}' "$feed/" \;

export HTTP_PROXY='http://127.0.0.1:1'
export HTTPS_PROXY='http://127.0.0.1:1'
export ALL_PROXY='http://127.0.0.1:1'
export NO_PROXY='127.0.0.1,localhost'
export QUINT_HOME="$scratch/quint-home"
export NUGET_PACKAGES="$scratch/packages"
export NUGET_HTTP_CACHE_PATH="$scratch/http-cache"
mkdir -p "$QUINT_HOME"

# This is the installed-package boundary: fresh cache, local preseed only, and network already
# unavailable. Both restores must resolve the complete closure without consulting nuget.org.
dotnet restore "$consumer/Consumer.fsproj" --configfile "$consumer/NuGet.Config" \
  --no-http-cache --force-evaluate >/dev/null
dotnet restore "$fable_probe/FableProbe.fsproj" --source "$feed" \
  --no-http-cache --force-evaluate >/dev/null

for run in a b; do
  mkdir -p "$scratch/$run"
  cp "$repo_root/docs/experiments/quint-q1/slices/"*.md "$scratch/$run/"
  cp "$repo_root/tests/fixtures/quint-general-sir/sir-combat.md" "$scratch/$run/"
  (cd "$scratch/$run" && "$LMT_BIN" requirements-and-evidence.md sir-damage-rule.md coordination-process.md sir-combat.md) \
    >"$scratch/$run/lmt.stdout" 2>"$scratch/$run/lmt.stderr"
  [[ ! -s "$scratch/$run/lmt.stdout" && ! -s "$scratch/$run/lmt.stderr" ]] \
    || fail "lmt emitted output in isolated run $run"

  for module in requirements.qnt sir-damage.qnt coordination.qnt sir-combat.qnt; do
    "$QUINT_BIN" typecheck --out="$scratch/$run/$module.typed.json" "$scratch/$run/$module"
  done
done

for artifact in requirements.qnt sir-damage.qnt coordination.qnt \
  sir-combat.qnt requirements.qnt.typed.json sir-damage.qnt.typed.json coordination.qnt.typed.json \
  sir-combat.qnt.typed.json; do
  cmp "$scratch/a/$artifact" "$scratch/b/$artifact" >/dev/null \
    || fail "isolated compilation is not byte-identical: $artifact"
done

artifact_assembly="$(find "$NUGET_PACKAGES/fs.gg.sdd.artifacts" -path '*/lib/net10.0/FS.GG.SDD.Artifacts.dll' -print -quit)"
[[ -n "$artifact_assembly" && -f "$artifact_assembly" ]] \
  || fail 'installed FS.GG.SDD.Artifacts assembly is absent from the fresh offline cache'

for run in a b; do
  dotnet fsi --reference:"$artifact_assembly" --exec \
    "$repo_root/tests/FS.GG.SDD.Artifacts.Tests/QuintGeneralSirAcceptance.fsx" \
    "$scratch/$run/sir-combat.qnt.typed.json" \
    "$repo_root/tests/fixtures/quint-general-sir/profile-bindings.json" \
    "$scratch/profile2-$run" >"$scratch/profile2-$run.log"
  grep -F 'PROFILE-2-SIR-ACCEPTED: rules=16 properties=7 actions=5' "$scratch/profile2-$run.log" >/dev/null \
    || fail "general S.I.R. profile did not accept isolated run $run"
done
diff -ru "$scratch/profile2-a" "$scratch/profile2-b" >/dev/null \
  || fail 'general S.I.R. contract and bindings drifted across isolated runs'

cp "$scratch/profile2-a/bindings.fable.fs" "$fable_probe/Bindings.fs"
printf '%s\n' \
  'open SirCombatGenerated' \
  'printfn "%s" ContractFingerprint' \
  'Catalogue |> List.filter (fun row -> row.ExportId = "EXPORT-Rules") |> List.map _.Id |> String.concat "," |> printfn "%s"' \
  'printfn "%s" CanonicalContractJson' >"$fable_probe/Program.fs"
"$FABLE_BIN" "$fable_probe/FableProbe.fsproj" --outDir "$scratch/fable-profile2" --noRestore --noCache --silent
node "$scratch/fable-profile2/Program.js" >"$scratch/fable-profile2.txt"
diff -u "$scratch/profile2-a/native.txt" "$scratch/fable-profile2.txt" >/dev/null \
  || fail 'general S.I.R. native and Fable bindings diverged'

dotnet fsi --reference:"$artifact_assembly" --exec \
  "$repo_root/tests/FS.GG.SDD.Artifacts.Tests/QuintExactIrAdapterTests.fsx" \
  "$scratch/a/requirements.qnt.typed.json" \
  "$scratch/a/sir-damage.qnt.typed.json" \
  "$scratch/a/coordination.qnt.typed.json" >"$scratch/exact-ir.log"
grep -F 'Exact Quint 0.32.0 Q1 IR corpus and 17 fail-closed mutations passed.' "$scratch/exact-ir.log" >/dev/null \
  || fail 'exact IR adapter did not complete its independent mutation corpus'

if [[ -n "${Q2_EXACT_IR_JUNIT_OUT:-}" ]]; then
  mkdir -p "$(dirname "$Q2_EXACT_IR_JUNIT_OUT")"
  printf '%s\n' \
    '<?xml version="1.0" encoding="utf-8"?>' \
    '<testsuite name="FS.GG.SDD.QuintQ2ExactIr" tests="20" failures="0">' \
    '  <testcase classname="QuintQ2ExactIr" name="requirements-exact-quint-0.32-ir" />' \
    '  <testcase classname="QuintQ2ExactIr" name="sir-exact-quint-0.32-ir" />' \
    '  <testcase classname="QuintQ2ExactIr" name="coordination-exact-quint-0.32-ir" />' \
    '  <testcase classname="QuintQ2ExactIr" name="mutation-profile-version" />' \
    '  <testcase classname="QuintQ2ExactIr" name="mutation-missing-profile-version" />' \
    '  <testcase classname="QuintQ2ExactIr" name="mutation-profile-identity" />' \
    '  <testcase classname="QuintQ2ExactIr" name="mutation-missing-source-binding" />' \
    '  <testcase classname="QuintQ2ExactIr" name="mutation-unknown-root-field" />' \
    '  <testcase classname="QuintQ2ExactIr" name="mutation-wrong-stage" />' \
    '  <testcase classname="QuintQ2ExactIr" name="mutation-compiler-warning" />' \
    '  <testcase classname="QuintQ2ExactIr" name="mutation-catalogue-opcode" />' \
    '  <testcase classname="QuintQ2ExactIr" name="mutation-expression-kind" />' \
    '  <testcase classname="QuintQ2ExactIr" name="mutation-property-kind" />' \
    '  <testcase classname="QuintQ2ExactIr" name="mutation-typedef-field" />' \
    '  <testcase classname="QuintQ2ExactIr" name="mutation-unsupported-choreo" />' \
    '  <testcase classname="QuintQ2ExactIr" name="mutation-empty-tables" />' \
    '  <testcase classname="QuintQ2ExactIr" name="mutation-type-effect-mismatch" />' \
    '  <testcase classname="QuintQ2ExactIr" name="mutation-catalogue-evidence" />' \
    '  <testcase classname="QuintQ2ExactIr" name="mutation-wrong-type-relation" />' \
    '  <testcase classname="QuintQ2ExactIr" name="mutation-hidden-init-semantics" />' \
    '</testsuite>' >"$Q2_EXACT_IR_JUNIT_OUT"
fi

declare -a rows=(
  'requirements-and-evidence.md requirements.qnt requirements'
  'sir-damage-rule.md sir-damage.qnt sir'
  'coordination-process.md coordination.qnt coordination'
)

for row in "${rows[@]}"; do
  read -r markdown module label <<<"$row"
  logical="docs/experiments/quint-q1/slices/$markdown"
  witness='-'
  if [[ "$label" == 'sir' ]]; then
    witness="$repo_root/tests/quint-q1/fixtures/sir-reviewed-witness.itf.json"
  fi
  out_a="$scratch/output-a/$label"
  out_b="$scratch/output-b/$label"
  dotnet run --project "$consumer/Consumer.fsproj" -c Release --no-restore -- \
    "$logical" "$scratch/a/$markdown" "$scratch/a/$module" "$scratch/a/$module.typed.json" "$witness" "$out_a"
  dotnet run --project "$consumer/Consumer.fsproj" -c Release --no-restore -- \
    "$logical" "$scratch/b/$markdown" "$scratch/b/$module" "$scratch/b/$module.typed.json" "$witness" "$out_b"

  if [[ "$label" == 'sir' ]]; then
    for replay in "$out_a/replay.txt" "$out_b/replay.txt"; do
      [[ -s "$replay" ]] || fail 'installed-package S.I.R. replay output is absent'
      grep -Fx 'positive=equivalent' "$replay" >/dev/null \
        || fail 'installed-package S.I.R. positive replay did not run'
      grep -E '^trace=[0-9a-f]{64}$' "$replay" >/dev/null \
        || fail 'installed-package S.I.R. trace identity is absent'
      grep -F 'divergence=2|ApplyDamage|docs/experiments/quint-q1/slices/sir-damage-rule.md:17:1|state' \
        "$replay" >/dev/null \
        || fail 'installed-package S.I.R. first-divergence control did not run'
      grep -E '^expected=[0-9a-f]{64}$' "$replay" >/dev/null \
        || fail 'installed-package S.I.R. expected-state identity is absent'
      grep -E '^actual=[0-9a-f]{64}$' "$replay" >/dev/null \
        || fail 'installed-package S.I.R. divergent-state identity is absent'
    done
  elif [[ -e "$out_a/replay.txt" || -e "$out_b/replay.txt" ]]; then
    fail "non-S.I.R. package compilation emitted replay output: $label"
  fi

  diff -ru "$out_a" "$out_b" >/dev/null || fail "package compiler output drifted across isolated runs: $label"

  cp "$out_a/bindings.fable.fs" "$fable_probe/Bindings.fs"
  module_name="$(sed -n 's/^module //p' "$fable_probe/Bindings.fs" | head -1)"
  printf '%s\n' \
    "open $module_name" \
    'printfn "%s" ContractFingerprint' \
    'Catalogue |> List.map _.Id |> String.concat "," |> printfn "%s"' \
    'printfn "%s" CanonicalContractJson' >"$fable_probe/Program.fs"
  fable_out="$scratch/fable-$label"
  "$FABLE_BIN" "$fable_probe/FableProbe.fsproj" --outDir "$fable_out" --noRestore --noCache --silent
  node "$fable_out/Program.js" >"$scratch/fable-$label.txt"
  diff -u "$out_a/native.txt" "$scratch/fable-$label.txt" >/dev/null \
    || fail ".NET and Fable canonical outputs diverged: $label"
done

cp "$scratch/output-a/requirements/bindings.fable.fs" "$fable_probe/Bindings.fs"
printf '%s\n' 'open RequirementsBindings' 'printfn "%s" DefinitelyMissingGeneratedIdentity' >"$fable_probe/Program.fs"
if "$FABLE_BIN" "$fable_probe/FableProbe.fsproj" --outDir "$scratch/fable-mutant" --noRestore --noCache --silent \
    >"$scratch/fable-mutant.log" 2>&1; then
  fail 'independent invalid generated-binding consumer unexpectedly compiled under Fable'
fi
grep -F 'DefinitelyMissingGeneratedIdentity' "$scratch/fable-mutant.log" >/dev/null \
  || fail 'Fable mutation failed without naming the independent missing binding'

if [[ -n "${Q2_JUNIT_OUT:-}" ]]; then
  mkdir -p "$(dirname "$Q2_JUNIT_OUT")"
  printf '%s\n' \
    '<?xml version="1.0" encoding="utf-8"?>' \
    '<testsuite name="FS.GG.SDD.QuintQ2CompilerAcceptance" tests="13" failures="0">' \
    '  <testcase classname="QuintQ2" name="preseeded-exact-tool-identity" />' \
    '  <testcase classname="QuintQ2" name="fresh-cache-offline-package-restore" />' \
    '  <testcase classname="QuintQ2" name="three-q1-slices" />' \
    '  <testcase classname="QuintQ2" name="two-isolated-extractions" />' \
    '  <testcase classname="QuintQ2" name="two-isolated-typechecks" />' \
    '  <testcase classname="QuintQ2" name="package-only-public-compiler" />' \
    '  <testcase classname="QuintQ2" name="installed-package-sir-replay" />' \
    '  <testcase classname="QuintQ2" name="canonical-receipt-parity" />' \
    '  <testcase classname="QuintQ2" name="fable-runtime-parity" />' \
    '  <testcase classname="QuintQ2" name="fable-independent-mutation" />' \
    '  <testcase classname="QuintQ2" name="general-sir-two-isolated-compilations" />' \
    '  <testcase classname="QuintQ2" name="general-sir-sixteen-rules-seven-properties-five-actions" />' \
    '  <testcase classname="QuintQ2" name="general-sir-native-fable-parity" />' \
    '</testsuite>' >"$Q2_JUNIT_OUT"
fi

printf 'Q2-COMPILER-ACCEPTED: offline package compiler/replay, 3 frozen slices + complete S.I.R. profile 2, 2 isolated runs, Fable parity, 17 IR and 1 Fable mutations\n'
