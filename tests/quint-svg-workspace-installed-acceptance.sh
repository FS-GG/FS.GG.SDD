#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0

repo_root="$(git rev-parse --show-toplevel)"
: "${QUINT_BIN:?preseed exact Quint 0.32.0 binary in QUINT_BIN}"
: "${LMT_BIN:?preseed exact lmt binary in LMT_BIN}"
: "${FABLE_BIN:?preseed exact Fable 5.13.0 executable in FABLE_BIN}"
: "${Q3_PACKAGE_SOURCE:?public package source is required}"

fail() { printf 'SVG-WORKSPACE-QUINT-REFUSAL: %s\n' "$*" >&2; exit 1; }
sha() { sha256sum "$1" | cut -d' ' -f1; }

[[ "$(sha "$QUINT_BIN")" == '939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f' ]] || fail 'wrong Quint object'
[[ "$(sha "$LMT_BIN")" == '37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10' ]] || fail 'wrong lmt object'
[[ "$(sha "$FABLE_BIN")" == '28f7d8bd23ca801cd3c3d86dbdd053fbdebeacb6f1c7ceffe903b3af69f75451' ]] || fail 'wrong Fable launcher'

scratch="$(mktemp -d /tmp/fsgg-svg-workspace-quint.XXXXXX)"
trap 'rm -rf -- "$scratch"' EXIT
version="$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' "$repo_root/Directory.Build.local.props" | head -1)"
[[ -n "$version" ]] || fail 'coherent package version is absent'

printf '%s\n' \
  '<?xml version="1.0" encoding="utf-8"?>' \
  '<configuration><packageSources><clear /><add key="public" value="'"$Q3_PACKAGE_SOURCE"'" /></packageSources></configuration>' \
  >"$scratch/NuGet.Config"
export NUGET_PACKAGES="$scratch/packages"
export NUGET_HTTP_CACHE_PATH="$scratch/http-cache"
dotnet tool install FS.GG.SDD.Cli --version "$version" --tool-path "$scratch/tool" \
  --configfile "$scratch/NuGet.Config" --no-cache >/dev/null
cli="$scratch/tool/fsgg-sdd"
[[ -x "$cli" ]] || fail 'public CLI was not installed'

export HTTP_PROXY='http://127.0.0.1:1'
export HTTPS_PROXY='http://127.0.0.1:1'
export ALL_PROXY='http://127.0.0.1:1'
export NO_PROXY='127.0.0.1,localhost'

profile1="$scratch/profile1"
"$cli" typed-sdd author --root "$profile1" --work legacy --title Legacy \
  --agent acceptance --session retained >"$scratch/profile1-author.json"
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

fixture='tests/fixtures/quint-retained-reducer'
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

probe="$scratch/fable-probe"
mkdir -p "$probe"
cp "$scratch/author-a/readiness/scene/quint/bindings.fs" "$probe/Bindings.fs"
printf '%s\n' \
  '<Project Sdk="Microsoft.NET.Sdk">' \
  '  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>' \
  '  <ItemGroup><Compile Include="Bindings.fs" /><Compile Include="Program.fs" /></ItemGroup>' \
  '</Project>' >"$probe/Probe.fsproj"
printf '%s\n' \
  'open RetainedSceneGenerated' \
  'printfn "%s" ContractFingerprint' \
  'printfn "%s" CanonicalContractJson' >"$probe/Program.fs"
dotnet restore "$probe/Probe.fsproj" --source "$Q3_PACKAGE_SOURCE" >/dev/null
dotnet run --project "$probe/Probe.fsproj" --no-restore >"$scratch/native.txt"
"$FABLE_BIN" "$probe/Probe.fsproj" --outDir "$scratch/fable" --noRestore --noCache --silent
node "$scratch/fable/Program.js" >"$scratch/fable.txt"
cmp "$scratch/native.txt" "$scratch/fable.txt" >/dev/null || fail '.NET/Fable projection parity failed'

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

"$cli" typed-sdd inspect --root "$profile1" --work legacy >"$scratch/profile1-after.json"
find "$profile1" -type f -print0 | sort -z | xargs -0 sha256sum >"$scratch/profile1.after"
cmp "$scratch/profile1.before" "$scratch/profile1.after" >/dev/null || fail 'profile-1 bytes changed'
cmp "$scratch/profile1-before.json" "$scratch/profile1-after.json" >/dev/null || fail 'profile-1 inspection changed'

migration="$scratch/migration"
mkdir -p "$migration"
"$cli" typed-sdd author --root "$migration" --work demo --title 'Neutral legacy authority' --agent acceptance --session v1 >/dev/null
find "$migration" -type f -print0 | sort -z | xargs -0 sha256sum >"$scratch/v1.before"
"$cli" typed-sdd migrate --root "$migration" --work demo --source work/demo/spec.md \
  --backend quint-specification-v1 --cache "$scratch/cache" --agent acceptance --session migration >"$scratch/migrate-preflight.json"
grep -F '"classification": "Migrated"' "$scratch/migrate-preflight.json" >/dev/null || fail 'migration preflight classification drifted'
find "$migration" -type f -print0 | sort -z | xargs -0 sha256sum >"$scratch/v1.preflight"
cmp "$scratch/v1.before" "$scratch/v1.preflight" >/dev/null || fail 'migration preflight wrote bytes'
"$cli" typed-sdd migrate --root "$migration" --work demo --source work/demo/spec.md --accept \
  --backend quint-specification-v1 --cache "$scratch/cache" --agent acceptance --session migration >"$scratch/migrate.json"
"$cli" typed-sdd inspect --root "$migration" --work demo >/dev/null || fail 'migrated authority did not inspect'
"$cli" typed-sdd rollback --root "$migration" --work demo --accept >"$scratch/rollback.json"
find "$migration" -type f -print0 | sort -z | xargs -0 sha256sum >"$scratch/v1.after"
cmp "$scratch/v1.before" "$scratch/v1.after" >/dev/null || fail 'rollback did not restore exact v1 bytes'

mkdir -p "$(dirname "${Q2_JUNIT_OUT:-$scratch/q2.xml}")" "$(dirname "${Q3_JUNIT_OUT:-$scratch/q3.xml}")"
cat >"${Q2_JUNIT_OUT:-$scratch/q2.xml}" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<testsuite name="FS.GG.SDD.SvgWorkspaceQuintQ2" tests="5" failures="0">
  <testcase classname="SvgWorkspaceQuintQ2" name="public-package-offline-install" />
  <testcase classname="SvgWorkspaceQuintQ2" name="exact-content-addressed-tools" />
  <testcase classname="SvgWorkspaceQuintQ2" name="profile2-author-inspect" />
  <testcase classname="SvgWorkspaceQuintQ2" name="deterministic-correspondence" />
  <testcase classname="SvgWorkspaceQuintQ2" name="dotnet-fable-parity" />
</testsuite>
EOF
cat >"${Q3_JUNIT_OUT:-$scratch/q3.xml}" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<testsuite name="FS.GG.SDD.SvgWorkspaceQuintQ3" tests="6" failures="0">
  <testcase classname="SvgWorkspaceQuintQ3" name="wrong-tool-refusal" />
  <testcase classname="SvgWorkspaceQuintQ3" name="wrong-profile-refusal" />
  <testcase classname="SvgWorkspaceQuintQ3" name="freshness-refusal" />
  <testcase classname="SvgWorkspaceQuintQ3" name="profile1-retention" />
  <testcase classname="SvgWorkspaceQuintQ3" name="migration-preflight-no-write" />
  <testcase classname="SvgWorkspaceQuintQ3" name="accepted-migration-byte-exact-rollback" />
</testsuite>
EOF

if [[ -n "${Q3_TOOLCHAIN_OUT:-}" ]]; then
  mkdir -p "$(dirname "$Q3_TOOLCHAIN_OUT")"
  printf '%s\n' '{"schema":"fsgg.svg-workspace.quint-toolchain/v1","profile":"fsgg-quint-profile/2","platform":"linux/amd64","quintSha256":"939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f","lmtSha256":"37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10"}' >"$Q3_TOOLCHAIN_OUT"
fi

printf 'SVG-WORKSPACE-QUINT-ACCEPTED: public=%s profile2=author-inspect-parity migration=rollback-exact profile1=retained\n' "$version"
