#!/usr/bin/env python3
"""Exercise the actual readback command, optionally against a retained nupkg."""
import hashlib
from pathlib import Path
import subprocess
import sys
import tempfile
import warnings
import zipfile

command = Path(__file__).parents[1] / "verify-package-payloads.py"
if len(sys.argv) > 1:
    with zipfile.ZipFile(sys.argv[1]) as archive:
        original = [(info.filename, archive.read(info)) for info in archive.infolist()]
else:
    original = [
        ("[Content_Types].xml", b"<Types>original metadata</Types>"),
        ("fixture.nuspec", b"<package>original identity</package>"),
        ("lib/[literal]*?.dll", b"original ordinary payload"),
    ]

ordinary = next(name for name, _ in original if name not in {"[Content_Types].xml", ".signature.p7s"})
passed = 0


def write(path, entries):
    with warnings.catch_warnings():
        warnings.simplefilter("ignore", UserWarning)  # Deliberate duplicate-entry control.
        with zipfile.ZipFile(path, "w") as archive:
            for name, data in entries:
                archive.writestr(name, data)


def check(label, entries, expected, diagnostic=None, corrupt=False, missing=False, third=None):
    global passed
    with tempfile.TemporaryDirectory() as directory:
        root = Path(directory)
        baseline, candidate = root / "original.nupkg", root / "candidate.nupkg"
        write(baseline, original)
        if missing:
            pass
        elif corrupt:
            candidate.write_bytes(b"not a ZIP archive")
        else:
            write(candidate, entries)
        args = [sys.executable, str(command), str(baseline), str(candidate)]
        if third is not None:
            extra = root / "third.nupkg"
            write(extra, third)
            args.append(str(extra))
        result = subprocess.run(args, capture_output=True, text=True)
        assert result.returncode == expected, (label, result.stdout, result.stderr)
        if diagnostic:
            assert diagnostic in result.stderr, (label, result.stderr)
        if expected == 0:
            content = dict(original)["[Content_Types].xml"]
            actual = Path(str(candidate) + ".payloads").read_text()
            assert f"{hashlib.sha256(content).hexdigest()}  [Content_Types].xml\n" in actual
        else:
            assert not list(root.glob("*.entries")) and not list(root.glob("*.payloads"))
    print("PASS", label)
    passed += 1


check("equal archives", original, 0)
check("entry order is immaterial", list(reversed(original)), 0)
check("unverifiable repository signature refuses", [(n, d) for n, d in original if n != ".signature.p7s"] + [(".signature.p7s", b"new signature")], 1, "signature verification")
check("literal Content_Types mutation refuses", [(n, b"changed metadata" if n == "[Content_Types].xml" else d) for n, d in original], 1, "[Content_Types].xml")
check("ordinary entry mutation refuses", [(n, b"changed ordinary payload" if n == ordinary else d) for n, d in original], 1, ordinary)
check("missing ordinary entry refuses", [(n, d) for n, d in original if n != ordinary], 1, "missing=")
check("missing Content_Types refuses", [(n, d) for n, d in original if n != "[Content_Types].xml"], 1, "missing [Content_Types].xml")
check("extra entry refuses", original + [("extra.bin", b"extra")], 1, "extra=")
check("duplicate entry refuses", original + [(ordinary, b"duplicate")], 1, "duplicate ZIP entries")
check("duplicate excluded signature refuses", [(n, d) for n, d in original if n != ".signature.p7s"] + [(".signature.p7s", b"one"), (".signature.p7s", b"two")], 1, "duplicate ZIP entries")
check("nested signature is not excluded", original + [("nested/.signature.p7s", b"payload")], 1, "extra=")
check("unreadable malformed ZIP refuses", [], 1, "File is not a zip file", corrupt=True)
check("missing archive refuses", [], 1, "No such file", missing=True)
check("newline entry refuses ambiguous receipt", original + [("injected\nname", b"payload")], 1, "newlines")
check("third archive mutation refuses", original, 1, "changed=", third=[(n, b"changed third archive" if n == ordinary else d) for n, d in original])
if len(sys.argv) > 2:
    # Genuine retained signed/unsigned archives; never synthesize a valid signature.
    with tempfile.TemporaryDirectory() as directory:
        import shutil
        paths = [Path(directory) / "selected.nupkg", Path(directory) / "signed.nupkg"]
        for source, target in zip(sys.argv[1:3], paths): shutil.copyfile(source, target)
        result = subprocess.run([sys.executable, str(command), *map(str, paths)], capture_output=True, text=True)
        assert result.returncode == 0, (result.stdout, result.stderr)
        passed += 1
        print("PASS genuine verified signed payload equality")
print(f"Literal package readback: {passed} controls passed")
