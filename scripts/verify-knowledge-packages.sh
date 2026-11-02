#!/usr/bin/env bash
# One installed-byte qualifier for retained local archives and later public NuGet.
set -euo pipefail
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="${FSGG_KNOWLEDGE_VERSION:?exact version required}"
source="${FSGG_KNOWLEDGE_SOURCE:?explicit candidate or public source required}"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo 'invalid stable version' >&2; exit 1; }
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT
export DOTNET_PROCESSOR_COUNT=1
export DOTNET_CLI_HOME="$scratch/cli-home"
export NUGET_PACKAGES="$scratch/packages"
export NUGET_HTTP_CACHE_PATH="$scratch/http"
export FSGG_KNOWLEDGE_VERSION="$version" FSGG_KNOWLEDGE_SOURCE="$source"
python3 - "$scratch" <<'PY'
import os, pathlib, sys, xml.etree.ElementTree as E
root=pathlib.Path(sys.argv[1]);config=E.Element('configuration');sources=E.SubElement(config,'packageSources');E.SubElement(sources,'clear')
E.SubElement(sources,'add',key='candidate',value=os.environ['FSGG_KNOWLEDGE_SOURCE'])
if os.environ['FSGG_KNOWLEDGE_SOURCE']!='https://api.nuget.org/v3/index.json':
 E.SubElement(sources,'add',key='public',value='https://api.nuget.org/v3/index.json')
mapping=E.SubElement(config,'packageSourceMapping');candidate=E.SubElement(mapping,'packageSource',key='candidate')
if os.environ['FSGG_KNOWLEDGE_SOURCE']=='https://api.nuget.org/v3/index.json': E.SubElement(candidate,'package',pattern='*')
else:
 E.SubElement(candidate,'package',pattern='FS.GG.SDD.*')
 public=E.SubElement(mapping,'packageSource',key='public');E.SubElement(public,'package',pattern='*')
E.ElementTree(config).write(root/'NuGet.Config',encoding='unicode')
PY
dotnet tool install FS.GG.SDD.Cli --version "$version" --tool-path "$scratch/tool" --configfile "$scratch/NuGet.Config" --no-cache
sdk_version="$(dotnet --version)"
sdk_root="$(dotnet --list-sdks | awk -v version="$sdk_version" '$1 == version {gsub(/[][]/, "", $2); print $2}')"
core_version="$(python3 - "$repo/Directory.Packages.props" <<'PY'
import re, sys, xml.etree.ElementTree as E
rows = [row.get('Version') for row in E.parse(sys.argv[1]).iter('PackageVersion')
        if row.get('Include') == 'FSharp.Core']
if len(rows) != 1 or not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+', rows[0] or ''):
    raise SystemExit('expected exactly one literal central FSharp.Core stable version')
print(rows[0])
PY
)"
python3 - "$scratch" "$sdk_root/$sdk_version/FSharp" "$core_version" <<'PY'
import hashlib, io, json, pathlib, shutil, sys, urllib.request, zipfile
root=pathlib.Path(sys.argv[1]); mirror=root/'official-core-fsi'; version=sys.argv[3]
shutil.copytree(sys.argv[2],mirror)
url=f'https://api.nuget.org/v3-flatcontainer/fsharp.core/{version}/fsharp.core.{version}.nupkg'
data=urllib.request.urlopen(url,timeout=30).read()
with zipfile.ZipFile(io.BytesIO(data)) as archive:
 core=archive.read('lib/netstandard2.0/FSharp.Core.dll')
 (mirror/'FSharp.Core.dll').write_bytes(core)
 identities={name:hashlib.sha256(archive.read(name)).hexdigest() for name in archive.namelist() if name.endswith('/FSharp.Core.dll')}
(root/'core-provenance.json').write_text(json.dumps(dict(url=url,archiveSha256=hashlib.sha256(data).hexdigest(),assets=identities)))
PY
export FSGG_KNOWLEDGE_FSI="$scratch/official-core-fsi/fsi.dll"
python3 "$repo/scripts/verify-knowledge-installed.py" "$scratch" "$version"
