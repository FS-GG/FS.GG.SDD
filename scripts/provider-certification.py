#!/usr/bin/env python3
"""Reusable provider qualification harness and package-only certification report (SDD928 C3).

A provider author packs a catalog, a template archive and an independently selected policy (for
example with the typed authoring example's `emit`). This harness takes that packed artifact plus the
INSTALLED SDD CLI and the INSTALLED Governance CLI, runs the installed journey, and writes a closed,
versioned report (`fsgg.provider-certification/v1`) that names the exact inputs, tools and host and
records every check with its observed evidence. It proves, from the packed artifact:

  lifecycle        settled scaffold, dry run, strict schema-2 readback, deterministic instantiation
  provenance       schema-2 digests equal the inputs, declared tools, literal invocations and bindings
  no-partial-state every refusal leaves the target and owned staging exactly as they were
  skill-union      provider and SDD-owned skills are recorded, present, and mirrors are byte-equal
  unsupported-tool wrong tool version and unsupported platform are refused before any invocation
  malformed        unsupported schema version and a malformed but digest-correct catalog are refused
  evidence         the declared build/test/package command runs through Governance Verify/GateExecution
                   under the capture-lifetime join; missing, stale and mismatched evidence is refused

Nothing is skipped silently: a check whose prerequisite failed is recorded as `skipped` and the
verdict is not certified. Exit status 0 means certified; `check-report` validates a report's integrity
for the party that admits it. This is a local cooperative profile on the exact supported host
(Linux-x64, .NET 10.0.12, glibc 2.44); it is not hostile containment, a hosted-runner profile or
distribution, and it does not exercise an authentic blocked regular-file read or uncertain close.

  provider-certification.py certify      --request REQUEST.json --output FRESH_DIR
  provider-certification.py check-report REPORT.json
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

sys.dont_write_bytecode = True

HERE = Path(__file__).resolve().parent
REQUEST_SCHEMA = 'fsgg.provider-certification-request/v1'
REPORT_SCHEMA = 'fsgg.provider-certification/v1'
SCOPE = ('Local cooperative profile on the exact Linux-x64 / .NET 10.0.12 / glibc 2.44 host: not hostile containment, '
         'not a hosted-runner profile, no authentic blocked-read or uncertain-close case, no distribution.')
STATUSES = ('pass', 'fail', 'skipped')
REQUIRED_EVIDENCE_CASES = ('positive', 'missing', 'stale', 'mismatched')

NO_PARTIAL_CASES = {  # name -> expected diagnostic
    'occupied-target': 'catalog.targetExists',
    'catalog-digest': 'catalog.rawDigestMismatch',
    'policy-digest': 'catalog.policyInvalid',
    'archive-tamper': 'catalog.archiveDigestMismatch',
    'malformed-catalog': 'catalog.malformed',
    'unsupported-schema-version': 'catalog.unsupportedSchema',
    'wrong-tool-version': 'catalog.toolVersionRefused',
    'unsupported-platform': 'catalog.admission.UnresolvedPlatform',
}
STATIC_CHECKS = [
    ('lifecycle.scaffold', 'lifecycle'), ('lifecycle.dry-run', 'lifecycle'),
    ('lifecycle.strict-readback', 'lifecycle'), ('lifecycle.deterministic-instantiation', 'lifecycle'),
    ('provenance.inputs-bound', 'provenance'), ('provenance.tools-and-invocations', 'provenance'),
    ('provenance.declared-bindings', 'provenance'),
    ('skill-union.recorded-and-present', 'skill-union'), ('skill-union.mirrors-equal', 'skill-union'),
] + [('no-partial-state.' + name, 'no-partial-state') for name in NO_PARTIAL_CASES] + [
    ('evidence.whole-request-refusal', 'evidence'), ('evidence.capture-lifetime', 'evidence'),
]


def required_check_ids(evidence_cases=REQUIRED_EVIDENCE_CASES):
    return [check_id for check_id, _ in STATIC_CHECKS] + ['evidence.' + name for name in evidence_cases]


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


# ---------------------------------------------------------------- report integrity (admission side)

SHA = re.compile(r'sha256:[0-9a-f]{64}')


def validate_report(report):
    """Return a list of integrity problems; empty means the report is closed, complete and consistent."""
    problems = []
    def expect(condition, message):
        if not condition:
            problems.append(message)
    expect(isinstance(report, dict) and set(report) == {'schema', 'harness', 'host', 'tools', 'artifact', 'checks', 'verdict', 'scope'},
           'report must be a closed object with schema, harness, host, tools, artifact, checks, verdict, scope')
    if problems:
        return problems
    expect(report['schema'] == REPORT_SCHEMA, 'unsupported report schema')
    expect(isinstance(report['scope'], str) and report['scope'], 'scope statement required')
    harness = report['harness']
    expect(isinstance(harness, dict) and set(harness) == {'sourceRevision', 'sourceTreeSha256'}
           and re.fullmatch(r'[0-9a-f]{40}', str(harness.get('sourceRevision'))) and re.fullmatch(r'[0-9a-f]{64}', str(harness.get('sourceTreeSha256'))),
           'harness identity must be an exact revision and tree digest')
    host = report['host']
    expect(isinstance(host, dict) and set(host) == {'machine', 'glibc', 'runtimes'} and isinstance(host.get('runtimes'), list), 'host profile required')
    tools = report['tools']
    expect(isinstance(tools, dict) and set(tools) == {'sdd', 'governance'}, 'tool identities for sdd and governance required')
    for name, tool in (tools.items() if isinstance(tools, dict) else []):
        expect(isinstance(tool, dict) and set(tool) == {'version', 'sha256'} and isinstance(tool.get('version'), str)
               and SHA.fullmatch(str(tool.get('sha256'))), f'{name} tool needs version and sha256')
    artifact = report['artifact']
    expect(isinstance(artifact, dict) and set(artifact) == {'provider', 'platform', 'catalogDigest', 'policyDigest', 'archiveDigest'}
           and all(isinstance(artifact.get(k), str) and artifact[k] for k in ('provider', 'platform'))
           and all(SHA.fullmatch(str(artifact.get(k))) for k in ('catalogDigest', 'policyDigest', 'archiveDigest')),
           'artifact identity needs provider, platform and the three sha256 digests')
    checks = report['checks']
    expect(isinstance(checks, list) and checks, 'checks required')
    ids = []
    for check in checks if isinstance(checks, list) else []:
        valid = isinstance(check, dict) and set(check) == {'id', 'group', 'status', 'detail', 'evidence'} \
            and isinstance(check.get('id'), str) and isinstance(check.get('group'), str) and check.get('status') in STATUSES \
            and isinstance(check.get('detail'), str) and isinstance(check.get('evidence'), dict)
        expect(valid, 'every check is closed: id, group, status, detail, evidence')
        if valid:
            ids.append(check['id'])
            expect(check['status'] != 'pass' or check['evidence'], 'a passing check must carry evidence: ' + check['id'])
            expect(check['status'] == 'pass' or check['detail'], 'a failed or skipped check must say why: ' + check['id'])
    expect(len(ids) == len(set(ids)), 'check ids must be unique')
    missing = [c for c in required_check_ids() if c not in ids]
    expect(not missing, 'required checks absent: ' + ', '.join(missing))
    verdict = report['verdict']
    expect(isinstance(verdict, dict) and set(verdict) == {'certified', 'total', 'passed', 'failed', 'skipped'}, 'closed verdict required')
    if isinstance(verdict, dict) and isinstance(checks, list) and not problems:
        by = {s: sorted(c['id'] for c in checks if isinstance(c, dict) and c.get('status') == s) for s in STATUSES}
        expect(verdict.get('total') == len(checks) and verdict.get('passed') == len(by['pass'])
               and verdict.get('failed') == by['fail'] and verdict.get('skipped') == by['skipped'], 'verdict counts must match the checks')
        expect(verdict.get('certified') is (not missing and not by['fail'] and not by['skipped']),
               'certified must be true exactly when every required check is present and passed')
    def walk(value, path='$'):
        if isinstance(value, str) and (value.startswith('/') or re.search(r'(^|[\s=])/(home|tmp|var|usr)/', value)):
            problems.append('machine-specific absolute path in report at ' + path)
        elif isinstance(value, dict):
            for key, item in value.items():
                walk(item, path + '.' + str(key))
        elif isinstance(value, list):
            for index, item in enumerate(value):
                walk(item, f'{path}[{index}]')
    walk(report)
    return problems


# ---------------------------------------------------------------- the harness

class Harness:
    def __init__(self, request, output):
        self.request = request
        self.output = output
        self.runner = load('catalog_harness', 'catalog-runtime-validation.py')
        self.driver = load('catalog_driver', 'catalog-cli-acceptance.py')
        self.environment = dict(request['environment'])
        self.manifest = json.loads(Path(request['artifact']['request']).read_text())
        self.manifest['transportExecutable'] = request['sdd']['transportExecutable']
        self.counter = 0
        self.checks = []
        self.sidecar = []
        self.positive_target = None
        self.summary = None

    # -- execution
    def execute(self, name, argv, cwd=None, timeout=150, mode='native-runtime', **extra):
        self.counter += 1
        command = dict(name=name, argv=argv, cwd=str(cwd or self.output), timeoutSeconds=timeout, cleanupSeconds=30,
                       environment=dict(self.environment, **extra.pop('environment', {})), outputBytes=16 * 1024 * 1024, **extra)
        if mode == 'native-runtime':
            command.update(mode='native-runtime', backend='pid-namespace')
        actual = self.runner.run_command(command, self.output / 'runner' / f'{self.counter:02d}-{name}')
        out = Path(actual['streams']['stdout']['log']).read_text(errors='replace') if 'stdout' in actual['streams'] else ''
        err = Path(actual['streams']['stderr']['log']).read_text(errors='replace') if 'stderr' in actual['streams'] else ''
        self.sidecar.append({'name': name, 'exitCode': actual['exitCode'], 'cleanup': actual['cleanup'], 'firstFailure': actual['firstFailure'],
                             'durationSeconds': actual['durationSeconds'], 'captureLifetime': actual.get('captureLifetime'),
                             'workerScan': actual.get('workerScan')})
        return actual, out, err

    def scaffold(self, name, manifest, target, extra=()):
        argv = self.request['sdd']['cliCommand'] + self.driver.cli_arguments(manifest, target) + list(extra)
        actual, out, err = self.execute(name, argv, timeout=150)
        try:
            projection = json.loads(out)
        except ValueError:
            projection = None
        return actual, projection, err

    # -- check bookkeeping
    def record(self, check_id, group, status, detail='', evidence=None):
        # Reports are portable: machine-specific absolute paths never enter them.
        detail = re.sub(r'(?<![\w.])/(?:[\w.\-+@=]+/)+[\w.\-+@=]*', '<path>', detail)
        self.checks.append({'id': check_id, 'group': group, 'status': status, 'detail': detail, 'evidence': evidence or {}})
        print(f'{status.upper():7} {check_id}' + (f' :: {detail}' if detail else ''), flush=True)

    def check(self, check_id, group, function, skip_reason=None):
        if skip_reason is not None:
            return self.record(check_id, group, 'skipped', skip_reason)
        try:
            evidence = function()
            self.record(check_id, group, 'pass', '', evidence)
        except (AssertionError, KeyError, ValueError, OSError, TypeError) as error:
            self.record(check_id, group, 'fail', type(error).__name__ + ': ' + (str(error) or 'assertion failed'))

    # -- lifecycle / provenance / skills
    def positive_leg(self):
        workdir = self.output / 'targets'
        workdir.mkdir()
        target = workdir / 'positive'
        state = {}
        def scaffold_check():
            actual, projection, err = self.scaffold('scaffold-positive', self.manifest, target)
            assert actual['exitCode'] == 0 and actual['cleanup'] == 'direct-child-reaped-streams-eof', f'exit {actual["exitCode"]} {err[-200:]}'
            assert projection and projection['status'] == 'succeeded' and projection['ownership'] == 'Settled' and not projection['diagnostics'], \
                'positive scaffold did not settle: ' + json.dumps(projection)[:300]
            assert (target / '.fsgg/scaffold-provenance.json').is_file(), 'no schema-2 provenance written'
            assert self.driver.staging_snapshot(target.parent) == {}, 'owned staging remains after success'
            state['ok'] = True
            return {'status': projection['status'], 'ownership': projection['ownership'], 'stagingRemaining': 0}
        self.check('lifecycle.scaffold', 'lifecycle', scaffold_check)
        ok = state.get('ok', False)
        why = 'prerequisite lifecycle.scaffold did not pass'
        self.positive_target = target if ok else None

        def dry_run():
            dry = workdir / 'dry-run'
            actual, projection, _ = self.scaffold('scaffold-dry-run', self.manifest, dry, ['--dry-run'])
            assert actual['exitCode'] == 0 and projection['status'] == 'prepared' and projection['ownership'] == 'Settled'
            assert projection['observations'] is None and not dry.exists(), 'dry run observed or created state'
            return {'status': projection['status'], 'targetCreated': False}
        self.check('lifecycle.dry-run', 'lifecycle', dry_run)

        def readback():
            summary_path = self.output / 'provenance-summary.json'
            actual, _, err = self.execute('strict-readback', self.request['authoring']['checkCommand'] +
                                          [str(target / '.fsgg/scaffold-provenance.json'), str(summary_path)], timeout=60, mode='ordinary')
            assert actual['passed'], 'strict schema-2 readback refused: ' + err[-200:]
            self.summary = json.loads(summary_path.read_text())
            assert self.summary['schemaVersion'] == 2
            return {'schemaVersion': 2, 'invocationCount': self.summary['invocationCount']}
        self.check('lifecycle.strict-readback', 'lifecycle', readback, None if ok else why)
        ok_summary = ok and self.summary is not None

        def deterministic():
            second = workdir / 'second' / 'positive'
            second.parent.mkdir()
            actual, projection, _ = self.scaffold('scaffold-repeat', self.manifest, second)
            assert actual['exitCode'] == 0 and projection['status'] == 'succeeded', 'repeat scaffold refused'
            first_tree, second_tree = self.driver.target_snapshot(target), self.driver.target_snapshot(second)
            assert first_tree == second_tree, 'instantiations differ: ' + ', '.join(sorted(k for k in set(first_tree) | set(second_tree) if first_tree.get(k) != second_tree.get(k))[:5])
            return {'files': len(first_tree), 'treeSha256': hashlib.sha256(json.dumps(first_tree, sort_keys=True).encode()).hexdigest()}
        self.check('lifecycle.deterministic-instantiation', 'lifecycle', deterministic, None if ok else why)

        m, s = self.manifest, self.summary
        def inputs_bound():
            assert s['rawCatalogDigest'] == m['catalogDigest'] and s['policyDigest'] == m['policyDigest'] and s['archiveDigest'] == m['archiveDigest'], 'provenance digests differ from the packed inputs'
            assert s['platform'] == m['platform']
            params = s['effectiveParameters']
            assert params == sorted(params, key=lambda item: item[0]) and dict(params) == dict(m.get('effectiveParameters', m['overrides'])), 'effective parameters differ from the declared overrides'
            return {'catalogDigest': s['rawCatalogDigest'], 'policyDigest': s['policyDigest'], 'archiveDigest': s['archiveDigest'], 'platform': s['platform']}
        self.check('provenance.inputs-bound', 'provenance', inputs_bound, None if ok_summary else why)

        def tools():
            assert any(t['id'] == m.get('toolId', 'opaque-tool') and t['version'] == m.get('toolVersion', '10.0.401') for t in s['tools']), 'declared tool not observed at its exact version'
            assert s['invocationCount'] > 0
            creation = [i for i in s['invocations'] if '--output' in i['arguments']]
            assert len(creation) == 1 and creation[0]['exitCode'] == 0 and creation[0]['executable'] == 'dotnet', 'exactly one successful template creation expected'
            prefix = ['new', m.get('templateId', 'fsgg-catalog-opaque-fixture'), '--output', 'workspace', '--no-update-check', '--debug:custom-hive', 'engine']
            assert creation[0]['arguments'] == prefix + self.driver.template_parameter_arguments(s['effectiveParameters']), 'template argv is not the literal declared parameters'
            return {'tools': [{'id': t['id'], 'version': t['version']} for t in s['tools']], 'invocationCount': s['invocationCount']}
        self.check('provenance.tools-and-invocations', 'provenance', tools, None if ok_summary else why)

        def bindings():
            declared = [c for c in s['declaredCommands'] if c['capabilityId'] == m['requiredCapabilityId']]
            assert len(declared) == 1, 'the required capability is not declared exactly once'
            assert declared[0]['executable'] == m['fixtureExecutable'] and declared[0]['arguments'] == m['fixtureArguments'] and declared[0]['workingDirectory'] == '.'
            assert hashlib.sha256(Path(m['fixtureExecutable']).read_bytes()).hexdigest() == m['fixtureExecutableSha256'], 'declared executable bytes changed'
            assert any(e['format'] == m['requiredEvidenceFormat'] and e['path'] == m['requiredAbsentOutput'] for e in s['declaredEvidence']), 'declared evidence output missing'
            assert not (target / m['requiredAbsentOutput']).exists(), 'declared evidence output exists before execution'
            return {'capabilityId': m['requiredCapabilityId'], 'evidenceFormat': m['requiredEvidenceFormat'], 'argumentCount': len(declared[0]['arguments'])}
        self.check('provenance.declared-bindings', 'provenance', bindings, None if ok_summary else why)

        def skills():
            neutral = lambda paths: {p for p in paths if p.startswith('.agents/skills/') and p.endswith('/SKILL.md')}
            provider, owned = neutral(s['producedPaths']), neutral(s['sddOwnedPaths'])
            on_disk = {str(p.relative_to(target)) for p in (target / '.agents/skills').glob('*/SKILL.md')}
            assert provider, 'no provider-produced skill is recorded'
            assert not (provider & owned), 'a provider skill collides with an SDD-owned skill: ' + str(sorted(provider & owned)[:3])
            missing = sorted((provider | owned) - on_disk)
            assert not missing, 'recorded skills absent from the workspace: ' + str(missing[:3])
            assert all((target / p).stat().st_size > 0 for p in provider | owned), 'a recorded skill is empty'
            return {'providerSkills': len(provider), 'sddOwnedSkills': len(owned), 'seededUnrecorded': len(on_disk - provider - owned), 'onDisk': len(on_disk)}
        self.check('skill-union.recorded-and-present', 'skill-union', skills, None if ok_summary else why)

        def mirrors():
            neutral = {p.parent.name: p.read_bytes() for p in (target / '.agents/skills').glob('*/SKILL.md')}
            mirror = {p.parent.name: p.read_bytes() for p in (target / '.claude/skills').glob('*/SKILL.md')}
            assert neutral, 'no neutral skills in the workspace'
            assert set(neutral) == set(mirror), 'mirror set differs: ' + str(sorted(set(neutral) ^ set(mirror))[:3])
            differing = sorted(k for k in neutral if neutral[k] != mirror[k])
            assert not differing, 'mirror bytes differ from the neutral skill: ' + str(differing[:3])
            recorded = {p for p in s['mirroredPaths']}
            assert all(p.startswith('.claude/skills/') and p.split('/')[2] in mirror for p in recorded), 'a recorded mirror is absent'
            return {'skills': len(neutral), 'recordedMirrors': len(recorded)}
        self.check('skill-union.mirrors-equal', 'skill-union', mirrors, None if ok_summary else why)

    # -- refusals: no partial state, malformed and unsupported provider controls
    def refusal_leg(self):
        m = self.manifest
        controls = self.output / 'controls'
        controls.mkdir()
        sha = lambda raw: 'sha256:' + hashlib.sha256(raw).hexdigest()
        encode = lambda value: json.dumps(value, separators=(',', ':')).encode()
        catalog, policy = json.loads(Path(m['catalog']).read_text()), json.loads(Path(m['policy']).read_text())
        def write(name, raw):
            path = controls / name
            path.write_bytes(raw)
            return str(path)
        tampered = write('tampered.nupkg', Path(m['archive']).read_bytes() + b'tampered-fixture-control')
        bad_schema = dict(catalog, schemaVersion=99)
        malformed = dict(catalog, unexpectedMember=1)
        probe = policy['toolProbes'][0]
        wrong = controls / 'wrong-version-tool'
        wrong.write_text('#!/bin/sh\nprintf "%s%s%s" ' + json.dumps(probe['versionPrefix']) + ' "0.0.0-certification-wrong" ' + json.dumps(probe['versionSuffix']) + '\n')
        wrong.chmod(0o700)
        wrong_policy = json.loads(json.dumps(policy))
        wrong_policy['toolProbes'][0]['executable'] = str(wrong)
        variants = {
            'occupied-target': {},
            'catalog-digest': {'catalogDigest': 'sha256:' + '0' * 64},
            'policy-digest': {'policyDigest': 'sha256:' + '0' * 64},
            'archive-tamper': {'archive': tampered},
            'malformed-catalog': {'catalog': write('malformed-catalog.json', encode(malformed)), 'catalogDigest': sha(encode(malformed))},
            'unsupported-schema-version': {'catalog': write('schema-99-catalog.json', encode(bad_schema)), 'catalogDigest': sha(encode(bad_schema))},
            'wrong-tool-version': {'policy': write('wrong-version-policy.json', encode(wrong_policy)), 'policyDigest': sha(encode(wrong_policy))},
            'unsupported-platform': {'platform': 'unsupported-certification-platform'},
        }
        for name, expected in NO_PARTIAL_CASES.items():
            def run_case(name=name, expected=expected):
                parent = self.output / 'refusals' / name
                parent.mkdir(parents=True)
                target = parent / 'target'
                if name == 'occupied-target':
                    target.mkdir()
                    (target / 'keep.txt').write_bytes(b'owned by existing caller')
                prior, staging = self.driver.target_snapshot(target), self.driver.staging_snapshot(parent)
                actual, projection, err = self.scaffold('refuse-' + name, dict(m, **variants[name]), target)
                assert actual['exitCode'] == 1 and actual['firstFailure'] == 'nonzero-exit' and actual['cleanup'] == 'direct-child-reaped-streams-eof', \
                    f'outer safeguard or crash instead of a product refusal: exit {actual["exitCode"]} {actual["firstFailure"]}'
                assert projection and projection['status'] in ('failed', 'refused') and projection['diagnostics'], 'no refusal projection'
                codes = {d['code'] for d in projection['diagnostics']}
                assert expected in codes, f'expected {expected}, observed {sorted(codes)}'
                assert projection['ownership'] in ('Settled', 'not-started', 'NotStarted'), 'ownership not settled: ' + str(projection['ownership'])
                assert self.driver.target_snapshot(target) == prior, 'target changed by a refused request'
                assert self.driver.staging_snapshot(parent) == staging, 'owned staging remains after a refusal'
                if name != 'occupied-target':
                    assert not target.exists(), 'a refused request created its target'
                return {'diagnostic': expected, 'status': projection['status'], 'ownership': projection['ownership'], 'targetUnchanged': True, 'stagingUnchanged': True}
            self.check('no-partial-state.' + name, 'no-partial-state', run_case)

    # -- Governance Verify/GateExecution under the capture-lifetime join
    def evidence_leg(self):
        gov = self.request['governance']
        m = self.manifest
        why = 'prerequisite lifecycle.scaffold did not pass'
        cases = gov['evidenceCases']
        if self.positive_target is None:
            for case in cases:
                self.record('evidence.' + case['name'], 'evidence', 'skipped', why)
            self.record('evidence.whole-request-refusal', 'evidence', 'skipped', why)
            self.record('evidence.capture-lifetime', 'evidence', 'skipped', why)
            return
        workspace = self.output / 'workspace'
        shutil.copytree(self.positive_target, workspace, symlinks=True)
        git_env = dict(os.environ, **self.environment)
        def git(*args):
            return subprocess.run(['git', '-C', str(workspace), '-c', 'user.name=certify', '-c', 'user.email=certify@invalid',
                                   '-c', 'commit.gpgsign=false', *args], capture_output=True, text=True, env=git_env, check=True)
        git('init', '-q'); git('add', '-A'); git('commit', '-qm', 'scaffolded workspace')
        for source in sorted(Path(gov['workspaceCatalog']).iterdir()):
            shutil.copy(source, workspace / '.fsgg' / source.name)
        git('add', '-A'); git('commit', '-qm', 'governance workspace catalog')
        provenance = '.fsgg/scaffold-provenance.json'
        evidence_path = m['requiredAbsentOutput']
        window = gov['captureLifetime']
        file_sha = lambda path: hashlib.sha256(Path(path).read_bytes()).hexdigest()
        base = ['--paths', *gov['verifyPaths'], '--provider-provenance', provenance, '--provider-provenance-sha256', file_sha(workspace / provenance),
                '--provider-policy', m['policy'], '--provider-policy-sha256', file_sha(m['policy']), '--provider-platform', m['platform']]
        lifetimes = []
        def verify(name, case_environment, tail=None, stale=False, supply_ends=True):
            repo = self.output / 'verify' / name
            repo.parent.mkdir(exist_ok=True)
            subprocess.run(['git', 'clone', '-q', str(workspace), str(repo)], check=True, env=git_env)
            if stale:
                (repo / evidence_path).parent.mkdir(parents=True, exist_ok=True)
                (repo / evidence_path).write_text('stale')
            extra = {'captureLifetime': window} if supply_ends else {}
            argv = [gov['tool'], 'verify', '--repo', str(repo), '--json', '--verify-out', str(self.output / 'verify' / (name + '-verify.json')),
                    '--cost-budget-out', str(self.output / 'verify' / (name + '-cost-budget.json')),
                    '--provenance-out', str(self.output / 'verify' / (name + '-provenance.json'))] + (tail or base)
            actual, out, err = self.execute('verify-' + name, argv, timeout=gov.get('timeoutSeconds', 150), environment=case_environment, **extra)
            capture = actual.get('captureLifetime')
            if supply_ends:
                lifetimes.append((name, capture, actual.get('workerScan'), actual['durationSeconds']))
            produced = (repo / evidence_path).is_file()
            return actual, out + err, repo, produced, (repo / evidence_path).read_text() if produced else None
        for case in cases:
            def run_case(case=case):
                actual, text, repo, produced, content = verify(case['name'], case.get('environment', {}), stale=case.get('staleEvidence', False))
                assert actual['exitCode'] == case['expectedExit'], f'exit {actual["exitCode"]} (expected {case["expectedExit"]}): {text[-250:]}'
                if case.get('expectedDiagnostic'):
                    assert case['expectedDiagnostic'] in text, 'intended diagnostic absent: ' + case['expectedDiagnostic']
                if case['expectedExit'] == 0:
                    verdict = json.loads((self.output / 'verify' / (case['name'] + '-verify.json')).read_text())
                    executed = [i.get('execution', {}).get('disposition') for i in verdict.get('passing', [])]
                    assert verdict.get('verdict') == 'pass' and executed == ['executed'], 'Verify did not execute and pass the declared command'
                    assert produced and content != 'stale', 'declared evidence was not produced by the run'
                if case.get('staleEvidence'):
                    assert content == 'stale', 'stale evidence was overwritten or accepted'
                return {'exit': actual['exitCode'], 'diagnostic': case.get('expectedDiagnostic'), 'evidenceProduced': produced and content != 'stale'}
            self.check('evidence.' + case['name'], 'evidence', run_case)

        def whole_request():
            tail = list(base)
            tail[tail.index('--provider-provenance-sha256') + 1] = '0' * 64
            actual, text, repo, produced, _ = verify('whole-request-refusal', {}, tail=tail)
            assert actual['exitCode'] == 3 and 'RawDigestMismatch' in text, f'exit {actual["exitCode"]}: {text[-250:]}'
            assert not produced, 'a refused whole request launched the declared command'
            return {'exit': 3, 'diagnostic': 'RawDigestMismatch', 'commandLaunched': False}
        self.check('evidence.whole-request-refusal', 'evidence', whole_request)

        def lifetime():
            assert lifetimes, 'no Verify invocation ran'
            settled = 0
            for name, capture, scan, duration in lifetimes:
                assert capture is not None and not capture['outerRuntimeEnded'] and capture['withinAdmittedWindow'], f'{name}: host did not exit by itself inside the admitted window'
                assert scan is not None and scan['survivors'] == [], f'{name}: a worker process survived teardown'
                assert capture['outcome'] in ('host-exited-after-settlement', 'host-refused-input-unavailable'), f'{name}: {capture["outcome"]}'
                settled += capture['hostSettled']
            assert settled >= 1, 'no invocation settled a held capture session'
            return {'invocations': len(lifetimes), 'settledCaptures': settled, 'workerSurvivors': 0, 'admittedWindowSeconds': lifetimes[0][1]['admittedWindowSeconds']}
        self.check('evidence.capture-lifetime', 'evidence', lifetime)


def host_profile():
    libc = subprocess.run(['ldd', '--version'], capture_output=True, text=True).stdout.splitlines()
    runtimes = subprocess.run(['/usr/share/dotnet/dotnet', '--list-runtimes'], capture_output=True, text=True).stdout.splitlines()
    return {'machine': os.uname().machine, 'glibc': libc[0].split()[-1] if libc else 'unknown',
            'runtimes': sorted(line.split()[1] for line in runtimes if line.startswith('Microsoft.NETCore.App '))}


def validate_request(request):
    keys = {'schema', 'sourceRoot', 'identityPaths', 'environment', 'sdd', 'authoring', 'governance', 'artifact', 'tools'}
    if not isinstance(request, dict) or set(request) != keys or request['schema'] != REQUEST_SCHEMA:
        raise ValueError('closed certification request required: ' + ', '.join(sorted(keys)))
    cases = request['governance'].get('evidenceCases')
    names = [c.get('name') for c in cases or []]
    if len(set(names)) != len(names) or not set(REQUIRED_EVIDENCE_CASES) <= set(names):
        raise ValueError('governance.evidenceCases must name distinct cases including ' + ', '.join(REQUIRED_EVIDENCE_CASES))
    for case in cases:
        if set(case) - {'name', 'environment', 'staleEvidence', 'expectedExit', 'expectedDiagnostic'} or not isinstance(case.get('expectedExit'), int):
            raise ValueError('evidence case needs name and expectedExit and allows environment, staleEvidence, expectedDiagnostic: ' + str(case.get('name')))
    positive = [c for c in cases if c['name'] == 'positive'][0]
    if positive['expectedExit'] != 0:
        raise ValueError('the positive evidence case must expect exit 0')
    for name in ('sdd', 'governance'):
        tool = request['tools'].get(name)
        if not isinstance(tool, dict) or set(tool) != {'version', 'sha256'} or not SHA.fullmatch(str(tool.get('sha256'))):
            raise ValueError(f'tools.{name} needs version and a sha256:<64 hex> package digest')
    if not Path(request['artifact']['request']).is_file():
        raise ValueError('artifact.request must be the emitted fixture manifest')


def certify(args):
    request = json.loads(Path(args.request).read_text())
    validate_request(request)
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=False)
    harness = Harness(request, output)
    runner = harness.runner
    source = Path(request['sourceRoot']).resolve()
    before = runner.tree_identity(source, request['identityPaths'])
    print('certifying provider artifact ' + harness.manifest['provider'], flush=True)
    harness.positive_leg()
    harness.refusal_leg()
    harness.evidence_leg()
    after = runner.tree_identity(source, request['identityPaths'])
    if before != after:
        harness.record('harness.source-identity-stable', 'harness', 'fail', 'harness source changed during the run')
    manifest = harness.manifest
    report = {
        'schema': REPORT_SCHEMA,
        'harness': {'sourceRevision': subprocess.run(['git', '-C', str(source), 'rev-parse', 'HEAD'], capture_output=True, text=True).stdout.strip(),
                    'sourceTreeSha256': before},
        'host': host_profile(),
        'tools': request['tools'],
        'artifact': {'provider': manifest['provider'], 'platform': manifest['platform'], 'catalogDigest': manifest['catalogDigest'],
                     'policyDigest': manifest['policyDigest'], 'archiveDigest': manifest['archiveDigest']},
        'checks': harness.checks,
        'scope': SCOPE,
    }
    by = {s: sorted(c['id'] for c in harness.checks if c['status'] == s) for s in STATUSES}
    present = {c['id'] for c in harness.checks}
    complete = all(c in present for c in required_check_ids())
    report['verdict'] = {'certified': complete and not by['fail'] and not by['skipped'], 'total': len(harness.checks),
                         'passed': len(by['pass']), 'failed': by['fail'], 'skipped': by['skipped']}
    problems = validate_report(report)
    if problems:
        report['verdict']['certified'] = False
        print('REPORT INTEGRITY PROBLEMS: ' + '; '.join(problems), file=sys.stderr)
    (output / 'certification-report.json').write_text(json.dumps(report, indent=2) + '\n')
    (output / 'certification-evidence.json').write_text(json.dumps(harness.sidecar, indent=2) + '\n')
    print(json.dumps({'certified': report['verdict']['certified'], 'passed': report['verdict']['passed'], 'total': report['verdict']['total'],
                      'failed': by['fail'], 'skipped': by['skipped']}))
    for owner in runner.INCOMPLETE_OWNERS:
        owner.wait()
    return 0 if report['verdict']['certified'] and not problems else 1


def check_report(args):
    report = json.loads(Path(args.report).read_text())
    problems = validate_report(report)
    print(json.dumps({'valid': not problems, 'certified': bool(report.get('verdict', {}).get('certified')) if isinstance(report, dict) else False, 'problems': problems}))
    return 0 if not problems and report['verdict']['certified'] else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest='mode', required=True)
    run = sub.add_parser('certify')
    run.add_argument('--request', required=True)
    run.add_argument('--output', required=True)
    check = sub.add_parser('check-report')
    check.add_argument('report')
    args = parser.parse_args()
    return certify(args) if args.mode == 'certify' else check_report(args)


if __name__ == '__main__':
    raise SystemExit(main())
