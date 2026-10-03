#!/usr/bin/env bash
# Verify the only package artifact a later tag/release run may promote.
set -euo pipefail

if [ "$#" -ne 3 ]; then
  echo "usage: verify-release-candidate.sh <package-dir> <expected-head> <expected-version>" >&2
  exit 2
fi

package_dir="$(cd "$1" && pwd)"
expected_head="$2"
expected_version="$3"

case "$expected_head" in
  [0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f]) ;;
  *) echo "release candidate head is not a full lowercase Git SHA: $expected_head" >&2; exit 1 ;;
esac

inventory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/sdd-release-packages.txt"
cd "$package_dir"
shopt -s nullglob
all_packages=(*.nupkg)
mapfile -t package_ids < "$inventory"
[ "${#all_packages[@]}" -eq "${#package_ids[@]}" ] || { echo "release candidate package count differs from the reviewed inventory" >&2; exit 1; }
for id in "${package_ids[@]}"; do
  [ -f "$id.$expected_version.nupkg" ] || { echo "missing reviewed package $id $expected_version" >&2; exit 1; }
done
[ -f candidate.env ] || { echo "release candidate identity manifest is missing" >&2; exit 1; }
[ -f pre-push.sha256 ] || { echo "release candidate hash manifest is missing" >&2; exit 1; }

expected_identity="$(printf '%s\n' \
  'schema=fsgg.sdd.release-candidate/v2' \
  "head=$expected_head" \
  "version=$expected_version" \
  "packages=$(IFS=,; echo "${package_ids[*]}")")"
[ "$(cat candidate.env)" = "$expected_identity" ] || { echo "release candidate identity manifest does not match the requested head/version" >&2; exit 1; }

mapfile -t hashed_names < <(awk 'NF == 2 { print $2 }' pre-push.sha256 | LC_ALL=C sort)
[ "${#hashed_names[@]}" -eq "${#package_ids[@]}" ] || { echo "release candidate hash count differs from the reviewed inventory" >&2; exit 1; }
for index in "${!package_ids[@]}"; do
  [ "${hashed_names[$index]}" = "${package_ids[$index]}.$expected_version.nupkg" ] || { echo "hash manifest package/version mismatch" >&2; exit 1; }
done
sha256sum --check --strict pre-push.sha256

python3 - "$expected_head" "$expected_version" "${package_ids[@]}" <<'PYCODE'
import sys, zipfile, xml.etree.ElementTree as E
head, version, *ids = sys.argv[1:]
for package_id in ids:
    path = f"{package_id}.{version}.nupkg"
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        nuspecs = [n for n in names if n.endswith('.nuspec')]
        if len(nuspecs) != 1 or len(names) != len(set(names)):
            raise SystemExit(f"{path}: ambiguous package entries")
        root = E.fromstring(archive.read(nuspecs[0]))
        metadata = root.find('{*}metadata')
        if metadata is None or metadata.findtext('{*}id') != package_id or metadata.findtext('{*}version') != version:
            raise SystemExit(f"{path}: package metadata identity/version mismatch")
        repository = metadata.find('{*}repository')
        if repository is None or repository.get('commit') != head:
            raise SystemExit(f"{path}: repository source binding mismatch")
        if package_id == 'FS.GG.SDD.Knowledge':
            for member in ['api-surface/Store.fsi', 'api-surface/Workspace.fsi', 'lib/net10.0/FS.GG.SDD.Knowledge.dll']:
                if member not in names:
                    raise SystemExit(f"{path}: required SDK member missing: {member}")
PYCODE

echo "release candidate verified: head=$expected_head version=$expected_version packages=${#package_ids[@]}"
