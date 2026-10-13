"""Prepare a graphics-only Unity project from an AssetRipper export.

The Windows game and the recovered export are read-only inputs. Generated
assets stay in this standalone workspace.
"""
from pathlib import Path
import argparse, collections, hashlib, json, re, shutil

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_RECOVERED = ROOT / 'Recovered/ExportedProject'
TOOLS = ROOT / '.tools'
TEMPLATE = Path(__file__).resolve().parent

def meta_guid(path):
    p = Path(str(path) + '.meta')
    if not p.exists(): return None
    m = re.search(r'^guid: ([a-f0-9]{32})$', p.read_text('utf-8'), re.M)
    return m.group(1) if m else None

def shader_index(root):
    result = {}
    for p in root.rglob('*.shader'):
        m = re.search(r'\bShader\s+"([^"]+)"', p.read_text('utf-8-sig'))
        if m and meta_guid(p): result[m.group(1)] = meta_guid(p)
    return result

def copy_tree(source, target, exclude=()):
    target.mkdir(parents=True, exist_ok=True)
    for p in source.iterdir():
        if p.name in exclude or p.name.endswith('~') or p.name.startswith('.'):
            continue
        out = target / p.name
        if p.is_dir(): copy_tree(p, out, exclude)
        else: shutil.copy2(p, out)

def split_yaml(text):
    parts = re.split(r'^--- !u!(\d+) &(-?\d+)\n', text, flags=re.M)
    return parts[0], [(int(parts[i]), parts[i+1], parts[i+2]) for i in range(1, len(parts), 3)]

def field(text, name, default=''):
    m = re.search(r'^  '+re.escape(name)+r': (.*)$', text, re.M)
    return m.group(1).strip().strip("'") if m else default

def go_id(body):
    m = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', body)
    return m.group(1) if m else '0'

def make_scene(source, target, source_assets, lighting_preset='day'):
    header, blocks = split_yaml(source.read_text('utf-8-sig'))
    removed = {i for t,i,b in blocks if t == 114}
    objects = {i:field(b, 'm_Name') for t,i,b in blocks if t == 1}
    transforms = {go_id(b):(i,b) for t,i,b in blocks if t in [4,224]}
    transform_go = {i:g for g,(i,b) in transforms.items()}
    renderers = {i:go_id(b) for t,i,b in blocks if t in [23,137]}
    script_ids = {}
    for t,i,b in blocks:
        if t != 114: continue
        sid = re.search(r'^  scriptId: ([a-f0-9-]+)$',b,re.M)
        rid = re.search(r'^  objectRenderer: \{fileID: (\d+)\}',b,re.M)
        if sid and rid: script_ids[sid.group(1)] = rid.group(1)
    def hierarchy(g):
        if g not in transforms: return objects.get(g,'')
        parent = re.search(r'm_Father: \{fileID: (-?\d+)\}',transforms[g][1]).group(1)
        pg = transform_go.get(parent)
        return (hierarchy(pg)+'/' if pg else '') + objects.get(g,'')
    target.parent.mkdir(parents=True,exist_ok=True)
    output = [header]
    for t,i,b in blocks:
        if t == 114: continue
        if t == 1:
            b = re.sub(r'^  - component: \{fileID: (-?\d+)\}\n',
                       lambda m:'' if m.group(1) in removed else m.group(0),b,flags=re.M)
        # Remove references to stripped local script components, including
        # animation callbacks; external asset references are left untouched.
        b = re.sub(r'\{fileID: (-?\d+)\}',lambda m:'{fileID: 0}' if m.group(1) in removed else m.group(0),b)
        output.append(f'--- !u!{t} &{i}\n'+b)
    target.write_text(''.join(output),encoding='utf-8')
    shutil.copy2(Path(str(source)+'.meta'),Path(str(target)+'.meta'))
    # Recover the day preset from the original lightmap switcher, without
    # keeping its native/third-party scripting dependencies in the prototype.
    day = (source_assets/f'MonoBehaviour/RoomScene_{lighting_preset}.asset').read_text('utf-8')
    lighting = {'textures':[], 'renderers':[]}
    lookup = {meta_guid(p):'Assets/Recovered/'+p.relative_to(source_assets).as_posix()
              for p in (source_assets/'Texture2D').iterdir() if not p.name.endswith('.meta')}
    maps = re.search(r'^    lightmapsLight:\n(.*?)(?=^    [a-zA-Z])',day,re.M|re.S)
    if maps:
        lighting['textures'] = [lookup[g] for g in re.findall(r'guid: ([a-f0-9]{32})',maps.group(1)) if g in lookup]
    rows = re.search(r'^    rendererDatas:\n(.*?)(?=^    [a-zA-Z])',day,re.M|re.S)
    if rows:
        for row in re.split(r'^    - rendererShaderProperties:',rows.group(1),flags=re.M)[1:]:
            sid = re.search(r'objectId: ([a-f0-9-]+)',row)
            index = re.search(r'lightmapIndex: (\d+)',row)
            offset = re.search(r'lightmapScaleOffset: \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}',row)
            if not (sid and index and offset): continue
            rid = script_ids.get(sid.group(1)); idx = int(index.group(1))
            if rid in renderers and idx < len(lighting['textures']):
                lighting['renderers'].append({'path':hierarchy(renderers[rid]),'index':idx,'offset':[float(x) for x in offset.groups()]})
    return {'strippedBehaviours':len(removed),'gameObjects':len(objects),'rendererBindings':len(lighting['renderers'])},lighting

def restore_material(path, original_shaders, available):
    text = path.read_text('utf-8'); m = re.search(r'm_Shader: \{fileID: [^,]+, guid: ([a-f0-9]{32}), type: \d+\}',text)
    if not m: return None
    name = original_shaders.get(m.group(1))
    if name in available:
        replacement = name; method = 'source-restored'
    elif name in ['Unlit/Texture','Unlit/Color','UI/Unlit/Transparent']:
        replacement = 'Universal Render Pipeline/Unlit'; method = 'standard-remap'
    elif name == 'Particles/Standard Unlit':
        replacement = 'Universal Render Pipeline/Particles/Unlit'; method = 'standard-remap'
    else:
        replacement = 'Universal Render Pipeline/Unlit'; method = 'prototype-approximation'
    text = text[:m.start()] + f'm_Shader: {{fileID: 4800000, guid: {available[replacement]}, type: 3}}' + text[m.end():]
    if replacement in ['Universal Render Pipeline/Unlit','Universal Render Pipeline/Particles/Unlit'] and replacement != name:
        textures = re.findall(r'^      (\w+):\n(        m_Texture: \{fileID: [^\n]+\n        m_Scale: [^\n]+\n        m_Offset: [^\n]+\n)',text,re.M)
        candidates = sorted(textures,key=lambda x:0 if x[0] in ['_BaseMap','_MainTex'] else 1)
        best = next((v for k,v in candidates if 'm_Texture: {fileID: 0}' not in v),None)
        if best:
            if any(k=='_BaseMap' for k,v in textures):
                text = re.sub(r'^      _BaseMap:\n        m_Texture: [^\n]+\n        m_Scale: [^\n]+\n        m_Offset: [^\n]+\n','      _BaseMap:\n'+best,text,flags=re.M)
            else: text = text.replace('    m_TexEnvs:\n','    m_TexEnvs:\n      _BaseMap:\n'+best,1)
        color = re.search(r'^      _Color: (\{[^\n]+\})$',text,re.M)
        if color and not re.search(r'^      _BaseColor:',text,re.M):
            text = text.replace('    m_Colors:\n','    m_Colors:\n      _BaseColor: '+color.group(1)+'\n',1)
        queue = int(field(text,'m_CustomRenderQueue','-1'))
        zwrite = re.search(r'^      _ZWrite: ([^\n]+)',text,re.M)
        transparent = (any(s in (name or '') for s in ['Alpha','Transparent','Fog','Cloud','Raindrop'])
                       or queue >= 2500 or (zwrite and float(zwrite.group(1)) == 0))
        if transparent:
            text = re.sub(r'^  m_CustomRenderQueue: .*$', '  m_CustomRenderQueue: 3000',text,flags=re.M)
            for key,val in [('_Surface',1),('_Blend',0),('_SrcBlend',5),('_DstBlend',10),('_ZWrite',0)]:
                if re.search(r'^      '+key+':',text,re.M):text = re.sub(r'^      '+key+r': .*$',f'      {key}: {val}',text,flags=re.M)
                else:text = text.replace('    m_Floats:\n',f'    m_Floats:\n      {key}: {val}\n',1)
            text = text.replace('  m_ValidKeywords: []','  m_ValidKeywords:\n  - _SURFACE_TYPE_TRANSPARENT')
    path.write_text(text,encoding='utf-8')
    return {'material':path.name,'originalShader':name,'shader':replacement,'method':method}

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--recovered',type=Path,default=DEFAULT_RECOVERED)
    parser.add_argument('--project',type=Path,default=ROOT/'UnityProject')
    args=parser.parse_args(); recovered=args.recovered.resolve(); project=args.project.resolve()
    if recovered == project or recovered in project.parents or project in recovered.parents:
        raise ValueError('Use separate recovered and prototype directories.')
    source=recovered/'Assets'; target=project/'Assets/Recovered'
    if not (source/'App/Scenes/RoomScene.unity').is_file():raise FileNotFoundError('AssetRipper export is not ready.')
    project.mkdir(parents=True,exist_ok=True)
    for name in ['AnimationClip','AnimatorController','Avatar','Cubemap','Flare','Font','GameObject','Material','Mesh','RenderTexture','Sprite','TerrainLayer','Texture2D']:
        copy_tree(source/name,target/name)
    copy_tree(recovered/'ProjectSettings',project/'ProjectSettings')
    (project/'Assets/Prototype').mkdir(parents=True,exist_ok=True)
    (target/'AudioClip').mkdir(parents=True,exist_ok=True)
    for suffix in ['', '.meta']:
        shutil.copy2(source/('AudioClip/aqua room.ogg'+suffix),target/('AudioClip/aqua room.ogg'+suffix))
    scene,lighting=make_scene(source/'App/Scenes/RoomScene.unity',target/'App/Scenes/RoomScene.unity',source)
    copy_tree(source/'App/Scenes/RoomScene',target/'App/Scenes/RoomScene')
    (project/'Assets/Prototype/day-lighting.json').write_text(json.dumps(lighting,indent=2),encoding='utf-8')
    # Other recovered prefabs and controllers stay inspectable without bringing
    # back game startup scripts or state-machine code into this first scene.
    for p in (target/'GameObject').rglob('*.prefab'):
        h,bs=split_yaml(p.read_text('utf-8'));removed={i for t,i,b in bs if t==114};out=[h]
        for t,i,b in bs:
            if t==114:continue
            b=re.sub(r'^  - component: \{fileID: (-?\d+)\}\n',lambda m:'' if m.group(1) in removed else m.group(0),b,flags=re.M)
            b=re.sub(r'\{fileID: (-?\d+)\}',lambda m:'{fileID: 0}' if m.group(1) in removed else m.group(0),b)
            out.append(f'--- !u!{t} &{i}\n'+b)
        p.write_text(''.join(out),encoding='utf-8')
    for p in (target/'AnimatorController').rglob('*.controller'):
        text=p.read_text('utf-8')
        text=re.sub(r'^  m_StateMachineBehaviours:\n(?:  -[^\n]*\n)+','  m_StateMachineBehaviours: []\n',text,flags=re.M)
        p.write_text(text,encoding='utf-8')
    lil=TOOLS/'lilToon-1.7.3/lilToon-b96470d3dd9092b840052578048b2307fe6d8786/Assets/lilToon'
    copy_tree(lil,project/'Assets/lilToon')
    packages=['com.unity.render-pipelines.core','com.unity.render-pipelines.universal','com.unity.shadergraph','com.unity.render-pipelines.universal-config']
    editor_packages = Path(r'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Data\Resources\PackageManager\BuiltInPackages')
    if not editor_packages.is_dir():
        raise FileNotFoundError('Install Unity 2022.3.62f2 before preparing its matching render-pipeline sources.')
    for name in packages:
        destination = project/'Packages'/name
        # A previous staging-source snapshot can contain newer files than the
        # installed Editor. Replace only these owned, generated package dirs.
        if destination.exists():
            resolved = destination.resolve()
            if destination.is_symlink() or not resolved.is_relative_to((project/'Packages').resolve()) or resolved.name not in packages:
                raise ValueError('Unsafe package replacement path: '+str(resolved))
            shutil.rmtree(resolved)
        copy_tree(editor_packages/name,destination,exclude=('Tests','Tests.meta'))
    manifest=json.loads((recovered/'Packages/manifest.json').read_text('utf-8'))
    manifest['dependencies'].update({'com.unity.ugui':'1.0.0','com.unity.mathematics':'1.2.1','com.unity.burst':'1.8.9','com.unity.searcher':'4.9.2'})
    (project/'Packages/manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    available=shader_index(project/'Packages');available.update(shader_index(project/'Assets/lilToon'))
    original={meta_guid(p):name for p in (source/'Shader').glob('*.shader')
              if (m:=re.search(r'\bShader\s+"([^"]+)"',p.read_text('utf-8-sig'))) and (name:=m.group(1))}
    mappings=[r for p in (target/'Material').glob('*.mat') if (r:=restore_material(p,original,available))]
    weather={'materials':['Assets/Recovered/Material/'+r['material'] for r in mappings
                          if (r['originalShader'] or '').startswith('Distant Lands/Cozy/') and r['method']=='prototype-approximation']}
    (project/'Assets/Prototype/simplified-weather.json').write_text(json.dumps(weather,indent=2),encoding='utf-8')
    copy_tree(TEMPLATE/'Runtime',project/'Assets/Prototype/Runtime')
    copy_tree(TEMPLATE/'Editor',project/'Assets/Prototype/Editor')
    report={'project':str(project),'recovered':str(recovered),'unity':'2022.3.62f2',
            'lilToon':{'version':'1.7.3','commit':'b96470d3dd9092b840052578048b2307fe6d8786'},
            'graphicsSource':'Unity 2022.3.62f2 installed BuiltInPackages',
            'stage':'prepared-not-imported','scene':scene,'materials':mappings,
            'counts':dict(collections.Counter(r['method'] for r in mappings)),
            'copiedAssets':sum(1 for p in target.rglob('*') if p.is_file() and not p.name.endswith('.meta'))}
    (project/'prototype-preparation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    shutil.copy2(TEMPLATE/'README.md',project/'README.md')
    print(json.dumps({k:v for k,v in report.items() if k!='materials'},indent=2),flush=True)

if __name__=='__main__':main()
