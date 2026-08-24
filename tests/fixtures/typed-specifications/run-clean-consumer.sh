#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
fixture="$repo_root/tests/fixtures/typed-specifications/consumer"
scratch="$(mktemp -d)"
feed="$scratch/feed"
consumer="$scratch/consumer"
trap 'rm -rf "$scratch"' EXIT

mkdir -p "$feed"
cp -R "$fixture" "$consumer"

dotnet pack "$repo_root/src/FS.GG.Contracts/FS.GG.Contracts.fsproj" -c Release -o "$feed"
dotnet pack "$repo_root/src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj" -c Release -o "$feed"
dotnet restore "$consumer/Consumer.fsproj" --configfile "$consumer/NuGet.Config"
dotnet run --project "$consumer/Consumer.fsproj" -c Release --no-restore

if find "$consumer" -type f \( -name '*.fsproj' -o -name '*.props' -o -name '*.targets' \) -print0 \
  | xargs -0 grep -E 'ProjectReference|FS\.GG\.SIR|FS\.GG\.Coord'; then
  echo 'clean consumer contains a forbidden source or dependency shortcut' >&2
  exit 1
fi
