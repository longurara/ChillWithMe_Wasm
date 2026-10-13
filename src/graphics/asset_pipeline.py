"""Restore the complete asset library without importing Windows runtime code.

Run after prepare_project.py. Unity builds the catalog as WebGL asset bundles;
the recovered Windows Addressables bundles are never shipped as WebGL data.
"""
from pathlib import Path
import argparse, hashlib, json, re, shutil, struct, sys, tarfile, urllib.request

ROOT = Path(__file__).resolve().parents[2]
TOOLS = ROOT / '.tools'
TMP_SHA256 = '1ce172027b906a30be33cefe7b2ee46e1c8d35f729359b8b9785fc120d57b637'

def guid(path):
    match = re.search(r'^guid: ([a-f0-9]{32})$', Path(str(path)+'.meta').read_text('utf-8'), re.M)
    return match.group(1) if match else None

def copy_assets(source, target, exclude=()):
    target.mkdir(parents=True, exist_ok=True)
    for item in source.rglob('*'):
        if item.relative_to(source).parts[0] in exclude:continue
        if item.is_file():
            out = target / item.relative_to(source)
            out.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(item, out)

def safe_ui_scripts(source, project, tmp):
    sys.path.insert(0,str(ROOT/'research/python'))
    from Crypto.Hash import MD4
    ugui = next((project/'Library/PackageCache').glob('com.unity.ugui@1.0.0'),None)
    if ugui is None: raise ValueError('Import the prepared project before restoring UI assets')
    lookup = {}
    for assembly, directory in [('Unity.TextMeshPro.dll',tmp/'Scripts/Runtime'),('UnityEngine.UI.dll',ugui/'Runtime')]:
        assembly_guid = guid(source/'Plugins'/assembly)
        for item in directory.rglob('*.cs'):
            if not Path(str(item)+'.meta').exists():continue
            text=item.read_text('utf-8-sig')
            namespace=re.search(r'^\s*namespace\s+([\w.]+)',text,re.M)
            if not namespace:continue
            identifier=struct.unpack('<i',MD4.new(('s\0\0\0'+namespace.group(1)+item.stem).encode()).digest()[:4])[0]
            lookup[(assembly_guid,str(identifier))] = guid(item)
    return lookup

def restore_ui_document(text, scripts):
    from prepare_project import split_yaml
    header,blocks=split_yaml(text);removed=set();retained=0;replacement={}
    for typ,identifier,body in blocks:
        if typ!=114:continue
        script=re.search(r'm_Script: \{fileID: (-?\d+), guid: ([a-f0-9]{32}), type: \d+\}',body)
        match=scripts.get((script.group(2),script.group(1))) if script else None
        if match:replacement[identifier]=match;retained+=1
        else:removed.add(identifier)
    out=[header]
    for typ,identifier,body in blocks:
        if identifier in removed:continue
        if identifier in replacement:
            body=re.sub(r'^  m_Script: .*$',f'  m_Script: {{fileID: 11500000, guid: {replacement[identifier]}, type: 3}}',body,flags=re.M)
        body=re.sub(r'^  - component: \{fileID: (-?\d+)\}\n',lambda m:'' if m.group(1) in removed else m.group(0),body,flags=re.M)
        body=re.sub(r'\{fileID: (-?\d+)\}',lambda m:'{fileID: 0}' if m.group(1) in removed else m.group(0),body)
        out.append(f'--- !u!{typ} &{identifier}\n'+body)
    return ''.join(out),retained,len(removed)

def bone_physics(source,project):
    from prepare_project import split_yaml,go_id,field
    from Crypto.Hash import MD4
    import yaml
    _,blocks=split_yaml((source/'App/Scenes/RoomScene.unity').read_text('utf-8'))
    objects={identifier:field(body,'m_Name') for typ,identifier,body in blocks if typ==1}
    transforms={identifier:body for typ,identifier,body in blocks if typ==4}
    by_gameobject={go_id(body):identifier for identifier,body in transforms.items()}
    def hierarchy(identifier):
        body=transforms[identifier];parent=re.search(r'm_Father: \{fileID: (-?\d+)\}',body).group(1)
        return (hierarchy(parent)+'/' if parent in transforms else '')+objects.get(go_id(body),'')
    dll=guid(source/'Plugins/MagicaClothV2.dll')
    types={str(struct.unpack('<i',MD4.new(('s\0\0\0MagicaCloth2'+name).encode()).digest()[:4])[0]):name
           for name in ('MagicaCloth','MagicaSphereCollider','MagicaCapsuleCollider')}
    groups=[];colliders=[]
    for typ,identifier,body in blocks:
        if typ!=114:continue
        reference=re.search(r'm_Script: \{fileID: (-?\d+), guid: ([a-f0-9]{32})',body)
        if not reference or reference.group(2)!=dll:continue
        name=types.get(reference.group(1))
        if name=='MagicaCloth':
            data=yaml.safe_load(body)['MonoBehaviour']['serializeData']
            def curve(value):
                return {'value':value.get('value',0),'useCurve':bool(value.get('useCurve',False)),
                        'keys':[{'time':k['time'],'value':k['value'],'inSlope':k.get('inSlope',0),
                                 'outSlope':k.get('outSlope',0)} for k in value.get('curve',{}).get('m_Curve',[])]}
            roots=re.search(r'    rootBones:\n(.*?)(?=    connectionMode)',body,re.S)
            collider_list=re.search(r'      colliderList:\n(.*?)(?=^      \w)',body,re.S|re.M)
            scalar=lambda pattern,default:float(match.group(1)) if (match:=re.search(pattern,body,re.M)) else default
            groups.append({'name':objects[go_id(body)],'roots':[hierarchy(i) for i in re.findall(r'fileID: (\d+)',roots.group(1))],
                'gravity':scalar(r'^    gravity: ([\d.]+)',5),'damping':scalar(r'^    damping:\n      value: ([\d.]+)',0.2),
                'radius':scalar(r'^    radius:\n      value: ([\d.]+)',0.02),
                'colliders':re.findall(r'fileID: (\d+)',collider_list.group(1)) if collider_list else [],
                'gravityDirection':list(data.get('gravityDirection',{'x':0,'y':-1,'z':0}).values()),
                'dampingCurve':curve(data['damping']),'radiusCurve':curve(data['radius']),
                'angleCurve':curve(data['angleLimitConstraint']['limitAngle']),
                'useAngleLimit':bool(data['angleLimitConstraint']['useAngleLimit']),
                'restorationCurve':curve(data['angleRestorationConstraint']['stiffness']),
                'useRestoration':bool(data['angleRestorationConstraint']['useAngleRestoration']),
                'velocityAttenuation':data['angleRestorationConstraint']['velocityAttenuation'],
                'worldInertia':data['inertiaConstraint']['worldInertia'],
                'localInertia':data['inertiaConstraint']['localInertia'],
                'speedLimit':curve(data['inertiaConstraint']['particleSpeedLimit']),
                'teleportDistance':data['inertiaConstraint']['teleportDistance'],
                'teleportRotation':data['inertiaConstraint']['teleportRotation'],
                'maxDistance':curve(data['motionConstraint']['maxDistance']),
                'useMaxDistance':bool(data['motionConstraint']['useMaxDistance']),
                'sourceConstraints':{key:data[key] for key in ('angleLimitConstraint','angleRestorationConstraint',
                    'inertiaConstraint','motionConstraint','springConstraint','triangleBendingConstraint') if key in data}})
        elif name:
            def vector(key):
                match=re.search(r'^  '+key+r': \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}',body,re.M)
                return [float(x) for x in match.groups()]
            direction=re.search(r'^  direction: (\d+)',body,re.M)
            colliders.append({'id':identifier,'path':hierarchy(by_gameobject[go_id(body)]),'sphere':name=='MagicaSphereCollider',
                'center':vector('center'),'size':vector('size'),'axis':int(direction.group(1)) if direction else 1})
    report={'implementation':'Fixed-step CPU secondary bone rotations; original Magica parameters are a baseline, not solver parity',
            'groups':groups,'colliders':colliders}
    (project/'Assets/Prototype/bone-physics.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    return {'groups':len(groups),'roots':sum(len(g['roots']) for g in groups),'colliders':len(colliders)}

def tmp_sources(project):
    TOOLS.mkdir(parents=True, exist_ok=True)
    archive = TOOLS / 'textmeshpro-3.0.6.tgz'
    if not archive.exists():
        urllib.request.urlretrieve('https://download.packages.unity.com/com.unity.textmeshpro/-/com.unity.textmeshpro-3.0.6.tgz', archive)
    if hashlib.sha256(archive.read_bytes()).hexdigest() != TMP_SHA256:
        raise ValueError('TextMeshPro archive hash mismatch')
    unpacked = (TOOLS / 'textmeshpro-3.0.6').resolve()
    with tarfile.open(archive) as tar:
        for member in tar.getmembers():
            if not member.isfile(): continue
            out = (unpacked / member.name).resolve()
            if not out.is_relative_to(unpacked): raise ValueError('Unsafe package member')
            out.parent.mkdir(parents=True, exist_ok=True)
            out.write_bytes(tar.extractfile(member).read())
    package = unpacked / 'package'
    # Preserve the package's original source and license. No compiled DLL from
    # the Windows game's Plugins directory is reused.
    destination=project/'Packages/com.unity.textmeshpro'
    old_tests=destination/'Tests'
    if old_tests.exists():
        resolved=old_tests.resolve()
        if old_tests.is_symlink() or not resolved.is_relative_to(destination.resolve()) or resolved.name!='Tests':
            raise ValueError('Unsafe generated test-directory cleanup')
        shutil.rmtree(resolved)
    copy_assets(package,destination,exclude=('Tests','Tests.meta'))
    resource = package / 'Package Resources/TMP Essential Resources.unitypackage'
    with tarfile.open(resource) as tar:
        members = {m.name: m for m in tar.getmembers() if m.isfile()}
        for name in members:
            if not name.endswith('/pathname'): continue
            path = tar.extractfile(members[name]).read().decode('utf-8').strip()
            if not path.startswith('Assets/TextMesh Pro/Shaders/'): continue
            out = (project / path).resolve()
            if not out.is_relative_to(project.resolve()): raise ValueError('Unsafe resource member')
            asset = name.rsplit('/',1)[0] + '/asset'
            meta = name.rsplit('/',1)[0] + '/asset.meta'
            if asset not in members: continue
            out.parent.mkdir(parents=True, exist_ok=True)
            out.write_bytes(tar.extractfile(members[asset]).read())
            if meta in members: Path(str(out)+'.meta').write_bytes(tar.extractfile(members[meta]).read())
    shaders = {}
    for path in (project/'Assets/TextMesh Pro/Shaders').glob('*.shader'):
        match = re.search(r'Shader\s+"([^"]+)"',path.read_text('utf-8-sig'))
        if match: shaders[match.group(1)] = guid(path)
    return package, shaders

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--project',type=Path,default=ROOT/'UnityProject')
    parser.add_argument('--recovered',type=Path,default=ROOT/'Recovered/ExportedProject')
    args = parser.parse_args(); project = args.project.resolve(); source = args.recovered.resolve()/'Assets'
    if not (project/'Assets/Prototype/Room.unity').is_file(): raise ValueError('Prepare the graphics project first')
    package, shaders = tmp_sources(project)
    scripts = safe_ui_scripts(source,project,package)
    physics=bone_physics(source,project)
    for kind in ('AudioClip','AudioMixerController','TextAsset'):
        copy_assets(source/kind,project/'Assets/Recovered'/kind)
    fonts = []
    data_dir = project/'Assets/RecoveredData'; data_dir.mkdir(parents=True,exist_ok=True)
    data = []
    ui_stats={'retainedComponents':0,'removedGameplayComponents':0,'documents':0}
    for kind in ('GameObject','App/Scenes'):
        for item in (source/kind).rglob('*'):
            if item.suffix not in ('.prefab','.unity'):continue
            text,retained,removed=restore_ui_document(item.read_text('utf-8-sig'),scripts)
            out=project/'Assets/Recovered'/kind/item.relative_to(source/kind)
            out.parent.mkdir(parents=True,exist_ok=True);out.write_text(text,encoding='utf-8')
            shutil.copy2(Path(str(item)+'.meta'),Path(str(out)+'.meta'))
            ui_stats['retainedComponents']+=retained;ui_stats['removedGameplayComponents']+=removed;ui_stats['documents']+=1
    for item in list((source/'MonoBehaviour').glob('*.asset'))+list((source/'Resources').glob('*.asset')):
        text = item.read_text('utf-8-sig')
        reference=re.search(r'm_Script: \{fileID: (-?\d+), guid: ([a-f0-9]{32}), type: \d+\}',text)
        safe_guid=scripts.get((reference.group(2),reference.group(1))) if reference else None
        script = ('TMP_FontAsset' if '  m_AtlasTextures:' in text else
                  'TMP_SpriteAsset' if '  m_SpriteCharacterTable:' in text else None)
        if script or safe_guid:
            script_guid = guid(package/('Scripts/Runtime/'+script+'.cs')) if script else safe_guid
            text = re.sub(r'^  m_Script: .*$',f'  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}',text,flags=re.M)
            out = project/'Assets/Recovered'/item.parent.name/item.name;out.parent.mkdir(parents=True,exist_ok=True)
            out.write_text(text,encoding='utf-8');shutil.copy2(Path(str(item)+'.meta'),Path(str(out)+'.meta'))
            fonts.append({'path':out.relative_to(project).as_posix(),'type':script or 'UISettings','guid':guid(item)})
        else:
            # Keep master/scenario settings and their GUID links inspectable as
            # structured text. Their original gameplay classes are a separate
            # port stage; do not create ScriptableObjects with missing scripts.
            out = data_dir/item.parent.name/(item.stem+'.yaml.txt');out.parent.mkdir(parents=True,exist_ok=True);out.write_text(text,encoding='utf-8')
            data.append({'path':out.relative_to(project).as_posix(),'guid':guid(item),'name':item.stem})
    extra_assets=[]
    for item in (source/'Resources').rglob('*'):
        if not item.is_file() or item.suffix in ('.meta','.asset','.shader'):continue
        out=project/'Assets/Recovered/ResourceAssets'/item.relative_to(source/'Resources')
        out.parent.mkdir(parents=True,exist_ok=True)
        if item.suffix=='.prefab':
            text,retained,removed=restore_ui_document(item.read_text('utf-8-sig'),scripts)
            out.write_text(text,encoding='utf-8');ui_stats['retainedComponents']+=retained;ui_stats['removedGameplayComponents']+=removed;ui_stats['documents']+=1
        else:shutil.copy2(item,out)
        shutil.copy2(Path(str(item)+'.meta'),Path(str(out)+'.meta'))
        extra_assets.append({'path':out.relative_to(project).as_posix(),'name':item.stem,'category':'resource','guid':guid(item),'sourceBytes':item.stat().st_size})
    for item in (source/'MonoBehaviour').glob('*.playable'):
        out=data_dir/'Timeline'/(item.name+'.yaml.txt');out.parent.mkdir(parents=True,exist_ok=True);out.write_text(item.read_text('utf-8-sig'),encoding='utf-8')
        data.append({'path':out.relative_to(project).as_posix(),'guid':guid(item),'name':item.stem})
    settings=project/'Assets/Recovered/Resources/TMP Settings.asset'
    if settings.exists():
        text=settings.read_text('utf-8')
        base_font=next((f for f in fonts if Path(f['path']).stem=='Base_JP'),None)
        if base_font:text=text.replace('m_defaultFontAsset: {fileID: 0}',f"m_defaultFontAsset: {{fileID: 11400000, guid: {base_font['guid']}, type: 2}}")
        active_settings=project/'Assets/Resources/TMP Settings.asset';active_settings.parent.mkdir(parents=True,exist_ok=True)
        active_settings.write_text(text,encoding='utf-8')
        # Unity Resources needs exactly one settings object. Move the owned
        # generated copy with its GUID instead of creating a duplicate GUID.
        shutil.move(str(settings)+'.meta',str(active_settings)+'.meta');settings.unlink()
        for f in fonts:
            if f['path']=='Assets/Recovered/Resources/TMP Settings.asset':f['path']='Assets/Resources/TMP Settings.asset'
    mappings = []
    report = json.loads((project/'prototype-preparation.json').read_text('utf-8'))
    for mapping in report['materials']:
        name = mapping['originalShader']
        if name in shaders:
            item = project/'Assets/Recovered/Material'/mapping['material']
            text = (source/'Material'/mapping['material']).read_text('utf-8')
            text = re.sub(r'^  m_Shader: .*$',f'  m_Shader: {{fileID: 4800000, guid: {shaders[name]}, type: 3}}',text,flags=re.M)
            item.write_text(text,encoding='utf-8')
            mapping.update(shader=name,method='source-restored')
            mappings.append(mapping['material'])
    (project/'prototype-preparation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    metadata_path = ROOT/'research/extra-metadata.json'
    metadata = json.loads(metadata_path.read_text('utf-8')) if metadata_path.exists() else {}
    music_names = {x['name'] for b in metadata.get('bundles',[]) if b['name'].startswith('audio_assets') for x in b['audio']}
    voice_names = {x['name'] for b in metadata.get('bundles',[]) if b['name'].startswith('voice_assets') for x in b['audio']}
    entries = []
    for kind in ('AudioClip','AnimationClip','Mesh','Material','Texture2D','Sprite','Font','GameObject','Avatar','AnimatorController','Cubemap','Flare','TerrainLayer','AudioMixerController','RenderTexture','TextAsset'):
        for item in sorted((project/'Assets/Recovered'/kind).rglob('*')):
            if not item.is_file() or item.suffix=='.meta': continue
            category = kind
            if kind=='AudioClip':
                category = 'voice' if item.stem in voice_names else 'music' if item.stem in music_names else 'sound'
                meta=Path(str(item)+'.meta');text=meta.read_text('utf-8')
                match=re.search(r'(  defaultSettings:\n)(.*?)(?=  platformSettingOverrides:)',text,re.S)
                if match:
                    settings=match.group(2)
                    for key,value in [('loadType','1'),('quality','0.8' if category=='music' else '0.7'),('preloadAudioData','0')]:
                        settings=re.sub(r'^    '+key+r': .*$',f'    {key}: {value}',settings,flags=re.M)
                    meta.write_text(text[:match.start(2)]+settings+text[match.end(2):],encoding='utf-8')
            entries.append({'path':item.relative_to(project).as_posix(),'name':item.stem,'category':category,'guid':guid(item),'sourceBytes':item.stat().st_size})
    entries.extend({'path':f['path'],'name':Path(f['path']).stem,'category':'fontAsset','guid':f['guid'],'sourceBytes':(project/f['path']).stat().st_size} for f in fonts)
    entries.extend(extra_assets)
    catalog = {'version':1,'platform':'WebGL','assets':entries,'data':data,
               'restoration':{'tmpVersion':'3.0.6','tmpSourceMaterials':len(mappings),'fontAssets':len(fonts),'ui':ui_stats,'bonePhysics':physics},
               'excludedNative':{'plugins':'Windows DLLs','computeShaders':'WebGL has no compute shader backend','windowsAddressables':'rebuilt for WebGL'}}
    (project/'Assets/Prototype/asset-catalog-source.json').write_text(json.dumps(catalog,indent=2,ensure_ascii=False),encoding='utf-8')
    print(json.dumps({'assets':len(entries),'audio':sum(x['category'] in ('voice','music','sound') for x in entries),'fontAssets':len(fonts),'dataRecords':len(data),'restoredTMPMaterials':len(mappings),'ui':ui_stats},indent=2))

if __name__ == '__main__': main()
