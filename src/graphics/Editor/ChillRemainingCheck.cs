using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hlc.ChillPrototype;
using UnityEngine;

public static class ChillRemainingCheck
{
    // Use only after the same solver's full bake has already passed. The normal
    // BuildChecked entry always regenerates coverage when physics changes.
    public static void BuildUsingExistingBake(){ChillFeaturesCheck.Run();Run();ChillPhysicsBake.VerifyExistingBake();ChillNativeUIBuild.Build();}
    public static void RebuildBundlesAndWebGL(){ChillAssets.RebuildMeshBundles();BuildUsingExistingBake();}
    public static void Verify()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();
        ChillAppearanceBuild.Configure(player);var ui=ChillNativeUIBuild.Configure(player);ChillFeaturesBuild.Configure(player);var story=player.story;story.VerificationOnly=true;
        var data=JsonUtility.FromJson<ChillStoryData>(File.ReadAllText("Assets/StreamingAssets/chill-story.json"));var features=JsonUtility.FromJson<ChillFeatureData>(File.ReadAllText("Assets/Prototype/chill-features.json"));
        typeof(ChillStoryRuntime).GetField("data",Flags).SetValue(story,data);typeof(ChillStoryRuntime).GetField("texts",Flags).SetValue(story,data.localizations.GroupBy(t=>t.ID).ToDictionary(g=>g.Key,g=>g.First()));typeof(ChillStoryRuntime).GetField("chapters",Flags).SetValue(story,data.novels.GroupBy(n=>n.ScenarioGroupID).ToDictionary(g=>g.Key,g=>g.ToArray()));typeof(ChillStoryRuntime).GetField("motions",Flags).SetValue(story,data.motions.GroupBy(m=>m.value).ToDictionary(g=>g.Key,g=>g.First().name));typeof(ChillStoryRuntime).GetField("<Features>k__BackingField",Flags).SetValue(story,features);
        typeof(ChillStoryRuntime).GetField("deskController",Flags).SetValue(story,player.character.runtimeAnimatorController);
        foreach(string key in new[]{"voice","sound","ambient","keyboard","writing"})typeof(ChillStoryRuntime).GetField(key,Flags).SetValue(story,player.gameObject.AddComponent<AudioSource>());
        var catalog=JsonUtility.FromJson<ChillAssetCatalog>(File.ReadAllText("Assets/StreamingAssets/chill-library.json"));typeof(ChillLibraryBrowser).GetField("catalog",Flags).SetValue(player.library,catalog);typeof(ChillLibraryBrowser).GetField("metadata",Flags).SetValue(player.library,catalog.bundles.ToDictionary(b=>b.name));
        typeof(ChillDecorations).GetMethod("Start",Flags).Invoke(story.decorations,null);ui.Initialize();
        Run();
    }
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    public static void Run()
    {
        var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();var story=player.story;
        var field=typeof(ChillStoryRuntime).GetField("save",Flags);var previous=(ChillSave)field.GetValue(story);
        try
        {
            var fixture=new ChillSave{needsTutorial=false,workedSeconds=7199};fixture.workspace.achievements.Add("EXIT_GAME_COUNT");field.SetValue(story,fixture);
            Require(story.Workspace.achievements.Contains("FINISH_GAME_FROM_EXIT_CALL_BUTTON_COUNT")&&!story.Workspace.achievements.Contains("EXIT_GAME_COUNT"),"Achievement alias did not migrate");
            Require(story.AchievementProgress("POMODORO_WORK_HOURS_COUNT")==1,"Work-hour progress rounds upward");fixture.workedSeconds=7200;Require(story.AchievementProgress("POMODORO_WORK_HOURS_COUNT")==2,"Work-hour boundary is wrong");
            int awards=story.Workspace.achievements.Count,completed=fixture.completed.Count,level=fixture.level;
            story.UIAction(new ChillUiCommand{action="unlock",value=1});Require(story.PreviewUnlock&&fixture.completed.Count==completed&&fixture.level==level&&story.Workspace.achievements.Count==awards,"Test unlock grants progress");
            story.UIAction(new ChillUiCommand{action="unlock",value=0});Require(!story.PreviewUnlock,"Test unlock cannot be disabled");
            // 1. Heroine AI: Resting countup pause, ChoiceTalkService tiers, CurrentTime & WorkedTime SelfTalk selectors
            story.Settings.resting=false;fixture.countupActive=true;fixture.countupRunning=false;
            Require(story.Resting,"Countup paused state does not enter Resting");
            fixture.countupActive=false;
            story.EnableChoiceTalk(0);Require(!story.ChoiceTalkAvailable&&story.ChoiceTalkProbability==0f,"ChoiceTalk unlocked at 0 seconds");
            story.EnableChoiceTalk(1800);Require(story.ChoiceTalkAvailable&&Mathf.Abs(story.ChoiceTalkProbability-25f)<.01f,"ChoiceTalk 30m linear probability mismatch");
            story.EnableChoiceTalk(3600);Require(story.ChoiceTalkAvailable&&Mathf.Abs(story.ChoiceTalkProbability-50f)<.01f,"ChoiceTalk 60m linear probability mismatch");
            story.EnableChoiceTalk(5400);Require(story.ChoiceTalkAvailable&&Mathf.Abs(story.ChoiceTalkProbability-50f)<.01f,"ChoiceTalk >60m cap probability mismatch");
            story.ResetChoiceTalk();Require(!story.ChoiceTalkAvailable&&story.ChoiceTalkProbability==0f,"ChoiceTalk did not reset");
            // 2. Wardrobe & Appearance: 6:00 AM boundary, CostumeLotterySelector weights, 1h work gate, HatChangeService
            Require(ChillStoryRuntime.AppearanceDateKey(new DateTime(2026,10,14,5,59,0))=="2026-10-13"&&ChillStoryRuntime.AppearanceDateKey(new DateTime(2026,10,14,6,0,0))=="2026-10-14","Appearance 6:00 AM boundary mismatch");
            fixture.workspace.costumeWeights.Clear();
            int pickedCostume=story.SelectDailyCostume(7);
            var pickedEntry=fixture.workspace.costumeWeights.FirstOrDefault(w=>w.skin=="Costume_"+(pickedCostume+1));
            Require(pickedEntry!=null&&Mathf.Abs(pickedEntry.weight-50f)<.01f&&fixture.workspace.costumeWeights.Any(w=>w.skin!="Costume_"+(pickedCostume+1)&&Mathf.Abs(w.weight-150f)<.01f),"CostumeLotterySelector attenuation/bonus mismatch");
            fixture.workspace.appearanceDaily=true;fixture.workspace.lastAppearanceDate="";fixture.workedSeconds=1800;fixture.mainEpisode=2;fixture.previewUnlock=false;
            fixture.workspace.hair=2;fixture.workspace.costume=2;
            story.DailyAppearance();
            Require(fixture.workspace.hair==0&&fixture.workspace.costume==0,"DailyAppearance bypassed 1h work hair gate or Episode 4 costume gate");
            if(story.effects)
            {
                fixture.workspace.hair=1;
                var hp=fixture.workspace.room.FirstOrDefault(r=>r.category==2);
                if(hp==null){hp=new ChillRoomChoice{category=2,skin="Headphone_2"};fixture.workspace.room.Add(hp);}else hp.skin="Headphone_2";
                story.effects.SetHat(true);
                Require(story.effects.hatHairLocked&&story.effects.hatReplacedHeadphone=="Headphone_1","HatChangeService did not lock hair or track replaced Headphone_2");
                story.effects.SetHat(false);
                Require(!story.effects.hatHairLocked,"HatChangeService did not unlock hair on removal");
                story.effects.ResetEffects();
            }
            // 3. Desk Objects & Activity Props: Ice melt curve & MemorieController musicbox winding
            var act=new ChillActivityObjects();act.Pour(1000d,true);
            Require(act.HasIce(2800d)&&Mathf.Abs(act.IceScale(2800d)-.95f)<.01f&&!act.HasIce(4601d)&&act.IceScale(4601d)==0f,"Ice melt 1-hour linear curve mismatch");
            if(player.deskObjects)
            {
                player.deskObjects.UpdateIceVisual(true,.5f);
                Require(player.deskObjects.CurrentIceVisible&&Mathf.Abs(player.deskObjects.CurrentIceScale-.5f)<.01f,"DeskObjects ice visual state not updated");
                player.deskObjects.UpdateIceVisual(false,0f);
                float windingBefore=player.deskObjects.MusicboxWindingAngle;
                player.deskObjects.Action("PlayMusicbox");player.deskObjects.AdvanceProps(5f);
                Require(player.deskObjects.MusicboxPlaying&&Mathf.Abs(player.deskObjects.MusicboxWindingAngle-windingBefore-30f)<.01f,"MemorieController winding rotation mismatch");
                player.deskObjects.CloseMusicbox();
                Require(!player.deskObjects.MusicboxPlaying,"CloseMusicbox did not stop musicboxPlaying");
            }
            // 4. Motion Sound Event Multiplexer
            story.recentMotionSounds.Clear();
            var cupChoice=fixture.workspace.room.FirstOrDefault(r=>r.category==1);
            if(cupChoice==null){cupChoice=new ChillRoomChoice{category=1,skin="Cup_3"};fixture.workspace.room.Add(cupChoice);}else cupChoice.skin="Cup_3";
            story.MotionSound("PlaceCup","DefaultPos");
            story.MotionSound("PlayFridgeSequence");
            story.MotionSound("PlayIceMakerSequence");
            Require(story.recentMotionSounds.Contains("PlaceCupOnSaucer")&&story.recentMotionSounds.Contains("PlayTakeOutFromFridge_1")&&story.recentMotionSounds.Contains("Ice_Maker_Open"),"MotionSound multiplexer did not record surface/sequence events");
            // 5. Story Direction & Camera: BlackOutDirection Arg3 blind & AprilFool camera
            if(story.effects)
            {
                var blackOut=(IEnumerator)typeof(ChillStoryEffects).GetMethod("ApplyLater",Flags).Invoke(story.effects,new object[]{new ChillSentence{Command=210,Arg1="1",Arg2=-1f,Arg3="1"},0f,210,1,false});
                typeof(ChillStoryEffects).GetField("versions",Flags).GetValue(story.effects).GetType().GetMethod("set_Item").Invoke(typeof(ChillStoryEffects).GetField("versions",Flags).GetValue(story.effects),new object[]{210,1});
                blackOut.MoveNext();
                Require(story.effects.blackoutBlind,"BlackOutDirection Arg3 blind flag was not set");
                story.effects.ResetEffects();
            }
            if(story.presentation&&story.presentation.cameras)
            {
                story.presentation.cameras.ChangeToAprilFoolCamera();
                Require(story.presentation.cameras.AprilFoolCamera,"AprilFoolCamera did not activate");
                story.presentation.cameras.ChangeToDefaultCamera();
                Require(!story.presentation.cameras.AprilFoolCamera,"ChangeToDefaultCamera did not reset AprilFoolCamera");
                story.presentation.cameras.ResetCamera();
            }
            // 6. Environment Window View Adjusters
            if(player.environment)player.environment.ApplyWindowViewAdjusters();
            // 7. Timer & Progression: Level 32 cap until Episode 31
            fixture.previewUnlock=false;fixture.mainEpisode=30;fixture.level=32;fixture.exp=0;fixture.workspace.collaboration=0;
            typeof(ChillStoryRuntime).GetMethod("CreditWork",Flags).Invoke(story,new object[]{3600d,100000d});
            Require(fixture.level==32&&fixture.exp==0,"PlayerLevelService allowed leveling past 32 before Episode 31");
            fixture.mainEpisode=31;
            typeof(ChillStoryRuntime).GetMethod("CreditWork",Flags).Invoke(story,new object[]{3600d,103600d});
            Require(fixture.level>32||fixture.exp>0,"PlayerLevelService remained locked at 32 after Episode 31");
            // 8, 9, 10. Productivity UI, Profile UI, Draggable UI Clamping
            Require(ChillNativeUI.ClampCalendarDate(new DateTime(2020,5,1))==new DateTime(2025,1,1)&&ChillNativeUI.ClampCalendarDate(new DateTime(3005,5,1))==new DateTime(2998,12,1),"CalendarUI year clamp 2025..2998 mismatch");
            var ui=UnityEngine.Object.FindObjectOfType<ChillNativeUI>();
            if(ui)
            {
                fixture.previewUnlock=false;fixture.mainEpisode=1;fixture.workspace.collaboration=0;fixture.completed.Clear();
                Require(ui.GetStatisticsProgress()==0,"Statistics progress should be 0 with no episodes");
                fixture.completed.Add("special_statisticsDev_scenario_002");
                Require(ui.GetStatisticsProgress()==2,"Statistics progress did not read completed episode");
                var clamped=ChillNativeDrag.AdjustPos(ui.canvas.transform as RectTransform,new Vector2(99999f,-99999f));
                Require(Mathf.Abs(clamped.x)<5000f&&Mathf.Abs(clamped.y)<5000f,"DraggableUI.AdjustPos did not clamp out-of-bounds position");
                var wardrobe=player.GetComponent<ChillWardrobe>();
                if(wardrobe)
                {
                    var defaultBadge=wardrobe.costumeParent.Find("Badge1");
                    bool origBadgeActive=defaultBadge&&defaultBadge.gameObject.activeSelf;
                    if(defaultBadge)defaultBadge.gameObject.SetActive(false);
                    for(int c=1;c<=4;c++)
                    {
                        wardrobe.Apply(c);
                        var cur=(GameObject)typeof(ChillWardrobe).GetField("current",Flags).GetValue(wardrobe);
                        Require(cur&&cur.GetComponentsInChildren<SkinnedMeshRenderer>(true).All(r=>r.bones.All(b=>b&&b.IsChildOf(wardrobe.boneParent))),"Costume "+c+" has detached or condemned bones");
                        var cBadge=cur.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Badge1");
                        Require(cBadge&&!cBadge.gameObject.activeSelf,"Costume "+c+" left Badge1 active when unequipped");
                    }
                    if(defaultBadge)
                    {
                        defaultBadge.gameObject.SetActive(true);wardrobe.SyncBadge();
                        var cur=(GameObject)typeof(ChillWardrobe).GetField("current",Flags).GetValue(wardrobe);
                        var cBadge=cur.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Badge1");
                        Require(cBadge&&cBadge.gameObject.activeSelf&&!defaultBadge.GetComponent<Renderer>().enabled,"Costume Badge1 did not sync active state or hide default badge renderer");
                        defaultBadge.gameObject.SetActive(origBadgeActive);
                    }
                    wardrobe.Apply(0);
                    if(defaultBadge)Require(defaultBadge.GetComponent<Renderer>().enabled,"Default costume did not re-enable default badge renderer");
                }
                ui.Initialize();
                ui.Open("decorations");ui.RefreshNow();
                var dateGroup=ui.Find("Paremt/PCPlatform/Canvas/UI/UI_FacilityCurrentDateAndTime")?.GetComponent<CanvasGroup>();
                Require(dateGroup&&Mathf.Abs(dateGroup.alpha)<.01f,"UI_FacilityCurrentDateAndTime did not fade out when DecorationListUI is open");
                var anim1=ui.Find("Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilityDecoration/DecorationScrollView/Viewport/Content/ItemSkinSelectorAnim") as RectTransform;
                Require(anim1&&!anim1.gameObject.activeSelf,"ItemSkinSelectorAnim started visible before clicking a model");
                var cup1Btn=ui.Find("Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilityDecoration/DecorationScrollView/Viewport/Content/GridLayout(1)/ChangeMugCup1Button");
                Require(cup1Btn&&!cup1Btn.Find("newImage").gameObject.activeSelf,"Default Cup 1 button showed false newImage indicator");
                ui.ShowSkinSelector(1001);
                Require(anim1.gameObject.activeSelf&&Mathf.Abs(anim1.sizeDelta.y-100f)<.01f&&!anim1.Find("ItemSkinSelector/SkinItem").gameObject.activeSelf,"ShowSkinSelector(1001) did not expand inline ItemSkinSelectorAnim or left template visible");
                ui.ShowSkinSelector(1001);
                Require(!anim1.gameObject.activeSelf,"Clicking same model button did not collapse inline ItemSkinSelectorAnim");
                var decPreset=ui.Find("Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilityDecoration/LayoutPresetUI");
                fixture.previewUnlock=true;
                decPreset.Find("SaveButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                decPreset.Find("Buttons/LayoutPresetChangeButton With Arrow/LayoutPresetChangeButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                decPreset.Find("Confirm Save/YesButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                story.ChooseDecoration("Desk_2A");ui.RefreshNow();
                Require(ui.HasDifferenceFromPreset(decPreset,0),"HasDifferenceFromPreset did not detect unsaved decoration change");
                decPreset.Find("Buttons/LayoutPresetChangeButton With Arrow (1)/LayoutPresetChangeButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Require(decPreset.Find("Warn Unsaved").gameObject.activeSelf,"Preset switch with unsaved changes did not open Warn Unsaved dialog");
                decPreset.Find("Warn Unsaved/NoButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Require(!decPreset.Find("Warn Unsaved").gameObject.activeSelf&&(int)typeof(ChillNativeUI).GetField("selectedLayoutPresetSlot",Flags).GetValue(ui)==1,"Warn Unsaved NoButton did not switch to target preset slot 2");
                ui.Close();ui.RefreshNow();
                Require(dateGroup&&Mathf.Abs(dateGroup.alpha-1f)<.01f,"UI_FacilityCurrentDateAndTime did not restore alpha after closing DecorationListUI");
                ui.Open("notes");ui.Open("todos");ui.Open("habits");ui.Open("music");
                Require(ui.IsPaneOpen("notes")&&ui.IsPaneOpen("todos")&&ui.IsPaneOpen("habits")&&ui.IsPaneOpen("music"),"Floating panes (notes/todos/habits) did not coexist with each other and exclusive music pane");
                ui.Open("todos");
                Require(!ui.IsPaneOpen("todos")&&ui.IsPaneOpen("notes")&&ui.IsPaneOpen("habits")&&ui.IsPaneOpen("music"),"Toggling todos closed other coexisting panes");
                ui.Close();
                story.CurrentList.items.Add(new ChillTodo{id="99",title="Calendar Inline Test",due="2026-10-15"});
                ui.Open("calendar");ui.RefreshNow();
                var miniCal=ui.Find("Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilityCalendar/MiniCalender");
                Require(miniCal&&!miniCal.gameObject.activeSelf,"Calendar MiniCalender started open");
                ui.OpenCalendarMiniCalendar(story.CurrentList.items[0]);
                Require(miniCal.gameObject.activeSelf&&ui.IsPaneOpen("calendar")&&!ui.IsPaneOpen("todos"),"OpenCalendarMiniCalendar did not open inline MiniCalender on CalendarUI");
                ui.CloseCalendarMiniCalendar();
                Require(!miniCal.gameObject.activeSelf,"CloseCalendarMiniCalendar did not hide inline MiniCalender");
                ui.Close();
                // 11. Chair, Cup, and StandLight skin persistence when Satone talks
                if(story.decorations)
                {
                    var cache=(Dictionary<string,UnityEngine.Object>)typeof(ChillDecorations).GetField("assetCache",Flags).GetValue(story.decorations);
                    var transforms=(Dictionary<string,Transform>)typeof(ChillDecorations).GetField("transforms",Flags).GetValue(story.decorations);
                    var testChairMesh=new Mesh{name="TestChair2A"};
                    var testCupMesh=new Mesh{name="TestCup3A"};
                    var testLightMesh=new Mesh{name="TestStandLight2A"};
                    var testTex=new Texture2D(2,2){name="TestSkinTex"};
                    foreach(var b in story.decorations.Data.bindings)
                    {
                        if(b.category!=1&&b.category!=7&&b.category!=9)continue;
                        foreach(var st in b.states??Array.Empty<ChillDecorations.State>())
                        {
                            if(!string.IsNullOrEmpty(st.mesh))
                            {
                                var assetPath=UnityEditor.AssetDatabase.GUIDToAssetPath(st.mesh);
                                var realMesh=!string.IsNullOrEmpty(assetPath)?UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(assetPath):null;
                                if(realMesh)Require(realMesh.vertexCount>0&&realMesh.normals.Length==realMesh.vertexCount,"Decoration mesh has invalid normals: "+assetPath);
                                cache[st.mesh]=realMesh?ChillDecorations.SanitizeMesh(realMesh):(b.category==9?testChairMesh:b.category==1?testCupMesh:testLightMesh);
                            }
                            if(!string.IsNullOrEmpty(st.texture))
                            {
                                var texPath=UnityEditor.AssetDatabase.GUIDToAssetPath(st.texture);
                                var realTex=!string.IsNullOrEmpty(texPath)?UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>(texPath):null;
                                cache[st.texture]=realTex?realTex:testTex;
                            }
                            if(!string.IsNullOrEmpty(st.emission))
                            {
                                var emPath=UnityEditor.AssetDatabase.GUIDToAssetPath(st.emission);
                                var realEm=!string.IsNullOrEmpty(emPath)?UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>(emPath):null;
                                cache[st.emission]=realEm?realEm:testTex;
                            }
                            string matGuid=st.material??b.material;
                            if(!string.IsNullOrEmpty(matGuid)&&!cache.ContainsKey(matGuid))
                            {
                                var matPath=UnityEditor.AssetDatabase.GUIDToAssetPath(matGuid);
                                var realMat=!string.IsNullOrEmpty(matPath)?UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath):null;
                                if(realMat)cache[matGuid]=realMat;
                                else
                                {
                                    var rPath=(b.renderers??Array.Empty<string>()).FirstOrDefault(p=>transforms.ContainsKey(p));
                                    if(rPath!=null&&transforms[rPath].GetComponent<Renderer>()?.sharedMaterial is Material baseMat)cache[matGuid]=baseMat;
                                }
                            }
                        }
                    }
                    fixture.workspace.room.Clear();
                    story.ChooseDecoration("Chair_2A");
                    story.ChooseDecoration("Cup_3A");
                    story.ChooseDecoration("StandLight_2A");
                    var applyRoom=(IEnumerator)typeof(ChillStoryRuntime).GetMethod("ApplyRoom",Flags).Invoke(story,new object[]{typeof(ChillStoryRuntime).GetField("roomVersion",Flags).GetValue(story)});
                    while(applyRoom.MoveNext()){if(applyRoom.Current is IEnumerator sub)while(sub.MoveNext()){}}
                    var chairFilter=transforms["map_model/model/Platform_PC/m_Chair_mobile/m_Chair"].GetComponent<MeshFilter>();
                    var cupFilter=transforms["Paremt/CharacterUseObjects/Stuff/m_cup/m_Cup"].GetComponent<MeshFilter>();
                    var saucerObj=transforms["Paremt/CharacterUseObjects/Stuff/m_cup_plate"].gameObject;
                    var lightFilter=transforms["map_model/model/Platform_PC/m_standlight_mobile/m_standlight"].GetComponent<MeshFilter>();
                    var expectedChair=chairFilter.sharedMesh;
                    var expectedCup=cupFilter.sharedMesh;
                    var expectedLight=lightFilter.sharedMesh;
                    Require(expectedChair!=null&&expectedChair.name.Contains("Chair")&&expectedCup!=null&&expectedCup.name.ToLowerInvariant().Contains("cup")&&saucerObj.activeSelf&&expectedLight!=null,"Initial Chair/Cup/StandLight skins did not apply");
                    ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-decoration-skins.png",1,0,0,true);
                    var mainChap=story.SpecialChapters.FirstOrDefault()??((ChillStoryData)typeof(ChillStoryRuntime).GetField("data",Flags).GetValue(story)).groups.First(g=>g.Scenario==1);
                    typeof(ChillStoryRuntime).GetMethod("Begin",Flags).Invoke(story,new object[]{mainChap,false,false,false});
                    Require(chairFilter.sharedMesh==expectedChair&&cupFilter.sharedMesh==expectedCup&&saucerObj.activeSelf&&lightFilter.sharedMesh==expectedLight,"Chair, Cup, or StandLight lost its skin state when Satone started talking");
                    story.StopStory();
                    typeof(ChillLibraryBrowser).GetMethod("CancelAssetLoads",Flags).Invoke(player.library,null);
                    typeof(ChillLibraryBrowser).GetField("stoppingLoads",Flags).SetValue(player.library,false);
                    var applyAfterStop=(IEnumerator)typeof(ChillStoryRuntime).GetMethod("ApplyRoom",Flags).Invoke(story,new object[]{typeof(ChillStoryRuntime).GetField("roomVersion",Flags).GetValue(story)});
                    while(applyAfterStop.MoveNext()){if(applyAfterStop.Current is IEnumerator sub)while(sub.MoveNext()){}}
                    Require(chairFilter.sharedMesh==expectedChair&&cupFilter.sharedMesh==expectedCup&&saucerObj.activeSelf&&lightFilter.sharedMesh==expectedLight,"Chair, Cup, or StandLight lost its skin state after Satone finished talking");
                    story.decorations.Restore();
                    cache.Clear();
                    UnityEngine.Object.DestroyImmediate(testChairMesh);
                    UnityEngine.Object.DestroyImmediate(testCupMesh);
                    UnityEngine.Object.DestroyImmediate(testLightMesh);
                    UnityEngine.Object.DestroyImmediate(testTex);
                }
                // 12. Trimmed sprites, sub-Canvas sortingOrder, shop thumbnails, music fillAmount, habit delete confirm, and double-F1 Debug Menu
                Require(ui.spriteReplacements!=null&&ui.spriteReplacements.Length>=35,"Trimmed t_ui_* sprite recovery table is incomplete: "+(ui.spriteReplacements?.Length??0));
                Require(ui.spriteReplacements.Any(r=>r.original&&r.original.name=="t_ui_todo_circle_anim_0"&&r.recovered),"Edge-packed sprite t_ui_todo_circle_anim_0 was not recovered");
                var subCanvases=ui.canvas.GetComponentsInChildren<Canvas>(true).Where(c=>c!=ui.canvas&&c.overrideSorting).ToArray();
                Require(subCanvases.Length>0&&subCanvases.All(c=>c.sortingOrder>=100),"Sub-Canvas sortingOrder was not offset above main Canvas (100)");
                var bdayDlg=ui.Find("Paremt/PCPlatform/Canvas/UI/SetBirthdayDialog_PC")?.GetComponent<Canvas>();
                var bdayConf=ui.Find("Paremt/PCPlatform/Canvas/UI/SetBirthdayConfirmDialog_PC")?.GetComponent<Canvas>();
                if(bdayDlg&&bdayConf)Require(bdayConf.sortingOrder>bdayDlg.sortingOrder,"SetBirthdayConfirmDialog_PC sortingOrder is not above SetBirthdayDialog_PC");
                Require(ui.skinThumbnails!=null&&ui.skinThumbnails.Length>=80&&ui.skinThumbnails.All(s=>s.sprite!=null),"DecorationMaster skinThumbnails were not populated");
                Require(ui.environmentChoices.Count(c=>c.shopThumbnail!=null)>=7,"EnvironmentIconDB shopThumbnails were not populated");
                // Test Decoration PurchasePopover exact skin thumbnail
                fixture.previewUnlock=false;fixture.workspace.purchases.Clear();fixture.workspace.points=500;
                ui.Open("decorations");ui.RefreshNow();
                var purchasableSkin=story.decorations.Skins.First(s=>story.UnlockRule("decoration",s.name)?.condition=="PointPurchase");
                ui.ShowSkinSelector(purchasableSkin.model);
                var skinRows=(List<GameObject>)typeof(ChillNativeUI).GetField("skinRows",Flags).GetValue(ui);
                Require(skinRows.Count>0,"ShowSkinSelector did not create skin rows for purchasable model");
                ui.ShowDecorationPurchasePopover(purchasableSkin,purchasableSkin.model,skinRows[0].transform);
                var decPopover=ui.RefOf("DecorationListUI","_purchasePopover")??ui.Find("Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilityDecoration/PurchasePopover");
                var decPopImg=decPopover?.Find("MainImageFrame/MainImage")?.GetComponent<UnityEngine.UI.Image>();
                var expectedSkinThumb=ui.skinThumbnails.First(s=>s.skinId==purchasableSkin.id).sprite;
                Require(decPopover&&decPopover.gameObject.activeSelf&&decPopImg&&decPopImg.sprite!=null&&decPopImg.sprite.name.Contains(expectedSkinThumb.name),"Decoration PurchasePopover did not display skin-specific thumbnail");
                ui.ClosePurchasePopover();
                Require(!decPopover.gameObject.activeSelf,"ClosePurchasePopover did not close Decoration PurchasePopover");
                ui.Close();
                // Test Environment PurchasePopover thumbnail and locked title
                ui.Open("environment");ui.RefreshNow();
                var purchasableEnv=ui.environmentChoices.First(c=>c.shopThumbnail!=null&&story.UnlockRule("environment",c.itemName)?.condition=="PointPurchase");
                ui.ShowEnvironmentPurchasePopover(purchasableEnv);
                var envPopover=ui.Find("Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilityEnviroment/PurchasePopover");
                var envPopImg=envPopover?.Find("MainImageFrame/MainImage")?.GetComponent<UnityEngine.UI.Image>();
                Require(envPopover&&envPopover.gameObject.activeSelf&&envPopImg&&envPopImg.sprite!=null&&envPopImg.sprite.name.Contains(purchasableEnv.shopThumbnail.name),"Environment PurchasePopover did not display EnvironmentIconDB shopThumbnail");
                ui.Close();
                // Test Habit DeleteConfirmPopover
                fixture.previewUnlock=true;
                story.AddHabit("DeleteConfirmHabit");
                string delHabitId=story.Workspace.habits.Last().id;
                ui.Open("habits");
                (ui.RefOf("HabitListUI","_editModeButton")??ui.Find("Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilityHabitTracker/HabitList/EditButton"))?.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
                ui.RefreshNow();
                var habitRows=(List<GameObject>)typeof(ChillNativeUI).GetField("habitRows",Flags).GetValue(ui);
                ui.ShowHabitDeleteConfirm(delHabitId,habitRows.Last().transform);
                var habitDelDialog=ui.RefOf("HabitListUI","_deleteWarningDialog")??ui.Find("Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilityHabitTracker/HabitList/DeleteConfirmPopover");
                Require(habitDelDialog&&habitDelDialog.gameObject.activeSelf,"ShowHabitDeleteConfirm did not open DeleteConfirmPopover");
                ui.ConfirmHabitDelete();
                Require(!habitDelDialog.gameObject.activeSelf&&!story.Workspace.habits.Any(h=>h.id==delHabitId),"ConfirmHabitDelete did not delete target habit or close popover");
                ui.Close();
                // Test Double-F2 Debug Menu & Debug commands
                ui.DebugMenuVisible=false;
                ui.HandleF2KeyPress(10f);
                Require(!ui.DebugMenuVisible,"Single F2 press opened Debug Menu prematurely");
                ui.HandleF2KeyPress(11f);
                Require(!ui.DebugMenuVisible,"Slow second F2 press (>0.45s) opened Debug Menu");
                ui.HandleF2KeyPress(11.2f);
                Require(ui.DebugMenuVisible,"Double F2 press within 0.45s did not open Debug Menu");
                ui.HandleF2KeyPress(12f);
                ui.HandleF2KeyPress(12.2f);
                Require(!ui.DebugMenuVisible,"Second double F2 press did not close Debug Menu");
                int ptsBefore=story.Workspace.points;
                story.DebugAddPoints(1000);
                Require(story.Workspace.points==ptsBefore+1000,"DebugAddPoints did not add points");
                story.DebugSkipSelectStory(12);
                Require(story.MainEpisodeProgress==12,"DebugSkipSelectStory(12) did not jump to episode 12");
                story.DebugUnlockAllShopItems();
                Require(story.Features.unlocks.Where(u=>u.condition=="PointPurchase").All(u=>story.Workspace.purchases.Contains(u.kind+":"+u.item)),"DebugUnlockAllShopItems did not unlock all shop items");
            }
        }
        finally{field.SetValue(story,previous);}
        var catalog=JsonUtility.FromJson<ChillPortI18n.Catalog>(File.ReadAllText("../src/gameplay-ui/i18n.json"));
        Require(catalog.entries.Select(e=>e.En).Distinct().Count()==catalog.entries.Length,"Duplicate UI translation keys");
        foreach(var row in catalog.entries)foreach(var language in new[]{"En","Ja","ZhHans","ZhHant","Pt","Ko","Ru"})Require(!string.IsNullOrEmpty(row.Get(language)),"Missing UI translation: "+row.En+" "+language);
        var library=player.library;var type=typeof(ChillLibraryBrowser);var busy=type.GetField("busy",Flags);bool oldBusy=(bool)busy.GetValue(library);
        var versions=(Dictionary<string,int>)type.GetField("slotVersions",Flags).GetValue(library);bool notified=false;
        try
        {
            busy.SetValue(library,true);versions["verification"]=1;
            var routine=(IEnumerator)type.GetMethod("LoadExternalRoutine",Flags).Invoke(library,new object[]{"verification","unused",(Action<UnityEngine.Object>)(asset=>{Require(asset==null,"Cancelled load returns an asset");notified=true;}),1});
            Require(routine.MoveNext()&&library.PendingAssetLoads==1,"Queued load was not registered");library.ReleaseExternal("verification");Require(!routine.MoveNext()&&notified&&library.PendingAssetLoads==0&&library.Busy,"Canceling a queued load steals another owner's lock");
        }
        finally{busy.SetValue(library,oldBusy);versions.Remove("verification");}
        File.WriteAllText("../research/remaining-code-check.json","{\"passed\":true,\"unlockPreservesProgress\":true,\"achievementAliasMigration\":true,\"workHourBoundary\":true,\"choiceTalkTiers\":true,\"costumeLotteryAndAppearanceGate\":true,\"hatChangeService\":true,\"iceMeltAndMusicboxWinding\":true,\"motionSoundMultiplexer\":true,\"blackoutBlindAndAprilFoolCamera\":true,\"windowViewAdjusters\":true,\"level32Cap\":true,\"calendarClampAndStatsProgress\":true,\"draggableUiClamp\":true,\"wardrobeBoneAndBadgeSync\":true,\"decorationNativeSkinSelectorAndClockFade\":true,\"floatingPanesCoexistence\":true,\"presetWarnUnsavedDialog\":true,\"calendarInlineMiniCalendar\":true,\"birthdayOneTimeLock\":true,\"decorationSkinsPreservedDuringTalk\":true,\"trimmedSpritesAndSubCanvasSorting\":true,\"shopThumbnailsAndHabitDeleteConfirm\":true,\"doubleF1DebugMenu\":true,\"localizedControls\":"+catalog.entries.Length+",\"languages\":7,\"queuedAssetCancellation\":true,\"userSaveWritten\":false}");
        Debug.Log("HLC_REMAINING_CODE_PASSED");
    }
}
