"""Benign real subprocess tests of the maintained harness (no CLR/provider)."""
import importlib.util
from pathlib import Path
import sys
import json
import subprocess
import tempfile
import threading
import unittest

# Test imports must not create source-tree artifacts that change identity.
sys.dont_write_bytecode = True

spec = importlib.util.spec_from_file_location('harness', Path(__file__).parents[1] / 'catalog-runtime-validation.py')
harness = importlib.util.module_from_spec(spec)
spec.loader.exec_module(harness)


class HarnessTests(unittest.TestCase):
    def run_fixture(self, code, **options):
        with tempfile.TemporaryDirectory() as output:
            command = {'argv': [sys.executable, '-c', code], 'timeoutSeconds': 2, 'cleanupSeconds': 1}
            command.update(options)
            result = harness.run_command(command, output, options.pop('cancel', None))
            logs = {name: Path(stream['log']).read_bytes() for name, stream in result['streams'].items()}
            return result, logs

    def test_literal_arguments_environment_and_streams(self):
        result, logs = self.run_fixture('import sys,os; print(repr(sys.argv[1:])); print(os.environ["HARNESS_TEST"],file=sys.stderr)',
                                        argv=[sys.executable, '-c', 'import sys,os; print(repr(sys.argv[1:])); print(os.environ["HARNESS_TEST"],file=sys.stderr)', '', 'a b', '$(literal)'],
                                        environment={'HARNESS_TEST': 'actual'})
        self.assertTrue(result['passed'])
        self.assertIn(b"['', 'a b', '$(literal)']", logs['stdout'])
        self.assertEqual(b'actual\n', logs['stderr'])
        self.assertEqual('direct-child-reaped-streams-eof', result['cleanup'])

    def test_nonzero_is_failure(self):
        result, _ = self.run_fixture('raise SystemExit(7)')
        self.assertEqual(7, result['exitCode'])
        self.assertEqual('nonzero-exit', result['firstFailure'])

    def test_output_is_capped_before_retention(self):
        result, logs = self.run_fixture('import os; os.write(1,b"x"*20000); os.write(2,b"y"*20000)', outputBytes=100)
        self.assertEqual('output-limit', result['firstFailure'])
        self.assertEqual(100, sum(map(len, logs.values())))
        self.assertGreater(result['outputBytesObserved'], result['outputBytesRetained'])

    def test_timeout_observes_actual_termination(self):
        result, _ = self.run_fixture('import time; time.sleep(10)', timeoutSeconds=.15)
        self.assertEqual('timeout', result['firstFailure'])
        self.assertLess(result['exitCode'], 0)
        self.assertEqual('direct-child-reaped-streams-eof', result['cleanup'])

    def test_term_ignored_escalates_to_kill(self):
        result, _ = self.run_fixture('import signal,time; signal.signal(signal.SIGTERM,signal.SIG_IGN); print("ready",flush=True); time.sleep(10)', timeoutSeconds=.2)
        self.assertEqual('timeout', result['firstFailure'])
        self.assertEqual(-9, result['exitCode'])
        self.assertEqual('direct-child-reaped-streams-eof', result['cleanup'])

    def test_cwd_is_selected(self):
        with tempfile.TemporaryDirectory() as cwd:
            result, logs = self.run_fixture('import os; print(os.getcwd())', cwd=cwd)
            self.assertTrue(result['passed'])
            self.assertEqual(cwd.encode() + b'\n', logs['stdout'])

    def test_cancellation(self):
        cancel = threading.Event()
        timer = threading.Timer(.15, cancel.set)
        timer.start()
        try:
            result, _ = self.run_fixture('import time; time.sleep(10)', cancel=cancel)
        finally:
            timer.join()
        self.assertEqual('cancelled', result['firstFailure'])
        self.assertLess(result['exitCode'], 0)

    def test_launch_failure_is_not_zero(self):
        result, _ = self.run_fixture('', argv=['/definitely/not/a/tool'])
        self.assertIsNone(result['exitCode'])
        self.assertFalse(result['passed'])
        self.assertEqual('not-started', result['cleanup'])

    def test_native_runtime_refuses_before_launch(self):
        with tempfile.TemporaryDirectory() as output:
            result = harness.run_command({'argv': ['/definitely/not/a/tool'], 'mode': 'native-runtime'}, output)
        self.assertEqual('native-teardown-backend-not-selected', result['firstFailure'])
        self.assertNotIn('pid', result)

    def test_config_report_stops_after_first_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            config = root / 'config.json'
            repository = Path(__file__).resolve().parents[2]
            config.write_text(json.dumps({'sourceRoot': str(repository), 'commands': [
                {'argv': [sys.executable, '-c', 'raise SystemExit(7)']},
                {'argv': [sys.executable, '-c', 'raise SystemExit(0)']}]}))
            completed = subprocess.run([sys.executable, str(repository / 'scripts/catalog-runtime-validation.py'),
                                        '--config', str(config), '--output', str(root / 'output')],
                                       capture_output=True, timeout=5)
            self.assertEqual(1, completed.returncode)
            report = json.loads((root / 'output/result.json').read_text())
            self.assertEqual(1, len(report['commands']))
            self.assertEqual(7, report['commands'][0]['exitCode'])
            self.assertEqual(64, len(report['sourceTreeSha256']))
            self.assertEqual(40, len(report['sourceRevision']))

    def test_tracked_source_mutation_refuses_exact_source_acceptance(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            repository = root / 'repo'
            repository.mkdir()
            source = repository / 'source.txt'
            source.write_text('before')
            for argv in [['git', 'init', '-q'], ['git', 'add', 'source.txt'],
                         ['git', '-c', 'user.name=Harness Test', '-c', 'user.email=harness@example.invalid',
                          'commit', '-qm', 'fixture']]:
                subprocess.run(argv, cwd=repository, check=True, capture_output=True)
            config = root / 'config.json'
            config.write_text(json.dumps({'sourceRoot': str(repository), 'commands': [
                {'argv': [sys.executable, '-c', 'from pathlib import Path; Path("source.txt").write_text("after")'],
                 'cwd': str(repository)}]}))
            runner = Path(__file__).resolve().parents[1] / 'catalog-runtime-validation.py'
            completed = subprocess.run([sys.executable, str(runner), '--config', str(config),
                                        '--output', str(root / 'output')], capture_output=True, timeout=5)
            report = json.loads((root / 'output/result.json').read_text())
            self.assertEqual(1, completed.returncode)
            self.assertTrue(report['commandsPassed'])
            self.assertTrue(report['sourceChangedDuringRun'])
            self.assertFalse(report['sourceIdentityStable'])
            self.assertFalse(report['passed'])
            self.assertNotEqual(report['sourceTreeSha256'], report['sourceTreeSha256After'])

    def test_selected_identity_covers_product_and_excludes_disjoint_docs(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'src').mkdir()
            (root / 'src/Body.fs').write_text('before')
            (root / 'notes.md').write_text('before')
            subprocess.run(['git', 'init', '-q'], cwd=root, check=True, capture_output=True)
            subprocess.run(['git', 'add', '.'], cwd=root, check=True, capture_output=True)
            before = harness.tree_identity(root, ['src/*.fs'])
            (root / 'notes.md').write_text('disjoint change')
            self.assertEqual(before, harness.tree_identity(root, ['src/*.fs']))
            (root / 'src/Body.fs').write_text('product change')
            self.assertNotEqual(before, harness.tree_identity(root, ['src/*.fs']))

    def test_namespace_timeout_retires_separate_session_pipe_holder(self):
        # Real benign forked fixture, not product qualification. The descendant
        # keeps both pipes open and creates a separate session inside the namespace.
        code = 'import os,time; pid=os.fork(); os.setsid() if pid==0 else None; print("holder" if pid==0 else "parent",flush=True); time.sleep(10)'
        result, logs = self.run_fixture(code, mode='native-runtime', backend='pid-namespace', timeoutSeconds=.3)
        self.assertIn(b'holder', logs['stdout'])
        self.assertIn(b'parent', logs['stdout'])
        self.assertIsNotNone(result['exitCode'])
        self.assertNotEqual(0, result['exitCode'])
        self.assertEqual('direct-child-reaped-streams-eof', result['cleanup'])
        self.assertIn(result['firstFailure'], ('timeout', 'backend-timeout-or-kill'))
        self.assertLess(result['durationSeconds'], 2)
        self.assertFalse(result['productEnvironmentQualified'])
        self.assertEqual('inherited-outer-pid-namespace', result['procView'])

    def test_namespace_cancellation_retires_separate_session_holder(self):
        cancel = threading.Event()
        timer = threading.Timer(.2, cancel.set)
        timer.start()
        try:
            result, logs = self.run_fixture('import os,time; pid=os.fork(); os.setsid() if pid==0 else None; print("ready",flush=True); time.sleep(10)',
                                            mode='native-runtime', backend='pid-namespace', cancel=cancel)
        finally:
            timer.join()
        self.assertIn(b'ready', logs['stdout'])
        self.assertEqual('cancelled', result['firstFailure'])
        self.assertEqual('direct-child-reaped-streams-eof', result['cleanup'])
        self.assertNotEqual(0, result['exitCode'])
        self.assertLess(result['durationSeconds'], 2)

    def test_namespace_normal_exit_retires_background_holder(self):
        code = 'import os,time; pid=os.fork(); (os.setsid(),print("holder",flush=True),time.sleep(10)) if pid==0 else time.sleep(.1)'
        result, logs = self.run_fixture(code, mode='native-runtime', backend='pid-namespace')
        self.assertIn(b'holder', logs['stdout'])
        self.assertTrue(result['passed'])
        self.assertLess(result['durationSeconds'], 1)
        self.assertFalse(result['productEnvironmentQualified'])

    def test_invalid_limits_refuse(self):
        for value in [0, -1, float('nan'), float('inf'), True]:
            with self.assertRaises(ValueError):
                harness.validate({'argv': ['tool'], 'timeoutSeconds': value})


if __name__ == '__main__':
    unittest.main()
