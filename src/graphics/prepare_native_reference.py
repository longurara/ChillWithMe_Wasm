"""Extract complete native cloth settings for the local Windows reference runner."""
import json,re,sys
from pathlib import Path
from prepare_project import ROOT,split_yaml,field,go_id,meta_guid
sys.path.insert(0,str(ROOT/'.tools/python'))
import yaml

source=ROOT/'Recovered/ExportedProject/Assets'
_,blocks=split_yaml((source/'App/Scenes/RoomScene.unity').read_text('utf-8-sig'))
objects={i:field(b,'m_Name') for t,i,b in blocks if t==1}
transforms={i:b for t,i,b in blocks if t==4}
bygo={go_id(b):i for i,b in transforms.items()}
def path(i):
    b=transforms[i];parent=re.search(r'm_Father: \{fileID: (-?\d+)\}',b).group(1)
    return (path(parent)+'/' if parent in transforms else '')+objects[go_id(b)]
profile=json.loads((ROOT/'UnityProject/Assets/Prototype/bone-physics.json').read_text('utf-8'))
dll=meta_guid(source/'Plugins/MagicaClothV2.dll')
groups=[]
for t,i,b in blocks:
    if t!=114 or not re.search(r'm_Script: .*guid: '+dll,b):continue
    data=yaml.safe_load(b)['MonoBehaviour']
    if 'serializeData' not in data:continue
    s=data['serializeData'];roots=[path(str(r['fileID'])) for r in s['rootBones']]
    group=next(g for g in profile['groups'] if g['roots']==roots)
    # Reference lists are rebound to the reconstructed scene by the runner.
    params=json.loads(json.dumps(s))
    for key in ('rootBones','sourceRenderers','paintMaps'):params.pop(key,None)
    params['colliderCollisionConstraint'].pop('colliderList',None)
    groups.append({'path':path(bygo[go_id(b)]),'roots':roots,'colliders':group['colliders'],'parameters':json.dumps(params,separators=(',',':'))})
target=ROOT/'.tools/native-reference.json'
target.write_text(json.dumps({'groups':groups,'colliders':profile['colliders']},indent=2),'utf-8')
print('Native reference:',len(groups),'cloth groups,',len(profile['colliders']),'source colliders')
