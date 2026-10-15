"""Read-only checks shared by qualification and its source-only controls."""
import base64
import hashlib
from pathlib import Path


def check_package_folders(folders, expected):
    # NuGet may serialize the same directory with or without a trailing separator.
    if len(folders) != 1 or Path(next(iter(folders))).resolve() != Path(expected).resolve():
        raise RuntimeError('unexpected package cache')


def check_package_hashes(raw, raw_sha512, metadata, entry, version):
    # Signed archive SHA512 and NuGet's normalized contentHash are distinct identities.
    observed_raw_sha512 = base64.b64encode(hashlib.sha512(raw).digest()).decode()
    if raw_sha512 != observed_raw_sha512:
        raise RuntimeError('restored raw archive SHA512 mismatch')
    normalized = metadata.get('contentHash')
    if not normalized or entry['resolved'] != version or entry['contentHash'] != normalized:
        raise RuntimeError('restored normalized content hash mismatch')
    return {'nugetContentHash': normalized, 'rawSha512': observed_raw_sha512}
