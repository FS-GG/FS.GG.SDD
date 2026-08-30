#!/usr/bin/env bash
# Mutation tests for exact archive custody from dry-run artifact to tag publication.
set -uo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
verifier="$repo/scripts/verify-release-candidate.sh"
fail=0
head_sha="0123456789abcdef0123456789abcdef01234567"
version="1.5.1"

make_package() {
  local path="$1" id="$2" timestamp="$3"
  python3 - "$path" "$id" "$version" "$head_sha" "$timestamp" <<'PY'
import sys, zipfile
path, package_id, version, head, timestamp = sys.argv[1:]
year = int(timestamp)
info = zipfile.ZipInfo(f"{package_id}.nuspec", (year, 1, 1, 0, 0, 0))
body = f'''<?xml version="1.0"?><package><metadata><id>{package_id}</id><version>{version}</version><repository type="git" commit="{head}" /></metadata></package>'''.encode()
with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as archive:
    archive.writestr(info, body)
PY
}

seed_candidate() {
  local root="$1" timestamp="$2"
  mkdir -p "$root"
  make_package "$root/FS.GG.SDD.Artifacts.$version.nupkg" FS.GG.SDD.Artifacts "$timestamp"
  make_package "$root/FS.GG.SDD.Cli.$version.nupkg" FS.GG.SDD.Cli "$timestamp"
  printf '%s\n' \
    'schema=fsgg.sdd.release-candidate/v1' \
    "head=$head_sha" \
    "version=$version" \
    'packages=FS.GG.SDD.Artifacts,FS.GG.SDD.Cli' > "$root/candidate.env"
  (cd "$root" && sha256sum FS.GG.SDD.Artifacts.*.nupkg FS.GG.SDD.Cli.*.nupkg | LC_ALL=C sort -k2 > pre-push.sha256)
}

run_case() {
  local name="$1" root="$2" expected="$3" observed
  "$verifier" "$root" "$head_sha" "$version" >/dev/null 2>&1
  observed=$?
  if [ "$observed" -eq "$expected" ]; then
    printf '  ok   %-38s -> exit %s\n' "$name" "$observed"
  else
    printf '  FAIL %-38s expected exit %s, got %s\n' "$name" "$expected" "$observed"
    fail=1
  fi
}

root="$(mktemp -d)"
trap 'rm -rf "$root"' EXIT

positive="$root/positive"
seed_candidate "$positive" 2020
run_case "exact retained artifact passes" "$positive" 0

wrong_head="$root/wrong-head"
cp -R "$positive" "$wrong_head"
sed -i 's/^head=.*/head=ffffffffffffffffffffffffffffffffffffffff/' "$wrong_head/candidate.env"
run_case "head substitution reds" "$wrong_head" 1

wrong_hash="$root/wrong-hash"
cp -R "$positive" "$wrong_hash"
printf 'changed-after-qualification\n' >> "$wrong_hash/FS.GG.SDD.Cli.$version.nupkg"
run_case "post-handoff byte mutation reds" "$wrong_hash" 1

# Back-to-back container inversion: equal extracted payloads do not imply equal nupkg bytes.
pack_a="$root/pack-a"
pack_b="$root/pack-b"
seed_candidate "$pack_a" 2020
seed_candidate "$pack_b" 2021
sha_a="$(sha256sum "$pack_a/FS.GG.SDD.Cli.$version.nupkg" | cut -d' ' -f1)"
sha_b="$(sha256sum "$pack_b/FS.GG.SDD.Cli.$version.nupkg" | cut -d' ' -f1)"
if [ "$sha_a" != "$sha_b" ] && diff -u \
    <(unzip -p "$pack_a/FS.GG.SDD.Cli.$version.nupkg" '*.nuspec') \
    <(unzip -p "$pack_b/FS.GG.SDD.Cli.$version.nupkg" '*.nuspec') >/dev/null; then
  printf '  ok   %-38s\n' "back-to-back containers differ"
else
  printf '  FAIL %-38s\n' "back-to-back containers differ"
  fail=1
fi

substituted="$root/substituted"
cp -R "$pack_a" "$substituted"
cp "$pack_b/FS.GG.SDD.Cli.$version.nupkg" "$substituted/FS.GG.SDD.Cli.$version.nupkg"
run_case "equal-payload archive swap reds" "$substituted" 1

if [ "$fail" -ne 0 ]; then
  echo "release-artifact-custody.test.sh: FAILURES" >&2
  exit 1
fi
echo "release-artifact-custody.test.sh: all passed"
