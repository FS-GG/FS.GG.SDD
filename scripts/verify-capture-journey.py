#!/usr/bin/env python3
"""Package-only authoring -> CLI -> Governance Verify/GateExecution journey (SDD928 C3.1-03).

Everything the journey runs comes from PUBLISHED packages installed from nuget.org into an empty
private cache: the SDD CLI tool, the Governance CLI tool (which carries `fsgg-governance verify`) and
the packages the typed authoring example compiles against. Only the example's source, its template
data, the fixture and the Governance workspace catalog come from this checkout.

The Governance Verify leg is where the capture-lifetime join lives. One Verify provider invocation
owns one capture session (Governance#465/#468): the Verify host owns it, and the OUTER admitted runtime
supplies its original work and cleanup ends and owns whole-worker teardown. Here that outer runtime is
the maintained runner (scripts/catalog-runtime-validation.py, `captureLifetime`): it supplies
FSGG_VERIFY_WORK_SECONDS / FSGG_VERIFY_CLEANUP_SECONDS, fits the whole capture window inside its own
work window, never signals the host inside it, and ends the whole pid-namespace worker afterwards.
Descendants the fixture leaves behind in their own sessions are witnessed absent afterwards.

This is ordinary development verification on a host that satisfies the catalog executor's contract
(Linux x64, .NET 10.0.12, glibc 2.44) with at most six CPUs. It is a local cooperative profile: not
hostile containment, not a hosted-runner profile, and not provider certification. It does not exercise
an authentic blocked regular-file read or an uncertain physical close (Governance#423 prerequisite b).

  verify-capture-journey.py run --work FRESH_DIR [--sdd-version 2.5.1] [--governance-version 1.15.0]
                                [--expect-sha256 PACKAGE_ID.VERSION=HEX ...]
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import urllib.request

sys.dont_write_bytecode = True

ROOT = Path(__file__).resolve().parents[1]
FIXTURES = ROOT / 'tests/fixtures/provider-verify-journey'
TEMPLATE = ROOT / 'tests/fixtures/provider-catalog-runtime/opaque-template'
SAMPLE = ROOT / 'examples/provider-authoring'
DOTNET = '/usr/share/dotnet/dotnet'
SDK_VERSION = '10.0.401'
MAXIMUM_CPUS = 6
IDENTITY_PATHS = ['scripts/*.py', 'scripts/fixtures/*', 'examples/provider-authoring/*',
                  'tests/fixtures/provider-verify-journey/*', 'tests/fixtures/provider-verify-journey/fsgg/*',
                  'tests/fixtures/provider-verify-journey/fsgg-build-test-package/*']

# Verify capture window the outer runner admits to every Verify invocation (seconds).
CAPTURE_WORK = 60
CAPTURE_CLEANUP_END = 75
CAPTURE_STARTUP_ALLOWANCE = 5
VERIFY_TIMEOUT = 150

spec = importlib.util.spec_from_file_location('catalog_harness', ROOT / 'scripts/catalog-runtime-validation.py')
harness = importlib.util.module_from_spec(spec)
spec.loader.exec_module(harness)


# nuget.org whole-file SHA-256 of the published tool packages. The leading eight characters are recorded
# on FS-GG/FS.GG.SDD#935 (SDD 2.5.1, comment 6387941247) and #928 (Governance 1.15.0, comment 6363029199);
# the full values were verified against fresh nuget.org downloads by an independent reviewer on 2026-10-24.
PUBLISHED_SHA256 = {
    'FS.GG.SDD.Cli.2.5.1': '94e20819a011419531b696cc8510c122edc5cce2f2dbe93bb09864a391cfe241',
    'FS.GG.Governance.Cli.1.15.0': '9eb3eed64cacdd84ba3d82439d8e3b6ffb3c9e035c6e5de00a1520094227879b',
}


def sha256_file(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


class Journey:
    def __init__(self, work, sdd_version, governance_version, expectations):
        self.work = work
        self.sdd_version = sdd_version
        self.governance_version = governance_version
        self.expectations = expectations
        self.results = []
        self.counter = 0
        self.environment = {}

    def record(self, name, passed, detail=''):
        self.results.append({'name': name, 'passed': bool(passed), 'detail': detail})
        print(('PASS ' if passed else 'FAIL ') + name + (' :: ' + detail if detail else ''), flush=True)
        return bool(passed)

    def step(self, name, argv, cwd=None, timeout=900, environment=None, mode='ordinary', **extra):
        """One literal command through the maintained runner; returns its execution record and logs."""
        self.counter += 1
        output = self.work / 'runner' / f'{self.counter:02d}-{name}'
        command = dict(name=name, argv=argv, cwd=str(cwd or self.work), timeoutSeconds=timeout, cleanupSeconds=30,
                       environment=dict(self.environment, **(environment or {})), outputBytes=16 * 1024 * 1024, **extra)
        if mode == 'native-runtime':
            command.update(mode='native-runtime', backend='pid-namespace')
        actual = harness.run_command(command, output)
        stdout = Path(actual['streams']['stdout']['log']).read_text(errors='replace') if 'stdout' in actual['streams'] else ''
        stderr = Path(actual['streams']['stderr']['log']).read_text(errors='replace') if 'stderr' in actual['streams'] else ''
        return actual, stdout, stderr


def host_facts():
    facts = {'kernel': os.uname().release, 'machine': os.uname().machine, 'cpus': sorted(os.sched_getaffinity(0))}
    libc = subprocess.run(['ldd', '--version'], capture_output=True, text=True).stdout.splitlines()
    facts['glibc'] = libc[0].split()[-1] if libc else None
    runtimes = subprocess.run([DOTNET, '--list-runtimes'], capture_output=True, text=True).stdout.splitlines()
    facts['runtimes'] = [line.split()[1] for line in runtimes if line.startswith('Microsoft.NETCore.App ')]
    return facts


def prepare_environment(work):
    env = {
        'HOME': str(work / 'home'), 'DOTNET_CLI_HOME': str(work / 'home'), 'NUGET_PACKAGES': str(work / 'nuget-packages'),
        'TMPDIR': str(work / 'tmp'), 'NUGET_HTTP_CACHE_PATH': str(work / 'cache/nuget-http'),
        'NUGET_PLUGINS_CACHE_PATH': str(work / 'cache/nuget-plugins'),
        'XDG_CONFIG_HOME': str(work / 'home/.config'), 'XDG_CACHE_HOME': str(work / 'home/.cache'),
        'XDG_DATA_HOME': str(work / 'home/.local/share'),
        'DOTNET_NOLOGO': '1', 'DOTNET_CLI_TELEMETRY_OPTOUT': '1', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE': '1',
        'DOTNET_MULTILEVEL_LOOKUP': '0', 'DOTNET_PROCESSOR_COUNT': str(MAXIMUM_CPUS),
        'PYTHONDONTWRITEBYTECODE': '1', 'GIT_CONFIG_NOSYSTEM': '1', 'GIT_TERMINAL_PROMPT': '0',
        'PATH': '/usr/local/bin:/usr/bin:/bin', 'LANG': 'C.UTF-8',
    }
    for name in ('home', 'nuget-packages', 'tmp', 'cache', 'inputs/fixture', 'results', 'tools'):
        (work / name).mkdir(parents=True, exist_ok=True)
    # Stop MSBuild directory walk-up at the work directory, and pin the SDK.
    for name in ('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props'):
        (work / name).write_text('<Project />\n')
    (work / 'global.json').write_text(json.dumps({'sdk': {'version': SDK_VERSION, 'rollForward': 'disable'}}) + '\n')
    (work / 'nuget-public.config').write_text(
        '<?xml version="1.0" encoding="utf-8"?>\n<configuration><packageSources><clear />'
        '<add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources></configuration>\n')
    return env


def download_nupkg(package, version, destination):
    """Fetch the exact published package bytes from nuget.org's flat container."""
    name = package.lower()
    url = f'https://api.nuget.org/v3-flatcontainer/{name}/{version}/{name}.{version}.nupkg'
    request = urllib.request.Request(url, headers={'User-Agent': 'fsgg-sdd-verify-capture-journey'})
    with urllib.request.urlopen(request, timeout=300) as response:
        data = response.read()
    destination.write_bytes(data)
    return hashlib.sha256(data).hexdigest()


def install_tools(journey):
    """Install both published tools into an isolated tool path from the exact nuget.org bytes."""
    work = journey.work
    downloads = work / 'dl'
    downloads.mkdir()
    (work / 'nuget-local.config').write_text(
        '<?xml version="1.0" encoding="utf-8"?>\n<configuration><packageSources><clear />'
        f'<add key="local" value="{downloads}" /></packageSources></configuration>\n')
    tools = {}
    for package, version, executable in [('FS.GG.SDD.Cli', journey.sdd_version, 'fsgg-sdd'),
                                         ('FS.GG.Governance.Cli', journey.governance_version, 'fsgg-governance')]:
        try:
            digest = download_nupkg(package, version, downloads / f'{package.lower()}.{version}.nupkg')
        except OSError as error:
            digest = None
            journey.record(f'{package} {version} downloads from nuget.org', False, str(error))
        tools[package] = {'version': version, 'nupkgSha256': digest, 'executable': str(work / 'tools' / executable)}
        if digest is None:
            continue
        expected = journey.expectations.get(f'{package}.{version}')
        if expected is not None:
            journey.record(f'{package} {version} bytes equal the independently recorded SHA-256', digest == expected, f'{digest} vs {expected}')
        actual, out, err = journey.step('install-' + package.lower(), [DOTNET, 'tool', 'install', package, '--version', version,
                                        '--tool-path', str(work / 'tools'), '--configfile', str(work / 'nuget-local.config')], timeout=600)
        journey.record(f'{package} {version} installs from the downloaded nuget.org bytes into the empty cache', actual['passed'],
                       f'sha256={digest}' if actual['passed'] else (out + err)[-300:])
    return tools


def locate_sdd_dll(work, version):
    path = work / 'tools/.store/fs.gg.sdd.cli' / version / 'fs.gg.sdd.cli' / version / 'tools/net10.0/any/FS.GG.SDD.Cli.dll'
    return path if path.is_file() else None


def build_authoring(journey):
    work = journey.work
    sample = work / 'authoring'
    shutil.copytree(SAMPLE, sample)
    (sample / 'packages.lock.json').unlink()  # re-resolved against the published version from nuget.org only
    version = f'-p:SddPackageVersion={journey.sdd_version}'
    restore, out, err = journey.step('authoring-restore', [DOTNET, 'restore', str(sample / 'Authoring.fsproj'), '--configfile',
                                     str(work / 'nuget-public.config'), '--packages', str(work / 'authoring-pkgcache'), version], timeout=900)
    journey.record('authoring: cold package-only restore from nuget.org into an empty cache', restore['passed'], (out + err)[-400:] if not restore['passed'] else '')
    build, out, err = journey.step('authoring-build', [DOTNET, 'build', str(sample / 'Authoring.fsproj'), '-c', 'Release', '--no-restore',
                                   '-m:1', '-p:UseSharedCompilation=false', version, f'-p:RestorePackagesPath={work / "authoring-pkgcache"}'], timeout=900)
    clean = build['passed'] and ' 0 Warning(s)' in out and ' 0 Error(s)' in out
    journey.record('authoring: typed compile has 0 warnings and 0 errors', clean, (out + err)[-400:] if not clean else '')
    assets = json.loads((sample / 'obj/project.assets.json').read_text()) if (sample / 'obj/project.assets.json').is_file() else {}
    kinds = sorted({library['type'] for library in assets.get('libraries', {}).values()})
    journey.record('authoring: every resolved library is a package (no project reference)', kinds == ['package'] and bool(assets.get('libraries')),
                   f'{len(assets.get("libraries", {}))} libraries, types={kinds}')
    shutil.copy(sample / 'packages.lock.json', work / 'results/authoring-packages.lock.json')
    dll = sample / 'bin/Release/net10.0/Authoring.dll'
    return dll if dll.is_file() else None


def fixture_policy(fixture, policy_id, required=('test:test',)):
    mappings = '[{"format":"fsgg.governance-handoff@2.0.0","id":"fsgg.governance-handoff","version":"2.0.0"}]'
    return {"schemaVersion": 1, "id": policy_id, "version": "1.0.0",
            "evidenceMap": {"id": "verify-journey-map", "version": "1.0.0",
                            "digest": "sha256:" + hashlib.sha256(mappings.encode()).hexdigest(), "mappings": json.loads(mappings)},
            "requiredCapabilityIds": sorted(required), "semanticOnlyCapabilityIds": [],
            "knownPlatforms": ["linux-x64"], "environments": [{"id": "fixture-local", "environment": "local"}],
            "supportedEvidenceFormats": [{"id": "fsgg.governance-handoff", "version": "2.0.0"}],
            "maximumPreflightSeconds": 30, "maximumScaffoldSeconds": 60,
            "toolProbes": [{"id": "governance-test-fixture", "executable": str(fixture), "arguments": ["--version"],
                            "versionPrefix": "verify-journey-fixture ", "versionSuffix": "\n", "maximumOutputCharacters": 128}],
            "archives": [{"templateSource": "opaque-source", "packageId": "FS.GG.SDD.Catalog.OpaqueFixture", "packageVersion": "1.0.1"}]}


def prepare_inputs(journey):
    work = journey.work
    inputs = work / 'inputs'
    shutil.copytree(TEMPLATE, inputs / 'template')
    (inputs / 'tests-input.json').write_bytes(b'{"schemaVersion":1}')
    fixture = inputs / 'fixture/verify-journey-fixture'
    shutil.copy(FIXTURES / 'test-fixture.sh', fixture)
    fixture.chmod(0o700)
    for name, policy_id in [('policy.json', 'verify-journey-policy'), ('policy-other.json', 'verify-journey-other-policy')]:
        (inputs / name).write_bytes(json.dumps(fixture_policy(fixture, policy_id), separators=(',', ':')).encode())
    return fixture


def authoring_controls(journey, dll, fixture):
    """Deterministic double emission and invalid-policy refusal through the actual compiled sample."""
    work = journey.work
    inputs = work / 'inputs'
    second_template = work / 'inputs-second-template'
    shutil.copytree(inputs / 'template', second_template)
    # Change creation order and mtimes: the emitted bytes must not depend on either.
    for index, path in enumerate(sorted(second_template.rglob('*'), reverse=True)):
        os.utime(path, (1_000_000_000 + index * 7, 1_000_000_000 + index * 13))
    def emit(name, template, policy, out):
        return journey.step(name, [DOTNET, str(dll), 'emit', '--template-root', str(template), '--fixture-executable', str(fixture),
                                   '--test-input', str(inputs / 'tests-input.json'), '--policy', str(policy), '--out', str(out)], timeout=120)
    first, _, e1 = emit('emit-first', inputs / 'template', inputs / 'policy.json', work / 'emit-first')
    second, _, e2 = emit('emit-second', second_template, inputs / 'policy.json', work / 'emit-second')
    same = first['passed'] and second['passed'] and all(
        (work / 'emit-first' / name).read_bytes() == (work / 'emit-second' / name).read_bytes()
        for name in ('catalog.json', 'opaque-fixture.nupkg', 'policy.json'))
    journey.record('authoring: deterministic double emission (catalog, archive, policy byte-equal)', same, (e1 + e2)[-300:] if not same else
                   ', '.join(f'{n}={sha256_file(work / "emit-first" / n)[:12]}' for n in ('catalog.json', 'opaque-fixture.nupkg', 'policy.json')))
    existing, _, _ = emit('emit-existing', inputs / 'template', inputs / 'policy.json', work / 'emit-first')
    journey.record('authoring: an existing output directory is refused', existing['exitCode'] == 1 and (work / 'emit-first/catalog.json').is_file(),
                   f'exit={existing["exitCode"]}')
    malformed = work / 'inputs/policy-malformed.json'
    malformed.write_bytes(b'{"schemaVersion":1,"id":')
    refused, out, err = emit('emit-malformed-policy', inputs / 'template', malformed, work / 'emit-malformed')
    journey.record('authoring: a malformed policy is refused with catalog.policyInvalid', refused['exitCode'] == 1 and 'catalog.policyInvalid' in out + err,
                   f'exit={refused["exitCode"]}')


def scaffold_leg(journey, tools, dll, fixture):
    """Real installed SDD CLI scaffold through the maintained driver, with strict schema-2 readback."""
    work = journey.work
    sdd_dll = locate_sdd_dll(work, journey.sdd_version)
    if sdd_dll is None:
        journey.record('installed SDD CLI assembly is present', False)
        return None
    # The catalog transport profile requires exactly the 10.0.12 host runtime.
    fx = next((v for v in host_facts()['runtimes'] if v == '10.0.12'), None)
    if fx is None:
        journey.record('runtime 10.0.12 is installed for the catalog transport profile', False)
        return None
    config = {'sourceRoot': str(ROOT), 'identityPaths': IDENTITY_PATHS,
              'producerCommand': [DOTNET, str(dll), 'emit'],
              'producerArguments': ['--template-root', str(work / 'inputs/template'), '--fixture-executable', str(fixture),
                                    '--test-input', str(work / 'inputs/tests-input.json'), '--policy', str(work / 'inputs/policy.json')],
              'transportExecutable': DOTNET, 'cliCommand': [DOTNET, 'exec', '--fx-version', fx, str(sdd_dll)],
              'verifyCommand': [DOTNET, str(dll), 'check'],
              'runtime': {'mode': 'native-runtime', 'backend': 'pid-namespace', 'timeoutSeconds': 150, 'cleanupSeconds': 15, 'outputBytes': 8388608,
                          'environment': journey.environment},
              'verification': {'mode': 'ordinary', 'timeoutSeconds': 30, 'cleanupSeconds': 10, 'outputBytes': 1048576,
                               'environment': journey.environment},
              'totalSeconds': 600,
              'fixtureExpectations': {'files': [{'path': 'product.txt', 'text':
                  'raw=Awkward name!?\npackage=example.org/explicit/module\ncode=IndependentCode\nempty=\nliteral=$(literal);& with spaces\n'}],
                  'absentPaths': [], 'producedSkillPaths': ['.agents/skills/opaque-fixture/SKILL.md']}}
    config_path = work / 'results/sdd-driver-config.json'
    config_path.write_text(json.dumps(config, indent=2))
    environment = dict(os.environ, **journey.environment)
    outcomes = {}
    for case, expected in [('positive', None), ('negatives', 7)]:
        output = work / 'results' / f'sdd-{case}'
        completed = subprocess.run([sys.executable, '-I', '-B', str(ROOT / 'scripts/catalog-cli-acceptance.py'), '--config', str(config_path),
                                    '--output', str(output), '--case', case], env=environment, capture_output=True, text=True, timeout=1200)
        report = json.loads((output / 'acceptance.json').read_text()) if (output / 'acceptance.json').is_file() else {}
        passed = completed.returncode == 0 and report.get('passed') is True and report.get('sourceIdentityStable') is True
        detail = f'cases={len(report.get("cases", []))}'
        if expected is not None:
            passed = passed and len([c for c in report.get('cases', []) if c.get('passed')]) == expected
        journey.record(f'SDD CLI maintained driver --case {case} passes with stable source identity', passed,
                       detail + ('' if passed else ' ' + (completed.stdout + completed.stderr)[-400:] + ' ' + str(report.get('firstFailure'))))
        outcomes[case] = report
    target = work / 'results/sdd-positive/positive-target'
    return target if (target / '.fsgg/scaffold-provenance.json').is_file() else None


def commit_workspace(journey, target):
    work = journey.work
    workspace = work / 'workspace'
    shutil.copytree(target, workspace, symlinks=True)
    def git(*args):
        return subprocess.run(['git', '-C', str(workspace), '-c', 'user.name=journey', '-c', 'user.email=journey@invalid',
                               '-c', 'commit.gpgsign=false', *args], capture_output=True, text=True,
                              env=dict(os.environ, **journey.environment), check=True)
    git('init', '-q')
    git('add', '-A')
    git('commit', '-qm', f'scaffolded by fsgg-sdd {journey.sdd_version}')
    for source in sorted((FIXTURES / 'fsgg').iterdir()):
        shutil.copy(source, workspace / '.fsgg' / source.name)
    git('add', '-A')
    git('commit', '-qm', 'independently authored Governance workspace catalog')
    return workspace


def verify_cases(journey, tools, workspace):
    """Run the installed `fsgg-governance verify` provider mode under the capture-lifetime join."""
    work = journey.work
    verify_root = work / 'results/verify'
    verify_root.mkdir(parents=True)
    tool = tools['FS.GG.Governance.Cli']['executable']
    provenance = '.fsgg/scaffold-provenance.json'
    policy, other_policy = work / 'inputs/policy.json', work / 'inputs/policy-other.json'
    evidence = 'out/governance-handoff.json'
    def arguments(prov_sha=None, policy_path=policy, platform='linux-x64', paths='inputs/tests.json'):
        return ['--paths', paths, '--provider-provenance', provenance,
                '--provider-provenance-sha256', prov_sha or sha256_file(workspace / provenance),
                '--provider-policy', str(policy_path), '--provider-policy-sha256', sha256_file(policy_path),
                '--provider-platform', platform]
    # name: (arguments, fixture mode, stale evidence, tooling timeout, supply the ends, expected exit, distinguishing stderr text)
    cases = {
        'positive': (arguments(), 'write', False, None, True, 0, None),
        'missing-evidence': (arguments(), 'none', False, None, True, 1, 'was not produced by the run'),
        'stale-evidence': (arguments(), 'write', True, None, True, 1, 'existed before launch'),
        'mismatched-evidence': (arguments(), 'mismatch', False, None, True, 1, 'sources do not match the workspace'),
        'nonzero-fixture-exit': (arguments(), 'exit2', False, None, True, 1, 'exited 2'),
        'detached-descendant': (arguments(), 'escape', False, None, True, 0, None),
        'declared-deadline': (arguments(), 'hang', False, 3, True, 1, 'WorkDeadlineReached'),
        'provenance-digest-mismatch': (arguments(prov_sha='0' * 64), 'write', False, None, True, 3, 'RawDigestMismatch'),
        'policy-identity-mismatch': (arguments(policy_path=other_policy), 'write', False, None, True, 3, 'differs from the independently selected policy'),
        'platform-mismatch': (arguments(platform='linux-arm64'), 'write', False, None, True, 3, 'UnsupportedPlatform'),
        'unrouted-selection': (arguments(paths='AGENTS.md'), 'write', False, None, True, 3, 'no selected effective gate'),
        'ends-not-supplied': (arguments(), 'write', False, None, False, 3, 'FSGG_VERIFY_WORK_SECONDS is not set'),
    }
    summary = {}
    for name, (tail, mode, stale, timeout, supply_ends, expected, reason) in cases.items():
        repo = verify_root / (name + '-ws')
        subprocess.run(['git', 'clone', '-q', str(workspace), str(repo)], check=True, env=dict(os.environ, **journey.environment))
        if timeout is not None:
            tooling = repo / '.fsgg/tooling.yml'
            tooling.write_text(tooling.read_text().replace('timeout: 30', f'timeout: {timeout}'))
            subprocess.run(['git', '-C', str(repo), '-c', 'user.name=journey', '-c', 'user.email=journey@invalid', '-c', 'commit.gpgsign=false',
                            'commit', '-qam', 'declared timeout'], check=True, capture_output=True, env=dict(os.environ, **journey.environment))
        if stale:
            (repo / 'out').mkdir(exist_ok=True)
            (repo / evidence).write_text('stale')
        before = subprocess.run(['git', '-C', str(repo), 'status', '--porcelain'], capture_output=True, text=True).stdout
        witness = verify_root / (name + '.witness')
        extra = {'captureLifetime': {'workSeconds': CAPTURE_WORK, 'cleanupEndSeconds': CAPTURE_CLEANUP_END,
                                     'startupAllowanceSeconds': CAPTURE_STARTUP_ALLOWANCE}} if supply_ends else {}
        actual, out, err = journey.step(
            'verify-' + name, [tool, 'verify', '--repo', str(repo), '--json', '--verify-out', str(verify_root / (name + '-verify.json')),
                               '--cost-budget-out', str(verify_root / (name + '-cost-budget.json')),
                               '--provenance-out', str(verify_root / (name + '-provenance.json'))] + tail,
            cwd=work, timeout=VERIFY_TIMEOUT, mode='native-runtime', descendantWitnessPath=str(witness),
            environment={'FSGG_FIXTURE_MODE': mode, 'FSGG_FIXTURE_WITNESS': str(witness)}, **extra)
        produced = (repo / evidence).is_file()
        content = (repo / evidence).read_text() if produced else None
        status = subprocess.run(['git', '-C', str(repo), 'status', '--porcelain'], capture_output=True, text=True).stdout
        capture = actual.get('captureLifetime')
        witnessed = actual.get('descendantWitness', {})
        ok = actual['exitCode'] == expected and actual['cleanup'] == 'direct-child-reaped-streams-eof'
        if supply_ends:
            # The host exited by itself inside the admitted window and the outer runtime never had to end it.
            # A pass/blocked exit means the host held and released the session (hostSettled); exit 3 is a
            # refusal that says nothing about whether a session was ever held, so it is not claimed settled.
            ok = ok and capture is not None and not capture['outerRuntimeEnded'] and capture['withinAdmittedWindow']
            ok = ok and capture['outcome'] == ('host-refused-input-unavailable' if expected == 3 else 'host-exited-after-settlement')
            ok = ok and capture['hostSettled'] == (expected != 3)
            ok = ok and actual['durationSeconds'] < CAPTURE_CLEANUP_END
            ok = ok and actual.get('workerScan', {}).get('survivors') == []
        ok = ok and witnessed.get('survivors') == []
        if reason is not None:
            # Each refusal must be the intended one, not merely the same exit code.
            ok = ok and reason in err + out
        if name in ('positive', 'detached-descendant'):
            verdict = json.loads((verify_root / (name + '-verify.json')).read_text()) if (verify_root / (name + '-verify.json')).is_file() else {}
            executed = [item.get('execution', {}).get('disposition') for item in verdict.get('passing', [])]
            ok = ok and verdict.get('verdict') == 'pass' and executed == ['executed']
        if name == 'positive':
            ok = ok and produced and content != 'stale'
        if name == 'stale-evidence':
            ok = ok and content == 'stale'
        if expected == 3:
            ok = ok and not produced
        if name in ('detached-descendant', 'declared-deadline'):
            # The fixture left a detached descendant (own session, TERM ignored, sleeping 300 s): it was
            # recorded, and none survives the worker that lived for seconds.
            ok = ok and witnessed.get('observed', 0) >= 2 and actual.get('descendantTeardown') == 'witnessed-absent'
        if name == 'declared-deadline':
            ok = ok and 2.5 <= actual['durationSeconds'] < 20
        if name == 'positive':
            ok = ok and witnessed.get('observed', 0) >= 1 and actual.get('descendantTeardown') == 'witnessed-absent'
        summary[name] = {'expectedExit': expected, 'exit': actual['exitCode'], 'ok': ok, 'evidenceProduced': produced,
                         'evidenceSha256': hashlib.sha256(content.encode()).hexdigest() if content else None,
                         'statusBefore': before.strip(), 'statusAfter': status.strip(), 'captureLifetime': capture,
                         'descendantWitness': {k: witnessed.get(k) for k in ('observed', 'survivors')}, 'workerScan': actual.get('workerScan'),
                         'descendantTeardown': actual.get('descendantTeardown'), 'durationSeconds': actual['durationSeconds'],
                         'cleanup': actual['cleanup'], 'firstFailure': actual['firstFailure'], 'stderr': err.strip()[:600]}
        window = ('host exited by itself inside the admitted window, session released' if supply_ends and expected != 3
                  else 'host refused (exit 3) inside the admitted window, no settlement claimed' if supply_ends
                  else 'host refused before any capture (no outer ends supplied)')
        journey.record(f'verify {name}: exit {actual["exitCode"]} (expected {expected}), {window}, '
                       f'descendants {witnessed.get("observed")} witnessed / {len(witnessed.get("survivors") or [])} surviving, '
                       f'worker scan {len((actual.get("workerScan") or {}).get("survivors") or [])} surviving',
                       ok, '' if ok else json.dumps(summary[name])[:700])
    (work / 'results/verify-summary.json').write_text(json.dumps(summary, indent=2) + '\n')
    return summary


def run_journey(args):
    if os.environ.get('VERIFY_CAPTURE_JOURNEY_CLEAN') != '1':
        # Governance captures the process environment for provider commands: run with a minimal,
        # credential-free environment so no ambient token or setting reaches any child.
        clean = {'VERIFY_CAPTURE_JOURNEY_CLEAN': '1', 'HOME': os.environ.get('HOME', '/'), 'PATH': '/usr/local/bin:/usr/bin:/bin',
                 'LANG': 'C.UTF-8', 'PYTHONDONTWRITEBYTECODE': '1'}
        os.execve(sys.executable, [sys.executable, '-I', '-B', *sys.argv], clean)
    work = Path(args.work).resolve()
    if work.exists() and any(work.iterdir()):
        raise SystemExit('--work must be a fresh empty directory')
    work.mkdir(parents=True, exist_ok=True)
    os.sched_setaffinity(0, set(sorted(os.sched_getaffinity(0))[:MAXIMUM_CPUS]))
    expectations = dict(PUBLISHED_SHA256, **dict(item.split('=', 1) for item in args.expect_sha256 or []))
    journey = Journey(work, args.sdd_version, args.governance_version, expectations)
    journey.environment = prepare_environment(work)
    before = harness.tree_identity(ROOT, IDENTITY_PATHS)
    facts = host_facts()
    exact_host = facts['machine'] == 'x86_64' and facts['glibc'] == '2.44' and any(v.startswith('10.0.12') for v in facts['runtimes'])
    journey.record('host is the supported Linux-x64 / .NET 10.0.12 / glibc 2.44 profile with at most six CPUs',
                   exact_host and len(facts['cpus']) <= MAXIMUM_CPUS, json.dumps(facts))
    tools = install_tools(journey)
    dll = build_authoring(journey)
    fixture = prepare_inputs(journey)
    target = None
    summary = {}
    if dll is not None and all(entry['nupkgSha256'] for entry in tools.values()):  # failures above are already recorded
        authoring_controls(journey, dll, fixture)
        target = scaffold_leg(journey, tools, dll, fixture)
    journey.record('the positive scaffold produced a workspace with schema-2 provenance for the Verify leg', target is not None)
    if target is not None:
        workspace = commit_workspace(journey, target)
        summary = verify_cases(journey, tools, workspace)
    after = harness.tree_identity(ROOT, IDENTITY_PATHS)
    journey.record('source identity (journey drivers, example and fixtures) is unchanged by the run', before == after)
    failed = [r for r in journey.results if not r['passed']]
    report = {'passed': not failed, 'cases': len(journey.results), 'failed': [r['name'] for r in failed], 'host': facts,
              'sddVersion': args.sdd_version, 'governanceVersion': args.governance_version, 'tools': tools,
              'sourceRevision': subprocess.run(['git', '-C', str(ROOT), 'rev-parse', 'HEAD'], capture_output=True, text=True).stdout.strip(),
              'sourceTreeSha256': before, 'captureWindow': {'workSeconds': CAPTURE_WORK, 'cleanupEndSeconds': CAPTURE_CLEANUP_END,
                                                            'startupAllowanceSeconds': CAPTURE_STARTUP_ALLOWANCE, 'runnerTimeoutSeconds': VERIFY_TIMEOUT},
              'results': journey.results, 'verify': summary,
              'scope': 'local cooperative profile; not hostile containment, not hosted, not provider certification; no authentic blocked-read or uncertain-close case'}
    (work / 'journey-result.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({'passed': report['passed'], 'cases': report['cases'], 'failed': report['failed']}))
    for owner in harness.INCOMPLETE_OWNERS:
        owner.wait()
    return 0 if report['passed'] else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest='mode', required=True)
    run = sub.add_parser('run')
    run.add_argument('--work', required=True, help='fresh empty working root (private cache, tools, results)')
    run.add_argument('--sdd-version', default='2.5.1')
    run.add_argument('--governance-version', default='1.15.0')
    run.add_argument('--expect-sha256', action='append', metavar='PACKAGE_ID.VERSION=HEX',
                     help='override or add an independently recorded nuget.org whole-file SHA-256 of a tool package '
                          '(the published SDD 2.5.1 and Governance 1.15.0 values are built in)')
    args = parser.parse_args()
    return run_journey(args)


if __name__ == '__main__':
    raise SystemExit(main())
