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

cd "$package_dir"
shopt -s nullglob
all_packages=(*.nupkg)
artifacts=(FS.GG.SDD.Artifacts.*.nupkg)
cli=(FS.GG.SDD.Cli.*.nupkg)

[ "${#all_packages[@]}" -eq 2 ] || { echo "release candidate must contain exactly two nupkgs" >&2; exit 1; }
[ "${#artifacts[@]}" -eq 1 ] || { echo "release candidate must contain exactly one Artifacts nupkg" >&2; exit 1; }
[ "${#cli[@]}" -eq 1 ] || { echo "release candidate must contain exactly one CLI nupkg" >&2; exit 1; }
[ "${artifacts[0]}" = "FS.GG.SDD.Artifacts.$expected_version.nupkg" ] || { echo "Artifacts filename/version mismatch" >&2; exit 1; }
[ "${cli[0]}" = "FS.GG.SDD.Cli.$expected_version.nupkg" ] || { echo "CLI filename/version mismatch" >&2; exit 1; }
[ -f candidate.env ] || { echo "release candidate identity manifest is missing" >&2; exit 1; }
[ -f pre-push.sha256 ] || { echo "release candidate hash manifest is missing" >&2; exit 1; }

expected_identity="$(printf '%s\n' \
  'schema=fsgg.sdd.release-candidate/v1' \
  "head=$expected_head" \
  "version=$expected_version" \
  'packages=FS.GG.SDD.Artifacts,FS.GG.SDD.Cli')"
[ "$(cat candidate.env)" = "$expected_identity" ] || { echo "release candidate identity manifest does not match the requested head/version" >&2; exit 1; }

mapfile -t hashed_names < <(awk 'NF == 2 { print $2 }' pre-push.sha256 | LC_ALL=C sort)
[ "${#hashed_names[@]}" -eq 2 ] || { echo "release candidate hash manifest must contain exactly two rows" >&2; exit 1; }
[ "${hashed_names[0]}" = "FS.GG.SDD.Artifacts.$expected_version.nupkg" ] || { echo "hash manifest Artifacts entry mismatch" >&2; exit 1; }
[ "${hashed_names[1]}" = "FS.GG.SDD.Cli.$expected_version.nupkg" ] || { echo "hash manifest CLI entry mismatch" >&2; exit 1; }
sha256sum --check --strict pre-push.sha256

for package in "${all_packages[@]}"; do
  unzip -p "$package" '*.nuspec' | grep -F "commit=\"$expected_head\"" >/dev/null \
    || { echo "$package does not bind repository commit $expected_head" >&2; exit 1; }
done

echo "release candidate verified: head=$expected_head version=$expected_version packages=2"
