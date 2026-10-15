using System;
using System.Linq;
using TMPro;
using UnityEngine;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillNativeUI
    {
        float lastF2PressTime=-10f;
        bool debugMenuVisible,nativeDebugOverlayVisible;
        int debugTab;
        Rect debugWindowRect=new Rect(24f,24f,560f,640f);
        Vector2 debugScrollPos;
        string debugChapterFilter="",debugEpisodeInput="10";

        public bool DebugMenuVisible
        {
            get=>debugMenuVisible;
            set=>debugMenuVisible=value;
        }

        public bool NativeDebugOverlayVisible
        {
            get=>nativeDebugOverlayVisible;
            set
            {
                nativeDebugOverlayVisible=value;
                var debugUi=Find("Paremt/PCPlatform/Canvas/DebugUI");
                if(debugUi)Show(debugUi.gameObject,value);
            }
        }

        public void HandleF2KeyPress(float now)
        {
            if(now-lastF2PressTime<=0.45f)
            {
                debugMenuVisible=!debugMenuVisible;
                lastF2PressTime=-10f;
            }
            else
            {
                lastF2PressTime=now;
            }
        }
        public void HandleF1KeyPress(float now)=>HandleF2KeyPress(now);

        void UpdateNativeDebugOverlay()
        {
            if(!nativeDebugOverlayVisible)return;
            var debugUi=Find("Paremt/PCPlatform/Canvas/DebugUI");
            if(!debugUi||!debugUi.gameObject.activeInHierarchy)return;
            var story=player?player.story:null;
            if(!story)return;
            float fps=Time.unscaledDeltaTime>0.0001f?1f/Time.unscaledDeltaTime:60f;
            foreach(var txt in debugUi.GetComponentsInChildren<TMP_Text>(true))
            {
                if(txt.name.Contains("FPS"))txt.text=$"FPS: {fps:0.0}";
                else txt.text=$"Lv.{story.DisplayLevel} | Ep.{story.MainEpisodeProgress} | Pt.{story.Workspace.points} | {(string.IsNullOrEmpty(story.CurrentChapterId)?"Idle":story.CurrentChapterId)}";
            }
        }

        void OnGUI()
        {
            if(!debugMenuVisible||!player||!player.story)return;
            GUI.depth=-1000;
            debugWindowRect.x=Mathf.Clamp(debugWindowRect.x,0f,Mathf.Max(0f,Screen.width-120f));
            debugWindowRect.y=Mathf.Clamp(debugWindowRect.y,0f,Mathf.Max(0f,Screen.height-80f));
            debugWindowRect=GUI.Window(94812,debugWindowRect,DrawDebugWindow,"Chill With You — Debug Menu (Double-F2)");
        }

        void DrawDebugWindow(int id)
        {
            var story=player.story;
            float fps=Time.unscaledDeltaTime>0.0001f?1f/Time.unscaledDeltaTime:60f;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"FPS: {fps:0.0} | Lv: {story.DisplayLevel} ({story.DisplayExp:0}/{story.DisplayRequiredExp:0}) | Pt: {story.Workspace.points} | Ep: {story.MainEpisodeProgress}");
            if(GUILayout.Button("Close [F2x2]",GUILayout.Width(105f)))debugMenuVisible=false;
            GUILayout.EndHorizontal();

            string[] tabs={"1. Unlocks & Save","2. Story & Scenarios","3. Heroine & Room","4. Time & UI"};
            debugTab=GUILayout.Toolbar(debugTab,tabs);
            GUILayout.Space(6f);

            debugScrollPos=GUILayout.BeginScrollView(debugScrollPos);
            if(debugTab==0)DrawDebugUnlocksTab(story);
            else if(debugTab==1)DrawDebugStoryTab(story);
            else if(debugTab==2)DrawDebugHeroineTab(story);
            else DrawDebugTimeAndUiTab(story);
            GUILayout.EndScrollView();

            GUI.DragWindow(new Rect(0f,0f,10000f,24f));
        }

        void DrawDebugUnlocksTab(ChillStoryRuntime story)
        {
            GUILayout.Label("=== Global Unlocks ===");
            GUILayout.BeginHorizontal();
            bool prevUnlock=story.PreviewUnlock;
            if(GUILayout.Button($"Preview Unlock All: {(prevUnlock?"ON":"OFF")}"))
            {
                story.DebugSetPreviewUnlock(!prevUnlock);
                RefreshNow();
            }
            if(GUILayout.Button("Unlock All Shop Items"))
            {
                story.DebugUnlockAllShopItems();
                RefreshNow();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("=== Points (Shop Currency) ===");
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("+100 Pt")){story.DebugAddPoints(100);RefreshNow();}
            if(GUILayout.Button("+1,000 Pt")){story.DebugAddPoints(1000);RefreshNow();}
            if(GUILayout.Button("+10,000 Pt")){story.DebugAddPoints(10000);RefreshNow();}
            if(GUILayout.Button("Reset 0 Pt")){story.DebugAddPoints(-story.Workspace.points);RefreshNow();}
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("=== Player Level & EXP ===");
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("+100 EXP")){story.DebugAddExp(100);RefreshNow();}
            if(GUILayout.Button("+1,000 EXP")){story.DebugAddExp(1000);RefreshNow();}
            if(GUILayout.Button("+1 Level")){story.DebugSetLevel(story.DisplayLevel+1);RefreshNow();}
            if(GUILayout.Button("Lv 32")){story.DebugSetLevel(32);RefreshNow();}
            if(GUILayout.Button("Max Lv 40")){story.DebugSetLevel(40);RefreshNow();}
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("=== Worked Time ===");
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("+1 Hour Work")){story.DebugAddWorkedHours(1d);RefreshNow();}
            if(GUILayout.Button("+10 Hours Work")){story.DebugAddWorkedHours(10d);RefreshNow();}
            if(GUILayout.Button("+50 Hours Work")){story.DebugAddWorkedHours(50d);RefreshNow();}
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("=== Collaboration Pass ===");
            GUILayout.BeginHorizontal();
            for(int c=0;c<=7;c++)
            {
                int col=c;
                if(GUILayout.Button(col==0?"None":"Collab "+col))
                {
                    story.SelectCollaboration(col);
                    specialSignature=null;
                    RefreshNow();
                }
            }
            GUILayout.EndHorizontal();
        }

        void DrawDebugStoryTab(ChillStoryRuntime story)
        {
            GUILayout.Label($"Current Story State: {(story.Playing?"PLAYING ("+story.CurrentChapterId+")":"IDLE")} | Main Episode: {story.MainEpisodeProgress}");
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("DebugSkipAllStory (Complete All)"))
            {
                story.DebugSkipAllStory();
                storySignature=null;
                specialSignature=null;
                RefreshNow();
            }
            if(GUILayout.Button("Trigger WantTalkUI"))
            {
                story.DebugTriggerWantToTalk();
                RefreshNow();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Skip Current Dialogue"))Action("story-skip");
            if(GUILayout.Button("Stop Story"))
            {
                story.StopStory();
                RefreshNow();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("=== DebugSkipSelectStory (Jump to Main Episode) ===");
            GUILayout.BeginHorizontal();
            foreach(int ep in new[]{1,5,10,15,20,25,30,36})
            {
                int targetEp=ep;
                if(GUILayout.Button("Ep "+targetEp))
                {
                    story.DebugSkipSelectStory(targetEp);
                    storySignature=null;
                    RefreshNow();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Episode (1-36):",GUILayout.Width(100f));
            debugEpisodeInput=GUILayout.TextField(debugEpisodeInput,GUILayout.Width(60f));
            if(GUILayout.Button("Apply Episode Jump")&&int.TryParse(debugEpisodeInput,out int parsedEp))
            {
                story.DebugSkipSelectStory(parsedEp);
                storySignature=null;
                RefreshNow();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter Chapters:",GUILayout.Width(100f));
            debugChapterFilter=GUILayout.TextField(debugChapterFilter);
            if(GUILayout.Button("Clear",GUILayout.Width(55f)))debugChapterFilter="";
            GUILayout.EndHorizontal();

            var chapters=story.AllChapters;
            string filter=(debugChapterFilter??"").Trim();
            int shown=0;
            foreach(var ch in chapters)
            {
                if(ch==null||string.IsNullOrEmpty(ch.ID))continue;
                if(!string.IsNullOrEmpty(filter)&&ch.ID.IndexOf(filter,StringComparison.OrdinalIgnoreCase)<0)continue;
                GUILayout.BeginHorizontal();
                bool done=story.ChapterCompleted(ch.ID);
                GUILayout.Label($"{(done?"[✓]":"[ ]")} {ch.ID} (Scen:{ch.Scenario} Ep:{ch.EpisodeNumber})",GUILayout.Width(390f));
                string cid=ch.ID;
                if(GUILayout.Button("Play",GUILayout.Width(80f)))
                {
                    Close();
                    story.DebugForcePlayChapter(cid);
                    RefreshNow();
                }
                GUILayout.EndHorizontal();
                if(++shown>=60)break;
            }
        }

        void DrawDebugHeroineTab(ChillStoryRuntime story)
        {
            GUILayout.Label("=== Satone Wardrobe & Appearance ===");
            GUILayout.BeginHorizontal();
            for(int h=0;h<3;h++)
            {
                int hairIdx=h;
                if(GUILayout.Button($"Hair {hairIdx+1}"))story.ChooseHair(hairIdx);
            }
            bool hasGlasses=!string.IsNullOrEmpty(ActiveDecorationSkin(1));
            if(GUILayout.Button($"Glasses: {(hasGlasses?"ON":"OFF")}"))
            {
                if(hasGlasses)story.ClearDecoration(1);
                else story.ChooseDecoration("Glasses_1");
                RefreshDecorationControls();
            }
            if(story.effects&&GUILayout.Button($"Hat: {(story.effects.hat&&story.effects.hat.activeSelf?"ON":"OFF")}"))
                story.effects.SetHat(!(story.effects.hat&&story.effects.hat.activeSelf));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            for(int c=0;c<5;c++)
            {
                int costIdx=c;
                if(GUILayout.Button(costIdx==0?"Default Outfit":"Costume "+costIdx))story.ChooseCostume(costIdx);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("=== Camera Presets ===");
            GUILayout.BeginHorizontal();
            if(story.presentation&&story.presentation.cameras)
            {
                var cam=story.presentation.cameras;
                if(GUILayout.Button("Default Cam"))cam.ChangeToDefaultCamera();
                if(GUILayout.Button("Room View 0"))cam.SelectRoomView(0);
                if(GUILayout.Button("Room View 1"))cam.SelectRoomView(1);
                if(GUILayout.Button("Room View 2"))cam.SelectRoomView(2);
                if(GUILayout.Button("AprilFool Cam"))cam.ChangeToAprilFoolCamera();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("=== Environment Time of Day ===");
            GUILayout.BeginHorizontal();
            if(player.environment)
            {
                string[] tNames={"Day (0)","Sunset (1)","Night (2)","Cloudy (3)"};
                for(int t=0;t<4;t++)
                {
                    int ti=t;
                    if(GUILayout.Button(tNames[ti])){player.environment.SelectTime(ti);RefreshNow();}
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("=== Desk Props ===");
            GUILayout.BeginHorizontal();
            if(player.deskObjects)
            {
                if(GUILayout.Button("Play Musicbox"))player.deskObjects.Action("PlayMusicbox");
                if(GUILayout.Button("Stop Musicbox"))player.deskObjects.CloseMusicbox();
                if(GUILayout.Button("Show Ice 100%"))player.deskObjects.UpdateIceVisual(true,1f);
                if(GUILayout.Button("Hide Ice"))player.deskObjects.UpdateIceVisual(false,0f);
            }
            GUILayout.EndHorizontal();
        }

        void DrawDebugTimeAndUiTab(ChillStoryRuntime story)
        {
            GUILayout.Label($"=== Time Scale (Current: {Time.timeScale:0.0}x) ===");
            GUILayout.BeginHorizontal();
            foreach(float ts in new[]{1f,2f,5f,10f})
            {
                float scale=ts;
                if(GUILayout.Button($"{scale:0}x"))Time.timeScale=scale;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("=== Native Debug Overlay ===");
            if(GUILayout.Button($"Scene DebugUI Overlay: {(NativeDebugOverlayVisible?"ON":"OFF")}"))
                NativeDebugOverlayVisible=!NativeDebugOverlayVisible;

            GUILayout.Space(6f);
            GUILayout.Label("=== Quick Open Native Facility Panes ===");
            string[] panes={"decorations","environment","music","story","special","calendar","habits","todos","notes","statistics","profile","settings"};
            for(int i=0;i<panes.Length;i+=4)
            {
                GUILayout.BeginHorizontal();
                for(int j=i;j<Mathf.Min(panes.Length,i+4);j++)
                {
                    string p=panes[j];
                    if(GUILayout.Button(p))Open(p);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6f);
            if(player.library)
            {
                GUILayout.Label($"=== Asset Preload Mode (Current: {player.library.PreloadMode} | Mounted: {player.library.MountedPackCount}/{player.library.TotalPackCount}) ===");
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("Mức 1 (~142MB)"))player.library.ChangePreloadMode("core");
                if(GUILayout.Button("Mức 2 (~217MB)"))player.library.ChangePreloadMode("voice");
                if(GUILayout.Button("Mức 3 (~454MB)"))player.library.ChangePreloadMode("all");
                if(GUILayout.Button("Live"))player.library.ChangePreloadMode("live");
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6f);
            GUILayout.Label("=== UI Diagnostics ===");
            GUILayout.Label($"Recovered Sprites: {(spriteReplacements?.Length??0)}");
            GUILayout.Label($"Skin Thumbnails Mapped: {(skinThumbnails?.Length??0)}");
            GUILayout.Label($"Environment Shop Thumbnails: {(environmentChoices?.Count(c=>c.shopThumbnail!=null)??0)}");
        }
    }
}
