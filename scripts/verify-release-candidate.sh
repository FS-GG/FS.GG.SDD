#!/usr/bin/env bash
# A fresh four-package candidate binds both version lines and the original archives.
set -euo pipefail
[ "$#" -eq 4 ] || { echo "usage: verify-release-candidate.sh <package-dir> <head> <sdd-version> <contracts-version>" >&2; exit 2; }
python3 - "$1" "$2" "$3" "$4" "$(dirname "${BASH_SOURCE[0]}")/sdd-release-packages.txt" <<'PYCODE'
import hashlib,re,sys,zipfile
from pathlib import Path
import xml.etree.ElementTree as E
root,head,version,contracts,inventory=sys.argv[1:]
root=Path(root)
if not re.fullmatch(r'[0-9a-f]{40}',head) or any(not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+',v) for v in [version,contracts]):
    raise SystemExit('invalid candidate source/version identity')
ids=['FS.GG.Contracts']+Path(inventory).read_text().splitlines()
if ids != ['FS.GG.Contracts','FS.GG.SDD.Artifacts','FS.GG.SDD.Cli','FS.GG.SDD.Knowledge']:
    raise SystemExit('reviewed four-package inventory drift')
versions={i: contracts if i=='FS.GG.Contracts' else version for i in ids}
paths={i:f'{i}.{versions[i]}.nupkg' for i in ids}
if sorted(p.name for p in root.glob('*.nupkg'))!=sorted(paths.values()):
    raise SystemExit('release candidate package population/version mismatch')
identity=f'schema=fsgg.sdd.release-candidate/v3\nhead={head}\nversion={version}\ncontracts_version={contracts}\npackages={",".join(ids)}\n'
if (root/'candidate.env').read_text()!=identity:
    raise SystemExit('release candidate identity does not match both source version lines')
lines=(root/'pre-push.sha256').read_text().splitlines()
expected_names=sorted(paths.values())
if len(lines)!=4 or any(not re.fullmatch(r'[0-9a-f]{64}  [A-Za-z0-9.]+\.nupkg',line) for line in lines):
    raise SystemExit('malformed candidate hash population')
if [line[66:] for line in lines]!=expected_names:
    raise SystemExit('candidate hash manifest inventory/version mismatch')
for line in lines:
    if hashlib.sha256((root/line[66:]).read_bytes()).hexdigest()!=line[:64]:
        raise SystemExit('post-handoff archive byte substitution')
with zipfile.ZipFile(root/paths["FS.GG.Contracts"]) as contracts_archive:
    contracts_dll=contracts_archive.read("lib/net10.0/FS.GG.Contracts.dll")
for package_id,path in paths.items():
    with zipfile.ZipFile(root/path) as archive:
        names=archive.namelist();nuspecs=[n for n in names if n.endswith('.nuspec')]
        if len(nuspecs)!=1 or len(names)!=len(set(names)):
            raise SystemExit(f'{path}: ambiguous package entries')
        metadata=E.fromstring(archive.read(nuspecs[0])).find('{*}metadata')
        if metadata is None or metadata.findtext('{*}id')!=package_id or metadata.findtext('{*}version')!=versions[package_id]:
            raise SystemExit(f'{path}: package metadata identity/version mismatch')
        repository=metadata.find('{*}repository')
        if repository is None or repository.get('commit')!=head:
            raise SystemExit(f'{path}: repository source binding mismatch')
        if package_id=='FS.GG.SDD.Artifacts':
            dependencies=[d for d in metadata.findall('.//{*}dependency') if d.get('id')=='FS.GG.Contracts']
            if len(dependencies)!=1 or dependencies[0].get('version','').replace(' ','') not in [contracts,'['+contracts+',)','['+contracts+']']:
                raise SystemExit(f'{path}: independent Contracts dependency join mismatch')
        if package_id=='FS.GG.SDD.Cli':
            member='tools/net10.0/any/FS.GG.Contracts.dll'
            if member not in names or archive.read(member)!=contracts_dll:
                raise SystemExit(f'{path}: tool Contracts bytes differ from retained library')
        if package_id=='FS.GG.SDD.Knowledge':
            required=['api-surface/Store.fsi','api-surface/Workspace.fsi','lib/net10.0/FS.GG.SDD.Knowledge.dll']
        elif package_id=='FS.GG.Contracts':
            required=['api-surface/Provider.fsi','lib/net10.0/FS.GG.Contracts.dll']
        else: required=[]
        if any(n not in names or not archive.read(n) for n in required):
            raise SystemExit(f'{path}: required public contract member missing or empty')
print(f'release candidate verified: head={head} sdd={version} contracts={contracts} packages=4')
PYCODE
