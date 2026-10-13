using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hlc.ChillPrototype;
using UnityEngine;
public static class ChillGameplayParityCheck
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags).Invoke(o,args);
    static void Require(bool value,string why){if(!value)throw new InvalidOperationException(why);}
    public static void Run(ChillStoryRuntime story,ChillNativeUI ui,ChillGraphicsPlayer player,ChillStoryData data)
    {
        Require(data.groups.Count(g=>g.Scenario==1)==36,"Main episode master is incomplete");
        Require(data.parameters.typing!=null&&Mathf.Abs(data.parameters.typing.waitLong-.125f)<.0001f&&Mathf.Abs(data.parameters.typing.waitMiddle-.125f)<.0001f,"Source punctuation timing was not extracted");
        var typing=new ChillTypewriter();typing.Start("A.B",.02f,data.parameters.typing);
        Require(typing.Advance(.021f)==2&&!typing.Complete,"Punctuation is not revealed before its pause");Require(typing.Advance(.12f)==2,"Punctuation pause is lost");Require(typing.Advance(.01f)==3&&!typing.Complete,"Last character wait was skipped");typing.Advance(.02f);Require(typing.Complete,"Last character never completes");
        typing.Start("<color=red>A<waitfor=1><waitinput>B</color>",.02f,data.parameters.typing);typing.Advance(.03f);Require(typing.WaitingForInput&&typing.VisibleText()=="<color=red>A</color>","Action tags leak or rich text is unbalanced");typing.Advance(10);Require(typing.WaitingForInput,"Input wait automatically completes");typing.Advance(0,true);Require(!typing.WaitingForInput&&!typing.VisibleText().Contains('B'),"Input wait consumed its following delay while waiting");typing.Advance(1.1f);Require(typing.Complete&&typing.Text=="<color=red>AB</color>","Authored wait does not finish");
        typing.Start("𝄞A",.02f,data.parameters.typing);Require(typing.Advance(0)==2,"Typewriter splits a Unicode surrogate pair");
        typing.Start("AB.",.2f,data.parameters.typing);typing.Accelerate(.01f);typing.Advance(.021f);Require(!typing.Complete,"DisplayAllText bypassed punctuation timing");typing.Advance(.13f);Require(typing.Complete,"Accelerated typewriter is stuck");

        var playlist=player.music.clip;var channel=(AudioSource)story.GetType().GetProperty("StoryMusic",Flags).GetValue(story);
        Require(channel!=player.music,"Story BGM still shares the playlist source");channel.clip=AudioClip.Create("Parity scenario music",4800,1,48000,false);Call(story,"StopStoryMusic",new ChillSentence{Arg1="unrelated"});Require(channel.clip,"Named BGM stop stopped a different track");Call(story,"StopStoryMusic",new ChillSentence{Arg1=""});Require(!channel.clip&&player.music.clip==playlist,"Stopping story BGM mutates the playlist");
        Require(story.Features.tutorialFocus.Count(p=>!string.IsNullOrEmpty(p.mask))==3&&story.Features.tutorialFocus.All(p=>ui.Find(p.path)&&ui.Find(p.image)),"Tutorial focus guides are missing");
        Require(!string.IsNullOrEmpty(story.Features.musicboxSound),"Musicbox native audio is missing");
        var objects=new ChillActivityObjects();for(int i=0;i<8;i++)objects.Drink();Require(objects.Empty&&objects.drinks==0,"Drink event underflows the cup");Require(objects.NeedCold(0,0,50)&&!objects.NeedHot(0,100,50),"Next pour lottery is not stable");objects.Pour(0,true);Require(!objects.CanRefill(3600),"Full cup is refilled");for(int i=0;i<5;i++)objects.Drink();Require(!objects.CanRefill(3599)&&objects.CanRefill(3600),"Hourly refill/ice cooldown is ignored");objects.Pour(4000,false);Require(objects.Hot(4599)&&!objects.Hot(4600),"Hot drink reaction has no ten-minute limit");objects.Window(true,0,0);Require(objects.windowOpen&&objects.nextWindow==3600,"First window cooldown differs from source");objects.Window(false,3600,0);Require(!objects.windowOpen&&objects.nextWindow==14400,"Subsequent window cooldown differs from source");objects.Musicbox(0,1);Require(objects.nextMusicbox==3600,"Musicbox cooldown differs from source");
        var camera=story.presentation.cameras;camera.Capture();for(int i=0;i<3;i++){Require(camera.SelectRoomView(i),"Source room camera is missing");Require(Vector3.Distance(player.view.transform.position,camera.fixedViews[i].position)<.0001f&&Quaternion.Angle(player.view.transform.rotation,camera.fixedViews[i].rotation)<.001f&&Mathf.Abs(player.view.fieldOfView-35)<.001f,"Room viewpoint differs from source transform/FOV");}Require(!camera.SelectRoomView(-1)&&!camera.SelectRoomView(3),"Invalid room camera accepted");camera.ResetCamera();
        var mesh=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up},triangles=new[]{0,1,2}};Require(ChillCharacterPicking.Intersects(mesh,new Ray(new Vector3(.2f,.2f,-1),Vector3.forward)),"Character mesh picking misses a visible triangle");Require(!ChillCharacterPicking.Intersects(mesh,new Ray(new Vector3(.9f,.9f,-1),Vector3.forward)),"Character click accepts empty space inside renderer bounds");UnityEngine.Object.DestroyImmediate(mesh);
        var type=story.GetType();void Set(string key,object value)=>type.GetField(key,Flags).SetValue(story,value);
        var save=(ChillSave)type.GetField("save",Flags).GetValue(story);string oldChapter=save.chapter;int oldCursor=save.cursor;var oldHistory=save.sceneHistory;
        string loopGroup=data.novels.First(r=>r.Command==81).ScenarioGroupID;var script=data.novels.Where(r=>r.ScenarioGroupID==loopGroup).ToArray();var loopRows=Enumerable.Range(0,script.Length).Where(i=>script[i].Command==80).ToList();int loopIndex=Array.FindIndex(script,r=>r.Command==81);
        Set("chapter",data.groups.First(g=>g.ID==loopGroup));Set("sentences",script);Set("cursor",loopIndex);save.sceneHistory=loopRows;var loops=(List<ChillSentence>)type.GetField("loops",Flags).GetValue(story);loops.Clear();
        var resume=(IEnumerator)Call(story,"ReadFromSavedScene",true);Require(resume.MoveNext()&&loops.Count==4&&!story.Replaying,"Resume loses the text-loop bank");
        var selection=(IEnumerator)Call(story,"Choose",Array.FindIndex(script,r=>r.ID==script[loopIndex].Arg3),3);Require(selection.MoveNext(),"Loop selection did not wait for input");ui.RefreshNow();Canvas.ForceUpdateCanvases();var config=JsonUtility.FromJson<ChillNativeUI.Configuration>(ui.configuration.text);var selectionRoot=ui.Find(config.bindings.First(b=>b.script=="SentenceSelectionButtonsUI").path);var button=selectionRoot.GetComponentsInChildren<UnityEngine.UI.Button>().First();var text=button.GetComponentInChildren<TMPro.TMP_Text>();Require(((RectTransform)button.transform).rect.width>900&&text.color.a>.99f,"Choice retains collapsed layout or used-choice prefab color");
        ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-parity-selection.png",0,0,0,true,camera=>{var point=RectTransformUtility.WorldToScreenPoint(camera,((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center));var hits=new List<UnityEngine.EventSystems.RaycastResult>();var system=UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();ui.canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>().Raycast(new UnityEngine.EventSystems.PointerEventData(system){position=point},hits);Require(hits.Count>0&&UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"Choice click is intercepted by the dialogue backdrop");});
        Set("chapter",null);Require(!selection.MoveNext(),"Cancelled selection survives story exit");
        loops.Clear();Set("cursor",0);Set("current",null);Set("chosen",-1);save.chapter=oldChapter;save.cursor=oldCursor;save.sceneHistory=oldHistory;ui.RefreshNow();
        int oldMode=save.timerMode;bool oldRunning=save.timerRunning,oldCountup=save.countupActive;double oldRemaining=save.pausedRemaining;
        save.timerRunning=false;save.pausedRemaining=0;save.countupActive=false;
        var timer=config.bindings.First(b=>b.script=="TimerParentUI");
        ChillNativeInteraction Mode(string key)=>ui.Find(timer.refs.First(r=>r.name==key).path).GetComponent<ChillNativeInteraction>();
        var pomodoro=Mode("_changePomodoroTimerInteractiveUI");var countup=Mode("_changeCountupTimerInteractiveUI");
        void Selected(ChillNativeInteraction selected,ChillNativeInteraction inactive)
        {
            Require(selected.use.All(g=>g.color.a>.99f)&&inactive.use.All(g=>g.color.a<.01f),"Timer mode highlights retain the previous selection");
            Require(selected.GetComponent<UnityEngine.UI.Button>().transition==UnityEngine.UI.Selectable.Transition.None,"Selectable tint can overwrite timer highlight alpha");
        }
        save.timerMode=0;ui.RefreshNow();Selected(pomodoro,countup);Call(countup,"Enter");Require(countup.hover.All(g=>g.color.a>.99f),"Inactive use layer erases the shared hover image");
        save.timerMode=1;ui.RefreshNow();Selected(countup,pomodoro);Call(pomodoro,"Enter");save.timerMode=0;ui.RefreshNow();Selected(pomodoro,countup);
        foreach(string scriptName in new[]{"PomodoroTimerUI","CountupTimerUI"})
        {
            var binding=config.bindings.First(b=>b.script==scriptName);var start=ui.Find(binding.refs.First(r=>r.name.StartsWith("_start")).path).GetComponent<ChillNativeInteraction>();
            start.DeactivateAll();Require(start.hover.All(g=>g.color.a<.01f),"Idle Play ring should be hidden before hover");Call(start,"Enter");Require(start.hover.All(g=>g.color.a>.99f),"Native Play hover ring does not appear");start.OnPointerExit(null);Require(start.hover.All(g=>g.color.a<.01f),"Play ring remains after pointer exit");
        }
        save.timerMode=oldMode;save.timerRunning=oldRunning;save.countupActive=oldCountup;save.pausedRemaining=oldRemaining;ui.RefreshNow();
        File.WriteAllText(Path.GetFullPath("../research/gameplay-parity-check.json"),"{\"passed\":true,\"mainEpisodes\":36,\"sourcePunctuationPolicy\":true,\"lastCharacterWait\":true,\"richTextAndWaitActions\":true,\"unicodeTyping\":true,\"separateStoryMusic\":true,\"tutorialGuides\":3,\"drinkReadiness\":true,\"windowCooldown\":true,\"musicboxCooldown\":true,\"sourceRoomViews\":3,\"meshPicking\":true,\"textLoopResumeAndCancellation\":true,\"choiceRaycastAndLayout\":true,\"exclusiveTimerSelection\":true,\"nativePlayHoverRing\":true,\"userSaveWritten\":false,\"browserGameplayRun\":false}");
        Debug.Log("HLC_GAMEPLAY_PARITY_CHECK_PASSED episodes=36 typewriter=true storyMusicSeparate=true objectReadiness=true cameraViews=3");
    }
}
