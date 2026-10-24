#!/usr/bin/env python3
"""Guards the committed third-party provider-source fixture used by the installed journeys."""
import hashlib
import importlib.util
import json
import tempfile
import unittest
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('journey', ROOT / 'scripts/provider-source-journey.py')
journey = importlib.util.module_from_spec(spec)
spec.loader.exec_module(journey)
FIXTURE = ROOT / 'tests/fixtures/third-party-provider-source'


class FixtureTests(unittest.TestCase):
    def test_committed_envelope_is_the_deterministic_build(self):
        with tempfile.TemporaryDirectory() as out:
            digests = journey.write_source(Path(out) / 'source', Path(out))
            built = (Path(out) / 'envelope.zip').read_bytes()
        self.assertEqual(built, (FIXTURE / 'envelope.zip').read_bytes())
        self.assertEqual(digests['envelopeSha256'], 'sha256:' + hashlib.sha256(built).hexdigest())

    def test_envelope_population_is_exactly_the_manifest_and_its_members(self):
        with zipfile.ZipFile(FIXTURE / 'envelope.zip') as archive:
            names = [info.filename for info in archive.infolist()]
            manifest = json.loads(archive.read('source.json'))
            for info in archive.infolist():
                self.assertEqual(0o100644, info.external_attr >> 16)
                self.assertEqual(zipfile.ZIP_STORED, info.compress_type)
            members = {m['path']: m for m in manifest['members']}
            for name, member in members.items():
                data = archive.read(name)
                self.assertEqual(member['bytes'], len(data))
                self.assertEqual(member['sha256'], 'sha256:' + hashlib.sha256(data).hexdigest())
        self.assertEqual(sorted(names), sorted(['source.json', *members]))

    def test_policy_and_template_identity_agree(self):
        policy = json.loads((FIXTURE / 'policy.json').read_text())
        nuspec = (FIXTURE / 'template.nuspec').read_text()
        for archive in policy['archives']:
            self.assertIn('<id>' + archive['packageId'] + '</id>', nuspec)
            self.assertIn('<version>' + archive['packageVersion'] + '</version>', nuspec)
            self.assertEqual(archive['templateSource'], journey.TEMPLATE_SOURCE)
        catalog = json.loads((FIXTURE / 'catalog.json').read_text())
        self.assertEqual([journey.PROVIDER], [p['id'] for p in catalog['providers']])
        self.assertEqual(journey.TEMPLATE_SOURCE, catalog['providers'][0]['templateSource'])


if __name__ == '__main__':
    unittest.main()
