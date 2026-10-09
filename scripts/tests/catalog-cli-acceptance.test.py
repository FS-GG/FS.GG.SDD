"""Pure driver checks; no compiled product/provider invocation."""
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location('driver', Path(__file__).parents[1] / 'catalog-cli-acceptance.py')
driver = importlib.util.module_from_spec(spec)
spec.loader.exec_module(driver)


class DriverTests(unittest.TestCase):
    def test_literal_empty_and_metacharacter_parameters(self):
        fixture = dict(catalog='/catalog', catalogDigest='catalog-digest', provider='provider', archive='/archive',
                       archiveDigest='archive-digest', policy='/policy', policyDigest='policy-digest', platform='linux-x64',
                       transportExecutable='/dotnet', preflightSeconds=30, scaffoldSeconds=60,
                       overrides=[['empty', ''], ['literal', '$(literal);& with spaces']])
        argv = driver.cli_arguments(fixture, Path('/target'))
        self.assertEqual(['--param', 'empty=', '--param', 'literal=$(literal);& with spaces'], argv[-4:])
        self.assertIn('/target', argv)
        altered = driver.replace_value(argv, '--catalog-sha256', 'bad')
        self.assertEqual('catalog-digest', argv[argv.index('--catalog-sha256')+1])
        self.assertEqual('bad', altered[altered.index('--catalog-sha256')+1])

    def test_negative_selection_requires_real_refusal_codes_without_environment_claim(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            archive = root / 'fixture.nupkg'
            archive.write_bytes(b'fixture')
            cli = root / 'cli.dll'
            cli.write_bytes(b'compiled-fixture-identity')
            manifest = root / 'request.json'
            manifest.write_text(json.dumps(dict(catalog='/catalog', catalogDigest='catalog-digest', provider='provider',
                archive=str(archive), archiveDigest='archive-digest', policy='/policy', policyDigest='policy-digest',
                platform='linux-x64', preflightSeconds=30, scaffoldSeconds=60, overrides=[], generatorAssemblySha256='fixture')))
            config = root / 'config.json'
            config.write_text(json.dumps(dict(sourceRoot=str(root), fixtureManifest=str(manifest), transportExecutable='/dotnet',
                cliCommand=[str(cli)], runtime=dict(mode='native-runtime', backend='pid-namespace'), totalSeconds=30)))
            output = root / 'result'
            codes = {'occupied-target':'catalog.targetExists', 'catalog-digest':'catalog.rawDigestMismatch',
                'policy-digest':'catalog.policyInvalid', 'archive-tamper':'catalog.archiveDigestMismatch',
                'unsupported-platform':'catalog.admission.UnresolvedPlatform', 'duplicate-option':'catalog.duplicateOption'}
            observed = []
            def execute(command, logs):
                observed.append(command['name'])
                logs.mkdir()
                dryrun = command['name'] == 'dryrun'
                stdout = logs / 'stdout.log'
                stdout.write_text(json.dumps(dict(status='prepared' if dryrun else 'failed', ownership='Settled',
                    observations=None, diagnostics=[] if dryrun else [dict(code=codes[command['name']])])) )
                return dict(cleanup='direct-child-reaped-streams-eof', streams=dict(stdout=dict(log=str(stdout))),
                    passed=dryrun, exitCode=0 if dryrun else 1, firstFailure=None if dryrun else 'nonzero-exit')
            argv = ['driver', '--config', str(config), '--output', str(output), '--case', 'negatives']
            with mock.patch.object(sys, 'argv', argv), mock.patch.object(driver.harness, 'run_command', execute), \
                 mock.patch.object(driver.harness, 'tree_identity', return_value='stable'):
                self.assertEqual(0, driver.main())
            report = json.loads((output / 'acceptance.json').read_text())
            self.assertEqual(['dryrun'] + list(codes), observed)
            self.assertTrue(report['casesPassed'])
            self.assertTrue(report['sourceIdentityStable'])
            self.assertFalse(report['productEnvironmentQualified'])
            self.assertEqual('negatives', report['acceptanceScope'])

    def test_target_snapshot_observes_byte_changes_and_new_paths(self):
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory)
            (target / 'keep').write_bytes(b'original')
            original = driver.target_snapshot(target)
            (target / 'keep').write_bytes(b'changed')
            self.assertNotEqual(original, driver.target_snapshot(target))
            (target / 'keep').write_bytes(b'original')
            self.assertEqual(original, driver.target_snapshot(target))
            (target / 'new').mkdir()
            self.assertNotEqual(original, driver.target_snapshot(target))

    def test_staging_snapshot_detects_new_owned_sibling(self):
        with tempfile.TemporaryDirectory() as directory:
            parent = Path(directory)
            original = driver.staging_snapshot(parent)
            root = parent / '.fsgg-catalog-owned-fixture'
            root.mkdir()
            (root / 'input').write_bytes(b'owned')
            self.assertNotEqual(original, driver.staging_snapshot(parent))
            (parent / 'unrelated').mkdir()
            self.assertEqual({'.fsgg-catalog-owned-fixture'}, set(driver.staging_snapshot(parent)))


if __name__ == '__main__':
    unittest.main()
