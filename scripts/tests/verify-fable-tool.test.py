#!/usr/bin/env python3
"""Pure archive/launcher controls; never install or execute the fixture tool."""
import argparse
import hashlib
import importlib.util
import os
from pathlib import Path
import shutil
import tempfile
import zipfile

SOURCE = Path(__file__).resolve().parents[1] / 'verify-fable-tool.py'
spec = importlib.util.spec_from_file_location('verify_fable_tool', SOURCE)
verifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verifier)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, required=True)
    parser.add_argument('--sdk-template', type=Path, required=True)
    parser.add_argument('--fixture-parent', type=Path)
    args = parser.parse_args()
    assert verifier.digest(args.archive) == verifier.ARCHIVE_SHA256
    passed = []
    with tempfile.TemporaryDirectory(prefix='fable-identity-controls.', dir=args.fixture_parent) as directory:
        root = Path(directory)
        tools = root / 'first-tools'
        store = tools / '.store/fable/5.13.0/fable/5.13.0'
        store.mkdir(parents=True)
        # The independently pinned archive is test data, not executable authority.
        with zipfile.ZipFile(args.archive) as package:
            assert len(package.infolist()) <= 4096
            for entry in package.infolist():
                path = Path(entry.filename)
                assert not path.is_absolute() and '..' not in path.parts
                if entry.is_dir():
                    continue
                target = store / path
                target.parent.mkdir(parents=True, exist_ok=True)
                with package.open(entry) as source, target.open('wb') as destination:
                    shutil.copyfileobj(source, destination, length=1024**2)
        archive = store / 'Fable.nupkg'
        shutil.copyfile(args.archive, archive)
        template = root / 'apphost'
        template.write_bytes(args.sdk_template.read_bytes())
        relative = '.store/fable/5.13.0/fable/5.13.0/' + verifier.ENTRY
        tool = tools / 'fable'
        tool.write_bytes(verifier.reconstruct(template.read_bytes(), relative))
        tool.chmod(0o755)

        def valid(name):
            result = verifier.verify(tool, template, 'prepared-sdk-fixture')
            assert result['packageSha256'] == verifier.ARCHIVE_SHA256
            passed.append(name)
            return result

        def refused(name):
            try:
                verifier.verify(tool, template, 'prepared-sdk-fixture')
            except (ValueError, OSError):
                passed.append(name)
                return
            raise AssertionError('invalid tool accepted: ' + name)

        original = valid('authentic-archive-payload-and-bound-launcher')
        moved = root / 'relocated-tools'
        tools.rename(moved)
        tool = moved / 'fable'
        store = moved / '.store/fable/5.13.0/fable/5.13.0'
        archive = store / 'Fable.nupkg'
        assert valid('absolute-tool-directory-relocation')['launcherSha256'] == original['launcherSha256']
        launcher = tool.read_bytes()
        template_bytes = template.read_bytes()
        # A distinct prepared template models a separately trusted SDK identity.
        modified = bytearray(template_bytes)
        modified[100] ^= 1
        template.write_bytes(modified)
        refused('different-sdk-template-old-launcher-refused')
        tool.write_bytes(verifier.reconstruct(bytes(modified), relative))
        valid('different-prepared-sdk-template-with-correct-binding')
        template.write_bytes(template_bytes)
        tool.write_bytes(launcher)
        tool.write_bytes(b'#!/bin/sh\necho 5.13.0\n')
        refused('fake-version-reporter-refused-without-execution')
        tool.write_bytes(verifier.reconstruct(template_bytes, relative.replace('/fable.dll', '/other.dll')))
        refused('wrong-apphost-entrypoint')
        tool.write_bytes(launcher)
        payload = store / verifier.ENTRY
        payload_bytes = payload.read_bytes()
        payload.write_bytes(payload_bytes + b'changed')
        refused('modified-fable-payload')
        payload.write_bytes(payload_bytes)
        dependency = next(p for p in payload.parent.glob('*.dll') if p != payload)
        dependency_bytes = dependency.read_bytes()
        dependency.unlink()
        refused('missing-executed-dependency')
        dependency.write_bytes(dependency_bytes)
        extra = payload.parent / 'unexpected.dll'
        extra.write_bytes(b'wrong')
        refused('extra-executed-payload')
        extra.unlink()
        saved = root / 'saved-entry'
        payload.rename(saved)
        payload.symlink_to(saved)
        refused('payload-symlink')
        payload.unlink()
        saved.rename(payload)
        with archive.open('r+b') as stream:
            first = stream.read(1)
            stream.seek(0)
            stream.write(bytes([first[0] ^ 1]))
        refused('wrong-package-archive')
        shutil.copyfile(args.archive, archive)
        wrong_version = store.parent / '5.12.0'
        store.rename(wrong_version)
        refused('wrong-package-version-store')
        wrong_version.rename(store)
        valid('exact-restoration')
        size = sum(p.stat().st_size for p in root.rglob('*') if p.is_file())
        assert size < 64 * 1024**2
    print('Fable identity pure controls: ' + str(len(passed)) + ' passed; no fixture tool executed')
    for name in passed:
        print('PASS ' + name)


if __name__ == '__main__':
    main()
