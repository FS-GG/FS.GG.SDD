#!/usr/bin/env python3
"""Exercise the installed tool and NuGet-resolved FSI SDK, never source DLLs."""
import json
import hashlib
import os
from pathlib import Path
import subprocess
import sys

scratch = Path(sys.argv[1])
version = sys.argv[2]
cli = scratch / 'tool/fsgg-sdd'
root = scratch / 'workspace'
root.mkdir()


def call(argv, cwd=root):
    return subprocess.run([str(x) for x in argv], cwd=cwd, check=True, text=True, capture_output=True).stdout


def knowledge(*args):
    output = call([cli, 'knowledge', *args, '--root', root])
    return json.loads(output) if output else None


assert version in call([cli, '--version'])
call([cli, 'init', '--root', root])
assert not (root / '.fsgg/knowledge').exists()
call([cli, 'typed-sdd', 'author', '--root', root, '--work', 'first', '--title', 'Installed typed initialization',
      '--agent', 'package-qualification', '--session', 'installed/1', '--backend', 'fsharp-specification-v1'])
initial = knowledge('get', '--id', 'project-knowledge')
assert initial['Record']['Author'] == 'fsgg-sdd'
(root / '.gitignore').write_text((root / '.gitignore').read_text() + '\n.fsgg/**\n')
knowledge('initialize')
call(['git', 'init'])
call(['git', 'add', '.'])
call(['git', '-c', 'user.name=Installed Knowledge', '-c', 'user.email=fixture@example.invalid',
      'commit', '-m', 'Normal initial typed workspace'])
tracked = call(['git', 'ls-files'])
assert '.fsgg/knowledge/schema.json' in tracked and '.fsgg/knowledge-guide.md' in tracked
finding = dict(initial['Record'], Id='failed-experiment', Kind='experiment', Title='Installed failed experiment',
               Author='package-qualification', Summary='A synthetic assumption failed; retain the outcome and limits.')
path = scratch / 'finding.json';path.write_text(json.dumps(finding))
knowledge('capture', '--record', path)
first = knowledge('get', '--id', 'failed-experiment')
call(['git', 'add', '.'])
call(['git', '-c', 'user.name=Installed Knowledge', '-c', 'user.email=fixture@example.invalid', 'commit', '-m', 'Capture finding'])
old_commit = call(['git', 'rev-parse', 'HEAD']).strip()
finding['Summary'] = 'A corrected assumption passed; retain the earlier failed conclusion in Git.'
path.write_text(json.dumps(finding));knowledge('capture', '--record', path, '--expected', first['Revision'])
call(['git', 'add', '.'])
call(['git', '-c', 'user.name=Installed Knowledge', '-c', 'user.email=fixture@example.invalid', 'commit', '-m', 'Refine finding'])
assert knowledge('get-version', '--id', 'failed-experiment', '--commit', old_commit)['Revision'] == first['Revision']
assert len(knowledge('history', '--id', 'failed-experiment')) == 2
store = root / '.fsgg/knowledge'
script = scratch / 'consumer.fsx'
script.write_text(f'''#r "nuget: FS.GG.SDD.Knowledge, {version}"
open FS.GG.SDD.Knowledge
printfn "CORE %s" typeof<int list>.Assembly.Location
let store = {json.dumps(str(store))}
Store.capture store None {{ Workspace.initialRecord with Id = "sdk-finding"; Author = "package-qualification"; Summary = "Installed SDK finding." }} |> ignore
for v in Store.all store do printfn "VERSION %s %s" v.Record.Id v.Revision
''')
output = call(['dotnet', os.environ['FSGG_KNOWLEDGE_FSI'], '--exec', script], cwd=scratch)
core_provenance = json.loads((scratch / 'core-provenance.json').read_text())
fsi_core = Path(next(line.removeprefix('CORE ') for line in output.splitlines() if line.startswith('CORE ')))
fsi_core_digest = hashlib.sha256(fsi_core.read_bytes()).hexdigest()
assert fsi_core_digest == core_provenance['assets']['lib/netstandard2.0/FSharp.Core.dll']
tool_core = next((scratch / 'tool/.store').rglob('FSharp.Core.dll'))
tool_core_digest = hashlib.sha256(tool_core.read_bytes()).hexdigest()
assert tool_core_digest == core_provenance['assets']['lib/netstandard2.1/FSharp.Core.dll']
expected = knowledge('search')
for item in expected:
    assert 'VERSION ' + item['Record']['Id'] + ' ' + item['Revision'] in output
assert knowledge('get', '--id', 'sdk-finding')['Record']['Summary'] == 'Installed SDK finding.'
call(['git', 'add', '.'])
call(['git', '-c', 'user.name=Installed Knowledge', '-c', 'user.email=fixture@example.invalid', 'commit', '-m', 'Capture SDK finding'])
archive = scratch / 'selected.json';knowledge('export', '--ids', 'failed-experiment,sdk-finding', '--archive', archive)
assert not json.loads(archive.read_text())['HistoryIncluded']
restored = scratch / 'restored';knowledge('restore', '--store', restored, '--archive', archive)
assert len(knowledge('search', '--store', restored)) == 2
bundle = scratch / 'history.bundle';call(['git', 'bundle', 'create', bundle, '--all'])
clone = scratch / 'clone';call(['git', 'clone', bundle, clone])
assert not (clone / '.fsgg/cache').exists()
assert len(json.loads(call([cli, 'knowledge', 'history', '--root', clone, '--id', 'failed-experiment']))) == 2
knowledge('check')
print(json.dumps(dict(outcome='passed', version=version, source=os.environ['FSGG_KNOWLEDGE_SOURCE'],
                     installedCli=True, nugetFsiSdk=True, typedEntry=True, normalInitialGit=True,
                     history=True, selectedRecovery=True, cachelessClone=True,
                     coreProvenance=core_provenance, installedCoreSha256=tool_core_digest,
                     fsiCoreSha256=fsi_core_digest, fsiMode='private-sdk-mirror-with-official-nuget-core')))
