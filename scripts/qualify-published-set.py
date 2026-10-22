#!/usr/bin/env python3
"""Authenticate an original retained candidate, then read back its already-published set from both feeds.

Read-only: it never packs, pushes, tags or consumes publisher credentials. nuget.org may index a push
tens of minutes later, so absence is retried within a fixed bound before it is reported as a failure."""
import argparse
import base64
import http.client
import importlib.util
import json
import os
from pathlib import Path
import re
import sys
import time
import zipfile
import urllib.error
import urllib.parse
import urllib.request

sys.dont_write_bytecode = True
_spec = importlib.util.spec_from_file_location('retained_reader', Path(__file__).with_name('preflight-retained-occupancy.py'))
_reader = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_reader)
PACKAGES = _reader.PACKAGES
MAX_PACKAGE = 32 * 1024 * 1024
# Total indexing allowance across all eight reads: 19 polls at 15 s, then 30 s polls (about 44.75 minutes).
MAX_WAIT_SECONDS = 2700
GITHUB_DOWNLOAD = 'https://nuget.pkg.github.com/FS-GG/download/{id}/{version}/{id}.{version}.nupkg'
NUGET_DOWNLOAD = 'https://api.nuget.org/v3-flatcontainer/{id}/{version}/{id}.{version}.nupkg'


class Absent(Exception):
    """The feed does not (yet) serve the selected archive."""


def interval(attempt):
    return 15 if attempt < 20 else 30


class Redirect(urllib.request.HTTPRedirectHandler):
    """HTTPS only; credentials never follow a redirect to another host."""
    def redirect_request(self, request, response, code, message, headers, target):
        if urllib.parse.urlparse(target).scheme != 'https':
            raise ValueError('non-HTTPS package redirect refused')
        redirected = super().redirect_request(request, response, code, message, headers, target)
        if redirected is not None and urllib.parse.urlparse(target).netloc != urllib.parse.urlparse(request.full_url).netloc:
            redirected.remove_header('Authorization')
        return redirected


def open_feed(url, authorization=None):
    """One bounded read. HTTP404/429/5xx and transient transport failures raise Absent; other denials are fatal."""
    headers = {'Authorization': authorization} if authorization else {}
    try:
        with urllib.request.build_opener(Redirect()).open(urllib.request.Request(url, headers=headers), timeout=60) as response:
            raw = response.read(MAX_PACKAGE + 1)
            declared = response.headers.get('Content-Length')
    except urllib.error.HTTPError as error:
        if error.code in (404, 429) or error.code >= 500:
            raise Absent('HTTP' + str(error.code)) from error
        raise ValueError('package read refused HTTP' + str(error.code)) from error
    except (urllib.error.URLError, TimeoutError, ConnectionError, http.client.HTTPException) as error:
        raise Absent(type(error).__name__) from error
    if len(raw) > MAX_PACKAGE:
        raise ValueError('package response exceeds selected byte bound')
    if declared is not None and declared.isdigit() and int(declared) != len(raw):
        raise Absent('truncated body')
    return raw


def readback(version, destination, github_authorization, fetch=open_feed, sleep=time.sleep,
             monotonic=time.monotonic, limit=MAX_WAIT_SECONDS):
    """Write both feed archives of every member under destination/feed-readback; wait boundedly for indexing."""
    target = destination / 'feed-readback'
    target.mkdir(mode=0o700, parents=True, exist_ok=False)
    started = monotonic()
    observations = []
    for package in PACKAGES:
        lower = package.lower()
        for feed, template, authorization in [('github', GITHUB_DOWNLOAD, github_authorization),
                                              ('nuget', NUGET_DOWNLOAD, None)]:
            url = template.format(id=lower, version=version)
            attempt = 0
            while True:
                attempt += 1
                try:
                    raw = fetch(url, authorization)
                    break
                except Absent as reason:
                    waited = monotonic() - started
                    if waited + interval(attempt) > limit:
                        raise ValueError(f'{package} {version} {feed}: not served within {limit} seconds ({reason})')
                    sleep(interval(attempt))
            if not raw:
                raise ValueError(f'{package} {version} {feed}: empty archive')
            with (target / f'{lower}.{feed}.nupkg').open('xb') as output:
                output.write(raw)
            observations.append(dict(packageId=package, version=version, feed=feed, attempts=attempt,
                                     bytes=len(raw)))
    return observations


def qualify(run_id, archive_sha256, source, version, destination, read=_reader.authenticated_reader,
            fetch=open_feed, sleep=time.sleep, monotonic=time.monotonic, limit=MAX_WAIT_SECONDS):
    if (not re.fullmatch(r'[1-9][0-9]{0,19}', run_id) or not re.fullmatch(r'[0-9a-f]{64}', archive_sha256)
            or not re.fullmatch(r'[0-9a-f]{40}', source) or not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+', version)):
        raise ValueError('published-set qualification requires original run, ZIP SHA256, source and stable version')
    token, actor = os.environ.get('GH_TOKEN', ''), os.environ.get('GITHUB_ACTOR', '')
    if not token or not actor:
        raise ValueError('normal authenticated read context required')
    # Same authentication as the retained promotion: successful original run, ZIP digest, source, version, v4 manifest.
    _, _, contracts, artifact, gates = _reader.authenticate_candidate(
        run_id, archive_sha256, destination / 'packages', read, source, version)
    authorization = 'Basic ' + base64.b64encode((actor + ':' + token).encode()).decode()
    observations = readback(version, destination, authorization, fetch, sleep, monotonic, limit)
    return dict(schema='fsgg.sdd.published-readback/v1', publicationAuthorized=False, candidateRun=int(run_id),
                candidateArtifact=artifact['id'], archiveSha256=archive_sha256, candidateSource=source,
                version=version, contractsVersion=contracts, originalQualificationGates=gates,
                verificationSource=os.environ.get('GITHUB_SHA'), observations=observations)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['run-id', 'archive-sha256', 'source', 'version']:
        parser.add_argument('--' + name, required=True)
    parser.add_argument('--destination', type=Path, required=True)
    arguments = parser.parse_args()
    try:
        result = qualify(arguments.run_id, arguments.archive_sha256, arguments.source, arguments.version,
                         arguments.destination)
    except (ValueError, OSError, KeyError, TypeError, zipfile.BadZipFile) as error:
        print(json.dumps(dict(outcome='Refused', publicationAuthorized=False,
                              reason=_reader.sanitized(str(error) if isinstance(error, ValueError) else type(error).__name__))))
        return 1
    print(json.dumps(result, sort_keys=True))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
