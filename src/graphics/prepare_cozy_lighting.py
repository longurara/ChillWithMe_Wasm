"""Read COZY atmosphere/time inputs; evaluate them in Unity with native gradients."""
import json,re,struct,sys
from pathlib import Path
from prepare_project import ROOT,split_yaml,meta_guid,field,go_id
sys.path[:0]=[str(ROOT/'.tools/python'),str(ROOT/'research/python')]
from Crypto.Hash import MD4
import yaml

source=ROOT/'Recovered/ExportedProject/Assets'
_,blocks=split_yaml((source/'App/Scenes/RoomScene.unity').read_text('utf-8-sig'))
objects={i:field(b,'m_Name') for t,i,b in blocks if t==1}
transforms={i:b for t,i,b in blocks if t==4};bygo={go_id(b):i for i,b in transforms.items()}
def path(i):
    b=transforms[i];p=re.search(r'm_Father: \{fileID: (-?\d+)\}',b).group(1)
    return (path(p)+'/' if p in transforms else '')+objects[go_id(b)]
plugin=meta_guid(source/'Plugins/DistantLands.Cozy.Runtime.dll')
types={str(struct.unpack('<i',MD4.new(('s\0\0\0DistantLands.Cozy'+n).encode()).digest()[:4])[0]):n for n in ('CozyWeather','CozyTimeModule','CozyAtmosphereModule')}
components=[]
for t,i,b in blocks:
    if t!=114:continue
    m=re.search(r'm_Script: \{fileID: (-?\d+), guid: ([a-f0-9]+)',b)
    if m and m[2]==plugin and m[1] in types:components.append((types[m[1]],path(bygo[go_id(b)]),yaml.safe_load(b)['MonoBehaviour']))
assets={meta_guid(p):p for p in source.rglob('*.asset')}
def variable(value):
    gradient=value.get('gradientVal',{});curve=value.get('curveVal',{})
    return dict(mode=value.get('mode',1),color=value.get('colorVal',{}),value=value.get('floatVal',1),
        colors=[dict(time=gradient[f'ctime{i}']/65535,color=gradient[f'key{i}']) for i in range(gradient.get('m_NumColorKeys',0))],
        alphas=[dict(time=gradient[f'atime{i}']/65535,value=gradient[f'key{i}']['a']) for i in range(gradient.get('m_NumAlphaKeys',0))],
        gradientMode=gradient.get('m_Mode',0),curve=curve.get('m_Curve',[]))
presets=[]
for time_name in ('day','sunset','night','Cloudy'):
    marker='/DateTime/'+time_name+'/'
    weather=next(d for t,p,d in components if t=='CozyWeather' and marker in p)
    time=next(d for t,p,d in components if t=='CozyTimeModule' and marker in p)
    module=next(d for t,p,d in components if t=='CozyAtmosphereModule' and marker in p)
    profile=assets[module['atmosphereProfile']['guid']];_,rows=split_yaml(profile.read_text('utf-8-sig'));data=yaml.safe_load(rows[0][2])['MonoBehaviour']
    presets.append(dict(name=time_name,time=time['modifiedDayPercentage'] if weather['usePhysicalSunHeight'] else time['m_DayPercentage'],
        cumulus=weather['cumulus'],saturation=weather['filterSaturation'],brightness=weather['filterValue'],filter=weather['filterColor'],sunFilter=weather['sunFilter'],
        zenith=variable(data['ambientLightZenithColor']),horizon=variable(data['ambientLightHorizonColor']),multiplier=variable(data['ambientLightMultiplier']),
        sun=variable(data['sunlightColor']),sky=variable(data['skyZenithColor']),direction=variable(data['sunDirection']),pitch=variable(data['sunPitch'])))
out=ROOT/'UnityProject/Assets/Prototype/cozy-lighting.json';out.write_text(json.dumps({'presets':presets},indent=2),'utf-8')
print('Source COZY lighting inputs:',len(presets),'presets')
