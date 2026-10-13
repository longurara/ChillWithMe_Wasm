using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Hlc.ChillPrototype;

public static class ChillNativeHudCheck
{
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
    static T Get<T>(object target,string field)=>(T)target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");
        var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();ChillAppearanceBuild.Configure(player);var ui=ChillNativeUIBuild.Configure(player);
        var data=JsonUtility.FromJson<ChillStoryData>(File.ReadAllText("Assets/StreamingAssets/chill-story.json"));
        Set(player.story,"data",data);Set(player.story,"texts",data.localizations.GroupBy(t=>t.ID).ToDictionary(g=>g.Key,g=>g.First()));
        Set(player.story,"chapters",data.novels.GroupBy(t=>t.ScenarioGroupID).ToDictionary(g=>g.Key,g=>g.ToArray()));Set(player.story,"saveAllowed",false);
        var catalog=JsonUtility.FromJson<ChillAssetCatalog>(File.ReadAllText("Assets/StreamingAssets/chill-library.json"));Set(player.library,"catalog",catalog);Set(player.library,"metadata",catalog.bundles.ToDictionary(b=>b.name));
        ui.Initialize();player.SetVolume(.35f);ui.RefreshNow();
        var config=JsonUtility.FromJson<ChillNativeUI.Configuration>(ui.configuration.text);
        Transform Ref(string script,string key)=>ui.Find(config.bindings.First(b=>b.script==script).refs.First(r=>r.name==key).path);
        var volume=Ref("MusicUI","_volumeSlider").GetComponent<Slider>();var audio=Ref("MusicVolumeUI","volumeIconImage").GetComponent<Image>();
        Require(!volume.gameObject.activeSelf,"Volume popup is always visible");Require(audio.sprite==ui.musicAudible,"Nonzero volume uses mute icon");
        ui.SetVolumePopup(true);Require(volume.gameObject.activeSelf,"Volume popup cannot open");ui.SetVolumePopup(false);
        player.SetVolume(0);ui.RefreshNow();Require(audio.sprite==ui.musicMuted,"Mute icon failed");player.SetVolume(.35f);ui.RefreshNow();
        var save=Get<ChillSave>(player.story,"save");save.workMinutes=20;save.breakMinutes=5;save.rounds=1;save.loopCount=2;save.timerRunning=true;save.timerEnd=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()/1000.0+1198;save.lastTimerTick=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()/1000.0;
        ui.RefreshNow();Require(player.story.UISnapshot("notes").timerText.StartsWith("00:19:"),"Native hh:mm:ss format failed");
        var timer=Ref("PomodoroTimerStateView","_pomodoroPlayOrPauseButtonImage").GetComponent<Image>();Require(timer.overrideSprite==ui.timerPause,"Pause icon overridden by stale sprite");
        Require(ui.timerPause.rect.width==52 && ui.timerPlay.rect.width==52,"Timer logical size is not native");
        ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-native-hud-running.png",1,0,.4f);
        player.story.UIAction(new ChillUiCommand{action="timer-toggle"});ui.RefreshNow();Require(player.story.UISnapshot("notes").timerActive,"Pausing resets UI");Require(!Ref("PomodoroTimerStateView","_skipButtonObject").gameObject.activeSelf,"Skip remains visible while paused");Require(timer.overrideSprite==ui.timerPlay,"Play icon fails to switch");
        ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-native-hud-paused.png",1,0,.4f);
        var level=ui.Find("Paremt/PCPlatform/Canvas/UI/UI_FacilityPlayerLevel/Normal_LevelBaseImage").GetComponent<Image>();
        Require(level.sprite.rect.size==new Vector2(256,256),"Level animation frame lost logical padding");
        Canvas.ForceUpdateCanvases();var levelBounds=VisibleRect(level);var timerBase=ui.Find("Paremt/PCPlatform/Canvas/UI/UI_FacilityPomodoro/PomodoroTimer").GetComponent<Image>();var timerBounds=VisibleRect(timerBase);
        Require(levelBounds.xMax+2<timerBounds.xMin,"Level circle overlaps Pomodoro circle: "+levelBounds+" vs "+timerBounds);
        player.story.UIAction(new ChillUiCommand{action="timer-reset"});ui.RefreshNow();Require(!player.story.UISnapshot("notes").timerActive,"Reset leaves timer active");
        Set(player.story,"noticeMessage","");Set(player.story,"noticeError",false);Set(player.story,"noticeRevision",0);ui.RefreshNow();
        ui.Open("music");Canvas.ForceUpdateCanvases();
        var rows=Get<List<GameObject>>(ui,"musicRows");Require(rows.Count==149,"Music catalog row count mismatch");
        Require(rows.All(r=>Mathf.Abs(((RectTransform)r.transform).rect.height-((RectTransform)ui.musicCell.transform).rect.height)<.1f),"Playlist row height differs from native prefab");
        var scroll=Ref("MusicPlayListView","_scrollRect").GetComponent<ScrollRect>();Require(scroll.content.rect.height>scroll.viewport.rect.height,"Music list does not scroll");
        ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-native-hud-playlist.png",1,0,.4f);
        ui.Close();
        save.workMinutes=25;ui.RefreshNow();ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-native-pomodoro-settings.png",1,0,.4f);
        Ref("TimerParentUI","_changeCountupTimerButton").GetComponent<Button>().onClick.Invoke();ui.RefreshNow();Require(save.timerMode==1,"Countup mode switch failed");
        ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-native-countup-ready.png",1,0,.4f);
        Ref("CountupTimerUI","_startCountupButton").GetComponent<Button>().onClick.Invoke();Require(save.countupRunning && save.countupActive,"Countup start failed");save.countupStarted-=65;save.lastCountupTick-=65;ui.RefreshNow();Require(player.story.UISnapshot("notes").countupText=="00:01:05","Countup elapsed time failed");
        Ref("CountupTimerUI","_playOrPauseCountupButton").GetComponent<Button>().onClick.Invoke();Require(!save.countupRunning && save.countupSeconds>=65,"Countup pause failed");Ref("CountupTimerUI","_playOrPauseCountupButton").GetComponent<Button>().onClick.Invoke();Require(save.countupRunning,"Countup resume failed");
        Ref("CountupTimerUI","_resetButton").GetComponent<Button>().onClick.Invoke();ui.RefreshNow();Require(!save.countupActive && save.countupSeconds==0,"Countup reset failed");Ref("TimerParentUI","_changePomodoroTimerButton").GetComponent<Button>().onClick.Invoke();ui.RefreshNow();
        Ref("PomodoroTimerUI","_addLoopCountButton").GetComponent<Button>().onClick.Invoke();ui.RefreshNow();Require(save.loopCount==3,"Loop increment failed");Ref("PomodoroTimerUI","_subtractLoopButtonButton").GetComponent<Button>().onClick.Invoke();ui.RefreshNow();Require(save.loopCount==2,"Loop decrement failed");
        Ref("PomodoroTimerUI","_togglePomodoroPresetButton").GetComponent<Button>().onClick.Invoke();Require(Ref("PomodoroPresetUI","canvasGroup").gameObject.activeSelf,"Native presets did not open");
        ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-native-timer-presets.png",1,0,.4f);ui.Close();
        ui.Open("story");Canvas.ForceUpdateCanvases();var storyRows=Get<List<GameObject>>(ui,"storyRows");Require(storyRows.Count>0,"Talk log is empty");
        var first=storyRows[0];var firstRect=(RectTransform)first.transform;
        ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-native-talk-log.png",1,0,.4f,false,camera=>{
        var point=RectTransformUtility.WorldToScreenPoint(camera,firstRect.TransformPoint(firstRect.rect.center));
        var eventSystem=UnityEngine.Object.FindObjectsOfType<EventSystem>().First(e=>e.gameObject.activeInHierarchy);
        var pointer=new PointerEventData(eventSystem){position=point};var hits=new List<RaycastResult>();ui.canvas.GetComponent<GraphicRaycaster>().Raycast(pointer,hits);
        Debug.Log("HLC_TALK_RAYCAST "+string.Join(" | ",hits.Take(5).Select(h=>h.gameObject.name)));
        Require(hits.Count>0,"Talk log receives no pointer events");var target=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);Require(target && (target==first || target.transform.IsChildOf(first.transform)),"Talk log click intercepted by "+(target?target.name:hits[0].gameObject.name));
        Require(first.GetComponentsInChildren<Button>(true).All(b=>b.onClick.GetPersistentEventCount()==0 && b.IsInteractable()),"Talk log has disabled child buttons");
        });ui.Close();
        var exit=UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prototype/NativeSprites/t_ui_buttonicon_active_exit.asset");
        Require(exit.rect.width==80 && exit.rect.height==80,"Exit icon logical rect is not square");
        Require(UnityEngine.Sprites.DataUtility.GetPadding(exit).y>20,"Phone icon lost transparent padding");
        File.WriteAllText(Path.GetFullPath("../research/native-hud-check.json"),JsonUtility.ToJson(new Report{passed=true,musicRows=rows.Count,recoveredSprites=ui.spriteReplacements.Length,timerStateSwitch=true,countupStateSwitch=true,timerPresets=true,levelSeparated=true,talkLogReceivesClicks=true,volumePopup=true,nativeSpritePadding=true,userSaveWritten=false,browserGameplayRun=false},true));
        Debug.Log("HLC_NATIVE_HUD_CHECK_PASSED rows="+rows.Count+" sprites="+ui.spriteReplacements.Length);
        ChillToonWeb.CheckAdditionalLightBudget();
        ChillLightingBuild.CheckDeskLamp(player,ui.canvas);
    }
    static Rect VisibleRect(Image image)
    {var dims=(Vector4)typeof(Image).GetMethod("GetDrawingDimensions",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(image,new object[]{false});var a=image.rectTransform.TransformPoint(new Vector3(dims.x,dims.y));var b=image.rectTransform.TransformPoint(new Vector3(dims.z,dims.w));return Rect.MinMaxRect(a.x,a.y,b.x,b.y);}
    [Serializable]class Report{public bool passed,timerStateSwitch,countupStateSwitch,timerPresets,levelSeparated,talkLogReceivesClicks,volumePopup,nativeSpritePadding,userSaveWritten,browserGameplayRun;public int musicRows,recoveredSprites;}
}
