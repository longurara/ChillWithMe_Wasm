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
        Directory.CreateDirectory("Assets/Resources");File.Copy("../src/gameplay-ui/i18n.json","Assets/Resources/chill-port-i18n.json",true);AssetDatabase.ImportAsset("Assets/Resources/chill-port-i18n.json");
        var old=player.GetComponent<ChillGameplayUI>();if(old)UnityEngine.Object.DestroyImmediate(old);
        var ui=player.GetComponent<ChillNativeUI>() ?? player.gameObject.AddComponent<ChillNativeUI>();ui.player=player;
        if(!player.GetComponent<ChillPerformanceCapture>())player.gameObject.AddComponent<ChillPerformanceCapture>();
        ui.configuration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/native-ui.json");
        if(!ui.configuration)throw new InvalidOperationException("Run prepare_native_ui.py first");
        var all=UnityEngine.Object.FindObjectsOfType<Transform>(true);
        foreach(var penguin in all.Where(t=>t.name=="m_penguin"&&t.GetComponent<Collider>()))
        {
            var click=penguin.GetComponent<ChillPenguinClick>()??penguin.gameObject.AddComponent<ChillPenguinClick>();click.story=player.story;
        }
        ui.canvas=all.First(t=>PathOf(t)=="Paremt/PCPlatform/Canvas").GetComponent<Canvas>();
        for(var t=ui.canvas.transform;t;t=t.parent)t.gameObject.SetActive(true);
        ui.canvas.enabled=true;ui.canvas.renderMode=RenderMode.ScreenSpaceOverlay;ui.canvas.overrideSorting=true;ui.canvas.sortingOrder=100;
        foreach(var subCanvas in ui.canvas.GetComponentsInChildren<Canvas>(true))
        {
            if(subCanvas==ui.canvas||!subCanvas.overrideSorting)continue;
            if(subCanvas.sortingOrder<100)subCanvas.sortingOrder+=100;
            if(subCanvas.name=="SetBirthdayConfirmDialog_PC"&&subCanvas.sortingOrder<=102)subCanvas.sortingOrder=103;
        }
        var scaler=ui.canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        if(!ui.canvas.GetComponent<GraphicRaycaster>())ui.canvas.gameObject.AddComponent<GraphicRaycaster>();
        foreach(var eventSystem in UnityEngine.Object.FindObjectsOfType<EventSystem>(true)){if(eventSystem.gameObject.name=="Chill native UI input")UnityEngine.Object.DestroyImmediate(eventSystem.gameObject);else eventSystem.gameObject.SetActive(false);}
        new GameObject("Chill native UI input",typeof(EventSystem),typeof(StandaloneInputModule));
        GameObject Prefab(string name)=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recovered/GameObject/"+name+".prefab");
        ui.musicCell=Prefab("MusicCell");ui.storyCell=Prefab("StoryCell");ui.todoCell=Prefab("TodoCell");ui.todoListCell=Prefab("SelectTodoListCell");ui.selectionCell=Prefab("SelectionButton");
        ui.storyCategoryTitlePrefab=Prefab("Title");ui.calendarHabitCell=Prefab("CalendarCompleteHabitCell");ui.specialReadIconPrefab=Prefab("SpecialReadIcon");
        ui.specialCells=new[]{Prefab("SpecialCellAlterEgo"),Prefab("SpecialCellBearsRestaurant"),Prefab("SpecialCellValentine2026"),Prefab("SpecialCellLunaNewYear2026"),Prefab("SpecialCellNearSpring2026"),Prefab("SpecialCellMagicalPrincess"),Prefab("SpecialCellStatisticsDev")};
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
        ui.fallbackFont.atlasPopulationMode=AtlasPopulationMode.Dynamic;
        if(ui.fallbackFont.sourceFontFile)
        {
            string symbols="✓☑□×↑↓←→•–—…%+−/0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            ui.fallbackFont.TryAddCharacters(symbols,out _);
            foreach(var atlas in ui.fallbackFont.atlasTextures)if(atlas&&!AssetDatabase.IsSubAsset(atlas))AssetDatabase.AddObjectToAsset(atlas,ui.fallbackFont);
            EditorUtility.SetDirty(ui.fallbackFont);
        }
        const string prefix="Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilitySetting/General/ScrollView";
        var generalScroll=all.First(t=>PathOf(t)==prefix) as RectTransform;
        generalScroll.anchorMin=generalScroll.anchorMax=generalScroll.pivot=new Vector2(1,1);generalScroll.anchoredPosition=new Vector2(-22,-146.2f);generalScroll.sizeDelta=new Vector2(1152,590.1f);
        var content=generalScroll.Find("Viewport/Content");
        var contentRect=(RectTransform)content;contentRect.anchorMin=new Vector2(1,1);contentRect.anchorMax=new Vector2(1,1);contentRect.pivot=new Vector2(1,1);contentRect.anchoredPosition=Vector2.zero;
        var vlg=content.GetComponent<VerticalLayoutGroup>();if(vlg){vlg.enabled=true;vlg.padding=new RectOffset(0,6,53,0);vlg.childAlignment=TextAnchor.UpperLeft;vlg.spacing=0;vlg.childForceExpandWidth=true;vlg.childForceExpandHeight=true;vlg.childControlWidth=false;vlg.childControlHeight=false;vlg.childScaleHeight=true;vlg.reverseArrangement=true;}
        var csf=content.GetComponent<ContentSizeFitter>();if(csf){csf.enabled=true;csf.horizontalFit=ContentSizeFitter.FitMode.PreferredSize;csf.verticalFit=ContentSizeFitter.FitMode.PreferredSize;}
        foreach(var oldWeb in content.Cast<Transform>().Where(t=>t.name.StartsWith("Web")).ToArray())UnityEngine.Object.DestroyImmediate(oldWeb.gameObject);
        var source=generalScroll.Find("GeneralInitButton").gameObject;source.SetActive(true);
        var sourceRect=(RectTransform)source.transform;sourceRect.anchorMin=sourceRect.anchorMax=new Vector2(.5f,0);sourceRect.pivot=new Vector2(.5f,.5f);sourceRect.anchoredPosition=new Vector2(-8.4f,2.5f);sourceRect.sizeDelta=new Vector2(283.1f,39.8f);
        var languageRow=(RectTransform)content.Find("ChangeLanguage");languageRow.gameObject.SetActive(true);languageRow.anchorMin=languageRow.anchorMax=new Vector2(0,1);languageRow.pivot=new Vector2(.5f,.5f);languageRow.anchoredPosition=new Vector2(630,-205.25f);languageRow.sizeDelta=new Vector2(1260,60.9f);
        var languageLabel=languageRow.Find("TitleText") as RectTransform;if(languageLabel){languageLabel.anchorMin=languageLabel.anchorMax=new Vector2(0,.5f);languageLabel.pivot=new Vector2(0,.5f);languageLabel.anchoredPosition=new Vector2(324,0);languageLabel.sizeDelta=new Vector2(194.75f,41.7f);}
        var languageList=languageRow.Find("LanguagePulldownList") as RectTransform;if(languageList){languageList.anchorMin=languageList.anchorMax=new Vector2(0,1);languageList.pivot=new Vector2(.5f,.5f);languageList.anchoredPosition=new Vector2(841,-10.3f);languageList.sizeDelta=new Vector2(343,40);}
        var faqTemplate=content.Find("FAQ").gameObject;
        string[] saveNames={"WebExportSaveButton","WebImportSaveButton"},saveCaptions={"Export web save","Import web save"};
        for(int i=0;i<saveNames.Length;i++)
        {
            var row=UnityEngine.Object.Instantiate(faqTemplate,content,false);row.name=saveNames[i];row.SetActive(true);
            var rect=(RectTransform)row.transform;rect.sizeDelta=new Vector2(1260,60.9f);
            var faqIcon=row.transform.Find("TitleText/FAQButton");if(faqIcon)UnityEngine.Object.DestroyImmediate(faqIcon.gameObject);
            var titleText=row.transform.Find("TitleText")?.GetComponent<TMP_Text>();if(titleText){titleText.text=saveCaptions[i];titleText.richText=false;}
            var actionBtn=UnityEngine.Object.Instantiate(source,row.transform,false);actionBtn.name="ActionButton";actionBtn.SetActive(true);
            var btnRect=(RectTransform)actionBtn.transform;btnRect.anchorMin=btnRect.anchorMax=new Vector2(0,.5f);btnRect.pivot=new Vector2(.5f,.5f);btnRect.anchoredPosition=new Vector2(841,0);btnRect.sizeDelta=new Vector2(343,40);
            var btnLabel=actionBtn.GetComponentInChildren<TMP_Text>(true);if(btnLabel){btnLabel.text=saveCaptions[i];btnLabel.richText=false;}
            var rowBtn=row.GetComponent<Button>()??row.AddComponent<Button>();rowBtn.targetGraphic=actionBtn.GetComponent<Graphic>();
        }
        string[] generalOrder={"WebImportSaveButton","WebExportSaveButton","InquiryID","FAQ","UIHelp","SaveDataUploadInterval","SelectLunaNewYear2026","ChangeLanguage","ChangeDayOfWeek","ChangeAlwaysOnTop","SelectTimeOfDayType"};
        string[] generalHidden={"InquiryID","SaveDataUploadInterval","SelectLunaNewYear2026"};
        for(int i=0;i<generalOrder.Length;i++)
        {
            var row=content.Find(generalOrder[i]) as RectTransform;if(!row)continue;
            row.sizeDelta=new Vector2(1260,60.9f);row.SetSiblingIndex(i);
            row.gameObject.SetActive(!generalHidden.Contains(generalOrder[i]));
        }
        var graphics=all.First(t=>PathOf(t).EndsWith("UI_FacilitySetting/Graphics"));
        var graphicsScroll=graphics.Find("ScrollView") as RectTransform;
        if(graphicsScroll){graphicsScroll.anchorMin=graphicsScroll.anchorMax=graphicsScroll.pivot=new Vector2(1,1);graphicsScroll.anchoredPosition=new Vector2(-22,-146.2f);graphicsScroll.sizeDelta=new Vector2(1152,590.1f);}
        var graphicsContent=graphics.Find("ScrollView/Viewport/Content");
        if(graphicsContent)
        {
            var gRect=(RectTransform)graphicsContent;gRect.anchorMin=gRect.anchorMax=new Vector2(1,1);gRect.pivot=new Vector2(1,1);gRect.anchoredPosition=Vector2.zero;
            foreach(var oldWeb in graphicsContent.Cast<Transform>().Where(t=>t.name.StartsWith("Web")).ToArray())UnityEngine.Object.DestroyImmediate(oldWeb.gameObject);
            var gvlg=graphicsContent.GetComponent<VerticalLayoutGroup>();if(gvlg){gvlg.enabled=true;gvlg.padding=new RectOffset(0,6,53,0);gvlg.childAlignment=TextAnchor.UpperLeft;gvlg.spacing=0;gvlg.childForceExpandWidth=true;gvlg.childForceExpandHeight=true;gvlg.childControlWidth=false;gvlg.childControlHeight=false;gvlg.childScaleHeight=true;gvlg.reverseArrangement=true;}
            var gcsf=graphicsContent.GetComponent<ContentSizeFitter>();if(gcsf){gcsf.enabled=true;gcsf.horizontalFit=ContentSizeFitter.FitMode.PreferredSize;gcsf.verticalFit=ContentSizeFitter.FitMode.PreferredSize;}
            string[] graphicsOrder={"FrameRate","ChangeAlwaysOnTop","RenderScale","GraphicQualityPulldownList","SelectPulldownList","WindowMode"};
            for(int i=0;i<graphicsOrder.Length;i++){var row=graphicsContent.Find(graphicsOrder[i]) as RectTransform;if(row){row.sizeDelta=new Vector2(1260,graphicsOrder[i]=="FrameRate"?0f:60.9f);row.SetSiblingIndex(i);row.gameObject.SetActive(true);}}
        }
        var audioContent=all.FirstOrDefault(t=>PathOf(t).EndsWith("UI_FacilitySetting/MusicAudio/ScrollView/Viewport/Content")) as RectTransform;
        if(audioContent){audioContent.anchorMin=audioContent.anchorMax=new Vector2(1,1);audioContent.pivot=new Vector2(1,1);audioContent.anchoredPosition=Vector2.zero;}
        var graphicInit=graphics.Find("GraphicInitButton")??graphics.Find("GraphicsInitButton");if(graphicInit)graphicInit.gameObject.SetActive(true);
        foreach(var b in graphics.GetComponentsInChildren<Button>(true))b.interactable=true;
        ConfigureControls(player,ui,content,source);
        if(!ui.musicCell || !ui.storyCell || !ui.todoCell || !ui.todoListCell || !ui.selectionCell || !ui.storyCategoryTitlePrefab || !ui.calendarHabitCell || !ui.specialReadIconPrefab || ui.specialCells.Any(c=>!c) || !ui.musicPlay || !ui.musicPause)throw new InvalidOperationException("Native UI assets missing");
        ChillNativeSpriteBuild.Configure(ui);
        ChillLightingBuild.Configure();
        return ui;
    }
    static void ConfigureControls(ChillGraphicsPlayer player,ChillNativeUI ui,Transform content,GameObject nativeButton)
    {
        ui.workModeButtons=Array.Empty<Button>();
        ui.historySettingsButton=ui.historyDialogueButton=ui.historyCloseButton=null;
        ui.historyPanel=null;ui.historyInput=ui.historyTitle=null;
        ui.clearEnvironmentButton=null;
        var environmentRoot=ui.canvas.transform.Find("UI/ChangeOrderObjects/UI_FacilityEnviroment");
        var oldClear=environmentRoot.Find("WebClearEnvironment");if(oldClear)UnityEngine.Object.DestroyImmediate(oldClear.gameObject);
        var environmentScroll=environmentRoot.Find("EnviromentScrollView").GetComponent<ScrollRect>();
        var envScrollRect=(RectTransform)environmentScroll.transform;envScrollRect.anchorMin=new Vector2(1,0);envScrollRect.anchorMax=new Vector2(1,1);envScrollRect.pivot=new Vector2(1,1);envScrollRect.anchoredPosition=new Vector2(0,-240);envScrollRect.sizeDelta=new Vector2(603.5f,-384.9f);
        if(environmentScroll.viewport){environmentScroll.viewport.anchorMin=Vector2.zero;environmentScroll.viewport.anchorMax=Vector2.one;environmentScroll.viewport.pivot=new Vector2(0,1);environmentScroll.viewport.anchoredPosition=Vector2.zero;environmentScroll.viewport.sizeDelta=Vector2.zero;}
        var environmentContent=environmentRoot.Find("EnviromentScrollView/Viewport/Content");
        var envContentRect=(RectTransform)environmentContent;envContentRect.anchorMin=new Vector2(0,1);envContentRect.anchorMax=new Vector2(0,1);envContentRect.pivot=new Vector2(0,1);envContentRect.anchoredPosition=Vector2.zero;
        var envVlg=environmentContent.GetComponent<VerticalLayoutGroup>();if(envVlg){envVlg.enabled=true;envVlg.padding=new RectOffset(30,0,0,0);envVlg.spacing=3.6f;envVlg.childAlignment=TextAnchor.UpperCenter;envVlg.childControlWidth=false;envVlg.childControlHeight=false;envVlg.childForceExpandWidth=true;envVlg.childForceExpandHeight=true;}
        var envCsf=environmentContent.GetComponent<ContentSizeFitter>();if(envCsf){envCsf.enabled=true;envCsf.horizontalFit=ContentSizeFitter.FitMode.PreferredSize;envCsf.verticalFit=ContentSizeFitter.FitMode.PreferredSize;}
        foreach(Transform child in environmentContent)child.gameObject.SetActive(true);
        var grid0=environmentContent.Find("GridLayout");
        var grid1=environmentContent.Find("GridLayout (1)");
        var grid2=environmentContent.Find("GridLayout (2)");
        void SetupEnvGrid(Transform gRoot,float spacingY,int count)
        {
            if(!gRoot)return;
            int rows=(count+3)/4;
            var gr=(RectTransform)gRoot;gr.anchorMin=gr.anchorMax=Vector2.zero;gr.pivot=new Vector2(.5f,.5f);gr.sizeDelta=new Vector2(112f,rows*(112f+spacingY));
            var glg=gRoot.GetComponent<GridLayoutGroup>();if(glg){glg.enabled=true;glg.cellSize=new Vector2(112,112);glg.spacing=new Vector2(9.24f,spacingY);glg.padding=new RectOffset(32,0,-3,0);glg.startCorner=GridLayoutGroup.Corner.UpperLeft;glg.startAxis=GridLayoutGroup.Axis.Horizontal;glg.childAlignment=TextAnchor.UpperLeft;glg.constraint=GridLayoutGroup.Constraint.FixedColumnCount;glg.constraintCount=4;}
            var gcsf=gRoot.GetComponent<ContentSizeFitter>();if(gcsf)gcsf.enabled=false;
        }
        SetupEnvGrid(grid0,32.56f,12);SetupEnvGrid(grid1,0f,13);SetupEnvGrid(grid2,32.56f,17);
        var cardSpecs=new(string cardName,Transform grid,string item,int ambientId,string locKey)[]
        {
            ("ChangeLightRainButton",grid0,"LightRain",8,"ui_enviroment_lightrain_01"),
            ("ChangeHeavyRainButton",grid0,"HeavyRain",9,"ui_enviroment_heavyrain_01"),
            ("ChangeThunderRainButton",grid0,"ThunderRain",10,"ui_enviroment_rainthunder_01"),
            ("ChangeWindBellButton",grid0,"WindBell",7,"ui_enviroment_windbell_01"),
            ("ChangeHorizonButton",grid0,"Horizon",29,"ui_enviroment_horizon_01"),
            ("ChangeDeepSeaButton",grid0,"DeepSea",3,"ui_enviroment_deepsea_01"),
            ("ChangeJellyfishButton",grid0,"Jellyfish",26,"ui_enviroment_jellyfish_01"),
            ("ChangeWhaleButton",grid0,"Whale",19,"ui_enviroment_whales_01"),
            ("ChangeFireworksButton",grid0,"Fireworks",5,"ui_enviroment_fireworks_01"),
            ("ChangeHotSpringButton",grid0,"HotSpring",20,"ui_enviroment_hotspring_01"),
            ("ChangeLocomotiveButton",grid0,"Locomotive",21,"ui_enviroment_locomotive_01"),
            ("ChangeCityButton",grid0,"City",25,"ui_enviroment_city_01"),
            ("ChangeBalloonButton",grid1,"Balloon",-1,"ui_enviroment_balloon_01"),
            ("ChangeSpaceButton",grid1,"Space",-1,"ui_enviroment_universe_01"),
            ("ChangeSpaceBButton",grid1,"SpaceB",-1,"ui_enviroment_universe_02"),
            ("ChangeSakuraButton",grid1,"Sakura",-1,"ui_enviroment_sakura_01"),
            ("ChangeFreshGreeneryButton",grid1,"FreshGreenery",-1,"ui_enviroment_freshgreenery_01"),
            ("ChangeSnowButton",grid1,"Snow",-1,"ui_enviroment_snow_01"),
            ("ChangeAuroraButton",grid1,"Aurora",-1,"ui_enviroment_aurora_01"),
            ("ChangeBooksButtonPrefab",grid1,"Books",-1,"ui_enviroment_book_01"),
            ("ChangeJetButton",grid1,"Jet",-1,"ui_enviroment_jet_01"),
            ("ChangeBlueButterflyButton",grid1,"BlueButterfly",-1,"ui_enviroment_alterego_01"),
            ("ChangeValentineSweetsButton",grid1,"ValentineSweets",-1,"ui_enviroment_valentineSweets_01"),
            ("ChangeTulipButton",grid1,"Tulip",-1,"ui_enviroment_tulip_01"),
            ("ChangeFerrisWheelButton",grid1,"FerrisWheel",-1,"ui_enviroment_ferriswheel_01"),
            ("ChangeRadioNoiseButton",grid2,"RadioNoise",0,"ui_enviroment_radionoise_01"),
            ("ChangePinkNoiseButton",grid2,"PinkNoise",1,"ui_enviroment_pinknoise_01"),
            ("ChangeRecordNoiseButton",grid2,"RecordNoise",2,"ui_enviroment_recordnoise_01"),
            ("ChangeRoomNoiseButton",grid2,"RoomNoise",23,"ui_enviroment_roomnoise_01"),
            ("ChangeSeaWaveButton",grid2,"Sea",22,"ui_enviroment_seawave_01"),
            ("ChangeBrookButton",grid2,"Brook",28,"ui_enviroment_brook_01"),
            ("ChangeWindButton",grid2,"Wind",4,"ui_enviroment_wind_01"),
            ("ChangeCookSimmerButton",grid2,"CookSimmer",11,"ui_enviroment_cook_01"),
            ("ChangeCook(B)Button",grid2,"CookTypeB",24,"ui_enviroment_cook_02"),
            ("ChangeFireplaceButton",grid2,"Fireplace",27,"ui_enviroment_fireplace_01"),
            ("ChangeCricketsButton",grid2,"Crickets",12,"ui_enviroment_crickets_01"),
            ("ChangeFrog1Button",grid2,"Frog1",13,"ui_enviroment_frog_01"),
            ("ChangeFrog2Button",grid2,"Frog2",14,"ui_enviroment_frog_02"),
            ("ChangeChicadaButton",grid2,"Chicada",15,"ui_enviroment_chicada_01"),
            ("ChangeHIgurashiButton",grid2,"Higurashi",16,"ui_enviroment_higurashi_01"),
            ("ChangetTurtledoveButton",grid2,"TurtleDove",17,"ui_enviroment_turtledove_01"),
            ("ChangeBirdChorusButton",grid2,"BirdChorus",18,"ui_enviroment_birdchorus_01")
        };
        var envShopMap=new System.Collections.Generic.Dictionary<string,int>{{"City",35},{"Jellyfish",36},{"Aurora",39},{"Tulip",41},{"FerrisWheel",42},{"FreshGreenery",43},{"Horizon",45}};
        var envShopSprites=new System.Collections.Generic.Dictionary<int,Sprite>();
        string envIconDbPath="../Recovered/ExportedProject/Assets/MonoBehaviour/EnvironmentIconDB_PC.asset";
        if(File.Exists(envIconDbPath))
        {
            foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(envIconDbPath),@"- Key: (\d+)\r?\n      Value:\r?\n        ShopThumbnail: \{fileID: 21300000, guid: ([a-f0-9]+), type: 2\}"))
            {
                var sp=AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(m.Groups[2].Value));
                if(sp)envShopSprites[int.Parse(m.Groups[1].Value)]=sp;
            }
        }
        var skinThumbList=new System.Collections.Generic.List<ChillNativeUI.SkinThumbnail>();
        string decMasterPath="../Recovered/ExportedProject/Assets/MonoBehaviour/DecorationMaster.asset";
        if(File.Exists(decMasterPath))
        {
            foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(decMasterPath),@"- SkinType: (\d+)(?:\r?\n    [^\r\n]+)*?\r?\n    Thumbnail: \{fileID: 21300000, guid: ([a-f0-9]+), type: 2\}"))
            {
                var sp=AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(m.Groups[2].Value));
                if(sp)skinThumbList.Add(new ChillNativeUI.SkinThumbnail{skinId=int.Parse(m.Groups[1].Value),sprite=sp});
            }
        }
        ui.skinThumbnails=skinThumbList.ToArray();
        var allEnvCards=environmentContent.GetComponentsInChildren<Transform>(true).Where(t=>t.Find("MainButton")).ToDictionary(t=>t.name,t=>t);
        var choices=new System.Collections.Generic.List<ChillNativeUI.EnvironmentChoice>();
        foreach(var spec in cardSpecs)
        {
            if(!allEnvCards.TryGetValue(spec.cardName,out var card))continue;
            card.SetParent(spec.grid,false);card.SetAsLastSibling();card.gameObject.SetActive(true);
            var caption=card.Find("EnviromentNameText (TMP)")?.GetComponent<TMP_Text>();
            if(caption)caption.text=spec.locKey;
            var ambVol=card.Find("AmbientSoundVolume");if(ambVol)ambVol.gameObject.SetActive(spec.ambientId>=0);
            var shopBtn=card.Find("ShopButton");if(shopBtn)shopBtn.gameObject.SetActive(false);
            var newImg=card.Find("newImage");if(newImg)newImg.gameObject.SetActive(false);
            foreach(var group in card.GetComponentsInChildren<CanvasGroup>(true)){group.alpha=1;group.interactable=group.blocksRaycasts=true;}
            int envIndex=spec.grid!=grid2?player.environment.FindEnvironment(spec.item):0;
            Sprite shopThumb=envShopMap.TryGetValue(spec.item,out int ek)&&envShopSprites.TryGetValue(ek,out var es)?es:null;
            choices.Add(new ChillNativeUI.EnvironmentChoice{root=card.gameObject,index=envIndex,ambientId=spec.ambientId,itemName=spec.item,localizeKey=spec.locKey,main=card.Find("MainButton")?.GetComponent<Button>(),view=card.Find("ChangeEnviromentViewButton")?.GetComponent<Button>(),shopThumbnail=shopThumb});
        }
        ui.environmentChoices=choices.ToArray();
        environmentScroll.horizontal=false;environmentScroll.vertical=true;environmentScroll.verticalNormalizedPosition=1;
        foreach(string name in new[]{"LayoutPresetUI","PlayerPointUI"}){var native=environmentRoot.Find(name);if(native)native.gameObject.SetActive(true);}
        foreach(string name in new[]{"PurchasePopover","AutoTimeChanger"}){var popup=environmentRoot.Find(name);if(popup)popup.gameObject.SetActive(false);}
        foreach(string name in new[]{"AutoSettingButton","AutoButton"}){var auto=environmentRoot.Find("EnviromentOther/WeatherChangeButtons/"+name);if(auto)auto.gameObject.SetActive(true);}
        var root=ui.canvas.transform.Find("UI/ChangeOrderObjects");
        foreach(string oldName in new[]{"WebStoryHistory","WebDialogueHistory","WebStatusNotice"})
        {
            var oldA=ui.canvas.transform.Find(oldName);if(oldA)UnityEngine.Object.DestroyImmediate(oldA.gameObject);
            var oldB=root?root.Find(oldName):null;if(oldB)UnityEngine.Object.DestroyImmediate(oldB.gameObject);
        }
        var noticeParent=ui.canvas.transform.Find("SmallAnnounceParent");if(noticeParent)noticeParent.gameObject.SetActive(true);
        var notice=ui.canvas.transform.Find("SmallAnnounceParent/SmallAnnounceUI");
        ui.noticePanel=notice.gameObject;ui.noticeText=notice.GetComponentInChildren<TMP_Text>(true);ui.noticeText.richText=false;ui.noticeText.enableAutoSizing=true;ui.noticeText.fontSizeMin=18;ui.noticeText.fontSizeMax=24;
        var noticeRect=(RectTransform)notice;noticeRect.anchorMin=noticeRect.anchorMax=new Vector2(.5f,1);noticeRect.pivot=new Vector2(.5f,1);noticeRect.anchoredPosition=new Vector2(0,-75);noticeRect.sizeDelta=new Vector2(850,70);
        if(ui.noticeText.transform!=notice)Stretch((RectTransform)ui.noticeText.transform,new Vector2(20,8),new Vector2(-20,-8));
        notice.gameObject.SetActive(false);
        Debug.Log("HLC_NATIVE_CONTROLS environmentChoices="+ui.environmentChoices.Length+" language=En,Ja notices=ready");
        if(ui.environmentChoices.Length==0 || !ui.noticeText)throw new InvalidOperationException("Native control bindings missing.");
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
        if(!player.GetComponent<ChillStartupGate>())player.gameObject.AddComponent<ChillStartupGate>();
        player.bones.configuration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/bone-physics.json");
        player.bones.bakedConfiguration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/bone-bakes.bytes")??AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/bone-bakes.json");
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
        ui.Close();ui.Open("notes");ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-native-ui-notes.png");
        Debug.Log("HLC_CHILL_NATIVE_UI_PREVIEW hooks="+ui.HookCount);
    }
}
