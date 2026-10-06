#!/usr/bin/env bash
# Mutation tests for exact archive custody from dry-run artifact to tag publication.
set -uo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
verifier="$repo/scripts/verify-release-candidate.sh"
fail=0
head_sha="0123456789abcdef0123456789abcdef01234567"
version="2.1.0"
contracts_version="7.6.0"

make_package() {
  local path="$1" id="$2" timestamp="$3" selected_version="$version"
  [ "$id" != FS.GG.Contracts ] || selected_version="$contracts_version"
  python3 - "$path" "$id" "$selected_version" "$head_sha" "$timestamp" <<'PY'
import sys, zipfile
path, package_id, version, head, timestamp = sys.argv[1:]
year = int(timestamp)
info = zipfile.ZipInfo(f"{package_id}.nuspec", (year, 1, 1, 0, 0, 0))
dependencies = '<dependencies><group targetFramework="net10.0"><dependency id="FS.GG.Contracts" version="7.6.0" /></group></dependencies>' if package_id == "FS.GG.SDD.Artifacts" else ""
body = f'''<?xml version="1.0"?><package><metadata><id>{package_id}</id><version>{version}</version><repository type="git" commit="{head}" />{dependencies}</metadata></package>'''.encode()
with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as archive:
    archive.writestr(info, body)
    if package_id == "FS.GG.Contracts":
        for member in ["api-surface/Provider.fsi", "lib/net10.0/FS.GG.Contracts.dll"]:
            archive.writestr(member, b"synthetic Contracts fixture")
    if package_id == "FS.GG.SDD.Cli":
        archive.writestr("tools/net10.0/any/FS.GG.Contracts.dll", b"synthetic Contracts fixture")
    if package_id == "FS.GG.SDD.Knowledge":
        for member in ["api-surface/Store.fsi", "api-surface/Workspace.fsi", "lib/net10.0/FS.GG.SDD.Knowledge.dll"]:
            archive.writestr(member, b"synthetic custody fixture")
PY
}

seed_candidate() {
  local root="$1" timestamp="$2"
  mkdir -p "$root"
  make_package "$root/FS.GG.Contracts.$contracts_version.nupkg" FS.GG.Contracts "$timestamp"
  make_package "$root/FS.GG.SDD.Artifacts.$version.nupkg" FS.GG.SDD.Artifacts "$timestamp"
  make_package "$root/FS.GG.SDD.Cli.$version.nupkg" FS.GG.SDD.Cli "$timestamp"
  make_package "$root/FS.GG.SDD.Knowledge.$version.nupkg" FS.GG.SDD.Knowledge "$timestamp"
  printf '%s\n' \
    'schema=fsgg.sdd.release-candidate/v3' \
    "head=$head_sha" \
    "version=$version" \
    "contracts_version=$contracts_version" \
    'packages=FS.GG.Contracts,FS.GG.SDD.Artifacts,FS.GG.SDD.Cli,FS.GG.SDD.Knowledge' > "$root/candidate.env"
  (cd "$root" && sha256sum FS.GG.Contracts.*.nupkg FS.GG.SDD.Artifacts.*.nupkg FS.GG.SDD.Cli.*.nupkg FS.GG.SDD.Knowledge.*.nupkg | LC_ALL=C sort -k2 > pre-push.sha256)
}

run_case() {
  local name="$1" root="$2" expected="$3" observed
  "$verifier" "$root" "$head_sha" "$version" "$contracts_version" >/dev/null 2>&1
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

missing="$root/missing-knowledge"
cp -R "$pack_a" "$missing"
rm "$missing/FS.GG.SDD.Knowledge.$version.nupkg"
run_case "missing Knowledge archive reds" "$missing" 1

extra="$root/extra"
cp -R "$pack_a" "$extra"
cp "$extra/FS.GG.SDD.Cli.$version.nupkg" "$extra/Unexpected.$version.nupkg"
run_case "extra archive reds" "$extra" 1

wrong="$root/wrong-version"
cp -R "$pack_a" "$wrong"
mv "$wrong/FS.GG.SDD.Knowledge.$version.nupkg" "$wrong/FS.GG.SDD.Knowledge.2.0.3.nupkg"
run_case "Knowledge version skew reds" "$wrong" 1

missing_contracts="$root/missing-contracts"
cp -R "$positive" "$missing_contracts"
rm "$missing_contracts/FS.GG.Contracts.$contracts_version.nupkg"
run_case "missing Contracts refuses" "$missing_contracts" 1
wrong_contracts="$root/wrong-contracts"
cp -R "$positive" "$wrong_contracts"
sed -i 's/^contracts_version=.*/contracts_version=7.5.2/' "$wrong_contracts/candidate.env"
run_case "independent Contracts version refuses" "$wrong_contracts" 1
mutate_and_rehash() {
  python3 - "$1" "$2" "$version" "$contracts_version" <<'PYCONTROL'
from pathlib import Path
import hashlib,sys,zipfile
root,role,version,contracts=sys.argv[1:];root=Path(root)
id={'dependency':'FS.GG.SDD.Artifacts','embedded':'FS.GG.SDD.Cli','source':'FS.GG.Contracts'}[role]
path=root/f'{id}.{contracts if role=="source" else version}.nupkg'
with zipfile.ZipFile(path) as z: entries={n:z.read(n) for n in z.namelist()}
if role=='embedded':entries['tools/net10.0/any/FS.GG.Contracts.dll']=b'different compiler payload'
elif role=='dependency':entries[id+'.nuspec']=entries[id+'.nuspec'].replace(b'version="7.6.0"',b'version="7.5.2"')
else:entries[id+'.nuspec']=entries[id+'.nuspec'].replace(b'commit="0123456789abcdef0123456789abcdef01234567"',b'commit="ffffffffffffffffffffffffffffffffffffffff"')
with zipfile.ZipFile(path,'w') as z:
    for name,raw in entries.items():z.writestr(name,raw)
(root/'pre-push.sha256').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.name+'\n' for p in sorted(root.glob('*.nupkg'))))
PYCONTROL
}
for role in dependency embedded source; do
  target="$root/semantic-$role"
  cp -R "$positive" "$target"
  mutate_and_rehash "$target" "$role"
  run_case "rehashed $role substitution refuses" "$target" 1
done

if [ "$fail" -ne 0 ]; then
  echo "release-artifact-custody.test.sh: FAILURES" >&2
  exit 1
fi
echo "release-artifact-custody.test.sh: all passed"
