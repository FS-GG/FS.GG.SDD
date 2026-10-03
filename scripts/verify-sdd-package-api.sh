#!/usr/bin/env bash
# Compare existing shipped SDD assemblies; the new Knowledge package has no baseline yet.
set -euo pipefail
candidate="${1:?candidate package directory required}"
version="${2:?candidate version required}"
candidate="$(cd "$candidate" && pwd)"
scratch="$(mktemp -d)"
cleanup() { python3 -c 'import shutil,sys; shutil.rmtree(sys.argv[1])' "$scratch"; }
trap cleanup EXIT
export DOTNET_PROCESSOR_COUNT=1
export DOTNET_CLI_HOME="$scratch/home"
export NUGET_PACKAGES="$scratch/packages"
cat > "$scratch/NuGet.Config" <<'XML'
<configuration><packageSources><clear/><add key="public" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>
XML
dotnet tool install Microsoft.DotNet.ApiCompat.Tool --version 10.0.401 \
  --tool-path "$scratch/tool" --configfile "$scratch/NuGet.Config"
python3 - "$scratch" "$candidate" "$version" <<'PY'
from pathlib import Path
import sys, urllib.request, zipfile
scratch, candidate = map(Path, sys.argv[1:3]); version = sys.argv[3]
for id in ['FS.GG.SDD.Artifacts', 'FS.GG.SDD.Cli']:
    url = f'https://api.nuget.org/v3-flatcontainer/{id.lower()}/2.0.3/{id.lower()}.2.0.3.nupkg'
    baseline = scratch / f'{id}.2.0.3.nupkg'
    urllib.request.urlretrieve(url, baseline)
    for side, archive in [('left', baseline), ('right', candidate / f'{id}.{version}.nupkg')]:
        with zipfile.ZipFile(archive) as z:
            z.extractall(scratch / side / id)
PY
left="$scratch/left/FS.GG.SDD.Cli/tools/net10.0/any"
right="$scratch/right/FS.GG.SDD.Cli/tools/net10.0/any"
for assembly in FS.GG.SDD.Artifacts FS.GG.SDD.Cli FS.GG.SDD.Commands FS.GG.SDD.Validation; do
  "$scratch/tool/apicompat" -l "$left/$assembly.dll" -r "$right/$assembly.dll" \
    --lref "$left" --rref "$right"
done
"$scratch/tool/apicompat" \
  -l "$scratch/left/FS.GG.SDD.Artifacts/lib/net10.0/FS.GG.SDD.Artifacts.dll" \
  -r "$scratch/right/FS.GG.SDD.Artifacts/lib/net10.0/FS.GG.SDD.Artifacts.dll" \
  --lref "$left" --rref "$right"
printf '%s\n' 'ApiCompat compared the four shipped tool assemblies and standalone Artifacts against public 2.0.3.' \
  'Knowledge is a new package with no published API baseline; it was not classified as compared.'
