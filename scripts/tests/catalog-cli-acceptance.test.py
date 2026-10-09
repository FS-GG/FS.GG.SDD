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
            for typed in [False, True]:
                observed.clear()
                selected = json.loads(config.read_text())
                if typed:
                    selected.pop('fixtureManifest')
                    selected.update(producerCommand=['/dotnet','/Authoring.dll','emit'], producerArguments=['--policy','literal policy'], verification=dict(mode='ordinary'))
                config.write_text(json.dumps(selected))
                output = root / ('typed-result' if typed else 'result')
                def selected_execute(command, logs):
                    if command['name'] == 'authoring-inputs':
                        observed.append(command['name'])
                        inputs=Path(command['argv'][-1]);inputs.mkdir()
                        (inputs/'request.json').write_bytes(manifest.read_bytes())
                        return dict(passed=True,cleanup='direct-child-reaped-streams-eof')
                    return execute(command, logs)
                argv = ['driver', '--config', str(config), '--output', str(output), '--case', 'negatives']
                with mock.patch.object(sys, 'argv', argv), mock.patch.object(driver.harness, 'run_command', selected_execute), \
                     mock.patch.object(driver.harness, 'tree_identity', return_value='stable'):
                    self.assertEqual(0, driver.main())
                report = json.loads((output / 'acceptance.json').read_text())
                self.assertEqual((['authoring-inputs'] if typed else []) + ['dryrun'] + list(codes), observed)
                self.assertTrue(report['casesPassed'])
                self.assertTrue(report['sourceIdentityStable'])
                self.assertFalse(report['productEnvironmentQualified'])
                self.assertEqual('negatives', report['acceptanceScope'])

    def test_typed_input_command_preserves_literals_and_original_budget(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            config = dict(producerCommand=['/dotnet', '/Authoring.dll', 'emit'],
                producerArguments=['--template-root', 'path with spaces', '--test-input', '$(literal);&', ''],
                verification=dict(mode='ordinary', cwd='/working', timeoutSeconds=30))
            report = {}
            def execute(command, output):
                self.assertEqual(config['producerCommand'] + config['producerArguments'] +
                    ['--out', str(root / 'authoring-inputs')], command['argv'])
                self.assertEqual('/working', command['cwd'])
                self.assertEqual(7, command['timeoutSeconds'])
                self.assertFalse((root / 'authoring-inputs').exists())
                return dict(passed=True, cleanup='direct-child-reaped-streams-eof')
            with mock.patch.object(driver.harness, 'run_command', execute), mock.patch.object(driver.time, 'monotonic', return_value=3):
                self.assertEqual(root / 'authoring-inputs/request.json', driver.fixture_input(config, root, 10, report))
            self.assertTrue(report['fixturePreparation']['passed'])

    def test_typed_input_refusals_do_not_launch(self):
        config = dict(producerCommand=['/dotnet', '/Authoring.dll', 'emit'], verification={})
        for change in [dict(fixtureManifest='/old/request.json'), dict(producerCommand=[]), dict(producerCommand=None),
            dict(producerArguments='not argv'), dict(producerArguments=['--out', '/existing']),
            dict(producerArguments=['--out=/existing']), dict(producerCommand=['bad\0path'])]:
            with self.subTest(change=change), mock.patch.object(driver.harness, 'run_command') as run:
                with self.assertRaises(ValueError):
                    driver.fixture_input(dict(config, **change), Path('/output'), 10, {})
                run.assert_not_called()
        with mock.patch.object(driver.harness, 'run_command') as run:
            with self.assertRaises(ValueError):
                driver.fixture_input(dict(fixtureManifest='/manifest', producerArguments=[]), Path('/output'), 10, {})
            run.assert_not_called()
        with mock.patch.object(driver.harness, 'run_command') as run, mock.patch.object(driver.time, 'monotonic', return_value=10):
            with self.assertRaises(ValueError):driver.fixture_input(config, Path('/output'), 10, {})
            run.assert_not_called()

    def test_typed_input_failure_records_first_failure_without_cli_or_environment_acceptance(self):
        for actual in [dict(passed=False, cleanup='direct-child-reaped-streams-eof'),
                       dict(passed=True, cleanup='unknown')]:
            with self.subTest(actual=actual), tempfile.TemporaryDirectory() as directory:
                root = Path(directory);config = root / 'config.json';output = root / 'result'
                config.write_text(json.dumps(dict(sourceRoot=str(root), producerCommand=['/dotnet','/Authoring.dll','emit'],
                    producerArguments=[], verification=dict(mode='ordinary'),
                    runtime=dict(mode='native-runtime',backend='pid-namespace'))))
                with mock.patch.object(sys, 'argv', ['driver','--config',str(config),'--output',str(output)]), \
                    mock.patch.object(driver.harness, 'tree_identity', return_value='stable'), \
                    mock.patch.object(driver.harness, 'run_command', return_value=actual) as run:
                    self.assertEqual(1, driver.main())
                    self.assertEqual(1, run.call_count)
                    self.assertEqual('authoring-inputs', run.call_args.args[0]['name'])
                report=json.loads((output/'acceptance.json').read_text())
                self.assertFalse(report['passed']);self.assertFalse(report['productEnvironmentQualified'])
                self.assertEqual([],report['cases']);self.assertEqual(actual,report['fixturePreparation'])
                self.assertIn('input production failed',report['firstFailure'])

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
