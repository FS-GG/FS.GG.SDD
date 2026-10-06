#!/usr/bin/env python3
"""Read-only both-feed preflight; occupied versions may resume only exact payloads."""
import argparse
import base64
import io
import json
import os
from pathlib import Path
import re
import sys
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


def preflight(version, ids, github_index, github_download, public_download, token, actor, candidate=None, contracts_version=None, github_api="https://api.github.com"):
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version):
        raise ValueError("invalid stable release version")
    if not token or not actor:
        raise ValueError("authenticated publisher feed read is required; absence is not occupancy evidence")
    if contracts_version is not None and not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", contracts_version):
        raise ValueError("invalid independent Contracts version")
    versions = {package_id: contracts_version if package_id == "FS.GG.Contracts" else version for package_id in ids}
    if any(v is None for v in versions.values()):
        raise ValueError("independent Contracts version is required")
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
    for known in ['fs.gg.sdd.artifacts', 'fs.gg.sdd.cli']:
        suffix = f'/{known}/2.0.3/{known}.2.0.3.nupkg'
        baseline = read(github_download.rstrip('/') + suffix, True)
        if baseline is None:
            raise ValueError(f"known GitHub baseline {known} 2.0.3 unreadable; occupancy unproved")
        observed = payload(baseline)
        specifications = [data for name, data in observed.items() if name.endswith('.nuspec')]
        if len(specifications) != 1:
            raise ValueError(f"known baseline {known} has no unique package identity")
        metadata = ET.fromstring(specifications[0])
        if metadata.findtext('.//{*}id', '').lower() != known or metadata.findtext('.//{*}version') != '2.0.3':
            raise ValueError(f"known baseline {known} identity/version mismatch")
        public_baseline = read(public_download.rstrip('/') + suffix)
        if public_baseline is None or payload(public_baseline) != observed:
            raise ValueError(f"known baseline {known} differs from its public normalized payload")
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
    ids = ["FS.GG.Contracts"] + Path(__file__).with_name("sdd-release-packages.txt").read_text().splitlines()
    preflight(args.version, ids, "https://nuget.pkg.github.com/FS-GG/index.json",
              "https://nuget.pkg.github.com/FS-GG/download", "https://api.nuget.org/v3-flatcontainer",
              os.environ.get("GH_TOKEN", ""), os.environ.get("GITHUB_ACTOR", ""), args.candidate, args.contracts_version)


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"release occupancy refused: {error}", file=sys.stderr)
        sys.exit(1)
