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


def good_report():
    checks = [{'id': i, 'group': i.split('.')[0], 'status': 'pass', 'detail': '', 'evidence': {'observed': True}} for i in cert.required_check_ids()]
    return {'schema': cert.REPORT_SCHEMA,
            'harness': {'sourceRevision': 'b' * 40, 'sourceTreeSha256': 'c' * 64},
            'host': {'machine': 'x86_64', 'glibc': '2.44', 'runtimes': ['10.0.12']},
            'tools': {'sdd': {'version': '2.5.1', 'sha256': DIGEST}, 'governance': {'version': '1.15.0', 'sha256': DIGEST}},
            'artifact': {'provider': 'p', 'platform': 'linux-x64', 'catalogDigest': DIGEST, 'policyDigest': DIGEST, 'archiveDigest': DIGEST},
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
            report['checks'][0]['evidence'] = {'note': text}
            self.assertTrue(any('absolute path' in p for p in cert.validate_report(report)), text)
        report = good_report()
        report['checks'][0]['evidence'] = {'diagnostic': 'catalog.targetExists', 'path': 'out/governance-handoff.json'}
        self.assertEqual([], cert.validate_report(report))

    def test_non_objects_are_rejected_without_raising(self):
        for value in (None, [], 'x', {}, {'schema': cert.REPORT_SCHEMA}):
            self.assertTrue(cert.validate_report(value))


class ExampleTests(unittest.TestCase):
    def test_the_committed_example_is_a_valid_certified_report(self):
        example = json.loads((ROOT / 'tests/fixtures/provider-certification/report.example.json').read_text())
        self.assertEqual([], cert.validate_report(example))
        self.assertTrue(example['verdict']['certified'])
        self.assertEqual(sorted(cert.required_check_ids() + ['evidence.nonzero-exit']), sorted(c['id'] for c in example['checks']))

    def test_the_committed_example_fails_when_tampered(self):
        example = json.loads((ROOT / 'tests/fixtures/provider-certification/report.example.json').read_text())
        example['checks'][0]['status'] = 'fail'
        self.assertTrue(cert.validate_report(example))


class RequestTests(unittest.TestCase):
    def request(self, tmp):
        manifest = Path(tmp) / 'request.json'
        manifest.write_text('{}')
        cases = [{'name': n, 'expectedExit': 0 if n == 'positive' else 1} for n in cert.REQUIRED_EVIDENCE_CASES]
        tool = {'version': '1', 'sha256': DIGEST}
        return {'schema': cert.REQUEST_SCHEMA, 'sourceRoot': '.', 'identityPaths': ['scripts/*.py'], 'environment': {}, 'sdd': {}, 'authoring': {},
                'governance': {'evidenceCases': cases}, 'artifact': {'request': str(manifest)}, 'tools': {'sdd': tool, 'governance': dict(tool)}}

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


if __name__ == '__main__':
    unittest.main()
