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
    require('--no-push-observation artifacts/packages/occupancy-observation.json' in candidate,
            'no-push candidate does not retain truthful occupancy observations')
    require('--no-push-observation' not in publish, 'publisher uses candidate-only unknown allowance')
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
    occupancy=jobs.get('occupancy-preflight','')
    require('actions: read' in occupancy and 'packages: read' in occupancy
            and 'contents: read' in occupancy and ': write' not in occupancy,
            'occupancy-only job lacks minimal read permissions')
    require('timeout-minutes: 6' in occupancy and 'preflight-retained-occupancy.py' in occupancy,
            'occupancy-only job lacks bounded maintained runner')
    require('dotnet pack' not in occupancy and 'nuget push' not in occupancy
            and 'NuGet/login' not in occupancy, 'occupancy-only job reaches release effects')
    require("github.event_name == 'workflow_dispatch'" in occupancy,
            'occupancy-only mode is not an explicit manual selection')
    for name in ['resolve-versions','contracts-tests','artifacts-tests','commands-tests','cli-tests',
                 'knowledge-tests','publish-artifacts','locate-artifacts','publish-cli','qualify-public','readback-cli']:
        condition=next((line for line in jobs.get(name,'').splitlines() if line.strip().startswith('if:')), '')
        require("inputs.occupancy_candidate_run == ''" in condition
                and "inputs.occupancy_candidate_sha256 == ''" in condition,
                f'{name} can run during occupancy-only selection')
        require(all("inputs.qualify_" + field + " == ''" in condition
                    for field in ['published_version', 'candidate_run', 'candidate_sha256', 'candidate_source']),
                f'{name} can run during published-set qualification')
    require('group: release-coherent-sdd' in text, 'release writers do not share a publication fence')
    for name in ['resolve-versions','contracts-tests','artifacts-tests','commands-tests','cli-tests','knowledge-tests']:
        require('ref: ${{ inputs.promotion_candidate_source || github.sha }}' in jobs[name],
                name + ' does not qualify original retained source')
        if name != 'resolve-versions':
            require('needs: [resolve-versions]' in jobs[name], name + ' launches before mode preflight')
        require('persist-credentials: false' in jobs[name] and 'FSGG_PACKAGES_READ_TOKEN' not in jobs[name],
                name + ' exposes protected credentials to original source')
    for field in ['promotion_candidate_run','promotion_candidate_sha256','promotion_candidate_source']:
        require("inputs." + field + " == ''" in candidate, 'retained promotion can repack')
    require('refs/heads/main' in resolver and 'PYPROMOTION' in resolver,
            'retained promotion lacks explicit protected selection validation')
    locate = jobs.get('locate-artifacts','')
    require('persist-credentials: false' in locate and 'persist-credentials: false' in publish,
            'retained execution exposes persisted protected checkout credentials')
    public = jobs.get('qualify-public', '')
    require('FSGG_SDD_PACKAGE_VERSION:' in locate and 'SVG_WORKSPACE_PACKAGE_VERSION:' in public,
            'retained qualifiers use current verifier version')
    require('--write-sdk-binding "$RUNNER_TEMP/quint-release-tools/fable-sdk-binding.json"' in public
            and 'FABLE_SDK_BINDING=%s' in public and 'scripts/tests/verify-fable-tool.test.py' in public,
            'public receiver gate lacks provisioned Fable SDK binding/controls')
    require('bash scripts/verify-cli-tool.sh' in locate and 'bash scripts/verify-sdd-package-api.sh' in locate
            and 'bash scripts/verify-knowledge-packages.sh' in locate,
            'retained packages miss current release qualification')
    require('--expected-source "$CANDIDATE_SOURCE"' in locate and '--expected-version "$VERSION"' in locate
            and '--archive-sha256 "$CANDIDATE_SHA256"' in locate,
            'retained lookup does not authenticate exact original selection')
    require('--expected-source "$CANDIDATE_SOURCE"' in publish and '--expected-version "$VERSION"' in publish
            and '--archive-sha256 "$CANDIDATE_SHA256"' in publish,
            'publisher does not reauthenticate retained ZIP/source/version')
    require('artifacts/packages "${{ needs.locate-artifacts.outputs.candidate_source }}"' in publish,
            'publisher relabels retained package source')
    require('FSGG_PACKAGES_READ_TOKEN' in occupancy and 'FSGG_PACKAGES_READ_TOKEN' in publish,
            'protected preflights cannot use explicit read authority')
    require('needs: [resolve-versions, locate-artifacts, publish-cli]' in public,
            'public qualification can precede feed writes/readbacks')
    require('ARTIFACT_ID: ${{ needs.publish-cli.outputs.receipt_artifact_id }}' in public
            and 'ARTIFACT_SHA256: ${{ needs.publish-cli.outputs.receipt_digest }}' in public
            and 'scripts/verify-publication-receipt.py' in public
            and 'receipt_artifact_id: ${{ steps.publish-receipt.outputs.artifact-id }}' in publish
            and 'receipt_digest: ${{ steps.publish-receipt.outputs.artifact-digest }}' in publish,
            'read-only qualification loses immutable publisher receipt join')
    require('artifacts/packages/*.nupkg' in publish and 'verify-release-candidate.sh artifacts/qualified-receipt/packages' in public
            and 'scripts/verify-package-payloads.py' in public,
            'public qualification loses original archive/source/eight-feed joins')
    require('contents: read' in public and 'actions: read' in public and 'packages: read' in public
            and ': write' not in public and 'NuGet/login' not in public and public.count('GH_TOKEN:') == 1,
            'public package execution has publication credentials')
    # The denylist above is not a boundary on its own: pin the allowlist. The public job may
    # reference exactly one secret, the read-only workflow token, and no feed/publisher secret.
    require(len(re.findall(r'\bsecrets\b', public)) == 1 and public.count('secrets.GITHUB_TOKEN') == 1
            and 'FSGG_PACKAGES_READ' not in public and 'NUGET_API_KEY' not in public,
            'public job references a secret other than the read-only workflow token')
    require('| tee retained-disposition.json' in locate and '| tee retained-promotion-disposition.json' in publish,
            'refused retained promotion does not surface its typed disposition')
    require('scripts/verify-knowledge-packages.sh' not in publish and 'dotnet tool install FS.GG.SDD.Cli' not in publish
            and 'tests/quint-svg-workspace-installed-acceptance.sh' not in publish,
            'publisher executes installed public candidate with write/OIDC permissions')
    require('scripts/verify-knowledge-packages.sh' in public and 'dotnet tool install FS.GG.SDD.Cli' in public
            and 'tests/quint-svg-workspace-installed-acceptance.sh' in public,
            'read-only public package qualification gate removed')
    require('for id in fs.gg.sdd.artifacts fs.gg.sdd.commands fs.gg.sdd.cli fs.gg.sdd.knowledge; do' in publish,'readback excludes an SDD member')
    require('artifacts/packages/contracts-reuse.json' in publish and 'artifacts/packages/dependencies/*.nupkg' in publish,'durable receipt loses dependency provenance')
    # Read-only qualification of an already-published set: no effect, no OIDC, no feed-read secret, same installed gate.
    qualify = jobs.get('qualify-published', '')
    require(qualify != '' and "github.event_name == 'workflow_dispatch'" in qualify and 'refs/heads/main' in qualify
            and 'PYQUALIFY' in qualify, 'published-set qualification is not an explicit protected-main selection')
    require('contents: read' in qualify and 'actions: read' in qualify and 'packages: read' in qualify
            and ': write' not in qualify and 'id-token' not in qualify and 'NuGet/login' not in qualify
            and 'dotnet nuget push' not in qualify and 'dotnet pack' not in qualify
            and 'persist-credentials: false' in qualify and qualify.count('GH_TOKEN:') == 1,
            'published-set qualification has publication credentials or effects')
    require(len(re.findall(r'\bsecrets\b', qualify)) == 1 and qualify.count('secrets.GITHUB_TOKEN') == 1
            and 'FSGG_PACKAGES_READ' not in qualify and 'NUGET_API_KEY' not in qualify,
            'published-set qualification references a secret other than the read-only workflow token')
    require('scripts/qualify-published-set.py' in qualify and '--source "$QUALIFY_SOURCE"' in qualify
            and '--archive-sha256 "$QUALIFY_SHA256"' in qualify and '--version "$QUALIFY_VERSION"' in qualify
            and 'verify-release-candidate.sh artifacts/qualified-receipt/packages' in qualify
            and 'scripts/verify-package-payloads.py' in qualify and 'published-dual-feed-receipt' in qualify,
            'published-set qualification loses original custody, eight-archive joins or durable receipt')
    selection = next((line for line in qualify.splitlines() if line.strip().startswith('if:')), '')
    require("github.repository == 'FS-GG/FS.GG.SDD'" in selection and "inputs.occupancy_candidate_run == ''" in selection
            and "inputs.occupancy_candidate_sha256 == ''" in selection,
            'published-set qualification lacks the repository guard or can overlap occupancy-only selection')
    require("'PUBLISH_VERSION','READBACK_VERSION','PROMOTION_RUN','PROMOTION_SHA256','PROMOTION_SOURCE'" in qualify
            and 'qualification excludes publish, readback and promotion modes' in qualify,
            'published-set qualification does not refuse mixed modes')
    require('"artifacts/qualified-receipt/feed-readback/fs.gg.sdd.$lower.github.nupkg" \\\n'
            '              "artifacts/qualified-receipt/feed-readback/fs.gg.sdd.$lower.nuget.nupkg"' in qualify
            and 'for id in Artifacts Commands Cli Knowledge; do' in qualify,
            'published-set qualification does not join every package with both feed archives')
    receipt_step = qualify[qualify.index('      - name: Retain the durable dual-feed receipt'):]
    receipt_step = receipt_step[:receipt_step.index('\n      - name:', 10)]
    require('if:' not in receipt_step and 'if-no-files-found: error' in receipt_step,
            'published-set receipt can be retained without passing custody and eight-archive joins')
    def installed_gate(job):
        return job[job.index('      - name: Qualify the public Knowledge CLI and SDK\n'):
                   job.index('      - name: Retain public qualification evidence')]
    require(installed_gate(public).replace('${{ needs.resolve-versions.outputs.cli_version }}',
                                           '${{ inputs.qualify_published_version }}') == installed_gate(qualify),
            'published-set qualification installed gate drifted from the publisher-following public gate')
    require('seq 1 99' in publish and 'sleep $(( attempt < 20 ? 15 : 30 ))' in publish and 'seq 1 40' not in publish,
            'publisher readback wait does not tolerate observed nuget.org indexing lag')

if __name__=='__main__':
    check((Path(__file__).parents[1]/'.github/workflows/release.yml').read_text())
    print('four-SDD-package custody/reused dependency preflight passed (source only)')
