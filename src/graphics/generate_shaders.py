"""WebGL implementations for custom shaders whose Windows sources are absent.

Property names, textures, blend modes and queues come from recovered metadata.
The HLSL below is newly written effect behavior, not recovered original graphs.
The catalog explicitly distinguishes this work from source-restored shaders.
"""
from pathlib import Path
import collections, hashlib, json, re
from asset_pipeline import ROOT, guid

TEMPLATE = '''Shader "@NAME@" {
Properties {
@PROPERTIES@
}
SubShader {
Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="@TYPE@" "Queue"="@QUEUE@" }
Pass {
Name "WebForward"
Tags { "LightMode"="UniversalForward" }
Blend @SRC@ @DST@
ZWrite @ZWRITE@
Cull @CULL@
HLSLPROGRAM
#pragma target 3.0
#pragma vertex HlcVertex
#pragma fragment HlcFragment
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
@TEXTURES@
CBUFFER_START(UnityPerMaterial)
@UNIFORMS@
CBUFFER_END
float HlcHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
float HlcNoise(float2 p) {
 float2 i=floor(p), f=frac(p); f=f*f*(3.0-2.0*f);
 return lerp(lerp(HlcHash(i),HlcHash(i+float2(1,0)),f.x),lerp(HlcHash(i+float2(0,1)),HlcHash(i+1),f.x),f.y);
}
struct HlcInput { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; };
struct HlcOutput { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; float3 normalWS:TEXCOORD2; float4 color:COLOR; };
HlcOutput HlcVertex(HlcInput input) {
 HlcOutput output; float3 p=input.positionOS.xyz;
 @VERTEX@
 output.positionWS=TransformObjectToWorld(p);
 output.positionCS=TransformWorldToHClip(output.positionWS);
 output.normalWS=TransformObjectToWorldNormal(input.normalOS);
 output.uv=input.uv; output.color=input.color;
 return output;
}
half4 HlcFragment(HlcOutput input):SV_Target {
 float2 uv=input.uv;
 @UV@
 half4 color=@SAMPLE@;
 @FRAGMENT@
 return color;
}
ENDHLSL
}
}
}
'''

def properties(source):
    text=source.read_text('utf-8-sig')
    block=re.search(r'Properties\s*\{(.*?)\n\s*\}\s*//DummyShaderTextExporter',text,re.S)
    if not block: raise ValueError('Missing recovered property declarations: '+str(source))
    result={}
    for line in block.group(1).splitlines():
        item=re.search(r'(\w+)\s+\("[^"\n]*",\s*(.+)\)\s*=\s*(.*)',line)
        if item:result[item.group(1)]={'type':item.group(2),'default':item.group(3),'line':line.strip()}
    return block.group(1).strip(),result

def behavior(name,fields):
    f=lambda n,d='1.0': n if n in fields else d
    textures={n for n,v in fields.items() if v['type']=='2D'}
    def sample(n,uv='uv',fallback='half4(1,1,1,1)'):
        return f'SAMPLE_TEXTURE2D({n},sampler{n},{uv})' if n in textures else fallback
    primary=next((n for n in ['_MainTex','_Texture','_MainTexture','_GrassTexture','_RenderTexture','_Texture2D'] if n in textures),None)
    if primary is None:primary=next((n for n in textures if not n.startswith('unity_') and n not in ['_Emission','_Normals','_texcoord']),None)
    vertex=[];uv=[];fragment=[];sampling=sample(primary) if primary else 'half4(1,1,1,1)'
    if '_Color' in fields:fragment.append('color *= _Color;')
    if '_HDR' in fields:fragment.append('color.rgb *= _HDR.rgb;')
    if '_SunColorMultiply' in fields:fragment.append('color.rgb *= lerp(half3(1,1,1),GetMainLight().color,saturate(_SunColorMultiply));')
    if '_Emission' in textures:fragment.append('color.rgb += '+sample('_Emission')+'.rgb;')
    if name=='Bakery/Light':fragment.append('color.rgb *= intensity;')
    if 'BirdShader' in name:
        vertex.append('p.y += sin(_Time.y*_FlapSpeed + abs(p.x)*_FlapOffset)*abs(p.x)*_FlapScale; p.z += cos(_Time.y*_FlapSpeed)*_Recoil;')
    elif 'BookShader' in name:
        vertex.append('float a=sin(_Time.y*_FlipSpeed)*0.7; float edge=saturate(abs(p.x)/max(abs(_PageWidth),0.001)); p.y+=sin(a)*abs(p.x)*edge; p.x*=lerp(1,cos(a),edge);')
    elif 'JellyfishShader' in name:
        vertex.append('float pulse=sin(_Time.y*_Speed + p.y*_Split); p.xz*=1+pulse*_Scale; p.y+=pulse*_Scale*0.25;')
    elif 'FishShader' in name:
        vertex.append('p.x += sin(_Time.y*2.0+p.z*4.0)*p.z*0.025;')
    elif 'Stylized Butterfly' in name:
        vertex.append('p.y += sin(_Time.y*_SwaySpeed)*abs(p.x)*_SwayAmount;')
        fragment.append('color *= _MainColor; clip(color.a-max(_AlphaClipThreshold,0.001));')
    elif 'Stlylized Leaves' in name:
        vertex.append('float wind=sin(_Time.y*(1+_FlutterSpeed)+p.x*_WindNoiseSize+p.z*_FlutterScale); p.x+=wind*_WindStrength*0.02; p.z+=sin(_Time.y+p.y)*_FlutterAmount*0.002;')
        fragment.append('color.rgb *= lerp(_BottomColor.rgb,_TopColor.rgb,saturate(uv.y+_GradientOffset)); color.rgb *= _FilterColor.rgb; if(_AlphaClip>0.5) clip(color.a-_AlphaCutoff);')
    elif 'Stylized Grass' in name:
        vertex.append('p.xz += _WindStrength.xz*sin(_Time.y*_WindSpeed+p.x*_WindScale)*saturate(input.uv.y)*0.03;')
        fragment.append('color.rgb *= lerp(_BottomColor.rgb,_TopColor.rgb,saturate(uv.y*_GradientAmount)); if(_AlphaClip>0.5) clip(color.a-_AlphaCutoff);')
    elif 'Dynamic Coral' in name:
        vertex.append('float wave=sin(_Time.y*_WaveSpeed+p.y*_MainWaveScale); float flutter=sin(_Time.y*_FlutterSpeed+p.x*_FlutterScale); p.xz+=(wave*_MainWaveAmount+flutter*_FlutterAmount)*saturate(input.uv.y)*0.08;')
        fragment.append('color.rgb=lerp(_MainColor.rgb,_TopColor.rgb,saturate(uv.y+_GradientOffset1))+_Emmision.rgb;')
    elif name.endswith('/Aurora'):
        fragment.append('float n=HlcNoise(float2(uv.x*_NoiseScale+_Time.y*_TimeScale,uv.y*_Scale)); float band=saturate((n-_Contrast)*3.0); color.rgb=lerp(_BottomColor.rgb,_TopColor.rgb,saturate(uv.y)); color.a=band*sin(saturate(uv.y)*3.14159265);')
    elif 'Stylized Sky' in name:
        fragment.append('float height=saturate(normalize(input.positionWS-_WorldSpaceCameraPos).y); color=half4(lerp(_HlcHorizon.rgb,_HlcZenith.rgb,pow(height,0.55)),1);')
    elif 'Stylized Clouds' in name:
        fragment.append('float n=HlcNoise(uv*8+float2(_Time.y*0.015,0)); n=n*0.7+HlcNoise(uv*19-float2(_Time.y*0.008,0))*0.3; color=half4(_HlcCloudTint.rgb,smoothstep(1-_HlcCoverage,1-_HlcCoverage+0.15,n)*_HlcCloudTint.a);')
    elif 'Stylized Fog' in name:
        sampling=sample('_FogVariationTexture','uv+float2(_Time.y*0.007,0)')
        fragment.append('float distanceFade=saturate(distance(input.positionWS,_WorldSpaceCameraPos)/80.0); color=half4(_HlcFogTint.rgb,color.r*distanceFade*_HlcFogDensity);')
    elif 'Disabled Shader' in name or 'Fog Culling' in name:
        fragment.append('clip(-1);')
    elif 'KawaseBlur' in name:
        step='float2(_HlcBlurRadius/max(_ScreenParams.x,1),_HlcBlurRadius/max(_ScreenParams.y,1))'
        sampling='('+sample('_MainTex','uv+'+step)+'+'+sample('_MainTex','uv-'+step)+'+'+sample('_MainTex','uv+float2(1,-1)*'+step)+'+'+sample('_MainTex','uv+float2(-1,1)*'+step)+')*0.25'
    elif 'WetSurfaceURP' in name:
        uv.append('float n=HlcNoise(uv*32+float2(0,-_Time.y)); uv+=float2(sin(n*6.28),cos(n*6.28))*_BumpAmt*0.0004;')
        fragment.append('color.rgb=lerp(color.rgb,_DropTint.rgb,saturate(_TintAmt)); color.rgb=lerp(color.rgb,_FogCol.rgb,saturate(_FogAmt)); half3 reflected=SAMPLE_TEXTURECUBE(_Cube,sampler_Cube,reflect(normalize(input.positionWS-_WorldSpaceCameraPos),normalize(input.normalWS))).rgb; color.rgb=lerp(color.rgb,reflected,saturate(_Reflect)); color.a=0.35;')
    elif name.endswith('/Wiper'):
        fragment.append('float d=distance(uv,_HlcWipeCenter.xy); color=half4(1,1,1,1-smoothstep(_HlcWipeRadius,_HlcWipeRadius+0.02,d));')
    elif 'Stylized Moon' in name:
        fragment.append('color.a=saturate(color.a*_AlphaScale+_AlphaOffset);')
    elif name.endswith('/EnvUnlit_ColorLinear_Space'):
        fragment.append('float3 tint=_Env<0.5?_Day.rgb:(_Env<1.5?_Evening.rgb:(_Env<2.5?_Night.rgb:_Cloud.rgb)); color.rgb*=tint; color.a *= '+sample('_AlphaMask')+'.r; color.rgb=lerp(color.rgb,tint,saturate(_Foggy));')
    elif 'FurinShader' in name:
        fragment.append('half4 second='+sample('_MainTex2')+'; float mask=lerp(color.a,color.b,step(0.5,_IsUseBlueChannel)); clip(mask-0.05); color.rgb=lerp(color.rgb,second.rgb,step(0.5,_IsUseBlueChannel)); color.a=1;')
    elif 'RaindropShader' in name:
        uv.append('uv=uv*_Tiling.xy+float2(0,_Time.y*_DropSpeed);')
        sampling=sample('_Drop')
        fragment.append('float splash='+sample('_Suputter','frac(input.uv*_Tiling.xy+_Time.y*_SuputterSpeed)')+'.r; color.rgb=half3(0.45,0.6,0.75)*(color.r+splash*0.25); color.a=saturate(color.r+splash*0.25)*0.35;')
    elif 'WindowTransition' in name:
        mask=next((n for n in textures if n.startswith('_SampleTexture')),None)
        fragment=['float mask='+sample(mask,'uv*_Tilling.xy')+'.r; clip(_Threshold-mask-0.00001); color=_Color; color.a=1;']
    elif 'TreeShader' in name:
        uv.append('float hlcRow=floor(uv.y*_DotScale); float glitch=step(1-_Ratio,HlcHash(float2(hlcRow,floor(_Time.y*_Speed)))); uv.x+=(HlcHash(float2(hlcRow,_Time.y))-.5)*_Length*glitch;')
        fragment.append('color.a*=1-saturate(_Fade); color.rgb*=lerp(0.35,1,_SunlightScaler); clip(color.a-0.01);')
    elif any(key in name for key in ['ScreenGlitch','ScreenColorGlitch','ScreenMoveGlitch','NoiseShader']):
        ratio=f('_Ratio','0.0'); speed=f('_Speed','12.0'); scale=f('_DotScale','32.0'); length=f('_Length','0.08')
        uv.append(f'float row=floor(uv.y*{scale}); float tick=floor(_Time.y*{speed}); float jitter=step(1-saturate({ratio}),HlcHash(float2(row,tick))); uv.x+=(HlcHash(float2(row+3,tick))-.5)*{length}*jitter;')
        if 'ScreenMoveGlitch' in name:
            flow=sample('_FlowMap','input.uv')
            uv.append(f'uv+=({flow}.rg-.5)*{ratio}*0.15;')
        if 'ScreenColorGlitch' in name:
            fragment.append(f'color.r={sample(primary,"uv+float2(0.006*jitter,0)")}.r; color.b={sample(primary,"uv-float2(0.006*jitter,0)")}.b;')
        if 'NoImage' in name or 'NoiseShader' in name:
            fragment.append('float noise=HlcHash(uv*1024+_Time.y); color.rgb=lerp(color.rgb,half3(noise,noise,noise),'+ratio+'); color.a=saturate('+ratio+');')
    elif 'ScreenBlackout' in name:fragment.append('color=half4(0,0,0,_HlcOpacity);')
    elif 'post_flear' in name or 'post_para' in name:
        uv.append('uv=(uv-_shadow_position.xy)*_shadow_offset.xy+_shadow_position.xy;')
        fragment.append('float mask=pow(saturate(color.r),'+f('_shadow_contraast','1.0')+'); color.rgb=lerp(color.rgb,_shadow_COL.rgb,mask*_shadow_COL.a);')
    elif 'SG_VFX_UnlitNearFade' in name:
        angle='_Rotate*0.0174532925'
        uv.append(f'float a={angle}; uv=mul(float2x2(cos(a),-sin(a),sin(a),cos(a)),uv-.5)+.5; uv=uv*_Tiling_Offset.xy+_Tiling_Offset.zw;')
        fragment.append('float d=distance(input.positionWS,_WorldSpaceCameraPos); float fade=saturate((d-_Fade_Start)/max(_Fade_End-_Fade_Start,0.01)); color.rgb*=_Color_Intensity; color.a*=_Alpha_Intensity*fade;')
    return '\n'.join(vertex),'\n'.join(uv),sampling,'\n'.join(fragment)

def write_meta(path,seed):
    value=hashlib.sha256(('hlc-chill-web-shader-v1:'+seed).encode()).hexdigest()[:32]
    Path(str(path)+'.meta').write_text(f'fileFormatVersion: 2\nguid: {value}\nShaderImporter:\n  externalObjects: {{}}\n  defaultTextures: []\n  nonModifiableTextures: []\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n',encoding='utf-8')
    return value

def generate(source,target,profile):
    raw=source.read_text('utf-8-sig');name=re.search(r'Shader\s+"([^"]+)"',raw).group(1)
    props,fields=properties(source)
    extra={}
    if 'Stylized Sky' in name:extra={'_HlcZenith':'Color) = (0.14,0.42,0.75,1)','_HlcHorizon':'Color) = (0.68,0.85,0.94,1)'}
    elif 'Stylized Clouds' in name:extra={'_HlcCloudTint':'Color) = (0.9,0.94,1,0.8)','_HlcCoverage':'Range(0,1)) = 0.45'}
    elif 'Stylized Fog' in name:extra={'_HlcFogTint':'Color) = (0.7,0.8,0.88,1)','_HlcFogDensity':'Range(0,1)) = 0.2'}
    elif 'ScreenBlackout' in name:extra={'_HlcOpacity':'Range(0,1)) = 0'}
    elif 'KawaseBlur' in name:extra={'_HlcBlurRadius':'Float) = 1.5'}
    elif name.endswith('/Wiper'):extra={'_HlcWipeCenter':'Vector) = (0.5,0.5,0,0)','_HlcWipeRadius':'Float) = 0.1'}
    for key,val in extra.items():
        line=f'{key} ("{key}", {val}'
        props+='\n'+line;fields[key]={'type':'Vector' if 'Vector)' in val else 'Color' if 'Color)' in val else 'Float','line':line}
    uniforms=[];textures=[]
    for key,val in fields.items():
        if key.startswith('unity_'):continue
        typ=val['type']
        if typ=='2D':textures.append(f'TEXTURE2D({key}); SAMPLER(sampler{key});')
        elif typ=='Cube':textures.append(f'TEXTURECUBE({key}); SAMPLER(sampler{key});')
        elif typ in ('Vector','Color'):uniforms.append('float4 '+key+';')
        elif typ=='Float' or typ.startswith('Range'):uniforms.append('float '+key+';')
    vertex,uv,sampling,fragment=behavior(name,fields)
    if profile['queue']=='AlphaTest' and 'WindowTransition' not in name and 'FurinShader' not in name and 'TreeShader' not in name:
        fragment+='\nclip(color.a-0.01);'
    # Near-fade blend values are material-driven in the original. A conventional
    # alpha blend is the compatible WebGL path; never emit Blend Zero Zero.
    blends={0:'Zero',1:'One',2:'DstColor',3:'SrcColor',4:'OneMinusDstColor',5:'SrcAlpha',6:'OneMinusSrcColor',7:'DstAlpha',8:'OneMinusDstAlpha',9:'SrcAlphaSaturate',10:'OneMinusSrcAlpha'}
    src,dst=int(profile['srcBlend']),int(profile['dstBlend'])
    if src==0 and dst==0:src,dst=5,10
    tokens={'NAME':'HLC/Web/'+name,'PROPERTIES':props,'TYPE':profile['renderType'],'QUEUE':profile['queue'],
            'SRC':blends[src],'DST':blends[dst],'ZWRITE':'On' if profile['zwrite'] else 'Off',
            'CULL':'Off' if any(x in name for x in ('Leaves','Grass','Butterfly','Stylized Sky','Clouds','Moon','Fog','Aurora','Furin')) else 'Back',
            'TEXTURES':'\n'.join(textures),'UNIFORMS':'\n'.join(uniforms),'VERTEX':vertex,'UV':uv,'SAMPLE':sampling,'FRAGMENT':fragment}
    result=TEMPLATE
    for token,value in tokens.items():result=result.replace('@'+token+'@',value)
    target.write_text(result,encoding='utf-8')
    return name,write_meta(target,name)

def main():
    project=ROOT/'UnityProject';source=ROOT/'Recovered/ExportedProject/Assets'
    out=project/'Assets/Prototype/WebShaders';out.mkdir(parents=True,exist_ok=True)
    profiles=json.loads((Path(__file__).parent/'shader_profiles.json').read_text())
    report=json.loads((project/'prototype-preparation.json').read_text())
    clear_glass=out/'ClearGlass.shader'
    clear_glass.write_text((Path(__file__).parent/'ClearGlass.shader').read_text(),encoding='utf-8')
    clear_guid=write_meta(clear_glass,'Clear Glass')
    story_screen=out/'StoryScreen.shader'
    story_screen.write_text((Path(__file__).parent/'StoryScreen.shader').read_text(),encoding='utf-8')
    write_meta(story_screen,'Story Screen')
    lookup={}
    for shader in (source/'Shader').glob('*.shader'):
        raw=shader.read_text('utf-8-sig');name=re.search(r'Shader\s+"([^"]+)"',raw).group(1)
        if name not in profiles or name.startswith('TextMeshPro/'):continue
        key,value=generate(shader,out/shader.name,profiles[name]);lookup[key]=value
    base=project/'Assets/TextMesh Pro/Shaders/TMP_SDF.shader'
    text=base.read_text('utf-8-sig').replace('Shader "TextMeshPro/Distance Field"','Shader "HLC/Web/TextMeshPro/Distance Field Color Glitch"',1)
    text=text.replace('Properties {','Properties {\n_GlitchColor1 ("Glitch Color 1",Color) = (0.93,0,0.7,0.6)\n_GlitchColor2 ("Glitch Color 2",Color) = (0,0.84,0.93,0.6)\n_GlitchDistance ("Glitch Distance",Float) = 1',1)
    text=text.replace('fixed4 PixShader(pixel_t input) : SV_Target','float4 _GlitchColor1, _GlitchColor2;\nfloat _GlitchDistance;\nfixed4 PixShader(pixel_t input) : SV_Target',1)
    text=text.replace('return faceColor * input.color.a;','float shift=_GlitchDistance/max(_TextureWidth,1);\nfloat left=tex2D(_MainTex,input.atlas-float2(shift,0)).a;\nfloat right=tex2D(_MainTex,input.atlas+float2(shift,0)).a;\nfaceColor.rgb += (_GlitchColor1.rgb*saturate(left-c)+_GlitchColor2.rgb*saturate(right-c))*faceColor.a;\nreturn faceColor * input.color.a;',1)
    target=base.parent/'HLC_TMP_ColorGlitch.shader';target.write_text(text,encoding='utf-8')
    lookup['TextMeshPro/Distance Field Color Glitch']=write_meta(target,'TextMeshPro/Distance Field Color Glitch')
    for mapping in report['materials']:
        name=mapping['originalShader']
        if mapping['material'] in ('mt_Lens.mat','mt_glass.mat'):
            raw=(source/'Material'/mapping['material']).read_text('utf-8')
            raw=re.sub(r'^  m_Shader: .*$',f'  m_Shader: {{fileID: 4800000, guid: {clear_guid}, type: 3}}',raw,flags=re.M)
            raw=re.sub(r'^  m_CustomRenderQueue: .*$', '  m_CustomRenderQueue: 3000',raw,flags=re.M)
            (project/'Assets/Recovered/Material'/mapping['material']).write_text(raw,encoding='utf-8')
            mapping.update(shader='HLC/Web/Clear Glass',method='web-reimplementation')
            continue
        if name is None:
            raw=(source/'Material'/mapping['material']).read_text('utf-8')
            if re.search(r'm_Shader: \{fileID: \d+, guid: 0000000000000000[ef]000000000000000, type: 0\}',raw):
                (project/'Assets/Recovered/Material'/mapping['material']).write_text(raw,encoding='utf-8')
                mapping.update(shader='Unity built-in shader reference',method='builtin-restored')
            continue
        if name not in lookup:continue
        raw=(source/'Material'/mapping['material']).read_text('utf-8')
        raw=re.sub(r'^  m_Shader: .*$',f'  m_Shader: {{fileID: 4800000, guid: {lookup[name]}, type: 3}}',raw,flags=re.M)
        (project/'Assets/Recovered/Material'/mapping['material']).write_text(raw,encoding='utf-8')
        mapping.update(shader='HLC/Web/'+name,method='web-reimplementation')
    report['counts']=dict(collections.Counter(m['method'] for m in report['materials']))
    original_names={guid(p):re.search(r'Shader\s+"([^"]+)"',p.read_text('utf-8-sig')).group(1) for p in (source/'Shader').glob('*.shader')}
    available={}
    for directory in (project/'Assets/TextMesh Pro/Shaders',project/'Assets/lilToon',project/'Packages'):
        for item in directory.rglob('*.shader'):
            match=re.search(r'Shader\s+"([^"]+)"',item.read_text('utf-8-sig'))
            if match and Path(str(item)+'.meta').exists():available[match.group(1)]=guid(item)
    resource_materials=[]
    for item in (source/'Resources').rglob('*.mat'):
        raw=item.read_text('utf-8');reference=re.search(r'm_Shader: .*?guid: ([a-f0-9]{32})',raw)
        name=original_names.get(reference.group(1)) if reference else None
        shader_guid=lookup.get(name) or available.get(name)
        method='web-reimplementation' if name in lookup else 'source-restored' if shader_guid else 'builtin-restored'
        if shader_guid:raw=re.sub(r'^  m_Shader: .*$',f'  m_Shader: {{fileID: 4800000, guid: {shader_guid}, type: 3}}',raw,flags=re.M)
        elif reference and not reference.group(1).startswith('0000000000000000'):
            raise ValueError('Unresolved resource shader: '+str(item)+' '+str(name))
        target=project/'Assets/Recovered/ResourceAssets'/item.relative_to(source/'Resources');target.write_text(raw,encoding='utf-8')
        resource_materials.append({'path':target.relative_to(project).as_posix(),'shader':name or 'Unity built-in','method':method})
    report['resourceMaterials']=resource_materials
    report['resourceMaterialCounts']=dict(collections.Counter(m['method'] for m in resource_materials))
    (project/'prototype-preparation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    (out/'implementation-notes.json').write_text(json.dumps({'sourceRecovered':False,'method':'new URP HLSL based on recovered properties and render states','shaders':list(lookup),'parity':'Effect intent preserved; original Shader Graph math is unavailable and appearance needs review.'},indent=2),encoding='utf-8')
    print(json.dumps({'generatedShaders':len(lookup),'materials':report['counts']},indent=2))

if __name__=='__main__':main()
