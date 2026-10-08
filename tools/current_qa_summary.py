"""Validate executed 0.3 game evidence, compare upstream inventory, report observed frames."""
import argparse, hashlib, json, math, re, statistics
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--benchmark', default='qa-alpha3-benchmark')
args = parser.parse_args()
research = ROOT / 'research'
game_root = research / 'game-qa'
product = json.loads((ROOT / 'launcher/product.json').read_text())
manifest = json.loads((ROOT / 'launcher/payload/client-manifest.json').read_text())
payload = ROOT / 'launcher/payload' / manifest['filename']
sha = hashlib.sha256(payload.read_bytes()).hexdigest()
assert sha == manifest['sha256'], 'Packaged payload hash mismatch'
names = ['qa-alpha3-native', 'qa-alpha3-stack', 'qa-alpha3-baseline', args.benchmark]
results = {}
for name in names:
    assert name.startswith('qa-') and Path(name).name == name
    data = json.loads((game_root / name / 'result.json').read_text())
    assert data['checks'] and not any(c['status'] == 'FAIL' for c in data['checks']), name
    if name != 'qa-alpha3-baseline':
        runtime = ROOT.parent / name / 'mods' / manifest['filename']
        actual = hashlib.sha256(runtime.read_bytes()).hexdigest()
        assert actual == sha, name + ': QA used different client bytes'
        assert data.get('artifact_sha256', actual) == sha
    assert data.get('launch', {}).get('exit_code', 0) == 0, name + ': abnormal game exit'
    results[name] = data

current = {c['name']: c for c in results['qa-alpha3-stack']['checks'] if c['status'] == 'CAPTURED'}
reference = {c['name']: c for c in results['qa-alpha3-baseline']['checks'] if c['status'] == 'CAPTURED'}
scenarios = ['right-drag', 'left-collect', 'shift-drag', 'wheel-out', 'wheel-in',
             'wheel-in-first', 'wheel-in-last', 'reverse-out', 'reverse-in']
comparisons = []
for name in scenarios:
    a, b = current[name], reference[name]
    same = a['slots'] == b['slots'] and a['held'] == b['held']
    comparisons.append({'name': name, 'status': 'PASS' if same else 'FAIL'})
assert all(c['status'] == 'PASS' for c in comparisons), comparisons

frames = results[args.benchmark]['frames']
assert frames, 'No observed frametimes'
assert {f['window'] for f in frames} == {0, 1, 2, 3}, 'Incomplete OFF/ON pairs'
windows = []
for window in sorted({f['window'] for f in frames}):
    samples = [f for f in frames if f['window'] == window]
    values = [f['ms'] for f in samples]
    assert len(values) >= 100, 'Insufficient frame samples'
    assert all(math.isfinite(v) and v > 0 for v in values)
    slow = sorted(values, reverse=True)
    windows.append({'window': window, 'mode': 'OFF' if window % 2 == 0 else 'ON',
                    'samples': len(values), 'average_fps': 1000 / statistics.mean(values),
                    'one_percent_low': 1000 / statistics.mean(slow[:max(1, math.ceil(len(slow) * .01))]),
                    'mean_frametime_ms': statistics.mean(values),
                    'p99_ms': sorted(values)[min(len(values)-1, math.ceil(len(values)*.99)-1)],
                    'max_frametime_ms': max(values), 'spikes_over_50ms': sum(v > 50 for v in values),
                    'peak_used_heap_mib': max(f['heap'] for f in samples) / 1048576})
pauses = []
for line in (research / (args.benchmark + '-gc.log')).read_text().splitlines():
    if '[GC ' in line or '[Full GC ' in line:
        found = re.findall(r'([0-9.]+) secs\]', line)
        if found:
            pauses.append(float(found[0]) * 1000)
summary = {'version': product['version'], 'artifact_sha256': sha,
           'runtime_payload_hashes': 'PASS',
           'game_checks': {name: {'pass': sum(c['status'] == 'PASS' for c in d['checks']),
                                 'captured': sum(c['status'] == 'CAPTURED' for c in d['checks'])}
                           for name, d in results.items()},
           'inventory': {'status': 'PASS', 'reference_file_id': 2287384, 'comparisons': comparisons,
                         'scope': 'OS AWT Robot input to real Forge GuiChest; 45 slot counts and held count',
                         'unverified': ['Physical human input', 'NBT identity', 'Crafting output', 'Third-party GUI', 'Live server']},
           'performance': {'status': 'INCONCLUSIVE_RELEASE_GATE_NOT_PASSED', 'benchmark': args.benchmark,
                           'scope': 'Software renderer, same Ttro process, selected modules OFF/ON/OFF/ON; no Vanilla/Lunar comparison',
                           'windows': windows,
                           'gc_launch_total': {'count': len(pauses), 'total_pause_ms': sum(pauses),
                                               'max_pause_ms': max(pauses, default=0)},
                           'unmeasured': ['Hardware GPU', 'Total process RAM/CPU', 'Input-to-photon', 'Startup time', 'Mode-specific GC attribution']}}
(research / 'qa-summary-alpha3.json').write_text(json.dumps(summary, indent=2) + '\n')
print(json.dumps(summary, indent=2))
