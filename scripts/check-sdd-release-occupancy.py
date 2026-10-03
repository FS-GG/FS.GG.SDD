#!/usr/bin/env python3
"""Read-only both-feed preflight; occupied versions may resume only exact payloads."""
import argparse
import base64
import io
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


def preflight(version, ids, github_index, github_download, public_download, token, actor, candidate=None):
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version):
        raise ValueError("invalid stable release version")
    if not token or not actor:
        raise ValueError("authenticated publisher feed read is required; absence is not occupancy evidence")
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

    def read(url, authenticated=False):
        request = urllib.request.Request(url)
        if authenticated:
            request.add_header("Authorization", "Basic " + auth)
        try:
            with opener.open(request, timeout=30) as response:
                return response.read()
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
    for package_id in ids:
        lower = package_id.lower()
        suffix = f"/{lower}/{version}/{lower}.{version}.nupkg"
        expected = payload((candidate / f"{package_id}.{version}.nupkg").read_bytes()) if candidate else None
        for feed, base, authenticated in [
            ("github", github_download, True), ("nuget", public_download, False)
        ]:
            data = read(base.rstrip("/") + suffix, authenticated)
            if data is not None:
                if expected is None or payload(data) != expected:
                    raise ValueError(f"{package_id} {version} occupied on {feed}; refusing substitution")
                print(f"{package_id} {version} {feed}: exact retained payload already present")
            else:
                print(f"{package_id} {version} {feed}: absent")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("version")
    parser.add_argument("--candidate", type=Path)
    args = parser.parse_args()
    ids = Path(__file__).with_name("sdd-release-packages.txt").read_text().splitlines()
    preflight(args.version, ids, "https://nuget.pkg.github.com/FS-GG/index.json",
              "https://nuget.pkg.github.com/FS-GG/download", "https://api.nuget.org/v3-flatcontainer",
              os.environ.get("GH_TOKEN", ""), os.environ.get("GITHUB_ACTOR", ""), args.candidate)


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"release occupancy refused: {error}", file=sys.stderr)
        sys.exit(1)
