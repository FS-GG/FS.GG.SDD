#!/usr/bin/env python3
"""Third-party provider-source journeys against an INSTALLED fsgg-sdd tool.

Builds the deterministic example third-party source from
tests/fixtures/third-party-provider-source, then drives the installed CLI through
local-directory, pinned-HTTPS and offline-cache create journeys, builds and tests
each created product, and runs negative controls. Every CLI call goes through the
maintained runner (scripts/catalog-runtime-validation.py) in native-runtime mode
with the pid-namespace backend, so the process lifetime boundary is the runner's.

This is ordinary development verification on a host that already satisfies the
catalog executor's contract (Linux x64, .NET 10.0.12, glibc 2.44); it is not a
claim about any other host.

  provider-source-journey.py build  --out DIR            # write source dir + envelope.zip + digests.json
  provider-source-journey.py run    --cli PATH --work DIR --https-url URL [--source-root GIT_ROOT]
"""
import argparse
import hashlib
import io
import json
import os
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FIXTURE = ROOT / 'tests/fixtures/third-party-provider-source'
REVISION = '1.0.0'
SOURCE_ID = 'third-party-example'
PROVIDER = 'generic-fixture'
TEMPLATE_SOURCE = 'opaque-source'
DOTNET = '/usr/share/dotnet/dotnet'
FIXED_TIME = (2026, 10, 24, 0, 0, 0)


def sha(data):
    return 'sha256:' + hashlib.sha256(data).hexdigest()


def deterministic_zip(entries):
    """Stored (no compression), fixed timestamps, sorted names, regular-file mode."""
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, 'w', zipfile.ZIP_STORED) as archive:
        for name, data in entries:
            info = zipfile.ZipInfo(name, FIXED_TIME)
            info.external_attr = 0o100644 << 16
            info.create_system = 3
            archive.writestr(info, data)
    return buffer.getvalue()


def template_archive():
    entries = [('FS.GG.SDD.Catalog.OpaqueFixture.nuspec', (FIXTURE / 'template.nuspec').read_bytes())]
    content = FIXTURE / 'template-content'
    for path in sorted(p for p in content.rglob('*') if p.is_file()):
        entries.append(('content/' + path.relative_to(content).as_posix(), path.read_bytes()))
    return deterministic_zip(entries)


def build_source():
    """Return {name: bytes} for the source population and the manifest."""
    catalog = (FIXTURE / 'catalog.json').read_bytes()
    archive = template_archive()
    members = [
        {'path': 'catalog.json', 'bytes': len(catalog), 'sha256': sha(catalog)},
        {'path': 'template.nupkg', 'bytes': len(archive), 'sha256': sha(archive)},
    ]
    manifest = json.dumps({
        'schema': 'fsgg.provider-source/1', 'id': SOURCE_ID, 'revision': REVISION,
        'catalog': 'catalog.json', 'members': members,
        'archives': [{'templateSource': TEMPLATE_SOURCE, 'path': 'template.nupkg'}],
    }, separators=(',', ':')).encode()
    return {'source.json': manifest, 'catalog.json': catalog, 'template.nupkg': archive}


def write_source(out, aux=None):
    """Write exactly the source population to `out`; envelope and digests go to `aux`."""
    out.mkdir(parents=True, exist_ok=True)
    aux = aux or out.parent / (out.name + '-aux')
    aux.mkdir(parents=True, exist_ok=True)
    files = build_source()
    for name, data in files.items():
        (out / name).write_bytes(data)
    envelope = deterministic_zip([(name, files[name]) for name in ('source.json', 'catalog.json', 'template.nupkg')])
    (aux / 'envelope.zip').write_bytes(envelope)
    digests = {'manifestSha256': sha(files['source.json']), 'envelopeSha256': sha(envelope),
               'catalogSha256': sha(files['catalog.json']), 'archiveSha256': sha(files['template.nupkg']),
               'policySha256': sha((FIXTURE / 'policy.json').read_bytes()), 'revision': REVISION}
    (aux / 'digests.json').write_text(json.dumps(digests, indent=2) + '\n')
    return digests


class Journey:
    def __init__(self, args):
        self.cli = args.cli
        self.work = Path(args.work).resolve()
        self.runner = ROOT / 'scripts/catalog-runtime-validation.py'
        self.source_root = Path(args.source_root).resolve()
        self.results = []
        self.counter = 0
        self.environment = {k: os.environ[k] for k in ('NUGET_PACKAGES', 'DOTNET_CLI_HOME', 'TMPDIR',
                                                       'DOTNET_CLI_TELEMETRY_OPTOUT', 'DOTNET_NOLOGO') if k in os.environ}

    def record(self, name, passed, detail=''):
        self.results.append({'name': name, 'passed': bool(passed), 'detail': detail})
        print(('PASS ' if passed else 'FAIL ') + name + (' :: ' + detail if detail else ''), flush=True)

    def run(self, name, argv, cwd=None, timeout=240):
        """Run one literal command through the native-runtime pid-namespace runner."""
        self.counter += 1
        output = self.work / 'runner' / f'{self.counter:02d}-{name}'
        config = {'sourceRoot': str(self.source_root), 'commands': [{
            'name': name, 'mode': 'native-runtime', 'backend': 'pid-namespace', 'argv': argv,
            'cwd': str(cwd or self.work), 'environment': self.environment,
            'timeoutSeconds': timeout, 'cleanupSeconds': 30}]}
        output.parent.mkdir(parents=True, exist_ok=True)
        config_path = output.parent / f'{self.counter:02d}-{name}.json'
        config_path.write_text(json.dumps(config))
        subprocess.run([sys.executable, str(self.runner), '--config', str(config_path), '--output', str(output)],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        result = json.loads((output / 'result.json').read_text())
        command = result['commands'][0]
        stdout = Path(command['streams']['stdout']['log']).read_text(errors='replace')
        stderr = Path(command['streams']['stderr']['log']).read_text(errors='replace')
        return command.get('exitCode'), stdout, stderr, command

    def cli_call(self, name, *args, expect_exit=0, expect_text=None):
        code, out, err, command = self.run(name, [self.cli, *args])
        ok = code == expect_exit and command.get('cleanup') == 'direct-child-reaped-streams-eof'
        if expect_text is not None:
            # A refusal must carry its intended diagnostic, not merely a nonzero exit.
            ok = ok and expect_text in out
        return ok, code, out, err

    def json_of(self, text):
        for line in reversed(text.strip().splitlines()):
            line = line.strip()
            if line.startswith('{'):
                try:
                    return json.loads(line)
                except ValueError:
                    pass
        try:
            return json.loads(text)
        except ValueError:
            return None


def source_set(path, entries):
    path.write_text(json.dumps({'schema': 'fsgg.provider-source-set/1', 'sources': entries}, separators=(',', ':')))
    return sha(path.read_bytes())


def acquisition_policy(path, origins):
    path.write_text(json.dumps({
        'schema': 'fsgg.provider-source-policy/1', 'allowedOrigins': origins, 'maximumRedirects': 0,
        'acquisitionSeconds': 60, 'maximumTransferBytes': 1048576, 'maximumExpandedBytes': 4194304}, separators=(',', ':')))
    return sha(path.read_bytes())


def verify_product(journey, label, target):
    expected = {
        'product.txt': 'raw=Third Party Example\npackage=example.org/third-party\ncode=IndependentCode\nempty=\nliteral=$(literal);& value\n'}
    good = True
    for name, text in expected.items():
        path = target / name
        match = path.is_file() and path.read_text() == text
        journey.record(f'{label}: {name} has the literal parameter bytes', match, '' if match else repr(path.read_text() if path.is_file() else None))
        good &= match
    provenance = target / '.fsgg/scaffold-provenance.json'
    document = json.loads(provenance.read_text()) if provenance.is_file() else {}
    journey.record(f'{label}: schema3 provenance records the source observation',
                   document.get('schemaVersion') == 3 and 'source' in document and 'catalog' in document,
                   f"schemaVersion={document.get('schemaVersion')}")
    return document


def build_and_test(journey, label, target):
    code, out, err, _ = journey.run(f'{label}-build', [DOTNET, 'build', 'src/Product/Product.fsproj', '-c', 'Release', '-nodeReuse:false'], cwd=target, timeout=600)
    journey.record(f'{label}: created product builds', code == 0, f'exit={code}' + ('' if code == 0 else ' ' + (out + err)[-600:]))
    code, out, err, _ = journey.run(f'{label}-test', [DOTNET, 'test', 'tests/Product.Tests/Product.Tests.fsproj', '-nodeReuse:false'], cwd=target, timeout=900)
    passed = code == 0 and 'Passed:     2' in out.replace('Passed:    2', 'Passed:     2') or (code == 0 and 'Passed!' in out)
    journey.record(f'{label}: created product tests pass', passed, f'exit={code}' + ('' if code == 0 else ' ' + (out + err)[-800:]))


def run_journeys(args):
    work = Path(args.work).resolve()
    if work.exists() and any(work.iterdir()):
        raise SystemExit('--work must be a fresh empty directory')
    work.mkdir(parents=True, exist_ok=True)
    journey = Journey(args)
    (work / 'products').mkdir()
    sources = work / 'sources'
    local = sources / 'third-party'
    digests = write_source(local, sources / 'third-party-aux')
    policy = FIXTURE / 'policy.json'
    params = ['--param', 'raw=Third Party Example', '--param', 'packageIdentity=example.org/third-party',
              '--param', 'code=IndependentCode', '--param', 'literal=$(literal);& value', '--param', 'empty=']
    common_exec = ['--provider', PROVIDER, '--admission-policy', str(policy), '--admission-policy-sha256', digests['policySha256'],
                   '--platform', 'linux-x64', '--transport-executable', DOTNET, '--preflight-timeout-seconds', '30',
                   '--scaffold-timeout-seconds', '60', '--json']

    version_ok, _, out, _ = journey.cli_call('version', '--version')
    journey.record('installed CLI reports its version', version_ok, out.strip()[:200])

    # Local journey -------------------------------------------------------------------
    local_set = work / 'local-set.json'
    local_set_sha = source_set(local_set, [{'id': SOURCE_ID, 'origin': 'explicit',
                                           'locator': {'kind': 'local-directory', 'path': str(local)},
                                           'manifestSha256': digests['manifestSha256']}])
    local_policy = work / 'local-acquisition.json'
    local_policy_sha = acquisition_policy(local_policy, [])
    cache = work / 'cache-local'
    cache.mkdir()
    source_args = ['--source-set', str(local_set), '--source-set-sha256', local_set_sha,
                   '--acquisition-policy', str(local_policy), '--acquisition-policy-sha256', local_policy_sha,
                   '--source-cache', str(cache)]
    ok, code, out, err = journey.cli_call('local-inspect', 'catalog', 'inspect', *source_args, '--json')
    report = journey.json_of(out) or {}
    journey.record('local: catalog inspect lists the third-party provider', ok and PROVIDER in out, f'exit={code}')
    target = work / 'products' / 'local'
    ok, code, out, err = journey.cli_call('local-scaffold', 'scaffold', *source_args, *params, '--root', str(target), *common_exec)
    journey.record('local: scaffold from a local-directory source succeeds', ok and target.is_dir(), f'exit={code} {(out + err)[-500:] if not ok else ""}')
    if target.is_dir():
        verify_product(journey, 'local', target)
        build_and_test(journey, 'local', target)
    local_tree = sorted(p.relative_to(target).as_posix() for p in target.rglob('*') if p.is_file() and 'bin/' not in p.as_posix() and 'obj/' not in p.as_posix()) if target.is_dir() else []

    # Offline journey: the original local source is removed ----------------------------
    shutil.rmtree(local)
    offline_target = work / 'products' / 'offline'
    ok, code, out, err = journey.cli_call('offline-scaffold', 'scaffold', *source_args, '--offline', *params, '--root', str(offline_target), *common_exec)
    journey.record('offline: scaffold from the verified cache after the original source was removed', ok and offline_target.is_dir(), f'exit={code} {(out + err)[-500:] if not ok else ""}')
    if offline_target.is_dir():
        offline_tree = sorted(p.relative_to(offline_target).as_posix() for p in offline_target.rglob('*') if p.is_file())
        journey.record('offline: product file set equals the local journey', offline_tree == local_tree)
        differing = [name for name in local_tree
                     if (offline_target / name).read_bytes() != (target / name).read_bytes()]
        # Only the provenance (cache-read vs original acquisition) and the project id derived
        # from the target directory name may differ.
        journey.record('offline: product bytes equal the local journey except provenance and the directory-derived project id',
                       sorted(differing) == ['.fsgg/project.yml', '.fsgg/scaffold-provenance.json'], str(differing))
        document = json.loads((offline_target / '.fsgg/scaffold-provenance.json').read_text())
        journey.record('offline: provenance records cache-read with no network hops',
                       document['source']['acquisition'] == 'cache-read' and document['source']['networkHops'] == [])
        build_and_test(journey, 'offline', offline_target)
    empty_cache = work / 'cache-empty'
    empty_cache.mkdir()
    refuse_target = work / 'products' / 'offline-empty'
    ok, code, out, err = journey.cli_call('offline-empty', 'scaffold', *[a if a != str(cache) else str(empty_cache) for a in source_args],
                                          '--offline', *params, '--root', str(refuse_target), *common_exec, expect_exit=1, expect_text='source.offlineCacheMiss')
    journey.record('offline: an empty cache refuses without creating a target', ok and not refuse_target.exists(), f'exit={code}')

    # Pinned HTTPS journey -------------------------------------------------------------
    if args.https_url:
        https_set = work / 'https-set.json'
        https_set_sha = source_set(https_set, [{'id': SOURCE_ID, 'origin': 'explicit', 'locator': {
            'kind': 'https-envelope', 'uri': args.https_url, 'revision': REVISION, 'envelopeSha256': digests['envelopeSha256']},
            'manifestSha256': digests['manifestSha256']}])
        origin = '/'.join(args.https_url.split('/')[:3])
        https_policy = work / 'https-acquisition.json'
        https_policy_sha = acquisition_policy(https_policy, [origin])
        https_cache = work / 'cache-https'
        https_cache.mkdir()
        https_args = ['--source-set', str(https_set), '--source-set-sha256', https_set_sha,
                      '--acquisition-policy', str(https_policy), '--acquisition-policy-sha256', https_policy_sha,
                      '--source-cache', str(https_cache)]
        ok, code, out, err = journey.cli_call('https-inspect', 'catalog', 'inspect', *https_args, '--json')
        journey.record('https: catalog inspect fetches and lists the pinned envelope', ok and PROVIDER in out, f'exit={code} {(out + err)[-400:] if not ok else ""}')
        # A separate empty cache so the scaffold performs its own genuine fetch (not a cache read).
        fetch_cache = work / 'cache-https-scaffold'
        fetch_cache.mkdir()
        fetch_args = https_args[:-1] + [str(fetch_cache)]
        https_target = work / 'products' / 'https'
        ok, code, out, err = journey.cli_call('https-scaffold', 'scaffold', *fetch_args, *params, '--root', str(https_target), *common_exec)
        journey.record('https: scaffold from the pinned HTTPS envelope succeeds', ok and https_target.is_dir(), f'exit={code} {(out + err)[-500:] if not ok else ""}')
        if https_target.is_dir():
            document = verify_product(journey, 'https', https_target)
            source = document.get('source', {})
            hops = source.get('networkHops') or []
            journey.record('https: provenance records a genuine fetch with the connected public endpoint and status 200',
                           source.get('acquisition') == 'https-fetch' and source.get('resolvedUri') == args.https_url
                           and source.get('revision') == REVISION and bool(hops) and hops[-1].get('statusCode') == 200,
                           json.dumps({k: source.get(k) for k in ('acquisition', 'resolvedUri', 'networkHops')})[:400])
            build_and_test(journey, 'https', https_target)
            https_offline = work / 'products' / 'https-offline'
            ok, code, out, err = journey.cli_call('https-offline', 'scaffold', *fetch_args, '--offline', *params, '--root', str(https_offline), *common_exec)
            offline_doc = json.loads((https_offline / '.fsgg/scaffold-provenance.json').read_text()) if https_offline.is_dir() else {}
            journey.record('https: offline scaffold from the fetched cache succeeds with empty network observations',
                           ok and offline_doc.get('source', {}).get('acquisition') == 'cache-read' and offline_doc['source']['networkHops'] == [],
                           f'exit={code}')
        # origin not allowed by the independently selected policy
        denied_policy = work / 'https-denied.json'
        denied_sha = acquisition_policy(denied_policy, ['https://example.org'])
        denied_cache = work / 'cache-denied'
        denied_cache.mkdir()
        ok, code, out, err = journey.cli_call('https-denied', 'catalog', 'inspect', '--source-set', str(https_set), '--source-set-sha256', https_set_sha,
                                              '--acquisition-policy', str(denied_policy), '--acquisition-policy-sha256', denied_sha,
                                              '--source-cache', str(denied_cache), '--json', expect_exit=1, expect_text='source.originDenied')
        journey.record('https: an origin outside the acquisition policy is refused', ok and not any(denied_cache.iterdir()), f'exit={code}')

    # Negative controls on a fresh local source ----------------------------------------
    bad = sources / 'tampered'
    write_source(bad, sources / 'tampered-aux')
    (bad / 'catalog.json').write_bytes((bad / 'catalog.json').read_bytes() + b' ')
    bad_set = work / 'bad-set.json'
    bad_sha = source_set(bad_set, [{'id': SOURCE_ID, 'origin': 'explicit', 'locator': {'kind': 'local-directory', 'path': str(bad)},
                                    'manifestSha256': digests['manifestSha256']}])
    bad_cache = work / 'cache-bad'
    bad_cache.mkdir()
    ok, code, out, err = journey.cli_call('tampered-member', 'catalog', 'inspect', '--source-set', str(bad_set), '--source-set-sha256', bad_sha,
                                          '--acquisition-policy', str(local_policy), '--acquisition-policy-sha256', local_policy_sha,
                                          '--source-cache', str(bad_cache), '--json', expect_exit=1, expect_text='source.memberDigestMismatch')
    journey.record('negative: a member whose bytes differ from the manifest is refused before cache publication', ok and not any(bad_cache.iterdir()), f'exit={code}')

    link = sources / 'symlinked'
    write_source(link, sources / 'symlinked-aux')
    (link / 'catalog.json').unlink()
    (link / 'catalog.json').symlink_to(FIXTURE / 'catalog.json')
    link_set = work / 'link-set.json'
    link_sha = source_set(link_set, [{'id': SOURCE_ID, 'origin': 'explicit', 'locator': {'kind': 'local-directory', 'path': str(link)},
                                      'manifestSha256': digests['manifestSha256']}])
    link_cache = work / 'cache-link'
    link_cache.mkdir()
    ok, code, out, err = journey.cli_call('symlinked-member', 'catalog', 'inspect', '--source-set', str(link_set), '--source-set-sha256', link_sha,
                                          '--acquisition-policy', str(local_policy), '--acquisition-policy-sha256', local_policy_sha,
                                          '--source-cache', str(link_cache), '--json', expect_exit=1, expect_text='errno=40')
    journey.record('negative: a symlinked member is refused', ok and not any(link_cache.iterdir()), f'exit={code}')

    wrong = work / 'wrong-manifest-set.json'
    wrong_sha = source_set(wrong, [{'id': SOURCE_ID, 'origin': 'explicit', 'locator': {'kind': 'local-directory', 'path': str(sources / 'tampered')},
                                    'manifestSha256': sha(b'not the manifest')}])
    wrong_cache = work / 'cache-wrong'
    wrong_cache.mkdir()
    ok, code, out, err = journey.cli_call('wrong-manifest-digest', 'catalog', 'inspect', '--source-set', str(wrong), '--source-set-sha256', wrong_sha,
                                          '--acquisition-policy', str(local_policy), '--acquisition-policy-sha256', local_policy_sha,
                                          '--source-cache', str(wrong_cache), '--json', expect_exit=1, expect_text='source.rawDigestMismatch')
    journey.record('negative: a wrong pinned manifest digest is refused', ok and not any(wrong_cache.iterdir()), f'exit={code}')

    failed = [r for r in journey.results if not r['passed']]
    summary = {'passed': not failed, 'cases': len(journey.results), 'failed': [r['name'] for r in failed], 'results': journey.results}
    (work / 'journey-result.json').write_text(json.dumps(summary, indent=2) + '\n')
    print(json.dumps({'passed': summary['passed'], 'cases': summary['cases'], 'failed': summary['failed']}))
    return 0 if summary['passed'] else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest='mode', required=True)
    build = sub.add_parser('build')
    build.add_argument('--out', required=True)
    run = sub.add_parser('run')
    run.add_argument('--cli', required=True, help='absolute path of the installed fsgg-sdd executable')
    run.add_argument('--work', required=True, help='fresh empty working root')
    run.add_argument('--https-url', help='pinned https URL serving tests/fixtures/third-party-provider-source envelope.zip')
    run.add_argument('--source-root', default=str(ROOT), help='git checkout the runner records identity for')
    args = parser.parse_args()
    if args.mode == 'build':
        out = Path(args.out)
        print(json.dumps(write_source(out / 'source', out), indent=2))
        return 0
    return run_journeys(args)


if __name__ == '__main__':
    raise SystemExit(main())
