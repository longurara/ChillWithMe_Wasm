"""Read native feature policies and GUIDs without modifying recovered assets."""
import json,re,sys
from pathlib import Path
from prepare_story import ROOT,SOURCE,table,enumeration,UnityLoader
import prepare_native_ui as native

def component(name):
    for t,i,b in native.blocks:
        match=re.search(r'm_Script: .*guid: ([a-f0-9]+)',b)
        if t==114 and match and native.scripts.get(match.group(1))==name:
            return native.yaml.load(b,Loader=UnityLoader)['MonoBehaviour']
    return {}

music=[]
for row in table('MusicMaster')['AudioCollection']:
    music.append(dict(guid=row['AudioClip'].get('guid',''),title=re.sub(r'<[^>]+>','',str(row.get('Title',''))),artist=re.sub(r'<[^>]+>','',str(row.get('Credit',''))),tag=int(row.get('Tag',0)),requiredChapter='main_32' if row.get('UUID')=='428d0ecc-81d7-1c75-09bf-0970e0608b5f' else ''))
ambient_names=enumeration('Bulbul/AmbientSoundType.cs','AmbientSoundType')
ambient=[]
for row in table('AmbientMaster')['AmbientSounds']:
    name=next(k for k,v in ambient_names.items() if v==row['AmbientSound'])
    ambient.append(dict(id=row['AmbientSound'],name=name,guid=row['AudioClip'].get('guid','')))
policies=[]
def flatten_policy(prefix,values):
    for key,value in values.items():
        if isinstance(value,dict):flatten_policy(prefix+'.'+key,value)
        elif isinstance(value,(int,float)):policies.append(dict(name=prefix+'.'+key,value=value))
for key in ('PomodoroVoiceData','PomodoroTalkData','GameEndTalkData','PointUpInfoData'):
    flatten_policy(key,table('GameParameterMaster')[key])
for name in ('NextActionSelector','HeroineSelfTalkController'):
    for key,value in component(name).items():
        if not key.startswith('m_') and isinstance(value,(int,float)):
            policies.append(dict(name=key,value=value))
for group,values in table('HeroineAIMaster').items():
    if group in ('LeaveChairData','PlayerStatusReactionData'):
        policies.extend(dict(name=group+'.'+k,value=v) for k,v in values.items())
actions=[]
for file in (SOURCE/'Scripts/Assembly-CSharp').rglob('HeroineState*.cs'):
    names=list(dict.fromkeys(re.findall(r'AnimationType\.(\w+)',file.read_text('utf-8-sig'))))
    if names:
        actions.append(dict(name=file.stem,motions=names))
        for key,value in component(file.stem).items():
            if not key.startswith('m_') and isinstance(value,(int,float)):
                policies.append(dict(name=file.stem+'.'+key,value=value))
credits=component('EndCreditsService')
asset_names={native.meta_guid(p):p.stem for p in (SOURCE/'MonoBehaviour').glob('*.asset')}
post=[]
for name in ('Volume_day','Volume_sunset','Volume_night','Volume_cloud'):
    record=dict(name=name)
    for ref in table(name)['components']:
        asset=asset_names[ref['guid']];values=table(asset)
        if asset.startswith('Beautify'):
            for key,target,default in [('brightness','brightness',1),('contrast','contrast',1),('saturate','saturation',1),('bloomIntensity','bloom',0),('bloomThreshold','threshold',1)]:
                item=values.get(key,{})
                record[target]=item.get('m_Value',default) if item.get('m_OverrideState') else default
        elif asset.startswith('LiftGammaGain'):
            for key in ('lift','gamma','gain'):
                record[key]=values[key]['m_Value'];record[key+'Override']=bool(values[key]['m_OverrideState'])
    post.append(record)
def sound_bank(master,collection):
    return [dict(id=row['SeSound'],guid=row['AudioClip'].get('guid','')) for row in table(master)[collection]]
def unlocks(master,kind):
    return [dict(kind=kind,item=r['ItemType'],condition=r['ConditionsType'],argument=str(r['Arg1']),expiry=int(r.get('UnlockExpire',0))) for r in table(master)['Items']]
unlock_rules=unlocks('UnlockDecorationMaster','decoration')+unlocks('UnlockEnvironmentMaster','environment')
wardrobe_source=component('CostumeChangeService')
wardrobe=dict(defaults=[native.reference(r) for r in wardrobe_source['_defaultCostumeObjects']],parent=native.reference(wardrobe_source['_costumeParent']),bones=native.reference(wardrobe_source['_characterBornParent']),materials=[r['Value']['Material']['guid'] for r in table('CostumeSkinDB')['_entries']['m_list']])
timer_buttons=[];decoration_buttons=[]
for t,i,b in native.blocks:
    match=re.search(r'm_Script: .*guid: ([a-f0-9]+)',b)
    if t==114 and match and native.scripts.get(match.group(1))=='TimerSoundTypeChangeButton':
        row=native.yaml.load(b,Loader=UnityLoader)['MonoBehaviour'];timer_buttons.append(dict(path=native.reference(row['_changeButton']),id=row['_timerSeType']))
    if t==114 and match and native.scripts.get(match.group(1))=='DecorationButtonUI':
        row=native.yaml.load(b,Loader=UnityLoader)['MonoBehaviour'];decoration_buttons.append(dict(path=native.reference(row['_changeButton']),id=row['_model']))

def scene_choices(script,type_key,button_key,image_key=None):
    result=[]
    for t,i,b in native.blocks:
        match=re.search(r'm_Script: .*guid: ([a-f0-9]+)',b)
        if t==114 and match and native.scripts.get(match.group(1))==script:
            row=native.yaml.load(b,Loader=UnityLoader)['MonoBehaviour']
            result.append(dict(id=row[type_key],path=native.reference(row[button_key]),image=native.reference(row[image_key]) if image_key else ''))
    return result
collab_names=['AlterEgo','BearsRestaurant','Valentine2026','LunaNewYear2026','NearSpring2026','MagicalPrincess','StatisticsDev']
collabs=[dict(scenario=50+i,name=name,thresholds=table('GameParameterMaster')[name+'Data'].get('nextLevelNecessaryExpArray',[table('GameParameterMaster')[name+'Data'].get('nextLevelNecessaryExp',600)])) for i,name in enumerate(collab_names)]
daily_costumes=[dict(id=i,weights=[r['Month%02d'%m] for m in range(1,13)]) for i,r in enumerate(table('CostumeChangeWeightMaster')['Items'])]
statistics_component=component('StatisticsCoreUI')
statistics_buttons=[dict(id=i,path=native.reference(r)) for i,r in enumerate(statistics_component.get('_changeViewButtons',[]))]
profile_buttons=scene_choices('ProfilePresetIconButtonView','_type','_button','_iconImage')
status_buttons=scene_choices('PlayerStatusSelectButtonView','_type','_button')
skin_colors=[dict(id=r['SkinType'],upper=r['IconColor1'],lower=r['IconColor2']) for r in table('DecorationMaster')['DecorationSkins']]
snacks=[]
for kind,name,pairs in [('cookie','HeroineCookie',[('_cookie01Mesh','Cookie01'),('_cookie02Mesh','Cookie02')]),('cupcake','HeroineCupcake',[('_bakingCupMesh','BakingCup'),('_cake01Mesh','Cake01'),('_cake02Mesh','Cake02')])]:
    comp=component(name)
    for row in table(kind.title()+'KindMaster')['_records']['m_list']:
        v=row['Value'];parts=[]
        for target,prefix in pairs:
            parts.append(dict(path=native.reference(comp[target]),mesh=v[prefix+'Mesh'].get('guid',''),material=v[prefix+'Material'].get('guid','')))
        snacks.append(dict(kind=kind,id=row['Key'],rarity=v['Rarity'],parts=parts))

config=dict(version=2,collaborations=collabs,dailyCostumes=daily_costumes,statisticsButtons=statistics_buttons,profileButtons=profile_buttons,statusButtons=status_buttons,skinColors=skin_colors,snacks=snacks,music=music,ambient=ambient,systemSounds=sound_bank('SystemSeMaster','SystemSes'),timerSounds=sound_bank('TimerSeMaster','TimerSes'),policies=policies,actions=actions,unlocks=unlock_rules,
            post=post,wardrobe=wardrobe,timerButtons=timer_buttons,decorationButtons=decoration_buttons,
            creditsParent=native.reference(credits['_creditsParent']),
            creditsGroups=[native.reference(r) for r in credits['_creditsCanvasGroups']],
            creditsFadeIn=credits['FadeInSeconds'],creditsHold=credits['ShowSeconds'],creditsFadeOut=credits['FadeOutSeconds'],creditsGap=credits['DelayNextCreditSeconds'])
config['tutorialFocus']=[]
config['musicboxSound']=component('MemorieController').get('_soundMusicbox',{}).get('guid','')
for kind,_,body in native.blocks:
    script=re.search(r'm_Script: .*guid: ([a-f0-9]+)',body)
    if kind==114 and script and native.scripts.get(script.group(1))=='TutorialFocusUI':
        values=native.yaml.load(body,Loader=UnityLoader)['MonoBehaviour']
        config['tutorialFocus'].append(dict(path=native.reference(values['m_GameObject']),image=native.reference(values['_focusImage']),mask=native.reference(values['_maskImage'])))
output=ROOT/'UnityProject/Assets/Prototype/chill-features.json'
output.write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8')
(ROOT/'research/features-source-data.json').write_text(json.dumps({'music':len(music),'ambient':len(ambient),'policies':len(policies),'actions':actions,'credits':len(config['creditsGroups'])},ensure_ascii=False,indent=2),encoding='utf-8')
print('Native feature data:',len(music),'music,',len(ambient),'ambient,',len(actions),'action policies')
