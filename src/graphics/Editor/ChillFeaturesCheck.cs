using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Hlc.ChillPrototype;
public static class ChillFeaturesCheck
{
    static void Set(object obj,string key,object value)=>obj.GetType().GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
    static T Get<T>(object obj,string key)=>(T)obj.GetType().GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
    static object Invoke(object obj,string key,params object[] values)=>obj.GetType().GetMethod(key,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,values);
    static void Require(bool value,string why){if(!value)throw new InvalidOperationException(why);}
    [Serializable]class Report{public bool passed,userSaveWritten,browserGameplayRun,musicPlaylist,favorites,seek,settingsRoundTrip,invalidSettingsRejected,nativeTutorial,nativeRaycasts,animationPaths,storySkip,resumePreserved;public int musicTracks,ambientTracks,nativeHooks;}
    static void CheckPanel(GameObject panel,Camera camera)
    {
        var rect=(RectTransform)panel.transform;var corners=new Vector3[4];rect.GetWorldCorners(corners);var points=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(camera,p)).ToArray();
        Require(points.Max(p=>p.x)>50&&points.Min(p=>p.x)<1230&&points.Max(p=>p.y)>50&&points.Min(p=>p.y)<670,"Native window moved outside viewport: "+panel.name);
        Require(panel.GetComponentsInParent<CanvasGroup>().All(g=>g.alpha>.9f),"Native window is transparent: "+panel.name);
    }
    public static void Run()
    {
        ChillStartupCheck.Run();ChillPCSaveMetadata.Build();
        EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();
        ChillAppearanceBuild.Configure(player);var ui=ChillNativeUIBuild.Configure(player);ChillFeaturesBuild.Configure(player);var story=player.story;story.VerificationOnly=true;
        var data=JsonUtility.FromJson<ChillStoryData>(File.ReadAllText("Assets/StreamingAssets/chill-story.json"));var features=JsonUtility.FromJson<ChillFeatureData>(File.ReadAllText("Assets/Prototype/chill-features.json"));
        Set(story,"data",data);Set(story,"texts",data.localizations.GroupBy(t=>t.ID).ToDictionary(g=>g.Key,g=>g.First()));Set(story,"chapters",data.novels.GroupBy(n=>n.ScenarioGroupID).ToDictionary(g=>g.Key,g=>g.ToArray()));Set(story,"motions",data.motions.GroupBy(m=>m.value).ToDictionary(g=>g.Key,g=>g.First().name));Set(story,"<Features>k__BackingField",features);
        Set(story,"deskController",player.character.runtimeAnimatorController);
        foreach(string key in new[]{"voice","sound","ambient","keyboard","writing"})Set(story,key,player.gameObject.AddComponent<AudioSource>());
        var catalog=JsonUtility.FromJson<ChillAssetCatalog>(File.ReadAllText("Assets/StreamingAssets/chill-library.json"));Set(player.library,"catalog",catalog);Set(player.library,"metadata",catalog.bundles.ToDictionary(b=>b.name));
        Invoke(story.decorations,"Start");ui.Initialize();
        Require(ui.Find("Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilityProfile").gameObject.activeSelf,"Profile entry toolbar is hidden");
        Require(!ui.Find("Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilityProfile/PlayerProfile/PlayerProfileWindow").gameObject.activeSelf,"Profile popup starts open");
        var beforeRestore=ui.canvas.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.gameObject,t=>t.gameObject.activeSelf);
        story.decorations.Restore();Require(beforeRestore.All(pair=>pair.Key.activeSelf==pair.Value),"Decoration restore mutated native UI visibility");
        var taskButton=ui.Find("Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilityTodo/AddTodoUI/AddTodoButton").GetComponent<Button>();
        int taskCount=story.CurrentList.items.Count;taskButton.onClick.Invoke();Require(story.CurrentList.items.Count==taskCount+1,"Native New Task callback is missing");story.DeleteTodo(story.CurrentList.items.Count-1);
        story.ApplyFeatureSettings();if(QualitySettings.vSyncCount!=0)throw new InvalidOperationException("VSync overrides the user's frame limit");ui.RefreshNow();var save=Get<ChillSave>(story,"save");
        ChillCompletionCheck.Run(story,ui,player,save);save.previewUnlock=true;
        ChillParityCoreCheck.Run(story,player);
        ChillCozyLightingCheck.Run(player);
        Require(features.music.Length==43 && features.ambient.Length==30,"Native music/ambient master count mismatch");
        Require(features.music.Concat(features.ambient.Select(t=>new ChillTrack{guid=t.guid})).All(t=>catalog.assets.Any(a=>a.guid==t.guid&&a.type=="AudioClip")),"A music/ambient GUID is not in the built catalog");
        Require(features.systemSounds.Length==12&&features.timerSounds.Length==7&&features.systemSounds.Concat(features.timerSounds).All(t=>catalog.assets.Any(a=>a.guid==t.guid&&a.type=="AudioClip"&&catalog.bundles.Any(b=>b.name==a.bundle))),"Native sound cue bank is incomplete");
        Require(ui.tutorialSprites.Length==28&&ui.tutorialSprites.All(s=>s),"Native tutorial localization sprites are missing");
        story.ToggleFavorite(features.music[0].guid);story.SetPlaylist("Favorites");Require(story.Queue.Length==1,"Favorite filter failed");story.ToggleFavorite(features.music[0].guid);Require(story.Queue.Length==0,"Unfavorite does not remove filtered row");
        story.CreatePlaylist("Verification");story.AddTrackToPlaylist(features.music[0].guid);story.AddTrackToPlaylist(features.music[1].guid);Require(story.Queue.Length==2,"Custom playlist lost tracks");story.MoveTrack(features.music[1].guid,-1);Require(story.Queue[0].guid==features.music[1].guid,"Playlist reorder failed");story.RemoveTrack(features.music[0].guid);Require(story.Queue.Length==1,"Playlist remove failed");story.DeletePlaylist();Require(save.musicState.playlist=="ALL","Playlist delete failed");
        var clip=AudioClip.Create("Verification music",48000*5,1,48000,false);player.music.clip=clip;Set(story,"managedMusic",clip);save.musicState.selected=features.music[0].guid;
        story.SeekMusic(2);Require(Mathf.Abs(save.musicState.position-2)<.02f,"Music seek did not persist its target");story.SeekMusic(100);Require(save.musicState.position<clip.length,"Music seek exceeds clip length");
        story.Settings.voice=.4f;story.Settings.sfx=.3f;story.Settings.ambient=.2f;story.Settings.music=.25f;story.Settings.activeFps=30;story.Settings.autoTime=true;story.Settings.clock24=true;story.CommitSettings();
        var roundTrip=JsonUtility.FromJson<ChillSave>(JsonUtility.ToJson(save));Invoke(story,"Validate",roundTrip);Require(roundTrip.settings.voice==.4f&&roundTrip.settings.activeFps==30&&roundTrip.musicState.selected==features.music[0].guid,"Settings/music save round trip failed");
        roundTrip.settings.master=float.NaN;bool rejected=false;try{Invoke(story,"Validate",roundTrip);}catch(TargetInvocationException){rejected=true;}Require(rejected,"Invalid imported volume accepted");
        // Recover old saves without nested feature objects.
        var old=JsonUtility.FromJson<ChillSave>(JsonUtility.ToJson(save));old.settings=null;old.musicState=null;old.conversationHistory=null;Invoke(story,"Validate",old);Require(old.settings.master==1&&old.settings.activeFps==60,"Old save migration did not restore defaults");
        ui.Open("music");ui.RefreshNow();Canvas.ForceUpdateCanvases();var rows=Get<List<GameObject>>(ui,"musicRows");Require(rows.Count==43,"Native playlist contains non-music assets");
        Require(rows.All(row=>row.GetComponentsInChildren<Button>(true).All(button=>button.onClick.GetPersistentEventCount()==0)),"Playlist keeps Windows callbacks");
        Debug.Log("HLC_MUSIC_PANEL scale="+Get<GameObject>(ui,"openPane").transform.localScale+" groups="+string.Join("|",Get<GameObject>(ui,"openPane").GetComponentsInChildren<CanvasGroup>(true).Select(g=>g.name+"="+g.alpha)));
        ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-features-music.png",1,0,.3f,false,camera=>CheckPanel(Get<GameObject>(ui,"openPane"),camera));
        ui.Close();ui.Open("settings");ui.RefreshNow();ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-features-settings.png",1,0,.3f);
        Invoke(ui,"OpenAmbient");Canvas.ForceUpdateCanvases();var mixer=Get<GameObject>(ui,"ambientPanel");Require(mixer.GetComponentsInChildren<Slider>(true).Length==30,"Ambient mixer does not contain all native tracks");
        Require(mixer.GetComponentsInChildren<TMPro.TMP_Text>().Where(t=>t.name.Contains("Current")).All(t=>t.text=="0"),"Ambient volume labels disagree with zero gain");
        ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-features-ambient.png",1,0,.3f,false,camera=>CheckPanel(mixer,camera));ui.Close();
        Invoke(ui,"SetTutorialPage",0);var config=JsonUtility.FromJson<ChillNativeUI.Configuration>(ui.configuration.text);
        var tutorial=ui.Find(config.bindings.First(b=>b.script=="TutorialView").path);tutorial.gameObject.SetActive(true);ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-features-tutorial.png",1,0,.3f);tutorial.gameObject.SetActive(false);
        // Check real hit targets, not merely calling the callback method.
        ui.Open("music");ui.RefreshNow();Canvas.ForceUpdateCanvases();rows=Get<List<GameObject>>(ui,"musicRows");
        ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-features-raycast.png",1,0,.3f,false,camera=>{
            var favorite=rows[0].GetComponentsInChildren<Transform>(true).First(t=>t.name=="FavoriteIcon");var targetRect=favorite.GetComponent<Button>().targetGraphic.rectTransform;
            var point=RectTransformUtility.WorldToScreenPoint(camera,targetRect.TransformPoint(targetRect.rect.center));var es=UnityEngine.Object.FindObjectsOfType<EventSystem>().First(e=>e.gameObject.activeInHierarchy);var hits=new List<RaycastResult>();ui.canvas.GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(es){position=point},hits);
            Require(hits.Count>0,"Favorite receives no raycast");var target=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);Require(target==favorite.gameObject,"Favorite click intercepted by "+(target?target.name:hits[0].gameObject.name));
        });ui.Close();
        CheckDialogue(story,ui,data);
        ChillStoryPresentationCheck.Run(story,ui,data);
        ChillGameplayParityCheck.Run(story,ui,player,data);
        ChillStoryCommandCheck.Run(story,ui,player,data);
        ChillDailyLifeCheck.Run(story,player,data);
        ChillPaneLayoutCheck.Run(story,ui,player);
        ChillHoverCheck.Run(story,ui);
        ChillBreakPropsCheck.Run(story,player,data);save=Get<ChillSave>(story,"save");
        ChillStoryFlowCheck.Run(story,player,data);
        ChillStoryMotionSweep.Run(player,data);
        ChillMotionContinuityCheck.Run(story,player,data);
        ChillPhysicsCheck.Run(player,data);
        ChillCoatGravityCheck.Run(player);
        ChillWindowCoatCheck.Run(player);
        string[] required={"WorkBase001","WorkBase002","WorkBase003","BreakBase001","BreakBase002","BreakBase003_ListenMusicLow","BreakBase004","BreakBase005","BreakBase006","BreakBase005_Sleep","BreakBase005_JumpUp","Wild003_LeaveChair_Cup","Wild003_LeaveChair_Normal","Wild003_LeaveChair_Sofa","Wild003_LeaveChair_Cupcake_Comeback","Wild003_LeaveChair_Cookie_Comeback","Wild003_LeaveChair_BreadA_Comeback"};
        player.character.runtimeAnimatorController=story.storyController;player.character.Rebind();
        foreach(var name in required)
        {var motion=data.motions.FirstOrDefault(m=>m.name==name);Require(motion!=null,"Missing source motion "+name);var path=story.presentation.statePaths.FirstOrDefault(s=>s.motion==motion.value);Require(path!=null&&player.character.HasState(0,Animator.StringToHash(path.path)),"Missing native animator state "+name);}
        foreach(var name in new[]{"BreakBase004_DrinkTea","BreakBase005_Sleep","Wild003_LeaveChair_Sofa"})
        {Invoke(story,"SetNativeMotion",name);var path=story.presentation.statePaths.First(s=>s.name==name);player.character.Play(path.path,0,.35f);player.character.Update(0);ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-features-"+name+".png",1,0,0,true);}
        var sourceChapter=data.groups.First(g=>g.Scenario==9);Set(story,"chapter",sourceChapter);Set(story,"automaticDialogue",true);Set(story,"suspendedChapter",data.groups.First(g=>g.Scenario==1).ID);Set(story,"suspendedCursor",0);Set(story,"suspendedScene",new List<int>());
        story.UIAction(new ChillUiCommand{action="story-skip"});Require(!story.Playing&&save.completed.Contains(sourceChapter.ID)&&save.chapter==data.groups.First(g=>g.Scenario==1).ID,"Skip destroyed a resumable story");
        story.Settings.resting=true;Set(story,"chapter",sourceChapter);Set(story,"automaticDialogue",true);Set(story,"autonomyInstalled",true);Set(story,"previousRest",true);Set(story,"breakMode",2);Set(story,"selfTalkCount",3);Set(story,"nextAction",123456789d);Set(story,"suspendedChapter",save.chapter);Set(story,"suspendedScene",new List<int>());
        story.UIAction(new ChillUiCommand{action="story-skip"});Require(Get<int>(story,"breakMode")==2&&Get<int>(story,"selfTalkCount")==3&&Get<double>(story,"nextAction")==123456789d,"Automatic dialogue reset break activity or self-talk limit");
        File.WriteAllText(Path.GetFullPath("../research/features-check.json"),JsonUtility.ToJson(new Report{passed=true,userSaveWritten=false,browserGameplayRun=false,musicTracks=features.music.Length,ambientTracks=features.ambient.Length,nativeHooks=ui.HookCount,musicPlaylist=true,favorites=true,seek=true,settingsRoundTrip=true,invalidSettingsRejected=true,nativeTutorial=true,nativeRaycasts=true,animationPaths=true,storySkip=true,resumePreserved=true},true));
        UnityEngine.Object.DestroyImmediate(clip);Debug.Log("HLC_FEATURES_CHECK_PASSED music=43 ambient=30 sourceAnimations="+required.Length+" userSaveWritten=false");
    }
    static void CheckDialogue(ChillStoryRuntime story,ChillNativeUI ui,ChillStoryData data)
    {
        var config=JsonUtility.FromJson<ChillNativeUI.Configuration>(ui.configuration.text);
        Transform Ref(string script,string key){var binding=config.bindings.First(b=>b.script==script);return ui.Find(key=="self"?binding.path:binding.refs.First(r=>r.name==key).path);}
        foreach(var group in data.groups)
        {
            Set(story,"chapter",group);bool main=group.Scenario>=1&&group.Scenario<=3||group.Scenario==24||group.Scenario>=50&&group.Scenario<=56;
            Require(story.UISnapshot("").mainStoryText==main,"Wrong source message type for "+group.ID);
        }
        var uiRoot=ui.canvas.transform.Find("UI");var storyRoot=Ref("StorySystemUI","self");
        int sourceUiOrder=uiRoot.GetSiblingIndex();
        foreach(var panel in new[]{"story","settings","history","music"})
        {
            ui.Open(panel);Require(uiRoot.GetSiblingIndex()>storyRoot.GetSiblingIndex(),"Popup did not rise above dialogue: "+panel);
            ui.Close();Require(uiRoot.GetSiblingIndex()==sourceUiOrder&&uiRoot.GetSiblingIndex()<storyRoot.GetSiblingIndex(),"Closing popup leaves its background over the dialogue: "+panel);
        }
        Set(story,"chapter",data.groups.First(g=>g.Scenario==13||g.Scenario==1401));Set(story,"automaticDialogue",true);
        Set(story,"current",new ChillSentence{Talker=1});string text="Welcome back. Let's both do our best. A longer greeting must wrap above the music controls without covering the track title, playback buttons, or progress bar.";
        Set(story,"message",text);Set(story,"shownChars",text.Length);ui.RefreshNow();Canvas.ForceUpdateCanvases();
        var normal=Ref("StorySystemUI","_normalTextMessage").GetComponent<TMPro.TMP_Text>();normal.ForceMeshUpdate();
        Require(Ref("StorySystemUI","_normalTextParent").gameObject.activeInHierarchy&&!Ref("StorySystemUI","_mainStoryTextParent").gameObject.activeSelf,"Greeting uses the main story window");
        Require(Ref("StorySystemUI","_normalTextCanvasGroup").GetComponent<CanvasGroup>().alpha==1,"Greeting is invisible");
        Require(!Ref("StorySystemUI","_heroineNameObject").gameObject.activeSelf&&!Ref("StorySystemUI","_autoButton").gameObject.activeSelf&&!Ref("StorySystemUI","_skipButton").gameObject.activeSelf&&!ui.historyDialogueButton.gameObject.activeSelf,"Greeting retains episode-only controls");
        var characters=normal.textInfo.characterInfo.Take(normal.textInfo.characterCount).Where(c=>c.isVisible).ToArray();Require(characters.Length>0,"Greeting text has no visible glyphs");
        float bottom=characters.Min(c=>normal.transform.TransformPoint(c.bottomLeft).y);var corners=new Vector3[4];((RectTransform)Ref("MusicUI","_playOrPauseButtonImage")).GetWorldCorners(corners);
        Require(bottom>corners.Max(c=>c.y)+2,"Greeting overlaps music playback controls");
        Set(story,"chapter",data.groups.First(g=>g.Scenario==1));Set(story,"automaticDialogue",false);ui.RefreshNow();
        Require(Ref("StorySystemUI","_mainStoryTextParent").gameObject.activeInHierarchy&&!Ref("StorySystemUI","_normalTextParent").gameObject.activeSelf,"Episode did not restore main story text");
        Set(story,"chapter",null);Set(story,"current",null);Set(story,"message","");Set(story,"shownChars",0);ui.RefreshNow();
        Require(!Ref("StorySystemUI","self").gameObject.activeSelf&&!Ref("StorySystemUI","_autoButton").gameObject.activeSelf,"Dialogue remains open after ending");
        File.WriteAllText(Path.GetFullPath("../research/dialogue-layout-check.json"),"{\"passed\":true,\"sourceMessageClassification\":true,\"greetingAboveMusic\":true,\"episodeControlsHiddenDuringGreeting\":true,\"mainStoryRestored\":true,\"clearedOnEnd\":true,\"browserGameplayRun\":false,\"userSaveWritten\":false}");
        Debug.Log("HLC_DIALOGUE_LAYOUT_CHECK_PASSED sourceMessageClassification=true greetingAboveMusic=true");
    }
    public static void BuildChecked(){Run();ChillRemainingCheck.Run();ChillPhysicsBake.Build();ChillNativeUIBuild.Build();}
}
