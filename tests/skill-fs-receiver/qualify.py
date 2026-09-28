#!/usr/bin/env python3
"""Qualify the public SKILL-FS-01 SDD receiver and its retained upgrade path."""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import shutil
import stat
import subprocess
import sys
import tempfile
import urllib.request
import zipfile


PUBLIC_ARCHIVES = {
    "fs.gg.coord.cli": "e68a2134242c1e59611a41f98aa5b7d26a545fd6411a0de01af610f4862cc332",
    "fs.gg.drivers": "0ab12fa50fe5f6785d901dd8f5b7cd346ba958d60e15a25e713733da80cdddef",
    "fs.gg.kit": "fb74b5ab1de83a4a69a3ebd93cc60391b355f49382f6ae2de7a91a750f874ba6",
}
FORBIDDEN = {
    "fsgg_telemetry_defaults.py",
    "native_collaboration_usage.py",
    "roadmap-telemetry.py",
    "preflight.py",
}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def run(args: list[str], *, cwd: Path | None = None, env: dict[str, str] | None = None) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(args, cwd=cwd, env=env, text=True, capture_output=True)
    if result.returncode:
        raise RuntimeError(
            f"command failed ({result.returncode}): {' '.join(args)}\nstdout:\n{result.stdout}\nstderr:\n{result.stderr}"
        )
    return result


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def download_package(root: Path, package: str, version: str) -> Path:
    root.mkdir(parents=True, exist_ok=True)
    target = root / f"{package}.{version}.nupkg"
    url = f"https://api.nuget.org/v3-flatcontainer/{package}/{version}/{package}.{version}.nupkg"
    with urllib.request.urlopen(url, timeout=60) as response, target.open("wb") as output:
        shutil.copyfileobj(response, output)
    return target


def public_package_gate(root: Path, coord_version: str) -> None:
    for package, expected in PUBLIC_ARCHIVES.items():
        archive = download_package(root, package, coord_version)
        require(sha256(archive) == expected, f"public archive digest drifted for {package} {coord_version}")
        with zipfile.ZipFile(archive) as package_zip:
            names = package_zip.namelist()
            if package == "fs.gg.drivers":
                forbidden = [name for name in names if Path(name).name in FORBIDDEN]
                require(not forbidden, f"retired helper remains in public Drivers: {forbidden}")
                skill = package_zip.read("drivers/skills/work-roadmap/SKILL.md").decode()
                require("fsgg-coord-engine skill roadmap-telemetry" in skill, "work-roadmap does not call the packaged adapter")
                require(not any(name in skill for name in FORBIDDEN), "work-roadmap retains an executable legacy reference")


def install_tool(tool_root: Path, package: str, version: str, env: dict[str, str]) -> Path:
    tool_root.mkdir(parents=True, exist_ok=True)
    run(
        [
            "dotnet",
            "tool",
            "install",
            package,
            "--version",
            version,
            "--tool-path",
            str(tool_root),
            "--add-source",
            "https://api.nuget.org/v3/index.json",
            "--ignore-failed-sources",
        ],
        env=env,
    )
    command = "fsgg-sdd" if package == "fs.gg.sdd.cli" else "fsgg-coord-engine"
    return tool_root / command


def registry_text(template_version: str) -> str:
    return f'''schemaVersion: 1
providers:
  - name: console
    contractVersion: "1.1.0"
    templateId: fs-gg-console
    source: FS.GG.Workspace.Template::{template_version}
    parameters:
      - key: productName
        required: true
'''


def scaffold(command: Path, workspace: Path, template_version: str, env: dict[str, str]) -> dict:
    (workspace / ".fsgg").mkdir(parents=True)
    (workspace / ".fsgg/providers.yml").write_text(registry_text(template_version))
    result = run(
        [str(command), "scaffold", "--root", str(workspace), "--provider", "console", "--param", "productName=SkillFs"],
        env=env,
    )
    report = json.loads(result.stdout)
    require(report["outcome"] == "succeeded", "public SDD scaffold did not succeed")
    return report


def assert_python_free(workspace: Path, coord_version: str) -> str:
    manifest = json.loads((workspace / ".config/dotnet-tools.json").read_text())
    require(manifest["tools"]["fs.gg.coord.cli"]["version"] == coord_version, "scaffolded Coord.Cli pin is not current")
    bodies: list[bytes] = []
    for root in (".agents", ".claude"):
        skill_root = workspace / root / "skills/work-roadmap"
        require(skill_root.is_dir(), f"missing {root} work-roadmap")
        bodies.append((skill_root / "SKILL.md").read_bytes())
        offenders = [path for path in skill_root.rglob("*") if path.is_file() and path.name in FORBIDDEN]
        require(not offenders, f"retired work-roadmap helpers materialized under {root}: {offenders}")
    require(bodies[0] == bodies[1], "work-roadmap differs between visible roots")
    require(not (workspace / "tools/fsgg_telemetry_defaults.py").exists(), "retired workspace helper remains")
    return hashlib.sha256(bodies[0]).hexdigest()


def guarded_retire_driver_paths(workspace: Path, *, first_half_only: bool) -> int:
    provenance_path = workspace / ".fsgg/scaffold-provenance.json"
    provenance = json.loads(provenance_path.read_text())
    remaining: list[Path] = []
    for row in provenance["driverPaths"]:
        path = workspace / row["path"]
        if not path.exists():
            continue
        require(path.is_file(), f"driver path is not a regular file: {row['path']}")
        expected = row.get("sha256")
        require(expected and sha256(path) == expected, f"refusing to remove modified driver file: {row['path']}")
        remaining.append(path)
    selected = remaining[: max(1, len(remaining) // 2)] if first_half_only and remaining else remaining
    for path in selected:
        path.unlink()
    return len(selected)


def update_owned_tool_entries(workspace: Path, sdd_version: str, coord_version: str) -> None:
    path = workspace / ".config/dotnet-tools.json"
    manifest = json.loads(path.read_text())
    tools = manifest["tools"]
    tools["fixture.co-tenant"] = {"version": "1.0.0", "commands": ["fixture"], "rollForward": False}
    for package, command, version in (
        ("fs.gg.sdd.cli", "fsgg-sdd", sdd_version),
        ("fs.gg.coord.cli", "fsgg-coord-engine", coord_version),
    ):
        entry = tools[package]
        require(entry.get("commands") == [command], f"unexpected retained owned entry for {package}")
        entry["version"] = version
    path.write_text(json.dumps(manifest, indent=2) + "\n")


def retained_upgrade(
    root: Path,
    target_sdd: Path,
    prior_sdd: Path,
    target_sdd_version: str,
    coord_version: str,
    template_version: str,
    env: dict[str, str],
    clean_digest: str,
) -> None:
    workspace = root / "retained"
    scaffold(prior_sdd, workspace, template_version, env)
    sentinel = workspace / "OWNER-NOTES.txt"
    sentinel.write_text("owner-authored; preserve exactly\n")
    expected_sentinel = sentinel.read_bytes()

    first = guarded_retire_driver_paths(workspace, first_half_only=True)
    require(first > 0, "interruption fixture removed no driver files")
    second = guarded_retire_driver_paths(workspace, first_half_only=False)
    require(second > 0, "resumed retirement found no remaining driver files")
    require(guarded_retire_driver_paths(workspace, first_half_only=False) == 0, "retirement is not idempotent")

    update_owned_tool_entries(workspace, target_sdd_version, coord_version)
    report = json.loads(run([str(target_sdd), "upgrade", "--root", str(workspace), "--yes"], env=env).stdout)
    require(report["outcome"] == "succeeded", "retained upgrade did not succeed")
    require(sentinel.read_bytes() == expected_sentinel, "retained upgrade changed an owner-authored file")
    manifest = json.loads((workspace / ".config/dotnet-tools.json").read_text())
    require("fixture.co-tenant" in manifest["tools"], "retained upgrade removed a co-tenant tool")
    require(assert_python_free(workspace, coord_version) == clean_digest, "retained and clean work-roadmap bytes differ")


def pending_state_replay(root: Path, coord: Path, env: dict[str, str]) -> None:
    fixture = root / "state-replay"
    bin_root = fixture / "bin"
    state_root = fixture / "state"
    dispatch_root = state_root / "orchestrator-dispatches"
    bin_root.mkdir(parents=True)
    dispatch_root.mkdir(parents=True)
    state_root.chmod(0o700)
    dispatch_root.chmod(0o700)
    fake = bin_root / "fake-engine"
    fake.write_text(
        "#!/usr/bin/env bash\nset -euo pipefail\n"
        "if [[ \" $* \" == *\" workspace \"* ]] && { [[ \" $* \" == *\" status \"* ]] || [[ \" $* \" == *\" binding \"* ]]; }; then\n"
        "  printf '{\"schema\":\"fsgg.telemetry.workspace-binding/1\",\"configPath\":\"%s\",\"repository\":\"FS-GG/.github\",\"producerId\":\"fixture-association\",\"bindingDigest\":\"%064d\",\"destination\":\"remote\",\"privateStateRoot\":\"%s\"}\\n' \"$FSGG_FAKE_CONFIG\" 0 \"$FSGG_FAKE_STATE\"\n"
        "elif [[ \" $* \" == *\" submit \"* ]]; then\n"
        "  input=''; while (($#)); do if [[ \"$1\" == '--input' ]]; then input=\"$2\"; break; fi; shift; done\n"
        "  base64 -w0 \"$input\" >> \"$FSGG_FAKE_LOG\"; printf '\\n' >> \"$FSGG_FAKE_LOG\"; printf 'applied\\n'\n"
        "elif [[ \" $* \" == *\" publisher-event \"* ]]; then\n"
        "  printf '{\"schema\":\"fsgg.telemetry.dashboard-event-health/1\",\"status\":\"ready\",\"reason\":null,\"observedAt\":\"2026-09-27T00:00:00Z\",\"publicRevision\":null,\"commit\":null}\\n'\n"
        "else printf '{}\\n'; fi\n"
    )
    fake.chmod(stat.S_IRUSR | stat.S_IWUSR | stat.S_IXUSR)
    config = fixture / "config.json"
    config.write_text(json.dumps({
        "schema": "fsgg.telemetry.workspace-config/1",
        "engine": "fake-engine",
        "associations": [{
            "producerId": "fixture-association",
            "repositories": ["FS-GG/.github"],
            "destination": {"credentialReference": "fixture-ref"},
        }],
        "retiredAssociations": [],
    }, separators=(",", ":")) + "\n")
    config.chmod(0o600)

    token = "1" * 32
    invocation = "2" * 32
    batch = {
        "schema": "fsgg.telemetry.ingest/1",
        "ingestId": f"{invocation}-000001",
        "sourceIdentity": "fixture-producer",
        "generation": invocation,
        "cursor": "1",
        "eventCount": 1,
        "events": [{
            "kind": "expected-dispatch",
            "identity": "expected-dispatch-" + "3" * 32,
            "itemId": "SKILL-FS-01.3",
            "revision": 0,
            "dispatchId": "3" * 32,
        }],
    }
    state = {
        "schema": "fsgg.telemetry.roadmap-dispatch-state/1",
        "token": token,
        "phase": "begin-pending",
        "sequence": 1,
        "featureId": "SKILL-FS-01",
        "itemId": "SKILL-FS-01.3",
        "originalItemId": "SKILL-FS-01.3",
        "originalAssignmentDigest": None,
        "attemptId": "fixture-attempt",
        "parentAttemptId": None,
        "producerStream": "fixture-producer",
        "model": "fixture-model",
        "effort": "medium",
        "relation": "root",
        "parentDispatchId": None,
        "parentInvocationId": None,
        "lateAfterSeconds": 60,
        "invocationId": invocation,
        "associationProducer": "fixture-association",
        "associationDigest": "0" * 64,
        "pendingPublication": {"operation": "begin", "nextPhase": "expected", "batch": batch},
    }
    state_path = dispatch_root / f"{token}.json"
    state_path.write_text(json.dumps(state, separators=(",", ":")) + "\n")
    state_path.chmod(0o600)

    replay_env = dict(env)
    replay_env.update({
        "PATH": str(bin_root) + os.pathsep + env["PATH"],
        "FSGG_TELEMETRY_REPOSITORY": "FS-GG/.github",
        "FSGG_TELEMETRY_CREDENTIAL_FIXTURE_REF": "synthetic-loaded",
        "FSGG_FAKE_CONFIG": str(config),
        "FSGG_FAKE_STATE": str(state_root),
        "FSGG_FAKE_LOG": str(fixture / "published.log"),
    })
    result = run([
        str(coord), "skill", "roadmap-telemetry", "--config", str(config), "begin",
        "--feature", "SKILL-FS-01", "--item", "SKILL-FS-01.3", "--attempt", "fixture-attempt",
        "--model", "fixture-model", "--effort", "medium", "--producer", "fixture-producer",
        "--relation", "root", "--late-after-seconds", "60",
    ], env=replay_env)
    require(json.loads(result.stdout)["token"] == token, "retained state did not preserve its token")
    published = base64.b64decode((fixture / "published.log").read_text().strip())
    expected = (json.dumps(batch, separators=(",", ":")) + "\n").encode()
    require(published == expected, "retained pending state did not replay its exact batch bytes")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--sdd-version", default="2.0.3")
    parser.add_argument("--prior-sdd-version", default="2.0.2")
    parser.add_argument("--coord-version", default="0.94.0")
    parser.add_argument("--template-version", default="0.15.0")
    parser.add_argument("--target-sdd-command", type=Path)
    parser.add_argument("--static-only", action="store_true")
    parser.add_argument("--keep", action="store_true")
    args = parser.parse_args()

    root = Path(tempfile.mkdtemp(prefix="skill-fs-sdd-receiver-"))
    env = dict(os.environ)
    env.update({
        "DOTNET_CLI_HOME": str(root / "dotnet-home"),
        "NUGET_PACKAGES": str(root / "packages"),
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        "DOTNET_NOLOGO": "1",
    })
    try:
        public_package_gate(root / "packages-public", args.coord_version)
        if args.static_only:
            print("SKILL-FS receiver static public gate: PASS")
            return 0

        target_sdd = (
            args.target_sdd_command.resolve()
            if args.target_sdd_command
            else install_tool(root / "target-sdd", "fs.gg.sdd.cli", args.sdd_version, env)
        )
        require(target_sdd.is_file(), f"target SDD command does not exist: {target_sdd}")
        prior_sdd = install_tool(root / "prior-sdd", "fs.gg.sdd.cli", args.prior_sdd_version, env)
        coord = install_tool(root / "coord", "fs.gg.coord.cli", args.coord_version, env)

        clean = root / "clean"
        scaffold(target_sdd, clean, args.template_version, env)
        clean_digest = assert_python_free(clean, args.coord_version)
        discovery_result = subprocess.run(
            [str(coord), "skill", "telemetry-config", "discover"],
            env=env,
            text=True,
            capture_output=True,
        )
        require(discovery_result.returncode == 2, "not-configured discovery exit code drifted")
        discovery = json.loads(discovery_result.stdout)
        require(discovery == {"schema": "fsgg.telemetry.config-discovery/1", "status": "not-configured"}, "installed discovery bytes drifted")

        retained_upgrade(
            root,
            target_sdd,
            prior_sdd,
            args.sdd_version,
            args.coord_version,
            args.template_version,
            env,
            clean_digest,
        )
        pending_state_replay(root, coord, env)
        source = "local candidate" if args.target_sdd_command else "public package"
        print(f"SKILL-FS SDD clean + retained receiver qualification ({source}): PASS")
        return 0
    finally:
        if args.keep:
            print(f"retained qualification root: {root}", file=sys.stderr)
        else:
            shutil.rmtree(root, ignore_errors=True)


if __name__ == "__main__":
    raise SystemExit(main())
