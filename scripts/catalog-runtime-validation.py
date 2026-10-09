#!/usr/bin/env python3
"""One local command runner for catalog compilation, tests, and runtime validation.

Cooperative process groups are interruption assistance, not containment. Native
runtime commands require explicit selection of the PID namespace backend. This
backend supplies a process lifetime boundary, not a qualified product environment.
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
    return argv


# A library caller can inspect incomplete owners; a CLI invocation keeps the same
# live child until it actually exits. No new signaling begins after its deadline.
INCOMPLETE_OWNERS = []


def run_command(command, log_dir, cancel=None):
    argv = validate(command)
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
