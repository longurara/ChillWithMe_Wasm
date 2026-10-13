using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Hlc.ChillPrototype
{
    // Reuses the recovered canvas, graphics, typography and cell prefabs.
    // Only the missing Windows service callbacks are adapted to the web model.
    public sealed partial class ChillNativeUI : MonoBehaviour
    {
        [Serializable] public class Ref { public string name,path,asset; }
        [Serializable] public class Binding { public string script,path; public Ref[] refs; }
        [Serializable] public class Label {public string key,en,ja;}
        [Serializable] public class Interaction {public string path;public string[] hover,use,baseline;public float hoverAlpha,useAlpha,baseAlpha;public bool hideHoverOnUse;}
        [Serializable] public class Configuration { public Binding[] bindings;public Label[] labels;public Interaction[] interactions;public string[] storyHiddenObjects; }
        public ChillGraphicsPlayer player;
        public Canvas canvas;
        public TextAsset configuration;
        public GameObject musicCell,storyCell,todoCell,selectionCell;
        public Sprite musicPlay,musicPause,timerPlay,timerPause;
        public Sprite musicAudible,musicMuted;
        public Sprite countupPlay,countupPause,wantTalkNormal;
        [Serializable]public class SpriteReplacement{public Sprite original,recovered;}
        public SpriteReplacement[] spriteReplacements;
        public TMP_FontAsset fallbackFont;
        [Serializable] public class EnvironmentChoice {public GameObject root;public Button main,view;public int index;}
        public Button[] workModeButtons;
        public EnvironmentChoice[] environmentChoices;
        public Button clearEnvironmentButton,historySettingsButton,historyDialogueButton,historyCloseButton;
        public GameObject historyPanel,noticePanel;
        public TMP_InputField historyInput,historyTitle;
        public TMP_Text noticeText;
        readonly Dictionary<string,Transform> paths=new Dictionary<string,Transform>();
        readonly Dictionary<string,Binding> bindings=new Dictionary<string,Binding>();
        readonly Dictionary<TMP_Text,string> localized=new Dictionary<TMP_Text,string>();
        readonly Dictionary<string,Label> labels=new Dictionary<string,Label>();
        readonly List<GameObject> musicRows=new List<GameObject>(),storyRows=new List<GameObject>(),todoRows=new List<GameObject>(),choiceRows=new List<GameObject>();
        GameObject openPane;
        Transform nativeUiRoot;
        int nativeUiSibling;
        GameObject reveal;
        string pane="",language="",musicSignature,storySignature,todoSignature,choiceSignature;
        bool initialized,hidden,shuffle,translatedReady;
        float nextUpdate,previousVolume=.35f;
        int shownNoticeRevision;
        float noticeUntil;
        RectTransform languageDropdown;
        Transform languageOptions;
        float languageClosedHeight;
        bool languageOpen;
        Slider volumeSlider;
        RectTransform volumePadding,volumeButton;
        readonly Dictionary<Transform,float> rowHeights=new Dictionary<Transform,float>();
        ChillStoryUiState state;
        static string PathOf(Transform t) => t.parent ? PathOf(t.parent)+"/"+t.name : t.name;
        void Start() { Initialize(); }
        public void Initialize()
        {
            if (initialized) return; initialized=true;
            ChillGameplayUI.UseNativeUI(true);
            foreach(var t in UnityEngine.Object.FindObjectsOfType<Transform>(true)) paths[PathOf(t)]=t;
            nativeUiRoot=canvas.transform.Find("UI");if(nativeUiRoot)nativeUiSibling=nativeUiRoot.GetSiblingIndex();
            var config=JsonUtility.FromJson<Configuration>(configuration.text);
            InitializeStoryHud(config);
            FixSprites(canvas.gameObject);
            CleanEvents(canvas.gameObject);
            foreach(var binding in config.bindings) if(!bindings.ContainsKey(binding.script)) bindings[binding.script]=binding;
            foreach(var label in config.labels)labels[label.key]=label;
            for(var t=canvas.transform;t;t=t.parent)t.gameObject.SetActive(true);
            canvas.enabled=true;
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=100;
            foreach(var animator in canvas.GetComponentsInChildren<Animator>(true)) animator.enabled=false;
            foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                if(text.font && text.fontSharedMaterial && text.font.atlasTexture && text.fontSharedMaterial.mainTexture != text.font.atlasTexture)text.fontSharedMaterial=text.font.material;
                if(fallbackFont && text.font && !text.font.fallbackFontAssetTable.Contains(fallbackFont)) text.font.fallbackFontAssetTable.Add(fallbackFont);
                if(text.text.StartsWith("ui_") || text.text == "heroine_name") localized[text]=text.text;
            }
            foreach(var group in canvas.GetComponentsInParent<CanvasGroup>()) {group.alpha=1;group.interactable=group.blocksRaycasts=true;}
            foreach(string name in new[] { "DebugUI","Blind","UI_Direction","CallIcon","StoryFadeController","EndCreditsService","RoomGameManagerRayCastBlocker" }) Set(canvas.transform.Find(name),false);
            Set(Find("Paremt/PCPlatform/Canvas/UI/PoorConnectionUIPC"),false);
            Set(Find("Paremt/PCPlatform/Canvas/UI/UI_FacilityPlayerPoint"),false);
            var changes=Find("Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects");
            if(changes) foreach(Transform child in changes) child.gameObject.SetActive(false);
            Set(Find("Paremt/PCPlatform/Canvas/UI/MostFrontArea/ProfileIconSettingWindow"),false);
            foreach(var t in canvas.GetComponentsInChildren<Transform>(true))
                if(t.name.Contains("LockImage") || t.name == "PoorConnectionUIPC" || t.name == "NewMark" || t.name == "IconNotice") t.gameObject.SetActive(false);
            foreach(var t in canvas.GetComponentsInChildren<Transform>(true))if(t.name=="ProfileIconSettingWindow" || t.name=="StatusChangeView")t.gameObject.SetActive(false);
            Wire("MusicUI","_facilityOpenButton",()=>Open("music"));Wire("MusicUI","_listCloseButton",Close);
            Wire("MusicUI","_playOrPauseButton",()=>{if(player.music.isPlaying)player.music.Pause();else player.music.Play();});
            Wire("MusicUI","_musicNextButton",()=>NextMusic(1));Wire("MusicUI","_musicBackButton",()=>NextMusic(-1));
            Wire("MusicUI","_shuffleButton",()=>shuffle=!shuffle);Wire("MusicUI","_loopButton",()=>player.music.loop=!player.music.loop);
            Wire("MusicUI","_switchMuteButton",()=>{if(player.Volume > 0){previousVolume=player.Volume;player.SetVolume(0);}else player.SetVolume(previousVolume);});
            volumeSlider=Get<Slider>("MusicUI","_volumeSlider");
            volumePadding=RefOf("MusicVolumeUI","sliderPadding") as RectTransform;volumeButton=RefOf("MusicVolumeUI","pointerEnterHandler") as RectTransform;
            if(volumeSlider){volumeSlider.gameObject.SetActive(false);volumeSlider.onValueChanged.RemoveAllListeners();volumeSlider.onValueChanged.AddListener(value=>{if(value>0)previousVolume=value;player.SetVolume(value);});}
            if(volumeButton){var trigger=volumeButton.GetComponent<EventTrigger>() ?? volumeButton.gameObject.AddComponent<EventTrigger>();trigger.triggers.Clear();var enter=new EventTrigger.Entry{eventID=EventTriggerType.PointerEnter};enter.callback.AddListener(_=>SetVolumePopup(true));trigger.triggers.Add(enter);}
            Wire("NoteUI","_facilityOpenButton",()=>Open("notes"));Wire("NoteUI","_noteCloseButton",Close);
            var note=Get<TMP_InputField>("CurrentPageUI","_mainInputField");if(note){note.characterLimit=64000;note.richText=false;note.onValueChanged.RemoveAllListeners();note.onValueChanged.AddListener(value=>Action("note",text:value));}
            var title=Get<TMP_InputField>("CurrentPageUI","_titleInputField");if(title){title.SetTextWithoutNotify("Notes");title.readOnly=true;}
            Wire("TodoListUI","_facilityOpenButton",()=>Open("todos"));Wire("TodoListUI","_closeButton",Close);
            var add=Find(bindings["TodoListUI"].path+"/AddTodoUI");
            var addInput=add ? add.GetComponentInChildren<TMP_InputField>(true) : null;
            if(addInput){addInput.characterLimit=200;addInput.richText=false;Wire("TodoListUI","addTodoButton",()=>{if(!string.IsNullOrWhiteSpace(addInput.text)){Action("todo-add",text:addInput.text);addInput.SetTextWithoutNotify("");todoSignature=null;}});}
            Wire("StorySelectUI","_facilityOpenButton",()=>Open("story"));Wire("StorySelectUI","_closeButton",Close);
            Wire("StorySelectUI","_tagChangeToMainButton",()=>{Action("chapter-kind",0);storySignature=null;});
            Wire("StorySelectUI","_tagChangeToSpecialButton",()=>{Action("chapter-kind",1);storySignature=null;});
            Wire("StorySystemUI","_transparentButton",()=>Action("next"));
            Wire("StorySystemUI","_autoButton",()=>Action("auto",state != null && state.autoRead ? 0 : 1));
            Wire("StorySystemUI","_skipButton",()=>Action("story-skip"));
            Wire("PomodoroTimerUI","_startPomodoroButton",()=>Action("timer-toggle"));Wire("PomodoroTimerUI","_playOrPausePomodoroButton",()=>Action("timer-toggle"));
            Wire("PomodoroTimerUI","_resetButton",()=>Action("timer-reset"));Wire("PomodoroTimerUI","_skipPomodoroButton",()=>Action("timer-skip"));
            Wire("PomodoroTimerUI","_addWorkTimeButton",()=>Action("work-minutes",(state?.workMinutes ?? 25)+1));Wire("PomodoroTimerUI","_subtractWorkTimeButton",()=>Action("work-minutes",(state?.workMinutes ?? 25)-1));
            Wire("PomodoroTimerUI","_addBreakTimeButton",()=>Action("break-minutes",(state?.breakMinutes ?? 5)+1));Wire("PomodoroTimerUI","_subtractBreakTimeButton",()=>Action("break-minutes",(state?.breakMinutes ?? 5)-1));
            NumberInput("_workTimeMaxMinutesText","work-minutes");NumberInput("_breakTimeMaxMinutesText","break-minutes");
            NumberInput("_loopCountMaxText","loop-count");
            Wire("PomodoroTimerUI","_addLoopCountButton",()=>Action("loop-count",(state?.loopCount ?? 2)+1));Wire("PomodoroTimerUI","_subtractLoopButtonButton",()=>Action("loop-count",(state?.loopCount ?? 2)-1));
            Wire("PomodoroTimerUI","_togglePomodoroPresetButton",()=>Open("presets"));
            Wire("TimerParentUI","_changePomodoroTimerButton",()=>Action("timer-mode",0));Wire("TimerParentUI","_changeCountupTimerButton",()=>Action("timer-mode",1));
            Wire("CountupTimerUI","_startCountupButton",()=>Action("countup-toggle"));Wire("CountupTimerUI","_playOrPauseCountupButton",()=>Action("countup-toggle"));Wire("CountupTimerUI","_resetButton",()=>Action("countup-reset"));
            var presetBindings=config.bindings.Where(b=>b.script=="PomodoroPresetInstance").OrderBy(b=>b.path,StringComparer.Ordinal).ToArray();
            for(int i=0;i<presetBindings.Length;i++)
            {int slot=i;var binding=presetBindings[i];foreach(var pair in new[]{("nameInput","preset-name"),("loopCountInput","preset-loops"),("workTimeInput","preset-work"),("breakTimeInput","preset-break")})
             {var input=Find(binding.refs.First(r=>r.name==pair.Item1).path)?.GetComponent<TMP_InputField>();if(input){string action=pair.Item2;input.richText=false;input.characterLimit=action=="preset-name"?80:3;input.onEndEdit.RemoveAllListeners();input.onEndEdit.AddListener(value=>Action(action,slot,value));}}
             var play=Find(binding.refs.First(r=>r.name=="playButton").path);if(play)Bind(play.gameObject,()=>{Action("preset-play",slot);Close();});}
            Wire("EnvironmentUI","_facilityOpenButton",()=>Open("environment"));Wire("EnvironmentUI","_closeButton",Close);
            string[] dayKeys={"_timeDayController","_timeSunsetController","_timeNightController","_timeCloudyController"};
            for(int i=0;i<dayKeys.Length;i++){int value=i;Wire("EnvironmentUI",dayKeys[i],()=>Action("time-mode",value));}
            for(int i=0;i<(workModeButtons?.Length ?? 0);i++){int value=i;Bind(workModeButtons[i].gameObject,()=>Action("mode",value));}
            foreach(var choice in environmentChoices ?? Array.Empty<EnvironmentChoice>())
            {
                int index=choice.index;Show(choice.root,true);
                if(choice.main)Bind(choice.main.gameObject,()=>Action("window-mode",player.environment.selectedEnvironment==index?0:index));
                if(choice.view)Bind(choice.view.gameObject,()=>Action("window-mode",player.environment.selectedEnvironment==index?0:index));
            }
            if(clearEnvironmentButton)Bind(clearEnvironmentButton.gameObject,()=>Action("window-mode",0));
            Wire("SettingUI","_facilityOpenButton",()=>Open("settings"));Wire("SettingUI","_closeButton",Close);
            foreach(string key in new[]{"_generalInteractableUI","_graphicInteractableUI","_audioInteractableUI"}) {string tab=key;Wire("SettingUI",key,()=>SettingsTab(tab));}
            Wire("SettingUI","_languageENInteractableUI",()=>SelectLanguage("En"));Wire("SettingUI","_languageJPInteractableUI",()=>SelectLanguage("Ja"));
            var languageRoot=RefOf("SettingUI","_languagePulldownList");
            if(languageRoot)
            {
                languageDropdown=languageRoot.Find("Pulldown") as RectTransform;
                languageOptions=languageDropdown?languageDropdown.Find("TodoGroupText (TMP)/Content"):null;
                if(languageDropdown)languageClosedHeight=languageDropdown.sizeDelta.y;
                foreach(string key in new[]{"_languageSPInteractableUI","_languageTCInteractableUI","_languagePTInteractableUI","_languageKOInteractableUI","_languageRUInteractableUI"})Set(RefOf("SettingUI",key),false);
                var languageToggle=languageRoot.Find("PulldownButton");if(languageToggle)Bind(languageToggle.gameObject,()=>SetLanguageOpen(!languageOpen));
                SetLanguageOpen(false);
            }
            HideUnsupportedSettings();SettingsTab("_generalInteractableUI");
            var fps=Get<TMP_InputField>("SettingUI","_activeFrameRateInputField");if(fps){fps.onEndEdit.RemoveAllListeners();fps.onEndEdit.AddListener(value=>{if(int.TryParse(value,out int n))player.SetLowQuality(n<=30);});}
            var master=RefOf("SettingUI","_masterVolumeChangeUI")?.GetComponentInChildren<Slider>(true);if(master){master.onValueChanged.RemoveAllListeners();master.onValueChanged.AddListener(value=>AudioListener.volume=Mathf.Clamp01(value));}
            const string saveRoot="Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilitySetting/General/ScrollView/Viewport/Content/";
            WirePath(saveRoot+"WebSaveProgressButton",()=>Action("save"));WirePath(saveRoot+"WebExportSaveButton",()=>Action("save-export"));WirePath(saveRoot+"WebImportSaveButton",()=>Action("save-import"));
            if(historySettingsButton)Bind(historySettingsButton.gameObject,()=>Open("history"));
            if(historyDialogueButton)Bind(historyDialogueButton.gameObject,()=>Open("history"));
            if(historyCloseButton)Bind(historyCloseButton.gameObject,Close);
            if(historyInput){historyInput.readOnly=true;historyInput.richText=false;historyInput.onValueChanged.RemoveAllListeners();historyInput.onEndEdit.RemoveAllListeners();}
            if(historyTitle){historyTitle.readOnly=true;historyTitle.richText=false;historyTitle.onValueChanged.RemoveAllListeners();historyTitle.onEndEdit.RemoveAllListeners();}
            if(historyPanel){Show(historyPanel,false);var handle=historyPanel.transform.Find("DragButton");if(handle){var drag=handle.GetComponent<ChillNativeDrag>() ?? handle.gameObject.AddComponent<ChillNativeDrag>();drag.target=(RectTransform)historyPanel.transform;}}
            Show(noticePanel,false);
            if(noticePanel)foreach(var graphic in noticePanel.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
            var hide=Find("Paremt/PCPlatform/Canvas/UI/MostFrontArea/RightIcons/IconHide_Button");
            if(hide){reveal=Instantiate(hide.gameObject,canvas.transform,false);reveal.name="Native reveal UI";var rect=(RectTransform)reveal.transform;rect.anchorMin=rect.anchorMax=new Vector2(1,0);rect.anchoredPosition=new Vector2(-65,65);reveal.SetActive(false);Bind(reveal,()=>ToggleHidden());}
            WirePath("Paremt/PCPlatform/Canvas/UI/MostFrontArea/RightIcons/IconHide_Button",ToggleHidden);
            foreach(string type in new[]{"NoteUI","TodoListUI","StorySelectUI","MusicPlayListView","EnvironmentUI","SettingUI"}){var window=RefOf(type,"self");var handle=window?window.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="DragButton"):null;if(handle){var drag=handle.gameObject.AddComponent<ChillNativeDrag>();drag.target=window as RectTransform;}}
            foreach(var button in canvas.GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
            // Rebuild listeners after clearing serialized Windows callbacks.
            foreach(var hook in hooks) {hook.button.onClick.RemoveAllListeners();hook.button.onClick.AddListener(()=>{hook.action();player.story.UISelectSound();});}
            foreach(var button in canvas.GetComponentsInChildren<Button>(true))if(!hooks.Any(h=>h.button==button))button.interactable=false;
            foreach(var settings in config.interactions ?? Array.Empty<Interaction>())
            {var target=Find(settings.path);if(!target)continue;var interaction=target.GetComponent<ChillNativeInteraction>() ?? target.gameObject.AddComponent<ChillNativeInteraction>();
             Graphic[] Images(string[] refs)=>(refs ?? Array.Empty<string>()).Select(p=>Find(p)?.GetComponent<Graphic>()).Where(g=>g).ToArray();
             interaction.hover=Images(settings.hover);interaction.use=Images(settings.use);interaction.baseline=Images(settings.baseline);interaction.hoverAlpha=settings.hoverAlpha;interaction.useAlpha=settings.useAlpha;interaction.baseAlpha=settings.baseAlpha;interaction.hideHoverOnUse=settings.hideHoverOnUse;interaction.SetUsing(false);}
            InitializeFeatureControls();InitializeWorkspaceControls();InitializeCompletionControls();Show(RefOf("FacilityProfile","_canvasGroup")?.gameObject,true);Set(RefOf("FacilityProfile","_background"),false);Set(RefOf("PlayerProfileView","self"),false);Set(RefOf("HeroineProfileView","self"),false);Close();Refresh(true);
        }
        class Hook {public Button button;public Action action;}
        readonly List<Hook> hooks=new List<Hook>();
        public Transform Find(string path) => path != null && paths.TryGetValue(path,out var t) ? t : null;
        Transform RefOf(string script,string key)
        {return bindings.TryGetValue(script,out var b) ? Find(key == "self" ? b.path : b.refs.FirstOrDefault(r=>r.name==key)?.path) : null;}
        T Get<T>(string script,string key) where T:Component {var t=RefOf(script,key);return t ? t.GetComponent<T>() ?? t.GetComponentInChildren<T>(true) : null;}
        static void Set(Transform t,bool active){if(t)t.gameObject.SetActive(active);}
        static void Show(GameObject go,bool active)
        {if(!go)return;go.SetActive(active);if(active){var group=go.GetComponent<CanvasGroup>();if(group){group.alpha=1;group.interactable=group.blocksRaycasts=true;}}}
        void Wire(string script,string key,Action callback){var t=RefOf(script,key);if(t)Bind(t.gameObject,callback);}
        void WirePath(string path,Action callback){var t=Find(path);if(t)Bind(t.gameObject,callback);}
        void Bind(GameObject go,Action callback)
        {var button=go.GetComponent<Button>() ?? go.AddComponent<Button>();if(!button.targetGraphic)button.targetGraphic=go.GetComponent<Graphic>() ?? go.GetComponentInChildren<Graphic>(true);if(button.targetGraphic)button.targetGraphic.raycastTarget=true;button.interactable=true;button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>{callback();player.story.UISelectSound();});hooks.RemoveAll(h=>!h.button||h.button==button);hooks.Add(new Hook{button=button,action=callback});}
        void Action(string action,float value=0,string text=null,string id=null){player.story.UIAction(new ChillUiCommand{action=action,value=value,text=text,id=id});nextUpdate=0;}
        void SelectLanguage(string value){Action("language",text:value);storySignature=null;SetLanguageOpen(false);Refresh(true);}
        void SetLanguageOpen(bool value)
        {
            languageOpen=value;Set(languageOptions,value);
            if(languageDropdown)languageDropdown.sizeDelta=new Vector2(languageDropdown.sizeDelta.x,languageClosedHeight+(value?145:0));
            if(value && languageDropdown)languageDropdown.parent.parent.SetAsLastSibling();
        }
        void NumberInput(string field,string action){var input=Get<TMP_InputField>("PomodoroTimerUI",field);if(input){input.onEndEdit.RemoveAllListeners();input.onEndEdit.AddListener(value=>{if(int.TryParse(value,out int n))Action(action,n);});}}
        public void Open(string name)
        {
            if(pane==name){Close();return;}player.story.TutorialSignal(name);Close();pane=name;
            player.story.HistoryVisible=name=="history";
            string script=name=="music"?"MusicPlayListView":name=="notes"?"NoteUI":name=="todos"?"TodoListUI":name=="story"?"StorySelectUI":name=="environment"?"EnvironmentUI":name=="presets"?"PomodoroPresetUI":name=="calendar"?"CalendarUI":name=="habits"?"HabitTrackerUI":name=="statistics"?"TotalWorkStatisticsUI":name=="timer-audio"?"TimerSoundSettingView":name=="profile"?"PlayerProfileView":name=="heroine-profile"?"HeroineProfileView":name=="special"?"SpecialView":name=="decorations"?"DecorationListUI":"SettingUI";
            openPane=name=="history"||name=="achievements"?historyPanel:RefOf(script,"self")?.gameObject;if(name=="timer-audio"&&timerAudioPane){Show(openPane,true);openPane=timerAudioPane;}Show(openPane,true);if(openPane){if(nativeUiRoot)nativeUiRoot.SetAsLastSibling();openPane.transform.SetAsLastSibling();}
            ShowWorkspacePane();
            if(name=="achievements"){if(historyTitle)historyTitle.SetTextWithoutNotify("Achievements");if(historyInput)historyInput.SetTextWithoutNotify(string.Join("\n\n",player.story.Workspace.achievements.Select(a=>"✓ "+a.Replace('_',' '))));}
            if(name=="story")storySignature=null;if(name=="todos")todoSignature=null;if(name=="music")musicSignature=null;Refresh(true);
        }
        public void Close(){if(skinPopup)Show(skinPopup.gameObject,false);Set(RefOf("ProfileIconSettingView","self"),false);Set(RefOf("StatusChangeView","self"),false);Show(openPane,false);openPane=null;pane="";player.story.HistoryVisible=false;SetLanguageOpen(false);if(nativeUiRoot)nativeUiRoot.SetSiblingIndex(nativeUiSibling);}
        void ToggleHidden(){if(StoryHudHidden)return;hidden=!hidden;Set(Find("Paremt/PCPlatform/Canvas/UI"),!hidden);if(reveal)reveal.SetActive(hidden);Close();}
        void NextMusic(int step)
        {if(player.story.Playing || player.library.Busy)return;var entries=player.library.MusicEntries;if(entries.Length==0)return;int current=Array.FindIndex(entries,t=>player.music.clip && t.name==player.music.clip.name);int index=shuffle?UnityEngine.Random.Range(0,entries.Length):(current+step+entries.Length)%entries.Length;player.library.SelectMusic(entries[index].guid);}
        void Update()
        {
            if(pane=="presets" && Input.GetMouseButtonUp(0) && openPane && !Contains(openPane.transform as RectTransform))Close();
            if(volumeSlider && volumeSlider.gameObject.activeInHierarchy && !Input.GetMouseButton(0) && !Contains(volumePadding) && !Contains(volumeButton))SetVolumePopup(false);
            if(Input.GetKeyDown(KeyCode.Escape)){if(hidden && !StoryHudHidden)ToggleHidden();else Close();}
            if(Time.unscaledTime>=nextUpdate){nextUpdate=Time.unscaledTime+.2f;Refresh(false);}
        }
        static bool Contains(RectTransform rect)=>rect && RectTransformUtility.RectangleContainsScreenPoint(rect,Input.mousePosition);
        public void SetVolumePopup(bool visible){if(volumeSlider)volumeSlider.gameObject.SetActive(visible);}
        Sprite RecoveredSprite(Sprite original)=>(spriteReplacements ?? Array.Empty<SpriteReplacement>()).FirstOrDefault(p=>p.original==original)?.recovered ?? original;
        void FixSprites(GameObject root)
        {
            foreach(var image in root.GetComponentsInChildren<Image>(true))
            {
                var original=image.sprite;var active=image.overrideSprite;
                image.sprite=RecoveredSprite(original);
                image.overrideSprite=active!=original?RecoveredSprite(active):null;
            }
            foreach(var button in root.GetComponentsInChildren<Button>(true)){var sprites=button.spriteState;sprites.highlightedSprite=RecoveredSprite(sprites.highlightedSprite);sprites.pressedSprite=RecoveredSprite(sprites.pressedSprite);sprites.selectedSprite=RecoveredSprite(sprites.selectedSprite);sprites.disabledSprite=RecoveredSprite(sprites.disabledSprite);button.spriteState=sprites;}
        }
        void Write(string script,string key,string value){var label=Get<TMP_Text>(script,key);if(label && label.text!=value)label.text=value;}
        void Refresh(bool force)
        {
            if(!initialized)return;state=player.story.UISnapshot("notes");
            if(language!=state.language || force || (!translatedReady&&state.ready)){translatedReady=state.ready;language=state.language;foreach(var pair in localized)if(pair.Key && labels.TryGetValue(pair.Value,out var label))pair.Key.text=player.story.UITranslate(pair.Value);}
            RefreshFeatureControls();RefreshWorkspaceControls();RefreshCompletionControls();
            var playImage=Get<Image>("MusicUI","_playOrPauseButtonImage");if(playImage)playImage.sprite=player.music.isPlaying?musicPause:musicPlay;
            var volume=Get<Slider>("MusicUI","_volumeSlider");if(volume)volume.SetValueWithoutNotify(player.Volume);
            var audioIcon=Get<Image>("MusicVolumeUI","volumeIconImage");if(audioIcon)audioIcon.sprite=player.Volume>0?musicAudible:musicMuted;
            var progress=Get<Slider>("MusicUI","musicProgressSlider");if(progress && player.music.clip){progress.minValue=0;progress.maxValue=player.music.clip.length;if(!seekDragging)progress.SetValueWithoutNotify(player.music.time);}
            if(pane=="music")MusicRows();
            var setting=RefOf("PomodoroTimerStateView","_settingParentObject");var usingTimer=RefOf("PomodoroTimerStateView","_usingTimerParentObject");
            bool hasTimer=state.timerActive;
            Show(setting?setting.gameObject:null,!hasTimer);Show(usingTimer?usingTimer.gameObject:null,hasTimer);
            Set(RefOf("PomodoroTimerStateView","_settingBaseCanvas"),!hasTimer);
            Set(RefOf("PomodoroTimerStateView","_skipButtonObject"),hasTimer && state.timerRunning);
            var meter=Get<Image>("PomodoroTimerStateView","_meterOverWriteImage");if(meter){meter.fillAmount=state.timerMeter;meter.fillClockwise=!state.breakTime;}
            Write("PomodoroTimerStateView","_timeText",state.timerText);Write("PomodoroTimerStateView","_remainLoopCountText",Mathf.Min(state.loopCount,state.rounds+(state.breakTime?0:1))+"/"+state.loopCount);Write("PomodoroTimerStateView","_pomodoroTypeText",player.story.UITranslate(!state.timerRunning && hasTimer?"ui_pomodoro_pause_title":state.breakTime?"ui_pomodoro_break_title":"ui_pomodoro_work_title"));
            foreach(var pair in new[]{("_workTimeMaxMinutesText",state.workMinutes),("_breakTimeMaxMinutesText",state.breakMinutes),("_loopCountMaxText",state.loopCount)}){var input=Get<TMP_InputField>("PomodoroTimerUI",pair.Item1);if(input && !input.isFocused){input.readOnly=false;input.SetTextWithoutNotify(pair.Item2.ToString());input.ForceLabelUpdate();}if(input)input.interactable=!hasTimer && !state.playing;}
            var timerImage=Get<Image>("PomodoroTimerStateView","_pomodoroPlayOrPauseButtonImage");if(timerImage)timerImage.sprite=state.timerRunning?timerPause:timerPlay;
            Set(RefOf("PomodoroTimerStateView","_pauseOverWriteImage"),hasTimer && !state.timerRunning);Set(RefOf("PomodoroTimerStateView","_restOverWriteImage"),hasTimer && state.breakTime);
            RefreshTimerModes();
            Set(Find("Paremt/PCPlatform/Canvas/UI/UI_FacilityPlayerLevel/LevelExpAppImage"),false);
            var levelMeter=Get<Image>("AddExpMeterUI","_meterOverWriteImage");if(levelMeter)levelMeter.fillAmount=Mathf.Clamp01(state.exp/Mathf.Max(1,state.requiredExp));
            var note=Get<TMP_InputField>("CurrentPageUI","_mainInputField");if(note && !note.isFocused)note.SetTextWithoutNotify(state.note ?? "");
            Write("TodoListUI","_remainTodoCountText",state.todos.Count(t=>!t.done).ToString());
            if(pane=="todos")TodoRows();if(pane=="story")StoryRows();Dialogue();
            RefreshControls();
            foreach(var label in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                if(label.name=="PlayerLevelText" || label.name=="LevelCurrentText")label.text=state.level.ToString();
                if(label.name=="CurrentDateText" || label.name=="DateText" && label.transform.parent.name=="UI_FacilityCurrentDateAndTime")label.text=DateTime.Now.ToString("dddd, MMMM dd, yyyy");
                if(label.name=="CurrentTimeText")label.text=DateTime.Now.ToString(player.story.Settings.clock24?"HH:mm":"hh:mm");
                if(label.name=="AMPMText")label.text=player.story.Settings.clock24?"":DateTime.Now.ToString("tt");
            }
        }
        public int HookCount => hooks.Count;
        public void RefreshNow(){Refresh(true);}
        void RefreshTimerModes()
        {
            bool countup=state.timerMode==1,active=countup?state.countupActive:state.timerActive,available=!state.playing;
            Show(RefOf("TimerParentUI","_pomodoroTimerActivater")?.gameObject,!countup);Show(RefOf("TimerParentUI","_countupTimerActivater")?.gameObject,countup);
            Show(RefOf("TimerParentUI","_timerSettingMenuCanvasGroup")?.gameObject,!active);
            Show(RefOf("TimerParentUI","_pomodoroPresetOpenButton")?.gameObject,!countup && !active);
            foreach(var pair in new[]{("_changePomodoroTimerButton",0),("_changeCountupTimerButton",1)})
            {var button=Get<Button>("TimerParentUI",pair.Item1);if(button){button.interactable=available && !active && state.timerMode!=pair.Item2;button.GetComponent<ChillNativeInteraction>()?.SetUsing(state.timerMode==pair.Item2);}}
            foreach(string key in new[]{"_addWorkTimeButton","_subtractWorkTimeButton","_addBreakTimeButton","_subtractBreakTimeButton","_addLoopCountButton","_subtractLoopButtonButton","_togglePomodoroPresetButton"}){var button=Get<Button>("PomodoroTimerUI",key);if(button)button.interactable=!state.timerActive && available;}
            foreach(string script in new[]{"PomodoroTimerUI","CountupTimerUI"})foreach(string key in new[]{"_startPomodoroButton","_playOrPausePomodoroButton","_resetButton","_skipPomodoroButton","_startCountupButton","_playOrPauseCountupButton"}){var button=Get<Button>(script,key);if(button)button.interactable=available;}
            if(countup)
            {Show(RefOf("CountupTimerStateView","_settingParentObject")?.gameObject,!active);Show(RefOf("CountupTimerStateView","_usingTimerParentObject")?.gameObject,active);Show(RefOf("CountupTimerStateView","_settingBaseCanvas")?.gameObject,!active);
             Set(RefOf("CountupTimerStateView","_pauseOverWriteImage"),active && !state.countupRunning);Write("CountupTimerStateView","_timeText",state.countupText);Write("CountupTimerStateView","_countupTypeText",player.story.UITranslate(state.countupRunning?"ui_pomodoro_work_title":"ui_pomodoro_pause_title"));
             var icon=Get<Image>("CountupTimerStateView","_countupPlayOrPauseButtonImage");if(icon)icon.sprite=state.countupRunning?countupPause:countupPlay;}
            if(pane=="presets")
            {var rows=JsonUtility.FromJson<Configuration>(configuration.text).bindings.Where(b=>b.script=="PomodoroPresetInstance").OrderBy(b=>b.path,StringComparer.Ordinal).ToArray();for(int i=0;i<rows.Length;i++){var preset=state.timerPresets[i];foreach(var pair in new[]{("nameInput",preset.name),("loopCountInput",preset.loopCount.ToString()),("workTimeInput",preset.workMinutes.ToString()),("breakTimeInput",preset.breakMinutes.ToString())}){var input=Find(rows[i].refs.First(r=>r.name==pair.Item1).path)?.GetComponent<TMP_InputField>();if(input && !input.isFocused){input.SetTextWithoutNotify(pair.Item2 ?? "");input.ForceLabelUpdate();}}}}
        }
        void RefreshControls()
        {
            bool available=state.ready && !state.playing;
            if(!available && languageOpen)SetLanguageOpen(false);
            for(int i=0;i<(workModeButtons?.Length ?? 0);i++)
            {
                var button=workModeButtons[i];button.interactable=available;
                var label=button.GetComponentInChildren<TMP_Text>(true);if(label)label.text=state.language=="Ja"?new[]{"読書","書く","タイピング"}[i]:new[]{"Read","Write","Type"}[i];
                Highlight(button,i==player.SelectedState);
            }
            foreach(var choice in environmentChoices ?? Array.Empty<EnvironmentChoice>())
            {
                var lockImage=choice.root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name.Contains("LockImage"));if(lockImage)Set(lockImage,player.story.FeatureReady&&!player.story.ItemAvailable("environment",player.story.WindowItem(choice.index)));
                if(choice.main){choice.main.interactable=available;Highlight(choice.main,choice.index==player.environment.selectedEnvironment);}
                if(choice.view){choice.view.interactable=available;Highlight(choice.view,choice.index==player.environment.selectedEnvironment);}
            }
            if(clearEnvironmentButton){clearEnvironmentButton.interactable=available;Highlight(clearEnvironmentButton,player.environment.selectedEnvironment==0);}
            string[] dayKeys={"_timeDayController","_timeSunsetController","_timeNightController","_timeCloudyController"};
            for(int i=0;i<dayKeys.Length;i++){var button=Get<Button>("EnvironmentUI",dayKeys[i]);if(button){button.interactable=available;Highlight(button,i==player.environment.selectedTime);}}
            var languageRoot=RefOf("SettingUI","_languagePulldownList");var current=languageRoot?languageRoot.Find("Pulldown/TodoGroupText (TMP)")?.GetComponent<TMP_Text>():null;
            if(current)current.text=LanguageCaption(state.language);
            foreach(string key in new[]{"_languageENInteractableUI","_languageJPInteractableUI"}){var button=Get<Button>("SettingUI",key);if(button)button.interactable=available;}
            var languageToggle=languageRoot?languageRoot.Find("PulldownButton")?.GetComponent<Button>():null;if(languageToggle)languageToggle.interactable=available;
            if(historyDialogueButton){historyDialogueButton.gameObject.SetActive(state.playing && !state.automaticDialogue);historyDialogueButton.interactable=state.ready;var caption=historyDialogueButton.GetComponentInChildren<TMP_Text>(true);if(caption)caption.text=state.language=="Ja"?"履歴":"History";}
            if(pane=="history" && historyInput)
            {
                var snapshot=player.story.UISnapshot("history");string text=snapshot.history.Length==0?(state.language=="Ja"?"会話履歴はまだありません。":"No conversation history yet."):string.Join("\n\n",snapshot.history);
                if(historyInput.text!=text){historyInput.SetTextWithoutNotify(text);historyInput.ForceLabelUpdate();}
                if(historyTitle)historyTitle.SetTextWithoutNotify(state.language=="Ja"?"会話履歴":"Conversation history");
            }
            if(state.noticeRevision!=shownNoticeRevision)
            {
                shownNoticeRevision=state.noticeRevision;noticeUntil=Time.unscaledTime+6;
                if(noticeText){noticeText.text=state.noticeMessage;noticeText.color=state.noticeError?new Color(1,.7f,.7f):Color.white;}
                Show(noticePanel,!string.IsNullOrEmpty(state.noticeMessage));if(noticePanel)noticePanel.transform.SetAsLastSibling();
            }
            else if(!state.noticeError && Time.unscaledTime>noticeUntil)Show(noticePanel,false);
            if(noticePanel && noticePanel.activeSelf)noticePanel.transform.SetAsLastSibling();
        }
        static void Highlight(Button button,bool selected){var colors=button.colors;colors.normalColor=selected?new Color(.65f,1,1):Color.white;button.colors=colors;}
        void LegacyMusicRows()
        {
            var items=player.library.MusicEntries;string signature=string.Join("|",items.Select(t=>t.guid));if(signature==musicSignature)return;musicSignature=signature;
            Clear(musicRows);var parent=RefOf("MusicPlayListView","_playListButtonsParent");if(!parent || !musicCell)return;
            Set(RefOf("MusicPlayListView","_dummyButton"),false);Layout(parent,((RectTransform)musicCell.transform).rect.height);
            foreach(var item in items){var go=Cell(musicCell,parent);SetText(go,"MusicTitleText",item.name);SetText(go,"MusicArtistNameText","");foreach(var t in go.GetComponentsInChildren<Transform>(true))if(t.name=="RemoveButton" || t.name=="FavoriteIcon" || t.name=="GreyOutImage" || t.name=="Doraggable" || t.name=="PlayCheck" || t.name=="Playing")t.gameObject.SetActive(false);var play=go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="PlayListMusicPlayButton");Bind(play?play.gameObject:go,()=>{if(!player.story.Playing)player.library.SelectMusic(item.guid);});musicRows.Add(go);}
            var scroll=Get<ScrollRect>("MusicPlayListView","_scrollRect");if(scroll){Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(parent as RectTransform);scroll.verticalNormalizedPosition=1;}
        }
        void StoryRows()
        {
            var snapshot=player.story.UISnapshot("story",true);string signature=JsonUtility.ToJson(snapshot.chapters == null ? new ChillStoryUiState() : new ChillStoryUiState{chapters=snapshot.chapters});if(signature==storySignature)return;storySignature=signature;
            Clear(storyRows);var parent=RefOf("StorySelectUI","cellParent");if(!parent || !storyCell)return;Layout(parent,115);
            if(!string.IsNullOrEmpty(snapshot.chapter)){var resume=Cell(storyCell,parent);SetText(resume,"MainTitleText","Continue reading");SetText(resume,"SubTitleText",snapshot.chapter);var button=resume.GetComponentInChildren<Button>(true) ?? resume.AddComponent<Button>();button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>{Action("story-resume");Close();});foreach(var child in resume.GetComponentsInChildren<Button>(true))Bind(child.gameObject,()=>{Action("story-resume");Close();});storyRows.Add(resume);}
            foreach(var item in snapshot.chapters){var go=Cell(storyCell,parent);SetText(go,"MainTitleText",item.title);SetText(go,"SubTitleText",item.detail);foreach(var t in go.GetComponentsInChildren<Transform>(true))if(t.name=="NewMark")t.gameObject.SetActive(!item.done);var buttons=go.GetComponentsInChildren<Button>(true);if(buttons.Length==0)buttons=new[]{go.AddComponent<Button>()};foreach(var button in buttons){Bind(button.gameObject,()=>{if(item.enabled){Action("story-start",id:item.id);Close();}});button.interactable=item.enabled;}storyRows.Add(go);}
        }
        void TodoRows()
        {
            string signature=player.story.Workspace.listId+completedExpanded+string.Join("|",state.todos.Select(t=>t.id+":"+t.title+":"+t.done+":"+t.due));if(signature==todoSignature)return;todoSignature=signature;
            Clear(todoRows);var parent=RefOf("TodoListUI","_uncompleteTodoUIParent");if(!parent || !todoCell)return;Layout(parent,78);Set(RefOf("TodoListUI","dummyButton"),false);
            var completed=RefOf("TodoListUI","_completeTodoUIParent");if(completed)Layout(completed,78);Set(RefOf("TodoListUI","_completedSection"),completedExpanded);
            for(int i=0;i<state.todos.Length;i++)
            {
                int index=i;var todo=state.todos[i];if(todo.done&&!completedExpanded)continue;var go=Cell(todoCell,todo.done&&completed?completed:parent);var input=go.GetComponentInChildren<TMP_InputField>(true);
                if(input){input.richText=false;input.characterLimit=200;input.SetTextWithoutNotify(todo.title);input.readOnly=false;input.onEndEdit.RemoveAllListeners();input.onEndEdit.AddListener(value=>player.story.EditTodo(todo.id,value,todo.due,todo.completedDate));}
                foreach(var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if(t.name=="DateTime"){t.gameObject.SetActive(true);var label=t.GetComponentInChildren<TMP_Text>(true);if(label){label.text=todo.done?todo.completedDate:todo.due==""?"Date":todo.due;label.enableAutoSizing=true;label.fontSizeMin=9;label.fontSizeMax=16;}foreach(var extra in t.GetComponentsInChildren<TMP_Text>(true))if(extra!=label)extra.gameObject.SetActive(false);Bind(t.gameObject,()=>EditTodoDate(todo));}
                    if(t.name=="DragButton"){t.gameObject.SetActive(true);var drag=t.gameObject.GetComponent<ChillNativeRowDrag>()??t.gameObject.AddComponent<ChillNativeRowDrag>();drag.moved=step=>player.story.MoveTodo(index,step);}
                    if(t.name=="BackCompleteImage"||t.name=="TodoSwitchFinishButtonImage")t.gameObject.SetActive(todo.done);
                    if(t.name=="TodoSwitchFinishButton")Bind(t.gameObject,()=>Action("todo-toggle",index));if(t.name=="TodoRemoveButton")Bind(t.gameObject,()=>Action("todo-delete",index));
                }todoRows.Add(go);
            }
        }
        void Dialogue()
        {
            bool main=state.playing && state.mainStoryText;
            ApplyStoryHud(main);
            Set(RefOf("StorySystemUI","_transparentButton"),state.playing && !state.waitingForScript);
            Show(RefOf("WantTalkUI","self")?.gameObject,state.waitingForScript || state.wantsToTalk);
            var talkGuide=RefOf("WantTalkUI","_wantTalkImage")?.GetComponent<Image>();if(talkGuide && wantTalkNormal)talkGuide.sprite=RecoveredSprite(wantTalkNormal);
            Set(RefOf("StorySystemUI","self"),state.playing);Set(RefOf("StorySystemUI","_heroineNameObject"),main && !string.IsNullOrEmpty(state.speaker));
            Set(RefOf("StorySystemUI","_autoButton"),state.playing && !state.automaticDialogue);Set(RefOf("StorySystemUI","_skipButton"),state.playing && !state.automaticDialogue);
            Write("StorySystemUI","_heroineNameTextLocalizationBehavior",state.speaker);Write("StorySystemUI","_auteText",state.autoRead?"Auto ON":"Auto");Write("StorySystemUI","_skipText","Skip");
            // Source StorySystemUI uses NormalTextParent for greetings/reactions,
            // and MainStoryTextMessage only for main/after/DLC/extra/special stories.
            Set(RefOf("StorySystemUI","_mainStoryTextParent"),main);
            Show(RefOf("StorySystemUI","_normalTextParent")?.gameObject,state.playing && !main);
            var bottom=RefOf("StorySystemUI","_bottomBackImage") as RectTransform;if(bottom)bottom.sizeDelta=new Vector2(bottom.sizeDelta.x,main?308:180);
            var target=RefOf("StorySystemUI",main?"_mainStoryTextMessage":"_normalTextMessage");var label=target ? target.GetComponent<TMP_Text>() ?? target.GetComponentInChildren<TMP_Text>(true) : null;
            if(label){label.richText=false;label.text=state.text;}
            string signature=JsonUtility.ToJson(new ChillStoryUiState{choices=state.choices});if(signature==choiceSignature)return;choiceSignature=signature;Clear(choiceRows);
            var parent=RefOf("SentenceSelectionButtonsUI","self");if(!parent || !selectionCell)return;
            Layout(parent,72);
            Show(parent.gameObject,state.playing && state.choices.Length>0);
            for(int i=0;i<state.choices.Length;i++){int index=i;var choice=state.choices[i];var go=Cell(selectionCell,parent);var text=go.GetComponentInChildren<TMP_Text>(true);if(text){text.richText=false;text.text=choice.title;}Bind(go,()=>Action("choice",index));go.GetComponent<Button>().interactable=choice.enabled;choiceRows.Add(go);}
        }
        static void SetText(GameObject go,string name,string value){var t=go.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name==name);if(t){t.richText=false;t.text=value;}}
        void Layout(Transform parent,float height)
        {
            rowHeights[parent]=height;
            var layout=parent.GetComponent<VerticalLayoutGroup>() ?? parent.gameObject.AddComponent<VerticalLayoutGroup>();layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;layout.spacing=5;
            var size=parent.GetComponent<ContentSizeFitter>() ?? parent.gameObject.AddComponent<ContentSizeFitter>();size.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        }
        GameObject Cell(GameObject prefab,Transform parent)
        {var go=Instantiate(prefab,parent,false);Show(go,true);FixSprites(go);CleanEvents(go);foreach(var a in go.GetComponentsInChildren<Animator>(true))a.enabled=false;var element=go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();element.ignoreLayout=false;element.minHeight=element.preferredHeight=rowHeights.TryGetValue(parent,out var height)?height:64;element.flexibleHeight=0;return go;}
        static void CleanEvents(GameObject root)
        {
            foreach(var input in root.GetComponentsInChildren<TMP_InputField>(true)){input.onValueChanged=new TMP_InputField.OnChangeEvent();input.onEndEdit=new TMP_InputField.SubmitEvent();input.onSubmit=new TMP_InputField.SubmitEvent();input.onSelect=new TMP_InputField.SelectionEvent();input.onDeselect=new TMP_InputField.SelectionEvent();}
            foreach(var button in root.GetComponentsInChildren<Button>(true))button.onClick=new Button.ButtonClickedEvent();
            foreach(var slider in root.GetComponentsInChildren<Slider>(true))slider.onValueChanged=new Slider.SliderEvent();
            foreach(var toggle in root.GetComponentsInChildren<Toggle>(true))toggle.onValueChanged=new Toggle.ToggleEvent();
        }
        void Clear(List<GameObject> items){foreach(var go in items)if(go){go.SetActive(false);if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}items.Clear();hooks.RemoveAll(h=>!h.button);}
        void HideUnsupportedSettings()
        {
            foreach(string key in new[]{"_accountParent","_newsParent","_accountInteractableUI","_newsInteractableUI"})Set(RefOf("SettingUI",key),false);
        }
        void SettingsTab(string key)
        {
            Set(RefOf("SettingUI","_generalParent"),key=="_generalInteractableUI");Set(RefOf("SettingUI","_graphicParent"),key=="_graphicInteractableUI");Set(RefOf("SettingUI","_audioParent"),key=="_audioInteractableUI");Set(RefOf("SettingUI","_creditsParent"),key=="_creditsInteractableUI");
        }
        void OnDisable(){if(initialized)ChillGameplayUI.UseNativeUI(false);}
    }
}
