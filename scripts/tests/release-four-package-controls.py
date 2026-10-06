#!/usr/bin/env python3
"""Synthetic, no-network controls for actual workflow and both-version occupancy."""
import contextlib,importlib.util,io,json,tempfile,urllib.error,zipfile
from pathlib import Path
from unittest.mock import patch
repo=Path(__file__).parents[2]
def load(name,path):
    spec=importlib.util.spec_from_file_location(name,path);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module
static=load('static',repo/'scripts/check-release-custody.py')
occupancy=load('occupancy',repo/'scripts/check-sdd-release-occupancy.py')
text=(repo/'.github/workflows/release.yml').read_text()
count=0
def verdict(action,expected):
    global count
    try:action()
    except (ValueError,FileNotFoundError):
        assert not expected
    else:assert expected
    count+=1
verdict(lambda:static.check(text),True)
for old,new in [
 ('needs: [resolve-versions, contracts-tests, artifacts-tests, cli-tests, knowledge-tests]','needs: [resolve-versions, artifacts-tests, cli-tests, knowledge-tests]'),
 ("needs.resolve-versions.outputs.push == 'true'","needs.resolve-versions.outputs.push == 'false'"),
 ('contracts_version=', 'forgotten_contract='),
 ('dotnet pack src/FS.GG.Contracts/FS.GG.Contracts.fsproj','dotnet pack WRONG.fsproj'),
 ('run-id: ${{ needs.locate-artifacts.outputs.candidate_run_id }}','run-id: 42'),
 ('sha256sum --check --strict pre-push.sha256','true # ignored digest'),
 ('dotnet nuget push "artifacts/packages/FS.GG.Contracts.${{ needs.resolve-versions.outputs.contracts_version }}.nupkg"','dotnet nuget push "artifacts/packages/OTHER.nupkg"'),
 ('--contracts-version','--ignored-version')]:
    assert old in text
    verdict(lambda old=old,new=new:static.check(text.replace(old,new)),False)
verdict(lambda:static.check(text.replace('  publish-cli:\n','  publish-cli:\n    run: dotnet pack extra.fsproj\n')),False)

def package(package_id,version,body=b'fixture',signature=None):
    buf=io.BytesIO()
    with zipfile.ZipFile(buf,'w') as z:
        z.writestr(package_id+'.nuspec',f'<package><metadata><id>{package_id}</id><version>{version}</version></metadata></package>')
        z.writestr('lib/fixture.dll',body)
        if signature:z.writestr('.signature.p7s',signature)
    return buf.getvalue()
ids=['FS.GG.Contracts','FS.GG.SDD.Artifacts','FS.GG.SDD.Cli','FS.GG.SDD.Knowledge']
versions={i:'7.6.0' if i=='FS.GG.Contracts' else '2.2.0' for i in ids}
responses={}
def seed():
    responses.clear();responses['https://fixture/index']=(200,b'{}')
    for i in ['FS.GG.SDD.Artifacts','FS.GG.SDD.Cli']:
        for f in ['org','public']:
            lower=i.lower();responses[f'https://fixture/{f}/{lower}/2.0.3/{lower}.2.0.3.nupkg']=(200,package(i,'2.0.3'))
    for i in ids:
        responses[f'https://fixture/api/orgs/FS-GG/packages/nuget/{i.lower()}/versions?per_page=100&page=1']=(200,b'[]')
class Opener:
    def open(self,request,timeout):
        status,raw=responses.get(request.full_url,(404,b''))
        if status!=200:raise urllib.error.HTTPError(request.full_url,status,'synthetic',None,None)
        return io.BytesIO(raw)
def check(expected,candidate=None,contracts='7.6.0',token='synthetic'):
    with patch.object(occupancy.urllib.request,'build_opener',return_value=Opener()), contextlib.redirect_stdout(io.StringIO()):
        verdict(lambda:occupancy.preflight('2.2.0',ids,'https://fixture/index','https://fixture/org','https://fixture/public',token,'fixture',candidate,contracts,github_api='https://fixture/api'),expected)
seed();check(True);check(False,token='');check(False,contracts=None);check(False,contracts='7.6-preview')
api='https://fixture/api/orgs/FS-GG/packages/nuget/fs.gg.contracts/versions?per_page=100&page=1'
responses[api]=(403,b'');check(False)
responses[api]=(404,b'');check(False)
responses[api]=(200,b'{"not":"population"}');check(False)
responses[api]=(200,b'[{"name":"7.6.0"}]');check(False)
seed()
with tempfile.TemporaryDirectory() as td:
    root=Path(td)
    for i in ids:
        raw=package(i,versions[i]);(root/f'{i}.{versions[i]}.nupkg').write_bytes(raw)
        for f in ['org','public']:
            lower=i.lower();responses[f'https://fixture/{f}/{lower}/{versions[i]}/{lower}.{versions[i]}.nupkg']=(200,raw)
    check(True,root)
    key='https://fixture/public/fs.gg.contracts/7.6.0/fs.gg.contracts.7.6.0.nupkg'
    responses[key]=(200,package('FS.GG.Contracts','7.6.0',signature=b'signing-envelope'));check(True,root)
    responses[key]=(200,package('FS.GG.Contracts','7.6.0',body=b'conflict'));check(False,root)
    responses[key]=(403,b'');check(False,root)
seed()
for page in range(1,11):
    responses[api.replace('&page=1',f'&page={page}')]=(200,json.dumps([{'name':f'0.0.{i}'} for i in range(100)]).encode())
check(False)
print(f'four-package source controls: {count} passed; zero live feed/package execution')
