"""Controls for the schema-v1 compatibility window gate: the real tree passes, and every kind of drift fails."""
import importlib.util
import json
from pathlib import Path
import re
import shutil
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('window_checker', ROOT / 'scripts/check-provider-schema-window.py')
checker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(checker)

FILES = [
    checker.DOC,
    'Directory.Build.local.props',
    'src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs',
    'src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fs',
    'src/FS.GG.SDD.Artifacts/SchemaVersion.fs',
    'src/FS.GG.SDD.Artifacts/CatalogScaffoldProvenance.fs',
    'src/FS.GG.SDD.Artifacts/SourceScaffoldProvenance.fs',
    'tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs',
    'tests/FS.GG.SDD.Artifacts.Tests/ProviderRegistryParseTests.fs',
    'tests/FS.GG.SDD.Artifacts.Tests/ScaffoldProvenanceTests.fs',
    'scripts/provider-schema-window-journey.py',
]


def copy_tree():
    temporary = tempfile.TemporaryDirectory()
    root = Path(temporary.name)
    for relative in FILES:
        target = root / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy(ROOT / relative, target)
    return temporary, root


def rewrite(root, relative, old, new, count=1):
    path = root / relative
    text = path.read_text()
    assert old in text, f'control precondition: {old!r} not in {relative}'
    path.write_text(text.replace(old, new, count))


def edit_declaration(root, mutate):
    path = root / checker.DOC
    text = path.read_text()
    match = re.search(r'(```json provider-schema-window\n)(.*?)(\n```)', text, re.S)
    window = json.loads(match.group(2))
    mutate(window)
    path.write_text(text[:match.start(2)] + json.dumps(window, indent=2) + text[match.end(2):])


class WindowGateTests(unittest.TestCase):
    def problems(self, mutate):
        temporary, root = copy_tree()
        with temporary:
            mutate(root)
            return checker.check(root)

    def test_the_real_tree_passes(self):
        self.assertEqual([], checker.check(ROOT))
        self.assertEqual([], self.problems(lambda root: None))

    def test_a_changed_supported_range_in_source_fails(self):
        problems = self.problems(lambda r: rewrite(r, 'src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs', '>=1.0.0 <3.0.0', '>=1.0.0 <4.0.0'))
        self.assertTrue(any('supportedContractRange' in p for p in problems), problems)

    def test_a_changed_declared_range_fails(self):
        problems = self.problems(lambda r: edit_declaration(r, lambda w: w['registry'].update(supportedContractRange='>=1.0.0 <4.0.0')))
        self.assertTrue(any('supportedContractRange' in p for p in problems), problems)

    def test_a_changed_major_two_minimum_fails(self):
        problems = self.problems(lambda r: rewrite(r, 'src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs', 'Fsgg.Version.compare minimum "2.1.0"', 'Fsgg.Version.compare minimum "2.2.0"'))
        self.assertTrue(any('minimum' in p for p in problems), problems)

    def test_changed_admission_semantics_fail(self):
        problems = self.problems(lambda r: rewrite(r, 'src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs', '| Some 1 -> true', '| Some 1 -> false'))
        self.assertTrue(any('isSupportedContract' in p for p in problems), problems)

    def test_a_major_bump_without_revisiting_the_window_fails(self):
        problems = self.problems(lambda r: rewrite(r, 'Directory.Build.local.props', '<Version>2.5.1</Version>', '<Version>3.0.0</Version>'))
        self.assertTrue(any('current line 3.x' in p for p in problems), problems)
        self.assertTrue(any('4.0.0' in p for p in problems), problems)

    def test_a_published_release_newer_than_current_fails(self):
        problems = self.problems(lambda r: edit_declaration(r, lambda w: w['publishedReleases'].append('2.9.0')))
        self.assertTrue(any('newer than the current' in p for p in problems), problems)

    def test_unsorted_or_non_spanning_release_lists_fail(self):
        self.assertTrue(self.problems(lambda r: edit_declaration(r, lambda w: w['publishedReleases'].reverse())))
        self.assertTrue(self.problems(lambda r: edit_declaration(r, lambda w: w.update(publishedReleases=['2.0.3', '2.5.1'], opensAtRelease='2.0.3'))))
        self.assertTrue(self.problems(lambda r: edit_declaration(r, lambda w: w.update(opensAtRelease='1.8.0'))))

    def test_a_legacy_provenance_schema_change_fails(self):
        problems = self.problems(lambda r: rewrite(r, 'src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fs', 'SchemaVersion = 1\n', 'SchemaVersion = 4\n'))
        self.assertTrue(any('legacy provenance writer' in p for p in problems), problems)
        problems = self.problems(lambda r: rewrite(r, 'src/FS.GG.SDD.Artifacts/SchemaVersion.fs', 'let supportedRange = "1"', 'let supportedRange = "2"'))
        self.assertTrue(any('legacy reader' in p for p in problems), problems)

    def test_catalog_and_source_route_schema_drift_fails(self):
        self.assertTrue(self.problems(lambda r: rewrite(r, 'src/FS.GG.SDD.Artifacts/CatalogScaffoldProvenance.fs', 'r.SchemaVersion <> 2', 'r.SchemaVersion <> 5')))
        self.assertTrue(self.problems(lambda r: rewrite(r, 'src/FS.GG.SDD.Artifacts/SourceScaffoldProvenance.fs', 'record.SchemaVersion <> 3', 'record.SchemaVersion <> 6')))

    def test_a_renamed_or_removed_pinned_test_fails(self):
        problems = self.problems(lambda r: rewrite(r, 'tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs', '``scaffold unsupported contract version blocks before invocation``', '``renamed``'))
        self.assertTrue(any('pinned test not found by name' in p for p in problems), problems)
        problems = self.problems(lambda r: (r / 'tests/FS.GG.SDD.Artifacts.Tests/ProviderRegistryParseTests.fs').unlink())
        self.assertTrue(any('pinned test file is missing' in p for p in problems), problems)

    def test_guarantee_table_and_declaration_must_agree(self):
        self.assertTrue(self.problems(lambda r: edit_declaration(r, lambda w: w['guarantees'].pop())))
        self.assertTrue(self.problems(lambda r: edit_declaration(r, lambda w: w['guarantees'].append({'id': 'G9', 'journey': True}))))

    def test_a_guarantee_must_be_pinned_to_exactly_one_kind_of_evidence(self):
        self.assertTrue(self.problems(lambda r: edit_declaration(r, lambda w: w['guarantees'][0].update(journey=True))))
        self.assertTrue(self.problems(lambda r: edit_declaration(r, lambda w: w['guarantees'][0].pop('tests'))))

    def test_a_missing_journey_driver_fails(self):
        problems = self.problems(lambda r: (r / 'scripts/provider-schema-window-journey.py').unlink())
        self.assertTrue(any('journey driver' in p for p in problems), problems)

    def test_a_missing_or_duplicated_declaration_block_fails(self):
        self.assertTrue(self.problems(lambda r: rewrite(r, checker.DOC, '```json provider-schema-window', '```json other')))
        def duplicate(root):
            path = root / checker.DOC
            text = path.read_text()
            block = re.search(r'```json provider-schema-window\n.*?\n```', text, re.S).group(0)
            path.write_text(text + '\n' + block + '\n')
        self.assertTrue(any('exactly one' in p for p in self.problems(duplicate)))

    def test_an_announcement_of_less_than_one_minor_fails(self):
        self.assertTrue(self.problems(lambda r: edit_declaration(r, lambda w: w.update(announceMinorReleasesBeforeClose=0))))

    def test_every_pinned_name_is_unique_per_file_and_non_empty(self):
        window, _ = checker.declaration(ROOT)
        pins = [(p['file'], p['name']) for g in window['guarantees'] for p in g.get('tests', [])]
        self.assertEqual(len(pins), len(set(pins)))
        self.assertTrue(all(name.strip() for _, name in pins))


if __name__ == '__main__':
    unittest.main()
