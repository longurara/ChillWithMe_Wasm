using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillNativeUI
    {
        public Sprite[] tutorialSprites;
        bool tutorialVisible;int tutorialPage;
        const string SettingsContent="Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilitySetting/General/ScrollView/Viewport/Content";
        const string MusicRoot="Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/MusicPlayList";
        readonly Dictionary<string,Button> featureButtons=new Dictionary<string,Button>();
        readonly List<Tuple<Slider,Func<float>>> featureSliders=new List<Tuple<Slider,Func<float>>>();
        GameObject ambientPanel;
        Button playlistSelector;
        TMP_InputField playlistName,birthdayInput,timeBoundariesInput;
        Coroutine creditsRoutine;
        Transform creditsParent;
        bool featureDataControls,seekDragging;
        int previousWidth,previousHeight;
        Button FeatureButton(Transform parent,string key,string caption,float y,float width=470,float x=0)
        {
            var source=Find(SettingsContent+"/WebSaveProgressButton");if(!source)return null;
            var go=Instantiate(source.gameObject,parent,false);go.name="WebFeature_"+key;Show(go,true);FixSprites(go);CleanEvents(go);
            foreach(var animator in go.GetComponentsInChildren<Animator>(true))animator.enabled=false;
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(width,52);
            var text=go.GetComponentInChildren<TMP_Text>(true);if(text){text.text=caption;text.richText=false;text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=24;}
            foreach(var group in go.GetComponentsInChildren<CanvasGroup>(true)){group.alpha=1;group.interactable=group.blocksRaycasts=true;}
            var button=go.GetComponent<Button>();button.onClick.RemoveAllListeners();button.interactable=true;featureButtons[key]=button;return button;
        }
        void FeatureCaption(string key,string text)
        {if(featureButtons.TryGetValue(key,out var button)&&button){var label=button.GetComponentInChildren<TMP_Text>(true);if(label)label.text=text;}}
        TMP_InputField FeatureInput(Transform parent,string key,string text,float y,float width=470,float x=0)
        {
            var source=Get<TMP_InputField>("CurrentPageUI","_titleInputField");if(!source)return null;
            var input=Instantiate(source,parent,false);CleanEvents(input.gameObject);input.name="WebFeature_"+key;input.gameObject.SetActive(true);input.readOnly=false;input.richText=false;input.lineType=TMP_InputField.LineType.SingleLine;input.characterLimit=60;input.onValueChanged.RemoveAllListeners();input.onEndEdit.RemoveAllListeners();input.SetTextWithoutNotify(text);
            var rect=(RectTransform)input.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(width,48);
            if(input.textComponent){input.textComponent.fontSize=22;input.textComponent.enableAutoSizing=true;input.textComponent.fontSizeMin=16;input.textComponent.fontSizeMax=22;}
            return input;
        }
        void FeatureHook(Button button,Action callback){if(button)Bind(button.gameObject,callback);}
        static void NormalizeVolumeRow(Transform root)
        {
            var slider=root.GetComponentInChildren<Slider>(true);if(!slider)return;
            var sliderParent=slider.transform.parent as RectTransform;
            if(sliderParent&&sliderParent!=root){sliderParent.anchorMin=Vector2.zero;sliderParent.anchorMax=Vector2.one;sliderParent.offsetMin=Vector2.zero;sliderParent.offsetMax=Vector2.zero;}
            var sr=(RectTransform)slider.transform;sr.anchorMin=new Vector2(0,.23f);sr.anchorMax=new Vector2(1,.23f);sr.pivot=new Vector2(.5f,.5f);sr.anchoredPosition=new Vector2(-15,0);sr.sizeDelta=new Vector2(-95,20);
            foreach(var button in root.GetComponentsInChildren<Button>(true)){var r=(RectTransform)button.transform;r.anchorMin=r.anchorMax=new Vector2(1,.65f);r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(-24,0);r.sizeDelta=new Vector2(38,38);}
            foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                var r=(RectTransform)label.transform;
                if(label.name.Contains("Title")){r.anchorMin=new Vector2(0,.65f);r.anchorMax=new Vector2(1,.65f);r.pivot=new Vector2(0,.5f);r.anchoredPosition=new Vector2(8,0);r.sizeDelta=new Vector2(-100,35);label.alignment=TextAlignmentOptions.MidlineLeft;label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=24;}
                else if(label.name.Contains("Current")){r.anchorMin=r.anchorMax=new Vector2(1,.65f);r.pivot=new Vector2(1,.5f);r.anchoredPosition=new Vector2(-58,0);r.sizeDelta=new Vector2(60,35);label.alignment=TextAlignmentOptions.MidlineRight;}
            }
        }
        void FeatureSlider(Transform root,Func<float> get,Action<float> set)
        {
            var slider=root?root.GetComponentInChildren<Slider>(true):null;if(!slider)return;
            slider.minValue=0;slider.maxValue=1;slider.wholeNumbers=false;slider.interactable=true;slider.onValueChanged.RemoveAllListeners();slider.onValueChanged.AddListener(value=>{set(value);player.story.CommitSettings();});featureSliders.Add(Tuple.Create(slider,get));
            var mute=root.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name.Contains("Mute")||b.name.Contains("MusicAudioButton"));float previous=.5f;
            if(mute)Bind(mute.gameObject,()=>{if(get()>0){previous=get();set(0);}else set(previous);player.story.CommitSettings();});
        }
        void InitializeFeatureControls()
        {
            var story=player.story;
            string[] languageKeys={"_languageSPInteractableUI","_languageTCInteractableUI","_languagePTInteractableUI","_languageKOInteractableUI","_languageRUInteractableUI"};
            string[] languages={"ZhHans","ZhHant","Pt","Ko","Ru"};
            for(int i=0;i<languageKeys.Length;i++){string lang=languages[i];Set(RefOf("SettingUI",languageKeys[i]),true);Wire("SettingUI",languageKeys[i],()=>SelectLanguage(lang));}
            Wire("TutorialView","_previousPageButton",()=>SetTutorialPage(tutorialPage-1));Wire("TutorialView","_nextPageButton",()=>SetTutorialPage(tutorialPage+1));Wire("TutorialView","_finishTutorialButton",CloseTutorial);Wire("TutorialView","_closeTutorialButton",CloseTutorial);
            Set(RefOf("TutorialView","self"),false);
            Wire("MusicUI","_playOrPauseButton",story.ToggleMusic);
            Wire("MusicUI","_musicNextButton",()=>story.NextTrack(1));Wire("MusicUI","_musicBackButton",()=>story.NextTrack(-1));
            Wire("MusicUI","_shuffleButton",story.ToggleShuffle);Wire("MusicUI","_loopButton",story.ToggleRepeat);
            if(volumeSlider){volumeSlider.onValueChanged.RemoveAllListeners();volumeSlider.onValueChanged.AddListener(value=>{if(value>0)previousVolume=value;story.Settings.music=value;story.CommitSettings();});}
            Wire("MusicUI","_switchMuteButton",()=>{if(player.Volume>0){previousVolume=player.Volume;story.Settings.music=0;}else story.Settings.music=previousVolume;story.CommitSettings();});
            var seek=Get<Slider>("MusicUI","musicProgressSlider");
            if(seek)
            {
                seek.interactable=true;seek.onValueChanged.RemoveAllListeners();seek.onValueChanged.AddListener(story.SeekMusic);
                var trigger=seek.GetComponent<EventTrigger>()??seek.gameObject.AddComponent<EventTrigger>();trigger.triggers.Clear();
                foreach(var eventId in new[]{EventTriggerType.PointerDown,EventTriggerType.BeginDrag,EventTriggerType.PointerUp,EventTriggerType.EndDrag})
                {var entry=new EventTrigger.Entry{eventID=eventId};var value=eventId==EventTriggerType.PointerDown||eventId==EventTriggerType.BeginDrag;entry.callback.AddListener(_=>{seekDragging=value;if(!value)story.SeekMusic(seek.value);});trigger.triggers.Add(entry);}
            }
            WirePath(MusicRoot+"/Contents/LocalMusicImport/FileImportButton",()=>ChillPersonalMusic.Import(story.gameObject.name,false));
            WirePath(MusicRoot+"/Contents/LocalMusicImport/FolderImportButton",()=>ChillPersonalMusic.Import(story.gameObject.name,true));
            WirePath(MusicRoot+"/Contents/LocalMusicImportButton",()=>ChillPersonalMusic.Import(story.gameObject.name,false));
            WirePath(MusicRoot+"/Contents/PlayButton",()=>{if(story.Queue.Length>0)story.SelectTrack(story.Queue[0].guid);});
            WirePath(MusicRoot+"/Contents/ShufflePlayButton",()=>{if(!story.MusicState.shuffle)story.ToggleShuffle();story.NextTrack(1);});
            var tagRoot=Find(MusicRoot+"/TagSelectButton");
            if(tagRoot){foreach(var child in tagRoot.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Content"))Show(child.gameObject,false);Bind(tagRoot.gameObject,()=>{var names=story.PlaylistNames;story.SetPlaylist(names[(Array.IndexOf(names,story.MusicState.playlist)+1)%names.Length]);musicSignature=null;});playlistSelector=tagRoot.GetComponent<Button>();}
            var general=Find(SettingsContent);if(general)
            {
                var rect=(RectTransform)general;rect.sizeDelta=new Vector2(rect.sizeDelta.x,1510);
                float y=-565;
                void Add(string key,string title,Action callback){var button=FeatureButton(general,key,title,y);FeatureHook(button,callback);y-=65;}
                Add("autonomy","Satone actions",()=>{story.Settings.autonomy=!story.Settings.autonomy;story.ResetAutonomy();story.CommitSettings();});
                Add("rest","Satone: work / break",()=>story.SetResting(!story.Settings.resting));
                Add("selfTalk","Self talk",()=>{story.Settings.selfTalk=!story.Settings.selfTalk;story.CommitSettings();});
                Add("greeting","Greeting on launch",()=>{story.Settings.autoGreeting=!story.Settings.autoGreeting;story.CommitSettings();});
                Add("autoTime","Automatic day / evening / night",()=>{story.Settings.autoTime=!story.Settings.autoTime;story.CommitSettings();});
                Add("clock24","12 / 24 hour clock",()=>{story.Settings.clock24=!story.Settings.clock24;story.CommitSettings();});
                Add("ambient","Ambient mixer",OpenAmbient);
                Add("tutorial","Replay tutorial",()=>{Close();story.Tutorial();});
                Add("credits","Credits",()=>{Close();if(creditsRoutine!=null)StopCoroutine(creditsRoutine);creditsRoutine=StartCoroutine(PlayNativeCredits(story.Features));});
                Add("farewell","Say goodbye and save",()=>{Close();story.Farewell();});
                birthdayInput=FeatureInput(general,"birthday","Birthday: MM-DD",y);if(birthdayInput)birthdayInput.onEndEdit.AddListener(value=>{var parts=value.Replace("Birthday:","").Trim().Split('-');if(parts.Length==2&&int.TryParse(parts[0],out int month)&&int.TryParse(parts[1],out int day)&&month>=1&&month<=12&&day>=1&&day<=DateTime.DaysInMonth(2000,month)){story.Settings.birthdayMonth=month;story.Settings.birthdayDay=day;story.CommitSettings();}});
                timeBoundariesInput=FeatureInput(general,"timeBoundaries","06:00 / 17:00 / 19:00",y-65);
                if(timeBoundariesInput)timeBoundariesInput.onEndEdit.AddListener(value=>{
                    var parts=value.Split('/');var hours=new float[3];bool valid=parts.Length==3;
                    for(int i=0;i<parts.Length&&valid;i++){var clock=parts[i].Trim().Split(':');valid=clock.Length==2&&int.TryParse(clock[0],out int h)&&int.TryParse(clock[1],out int m)&&h>=0&&h<24&&m>=0&&m<60;if(valid)hours[i]=int.Parse(clock[0])+int.Parse(clock[1])/60f;}
                    if(valid&&hours[0]<hours[1]&&hours[1]<hours[2]){var s=story.Settings;s.dayStart=hours[0];s.sunsetStart=hours[1];s.nightStart=hours[2];story.CommitSettings();}
                    else story.Notify("Use day / evening / night in order, e.g. 06:00 / 17:00 / 19:00.",true);
                });
            }
            Wire("SettingUI","_creditsInteractableUI",()=>SettingsTab("_creditsInteractableUI"));Set(RefOf("SettingUI","_creditsInteractableUI"),true);
            Wire("SettingUI","_tutorialHelpButton",()=>{Close();story.Tutorial();});
            FeatureSlider(RefOf("SettingUI","_masterVolumeChangeUI"),()=>story.Settings.master,v=>story.Settings.master=v);
            FeatureSlider(RefOf("SettingUI","_systemSEVolumeChangeUI"),()=>story.Settings.sfx,v=>story.Settings.sfx=v);
            FeatureSlider(RefOf("SettingUI","_ambientBGMVolumeChangeUI"),()=>story.Settings.ambient,v=>story.Settings.ambient=v);
            FeatureSlider(RefOf("SettingUI","_timerSEVolumeChangeUI"),()=>story.Settings.timer,v=>story.Settings.timer=v);
            var voiceRoot=Find("Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilityProfile/HeroineProfile/HeroineProfileWindow/ProfileCommonVolumeSetting/VoiceVolumeSetting");
            var audioParent=RefOf("SettingUI","_audioParent");
            if(voiceRoot&&audioParent)
            {
                var content=audioParent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Content");
                if(content){var clone=Instantiate(voiceRoot,content,false);clone.name="WebVoiceVolume";clone.gameObject.SetActive(true);var rect=(RectTransform)clone;rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-420);rect.sizeDelta=new Vector2(540,80);NormalizeVolumeRow(clone);FeatureSlider(clone,()=>story.Settings.voice,v=>story.Settings.voice=v);((RectTransform)content).sizeDelta=new Vector2(0,540);}
            }
            Wire("SettingUI","_audioInitButton",()=>{var s=story.Settings;s.master=s.voice=s.sfx=s.ambient=s.timer=1;s.music=.35f;story.CommitSettings();});
            Wire("SettingUI","_borderlessFullscreenInteractableUI",ChillPersonalMusic.Fullscreen);
            Wire("SettingUI","_windowedInteractableUI",ChillPersonalMusic.Fullscreen);
            var graphics=RefOf("SettingUI","_graphicParent");
            if(graphics)
            {
                var content=graphics.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Content");if(content)
                {
                    foreach(Transform child in content)Show(child.gameObject,false);
                    foreach(var layout in content.GetComponents<LayoutGroup>())layout.enabled=false;foreach(var fitter in content.GetComponents<ContentSizeFitter>())fitter.enabled=false;
                    ((RectTransform)content).sizeDelta=new Vector2(0,630);
                    FeatureHook(FeatureButton(content,"quality","Graphics quality",-20),()=>{story.Settings.quality=(story.Settings.quality+1)%3;story.CommitSettings();});
                    FeatureHook(FeatureButton(content,"fps","Frame rate",-90),()=>{story.Settings.activeFps=story.Settings.activeFps==30?60:story.Settings.activeFps==60?120:30;story.CommitSettings();});
                    FeatureHook(FeatureButton(content,"inactiveFps","Background frame rate",-160),()=>{story.Settings.inactiveFps=story.Settings.inactiveFps==15?30:15;story.CommitSettings();});
                    FeatureHook(FeatureButton(content,"scale","Render scale",-230),()=>{story.Settings.renderScale=story.Settings.renderScale<.9f?1:story.Settings.renderScale<1.2f?1.25f:.75f;story.CommitSettings();});
                    FeatureHook(FeatureButton(content,"post","Post processing",-300),()=>{story.Settings.postProcessing=!story.Settings.postProcessing;story.CommitSettings();});
                    FeatureHook(FeatureButton(content,"fit","Fit UI to screen",-370),()=>{story.Settings.mobileFit=!story.Settings.mobileFit;story.CommitSettings();});
                    FeatureHook(FeatureButton(content,"fullscreen","Fullscreen",-440),ChillPersonalMusic.Fullscreen);
                    FeatureHook(FeatureButton(content,"graphicsReset","Reset graphics",-510),()=>{var s=story.Settings;s.quality=1;s.activeFps=60;s.inactiveFps=15;s.renderScale=1;s.postProcessing=s.mobileFit=true;story.CommitSettings();});
                }
            }
            var music=Find(MusicRoot);if(music)
            {
                // Reserve a footer for playlist controls.
                var scroll=Get<ScrollRect>("MusicPlayListView","_scrollRect");if(scroll){var r=(RectTransform)scroll.transform;r.offsetMin=new Vector2(r.offsetMin.x,r.offsetMin.y+125);}
                Canvas.ForceUpdateCanvases();var windowRect=(RectTransform)music;float footerY=-windowRect.rect.height+120;
                if(scroll){var sr=(RectTransform)scroll.transform;footerY=music.InverseTransformPoint(sr.TransformPoint(new Vector3(sr.rect.center.x,sr.rect.yMin))).y-windowRect.rect.yMax-8;}
                playlistName=FeatureInput(music,"playlistName","New playlist",footerY,230,-150);
                FeatureHook(FeatureButton(music,"playlistCreate","Create",footerY,120,70),()=>{story.CreatePlaylist(playlistName.text);musicSignature=null;});
                FeatureHook(FeatureButton(music,"playlistDelete","Delete list",footerY,120,210),()=>{story.DeletePlaylist();musicSignature=null;});
                FeatureHook(FeatureButton(music,"playlistAdd","Add current track to this playlist",footerY-58,500),()=>{if(story.CurrentTrack!=null)story.AddTrackToPlaylist(story.CurrentTrack.guid);musicSignature=null;});
            }
        }
        static string LanguageCaption(string language)=>language=="Ja"?"日本語":language=="ZhHans"?"简体中文":language=="ZhHant"?"繁體中文":language=="Pt"?"Português":language=="Ko"?"한국어":language=="Ru"?"Русский":"English";
        public IEnumerator FocusTutorial(int command)
        {
            string key=command==16?"_pomodoroFocusUI":command==17?"_todoFocusUI":"_noteFocusUI";var focus=RefOf("TutorialView",key);
            Set(focus,true);if(focus){var group=focus.GetComponent<CanvasGroup>();if(group)group.alpha=1;}
            yield return new WaitForSecondsRealtime(3.5f);Set(focus,false);
        }
        void CloseTutorial(){tutorialVisible=false;Set(RefOf("TutorialView","self"),false);}
        void SetTutorialPage(int page)
        {
            tutorialPage=Mathf.Clamp(page,0,3);
            string[] roots={"_screenUIObject","_pomodoroTimerObject","_levelAndStoryObject","_newEnvironmentObject"};
            Set(RefOf("TutorialView","_gameDemoObject"),false);
            for(int i=0;i<roots.Length;i++)Set(RefOf("TutorialView",roots[i]),i==tutorialPage);
            Set(RefOf("TutorialView","_previousPageButton"),tutorialPage>0);Set(RefOf("TutorialView","_nextPageButton"),tutorialPage<3);Set(RefOf("TutorialView","_finishTutorialButton"),tutorialPage==3);
            string[] images={"_screenImage","_pomodoroTimerImage","_levelAndStoryImage","_newEnvironmentImage"};
            int locale=Array.IndexOf(new[]{"En","Ja","ZhHans","ZhHant","Pt","Ko","Ru"},player.story.Settings==null?"En":player.story.UISnapshot("notes").language);
            var image=Get<Image>("TutorialView",images[tutorialPage]);int index=tutorialPage*7+Mathf.Max(0,locale);if(image&&tutorialSprites!=null&&tutorialSprites.Length>index&&tutorialSprites[index])image.sprite=tutorialSprites[index];
        }
        public IEnumerator ShowNativeTutorial()
        {
            tutorialVisible=true;Set(RefOf("TutorialView","self"),true);SetTutorialPage(0);var root=RefOf("TutorialView","self");if(root){root.SetAsLastSibling();foreach(var group in root.GetComponentsInChildren<CanvasGroup>(true)){group.alpha=1;group.interactable=group.blocksRaycasts=true;}}
            while(tutorialVisible)yield return null;
        }
        void RefreshFeatureControls()
        {
            var story=player.story;
            Write("MusicUI","_musicTitleText",story.MusicLoading?"Loading…":story.CurrentTrack?.title ?? (player.music.clip?player.music.clip.name:"Select music"));
            Write("MusicUI","_artistNameText",story.CurrentTrack?.artist??"");
            if(!story.FeatureReady)return;
            var s=story.Settings;
            foreach(var item in featureSliders)if(item.Item1){item.Item1.SetValueWithoutNotify(item.Item2());var label=item.Item1.transform.parent.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name.Contains("Current"));if(label)label.text=Mathf.RoundToInt(item.Item2()*100).ToString();}
            FeatureCaption("autonomy","Satone actions: "+(s.autonomy?"ON":"OFF"));FeatureCaption("rest","Satone: "+(story.Resting?"Break":"Work"));FeatureCaption("selfTalk","Self talk: "+(s.selfTalk?"ON":"OFF"));FeatureCaption("greeting","Greeting on launch: "+(s.autoGreeting?"ON":"OFF"));FeatureCaption("autoTime","Automatic time of day: "+(s.autoTime?"ON":"OFF"));FeatureCaption("clock24",s.clock24?"24 hour clock":"12 hour clock");
            FeatureCaption("quality","Graphics: "+new[]{"Low","Medium","High"}[s.quality]);FeatureCaption("fps","Frame rate: "+s.activeFps);FeatureCaption("inactiveFps","Background: "+s.inactiveFps+" FPS");FeatureCaption("scale","Render scale: "+s.renderScale.ToString("0.00"));FeatureCaption("post","Post processing: "+(s.postProcessing?"ON":"OFF"));FeatureCaption("fit","Fit UI to screen: "+(s.mobileFit?"ON":"OFF"));
            if(playlistSelector){var label=playlistSelector.GetComponentInChildren<TMP_Text>(true);if(label)label.text=story.MusicState.playlist;}
            var shuffleIcon=Get<Image>("MusicUI","_shuffleChangeButtonImage");if(shuffleIcon)shuffleIcon.color=story.MusicState.shuffle?Color.white:new Color(1,1,1,.4f);
            var repeatIcon=Get<Image>("MusicUI","_loopChangeButtonImage");if(repeatIcon)repeatIcon.color=story.MusicState.repeat?Color.white:new Color(1,1,1,.4f);
            if(birthdayInput&&!birthdayInput.isFocused)birthdayInput.SetTextWithoutNotify(s.birthdayMonth==0?"Birthday: MM-DD":"Birthday: "+s.birthdayMonth.ToString("00")+"-"+s.birthdayDay.ToString("00"));
            if(timeBoundariesInput&&!timeBoundariesInput.isFocused)timeBoundariesInput.SetTextWithoutNotify(string.Join(" / ",new[]{s.dayStart,s.sunsetStart,s.nightStart}.Select(h=>TimeSpan.FromMinutes(Math.Round(h*60)).ToString(@"hh\:mm"))));
            var seek=Get<Slider>("MusicUI","musicProgressSlider");if(seek)seek.interactable=player.music.clip&&!state.playing&&!story.MusicLoading;
            if(languageOpen&&languageDropdown)languageDropdown.sizeDelta=new Vector2(languageDropdown.sizeDelta.x,languageClosedHeight+400);
            if(s.mobileFit){var scaler=canvas.GetComponent<CanvasScaler>();scaler.matchWidthOrHeight=Screen.width/(float)Mathf.Max(1,Screen.height)<1920f/1080?0:1;FitOpenPanel();}
            if(ambientPanel&&ambientPanel.activeSelf)RefreshAmbientLevels();
            if(s.clock24)foreach(var label in canvas.GetComponentsInChildren<TMP_Text>(true)){if(label.name=="CurrentTimeText")label.text=DateTime.Now.ToString("HH:mm");if(label.name=="AMPMText")label.text="";}
        }
        void FitOpenPanel()
        {
            var panel=ambientPanel&&ambientPanel.activeSelf?ambientPanel:openPane;if(!panel)return;
            var rect=panel.transform as RectTransform;var space=canvas.transform as RectTransform;
            if(space.rect.width<200||space.rect.height<100){rect.localScale=Vector3.one;return;}
            float scale=Mathf.Min(1,space.rect.width*.96f/Mathf.Max(1,rect.rect.width),space.rect.height*.94f/Mathf.Max(1,rect.rect.height));rect.localScale=new Vector3(scale,scale,1);
            var corners=new Vector3[4];rect.GetWorldCorners(corners);var bounds=new Bounds(space.InverseTransformPoint(corners[0]),Vector3.zero);for(int i=1;i<4;i++)bounds.Encapsulate(space.InverseTransformPoint(corners[i]));
            Vector3 shift=Vector3.zero;if(bounds.max.x>space.rect.xMax)shift.x=space.rect.xMax-bounds.max.x;if(bounds.min.x<space.rect.xMin)shift.x=space.rect.xMin-bounds.min.x;
            if(bounds.max.y>space.rect.yMax)shift.y=space.rect.yMax-bounds.max.y;if(bounds.min.y<space.rect.yMin)shift.y=space.rect.yMin-bounds.min.y;rect.localPosition+=shift;
        }
        void MusicRows()
        {
            var story=player.story;if(!story.FeatureReady){LegacyMusicRows();return;}
            string signature=story.MusicRevision+"|"+story.MusicState.playlist;if(signature==musicSignature)return;bool resetPosition=musicSignature==null;musicSignature=signature;
            var scroll=Get<ScrollRect>("MusicPlayListView","_scrollRect");float position=resetPosition?1:scroll?scroll.verticalNormalizedPosition:1;
            Clear(musicRows);var parent=RefOf("MusicPlayListView","_playListButtonsParent");if(!parent||!musicCell)return;Set(RefOf("MusicPlayListView","_dummyButton"),false);Layout(parent,((RectTransform)musicCell.transform).rect.height);
            foreach(var item in story.Queue)
            {
                string guid=item.guid;var go=Cell(musicCell,parent);SetText(go,"MusicTitleText",item.title);SetText(go,"MusicArtistNameText",item.artist);
                foreach(var t in go.GetComponentsInChildren<Transform>(true))if(t.name=="GreyOutImage"||t.name=="Doraggable")t.gameObject.SetActive(false);
                var play=go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="PlayListMusicPlayButton");Bind(play?play.gameObject:go,()=>story.SelectTrack(guid));
                var favorite=go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="FavoriteIcon");
                if(favorite){favorite.gameObject.SetActive(true);Bind(favorite.gameObject,()=>story.ToggleFavorite(guid));var hit=favorite.Find("FavoriteIconBase")?.GetComponent<Graphic>();if(hit){favorite.GetComponent<Button>().targetGraphic=hit;hit.raycastTarget=true;}var active=favorite.Find("FavoriteIconActive");if(active)active.gameObject.SetActive(story.MusicState.favorites.Contains(guid));}
                var remove=go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="RemoveButton");if(remove){remove.gameObject.SetActive(item.personal||story.MusicState.playlists.Any(p=>p.name==story.MusicState.playlist));Bind(remove.gameObject,()=>story.RemoveTrack(guid));}
                foreach(var t in go.GetComponentsInChildren<Transform>(true))if(t.name=="Playing"||t.name=="PlayCheck")t.gameObject.SetActive(story.MusicState.selected==guid);
                if(story.MusicState.playlists.Any(p=>p.name==story.MusicState.playlist))
                {var up=FeatureButton(go.transform,"moveUp"+guid,"↑",-8,38,180);var down=FeatureButton(go.transform,"moveDown"+guid,"↓",-8,38,222);FeatureHook(up,()=>story.MoveTrack(guid,-1));FeatureHook(down,()=>story.MoveTrack(guid,1));var handle=go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Doraggable");if(handle){handle.gameObject.SetActive(true);var drag=handle.gameObject.GetComponent<ChillNativeRowDrag>()??handle.gameObject.AddComponent<ChillNativeRowDrag>();drag.moved=step=>story.MoveTrack(guid,step);}}
                musicRows.Add(go);
            }
            if(scroll){Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(parent as RectTransform);scroll.verticalNormalizedPosition=position;}
        }
        void OpenAmbient()
        {
            Close();if(ambientPanel){Show(ambientPanel,true);openPane=ambientPanel;pane="ambient";return;}
            var story=player.story;if(!story.FeatureReady)return;
            ambientPanel=Instantiate(historyPanel,historyPanel.transform.parent,false);ambientPanel.name="Native ambient mixer";Show(ambientPanel,true);foreach(var g in ambientPanel.GetComponentsInChildren<CanvasGroup>(true)){g.alpha=1;g.interactable=g.blocksRaycasts=true;}FixSprites(ambientPanel);
            var title=ambientPanel.GetComponentsInChildren<TMP_InputField>(true).FirstOrDefault(t=>t.name.Contains("Title"));if(title){title.SetTextWithoutNotify("Ambient mixer");title.ForceLabelUpdate();if(title.placeholder)title.placeholder.gameObject.SetActive(false);title.readOnly=true;}
            var oldInput=ambientPanel.GetComponentsInChildren<TMP_InputField>(true).FirstOrDefault(t=>t!=title);if(oldInput)oldInput.gameObject.SetActive(false);
            foreach(var text in ambientPanel.GetComponentsInChildren<TMP_Text>(true))if(text.name.IndexOf("Placeholder",StringComparison.OrdinalIgnoreCase)>=0)text.gameObject.SetActive(false);
            var close=ambientPanel.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name=="CloseButtonUI");if(close)Bind(close.gameObject,Close);
            var viewport=new GameObject("Viewport",typeof(RectTransform),typeof(Image),typeof(Mask));viewport.transform.SetParent(ambientPanel.transform,false);var rect=(RectTransform)viewport.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(25,30);rect.offsetMax=new Vector2(-25,-90);viewport.GetComponent<Mask>().showMaskGraphic=false;
            var content=new GameObject("Content",typeof(RectTransform));content.transform.SetParent(viewport.transform,false);var cr=(RectTransform)content.transform;cr.anchorMin=new Vector2(0,1);cr.anchorMax=Vector2.one;cr.pivot=new Vector2(.5f,1);cr.sizeDelta=new Vector2(0,story.Features.ambient.Length*95+70);cr.anchoredPosition=Vector2.zero;
            var scroll=viewport.AddComponent<ScrollRect>();scroll.viewport=rect;scroll.content=cr;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            var source=RefOf("SettingUI","_masterVolumeChangeUI");
            int index=0;foreach(var track in story.Features.ambient)
            {
                int id=track.id;var go=Instantiate(source,content.transform,false);go.name="NativeAmbient_"+id;go.gameObject.SetActive(true);var r=(RectTransform)go;r.anchorMin=r.anchorMax=new Vector2(.5f,1);r.pivot=new Vector2(.5f,1);r.anchoredPosition=new Vector2(0,-index++*95);r.sizeDelta=new Vector2(560,85);
                NormalizeVolumeRow(go);var labels=go.GetComponentsInChildren<TMP_Text>(true);foreach(var label in labels)if(label.name.Contains("Title"))label.text=System.Text.RegularExpressions.Regex.Replace(track.name,"([a-z])([A-Z])","$1 $2");
                var slider=go.GetComponentInChildren<Slider>(true);if(slider){slider.name="Ambient_"+id;slider.SetValueWithoutNotify(story.AmbientLevel(id));slider.onValueChanged.RemoveAllListeners();slider.onValueChanged.AddListener(value=>story.SetAmbience(id,value));}
                foreach(var button in go.GetComponentsInChildren<Button>(true))Bind(button.gameObject,()=>story.SetAmbience(id,story.AmbientLevel(id)>0?0:.5f));
            }
            FeatureHook(FeatureButton(content.transform,"stopAmbient","Stop all ambient sounds",-index*95),story.StopAllAmbience);
            openPane=ambientPanel;pane="ambient";RefreshAmbientLevels();FitOpenPanel();
        }
        void RefreshAmbientLevels()
        {
            foreach(var slider in ambientPanel.GetComponentsInChildren<Slider>(true))if(int.TryParse(slider.name.Replace("Ambient_",""),out int id))
            {
                float level=player.story.AmbientLevel(id);slider.SetValueWithoutNotify(level);
                var row=slider.GetComponentInParent<RectTransform>();while(row&& !row.name.StartsWith("NativeAmbient_"))row=row.parent as RectTransform;
                if(row)foreach(var label in row.GetComponentsInChildren<TMP_Text>(true))if(label.name.Contains("Current"))label.text=Mathf.RoundToInt(level*100).ToString();
            }
        }
        public IEnumerator PlayNativeCredits(ChillFeatureData features)
        {
            if(features==null)yield break;StopNativeCredits();creditsParent=Find(features.creditsParent);if(!creditsParent)yield break;
            var groups=features.creditsGroups.Select(path=>Find(path)?.GetComponent<CanvasGroup>()).Where(g=>g).ToArray();
            foreach(var group in groups){group.alpha=0;group.blocksRaycasts=false;group.gameObject.SetActive(false);}
            creditsParent.gameObject.SetActive(true);creditsParent.SetAsLastSibling();
            foreach(var group in groups)
            {
                group.gameObject.SetActive(true);float t=0;
                while(t<features.creditsFadeIn){t+=Time.unscaledDeltaTime;group.alpha=Mathf.Clamp01(t/Mathf.Max(.01f,features.creditsFadeIn));yield return null;}
                group.alpha=1;yield return new WaitForSecondsRealtime(features.creditsHold);t=0;
                while(t<features.creditsFadeOut){t+=Time.unscaledDeltaTime;group.alpha=1-Mathf.Clamp01(t/Mathf.Max(.01f,features.creditsFadeOut));yield return null;}
                group.alpha=0;group.gameObject.SetActive(false);yield return new WaitForSecondsRealtime(features.creditsGap);
            }
            creditsParent.gameObject.SetActive(false);creditsRoutine=null;
        }
        public void StopNativeCredits(){if(creditsRoutine!=null){StopCoroutine(creditsRoutine);creditsRoutine=null;}if(creditsParent)creditsParent.gameObject.SetActive(false);}
    }
}
