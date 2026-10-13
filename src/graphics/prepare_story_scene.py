"""Recover scene bindings for decoration, hat and screen-sharing commands."""
import json, re, sys
from prepare_project import ROOT, split_yaml, field, go_id
from prepare_story import SOURCE, table, enumeration
sys.path.insert(0,str(ROOT/'.tools/python'))
import yaml

_,blocks=split_yaml((SOURCE/'App/Scenes/RoomScene.unity').read_text('utf-8-sig'))
objects={i:field(b,'m_Name') for t,i,b in blocks if t==1}
transforms={i:(go_id(b),re.search(r'm_Father: \{fileID: (-?\d+)\}',b).group(1)) for t,i,b in blocks if t in (4,224)}
go_transforms={g:i for i,(g,p) in transforms.items()}
components={i:(t,b) for t,i,b in blocks}
def path_of(go):
    tid=go_transforms.get(go)
    parent=transforms[tid][1] if tid else '0'
    return (path_of(transforms[parent][0])+'/' if parent in transforms else '')+objects.get(go,'')
def reference(ref):
    key=str(ref.get('fileID',0)) if isinstance(ref,dict) else '0'
    if key not in components:return ''
    t,b=components[key]
    return path_of(key if t==1 else go_id(b))
def refs(items):return [reference(ref) for ref in items or [] if reference(ref)]
def external(ref):return ref.get('guid','') if isinstance(ref,dict) else ''
def pairs(value):return (value or {}).get('m_list',[])

master=table('DecorationMaster')
skin_names=enumeration('DecorationService.cs','DecorationSkinType')
model_categories={row['ModelType']:row['CategoryType'] for row in master['DecorationModels']}
skins=[{'name':name,'id':number,'model':row['ModelType'],'category':model_categories.get(row['ModelType'],0)} for name,number in skin_names.items() for row in master['DecorationSkins'] if row['SkinType']==number]
bindings=[];special={}
for t,i,b in blocks:
    if t!=114:continue
    if '  hairPinStyles:' in b and '  hairPins:' in b:
        hair_source=yaml.safe_load(b)['MonoBehaviour']
        special.update(hairNormal=refs(hair_source['normal']),
                       hairAlternates=refs(hair_source['hairPinStyles']),
                       hairPins=refs([row['Value'] for row in pairs(hair_source['hairPins'])]))
    if any(key in b for key in ('  _magicalPrincessBeret:','  _screenSharing:')):
        raw=yaml.safe_load(b)['MonoBehaviour']
        if '_magicalPrincessBeret' in raw:special.update(hat=reference(raw['_magicalPrincessBeret']),hair=reference(raw.get('_hairMeshRenderer')))
        if '_screenSharing' in raw and components.get(str(raw['_screenSharing'].get('fileID',0)),(0,''))[0]==95:special['screenSharing']=reference(raw['_screenSharing'])
    if not any(key in b for key in ('  _skinDatas:','  _skinTextures:','  _modelObjs:')):continue
    raw=yaml.safe_load(b)['MonoBehaviour']
    category=raw.get('_categoryType',3 if '_frameMeshFilter' in raw else 2 if '_modelDatas' in raw else 0)
    entry={'category':category,'kind':'common','meshes':refs(raw.get('_meshFilters')), 'renderers':refs(raw.get('_meshRenderers')),
           'material':external(raw.get('_material')),'modelRoot':reference(raw.get('_modelRootObj')),'models':[],'states':[]}
    if '_frameMeshFilter' in raw:
        entry.update(kind='glasses',meshes=refs([raw['_frameMeshFilter']]),renderers=refs([raw['_frameMeshRenderer']]),lens=reference(raw.get('_lensMeshFilter')))
    if '_modelDatas' in raw:
        entry['kind']='headphone'
        for row in pairs(raw['_modelDatas']):entry['models'].append({'id':row['Key'],'objects':refs([row['Value']['ModelObj']]),'renderer':reference(row['Value']['Renderer'])})
    if '_modelObjs' in raw:
        entry['kind']='active'
        for row in pairs(raw['_modelObjs']):entry['models'].append({'id':row['Key'],'objects':refs(row['Value'])})
        for row in pairs(raw.get('_skinObjs')):entry['states'].append({'id':row['Key'],'objects':refs(row['Value'])})
    for row in pairs(raw.get('_skinDatas')):
        value=row['Value'];entry['states'].append({'id':row['Key'],'mesh':external(value.get('Mesh')),'lens':external(value.get('LensMesh')),
            'material':external(value.get('Material')),'texture':external(value.get('MainTex')),'emission':external(value.get('EmissionTex'))})
    for row in pairs(raw.get('_skinTextures')):
        entry['kind']='texture';entry['states'].append({'id':row['Key'],'texture':external(row['Value'])})
    bindings.append(entry)
defaults=[next(s['name'] for s in skins if s['id']==row['SkinType']) for row in master['DecorationSkins'] if row.get('IsCategoryDefault')]
lantern_body=next(b for t,i,b in blocks if t==114 and re.search(r'^  lantern: \{fileID:',b,re.M))
lantern=yaml.safe_load(lantern_body)['MonoBehaviour']['lantern']
output={'skins':skins,'bindings':bindings,'defaultSkins':defaults,'inactiveOnStart':[reference(lantern)],**special}
target=ROOT/'UnityProject/Assets/Prototype/story-scene.json'
target.write_text(json.dumps(output,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
print('Decoration bindings:',len(bindings),'skins:',len(skins),'special:',special)
