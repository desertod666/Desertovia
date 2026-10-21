"""Loopback-only development rehearsal. Run prepare, restart the local API, then verify.

Writes synthetic evidence and an authentication cookie outside the repository.
No model, Telegram, public server, payment or migration rollback is called.
"""
import argparse
import datetime
import http.cookiejar
import json
from pathlib import Path
import urllib.error
import urllib.parse
import urllib.request
import uuid

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('phase', choices=['prepare', 'verify'])
parser.add_argument('--base-url', default='http://127.0.0.1:5180')
parser.add_argument('--evidence', type=Path, required=True)
args = parser.parse_args()
base = args.base_url.rstrip('/')
url = urllib.parse.urlsplit(base)
if url.scheme != 'http' or url.hostname not in ('127.0.0.1', 'localhost', '::1') or url.path or url.query or url.fragment or url.username:
    parser.error('Only an explicit loopback development API is supported.')
folder = args.evidence.resolve()
repo = Path(__file__).resolve().parents[1]
if folder == repo or repo in folder.parents:
    parser.error('Authentication and synthetic raw evidence must stay outside the repository.')
folder.mkdir(parents=True, exist_ok=True)
state_file = folder / 'state.json'
cookie_file = folder / 'development.cookies'
cookies = http.cookiejar.MozillaCookieJar(str(cookie_file))
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cookies))


def call(method, path, body=None):
    data = None if body is None else json.dumps(body).encode()
    request = urllib.request.Request(base + path, data=data, method=method, headers={'Content-Type': 'application/json'})
    with opener.open(request, timeout=60) as response:
        raw = response.read()
        return json.loads(raw) if raw else None


def write(path, value):
    with path.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write('\n')


if args.phase == 'prepare':
    if state_file.exists() or cookie_file.exists():
        raise SystemExit('Existing evidence is preserved. Choose a new rehearsal directory.')
    suffix = uuid.uuid4().hex[:10]
    call('POST', '/auth/dev-login', {'name': 'policy-' + suffix})
    call('POST', '/api/me/character', {'name': 'Policy ' + suffix,
         'appearance': {'gender': 'Female', 'style': 0, 'skinTone': 0, 'hairColor': 0, 'outfitColor': 0}})
    village = call('POST', '/api/villages', {'name': 'Policy ' + suffix, 'isPrivate': True})
    path = '/api/villages/' + urllib.parse.quote(village['slug'], safe='') + '/director'
    initial = call('GET', path)
    assert initial['state']['policyVersion'] == 0 and initial['state']['disabledKinds'] == []
    assert initial['enabled'] is False, 'This rehearsal requires the model worker to be disabled.'
    stop = {'requestId': str(uuid.uuid4()), 'enabled': False, 'expectedPolicyVersion': 0}
    ack = call('PUT', path + '/category/raid', stop)
    assert ack['policyVersion'] == 1 and ack['disabledKinds'] == ['raid'] and ack['changed'] is True
    enable = call('PUT', path + '/category/raid', {'requestId': str(uuid.uuid4()), 'enabled': True, 'expectedPolicyVersion': 1})
    assert enable['policyVersion'] == 2 and enable['disabledKinds'] == []
    assert call('PUT', path + '/category/raid', stop) == ack
    current = call('GET', path)
    assert current['state']['policyVersion'] == 2 and current['state']['disabledKinds'] == []
    assert len(current['policyChanges']) == 2 and current['state']['callsToday'] == 0
    cookies.save(ignore_discard=True, ignore_expires=True)
    write(state_file, {'baseUrl': base, 'path': path, 'stop': stop, 'ack': ack, 'beforeRestart': current})
    print('Prepared one private synthetic world; two durable requests; stale replay did not reapply the stop. Restart the local API, then verify.')
else:
    state = json.loads(state_file.read_text(encoding='utf-8'))
    assert state['baseUrl'] == base
    cookies.load(ignore_discard=True, ignore_expires=True)
    current = call('GET', state['path'])
    assert current['enabled'] is False and current['state']['policyVersion'] == 2 and current['state']['disabledKinds'] == []
    assert sorted(current['policyChanges'], key=lambda r: r['requestId']) == sorted(state['beforeRestart']['policyChanges'], key=lambda r: r['requestId'])
    assert call('PUT', state['path'] + '/category/raid', state['stop']) == state['ack']
    final = call('GET', state['path'])
    assert final['state']['policyVersion'] == 2 and final['state']['disabledKinds'] == [] and len(final['policyChanges']) == 2
    assert final['state']['callsToday'] == 0
    result = {'verifiedAtUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
              'scope': 'Actual loopback HTTP; durable original acknowledgement, current policy and two audit rows across an external API restart',
              'policyVersion': 2, 'auditRows': 2, 'staleReplayUnchanged': True, 'modelCalls': 0,
              'privateSyntheticWorld': True, 'publicRelease': False}
    write(folder / 'result.json', result)
    print(json.dumps(result))
