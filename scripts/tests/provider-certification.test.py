"""Controls for the provider certification harness' report integrity and request validation.

These are pure: they build a synthetic complete report and prove that the admission-side validator
rejects every way of faking, truncating or leaking from it. The installed journey itself is run by
scripts/qualify-reference-provider.py.
"""
import copy
import importlib.util
import json
from pathlib import Path
import sys
import unittest

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('certification', ROOT / 'scripts/provider-certification.py')
cert = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cert)

DIGEST = 'sha256:' + 'a' * 64


def evidence_for(check_id):
    """Evidence that satisfies the admission-side semantic rules for each required check."""
    refusal = {'status': 'failed', 'ownership': 'Settled', 'targetUnchanged': True, 'stagingUnchanged': True}
    table = {
        'host.supported-profile': {'machine': 'x86_64', 'glibc': '2.44', 'runtime': '10.0.12'},
        'lifecycle.scaffold': {'status': 'succeeded', 'ownership': 'Settled', 'stagingRemaining': 0},
        'lifecycle.dry-run': {'status': 'prepared', 'targetCreated': False},
        'lifecycle.strict-readback': {'schemaVersion': 2, 'invocationCount': 5},
        'lifecycle.deterministic-instantiation': {'files': 200, 'treeSha256': 'd' * 64},
        'provenance.inputs-bound': {'catalogDigest': DIGEST, 'policyDigest': DIGEST, 'archiveDigest': DIGEST, 'platform': 'linux-x64'},
        'provenance.tools-and-invocations': {'tools': [{'id': 't', 'version': '1'}], 'invocationCount': 5},
        'provenance.declared-bindings': {'capabilityIds': ['test:test'], 'declaredCommands': 1, 'evidenceOutputs': 1, 'evidenceFormat': 'f@1', 'argumentCount': 8},
        'capability.build': {'outputPath': 'out/build/product.out', 'outputSha256': 'b' * 64, 'expectedSha256': 'b' * 64, 'citedSha256': 'b' * 64,
                             'sourcePath': 'product.txt', 'sourceSha256': 'a' * 64, 'transform': 'ascii-uppercase', 'nodeState': 'real'},
        'capability.package': {'artifactPath': 'out/package/product.pack', 'artifactSha256': 'c' * 64, 'citedSha256': 'c' * 64, 'format': 'tar',
                               'members': ['product.out'], 'memberSources': {'product.out': 'out/build/product.out'}, 'memberBytesMatchSources': True, 'nodeState': 'real'},
        'skill-union.recorded-and-present': {'providerSkills': 1, 'sddOwnedSkills': 21, 'seededUnrecorded': 6, 'onDisk': 28},
        'skill-union.mirrors-equal': {'skills': 28, 'recordedMirrors': 1},
        'evidence.positive': {'exit': 0, 'diagnostic': None, 'evidenceProduced': True},
        'evidence.missing': {'exit': 1, 'diagnostic': 'was not produced by the run', 'evidenceProduced': False},
        'evidence.stale': {'exit': 1, 'diagnostic': 'existed before launch', 'evidenceProduced': False},
        'evidence.mismatched': {'exit': 1, 'diagnostic': 'sources do not match the workspace', 'evidenceProduced': True},
        'evidence.whole-request-refusal': {'exit': 3, 'diagnostic': 'RawDigestMismatch', 'commandLaunched': False},
        'evidence.capture-lifetime': {'invocations': 6, 'settledCaptures': 5, 'workerSurvivors': 0, 'admittedWindowSeconds': 80},
    }
    if check_id.startswith('no-partial-state.'):
        return dict(refusal, diagnostic=cert.NO_PARTIAL_CASES[check_id.split('.', 1)[1]])
    return table[check_id]


def good_report(capabilities=('test:test',)):
    checks = [{'id': i, 'group': i.split('.')[0], 'status': 'pass', 'detail': '', 'evidence': evidence_for(i)}
              for i in cert.required_check_ids(capabilities=capabilities)]
    if len(capabilities) > 1:
        for check in checks:
            if check['id'] == 'provenance.declared-bindings':
                check['evidence'] = dict(check['evidence'], capabilityIds=sorted(capabilities), declaredCommands=len(capabilities), evidenceOutputs=len(capabilities))
    return {'schema': cert.REPORT_SCHEMA,
            'harness': {'sourceRevision': 'b' * 40, 'sourceTreeSha256': 'c' * 64},
            'host': {'machine': 'x86_64', 'glibc': '2.44', 'runtimes': ['10.0.11', '10.0.12']},
            'tools': {'sdd': {'version': '2.5.1', 'sha256': DIGEST}, 'governance': {'version': '1.15.0', 'sha256': DIGEST}},
            'artifact': {'provider': 'p', 'platform': 'linux-x64', 'capabilities': sorted(capabilities), 'catalogDigest': DIGEST, 'policyDigest': DIGEST, 'archiveDigest': DIGEST},
            'checks': checks, 'scope': cert.SCOPE,
            'verdict': {'certified': True, 'total': len(checks), 'passed': len(checks), 'failed': [], 'skipped': []}}


def recount(report):
    by = {s: sorted(c['id'] for c in report['checks'] if c['status'] == s) for s in cert.STATUSES}
    report['verdict'].update(total=len(report['checks']), passed=len(by['pass']), failed=by['fail'], skipped=by['skipped'])
    return report


class ReportIntegrityTests(unittest.TestCase):
    def test_a_complete_passing_report_is_valid(self):
        self.assertEqual([], cert.validate_report(good_report()))

    def test_required_inventory_covers_every_claimed_proof(self):
        ids = set(cert.required_check_ids())
        for group in ('lifecycle', 'provenance', 'no-partial-state', 'skill-union', 'evidence'):
            self.assertTrue(any(i.startswith(group + '.') for i in ids), group)
        for case in ('wrong-tool-version', 'unsupported-platform', 'malformed-catalog', 'unsupported-schema-version', 'archive-tamper', 'occupied-target'):
            self.assertIn('no-partial-state.' + case, ids)
        for case in cert.REQUIRED_EVIDENCE_CASES:
            self.assertIn('evidence.' + case, ids)
        self.assertEqual(len(ids), len(cert.required_check_ids()))

    def test_removing_any_required_check_is_detected(self):
        for victim in cert.required_check_ids():
            report = good_report()
            report['checks'] = [c for c in report['checks'] if c['id'] != victim]
            recount(report)
            self.assertTrue(any(victim in p for p in cert.validate_report(report)), victim)

    def test_a_certified_verdict_over_a_failed_or_skipped_check_is_detected(self):
        for status in ('fail', 'skipped'):
            report = good_report()
            report['checks'][3].update(status=status, detail='because')
            self.assertTrue(cert.validate_report(report), status)  # counts and certified no longer agree
            recount(report)
            self.assertTrue(cert.validate_report(report), status)  # still claims certified
            report['verdict']['certified'] = False
            self.assertEqual([], cert.validate_report(report), status)  # an honest uncertified report is valid

    def test_counts_must_match_the_checks(self):
        report = good_report()
        report['verdict']['passed'] -= 1
        self.assertTrue(cert.validate_report(report))

    def test_a_pass_needs_evidence_and_a_non_pass_needs_a_reason(self):
        report = good_report()
        report['checks'][0]['evidence'] = {}
        self.assertTrue(cert.validate_report(report))
        report = good_report()
        report['checks'][0].update(status='fail', detail='')
        recount(report)
        report['verdict']['certified'] = False
        self.assertTrue(cert.validate_report(report))

    def test_duplicate_unknown_and_malformed_members_are_detected(self):
        mutations = [
            lambda r: r['checks'].append(copy.deepcopy(r['checks'][0])),
            lambda r: r.update(extra=1),
            lambda r: r['checks'][0].update(extra=1),
            lambda r: r['verdict'].update(extra=1),
            lambda r: r.update(schema='fsgg.provider-certification/v2'),
            lambda r: r['artifact'].update(archiveDigest='sha256:zz'),
            lambda r: r['artifact'].pop('providers', None) or r['artifact'].pop('provider'),
            lambda r: r['tools'].pop('governance'),
            lambda r: r['tools']['sdd'].update(sha256='abc'),
            lambda r: r['harness'].update(sourceRevision='main'),
            lambda r: r.update(scope=''),
            lambda r: r['checks'][0].update(status='maybe'),
        ]
        for index, mutate in enumerate(mutations):
            report = good_report()
            mutate(report)
            self.assertTrue(cert.validate_report(report), index)

    def test_machine_specific_paths_cannot_enter_a_report(self):
        for text in ('/home/developer/x', 'see /tmp/run/out for details', 'at /var/lib/y'):
            report = good_report()
            report['checks'][0]['evidence'] = dict(report['checks'][0]['evidence'], note=text)
            self.assertTrue(any('absolute path' in p for p in cert.validate_report(report)), text)
        report = good_report()
        report['checks'][0]['evidence'] = dict(report['checks'][0]['evidence'], path='out/governance-handoff.json')
        self.assertEqual([], cert.validate_report(report))

    def test_non_objects_are_rejected_without_raising(self):
        for value in (None, [], 'x', {}, {'schema': cert.REPORT_SCHEMA}):
            self.assertTrue(cert.validate_report(value))


BTP = ('build:build', 'package:package', 'test:test')


class CapabilityEvidenceTests(unittest.TestCase):
    """Declared build and package capabilities are certified by their actual outputs, not their labels."""

    def mutated(self, check_id, **changes):
        report = good_report(BTP)
        check = next(c for c in report['checks'] if c['id'] == check_id)
        check['evidence'] = dict(check['evidence'], **changes)
        return cert.validate_report(report)

    def test_a_build_test_package_report_is_valid_and_requires_both_capability_checks(self):
        self.assertEqual([], cert.validate_report(good_report(BTP)))
        self.assertEqual(['capability.build', 'capability.package'], [i for i in cert.required_check_ids(capabilities=BTP) if i.startswith('capability.')])
        for victim in ('capability.build', 'capability.package'):
            report = good_report(BTP)
            report['checks'] = [c for c in report['checks'] if c['id'] != victim]
            recount(report)
            self.assertTrue(any(victim in p for p in cert.validate_report(report)), victim)

    def test_a_single_capability_report_needs_no_capability_checks_and_cannot_claim_them_away(self):
        self.assertEqual([], cert.validate_report(good_report()))
        report = good_report(BTP)
        report['artifact']['capabilities'] = ['test:test']  # hiding declared capabilities contradicts the bindings evidence
        self.assertTrue(cert.validate_report(report))
        report = good_report()
        report['artifact']['capabilities'] = sorted(BTP)  # claiming capabilities without their checks
        self.assertTrue(cert.validate_report(report))

    def test_build_evidence_must_show_the_exact_expected_output_cited(self):
        self.assertTrue(self.mutated('capability.build', outputSha256='d' * 64))
        self.assertTrue(self.mutated('capability.build', citedSha256='d' * 64))
        self.assertTrue(self.mutated('capability.build', expectedSha256='d' * 64))
        self.assertTrue(self.mutated('capability.build', outputSha256='short', expectedSha256='short', citedSha256='short'))
        self.assertTrue(self.mutated('capability.build', nodeState='stale'))
        self.assertTrue(self.mutated('capability.build', sourceSha256='nope'))
        self.assertTrue(self.mutated('capability.build', sourcePath=''))

    def test_package_evidence_must_show_the_cited_identity_and_matching_members(self):
        self.assertTrue(self.mutated('capability.package', citedSha256='d' * 64))
        self.assertTrue(self.mutated('capability.package', memberBytesMatchSources=False))
        self.assertTrue(self.mutated('capability.package', members=[]))
        self.assertTrue(self.mutated('capability.package', members=[1]))
        self.assertTrue(self.mutated('capability.package', format='zip'))
        self.assertTrue(self.mutated('capability.package', nodeState='stale'))
        self.assertTrue(self.mutated('capability.package', memberSources={}))
        self.assertTrue(self.mutated('capability.package', memberSources={'other.out': 'x'}))

    def test_bindings_must_cover_exactly_the_declared_capabilities(self):
        self.assertTrue(self.mutated('provenance.declared-bindings', capabilityIds=['test:test']))
        self.assertTrue(self.mutated('provenance.declared-bindings', declaredCommands=1))
        self.assertTrue(self.mutated('provenance.declared-bindings', evidenceOutputs=2))

    def test_transforms_are_closed_and_exact(self):
        self.assertEqual(b'RAW=A B!?\n', cert.TRANSFORMS['ascii-uppercase'](b'raw=a b!?\n'))
        self.assertEqual(b'abc', cert.TRANSFORMS['identity'](b'abc'))
        self.assertEqual({'identity', 'ascii-uppercase'}, set(cert.TRANSFORMS))


def make_tar(members, mode='w:', **options):
    import io, tarfile
    buffer = io.BytesIO()
    with tarfile.open(fileobj=buffer, mode=mode) as archive:
        for name, data in members:
            info = tarfile.TarInfo(name)
            info.size = len(data)
            for key, value in options.items():
                setattr(info, key, value)
            archive.addfile(info, io.BytesIO(data))
    return buffer.getvalue()


class TarVerificationTests(unittest.TestCase):
    EXPECTED = {'product.out': b'RAW=A\n'}

    def refused(self, pack, expected=None, fragment=None):
        with self.assertRaises(AssertionError) as caught:
            cert.verify_tar(pack, expected if expected is not None else self.EXPECTED)
        if fragment:
            self.assertIn(fragment, str(caught.exception))

    def test_the_exact_members_with_the_exact_bytes_are_accepted(self):
        self.assertEqual(['product.out'], cert.verify_tar(make_tar([('product.out', b'RAW=A\n')]), self.EXPECTED))
        self.assertEqual(['a', 'b'], cert.verify_tar(make_tar([('b', b'2'), ('a', b'1')]), {'a': b'1', 'b': b'2'}))

    def test_wrong_bytes_missing_and_extra_members_are_refused(self):
        self.refused(make_tar([('product.out', b'tampered package content\n')]), fragment='differs')
        self.refused(make_tar([]), fragment='exactly the declared members')
        self.refused(make_tar([('product.out', b'RAW=A\n'), ('extra', b'x')]), fragment='exactly the declared members')
        self.refused(make_tar([('other.out', b'RAW=A\n')]), fragment='exactly the declared members')

    def test_duplicate_unsafe_and_non_regular_members_are_refused(self):
        self.refused(make_tar([('product.out', b'RAW=A\n'), ('product.out', b'RAW=A\n')]), fragment='repeats')
        for name in ('/abs/product.out', '../product.out', 'a/../product.out', './product.out', 'a//b'):
            self.refused(make_tar([(name, b'x')]), {name: b'x'}, 'not contained')
        import io, tarfile
        for kind in (tarfile.SYMTYPE, tarfile.LNKTYPE, tarfile.DIRTYPE, tarfile.FIFOTYPE):
            buffer = io.BytesIO()
            with tarfile.open(fileobj=buffer, mode='w:') as archive:
                info = tarfile.TarInfo('product.out')
                info.type = kind
                info.linkname = 'elsewhere'
                archive.addfile(info)
            self.refused(buffer.getvalue(), fragment='not a regular file')

    def test_compressed_truncated_and_garbage_input_is_a_refusal_not_a_crash(self):
        plain = make_tar([('product.out', b'RAW=A\n')])
        self.refused(make_tar([('product.out', b'RAW=A\n')], mode='w:gz'), fragment='readable uncompressed tar')
        self.refused(make_tar([('product.out', b'RAW=A\n')], mode='w:xz'), fragment='readable uncompressed tar')
        self.refused(b'', fragment='readable uncompressed tar')
        self.refused(b'not a tar archive at all' * 100, fragment='readable uncompressed tar')
        self.refused(plain[:700], fragment='readable uncompressed tar')
        gz = make_tar([('product.out', b'RAW=A\n' * 5000)], mode='w:gz')
        self.refused(gz[: len(gz) // 2], fragment='readable uncompressed tar')

    def test_oversized_members_are_refused(self):
        import unittest.mock
        with unittest.mock.patch.object(cert, 'MAXIMUM_MEMBER_BYTES', 4):
            self.refused(make_tar([('product.out', b'12345')]), {'product.out': b'12345'}, 'exceeds the size bound')
            self.assertEqual(['product.out'], cert.verify_tar(make_tar([('product.out', b'1234')]), {'product.out': b'1234'}))

    def test_read_regular_refuses_links_and_non_files(self):
        import tempfile, os
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / 'real').mkdir()
            (root / 'real/file').write_bytes(b'ok')
            os.symlink(root / 'real/file', root / 'link')
            os.symlink(root / 'real', root / 'dirlink')
            self.assertEqual(b'ok', cert.read_regular(root, 'real/file'))
            for relative in ('link', 'dirlink/file', 'real', 'absent'):
                with self.assertRaises(AssertionError):
                    cert.read_regular(root, relative)


class SemanticEvidenceTests(unittest.TestCase):
    """The admitter cannot recompute a run, so the report must at least say the right things per check."""

    def mutated(self, check_id, **changes):
        report = good_report()
        check = next(c for c in report['checks'] if c['id'] == check_id)
        check['evidence'] = dict(check['evidence'], **changes)
        return cert.validate_report(report)

    def test_arbitrary_evidence_no_longer_passes(self):
        report = good_report()
        for check in report['checks']:
            check['evidence'] = {'x': 0}
        problems = cert.validate_report(report)
        for check_id in cert.required_check_ids():
            self.assertTrue(any(p.startswith(check_id + ':') for p in problems), check_id)

    def test_each_refusal_must_carry_its_intended_diagnostic_and_unchanged_state(self):
        for name, code in cert.NO_PARTIAL_CASES.items():
            cid = 'no-partial-state.' + name
            self.assertTrue(self.mutated(cid, diagnostic='catalog.somethingElse'), cid)
            self.assertTrue(self.mutated(cid, targetUnchanged=False), cid)
            self.assertTrue(self.mutated(cid, stagingUnchanged=False), cid)
            self.assertTrue(self.mutated(cid, status='succeeded'), cid)
            self.assertTrue(self.mutated(cid, ownership='Unknown'), cid)

    def test_provenance_digests_must_equal_the_reported_artifact(self):
        self.assertTrue(self.mutated('provenance.inputs-bound', archiveDigest='sha256:' + 'e' * 64))
        self.assertTrue(self.mutated('provenance.inputs-bound', platform='linux-arm64'))

    def test_evidence_cases_have_their_required_shape(self):
        self.assertTrue(self.mutated('evidence.positive', exit=1))
        self.assertTrue(self.mutated('evidence.positive', evidenceProduced=False))
        for name in cert.NEGATIVE_EVIDENCE_CASES:
            self.assertTrue(self.mutated('evidence.' + name, exit=0), name)
            self.assertTrue(self.mutated('evidence.' + name, diagnostic=''), name)
        self.assertTrue(self.mutated('evidence.stale', evidenceProduced=True))
        self.assertTrue(self.mutated('evidence.whole-request-refusal', commandLaunched=True))
        self.assertTrue(self.mutated('evidence.whole-request-refusal', exit=1))
        self.assertTrue(self.mutated('evidence.capture-lifetime', workerSurvivors=1))
        self.assertTrue(self.mutated('evidence.capture-lifetime', settledCaptures=0))

    def test_the_required_negative_cases_are_bound_to_their_own_behaviour(self):
        self.assertTrue(self.mutated('evidence.missing', evidenceProduced=True))
        self.assertTrue(self.mutated('evidence.missing', diagnostic='x'))
        self.assertTrue(self.mutated('evidence.mismatched', evidenceProduced=False))
        self.assertTrue(self.mutated('evidence.mismatched', diagnostic='was not produced by the run'))
        self.assertTrue(self.mutated('evidence.stale', diagnostic='sources do not match the workspace'))

    def test_wrongly_typed_evidence_is_a_problem_not_a_traceback(self):
        for check_id, key, value in [('provenance.declared-bindings', 'argumentCount', '8'), ('skill-union.recorded-and-present', 'providerSkills', '1'),
                                     ('skill-union.mirrors-equal', 'skills', None), ('lifecycle.deterministic-instantiation', 'files', True),
                                     ('evidence.positive', 'exit', False), ('evidence.capture-lifetime', 'workerSurvivors', False),
                                     ('lifecycle.scaffold', 'stagingRemaining', False), ('evidence.missing', 'exit', '1')]:
            self.assertTrue(self.mutated(check_id, **{key: value}), (check_id, key))

    def test_the_host_must_be_the_supported_profile_and_agree_with_the_report(self):
        self.assertTrue(self.mutated('host.supported-profile', glibc='2.39'))
        self.assertTrue(self.mutated('host.supported-profile', machine='aarch64'))
        report = good_report()
        report['host']['runtimes'] = ['10.0.11']
        self.assertTrue(cert.validate_report(report))

    def test_non_passing_checks_are_not_semantically_constrained(self):
        report = good_report()
        check = next(c for c in report['checks'] if c['id'] == 'no-partial-state.archive-tamper')
        check.update(status='fail', detail='crashed', evidence={})
        recount(report)
        report['verdict']['certified'] = False
        self.assertEqual([], cert.validate_report(report))


class ExampleTests(unittest.TestCase):
    EXAMPLES = {'report.example.json': ('test:test',), 'report.build-test-package.example.json': BTP}

    def load(self, name):
        return json.loads((ROOT / 'tests/fixtures/provider-certification' / name).read_text())

    def test_the_committed_examples_are_valid_certified_reports(self):
        for name, capabilities in self.EXAMPLES.items():
            example = self.load(name)
            self.assertEqual([], cert.validate_report(example), name)
            self.assertTrue(example['verdict']['certified'], name)
            self.assertEqual(sorted(capabilities), example['artifact']['capabilities'], name)
            self.assertEqual(sorted(cert.required_check_ids(capabilities=capabilities) + ['evidence.nonzero-exit']), sorted(c['id'] for c in example['checks']), name)

    def test_the_build_test_package_example_carries_real_output_evidence(self):
        checks = {c['id']: c for c in self.load('report.build-test-package.example.json')['checks']}
        build, package = checks['capability.build']['evidence'], checks['capability.package']['evidence']
        self.assertEqual(build['outputSha256'], build['citedSha256'])
        self.assertEqual(package['artifactSha256'], package['citedSha256'])
        self.assertEqual(['product.out'], package['members'])

    def test_the_committed_examples_fail_when_tampered(self):
        for name in self.EXAMPLES:
            example = self.load(name)
            example['checks'][0]['status'] = 'fail'
            self.assertTrue(cert.validate_report(example), name)


class RequestTests(unittest.TestCase):
    def request(self, tmp):
        manifest = Path(tmp) / 'request.json'
        manifest.write_text(json.dumps({'requiredCapabilityId': 'test:test', 'fixtureExecutable': '/x', 'fixtureArguments': [], 'requiredAbsentOutput': 'out/x', 'requiredEvidenceFormat': 'f@1'}))
        cases = [dict({'name': n, 'expectedExit': 0 if n == 'positive' else 1}, **({} if n == 'positive' else {'expectedDiagnostic': cert.REQUIRED_DIAGNOSTICS[n]}), **({'staleEvidence': True} if n == 'stale' else {})) for n in cert.REQUIRED_EVIDENCE_CASES]
        tool = {'version': '1', 'sha256': DIGEST}
        return {'schema': cert.REQUEST_SCHEMA, 'sourceRoot': '.', 'identityPaths': ['scripts/*.py'], 'environment': {}, 'sdd': {}, 'authoring': {},
                'governance': {'evidenceCases': cases}, 'artifact': {'request': str(manifest)}, 'tools': {'sdd': tool, 'governance': dict(tool)}}

    def test_capability_expectations_are_required_for_declared_build_and_package(self):
        import tempfile
        with tempfile.TemporaryDirectory() as tmp:
            request = self.request(tmp)
            manifest = json.loads(Path(request['artifact']['request']).read_text())
            manifest['capabilities'] = [{'id': i, 'executable': '/x', 'arguments': [], 'workingDirectory': '.', 'evidenceId': i, 'evidencePath': 'out/' + i, 'evidenceFormat': 'f@1'}
                                        for i in BTP]
            Path(request['artifact']['request']).write_text(json.dumps(manifest))
            with self.assertRaises(ValueError): cert.validate_request(request)
            request['governance']['capabilityExpectations'] = {
                'build': {'from': 'product.txt', 'output': 'out/build/product.out', 'transform': 'ascii-uppercase'},
                'package': {'artifact': 'out/package/product.pack', 'format': 'tar', 'members': {'product.out': 'out/build/product.out'}}}
            cert.validate_request(request)
            for mutate in (lambda e: e['build'].update(transform='rot13'), lambda e: e['package'].update(format='zip'), lambda e: e['package'].update(members={}),
                           lambda e: e['build'].update(output='/abs/out'), lambda e: e['package']['members'].update({'x': '../up'}), lambda e: e.update(deploy={})):
                bad = json.loads(json.dumps(request)); mutate(bad['governance']['capabilityExpectations'])
                with self.assertRaises(ValueError): cert.validate_request(bad)

    def test_valid_request_and_refusals(self):
        import tempfile
        with tempfile.TemporaryDirectory() as tmp:
            request = self.request(tmp)
            cert.validate_request(request)
            bad = copy.deepcopy(request); bad['governance']['evidenceCases'].pop()
            with self.assertRaises(ValueError): cert.validate_request(bad)
            bad = copy.deepcopy(request); bad['governance']['evidenceCases'][0]['expectedExit'] = 1
            with self.assertRaises(ValueError): cert.validate_request(bad)
            bad = copy.deepcopy(request); bad['tools']['sdd']['sha256'] = 'abc'
            with self.assertRaises(ValueError): cert.validate_request(bad)
            bad = copy.deepcopy(request); bad['unexpected'] = 1
            with self.assertRaises(ValueError): cert.validate_request(bad)
            bad = copy.deepcopy(request); bad['governance']['evidenceCases'].append({'name': 'positive', 'expectedExit': 0})
            with self.assertRaises(ValueError): cert.validate_request(bad)
            # The required negative cases are behaviours, not labels.
            for name in cert.NEGATIVE_EVIDENCE_CASES:
                bad = copy.deepcopy(request); [c for c in bad['governance']['evidenceCases'] if c['name'] == name][0]['expectedExit'] = 0
                with self.assertRaises(ValueError): cert.validate_request(bad)
                bad = copy.deepcopy(request); [c for c in bad['governance']['evidenceCases'] if c['name'] == name][0]['expectedDiagnostic'] = 'some other diagnostic'
                with self.assertRaises(ValueError): cert.validate_request(bad)
                bad = copy.deepcopy(request); [c for c in bad['governance']['evidenceCases'] if c['name'] == name][0].pop('expectedDiagnostic')
                with self.assertRaises(ValueError): cert.validate_request(bad)
            bad = copy.deepcopy(request); [c for c in bad['governance']['evidenceCases'] if c['name'] == 'stale'][0].pop('staleEvidence')
            with self.assertRaises(ValueError): cert.validate_request(bad)
            bad = copy.deepcopy(request); [c for c in bad['governance']['evidenceCases'] if c['name'] == 'positive'][0]['staleEvidence'] = True
            with self.assertRaises(ValueError): cert.validate_request(bad)


if __name__ == '__main__':
    unittest.main()
