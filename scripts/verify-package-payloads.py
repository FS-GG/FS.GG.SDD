#!/usr/bin/env python3
"""Compare literal package entries; repository signatures are the sole exclusion."""
import argparse
import importlib.util
import hashlib
from pathlib import Path
import sys
import zipfile

sys.dont_write_bytecode = True
_spec = importlib.util.spec_from_file_location("release_occupancy", Path(__file__).with_name("check-sdd-release-occupancy.py"))
_occupancy = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_occupancy)


def payloads(path):
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)):
            raise ValueError(f"{path}: duplicate ZIP entries")
        if "[Content_Types].xml" not in names:
            raise ValueError(f"{path}: missing [Content_Types].xml")
        if any("\n" in name or "\r" in name for name in names):
            raise ValueError(f"{path}: entry names cannot contain newlines")
        return {
            name: hashlib.sha256(archive.read(name)).hexdigest()
            for name in sorted(names)
            if name != ".signature.p7s"
        }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("packages", type=Path, nargs="+")
    args = parser.parse_args()
    if len(args.packages) < 2:
        parser.error("at least two package archives are required")
    try:
        # Validate literal ZIP structure first; excluding a differing signature
        # requires the same bounded SDK verification used by occupancy.
        manifests = [(path, payloads(path)) for path in args.packages]
        original_bytes = args.packages[0].read_bytes()
        original_path, original = manifests[0]
        for path, manifest in manifests[1:]:
            if manifest != original:
                missing = sorted(original.keys() - manifest.keys())
                extra = sorted(manifest.keys() - original.keys())
                changed = sorted(
                    name for name in original.keys() & manifest.keys()
                    if original[name] != manifest[name]
                )
                raise ValueError(
                    f"{path}: payload differs from {original_path}; "
                    f"missing={missing}, extra={extra}, changed={changed}"
                )
        for path in args.packages[1:]:
            _occupancy.verified_contracts_payloads(original_bytes, path.read_bytes(), "package readback")
        # Read and compare every archive before emitting receipt maps.
        for path, manifest in manifests:
            Path(str(path) + ".entries").write_text(
                "".join(name + "\n" for name in manifest), encoding="utf-8"
            )
            Path(str(path) + ".payloads").write_text(
                "".join(f"{digest}  {name}\n" for name, digest in manifest.items()),
                encoding="utf-8",
            )
        print(f"Literal package payloads match: {len(manifests)} archives, {len(original)} entries")
        return 0
    except (OSError, ValueError, zipfile.BadZipFile, RuntimeError, NotImplementedError) as error:
        print(f"Package payload verification refused: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
