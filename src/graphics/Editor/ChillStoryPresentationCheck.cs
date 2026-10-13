using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hlc.ChillPrototype;
using UnityEngine;
public static class ChillStoryPresentationCheck
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(object o,string key,object value)=>o.GetType().GetField(key,Flags).SetValue(o,value);
    static T Get<T>(object o,string key)=>(T)o.GetType().GetField(key,Flags).GetValue(o);
    static object Call(object o,string key,params object[] args)=>o.GetType().GetMethod(key,Flags).Invoke(o,args);
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    public static void Run(ChillStoryRuntime story,ChillNativeUI ui,ChillStoryData data)
    {
        var config=JsonUtility.FromJson<ChillNativeUI.Configuration>(ui.configuration.text);
        Require(config.storyHiddenObjects.Length==11,"Source story HUD list is incomplete");
        var targets=config.storyHiddenObjects.Select(p=>ui.Find(p)?.gameObject).ToArray();Require(targets.All(g=>g),"Source story HUD object is missing");
        var before=targets.ToDictionary(g=>g,g=>g.activeSelf);
        Set(story,"chapter",data.groups.First(g=>g.Scenario==1));Set(story,"automaticDialogue",false);
        Set(story,"current",new ChillSentence{Command=0,Talker=1});Set(story,"message","Even when the math checks out,\nit still feels kind of off.");Set(story,"shownChars",1000);Set(story,"shownChars",Get<string>(story,"message").Length);
        ui.RefreshNow();Require(ui.StoryHudHidden&&targets.All(g=>!g.activeSelf),"Main story leaves the room HUD visible");
        var binding=config.bindings.First(b=>b.script=="StorySystemUI");Transform Ref(string key)=>ui.Find(key=="self"?binding.path:binding.refs.First(r=>r.name==key).path);
        Require(Ref("self").gameObject.activeInHierarchy&&Ref("_bottomBackImage").gameObject.activeInHierarchy&&Ref("_autoButton").gameObject.activeInHierarchy&&Ref("_skipButton").gameObject.activeInHierarchy,"Story HUD suppression hides dialogue controls/background");
        Require(ui.historyPanel.transform.parent==ui.canvas.transform,"Story history remains under the hidden room HUD");
        ui.Open("history");ui.RefreshNow();Require(ui.historyPanel.activeInHierarchy&&targets.All(g=>!g.activeSelf),"History cannot open independently during a story");ui.Close();
        Set(story,"chapter",null);Set(story,"current",null);Set(story,"message","");Set(story,"shownChars",0);ui.RefreshNow();Require(!ui.StoryHudHidden&&before.All(p=>p.Key.activeSelf==p.Value),"Ending a story loses the previous room HUD state");
        Call(ui,"ToggleHidden");Require(!ui.canvas.transform.Find("UI").gameObject.activeSelf,"Manual HUD hide failed");
        Set(story,"chapter",data.groups.First(g=>g.Scenario==52));ui.RefreshNow();Require(ui.StoryHudHidden&&Ref("_bottomBackImage").gameObject.activeInHierarchy,"Story backdrop is missing after a manually hidden HUD");
        Set(story,"chapter",null);ui.RefreshNow();Require(!ui.canvas.transform.Find("UI").gameObject.activeSelf,"Story ending undoes manual HUD hiding");Call(ui,"ToggleHidden");
        Set(story,"chapter",data.groups.First(g=>g.Scenario==18));ui.RefreshNow();Require(!ui.StoryHudHidden,"Tutorial loses facility controls");
        Set(story,"chapter",data.groups.First(g=>g.Scenario==13));Set(story,"automaticDialogue",true);ui.RefreshNow();Require(!ui.StoryHudHidden,"Greeting hides room controls");Set(story,"chapter",null);Set(story,"automaticDialogue",false);ui.RefreshNow();

        var gate=new ChillAutoAdvanceGate();Require(!gate.Ready(false,0,2)&&!gate.Ready(false,10,2),"Voice playback consumes the Auto delay");Require(!gate.Ready(true,10,2)&&!gate.Ready(true,11.9,2)&&gate.Ready(true,12,2),"Auto does not wait Arg2 after voice and text finish");Require(!gate.Ready(false,20,2)&&!gate.Ready(true,21,2),"History/manual pause does not reset the Auto wait");
        Require(Mathf.Abs(data.parameters.TalkSpeedData.Delay("En")-.02f)<.0001f&&Mathf.Abs(data.parameters.TalkSpeedData.Delay("Ja")-.13f)<.0001f,"Typewriter ignores the source language speed master");

        var effects=story.effects;var versions=Get<Dictionary<int,int>>(effects,"versions");
        foreach(int command in new[]{200,205})
        {
            versions[command]=1;var row=new ChillSentence{Command=command,Arg1="0.5",Arg2=1};var sequence=(IEnumerator)Call(effects,"ApplyLater",row,0f,command,1,false);
            Require(sequence.MoveNext()&&sequence.Current is WaitForSecondsRealtime wait&&Mathf.Abs(wait.waitTime-1)<.001f,"Tutorial effect does not hold for its authored second");
            Require((command==200?effects.glitch:effects.shake)==.5f,"Tutorial effect ratio is not applied immediately");Require(!sequence.MoveNext()&&(command==200?effects.glitch:effects.shake)==0,"Tutorial effect never stops");
            sequence=(IEnumerator)Call(effects,"ApplyLater",row,0f,command,1,false);sequence.MoveNext();versions[command]=2;if(command==200)effects.glitch=.8f;else effects.shake=.8f;sequence.MoveNext();Require((command==200?effects.glitch:effects.shake)==.8f,"Old effect timeout cancels a newer effect");
            effects.RestoreFinal(row);Require((command==200?effects.glitch:effects.shake)==0,"Resume reinstates an expired tutorial effect");row.Arg2=-1;effects.RestoreFinal(row);Require((command==200?effects.glitch:effects.shake)==.5f,"Persistent source effect is lost on resume");
        }
        effects.ResetEffects();
        File.WriteAllText(Path.GetFullPath("../research/story-presentation-check.json"),"{\"passed\":true,\"sourceHudObjects\":11,\"hudHiddenDuringLongStory\":true,\"historyAndDialogueRemainUsable\":true,\"hudRestoredAfterStory\":true,\"manualHidePreserved\":true,\"tutorialAndGreetingsKeepHud\":true,\"autoDelayAfterVoiceAndText\":true,\"languageTypewriterMaster\":true,\"finiteTutorialEffectsStop\":true,\"expiredEffectsNotRestored\":true,\"staleTimeoutIgnored\":true,\"userSaveWritten\":false,\"browserGameplayRun\":false}");
        Debug.Log("HLC_STORY_PRESENTATION_CHECK_PASSED hud=11 autoWaitAfterVoice=true finiteEffectsStop=true");
    }
}
