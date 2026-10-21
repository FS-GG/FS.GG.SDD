#!/usr/bin/env python3
"""Verify the pinned Fable payload and SDK-generated Linux tool launcher without executing Fable."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile

VERSION = '5.13.0'
ARCHIVE_SHA256 = 'e409e7f2c0f4ecb86d4645b47e99b83fdd1c8d57ee8f279bf0b5b87df67f1012'
ENTRY = 'tools/net10.0/any/fable.dll'
APPHOST_MARKER = b'c3ab8ff13720e8ad9047dd39466b3c8974e592c2fa383d4a3960714caef0c4f2'
MAX_ARCHIVE = 32 * 1024**2
MAX_EXPANDED = 128 * 1024**2


def require(value, reason):
    if not value:
        raise ValueError(reason)


def digest(path):
    result = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024**2), b''):
            result.update(block)
    return result.hexdigest()


def regular(path):
    # Every ancestor belongs to the qualified tool/SDK installation, not a redirect.
    for part in (path, *path.parents):
        require(not part.is_symlink(), 'symlink in identity path: ' + str(part))
    require(stat.S_ISREG(path.stat().st_mode), 'not a regular identity file: ' + str(path))


def reconstruct(template, relative_entry):
    require(template.startswith(b'\x7fELF') and template[18:20] == b'\x3e\x00', 'Linux amd64 SDK apphost required')
    require(template.count(APPHOST_MARKER) == 1, 'SDK apphost placeholder is absent or ambiguous')
    offset = template.index(APPHOST_MARKER)
    require(offset + 1024 <= len(template), 'truncated SDK apphost buffer')
    require(template[offset + len(APPHOST_MARKER):offset + 1024] == bytes(1024 - len(APPHOST_MARKER)), 'unexpected SDK apphost buffer')
    entry = relative_entry.encode('utf-8')
    require(len(entry) < 1024 and b'\0' not in entry, 'invalid relative apphost entry')
    return template[:offset] + entry + bytes(1024 - len(entry)) + template[offset + 1024:]


def verify(tool, sdk_template, sdk_version):
    tool = Path(os.path.abspath(tool))
    sdk_template = Path(os.path.abspath(sdk_template))
    regular(tool)
    regular(sdk_template)
    require(tool.name == 'fable' and os.access(tool, os.X_OK), 'expected executable fable launcher')
    store = tool.parent / '.store' / 'fable' / VERSION / 'fable' / VERSION
    archives = [p for p in (store / 'Fable.nupkg', store / ('fable.' + VERSION + '.nupkg')) if p.exists() or p.is_symlink()]
    require(archives, 'pinned Fable archive is absent')
    for archive in archives:
        regular(archive)
        require(archive.stat().st_size <= MAX_ARCHIVE and digest(archive) == ARCHIVE_SHA256, 'wrong Fable package archive: ' + str(archive))
    expected = set()
    expanded = 0
    with zipfile.ZipFile(archives[0]) as package:
        entries = package.infolist()
        require(0 < len(entries) <= 4096 and len({p.filename for p in entries}) == len(entries), 'invalid package entry inventory')
        for entry in entries:
            path = PurePosixPath(entry.filename)
            require(not path.is_absolute() and '..' not in path.parts and '\\' not in entry.filename and ':' not in path.parts[0] and not entry.flag_bits & 1, 'unsafe package entry')
            require(stat.S_IFMT(entry.external_attr >> 16) in (0, stat.S_IFDIR, stat.S_IFREG), 'special package entry')
            if entry.is_dir():
                continue
            expanded += entry.file_size
            require(expanded <= MAX_EXPANDED, 'expanded package bound')
            # NuGet omits OPC transport metadata from its extracted package store.
            if entry.filename == '[Content_Types].xml' or path.parts[0] in ('_rels', 'package'):
                continue
            installed = store.joinpath(*path.parts)
            regular(installed)
            result = hashlib.sha256()
            with package.open(entry) as source:
                for block in iter(lambda: source.read(1024**2), b''):
                    result.update(block)
            require(installed.stat().st_size == entry.file_size and digest(installed) == result.hexdigest(), 'Fable payload differs: ' + entry.filename)
            expected.add(entry.filename)
        settings = ET.fromstring(package.read('tools/net10.0/any/DotnetToolSettings.xml'))
        commands = [x.attrib for x in settings.iter() if x.tag.rsplit('}', 1)[-1] == 'Command']
        require(commands == [{'Name': 'fable', 'EntryPoint': 'fable.dll', 'Runner': 'dotnet'}], 'wrong Fable command routing')
    # Extra files in the executed payload directory can alter dependency resolution.
    payload_root = store / 'tools' / 'net10.0' / 'any'
    actual = set()
    for path in payload_root.rglob('*'):
        require(not path.is_symlink(), 'payload symlink')
        if path.is_file():
            actual.add(path.relative_to(store).as_posix())
    require(actual == {p for p in expected if p.startswith('tools/net10.0/any/')}, 'unexpected executed payload files')
    relative = (Path('.store') / 'fable' / VERSION / 'fable' / VERSION / ENTRY).as_posix()
    template = sdk_template.read_bytes()
    require(tool.read_bytes() == reconstruct(template, relative), 'launcher does not bind selected SDK template to pinned Fable payload')
    return {'version': VERSION, 'packageSha256': ARCHIVE_SHA256, 'launcherSha256': digest(tool),
            'sdkVersion': sdk_version, 'sdkTemplateSha256': hashlib.sha256(template).hexdigest(),
            'relativeEntryPoint': relative, 'verifiedPackageFiles': len(expected), 'fableExecuted': False}


def selected_sdk():
    dotnet = shutil.which('dotnet')
    require(dotnet, 'dotnet SDK is absent')
    root = Path(dotnet).resolve().parent
    result = subprocess.run([str(root / 'dotnet'), '--version'], capture_output=True, text=True, timeout=15, check=True)
    version = result.stdout.strip()
    require(re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?', version), 'unreadable selected SDK version')
    return root / 'sdk' / version / 'AppHostTemplate' / 'apphost', version


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--tool', required=True)
    args = parser.parse_args()
    identity = {'tool': args.tool}
    try:
        template, version = selected_sdk()
        identity.update(sdkVersion=version, sdkTemplate=str(template), sdkTemplateSha256=digest(template),
                        actualLauncherSha256=digest(Path(args.tool)))
        print(json.dumps(verify(args.tool, template, version), sort_keys=True))
        return 0
    except (ValueError, OSError, subprocess.SubprocessError, zipfile.BadZipFile) as error:
        print('FABLE-TOOL-REFUSAL: ' + str(error) + '; identity=' + json.dumps(identity, sort_keys=True), file=sys.stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
