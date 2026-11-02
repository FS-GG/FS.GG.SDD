"""Offline controls for scripts/check-skills-pin-staleness.py (FS.GG.SDD#1156): the staleness
bound passes inside the window, fails outside it, ignores prereleases, and refuses unreadable input."""
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.dont_write_bytecode = True
SCRIPT = Path(__file__).parents[1] / "check-skills-pin-staleness.py"


def props(directory: Path, drivers: str) -> Path:
    path = directory / "Directory.Packages.local.props"
    path.write_text(
        '<Project><ItemGroup>'
        f'<PackageVersion Include="FS.GG.Drivers" Version="{drivers}" />'
        '<PackageVersion Include="FS.GG.Game.Skills" Version="[0.9.0]" />'
        '<PackageVersion Include="YamlDotNet" Version="1.0.0" />'
        '</ItemGroup></Project>',
        encoding="utf-8",
    )
    return path


def index(directory: Path, name: str, versions: list[str]) -> str:
    path = directory / f"{name}.json"
    path.write_text(json.dumps({"versions": versions}), encoding="utf-8")
    return f"{name}={path}"


def run(*args: str):
    return subprocess.run([sys.executable, "-I", "-B", str(SCRIPT), *args], capture_output=True, text=True)


class StalenessTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.dir = Path(self.tmp.name)
        self.games = index(self.dir, "FS.GG.Game.Skills", ["0.8.0", "0.9.0"])

    def tearDown(self):
        self.tmp.cleanup()

    def drivers(self, versions: list[str]) -> str:
        return index(self.dir, "FS.GG.Drivers", versions)

    def test_within_bound_passes(self):
        feed = [f"0.{n}.0" for n in range(90, 100)]
        result = run("--props", str(props(self.dir, "0.95.0")), "--index", self.drivers(feed), "--index", self.games)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("4 behind", result.stdout)

    def test_beyond_bound_fails(self):
        feed = [f"0.{n}.0" for n in range(90, 100)]
        result = run("--props", str(props(self.dir, "0.90.0")), "--index", self.drivers(feed), "--index", self.games)
        self.assertEqual(result.returncode, 1)
        self.assertIn("STALE", result.stdout)

    def test_bound_is_configurable(self):
        feed = [f"0.{n}.0" for n in range(90, 100)]
        args = ["--props", str(props(self.dir, "0.90.0")), "--index", self.drivers(feed), "--index", self.games]
        self.assertEqual(run(*args, "--max-behind", "9").returncode, 0)
        self.assertEqual(run(*args, "--max-behind", "8").returncode, 1)

    def test_prereleases_are_not_counted(self):
        feed = ["0.1.0", "0.2.0-preview.1", "0.2.0-preview.2", "0.2.0-preview.3", "0.3.0-rc.1", "0.4.0-rc.1", "0.5.0-rc.1", "0.2.0"]
        result = run("--props", str(props(self.dir, "0.1.0")), "--index", self.drivers(feed), "--index", self.games)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("1 behind", result.stdout)

    def test_unpublished_pin_fails(self):
        result = run("--props", str(props(self.dir, "9.9.9")), "--index", self.drivers(["0.1.0"]), "--index", self.games)
        self.assertEqual(result.returncode, 1)
        self.assertIn("not a published stable version", result.stderr)

    def test_missing_props_is_a_usage_error(self):
        self.assertEqual(run("--props", str(self.dir / "absent.props")).returncode, 2)


if __name__ == "__main__":
    unittest.main()
