"""Extract original canvas control references before Windows services are stripped."""
import json,re,sys
from pathlib import Path
from prepare_project import ROOT,split_yaml,field,go_id,meta_guid
sys.path.insert(0,str(ROOT/'.tools/python'))
import yaml

source=ROOT/'Recovered/ExportedProject/Assets'
_,blocks=split_yaml((source/'App/Scenes/RoomScene.unity').read_text('utf-8-sig'))
components={i:(t,b) for t,i,b in blocks}
objects={i:field(b,'m_Name') for t,i,b in blocks if t==1}
transforms={i:(go_id(b),re.search(r'm_Father: \{fileID: (-?\d+)\}',b).group(1)) for t,i,b in blocks if t in (4,224)}
bygo={g:i for i,(g,parent) in transforms.items()}
children={i:[str(r['fileID']) for r in yaml.safe_load(b).get('RectTransform',yaml.safe_load(b).get('Transform',{})).get('m_Children',[])] for t,i,b in blocks if t in (4,224)}
def path(go):
    tid=bygo.get(go);parent=transforms[tid][1] if tid else '0'
    name=objects.get(go,'')
    if parent in transforms and objects.get(transforms[parent][0])=='ChangeTimerUI':
        siblings=[i for i in children.get(parent,[]) if objects.get(transforms[i][0])==name]
        if len(siblings)>1:name+=f'[{siblings.index(tid)}]'
    return (path(transforms[parent][0])+'/' if parent in transforms else '')+name
def reference(value):
    i=str(value.get('fileID',0))
    return path(i if components[i][0]==1 else go_id(components[i][1])) if i in components else ''
scripts={meta_guid(p):p.stem for p in (source/'Scripts/Assembly-CSharp').rglob('*.cs')}
selected={'TutorialView','TimerParentUI','CountupTimerUI','CountupTimerStateView','PomodoroPresetUI','PomodoroPresetInstance','PlayerLevelUIService','AddExpMeterUI','MusicUI','MusicPlayListView','MusicVolumeUI','StorySelectUI','StorySystemUI','SentenceSelectionButtonsUI','NoteUI','CurrentPageUI','TodoListUI','PomodoroTimerUI','PomodoroTimerStateView','EnvironmentUI','SettingUI','DraggableUI','FacilityChangeStatus','FacilityHideUI'}
selected.update({'HabitTrackerUI','HabitListUI','HabitItemUI','CalendarUI','CalenderCoreUI','CalendarCoreUI','TotalWorkStatisticsUI','StatisticsCoreUI','PlayerProfileView','HeroineProfileView','TimerSoundSettingView','SelectPageListUI','ProfileIconSettingView','SpecialView','SpecialSelectListUI','FacilityDecoration'})
selected.update({'DecorationListUI','DecorationPresetUI','HabitItemDateUI','HabitWeekLabelUI','ProfileView'})
selected.add('WantTalkUI')
selected.update({'FacilityProfile','StatusChangeView','ProfilePresetIconButton','ItemSkinSelectorUI','ItemSkinButton','PurchasePopover','BirthdaySettingDialog','ProfilePresetIconButtonView','PlayerStatusSelectButtonView','HabitDateLabelUI'})
bindings=[]
for t,i,b in blocks:
    if t!=114:continue
    script=re.search(r'm_Script: .*guid: ([a-f0-9]+)',b)
    name=scripts.get(script.group(1)) if script else None
    if name not in selected:continue
    raw=yaml.safe_load(b)['MonoBehaviour'];refs=[]
    for key,value in raw.items():
        if isinstance(value,dict) and 'fileID' in value:
            refs.append({'name':key,'path':reference(value),'asset':value.get('guid','')})
    bindings.append({'script':name,'path':path(go_id(b)),'refs':refs})
story=json.loads((ROOT/'UnityProject/Assets/StreamingAssets/chill-story.json').read_text('utf-8'))
interactions=[]
for t,i,b in blocks:
    if t!=114:continue
    script=re.search(r'm_Script: .*guid: ([a-f0-9]+)',b)
    if not script or scripts.get(script.group(1)) not in {'InteractableUI','InteractableUISettingTab'}:continue
    p=path(go_id(b))
    if not p.startswith('Paremt/PCPlatform/Canvas/'):continue
    raw=yaml.safe_load(b)['MonoBehaviour']
    interactions.append({'path':p,'hover':[reference(r) for r in raw.get('_mouseOverImage',[])],'use':[reference(r) for r in raw.get('_usingImage',[])],'baseline':[reference(r) for r in raw.get('_baseImage',[])],
        'hoverAlpha':raw.get('_mouseOverImageAlpha',1),'useAlpha':raw.get('_usingImageAlpha',1),'baseAlpha':raw.get('_baseImageAlpha',1),'hideHoverOnUse':bool(raw.get('_hideMouseOverOnUse',False))})
story_hidden=[]
for t,i,b in blocks:
    script=re.search(r'm_Script: .*guid: ([a-f0-9]+)',b)
    if t==114 and script and scripts.get(script.group(1))=='UIShowManagerForPC':
        story_hidden.extend(reference(r) for r in yaml.safe_load(b)['MonoBehaviour'].get('_deactivationUIObjectsForStoryPlay',[]))
if not story_hidden or any(not p for p in story_hidden):raise RuntimeError('Source story HUD references are missing')
tooltips=[];tooltip_ui=None;tooltip_service=None;first_timer_guide=None
for t,i,b in blocks:
    if t!=114:continue
    script=re.search(r'm_Script: .*guid: ([a-f0-9]+)',b);name=scripts.get(script.group(1)) if script else None
    p=path(go_id(b))
    if not p.startswith('Paremt/PCPlatform/Canvas/'):continue
    raw=yaml.safe_load(b)['MonoBehaviour']
    if name=='TooltipTarget':
        data=raw.get('_data',{})
        if data.get('ContentLocalizeID'):tooltips.append({'path':p,'key':data['ContentLocalizeID'],'direct':bool(data.get('IsDirectStringBind'))})
    elif name=='TooltipUI':
        tooltip_ui={'root':p,'text':reference(raw['_textLocalization']),'offset':[raw['_pointerOffset']['x'],raw['_pointerOffset']['y']],'fadeIn':raw['_fadeInDuration'],'fadeOut':raw['_fadeOutDuration']}
    elif name=='TooltipService':tooltip_service=raw
    elif name=='FirstTimerGuideView':first_timer_guide={'focus':reference(raw['_focusImage']),'fadeIn':raw['_fadeInDuration'],'fadeOut':raw['_fadeOutDuration'],'rotate':raw['_rotateDuration'],'scale':raw['_scaleDuration'],'multiple':raw['_scaleAdjustMultiple']}
if not tooltip_ui or not tooltip_service:raise RuntimeError('Native PC tooltip configuration is missing')
tooltip_ui.update(activateDelay=tooltip_service['_activateDelay'],deactivateDelay=tooltip_service['_deactivateDelay'])
config={'bindings':bindings,'interactions':interactions,'tooltips':tooltips,'tooltipPresentation':tooltip_ui,'firstTimerGuide':first_timer_guide,'storyHiddenObjects':story_hidden,'labels':[{'key':r['ID'],'en':r['En'],'ja':r['Ja']} for r in story['localizations'] if r['ID'].startswith('ui_') or r['ID']=='heroine_name']}
(ROOT/'UnityProject/Assets/Prototype/native-ui.json').write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8')
(ROOT/'research/native-ui-binding-report.json').write_text(json.dumps({'controllers':len(bindings),'references':sum(len(b['refs']) for b in bindings),'storyHiddenObjects':len(story_hidden),'hoverBindings':len(interactions),'tooltipTargets':len(tooltips),'nativeCanvas':'Paremt/PCPlatform/Canvas','source':str(source/'App/Scenes/RoomScene.unity')},indent=2),encoding='utf-8')
print('Native UI controllers',len(bindings),'references',sum(len(b['refs']) for b in bindings))
