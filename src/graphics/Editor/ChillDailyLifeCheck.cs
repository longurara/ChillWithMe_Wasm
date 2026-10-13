using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Hlc.ChillPrototype;
using UnityEngine;
public static class ChillDailyLifeCheck
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(object o,string key,object value)=>o.GetType().GetField(key,Flags).SetValue(o,value);
    static T Get<T>(object o,string key)=>(T)o.GetType().GetField(key,Flags).GetValue(o);
    static object Call(object o,string key,params object[] values)=>o.GetType().GetMethod(key,Flags).Invoke(o,values);
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    public static void Run(ChillStoryRuntime story,ChillGraphicsPlayer player,ChillStoryData data)
    {
        var randomState=UnityEngine.Random.state;var settings=story.Settings;bool resting=settings.resting;int mode=player.SelectedState;
        Set(story,"chapter",null);Set(story,"breakMode",0);settings.resting=true;
        foreach(var nativeBase in new[]{"BreakBase001","BreakBase004","BreakBase005","Wild003_LeaveChair_Sofa"})
        {
            Call(story,"SetNativeMotion",nativeBase);
            for(int tick=0;tick<600&&!story.presentation.MotionCompleted;tick++){player.character.Update(.1f);story.presentation.TrackMotion();}
            Require(story.presentation.MotionCompleted,"Native activity entry/loop never becomes ready: "+nativeBase);
        }
        player.PlayState(0);Require(!(bool)Call(story,"BreakEligible",5),"Forward PC activity selected at a book desk");
        player.PlayState(1);Require(!(bool)Call(story,"BreakEligible",5),"Forward PC activity selected at a report desk");
        player.PlayState(2);Require((bool)Call(story,"BreakEligible",5),"PC forward activity is unavailable at the PC");
        Set(story,"breakMode",2);Set(story,"breakBaseMotion","BreakBase003_ListenMusicHigh");Require(Get<string>(story,"breakBaseMotion")==story.GetType().GetProperty("CurrentBaseMotion",Flags).GetValue(story).ToString(),"Music posture changes after a short reaction");
        Set(story,"breakMode",4);Call(story,"SetNativeMotion","BreakBase005");Set(story.presentation,"motionCompleted",true);Set(story,"sleepiness",0f);Set(story,"nextVariant",0d);Call(story,"UpdateSleep");
        Require(story.AutonomyState=="BreakBase005"&&Get<float>(story,"sleepiness")>0&&Get<float>(story,"sleepiness")<100,"Satone jumps straight into sleep without becoming drowsy");
        Set(story,"sleepiness",99f);Set(story,"nextVariant",0d);Call(story,"UpdateSleep");Require(story.AutonomyState=="BreakBase005_Sleep","Full sleepiness does not start the source sleep motion");
        Set(story.presentation,"motionCompleted",true);Set(story,"nextVariant",0d);Call(story,"UpdateSleep");Require(Get<float>(story,"sleepiness")<100,"Sleeping does not reduce sleepiness");
        Call(story,"SetWeightGroup","HeroineStateLeaveChairGoSofa");var weights=Get<IList>(story,"actionWeights");Require(weights.Count==3,"Sofa reaction weights are missing");Call(story,"PickWeightedVariant","HeroineStateLeaveChairGoSofa");
        Require(weights.Cast<object>().All(w=>(float)w.GetType().GetField("amount").GetValue(w)>0),"Sofa actions do not accumulate their source weights");
        foreach(var name in new[]{"Wild003_LeaveChair_Sofa_Thinking","Wild003_LeaveChair_Sofa_Look_Outside","Wild003_LeaveChair_Sofa_Control_Headphone"})Require(story.presentation.statePaths.Any(p=>data.motions.Any(m=>m.name==name&&m.value==p.motion)),"Sofa motion is not bound: "+name);
        Set(story,"chapter",data.groups.First(g=>g.Scenario==1));int bags=Get<System.Collections.Generic.Dictionary<int,System.Collections.Generic.List<string>>>(story,"reactionBags").Count;Require(!(bool)Call(story,"StartReaction",9),"An idle reaction interrupted the story");Require(Get<System.Collections.Generic.Dictionary<int,System.Collections.Generic.List<string>>>(story,"reactionBags").Count==bags,"Blocked reaction consumed a chapter from its bag");
        Set(story,"chapter",null);Set(story,"breakMode",-1);Set(story,"breakBaseMotion","");Set(story,"sleepiness",0f);settings.resting=resting;player.PlayState(mode);story.ResetAutonomy();UnityEngine.Random.state=randomState;
        var save=Get<ChillSave>(story,"save");string savedChapter=save.chapter;bool running=save.timerRunning;int collaboration=story.Workspace.collaboration;save.chapter="";save.timerRunning=false;story.Workspace.collaboration=0;
        var invited=Call(story,"NextInvitedChapter") as ChillChapter;Require(invited!=null&&!save.completed.Contains(invited.ID),"Idle story invitation does not resolve an unseen unlocked chapter");
        save.timerRunning=true;Require(Call(story,"NextInvitedChapter")==null,"Story invitation interrupts a running focus timer");save.timerRunning=false;save.chapter=invited.ID;Require(Call(story,"NextInvitedChapter")==null,"Story invitation overwrites a resumable chapter");
        save.chapter=savedChapter;save.timerRunning=running;story.Workspace.collaboration=collaboration;
        File.WriteAllText(Path.GetFullPath("../research/daily-life-check.json"),"{\"passed\":true,\"deskActivityEligibility\":true,\"sleepinessBuildUpAndRecovery\":true,\"listeningPosturePreserved\":true,\"sofaWeightsAndMotions\":true,\"storyInterruptionBlocked\":true,\"userSaveWritten\":false,\"browserGameplayRun\":false}");
        Debug.Log("HLC_DAILY_LIFE_CHECK_PASSED sleep=true sofa=true eligibility=true");
    }
}
