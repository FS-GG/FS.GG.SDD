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
framework="$(python3 - <<'PY'
from pathlib import Path
import shutil
root=Path(shutil.which('dotnet')).resolve().parent/'packs/Microsoft.NETCore.App.Ref'
candidates=[p for p in root.iterdir() if p.name.startswith('10.0.') and all(part.isdigit() for part in p.name.split('.'))]
selected=max(candidates,key=lambda p:tuple(map(int,p.name.split('.'))))/'ref/net10.0'
assert selected.is_dir()
print(selected)
PY
)"
compare() {
  if "$scratch/tool/apicompat" "$@" --lref "$left,$framework" --rref "$right,$framework" > "$scratch/comparison.log" 2>&1; then
    comparison_status=0
  else
    comparison_status=$?
  fi
  cat "$scratch/comparison.log"
  [ "$comparison_status" -eq 0 ] || return "$comparison_status"
  python3 - "$scratch/comparison.log" <<'PY'
from pathlib import Path
import sys
if 'Could not resolve reference' in Path(sys.argv[1]).read_text():
    raise SystemExit('ApiCompat reference resolution incomplete; refusing a compared verdict')
PY
}
for assembly in FS.GG.SDD.Artifacts FS.GG.SDD.Cli FS.GG.SDD.Commands FS.GG.SDD.Validation; do
  compare -l "$left/$assembly.dll" -r "$right/$assembly.dll"
done
compare \
  -l "$scratch/left/FS.GG.SDD.Artifacts/lib/net10.0/FS.GG.SDD.Artifacts.dll" \
  -r "$scratch/right/FS.GG.SDD.Artifacts/lib/net10.0/FS.GG.SDD.Artifacts.dll"
printf '%s\n' 'ApiCompat compared the four shipped tool assemblies and standalone Artifacts against public 2.0.3.' \
  'Knowledge is a new package with no published API baseline; it was not classified as compared.'
