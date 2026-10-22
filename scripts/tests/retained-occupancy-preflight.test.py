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

    def test_retained_selection_cannot_substitute_source_or_version(self):
        self.assertEqual(self.invoke(expected_source=head, expected_version='2.3.0')['candidateSource'], head)
        for changes in [dict(expected_source='d' * 40, expected_version='2.3.0'),
                        dict(expected_source=head, expected_version='2.4.0'),
                        dict(expected_source=head), dict(expected_version='2.3.0')]:
            self.calls.clear()
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                self.invoke(**changes)
            self.assertFalse(self.calls)

    def test_original_qualification_jobs_must_be_complete_and_successful(self):
        original = self.read
        for mutation in ['incomplete', 'failed', 'foreign-head', 'duplicate']:
            def read(suffix, cap):
                raw = original(suffix, cap)
                if '/jobs?' not in suffix:
                    return raw
                data = json.loads(raw)
                if mutation == 'incomplete':
                    data['total_count'] += 1
                elif mutation == 'failed':
                    data['jobs'][0]['conclusion'] = 'failure'
                elif mutation == 'foreign-head':
                    data['jobs'][0]['head_sha'] = 'd' * 40
                else:
                    data['jobs'].append(data['jobs'][0]); data['total_count'] += 1
                return json.dumps(data).encode()
            self.read = read
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                self.invoke(expected_source=head, expected_version='2.3.0')
            self.assertFalse(self.calls)
        self.read = original

    def test_package_credentials_only_reach_occupancy_and_basic_is_redacted(self):
        original = self.command
        def inspect(argv, **kwargs):
            if not self.calls:
                self.assertNotIn('FSGG_PACKAGES_READ_TOKEN', kwargs['env'])
                self.assertNotIn('GH_TOKEN', kwargs['env'])
            else:
                self.assertEqual(kwargs['env']['FSGG_PACKAGES_READ_TOKEN'], 'private-token')
                self.assertEqual(kwargs['env']['GH_TOKEN'], 'actions-token')
            return original(argv, **kwargs)
        self.command = inspect
        with patch.dict(os.environ, {'FSGG_PACKAGES_READ_TOKEN': 'private-token',
                                    'FSGG_PACKAGES_READ_ACTOR': 'owner', 'GH_TOKEN': 'actions-token'}):
            self.invoke()
            import base64
            encoded = base64.b64encode(b'owner:private-token').decode()
            self.assertEqual(runner.sanitized(encoded + ' private-token'), '[redacted] [redacted]')

    def test_retained_qualifiers_use_selected_version_without_source_evaluation(self):
        cli = (repo / 'scripts/verify-cli-tool.sh').read_text()
        cli_code = cli.split('version="${FSGG_SDD_PACKAGE_VERSION:-}"', 1)[1].split('echo "Smoke:', 1)[0]
        cli_code = 'version="${FSGG_SDD_PACKAGE_VERSION:-}"' + cli_code + '\nprintf "%s" "$version"'
        receiver = (repo / 'tests/quint-svg-workspace-installed-acceptance.sh').read_text()
        receiver_code = receiver.split('version="${SVG_WORKSPACE_PACKAGE_VERSION:-}"', 1)[1].split('if [[ "${SVG_WORKSPACE_OFFLINE_STAGE', 1)[0]
        receiver_code = 'version="${SVG_WORKSPACE_PACKAGE_VERSION:-}"' + receiver_code + '\nprintf "%s" "$version"'
        self.assertIn('SVG_WORKSPACE_PACKAGE_VERSION="$version"', receiver)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'Directory.Build.local.props').write_text('<Version>9.9.0</Version>')
            # No dotnet exists in this fixture; explicit retained version must not
            # invoke msbuild or pack. Source props deliberately differ.
            for code, good in [(cli_code, dict(FSGG_SDD_PACKAGE_VERSION='2.3.0', FSGG_SDD_PACKAGE_DIR='selected')),
                               (receiver_code, dict(SVG_WORKSPACE_PACKAGE_VERSION='2.3.0', Q3_PACKAGE_SOURCE='selected'))]:
                prefix = 'set -euo pipefail; fail() { exit 1; }; repo_root="' + directory + '"; '
                result = subprocess.run(['bash', '-c', prefix + code],
                                        env=dict(os.environ, **good), capture_output=True, timeout=3)
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertEqual(result.stdout, b'2.3.0')
                for key in good:
                    bad = good | {key: ''}
                    if key.endswith('VERSION'):
                        bad[key] = 'malformed'
                    result = subprocess.run(['bash', '-c', prefix + code],
                                            env=dict(os.environ, **bad), capture_output=True, timeout=3)
                    self.assertNotEqual(result.returncode, 0)

    def test_actual_promotion_guard_rejects_partial_and_unprotected_selection(self):
        text = (repo / '.github/workflows/release.yml').read_text()
        block = text.split("python3 - <<'PYPROMOTION'\n", 1)[1].split('          PYPROMOTION', 1)[0]
        code = '\n'.join(line[10:] for line in block.splitlines())
        good = dict(PROMOTION_RUN='42', PROMOTION_SHA256=self.digest, PROMOTION_SOURCE=head,
                    PUBLISH_VERSION='2.3.0', EVENT_NAME='workflow_dispatch', REF_NAME='refs/heads/main')
        for overrides, expected in [({}, 0), ({'PROMOTION_RUN': ''}, 1),
                                    ({'PROMOTION_SHA256': ''}, 1), ({'PROMOTION_SOURCE': ''}, 1),
                                    ({'PUBLISH_VERSION': ''}, 1), ({'EVENT_NAME': 'push'}, 1),
                                    ({'REF_NAME': 'refs/heads/other'}, 1), ({'READBACK_VERSION': '2.3.0'}, 1)]:
            result = subprocess.run([sys.executable, '-c', code], env=dict(os.environ, **(good | overrides)),
                                    stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=3)
            self.assertEqual(result.returncode, expected)

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


authority_spec = importlib.util.spec_from_file_location('authority', repo / 'scripts/check-sdd-release-occupancy.py')
authority = importlib.util.module_from_spec(authority_spec)
authority_spec.loader.exec_module(authority)


class NamespaceControls(unittest.TestCase):
    """Synthetic native REST replies exercise the real authority decision, without network."""
    def setUp(self):
        self.headers = {'X-OAuth-Scopes': 'read:packages, read:org'}
        self.rows = {'/user': {'id': 17, 'login': 'owner'},
                     '/orgs/FS-GG': {'id': 23, 'login': 'FS-GG'},
                     '/orgs/FS-GG/memberships/owner': {
                         'state': 'active', 'role': 'admin',
                         'user': {'id': 17, 'login': 'owner'},
                         'organization': {'id': 23, 'login': 'FS-GG'}},
                     self.page(1): []}
        self.extra_headers = {}

    @staticmethod
    def page(number):
        return '/orgs/FS-GG/packages?package_type=nuget&per_page=100&page=' + str(number)

    @staticmethod
    def package(number, name=None):
        return dict(id=number, name=name or ('package' + str(number)), package_type='nuget',
                    visibility='private', owner={'id': 23, 'login': 'FS-GG'})

    def read(self, path):
        value = self.rows[path]
        if isinstance(value, Exception):
            raise value
        return json.dumps(value).encode(), self.headers | self.extra_headers.get(path, {})

    def invoke(self):
        return authority.authoritative_namespace_absence('FS.GG.SDD.Commands', 'owner', self.read)

    def test_complete_owner_population_proves_absence_without_exposing_names(self):
        self.rows[self.page(1)] = [self.package(1)]
        proof = self.invoke()
        self.assertEqual(proof['actorId'], 17)
        self.assertEqual(proof['organizationId'], 23)
        self.assertEqual(proof['packageCount'], 1)
        self.assertFalse(proof['publicationAuthorized'])
        self.assertNotIn('package1', json.dumps(proof))
        self.assertEqual(len(proof['populationSha256']), 64)

    def test_multiple_pages_and_case_variant_target_on_last_page(self):
        self.rows[self.page(1)] = [self.package(i) for i in range(1, 101)]
        self.rows[self.page(2)] = [self.package(101)]
        self.assertEqual(self.invoke()['pages'], 2)
        self.rows[self.page(2)] = [self.package(101, 'fs.gg.sdd.COMMANDS')]
        with self.assertRaisesRegex(ValueError, 'listed'):
            self.invoke()

    def test_native_owner_numeric_join_required(self):
        path = '/orgs/FS-GG/memberships/owner'
        original = self.rows[path]
        for changes in [{'role': 'member'}, {'role': None}, {'state': 'pending'},
                        {'user': {'id': 18, 'login': 'owner'}},
                        {'user': {'id': 17.0, 'login': 'owner'}},
                        {'organization': {'id': 23.0, 'login': 'FS-GG'}},
                        {'user': {'id': 17, 'login': 'spoof'}},
                        {'organization': {'id': 24, 'login': 'FS-GG'}},
                        {'organization': None}]:
            self.rows[path] = original | changes
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                self.invoke()
        self.rows[path] = original
        for path, bad in [('/user', {'id': 18, 'login': 'other'}),
                          ('/user', {'id': True, 'login': 'owner'}),
                          ('/orgs/FS-GG', {'id': 23, 'login': 'other'}),
                          ('/orgs/FS-GG', [])]:
            original = self.rows[path]
            self.rows[path] = bad
            with self.assertRaises(ValueError):
                self.invoke()
            self.rows[path] = original

    def test_public_readability_scopes_or_separate_owner_are_insufficient(self):
        for headers in [{}, {'X-OAuth-Scopes': 'read:packages'},
                        {'X-OAuth-Scopes': 'read:org'},
                        self.headers | {'X-GitHub-SSO': 'required; url=SECRET'},
                        self.headers | {'X-GitHub-SSO': 'partial-results; organizations=23'}]:
            old = self.headers
            self.headers = headers
            with self.subTest(headers=headers), self.assertRaises(ValueError):
                self.invoke()
            self.headers = old

    def test_every_authority_endpoint_denied_or_malformed_refuses(self):
        for path in list(self.rows):
            original = self.rows[path]
            for status in [401, 403, 404]:
                self.rows[path] = ValueError('authority read HTTP' + str(status))
                with self.subTest(path=path, status=status), self.assertRaises(ValueError):
                    self.invoke()
            self.rows[path] = original
        self.rows[self.page(1)] = {'not': 'a population'}
        with self.assertRaises(ValueError):
            self.invoke()

    def test_entry_duplicates_stalls_caps_and_malformed_fields_refuse(self):
        good = self.package(1)
        for entries in [[good, good], [good, self.package(2, good['name'].upper())],
                        [good | {'owner': {'id': 24, 'login': 'FS-GG'}}],
                        [good | {'owner': {'id': 23.0, 'login': 'FS-GG'}}],
                        [good | {'package_type': 'npm'}], [good | {'id': True}],
                        [good | {'visibility': None}], [good | {'name': ''}], [None],
                        [good] * 101]:
            self.rows[self.page(1)] = entries
            with self.subTest(entries=entries[:2]), self.assertRaises(ValueError):
                self.invoke()
        for page in range(1, 11):
            self.rows[self.page(page)] = [self.package((page - 1) * 100 + i) for i in range(1, 101)]
        with self.assertRaisesRegex(ValueError, 'page bound'):
            self.invoke()

    def test_noncanonical_links_and_incomplete_pagination_refuse(self):
        for link in ['<https://evil.invalid/>; rel="next"',
                     '<https://api.github.com' + self.page(2) + '>; rel="next"',
                     'malformed', '<https://api.github.com' + self.page(1) + '>; rel="next"',
                     '<https://api.github.com' + self.page(2) + '>; rel="last"',
                     '<https://api.github.com' + self.page(2) + '>; rel="first"',
                     '<https://api.github.com' + self.page(1) + '>; rel="prev"',
                     ', '.join(['<https://api.github.com' + self.page(1) + '>; rel="last"'] * 2)]:
            self.extra_headers[self.page(1)] = {'Link': link}
            with self.subTest(link=link), self.assertRaises(ValueError):
                self.invoke()

    def test_owner_requirement_has_causal_negative_control(self):
        # Removing the owner check admits an ordinary member: the negative case
        # must therefore detect the mutant rather than pass vacuously.
        text = (repo / 'scripts/check-sdd-release-occupancy.py').read_text()
        mutant = text.replace("or membership.get('role') != 'admin'", 'or False')
        self.assertNotEqual(text, mutant)
        namespace = {'__name__': 'synthetic_mutant'}
        exec(compile(mutant, '<owner-control-mutant>', 'exec'), namespace)
        self.rows['/orgs/FS-GG/memberships/owner']['role'] = 'member'
        with self.assertRaises(ValueError):
            self.invoke()
        proof = namespace['authoritative_namespace_absence']('FS.GG.SDD.Commands', 'owner', self.read)
        self.assertTrue(proof['ownerRoleVerified'])


receipt_spec = importlib.util.spec_from_file_location('receipt', repo / 'scripts/verify-publication-receipt.py')
receipt = importlib.util.module_from_spec(receipt_spec)
receipt_spec.loader.exec_module(receipt)


class ReceiptControls(unittest.TestCase):
    """Synthetic writer archives exercise immutable joins and safe extraction."""
    def setUp(self):
        self.source, self.verifier = 'c' * 40, 'd' * 40
        self.files = {'packages/candidate.env': ('schema=fsgg.sdd.release-candidate/v4\nhead='
                      + self.source + '\nversion=2.3.0\ncontracts_version=7.6.0\npackages='
                      + ','.join(runner.PACKAGES) + '\n').encode(),
                      'packages/pre-push.sha256': b'fixture', 'packages/contracts-reuse.json': b'{}',
                      'packages/dependencies/FS.GG.Contracts.7.6.0.nupkg': b'fixture'}
        for package in runner.PACKAGES:
            for base in ['packages/' + package + '.2.3.0.nupkg'] + [
                    'feed-readback/' + package.lower() + '.' + feed + '.nupkg' for feed in ['github', 'nuget']]:
                for suffix in ['', '.entries', '.payloads']:
                    self.files[base + suffix] = b'fixture'
        self.make_archive()

    def make_archive(self):
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, 'w') as archive:
            for path, data in self.files.items():
                archive.writestr(path, data)
        self.raw = stream.getvalue()
        self.digest = hashlib.sha256(self.raw).hexdigest()
        self.metadata = dict(id=17, expired=False, digest='sha256:' + self.digest,
                             name='sdd-2.3.0-dual-feed-receipt-' + self.verifier,
                             workflow_run=dict(id=42, head_sha=self.verifier), size_in_bytes=len(self.raw))

    def read(self, suffix, cap):
        return self.raw if suffix.endswith('/zip') else json.dumps(self.metadata).encode()

    def invoke(self):
        with tempfile.TemporaryDirectory() as directory:
            return receipt.verify('17', self.digest, '42', self.verifier, self.source, '2.3.0',
                                  Path(directory) / 'selected', read=self.read)

    def test_native_writer_digest_and_distinct_original_source_join(self):
        result = self.invoke()
        self.assertEqual(result['candidateSource'], self.source)
        self.assertEqual(result['verificationSource'], self.verifier)
        self.assertFalse(result['publicationAuthorized'])

    def test_wrong_native_writer_artifact_source_or_digest_refuses(self):
        original = dict(self.metadata)
        for changes in [dict(id=18), dict(expired=True), dict(digest='sha256:' + '0' * 64),
                        dict(name='foreign-receipt'), dict(workflow_run=dict(id=43, head_sha=self.verifier)),
                        dict(workflow_run=dict(id=42, head_sha=self.source)), dict(size_in_bytes=0)]:
            self.metadata = original | changes
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                self.invoke()
        self.metadata = original
        self.raw += b'substituted'
        with self.assertRaises(ValueError):
            self.invoke()

    def test_member_traversal_extra_missing_or_original_source_substitution_refuses(self):
        original = dict(self.files)
        for mutation in ['traversal', 'extra', 'missing', 'original-source']:
            self.files = dict(original)
            if mutation == 'traversal':
                self.files['../escape'] = self.files.pop('packages/contracts-reuse.json')
            elif mutation == 'extra':
                self.files['packages/unselected.nupkg'] = b'fixture'
            elif mutation == 'missing':
                self.files.pop('feed-readback/fs.gg.sdd.commands.github.nupkg')
            else:
                self.files['packages/candidate.env'] = self.files['packages/candidate.env'].replace(self.source.encode(), self.verifier.encode())
            self.make_archive()
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                self.invoke()

unittest.main()
