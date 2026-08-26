#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0

repo_root="$(git rev-parse --show-toplevel)"
: "${QUINT_BIN:?preseed exact Quint 0.32.0 binary in QUINT_BIN}"
: "${LMT_BIN:?preseed exact lmt binary in LMT_BIN}"
: "${FABLE_BIN:?preseed exact Fable 5.13.0 executable in FABLE_BIN}"

fail() { printf 'Q3-ACCEPTANCE-REFUSAL: %s\n' "$*" >&2; exit 1; }
sha() { sha256sum "$1" | cut -d' ' -f1; }

[[ "$(sha "$QUINT_BIN")" == '939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f' ]] || fail 'wrong Quint cache object'
[[ "$(sha "$LMT_BIN")" == '37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10' ]] || fail 'wrong lmt cache object'

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
feed="$scratch/feed"
mkdir -p "$feed"

for project in FS.GG.Contracts FS.GG.SDD.Artifacts FS.GG.SDD.Commands FS.GG.SDD.Validation FS.GG.SDD.Cli; do
  dotnet pack "$repo_root/src/$project/$project.fsproj" -c Release -o "$feed" >/dev/null
done

version="$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' "$repo_root/Directory.Build.local.props" | head -1)"
[[ -n "$version" ]] || fail 'could not resolve coherent package version'

# Materialize external dependency nupkgs while provisioning is still allowed, then
# switch to one source and fresh caches before installing the reviewed tool package.
provisioning_packages="$scratch/provisioning-packages"
NUGET_PACKAGES="$provisioning_packages" dotnet restore "$repo_root/src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj" --no-http-cache >/dev/null
find "$provisioning_packages" -type f -name '*.nupkg' -exec cp -f '{}' "$feed/" \;

printf '%s\n' \
  '<?xml version="1.0" encoding="utf-8"?>' \
  '<configuration><packageSources><clear /><add key="local" value="'"$feed"'" /></packageSources></configuration>' \
  >"$scratch/NuGet.Config"

export HTTP_PROXY='http://127.0.0.1:1'
export HTTPS_PROXY='http://127.0.0.1:1'
export ALL_PROXY='http://127.0.0.1:1'
export NO_PROXY='127.0.0.1,localhost'
export NUGET_PACKAGES="$scratch/fresh-packages"
export NUGET_HTTP_CACHE_PATH="$scratch/fresh-http"

dotnet tool install FS.GG.SDD.Cli --version "$version" --tool-path "$scratch/tool" \
  --configfile "$scratch/NuGet.Config" --no-cache >/dev/null
cli="$scratch/tool/fsgg-sdd"
[[ -x "$cli" ]] || fail 'installed CLI executable is absent'

cache="$scratch/cache/objects"
mkdir -p "$cache"
cp "$QUINT_BIN" "$cache/939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f"
cp "$LMT_BIN" "$cache/37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10"

for run in a b; do
  root="$scratch/author-$run"
  mkdir -p "$root"
  "$cli" typed-sdd author --root "$root" --work demo --title Demo --agent acceptance --session exact \
    --backend quint-specification-v1 --cache "$scratch/cache" >"$scratch/author-$run.json"
  grep -F '"outcome": "succeeded"' "$scratch/author-$run.json" >/dev/null || fail "installed author $run failed"
  "$cli" typed-sdd inspect --root "$root" --work demo >"$scratch/inspect-$run.json"
  grep -F '"outcome": "succeeded"' "$scratch/inspect-$run.json" >/dev/null || fail "installed inspect $run failed"
done
diff -ru "$scratch/author-a" "$scratch/author-b" >/dev/null || fail 'two installed author roots differ'
cmp "$scratch/author-a.json" "$scratch/author-b.json" >/dev/null || fail 'two installed author reports differ'

# Hard process death at every live-author move must recover before another operation reads authority.
for boundary in $(seq 1 9); do
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
grep -F 'fsgg.requirements-extension/v1+base64' "$migration/work/demo/specification.md" >/dev/null || fail 'literate authority lacks retained v1 semantic payload'
"$cli" typed-sdd inspect --root "$migration" --work demo >/dev/null || fail 'migrated authority did not inspect'
set +e
FSGG_TYPED_SDD_TEST_CRASH_AFTER_MOVE=5 "$cli" typed-sdd rollback --root "$migration" --work demo --accept >/dev/null 2>&1
rollback_crash=$?
set -e
[[ $rollback_crash -ne 0 ]] || fail 'injected rollback crash unexpectedly succeeded'
"$cli" typed-sdd inspect --root "$migration" --work demo >/dev/null || fail 'rollback crash did not recover the complete v2 pre-state'
"$cli" typed-sdd rollback --root "$migration" --work demo --accept >"$scratch/rollback.json"
grep -F '"outcome": "succeeded"' "$scratch/rollback.json" >/dev/null || fail 'installed rollback failed'
find "$migration" -type f -print0 | sort -z | xargs -0 sha256sum >"$scratch/v1.after"
cmp "$scratch/v1.before" "$scratch/v1.after" >/dev/null || fail 'rollback did not restore the exact v1 tree'

if [[ -n "${Q3_JUNIT_OUT:-}" ]]; then
  mkdir -p "$(dirname "$Q3_JUNIT_OUT")"
  printf '%s\n' \
    '<?xml version="1.0" encoding="utf-8"?>' \
    '<testsuite name="FS.GG.SDD.QuintQ3TypedSddAcceptance" tests="12" failures="0">' \
    '  <testcase classname="QuintQ3" name="fresh-cache-offline-tool-install" />' \
    '  <testcase classname="QuintQ3" name="exact-content-addressed-tools" />' \
    '  <testcase classname="QuintQ3" name="two-isolated-author-runs" />' \
    '  <testcase classname="QuintQ3" name="crash-recovery-every-author-boundary" />' \
    '  <testcase classname="QuintQ3" name="concurrent-inspect-transaction-lock" />' \
    '  <testcase classname="QuintQ3" name="manifest-v2-inspect" />' \
    '  <testcase classname="QuintQ3" name="missing-cache-no-write" />' \
    '  <testcase classname="QuintQ3" name="edited-artifact-red" />' \
    '  <testcase classname="QuintQ3" name="typed-effect-semantic-closure" />' \
    '  <testcase classname="QuintQ3" name="v1-preflight-and-migration" />' \
    '  <testcase classname="QuintQ3" name="authenticated-byte-exact-rollback" />' \
    '  <testcase classname="QuintQ3" name="rollback-crash-recovery" />' \
    '</testsuite>' >"$Q3_JUNIT_OUT"
fi

printf 'Q3-TYPED-SDD-ACCEPTED: offline installed lifecycle, exact tools, deterministic v2, migration and rollback; Q2 replay/parity evidence verified when delegated\n'
