using System;
using System.Linq;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        bool recoveredBackup;
        public int RoomLevel=>save.level;
        public double WorkedSeconds=>save?.workedSeconds??0;
        public string UITranslate(string key) => texts != null && texts.ContainsKey(key) ? Text(key) : key;
        static string TimerLabel(double seconds)
        { long total=(long)Math.Max(0,Math.Min(seconds,7*24*3600)); return (total/3600).ToString("00")+":"+(total/60%60).ToString("00")+":"+(total%60).ToString("00"); }
        public ChillStoryUiState UISnapshot(string panel,bool allChapters=false)
        {
            EnsureTimerPresets();EnsureWorkspace();
            bool timerActive=PomodoroActive;
            double remaining=save.timerRunning?Math.Max(0,save.timerEnd-Now):save.pausedRemaining;
            float remainingFraction=Mathf.Clamp01((float)(remaining/Math.Max(60,(save.breakTime?save.breakMinutes:save.workMinutes)*60)));
            var result=new ChillStoryUiState { ready=data != null,status=status,playing=Playing,typing=typing,loadingAudio=loadingAudio,autoRead=autoRead,
                mainStoryText=chapter != null && (chapter.Scenario>=1 && chapter.Scenario<=3 || chapter.Scenario==24 || chapter.Scenario>=50 && chapter.Scenario<=56),automaticDialogue=automaticDialogue,
                waitingForScript=waitingForScript,wantsToTalk=WantsToTalk,
                language=save.language,level=DisplayLevel,completed=save.completed.Count,unlock=save.previewUnlock,saveAllowed=saveAllowed,
                noticeMessage=noticeMessage,noticeError=noticeError,noticeRevision=noticeRevision,
                chapter=save.chapter,title=chapter != null ? Text(chapter.TitleLocalizationID) : "",speaker=current != null && current.Talker == 1 ? "Satone" : "",
                text=typing?typewriter.VisibleText():message.Substring(0,Math.Min(shownChars,message.Length)),workMinutes=save.workMinutes,breakMinutes=save.breakMinutes,
                timerRunning=save.timerRunning,timerActive=timerActive,timerMeter=timerActive?(save.breakTime?remainingFraction:1-remainingFraction):0,breakTime=save.breakTime,rounds=save.rounds,exp=(float)DisplayExp,requiredExp=DisplayRequiredExp,
                timerText=TimerLabel(save.timerRunning ? Math.Max(0,save.timerEnd-Now) : save.pausedRemaining > 0 ? save.pausedRemaining : save.workMinutes*60),
                loopCount=save.loopCount,timerMode=save.timerMode,countupRunning=save.countupRunning,countupActive=save.countupActive,countupText=TimerLabel(CountupElapsed),timerPresets=save.timerPresets.ToArray(),
                workedText=TimeSpan.FromSeconds(save.workedSeconds).ToString(@"hh\:mm\:ss"),
                note=panel == "notes" ? save.note : "",todos=panel == "notes" ? save.todos.ToArray() : Array.Empty<ChillTodo>(),
                history=panel == "story" || panel == "history" ? history.ToArray() : Array.Empty<string>(),
                choices=choices.Select((c,i) => new ChillUiItem { id=i.ToString(),title=Text(c.LocalizationID),enabled=current == null || current.Command != 15 || !save.usedChoices.Contains(c.Arg2) }).ToArray(),
                chapters=Array.Empty<ChillUiItem>(),page=page,kind=listKind };
            if (data != null && panel == "story" && !Playing)
            {
                var list=data.groups.Where(g => chapters.ContainsKey(g.ID) && (listKind == 3 || listKind == 0 && (g.Scenario == 1 || g.Scenario == 18) || listKind == 1 && (g.Scenario == 2 || g.Scenario == 3 || g.Scenario >= 24 && g.Scenario < 104) || listKind == 2 && g.Scenario != 1 && g.Scenario != 18))
                    .Where(g => string.IsNullOrEmpty(search) || (g.ID+Text(g.TitleLocalizationID)).IndexOf(search,StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                page=Mathf.Clamp(page,0,Mathf.Max(0,(list.Length-1)/20)); result.page=page; result.chapterCount=list.Length;
                result.chapters=list.Skip(allChapters ? 0 : page*20).Take(allChapters ? list.Length : 20).Select(g => new ChillUiItem { id=g.ID,title=string.IsNullOrEmpty(Text(g.TitleLocalizationID)) ? g.ID : Text(g.TitleLocalizationID),detail=Unlocked(g) ? "" : "Cần cấp "+g.UnlockLevel,done=save.completed.Contains(g.ID),enabled=Unlocked(g) }).ToArray();
            }
            return result;
        }
        public void UIAction(ChillUiCommand command)
        {
            if (data == null) return;
            TutorialSignal(command.action);
            switch (command.action)
            {
                case "next": if (Playing && choices.Count == 0 && tutorialCommand==0) next=true; break;
                case "auto": autoRead=command.value != 0; SaveNow(); break;
                case "choice": int index=(int)command.value; if (Playing && index >= 0 && index < choices.Count && (current.Command != 15 || !save.usedChoices.Contains(choices[index].Arg2))) chosen=index; break;
                case "story-skip": if(Playing)Finish();break;
                case "story-stop": SaveNow(); StopStory(); break;
                case "story-start": var selected=data.groups.FirstOrDefault(g => g.ID == command.id); if (selected != null && Unlocked(selected) && chapters.ContainsKey(selected.ID)) Begin(selected,false); break;
                case "story-resume": var resume=data.groups.FirstOrDefault(g => g.ID == save.chapter); if (resume != null) Begin(resume,true); break;
                case "chapter-search": search=(command.text ?? "").Substring(0,Math.Min(200,(command.text ?? "").Length)); page=0; break;
                case "chapter-kind": listKind=Mathf.Clamp((int)command.value,0,3); page=0; break;
                case "chapter-page": page=Mathf.Max(0,(int)command.value); break;
                case "unlock": save.previewUnlock=command.value != 0; SaveNow(); break;
                case "language": if(!Playing){save.language=new[]{"En","Ja","ZhHans","ZhHant","Pt","Ko","Ru"}.Contains(command.text)?command.text:"En"; SaveNow();} break;
                case "mode": if (!Playing && command.value >= 0 && command.value < 3) {player.PlayState((int)command.value);SaveNow();} break;
                case "time-mode": if (!Playing && command.value >= 0 && command.value < player.environment.presets.Length && ChangeTimeAllowed((int)command.value)) {player.environment.SelectTime((int)command.value);SaveNow();} break;
                case "window-mode": if (!Playing && command.value >= 0 && command.value <= player.environment.environments.Length && ChangeWindowAllowed((int)command.value)) {player.environment.SelectEnvironment((int)command.value);SaveNow();} break;
                case "note": UpdateNote(command.text); break;
                case "todo-add": AddTodo(command.text); break;
                case "todo-toggle": ToggleTodo((int)command.value); break;
                case "todo-delete": DeleteTodo((int)command.value); break;
                case "work-minutes": if (!PomodoroActive) { save.workMinutes=Mathf.Clamp((int)command.value,1,999); SaveNow(); } break;
                case "break-minutes": if (!PomodoroActive) { save.breakMinutes=Mathf.Clamp((int)command.value,1,999); SaveNow(); } break;
                case "loop-count": if(!PomodoroActive){save.loopCount=Mathf.Clamp((int)command.value,1,99);SaveNow();}break;
                case "timer-mode": if(!PomodoroActive && !save.countupActive){save.timerMode=command.value==1?1:0;SaveNow();}break;
                case "countup-toggle":
                    if(save.timerMode!=1 || PomodoroActive || Playing)break;
                    TimerUserActivity();
                    if(save.countupRunning){UpdateCountup();save.countupSeconds=CountupElapsed;save.countupRunning=false;}
                    else{bool fresh=!save.countupActive;save.timerComplete=false;save.countupActive=save.countupRunning=true;save.countupStarted=save.lastCountupTick=Now;if(fresh)TimerVoice("start",true);}
                    TimerSound(save.countupRunning?0:6);SaveNow();break;
                case "countup-reset": bool finishedCountup=save.countupActive;UpdateCountup();save.countupRunning=save.countupActive=false;save.countupSeconds=0;if(finishedCountup){TimerSound(3);TimerVoice("finish",true);}SaveNow();break;
                case "preset-name":case "preset-loops":case "preset-work":case "preset-break":
                    EnsureTimerPresets();int presetIndex=(int)command.value;
                    if(!PomodoroActive && presetIndex>=0 && presetIndex<save.timerPresets.Count)
                    {var preset=save.timerPresets[presetIndex];if(command.action=="preset-name")preset.name=(command.text ?? "").Substring(0,Math.Min(80,(command.text ?? "").Length));
                     else if(int.TryParse(command.text,out int value)){if(command.action=="preset-loops")preset.loopCount=Mathf.Clamp(value,1,99);else if(command.action=="preset-work")preset.workMinutes=Mathf.Clamp(value,1,999);else preset.breakMinutes=Mathf.Clamp(value,1,999);}SaveNow();}break;
                case "preset-play":
                    EnsureTimerPresets();int presetSelected=(int)command.value;if(PomodoroActive || save.countupActive || Playing || presetSelected<0 || presetSelected>=save.timerPresets.Count)break;
                    var choice=save.timerPresets[presetSelected];save.timerMode=0;save.loopCount=choice.loopCount;save.workMinutes=choice.workMinutes;save.breakMinutes=choice.breakMinutes;
                    UIAction(new ChillUiCommand{action="timer-toggle"});break;
                case "timer-toggle":
                    if(save.timerMode!=0 || save.countupActive || Playing)break;
                    TimerUserActivity();
                    if (save.timerRunning) { UpdateTimer(); save.pausedRemaining=Math.Max(0,save.timerEnd-Now); save.timerRunning=false; }
                    else {bool fresh=!PomodoroActive;save.timerComplete=false;if(fresh){save.rounds=0;save.breakTime=false;}save.timerEnd=Now+(save.pausedRemaining > 0 ? save.pausedRemaining : (save.breakTime ? save.breakMinutes : save.workMinutes)*60); save.lastTimerTick=Now; save.timerRunning=true;if(fresh)TimerVoice("start",false); }
                    TimerSound(save.timerRunning?0:6);SaveNow(); break;
                case "timer-reset": bool finishedPomodoro=PomodoroActive;UpdateTimer(); save.timerRunning=false; save.pausedRemaining=0; save.breakTime=false; save.rounds=0;if(finishedPomodoro)TimerVoice("finish",false,true); SaveNow(); break;
                case "timer-skip": if(save.timerRunning){skippingTimer=true;try{save.timerEnd=Now;UpdateTimer();}finally{skippingTimer=false;}SaveNow();}break;
                case "save": if(recoveredBackup)saveAllowed=true;if (SaveNow()) {recoveredBackup=false;status="Đã lưu tiến trình.";Notify(status);} break;
                case "save-export": SaveNow(); ChillBrowserSave.Export(save); Notify("Đã gửi file save cho trình duyệt tải xuống."); break;
                case "save-import": ChillBrowserSave.Import(gameObject.name); break;
            }
        }
        bool PomodoroActive=>save.timerRunning || save.pausedRemaining>0 || save.rounds>0 && save.rounds<save.loopCount;
        double CountupElapsed=>save.countupSeconds+(save.countupRunning?Math.Max(0,Now-save.countupStarted):0);
        void EnsureTimerPresets(){if(save.timerPresets==null)save.timerPresets=new System.Collections.Generic.List<ChillTimerPreset>();while(save.timerPresets.Count<5)save.timerPresets.Add(new ChillTimerPreset());}
        void UpdateCountup(){if(!save.countupRunning || data==null)return;double now=Now;CreditWork(Math.Max(0,now-save.lastCountupTick),now);save.lastCountupTick=now;}
    }
}
