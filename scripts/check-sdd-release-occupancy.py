#!/usr/bin/env python3
"""Read-only both-feed preflight; occupied versions may resume only exact payloads."""
import argparse
import base64
import io
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import subprocess
import tempfile
import urllib.error
import urllib.request
import urllib.parse
import zipfile
import xml.etree.ElementTree as ET


def payload(data):
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)):
            raise ValueError("duplicate package entries")
        return {name: archive.read(name) for name in names if name != ".signature.p7s"}


CONTRACTS_VERSION = "7.6.0"
CONTRACTS_SOURCE = "cf2f046a10497a336d6243c9a314c3f91fb15771"
CONTRACTS_ARCHIVE_SHA256 = "b1df3ebd6251f5b18aaece4dd0c5449a7825dc7056f925cf2516febc35f9dfc5"
CONTRACTS_DLL_SHA256 = "91f484d28416c5d860a375a91ed70cdda1d3b6d85d504c15ea21e08a9af727ee"


def contracts_metadata(data, version):
    if version != CONTRACTS_VERSION or hashlib.sha256(data).hexdigest() != CONTRACTS_ARCHIVE_SHA256:
        raise ValueError("reused Contracts is not the selected published archive")
    entries = payload(data)
    specifications = [raw for name, raw in entries.items() if name.endswith('.nuspec')]
    if len(specifications) != 1:
        raise ValueError("reused Contracts package metadata is ambiguous")
    metadata = ET.fromstring(specifications[0]).find('{*}metadata')
    repository = metadata.find('{*}repository') if metadata is not None else None
    if (metadata is None or metadata.findtext('{*}id') != 'FS.GG.Contracts'
            or metadata.findtext('{*}version') != version or repository is None
            or repository.get('commit') != CONTRACTS_SOURCE):
        raise ValueError("reused Contracts published source/version mismatch")
    dll = entries.get('lib/net10.0/FS.GG.Contracts.dll', b'')
    if hashlib.sha256(dll).hexdigest() != CONTRACTS_DLL_SHA256:
        raise ValueError("reused Contracts DLL identity mismatch")
    normalized = json.dumps([[name, hashlib.sha256(raw).hexdigest()] for name, raw in sorted(entries.items())], separators=(',', ':')).encode()
    return dict(schema='fsgg.sdd.reused-dependency/v1', packageId='FS.GG.Contracts', version=version,
                sourceHead=CONTRACTS_SOURCE, archiveSha256=CONTRACTS_ARCHIVE_SHA256,
                payloadSha256=hashlib.sha256(normalized).hexdigest(), dllSha256=CONTRACTS_DLL_SHA256)


def verified_contracts_payloads(public, org):
    # Exact selected public bytes need no exclusion. A changed envelope only
    # permits excluding signatures after the SDK has verified every signed input.
    if public == org:
        return
    for data in [public, org]:
        with zipfile.ZipFile(io.BytesIO(data)) as archive:
            signed = '.signature.p7s' in archive.namelist()
        if signed:
            with tempfile.TemporaryDirectory(prefix='sdd-contracts-signature-') as directory:
                path = Path(directory) / 'FS.GG.Contracts.7.6.0.nupkg'
                path.write_bytes(data)
                try:
                    result = subprocess.run(['dotnet', 'nuget', 'verify', str(path), '--all'],
                        stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=30, check=False)
                except (OSError, subprocess.TimeoutExpired) as error:
                    raise ValueError('reused Contracts signature verification unavailable or timed out') from error
                if result.returncode != 0:
                    raise ValueError('reused Contracts signature verification refused')
    if payload(public) != payload(org):
        raise ValueError('reused Contracts normalized payload differs between feeds')


def preflight(version, ids, github_index, github_download, public_download, token, actor, candidate=None, contracts_version=None, github_api="https://api.github.com"):
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version):
        raise ValueError("invalid stable release version")
    if not token or not actor:
        raise ValueError("authenticated publisher feed read is required; absence is not occupancy evidence")
    if contracts_version is not None and not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", contracts_version):
        raise ValueError("invalid independent Contracts version")
    versions = {package_id: version for package_id in ids}
    if contracts_version != CONTRACTS_VERSION:
        raise ValueError("selected published Contracts dependency version is required")
    if "FS.GG.Contracts" in ids:
        raise ValueError("reused Contracts cannot be a release member")
    auth = base64.b64encode(f"{actor}:{token}".encode()).decode()

    class FeedRedirect(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, request, response, code, message, headers, new_url):
            redirected = super().redirect_request(request, response, code, message, headers, new_url)
            if redirected is not None:
                old = urllib.parse.urlparse(request.full_url)
                new = urllib.parse.urlparse(new_url)
                if (old.scheme, old.netloc) != (new.scheme, new.netloc):
                    redirected.remove_header("Authorization")
            return redirected
    opener = urllib.request.build_opener(FeedRedirect())

    def read(url, authenticated=False, api=False):
        request = urllib.request.Request(url)
        if api:
            request.add_header("Authorization", "Bearer " + token)
            request.add_header("Accept", "application/vnd.github+json")
        elif authenticated:
            request.add_header("Authorization", "Basic " + auth)
        try:
            with opener.open(request, timeout=30) as response:
                raw = response.read(4 * 1024 * 1024 + 1) if api else response.read()
                if api and len(raw) > 4 * 1024 * 1024:
                    raise ValueError("scoped version response exceeds bounded population")
                return raw
        except urllib.error.HTTPError as error:
            if error.code == 404:
                return None
            raise ValueError(f"feed access failed with HTTP {error.code}; occupancy unproved") from error

    # GitHub may conceal authorization failures as 404. Require an authenticated
    # index first; 403/404 here never means an unused package version.
    if read(github_index, True) is None:
        raise ValueError("authenticated GitHub feed index unavailable; occupancy unproved")
    # An index response alone does not establish access to the repository's
    # package namespace. Probe actual known published members with this token.
    for known in ['fs.gg.sdd.artifacts', 'fs.gg.sdd.knowledge']:
        suffix = f'/{known}/2.2.0/{known}.2.2.0.nupkg'
        baseline = read(github_download.rstrip('/') + suffix, True)
        if baseline is None:
            raise ValueError(f"known GitHub baseline {known} 2.2.0 unreadable; occupancy unproved")
        observed = payload(baseline)
        specifications = [data for name, data in observed.items() if name.endswith('.nuspec')]
        if len(specifications) != 1:
            raise ValueError(f"known baseline {known} has no unique package identity")
        metadata = ET.fromstring(specifications[0])
        if metadata.findtext('.//{*}id', '').lower() != known or metadata.findtext('.//{*}version') != '2.2.0':
            raise ValueError(f"known baseline {known} identity/version mismatch")
        public_baseline = read(public_download.rstrip('/') + suffix)
        if public_baseline is None or payload(public_baseline) != observed:
            raise ValueError(f"known baseline {known} differs from its public normalized payload")
    suffix = f'/fs.gg.contracts/{contracts_version}/fs.gg.contracts.{contracts_version}.nupkg'
    public_contracts = read(public_download.rstrip('/') + suffix)
    org_contracts = read(github_download.rstrip('/') + suffix, True)
    if public_contracts is None or org_contracts is None:
        raise ValueError("reused Contracts must already be available on both feeds")
    contracts_metadata(public_contracts, contracts_version)
    verified_contracts_payloads(public_contracts, org_contracts)
    if candidate is not None:
        retained = candidate / 'dependencies' / f'FS.GG.Contracts.{contracts_version}.nupkg'
        if retained.read_bytes() != public_contracts:
            raise ValueError("reused Contracts differs from the retained selected archive")
    print(f'FS.GG.Contracts {contracts_version}: qualified published dependency; no push')

    def prove_org_absence(package_id, selected_version):
        # A download 404 may conceal denied access. Require successful scoped enumeration.
        for page in range(1, 11):
            name = urllib.parse.quote(package_id.lower(), safe="")
            data = read(f"{github_api}/orgs/FS-GG/packages/nuget/{name}/versions?per_page=100&page={page}", api=True)
            if data is None:
                raise ValueError(f"{package_id}: scoped GitHub versions unreadable; occupancy unknown")
            if len(data) > 4 * 1024 * 1024:
                raise ValueError("scoped GitHub version response exceeds bounded population")
            entries = json.loads(data)
            if not isinstance(entries, list) or len(entries) > 100 or any(not isinstance(e, dict) or not isinstance(e.get("name"), str) for e in entries):
                raise ValueError("malformed scoped GitHub version population")
            if any(e["name"] == selected_version for e in entries):
                raise ValueError(f"{package_id} {selected_version} is listed but archive unreadable")
            if len(entries) < 100:
                return
        raise ValueError("scoped GitHub version population incomplete; occupancy unknown")

    for package_id in ids:
        selected_version = versions[package_id]
        lower = package_id.lower()
        suffix = f"/{lower}/{selected_version}/{lower}.{selected_version}.nupkg"
        expected = payload((candidate / f"{package_id}.{selected_version}.nupkg").read_bytes()) if candidate else None
        for feed, base, authenticated in [
            ("github", github_download, True), ("nuget", public_download, False)
        ]:
            data = read(base.rstrip("/") + suffix, authenticated)
            if data is not None:
                if expected is None or payload(data) != expected:
                    raise ValueError(f"{package_id} {selected_version} occupied on {feed}; refusing substitution")
                print(f"{package_id} {selected_version} {feed}: exact retained payload already present")
            else:
                if feed == "github":
                    prove_org_absence(package_id, selected_version)
                print(f"{package_id} {selected_version} {feed}: absent")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("version")
    parser.add_argument("--candidate", type=Path)
    parser.add_argument("--contracts-version", required=True)
    args = parser.parse_args()
    ids = Path(__file__).with_name("sdd-release-packages.txt").read_text().splitlines()
    preflight(args.version, ids, "https://nuget.pkg.github.com/FS-GG/index.json",
              "https://nuget.pkg.github.com/FS-GG/download", "https://api.nuget.org/v3-flatcontainer",
              os.environ.get("GH_TOKEN", ""), os.environ.get("GITHUB_ACTOR", ""), args.candidate, args.contracts_version)


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"release occupancy refused: {error}", file=sys.stderr)
        sys.exit(1)
