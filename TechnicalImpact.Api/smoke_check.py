"""Run against the running .NET API; Python standard library only."""
import json
from urllib.request import urlopen
from urllib.error import HTTPError

BASE = 'http://localhost:5080'

def get(path):
    with urlopen(BASE + path, timeout=10) as response:
        return json.load(response)

assert len(get('/api/assets')) == 11
result = get('/api/assets/static-db/impact?environment=Production')
assert result['potentialAssetCount'] == 4
assert {x['asset']['id']: x['depth'] for x in result['assets']} == {
    'occupation-api': 1, 'pricing-api': 1, 'quote-api': 2, 'broker-portal': 3}
assert get('/api/assets/static-db/impact?maxDepth=1')['potentialAssetCount'] == 2
edges = get('/api/assets/policy-api/relationships')
assert any(x['asset']['id'] == 'policy-db' for x in edges['outgoing'])
assert any(x['asset']['id'] == 'claims-worker' for x in edges['incoming'])
assert {x['asset']['id'] for x in get('/api/assets/policy-updated/impact')['assets']} == {
    'policy-api', 'claims-worker', 'quote-api', 'broker-portal'}
for path, status in [('/api/assets/missing', 404),
                     ('/api/graph?environment=Unknown', 400),
                     ('/api/assets/policy-api/impact?maxDepth=0', 400),
                     ('/api/assets/policy-api/impact?direction=invalid', 400)]:
    try:
        get(path)
        raise AssertionError(f'Expected {status} for {path}')
    except HTTPError as error:
        assert error.code == status
print('HTTP smoke checks passed.')
