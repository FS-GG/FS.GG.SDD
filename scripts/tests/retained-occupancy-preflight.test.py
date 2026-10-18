#!/usr/bin/env python3
"""Offline runner controls; candidate bytes and GitHub replies are synthetic."""
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

sys.dont_write_bytecode = True
repo = Path(__file__).parents[2]
spec = importlib.util.spec_from_file_location('runner', repo / 'scripts/preflight-retained-occupancy.py')
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)
head = 'c' * 40


def fixture():
    identity = ('schema=fsgg.sdd.release-candidate/v4\nhead=' + head
                + '\nversion=2.3.0\ncontracts_version=7.6.0\npackages='
                + ','.join(runner.PACKAGES) + '\n')
    files = {name + '.2.3.0.nupkg': b'synthetic-package' for name in runner.PACKAGES}
    files.update({'candidate.env': identity.encode(), 'pre-push.sha256': b'fixture',
                  'contracts-reuse.json': b'{}', 'occupancy-observation.json': b'{}',
                  'dependencies/FS.GG.Contracts.7.6.0.nupkg': b'synthetic-dependency'})
    stream = io.BytesIO()
    with zipfile.ZipFile(stream, 'w') as archive:
        for name, data in files.items():
            archive.writestr(name, data)
    raw = stream.getvalue()
    digest = hashlib.sha256(raw).hexdigest()
    run = dict(id=42, event='workflow_dispatch', status='completed', conclusion='success',
               head_sha=head, path='.github/workflows/release.yml',
               repository={'full_name': runner.REPOSITORY},
               head_repository={'full_name': runner.REPOSITORY})
    artifact = dict(id=17, name='coherent-sdd-packages-' + head, expired=False,
                    size_in_bytes=len(raw), digest='sha256:' + digest)
    return run, artifact, raw, digest


class Controls(unittest.TestCase):
    def setUp(self):
        self.run, self.artifact, self.raw, self.digest = fixture()
        self.reads = []
        self.calls = []

    def read(self, suffix, cap):
        self.reads.append(suffix)
        if suffix.endswith('/zip'):
            return self.raw
        if suffix.endswith('/artifacts?per_page=100'):
            return json.dumps({'total_count': 1, 'artifacts': [self.artifact]}).encode()
        return json.dumps(self.run).encode()

    def command(self, argv, **kwargs):
        self.calls.append(argv)
        output = ('FS.GG.Contracts 7.6.0: qualified published dependency; no push\n'
                  + ''.join(f'{package} 2.3.0 {feed}: absent\n' for package in runner.PACKAGES
                            for feed in ['github', 'nuget'])) if len(self.calls) == 2 else 'synthetic custody verified\n'
        return subprocess.CompletedProcess(argv, 0, output.encode())

    def invoke(self, **overrides):
        values = dict(run_id='42', archive_sha256=self.digest, publish_version='', readback_version='')
        values.update(overrides)
        with tempfile.TemporaryDirectory() as directory:
            return runner.preflight(**values, destination=Path(directory) / 'selected',
                                    read=self.read, command=self.command)

    def test_valid_runner_uses_original_source_and_existing_strict_checker(self):
        result = self.invoke()
        self.assertEqual(result['candidateSource'], head)
        self.assertEqual(result['outcome'], 'verified-read-only-occupancy')
        self.assertEqual(len(self.calls), 2)
        self.assertIn(head, self.calls[0])
        self.assertEqual(self.calls[1][1], str(repo / 'scripts/check-sdd-release-occupancy.py'))
        self.assertIn('--candidate', self.calls[1])
        self.assertNotIn('--no-push-observation', self.calls[1])
        self.assertEqual(len(result['observations']), 8)
        self.assertEqual(result['contractsDisposition']['disposition'], 'qualified-published-dependency-no-push')
        self.assertEqual([row['stage'] for row in result['helpers']], ['custody', 'occupancy'])
        self.assertEqual(result['helpers'][1]['exitCode'], 0)
        self.assertEqual(len(result['verifierFiles']), 3)

    def test_bad_or_mixed_inputs_never_read_or_launch(self):
        for args in [dict(run_id=''), dict(run_id='42;echo bad'), dict(archive_sha256=''),
                     dict(archive_sha256='x' * 64), dict(publish_version='2.3.0'),
                     dict(readback_version='2.3.0')]:
            with self.subTest(args=args), self.assertRaises(ValueError):
                self.invoke(**args)
        self.assertFalse(self.reads)
        self.assertFalse(self.calls)

    def test_untrusted_run_or_incomplete_artifact_population_blocks_commands(self):
        for key, value in [('conclusion', 'failure'), ('status', 'in_progress'),
                           ('event', 'push'), ('path', '.github/workflows/other.yml'),
                           ('head_repository', {'full_name': 'fork/other'}), ('id', 43)]:
            original = self.run[key]
            self.run[key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                self.invoke()
            self.run[key] = original
        self.assertFalse(self.calls)

    def test_expired_wrong_hash_or_wrong_name_never_launches(self):
        for key, value in [('expired', True), ('digest', 'sha256:' + '0' * 64),
                           ('name', 'coherent-sdd-packages-wrong'), ('size_in_bytes', 0)]:
            original = self.artifact[key]
            self.artifact[key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                self.invoke()
            self.artifact[key] = original
        self.assertFalse(self.calls)

    def test_download_substitution_never_launches(self):
        self.raw += b'substituted'
        with self.assertRaises(ValueError):
            self.invoke()
        self.assertFalse(self.calls)

    def test_failed_custody_never_calls_occupancy(self):
        def refused(argv, **kwargs):
            self.calls.append(argv)
            return subprocess.CompletedProcess(argv, 1, b'synthetic custody refusal')
        self.command = refused
        with self.assertRaisesRegex(ValueError, 'custody'):
            self.invoke()
        self.assertEqual(len(self.calls), 1)

    def test_denied_or_missing_feed_never_looks_verified(self):
        def refused(argv, **kwargs):
            self.calls.append(argv)
            return subprocess.CompletedProcess(argv, int(len(self.calls) == 2), b'release occupancy refused: feed access failed with HTTP 403; occupancy unproved')
        self.command = refused
        with self.assertRaisesRegex(runner.Refused, 'Unknown') as failure:
            self.invoke()
        result = failure.exception.result
        self.assertEqual(result['outcome'], 'Unknown')
        self.assertFalse(result['publicationAuthorized'])
        self.assertEqual(result['helpers'][-1]['stage'], 'occupancy')
        self.assertEqual(result['helpers'][-1]['exitCode'], 1)
        self.assertIn('feed access failed with HTTP 403; occupancy unproved', result['helpers'][-1]['diagnostic'])
        self.assertEqual(result['candidateSource'], head)
        self.assertEqual(result['archiveSha256'], self.digest)

    def test_timeout_never_looks_verified(self):
        self.command = lambda argv, **kwargs: (_ for _ in ()).throw(subprocess.TimeoutExpired(argv, 30))
        with self.assertRaisesRegex(runner.Refused, 'timed out') as failure:
            self.invoke()
        self.assertTrue(failure.exception.result['helpers'][0]['timedOut'])

    def test_scoped_namespace_cause_survives_original_error_artifact(self):
        def refused(argv, **kwargs):
            self.calls.append(argv)
            return subprocess.CompletedProcess(argv, int(len(self.calls) == 2),
                b'release occupancy refused: FS.GG.SDD.Commands: scoped GitHub versions unreadable; occupancy unknown\n')
        self.command = refused
        with self.assertRaises(runner.Refused) as failure:
            self.invoke()
        artifact = json.loads(json.dumps(failure.exception.result))
        self.assertIn('FS.GG.SDD.Commands: scoped GitHub versions unreadable; occupancy unknown',
                      artifact['helpers'][-1]['diagnostic'])
        self.assertFalse(artifact['publicationAuthorized'])
        self.assertEqual(len(self.calls), 2)

    def test_helper_diagnostic_redacts_credentials_and_refuses_oversize(self):
        with patch.dict(os.environ, {'GH_TOKEN': 'synthetic-secret-token'}):
            self.assertEqual(runner.sanitized('Authorization: Bearer synthetic-secret-token'),
                             'Authorization: Bearer [redacted]')
        self.command = lambda argv, **kwargs: subprocess.CompletedProcess(argv, 0, b'x' * 16385)
        with self.assertRaises(runner.Refused) as failure:
            self.invoke()
        self.assertEqual(len(failure.exception.result['helpers'][0]['diagnostic']), 16384)
        self.assertTrue(failure.exception.result['helpers'][0]['diagnosticTruncated'])
        record = runner.diagnostic_record(b'\xff' * 16384)
        self.assertLessEqual(record['diagnosticBytes'], 16384)
        self.assertTrue(record['diagnosticTruncated'])

    def test_workflow_isolates_all_other_jobs_and_uses_read_permissions(self):
        text = (repo / '.github/workflows/release.yml').read_text()
        chunks = text.split('\n  occupancy-preflight:\n')
        self.assertEqual(len(chunks), 2)
        for line in chunks[0].splitlines():
            if line.strip().startswith('if:') and 'inputs.readback_version' in line:
                self.assertIn("inputs.occupancy_candidate_run == ''", line)
                self.assertIn("inputs.occupancy_candidate_sha256 == ''", line)
        job = chunks[1]
        self.assertIn('actions: read', job)
        self.assertIn('packages: read', job)
        self.assertNotIn('write', job)
        self.assertNotIn('dotnet pack', job)
        self.assertNotIn('nuget push', job)
        self.assertIn('timeout-minutes: 6', job)

    def test_actual_early_workflow_guard_accepts_only_complete_exclusive_mode(self):
        text = (repo / '.github/workflows/release.yml').read_text()
        block = text.split("python3 - <<'PYGUARD'\n", 1)[1].split('          PYGUARD', 1)[0]
        code = '\n'.join(line[10:] for line in block.splitlines())
        good = dict(CANDIDATE_RUN='42', CANDIDATE_SHA256=self.digest,
                    PUBLISH_VERSION='', READBACK_VERSION='')
        for overrides, expected in [({}, 0), ({'CANDIDATE_RUN': ''}, 1),
                                    ({'CANDIDATE_SHA256': ''}, 1),
                                    ({'PUBLISH_VERSION': '2.3.0'}, 1),
                                    ({'READBACK_VERSION': '2.3.0'}, 1)]:
            result = subprocess.run([sys.executable, '-c', code],
                                    env=dict(os.environ, **(good | overrides)),
                                    stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                    timeout=3)
            self.assertEqual(result.returncode, expected)

    def test_incomplete_or_ambiguous_population_never_launches(self):
        original = self.read
        for listing in [{'total_count': 2, 'artifacts': [self.artifact]},
                        {'total_count': 2, 'artifacts': [self.artifact, self.artifact]}]:
            self.read = lambda suffix, cap: json.dumps(listing).encode() if '/artifacts?' in suffix else original(suffix, cap)
            with self.assertRaises(ValueError):
                self.invoke()
        self.assertFalse(self.calls)

    def test_zip_traversal_and_wrong_manifest_source_never_launch(self):
        original = self.raw
        for mutation in ['traversal', 'wrong-source']:
            source = zipfile.ZipFile(io.BytesIO(original))
            stream = io.BytesIO()
            with zipfile.ZipFile(stream, 'w') as target:
                for name in source.namelist():
                    data = source.read(name)
                    if mutation == 'traversal' and name == 'contracts-reuse.json':
                        name = '../outside.json'
                    if mutation == 'wrong-source' and name == 'candidate.env':
                        data = data.replace(head.encode(), b'd' * 40)
                    target.writestr(name, data)
            self.raw = stream.getvalue()
            self.digest = hashlib.sha256(self.raw).hexdigest()
            self.artifact.update(size_in_bytes=len(self.raw), digest='sha256:' + self.digest)
            with self.assertRaises(ValueError):
                self.invoke()
        self.assertFalse(self.calls)


unittest.main()
