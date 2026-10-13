"""Extract source cloth parameters without reimporting the asset library."""
import json, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / '.tools/python'))
sys.path.insert(0, str(ROOT / 'research/python'))
from asset_pipeline import bone_physics

if __name__ == '__main__':
    result = bone_physics(ROOT / 'Recovered/ExportedProject/Assets', ROOT / 'UnityProject')
    profile = json.loads((ROOT / 'UnityProject/Assets/Prototype/bone-physics.json').read_text('utf-8'))
    report = {'source': 'RoomScene MagicaCloth2 serialized parameters', **result,
              'profiles': [{k: v for k, v in g.items() if k not in ('roots', 'colliders', 'sourceConstraints')}
                           for g in profile['groups']],
              'webDifferences': ['Rotation-only CPU solver; no MeshCloth or self-collision',
                                'Angle caps: hair 30, coat 40, hat 25, custom sleeves 12 degrees',
                                '60 Hz solver; source depth curves evaluated with Hermite interpolation',
                                'Original tapered capsules plus five neck/arm capsules',
                                'Custom sleeves have authored source tweens and a web-only secondary chain']}
    (ROOT / 'research/physics-source-profile.json').write_text(json.dumps(report, indent=2) + '\n', 'utf-8')
    print(json.dumps(result))
