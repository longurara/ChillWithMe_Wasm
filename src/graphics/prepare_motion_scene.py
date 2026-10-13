"""Recover prop actions, presentation bindings and native animation events."""
import collections, json, re, sys
from pathlib import Path
from prepare_project import ROOT, split_yaml, field, go_id, meta_guid
sys.path.insert(0,str(ROOT/'.tools/python'))
import yaml

source=ROOT/'Recovered/ExportedProject/Assets'
_,blocks=split_yaml((source/'App/Scenes/RoomScene.unity').read_text('utf-8-sig'))
components={i:(t,b) for t,i,b in blocks}
objects={i:field(b,'m_Name') for t,i,b in blocks if t==1}
transforms={i:(go_id(b),re.search(r'm_Father: \{fileID: (-?\d+)\}',b).group(1)) for t,i,b in blocks if t in (4,224)}
go_transforms={g:i for i,(g,p) in transforms.items()}
def path_of(go):
    tid=go_transforms.get(go);parent=transforms[tid][1] if tid else '0'
    return (path_of(transforms[parent][0])+'/' if parent in transforms else '')+objects.get(go,'')
def ref(value):
    key=str((value or {}).get('fileID',0))
    if key not in components:return ''
    t,b=components[key];return path_of(key if t==1 else go_id(b))
def data(b):return yaml.safe_load(b)['MonoBehaviour']
def nested(value):
    key=str((value or {}).get('fileID',0))
    return data(components[key][1]) if key in components else {}
def names(raw,fields):return [ref(raw.get(name)) for name in fields]

desk=data(next(b for t,i,b in blocks if t==114 and '  _deskObjectAnimator:' in b))
bindings={name[1:]:ref(desk.get(name)) for name in ('_breakBookAnimator','_workBookAnimator','_deskObjectAnimator','_chairAnimator','_chairAnimatorMobile','_penAnimator','_coffeeDropParticle')}
bindings.update(windowAnimator=ref(nested(desk.get('_windowController')).get('_windowAnimator')),
                musicboxAnimator=ref(nested(desk.get('_memorie')).get('_memorieAnimator')))
cup=nested(desk.get('_cupcake'));cookie=nested(desk.get('_cookie'));bread=nested(desk.get('_bread'))
bindings.update(cupcake=names(cup,['_cake1Object','_cake2Object','_cupObject']),cookie=names(cookie,['_cookie1Object','_cookie2Object']),
                breadA=names(bread,['_breadA'+str(i)+'Object' for i in range(1,6)]),breadB=names(bread,['_breadB'+str(i)+'Object' for i in range(1,6)]))
scripts=source/'Scripts/Assembly-CSharp'
controller=(scripts/'HeroineUseObjectController.cs').read_text('utf-8-sig')
def enum_values(name):
    body=re.search(r'enum '+name+r'\s*\{(.*?)\}',controller,re.S).group(1)
    return {name:int(value) for name,value in re.findall(r'(\w+)\s*=\s*(-?\d+)',body)}
chair=enum_values('ChairAnimationType');pen=enum_values('PenAnimationType')
actions=[]
for name,kind,state in re.findall(r'public void (\w+)\(\)\s*\{\s*Change(Chair|Pen)Animation\(\w+\.(\w+)\);',controller):
    actions.append({'name':name,'kind':kind,'state':state,'value':(chair if kind=='Chair' else pen)[state],
                    'trigger':kind=='Chair' and state in ['PlayFirst','PlaySecond','PlayEnd','PlayOpenWindow','PlayCloseWindow','PlayStartMemorie']})
bindings['actions']=actions
sleeve_guid=meta_guid(scripts/'Bulbul/CostumeSleeveAdjuster.cs')
bindings['sleeves']=[{'renderer':ref(data(b).get('_meshRenderer')),'bone':ref(data(b).get('_sleeveTransform'))} for t,i,b in blocks if t==114 and sleeve_guid and sleeve_guid in b]

camera_raw=data(next(b for t,i,b in blocks if t==114 and '  _camTransform:' in b))
poses=[]
for camera in camera_raw['_virtualCameras']:
    camera_go=str(camera['fileID']);camera_path=ref(camera)
    mono=[data(b) for t,i,b in blocks if t==114 and go_id(b)==camera_go]
    native=next((x for x in mono if 'm_Lens' in x),{})
    follow=ref(native.get('m_Follow'));look=ref(native.get('m_LookAt'))
    children=[data(b) for t,i,b in blocks if t==114 and path_of(go_id(b)).startswith(camera_path+'/')]
    transposer=next((x for x in children if 'm_FollowOffset' in x),{})
    composer=next((x for x in children if 'm_TrackedObjectOffset' in x),{})
    vec=lambda value:[float((value or {}).get(k,0)) for k in ('x','y','z')]
    poses.append({'path':camera_path,'follow':follow,'lookAt':look,'followOffset':vec(transposer.get('m_FollowOffset')),
                  'lookOffset':vec(composer.get('m_TrackedObjectOffset')),'useFollow':bool(follow and transposer),'fov':native.get('m_Lens',{}).get('FieldOfView',35)})
camera_config={'animator':ref(camera_raw.get('_animator')),'fixedViews':[ref(v) for v in camera_raw['_camTransform']],'poses':poses}
look_raw=data(next(b for t,i,b in blocks if t==114 and '  _faceSkin:' in b and '  _lookAtHeadWeight:' in b))
look_config={'face':ref(look_raw['_faceSkin']),'head':ref(look_raw['_headTransform']),'eyes':ref(look_raw['_eyeTransform']),'target':ref(look_raw['_lookAtTarget'])}
shine_guid=meta_guid(scripts/'AprilFoolSunglasses.cs')
shine=[{'object':ref(data(b).get('_object')),'material':(data(b).get('_material') or {}).get('guid','')} for t,i,b in blocks if t==114 and shine_guid and shine_guid in b]
payload={'desk':bindings,'camera':camera_config,'look':look_config,'sunglasses':shine}
(ROOT/'UnityProject/Assets/Prototype/motion-scene.json').write_text(json.dumps(payload,ensure_ascii=False,indent=2),encoding='utf-8')

events=[]
for path in (source/'AnimationClip').glob('*.anim'):
    text=path.read_text('utf-8-sig');start=text.rfind('  m_Events:')
    if start<0:continue
    for row in yaml.safe_load(text[start:]).get('m_Events') or []:
        events.append({'clip':path.stem,'name':row['functionName'],'time':row['time'],'text':row.get('data') or '', 'integer':row.get('intParameter',0)})
counts=collections.Counter(e['name'] for e in events)
methods=[]
for name in sorted(counts):
    if not re.fullmatch(r'[A-Za-z_]\w*',name):raise ValueError('Invalid animation event '+name)
    methods.append('        public void '+name+'(AnimationEvent item) => Dispatch("'+name+'", item);')
generated='using UnityEngine;\nnamespace Hlc.ChillPrototype\n{\n    public sealed partial class ChillAnimationActions\n    {\n'+'\n'.join(methods)+'\n    }\n}\n'
(ROOT/'src/graphics/Runtime/ChillAnimationActions.Events.generated.cs').write_text(generated,encoding='utf-8')
(ROOT/'research/animation-event-report.json').write_text(json.dumps({'events':events,'methods':dict(counts),'chairPenActions':actions,'cameraCount':len(poses)},ensure_ascii=False,indent=2),encoding='utf-8')
print('Native animation events:',len(events),'methods:',len(counts),'chair/pen actions:',len(actions),'cameras:',len(poses),'sleeves:',len(bindings['sleeves']))
