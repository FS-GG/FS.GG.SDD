#!/usr/bin/env python3
"""Authenticate a retained candidate, then call the existing strict read-only preflight."""
import argparse
import base64
import hashlib
import io
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
import zipfile

sys.dont_write_bytecode = True
REPOSITORY = 'FS-GG/FS.GG.SDD'
PACKAGES = ['FS.GG.SDD.Artifacts', 'FS.GG.SDD.Commands', 'FS.GG.SDD.Cli', 'FS.GG.SDD.Knowledge']
SCRIPTS = Path(__file__).resolve().parent
MAX_ARCHIVE = 32 * 1024 * 1024


class Refused(ValueError):
    def __init__(self, reason, result):
        super().__init__(reason)
        self.result = dict(result, outcome='Unknown', reason=reason)


def sanitized(text):
    for name in ['GH_TOKEN', 'GITHUB_TOKEN', 'FSGG_PACKAGES_READ_TOKEN']:
        value = os.environ.get(name)
        if value:
            text = text.replace(value, '[redacted]')
            for actor in [os.environ.get('GITHUB_ACTOR', ''), os.environ.get('FSGG_PACKAGES_READ_ACTOR', '')]:
                encoded = base64.b64encode((actor + ':' + value).encode()).decode()
                text = text.replace(encoded, '[redacted]')
    text = re.sub(r'(?i)(authorization\s*:\s*(?:bearer|basic)\s+)\S+', r'\1[redacted]', text)
    return ''.join(character for character in text if character in '\n\t' or ord(character) >= 32)


def diagnostic_record(raw):
    text = sanitized(raw.decode('utf-8', errors='replace')).encode('utf-8')
    bounded = text[:16384].decode('utf-8', errors='ignore')
    return dict(diagnostic=bounded, diagnosticBytes=len(bounded.encode('utf-8')),
                diagnosticTruncated=len(raw) > 16384 or len(text) > 16384)


def typed_observations(text, version, contracts):
    observations = []
    dependency = None
    for line in text.splitlines():
        if line == f'FS.GG.Contracts {contracts}: qualified published dependency; no push':
            dependency = dict(packageId='FS.GG.Contracts', version=contracts,
                              disposition='qualified-published-dependency-no-push')
        for package in PACKAGES:
            for feed in ['github', 'nuget']:
                prefix = f'{package} {version} {feed}: '
                if line == prefix + 'absent':
                    observations.append(dict(packageId=package, version=version, feed=feed, status='Absent'))
                elif line == prefix + 'exact retained payload already present':
                    observations.append(dict(packageId=package, version=version, feed=feed,
                                             status='MatchedRetainedPayload'))
    return observations, dependency


def authenticated_reader(suffix, cap):
    token = os.environ.get('GH_TOKEN', '')
    if not token or not os.environ.get('GITHUB_ACTOR'):
        raise ValueError('normal authenticated producer read context required; occupancy Unknown')

    class Redirect(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, request, response, code, message, headers, url):
            if urllib.parse.urlparse(url).scheme != 'https':
                raise ValueError('non-HTTPS artifact redirect refused')
            redirected = super().redirect_request(request, response, code, message, headers, url)
            if redirected is not None and urllib.parse.urlparse(url).netloc != urllib.parse.urlparse(request.full_url).netloc:
                redirected.remove_header('Authorization')
            return redirected

    request = urllib.request.Request('https://api.github.com/repos/' + REPOSITORY + '/' + suffix,
                                     headers={'Authorization': 'Bearer ' + token,
                                              'Accept': 'application/vnd.github+json'})
    try:
        with urllib.request.build_opener(Redirect()).open(request, timeout=30) as response:
            raw = response.read(cap + 1)
    except urllib.error.HTTPError as error:
        # An inaccessible namespace cannot establish either absence or custody.
        raise ValueError('authenticated candidate read HTTP' + str(error.code) + '; custody/occupancy Unknown') from error
    if len(raw) > cap:
        raise ValueError('candidate response exceeds selected byte bound')
    return raw


def preflight(run_id, archive_sha256, publish_version, readback_version, destination,
              read=authenticated_reader, command=subprocess.run, expected_source=None, expected_version=None):
    # Validate the entire mode before downloading bytes or launching any helper.
    if (not re.fullmatch(r'[1-9][0-9]{0,19}', run_id)
            or not re.fullmatch(r'[0-9a-f]{64}', archive_sha256)
            or publish_version or readback_version):
        raise ValueError('occupancy-only requires one run ID and archive SHA256, with version/readback_version empty')
    if ((expected_source is None) != (expected_version is None)
            or (expected_source is not None and (not re.fullmatch(r'[0-9a-f]{40}', expected_source)
                or not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+', expected_version)))):
        raise ValueError('retained selection requires exact source and stable version together')
    run = json.loads(read('actions/runs/' + run_id, 1024 * 1024))
    if not isinstance(run, dict):
        raise ValueError('selected run metadata malformed; custody Unknown')
    source = run.get('head_sha', '')
    if (run.get('id') != int(run_id) or run.get('status') != 'completed'
            or run.get('conclusion') != 'success' or run.get('event') != 'workflow_dispatch'
            or run.get('head_branch') != 'main'
            or run.get('path') != '.github/workflows/release.yml'
            or run.get('repository', {}).get('full_name') != REPOSITORY
            or run.get('head_repository', {}).get('full_name') != REPOSITORY
            or not re.fullmatch(r'[0-9a-f]{40}', source)):
        raise ValueError('selected run is not an authenticated successful same-repository release candidate')
    if expected_source is not None and source != expected_source:
        raise ValueError('original candidate source differs from explicit retained selection')
    original_gates = None
    if expected_source is not None:
        jobs = json.loads(read('actions/runs/' + run_id + '/jobs?per_page=100', 1024 * 1024))
        required = ['Resolve published Contracts and four coherent SDD versions',
                    'Contracts tests (publish gate)', 'Artifacts tests (publish gate)',
                    'Commands tests and authored public surface (publish gate)',
                    'CLI tests (publish gate)', 'Knowledge tests (publish gate)',
                    'Build and retain four coherent SDD archives once; reuse published Contracts']
        entries = jobs.get('jobs') if isinstance(jobs, dict) else None
        if (not isinstance(entries, list) or jobs.get('total_count') != len(entries)
                or len(entries) > 100 or any(not isinstance(job, dict) for job in entries)):
            raise ValueError('original candidate qualification job population incomplete')
        for name in required:
            matches = [job for job in entries if job.get('name') == name]
            if (len(matches) != 1 or matches[0].get('run_id') != int(run_id)
                    or matches[0].get('head_sha') != source or matches[0].get('status') != 'completed'
                    or matches[0].get('conclusion') != 'success'):
                raise ValueError('original candidate qualification prerequisite missing or unsuccessful')
        original_gates = required
    listing = json.loads(read('actions/runs/' + run_id + '/artifacts?per_page=100', 1024 * 1024))
    if not isinstance(listing, dict):
        raise ValueError('candidate artifact population malformed')
    artifacts = listing.get('artifacts')
    if (not isinstance(artifacts, list) or listing.get('total_count') != len(artifacts)
            or len(artifacts) > 100 or any(not isinstance(a, dict) for a in artifacts)):
        raise ValueError('candidate artifact population incomplete or malformed')
    selected = [a for a in artifacts if a.get('name') == 'coherent-sdd-packages-' + source]
    if len(selected) != 1:
        raise ValueError('exactly one original coherent candidate artifact required')
    artifact = selected[0]
    if (type(artifact.get('id')) is not int or artifact['id'] <= 0
            or artifact.get('expired') is not False
            or type(artifact.get('size_in_bytes')) is not int
            or not 0 < artifact['size_in_bytes'] <= MAX_ARCHIVE
            or artifact.get('digest') != 'sha256:' + archive_sha256):
        raise ValueError('candidate expiration/size/authenticated digest mismatch')
    raw = read('actions/artifacts/' + str(artifact['id']) + '/zip', MAX_ARCHIVE)
    if len(raw) != artifact['size_in_bytes'] or hashlib.sha256(raw).hexdigest() != archive_sha256:
        raise ValueError('original candidate archive bytes substituted or incomplete')
    with zipfile.ZipFile(io.BytesIO(raw)) as archive:
        entries = archive.infolist()
        names = [entry.filename for entry in entries]
        if (len(entries) != 9 or len(set(names)) != 9
                or sum(entry.file_size for entry in entries) > 64 * 1024 * 1024
                or any(entry.file_size > 32 * 1024 * 1024 or entry.is_dir()
                       or stat.S_IFMT(entry.external_attr >> 16) not in [0, stat.S_IFREG]
                       for entry in entries)):
            raise ValueError('original candidate archive structure exceeds fixed regular-file population')
        identity = archive.read('candidate.env').decode('utf-8', errors='strict')
        lines = identity.splitlines()
        if len(lines) != 5 or any(line.count('=') != 1 for line in lines):
            raise ValueError('candidate identity shape invalid')
        fields = dict(line.split('=', 1) for line in lines)
        version = fields.get('version', '')
        contracts = fields.get('contracts_version', '')
        if (not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+', version)
                or contracts != '7.6.0'
                or identity != ('schema=fsgg.sdd.release-candidate/v4\nhead=' + source
                                + '\nversion=' + version + '\ncontracts_version=' + contracts
                                + '\npackages=' + ','.join(PACKAGES) + '\n')):
            raise ValueError('candidate does not bind the selected original run/source/coherent inventory')
        if expected_version is not None and version != expected_version:
            raise ValueError('original candidate version differs from explicit retained selection')
        expected = {name + '.' + version + '.nupkg' for name in PACKAGES}
        expected.update(['candidate.env', 'pre-push.sha256', 'contracts-reuse.json',
                         'occupancy-observation.json', 'dependencies/FS.GG.Contracts.' + contracts + '.nupkg'])
        if set(names) != expected:
            raise ValueError('candidate contains unexpected paths or missing retained bytes')
        destination.mkdir(mode=0o700, parents=True, exist_ok=False)
        for entry in entries:
            target = destination / entry.filename
            target.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
            with target.open('xb') as output:
                output.write(archive.read(entry))
            target.chmod(0o400)

    environment = dict(os.environ, PYTHONDONTWRITEBYTECODE='1')
    custody_environment = {key: value for key, value in environment.items()
                           if key not in ['GH_TOKEN', 'GITHUB_TOKEN', 'FSGG_PACKAGES_READ_TOKEN',
                                          'FSGG_PACKAGES_READ_ACTOR']}
    proof_path = destination.parent / 'namespace-proof.json'
    commands = [
        ['bash', str(SCRIPTS / 'verify-release-candidate.sh'), str(destination), source, version, contracts],
        [sys.executable, str(SCRIPTS / 'check-sdd-release-occupancy.py'), version,
         '--contracts-version', contracts, '--candidate', str(destination),
         '--namespace-proof', str(proof_path)],
    ]
    result = dict(schema='fsgg.sdd.retained-occupancy-preflight/v1', publicationAuthorized=False,
                  candidateRun=int(run_id), candidateArtifact=artifact['id'],
                  archiveSha256=archive_sha256, candidateSource=source,
                  version=version, contractsVersion=contracts,
                  verificationSource=os.environ.get('GITHUB_SHA'),
                  verifierFiles={name: hashlib.sha256((SCRIPTS / name).read_bytes()).hexdigest()
                                 for name in ['preflight-retained-occupancy.py', 'verify-release-candidate.sh',
                                              'check-sdd-release-occupancy.py']}, helpers=[])
    if original_gates is not None:
        result['originalQualificationGates'] = original_gates
    for index, argv in enumerate(commands):
        stage = 'custody' if index == 0 else 'occupancy'
        try:
            completed = command(argv, cwd=SCRIPTS.parent, env=custody_environment if index == 0 else environment, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, timeout=30 if index == 0 else 180, check=False)
        except subprocess.TimeoutExpired as error:
            result['helpers'].append(dict(stage=stage, exitCode=None, timedOut=True,
                                          **diagnostic_record(error.output or b'')))
            raise Refused(stage + ' timed out; disposition Unknown', result) from error
        recorded = diagnostic_record(completed.stdout)
        result['helpers'].append(dict(stage=stage, exitCode=completed.returncode, timedOut=False,
                                      capturedBytes=len(completed.stdout), **recorded))
        if recorded['diagnosticTruncated']:
            raise Refused('preflight diagnostic output exceeds fixed bound; occupancy Unknown', result)
        if stage == 'occupancy':
            result['observations'], result['contractsDisposition'] = typed_observations(recorded['diagnostic'], version, contracts)
        if completed.returncode:
            raise Refused('original custody refused' if index == 0 else 'strict occupancy refused; disposition Unknown', result)
    if proof_path.exists():
        raw_proof = proof_path.read_bytes()
        if len(raw_proof) > 16384:
            raise Refused('namespace proof exceeds fixed bound; occupancy Unknown', result)
        proof = json.loads(raw_proof)
        if (not isinstance(proof, dict) or proof.get('schema') != 'fsgg.sdd.namespace-proof/v1'
                or proof.get('publicationAuthorized') is not False or not isinstance(proof.get('proofs'), list)):
            raise Refused('malformed typed namespace proof; occupancy Unknown', result)
        result['namespaceProof'] = proof
    keys = {(row['packageId'], row['feed']) for row in result['observations']}
    if len(result['observations']) != 8 or len(keys) != 8 or result['contractsDisposition'] is None:
        raise Refused('successful checker output lacks complete typed disposition; occupancy Unknown', result)
    return dict(result, outcome='verified-read-only-occupancy')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run-id', required=True)
    parser.add_argument('--archive-sha256', required=True)
    parser.add_argument('--publish-version', default='')
    parser.add_argument('--readback-version', default='')
    parser.add_argument('--expected-source')
    parser.add_argument('--expected-version')
    parser.add_argument('--destination', type=Path, required=True)
    args = parser.parse_args()
    try:
        result = preflight(**vars(args))
    except Refused as error:
        print(json.dumps(error.result, sort_keys=True))
        return 1
    except (ValueError, OSError, KeyError, TypeError, zipfile.BadZipFile, subprocess.TimeoutExpired) as error:
        print(json.dumps(dict(outcome='Unknown', publicationAuthorized=False,
                              reason=str(error) if isinstance(error, ValueError) else type(error).__name__)))
        return 1
    print(json.dumps(result, sort_keys=True))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
