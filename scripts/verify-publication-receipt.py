#!/usr/bin/env python3
"""Authenticate one immutable writer receipt before read-only package qualification."""
import argparse
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import re
import stat
import zipfile

_spec = importlib.util.spec_from_file_location('retained_reader', Path(__file__).with_name('preflight-retained-occupancy.py'))
_reader = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_reader)
PACKAGES = _reader.PACKAGES


def verify(artifact_id, digest, run_id, verifier_source, source, version, destination,
           read=_reader.authenticated_reader):
    if (any(not re.fullmatch(r'[1-9][0-9]{0,19}', value) for value in [artifact_id, run_id])
            or not re.fullmatch(r'[0-9a-f]{64}', digest)
            or any(not re.fullmatch(r'[0-9a-f]{40}', value) for value in [verifier_source, source])
            or not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+', version)):
        raise ValueError('invalid immutable writer receipt selection')
    metadata = json.loads(read('actions/artifacts/' + artifact_id, 1024 * 1024))
    if (not isinstance(metadata, dict) or metadata.get('id') != int(artifact_id)
            or metadata.get('expired') is not False or metadata.get('digest') != 'sha256:' + digest
            or metadata.get('name') != 'sdd-' + version + '-dual-feed-receipt-' + verifier_source
            or not isinstance(metadata.get('workflow_run'), dict)
            or metadata['workflow_run'].get('id') != int(run_id)
            or metadata['workflow_run'].get('head_sha') != verifier_source
            or type(metadata.get('size_in_bytes')) is not int
            or not 0 < metadata['size_in_bytes'] <= 128 * 1024**2):
        raise ValueError('writer receipt native identity/digest/source mismatch')
    raw = read('actions/artifacts/' + artifact_id + '/zip', 128 * 1024**2)
    if len(raw) != metadata['size_in_bytes'] or hashlib.sha256(raw).hexdigest() != digest:
        raise ValueError('writer receipt ZIP substitution or truncation')
    expected = {'packages/candidate.env', 'packages/pre-push.sha256', 'packages/contracts-reuse.json',
                'packages/dependencies/FS.GG.Contracts.7.6.0.nupkg'}
    for package in PACKAGES:
        base = 'packages/' + package + '.' + version + '.nupkg'
        expected.update([base, base + '.entries', base + '.payloads'])
        for feed in ['github', 'nuget']:
            base = 'feed-readback/' + package.lower() + '.' + feed + '.nupkg'
            expected.update([base, base + '.entries', base + '.payloads'])
    with zipfile.ZipFile(io.BytesIO(raw)) as archive:
        entries = archive.infolist()
        names = [entry.filename for entry in entries]
        if (len(names) != len(expected) or set(names) != expected
                or sum(entry.file_size for entry in entries) > 192 * 1024**2
                or any(entry.file_size > 32 * 1024**2 or entry.is_dir()
                       or stat.S_IFMT(entry.external_attr >> 16) not in [0, stat.S_IFREG]
                       for entry in entries)):
            raise ValueError('writer receipt archive member/path/size boundary refused')
        identity = ('schema=fsgg.sdd.release-candidate/v4\nhead=' + source + '\nversion=' + version
                    + '\ncontracts_version=7.6.0\npackages=' + ','.join(PACKAGES) + '\n')
        if archive.read('packages/candidate.env') != identity.encode():
            raise ValueError('writer receipt original candidate source/version mismatch')
        destination.mkdir(mode=0o700, parents=True, exist_ok=False)
        for entry in entries:
            target = destination / entry.filename
            target.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
            with target.open('xb') as output:
                output.write(archive.read(entry))
            target.chmod(0o600)
    return dict(artifactId=int(artifact_id), archiveSha256=digest, writerRun=int(run_id),
                candidateSource=source, verificationSource=verifier_source, version=version,
                publicationAuthorized=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['artifact-id', 'digest', 'run-id', 'verifier-source', 'source', 'version']:
        parser.add_argument('--' + name, required=True)
    parser.add_argument('--destination', type=Path, required=True)
    args = parser.parse_args()
    try:
        print(json.dumps(verify(**vars(args)), sort_keys=True))
    except (ValueError, OSError, KeyError, TypeError, zipfile.BadZipFile):
        parser.exit(1, 'immutable writer receipt refused; qualification blocked\n')


if __name__ == '__main__':
    main()
