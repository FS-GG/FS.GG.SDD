#!/usr/bin/env bash
# Actual verifier controls with synthetic archives; no build, feed or publication.
set -euo pipefail
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
python3 - "$repo" <<'PY'
from pathlib import Path
import hashlib,io,json,shutil,subprocess,sys,tempfile,zipfile
repo=Path(sys.argv[1]);head='0123456789abcdef0123456789abcdef01234567';version='2.3.0';contracts='7.6.0'
ids=['FS.GG.SDD.Artifacts','FS.GG.SDD.Commands','FS.GG.SDD.Cli','FS.GG.SDD.Knowledge'];dll=b'synthetic published Contracts';commands=b'synthetic Commands'
source='cf2f046a10497a336d6243c9a314c3f91fb15771'
def package(id,year=2020):
    stream=io.BytesIO();v=contracts if id=='FS.GG.Contracts' else version;h=source if id=='FS.GG.Contracts' else head
    dependency='<dependencies><group targetFramework="net10.0"><dependency id="FS.GG.Contracts" version="[7.6.0]" /></group></dependencies>' if id in ['FS.GG.SDD.Artifacts','FS.GG.SDD.Commands'] else ''
    entries={id+'.nuspec':f'<package><metadata><id>{id}</id><version>{v}</version><repository commit="{h}"/>{dependency}</metadata></package>'.encode()}
    if id=='FS.GG.Contracts': entries['lib/net10.0/FS.GG.Contracts.dll']=dll
    elif id=='FS.GG.SDD.Cli': entries.update({'tools/net10.0/any/FS.GG.Contracts.dll':dll,'tools/net10.0/any/FS.GG.SDD.Commands.dll':commands})
    else: entries[f'lib/net10.0/{id}.dll']=commands if id=='FS.GG.SDD.Commands' else b'actual fixture DLL'
    if id=='FS.GG.SDD.Knowledge': entries.update({'api-surface/Store.fsi':b'fixture Store','api-surface/Workspace.fsi':b'fixture Workspace'})
    with zipfile.ZipFile(stream,'w') as z:
        for name,raw in entries.items(): z.writestr(zipfile.ZipInfo(name,(year,1,1,0,0,0)),raw)
    return stream.getvalue()
def rehash(root):
    (root/'pre-push.sha256').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.name+'\n' for p in sorted(root.glob('*.nupkg'))))
with tempfile.TemporaryDirectory() as td:
    work=Path(td);scripts=work/'scripts';scripts.mkdir();raw=package('FS.GG.Contracts')
    for name in ['verify-release-candidate.sh','check-sdd-release-occupancy.py','sdd-release-packages.txt']: shutil.copyfile(repo/'scripts'/name,scripts/name)
    # Substitute only the reviewed identity constants for a disclosed synthetic dependency.
    # The real source retains its fixed published identities; no test switch enters the rail.
    adapter=scripts/'check-sdd-release-occupancy.py'
    adapter.write_text(adapter.read_text().replace('b1df3ebd6251f5b18aaece4dd0c5449a7825dc7056f925cf2516febc35f9dfc5',hashlib.sha256(raw).hexdigest()).replace('91f484d28416c5d860a375a91ed70cdda1d3b6d85d504c15ea21e08a9af727ee',hashlib.sha256(dll).hexdigest()))
    verifier=scripts/'verify-release-candidate.sh'
    def invoke(root,record=False):
        args=['bash',str(verifier)]+(['--record-contracts',str(root),contracts] if record else [str(root),head,version,contracts])
        return subprocess.run(args,capture_output=True,text=True)
    def seed(name,year=2020):
        root=work/name;(root/'dependencies').mkdir(parents=True)
        (root/'dependencies'/f'FS.GG.Contracts.{contracts}.nupkg').write_bytes(raw)
        for id in ids: (root/f'{id}.{version}.nupkg').write_bytes(package(id,year))
        assert invoke(root,True).returncode==0
        (root/'candidate.env').write_text(f'schema=fsgg.sdd.release-candidate/v4\nhead={head}\nversion={version}\ncontracts_version={contracts}\npackages={",".join(ids)}\n')
        rehash(root);return root
    positive=seed('positive');count=0
    def check(label,root,expected):
        global count
        result=invoke(root);assert (result.returncode==0)==expected,(label,result.stdout,result.stderr)
        count+=1;print('PASS',label)
    def changed(label,action):
        root=work/label;shutil.copytree(positive,root);action(root);check(label,root,False)
    def mutate(root,id,entry,new):
        path=root/f'{id}.{version}.nupkg'
        with zipfile.ZipFile(path) as z: entries={name:z.read(name) for name in z.namelist()}
        entries[entry]=new(entries[entry])
        with zipfile.ZipFile(path,'w') as z:
            for name,data in entries.items(): z.writestr(name,data)
        rehash(root)
    check('exact four SDD archives plus reused dependency',positive,True)
    changed('head',(lambda r:(r/'candidate.env').write_text((r/'candidate.env').read_text().replace(head,'f'*40))))
    changed('historical-v3',(lambda r:(r/'candidate.env').write_text((r/'candidate.env').read_text().replace('/v4','/v3'))))
    changed('byte-mutation',(lambda r:(r/f'FS.GG.SDD.Cli.{version}.nupkg').write_bytes(b'changed')))
    other=seed('other',2021)
    assert (positive/f'FS.GG.SDD.Cli.{version}.nupkg').read_bytes()!=(other/f'FS.GG.SDD.Cli.{version}.nupkg').read_bytes()
    changed('equal-payload-container-swap',(lambda r:shutil.copyfile(other/f'FS.GG.SDD.Cli.{version}.nupkg',r/f'FS.GG.SDD.Cli.{version}.nupkg')))
    changed('missing-commands',(lambda r:(r/f'FS.GG.SDD.Commands.{version}.nupkg').unlink()))
    changed('extra-release-contracts',(lambda r:shutil.copyfile(r/'dependencies'/f'FS.GG.Contracts.{contracts}.nupkg',r/f'FS.GG.Contracts.{contracts}.nupkg')))
    changed('missing-reused-contracts',(lambda r:(r/'dependencies'/f'FS.GG.Contracts.{contracts}.nupkg').unlink()))
    changed('dependency-source-metadata',(lambda r:(r/'contracts-reuse.json').write_text((r/'contracts-reuse.json').read_text().replace(source,'f'*40))))
    changed('dependency-payload-metadata',(lambda r:(r/'contracts-reuse.json').write_text((r/'contracts-reuse.json').read_text().replace('"payloadSha256": "','"payloadSha256": "x'))))
    changed('rehashed-commands-source',(lambda r:mutate(r,'FS.GG.SDD.Commands','FS.GG.SDD.Commands.nuspec',lambda b:b.replace(head.encode(),b'f'*40))))
    changed('rehashed-commands-dependency',(lambda r:mutate(r,'FS.GG.SDD.Commands','FS.GG.SDD.Commands.nuspec',lambda b:b.replace(b'[7.6.0]',b'[7.5.2]'))))
    changed('rehashed-tool-contracts',(lambda r:mutate(r,'FS.GG.SDD.Cli','tools/net10.0/any/FS.GG.Contracts.dll',lambda _:b'rebuilt equal-version DLL')))
    changed('rehashed-tool-commands',(lambda r:mutate(r,'FS.GG.SDD.Cli','tools/net10.0/any/FS.GG.SDD.Commands.dll',lambda _:b'different Commands')))
    changed('repacked-reused-dependency',(lambda r:(r/'dependencies'/f'FS.GG.Contracts.{contracts}.nupkg').write_bytes(package('FS.GG.Contracts',2021))))
    assert invoke(positive,True).returncode!=0 # Metadata is written once; an existing record is not overwritten.
    print(f'Archive custody: {count} controls passed; metadata overwrite refused; no live package execution.')
PY
