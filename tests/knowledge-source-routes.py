import json, os, pathlib, subprocess, tempfile
repo=pathlib.Path(__file__).resolve().parents[1]
cli=repo/'src/FS.GG.SDD.Cli/bin/Debug/net10.0/FS.GG.SDD.Cli'
env=dict(os.environ,DOTNET_PROCESSOR_COUNT='1')
def call(args,cwd=repo):
 r=subprocess.run([str(x) for x in args],cwd=cwd,env=env,check=True,text=True,capture_output=True)
 return r.stdout
def knowledge(root,*args): return call([cli,'knowledge',*args,'--root',root])
with tempfile.TemporaryDirectory(prefix='knowledge-source-route-') as td:
 base=pathlib.Path(td); root=base/'typed'; root.mkdir()
 registry=repo/'tests/fixtures/scaffold-provider/registries/lifecycle.providers.yml'
 (root/'.fsgg').mkdir()
 (root/'.fsgg/providers.yml').write_text(registry.read_text().replace('__FIXTURE__',str(repo/'tests/fixtures/scaffold-provider')))
 report=json.loads(call([cli,'scaffold','--root',root,'--provider','fixture','--param','productName=Acme','--param','lifecycle=typed-sdd','--no-update']))
 assert report['outcome'] in ['succeeded','succeededWithWarnings'],report
 current=json.loads(knowledge(root,'get','--id','project-knowledge'))
 assert current['Record']['Author']=='fsgg-sdd'
 git=lambda *args: call(['git','-C',root,*args])
 ignore=root/'.gitignore'
 ignore.write_text(ignore.read_text()+'\n.fsgg/**\n')
 knowledge(root,'initialize')
 git('add','.')
 git('-c','user.name=Knowledge Fixture','-c','user.email=knowledge@example.invalid','commit','-m','Normal initial commit')
 tracked=git('ls-files')
 assert '.fsgg/knowledge/schema.json' in tracked and '.fsgg/knowledge/records/project-knowledge.json' in tracked and '.fsgg/knowledge-guide.md' in tracked
 before=(root/'.fsgg/knowledge-guide.md').read_bytes()
 (root/'.fsgg/knowledge-guide.md').write_bytes(before+b'\nProject-owned guidance.\n')
 knowledge(root,'initialize')
 assert (root/'.fsgg/knowledge-guide.md').read_bytes()==before+b'\nProject-owned guidance.\n'
 ids=[]
 for kind in ['architecture','decision','diagnostic','experiment','bug-fix']:
  record=dict(current['Record'],Id=kind,Kind=kind,Author='synthetic-fixture',Title=kind,Summary='Synthetic concise failed-outcome finding for '+kind)
  record['Evidence']=[dict(Locator='docs/evidence.md',Repository='synthetic',Revision='',Path='docs/evidence.md',Digest='',Run='source-fixture/1')]
  path=base/(kind+'.json');path.write_text(json.dumps(record))
  knowledge(root,'capture','--record',path);ids.append(kind)
 versions=json.loads(knowledge(root,'search','--text','Synthetic concise'))
 assert len(versions)==5
 script=call(['dotnet','fsi','--exec',repo/'docs/examples/knowledge.fsx',root/'.fsgg/knowledge','Synthetic concise'])
 assert all(v['Record']['Id'] in script and v['Revision'] in script for v in versions)
 archive=base/'selected.json';knowledge(root,'export','--ids',','.join(ids),'--archive',archive)
 restored=base/'restored'
 knowledge(root,'restore','--store',restored,'--archive',archive)
 assert len(json.loads(knowledge(root,'search','--store',restored,'--text','Synthetic concise')))==5
 git('add','.')
 git('-c','user.name=Knowledge Fixture','-c','user.email=knowledge@example.invalid','commit','-m','Capture findings')
 old_commit=git('rev-parse','HEAD').strip()
 old=json.loads(knowledge(root,'get','--id','experiment'))
 revised=dict(old['Record'],Summary='Synthetic concise failed outcome with refined limits.')
 path=base/'revised.json';path.write_text(json.dumps(revised))
 knowledge(root,'capture','--record',path,'--expected',old['Revision'])
 git('add','.')
 git('-c','user.name=Knowledge Fixture','-c','user.email=knowledge@example.invalid','commit','-m','Refine finding')
 assert len(json.loads(knowledge(root,'history','--id','experiment')))==2
 assert json.loads(knowledge(root,'get-version','--id','experiment','--commit',old_commit))['Revision']==old['Revision']
 bundle=base/'history.bundle';git('bundle','create',bundle,'--all')
 clone=base/'clone';call(['git','clone',bundle,clone])
 assert len(json.loads(knowledge(clone,'history','--id','experiment')))==2
 assert len(json.loads(knowledge(clone,'search','--text','Synthetic concise')))==5
 assert not (clone/'.fsgg/cache').exists()
 generic=base/'generic';generic.mkdir()
 call([cli,'init','--root',generic])
 assert (generic/'.fsgg/knowledge-guide.md').exists() and not (generic/'.fsgg/knowledge').exists()
 call([cli,'typed-sdd','author','--root',generic,'--work','first','--title','First typed specification','--agent','synthetic-fixture','--session','source-route/1','--backend','fsharp-specification-v1'])
 assert json.loads(knowledge(generic,'get','--id','project-knowledge'))['Record']['Id']=='project-knowledge'
 print(json.dumps(dict(outcome='passed',sourceRoutes=['actual-effective-typed-scaffold','generic-init-no-lifecycle-change','generic-init-accepted-typed-author','explicit-knowledge-initialize'],findingKinds=ids,normalInitialGitInclusion=True,apiCliFsxSameVersions=True,selectedExportRestore=True,gitHistoryAndBundleClone=True,cacheRequired=False)))
