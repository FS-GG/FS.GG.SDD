#!/usr/bin/env bash
# Four once-packed SDD archives plus a separately retained published dependency.
set -euo pipefail
scripts="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ "${1:-}" = --record-contracts ]; then
  [ "$#" -eq 3 ] || { echo 'usage: verify-release-candidate.sh --record-contracts <package-dir> <contracts-version>' >&2; exit 2; }
  python3 - "$2" "$3" "$scripts" <<'PY'
import importlib.util,json,sys
from pathlib import Path
root,version,scripts=sys.argv[1:];root=Path(root)
spec=importlib.util.spec_from_file_location('occupancy',Path(scripts)/'check-sdd-release-occupancy.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
metadata=module.contracts_metadata((root/'dependencies'/f'FS.GG.Contracts.{version}.nupkg').read_bytes(),version)
with (root/'contracts-reuse.json').open('x') as output: output.write(json.dumps(metadata,sort_keys=True,indent=2)+'\n')
PY
  exit 0
fi
[ "$#" -eq 4 ] || { echo 'usage: verify-release-candidate.sh <package-dir> <head> <sdd-version> <contracts-version>' >&2; exit 2; }
python3 - "$1" "$2" "$3" "$4" "$scripts" <<'PYCODE'
import hashlib,importlib.util,json,re,sys,zipfile
from pathlib import Path
import xml.etree.ElementTree as E
root,head,version,contracts,scripts=sys.argv[1:];root=Path(root);scripts=Path(scripts)
spec=importlib.util.spec_from_file_location('occupancy',scripts/'check-sdd-release-occupancy.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
if not re.fullmatch(r'[0-9a-f]{40}',head) or any(not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+',v) for v in [version,contracts]):
    raise SystemExit('invalid candidate source/version identity')
ids=(scripts/'sdd-release-packages.txt').read_text().splitlines()
if ids != ['FS.GG.SDD.Artifacts','FS.GG.SDD.Commands','FS.GG.SDD.Cli','FS.GG.SDD.Knowledge']:
    raise SystemExit('reviewed four-SDD-package inventory drift')
paths={i:f'{i}.{version}.nupkg' for i in ids}
if sorted(p.name for p in root.glob('*.nupkg'))!=sorted(paths.values()):
    raise SystemExit('release candidate package population/version mismatch')
dependency=root/'dependencies'/f'FS.GG.Contracts.{contracts}.nupkg'
if list((root/'dependencies').glob('*.nupkg')) != [dependency]:
    raise SystemExit('reused dependency archive inventory mismatch')
metadata=module.contracts_metadata(dependency.read_bytes(),contracts)
if json.loads((root/'contracts-reuse.json').read_text()) != metadata:
    raise SystemExit('reused Contracts metadata/source/archive/payload identity mismatch')
identity=f'schema=fsgg.sdd.release-candidate/v4\nhead={head}\nversion={version}\ncontracts_version={contracts}\npackages={",".join(ids)}\n'
if (root/'candidate.env').read_text()!=identity:
    raise SystemExit('release candidate identity does not match source and reused dependency')
lines=(root/'pre-push.sha256').read_text().splitlines()
if len(lines)!=4 or any(not re.fullmatch(r'[0-9a-f]{64}  [A-Za-z0-9.]+\.nupkg',line) for line in lines):
    raise SystemExit('malformed candidate hash population')
if [line[66:] for line in lines]!=sorted(paths.values()):
    raise SystemExit('candidate hash manifest inventory/version mismatch')
for line in lines:
    if hashlib.sha256((root/line[66:]).read_bytes()).hexdigest()!=line[:64]:
        raise SystemExit('post-handoff archive byte substitution')
with zipfile.ZipFile(dependency) as archive:
    contracts_dll=archive.read('lib/net10.0/FS.GG.Contracts.dll')
with zipfile.ZipFile(root/paths['FS.GG.SDD.Commands']) as archive:
    commands_dll=archive.read('lib/net10.0/FS.GG.SDD.Commands.dll')
for package_id,path in paths.items():
    with zipfile.ZipFile(root/path) as archive:
        names=archive.namelist();nuspecs=[n for n in names if n.endswith('.nuspec')]
        if len(nuspecs)!=1 or len(names)!=len(set(names)):
            raise SystemExit(f'{path}: ambiguous package entries')
        package=E.fromstring(archive.read(nuspecs[0])).find('{*}metadata')
        if package is None or package.findtext('{*}id')!=package_id or package.findtext('{*}version')!=version:
            raise SystemExit(f'{path}: package metadata identity/version mismatch')
        repository=package.find('{*}repository')
        if repository is None or repository.get('commit')!=head:
            raise SystemExit(f'{path}: repository source binding mismatch')
        if package_id in ['FS.GG.SDD.Artifacts','FS.GG.SDD.Commands']:
            dependencies=[d for d in package.findall('.//{*}dependency') if d.get('id')=='FS.GG.Contracts']
            if len(dependencies)!=1 or dependencies[0].get('version','').replace(' ','') not in [contracts,'['+contracts+',)','['+contracts+']']:
                raise SystemExit(f'{path}: published Contracts dependency join mismatch')
        if package_id=='FS.GG.SDD.Cli':
            for name,expected in [('FS.GG.Contracts',contracts_dll),('FS.GG.SDD.Commands',commands_dll)]:
                member=f'tools/net10.0/any/{name}.dll'
                if member not in names or archive.read(member)!=expected:
                    raise SystemExit(f'{path}: tool {name} bytes differ from retained library')
        required=(['api-surface/Store.fsi','api-surface/Workspace.fsi'] if package_id=='FS.GG.SDD.Knowledge' else [])
        if package_id!='FS.GG.SDD.Cli': required.append(f'lib/net10.0/{package_id}.dll')
        if any(n not in names or not archive.read(n) for n in required):
            raise SystemExit(f'{path}: required public contract member missing or empty')
print(f'release candidate verified: head={head} sdd={version} packages=4; Contracts={contracts} reused from {metadata["sourceHead"]}')
PYCODE
