#!/usr/bin/env python3
"""Check that the declared schema-v1 provider compatibility window matches the source and its tests.

The declaration is docs/reference/provider-schema-compatibility.md (its `provider-schema-window` JSON
block). This gate fails when the declaration, the source constants it names or the unit tests it pins drift:

  * the supported contract range equals `supportedContractRange` in HandlersScaffold.fs, and the major-2
    minimum equals the literal that code compares against;
  * the open-through line and the earliest close derive from the current SDD major line
    (Directory.Build.local.props), so a major bump without revisiting the window fails;
  * the legacy provenance schema equals the one the writer emits and the catalog/source routes' schemas equal
    theirs, and the legacy schema range (`SchemaVersion.supportedRange`) still admits the legacy schema only;
  * the published-release list is ascending, begins at the release the window opens with and does not exceed
    the current version;
  * every guarantee in the page table is declared, every unit test it pins exists by name in the named file,
    and a guarantee is either pinned to tests or to the installed journey.

  check-provider-schema-window.py [--root DIR]
"""
import argparse
import json
from pathlib import Path
import re
import sys

sys.dont_write_bytecode = True
DOC = 'docs/reference/provider-schema-compatibility.md'
SEMVER = re.compile(r'^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$')
SCHEMA = 'fsgg.provider-schema-window/v1'


def parse_version(text):
    match = SEMVER.match(text)
    if not match:
        raise ValueError('not a stable semantic version: ' + repr(text))
    return tuple(int(part) for part in match.groups())


def declaration(root):
    text = (root / DOC).read_text()
    blocks = re.findall(r'```json provider-schema-window\n(.*?)\n```', text, re.S)
    if len(blocks) != 1:
        raise ValueError('the declaration page needs exactly one `json provider-schema-window` block')
    return json.loads(blocks[0]), text


def check(root):
    root = Path(root)
    problems = []
    try:
        window, page = declaration(root)
    except (OSError, ValueError) as error:
        return [f'declaration unreadable: {error}']

    def expect(condition, message):
        if not condition:
            problems.append(message)

    expect(window.get('schema') == SCHEMA, 'unsupported declaration schema')
    scaffold = (root / 'src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs').read_text()
    found = re.search(r'let supportedContractRange = "([^"]+)"', scaffold)
    registry = window.get('registry', {})
    expect(found is not None and found.group(1) == registry.get('supportedContractRange'),
           f'supportedContractRange in source ({found.group(1) if found else None}) differs from the declaration ({registry.get("supportedContractRange")})')
    minimum = re.search(r'Fsgg\.Version\.compare minimum "([^"]+)"', scaffold)
    expect(minimum is not None and minimum.group(1) == registry.get('major2MinimumCliVersion'),
           'the major-2 minimum CLI version in source differs from the declaration')
    expect('| Some 1 -> true' in scaffold and '| Some 2 -> stableVersion version |> Option.isSome' in scaffold,
           'isSupportedContract no longer accepts exactly major 1 and a stable major 2')
    range_match = re.fullmatch(r'>=(\d+\.\d+\.\d+) <(\d+\.\d+\.\d+)', str(registry.get('supportedContractRange')))
    expect(range_match is not None, 'the supported contract range is not ">=X <Y"')

    props = (root / 'Directory.Build.local.props').read_text()
    version = re.search(r'<Version>([^<]+)</Version>', props)
    try:
        current = parse_version(version.group(1)) if version else None
        expect(current is not None, 'the current SDD version was not found')
    except ValueError as error:
        current = None
        problems.append(str(error))
    if current is not None:
        expect(window.get('openThroughLine') == f'{current[0]}.x', f'the window must stay open through the current line {current[0]}.x')
        expect(window.get('closesNotBefore') == f'{current[0] + 1}.0.0', f'the earliest close must be {current[0] + 1}.0.0 (the next major)')
        releases = window.get('publishedReleases', [])
        try:
            parsed = [parse_version(r) for r in releases]
            expect(parsed == sorted(set(parsed)) and parsed, 'publishedReleases must be ascending and distinct')
            expect(parsed and releases[0] == window.get('opensAtRelease'), 'publishedReleases must begin at the release the window opens with')
            expect(parsed and parsed[-1] <= current, 'a listed release is newer than the current version')
            expect(any(p[0] == current[0] for p in parsed), 'publishedReleases must include a release of the current major line')
            expect(len({p[0] for p in parsed}) >= 2, 'publishedReleases must span more than one major line')
        except ValueError as error:
            problems.append(str(error))
    expect(window.get('announceMinorReleasesBeforeClose', 0) >= 1, 'a close must be announced at least one MINOR release earlier')

    legacy = window.get('provenance', {}).get('legacyRouteSchema')
    writer = (root / 'src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fs').read_text()
    written = re.search(r'SchemaVersion = (\d+)\n', writer)
    expect(written is not None and int(written.group(1)) == legacy, 'the legacy provenance writer schema differs from the declaration')
    schema_source = (root / 'src/FS.GG.SDD.Artifacts/SchemaVersion.fs').read_text()
    expect(re.search(r'let supportedRange = "(\d+)"', schema_source) is not None and re.search(r'let supportedRange = "(\d+)"', schema_source).group(1) == str(legacy),
           'the legacy reader schema range differs from the declaration')
    catalog = (root / 'src/FS.GG.SDD.Artifacts/CatalogScaffoldProvenance.fs').read_text()
    source = (root / 'src/FS.GG.SDD.Artifacts/SourceScaffoldProvenance.fs').read_text()
    expect('SchemaVersion = 2' in catalog and 'r.SchemaVersion <> 2' in catalog, 'the catalog route no longer writes and reads provenance schema 2')
    expect('SchemaVersion = 3' in source and 'record.SchemaVersion <> 3' in source, 'the provider-source route no longer writes and reads provenance schema 3')
    expect(window.get('provenance', {}).get('catalogRouteSchemas') == [2, 3], 'catalogRouteSchemas must be [2, 3]')

    declared_ids = set()
    for guarantee in window.get('guarantees', []):
        gid = guarantee.get('id')
        declared_ids.add(gid)
        tests, journey = guarantee.get('tests'), guarantee.get('journey')
        expect(bool(tests) != bool(journey), f'{gid}: a guarantee is pinned to unit tests or to the installed journey, not both or neither')
        for pin in tests or []:
            path = root / str(pin.get('file', ''))
            expect(path.is_file(), f'{gid}: pinned test file is missing: {pin.get("file")}')
            if path.is_file():
                expect(f'``{pin.get("name")}``' in path.read_text(), f'{gid}: pinned test not found by name: {pin.get("name")}')
    table = set(re.findall(r'^\| (G\d+) \|', page, re.M))
    expect(table == declared_ids and declared_ids, f'guarantee ids differ between the page table {sorted(table)} and the declaration {sorted(declared_ids)}')
    journey = root / 'scripts/provider-schema-window-journey.py'
    expect(any(g.get('journey') for g in window.get('guarantees', [])) <= journey.is_file(), 'the installed journey driver the declaration relies on is missing')
    return problems


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--root', default=str(Path(__file__).resolve().parents[1]))
    args = parser.parse_args()
    problems = check(args.root)
    print(json.dumps({'valid': not problems, 'problems': problems}, indent=2))
    return 0 if not problems else 1


if __name__ == '__main__':
    raise SystemExit(main())
