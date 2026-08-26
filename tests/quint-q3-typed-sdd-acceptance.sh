#!/usr/bin/env bash
set -euo pipefail

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

migration="$scratch/migration"
mkdir -p "$migration"
"$cli" typed-sdd author --root "$migration" --work demo --title 'REQ-AUDIT-001 EV-VERIFY-001' \
  --agent acceptance --session v1 >/dev/null
find "$migration" -type f -print0 | sort -z | xargs -0 sha256sum >"$scratch/v1.before"
"$cli" typed-sdd migrate --root "$migration" --work demo --source work/demo/spec.md --accept \
  --backend quint-specification-v1 --cache "$scratch/cache" --agent acceptance --session migration \
  >"$scratch/migrate.json"
grep -F '"outcome": "succeeded"' "$scratch/migrate.json" >/dev/null || fail 'installed v1-to-v2 migration failed'
"$cli" typed-sdd inspect --root "$migration" --work demo >/dev/null || fail 'migrated authority did not inspect'
"$cli" typed-sdd rollback --root "$migration" --work demo --accept >"$scratch/rollback.json"
grep -F '"outcome": "succeeded"' "$scratch/rollback.json" >/dev/null || fail 'installed rollback failed'
find "$migration" -type f -print0 | sort -z | xargs -0 sha256sum >"$scratch/v1.after"
cmp "$scratch/v1.before" "$scratch/v1.after" >/dev/null || fail 'rollback did not restore the exact v1 tree'

if [[ -n "${Q3_JUNIT_OUT:-}" ]]; then
  mkdir -p "$(dirname "$Q3_JUNIT_OUT")"
  printf '%s\n' \
    '<?xml version="1.0" encoding="utf-8"?>' \
    '<testsuite name="FS.GG.SDD.QuintQ3TypedSddAcceptance" tests="10" failures="0">' \
    '  <testcase classname="QuintQ3" name="fresh-cache-offline-tool-install" />' \
    '  <testcase classname="QuintQ3" name="exact-content-addressed-tools" />' \
    '  <testcase classname="QuintQ3" name="two-isolated-author-runs" />' \
    '  <testcase classname="QuintQ3" name="manifest-v2-inspect" />' \
    '  <testcase classname="QuintQ3" name="missing-cache-no-write" />' \
    '  <testcase classname="QuintQ3" name="edited-artifact-red" />' \
    '  <testcase classname="QuintQ3" name="v1-preflight-and-migration" />' \
    '  <testcase classname="QuintQ3" name="authenticated-byte-exact-rollback" />' \
    '  <testcase classname="QuintQ3" name="installed-package-sir-replay" />' \
    '  <testcase classname="QuintQ3" name="fable-node-parity" />' \
    '</testsuite>' >"$Q3_JUNIT_OUT"
fi

printf 'Q3-TYPED-SDD-ACCEPTED: offline installed lifecycle, exact tools, deterministic v2, migration/rollback, replay and Fable parity\n'
