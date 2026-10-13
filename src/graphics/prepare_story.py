"""Convert recovered story tables into an explicit browser/runtime schema."""
from pathlib import Path
import argparse, collections, hashlib, json, re, sys
from prepare_project import ROOT
sys.path.insert(0,str(ROOT/'.tools/python'))
import yaml

class UnityLoader(yaml.SafeLoader):
    pass
UnityLoader.yaml_implicit_resolvers={key:[entry for entry in values if entry[0]!='tag:yaml.org,2002:bool'] for key,values in yaml.SafeLoader.yaml_implicit_resolvers.items()}

SOURCE=ROOT/'Recovered/ExportedProject/Assets'
def table(name):
    text=(SOURCE/'MonoBehaviour'/f'{name}.asset').read_text('utf-8-sig')
    text=re.sub(r'^(?:%[^\n]*|--- !u![^\n]*)\n','',text,flags=re.M)
    # Unity stores localized strings such as "Yes"/"No" as plain YAML.
    # YAML 1.1's boolean resolver must not turn them into True/False text.
    return yaml.load(text,Loader=UnityLoader)['MonoBehaviour']

def enumeration(path,name):
    text=(SOURCE/'Scripts/Assembly-CSharp'/path).read_text('utf-8-sig')
    block=re.search(r'enum\s+'+name+r'[^\{]*\{(.*?)\}',text,re.S).group(1)
    result={};current=-1
    for item in block.split(','):
        item=re.sub(r'//[^\n]*','',item).strip()
        if not item:continue
        key,_,value=item.partition('=');key=key.strip();value=value.strip()
        if value:current=result[value.split('.')[-1]] if value.split('.')[-1] in result else int(value,0)
        else:current+=1
        result[key]=current
    return result

def scalar(value):return '' if value is None else str(value)

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--output',type=Path,default=ROOT/'UnityProject/Assets/StreamingAssets/chill-story.json')
    args=parser.parse_args()
    commands=enumeration('Bulbul/MasterData/CommandType.cs','CommandType')
    scenarios=enumeration('Bulbul/MasterData/ScenarioType.cs','ScenarioType')
    motions=enumeration('Bulbul/HeroineService.cs','AnimationType')
    groups=table('ScenarioGroupMaster')['Items'];novels=table('NovelMaster')['Items'];localizations=table('LocalizationMaster')['Items']
    for row in groups:
        for key in ('ID','TitleLocalizationID','SubtitleLocalizationID','UnlockStoryEpisode','PlayTiming','ContentType','Arg1','Arg2','Arg3'):row[key]=scalar(row.get(key))
    for row in novels:
        for key in ('ID','ScenarioGroupID','Arg1','Arg3','Arg4','LocalizationID'):row[key]=scalar(row.get(key))
    for row in localizations:
        for key in ('ID','Ja','En','ZhHans','ZhHant','Pt','Ko','Ru','Voice'):row[key]=scalar(row.get(key))
    by_group=collections.defaultdict(list)
    for row in novels:by_group[row['ScenarioGroupID']].append(row)
    localization_ids={row['ID'] for row in localizations}
    issues=[]
    for row in novels:
        if row['LocalizationID'] not in ('','0') and row['LocalizationID'] not in localization_ids:
            issues.append({'kind':'localization','id':row['ID'],'target':row['LocalizationID']})
        targets=[row['Arg1']] if row['Command'] in (3,4,15) else [row['Arg3'],row['Arg4']] if row['Command']==70 else [row['Arg3']] if row['Command']==81 else []
        for target in targets:
            if not any(n['ID']==target for n in by_group[row['ScenarioGroupID']]):issues.append({'kind':'jump','id':row['ID'],'target':target})
    meta={}
    for item in (SOURCE/'AudioClip').glob('*'):
        if item.name.endswith('.meta'):continue
        guid=re.search(r'^guid: ([a-f0-9]+)',Path(str(item)+'.meta').read_text(),re.M).group(1)
        meta[guid]=item.stem
    music=[]
    for name in ('MusicMaster','DirectionMusicMaster'):
        for row in table(name).get('AudioCollection',[]):
            music.append({'title':scalar(row.get('Title')),'clip':meta.get(row.get('AudioClip',{}).get('guid'),''),'credit':scalar(row.get('Credit'))})
    for table_name,enum_name,field,list_field in [('AmbientMaster','AmbientSoundType','AmbientSound','AmbientSounds'),('AmbientSeMaster','AmbientSeType','AmbientSeSound','AmbientSeSounds')]:
        names=enumeration('Bulbul/'+enum_name+'.cs',enum_name)
        for row in table(table_name)[list_field]:
            for name,value in names.items():
                if value==row[field]:music.append({'title':name,'clip':meta.get(row.get('AudioClip',{}).get('guid'),''),'credit':''})
    sound_source=(SOURCE/'Scripts/Assembly-CSharp/MotionSoundController.cs').read_text('utf-8-sig')
    for match in re.finditer(r'public void (\w+)\([^)]*\)\s*\{(.*?)(?=\n\tpublic |\n\tprivate )',sound_source,re.S):
        sound=re.search(r'AmbientSeType\.(\w+)',match.group(2))
        alias=next((row for row in music if sound and row['title']==sound.group(1)),None)
        if alias:music.append({'title':match.group(1),'clip':alias['clip'],'credit':''})
    data={'version':1,'groups':groups,'novels':novels,'localizations':localizations,'music':music,
          'commands':[{'name':k,'value':v} for k,v in commands.items()],
          'scenarios':[{'name':k,'value':v} for k,v in scenarios.items()],
          'motions':[{'name':k,'value':v} for k,v in motions.items()],
          'parameters':table('GameParameterMaster')}
    args.output.parent.mkdir(parents=True,exist_ok=True)
    payload=json.dumps(data,ensure_ascii=False,separators=(',',':')).encode()
    args.output.write_bytes(payload)
    report={'groups':len(groups),'novels':len(novels),'localizations':len(localizations),'motions':len(motions),
            'commands':dict(collections.Counter(next((k for k,v in commands.items() if v==r['Command']),str(r['Command'])) for r in novels)),
            'sourceIssues':issues,'sha256':hashlib.sha256(payload).hexdigest(),'bytes':len(payload)}
    (ROOT/'research/story-data-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k not in ('commands','sourceIssues')},indent=2))
    print('Source issues:',len(issues))

if __name__=='__main__':main()
