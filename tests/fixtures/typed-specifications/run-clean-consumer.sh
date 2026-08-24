#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
fixture="$repo_root/tests/fixtures/typed-specifications/consumer"
scratch="$(mktemp -d)"
feed="$scratch/feed"
consumer="$scratch/consumer"
tools="$scratch/tools"
trap 'rm -rf "$scratch"' EXIT

mkdir -p "$feed"
cp -R "$fixture" "$consumer"

dotnet pack "$repo_root/src/FS.GG.Contracts/FS.GG.Contracts.fsproj" -c Release -o "$feed"
dotnet pack "$repo_root/src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj" -c Release -o "$feed"
export NUGET_PACKAGES="$scratch/packages"
dotnet restore "$consumer/Consumer.fsproj" --configfile "$consumer/NuGet.Config"
dotnet run --project "$consumer/Consumer.fsproj" -c Release --no-restore
dotnet restore "$consumer/EvidenceParity.fsproj" --configfile "$consumer/NuGet.Config"

dotnet_output=$(dotnet run \
  --project "$consumer/EvidenceParity.fsproj" \
  -c Release \
  --no-restore)

dotnet tool install fable --version 5.13.0 --tool-path "$tools"
"$tools/fable" "$consumer/EvidenceParity.fsproj" \
  --outDir "$scratch/fable" \
  --noCache
fable_output=$(node "$scratch/fable/EvidenceParity.js")

expected=$'satisfied=0\ndiagnostics=SPEC-EVIDENCE-MISSING,SPEC-EVIDENCE-DUPLICATE,SPEC-EVIDENCE-KIND,SPEC-EVIDENCE-REF-REQUIRED'

if [[ "$dotnet_output" != "$expected" || "$fable_output" != "$expected" ]]; then
  echo '.NET/Fable invalid-evidence diagnostics diverged from the public contract' >&2
  diff -u <(printf '%s\n' "$dotnet_output") <(printf '%s\n' "$fable_output") >&2 || true
  printf 'expected:\n%s\n' "$expected" >&2
  exit 1
fi

if find "$consumer" -type f \( -name '*.fsproj' -o -name '*.props' -o -name '*.targets' \) -print0 \
  | xargs -0 grep -E 'ProjectReference|FS\.GG\.SIR|FS\.GG\.Coord'; then
  echo 'clean consumer contains a forbidden source or dependency shortcut' >&2
  exit 1
fi
