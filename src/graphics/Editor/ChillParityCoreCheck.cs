using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hlc.ChillPrototype;
using UnityEngine;

public static class ChillParityCoreCheck
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
    static void Set(object o,string field,object value)=>o.GetType().GetField(field,Flags).SetValue(o,value);
    static T Get<T>(object o,string field)=>(T)o.GetType().GetField(field,Flags).GetValue(o);
    static object Invoke(object o,string method,params object[] args)=>o.GetType().GetMethod(method,Flags).Invoke(o,args);
    public static void Run(ChillStoryRuntime story,ChillGraphicsPlayer player)
    {
        var original=Get<ChillSave>(story,"save");
        try
        {
            using(ChillRuntimeContext.Replay(new DateTimeOffset(2026,10,13,12,0,0,TimeSpan.Zero),412))
            {
                float first=ChillRuntimeContext.Value;
                using(ChillRuntimeContext.Replay(new DateTimeOffset(2026,10,13,12,0,0,TimeSpan.Zero),412))Require(first==ChillRuntimeContext.Value,"Replay random is not deterministic");
                var habit=new ChillHabit{id="h",createdDate="2026-10-10"};
                Require(!ChillHabitHistory.Scheduled(habit,"2026-10-09"),"Habit exists before creation");
                ChillHabitHistory.SetPaused(habit,true,"2026-10-13");
                Require(ChillHabitHistory.Scheduled(habit,"2026-10-12")&&!ChillHabitHistory.Scheduled(habit,"2026-10-13"),"Pause changes historical days");
                ChillHabitHistory.SetPaused(habit,false,"2026-10-15");
                Require(!ChillHabitHistory.Scheduled(habit,"2026-10-14")&&ChillHabitHistory.Scheduled(habit,"2026-10-15"),"Resume destroys paused interval");
                ChillHabitHistory.SetPaused(habit,true,"2026-10-15");
                Require(habit.deadPeriods.Count==1&&habit.deadPeriods[0].end=="","Adjacent pause is not merged");
                ChillHabitHistory.SetPaused(habit,false,"2026-10-13");Require(habit.deadPeriods.Count==0,"Same-day cancel leaves an invalid period");
                habit.weekdays=1;Require(ChillHabitHistory.Scheduled(habit,"2026-10-11")&&!ChillHabitHistory.Scheduled(habit,"2026-10-12"),"Weekday mask drift");
                var legacy=new ChillSave{version=1};legacy.workspace.habits.Add(new ChillHabit{id="legacy",paused=true,completed=new List<string>{"2026-10-12"}});
                Set(story,"save",legacy);var workspace=story.Workspace;
                var smallTalk=Get<ChillStoryData>(story,"data").groups.First(g=>g.ID=="smalltalk_01");Require(!story.ChapterAvailable(smallTalk),"Named chapter prerequisite was ignored");legacy.completed.Add("main_01");Require(story.ChapterAvailable(smallTalk),"Completed prerequisite did not unlock small talk");
                var extra=Get<ChillStoryData>(story,"data").groups.First(g=>g.ID=="extra_01");Require(!story.ChapterAvailable(extra),"Unpurchased environment unlocks its conversation");workspace.purchases.Add("environment:Jellyfish");Require(story.ChapterAvailable(extra),"Purchased environment leaves its conversation locked");
                Require(legacy.version==2&&workspace.habits[0].deadPeriods.Count==1&&story.HabitScheduled(workspace.habits[0],"2026-10-12"),"v1 migration loses history");
                story.DeleteHabit("legacy");Require(story.CompletedHabits("2026-10-12").Count()==1,"Deleted habit completion vanished");
                story.HideHabitOnCalendar("legacy","2026-10-12");Require(!story.CompletedHabits("2026-10-12").Any(),"Deleted completion cannot be removed from calendar");
                story.Settings.firstDayInWeek=1;Require(story.StartOfWeek(new DateTime(2026,10,14))==new DateTime(2026,10,12),"First weekday setting is ignored");
                Require((int)Invoke(story,"GreetingScenario",ChillRuntimeContext.Now-3*86400)==14,"Three-day greeting uses the wrong source category");
                Require((int)Invoke(story,"GreetingScenario",ChillRuntimeContext.Now-14*86400)==14,"Fourteen-day greeting boundary drift");
                Require((int)Invoke(story,"GreetingScenario",ChillRuntimeContext.Now-15*86400)==15,"Fifteen-day greeting boundary drift");
                Require((int)Invoke(story,"GreetingScenario",ChillRuntimeContext.Now-31*86400)==16,"Month greeting category drift");
                story.Settings.totalLoginCount=100;Require((int)Invoke(story,"GreetingScenario",ChillRuntimeContext.Now-86400)==17,"Hundredth login greeting missing");
                CheckSmallTalk(story,player);
                for(int click=1;click<=4;click++){story.ClickPenguin();Require(story.Workspace.penguinClicks==click&&!story.Workspace.achievements.Contains("CLICK_KOU_COUNT"),"Penguin achievement completes before five clicks");}
                story.ClickPenguin();story.ClickPenguin();Require(story.Workspace.penguinClicks==5&&story.Workspace.achievements.Count(a=>a=="CLICK_KOU_COUNT")==1,"Penguin progress does not cap at five clicks");
                story.SelectPlayerStatus(1);Invoke(story,"TimerUserActivity");Require(story.Workspace.profileStatus=="0"&&Get<double>(story,"statusReactionAt")==-1,"Timer triggers a status reaction instead of silently marking activity");
                legacy.previewUnlock=false;legacy.mainEpisode=31;legacy.level=33;legacy.connectionLostNextEpisode=false;
                var episode=Get<ChillStoryData>(story,"data").groups.First(g=>g.Scenario==1&&g.EpisodeNumber==32);
                Require(!story.ChapterAvailable(episode),"Episode 32 bypasses the source reconnection gate");
                legacy.connectionLostNextEpisode=true;Require(story.ChapterAvailable(episode),"Episode 32 remains locked after reconnection");
                legacy.mainEpisode=0;legacy.timerComplete=false;legacy.workspace.pomodoroAudio.voice=false;
                Invoke(story,"TimerVoice","finish",false,false);Require(story.Resting,"Completed timer returns Satone to work");
                for(int mode=0;mode<3;mode++)
                {
                    var save=new ChillSave();Set(story,"save",save);story.ResetAutonomy();player.PlayState(mode);
                    Set(story,"autonomyInstalled",true);Set(story,"previousMode",mode);Set(story,"previousRest",false);
                    var state=player.character.GetCurrentAnimatorStateInfo(0);
                    story.SetResting(true);
                    Require(Get<bool>(story,"autonomyInstalled")&&player.character.GetCurrentAnimatorStateInfo(0).fullPathHash==state.fullPathHash,"Work/break request immediately resets animation");
                    var flow=(IEnumerator)Invoke(story,"TransitionActivity");
                    Require(flow.MoveNext()&&Get<int>(story,"breakMode")==-1,"Transition commits before the first frame");
                    var stack=new Stack<IEnumerator>();stack.Push(flow);int frames=0;
                    while(stack.Count>0&&frames++<3600)
                    {
                        var current=stack.Peek();if(!current.MoveNext()){stack.Pop();continue;}
                        if(current.Current is IEnumerator nested)stack.Push(nested);
                        player.character.Update(1f/60);story.presentation.TrackMotion();ChillRuntimeContext.Advance(TimeSpan.FromSeconds(1d/60));
                    }
                    Require(stack.Count==0&&Get<int>(story,"breakMode")>=0&&Get<bool>(story,"previousRest"),"Work/break transition never commits");
                }
            }
        }
        finally{story.ResetAutonomy();Set(story,"save",original);player.PlayState(original.deskMode);}
        File.WriteAllText(Path.GetFullPath("../research/parity-core-check.json"),"{\"passed\":true,\"scope\":\"Editor behavioural checks, not PC visual parity\",\"replayClockAndSeed\":true,\"habitPauseHistory\":true,\"habitDeletionHistory\":true,\"legacyMigration\":true,\"workBreakDeferred\":true,\"deskModes\":3,\"userSaveWritten\":false}");
        Debug.Log("HLC_PARITY_CORE_CHECK_PASSED");
    }
    static void CheckSmallTalk(ChillStoryRuntime story,ChillGraphicsPlayer player)
    {
        var data=Get<ChillStoryData>(story,"data");var groups=data.groups;
        var save=Get<ChillSave>(story,"save");var oldPreview=save.previewUnlock;var oldWork=save.workedSeconds;
        try
        {
            var source=groups.First(g=>g.Scenario==23);
            var candidate=new ChillChapter{ID=source.ID,Scenario=23,PlayTiming="GameStartAndBreak",ContentType="SmallTalk_Cicada",Arg1="100",Arg2="100",Arg3="2",UnlockStoryEpisode="None"};
            data.groups=new[]{candidate};save.previewUnlock=true;save.workedSeconds=7199;
            Require(Invoke(story,"LotterySmallTalk",false)==null,"Cicada conversation ignores required work hours");
            save.workedSeconds=7200;Require(Invoke(story,"LotterySmallTalk",false)!=null,"Cicada conversation remains blocked at the source boundary");
            candidate.ContentType="SmallTalk_PassageOfTime";player.environment.SelectTime(player.environment.selectedTime);
            Require(Invoke(story,"LotterySmallTalk",false)==null,"Passage-of-time conversation ignores last window change");
            ChillRuntimeContext.Advance(TimeSpan.FromHours(2));Require(Invoke(story,"LotterySmallTalk",false)!=null,"Passage-of-time conversation remains blocked at two hours");
            candidate.PlayTiming="GameStart";Require(Invoke(story,"LotterySmallTalk",false)==null&&Invoke(story,"LotterySmallTalk",true)!=null,"Startup small talk leaks into break conversations");
        }
        finally{data.groups=groups;save.previewUnlock=oldPreview;save.workedSeconds=oldWork;}
    }
}
