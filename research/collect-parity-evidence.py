"""Collect bounded QA evidence without copying game assets or player save content."""
import hashlib
import json
import subprocess
import argparse
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def digest(path):
    with path.open('rb') as file:
        return hashlib.file_digest(file, 'sha256').hexdigest()

def cli_result(path):
    lines = path.read_text(encoding='utf-8-sig').splitlines()
    try:
        result = json.loads(lines[lines.index('### Result') + 1])
    except (ValueError, IndexError):
        raise RuntimeError(f'No successful result in {path.name}')
    if not result.get('passed'):
        raise RuntimeError(f'Failed result in {path.name}')
    return result

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--pc-managed', type=Path)
    args = parser.parse_args()
    artifacts = [ROOT / 'WebPlayer/Build' / name for name in
                 ('WebPlayer.wasm', 'WebPlayer.data', 'WebPlayer.framework.js')]
    newest = max(path.stat().st_mtime for path in artifacts)
    evidence = {}
    inputs = {}
    for browser in ('chrome24', 'edge24', 'firefox24'):
        path = ROOT / 'output/playwright' / f'{browser}-gameplay-final.txt'
        if path.stat().st_mtime < newest:
            raise RuntimeError(f'{browser} result predates this build')
        evidence[browser] = cli_result(path)
        path = ROOT / 'output/playwright' / f'{browser}-input-final.txt'
        if path.stat().st_mtime < newest:
            raise RuntimeError(f'{browser} input result predates this build')
        inputs[browser] = cli_result(path)
    performance_path = ROOT / 'output/playwright/nvidia-performance.txt'
    if performance_path.stat().st_mtime < newest:
        raise RuntimeError('GPU result predates this build')
    report = {
        'createdUtc': datetime.now(timezone.utc).isoformat(),
        'version': '1.24.0',
        'gitHead': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
        'buildSha256': {path.name: digest(path) for path in artifacts},
        'sourceStorySha256': digest(ROOT / 'UnityProject/Assets/StreamingAssets/chill-story.json'),
        'physicsAssets': {
            'sourceJsonBytes': (ROOT / 'UnityProject/Assets/Prototype/native-bone-bakes.json').stat().st_size,
            'webBinaryBytes': (ROOT / 'UnityProject/Assets/Prototype/bone-bakes.bytes').stat().st_size,
        },
        'browserGameplay': evidence,
        'browserPointerKeyboard': inputs,
        'shortGpuCapture': cli_result(performance_path),
        'boundedChromeLayout': cli_result(ROOT / 'output/playwright/chrome24-layouts-bounded-final.txt'),
        'completion95Established': False,
        'remainingGates': ['Full pointer/keyboard/DPI workflows', 'PC-matched rendering and physics captures',
                           'Remaining PC Demo/Release save fixtures', 'Sustained GPU frame-time qualification',
                           'Two-hour soak and context lifecycle'],
    }
    if args.pc_managed:
        report['referenceGameVersion'] = '1.17.3'
        report['referenceAssemblySha256'] = {name: digest(args.pc_managed / name) for name in
                                            ('Assembly-CSharp.dll', 'MagicaClothV2.dll', 'DistantLands.Cozy.Runtime.dll')}
    for name in ('parity-core-check', 'pc-save-check', 'save-store-check', 'physics-bake-check', 'cozy-lighting-check'):
        report[name] = json.loads((ROOT / 'research' / f'{name}.json').read_text(encoding='utf-8'))
    (ROOT / 'research/parity-browser-evidence.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'collected': True, 'browserCount': len(evidence), 'completion95Established': False}))

if __name__ == '__main__':
    main()
