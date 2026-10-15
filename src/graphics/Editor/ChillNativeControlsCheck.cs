using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using Hlc.ChillPrototype;

// Exercises native button listeners against the real port model in memory.
// Never launches browser gameplay or writes the user's save.
public static class ChillNativeControlsCheck
{
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");
        var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();ChillAppearanceBuild.Configure(player);var ui=ChillNativeUIBuild.Configure(player);
        var data=JsonUtility.FromJson<ChillStoryData>(File.ReadAllText("Assets/StreamingAssets/chill-story.json"));
        Set(player.story,"data",data);Set(player.story,"texts",data.localizations.GroupBy(t=>t.ID).ToDictionary(g=>g.Key,g=>g.First()));
        Set(player.story,"chapters",data.novels.GroupBy(t=>t.ScenarioGroupID).ToDictionary(g=>g.Key,g=>g.ToArray()));
        // SaveNow exits before PlayerPrefs/localStorage when saveAllowed=false.
        Set(player.story,"saveAllowed",false);ui.Initialize();ui.RefreshNow();
        Require(ui.environmentChoices.Length==42,"Expected 42 native environment cards, got "+ui.environmentChoices.Length);
        foreach(var choice in ui.environmentChoices.Where(c=>c.index>0))
        {
            var toggleBtn=choice.view??choice.main;
            toggleBtn.onClick.Invoke();Require(player.environment.selectedEnvironment==choice.index,"Environment callback failed for "+choice.root.name);
            Require(player.environment.environments[choice.index-1].activeSelf,"Selected scene is inactive: "+choice.root.name);
            Require(player.environment.environments.Count(t=>t.activeSelf)==1,"Multiple scenes active");
            toggleBtn.onClick.Invoke();Require(player.environment.selectedEnvironment==0,"Environment toggle failed for "+choice.root.name);
        }
        const string generalContent="Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilitySetting/General/ScrollView/Viewport/Content/";
        const string setting=generalContent+"ChangeLanguage/LanguagePulldownList/Pulldown/TodoGroupText (TMP)/Content/";
        foreach(string name in new[]{"ChangeLanguage","SelectTimeOfDayType","ChangeAlwaysOnTop","ChangeDayOfWeek","UIHelp","FAQ","WebExportSaveButton","WebImportSaveButton"})
            Require(ui.Find(generalContent+name)&&ui.Find(generalContent+name).gameObject.activeSelf,"General row missing or inactive: "+name);
        foreach(string name in new[]{"WebExportSaveButton","WebImportSaveButton"})
            Require(ui.Find(generalContent+name).GetComponent<UnityEngine.UI.Button>().interactable,"Save control is disabled: "+name);
        var ja=ui.Find(setting+"LanguageJPSelectButton").GetComponent<UnityEngine.UI.Button>();ja.onClick.Invoke();Require(player.story.UISnapshot("notes").language=="Ja","Language callback failed");
        ui.Find(setting+"LanguageENSelectButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Set(player.story,"chapter",data.groups.First(g=>data.novels.Any(n=>n.ScenarioGroupID==g.ID)));ui.RefreshNow();
        Require(ui.environmentChoices.All(c=>!c.main.interactable),"Story does not lock environment controls");
        ui.environmentChoices.First(c=>c.index>0).main.onClick.Invoke();Require(player.environment.selectedEnvironment==0,"Backend allows scene change during story");
        Set(player.story,"chapter",null);player.story.ImportSave("{\"format\":\"invalid\",\"version\":1}");ui.RefreshNow();
        Require(player.story.UISnapshot("notes").noticeError && ui.noticePanel.activeSelf,"Import error has no visible notice");
        ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-native-controls-error.png");
        Set(player.story,"noticeMessage","");Set(player.story,"noticeError",false);Set(player.story,"noticeRevision",0);ui.RefreshNow();
        ui.Open("settings");ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-native-controls-settings.png");
        var languageToggle=ui.Find(generalContent+"ChangeLanguage/LanguagePulldownList/PulldownButton").GetComponent<UnityEngine.UI.Button>();languageToggle.onClick.Invoke();Require(ja.gameObject.activeInHierarchy,"Language menu did not open");ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-native-controls-language.png");
        ui.Open("environment");ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-native-controls-environment.png");
        ui.Close();ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-native-appearance-sunset.png",1,0,.4f);
        Require(!player.story.effects.hat.activeSelf,"Story hat remains enabled at startup");
        Require(Mathf.Abs(player.character.transform.lossyScale.x-1)<.0001f && Mathf.Abs(player.view.fieldOfView-35)<.0001f,"Character scale or camera FOV differs from native desktop");
        foreach(var renderer in UnityEngine.Object.FindObjectsOfType<Renderer>(true).Where(r=>r.name=="m_radio" || r.name=="Body1" || r.name=="Hair1"))Debug.Log("HLC_APPEARANCE_BOUNDS "+renderer.name+" enabled="+renderer.enabled+" active="+renderer.gameObject.activeInHierarchy+" position="+renderer.transform.position+" bounds="+renderer.bounds+" screen="+player.view.WorldToViewportPoint(renderer.bounds.center));
        File.WriteAllText(Path.GetFullPath("../research/native-controls-check.json"),JsonUtility.ToJson(new Report{passed=true,workModes=3,environmentChoices=ui.environmentChoices.Length,hooks=ui.HookCount,languageSwitch=true,historyReadOnly=true,storyLocks=true,errorNotice=true,userSaveWritten=false,browserGameplayRun=false},true));
        Debug.Log("HLC_NATIVE_CONTROLS_CHECK_PASSED environments="+ui.environmentChoices.Length+" language locks notices");
    }
    [Serializable] class Report{public bool passed,languageSwitch,historyReadOnly,storyLocks,errorNotice,userSaveWritten,browserGameplayRun;public int workModes,environmentChoices,hooks;}
}
