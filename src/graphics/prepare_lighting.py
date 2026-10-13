"""Extract all baked lighting presets without modifying recovered input."""
from pathlib import Path
import json
from prepare_project import ROOT, make_scene

source = ROOT / 'Recovered/ExportedProject/Assets'
temporary = ROOT / '.tools/lighting-extract/RoomScene.unity'
target = ROOT / 'UnityProject/Assets/Prototype'
for preset in ('day', 'sunset', 'night', 'cloudy'):
    _, lighting = make_scene(source/'App/Scenes/RoomScene.unity', temporary, source, preset)
    (target/f'{preset}-lighting.json').write_text(json.dumps(lighting, indent=2), encoding='utf-8')
    print(preset, len(lighting['textures']), len(lighting['renderers']))
