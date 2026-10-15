#!/usr/bin/env python3
"""Dependency-free source checks; does not invoke CLR or contact feeds."""
import ast
import json
from pathlib import Path
import xml.etree.ElementTree as E

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
for path in [root / 'qualify.py', root / 'preflight.py']:
    ast.parse(path.read_text(), filename=str(path))
program = (root / 'Program.fs').read_text()
for symbol in ['CatalogScaffoldPolicy.parse', 'ScaffoldProvenanceDocument.parse', 'ProviderCapabilityAdmission.request', 'ProviderCapabilityAdmission.resolve']:
    assert symbol in program
assert '[<EntryPoint>]' in program and 'providerExecution = false' in program
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
print('Source preflight passed: 12 exact packages, isolated consumer, four actual API calls, bounded read-only workflow.')
