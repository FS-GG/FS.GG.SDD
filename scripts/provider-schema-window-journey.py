#!/usr/bin/env python3
"""Installed cross-release journey for the declared schema-v1 provider compatibility window (SDD928, #1139).

Installs every published FS.GG.SDD.Cli release the declaration lists (docs/reference/provider-schema-
compatibility.md) from nuget.org bytes into an empty private cache and proves, on the supported host, the
installed-behaviour guarantees G6-G8 and the installed halves of G1-G2:

  G6  the same schema-v1 registry provider scaffolds on every release; its provider-owned bytes and the
      provider-owned core of the schema-1 provenance record are identical across releases
  G7  the current release reads every older release's workspace read-only (`doctor`): no provenance or
      registry diagnostic and no write
  G8  out-of-range and below-minimum contracts are refused by the current release before any provider
      process or write; a major-2 provider is admitted exactly where the declaration says

Ordinary development verification under ADR-0096 (Linux-x64, .NET 10.0.12, glibc 2.44, at most six CPUs,
credential-free environment, nuget.org the only package source); a local cooperative profile.

  provider-schema-window-journey.py run --work FRESH_DIR [--releases 1.0.0,2.5.1]
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
TEMPLATE = ROOT / 'tests/fixtures/provider-catalog-runtime/opaque-template/content'
STABLE_PROVENANCE_KEYS = ('schemaVersion', 'providerName', 'providerContractVersion', 'templateRef', 'outcome',
                          'requiredMinimumCliVersion', 'effectiveParameters', 'producedPaths', 'mirroredPaths')
REFUSED_CONTRACTS = {  # contract version (and optional minimum) -> why it is out of range
    '3.0.0': None, '0.9.0': None, 'x.y.z': None, '2.1.0-preview': '2.1.0', '2.0.0': None, '2.1.0': '99.0.0',
}


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


jm = load('verify_capture_journey', 'verify-capture-journey.py')
checker = load('schema_window_checker', 'check-provider-schema-window.py')


def version_key(version):
    return tuple(int(part) for part in version.split('.'))


def registry(contract, minimum, source):
    text = ('schemaVersion: 1\nproviders:\n  - name: fixture\n    contractVersion: "' + contract + '"\n'
            '    templateId: fsgg-catalog-opaque-fixture\n    source: ' + str(source) + '\n    parameters:\n      - key: raw\n        required: true\n')
    if minimum is not None:
        text += '    minimumFsggSdd:\n      version: ' + minimum + '\n'
    return text


def tree(root):
    """Content digests of every file under root (links by target); the identity compared before and after."""
    result = {}
    for path in sorted(Path(root).rglob('*')):
        relative = str(path.relative_to(root))
        if path.is_symlink():
            result[relative] = 'link:' + os.readlink(path)
        elif path.is_file():
            result[relative] = hashlib.sha256(path.read_bytes()).hexdigest()
    return result


class Window:
    def __init__(self, journey, releases, current):
        self.journey, self.releases, self.current = journey, releases, current
        self.work = journey.work
        self.tools, self.digests, self.workspaces = {}, {}, {}

    def cli(self, version, *args, cwd, name):
        argv = [str(self.tools[version]), *args]
        actual, out, err = self.journey.step(name, argv, cwd=cwd, timeout=240)
        try:
            report = json.loads(out)
        except ValueError:
            report = None
        return actual, report, out, err

    def install(self):
        downloads = self.work / 'dl'
        downloads.mkdir()
        (self.work / 'nuget-local.config').write_text(
            '<?xml version="1.0" encoding="utf-8"?>\n<configuration><packageSources><clear />'
            f'<add key="local" value="{downloads}" /></packageSources></configuration>\n')
        for version in self.releases:
            try:
                digest = jm.download_nupkg('FS.GG.SDD.Cli', version, downloads / f'fs.gg.sdd.cli.{version}.nupkg')
            except OSError as error:
                self.journey.record(f'FS.GG.SDD.Cli {version} downloads from nuget.org', False, str(error))
                continue
            self.digests[version] = digest
            tool_path = self.work / 'tools' / version
            actual, out, err = self.journey.step(f'install-{version}', [jm.DOTNET, 'tool', 'install', 'FS.GG.SDD.Cli', '--version', version,
                                                 '--tool-path', str(tool_path), '--configfile', str(self.work / 'nuget-local.config')], timeout=600)
            exe = tool_path / 'fsgg-sdd'
            reported = ''
            if actual['passed'] and exe.is_file():
                self.tools[version] = exe
                _, _, out, _ = self.cli(version, '--version', cwd=self.work, name=f'version-{version}')
                reported = out.strip().splitlines()[0] if out.strip() else ''
            self.journey.record(f'FS.GG.SDD.Cli {version} installs from the nuget.org bytes into the empty cache and reports its version',
                                version in self.tools and reported.endswith(version), f'sha256={digest} reported={reported!r}')

    def scaffold(self, version, contract, minimum, label):
        target = self.work / 'ws' / f'{label}-{version}'
        (target / '.fsgg').mkdir(parents=True)
        (target / '.fsgg/providers.yml').write_text(registry(contract, minimum, self.work / 'template'))
        before = tree(target)
        actual, report, out, err = self.cli(version, 'scaffold', '--provider', 'fixture', '--param', 'raw=Acme', '--root', '.', '--json',
                                            cwd=target, name=f'scaffold-{label}-{version}')
        return target, before, actual, report

    def provenance_core(self, target):
        document = json.loads((target / '.fsgg/scaffold-provenance.json').read_text())
        return {key: document.get(key) for key in STABLE_PROVENANCE_KEYS}, document

    def g6(self):
        baseline = None
        for version in self.releases:
            if version not in self.tools:
                self.journey.record(f'G6 {version}: schema-v1 scaffold', False, 'tool not installed')
                continue
            target, before, actual, report = self.scaffold(version, '1.0.0', None, 'v1')
            ok = actual['exitCode'] == 0 and report is not None and report['outcome'] == 'succeeded' and report['scaffold']['outcome'] == 'providerSucceeded'
            detail = '' if ok else f'exit={actual["exitCode"]} outcome={report and report.get("outcome")}'
            core = None
            if ok and (target / '.fsgg/scaffold-provenance.json').is_file():
                core, document = self.provenance_core(target)
                ok = ok and core['schemaVersion'] == 1 and core['providerName'] == 'fixture' and core['providerContractVersion'] == '1.0.0'
                product = (target / 'product.txt').read_bytes()
                ok = ok and b'raw=Acme' in product
                if baseline is None:
                    baseline = (version, core, product)
                else:
                    same_core, same_product = core == baseline[1], product == baseline[2]
                    ok = ok and same_core and same_product
                    detail = '' if ok else f'differs from {baseline[0]}: core={same_core} product={same_product}'
            elif ok:
                ok, detail = False, 'no provenance written'
            self.workspaces[version] = target if ok else None
            self.journey.record(f'G6 {version}: the schema-v1 provider scaffolds, schema-1 provenance written, provider-owned bytes and provenance core '
                                f'identical to {baseline[0] if baseline else "the first release"}', ok, detail)

    def g2_contract_two(self):
        for version in self.releases:
            if version not in self.tools:
                continue
            admitted = version_key(version) >= (2, 1, 0)
            target, before, actual, report = self.scaffold(version, '2.1.0', '2.1.0', 'v2')
            codes = [d.get('id') for d in (report or {}).get('diagnostics', [])]
            if admitted:
                ok = actual['exitCode'] == 0 and report is not None and report['outcome'] == 'succeeded'
                if ok:
                    core, _ = self.provenance_core(target)
                    ok = core['providerContractVersion'] == '2.1.0' and core['requiredMinimumCliVersion'] == '2.1.0' and core['schemaVersion'] == 1
                self.journey.record(f'G2 {version}: a major-2 provider with minimum 2.1.0 is admitted and recorded in schema-1 provenance', ok, f'exit={actual["exitCode"]} {codes}')
            else:
                after = tree(target)
                ok = (actual['exitCode'] != 0 and report is not None and report['outcome'] == 'blocked' and report['scaffold']['providerInvoked'] is False and after == before)
                self.journey.record(f'G2 {version}: a release older than 2.1.0 refuses a major-2 provider before any provider process or write', ok, f'exit={actual["exitCode"]} {codes}')

    def g7(self):
        for version in self.releases:
            source = self.workspaces.get(version)
            if source is None:
                self.journey.record(f'G7 {version}: current release reads the workspace', False, 'no workspace')
                continue
            copy = self.work / 'doctor' / version
            shutil.copytree(source, copy, symlinks=True)
            before = tree(copy)
            actual, report, out, err = self.cli(self.current, 'doctor', '--json', cwd=copy, name=f'doctor-{self.current}-on-{version}')
            ids = [d.get('id') or d.get('code') or '' for d in (report or {}).get('diagnostics', [])]
            bad = [i for i in ids if 'provenance' in i.lower() or 'registry' in i.lower() or 'scaffold.' in i.lower()]
            ok = actual['exitCode'] == 0 and report is not None and report['outcome'] in ('noChange', 'succeededWithWarnings') and not bad and tree(copy) == before
            self.journey.record(f'G7 {self.current} reads the workspace scaffolded by {version} read-only with no provenance or registry diagnostic', ok,
                                f'exit={actual["exitCode"]} outcome={report and report.get("outcome")} diagnostics={ids} tree-unchanged={tree(copy) == before}')

    def g8(self):
        for contract, minimum in REFUSED_CONTRACTS.items():
            target, before, actual, report = self.scaffold(self.current, contract, minimum, 'refused-' + contract.replace('.', '_'))
            codes = [d.get('id') for d in (report or {}).get('diagnostics', [])]
            expected = {'scaffold.providerVersionUnsupported'} if minimum is None or contract == '2.1.0-preview' else {'scaffold.providerCapabilityUnsupported', 'scaffold.providerVersionUnsupported'}
            ok = (actual['exitCode'] != 0 and report is not None and report['outcome'] == 'blocked' and report['scaffold']['providerInvoked'] is False
                  and tree(target) == before and bool(set(codes))
                  and not (target / 'product.txt').exists())
            self.journey.record(f'G8 {self.current}: contract {contract}{" with minimum " + minimum if minimum else ""} is refused before any provider process or write', ok,
                                f'exit={actual["exitCode"]} diagnostics={codes}')
        # Major 1 keeps the legacy reader semantics: any 1.x is admitted.
        target, before, actual, report = self.scaffold(self.current, '1.9.9', None, 'accepted-1_9_9')
        self.journey.record(f'G8 {self.current}: a major-1 contract 1.9.9 keeps the legacy reader semantics and is admitted',
                            actual['exitCode'] == 0 and report is not None and report['outcome'] == 'succeeded', f'exit={actual["exitCode"]}')


def run(args):
    if os.environ.get('VERIFY_CAPTURE_JOURNEY_CLEAN') != '1':
        clean = {'VERIFY_CAPTURE_JOURNEY_CLEAN': '1', 'HOME': os.environ.get('HOME', '/'), 'PATH': '/usr/local/bin:/usr/bin:/bin',
                 'LANG': 'C.UTF-8', 'PYTHONDONTWRITEBYTECODE': '1'}
        os.execve(sys.executable, [sys.executable, '-I', '-B', *sys.argv], clean)
    work = Path(args.work).resolve()
    if work.exists() and any(work.iterdir()):
        raise SystemExit('--work must be a fresh empty directory')
    work.mkdir(parents=True, exist_ok=True)
    os.sched_setaffinity(0, set(sorted(os.sched_getaffinity(0))[:jm.MAXIMUM_CPUS]))
    window, _ = checker.declaration(ROOT)
    declared = window['publishedReleases']
    releases = args.releases.split(',') if args.releases else declared
    journey = jm.Journey(work, releases[-1], '-', {})
    journey.environment = jm.prepare_environment(work)
    facts = jm.host_facts()
    journey.record('host is the supported Linux-x64 / .NET 10.0.12 / glibc 2.44 profile with at most six CPUs',
                   facts['machine'] == 'x86_64' and facts['glibc'] == '2.44' and '10.0.12' in facts['runtimes'] and len(facts['cpus']) <= jm.MAXIMUM_CPUS, json.dumps(facts))
    problems = checker.check(ROOT)
    journey.record('the declaration, the source and the pinned tests agree (check-provider-schema-window)', not problems, '; '.join(problems))
    journey.record('every release the declaration lists is covered by this run', sorted(releases, key=version_key) == sorted(declared, key=version_key),
                   f'covered={releases} declared={declared}')
    shutil.copytree(TEMPLATE, work / 'template')
    (work / 'ws').mkdir()
    (work / 'doctor').mkdir()
    state = Window(journey, sorted(releases, key=version_key), sorted(releases, key=version_key)[-1])
    state.install()
    state.g6()
    state.g2_contract_two()
    state.g7()
    state.g8()
    failed = [r for r in journey.results if not r['passed']]
    report = {'passed': not failed, 'cases': len(journey.results), 'failed': [r['name'] for r in failed], 'host': facts, 'releases': releases,
              'currentRelease': state.current, 'toolNupkgSha256': state.digests, 'results': journey.results,
              'sourceRevision': subprocess.run(['git', '-C', str(ROOT), 'rev-parse', 'HEAD'], capture_output=True, text=True).stdout.strip(),
              'scope': 'local cooperative profile; ordinary verification under ADR-0096; not hosted or hostile; the window preserves SDD transport, not provider content'}
    (work / 'window-result.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({'passed': report['passed'], 'cases': report['cases'], 'failed': report['failed']}))
    for owner in jm.harness.INCOMPLETE_OWNERS:
        owner.wait()
    return 0 if report['passed'] else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest='mode', required=True)
    r = sub.add_parser('run')
    r.add_argument('--work', required=True)
    r.add_argument('--releases', help='comma-separated subset for debugging; a subset cannot pass (the declaration lists all)')
    return run(parser.parse_args())


if __name__ == '__main__':
    raise SystemExit(main())
