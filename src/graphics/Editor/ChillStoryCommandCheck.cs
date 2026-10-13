using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Hlc.ChillPrototype;
using UnityEngine;

public static class ChillStoryCommandCheck
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(object o,string key,object value)=>o.GetType().GetField(key,Flags).SetValue(o,value);
    static T Get<T>(object o,string key)=>(T)o.GetType().GetField(key,Flags).GetValue(o);
    static object Call(object o,string key,params object[] values)=>o.GetType().GetMethod(key,Flags).Invoke(o,values);
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    public static void Run(ChillStoryRuntime story,ChillNativeUI ui,ChillGraphicsPlayer player,ChillStoryData data)
    {
        Set(story,"chapter",data.groups.First(g=>g.Scenario==18));
        var script=data.novels.First(r=>r.Command==50);Set(story,"current",script);
        var wait=(IEnumerator)Call(story,"WaitForScript");Require(wait.MoveNext()&&story.WaitingForScript,"Controller wait did not start");
        story.UIAction(new ChillUiCommand{action="next"});Require(wait.MoveNext(),"Generic Next bypassed the controller wait");
        story.TutorialSignal("notes");Require(wait.MoveNext(),"Unrelated facility completed the controller wait");
        ui.RefreshNow();var configuration=JsonUtility.FromJson<ChillNativeUI.Configuration>(ui.configuration.text);
        var guide=configuration.bindings.First(b=>b.script=="WantTalkUI");Require(ui.Find(guide.path).gameObject.activeInHierarchy,"Controller wait does not show the source talk guide");
        story.TutorialSignal("satone");Require(!wait.MoveNext()&&!story.WaitingForScript,"Character readiness signal did not advance the controller wait");
        ui.RefreshNow();Require(!ui.Find(guide.path).gameObject.activeSelf,"Talk guide remains visible after readiness");

        var row=data.novels.First(r=>r.Command==22&&r.BodyMotion!=-1&&r.Arg2!=-1&&r.Arg1=="1");Set(story,"current",row);Call(story,"PreparePomodoroVoice",row);
        Require(!Get<bool>(story,"pomodoroVoiceReady"),"Timer voice starts before its animation cue");
        var actions=player.character.GetComponent<ChillAnimationActions>();bool oldPreview=actions.previewEvents;actions.previewEvents=true;
        actions.Dispatch("PlayPomodoroVoice",new AnimationEvent{intParameter=2});Require(!Get<bool>(story,"pomodoroVoiceReady"),"Wrong animation voice type releases the gate");
        actions.Dispatch("PlayPomodoroVoice",new AnimationEvent{intParameter=1});Require(Get<bool>(story,"pomodoroVoiceReady"),"Source animation event did not release the voice gate");
        Call(story,"ResetCommandWaits");actions.Dispatch("PlayPomodoroVoice",new AnimationEvent{intParameter=1});Require(!Get<bool>(story,"pomodoroVoiceReady"),"Stale voice event survived cancellation");
        Call(story,"PreparePomodoroVoice",new ChillSentence{Command=22,Arg1="1",BodyMotion=-1,FacialMotion=-1});Require(Get<bool>(story,"pomodoroVoiceReady"),"Voice without body motion waits for a nonexistent cue");
        Call(story,"PreparePomodoroVoice",new ChillSentence{Command=22,Arg1="0",BodyMotion=row.BodyMotion,FacialMotion=-1});Require(Get<bool>(story,"pomodoroVoiceReady"),"Immediate voice type incorrectly waits for an event");
        actions.previewEvents=oldPreview;

        var music=data.novels.First(r=>r.Command==25);Set(story,"talkLog",false);Require(!(bool)Call(story,"AppliesStoryMusic",music),"Talk-log-only BGM applies to a live reaction/resume");Set(story,"talkLog",true);Require((bool)Call(story,"AppliesStoryMusic",music),"Talk-log playback lost its BGM");
        var playlist=data.novels.First(r=>r.Command==26);Require(!(bool)Call(story,"AppliesStoryMusic",playlist)&&story.Tracks.Any(t=>t.title==playlist.Arg1),"MusicService command must resolve through the playlist");
        Require(Mathf.Abs((float)Call(story,"StoryMusicGain",new ChillSentence{Arg2=.5f})-.175f)<.001f,"Story BGM ratio ignores its base gain");
        Require(Mathf.Abs((float)Call(story,"StoryMusicStart",new ChillSentence{Arg3="2"},5f)-2)<.02f,"Story BGM resume loses its authored start offset");
        Require((float)Call(story,"StoryMusicStart",new ChillSentence{Arg3="123"},5f)<5,"Authored BGM offset exceeds clip length");
        int generation=Get<int>(story,"generation"),version=Get<int>(story,"storyMusicVersion");var fade=(IEnumerator)Call(story,"FadeStoryMusic",.35f,0f,1.5f,generation,version);Require(fade.MoveNext(),"Story music fade did not start");
        Call(story,"ResetCommandWaits");float gain=Get<float>(story,"cachedStoryMusic");Require(!fade.MoveNext()&&Get<float>(story,"cachedStoryMusic")==gain,"Cancelled fade changes a later track");
        Set(story,"chapter",null);Set(story,"current",null);ui.RefreshNow();
        File.WriteAllText(Path.GetFullPath("../research/story-command-check.json"),"{\"passed\":true,\"scriptWaitRequiresController\":true,\"sourceTalkGuide\":true,\"voiceWaitsForMatchingAnimationEvent\":true,\"staleEventsIgnored\":true,\"immediateVoicePaths\":true,\"talkLogMusicPolicy\":true,\"musicFadeCancellation\":true,\"userSaveWritten\":false,\"browserGameplayRun\":false}");
        Debug.Log("HLC_STORY_COMMAND_CHECK_PASSED controllerWait=true matchingVoiceCue=true talkLogMusic=true");
    }
}
