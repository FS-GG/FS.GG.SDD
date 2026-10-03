#!/usr/bin/env python3
import importlib.util
import io
from pathlib import Path
import tempfile
import threading
import http.server
import zipfile

spec = importlib.util.spec_from_file_location('occupancy', Path(__file__).parents[1] / 'check-sdd-release-occupancy.py')
module = importlib.util.module_from_spec(spec);spec.loader.exec_module(module)

def package(body, signature=None, package_id=None, version='2.0.3'):
    stream = io.BytesIO()
    with zipfile.ZipFile(stream, 'w') as archive:
        archive.writestr('lib/finding.dll', body)
        if package_id: archive.writestr(package_id + '.nuspec', f'<package><metadata><id>{package_id}</id><version>{version}</version></metadata></package>')
        if signature: archive.writestr('.signature.p7s', signature)
    return stream.getvalue()

responses = {}
class Handler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        code, body = responses.get(self.path, (404, b''))
        self.send_response(code);self.end_headers();self.wfile.write(body)
    def log_message(self, *args): pass
server = http.server.ThreadingHTTPServer(('127.0.0.1', 0), Handler)
thread = threading.Thread(target=server.serve_forever, daemon=True);thread.start()
base = 'http://127.0.0.1:' + str(server.server_port)
ids = ['FS.GG.SDD.Knowledge']
path = '/github/fs.gg.sdd.knowledge/2.1.0/fs.gg.sdd.knowledge.2.1.0.nupkg'
public_path = path.replace('/github/', '/public/')
count = 0

def check(expected, candidate=None, token='synthetic'):
    global count
    try:
        module.preflight('2.1.0', ids, base + '/index', base + '/github', base + '/public', token, 'fixture', candidate)
    except ValueError:
        assert not expected
    else: assert expected
    count += 1

try:
    responses['/index'] = (200, b'{}')
    known = '/github/fs.gg.sdd.artifacts/2.0.3/fs.gg.sdd.artifacts.2.0.3.nupkg'
    artifact = package(b'known-baseline', package_id='FS.GG.SDD.Artifacts')
    tool = package(b'known-tool', package_id='FS.GG.SDD.Cli')
    responses[known] = (200, artifact)
    responses[known.replace('/github/', '/public/')] = (200, artifact)
    responses['/github/fs.gg.sdd.cli/2.0.3/fs.gg.sdd.cli.2.0.3.nupkg'] = (200, tool)
    responses['/public/fs.gg.sdd.cli/2.0.3/fs.gg.sdd.cli.2.0.3.nupkg'] = (200, tool)
    check(True)
    check(False, token='')
    responses['/index'] = (403, b'');check(False)
    responses['/index'] = (404, b'');check(False)
    responses['/index'] = (200, b'{}')
    responses[known] = (403, b'');check(False)
    responses[known] = (404, b'');check(False)
    responses[known] = (200, package(b'known-baseline', package_id='Wrong.Identity'));check(False)
    responses[known] = (200, package(b'known-baseline', package_id='FS.GG.SDD.Artifacts', version='2.0.2'));check(False)
    responses[known] = (200, package(b'changed', package_id='FS.GG.SDD.Artifacts'));check(False)
    responses[known] = (200, artifact)
    responses[path] = (403, b'');check(False)
    responses[path] = (200, package(b'authored'));check(False)
    with tempfile.TemporaryDirectory() as td:
        root = Path(td);(root / 'FS.GG.SDD.Knowledge.2.1.0.nupkg').write_bytes(package(b'authored'))
        check(True, root)
        responses[path] = (200, package(b'authored', b'feed-signature'));check(True, root)
        responses[public_path] = (200, package(b'different'));check(False, root)
        responses[public_path] = (500, b'');check(False, root)
    print(f'release occupancy fixture: {count} cases passed')
finally:
    server.shutdown();server.server_close();thread.join()
