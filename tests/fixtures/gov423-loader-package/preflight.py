#!/usr/bin/env python3
"""Dependency-free source checks; does not invoke CLR or contact feeds."""
import ast
import base64
import hashlib
import json
import re
from pathlib import Path
import xml.etree.ElementTree as E
from package_checks import check_package_folders, check_package_hashes

root = Path(__file__).resolve().parent
expected = json.loads((root / 'expected-archives.json').read_text())
versions = json.loads((root / 'expected-closure.json').read_text())
assert len(expected) == 12 and {n: v['version'] for n, v in expected.items()} == versions
project = E.parse(root / 'Consumer.fsproj').getroot()
refs = {e.get('Include'): e.get('Version') for e in project.iter('PackageReference')}
assert refs == {n: '[' + v + ']' for n, v in versions.items()}
def require_package_only(candidate):
    assert not list(candidate.iter('ProjectReference')), 'ProjectReference is forbidden'

require_package_only(project)
assert len(list(project.iter('Compile'))) == 1
assert project.find('.//DisableImplicitLibraryPacksFolder').text == 'true'
assert project.find('.//DisableImplicitFSharpCoreReference').text == 'true'
for path in [root / 'qualify.py', root / 'preflight.py', root / 'package_checks.py']:
    ast.parse(path.read_text(), filename=str(path))
program = (root / 'Program.fs').read_text()
for symbol in ['CatalogScaffoldPolicy.parse', 'ScaffoldProvenanceDocument.parse', 'ProviderCapabilityAdmission.request', 'ProviderCapabilityAdmission.resolve']:
    assert symbol in program
assert '[<EntryPoint>]' in program and 'providerExecution = false' in program
# Direct indexing of a method-call result caused FS0597 in the retained run.
assert not re.search(r'\.AsArray\(\)\s*\[', program), 'bind AsArray() before indexing its result'
# The receiver also needs a binding: indexedNode.AsArray() failed in the third run.
indexed_method = r'\]\s*\.[A-Za-z_][A-Za-z_0-9]*\s*\('
assert re.search(indexed_method, 'n["observation"]["tools"].AsArray()')
assert not re.search(indexed_method, program), 'bind an indexed receiver before calling its method'


def require_canonical_fixture_order(text):
    parameters = re.search(r'Parameters = \[([^\]]+)\]', text).group(1)
    keys = re.findall(r'parameter "([^"]+)"', parameters)
    assert keys == ['code', 'package', 'raw'], 'fixture parameters must use serializer key order'
    produced = re.search(r'ProducedPaths =\s*ownership\.ProducedPaths(.*?)\n\s*}', text, re.S).group(1)
    assert 'List.sortWith (fun a b -> StringComparer.Ordinal.Compare(a.Path, b.Path))' in produced, 'fixture produced paths must use serializer ordinal path order'


require_canonical_fixture_order(program)
for mutation in [
    program.replace('parameter "code"; parameter "package"; parameter "raw"', 'parameter "raw"; parameter "package"; parameter "code"'),
    program.replace('|> List.sortWith (fun a b -> StringComparer.Ordinal.Compare(a.Path, b.Path))', ''),
]:
    try:
        require_canonical_fixture_order(mutation)
    except AssertionError:
        pass
    else:
        raise AssertionError('known-bad fixture ordering escaped preflight')
assert 'RequiredCapabilityIds = [ "build:build" ]' in program
workflow = root.parents[2] / '.github/workflows/release.yml'
text = workflow.read_text()
assert 'workflow_dispatch:' in text and 'timeout-minutes: 15' in text
assert 'actions: read' in text and 'contents: read' in text
assert 'cancel-in-progress: false' in text and 'if: always()' in text
for forbidden in ['pull_request:', '  push:', '  release:', 'secrets.', 'dotnet pack', 'nuget push', 'contents: write']:
    assert forbidden not in text, forbidden
# Independent bad fixture: a ProjectReference must fail the same ownership predicate.
bad = E.fromstring('<Project><ItemGroup><ProjectReference Include="sibling.fsproj" /></ItemGroup></Project>')
try:
    require_package_only(bad)
except AssertionError:
    pass
else:
    raise AssertionError('known-bad source reference escaped preflight')


def must_refuse(action):
    try:
        action()
    except RuntimeError:
        return
    raise AssertionError('known-bad package identity escaped preflight')


# Both observed NuGet spellings identify one expected cache; other roots still refuse.
cache = root / 'synthetic-cache'
for spelling in [str(cache), str(cache) + '/']:
    check_package_folders({spelling: {}}, cache)
must_refuse(lambda: check_package_folders({str(root / 'wrong-cache'): {}}, cache))
must_refuse(lambda: check_package_folders({str(cache): {}, str(root / 'other'): {}}, cache))

# Synthetic signed-package identities deliberately differ. The normalized hash comes
# from metadata, while the raw sidecar must still bind the exact archive bytes.
raw = b'synthetic signed archive bytes'
raw_sha512 = base64.b64encode(hashlib.sha512(raw).digest()).decode()
normalized = base64.b64encode(hashlib.sha512(b'synthetic normalized payload').digest()).decode()
assert raw_sha512 != normalized
metadata = {'contentHash': normalized}
entry = {'resolved': '1.0.0', 'contentHash': normalized}
assert check_package_hashes(raw, raw_sha512, metadata, entry, '1.0.0') == {
    'nugetContentHash': normalized, 'rawSha512': raw_sha512}
must_refuse(lambda: check_package_hashes(raw, raw_sha512, {'contentHash': raw_sha512}, entry, '1.0.0'))
must_refuse(lambda: check_package_hashes(raw, normalized, metadata, entry, '1.0.0'))
print('Source preflight passed: 12 exact packages, isolated consumer, four actual API calls, bounded read-only workflow.')
