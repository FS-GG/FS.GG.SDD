#!/usr/bin/env python3
"""One isolated cold package consumer; no producer builds, pack or publication."""
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import urllib.request
import xml.etree.ElementTree as ET
import zipfile
from package_checks import check_package_folders, check_package_hashes

SOURCE = Path(__file__).resolve().parent
DEST = Path(sys.argv[1]).resolve()
DEST.mkdir(parents=True, exist_ok=False)
STARTED = time.monotonic()
STAGES = []
EXPECTED = json.loads((SOURCE / 'expected-archives.json').read_text())
INSPECTION = json.loads((SOURCE / 'inspection.json').read_text())


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def write_json(name, value):
    (DEST / name).write_text(json.dumps(value, indent=2, sort_keys=True) + '\n')


def archive_identity(data, name, selected):
    require(hashlib.sha256(data).hexdigest() == selected['sha256'], f'{name}: raw archive SHA256 mismatch')
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        specs = [p for p in archive.namelist() if p.endswith('.nuspec')]
        require(len(specs) == 1, f'{name}: ambiguous nuspec')
        metadata = ET.fromstring(archive.read(specs[0]))
        fields = {e.tag.split('}')[-1]: e.text for e in metadata.iter()}
        require(fields['id'].lower() == name.lower() and fields['version'] == selected['version'], f'{name}: nuspec identity mismatch')
        if name.startswith('FS.GG.SDD.'):
            repository = next(e for e in metadata.iter() if e.tag.split('}')[-1] == 'repository')
            require(repository.get('commit') == INSPECTION['candidateSource'], f'{name}: source binding mismatch')
    return data


def run(args, cwd, env, label, seconds=180):
    # Deadline stays bounded even if a dependent subprocess stalls.
    remaining = min(seconds, 600 - (time.monotonic() - STARTED))
    require(remaining > 0, 'qualification elapsed budget exhausted')
    STAGES.append({'stage': label, 'command': args, 'status': 'running'})
    with (DEST / (label + '.log')).open('wb') as log:
        result = subprocess.run(args, cwd=cwd, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=remaining, check=False)
    STAGES[-1].update(status='passed' if result.returncode == 0 else 'failed', exitCode=result.returncode)
    print((DEST / (label + '.log')).read_text(), flush=True)
    require(result.returncode == 0, f'{label}: exit {result.returncode}')


def qualify():
    consumer = DEST / 'consumer'
    consumer.mkdir()
    for name in ['Consumer.fsproj', 'Program.fs', 'policy.json', 'inspection.json', 'expected-archives.json']:
        shutil.copyfile(SOURCE / name, consumer / name)
    # No parent repository props/targets or project references enter this copied consumer.
    require('ProjectReference' not in (consumer / 'Consumer.fsproj').read_text(), 'source project reference present')
    feed = DEST / 'selected-feed'
    feed.mkdir()
    env = os.environ.copy()
    env.update(DOTNET_CLI_HOME=str(DEST / 'dotnet-home'), NUGET_PACKAGES=str(DEST / 'packages'), NUGET_HTTP_CACHE_PATH=str(DEST / 'http-cache'), DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_PROCESSOR_COUNT='1', MSBUILDDISABLENODEREUSE='1')
    run(['dotnet', '--version'], consumer, env, 'sdk-version', 20)
    require((DEST / 'sdk-version.log').read_text().strip() == '10.0.401', 'unexpected SDK')
    blob = subprocess.check_output(['gh', 'api', 'repos/FS-GG/FS.GG.SDD/actions/artifacts/11626413573/zip'], timeout=60)
    require(hashlib.sha256(blob).hexdigest() == INSPECTION['artifactZipSha256'], 'original candidate artifact ZIP digest mismatch')
    (DEST / 'original-candidate.zip').write_bytes(blob)
    with zipfile.ZipFile(io.BytesIO(blob)) as original:
        for name in ['FS.GG.SDD.Commands', 'FS.GG.SDD.Artifacts', 'FS.GG.SDD.Knowledge', 'FS.GG.Contracts']:
            prefix = 'dependencies/' if name == 'FS.GG.Contracts' else ''
            filename = f'{name}.{EXPECTED[name]["version"]}.nupkg'
            (feed / filename).write_bytes(archive_identity(original.read(prefix + filename), name, EXPECTED[name]))
    for name, selected in EXPECTED.items():
        filename = f'{name}.{selected["version"]}.nupkg'
        if (feed / filename).exists():
            continue
        url = f'https://api.nuget.org/v3-flatcontainer/{name.lower()}/{selected["version"]}/{filename.lower()}'
        with urllib.request.urlopen(url, timeout=30) as response:
            data = response.read()
        (feed / filename).write_bytes(archive_identity(data, name, selected))
    write_json('selected-archives.json', EXPECTED)
    config = ET.Element('configuration')
    sources = ET.SubElement(config, 'packageSources')
    ET.SubElement(sources, 'clear')
    ET.SubElement(sources, 'add', key='exact-local-closure', value=str(feed))
    ET.ElementTree(config).write(consumer / 'NuGet.Config', encoding='unicode')
    run(['dotnet', 'restore', 'Consumer.fsproj', '--configfile', 'NuGet.Config', '--no-http-cache', '-p:NuGetAudit=false'], consumer, env, 'cold-restore')
    assets = json.loads((consumer / 'obj/project.assets.json').read_text())
    lock = json.loads((consumer / 'packages.lock.json').read_text())
    actual = {key.rsplit('/', 1)[0].lower(): key.rsplit('/', 1)[1] for key, value in assets['libraries'].items() if value['type'] == 'package'}
    require(actual == {n.lower(): v['version'] for n, v in EXPECTED.items()}, f'actual package closure differs: {actual}')
    require(all(v['type'] == 'package' for v in assets['libraries'].values()), 'nonpackage asset dependency')
    check_package_folders(assets['packageFolders'], DEST / 'packages')
    recorded = {}
    locks = next(iter(lock['dependencies'].values()))
    for name, selected in EXPECTED.items():
        package_root = DEST / 'packages' / name.lower() / selected['version']
        raw = package_root / f'{name.lower()}.{selected["version"]}.nupkg'
        raw_bytes = archive_identity(raw.read_bytes(), name, selected)
        entry = next(v for k, v in locks.items() if k.lower() == name.lower())
        raw_sha512 = (package_root / (raw.name + '.sha512')).read_text().strip()
        metadata = json.loads((package_root / '.nupkg.metadata').read_text())
        hashes = check_package_hashes(raw_bytes, raw_sha512, metadata, entry, selected['version'])
        recorded[name] = dict(selected, **hashes)
    write_json('resolved-closure.json', recorded)
    run(['dotnet', 'build', 'Consumer.fsproj', '-c', 'Release', '--no-restore', '-m:1', '-p:UseSharedCompilation=false', '-nodeReuse:false'], consumer, env, 'consumer-build')
    api_output = DEST / 'api'
    run(['dotnet', 'bin/Release/net10.0/Consumer.dll', str(api_output), 'policy.json'], consumer, env, 'loader-api-controls', 60)
    require(json.loads((api_output / 'api-result.json').read_text())['status'] == 'passed', 'API result did not pass')
    for name, selected in EXPECTED.items():
        archive_identity((feed / f'{name}.{selected["version"]}.nupkg').read_bytes(), name, selected)
    return {'status': 'passed', 'closure': recorded}

GOVERNANCE_SOURCE_HASHES = {'Directory.Build.props': 'f29e3b29afed4f93258003519c5af5676b0a9bfea7623350491f2d60ee082d0c', 'Directory.Build.local.props': '19354b0e33d0f14b0b93a83e5a9abdec27a8c98ab041e071b505ee3ff109265a', 'Directory.Packages.props': '8c25aef7532e4dd983ab8cdc05824f9297e19494b40697069bcabc384ac35bec', 'Directory.Packages.local.props': '7b4896c38eb4d9cac29fc82d6003d8f7f29fce83f63664942af2fc9957f046bc', 'global.json': '4e7d5022b2a5a8d02bb951bb264a4c8613eabc332572a0af5f9743d23f23d84e', 'nuget.config': '09b9b59876281c36a6430dc5ca60aa8d6fa39603235aa6987baaf9d99f575b03'}

def qualify_governance():
    """Qualify one exact downstream source against the already qualified package inputs."""
    source_head = '584f447ddd0f1ff9f932f16f9a28132f2919aca2'
    package_receipt_sha = '3f52c77d0d108aad940b594c2ed896eddd3f349d9ffff1cd683155f3c112c377'
    project = 'tests/FS.GG.Governance.VerifyCommand.Tests/FS.GG.Governance.VerifyCommand.Tests.fsproj'
    test_filter = 'FullyQualifiedName~ProviderContextLoader|FullyQualifiedName~ProviderPlanningLoop|FullyQualifiedName~SurfaceDrift|FullyQualifiedName~ScopeGuard'
    source_packet_sha = '0c6484b87535be51e35007ad1bf203cbf0cda1dd47e1f365a1a5aba8a213b031'
    # The known-good package-only run remains immutable; this mode consumes its receipts.
    blob = subprocess.check_output(['gh', 'api', 'repos/FS-GG/FS.GG.SDD/actions/artifacts/11960638495/zip'], timeout=60)
    require(hashlib.sha256(blob).hexdigest() == package_receipt_sha, 'qualified package receipt ZIP identity mismatch')
    (DEST / 'qualified-package-receipt.zip').write_bytes(blob)
    feed = DEST / 'selected-feed'
    feed.mkdir()
    with zipfile.ZipFile(io.BytesIO(blob)) as retained:
        manifest = json.loads(retained.read('evidence-sha256.json'))
        require(all(hashlib.sha256(retained.read(n)).hexdigest() == h for n, h in manifest.items()), 'qualified receipt manifest mismatch')
        receipt = json.loads(retained.read('result.json'))
        api = json.loads(retained.read('api/api-result.json'))
        require(receipt['status'] == api['status'] == 'passed' and len(api['checks']) == 26 and all(c['status'] == 'passed' for c in api['checks']), 'package API prerequisite incomplete')
        require(receipt['harnessHead'] == 'd3bcd43fb1ff259d0316012fecdd8c1a27faf349', 'package qualification source changed')
        for name, selected in EXPECTED.items():
            filename = f'{name}.{selected["version"]}.nupkg'
            (feed / filename).write_bytes(archive_identity(retained.read('selected-feed/' + filename), name, selected))
    source = DEST / 'governance-source'
    source.mkdir()
    env = os.environ.copy()
    env.update(DOTNET_CLI_HOME=str(DEST / 'dotnet-home'), NUGET_PACKAGES=str(DEST / 'packages'), NUGET_HTTP_CACHE_PATH=str(DEST / 'http-cache'), DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_PROCESSOR_COUNT='1', MSBUILDDISABLENODEREUSE='1')
    run(['git', 'init', '--quiet'], source, env, 'source-init', 20)
    run(['git', 'remote', 'add', 'origin', 'https://github.com/FS-GG/FS.GG.Governance.git'], source, env, 'source-remote', 20)
    run(['git', 'fetch', '--depth=1', 'origin', source_head], source, env, 'source-fetch', 60)
    run(['git', 'checkout', '--detach', source_head], source, env, 'source-checkout', 20)
    observed_head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=source, text=True).strip()
    require(observed_head == source_head, 'Governance source HEAD mismatch')
    # Full downstream checkout stays outside the SDD checkout and retains its own imports.
    require(not source.is_relative_to(SOURCE.parents[2]), 'downstream source inherited SDD parent imports')
    source_hashes = GOVERNANCE_SOURCE_HASHES
    for path, sha in source_hashes.items():
        require(hashlib.sha256((source / path).read_bytes()).hexdigest() == sha, 'Governance source input changed: ' + path)
    run(['dotnet', '--version'], source, env, 'sdk-version', 20)
    require((DEST / 'sdk-version.log').read_text().strip() == '10.0.401', 'unexpected downstream SDK')
    # Enumerate source ProjectReferences without evaluating or changing the producer projects.
    projects = set()
    def visit(relative):
        if relative in projects:
            return
        projects.add(relative)
        target = (source / relative).resolve()
        require(target.is_relative_to(source), 'downstream project reference escaped checkout')
        for edge in ET.parse(target).getroot().iter('ProjectReference'):
            child = (target.parent / edge.get('Include').replace('\\', '/')).resolve()
            require(child.is_relative_to(source), 'external downstream ProjectReference')
            visit(child.relative_to(source).as_posix())
    visit(project)
    require(len(projects) == 64, f'downstream project closure changed: {len(projects)}')
    write_json('source-selection.json', {'repository': 'FS-GG/FS.GG.Governance', 'head': source_head, 'packetSha256': source_packet_sha, 'projectClosure': sorted(projects), 'inputHashes': source_hashes})
    config = ET.Element('configuration')
    sources = ET.SubElement(config, 'packageSources')
    ET.SubElement(sources, 'clear')
    ET.SubElement(sources, 'add', key='selected-local', value=str(feed))
    ET.SubElement(sources, 'add', key='public', value='https://api.nuget.org/v3/index.json')
    mapping = ET.SubElement(config, 'packageSourceMapping')
    local = ET.SubElement(mapping, 'packageSource', key='selected-local')
    for name in EXPECTED:
        ET.SubElement(local, 'package', pattern=name)
    public = ET.SubElement(mapping, 'packageSource', key='public')
    ET.SubElement(public, 'package', pattern='*')
    selected_config = DEST / 'NuGet.Config'
    ET.ElementTree(config).write(selected_config, encoding='unicode')
    before_locks = {p: hashlib.sha256((source / p).read_bytes()).hexdigest() for p in [str(Path(n).parent / 'packages.lock.json') for n in projects] if (source / p).exists()}
    write_json('source-locks-before.json', before_locks)
    run(['dotnet', 'restore', project, '--configfile', str(selected_config), '--force-evaluate', '--no-http-cache', '-p:RestoreLockedMode=false', '-p:NuGetAudit=false', '-p:DisableImplicitLibraryPacksFolder=true'], source, env, 'governance-restore')
    # Preserve assets and lock changes before compile; do not bless or commit regenerated locks.
    changed = subprocess.check_output(['git', 'diff', '--name-only'], cwd=source, text=True).splitlines()
    allowed_locks = {'src/FS.GG.Governance.VerifyCommand/packages.lock.json', 'tests/FS.GG.Governance.VerifyCommand.Tests/packages.lock.json'}
    write_json('source-lock-changes.json', changed)
    asset_errors = []
    if not set(changed) <= allowed_locks:
        asset_errors.append({'stage': 'lock-scope', 'error': 'changes outside selected scope', 'paths': changed})
    selected_seen = set()
    config_bindings = []
    asset_records = []
    expected_config = source / 'src/FS.GG.Governance.Config/FS.GG.Governance.Config.fsproj'
    for relative in sorted(projects):
        try:
            directory = (source / relative).parent
            asset_path = directory / 'obj/project.assets.json'
            assets = json.loads(asset_path.read_text())
            check_package_folders(assets['packageFolders'], DEST / 'packages')
            libraries = assets['libraries']
            record = {'project': relative, 'libraries': {n: v['type'] for n, v in libraries.items()}}
            for identity, library in libraries.items():
                name, version = identity.rsplit('/', 1)
                require(library['type'] in ['package', 'project'], 'unexpected downstream dependency kind')
                if name.lower() == 'fs.gg.governance.config':
                    require(library['type'] == 'project', 'Governance Config source substituted by package')
                    binding = (directory / library['msbuildProject']).resolve()
                    require(binding == expected_config, 'unexpected Config project binding')
                    config_bindings.append(relative)
                selected_name = next((n for n in EXPECTED if n.lower() == name.lower()), None)
                if selected_name is None or library['type'] != 'package':
                    continue
                selected = EXPECTED[selected_name]
                require(version == selected['version'], 'selected package version drift: ' + name)
                cache = DEST / 'packages' / name.lower() / version
                raw = cache / f'{name.lower()}.{version}.nupkg'
                archive_identity(raw.read_bytes(), selected_name, selected)
                metadata = json.loads((cache / '.nupkg.metadata').read_text())
                require(Path(metadata['source']).resolve() == feed, 'selected package escaped exclusive local feed: ' + name)
                lock = json.loads((directory / 'packages.lock.json').read_text())
                entry = next(e for group in lock['dependencies'].values() for n, e in group.items() if n.lower() == name.lower())
                record.setdefault('selectedHashes', {})[selected_name] = check_package_hashes(raw.read_bytes(), (cache / (raw.name + '.sha512')).read_text().strip(), metadata, entry, version)
                selected_seen.add(selected_name)
            asset_records.append(record)
        except Exception as error:
            asset_errors.append({'project': relative, 'error': repr(error)})
    for required in [project, 'src/FS.GG.Governance.VerifyCommand/FS.GG.Governance.VerifyCommand.fsproj']:
        if required not in config_bindings:
            asset_errors.append({'project': required, 'error': 'Config source project binding absent'})
    if not {'FS.GG.SDD.Commands', 'FS.GG.SDD.Artifacts', 'FS.GG.SDD.Knowledge'} <= selected_seen:
        asset_errors.append({'stage': 'package-join', 'error': 'SDD package join absent'})
    write_json('downstream-assets.json', {'projects': asset_records, 'selectedPackageIds': sorted(selected_seen), 'configProjectConsumers': config_bindings, 'errors': asset_errors})
    require(not asset_errors, 'downstream restore/asset prerequisites failed; see downstream-assets.json')
    run(['dotnet', 'build', project, '-c', 'Release', '--no-restore', '-m:1', '-p:UseSharedCompilation=false', '-nodeReuse:false'], source, env, 'governance-build', 360)
    tests = DEST / 'tests'
    run(['dotnet', 'test', project, '-c', 'Release', '--no-build', '--no-restore', '--filter', test_filter, '--logger', 'trx;LogFileName=verify-provider-focused.trx', '--results-directory', str(tests)], source, env, 'governance-focused-tests', 120)
    trx = ET.parse(tests / 'verify-provider-focused.trx').getroot()
    rows = [e.attrib for e in trx.iter() if e.tag.split('}')[-1] == 'UnitTestResult']
    families = {}
    for family, minimum in [('ProviderContextLoader', 11), ('ProviderPlanningLoop', 8), ('SurfaceDrift', 1), ('ScopeGuard', 1)]:
        matches = [r for r in rows if family in r.get('testName', '')]
        require(len(matches) >= minimum and all(r.get('outcome') == 'Passed' for r in matches), f'{family}: missing, failed or skipped selected tests')
        families[family] = len(matches)
    require(rows and all(r.get('outcome') == 'Passed' for r in rows), 'focused TRX has nonpassing outcomes')
    write_json('focused-test-counts.json', families)
    for name, selected in EXPECTED.items():
        archive_identity((feed / f'{name}.{selected["version"]}.nupkg').read_bytes(), name, selected)
    return {'status': 'passed', 'downstreamSource': source_head, 'focusedTestCounts': families}



result = {'schema': ('fsgg.gov423.governance-source-run/v1' if '--governance' in sys.argv[2:] else 'fsgg.gov423.loader-package-run/v1'), 'candidate': INSPECTION, 'runId': os.environ.get('GITHUB_RUN_ID'), 'runAttempt': os.environ.get('GITHUB_RUN_ATTEMPT'), 'harnessHead': os.environ.get('GITHUB_SHA'), 'status': 'failed', 'providerExecution': False}
try:
    result.update(qualify_governance() if '--governance' in sys.argv[2:] else qualify())
except Exception as error:
    result['failure'] = repr(error)
    print(repr(error), file=sys.stderr)
finally:
    result['stages'] = STAGES
    result['elapsedSeconds'] = time.monotonic() - STARTED
    write_json('result.json', result)
    manifest = {str(p.relative_to(DEST)): hashlib.sha256(p.read_bytes()).hexdigest() for p in DEST.rglob('*') if p.is_file()}
    write_json('evidence-sha256.json', manifest)
    if '--governance' in sys.argv[2:]:
        # NuGet HTTP-cache names may contain ':'; retain them inside one portable upload name.
        shutil.make_archive(str(DEST.parent / (DEST.name + '-bundle')), 'gztar', root_dir=DEST)
sys.exit(0 if result['status'] == 'passed' else 1)
