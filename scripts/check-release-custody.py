#!/usr/bin/env python3
"""Cheap preparation checks over the actual four-package producer workflow."""
import re
from pathlib import Path


def check(text):
    starts=list(re.finditer(r"^  ([a-z][a-z-]+):\n",text,re.M))
    jobs={m[1]:text[m.start():starts[i+1].start() if i+1<len(starts) else len(text)] for i,m in enumerate(starts)}
    def require(ok,message):
        if not ok: raise ValueError(message)
    candidate=jobs.get("publish-artifacts", "")
    publish=jobs.get("publish-cli", "")
    require("publish-contracts" not in jobs,"Contracts has a second independent publisher")
    require("needs: [resolve-versions, contracts-tests, artifacts-tests, cli-tests, knowledge-tests]" in candidate,"candidate lacks a package test prerequisite")
    require("needs.resolve-versions.outputs.push == 'false'" in candidate,"candidate is not no-push only")
    require("dotnet nuget push" not in candidate and "NuGet/login" not in candidate,"candidate performs publication")
    require(text.count("dotnet pack src/FS.GG.Contracts/FS.GG.Contracts.fsproj")==1 and "dotnet pack src/FS.GG.Contracts/FS.GG.Contracts.fsproj" in candidate,"Contracts must pack once only in candidate")
    require(candidate.count("scripts/verify-release-candidate.sh")==2,"archives are not rechecked after package qualification")
    require("dotnet pack" not in publish,"publisher repacks")
    require("release-candidate/v3" in candidate and "contracts_version=" in candidate and "packages=FS.GG.Contracts,FS.GG.SDD.Artifacts,FS.GG.SDD.Cli,FS.GG.SDD.Knowledge" in candidate,"candidate does not bind all four packages and both versions")
    require("needs.resolve-versions.outputs.push == 'true'" in publish,"publisher is not effect-gated")
    require("run-id: ${{ needs.locate-artifacts.outputs.candidate_run_id }}" in publish,"publisher loses retained run custody")
    for job in [candidate,publish]:
        require('scripts/verify-release-candidate.sh' in job and '"${{ needs.resolve-versions.outputs.contracts_version }}"' in job,"candidate verification loses independent Contracts version")
        require('scripts/check-sdd-release-occupancy.py' in job and '--contracts-version' in job,"occupancy excludes Contracts")
    first=publish.index('dotnet nuget push')
    require(publish.index('scripts/verify-release-candidate.sh')<first and publish.index('scripts/check-sdd-release-occupancy.py')<first,"effects precede source/hash/occupancy gates")
    org=publish.index('--source https://nuget.pkg.github.com/FS-GG/index.json')
    login=publish.index('uses: NuGet/login@v1')
    public=publish.index('--source https://api.nuget.org/v3/index.json')
    require(org<login<public,"public feed does not follow org success")
    require(publish.index('sha256sum --check --strict pre-push.sha256')<public,"public effect lacks retained hash recheck")
    path='artifacts/packages/FS.GG.Contracts.${{ needs.resolve-versions.outputs.contracts_version }}.nupkg'
    require(publish.count('dotnet nuget push "'+path+'"')==2,"Contracts feed legs do not use the same exact retained archive")
    require(publish.count('dotnet nuget push')==8,"four-member dual-feed effect population drift")
    require('for id in fs.gg.contracts fs.gg.sdd.artifacts fs.gg.sdd.cli fs.gg.sdd.knowledge; do' in publish and 'package_version="$CONTRACTS_VERSION"' in publish,"readback excludes independent Contracts identity")

if __name__ == "__main__":
    check((Path(__file__).parents[1]/'.github/workflows/release.yml').read_text())
    print('four-package release custody preflight passed (source only)')
