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
    require(list(assets['packageFolders']) == [str(DEST / 'packages') + '/'], 'unexpected package cache')
    recorded = {}
    locks = next(iter(lock['dependencies'].values()))
    for name, selected in EXPECTED.items():
        package_root = DEST / 'packages' / name.lower() / selected['version']
        raw = package_root / f'{name.lower()}.{selected["version"]}.nupkg'
        archive_identity(raw.read_bytes(), name, selected)
        entry = next(v for k, v in locks.items() if k.lower() == name.lower())
        normalized = (package_root / (raw.name + '.sha512')).read_text().strip()
        require(entry['resolved'] == selected['version'] and entry['contentHash'] == normalized, f'{name}: restored content hash mismatch')
        recorded[name] = dict(selected, nugetContentHash=entry['contentHash'])
    write_json('resolved-closure.json', recorded)
    run(['dotnet', 'build', 'Consumer.fsproj', '-c', 'Release', '--no-restore', '-m:1', '-p:UseSharedCompilation=false', '-nodeReuse:false'], consumer, env, 'consumer-build')
    api_output = DEST / 'api'
    run(['dotnet', 'bin/Release/net10.0/Consumer.dll', str(api_output), 'policy.json'], consumer, env, 'loader-api-controls', 60)
    require(json.loads((api_output / 'api-result.json').read_text())['status'] == 'passed', 'API result did not pass')
    for name, selected in EXPECTED.items():
        archive_identity((feed / f'{name}.{selected["version"]}.nupkg').read_bytes(), name, selected)
    return {'status': 'passed', 'closure': recorded}


result = {'schema': 'fsgg.gov423.loader-package-run/v1', 'candidate': INSPECTION, 'runId': os.environ.get('GITHUB_RUN_ID'), 'runAttempt': os.environ.get('GITHUB_RUN_ATTEMPT'), 'harnessHead': os.environ.get('GITHUB_SHA'), 'status': 'failed', 'providerExecution': False}
try:
    result.update(qualify())
except Exception as error:
    result['failure'] = repr(error)
    print(repr(error), file=sys.stderr)
finally:
    result['stages'] = STAGES
    result['elapsedSeconds'] = time.monotonic() - STARTED
    write_json('result.json', result)
    manifest = {str(p.relative_to(DEST)): hashlib.sha256(p.read_bytes()).hexdigest() for p in DEST.rglob('*') if p.is_file()}
    write_json('evidence-sha256.json', manifest)
sys.exit(0 if result['status'] == 'passed' else 1)
