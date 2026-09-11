#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0

current_stage='bootstrap'
error_trap='status=$?; printf "Q3-ACCEPTANCE-ERROR: stage=%s line=%s status=%s command=%s\n" "$current_stage" "$LINENO" "$status" "$BASH_COMMAND" >&2'
trap "$error_trap" ERR

repo_root="$(git rev-parse --show-toplevel)"
: "${QUINT_BIN:?preseed exact Quint 0.32.0 binary in QUINT_BIN}"
: "${LMT_BIN:?preseed exact lmt binary in LMT_BIN}"
: "${FABLE_BIN:?preseed exact Fable 5.13.0 executable in FABLE_BIN}"

fail() { printf 'Q3-ACCEPTANCE-REFUSAL: %s\n' "$*" >&2; exit 1; }
sha() { sha256sum "$1" | cut -d' ' -f1; }

[[ "$(sha "$QUINT_BIN")" == '939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f' ]] || fail 'wrong Quint cache object'
[[ "$(sha "$LMT_BIN")" == '37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10' ]] || fail 'wrong lmt cache object'
[[ "$(sha "$FABLE_BIN")" == '28f7d8bd23ca801cd3c3d86dbdd053fbdebeacb6f1c7ceffe903b3af69f75451' ]] || fail 'wrong Fable 5.13.0 launcher'
NO_COLOR=1 "$FABLE_BIN" --version 2>&1 | grep -F '5.13.0' >/dev/null || fail 'wrong Fable executable version'

# Q3 inherits, rather than approximates, Q2's installed compiler, reviewed replay, and
# real Fable/Node parity proof.
if [[ "${Q3_SKIP_Q2:-0}" != '1' ]]; then
  bash "$repo_root/tests/quint-q2-compiler-acceptance.sh" >/dev/null
else
  : "${Q2_JUNIT_IN:?Q2_JUNIT_IN is required when Q3_SKIP_Q2=1}"
  [[ -s "$Q2_JUNIT_IN" ]] || fail 'Q2 JUnit evidence is missing when Q2 execution is delegated'
  grep -F 'failures="0"' "$Q2_JUNIT_IN" >/dev/null || fail 'Q2 JUnit evidence is not green'
  grep -F 'installed-package-sir-replay' "$Q2_JUNIT_IN" >/dev/null || fail 'Q2 JUnit lacks installed replay evidence'
  grep -F 'fable-runtime-parity' "$Q2_JUNIT_IN" >/dev/null || fail 'Q2 JUnit lacks Fable/Node parity evidence'
fi

scratch="$(mktemp -d /tmp/fsgg-quint-q3.XXXXXX)"
trap 'rm -rf -- "$scratch"' EXIT
version="$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' "$repo_root/Directory.Build.local.props" | head -1)"
[[ -n "$version" ]] || fail 'could not resolve coherent package version'

if [[ -n "${Q3_PACKAGE_SOURCE:-}" ]]; then
  feed="$Q3_PACKAGE_SOURCE"
else
  current_stage='pack-local-feed'
  feed="$scratch/feed"
  mkdir -p "$feed"
  for project in FS.GG.Contracts FS.GG.SDD.Artifacts FS.GG.SDD.Commands FS.GG.SDD.Validation FS.GG.SDD.Cli; do
    dotnet pack "$repo_root/src/$project/$project.fsproj" -c Release -o "$feed" >/dev/null
  done

  # Materialize external dependency nupkgs while provisioning is still allowed, then
  # switch to one local source before installing the reviewed tool package.
  provisioning_packages="$scratch/provisioning-packages"
  NUGET_PACKAGES="$provisioning_packages" dotnet restore "$repo_root/src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj" --no-http-cache >/dev/null
  find "$provisioning_packages" -type f -name '*.nupkg' -exec cp -f '{}' "$feed/" \;
fi

printf '%s\n' \
  '<?xml version="1.0" encoding="utf-8"?>' \
  '<configuration><packageSources><clear /><add key="local" value="'"$feed"'" /></packageSources></configuration>' \
  >"$scratch/NuGet.Config"

export NUGET_PACKAGES="$scratch/fresh-packages"
export NUGET_HTTP_CACHE_PATH="$scratch/fresh-http"

poison_network() {
  export HTTP_PROXY='http://127.0.0.1:1'
  export HTTPS_PROXY='http://127.0.0.1:1'
  export ALL_PROXY='http://127.0.0.1:1'
  export NO_PROXY='127.0.0.1,localhost'
}

[[ -z "${Q3_PACKAGE_SOURCE:-}" ]] && poison_network

dotnet tool install FS.GG.SDD.Cli --version "$version" --tool-path "$scratch/tool" \
  --configfile "$scratch/NuGet.Config" --no-cache >/dev/null
[[ -n "${Q3_PACKAGE_SOURCE:-}" ]] && poison_network
cli="$scratch/tool/fsgg-sdd"
[[ -x "$cli" ]] || fail 'installed CLI executable is absent'

current_stage='installed-provisioning'
profile1="$scratch/profile1-retained"
"$cli" typed-sdd author --root "$profile1" --work legacy --title Legacy \
  --agent acceptance --session profile1 >"$scratch/profile1-author.json"
"$cli" typed-sdd inspect --root "$profile1" --work legacy >"$scratch/profile1-before.json"
find "$profile1" -type f -print0 | sort -z | xargs -0 sha256sum >"$scratch/profile1.before"

printf 'wrong object\n' >"$scratch/wrong-quint"
if "$cli" typed-sdd provision --cache "$scratch/failed-cache" \
  --quint "$scratch/wrong-quint" --lmt "$LMT_BIN" >"$scratch/provision-wrong-object.json"; then
  fail 'wrong provisioning object unexpectedly succeeded'
fi
grep -F 'typedSdd.provision.objectMismatch' "$scratch/provision-wrong-object.json" >/dev/null \
  || fail 'wrong provisioning object diagnostic drifted'
[[ ! -d "$scratch/failed-cache/objects" ]] || fail 'failed provisioning retained an accepted object directory'

if "$cli" typed-sdd provision --cache "$scratch/wrong-profile-cache" \
  --profile fsgg-quint-profile/1 --quint "$QUINT_BIN" --lmt "$LMT_BIN" \
  >"$scratch/provision-wrong-profile.json"; then
  fail 'wrong-profile provisioning unexpectedly succeeded'
fi
grep -F 'typedSdd.provision.profileUnsupported' "$scratch/provision-wrong-profile.json" >/dev/null \
  || fail 'wrong-profile provisioning diagnostic drifted'
[[ ! -d "$scratch/wrong-profile-cache/objects" ]] || fail 'wrong-profile provisioning wrote cache objects'

"$cli" typed-sdd provision --cache "$scratch/cache" --quint "$QUINT_BIN" --lmt "$LMT_BIN" \
  >"$scratch/provision.json"
grep -F '"schema": "fsgg.typed-sdd.provision-report/v1"' "$scratch/provision.json" >/dev/null \
  || fail 'installed provision report schema is absent'
grep -F '"package": "FS.GG.SDD.Cli/' "$scratch/provision.json" >/dev/null \
  || fail 'installed provision report lacks package identity'
grep -F '"platform": "linux/amd64"' "$scratch/provision.json" >/dev/null \
  || fail 'installed provision report lacks platform identity'
grep -F '"profile": "fsgg-quint-profile/2"' "$scratch/provision.json" >/dev/null \
  || fail 'installed provision report lacks profile identity'
grep -F '"origin": "github:informalsystems/quint@v0.32.0"' "$scratch/provision.json" >/dev/null \
  || fail 'installed provision report lacks Quint provenance'
grep -F '"origin": "github:driusan/lmt@62fe18f2f6a6e11c158ff2b2209e1082a4fcd59c"' "$scratch/provision.json" >/dev/null \
  || fail 'installed provision report lacks lmt provenance'
cat >"$scratch/toolchain-evidence.json" <<EOF
{"schema":"fsgg.svg-qual.toolchain-evidence/v1","sddPackage":"FS.GG.SDD.Cli/$version","platform":"linux/amd64","profile":"fsgg-quint-profile/2","provisionOperation":"typed-sdd provision","provisionReportSchema":"fsgg.typed-sdd.provision-report/v1","cacheLayout":"objects/<sha256>","quint":{"version":"0.32.0","sha256":"939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f"},"lmt":{"source":"github:driusan/lmt@62fe18f2f6a6e11c158ff2b2209e1082a4fcd59c","sourceSha256":"88bc47acae2c26919ab96a5cafa80b12fac762092c57840a2baad1afcc7feda3","goVersion":"1.24.1","goArchiveSha256":"cb2396bae64183cdccf81a9a6df0aea3bce9511fc21469fb89a0c00470088073","cgoEnabled":"1","sha256":"37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10"},"fable":{"version":"5.13.0","launcherSha256":"28f7d8bd23ca801cd3c3d86dbdd053fbdebeacb6f1c7ceffe903b3af69f75451"}}
EOF

"$cli" typed-sdd provision --cache "$scratch/cache" --quint "$QUINT_BIN" --lmt "$LMT_BIN" \
  >"$scratch/provision-repeat.json"
cmp "$scratch/provision.json" "$scratch/provision-repeat.json" >/dev/null \
  || fail 'repeat provisioning report is not deterministic'

"$cli" typed-sdd provision --cache "$scratch/concurrent-cache" --quint "$QUINT_BIN" --lmt "$LMT_BIN" \
  >"$scratch/provision-concurrent-a.json" &
provision_a=$!
"$cli" typed-sdd provision --cache "$scratch/concurrent-cache" --quint "$QUINT_BIN" --lmt "$LMT_BIN" \
  >"$scratch/provision-concurrent-b.json" &
provision_b=$!
wait "$provision_a"
wait "$provision_b"
cmp "$scratch/provision-concurrent-a.json" "$scratch/provision-concurrent-b.json" >/dev/null \
  || fail 'concurrent duplicate provisioning did not converge'

if "$cli" typed-sdd provision --cache '' --quint "$QUINT_BIN" --lmt "$LMT_BIN" \
  >"$scratch/provision-invalid-cache.json"; then
  fail 'invalid cache path unexpectedly provisioned'
fi
grep -F 'typedSdd.provision.cachePathInvalid' "$scratch/provision-invalid-cache.json" >/dev/null \
  || fail 'invalid cache path diagnostic drifted'

cache="$scratch/cache/objects"
[[ "$(sha "$cache/939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f")" == \
  '939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f' ]] \
  || fail 'provisioned Quint object drifted'
[[ "$(sha "$cache/37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10")" == \
  '37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10' ]] \
  || fail 'provisioned lmt object drifted'
"$cli" typed-sdd provision --cache "$scratch/cache" \
  --quint "$cache/939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f" \
  --lmt "$cache/37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10" \
  >"$scratch/provision-source-alias.json" \
  || fail 'exact cache/source alias did not remain idempotent'

"$cli" typed-sdd inspect --root "$profile1" --work legacy >"$scratch/profile1-after.json"
find "$profile1" -type f -print0 | sort -z | xargs -0 sha256sum >"$scratch/profile1.after"
cmp "$scratch/profile1.before" "$scratch/profile1.after" >/dev/null \
  || fail 'profile-1 workspace changed during profile-2 provisioning'
cmp "$scratch/profile1-before.json" "$scratch/profile1-after.json" >/dev/null \
  || fail 'profile-1 inspect changed after profile-2 provisioning'

cp -a "$scratch/cache" "$scratch/modified-cache"
printf 'modified\n' >>"$scratch/modified-cache/objects/37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10"
if "$cli" typed-sdd provision --cache "$scratch/modified-cache" --quint "$QUINT_BIN" --lmt "$LMT_BIN" \
  >"$scratch/provision-cache-conflict.json"; then
  fail 'conflicting content-addressed cache object unexpectedly provisioned'
fi
grep -F 'typedSdd.provision.cacheConflict' "$scratch/provision-cache-conflict.json" >/dev/null \
  || fail 'cache conflict diagnostic drifted'
if "$cli" typed-sdd author --root "$scratch/modified-cache-root" --work demo --agent acceptance \
  --session modified --backend quint-specification-v1 --cache "$scratch/modified-cache" \
  >"$scratch/modified-cache.json"; then
  fail 'modified cache object unexpectedly authored an authority'
fi
grep -F 'typedSdd.v2.cacheInvalid' "$scratch/modified-cache.json" >/dev/null \
  || fail 'modified cache diagnostic drifted'
[[ -z "$(find "$scratch/modified-cache-root" -type f ! -path '*/typed-sdd-transactions/authority.lock' -print -quit 2>/dev/null)" ]] \
  || fail 'modified-cache refusal wrote authority files'

chmod -x "$cache/37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10"
"$cli" typed-sdd provision --cache "$scratch/cache" --quint "$QUINT_BIN" --lmt "$LMT_BIN" >/dev/null
[[ -x "$cache/37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10" ]] \
  || fail 'repeat provisioning did not repair exact object executable mode'

current_stage='small-neutral-profile2-authority'
small_root="$scratch/small-profile2"
small_fixture='tests/fixtures/quint-retained-reducer'
mkdir -p "$small_root/$small_fixture"
cp "$repo_root/$small_fixture/scene.md" "$small_root/$small_fixture/scene.md"
cp "$repo_root/$small_fixture/bindings.json" "$small_root/$small_fixture/bindings.json"
"$cli" typed-sdd author --root "$small_root" --work scene --title 'Tooling qualification reducer' \
  --agent acceptance --session small --backend quint-specification-v1 --cache "$scratch/cache" \
  --profile fsgg-quint-profile/2 --source "$small_fixture/scene.md" \
  --bindings "$small_fixture/bindings.json" >"$scratch/small-author.json"
grep -F '"outcome": "succeeded"' "$scratch/small-author.json" >/dev/null \
  || fail 'installed small profile-2 author failed'
"$cli" typed-sdd inspect --root "$small_root" --work scene >"$scratch/small-inspect.json"
grep -F '"outcome": "succeeded"' "$scratch/small-inspect.json" >/dev/null \
  || fail 'installed small profile-2 authority did not inspect'
grep -F 'RetainedSceneGenerated' "$small_root/readiness/scene/quint/bindings.fs" >/dev/null \
  || fail 'small profile-2 binding module was not generated'
grep -F 'ACT-Choose' "$small_root/readiness/scene/quint/bindings.fs" >/dev/null \
  || fail 'small profile-2 reducer action was not retained'

for run in a b; do
  current_stage="deterministic-author-$run"
  root="$scratch/author-$run"
  mkdir -p "$root"
  if ! "$cli" typed-sdd author --root "$root" --work demo --title Demo --agent acceptance --session exact \
    --backend quint-specification-v1 --cache "$scratch/cache" >"$scratch/author-$run.json"; then
    cat "$scratch/author-$run.json" >&2
    fail "installed author $run failed"
  fi
  grep -F '"outcome": "succeeded"' "$scratch/author-$run.json" >/dev/null || fail "installed author $run failed"
  "$cli" typed-sdd inspect --root "$root" --work demo >"$scratch/inspect-$run.json"
  grep -F '"outcome": "succeeded"' "$scratch/inspect-$run.json" >/dev/null || fail "installed inspect $run failed"
done
diff -ru "$scratch/author-a" "$scratch/author-b" >/dev/null || fail 'two installed author roots differ'
cmp "$scratch/author-a.json" "$scratch/author-b.json" >/dev/null || fail 'two installed author reports differ'

# Profile 2 proves that an installed consumer can supply a complete, literate Quint model rather
# than selecting a package-known whole-program digest. The selector document identifies only the
# declarations and source ranges to retain; all semantic values come from Quint's typed output.
current_stage='general-profile-installed-authority'
general_root="$scratch/general-profile"
general_fixture='tests/fixtures/quint-general-sir'
mkdir -p "$general_root/$general_fixture"
cp "$repo_root/$general_fixture/sir-combat.md" "$general_root/$general_fixture/sir-combat.md"
cp "$repo_root/$general_fixture/profile-bindings.json" "$general_root/$general_fixture/profile-bindings.json"
"$cli" typed-sdd author --root "$general_root" --work sir --title 'S.I.R. combat registry' \
  --agent acceptance --session general --backend quint-specification-v1 --cache "$scratch/cache" \
  --profile fsgg-quint-profile/2 --source "$general_fixture/sir-combat.md" \
  --bindings "$general_fixture/profile-bindings.json" >"$scratch/general-author.json"
grep -F '"outcome": "succeeded"' "$scratch/general-author.json" >/dev/null \
  || fail 'installed profile-2 author failed'
"$cli" typed-sdd inspect --root "$general_root" --work sir >"$scratch/general-inspect.json"
grep -F '"outcome": "succeeded"' "$scratch/general-inspect.json" >/dev/null \
  || fail 'installed profile-2 authority did not inspect'
grep -F 'fsgg-quint-profile/2' "$general_root/readiness/sir/typed-authority.json" >/dev/null \
  || fail 'profile-2 authority identity is absent'
grep -F 'COMBAT-ATTACK-RESOLUTION-001' "$general_root/readiness/sir/quint/contract.json" >/dev/null \
  || fail 'complete S.I.R. rule catalogue was not retained'
grep -F '"relationships":[{' "$general_root/readiness/sir/quint/contract.json" >/dev/null \
  || fail 'Quint relationship declarations were not projected'
grep -F '"verificationProfiles":[{' "$general_root/readiness/sir/quint/contract.json" >/dev/null \
  || fail 'Quint verification declarations were not projected'
grep -F '"bounds":[{' "$general_root/readiness/sir/quint/contract.json" >/dev/null \
  || fail 'Quint finite bounds were not projected'
grep -F '"impacts":[{' "$general_root/readiness/sir/quint/contract.json" >/dev/null \
  || fail 'Quint impact declarations were not projected'
grep -F '"compatibility":[{' "$general_root/readiness/sir/quint/contract.json" >/dev/null \
  || fail 'Quint compatibility declarations were not projected'
grep -F 'ACT-Consequences' "$general_root/readiness/sir/quint/bindings.fs" >/dev/null \
  || fail 'profile-2 action binding was not generated'

# Hard process death at every live-author move must recover before another operation reads authority.
for boundary in $(seq 1 10); do
  current_stage="author-crash-boundary-$boundary"
  crash_root="$scratch/crash-author-$boundary"
  mkdir -p "$crash_root"
  if FSGG_TYPED_SDD_TEST_CRASH_AFTER_MOVE="$boundary" "$cli" typed-sdd author \
    --root "$crash_root" --work demo --title Demo --agent acceptance --session "crash-$boundary" \
    --backend quint-specification-v1 --cache "$scratch/cache" >/dev/null 2>&1; then
    fail "injected author crash boundary $boundary unexpectedly succeeded"
  fi
  if "$cli" typed-sdd inspect --root "$crash_root" --work demo >"$scratch/crash-inspect-$boundary.json"; then
    fail "recovered empty pre-state at boundary $boundary exposed an authority"
  fi
  grep -F 'typedSdd.authorityMissing' "$scratch/crash-inspect-$boundary.json" >/dev/null \
    || fail "boundary $boundary did not recover before inspect"
  [[ -z "$(find "$crash_root" -type f ! -path '*/typed-sdd-transactions/authority.lock' -print -quit)" ]] \
    || fail "boundary $boundary left partial authority bytes"
done

# Inspect shares the transaction lock and cannot observe a prepared commit.
concurrent_root="$scratch/concurrent-author"
current_stage='concurrent-inspect-lock'
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
grep -F '"outcome": "succeeded"' "$scratch/concurrent-inspect.json" >/dev/null \
  || fail 'concurrent inspect observed an incomplete authority'

mkdir -p "$scratch/missing-cache-root"
current_stage='negative-integrity-cases'
if "$cli" typed-sdd author --root "$scratch/missing-cache-root" --work demo --agent acceptance --session missing \
  --backend quint-specification-v1 --cache "$scratch/missing-cache" >"$scratch/missing-cache.json"; then
  fail 'missing cache unexpectedly authored an authority'
fi
grep -F 'typedSdd.v2.cacheMissing' "$scratch/missing-cache.json" >/dev/null || fail 'missing cache diagnostic drifted'
[[ -z "$(find "$scratch/missing-cache-root" -mindepth 1 -print -quit)" ]] || fail 'missing-cache refusal wrote files'

printf 'edited\n' >"$scratch/author-a/readiness/demo/quint/contract.json"
if "$cli" typed-sdd inspect --root "$scratch/author-a" --work demo >"$scratch/edited.json"; then
  fail 'edited contract unexpectedly inspected green'
fi
grep -F 'typedSdd.v2.artifactMismatch' "$scratch/edited.json" >/dev/null || fail 'edited contract diagnostic drifted'

typed_effect="$scratch/author-b/readiness/demo/quint/typed-effect.json"
typed_manifest="$scratch/author-b/readiness/demo/typed-authority.json"
old_typed_sha="$(sha "$typed_effect")"
printf '{"forged":true}\n' >"$typed_effect"
new_typed_sha="$(sha "$typed_effect")"
sed -i "s/$old_typed_sha/$new_typed_sha/" "$typed_manifest"
if "$cli" typed-sdd inspect --root "$scratch/author-b" --work demo >"$scratch/forged-typed-effect.json"; then
  fail 'forged typed/effect observation unexpectedly inspected green'
fi
grep -F 'typedSdd.v2.typedEffectClosure' "$scratch/forged-typed-effect.json" >/dev/null \
  || fail 'typed/effect semantic adapter closure was not enforced'

migration="$scratch/migration"
current_stage='v1-migration'
mkdir -p "$migration"
"$cli" typed-sdd author --root "$migration" --work demo --title 'Unrelated legacy identifiers' \
  --agent acceptance --session v1 >/dev/null
find "$migration" -type f -print0 | sort -z | xargs -0 sha256sum >"$scratch/v1.before"
"$cli" typed-sdd migrate --root "$migration" --work demo --source work/demo/spec.md \
  --backend quint-specification-v1 --cache "$scratch/cache" --agent acceptance --session migration \
  >"$scratch/migrate-preflight.json"
grep -F '"classification": "Migrated"' "$scratch/migrate-preflight.json" >/dev/null || fail 'v1-to-v2 preflight did not classify Migrated'
find "$migration" -type f -print0 | sort -z | xargs -0 sha256sum >"$scratch/v1.preflight"
cmp "$scratch/v1.before" "$scratch/v1.preflight" >/dev/null || fail 'migration preflight wrote bytes'
"$cli" typed-sdd migrate --root "$migration" --work demo --source work/demo/spec.md --accept \
  --backend quint-specification-v1 --cache "$scratch/cache" --agent acceptance --session migration \
  >"$scratch/migrate.json"
grep -F '"outcome": "succeeded"' "$scratch/migrate.json" >/dev/null || fail 'installed v1-to-v2 migration failed'
grep -F 'semantic payload sha256:' "$scratch/migrate.json" >/dev/null || fail 'migration did not bind the exact v1 semantic payload'
preflight_payload="$(grep -F 'semantic payload sha256:' "$scratch/migrate-preflight.json" | sed 's/.*semantic payload sha256: \([0-9a-f]*\).*/\1/')"
accepted_payload="$(grep -F 'semantic payload sha256:' "$scratch/migrate.json" | sed 's/.*semantic payload sha256: \([0-9a-f]*\).*/\1/')"
[[ -n "$preflight_payload" && "$preflight_payload" == "$accepted_payload" ]] || fail 'accepted migration did not commit the preflight semantic proposal'
grep -F 'requirements-extension-v1' "$migration/readiness/demo/quint/contract.json" >/dev/null || fail 'compiled contract lacks v1 correspondence digest'
for semantic_id in SPEC-001 SB-001 US-001 FR-001 AC-001 EV001 Evaluate-AC-001; do
  grep -F "\"$semantic_id\"" "$migration/readiness/demo/quint/contract.json" >/dev/null \
    || fail "compiled contract failed to lower v1 semantic identity $semantic_id"
done
grep -F 'fsgg.requirements-extension/v1+base64' "$migration/work/demo/specification.md" >/dev/null || fail 'literate authority lacks retained v1 semantic payload'
"$cli" typed-sdd inspect --root "$migration" --work demo >/dev/null || fail 'migrated authority did not inspect'

# An accepted replacement and accepted rollback share one decision-to-commit lock. They cannot both
# commit from the same observed v2 authority or resurrect a stale replacement after rollback.
race_root="$scratch/replacement-rollback-race"
current_stage='replacement-rollback-race'
cp -a "$migration" "$race_root"
FSGG_TYPED_SDD_TEST_PAUSE_AFTER_PREPARE_MS=1000 "$cli" typed-sdd author \
  --root "$race_root" --work demo --title 'Concurrent replacement' --agent acceptance --session replacement \
  --backend quint-specification-v1 --cache "$scratch/cache" --accept >"$scratch/race-author.json" &
race_author_pid=$!
for _ in $(seq 1 100); do
  [[ -n "$(find "$race_root/.fsgg/typed-sdd-transactions" -name journal.json -print -quit 2>/dev/null)" ]] && break
  sleep 0.02
done
trap - ERR
set +e
"$cli" typed-sdd rollback --root "$race_root" --work demo --accept >"$scratch/race-rollback.json"
race_rollback_status=$?
wait "$race_author_pid"
race_author_status=$?
set -e
trap "$error_trap" ERR
[[ $race_author_status -eq 0 && $race_rollback_status -ne 0 ]] \
  || fail 'concurrent replacement and rollback did not serialize to exactly one accepted commit'
grep -F 'typedSdd.v2.rollbackMissing' "$scratch/race-rollback.json" >/dev/null \
  || fail 'serialized rollback did not diagnose the replacement-cleared rollback receipt'
"$cli" typed-sdd inspect --root "$race_root" --work demo >/dev/null \
  || fail 'serialized replacement/rollback race left invalid authority'
current_stage='rollback-crash-recovery'
trap - ERR
set +e
FSGG_TYPED_SDD_TEST_CRASH_AFTER_MOVE=5 "$cli" typed-sdd rollback --root "$migration" --work demo --accept >/dev/null 2>&1
rollback_crash=$?
set -e
trap "$error_trap" ERR
[[ $rollback_crash -ne 0 ]] || fail 'injected rollback crash unexpectedly succeeded'
"$cli" typed-sdd inspect --root "$migration" --work demo >/dev/null || fail 'rollback crash did not recover the complete v2 pre-state'
"$cli" typed-sdd rollback --root "$migration" --work demo --accept >"$scratch/rollback.json"
grep -F '"outcome": "succeeded"' "$scratch/rollback.json" >/dev/null || fail 'installed rollback failed'
find "$migration" -type f -print0 | sort -z | xargs -0 sha256sum >"$scratch/v1.after"
cmp "$scratch/v1.before" "$scratch/v1.after" >/dev/null || fail 'rollback did not restore the exact v1 tree'

if [[ -n "${Q3_JUNIT_OUT:-}" ]]; then
  current_stage='junit-report'
  mkdir -p "$(dirname "$Q3_JUNIT_OUT")"
  printf '%s\n' \
    '<?xml version="1.0" encoding="utf-8"?>' \
    '<testsuite name="FS.GG.SDD.QuintQ3TypedSddAcceptance" tests="22" failures="0">' \
    '  <testcase classname="QuintQ3" name="fresh-cache-offline-tool-install" />' \
    '  <testcase classname="QuintQ3" name="exact-content-addressed-tools" />' \
    '  <testcase classname="QuintQ3" name="installed-staged-provisioning" />' \
    '  <testcase classname="QuintQ3" name="provisioning-object-and-profile-refusal" />' \
    '  <testcase classname="QuintQ3" name="modified-cache-object-refusal" />' \
    '  <testcase classname="QuintQ3" name="profile1-remains-byte-identical" />' \
    '  <testcase classname="QuintQ3" name="duplicate-and-concurrent-provisioning" />' \
    '  <testcase classname="QuintQ3" name="invalid-cache-path-refusal" />' \
    '  <testcase classname="QuintQ3" name="cache-source-alias-idempotence" />' \
    '  <testcase classname="QuintQ3" name="small-neutral-profile2-author-inspect" />' \
    '  <testcase classname="QuintQ3" name="two-isolated-author-runs" />' \
    '  <testcase classname="QuintQ3" name="installed-general-profile-authority" />' \
    '  <testcase classname="QuintQ3" name="crash-recovery-every-author-boundary" />' \
    '  <testcase classname="QuintQ3" name="concurrent-inspect-transaction-lock" />' \
    '  <testcase classname="QuintQ3" name="manifest-v2-inspect" />' \
    '  <testcase classname="QuintQ3" name="missing-cache-no-write" />' \
    '  <testcase classname="QuintQ3" name="edited-artifact-red" />' \
    '  <testcase classname="QuintQ3" name="typed-effect-semantic-closure" />' \
    '  <testcase classname="QuintQ3" name="v1-preflight-and-migration" />' \
    '  <testcase classname="QuintQ3" name="replacement-rollback-decision-lock" />' \
    '  <testcase classname="QuintQ3" name="authenticated-byte-exact-rollback" />' \
    '  <testcase classname="QuintQ3" name="rollback-crash-recovery" />' \
    '</testsuite>' >"$Q3_JUNIT_OUT"
fi

if [[ -n "${Q3_TOOLCHAIN_OUT:-}" ]]; then
  mkdir -p "$(dirname "$Q3_TOOLCHAIN_OUT")"
  cp "$scratch/toolchain-evidence.json" "$Q3_TOOLCHAIN_OUT"
fi

printf 'Q3-TYPED-SDD-ACCEPTED: offline installed lifecycle, exact tools, deterministic v2, migration and rollback; Q2 replay/parity evidence verified when delegated\n'
