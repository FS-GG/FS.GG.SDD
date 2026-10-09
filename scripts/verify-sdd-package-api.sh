#!/usr/bin/env bash
# Compare shipped 2.2 APIs; Commands has no standalone package baseline. Cold-consume its actual candidate.
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
for id in ['FS.GG.SDD.Artifacts', 'FS.GG.SDD.Cli', 'FS.GG.SDD.Knowledge']:
    url = f'https://api.nuget.org/v3-flatcontainer/{id.lower()}/2.2.0/{id.lower()}.2.2.0.nupkg'
    baseline = scratch / f'{id}.2.2.0.nupkg'
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
compare \
  -l "$scratch/left/FS.GG.SDD.Knowledge/lib/net10.0/FS.GG.SDD.Knowledge.dll" \
  -r "$scratch/right/FS.GG.SDD.Knowledge/lib/net10.0/FS.GG.SDD.Knowledge.dll"
# The source Commands test gate checks authored .fsi and reflection baselines.
# This cold package consumer proves its standalone package is usable without source project references.
python3 - "$scratch" "$candidate" "$version" <<'PYCONSUMER'
from pathlib import Path
import sys, xml.etree.ElementTree as E
scratch,candidate=map(Path,sys.argv[1:3]);version=sys.argv[3]
consumer=scratch/'commands-consumer';consumer.mkdir()
project=E.Element('Project',Sdk='Microsoft.NET.Sdk');properties=E.SubElement(project,'PropertyGroup')
for name,value in [('TargetFramework','net10.0'),('OutputType','Exe')]: E.SubElement(properties,name).text=value
items=E.SubElement(project,'ItemGroup');E.SubElement(items,'Compile',Include='Program.fs')
E.SubElement(items,'PackageReference',Include='FS.GG.SDD.Commands',Version='['+version+']')
E.SubElement(items,'PackageReference',Include='FS.GG.Contracts',Version='[7.6.0]')
E.ElementTree(project).write(consumer/'consumer.fsproj',encoding='unicode')
(consumer/'Program.fs').write_text('open FS.GG.SDD.Commands\nif CommandHelp.commandEntries.IsEmpty then failwith "actual Commands public API unavailable"\nprintfn "Commands cold consumer passed"\n')
config=E.Element('configuration');sources=E.SubElement(config,'packageSources');E.SubElement(sources,'clear')
E.SubElement(sources,'add',key='candidate',value=str(candidate));E.SubElement(sources,'add',key='public',value='https://api.nuget.org/v3/index.json')
mapping=E.SubElement(config,'packageSourceMapping');local=E.SubElement(mapping,'packageSource',key='candidate');E.SubElement(local,'package',pattern='FS.GG.SDD.*')
public=E.SubElement(mapping,'packageSource',key='public');E.SubElement(public,'package',pattern='*')
E.ElementTree(config).write(consumer/'NuGet.Config',encoding='unicode')
PYCONSUMER
dotnet restore "$scratch/commands-consumer/consumer.fsproj" --configfile "$scratch/commands-consumer/NuGet.Config" --no-http-cache
dotnet run --project "$scratch/commands-consumer/consumer.fsproj" -c Release --no-restore
printf '%s\n' 'ApiCompat compared previously shipped tool assemblies and standalone Artifacts/Knowledge against public 2.2.0.' \
  'Commands has no observed standalone package baseline; authored public-surface tests and a cold actual-package consumer gate retention.'
