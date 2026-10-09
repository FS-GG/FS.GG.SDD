#!/usr/bin/env python3
"""Cheap preparation checks over four SDD producers and a reused published dependency."""
import re
from pathlib import Path


def check(text):
    starts=list(re.finditer(r"^  ([a-z][a-z-]+):\n",text,re.M))
    jobs={m[1]:text[m.start():starts[i+1].start() if i+1<len(starts) else len(text)] for i,m in enumerate(starts)}
    def require(ok,message):
        if not ok: raise ValueError(message)
    candidate=jobs.get('publish-artifacts','');publish=jobs.get('publish-cli','');resolver=jobs.get('resolve-versions','')
    gates='needs: [resolve-versions, contracts-tests, artifacts-tests, commands-tests, cli-tests, knowledge-tests]'
    require('publish-contracts' not in jobs,'Contracts has an independent publisher')
    require(gates in candidate and gates in jobs.get('locate-artifacts',''),'candidate lacks a package/source test prerequisite')
    require('tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj' in jobs.get('commands-tests',''),'Commands authored API/tests are not gated')
    require("needs.resolve-versions.outputs.push == 'false'" in candidate,'candidate is not no-push only')
    require('dotnet nuget push' not in candidate and 'NuGet/login' not in candidate,'candidate performs publication')
    require('dotnet pack src/FS.GG.Contracts' not in text,'reused Contracts must not be packed')
    require('dotnet nuget push "artifacts/packages/FS.GG.Contracts' not in text,'reused Contracts must not be pushed')
    require('FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj -getProperty:Version' in resolver and '"$commands_version"' in resolver,'Commands version not resolved/coherent')
    require("rows != ['[7.6.0]']" in resolver,'resolver loses exact published Contracts package pin')
    require('"$(strip_v "$INPUT_VERSION")" = "$cli_version"' in resolver,'manual version overrides source')
    require(candidate.count('scripts/verify-release-candidate.sh')==3,'dependency recording or archive qualification recheck missing')
    require('--record-contracts' in candidate and 'packages/dependencies/FS.GG.Contracts.' in candidate,'published dependency custody missing')
    require('dotnet pack' not in publish,'publisher repacks')
    require('release-candidate/v4' in candidate and 'contracts_version=' in candidate and 'packages=FS.GG.SDD.Artifacts,FS.GG.SDD.Commands,FS.GG.SDD.Cli,FS.GG.SDD.Knowledge' in candidate,'candidate inventory/schema/source identity drift')
    require("needs.resolve-versions.outputs.push == 'true'" in publish,'publisher is not effect-gated')
    require('run-id: ${{ needs.locate-artifacts.outputs.candidate_run_id }}' in publish,'publisher loses retained run custody')
    require('expected exactly one retained no-push candidate' in jobs.get('locate-artifacts',''),'candidate lookup is not unique/same-head')
    for job in [candidate,publish]:
        require('scripts/verify-release-candidate.sh' in job and '"${{ needs.resolve-versions.outputs.contracts_version }}"' in job,'candidate verification loses reused Contracts version')
        require('scripts/check-sdd-release-occupancy.py' in job and '--contracts-version' in job,'both-feed occupancy/dependency qualification missing')
    require('bash scripts/verify-sdd-package-api.sh' in candidate,'actual API/cold Commands consumer qualification missing')
    first=publish.index('dotnet nuget push')
    require(publish.index('scripts/verify-release-candidate.sh')<first and publish.index('scripts/check-sdd-release-occupancy.py')<first,'effects precede source/hash/occupancy gates')
    require(publish.index('--source https://nuget.pkg.github.com/FS-GG/index.json')<publish.index('uses: NuGet/login@v1')<publish.index('--source https://api.nuget.org/v3/index.json'),'public feed does not follow org success')
    require(publish.index('sha256sum --check --strict pre-push.sha256')<publish.index('--source https://api.nuget.org/v3/index.json'),'public effect lacks retained hash recheck')
    require(publish.count('dotnet nuget push')==8,'four-member dual-feed effect population drift')
    for package in ['Artifacts','Commands','Cli','Knowledge']:
        require(publish.count(f'dotnet nuget push "artifacts/packages/FS.GG.SDD.{package}.*.nupkg"')==2,f'{package} feed legs lose exact retained archive')
    require('for id in fs.gg.sdd.artifacts fs.gg.sdd.commands fs.gg.sdd.cli fs.gg.sdd.knowledge; do' in publish,'readback excludes an SDD member')
    require('artifacts/packages/contracts-reuse.json' in publish and 'artifacts/packages/dependencies/*.nupkg' in publish,'durable receipt loses dependency provenance')

if __name__=='__main__':
    check((Path(__file__).parents[1]/'.github/workflows/release.yml').read_text())
    print('four-SDD-package custody/reused dependency preflight passed (source only)')
