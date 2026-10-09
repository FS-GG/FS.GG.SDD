#!/usr/bin/env python3
"""Synthetic, no-network controls for actual workflow and both-version occupancy."""
import contextlib,hashlib,importlib.util,io,json,os,subprocess,sys,tempfile,urllib.error,zipfile
sys.dont_write_bytecode=True
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
    except (ValueError,FileNotFoundError,FileExistsError):
        assert not expected
    else:assert expected
    count+=1
verdict(lambda:static.check(text),True)
for old,new in [
 ('needs: [resolve-versions, contracts-tests, artifacts-tests, commands-tests, cli-tests, knowledge-tests]','needs: [resolve-versions, artifacts-tests, cli-tests, knowledge-tests]'),
 ("needs.resolve-versions.outputs.push == 'true'","needs.resolve-versions.outputs.push == 'false'"),
 ('contracts_version=', 'forgotten_contract='),
 ('release-candidate/v4','release-candidate/v3'),
 ('--record-contracts','--ignored-contracts'),
 ('run-id: ${{ needs.locate-artifacts.outputs.candidate_run_id }}','run-id: 42'),
 ('sha256sum --check --strict pre-push.sha256','true # ignored digest'),
 ('dotnet nuget push "artifacts/packages/FS.GG.SDD.Commands.*.nupkg"','dotnet nuget push "artifacts/packages/OTHER.nupkg"'),
 ('--contracts-version','--ignored-version'),
 ("rows != ['[7.6.0]']", "rows != ['7.6.0']")]:
    assert old in text
    verdict(lambda old=old,new=new:static.check(text.replace(old,new)),False)
verdict(lambda:static.check(text.replace('  publish-cli:\n','  publish-cli:\n    run: dotnet pack extra.fsproj\n')),False)
verdict(lambda:static.check(text+'\n# dotnet pack src/FS.GG.Contracts/FS.GG.Contracts.fsproj'),False)

def package(package_id,version,body=b'fixture',signature=None):
    buf=io.BytesIO()
    with zipfile.ZipFile(buf,'w') as z:
        z.writestr(package_id+'.nuspec',f'<package><metadata><id>{package_id}</id><version>{version}</version><repository commit="{occupancy.CONTRACTS_SOURCE}" /></metadata></package>')
        z.writestr('lib/net10.0/FS.GG.Contracts.dll' if package_id=='FS.GG.Contracts' else 'lib/fixture.dll',body)
        if signature:z.writestr('.signature.p7s',signature)
    return buf.getvalue()
ids=(repo/'scripts/sdd-release-packages.txt').read_text().splitlines()
versions={i:'2.3.0' for i in ids}
contracts=package('FS.GG.Contracts','7.6.0')
occupancy.CONTRACTS_ARCHIVE_SHA256=hashlib.sha256(contracts).hexdigest()
occupancy.CONTRACTS_DLL_SHA256=hashlib.sha256(b'fixture').hexdigest()
responses={}
def seed():
    responses.clear();responses['https://fixture/index']=(200,b'{}')
    for i in ['FS.GG.SDD.Artifacts','FS.GG.SDD.Knowledge']:
        for f in ['org','public']:
            lower=i.lower();responses[f'https://fixture/{f}/{lower}/2.2.0/{lower}.2.2.0.nupkg']=(200,package(i,'2.2.0'))
    for f in ['org','public']:
        responses[f'https://fixture/{f}/fs.gg.contracts/7.6.0/fs.gg.contracts.7.6.0.nupkg']=(200,contracts)
    for i in ids:
        responses[f'https://fixture/api/orgs/FS-GG/packages/nuget/{i.lower()}/versions?per_page=100&page=1']=(200,b'[]')
class Opener:
    def open(self,request,timeout):
        status,raw=responses.get(request.full_url,(404,b''))
        if status!=200:raise urllib.error.HTTPError(request.full_url,status,'synthetic',None,None)
        return io.BytesIO(raw)
def check(expected,candidate=None,contracts='7.6.0',token='synthetic',observation=None):
    with patch.object(occupancy.urllib.request,'build_opener',return_value=Opener()), contextlib.redirect_stdout(io.StringIO()):
        verdict(lambda:occupancy.preflight('2.3.0',ids,'https://fixture/index','https://fixture/org','https://fixture/public',token,'fixture',candidate,contracts,github_api='https://fixture/api',no_push_observation=observation),expected)
seed();check(True);check(False,token='');check(False,contracts=None);check(False,contracts='7.6-preview')
api='https://fixture/api/orgs/FS-GG/packages/nuget/fs.gg.sdd.commands/versions?per_page=100&page=1'
responses[api]=(403,b'');check(False)
responses[api]=(404,b'');check(False)
responses[api]=(200,b'{"not":"population"}');check(False)
responses[api]=(200,b'[{"name":"2.3.0"}]');check(False)
# Candidate-only qualification retains missing new Commands namespace as Unknown.
# The same observation cannot grant publisher permission or conceal other defects.
with tempfile.TemporaryDirectory() as td:
    root=Path(td)
    seed();responses[api]=(404,b'')
    observed=root/'unknown.json'
    check(True,observation=observed)
    document=json.loads(observed.read_text())
    assert document['mode']=='no-push-candidate' and document['publicationAuthorized'] is False
    assert len(document['observations'])==8
    unknown=[row for row in document['observations'] if row['status']=='Unknown']
    assert len(unknown)==1 and unknown[0]['packageId']=='FS.GG.SDD.Commands' and unknown[0]['feed']=='github'
    assert not any(row['packageId']=='FS.GG.SDD.Commands' and row['feed']=='github' and row['status']=='Absent' for row in document['observations'])
    count+=1
    original=observed.read_bytes();check(False,observation=observed);assert observed.read_bytes()==original;count+=1
    check(False)  # Publisher/default strict path still refuses this exact404.
    check(False,candidate=root,observation=root/'publisher.json')
    assert not (root/'publisher.json').exists();count+=1
    responses[api]=(403,b'');check(False,observation=root/'denied.json');assert not (root/'denied.json').exists();count+=1
    responses[api]=(200,b'{"not":"population"}');check(False,observation=root/'malformed.json')
    # A missing later page is incomplete enumeration, not a new namespace.
    responses[api]=(200,json.dumps([{'name':'2.2.0'}]*100).encode())
    check(False,observation=root/'partial.json');assert not (root/'partial.json').exists();count+=1
    seed();other='https://fixture/api/orgs/FS-GG/packages/nuget/fs.gg.sdd.artifacts/versions?per_page=100&page=1'
    responses[other]=(404,b'');check(False,observation=root/'other.json')
    seed();responses[api]=(404,b'')
    responses['https://fixture/org/fs.gg.sdd.cli/2.3.0/fs.gg.sdd.cli.2.3.0.nupkg']=(200,package('FS.GG.SDD.Cli','2.3.0'))
    check(False,observation=root/'occupied.json');assert not (root/'occupied.json').exists();count+=1
    seed();responses[api]=(200,b'[{"name":"2.3.0"}]');check(False,observation=root/'listed.json')
    seed();check(True,observation=root/'absent.json')
    assert all(row['status']=='Absent' for row in json.loads((root/'absent.json').read_text())['observations']);count+=1
verdict(lambda:static.check(text.replace('--no-push-observation artifacts/packages/occupancy-observation.json','')),False)
verdict(lambda:static.check(text.replace('--candidate artifacts/packages','--candidate artifacts/packages --no-push-observation bad.json')),False)
seed()
with tempfile.TemporaryDirectory() as td:
    root=Path(td)
    (root/'dependencies').mkdir();(root/'dependencies/FS.GG.Contracts.7.6.0.nupkg').write_bytes(contracts)
    for i in ids:
        raw=package(i,versions[i]);(root/f'{i}.{versions[i]}.nupkg').write_bytes(raw)
        for f in ['org','public']:
            lower=i.lower();responses[f'https://fixture/{f}/{lower}/{versions[i]}/{lower}.{versions[i]}.nupkg']=(200,raw)
    check(True,root)
    for member in ids:
        member_key=f'https://fixture/public/{member.lower()}/2.3.0/{member.lower()}.2.3.0.nupkg'
        raw=responses[member_key][1]
        responses[member_key]=(200,package(member,'2.3.0',signature=b'unverified-envelope'))
        with patch.object(occupancy.subprocess,'run',return_value=subprocess.CompletedProcess([],1)):check(False,root)
        with patch.object(occupancy.subprocess,'run',return_value=subprocess.CompletedProcess([],0)) as verify:
            check(True,root)
            assert verify.call_count==1 and verify.call_args.kwargs['timeout']==30
        responses[member_key]=(200,raw)
    key='https://fixture/org/fs.gg.contracts/7.6.0/fs.gg.contracts.7.6.0.nupkg'
    responses[key]=(200,package('FS.GG.Contracts','7.6.0',signature=b'signing-envelope'))
    with patch.object(occupancy.subprocess,'run',return_value=subprocess.CompletedProcess([],0)) as verify:
        check(True,root)
        assert verify.call_count==1 and verify.call_args.kwargs['timeout']==30
        assert verify.call_args.kwargs['stdout']==subprocess.DEVNULL and verify.call_args.kwargs['stderr']==subprocess.DEVNULL
    with patch.object(occupancy.subprocess,'run',return_value=subprocess.CompletedProcess([],1)):check(False,root)
    with patch.object(occupancy.subprocess,'run',side_effect=subprocess.TimeoutExpired('dotnet',30)):check(False,root)
    with patch.object(occupancy.subprocess,'run',side_effect=FileNotFoundError('dotnet')):check(False,root)
    with patch.object(occupancy.subprocess,'run',return_value=subprocess.CompletedProcess([],0)):
        responses[key]=(200,package('FS.GG.Contracts','7.6.0',body=b'conflict',signature=b'valid-envelope'));check(False,root)
    responses[key]=(200,contracts)
    with patch.object(occupancy.subprocess,'run',side_effect=AssertionError('exact raw equality must not launch verifier')):check(True,root)
    responses[key]=(200,package('FS.GG.Contracts','7.6.0',body=b'conflict'));check(False,root)
    responses[key]=(403,b'');check(False,root)
seed()
for page in range(1,11):
    responses[api.replace('&page=1',f'&page={page}')]=(200,json.dumps([{'name':f'0.0.{i}'} for i in range(100)]).encode())
check(False)
print(f'four-package source controls: {count} passed; zero live feed/package execution')

block=text.split('      - name: Resolve versions\n',1)[1].split('  contracts-tests:',1)[0].split('        run: |\n',1)[1]
block='\n'.join(line[10:] if line.startswith('          ') else line for line in block.splitlines())
with tempfile.TemporaryDirectory() as td:
    root=Path(td);bindir=root/'bin';bindir.mkdir()
    stub=bindir/'dotnet';stub.write_text('#!/bin/sh\ncase "$*" in *Commands*) echo "${COMMANDS_VERSION:-2.3.0}";; *) echo 2.3.0;; esac\n');stub.chmod(0o755)
    props=root/'Directory.Packages.local.props'
    props.write_text('<Project><ItemGroup><PackageVersion Include="FS.GG.Contracts" Version="[7.6.0]" /></ItemGroup></Project>')
    for event,version,tag,commands,expected,push in [
        ('workflow_dispatch','','','2.3.0',True,'false'),
        ('workflow_dispatch','v2.3.0','','2.3.0',True,'true'),
        ('workflow_dispatch','7.6.0','','2.3.0',False,''),
        ('push','','v2.3.0','2.3.0',True,'true'),
        ('push','','v7.6.0','2.3.0',False,''),
        ('workflow_dispatch','','','2.2.0',False,'')]:
        output=root/'output';output.write_text('')
        env=dict(os.environ,PATH=str(bindir)+':'+os.environ['PATH'],GITHUB_OUTPUT=str(output),EVENT_NAME=event,INPUT_VERSION=version,RELEASE_TAG=tag,REF_NAME=tag,COMMANDS_VERSION=commands)
        result=subprocess.run(['bash','-c',block],cwd=root,env=env,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
        assert (result.returncode==0)==expected, result.stderr
        if expected:assert 'push='+push in output.read_text() and 'contracts_version=7.6.0' in output.read_text()
        count+=1
    props.write_text('<Project><ItemGroup><PackageVersion Include="FS.GG.Contracts" Version="7.6.0" /></ItemGroup></Project>')
    result=subprocess.run(['bash','-c',block],cwd=root,env=env,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
    assert result.returncode!=0 and b'exact selected published Contracts package pin' in result.stderr
    count+=1
print(f'actual resolver controls included: {count} total passed; stub evaluation only')
