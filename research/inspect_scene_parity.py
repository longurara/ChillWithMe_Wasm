"""Read-only compare of imported transforms and asset selection, no game launch."""
import json,re,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'src/graphics'))
from prepare_project import ROOT,split_yaml,field,go_id
def scene(p):
    _,blocks=split_yaml(p.read_text('utf-8-sig'))
    gos={i:b for t,i,b in blocks if t==1}
    ts={i:(go_id(b),b) for t,i,b in blocks if t in (4,224)}
    bygo={g:i for i,(g,b) in ts.items()}
    def path(i):
        g,b=ts[i];parent=re.search(r'm_Father: \{fileID: (-?\d+)\}',b).group(1)
        return (path(parent)+'/' if parent in ts else '')+field(gos[g],'m_Name')
    components={}
    for t,i,b in blocks:components.setdefault(go_id(b),[]).append((t,b))
    rows={}
    for i,(g,b) in ts.items():
        name=field(gos[g],'m_Name');p=path(i)
        details={k:field(b,k) for k in ('m_LocalPosition','m_LocalRotation','m_LocalScale')}
        details['active']=field(gos[g],'m_IsActive')
        for t,cb in components.get(g,[]):
            if t==20:details['fov']=field(cb,'field of view');details['lensShift']=field(cb,'m_LensShift')
        rows[p]=details
    return rows
original=scene(ROOT/'Recovered/ExportedProject/Assets/App/Scenes/RoomScene.unity')
port=scene(ROOT/'UnityProject/Assets/Prototype/Room.unity')
interesting={p:{'original':v,'port':port.get(p)} for p,v in original.items() if p in port and (p in ('Character','Character/Character','Character/Character/Character','RoomCameraManager','RoomCameraManager/Cameras','RoomCameraManager/Cameras/MainCamera','CameraTransforms/CameraTransform0') or re.search(r'(Beret|m_radio|m_musicbox|m_lantern)',p,re.I) and p.count('/')<12)}
def numbers(s):return [float(x) for x in re.findall(r'[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?',s or '')]
def same(a,b,key):
    av,bv=numbers(a.get(key)),numbers(b.get(key))
    if len(av)!=len(bv):return False
    error=max([abs(x-y) for x,y in zip(av,bv)] or [0])
    if key=='m_LocalRotation':error=min(error,max([abs(x+y) for x,y in zip(av,bv)] or [0]))
    return error<1e-5
diffs=[{'path':p,'original':v,'port':port[p]} for p,v in original.items() if p in port and any(not same(v,port[p],k) for k in ('m_LocalPosition','m_LocalRotation','m_LocalScale','fov'))]
report={'method':'Read-only scene YAML comparison','sharedTransforms':len(set(original)&set(port)),'transformOrFovDifferences':diffs,'selectedObjects':interesting}
(ROOT/'research/scene-parity-check.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),'utf-8')
print('Shared transforms',report['sharedTransforms'],'transform/FOV differences',len(diffs))
for p,v in interesting.items():print(p,json.dumps(v))
print('Non-bone 3D differences:', [d['path'] for d in diffs if not d['path'].startswith(('Paremt/PCPlatform','Character/Character/Character/Character_Hips'))])
