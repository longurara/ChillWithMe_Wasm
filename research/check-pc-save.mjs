import assert from 'node:assert/strict';
import {readFileSync,readdirSync,writeFileSync} from 'node:fs';
import {join} from 'node:path';
import {gzipSync,deflateRawSync} from 'node:zlib';
import {parseES3,decodeSave,classify,convertPCSave,readZip} from '../src/gameplay-ui/pc-save.mjs';

const catalog={story:{groups:[{ID:'main_01',Scenario:1,EpisodeNumber:1}]},features:{music:[{uuid:'pc-track',guid:'track-guid'}],collaborations:[]},decorations:{skins:[{name:'Cup',category:1}]},metadata:{decorations:{1:'Cup'}}};
const record=(type,value,name='save.es3')=>({type,value,name});
const base=[record('PlayerDataV3',{LevelData:{_currentLevel:2,_currentExp:10},PomodoroTotalWorkSeconds:1800}),record('ScenarioProgressData',{FinishReadMainEpisodeNumber:1,PlayedScenarioGroupIDs:['main_01']})];
const parsed=parseES3('{"__type":"test", "ids":{639270970027740616:639270970027740616},"text":"{42: unchanged}"}');
assert.equal(parsed.ids['639270970027740616'],'639270970027740616');assert.equal(parsed.text,'{42: unchanged}');
assert.throws(()=>parseES3('{"x":1,"x":2}'));assert.throws(()=>parseES3('{"x":1} trailing'));assert.throws(()=>parseES3('{"x":1e999}'));assert.equal(parseES3('{"__proto__":{"x":1}}').__proto__.x,1);assert.equal({}.x,undefined);
assert.equal((await decodeSave(gzipSync(Buffer.from('{42:"ok"}'))))['42'],'ok');
assert.equal(classify({'0':{__type:'Bulbul.PlayerDataV3,Assembly-CSharp',value:{LevelData:{}}}},'x')[0].type,'PlayerDataV3');
const result=convertPCSave([...base,record('NoteList',{Titles:{'639270970027740616':'Note'},PageOrderList:['639270970027740616']}),record('PageDataV2',{UniqueID:'639270970027740616',MainText:'saved',ScrollValue:.4}),record('TodoListData',{UniqueID:'999999999999999999',TitleText:'Tasks',TodoDic:{'888888888888888888':{UniqueID:'888888888888888888',TodoText:'Task',completeDateTimeString:'20261013120000'}},TodoOrderList:['888888888888888888']}),record('CalenderMonthlyData',{Year:2026,Month:10,DiaryList:{13:{WorkTimeSeconds:600,DiaryText:'Diary'}}}),record('HabitAllHeaderData',{Habits:[{HabitID:'h',Title:'Habit',_createdDate:'20261001',DayOfWeekBitFlag:127}],DeletedHabits:[{HabitID:'deleted',Title:'Old'}]}),record('HabitAllDeadPeriodData',{DeadPeriodDic:{h:[{_startDate:'20261012',_endDate:'20261014'}]}}),record('HabitAllMonthlyData',{HabitDic:{h:{DateDataDic:{11:{Completed:true}}},deleted:{DateDataDic:{10:{Completed:true,HideOnCalendar:false}}}}},'habit_202610.es3'),record('MusicSettingV2',{FavoriteAudioUUIDs:['pc-track'],PlaylistOrder:['pc-track'],ExcludedFromPlaylistUUIDs:['pc-track']}),record('DecorationsData',{DecorationDic:{1:{_isActiveForSave:true}}}),record('SettingData',{_gameLanguageForSave:1,_timeFormatForSave:0})],catalog,new Date('2026-10-13T00:00:00Z'));
assert.equal(result.save.version,2);assert.equal(result.save.workspace.notes[0].id,'pc-639270970027740616');assert.equal(result.save.workspace.lists[0].items[0].id,'pc-888888888888888888');assert.equal(result.save.workspace.lists[0].items[0].completedDate,'2026-10-13');assert.equal(result.save.workspace.legacyWorkSeconds,1200);assert.equal(result.save.workspace.habits[0].deadPeriods[0].end,'2026-10-14');assert.equal(result.save.workspace.deletedHabits[0].completed[0],'2026-10-10');assert.deepEqual(result.save.musicState.excluded,['track-guid']);assert.equal(result.save.workspace.room[0].skin,'Cup');assert.equal(result.save.settings.clock24,true);assert.equal(result.save.language,'Ja');
assert.throws(()=>convertPCSave([...base,record('NoteList',{Titles:{1:'missing'},PageOrderList:[1]})],catalog));
assert.throws(()=>convertPCSave([...base,base[0]],catalog));
const legacy=convertPCSave([record('PlayerData',{LevelData:{_currentLevel:1}}),base[1],record('NoteData',{PageDic:{1:{TitleText:'v1',MainText:'text'}},PageOrderList:[1]})],catalog);assert.equal(legacy.save.workspace.notes[0].title,'v1');
assert.throws(()=>parseES3('['.repeat(81)+'0'+']'.repeat(81)));
await assert.rejects(readZip(new Uint8Array([1,2,3])));
function zipFixture(method){
 const data=Buffer.from('{42:"ok"}'),name=Buffer.from('save.es3'),packed=method?deflateRawSync(data):data;
 let crc=0xffffffff;for(const b of data){crc^=b;for(let n=0;n<8;n++)crc=(crc>>>1)^((crc&1)?0xedb88320:0);}crc=(crc^0xffffffff)>>>0;
 const local=Buffer.alloc(30),central=Buffer.alloc(46),end=Buffer.alloc(22);
 local.writeUInt32LE(0x04034b50);local.writeUInt16LE(method,8);local.writeUInt32LE(crc,14);local.writeUInt32LE(packed.length,18);local.writeUInt32LE(data.length,22);local.writeUInt16LE(name.length,26);
 central.writeUInt32LE(0x02014b50);central.writeUInt16LE(method,10);central.writeUInt32LE(crc,16);central.writeUInt32LE(packed.length,20);central.writeUInt32LE(data.length,24);central.writeUInt16LE(name.length,28);
 const directory=Buffer.concat([central,name]);end.writeUInt32LE(0x06054b50);end.writeUInt16LE(1,8);end.writeUInt16LE(1,10);end.writeUInt32LE(directory.length,12);end.writeUInt32LE(local.length+name.length+packed.length,16);
 return Buffer.concat([local,name,packed,directory,end]);
}
for(const method of [0,8]){const bytes=zipFixture(method),files=await readZip(bytes);assert.equal((await decodeSave(files[0].bytes))['42'],'ok');bytes[bytes.length-22-8-46+16]^=1;await assert.rejects(readZip(bytes),/Corrupt ZIP/);}
const report={passed:true,scope:'Local parser/converter checks; no save is written or imported into the game',numericDictionaryKeys:true,exact64BitIds:true,gzip:true,storedAndDeflatedZip:true,zipChecksumRejected:true,duplicateKeysRejected:true,missingPagesRejected:true,legacyNotes:true,habitHistory:true,playlistUUIDs:true,realPCSaveReadOnly:false};

// Optional source fixture stays outside Git. Output only counts, never content.
if(process.argv[2]){
 const source=process.argv[2],records=[];
 function visit(path){for(const entry of readdirSync(path,{withFileTypes:true})){const next=join(path,entry.name);if(entry.isDirectory())visit(next);else if(/\.(es3|dat|bin)$/i.test(entry.name))records.push({name:next,bytes:readFileSync(next)});}}
 visit(source);
 const context={story:JSON.parse(readFileSync('UnityProject/Assets/StreamingAssets/chill-story.json','utf8')),features:JSON.parse(readFileSync('UnityProject/Assets/Prototype/chill-features.json','utf8')),decorations:JSON.parse(readFileSync('UnityProject/Assets/Prototype/story-scene.json','utf8')),metadata:JSON.parse(readFileSync('UnityProject/Assets/Prototype/pc-save-metadata.json','utf8'))};
 const classified=[];for(const r of records)classified.push(...classify(await decodeSave(r.bytes),r.name,context.metadata));
 const output=convertPCSave(classified,context);report.realPCSaveReadOnly=true;report.sourceFiles=records.length;report.recognizedRecords=classified.length;report.warningTypes=output.warnings.map(w=>w.split(':')[0]);
 assert(output.save.workspace.notes.length>0);assert(output.save.completed.every(id=>context.story.groups.some(g=>g.ID===id)));
}
writeFileSync('research/pc-save-check.json',JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report));
