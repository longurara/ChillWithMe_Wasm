// ES3 dictionaries accept numeric property names. Preserve 64-bit IDs as text.
export function parseES3(text) {
  let at=0,entries=0;
  const ws=()=>{while(/\s/.test(text[at]||'')&&at<text.length)at++;};
  function quoted(){const start=at++;while(at<text.length){if(text[at]==='\\'){at+=2;continue;}if(text[at++]==='"')return JSON.parse(text.slice(start,at));}throw Error('Unterminated save string.');}
  function scalar(){const match=/^(?:-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?|true|false|null)/.exec(text.slice(at));if(!match)throw Error('Invalid save value.');at+=match[0].length;const token=match[0];if(/^-?\d+$/.test(token)&&!Number.isSafeInteger(Number(token)))return token;const value=JSON.parse(token);if(typeof value==='number'&&!Number.isFinite(value))throw Error('Invalid save number.');return value;}
  function read(depth){if(depth>80||++entries>500000)throw Error('Save is too complex.');ws();const c=text[at];if(c==='"')return quoted();if(c!=='{'&&c!=='[')return scalar();const object=c==='{',result=object?Object.create(null):[];at++;ws();if(text[at]===(object?'}':']')){at++;return result;}while(at<text.length){ws();let key;if(object){key=text[at]==='"'?quoted():String(scalar());if(Object.hasOwn(result,key))throw Error('Duplicate save key.');ws();if(text[at++]!==':')throw Error('Invalid save dictionary.');}const value=read(depth+1);if(object)result[key]=value;else result.push(value);ws();const next=text[at++];if(next===(object?'}':']'))return result;if(next!==',')throw Error('Invalid save separator.');}throw Error('Incomplete save.');}
  if(text.length>32*1024*1024)throw Error('Save file is too large.');text=text.replace(/^\uFEFF/,'');const result=read(0);ws();if(at!==text.length)throw Error('Extra data after save.');return result;
}

async function inflate(bytes,format,limit=32*1024*1024) {
  const reader=new Blob([bytes]).stream().pipeThrough(new DecompressionStream(format)).getReader();
  const chunks=[];let size=0;
  try{while(true){const {done,value}=await reader.read();if(done)break;size+=value.length;if(size>limit)throw Error('Expanded save is too large.');chunks.push(value);}}
  catch(error){await reader.cancel();throw error;}
  const output=new Uint8Array(size);let offset=0;for(const chunk of chunks){output.set(chunk,offset);offset+=chunk.length;}return output;
}
export async function decodeSave(bytes) {
  if(bytes.length>32*1024*1024)throw Error('Save file is too large.');
  if(bytes[0]===31&&bytes[1]===139)bytes=await inflate(bytes,'gzip');
  return parseES3(new TextDecoder('utf-8',{fatal:true}).decode(bytes));
}
export async function readZip(bytes) {
  if(bytes.length>128*1024*1024)throw Error('Save ZIP is too large.');
  const view=new DataView(bytes.buffer,bytes.byteOffset,bytes.byteLength);let end=-1;
  for(let p=bytes.length-22;p>=Math.max(0,bytes.length-65557);p--)if(view.getUint32(p,true)===0x06054b50&&p+22+view.getUint16(p+20,true)===bytes.length){end=p;break;}
  if(end<0||view.getUint16(end+4,true)||view.getUint16(end+6,true))throw Error('Unsupported save ZIP.');
  const count=view.getUint16(end+10,true);if(count>2000)throw Error('Too many ZIP entries.');let offset=view.getUint32(end+16,true),total=0;const files=[],names=new Set();
  for(let i=0;i<count;i++){
    if(offset+46>bytes.length||view.getUint32(offset,true)!==0x02014b50)throw Error('Invalid ZIP directory.');
    const flags=view.getUint16(offset+8,true),method=view.getUint16(offset+10,true),checksum=view.getUint32(offset+16,true),compressed=view.getUint32(offset+20,true),expanded=view.getUint32(offset+24,true),nameLength=view.getUint16(offset+28,true),extra=view.getUint16(offset+30,true),comment=view.getUint16(offset+32,true),local=view.getUint32(offset+42,true);
    if(offset+46+nameLength+extra+comment>bytes.length)throw Error('Invalid ZIP filename.');
    const name=new TextDecoder().decode(bytes.slice(offset+46,offset+46+nameLength));offset+=46+nameLength+extra+comment;
    if(name.split(/[\\/]/).includes('..')||name.startsWith('/')||names.has(name))throw Error('Invalid or duplicate ZIP path.');names.add(name);
    if(!/\.(es3|dat|bin)(\.bak)?$/i.test(name))continue;
    if(flags&1||![0,8].includes(method)||expanded>32*1024*1024||(total+=expanded)>128*1024*1024)throw Error('Unsupported or oversized ZIP entry.');
    if(local+30>bytes.length||view.getUint32(local,true)!==0x04034b50)throw Error('Invalid ZIP entry.');
    const start=local+30+view.getUint16(local+26,true)+view.getUint16(local+28,true);if(start+compressed>view.getUint32(end+16,true)||view.getUint16(local+8,true)!==method)throw Error('Truncated or mismatched ZIP entry.');
    const packed=bytes.slice(start,start+compressed),data=method===0?packed:await inflate(packed,'deflate-raw');if(data.length!==expanded)throw Error('Invalid ZIP entry size.');
    let crc=0xffffffff;for(const byte of data){crc^=byte;for(let bit=0;bit<8;bit++)crc=(crc>>>1)^((crc&1)?0xedb88320:0);}if(((crc^0xffffffff)>>>0)!==checksum)throw Error('Corrupt ZIP entry.');
    files.push({name,bytes:data});
  }return files;
}
const array=value=>Array.isArray(value)?value:[];
const number=(value,fallback=0)=>{const n=Number(value);return Number.isFinite(n)?n:fallback;};
const get=(value,name,fallback)=>value?.[name]??value?.[name[0].toLowerCase()+name.slice(1)]??fallback;
const date=value=>{if(value&&typeof value==='object'&&(value.ticks??value.Ticks)!==undefined){try{const stamp=new Date(Number((BigInt(value.ticks??value.Ticks)-621355968000000000n)/10000n));return stamp.toISOString().slice(0,10);}catch{return '';}}const text=String(value??'');const m=/^(\d{4})-?(\d{2})-?(\d{2})/.exec(text);if(!m)return '';const key=`${m[1]}-${m[2]}-${m[3]}`,check=new Date(`${key}T12:00:00Z`);return !Number.isNaN(+check)&&check.toISOString().slice(0,10)===key?key:'';};
const id=value=>String(value??'');

export function classify(document,name,metadata={}) {
  const records=[];
  for(const value of Object.values(document||{}))if(value&&typeof value==='object'&&value.__type&&Object.hasOwn(value,'value'))records.push({type:value.__type.split(',')[0].split('.').at(-1),value:value.value,name});
  if(!records.length&&get(document,'BirthdayMonth')!==undefined&&get(document,'ProfileIconData')!==undefined)records.push({type:'ProfileData',value:document,name});
  if(!records.length){const type=metadata.types?.[name.split(/[\\/]/).at(-1).split('_')[0].split('.')[0]];if(type&&!['AccountData','FilesSnapshot'].includes(type))records.push({type,value:document,name});}
  return records;
}

export function convertPCSave(records,{story,features,decorations,metadata={}},today=new Date()) {
  const warnings=[],byType=new Map();
  for(const record of records){const list=byType.get(record.type)||[];list.push(record);byType.set(record.type,list);}
  const one=type=>byType.get(type)?.[0]?.value;
  const all=type=>(byType.get(type)||[]).map(r=>r.value);
  const player=one('PlayerDataV3')||one('PlayerData')||one('RedundantPlayerData'),progress=one('ScenarioProgressData');
  if(!player||!progress)throw Error('Select a complete PC save folder containing player and story progress.');
  for(const type of ['PlayerDataV3','ScenarioProgressData','ProfileData'])if((byType.get(type)?.length||0)>1)throw Error('Select one player save, not several accounts or backups.');
  const level=get(player,'LevelData',{}),known=new Set(story.groups.map(g=>g.ID));
  const save={format:'chill-with-you-web',version:2,needsTutorial:!!get(player,'IsNeedTutorial',false),level:Math.max(1,number(get(level,'_currentLevel'),1)),exp:number(get(level,'_currentExp')),mainEpisode:number(get(progress,'FinishReadMainEpisodeNumber')),completed:[],usedChoices:[],sceneHistory:[],todos:[],note:'',chapter:'',cursor:0,storyLive:false,autoRead:!!get(one('StoryData'),'_isUseAuto'),language:'En',deskMode:0,timeMode:0,windowMode:0,workedSeconds:number(get(player,'PomodoroTotalWorkSeconds')),timerRunning:false,countupRunning:false,connectionLostNextEpisode:!!get(progress,'CanShowConnectionLostNextEpisode'),lastStoryUnlockWorkSeconds:number(get(player,'LastStoryUnlockWorkSeconds')),workMinutes:25,breakMinutes:5,loopCount:2,timerMode:0,timerPresets:[],settings:{autonomy:true,selfTalk:true,autoGreeting:true,mobileFit:true,quality:1,activeFps:60,inactiveFps:15,master:1,music:.35,voice:1,sfx:1,ambient:1,timer:1,renderScale:1,postProcessing:true,dayStart:6,sunsetStart:17,nightStart:19,ambience:[]},workspace:{notes:[],lists:[],habits:[],deletedHabits:[],days:[],room:[],presets:[],purchases:[],achievements:[],calendarEvents:[],panels:[],pcRecords:[],profileName:'Player',profileStatus:'0',profileIcon:'',points:0,hair:0,costume:0,collaboration:0,collaborationExp:new Array(7).fill(0),workMigrated:true,legacyWorkSeconds:0},musicState:{selected:'',playlist:'ALL',position:0,shuffle:false,repeat:false,favorites:[],excluded:[],playlists:[],imports:[]},conversationHistory:[]};
  const w=save.workspace;
  save.settings.totalLoginCount=number(get(player,'TotalLoginCount'));
  const login=get(player,'_lastLoginDateTimeString',''),parts=/^(\d{4})(\d{2})(\d{2})(\d{2})(\d{2})(\d{2})$/.exec(login);
  if(parts){const stamp=new Date(`${parts[1]}-${parts[2]}-${parts[3]}T${parts[4]}:${parts[5]}:${parts[6]}`);if(Number.isFinite(+stamp))save.settings.lastVisit=+stamp/1000;}
  w.playedDecorations=array(one('DecorationProgressData')?.PlayedDecoration);w.playedEnvironments=array(one('EnvironmentProgressData')?.PlayedEnvironment);
  const heroine=one('HeroineData')?.StartDirection;
  save.settings.greetingBags=[];
  for(const [scenario,key] of [[12,'_lessTowDaysCameraTouchTalkList'],[13,'_lessTowDaysTalkList'],[14,'_lessHarfMonthTalkList'],[15,'_greaterMonthTalkList'],[1400,'_lessTowDaysCameraTouchMorningTalkList'],[1402,'_lessTowDaysCameraTouchNoonTalkList'],[1404,'_lessTowDaysCameraTouchEveningTalkList'],[1406,'_lessTowDaysCameraTouchNightTalkList']])if(heroine?.[key])save.settings.greetingBags.push({scenario,lastTail:number(heroine[key]._beforeLast,-1),episodes:array(heroine[key]._randomList).map(v=>number(v))});
  if(one('LimitedTimeEventSaveData')?.Christmas2025SaveData?._hasSeenOpening)w.calendarEvents.push('event:2025-12-25');
  const objectParameters=one('DecorationParameterData');if(objectParameters){w.musicboxCountdown=number(objectParameters.MemorieDecorationCanUseCountdownDay,-1);w.musicboxUpdatedDate=date(objectParameters.MemorieDecorationLastUpdatedDate);}
  save.completed=array(get(progress,'PlayedScenarioGroupIDs')).filter(v=>known.has(v));
  for(const group of story.groups)if(group.Scenario===1&&group.EpisodeNumber<=save.mainEpisode&&!save.completed.includes(group.ID))save.completed.push(group.ID);
  if(array(get(progress,'PlayedScenarioGroupIDs')).some(v=>!known.has(v)))warnings.push('Some PC story IDs are absent from this game data and were not imported.');
  const noteList=one('NoteList');const pages=new Map(all('PageDataV2').map(p=>[id(p.UniqueID),p]));
  if(noteList)for(const key of array(noteList.PageOrderList)){const page=pages.get(id(key));if(!page)throw Error('A note page is missing from the selected folder.');w.notes.push({id:`pc-${id(key)}`,title:String(noteList.Titles?.[key]??'Note'),text:page.MainText??'',scroll:number(page.ScrollValue,1)});}
  const oldNote=one('NoteData');if(oldNote)for(const key of array(oldNote.PageOrderList)){const p=oldNote.PageDic?.[key];if(!p)throw Error('A legacy note page is missing.');w.notes.push({id:`pc-${id(key)}`,title:p.TitleText??'Note',text:p.MainText??'',scroll:1});}
  function task(t){return {id:`pc-${id(t.UniqueID)}`,title:t.TodoText??'',due:date(t.expireDateTimeString),completedDate:date(t.completeDateTimeString),done:!!date(t.completeDateTimeString)};}
  const lists=new Map(all('TodoListData').map(l=>[id(l.UniqueID),l]));for(const l of Object.values(one('TodoAllData')?.TodoListDic||{}))lists.set(id(l.UniqueID),l);
  for(const l of lists.values()){const order=array(l.TodoOrderList),dic=l.TodoDic||{};w.lists.push({id:`pc-${id(l.UniqueID)}`,title:l.TitleText??'Tasks',items:[...order.map(id),...Object.keys(dic).filter(k=>!order.map(id).includes(k))].map(k=>{if(!dic[k])throw Error('A task is missing.');return task(dic[k]);})});}
  for(const month of all('CalenderMonthlyData'))for(const [day,value] of Object.entries(month.DiaryList||{})){const key=date(`${month.Year}${String(month.Month).padStart(2,'0')}${String(day).padStart(2,'0')}`);if(!key)throw Error('Invalid PC calendar date.');w.days.push({date:key,diary:value.DiaryText??'',seconds:number(value.WorkTimeSeconds)});const completed=Object.values(value.CompleteTodoListDic||{}).map(task);if(completed.length){if(!w.lists.length)w.lists.push({id:'pc-calendar-tasks',title:'Tasks',items:[]});for(const t of completed)if(!w.lists.some(l=>l.items.some(x=>x.id===t.id)))w.lists[0].items.push({...t,done:true,completedDate:t.completedDate||key});}}
  const dead=one('HabitAllDeadPeriodData')?.DeadPeriodDic||{},headers=one('HabitAllHeaderData');
  function habit(h,deleted){const periods=array(dead[h.HabitID]).map(p=>({start:date(p._startDate),end:date(p._endDate)}));return {id:h.HabitID,title:h.Title??'Habit',createdDate:date(h._createdDate),deletedDate:deleted?date(today.toISOString()):'',weekdays:number(h.DayOfWeekBitFlag,127),paused:periods.some(p=>p.start<=date(today.toISOString())&&(!p.end||p.end>=date(today.toISOString()))),deadPeriods:deleted?[]:periods,completed:[],hiddenDates:[]};}
  w.habits=array(headers?.Habits).map(h=>habit(h,false));w.deletedHabits=array(headers?.DeletedHabits).map(h=>habit(h,true));
  for(const record of byType.get('HabitAllMonthlyData')||[]){const suffix=/(\d{4})(\d{2})\.(?:es3|dat)/.exec(record.name);if(!suffix)throw Error('Habit month filename is invalid.');for(const [key,span] of Object.entries(record.value.HabitDic||{})){const h=[...w.habits,...w.deletedHabits].find(h=>h.id===key);if(!h)throw Error('Habit history has no matching header.');for(const [day,d] of Object.entries(span.DateDataDic||{})){const key=date(`${suffix[1]}${suffix[2]}${String(day).padStart(2,'0')}`);if(!key)throw Error('Invalid habit date.');if(d.Completed)h.completed.push(key);if(d.HideOnCalendar)h.hiddenDates.push(key);}}}
  const profile=one('ProfileData');if(profile){save.settings.birthdayMonth=number(get(profile,'BirthdayMonth'));save.settings.birthdayDay=number(get(profile,'BirthdayDay'));w.profileStatus=id(get(profile,'Status',0));w.profileIcon=`preset:${Math.max(0,number(get(profile,'IconType')))}`;const avatar=get(get(profile,'ProfileIconData'),'profileIconBytes',get(get(profile,'ProfileIconData'),'ProfileIconBytes'));if(avatar&&number(get(profile,'IconType'))===-1){const base64=Array.isArray(avatar)?(avatar.length<=65536?btoa(String.fromCharCode(...avatar)):''):avatar;if(typeof base64==='string'&&base64.startsWith('iVBORw0KGgo')&&base64.length<89970)w.profileIcon='data:image/png;base64,'+base64;else warnings.push('Custom PC avatar needs to be selected again.');}}
  const setting=one('SettingData');if(setting){save.language=['En','Ja','En','ZhHans','ZhHant','Pt','Ko','Ru'][number(setting._gameLanguageForSave)]||'En';for(const [channel,prefix] of [['master','master'],['music','music'],['voice','voice'],['sfx','systemSE'],['ambient','ambientBGM'],['timer','timerSE']])if(setting[`_${prefix}VolumeForSave`]!==undefined)save.settings[channel]=setting[`_${prefix}IsMuteForSave`]?0:Math.max(0,Math.min(1,number(setting[`_${prefix}VolumeForSave`])));save.settings.activeFps=Math.max(15,Math.min(120,number(setting._activeFramerateForSave,60)));save.settings.inactiveFps=Math.max(5,Math.min(60,number(setting._deactiveFramerateForSave,15)));save.settings.clock24=number(setting._timeFormatForSave)===0;}
  const timer=one('PomodoroData');if(timer){save.loopCount=number(timer._loopCountForSave,2);save.workMinutes=number(timer._workMinutesForSave,25);save.breakMinutes=number(timer._breakMinutesForSave,5);save.timerPresets=array(timer.PresetData).map(p=>({name:p.Name??'',loopCount:number(p.LoopCount,2),workMinutes:number(p.WorkMinutes,25),breakMinutes:number(p.BreakMinutes,5)}));}
  const purchase=one('PointPurchaseDataV3')||one('PointPurchaseData');if(purchase){w.points=number(purchase.Point);for(const [key,kind] of [['PurchasedEnvironments','environment'],['PurchasedDecorations','decoration'],['PurchasedAmbientSounds','ambient']])for(const item of array(purchase[key]))w.purchases.push(`${kind}:${item}`);}
  const achievements=one('AchievementSaveData');w.achievements=array(achievements?._unlockedAchievements).filter(v=>typeof v==='string');
  const collaboration=one('CollaborationSaveData');if(collaboration){w.collaboration=number(collaboration._currentTypeForSave);for(const [i,key] of ['AlterEgoData','BearsRestaurantData','Valentine2026Data','LunaNewYear2026Data','NearSpring2026Data','MagicalPrincessData','StatisticsDevData'].entries()){const value=collaboration[key];if(!value)continue;const config=features.collaborations.find(c=>c.scenario===50+i),level=value.LevelData||{};w.collaborationExp[i]=(config?.thresholds||[]).slice(0,Math.max(0,number(level._currentLevel,1)-1)).reduce((a,b)=>a+b,0)+number(level._currentExp);for(const group of story.groups)if(group.Scenario===50+i&&group.EpisodeNumber<=number(value.LastReadEpisodeNumber)&&!save.completed.includes(group.ID))save.completed.push(group.ID);}}
  const tracks=new Map(features.music.map(t=>[t.uuid,t.guid]));const local=one('LocalMusicSetting');for(const t of array(local?.LocalAudioDatas)){const guid=`local-pc-${t.UUID}`;tracks.set(t.UUID,guid);w.profileName=w.profileName||'Player';save.musicState.imports.push({guid,title:String(t.FilePath||'Personal music').split(/[\\/]/).at(-1),fileName:String(t.FilePath||'').split(/[\\/]/).at(-1),artist:'Personal music',personal:true});}if(save.musicState.imports.length)warnings.push('Personal music files need to be imported again on this browser.');
  const music=one('MusicSettingV2')||one('MusicSetting');if(music){save.musicState.shuffle=!!music.IsShufflePlayMusic;save.musicState.favorites=array(music.FavoriteAudioUUIDs).map(v=>tracks.get(v)).filter(Boolean);const order=array(music.PlaylistOrder).map(v=>tracks.get(v)).filter(Boolean);if(order.length){save.musicState.playlists.push({name:'PC playlist',tracks:order});save.musicState.playlist='PC playlist';}save.musicState.excluded=array(music.ExcludedFromPlaylistUUIDs).map(v=>tracks.get(v)).filter(Boolean);}
  const skins=new Map(Object.entries(metadata.decorations||{}));const bySkin=new Map((decorations?.skins||[]).map(s=>[s.name,s]));
  function room(value){const choices=[];for(const [key,d] of Object.entries(value?.DecorationDic||{})){if(!d._isActiveForSave)continue;const name=skins.get(key),skin=bySkin.get(name);if(!skin){warnings.push('A selected PC decoration is unavailable in this build.');continue;}const existing=choices.findIndex(c=>c.category===skin.category);if(existing>=0)choices.splice(existing,1);choices.push({category:skin.category,skin:name});}return choices;}
  w.room=room(one('DecorationsData'));const presets=one('DecorationPresetsData');if(presets)w.presets=array(presets.Presets).map((p,i)=>({name:`PC preset ${i+1}`,time:0,window:0,hair:0,costume:0,choices:room(p)}));
  const costume=one('CostumeChangeSaveData');if(costume){const kind=costume.LatestChangeData?._lastChangedCostumeSkinType;w.costume=number(metadata.costumes?.[kind],0);w.lastAppearanceDate=date(costume.LatestChangeData?._lastChangedCostumeDateString);}
  const hair=one('HairLotteryData');if(hair)w.hair=number(hair.LatestChangeData?.Kind)===1?1+number(hair.LatestChangeData?.PinKind,0):0;
  const environment=one('EnviromentData');if(environment){for(const [key,v] of Object.entries(environment.WindowViewDic||{})){if(!v.IsActive)continue;const name=metadata.windows?.[key];const time=['Day','Sunset','Night','Cloudy'].indexOf(name);if(time>=0)save.timeMode=time;else{const index=metadata.environmentIndices?.[name];if(index!==undefined)save.windowMode=index;else warnings.push('A PC window scene needs to be selected again.');}}for(const [key,v] of Object.entries(environment.AmbientSoundDic||{}))save.settings.ambience.push({id:number(key),volume:v.IsMuteAmbient?0:Math.max(0,Math.min(1,number(v.SoundVolume)))});}
  const automatic=one('AutoTimeWindowChangeData');if(automatic){save.settings.autoTime=!!automatic.IsActiveAuto;for(const [field,key] of [['dayStart','TimeDayStart'],['sunsetStart','TimeSunsetStart'],['nightStart','TimeNightStart']]){const ticks=automatic[key]?.ticks;if(ticks!==undefined)save.settings[field]=number(ticks)/36000000000;}}
  const week=one('AllPlatformSettingData');if(week)save.settings.firstDayInWeek=number(week._firstDayInWeekForSave);
  w.legacyWorkSeconds=Math.max(0,save.workedSeconds-w.days.reduce((n,d)=>n+d.seconds,0));
  const envPresets=one('EnviromentPresetsData');if(envPresets)for(const [i,preset] of array(envPresets.Presets).entries()){
    let time=save.timeMode,window=0;for(const [key,v] of Object.entries(preset.WindowViewDic||{})){if(!v.IsActive)continue;const name=metadata.windows?.[key];const t=['Day','Sunset','Night','Cloudy'].indexOf(name);if(t>=0)time=t;else if(metadata.environmentIndices?.[name]!==undefined)window=metadata.environmentIndices[name];}
    w.presets.push({name:`PC environment ${i+1}`,time,window,hair:w.hair,costume:w.costume,choices:w.room.map(c=>({...c})),ambience:Object.entries(preset.AmbientSoundDic||{}).map(([key,v])=>({id:number(key),volume:v.IsMuteAmbient?0:Math.max(0,Math.min(1,number(v.SoundVolume)))}))});
  }
  const handled=new Set(['HeroineData','LimitedTimeEventSaveData','EnvironmentProgressData','DecorationProgressData','DecorationParameterData','PlayerDataV3','PlayerData','RedundantPlayerData','ScenarioProgressData','NoteList','PageDataV2','NoteData','TodoListData','TodoAllData','CalenderMonthlyData','HabitAllHeaderData','HabitAllDeadPeriodData','HabitAllMonthlyData','ProfileData','SettingData','AllPlatformSettingData','PomodoroData','PointPurchaseDataV3','PointPurchaseData','AchievementSaveData','CollaborationSaveData','LocalMusicSetting','MusicSettingV2','MusicSetting','DecorationsData','DecorationPresetsData','CostumeChangeSaveData','HairLotteryData','EnviromentData','EnviromentPresetsData','AutoTimeWindowChangeData','StoryData']);
  for(const [type,records] of byType)if(!handled.has(type)&&!['AccountData','FilesSnapshot'].includes(type)){for(const record of records)w.pcRecords.push({type,json:JSON.stringify(record.value)});warnings.push(`${type}: retained for future conversion; its behaviour is not yet imported.`);}
  return {save,warnings:[...new Set(warnings)],summary:{level:save.level,episode:save.mainEpisode,notes:w.notes.length,lists:w.lists.length,tasks:w.lists.reduce((n,l)=>n+l.items.length,0),habits:w.habits.length,days:w.days.length,points:w.points}};
}

export async function importPCSave(receiver,files,send) {
  const entries=[];for(const file of files){const bytes=new Uint8Array(await file.arrayBuffer());if(/\.zip$/i.test(file.name))entries.push(...await readZip(bytes));else if(/\.(es3|dat|bin)$/i.test(file.name))entries.push({name:file.webkitRelativePath||file.name,bytes});}
  if(!entries.length)throw Error('No PC save files were selected.');
  const profiles=new Set(entries.map(e=>/\/(?:Release|Demo)\/v[12]\/([^/]+)\//i.exec('/'+e.name)?.[1]).filter(Boolean));if(profiles.size>1)throw Error('Choose one player save folder.');
  const ranked=entries.filter(e=>!/\.bak$/i.test(e.name));const rank=name=>/\/Release\/v2\//i.test('/'+name)?4:/\/Release\/v1\//i.test('/'+name)?3:/\/Demo\/v2\//i.test('/'+name)?2:/\/Demo\/v1\//i.test('/'+name)?1:0;const selectedRank=Math.max(...ranked.map(e=>rank(e.name)));const selected=ranked.filter(e=>rank(e.name)===selectedRank);
  const [story,features,decorations,metadata]=await Promise.all(['StreamingAssets/chill-story.json','StreamingAssets/chill-features.json','StreamingAssets/story-scene.json','StreamingAssets/pc-save-metadata.json'].map(async path=>{const response=await fetch(path);if(!response.ok)throw Error('Cannot load PC save conversion data.');return response.json();}));
  const records=[];for(const entry of selected){const decoded=await decodeSave(entry.bytes);records.push(...classify(decoded,entry.name,metadata));}
  const result=convertPCSave(records,{story,features,decorations,metadata});
  const message=`PC save: level ${result.summary.level}, episode ${result.summary.episode}\n${result.summary.notes} notes, ${result.summary.tasks} tasks, ${result.summary.habits} habits\n${result.summary.points} points\n\n${result.warnings.join('\n')}\n\nReplace the current web save? A backup will be kept.`;
  if(globalThis.confirm(message))send(receiver,'ImportSave',JSON.stringify(result.save));return result;
}

export function choosePCSave(receiver,send) {
  const dialog=document.createElement('dialog');dialog.style.cssText='background:#242138;color:white;border:1px solid #a9a4c8;border-radius:16px;padding:24px;max-width:480px;font:18px sans-serif';
  const title=document.createElement('h2');title.textContent='Import PC save';dialog.append(title);
  const help=document.createElement('p');help.textContent='Choose one player save folder, or a ZIP containing that folder.';dialog.append(help);
  for(const [caption,folder] of [['Choose folder',true],['Choose ZIP / files',false]]){const button=document.createElement('button');button.textContent=caption;button.style.cssText='padding:12px;margin:5px';button.onclick=()=>{const input=document.createElement('input');input.type='file';input.multiple=true;if(folder)input.webkitdirectory=true;else input.accept='.zip,.es3,.dat,.bin';input.onchange=async()=>{button.disabled=true;try{await importPCSave(receiver,[...input.files],send);dialog.close();dialog.remove();}catch(error){help.textContent=error.message;button.disabled=false;}};input.click();};dialog.append(button);}
  const cancel=document.createElement('button');cancel.textContent='Cancel';cancel.onclick=()=>{dialog.close();dialog.remove();};dialog.append(cancel);dialog.addEventListener('cancel',()=>dialog.remove());document.body.append(dialog);dialog.showModal();
}
