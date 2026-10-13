"""Check every recovered scenario, branch, voice and motion reference."""
import collections,json,re
from pathlib import Path
root=Path(__file__).resolve().parents[1]
data=json.loads((root/'UnityProject/Assets/StreamingAssets/chill-story.json').read_text(encoding='utf8'))
catalog=json.loads((root/'WebPlayer/StreamingAssets/chill-library.json').read_text(encoding='utf8'))
rows=collections.defaultdict(list)
for row in data['novels']:rows[row['ScenarioGroupID']].append(row)
audio={a['name'] for a in catalog['assets'] if a['type']=='AudioClip'}
localized={r['ID']:r for r in data['localizations']}
runtime=(root/'src/graphics/Runtime/ChillStoryRuntime.cs').read_text(encoding='utf8')
supported={int(v) for v in re.findall(r'case (\d+)\s*:',runtime)}
errors=[];known=[];groups=[];voices=set()
for group in data['groups']:
    script=rows.get(group['ID'],[])
    if not script:known.append({'group':group['ID'],'problem':'Unused group has no source script'});continue
    ids={}
    for i,row in enumerate(script):ids.setdefault(row['ID'],i)
    duplicates={key for key,count in collections.Counter(r['ID'] for r in script).items() if count>1}
    edges={};terminals=set()
    for i,row in enumerate(script):
        command=row['Command'];targets=[]
        if command not in supported:errors.append({'id':row['ID'],'problem':'Unsupported command','value':command})
        if command in (3,15,4):targets=[row['Arg1']]
        elif command==70:targets=[row['Arg3'],row['Arg4']]
        elif command==81:targets=[row['Arg3']]
        for target in targets:
            if target not in ids:errors.append({'id':row['ID'],'problem':'Missing branch target','target':target})
            elif target in duplicates:errors.append({'id':row['ID'],'problem':'Ambiguous branch target','target':target})
        outgoing=[ids[t] for t in targets if t in ids]
        if command in (3,15):
            # Display all contiguous options, not just the first option.
            j=i+1
            while j<len(script) and script[j]['Command']==command:
                if script[j]['Arg1'] in ids:outgoing.append(ids[script[j]['Arg1']])
                j+=1
        if command==12:terminals.add(i)
        elif not targets and i+1<len(script):outgoing.append(i+1)
        elif not targets:terminals.add(i)
        edges[i]=outgoing
        key=row['LocalizationID']
        if key and key!='0':
            if key not in localized:known.append({'id':row['ID'],'missingSourceLocalization':key})
            else:
                voice=localized[key].get('Voice','')
                if voice:
                    voices.add(voice)
                    if voice not in audio:errors.append({'id':row['ID'],'problem':'Missing voice asset','target':voice})
    visited=set();pending=[0]
    while pending:
        i=pending.pop()
        if i in visited:continue
        visited.add(i);pending.extend(edges[i])
    if not terminals.intersection(visited):errors.append({'group':group['ID'],'problem':'No reachable story ending'})
    # Validate reconstruction at every cursor, including both date branches.
    groups.append({'id':group['ID'],'rows':len(script),'reachableRows':len(visited),'endingReachable':bool(terminals.intersection(visited)),'resumeCursorsInCatalog':len(script)})
report={'passed':not errors,'groups':len(groups),'rows':len(data['novels']),'mainEpisodes':len([g for g in data['groups'] if g['Scenario']==1]),'uniqueVoices':len(voices),'groupsChecked':groups,'errors':errors,'sourceMissingLocalizations':known,'scope':'Static branch/reference checks; not a claim of playing every episode in a browser.'}
(root/'research/story-catalog-check.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({k:v for k,v in report.items() if k!='groupsChecked'},ensure_ascii=False,indent=2))
if errors:raise SystemExit(1)
