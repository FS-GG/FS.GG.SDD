#!/usr/bin/env python3
"""Qualify the reference provider through the reusable certification harness (SDD928 C3).

Installs the PUBLISHED SDD CLI and Governance CLI from nuget.org into an empty private cache, builds
the typed authoring example against published packages, packs the reference provider with `emit`,
and runs scripts/provider-certification.py against that packed artifact. Then it proves the harness
is not vacuous: a deliberately bad provider (its fixture writes evidence citing the wrong bytes in the
positive case) must NOT certify, and tampering with the certified report must be detected.

This is ordinary development verification (ADR-0096) on the exact supported host (Linux-x64, .NET
10.0.12, glibc 2.44) with at most six CPUs, a credential-free environment and nuget.org as the only
package source. It is a local cooperative profile; it is not distribution (C4) and not the schema-v1
window, and the reference provider's policy, fixture and Governance catalog are SDD-owner fixtures.

  qualify-reference-provider.py run --work FRESH_DIR [--sdd-version 2.5.1] [--governance-version 1.15.0]
"""
import argparse
import copy
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
ROOT = HERE.parent


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


journey_module = load('verify_capture_journey', 'verify-capture-journey.py')
certification = load('provider_certification', 'provider-certification.py')

EVIDENCE_CASES = [
    {'name': 'positive', 'environment': {'FSGG_FIXTURE_MODE': 'write'}, 'expectedExit': 0},
    {'name': 'missing', 'environment': {'FSGG_FIXTURE_MODE': 'none'}, 'expectedExit': 1, 'expectedDiagnostic': 'was not produced by the run'},
    {'name': 'stale', 'environment': {'FSGG_FIXTURE_MODE': 'write'}, 'staleEvidence': True, 'expectedExit': 1, 'expectedDiagnostic': 'existed before launch'},
    {'name': 'mismatched', 'environment': {'FSGG_FIXTURE_MODE': 'mismatch'}, 'expectedExit': 1, 'expectedDiagnostic': 'sources do not match the workspace'},
    {'name': 'nonzero-exit', 'environment': {'FSGG_FIXTURE_MODE': 'exit2'}, 'expectedExit': 1, 'expectedDiagnostic': 'exited 2'},
]


def run_certify(journey, request, name, expect_certified):
    output = journey.work / 'certification' / name
    request_path = journey.work / 'certification' / (name + '-request.json')
    request_path.parent.mkdir(exist_ok=True)
    request_path.write_text(json.dumps(request, indent=2))
    completed = subprocess.run([sys.executable, '-I', '-B', str(HERE / 'provider-certification.py'), 'certify', '--request', str(request_path),
                                '--output', str(output)], env=dict(os.environ, **journey.environment), capture_output=True, text=True, timeout=1800)
    report = json.loads((output / 'certification-report.json').read_text()) if (output / 'certification-report.json').is_file() else None
    return completed, report, output


def run(args):
    if os.environ.get('VERIFY_CAPTURE_JOURNEY_CLEAN') != '1':
        clean = {'VERIFY_CAPTURE_JOURNEY_CLEAN': '1', 'HOME': os.environ.get('HOME', '/'), 'PATH': '/usr/local/bin:/usr/bin:/bin',
                 'LANG': 'C.UTF-8', 'PYTHONDONTWRITEBYTECODE': '1'}
        os.execve(sys.executable, [sys.executable, '-I', '-B', *sys.argv], clean)
    work = Path(args.work).resolve()
    if work.exists() and any(work.iterdir()):
        raise SystemExit('--work must be a fresh empty directory')
    work.mkdir(parents=True, exist_ok=True)
    os.sched_setaffinity(0, set(sorted(os.sched_getaffinity(0))[:journey_module.MAXIMUM_CPUS]))
    jm = journey_module
    journey = jm.Journey(work, args.sdd_version, args.governance_version, dict(jm.PUBLISHED_SHA256))
    journey.environment = jm.prepare_environment(work)
    facts = jm.host_facts()
    journey.record('host is the supported Linux-x64 / .NET 10.0.12 / glibc 2.44 profile with at most six CPUs',
                   facts['machine'] == 'x86_64' and facts['glibc'] == '2.44' and '10.0.12' in facts['runtimes'] and len(facts['cpus']) <= jm.MAXIMUM_CPUS,
                   json.dumps(facts))
    tools = jm.install_tools(journey)
    dll = jm.build_authoring(journey)
    fixture = jm.prepare_inputs(journey)
    sdd_dll = jm.locate_sdd_dll(work, args.sdd_version)
    artifact = work / 'artifact'
    if dll is not None and sdd_dll is not None and all(t['nupkgSha256'] for t in tools.values()):
        jm.authoring_controls(journey, dll, fixture)
        actual, out, err = journey.step('pack-reference-provider', [jm.DOTNET, str(dll), 'emit', '--template-root', str(work / 'inputs/template'),
                                        '--fixture-executable', str(fixture), '--test-input', str(work / 'inputs/tests-input.json'),
                                        '--policy', str(work / 'inputs/policy.json'), '--out', str(artifact)], timeout=120)
        journey.record('the reference provider packs deterministically through the typed authoring example', actual['passed'] and (artifact / 'request.json').is_file())
    if not (artifact / 'request.json').is_file():
        return finish(journey, facts, tools, {}, None)
    request = {
        'schema': certification.REQUEST_SCHEMA, 'sourceRoot': str(ROOT), 'identityPaths': jm.IDENTITY_PATHS + ['scripts/provider-certification.py'],
        'environment': journey.environment,
        'sdd': {'cliCommand': [jm.DOTNET, 'exec', '--fx-version', '10.0.12', str(sdd_dll)], 'transportExecutable': jm.DOTNET},
        'authoring': {'checkCommand': [jm.DOTNET, str(dll), 'check']},
        'governance': {'tool': tools['FS.GG.Governance.Cli']['executable'], 'workspaceCatalog': str(jm.FIXTURES / 'fsgg'),
                       'verifyPaths': ['inputs/tests.json'], 'timeoutSeconds': jm.VERIFY_TIMEOUT,
                       'captureLifetime': {'workSeconds': jm.CAPTURE_WORK, 'cleanupEndSeconds': jm.CAPTURE_CLEANUP_END,
                                           'startupAllowanceSeconds': jm.CAPTURE_STARTUP_ALLOWANCE},
                       'evidenceCases': EVIDENCE_CASES},
        'artifact': {'request': str(artifact / 'request.json')},
        'tools': {'sdd': {'version': args.sdd_version, 'sha256': 'sha256:' + tools['FS.GG.SDD.Cli']['nupkgSha256']},
                  'governance': {'version': args.governance_version, 'sha256': 'sha256:' + tools['FS.GG.Governance.Cli']['nupkgSha256']}},
    }
    summary = {}
    good, report, output = run_certify(journey, request, 'reference-provider', True)
    ok = good.returncode == 0 and report is not None and report['verdict']['certified'] and not certification.validate_report(report)
    journey.record('the reference provider certifies from its packed artifact with every required check passing', ok,
                   '' if ok else (json.dumps(report['verdict']) if report else good.stdout[-300:] + good.stderr[-300:]))
    if report:
        required = certification.required_check_ids()
        journey.record(f'all {len(required)} required checks are present and passed ({report["verdict"]["passed"]} checks)',
                       report['verdict']['passed'] == report['verdict']['total'] and set(required) <= {c['id'] for c in report['checks']})
        checked = subprocess.run([sys.executable, '-I', '-B', str(HERE / 'provider-certification.py'), 'check-report', str(output / 'certification-report.json')],
                                 capture_output=True, text=True)
        journey.record('check-report accepts the certified report', checked.returncode == 0, checked.stdout.strip()[:200])
        summary['certifiedChecks'] = report['verdict']
        tampered = copy.deepcopy(report)
        victim = next(c for c in tampered['checks'] if c['id'] == 'no-partial-state.archive-tamper')
        mutations = {
            'a required check removed': lambda r: r['checks'].remove(victim),
            'a failed check still certified': lambda r: victim.update(status='fail', detail='x'),
            'a skipped check still certified': lambda r: victim.update(status='skipped', detail='x'),
            'an unknown member added': lambda r: r.update(extra=1),
            'a digest malformed': lambda r: r['artifact'].update(archiveDigest='sha256:zz'),
            'an absolute path leaked': lambda r: victim['evidence'].update(path='/home/developer/x'),
        }
        for label, mutate in mutations.items():
            candidate = copy.deepcopy(tampered)
            victim = next(c for c in candidate['checks'] if c['id'] == 'no-partial-state.archive-tamper')
            mutate(candidate)
            journey.record(f'report integrity: {label} is detected', bool(certification.validate_report(candidate)))
    bad_request = copy.deepcopy(request)
    for case in bad_request['governance']['evidenceCases']:
        if case['name'] == 'positive':
            case['environment'] = {'FSGG_FIXTURE_MODE': 'mismatch'}  # a provider whose evidence cites the wrong bytes
    bad, bad_report, _ = run_certify(journey, bad_request, 'bad-provider', False)
    journey.record('a provider whose evidence cites the wrong bytes is NOT certified, exit nonzero, failing exactly evidence.positive',
                   bad.returncode == 1 and bad_report is not None and not bad_report['verdict']['certified']
                   and bad_report['verdict']['failed'] == ['evidence.positive'] and not bad_report['verdict']['skipped']
                   and not certification.validate_report(bad_report),
                   json.dumps(bad_report['verdict']) if bad_report else bad.stdout[-300:])
    manifest = json.loads((artifact / 'request.json').read_text())
    # A provider whose declared executable does not match its declared digest fails provenance, not evidence.
    (work / 'artifact-bad-bindings').mkdir()
    (work / 'artifact-bad-bindings/request.json').write_text(json.dumps(dict(manifest, fixtureExecutableSha256='0' * 64)))
    bad_bindings = copy.deepcopy(request)
    bad_bindings['artifact'] = {'request': str(work / 'artifact-bad-bindings/request.json')}
    result, result_report, _ = run_certify(journey, bad_bindings, 'bad-bindings', False)
    journey.record('a provider with a mismatched declared executable digest is NOT certified, failing exactly provenance.declared-bindings',
                   result.returncode == 1 and result_report is not None and not result_report['verdict']['certified']
                   and result_report['verdict']['failed'] == ['provenance.declared-bindings'] and not result_report['verdict']['skipped']
                   and not certification.validate_report(result_report),
                   json.dumps(result_report['verdict']) if result_report else result.stdout[-300:])
    # A provider whose scaffold cannot run: dependants are skipped with a reason and nothing certifies.
    (work / 'artifact-unrunnable').mkdir()
    (work / 'artifact-unrunnable/request.json').write_text(json.dumps(dict(manifest, archive=str(work / 'artifact-unrunnable/absent.nupkg'))))
    unrunnable = copy.deepcopy(request)
    unrunnable['artifact'] = {'request': str(work / 'artifact-unrunnable/request.json')}
    skipped, skipped_report, _ = run_certify(journey, unrunnable, 'unrunnable-provider', False)
    verdict = skipped_report['verdict'] if skipped_report else {}
    journey.record('a provider whose scaffold cannot run fails lifecycle.scaffold, its dependants are skipped with a reason, nothing certifies',
                   skipped.returncode == 1 and skipped_report is not None and not verdict['certified'] and 'lifecycle.scaffold' in verdict['failed']
                   and {'lifecycle.strict-readback', 'provenance.inputs-bound', 'skill-union.mirrors-equal'} <= set(verdict['skipped'])
                   and all(c['detail'] for c in skipped_report['checks'] if c['status'] != 'pass') and not certification.validate_report(skipped_report),
                   json.dumps(verdict)[:400])
    return finish(journey, facts, tools, summary, report)


def finish(journey, facts, tools, summary, report):
    failed = [r for r in journey.results if not r['passed']]
    result = {'passed': not failed, 'cases': len(journey.results), 'failed': [r['name'] for r in failed], 'host': facts,
              'sddVersion': journey.sdd_version, 'governanceVersion': journey.governance_version,
              'tools': {k: {'version': v['version'], 'nupkgSha256': v['nupkgSha256']} for k, v in tools.items()},
              'summary': summary, 'results': journey.results}
    (journey.work / 'qualification-result.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps({'passed': result['passed'], 'cases': result['cases'], 'failed': result['failed']}))
    for owner in journey_module.harness.INCOMPLETE_OWNERS:
        owner.wait()
    return 0 if result['passed'] else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest='mode', required=True)
    r = sub.add_parser('run')
    r.add_argument('--work', required=True)
    r.add_argument('--sdd-version', default='2.5.1')
    r.add_argument('--governance-version', default='1.15.0')
    return run(parser.parse_args())


if __name__ == '__main__':
    raise SystemExit(main())
