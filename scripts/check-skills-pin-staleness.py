#!/usr/bin/env python3
"""Release-preparation check: skills-package pins must not lag their published feed.

FS.GG.SDD embeds the bytes of FS.GG.Drivers and the FS.GG.*.Skills owner packages at build time
(ADR-0054 / ADR-0063), so a pin in Directory.Packages.local.props that falls behind silently
withholds new and changed skills from every workspace the CLI scaffolds or upgrades. FS.GG.SDD#1156
found FS.GG.Drivers 16 versions behind, with the `atlas` skill therefore absent from every
workspace.

The target is a bound on how many stable published versions the pin may trail: at most
`--max-behind` (default 5). This is a RELEASE-PREPARATION check, run by hand or by the release
route, not a pull-request gate: the feed moves independently of a PR, so a PR gate would turn an
unrelated upstream publication into a red check on unrelated work.

Usage:
  scripts/check-skills-pin-staleness.py [--props FILE] [--max-behind N] [--index PACKAGE=FILE ...]

`--index` substitutes a local NuGet flat-container `index.json` for the network (used by tests).
Exit 0 when every checked pin is within bound, 1 when any is not, 2 on usage or read errors.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import urllib.request
import xml.etree.ElementTree as ET

# FS.GG.Drivers plus every FS.GG.<Owner>.Skills owner package. Names a family, not a list of
# owners, so a new owner package is checked the day it is pinned.
CHECKED = re.compile(r"^FS\.GG\.(Drivers|[A-Za-z0-9.]+\.Skills)$")
STABLE = re.compile(r"^\d+(\.\d+){1,3}$")
FEED = "https://api.nuget.org/v3-flatcontainer/{id}/index.json"


def key(version: str) -> tuple[int, ...]:
    return tuple(int(part) for part in version.split("."))


def pins(props_path: str) -> dict[str, str]:
    found: dict[str, str] = {}
    for item in ET.parse(props_path).getroot().iter("PackageVersion"):
        package = item.get("Include") or ""
        if CHECKED.match(package):
            found[package] = (item.get("Version") or "").strip("[]() ")
    return found


def published(package: str, overrides: dict[str, str]) -> list[str]:
    if package in overrides:
        with open(overrides[package], encoding="utf-8") as handle:
            body = json.load(handle)
    else:
        url = FEED.format(id=package.lower())
        with urllib.request.urlopen(url, timeout=30) as response:  # noqa: S310 - fixed https feed
            body = json.load(response)
    return sorted((v for v in body["versions"] if STABLE.match(v)), key=key)


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--props", default="Directory.Packages.local.props")
    parser.add_argument("--max-behind", type=int, default=5)
    parser.add_argument("--index", action="append", default=[], metavar="PACKAGE=FILE")
    args = parser.parse_args(argv)
    if args.max_behind < 0:
        print("--max-behind must be >= 0", file=sys.stderr)
        return 2
    overrides = dict(item.split("=", 1) for item in args.index)

    try:
        checked = pins(args.props)
    except (OSError, ET.ParseError) as error:
        print(f"cannot read {args.props}: {error}", file=sys.stderr)
        return 2
    if not checked:
        print(f"no FS.GG.Drivers or FS.GG.*.Skills pin found in {args.props}", file=sys.stderr)
        return 2

    failed = False
    for package, pinned in sorted(checked.items()):
        try:
            versions = published(package, overrides)
        except (OSError, ValueError, KeyError) as error:
            print(f"{package}: cannot read the published versions: {error}", file=sys.stderr)
            return 2
        if not STABLE.match(pinned) or pinned not in versions:
            print(f"{package}: pin {pinned!r} is not a published stable version", file=sys.stderr)
            failed = True
            continue
        behind = len(versions) - 1 - versions.index(pinned)
        verdict = "ok" if behind <= args.max_behind else "STALE"
        print(f"{package}: pinned {pinned}, latest {versions[-1]}, {behind} behind (max {args.max_behind}) {verdict}")
        failed = failed or behind > args.max_behind
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
