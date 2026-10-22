#!/usr/bin/env python3
"""Offline controls for read-only qualification of an already-published set; every reply is synthetic."""
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

sys.dont_write_bytecode = True
repo = Path(__file__).parents[2]
spec = importlib.util.spec_from_file_location('qualifier', repo / 'scripts/qualify-published-set.py')
qualifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(qualifier)
head = 'c' * 40
version = '2.4.0'
ENVIRONMENT = dict(GH_TOKEN='synthetic-token', GITHUB_ACTOR='synthetic-actor', GITHUB_SHA='d' * 40)


def candidate():
    identity = ('schema=fsgg.sdd.release-candidate/v4\nhead=' + head + '\nversion=' + version
                + '\ncontracts_version=7.6.0\npackages=' + ','.join(qualifier.PACKAGES) + '\n')
    files = {name + '.' + version + '.nupkg': b'synthetic-package' for name in qualifier.PACKAGES}
    files.update({'candidate.env': identity.encode(), 'pre-push.sha256': b'fixture',
                  'contracts-reuse.json': b'{}', 'occupancy-observation.json': b'{}',
                  'dependencies/FS.GG.Contracts.7.6.0.nupkg': b'synthetic-dependency'})
    stream = io.BytesIO()
    with zipfile.ZipFile(stream, 'w') as archive:
        for name, data in files.items():
            archive.writestr(name, data)
    return stream.getvalue()


class Controls(unittest.TestCase):
    def setUp(self):
        self.raw = candidate()
        self.digest = hashlib.sha256(self.raw).hexdigest()
        self.run = dict(id=42, event='workflow_dispatch', status='completed', conclusion='success',
                        head_sha=head, head_branch='main', path='.github/workflows/release.yml',
                        repository={'full_name': qualifier._reader.REPOSITORY},
                        head_repository={'full_name': qualifier._reader.REPOSITORY})
        self.artifact = dict(id=17, name='coherent-sdd-packages-' + head, expired=False,
                             size_in_bytes=len(self.raw), digest='sha256:' + self.digest)
        self.fetched = []
        self.clock = 0.0
        self.sleeps = []
        self.missing_until = {}

    def read(self, suffix, cap):
        if suffix.endswith('/jobs?per_page=100'):
            names = ['Resolve published Contracts and four coherent SDD versions',
                     'Contracts tests (publish gate)', 'Artifacts tests (publish gate)',
                     'Commands tests and authored public surface (publish gate)',
                     'CLI tests (publish gate)', 'Knowledge tests (publish gate)',
                     'Build and retain four coherent SDD archives once; reuse published Contracts']
            return json.dumps(dict(total_count=7, jobs=[dict(name=name, run_id=42, head_sha=head,
                              status='completed', conclusion='success') for name in names])).encode()
        if suffix.endswith('/zip'):
            return self.raw
        if suffix.endswith('/artifacts?per_page=100'):
            return json.dumps({'total_count': 1, 'artifacts': [self.artifact]}).encode()
        return json.dumps(self.run).encode()

    def fetch(self, url, authorization):
        self.fetched.append((url, authorization))
        if self.missing_until.get(url, 0) > self.clock:
            raise qualifier.Absent('HTTP404')
        return b'published:' + url.encode()

    def sleep(self, seconds):
        self.sleeps.append(seconds)
        self.clock += seconds

    def invoke(self, **overrides):
        values = dict(run_id='42', archive_sha256=self.digest, source=head, version=version)
        values.update(overrides)
        with tempfile.TemporaryDirectory() as directory, patch.dict(os.environ, ENVIRONMENT):
            self.destination = Path(directory) / 'receipt'
            result = qualifier.qualify(**values, destination=self.destination, read=self.read,
                                       fetch=self.fetch, sleep=self.sleep, monotonic=lambda: self.clock)
            self.files = sorted(str(path.relative_to(self.destination)) for path in self.destination.rglob('*')
                                if path.is_file())
            return result

    def test_reads_all_eight_archives_beside_the_authenticated_original_candidate(self):
        result = self.invoke()
        self.assertFalse(result['publicationAuthorized'])
        self.assertEqual((result['candidateSource'], result['version']), (head, version))
        self.assertEqual(len(result['observations']), 8)
        self.assertEqual(len({(row['packageId'], row['feed']) for row in result['observations']}), 8)
        self.assertIn('packages/candidate.env', self.files)
        for package in qualifier.PACKAGES:
            for feed in ['github', 'nuget']:
                self.assertIn(f'feed-readback/{package.lower()}.{feed}.nupkg', self.files)
        github = [(url, auth) for url, auth in self.fetched if 'nuget.pkg.github.com' in url]
        public = [(url, auth) for url, auth in self.fetched if 'api.nuget.org' in url]
        self.assertEqual((len(github), len(public)), (4, 4))
        self.assertTrue(all(auth.startswith('Basic ') for _, auth in github))
        self.assertTrue(all(auth is None for _, auth in public))
        self.assertNotIn('synthetic-token', json.dumps(result))
        self.assertFalse(self.sleeps)

    def test_nuget_indexing_lag_is_waited_out_within_the_bound(self):
        lagging = qualifier.NUGET_DOWNLOAD.format(id='fs.gg.sdd.artifacts', version=version)
        self.missing_until[lagging] = 27 * 60
        result = self.invoke()
        row = [r for r in result['observations'] if r['packageId'] == 'FS.GG.SDD.Artifacts' and r['feed'] == 'nuget'][0]
        self.assertGreater(row['attempts'], 1)
        self.assertLessEqual(sum(self.sleeps), qualifier.MAX_WAIT_SECONDS)
        self.assertEqual(self.sleeps[0], 15)
        self.assertEqual(self.sleeps[-1], 30)

    def test_absence_beyond_the_bound_refuses_and_writes_no_success(self):
        self.missing_until[qualifier.NUGET_DOWNLOAD.format(id='fs.gg.sdd.cli', version=version)] = 10 ** 9
        with self.assertRaises(ValueError) as caught:
            self.invoke()
        self.assertIn('not served within', str(caught.exception))
        self.assertLessEqual(sum(self.sleeps), qualifier.MAX_WAIT_SECONDS)

    def test_bound_is_roughly_forty_five_minutes_with_backoff(self):
        self.assertEqual(qualifier.MAX_WAIT_SECONDS, 45 * 60)
        self.assertEqual([qualifier.interval(attempt) for attempt in [1, 19, 20, 99]], [15, 15, 30, 30])

    def test_original_custody_substitution_refuses_before_any_feed_read(self):
        for changes in [dict(source='e' * 40), dict(version='2.5.0'), dict(archive_sha256='f' * 64),
                        dict(run_id='0'), dict(source='short'), dict(version='v2.4.0')]:
            self.fetched.clear()
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                self.invoke(**changes)
            self.assertFalse(self.fetched)

    def test_failed_original_run_or_expired_candidate_refuses(self):
        for field, value in [('conclusion', 'failure'), ('head_branch', 'feature')]:
            self.run = dict(self.run, **{field: value})
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.invoke()
            self.run = dict(self.run, conclusion='success', head_branch='main')
        self.artifact['expired'] = True
        with self.assertRaises(ValueError):
            self.invoke()
        self.assertFalse(self.fetched)

    def test_requires_read_context_and_never_defaults_to_anonymous_org_reads(self):
        with patch.dict(os.environ, {}, clear=True), self.assertRaises(ValueError):
            qualifier.qualify('42', self.digest, head, version, Path('unused'), read=self.read,
                              fetch=self.fetch)
        self.assertFalse(self.fetched)

    def test_denied_reads_are_fatal_not_retried(self):
        def denied(url, authorization):
            raise ValueError('package read refused HTTP403')
        with tempfile.TemporaryDirectory() as directory, patch.dict(os.environ, ENVIRONMENT):
            with self.assertRaises(ValueError):
                qualifier.qualify('42', self.digest, head, version, Path(directory) / 'r', read=self.read,
                                  fetch=denied, sleep=self.sleep, monotonic=lambda: self.clock)
        self.assertFalse(self.sleeps)


unittest.main()
