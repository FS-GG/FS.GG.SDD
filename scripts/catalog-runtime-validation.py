#!/usr/bin/env python3
"""One local command runner for catalog compilation, tests, and runtime validation.

Cooperative process groups are interruption assistance, not containment. Native
runtime commands require explicit selection of the PID namespace backend. This
backend supplies a process lifetime boundary, not a qualified product environment.

A command may also declare a `captureLifetime` (see docs/validation/catalog-runtime.md,
"Governance Verify capture lifetime"). This runner is then the outer admitted runtime of one
Governance Verify provider invocation: it supplies the original work and cleanup ends, never
signals the Verify host inside them, and owns whole-worker teardown after them.
"""
import argparse
import hashlib
import fnmatch
import json
import math
import os
from pathlib import Path
import selectors
import signal
import subprocess
import time


def tree_identity(root, patterns=None):
    """Hash tracked and untracked source bytes, excluding ignored build outputs."""
    listed = subprocess.check_output(
        ['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard'], cwd=root)
    digest = hashlib.sha256()
    for name in sorted(set(listed.split(b'\0')) - {b''}):
        relative = os.fsdecode(name)
        if patterns is not None and not any(fnmatch.fnmatchcase(relative, pattern) for pattern in patterns):
            continue
        path = Path(root) / relative
        if path.is_symlink():
            content = os.fsencode(os.readlink(path))
            kind = b'link'
        elif path.is_file():
            content = path.read_bytes()
            kind = b'file'
        else:
            continue
        digest.update(name + b'\0' + kind + b'\0' + hashlib.sha256(content).digest())
    return digest.hexdigest()


WORK_VARIABLE = 'FSGG_VERIFY_WORK_SECONDS'
CLEANUP_VARIABLE = 'FSGG_VERIFY_CLEANUP_SECONDS'
MAXIMUM_CAPTURE_SECONDS = 1_000_000
DEFAULT_STARTUP_ALLOWANCE_SECONDS = 5
# Literal texts of the Governance Verify host (ProviderHost.fs) the classification reads.
HOST_RESIDENT_REPORT = 'stays resident while a read may be pending'
HOST_RETAINED_REPORT = 'is retained after the original cleanup end'


def capture_lifetime(command):
    """Validate and normalize the capture-lifetime declaration of one command, or None.

    The Governance Verify host measures both ends in whole seconds from its own start; the
    cleanup end is absolute (not a duration after the work end). The runner starts counting
    before the host does, so the host's ends are admitted only when the whole capture lifetime,
    plus a startup allowance for the host to begin, fits inside the runner's own work window.
    Then the runner never has to signal a host that is still inside its declared capture window.
    """
    declared = command.get('captureLifetime')
    if declared is None:
        return None
    if not isinstance(declared, dict) or not set(declared) <= {'workSeconds', 'cleanupEndSeconds', 'startupAllowanceSeconds'} \
            or not {'workSeconds', 'cleanupEndSeconds'} <= set(declared):
        raise ValueError('captureLifetime requires workSeconds and cleanupEndSeconds and allows startupAllowanceSeconds')
    work, cleanup = declared['workSeconds'], declared['cleanupEndSeconds']
    allowance = declared.get('startupAllowanceSeconds', DEFAULT_STARTUP_ALLOWANCE_SECONDS)
    for name, value, low in [('workSeconds', work, 1), ('cleanupEndSeconds', cleanup, 1), ('startupAllowanceSeconds', allowance, 0)]:
        if type(value) is not int or not low <= value <= MAXIMUM_CAPTURE_SECONDS:
            raise ValueError('captureLifetime.' + name + ' must be a whole number of seconds from ' + str(low) + ' to ' + str(MAXIMUM_CAPTURE_SECONDS))
    if cleanup < work:
        raise ValueError('captureLifetime.cleanupEndSeconds must not precede workSeconds (the host measures both from its start)')
    if command.get('mode') != 'native-runtime' or command.get('backend') != 'pid-namespace':
        raise ValueError('captureLifetime requires native-runtime mode with the pid-namespace backend: whole-worker teardown is the outer runtime duty')
    timeout = command.get('timeoutSeconds', 300)
    if allowance + cleanup > timeout:
        raise ValueError('captureLifetime startup allowance plus cleanupEndSeconds must fit inside timeoutSeconds, so the runner never signals a host inside its capture window')
    names = set(command.get('environment', {}))
    if names & {WORK_VARIABLE, CLEANUP_VARIABLE}:
        raise ValueError('captureLifetime is the only supplier of ' + WORK_VARIABLE + ' and ' + CLEANUP_VARIABLE)
    return {'workSeconds': work, 'cleanupEndSeconds': cleanup, 'startupAllowanceSeconds': allowance}


def process_start_time(pid):
    """Kernel start time (clock ticks) of an outer-view pid, or None when it is absent."""
    try:
        text = Path('/proc/' + str(pid) + '/stat').read_text()
    except OSError:
        return None
    # The command name may contain spaces and parentheses; the fields after the last ')' are fixed.
    fields = text.rsplit(')', 1)[-1].split()
    return fields[19] if len(fields) > 19 else None


def read_witness(path):
    """Parse '<outer-pid> <start-time>' lines appended by worker descendants."""
    entries = []
    for line in Path(path).read_text().splitlines():
        parts = line.split()
        if len(parts) == 2 and parts[0].isdigit() and parts[1].isdigit():
            entries.append((int(parts[0]), parts[1]))
    return entries


def classify_capture(declared, result, timeout):
    """Describe how the capture lifetime ended, from facts the runner itself observed."""
    stderr = ''
    log = result.get('streams', {}).get('stderr', {}).get('log')
    if log:
        try:
            stderr = Path(log).read_bytes().decode('utf-8', 'replace')
        except OSError:
            pass
    outer_end = result.get('firstFailure') in ('timeout', 'cancelled', 'backend-timeout-or-kill', 'cleanup-timeout')
    if outer_end:
        outcome = 'worker-ended-by-outer-runtime'
    elif result.get('exitCode') == 4 and HOST_RETAINED_REPORT in stderr:
        outcome = 'host-exited-with-retained-session'
    elif result.get('exitCode') is not None:
        outcome = 'host-exited-after-settlement'
    else:
        outcome = 'unknown'
    return dict(declared, outcome=outcome, outerRuntimeEnded=outer_end,
                hostReportedResident=HOST_RESIDENT_REPORT in stderr,
                hostReportedRetained=HOST_RETAINED_REPORT in stderr,
                runnerWorkEndSeconds=timeout,
                hostSettled=outcome == 'host-exited-after-settlement')


def validate(command):
    argv = command.get('argv')
    if not isinstance(argv, list) or not argv or any(not isinstance(x, str) or '\0' in x for x in argv):
        raise ValueError('argv must be a nonempty array of literal strings')
    for key, default in [('timeoutSeconds', 300), ('cleanupSeconds', 15)]:
        value = command.get(key, default)
        if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or value <= 0:
            raise ValueError(key + ' must be finite and positive')
    cap = command.get('outputBytes', 8 * 1024 * 1024)
    if type(cap) is not int or cap <= 0:
        raise ValueError('outputBytes must be a positive integer')
    if command.get('mode', 'ordinary') not in ('ordinary', 'native-runtime'):
        raise ValueError('mode must be ordinary or native-runtime')
    backend = command.get('backend')
    if backend not in (None, 'pid-namespace'):
        raise ValueError('unsupported backend')
    if command.get('mode', 'ordinary') == 'ordinary' and backend is not None:
        raise ValueError('backend is selected only for native-runtime mode')
    env = command.get('environment', {})
    if not isinstance(env, dict) or any(not isinstance(k, str) or not isinstance(v, str) or not k or '=' in k or '\0' in k + v for k, v in env.items()):
        raise ValueError('environment must contain string names and values')
    capture_lifetime(command)
    witness = command.get('descendantWitnessPath')
    if witness is not None and (not isinstance(witness, str) or not witness.startswith('/') or '\0' in witness):
        raise ValueError('descendantWitnessPath must be an absolute path')
    return argv


# A library caller can inspect incomplete owners; a CLI invocation keeps the same
# live child until it actually exits. No new signaling begins after its deadline.
INCOMPLETE_OWNERS = []


def run_command(command, log_dir, cancel=None):
    argv = validate(command)
    capture = capture_lifetime(command)
    witness = command.get('descendantWitnessPath')
    result = {'name': command.get('name', 'command'), 'command': argv,
              'cwd': str(Path(command.get('cwd', '.')).resolve()), 'exitCode': None,
              'firstFailure': None, 'cleanup': 'not-started', 'streams': {},
              'outputBytesObserved': 0, 'outputBytesRetained': 0,
              'descendantTeardown': 'not-proven'}
    launch_argv = argv
    native = command.get('mode') == 'native-runtime'
    if native:
        if command.get('backend') != 'pid-namespace':
            result['firstFailure'] = 'native-teardown-backend-not-selected'
            return result
        if not all(os.access(path, os.X_OK) for path in ('/usr/sbin/unshare', '/usr/sbin/timeout')):
            result['firstFailure'] = 'native-teardown-backend-unavailable'
            return result
        # timeout is namespace PID 1. Its exit retires the namespace's children,
        # including separate sessions. kill-child covers outer cancellation.
        launch_argv = ['/usr/sbin/unshare', '--user', '--map-root-user', '--pid',
                       '--fork', '--kill-child=KILL', '/usr/sbin/timeout',
                       '--signal=KILL', str(command.get('timeoutSeconds', 300)) + 's'] + argv
        result['backend'] = 'pid-namespace'
        result['backendCommand'] = launch_argv
        result['productEnvironmentQualified'] = False
        result['procView'] = 'inherited-outer-pid-namespace'
        result['descendantTeardown'] = 'pid-namespace-lifetime-boundary-selected'
    if witness is not None:
        # The runner creates the witness file; descendants only append '<outer-pid> <start-time>'.
        try:
            Path(witness).touch(mode=0o600, exist_ok=False)
        except OSError as error:
            result['firstFailure'] = 'descendant-witness-not-fresh: ' + str(error)
            return result
    directory = Path(log_dir)
    directory.mkdir(parents=True, exist_ok=True)
    timeout = command.get('timeoutSeconds', 300)
    cleanup = command.get('cleanupSeconds', 15)
    cap = command.get('outputBytes', 8 * 1024 * 1024)
    start = time.monotonic()
    work_end = start + timeout
    cleanup_end = work_end + cleanup
    process = None
    selector = selectors.DefaultSelector()
    files = {}
    interrupted = False
    kill_at = None
    eof = {'stdout': False, 'stderr': False}
    try:
        for name in eof:
            path = directory / (name + '.log')
            files[name] = path.open('wb')
            result['streams'][name] = {'log': str(path), 'eof': False, 'retainedBytes': 0}
        environment = os.environ.copy()
        environment.update(command.get('environment', {}))
        if capture is not None:
            # The one supplier of the Verify host's original ends (whole seconds from host start).
            environment[WORK_VARIABLE] = str(capture['workSeconds'])
            environment[CLEANUP_VARIABLE] = str(capture['cleanupEndSeconds'])
        process = subprocess.Popen(launch_argv, cwd=result['cwd'], env=environment,
                                   stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                                   stderr=subprocess.PIPE, start_new_session=True)
        result['pid'] = process.pid
        for name in eof:
            pipe = getattr(process, name)
            os.set_blocking(pipe.fileno(), False)
            selector.register(pipe, selectors.EVENT_READ, name)
        while True:
            now = time.monotonic()
            reason = None
            if cancel is not None and cancel.is_set():
                reason = 'cancelled'
            elif now >= work_end:
                reason = 'timeout'
            if reason and result['firstFailure'] is None:
                result['firstFailure'] = reason
            # Signal only while this Popen still reports its original child live.
            # Groups may escape; this is ordinary cooperative interruption only.
            if result['firstFailure'] and not interrupted:
                interrupted = True
                cleanup_end = min(cleanup_end, now + cleanup)
                if process.poll() is None:
                    try:
                        os.killpg(process.pid, signal.SIGTERM)
                    except ProcessLookupError:
                        pass  # Race with actual exit; the next poll observes status.
                    kill_at = min(cleanup_end, now + min(1.0, cleanup / 2))
            if interrupted and kill_at is not None and now >= kill_at and now < cleanup_end and process.poll() is None:
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                kill_at = None
            for key, _ in selector.select(min(.05, max(0, cleanup_end - now))):
                data = os.read(key.fileobj.fileno(), 65536)
                name = key.data
                if not data:
                    eof[name] = True
                    selector.unregister(key.fileobj)
                    key.fileobj.close()
                    continue
                result['outputBytesObserved'] += len(data)
                remaining = max(0, cap - result['outputBytesRetained'])
                retained = data[:remaining]
                files[name].write(retained)
                result['outputBytesRetained'] += len(retained)
                result['streams'][name]['retainedBytes'] += len(retained)
                if len(data) > remaining and result['firstFailure'] is None:
                    result['firstFailure'] = 'output-limit'
            status = process.poll()
            if status is not None and all(eof.values()):
                result['exitCode'] = status
                result['cleanup'] = 'direct-child-reaped-streams-eof'
                break
            if time.monotonic() >= cleanup_end:
                result['exitCode'] = status
                result['cleanup'] = 'incomplete'
                if result['firstFailure'] is None:
                    result['firstFailure'] = 'cleanup-timeout'
                break
        if native and result['exitCode'] in (124, 137) and result['firstFailure'] is None:
            result['firstFailure'] = 'backend-timeout-or-kill'
        if result['exitCode'] not in (None, 0) and result['firstFailure'] is None:
            result['firstFailure'] = 'nonzero-exit'
    except Exception as error:
        if result['firstFailure'] is None:
            result['firstFailure'] = type(error).__name__ + ': ' + str(error)
        if process is not None:
            # Ordinary failures still retire the known direct child. No historical
            # handles or unknown external process identities are adopted.
            try:
                if process.poll() is None and time.monotonic() < cleanup_end:
                    process.kill()
                result['exitCode'] = process.wait(timeout=max(.001, cleanup_end-time.monotonic()))
                result['cleanup'] = 'direct-child-reaped-streams-not-proven'
            except (OSError, subprocess.TimeoutExpired):
                result['cleanup'] = 'incomplete'
    finally:
        close_errors = []
        resources = [selector] + list(files.values())
        if process is not None:
            if process.returncode is None:
                INCOMPLETE_OWNERS.append(process)
            resources += [getattr(process, name) for name in eof if getattr(process, name) is not None]
        for resource in resources:
            try:
                resource.close()
            except OSError as error:
                close_errors.append(str(error))
        for name in files:
            result['streams'][name]['eof'] = eof[name]
        if close_errors:
            result['closeErrors'] = close_errors
            if result['firstFailure'] is None:
                result['firstFailure'] = 'resource-close-error'
    result['durationSeconds'] = round(time.monotonic() - start, 3)
    if witness is not None:
        # Descendants record their outer pid and kernel start time; after the worker ended none may
        # still be that same process. Kernel retirement of a namespace is asynchronous, so poll briefly.
        entries = read_witness(witness)
        deadline = time.monotonic() + 2
        survivors = [pid for pid, started in entries if process_start_time(pid) == started]
        while survivors and time.monotonic() < deadline:
            time.sleep(.05)
            survivors = [pid for pid, started in entries if process_start_time(pid) == started]
        result['descendantWitness'] = {'path': witness, 'observed': len(entries), 'survivors': survivors}
        if survivors:
            result['descendantTeardown'] = 'witnessed-survivor'
            if result['firstFailure'] is None:
                result['firstFailure'] = 'descendant-survived-teardown'
        elif entries and result['cleanup'] == 'direct-child-reaped-streams-eof':
            result['descendantTeardown'] = 'witnessed-absent'
    if capture is not None:
        result['captureLifetime'] = classify_capture(capture, result, timeout)
    result['passed'] = result['firstFailure'] is None and result['exitCode'] == 0 and all(eof.values())
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    config = json.loads(args.config.read_text())
    commands = config.get('commands')
    if not isinstance(commands, list) or not commands:
        parser.error('config requires a nonempty commands array')
    for command in commands:
        validate(command)
    root = Path(config.get('sourceRoot', '.')).resolve()
    identity_paths = config.get('identityPaths')
    if identity_paths is not None and (not isinstance(identity_paths, list) or not identity_paths or
                                       any(not isinstance(x, str) or not x or x.startswith('/') or '..' in x.split('/') for x in identity_paths)):
        parser.error('identityPaths requires nonempty repository-relative glob patterns')
    report = {'sourceRoot': str(root), 'identityPaths': identity_paths, 'sourceRevision': subprocess.check_output(
        ['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip(),
        'sourceTreeSha256': tree_identity(root, identity_paths), 'packageHashes': [], 'commands': []}
    for name in config.get('packages', []):
        path = Path(name)
        report['packageHashes'].append({'path': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    import threading
    cancellation = threading.Event()
    def cancel_signal(_signum, _frame):
        cancellation.set()
    previous = {sig: signal.signal(sig, cancel_signal) for sig in (signal.SIGINT, signal.SIGTERM)}
    try:
        for index, command in enumerate(commands):
            outcome = run_command(command, args.output / str(index), cancellation)
            report['commands'].append(outcome)
            if not outcome.get('passed', False):
                break
    finally:
        for sig, handler in previous.items():
            signal.signal(sig, handler)
    args.output.mkdir(parents=True, exist_ok=True)
    report['commandsPassed'] = len(report['commands']) == len(commands) and all(x.get('passed', False) for x in report['commands'])
    report['sourceTreeSha256After'] = tree_identity(root, identity_paths)
    report['sourceChangedDuringRun'] = report['sourceTreeSha256After'] != report['sourceTreeSha256']
    report['sourceIdentityStable'] = not report['sourceChangedDuringRun']
    report['passed'] = report['commandsPassed'] and report['sourceIdentityStable']
    if not report['sourceIdentityStable']:
        report['sourceIdentityFailure'] = 'source-changed-during-run'
    (args.output / 'result.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({'passed': report['passed'], 'result': str(args.output / 'result.json')}), flush=True)
    # A failed cleanup result is written before passive waiting. Waiting does not
    # upgrade that result, renew a deadline, or send another termination signal.
    for owner in INCOMPLETE_OWNERS:
        owner.wait()
    return 0 if report['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
