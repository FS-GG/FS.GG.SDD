"""Controls for the Governance Verify capture-lifetime join in the maintained runner.

Pure declaration controls always run. The namespace controls run a benign stand-in for the
Verify host, not Governance, and are skipped where unprivileged user namespaces are unavailable
(for example some hosted runners); a skip is not a pass of the teardown claim.
"""
import importlib.util
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import unittest

sys.dont_write_bytecode = True

spec = importlib.util.spec_from_file_location('harness', Path(__file__).parents[1] / 'catalog-runtime-validation.py')
harness = importlib.util.module_from_spec(spec)
spec.loader.exec_module(harness)

NAMESPACE = {'mode': 'native-runtime', 'backend': 'pid-namespace'}


def namespace_available():
    try:
        return subprocess.run(['/usr/sbin/unshare', '--user', '--map-root-user', '--pid', '--fork',
                               '--kill-child=KILL', '/usr/sbin/timeout', '--signal=KILL', '5s', '/bin/true'],
                              capture_output=True, timeout=10).returncode == 0
    except (OSError, subprocess.TimeoutExpired):
        return False


# Records this process as '<outer-pid> <start-time>' (the inherited /proc shows outer pids).
WITNESS = ('import os,sys\n'
           'def witness():\n'
           '    data=open("/proc/self/stat").read()\n'
           '    open(os.environ["WITNESS"],"a").write(data.split()[0]+" "+data.rsplit(")",1)[1].split()[19]+"\\n")\n')


def declared(**overrides):
    capture = {'workSeconds': 5, 'cleanupEndSeconds': 8, 'startupAllowanceSeconds': 2}
    capture.update(overrides)
    return dict(NAMESPACE, argv=['tool'], timeoutSeconds=12, captureLifetime=capture)


class DeclarationTests(unittest.TestCase):
    def test_valid_declaration_normalizes_with_the_default_allowance(self):
        command = declared()
        command['timeoutSeconds'] = 20
        del command['captureLifetime']['startupAllowanceSeconds']
        self.assertEqual({'workSeconds': 5, 'cleanupEndSeconds': 8, 'startupAllowanceSeconds': 5},
                         harness.capture_lifetime(command))
        harness.validate(declared())

    def test_absent_declaration_is_unchanged(self):
        self.assertIsNone(harness.capture_lifetime({'argv': ['tool']}))

    def test_refusals(self):
        refused = {
            'cleanup precedes work': declared(workSeconds=8, cleanupEndSeconds=7),
            'non-integer work': declared(workSeconds=5.5),
            'boolean work': declared(workSeconds=True),
            'zero work': declared(workSeconds=0),
            'negative allowance': declared(startupAllowanceSeconds=-1),
            'above the host bound': declared(cleanupEndSeconds=1_000_001),
            'does not fit the runner work window': declared(cleanupEndSeconds=11),
            'unknown key': declared(extraSeconds=1),
            'ordinary mode': {'argv': ['tool'], 'timeoutSeconds': 12, 'captureLifetime': declared()['captureLifetime']},
            'missing cleanup': dict(NAMESPACE, argv=['tool'], timeoutSeconds=12, captureLifetime={'workSeconds': 5}),
            'not an object': dict(NAMESPACE, argv=['tool'], timeoutSeconds=12, captureLifetime=5),
        }
        for name, command in refused.items():
            with self.subTest(name), self.assertRaises(ValueError):
                harness.validate(command)

    def test_the_runner_is_the_only_supplier_of_the_ends(self):
        for variable in (harness.WORK_VARIABLE, harness.CLEANUP_VARIABLE):
            command = declared()
            command['environment'] = {variable: '1'}
            with self.subTest(variable), self.assertRaises(ValueError):
                harness.validate(command)

    def test_the_boundary_exactly_fits(self):
        command = declared(workSeconds=5, cleanupEndSeconds=10, startupAllowanceSeconds=2)
        self.assertEqual(10, harness.capture_lifetime(command)['cleanupEndSeconds'])

    def test_witness_path_must_be_absolute(self):
        with self.assertRaises(ValueError):
            harness.validate({'argv': ['tool'], 'descendantWitnessPath': 'relative'})

    def test_witness_parsing_ignores_malformed_lines(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'w'
            path.write_text('12 345\nnot a pid\n7\n-1 5\n99 abc\n')
            self.assertEqual([(12, '345')], harness.read_witness(path))

    def test_start_time_distinguishes_a_live_process_and_an_absent_one(self):
        self.assertIsNotNone(harness.process_start_time(os.getpid()))
        self.assertIsNone(harness.process_start_time(2 ** 22 + 7))

    def test_classification_reads_only_runner_observations(self):
        with tempfile.TemporaryDirectory() as directory:
            log = Path(directory) / 'stderr.log'
            def classify(exit_code, failure, text, duration=1.0):
                log.write_text(text)
                result = {'exitCode': exit_code, 'firstFailure': failure, 'durationSeconds': duration,
                          'streams': {'stderr': {'log': str(log)}}}
                return harness.classify_capture({'workSeconds': 1, 'cleanupEndSeconds': 2, 'startupAllowanceSeconds': 1}, result, 9)
            for code in (0, 1, 3):
                settled = classify(code, None if code == 0 else 'nonzero-exit', '')
                self.assertEqual('host-exited-after-settlement', settled['outcome'], code)
                self.assertTrue(settled['hostSettled'])
            # Only the host's own mapping reserves 0/1/3 for a settled session: a crash, a usage error
            # and the tool error do not prove settlement, whatever the stderr says or omits.
            for code in (2, 139, 137):
                result = classify(code, 'nonzero-exit', '')
                self.assertEqual('host-exited-without-settlement-proof', result['outcome'], code)
                self.assertFalse(result['hostSettled'])
            self.assertEqual('host-exited-with-unsettled-session', classify(4, 'nonzero-exit', '')['outcome'])
            retained = classify(4, 'nonzero-exit', 'x ' + harness.HOST_RETAINED_REPORT)
            self.assertEqual('host-exited-with-retained-session', retained['outcome'])
            self.assertFalse(retained['hostSettled'])
            # Each of the host's unsettled reports blocks a settlement, even under a passing-looking exit.
            for text in ('exit 1 is replaced by 4: the capture session ' + harness.HOST_UNSETTLED_REPORT,
                         'the host retains it and ' + harness.HOST_RESIDENT_REPORT):
                for code in (1, 4):
                    result = classify(code, None, text)
                    self.assertEqual('host-exited-with-unsettled-session', result['outcome'], (code, text))
                    self.assertFalse(result['hostSettled'])
            late = classify(0, None, '', duration=4.5)  # window is allowance 1 + cleanup end 2
            self.assertEqual('host-exited-after-its-window', late['outcome'])
            self.assertFalse(late['withinAdmittedWindow'] or late['hostSettled'])
            resident = classify(-9, 'timeout', harness.HOST_RESIDENT_REPORT)
            self.assertEqual('worker-ended-by-outer-runtime', resident['outcome'])
            self.assertTrue(resident['outerRuntimeEnded'] and resident['hostReportedResident'])
            self.assertFalse(resident['hostSettled'])
            self.assertEqual('worker-ended-by-outer-runtime', classify(None, 'cleanup-timeout', '')['outcome'])
            self.assertEqual('unknown', classify(None, None, '')['outcome'])

    def test_worker_scan_finds_a_live_marked_process_and_nothing_else(self):
        marker = 'unit-' + str(os.getpid())
        self.assertEqual([], harness.worker_survivors(marker))
        child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(30)'],
                                 env=dict(os.environ, **{harness.WORKER_MARKER: marker}))
        try:
            self.assertEqual([child.pid], harness.worker_survivors(marker))
            self.assertEqual([], harness.worker_survivors(marker + 'x'))
        finally:
            child.kill()
            child.wait()
        self.assertEqual([], harness.worker_survivors(marker))


class JourneyFixtureTests(unittest.TestCase):
    def test_journey_inputs_are_committed_and_consistent(self):
        root = Path(__file__).resolve().parents[2]
        journey_spec = importlib.util.spec_from_file_location('journey', root / 'scripts/verify-capture-journey.py')
        journey = importlib.util.module_from_spec(journey_spec)
        journey_spec.loader.exec_module(journey)
        fixtures = root / 'tests/fixtures/provider-verify-journey'
        self.assertEqual(['capabilities.yml', 'governance.yml', 'policy.yml', 'tooling.yml'],
                         sorted(p.name for p in (fixtures / 'fsgg').iterdir()))
        script = (fixtures / 'test-fixture.sh').read_text()
        policy = journey.fixture_policy(Path('/f/verify-journey-fixture'), 'p')
        probe = policy['toolProbes'][0]
        self.assertIn(probe['versionPrefix'] + '1.0.0', script)
        self.assertIn('verify-journey-fixture run', (fixtures / 'fsgg/tooling.yml').read_text())
        # The capture window the journey admits fits the runner window it declares.
        command = dict(NAMESPACE, argv=['tool'], timeoutSeconds=journey.VERIFY_TIMEOUT, captureLifetime={
            'workSeconds': journey.CAPTURE_WORK, 'cleanupEndSeconds': journey.CAPTURE_CLEANUP_END,
            'startupAllowanceSeconds': journey.CAPTURE_STARTUP_ALLOWANCE})
        harness.validate(command)
        for mode in ('write', 'none', 'mismatch', 'exit2', 'escape', 'hang'):
            self.assertIn(mode, script)


@unittest.skipUnless(namespace_available(), 'unprivileged user namespaces are unavailable')
class NamespaceTests(unittest.TestCase):
    def run_stand_in(self, code, **options):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            witness = root / 'witness'
            command = dict(NAMESPACE, argv=[sys.executable, '-c', WITNESS + code], timeoutSeconds=12, cleanupSeconds=3,
                           environment={'WITNESS': str(witness)}, descendantWitnessPath=str(witness))
            command.update(options)
            result = harness.run_command(command, root / 'logs')
            logs = {name: Path(stream['log']).read_bytes() for name, stream in result['streams'].items()}
            return result, logs

    def test_settled_host_with_an_escaped_descendant(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            witness = root / 'witness'
            child = WITNESS + 'witness()\nimport time\ntime.sleep(60)\n'
            host = (WITNESS + 'import subprocess,sys,time,os\n'
                    'print(os.environ["FSGG_VERIFY_WORK_SECONDS"],os.environ["FSGG_VERIFY_CLEANUP_SECONDS"],flush=True)\n'
                    'witness()\n'
                    'subprocess.Popen([sys.executable,"-c",' + repr(child) + '],start_new_session=True)\n'
                    'time.sleep(.6)\n')
            command = dict(NAMESPACE, argv=[sys.executable, '-c', host], timeoutSeconds=12, cleanupSeconds=3,
                           environment={'WITNESS': str(witness)}, descendantWitnessPath=str(witness),
                           captureLifetime={'workSeconds': 4, 'cleanupEndSeconds': 6, 'startupAllowanceSeconds': 2})
            result = harness.run_command(command, root / 'logs')
            stdout = Path(result['streams']['stdout']['log']).read_text()
            self.assertEqual('4 6\n', stdout)
            self.assertTrue(result['passed'], result)
            capture = result['captureLifetime']
            self.assertEqual('host-exited-after-settlement', capture['outcome'])
            self.assertTrue(capture['hostSettled'])
            self.assertFalse(capture['outerRuntimeEnded'])
            self.assertEqual(2, result['descendantWitness']['observed'])
            self.assertEqual([], result['descendantWitness']['survivors'])
            self.assertEqual('witnessed-absent', result['descendantTeardown'])
            self.assertEqual([], result['workerScan']['survivors'])
            self.assertTrue(capture['withinAdmittedWindow'])
            self.assertLess(result['durationSeconds'], 4)

    def test_resident_host_is_ended_only_by_the_outer_runtime_after_its_window(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            witness = root / 'witness'
            child = WITNESS + 'witness()\nimport signal,time\nsignal.signal(signal.SIGTERM,signal.SIG_IGN)\ntime.sleep(60)\n'
            # A stand-in that reports as the Governance host does when a read may be pending, then
            # stays resident, ignores SIGTERM and leaves a detached descendant, until the worker is ended.
            host = (WITNESS + 'import signal,subprocess,sys,time\n'
                    'signal.signal(signal.SIGTERM,signal.SIG_IGN)\n'
                    'witness()\n'
                    'subprocess.Popen([sys.executable,"-c",' + repr(child) + '],start_new_session=True)\n'
                    'time.sleep(1)\n'
                    'print("fsgg verify [tool-error]: provider capture session is not released by the original cleanup end; the host retains it and ' + harness.HOST_RESIDENT_REPORT + '",file=sys.stderr,flush=True)\n'
                    'time.sleep(60)\n')
            command = dict(NAMESPACE, argv=[sys.executable, '-c', host], timeoutSeconds=4, cleanupSeconds=3,
                           environment={'WITNESS': str(witness)}, descendantWitnessPath=str(witness),
                           captureLifetime={'workSeconds': 1, 'cleanupEndSeconds': 2, 'startupAllowanceSeconds': 1})
            result = harness.run_command(command, root / 'logs')
            self.assertFalse(result['passed'])
            # The namespace's own KILL backstop and the runner's work end coincide; either is the outer end.
            self.assertIn(result['firstFailure'], ('timeout', 'backend-timeout-or-kill'))
            capture = result['captureLifetime']
            self.assertEqual('worker-ended-by-outer-runtime', capture['outcome'])
            self.assertTrue(capture['hostReportedResident'])
            self.assertFalse(capture['hostSettled'])
            # Not signalled inside its capture window: the outer end is the runner's work end.
            self.assertGreaterEqual(result['durationSeconds'], 3.9)
            self.assertLess(result['durationSeconds'], 6)
            self.assertEqual('direct-child-reaped-streams-eof', result['cleanup'])
            self.assertEqual(2, result['descendantWitness']['observed'])
            self.assertEqual([], result['descendantWitness']['survivors'])
            self.assertEqual('witnessed-absent', result['descendantTeardown'])
            self.assertEqual([], result['workerScan']['survivors'])

    def test_host_exit_four_with_its_retained_report_is_not_a_settlement(self):
        host = ('import sys\n'
                'print("fsgg verify [tool-error]: provider capture session ' + harness.HOST_RETAINED_REPORT + ' (x) with no read pending; exiting",file=sys.stderr)\n'
                'sys.exit(4)\n')
        with tempfile.TemporaryDirectory() as directory:
            command = dict(NAMESPACE, argv=[sys.executable, '-c', host], timeoutSeconds=12, cleanupSeconds=3,
                           captureLifetime={'workSeconds': 1, 'cleanupEndSeconds': 2, 'startupAllowanceSeconds': 1})
            result = harness.run_command(command, directory)
        self.assertEqual(4, result['exitCode'])
        self.assertEqual('host-exited-with-retained-session', result['captureLifetime']['outcome'])
        self.assertFalse(result['captureLifetime']['hostSettled'])
        self.assertFalse(result['passed'])

    def test_a_survivor_outside_the_namespace_boundary_is_detected(self):
        # Ordinary mode has no namespace: a detached descendant outlives the command. The witness
        # must say so rather than letting a zero exit stand for teardown.
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            witness = root / 'witness'
            child = WITNESS + 'witness()\nimport time\ntime.sleep(60)\n'
            host = ('import subprocess,sys,time\n'
                    'subprocess.Popen([sys.executable,"-c",' + repr(child) + '],start_new_session=True,stdin=subprocess.DEVNULL,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)\n'
                    'time.sleep(.6)\n')
            command = {'argv': [sys.executable, '-c', host], 'timeoutSeconds': 10, 'cleanupSeconds': 3,
                       'environment': {'WITNESS': str(witness)}, 'descendantWitnessPath': str(witness)}
            try:
                result = harness.run_command(command, root / 'logs')
            finally:
                for pid, started in harness.read_witness(witness):
                    if harness.process_start_time(pid) == started:
                        os.kill(pid, signal.SIGKILL)
        self.assertEqual('descendant-survived-teardown', result['firstFailure'])
        self.assertEqual('witnessed-survivor', result['descendantTeardown'])
        self.assertEqual(1, len(result['descendantWitness']['survivors']))
        self.assertFalse(result['passed'])

    def test_a_stale_witness_file_is_refused_before_launch(self):
        with tempfile.TemporaryDirectory() as directory:
            witness = Path(directory) / 'witness'
            witness.write_text('1 1\n')
            result = harness.run_command({'argv': ['/definitely/not/a/tool'], 'descendantWitnessPath': str(witness)}, Path(directory) / 'logs')
        self.assertTrue(result['firstFailure'].startswith('descendant-witness-not-fresh'))
        self.assertNotIn('pid', result)


if __name__ == '__main__':
    unittest.main()
