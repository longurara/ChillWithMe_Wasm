using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Hlc.ChillPrototype;

public static class ChillNativeUIBuild
{
    static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
    static void FixedLayout(Transform root)
    {
        foreach(var layout in root.GetComponents<LayoutGroup>())layout.enabled=false;
        foreach(var fitter in root.GetComponents<ContentSizeFitter>())fitter.enabled=false;
    }
    static void Stretch(RectTransform rect,Vector2 min,Vector2 max)
    {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=min;rect.offsetMax=max;}
    public static ChillNativeUI Configure(ChillGraphicsPlayer player)
    {
        var old=player.GetComponent<ChillGameplayUI>();if(old)UnityEngine.Object.DestroyImmediate(old);
        var ui=player.GetComponent<ChillNativeUI>() ?? player.gameObject.AddComponent<ChillNativeUI>();ui.player=player;
        ui.configuration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/native-ui.json");
        if(!ui.configuration)throw new InvalidOperationException("Run prepare_native_ui.py first");
        var all=UnityEngine.Object.FindObjectsOfType<Transform>(true);
        ui.canvas=all.First(t=>PathOf(t)=="Paremt/PCPlatform/Canvas").GetComponent<Canvas>();
        for(var t=ui.canvas.transform;t;t=t.parent)t.gameObject.SetActive(true);
        ui.canvas.enabled=true;ui.canvas.renderMode=RenderMode.ScreenSpaceOverlay;ui.canvas.overrideSorting=true;ui.canvas.sortingOrder=100;
        var scaler=ui.canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        if(!ui.canvas.GetComponent<GraphicRaycaster>())ui.canvas.gameObject.AddComponent<GraphicRaycaster>();
        foreach(var eventSystem in UnityEngine.Object.FindObjectsOfType<EventSystem>(true)){if(eventSystem.gameObject.name=="Chill native UI input")UnityEngine.Object.DestroyImmediate(eventSystem.gameObject);else eventSystem.gameObject.SetActive(false);}
        new GameObject("Chill native UI input",typeof(EventSystem),typeof(StandaloneInputModule));
        GameObject Prefab(string name)=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recovered/GameObject/"+name+".prefab");
        ui.musicCell=Prefab("MusicCell");ui.storyCell=Prefab("StoryCell");ui.todoCell=Prefab("TodoCell");ui.selectionCell=Prefab("SelectionButton");
        var data=JsonUtility.FromJson<ChillNativeUI.Configuration>(ui.configuration.text);
        Sprite Sprite(string script,string key)
        {string guid=data.bindings.FirstOrDefault(b=>b.script==script)?.refs.FirstOrDefault(r=>r.name==key)?.asset;return string.IsNullOrEmpty(guid)?null:AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(AssetDatabase.GUIDToAssetPath(guid));}
        ui.musicPlay=Sprite("MusicUI","_playButtonSprite");ui.musicPause=Sprite("MusicUI","_pauseButtonSprite");ui.timerPlay=Sprite("PomodoroTimerStateView","_playButtonSprite");ui.timerPause=Sprite("PomodoroTimerStateView","_pauseButtonSprite");
        ui.tutorialSprites=new Sprite[28];int tutorialIndex=0;
        foreach(string page in new[]{"screen","pomodoroTimer","levelAndStory","newEnvironment"})foreach(string locale in new[]{"EN","JP","SC","TC","PR","KR","RU"})ui.tutorialSprites[tutorialIndex++]=Sprite("TutorialView","_"+page+"Sprite"+locale);
        ui.countupPlay=Sprite("CountupTimerStateView","_playButtonSprite");ui.countupPause=Sprite("CountupTimerStateView","_pauseButtonSprite");
        ui.wantTalkNormal=Sprite("WantTalkUI","_wantTalkNormalSprite");
        const string fontPath="Assets/Prototype/Fonts/NativeUIFallback.asset";
        ui.fallbackFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        if(!ui.fallbackFont)
        {
            ui.fallbackFont=TMP_FontAsset.CreateFontAsset(player.uiFont,48,6,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
            ui.fallbackFont.name="Native UI supplemental glyphs";AssetDatabase.CreateAsset(ui.fallbackFont,fontPath);
            AssetDatabase.AddObjectToAsset(ui.fallbackFont.material,ui.fallbackFont);foreach(var atlas in ui.fallbackFont.atlasTextures)if(atlas)AssetDatabase.AddObjectToAsset(atlas,ui.fallbackFont);
        }
        // Save tools use clones of the game's own settings button, font and frame.
        const string prefix="Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilitySetting/General/ScrollView";
        var content=all.First(t=>PathOf(t)==prefix+"/Viewport/Content");
        FixedLayout(content);var contentRect=(RectTransform)content;
        contentRect.anchorMin=new Vector2(0,1);contentRect.anchorMax=new Vector2(1,1);contentRect.pivot=new Vector2(.5f,1);contentRect.anchoredPosition=Vector2.zero;contentRect.sizeDelta=new Vector2(0,590);
        var source=all.First(t=>PathOf(t)==prefix+"/GeneralInitButton").gameObject;
        string[] names={"WebSaveProgressButton","WebExportSaveButton","WebImportSaveButton"},captions={"Save progress","Export web save","Import web save"};
        for(int i=0;i<names.Length;i++)
        {
            var existing=content.Find(names[i]);var button=existing?existing.gameObject:UnityEngine.Object.Instantiate(source,content,false);button.name=names[i];button.SetActive(true);
            var rect=(RectTransform)button.transform;rect.anchorMin=new Vector2(.5f,1);rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-250-i*78);rect.sizeDelta=new Vector2(470,60);
            var label=button.GetComponentInChildren<TMP_Text>(true);if(label)label.text=captions[i];
        }
        foreach(var t in content.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==content && !t.name.StartsWith("Web") && t.name!="ChangeLanguage"))t.gameObject.SetActive(false);
        var languageRow=(RectTransform)content.Find("ChangeLanguage");languageRow.gameObject.SetActive(true);languageRow.anchorMin=languageRow.anchorMax=new Vector2(.5f,1);languageRow.pivot=new Vector2(.5f,1);languageRow.anchoredPosition=new Vector2(0,-35);languageRow.sizeDelta=new Vector2(570,80);
        var languageLabel=languageRow.Find("TitleText") as RectTransform;if(languageLabel){languageLabel.anchorMin=languageLabel.anchorMax=new Vector2(0,.5f);languageLabel.pivot=new Vector2(0,.5f);languageLabel.anchoredPosition=new Vector2(0,0);}
        var languageList=languageRow.Find("LanguagePulldownList") as RectTransform;if(languageList){languageList.anchorMin=languageList.anchorMax=new Vector2(1,1);languageList.pivot=new Vector2(1,1);languageList.anchoredPosition=new Vector2(0,-10);}
        source.SetActive(false);
        ConfigureControls(player,ui,content,source);
        var graphics=all.First(t=>PathOf(t).EndsWith("UI_FacilitySetting/Graphics"));
        foreach(var b in graphics.GetComponentsInChildren<Button>(true))b.interactable=false;
        if(!ui.musicCell || !ui.storyCell || !ui.todoCell || !ui.selectionCell || !ui.musicPlay || !ui.musicPause)throw new InvalidOperationException("Native UI assets missing");
        ChillNativeSpriteBuild.Configure(ui);
        ChillLightingBuild.Configure();
        return ui;
    }
    static void ConfigureControls(ChillGraphicsPlayer player,ChillNativeUI ui,Transform content,GameObject nativeButton)
    {
        Button CopyButton(Transform parent,string name,string caption,Vector2 position,Vector2 size)
        {
            var existing=parent.Find(name);var go=existing?existing.gameObject:UnityEngine.Object.Instantiate(nativeButton,parent,false);go.name=name;go.SetActive(true);
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=position;rect.sizeDelta=size;
            var label=go.GetComponentInChildren<TMP_Text>(true);if(label){label.text=caption;label.richText=false;}
            return go.GetComponent<Button>() ?? go.AddComponent<Button>();
        }
        ui.workModeButtons=new Button[3];string[] names={"Read","Write","Type"};
        for(int i=0;i<3;i++)ui.workModeButtons[i]=CopyButton(content,"WebWorkMode"+i,names[i],new Vector2((i-1)*185,-160),new Vector2(165,60));
        ui.historySettingsButton=CopyButton(content,"WebHistoryButton","Conversation history",new Vector2(0,-484),new Vector2(470,60));
        var environmentRoot=ui.canvas.transform.Find("UI/ChangeOrderObjects/UI_FacilityEnviroment");
        ui.environmentChoices=environmentRoot.GetComponentsInChildren<Transform>(true)
            .Where(t=>t.name.StartsWith("Change") && t.name.EndsWith("Button") && t.Find("MainButton"))
            .Select(t=>new {root=t,index=player.environment.FindEnvironment(t.name.Substring(6,t.name.Length-12))})
            .Where(t=>t.index>0).Select(t=>new ChillNativeUI.EnvironmentChoice{root=t.root.gameObject,index=t.index,main=t.root.Find("MainButton").GetComponent<Button>(),view=t.root.Find("ChangeEnviromentViewButton")?.GetComponent<Button>()}).ToArray();
        var environmentContent=environmentRoot.Find("EnviromentScrollView/Viewport/Content");FixedLayout(environmentContent);
        var gridRoot=environmentContent.Find("GridLayout");var grid=gridRoot.GetComponent<GridLayoutGroup>();
        foreach(Transform child in environmentContent)if(child!=gridRoot)child.gameObject.SetActive(false);
        foreach(Transform child in gridRoot)child.gameObject.SetActive(false);
        foreach(var choice in ui.environmentChoices)
        {
            choice.root.transform.SetParent(gridRoot,false);choice.root.SetActive(true);
            // Some cloned source cards retain the Fireworks placeholder label;
            // their original service replaced it from the environment master.
            var caption=choice.root.transform.Find("EnviromentNameText (TMP)")?.GetComponent<TMP_Text>();
            string environmentName=choice.root.name.Substring(6,choice.root.name.Length-12),label=null;
            switch(environmentName){case "HotSpring":label="ui_enviroment_hotspring_01";break;case "BlueButterfly":label="ui_enviroment_alterego_01";break;case "ValentineSweets":label="ui_enviroment_valentineSweets_01";break;case "FreshGreenery":label="ui_enviroment_freshgreenery_01";break;case "SpaceB":label="ui_enviroment_universe_02";break;}
            if(caption && label!=null)caption.text=label;
            foreach(string name in new[]{"ShopButton","AmbientSoundVolume","newImage"}){var unused=choice.root.transform.Find(name);if(unused)unused.gameObject.SetActive(false);}
            foreach(var group in choice.root.GetComponentsInChildren<CanvasGroup>(true)){group.alpha=1;group.interactable=group.blocksRaycasts=true;}
        }
        // The original section layout was managed by stripped Windows scripts.
        // Keep the native cards in one scrollable grid with stable row heights.
        grid.enabled=true;grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=4;grid.cellSize=new Vector2(112,135);grid.spacing=new Vector2(20,20);grid.padding=new RectOffset(15,15,10,10);grid.startCorner=GridLayoutGroup.Corner.UpperLeft;grid.startAxis=GridLayoutGroup.Axis.Horizontal;grid.childAlignment=TextAnchor.UpperCenter;
        foreach(var fitter in gridRoot.GetComponents<ContentSizeFitter>())fitter.enabled=false;
        float gridHeight=Mathf.Ceil(ui.environmentChoices.Length/4f)*155+20;
        var envContentRect=(RectTransform)environmentContent;envContentRect.anchorMin=new Vector2(0,1);envContentRect.anchorMax=new Vector2(1,1);envContentRect.pivot=new Vector2(.5f,1);envContentRect.anchoredPosition=Vector2.zero;envContentRect.sizeDelta=new Vector2(0,gridHeight);
        Stretch((RectTransform)gridRoot,Vector2.zero,Vector2.zero);
        var environmentScroll=environmentRoot.Find("EnviromentScrollView").GetComponent<ScrollRect>();Stretch((RectTransform)environmentScroll.transform,new Vector2(18,85),new Vector2(-18,-240));Stretch(environmentScroll.viewport,Vector2.zero,Vector2.zero);environmentScroll.horizontal=false;environmentScroll.vertical=true;environmentScroll.verticalNormalizedPosition=1;
        foreach(string name in new[]{"LayoutPresetUI","PurchasePopover","PlayerPointUI","AutoTimeChanger"}){var unsupported=environmentRoot.Find(name);if(unsupported)unsupported.gameObject.SetActive(false);}
        foreach(string name in new[]{"AutoSettingButton","AutoButton"}){var auto=environmentRoot.Find("EnviromentOther/WeatherChangeButtons/"+name);if(auto)auto.gameObject.SetActive(false);}
        ui.clearEnvironmentButton=CopyButton(environmentRoot,"WebClearEnvironment","Sky only",Vector2.zero,new Vector2(200,45));
        var clear=(RectTransform)ui.clearEnvironmentButton.transform;clear.anchorMin=clear.anchorMax=new Vector2(.5f,0);clear.pivot=new Vector2(.5f,0);clear.anchoredPosition=new Vector2(0,15);
        var root=ui.canvas.transform.Find("UI/ChangeOrderObjects");var note=root.Find("UI_FacilityNote");var history=ui.canvas.transform.Find("WebStoryHistory") ?? root.Find("WebStoryHistory");
        if(!history){history=UnityEngine.Object.Instantiate(note,root,false);history.name="WebStoryHistory";}
        // Long stories hide ChangeOrderObjects in the source. Keep the web
        // conversation history alongside dialogue so its button still works.
        history.SetParent(ui.canvas.transform,false);
        ui.historyPanel=history.gameObject;history.gameObject.SetActive(false);
        var historyRect=(RectTransform)history;historyRect.anchorMin=historyRect.anchorMax=new Vector2(.5f,.5f);historyRect.pivot=new Vector2(.5f,.5f);historyRect.anchoredPosition=Vector2.zero;historyRect.sizeDelta=new Vector2(660,560);
        ui.historyCloseButton=history.Find("CloseButtonUI").GetComponent<Button>();
        ui.historyInput=history.Find("Note/InputField (TMP)").GetComponent<TMP_InputField>();
        ui.historyTitle=history.Find("Note/Title InputField (TMP)").GetComponent<TMP_InputField>();
        foreach(string name in new[]{"SelectNotes","AddPageButtonUI","NoteTitleText"}){var unused=history.Find(name);if(unused)unused.gameObject.SetActive(false);}
        var historyBody=(RectTransform)history.Find("Note");Stretch(historyBody,new Vector2(25,30),new Vector2(-25,-15));
        foreach(string name in new[]{"Scroll View","Image","UpperLineImage (1)"}){var unused=historyBody.Find(name);if(unused)unused.gameObject.SetActive(false);}
        var titleRect=(RectTransform)ui.historyTitle.transform;titleRect.anchorMin=new Vector2(0,1);titleRect.anchorMax=new Vector2(1,1);titleRect.pivot=new Vector2(.5f,1);titleRect.anchoredPosition=new Vector2(0,-10);titleRect.sizeDelta=new Vector2(-40,40);
        Stretch((RectTransform)ui.historyInput.transform,Vector2.zero,new Vector2(-25,-65));ui.historyInput.lineType=TMP_InputField.LineType.MultiLineNewline;
        var historyScrollbar=historyBody.Find("Scrollbar Vertical") as RectTransform;if(historyScrollbar){historyScrollbar.anchorMin=new Vector2(1,0);historyScrollbar.anchorMax=Vector2.one;historyScrollbar.pivot=new Vector2(1,.5f);historyScrollbar.anchoredPosition=new Vector2(0,-32.5f);historyScrollbar.sizeDelta=new Vector2(14.5f,-65);}
        foreach(var button in history.GetComponentsInChildren<Button>(true))if(button!=ui.historyCloseButton && button.name!="DragButton")button.gameObject.SetActive(false);
        ui.historyInput.readOnly=true;ui.historyTitle.readOnly=true;
        var dialogueButton=ui.canvas.transform.Find("WebDialogueHistory");
        if(!dialogueButton){dialogueButton=UnityEngine.Object.Instantiate(ui.canvas.transform.Find("SkipButton"),ui.canvas.transform,false);dialogueButton.name="WebDialogueHistory";}
        ui.historyDialogueButton=dialogueButton.GetComponent<Button>();var dialogueRect=(RectTransform)dialogueButton;dialogueRect.anchoredPosition=new Vector2(405,139);dialogueRect.sizeDelta=new Vector2(150,30);
        var notice=ui.canvas.transform.Find("WebStatusNotice");
        if(!notice){notice=UnityEngine.Object.Instantiate(ui.canvas.transform.Find("SmallAnnounceParent/SmallAnnounceUI"),ui.canvas.transform,false);notice.name="WebStatusNotice";}
        ui.noticePanel=notice.gameObject;ui.noticeText=notice.GetComponentInChildren<TMP_Text>(true);ui.noticeText.richText=false;ui.noticeText.enableAutoSizing=true;ui.noticeText.fontSizeMin=18;ui.noticeText.fontSizeMax=24;
        var noticeRect=(RectTransform)notice;noticeRect.anchorMin=noticeRect.anchorMax=new Vector2(.5f,1);noticeRect.pivot=new Vector2(.5f,1);noticeRect.anchoredPosition=new Vector2(0,-75);noticeRect.sizeDelta=new Vector2(850,70);notice.gameObject.SetActive(false);
        Debug.Log("HLC_NATIVE_CONTROLS workModes=3 environmentChoices="+ui.environmentChoices.Length+" language=En,Ja history=ready notices=ready");
        if(ui.environmentChoices.Length==0 || ui.workModeButtons.Any(b=>!b) || !ui.historyInput || !ui.noticeText)throw new InvalidOperationException("Native web control bindings missing.");
    }
    public static void Build()
    { Build(false); }
    public static void BuildWithLighting()
    {
        ChillLightingBuild.CaptureDeskLamp();Build(true);
    }
    static void Build(bool refreshLightingBundles)
    {
        var scene=EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();
        ChillAppearanceBuild.Configure(player);var ui=Configure(player);ChillFeaturesBuild.Configure(player);
        player.bones.configuration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/bone-physics.json");
        player.bones.bakedConfiguration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/bone-bakes.json");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("HLC_CHILL_NATIVE_UI_READY canvas="+PathOf(ui.canvas.transform)+" nativePrefabs=4");
        if(refreshLightingBundles)ChillAssets.RebuildForLighting();
        ChillBuild.BuildWebGL();
    }
    public static void Preview()
    {
        EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();Configure(player);
        // Reload the configured scene only for subsequent full builds; this
        // preview keeps scene changes in memory and writes no player data.
        var ui=player.GetComponent<ChillNativeUI>();ui.Initialize();ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-native-ui-clean.png");
        foreach(var input in ui.canvas.GetComponentsInChildren<TMP_InputField>(true).Where(t=>t.name.Contains("TimeInput")))Debug.Log("HLC_NATIVE_INPUT "+PathOf(input.transform)+" value="+input.text+" component="+(input.textComponent?input.textComponent.name:"null")+" rendered="+(input.textComponent?input.textComponent.text:"null")+" placeholder="+(input.placeholder?input.placeholder.gameObject.activeSelf.ToString():"none"));
        foreach(var text in ui.canvas.GetComponentsInChildren<TMP_Text>().Where(t=>PathOf(t.transform).Contains("Pomodoro")))Debug.Log("HLC_NATIVE_TIMER_TEXT "+PathOf(text.transform)+" text="+text.text+" enabled="+text.enabled);
        ui.Open("music");ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-native-ui-music.png");
        ui.Open("notes");ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-native-ui-notes.png");
        Debug.Log("HLC_CHILL_NATIVE_UI_PREVIEW hooks="+ui.HookCount);
    }
}
