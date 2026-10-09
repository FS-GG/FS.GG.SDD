#!/usr/bin/env python3
"""Actual compiled CLI acceptance through the maintained execution harness.

Fixture generation and strict provenance verification call the existing compiled
F# fixture/helper API. This script contains no package or digest canonicalizer.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import time

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location('catalog_harness', Path(__file__).with_name('catalog-runtime-validation.py'))
harness = importlib.util.module_from_spec(spec)
spec.loader.exec_module(harness)

EXPECTED_PRODUCT = b'raw=Awkward name!?\npackage=example.org/explicit/module\ncode=IndependentCode\nempty=\nliteral=$(literal);& with spaces\n'


def target_snapshot(root):
    if not root.exists():
        return None
    return {str(path.relative_to(root)): ('link', str(path.readlink())) if path.is_symlink() else
            ('file', hashlib.sha256(path.read_bytes()).hexdigest()) if path.is_file() else ('directory', '')
            for path in sorted(root.rglob('*'))}


def staging_snapshot(parent):
    return {path.name: target_snapshot(path) for path in sorted(parent.glob('.fsgg-catalog-*'))}


def cli_arguments(fixture, target):
    argv = ['scaffold', '--catalog', fixture['catalog'], '--catalog-sha256', fixture['catalogDigest'],
            '--provider', fixture['provider'], '--root', str(target), '--template-archive', fixture['archive'],
            '--template-sha256', fixture['archiveDigest'], '--admission-policy', fixture['policy'],
            '--admission-policy-sha256', fixture['policyDigest'], '--platform', fixture['platform'],
            '--transport-executable', fixture['transportExecutable'],
            '--preflight-timeout-seconds', str(fixture['preflightSeconds']),
            '--scaffold-timeout-seconds', str(fixture['scaffoldSeconds']), '--json']
    for key, value in fixture['overrides']:
        argv += ['--param', key + '=' + value]
    return argv


def replace_value(argv, flag, value):
    result = list(argv)
    result[result.index(flag) + 1] = value
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--case', choices=['positive', 'negatives', 'probes', 'variants', 'all'], default='positive')
    args = parser.parse_args()
    config = json.loads(args.config.read_text())
    fixture = json.loads(Path(config['fixtureManifest']).read_text())
    fixture['transportExecutable'] = config['transportExecutable']
    args.output.mkdir(parents=True, exist_ok=False)
    source = Path(config['sourceRoot']).resolve()
    before = harness.tree_identity(source, config.get('identityPaths'))
    report = {'sourceTreeSha256': before, 'cases': [], 'passed': False,
              'productEnvironmentQualified': False, 'acceptanceScope': args.case,
              'generatorAssemblySha256': fixture['generatorAssemblySha256'],
              'cliAssemblySha256': hashlib.sha256(Path(config['cliCommand'][-1]).read_bytes()).hexdigest(),
              'archiveSha256': hashlib.sha256(Path(fixture['archive']).read_bytes()).hexdigest()}
    base = dict(config['runtime'])
    if base.get('mode') != 'native-runtime' or base.get('backend') != 'pid-namespace':
        parser.error('actual CLI cases require explicit native-runtime PID namespace backend')
    original_archive = Path(fixture['archive']).read_bytes()
    tampered = args.output / 'tampered.nupkg'
    tampered.write_bytes(original_archive + b'tampered-fixture-control')
    cases = [('positive', None), ('dryrun', '--dry-run'), ('occupied-target', None),
             ('catalog-digest', '--catalog-sha256'), ('policy-digest', '--admission-policy-sha256'),
             ('archive-tamper', '--template-archive'), ('unsupported-platform', '--platform'),
             ('duplicate-option', '--provider')]
    if args.case == 'positive':
        cases = cases[:1]
    elif args.case == 'negatives':
        cases = cases[1:]
    elif args.case in ('probes', 'variants'):
        cases = []
    probe_cases = {}
    if args.case in ('probes', 'all') and config.get('negativeProbes'):
        data = json.loads(Path(config['negativeProbes']).read_text())
        assert data['schema'] == 'fsgg.catalog-cli-negative-probes/v1'
        probe_cases = {case['name']: case for case in data['cases']}
        cases += [(name, None) for name in probe_cases]
    variants = config.get('fixtureVariants', {}) if args.case in ('variants', 'all') else {}
    cases += [(name, None) for name in variants]
    original_end = time.monotonic() + config.get('totalSeconds', 300)
    try:
        for name, changed_flag in cases:
            remaining = original_end - time.monotonic()
            if remaining <= 0:
                raise AssertionError('original acceptance budget expired')
            target = args.output / (name + '-target')
            if name == 'occupied-target':
                target.mkdir()
                (target / 'keep.txt').write_bytes(b'owned by existing caller')
            prior = target_snapshot(target)
            staging_before = staging_snapshot(target.parent)
            selected_fixture = fixture
            if name in variants:
                selected_fixture = json.loads(Path(variants[name]['manifest']).read_text())
                selected_fixture['transportExecutable'] = fixture['transportExecutable']
            if name in probe_cases:
                probe = probe_cases[name]
                code = probe['pythonCode'].replace('{{VERSION}}', '10.0.401').replace('{{TARGET}}', json.dumps(str(target)))
                python = Path(config['probeExecutable']).resolve(strict=True)
                policy = json.loads(Path(fixture['policy']).read_text())
                matches = [row for row in policy['toolProbes'] if row['id'] == 'opaque-tool']
                assert len(matches) == 1
                matches[0]['executable'] = str(python)
                matches[0]['arguments'] = ['-c', code]
                policy_path = args.output / (name + '-policy.json')
                policy_path.write_text(json.dumps(policy, ensure_ascii=False))
                manifest = args.output / (name + '-request.json')
                preparation = harness.run_command(dict(config['verification'], name=name + '-seal-policy',
                    argv=config['sealPolicyCommand'] + [str(config['fixtureManifest']), str(policy_path), str(manifest)],
                    timeoutSeconds=min(30, original_end - time.monotonic())), args.output / (name + '-prepare'))
                assert preparation['passed'], name + ': production policy parser refused fixture'
                selected_fixture = json.loads(manifest.read_text())
                selected_fixture['transportExecutable'] = fixture['transportExecutable']
            argv = cli_arguments(selected_fixture, target)
            if name == 'dryrun':
                argv += ['--dry-run']
            elif name in ('catalog-digest', 'policy-digest'):
                argv = replace_value(argv, changed_flag, 'sha256:' + '0' * 64)
            elif name == 'archive-tamper':
                argv = replace_value(argv, changed_flag, str(tampered))
            elif name == 'unsupported-platform':
                argv = replace_value(argv, changed_flag, 'unsupported-fixture-platform')
            elif name == 'duplicate-option':
                argv += ['--provider', fixture['provider']]
            command = dict(base, name=name, argv=config['cliCommand'] + argv,
                           timeoutSeconds=min(base.get('timeoutSeconds', 150), remaining))
            actual = harness.run_command(command, args.output / name)
            case = {'name': name, 'execution': actual, 'passed': False}
            report['cases'].append(case)
            if actual['cleanup'] != 'direct-child-reaped-streams-eof':
                raise AssertionError(name + ': actual cleanup incomplete')
            projection = json.loads(Path(actual['streams']['stdout']['log']).read_text())
            case['projection'] = projection
            case['stagingBefore'] = staging_before
            case['stagingAfter'] = staging_snapshot(target.parent)
            assert case['stagingAfter'] == staging_before, name + ': owned staging remains or changed'
            if name == 'positive':
                assert actual['passed'] and projection['status'] == 'succeeded' and projection['ownership'] == 'Settled', \
                    'positive CLI refused: ' + ', '.join(d['code'] for d in projection['diagnostics'])
                assert (target / 'product.txt').read_bytes() == EXPECTED_PRODUCT
                provenance = target / '.fsgg/scaffold-provenance.json'
                assert provenance.is_file()
                summary = args.output / 'provenance-summary.json'
                remaining = original_end - time.monotonic()
                if remaining <= 0:
                    raise AssertionError('original acceptance budget expired before strict readback')
                verify = dict(config['verification'], name='strict-provenance',
                              argv=config['verifyCommand'] + [str(provenance), str(summary)],
                              timeoutSeconds=min(config['verification'].get('timeoutSeconds', 30),
                                                 remaining))
                verification = harness.run_command(verify, args.output / 'strict-provenance')
                case['provenanceVerification'] = verification
                assert verification['passed']
                record = json.loads(summary.read_text())
                assert record['schemaVersion'] == 2 and record['platform'] == fixture['platform']
                assert record['rawCatalogDigest'] == fixture['catalogDigest']
                assert record['policyDigest'] == fixture['policyDigest']
                assert record['archiveDigest'] == fixture['archiveDigest']
                assert any(tool['id'] == 'opaque-tool' and tool['version'] == '10.0.401' for tool in record['tools'])
                assert record['invocationCount'] > 0 and record['mirroredPaths'] and record['sddOwnedPaths']
                assert dict(record['effectiveParameters']) == dict(fixture['overrides'])
                creation = [inv for inv in record['invocations'] if '--output' in inv['arguments']]
                assert len(creation) == 1 and creation[0]['exitCode'] == 0
                prefix = ['new', 'fsgg-catalog-opaque-fixture', '--output', 'workspace',
                          '--no-update-check', '--debug:custom-hive', 'engine']
                parameters = [item for key, value in record['effectiveParameters'] for item in ['--' + key, value]]
                assert creation[0]['arguments'] == prefix + parameters
                assert creation[0]['executable'] == 'dotnet'
                assert '.agents/skills/opaque-fixture/SKILL.md' in record['producedPaths']
                assert '.config/dotnet-tools.json' in record['sddOwnedPaths']
                executable_paths = ['scripts/check-claim-generation.py', 'tools/routine-delivery.py']
                case['executableModes'] = {path: oct((target / path).stat().st_mode & 0o777) for path in executable_paths}
                assert all((target / path).stat().st_mode & 0o111 == 0o111 for path in executable_paths)
                case['provenance'] = record
            elif name == 'dryrun':
                assert actual['passed'] and projection['status'] == 'prepared' and projection['ownership'] == 'Settled'
                assert projection['observations'] is None and target_snapshot(target) == prior
            else:
                assert actual['exitCode'] == 1 and projection['status'] in ('failed', 'refused') and projection['diagnostics']
                assert actual['firstFailure'] == 'nonzero-exit', 'outer safeguard failed instead of product refusal'
                assert projection['ownership'] in ('Settled', 'not-started', 'NotStarted')
                codes = {diagnostic['code'] for diagnostic in projection['diagnostics']}
                expected = {'occupied-target': 'catalog.targetExists', 'catalog-digest': 'catalog.rawDigestMismatch',
                            'policy-digest': 'catalog.policyInvalid', 'archive-tamper': 'catalog.archiveDigestMismatch',
                            'duplicate-option': 'catalog.duplicateOption', 'unsupported-platform': 'catalog.admission.UnresolvedPlatform'}.get(name)
                if name in variants:
                    expected = variants[name]['expectedDiagnostic']
                if expected is not None:
                    assert expected in codes, name + ': expected refusal not observed'
                if name in probe_cases:
                    probe = probe_cases[name]
                    assert codes.intersection(probe['expectedDiagnosticAnyOf']), name + ': expected product diagnostic absent'
                    assert projection['ownership'] == probe['expectedOwnership']
                    assert not (target / '.fsgg/scaffold-provenance.json').exists()
                    if probe.get('expectedTargetState') == 'fixture-sentinel':
                        assert set(target_snapshot(target)) == set(probe['expectedTargetFiles'])
                        for path, content in probe['expectedTargetFiles'].items():
                            assert (target / path).read_text() == content
                    else:
                        assert target_snapshot(target) == prior, name + ': target bytes changed'
                else:
                    assert target_snapshot(target) == prior, name + ': target bytes changed'
            case['passed'] = True
        report['casesPassed'] = True
    except (AssertionError, OSError, ValueError, KeyError) as error:
        report['firstFailure'] = type(error).__name__ + ': ' + (str(error) or 'assertion failed; see case evidence')
        report['casesPassed'] = False
    finally:
        report['sourceTreeSha256After'] = harness.tree_identity(source, config.get('identityPaths'))
        report['sourceIdentityStable'] = before == report['sourceTreeSha256After']
        report['passed'] = report.get('casesPassed', False) and report['sourceIdentityStable']
        # Actual CLI product environment acceptance comes only from this integration,
        # never from a benign harness test or namespace capability observation.
        report['productEnvironmentQualified'] = report['passed'] and any(case['name'] == 'positive' and case['passed'] for case in report['cases'])
        (args.output / 'acceptance.json').write_text(json.dumps(report, indent=2) + '\n')
    for owner in harness.INCOMPLETE_OWNERS:
        owner.wait()
    return 0 if report['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
