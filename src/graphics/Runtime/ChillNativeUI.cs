using System;
using System.Collections.Generic;
using System.Globalization;
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
        [Serializable] public class Tooltip {public string path,key;public bool direct;}
        [Serializable] public class TooltipPresentation {public string root,text;public float activateDelay,deactivateDelay,fadeIn,fadeOut;public float[] offset;}
        [Serializable] public class TimerGuide {public string focus;public float fadeIn,fadeOut,rotate,scale,multiple;}
        [Serializable] public class Configuration { public Binding[] bindings;public Label[] labels;public Interaction[] interactions;public Tooltip[] tooltips;public TooltipPresentation tooltipPresentation;public TimerGuide firstTimerGuide;public string[] storyHiddenObjects; }
        public ChillGraphicsPlayer player;
        public Canvas canvas;
        public TextAsset configuration;
        public GameObject musicCell,storyCell,todoCell,todoListCell,selectionCell;
        public GameObject storyCategoryTitlePrefab,calendarHabitCell,specialReadIconPrefab;
        public GameObject[] specialCells;
        public Sprite musicPlay,musicPause,timerPlay,timerPause;
        public Sprite musicAudible,musicMuted;
        public Sprite countupPlay,countupPause,wantTalkNormal;
        [Serializable]public class SpriteReplacement{public Sprite original,recovered;}
        public SpriteReplacement[] spriteReplacements;
        [Serializable]public class SkinThumbnail{public int skinId;public Sprite sprite;}
        public SkinThumbnail[] skinThumbnails;
        public TMP_FontAsset fallbackFont;
        [Serializable] public class EnvironmentChoice {public GameObject root;public Button main,view;public int index,ambientId=-1;public string itemName,localizeKey;public Sprite shopThumbnail;}
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
        readonly HashSet<string> activeFloatingPanes=new HashSet<string>();
        GameObject openPane,exclusiveOpenPane;
        Transform nativeUiRoot;
        int nativeUiSibling;
        GameObject reveal;
        string pane="",exclusivePane="",language="",musicSignature,storySignature,todoSignature,choiceSignature;
        static bool IsFloatingPane(string name)=>name=="notes"||name=="todos"||name=="habits";
        static string PaneScript(string name)=>name=="music"?"MusicPlayListView":name=="notes"?"NoteUI":name=="todos"?"TodoListUI":name=="story"?"StorySelectUI":name=="environment"?"EnvironmentUI":name=="presets"?"PomodoroPresetUI":name=="calendar"?"CalendarUI":name=="habits"?"HabitTrackerUI":name=="statistics"?"TotalWorkStatisticsUI":name=="timer-audio"?"TimerSoundSettingView":name=="profile"?"PlayerProfileView":name=="heroine-profile"?"HeroineProfileView":name=="special"?"SpecialView":name=="decorations"?"DecorationListUI":"SettingUI";
        public bool IsPaneOpen(string name)=>IsFloatingPane(name)?activeFloatingPanes.Contains(name):exclusivePane==name||pane==name;
        bool initialized,hidden,shuffle,translatedReady;
        int hiddenFrame=-1;
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
        public static string BindingPathOf(Transform t)
        {
            string name=t.name;
            if(t.parent&&t.parent.name=="ChangeTimerUI")
            {
                var siblings=t.parent.Cast<Transform>().Where(child=>child.name==t.name).ToArray();
                if(siblings.Length>1)name+="["+Array.IndexOf(siblings,t)+"]";
            }
            return t.parent?BindingPathOf(t.parent)+"/"+name:name;
        }
        void Start() { Initialize(); }
        public void Initialize()
        {
            if (initialized) return; initialized=true;
            ChillGameplayUI.UseNativeUI(true);
            foreach(var t in UnityEngine.Object.FindObjectsOfType<Transform>(true)){paths[PathOf(t)]=t;paths[BindingPathOf(t)]=t;}
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
            foreach(var subCanvas in canvas.GetComponentsInChildren<Canvas>(true))
            {
                if(subCanvas==canvas||!subCanvas.overrideSorting)continue;
                if(subCanvas.sortingOrder<100)subCanvas.sortingOrder+=100;
                if(subCanvas.name=="SetBirthdayConfirmDialog_PC"&&subCanvas.sortingOrder<=102)subCanvas.sortingOrder=103;
            }
            foreach(var animator in canvas.GetComponentsInChildren<Animator>(true)) animator.enabled=false;
            PrepareRuntimeFonts();
            foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                if(text.font && text.fontSharedMaterial && text.font.atlasTexture && text.fontSharedMaterial.mainTexture != text.font.atlasTexture)text.fontSharedMaterial=text.font.material;
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
            Wire("MusicUI","_facilityOpenButton",()=>Open("music"));Wire("MusicUI","_listCloseButton",CloseExclusivePane);
            Wire("MusicUI","_playOrPauseButton",()=>{if(player.music.isPlaying)player.music.Pause();else player.music.Play();});
            Wire("MusicUI","_musicNextButton",()=>NextMusic(1));Wire("MusicUI","_musicBackButton",()=>NextMusic(-1));
            Wire("MusicUI","_shuffleButton",()=>shuffle=!shuffle);Wire("MusicUI","_loopButton",()=>player.music.loop=!player.music.loop);
            Wire("MusicUI","_switchMuteButton",()=>{if(player.Volume > 0){previousVolume=player.Volume;player.SetVolume(0);}else player.SetVolume(previousVolume);});
            volumeSlider=Get<Slider>("MusicUI","_volumeSlider");
            volumePadding=RefOf("MusicVolumeUI","sliderPadding") as RectTransform;volumeButton=RefOf("MusicVolumeUI","pointerEnterHandler") as RectTransform;
            if(volumeSlider){volumeSlider.gameObject.SetActive(false);volumeSlider.onValueChanged.RemoveAllListeners();volumeSlider.onValueChanged.AddListener(value=>{if(value>0)previousVolume=value;player.SetVolume(value);});}
            if(volumeButton){var trigger=volumeButton.GetComponent<EventTrigger>() ?? volumeButton.gameObject.AddComponent<EventTrigger>();trigger.triggers.Clear();var enter=new EventTrigger.Entry{eventID=EventTriggerType.PointerEnter};enter.callback.AddListener(_=>SetVolumePopup(true));trigger.triggers.Add(enter);}
            Wire("NoteUI","_facilityOpenButton",()=>Open("notes"));Wire("NoteUI","_noteCloseButton",()=>CloseFloatingPane("notes"));
            var note=Get<TMP_InputField>("CurrentPageUI","_mainInputField");if(note){note.characterLimit=64000;note.richText=false;note.onValueChanged.RemoveAllListeners();note.onValueChanged.AddListener(value=>Action("note",text:value));}
            var title=Get<TMP_InputField>("CurrentPageUI","_titleInputField");if(title){title.SetTextWithoutNotify("Notes");title.readOnly=true;}
            Wire("TodoListUI","_facilityOpenButton",()=>Open("todos"));Wire("TodoListUI","_closeButton",()=>CloseFloatingPane("todos"));
            var add=Find(bindings["TodoListUI"].path+"/AddTodoUI");
            var addInput=add ? add.GetComponentInChildren<TMP_InputField>(true) : null;
            if(addInput){addInput.characterLimit=200;addInput.richText=false;Wire("TodoListUI","addTodoButton",()=>{if(!string.IsNullOrWhiteSpace(addInput.text)){Action("todo-add",text:addInput.text);addInput.SetTextWithoutNotify("");todoSignature=null;}});}
            Wire("StorySelectUI","_facilityOpenButton",()=>Open("story"));Wire("StorySelectUI","_closeButton",CloseExclusivePane);
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
             var play=Find(binding.refs.First(r=>r.name=="playButton").path);if(play)Bind(play.gameObject,()=>{Action("preset-play",slot);CloseExclusivePane();});}
            Wire("EnvironmentUI","_facilityOpenButton",()=>Open("environment"));Wire("EnvironmentUI","_closeButton",CloseExclusivePane);
            string[] dayKeys={"_timeDayController","_timeSunsetController","_timeNightController","_timeCloudyController"};
            for(int i=0;i<dayKeys.Length;i++){int value=i;Wire("EnvironmentUI",dayKeys[i],()=>Action("time-mode",value));}
            InitializeEnvironmentCards();
            Wire("SettingUI","_facilityOpenButton",()=>Open("settings"));Wire("SettingUI","_closeButton",CloseExclusivePane);
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
            foreach(var pair in new[]{("WebExportSaveButton","save-export"),("WebImportSaveButton","save-import")})
            {
                var row=Find(saveRoot+pair.Item1);if(!row)continue;
                string act=pair.Item2;
                Bind(row.gameObject,()=>Action(act));
                foreach(var btn in row.GetComponentsInChildren<Button>(true))Bind(btn.gameObject,()=>Action(act));
            }
            Show(noticePanel,false);
            if(noticePanel)foreach(var graphic in noticePanel.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
            WirePath("Paremt/PCPlatform/Canvas/UI/MostFrontArea/RightIcons/IconHide_Button",ToggleHidden);
            foreach(string type in new[]{"NoteUI","TodoListUI","StorySelectUI","MusicPlayListView","EnvironmentUI","SettingUI"}){var window=RefOf(type,"self");var handle=window?window.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="DragButton"):null;if(handle){var drag=handle.gameObject.AddComponent<ChillNativeDrag>();drag.target=window as RectTransform;}}
            foreach(var button in canvas.GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
            // Rebuild listeners after clearing serialized Windows callbacks.
            foreach(var hook in hooks) {hook.button.onClick.RemoveAllListeners();hook.button.onClick.AddListener(()=>{hook.action();player.story.UISelectSound();});}
            foreach(var button in canvas.GetComponentsInChildren<Button>(true))if(!hooks.Any(h=>h.button==button))button.interactable=false;
            foreach(var settings in config.interactions ?? Array.Empty<Interaction>())
            {var target=Find(settings.path);if(!target)continue;var interaction=target.GetComponent<ChillNativeInteraction>() ?? target.gameObject.AddComponent<ChillNativeInteraction>();
             Graphic[] Images(string[] refs)=>(refs ?? Array.Empty<string>()).Select(p=>Find(p)?.GetComponent<Graphic>()).Where(g=>g).ToArray();
             interaction.hover=Images(settings.hover);interaction.use=Images(settings.use);interaction.baseline=Images(settings.baseline);interaction.hoverAlpha=settings.hoverAlpha;interaction.useAlpha=settings.useAlpha;interaction.baseAlpha=settings.baseAlpha;interaction.hideHoverOnUse=settings.hideHoverOnUse;interaction.SetUsing(false);
             var button=target.GetComponent<Button>();if(button)button.transition=UnityEngine.UI.Selectable.Transition.None;}
            var modeRoots=new[]{RefOf("TimerParentUI","_changePomodoroTimerButton")?.parent,RefOf("TimerParentUI","_changeCountupTimerButton")?.parent};
            var modeParent=modeRoots.FirstOrDefault(t=>t)?.parent;
            if(modeParent)foreach(Transform child in modeParent)if(child.GetComponentInChildren<Button>(true)&&!modeRoots.Contains(child))child.gameObject.SetActive(false);
            foreach(var root in modeRoots.Where(t=>t))foreach(var graphic in root.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=graphic.GetComponent<Button>();
            InitializeFeatureControls();InitializeWorkspaceControls();InitializeCompletionControls();RepairPaneLayouts();InitializeTooltips(config);Show(RefOf("FacilityProfile","_canvasGroup")?.gameObject,true);Set(RefOf("FacilityProfile","_background"),false);Set(RefOf("PlayerProfileView","self"),false);Set(RefOf("HeroineProfileView","self"),false);Close();Refresh(true);
        }
        class Hook {public Button button;public Action action;}
        readonly List<Hook> hooks=new List<Hook>();
        public Transform Find(string path) => path != null && paths.TryGetValue(path,out var t) ? t : null;
        public Transform RefOf(string script,string key)
        {return bindings.TryGetValue(script,out var b) ? Find(key == "self" ? b.path : b.refs.FirstOrDefault(r=>r.name==key)?.path) : null;}
        T Get<T>(string script,string key) where T:Component {var t=RefOf(script,key);return t ? t.GetComponent<T>() ?? t.GetComponentInChildren<T>(true) : null;}
        static void Set(Transform t,bool active){if(t)t.gameObject.SetActive(active);}
        static void Show(GameObject go,bool active)
        {if(!go)return;go.SetActive(active);if(active){var group=go.GetComponent<CanvasGroup>();if(group){group.alpha=1;group.interactable=group.blocksRaycasts=true;}}}
        void Wire(string script,string key,Action callback){var t=RefOf(script,key);if(t)Bind(t.gameObject,callback);}
        void WirePath(string path,Action callback){var t=Find(path);if(t)Bind(t.gameObject,callback);}
        void Bind(GameObject go,Action callback)
        {
            if(!go)return;
            var button=go.GetComponent<Button>();
            if(!button)
            {
                var toggle=go.GetComponent<Toggle>();
                if(toggle)
                {
                    var target=toggle.targetGraphic;
                    if(Application.isPlaying)Destroy(toggle);else DestroyImmediate(toggle);
                    button=go.AddComponent<Button>();
                    if(button&&target)button.targetGraphic=target;
                }
                else button=go.AddComponent<Button>();
            }
            if(!button)return;
            if(!button.targetGraphic)button.targetGraphic=go.GetComponent<Graphic>() ?? go.GetComponentInChildren<Graphic>(true);
            if(button.targetGraphic)button.targetGraphic.raycastTarget=true;
            button.interactable=true;button.onClick.RemoveAllListeners();
            button.onClick.AddListener(()=>{callback();player.story.UISelectSound();});
            hooks.RemoveAll(h=>!h.button||h.button==button);
            hooks.Add(new Hook{button=button,action=callback});
        }
        public void UICommand(string json)
        {
            try
            {
                var command=JsonUtility.FromJson<ChillUiCommand>(json);
                if(command==null || string.IsNullOrEmpty(command.action))return;
                player.story.UIAction(command);nextUpdate=0;
            }
            catch(Exception error){player.story.Notify("Invalid UI command: "+error.Message,true);}
        }
        void Action(string action,float value=0,string text=null,string id=null){player.story.UIAction(new ChillUiCommand{action=action,value=value,text=text,id=id});nextUpdate=0;}
        void SelectLanguage(string value){Action("language",text:value);storySignature=null;SetLanguageOpen(false);Refresh(true);}
        void SetLanguageOpen(bool value)
        {
            languageOpen=value;Set(languageOptions,value);
            if(languageDropdown)languageDropdown.sizeDelta=new Vector2(languageDropdown.sizeDelta.x,languageClosedHeight+(value?400:0));
        }
        void NumberInput(string field,string action){var input=Get<TMP_InputField>("PomodoroTimerUI",field);if(input){input.onEndEdit.RemoveAllListeners();input.onEndEdit.AddListener(value=>{if(int.TryParse(value,out int n))Action(action,n);});}}
        public void Open(string name)
        {
            if((name=="story"||name=="special")&&player.story.IsStorySpecialLocked)
            {
                player.story.StartAutomaticScenario(1030);
                return;
            }
            if((name=="story"||name=="special")&&player.story.IsFacilityLocked)return;
            bool floating=IsFloatingPane(name);
            if(floating?activeFloatingPanes.Contains(name):exclusivePane==name)
            {
                if(floating)CloseFloatingPane(name);else CloseExclusivePane();
                return;
            }
            player.story.TutorialSignal(name);
            if(!floating)CloseExclusivePane();
            pane=name;
            if(floating)activeFloatingPanes.Add(name);else exclusivePane=name;
            player.story.HistoryVisible=exclusivePane=="history";
            string script=PaneScript(name);
            openPane=name=="history"||name=="achievements"?historyPanel:RefOf(script,"self")?.gameObject;
            if(!floating)exclusiveOpenPane=openPane;
            Show(openPane,true);if(openPane){if(nativeUiRoot)nativeUiRoot.SetAsLastSibling();openPane.transform.SetAsLastSibling();}
            ShowWorkspacePane();
            SetPaneUsing(pane);if(name=="story")storySignature=null;if(name=="todos")todoSignature=null;if(name=="music")musicSignature=null;Refresh(true);
            if(name=="decorations"||name=="environment"||name=="special"||name=="settings")
            {
                if(openPane){foreach(var scroll in openPane.GetComponentsInChildren<ScrollRect>(true)){if(scroll.content)LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=1f;if(scroll.content)scroll.content.anchoredPosition=new Vector2(scroll.content.anchoredPosition.x,0f);}}
            }
        }
        public void CloseFloatingPane(string name)
        {
            if(!activeFloatingPanes.Remove(name))return;
            if(tooltip)tooltip.Dismiss();
            if(name=="todos"){SetTodoPulldownOpen(false);CloseTodoMiniCalendar();}
            if(name=="habits"){CloseHabitDeleteConfirm();if(habitEdit)FinishHabitEdit(false);}
            Show(RefOf(PaneScript(name),"self")?.gameObject,false);
            SyncActivePaneReference();
            SetPaneUsing(pane);
        }
        public void CloseExclusivePane()
        {
            if(tooltip)tooltip.Dismiss();
            CloseBirthdayDialogs();HideSkinSelectors();CloseEnvironmentPopovers();CloseCalendarMiniCalendar();
            SetFirstDayOpen(false);SetResolutionOpen(false);SetQualityOpen(false);SetTagListOpen(false);
            Set(RefOf("ProfileIconSettingView","self"),false);Set(RefOf("StatusChangeView","self"),false);
            Show(exclusiveOpenPane,false);exclusiveOpenPane=null;exclusivePane="";
            player.story.HistoryVisible=false;SetLanguageOpen(false);
            SyncActivePaneReference();
            SetPaneUsing(pane);
        }
        void SyncActivePaneReference()
        {
            if(exclusiveOpenPane&&exclusiveOpenPane.activeSelf){pane=exclusivePane;openPane=exclusiveOpenPane;}
            else if(activeFloatingPanes.Count>0){pane=activeFloatingPanes.First();openPane=RefOf(PaneScript(pane),"self")?.gameObject;}
            else{pane="";openPane=null;if(nativeUiRoot)nativeUiRoot.SetSiblingIndex(nativeUiSibling);}
        }
        public void Close(){foreach(var fp in activeFloatingPanes.ToArray())CloseFloatingPane(fp);CloseExclusivePane();Show(openPane,false);openPane=null;pane="";if(nativeUiRoot)nativeUiRoot.SetSiblingIndex(nativeUiSibling);}
        void ToggleHidden(){if(StoryHudHidden)return;hidden=!hidden;hiddenFrame=Time.frameCount;Set(Find("Paremt/PCPlatform/Canvas/UI"),!hidden);if(reveal)reveal.SetActive(false);Close();}
        void NextMusic(int step)
        {if(player.story.Playing || player.library.Busy)return;var entries=player.library.MusicEntries;if(entries.Length==0)return;int current=Array.FindIndex(entries,t=>player.music.clip && t.name==player.music.clip.name);int index=shuffle?UnityEngine.Random.Range(0,entries.Length):(current+step+entries.Length)%entries.Length;player.library.SelectMusic(entries[index].guid);}
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.F2))HandleF2KeyPress(Time.unscaledTime);
            if((exclusivePane=="presets"||exclusivePane=="timer-audio") && Input.GetMouseButtonUp(0) && exclusiveOpenPane && !Contains(exclusiveOpenPane.transform as RectTransform))CloseExclusivePane();
            if(volumeSlider && volumeSlider.gameObject.activeInHierarchy && !Input.GetMouseButton(0) && !Contains(volumePadding) && !Contains(volumeButton))SetVolumePopup(false);
            if(hidden && !StoryHudHidden && Time.frameCount>hiddenFrame && (Input.GetMouseButtonDown(0) || Input.touchCount>0&&Input.GetTouch(0).phase==TouchPhase.Began)){ToggleHidden();return;}
            if(Input.GetKeyDown(KeyCode.Escape)){if(hidden && !StoryHudHidden)ToggleHidden();else Close();}
            UpdatePurchasePopover();
            UpdateHabitDeletePopover();
            UpdateNativeDebugOverlay();
            UpdateMusicTextScrollers();
            var pauseSign=RefOf("PomodoroTimerStateView","_pauseOverWriteImage");
            if(pauseSign && pauseSign.gameObject.activeInHierarchy)
            {
                float cycle=Time.unscaledTime%15f;
                float z=cycle<.8f?Mathf.Sin(cycle/.8f*Mathf.PI*6f)*8f*(1f-cycle/.8f):0f;
                pauseSign.localEulerAngles=new Vector3(0f,0f,z);
            }
            var expSpin=Find("Paremt/PCPlatform/Canvas/UI/UI_FacilityPlayerLevel/LevelExpAppImage");
            if(expSpin && expSpin.gameObject.activeInHierarchy)expSpin.Rotate(0f,0f,-90f*Time.unscaledDeltaTime);
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
        static string FormatCurrentDate(DateTime now,string lang)
        {
            string cultureName=lang=="Ja"?"ja-JP":lang=="ZhHans"?"zh-CN":lang=="ZhHant"?"zh-TW":lang=="Ko"?"ko-KR":lang=="Pt"?"pt-BR":lang=="Ru"?"ru-RU":"en-US";
            string pattern=lang=="Ja"||lang=="ZhHans"||lang=="ZhHant"||lang=="Ko"?"yyyy/MM/dd(ddd)":lang=="Pt"||lang=="Ru"?"dd/MM/yyyy(ddd)":"ddd, MMM dd, yyyy";
            try{return now.ToString(pattern,new CultureInfo(cultureName));}catch{return now.ToString("ddd, MMM dd, yyyy",CultureInfo.InvariantCulture);}
        }
        void Refresh(bool force)
        {
            if(!initialized)return;state=player.story.UISnapshot("notes");
            if(language!=state.language || force || (!translatedReady&&state.ready)){translatedReady=state.ready;language=state.language;foreach(var pair in localized)if(pair.Key && labels.TryGetValue(pair.Value,out var label))pair.Key.text=player.story.UITranslate(pair.Value);}
            RefreshFeatureControls();RefreshWorkspaceControls();RefreshCompletionControls();
            bool facLocked=player.story.IsFacilityLocked;
            Set(Find("Paremt/PCPlatform/Canvas/UI/MostFrontArea/LeftIcons/StoryLockImage"),facLocked);
            Set(Find("Paremt/PCPlatform/Canvas/UI/UI_FacilityPomodoro/PomodoroTimer/PomodoroLockImage"),facLocked);
            Set(Find("Paremt/PCPlatform/Canvas/UI/UI_FacilityPomodoro/CountupTimer/CountupLockImage"),facLocked);
            if(facLocked&&(IsPaneOpen("story")||IsPaneOpen("special")))CloseExclusivePane();
            var playImage=Get<Image>("MusicUI","_playOrPauseButtonImage");if(playImage)playImage.sprite=player.music.isPlaying?musicPause:musicPlay;
            var volume=Get<Slider>("MusicUI","_volumeSlider");if(volume)volume.SetValueWithoutNotify(player.Volume);
            var audioIcon=Get<Image>("MusicVolumeUI","volumeIconImage");if(audioIcon)audioIcon.sprite=player.Volume>0?musicAudible:musicMuted;
            var progress=Get<Slider>("MusicUI","musicProgressSlider");if(progress && player.music.clip){progress.minValue=0;progress.maxValue=player.music.clip.length;if(!seekDragging)progress.SetValueWithoutNotify(player.music.time);}
            if(IsPaneOpen("music"))MusicRows();
            var setting=RefOf("PomodoroTimerStateView","_settingParentObject");var usingTimer=RefOf("PomodoroTimerStateView","_usingTimerParentObject");
            bool hasTimer=state.timerActive;
            Show(setting?setting.gameObject:null,!hasTimer);Show(usingTimer?usingTimer.gameObject:null,hasTimer);
            Set(RefOf("PomodoroTimerStateView","_settingBaseCanvas"),!hasTimer);
            Set(RefOf("PomodoroTimerStateView","_skipButtonObject"),hasTimer && state.timerRunning);
            var meter=Get<Image>("PomodoroTimerStateView","_meterOverWriteImage");if(meter){meter.fillAmount=state.timerMeter;meter.fillClockwise=!state.breakTime;}
            Write("PomodoroTimerStateView","_timeText",state.timerText);Write("PomodoroTimerStateView","_remainLoopCountText",Mathf.Min(state.loopCount,state.rounds+(state.breakTime?0:1))+"/"+state.loopCount);Write("PomodoroTimerStateView","_pomodoroTypeText",player.story.UITranslate(!state.timerRunning && hasTimer?"ui_pomodoro_pause_title":state.breakTime?"ui_pomodoro_break_title":"ui_pomodoro_work_title"));
            foreach(var pair in new[]{("_workTimeMaxMinutesText",state.workMinutes),("_breakTimeMaxMinutesText",state.breakMinutes),("_loopCountMaxText",state.loopCount)}){var input=Get<TMP_InputField>("PomodoroTimerUI",pair.Item1);if(input && !input.isFocused){input.readOnly=false;input.SetTextWithoutNotify(pair.Item2.ToString());input.ForceLabelUpdate();}if(input)input.interactable=!hasTimer && !state.playing && !facLocked;}
            var timerImage=Get<Image>("PomodoroTimerStateView","_pomodoroPlayOrPauseButtonImage");if(timerImage)timerImage.sprite=state.timerRunning?timerPause:timerPlay;
            Set(RefOf("PomodoroTimerStateView","_pauseOverWriteImage"),hasTimer && !state.timerRunning);Set(RefOf("PomodoroTimerStateView","_restOverWriteImage"),hasTimer && state.breakTime);
            RefreshTimerModes();
            bool earningExp=(state.timerRunning&&!state.breakTime)||state.countupRunning;
            Set(Find("Paremt/PCPlatform/Canvas/UI/UI_FacilityPlayerLevel/LevelExpAppImage"),earningExp);
            var lvlRoot=Find("Paremt/PCPlatform/Canvas/UI/UI_FacilityPlayerLevel");
            if(lvlRoot)
            {
                int collab=player.story.FeatureReady?player.story.Workspace.collaboration:0;
                Set(lvlRoot.Find("Normal_LevelBaseImage"),collab==0);
                Set(lvlRoot.Find("LevelCurrentText"),collab==0);
                Set(lvlRoot.Find("LevelTitleText"),collab==0);
                string[] collabUi={"AlterEgo_LevelUI","BearsRestaurant_LevelUI","Valentine2026_LevelUI","LunaNewYear2026_LevelUI","NearSpring2026_LevelUI","MagicalPrincess_LevelUI","StatisticsDev_LevelUI"};
                for(int i=0;i<collabUi.Length;i++)Set(lvlRoot.Find(collabUi[i]),collab==i+1);
            }
            var levelMeter=Get<Image>("AddExpMeterUI","_meterOverWriteImage");if(levelMeter)levelMeter.fillAmount=Mathf.Clamp01(state.exp/Mathf.Max(1,state.requiredExp));
            var note=Get<TMP_InputField>("CurrentPageUI","_mainInputField");if(note && !note.isFocused)note.SetTextWithoutNotify(state.note ?? "");
            Write("TodoListUI","_remainTodoCountText",state.todos.Count(t=>t.done)+"/"+state.todos.Length);
            if(IsPaneOpen("todos"))TodoRows();if(IsPaneOpen("story"))StoryRows();Dialogue();
            RefreshControls();
            var now=DateTime.Now;
            bool clock24=player.story.Settings.clock24;
            string dateStr=FormatCurrentDate(now,state.language);
            string timeStr=clock24||state.language=="ZhHans"&&now.Hour<13?now.ToString("HH:mm"):now.ToString("hh:mm");
            string ampmStr=clock24?"":player.story.UITranslate(state.language=="ZhHans"?(now.Hour<12?"ui_time_am":now.Hour<13?"ui_time_midtime":"ui_time_pm"):(now.Hour<12?"ui_time_am":"ui_time_pm"));
            foreach(var label in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                if(label.name=="PlayerLevelText" || label.name=="LevelCurrentText")label.text=state.level.ToString();
                if(label.name=="CurrentDateText" || label.name=="DateText" && label.transform.parent.name=="UI_FacilityCurrentDateAndTime")label.text=dateStr;
                if(label.name=="CurrentTimeText")label.text=timeStr;
                if(label.name=="AMPMText")label.text=ampmStr;
            }
        }
        public int HookCount => hooks.Count;
        public void RefreshNow(){Refresh(true);}
        int displayedTimerMode=-1;
        void RefreshTimerModes()
        {
            bool countup=state.timerMode==1,active=countup?state.countupActive:state.timerActive,available=!state.playing&&!player.story.IsFacilityLocked;
            Show(RefOf("TimerParentUI","_pomodoroTimerActivater")?.gameObject,!countup);Show(RefOf("TimerParentUI","_countupTimerActivater")?.gameObject,countup);
            Show(RefOf("TimerParentUI","_timerSettingMenuCanvasGroup")?.gameObject,!active);
            Show(RefOf("TimerParentUI","_pomodoroPresetOpenButton")?.gameObject,!countup && !active);
            foreach(var pair in new[]{("_changePomodoroTimerButton","_changePomodoroTimerInteractiveUI",0),("_changeCountupTimerButton","_changeCountupTimerInteractiveUI",1)})
            {
                var button=Get<Button>("TimerParentUI",pair.Item1);if(button)button.interactable=available && !active && state.timerMode!=pair.Item3;
                var interaction=Get<ChillNativeInteraction>("TimerParentUI",pair.Item2);
                if(interaction){if(displayedTimerMode!=state.timerMode)interaction.DeactivateAll();interaction.SetUsing(state.timerMode==pair.Item3);}
            }
            displayedTimerMode=state.timerMode;RefreshFirstGuide();
            foreach(string key in new[]{"_addWorkTimeButton","_subtractWorkTimeButton","_addBreakTimeButton","_subtractBreakTimeButton","_addLoopCountButton","_subtractLoopButtonButton","_togglePomodoroPresetButton"}){var button=Get<Button>("PomodoroTimerUI",key);if(button)button.interactable=!state.timerActive && available;}
            foreach(string script in new[]{"PomodoroTimerUI","CountupTimerUI"})foreach(string key in new[]{"_startPomodoroButton","_playOrPausePomodoroButton","_resetButton","_skipPomodoroButton","_startCountupButton","_playOrPauseCountupButton"}){var button=Get<Button>(script,key);if(button)button.interactable=available;}
            if(countup)
            {Show(RefOf("CountupTimerStateView","_settingParentObject")?.gameObject,!active);Show(RefOf("CountupTimerStateView","_usingTimerParentObject")?.gameObject,active);Show(RefOf("CountupTimerStateView","_settingBaseCanvas")?.gameObject,!active);
             Set(RefOf("CountupTimerStateView","_pauseOverWriteImage"),active && !state.countupRunning);Write("CountupTimerStateView","_timeText",state.countupText);Write("CountupTimerStateView","_countupTypeText",player.story.UITranslate(state.countupRunning?"ui_pomodoro_work_title":"ui_pomodoro_pause_title"));
             var icon=Get<Image>("CountupTimerStateView","_countupPlayOrPauseButtonImage");if(icon)icon.sprite=state.countupRunning?countupPause:countupPlay;}
            if(IsPaneOpen("presets"))
            {var rows=JsonUtility.FromJson<Configuration>(configuration.text).bindings.Where(b=>b.script=="PomodoroPresetInstance").OrderBy(b=>b.path,StringComparer.Ordinal).ToArray();for(int i=0;i<rows.Length;i++){var preset=state.timerPresets[i];foreach(var pair in new[]{("nameInput",preset.name),("loopCountInput",preset.loopCount.ToString()),("workTimeInput",preset.workMinutes.ToString()),("breakTimeInput",preset.breakMinutes.ToString())}){var input=Find(rows[i].refs.First(r=>r.name==pair.Item1).path)?.GetComponent<TMP_InputField>();if(input && !input.isFocused){input.SetTextWithoutNotify(pair.Item2 ?? "");input.ForceLabelUpdate();}}}}
        }
        void RefreshControls()
        {
            bool available=state.ready && !state.playing;
            if(!available && languageOpen)SetLanguageOpen(false);
            RefreshEnvironmentCards(available);
            string[] dayKeys={"_timeDayController","_timeSunsetController","_timeNightController","_timeCloudyController"};
            for(int i=0;i<dayKeys.Length;i++){var button=Get<Button>("EnvironmentUI",dayKeys[i]);if(button){button.interactable=available;Highlight(button,i==player.environment.selectedTime);}}
            var languageRoot=RefOf("SettingUI","_languagePulldownList");var current=languageRoot?languageRoot.Find("Pulldown/TodoGroupText (TMP)")?.GetComponent<TMP_Text>():null;
            if(current)current.text=LanguageCaption(state.language);
            foreach(string key in new[]{"_languageENInteractableUI","_languageJPInteractableUI"}){var button=Get<Button>("SettingUI",key);if(button)button.interactable=available;}
            var languageToggle=languageRoot?languageRoot.Find("PulldownButton")?.GetComponent<Button>():null;if(languageToggle)languageToggle.interactable=available;
            if(state.noticeRevision!=shownNoticeRevision)
            {
                shownNoticeRevision=state.noticeRevision;noticeUntil=Time.unscaledTime+6;
                if(noticeText){noticeText.text=state.noticeMessage;noticeText.color=state.noticeError?new Color(1,.7f,.7f):Color.white;}
                if(noticePanel&&noticePanel.transform.parent)noticePanel.transform.parent.gameObject.SetActive(true);
                Show(noticePanel,!string.IsNullOrEmpty(state.noticeMessage));
                if(noticePanel){if(noticePanel.transform.parent)noticePanel.transform.parent.SetAsLastSibling();noticePanel.transform.SetAsLastSibling();}
            }
            else if(Time.unscaledTime>noticeUntil)Show(noticePanel,false);
            if(noticePanel && noticePanel.activeSelf){if(noticePanel.transform.parent)noticePanel.transform.parent.SetAsLastSibling();noticePanel.transform.SetAsLastSibling();}
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
            var snapshot=player.story.UISnapshot("story",true);
            Get<ChillNativeInteraction>("StorySelectUI","_tagChangeToMainInteractableUI")?.SetUsing(snapshot.kind==0);
            Get<ChillNativeInteraction>("StorySelectUI","_tagChangeToSpecialInteractableUI")?.SetUsing(snapshot.kind==1);
            string signature=snapshot.kind+":"+JsonUtility.ToJson(snapshot.chapters == null ? new ChillStoryUiState() : new ChillStoryUiState{chapters=snapshot.chapters});if(signature==storySignature)return;storySignature=signature;
            Clear(storyRows);var parent=RefOf("StorySelectUI","cellParent");if(!parent || !storyCell)return;Layout(parent,115);
            string lastGroup="";
            foreach(var item in snapshot.chapters)
            {
                if(snapshot.kind==1&&!string.IsNullOrEmpty(item.groupTitle)&&item.groupTitle!=lastGroup&&storyCategoryTitlePrefab)
                {
                    lastGroup=item.groupTitle;
                    var header=Cell(storyCategoryTitlePrefab,parent);
                    SetText(header,"EnviromentNameText (TMP)",lastGroup);
                    storyRows.Add(header);
                }
                var go=Cell(storyCell,parent);SetText(go,"MainTitleText",item.title);SetText(go,"SubTitleText",item.detail);
                foreach(var t in go.GetComponentsInChildren<Transform>(true))if(t.name=="NewMark")t.gameObject.SetActive(!item.done&&item.enabled);
                var buttons=go.GetComponentsInChildren<Button>(true);if(buttons.Length==0)buttons=new[]{go.AddComponent<Button>()};
                foreach(var button in buttons){Bind(button.gameObject,()=>{if(item.enabled){Action("story-start",id:item.id);Close();}});button.interactable=item.enabled;}
                storyRows.Add(go);
            }
        }
        void TodoRows()
        {
            string signature=player.story.Workspace.listId+completedExpanded+string.Join("|",state.todos.Select(t=>t.id+":"+t.title+":"+t.done+":"+t.due+":"+t.completedDate));if(signature==todoSignature)return;todoSignature=signature;
            Clear(todoRows);var parent=RefOf("TodoListUI","_uncompleteTodoUIParent");if(!parent || !todoCell)return;Layout(parent,64);Set(RefOf("TodoListUI","dummyButton"),false);
            var completed=RefOf("TodoListUI","_completeTodoUIParent");if(completed)Layout(completed,64);
            var section=RefOf("TodoListUI","_completedSection") as RectTransform;
            if(section)
            {
                Set(section,true);section.sizeDelta=new Vector2(completedExpanded?364.2f:-3.3f,section.sizeDelta.y);
                var completeList=section.Find("CompleteList");
                Set(completeList,completedExpanded);
                if(completeList)Set(completeList.Find("CompleteTaskNotion"),!state.todos.Any(t=>t.done));
            }
            var orderedIndices=Enumerable.Range(0,state.todos.Length).Where(i=>!state.todos[i].done)
                .Concat(completedExpanded?Enumerable.Range(0,state.todos.Length).Where(i=>state.todos[i].done).OrderByDescending(i=>state.todos[i].completedDate??"")
                .Take(50):Enumerable.Empty<int>()).ToArray();
            foreach(int i in orderedIndices)
            {
                int index=i;var todo=state.todos[i];var go=Cell(todoCell,todo.done&&completed?completed:parent);var input=go.GetComponentInChildren<TMP_InputField>(true);
                if(input){input.richText=false;input.characterLimit=200;input.SetTextWithoutNotify(todo.title);input.readOnly=false;input.onEndEdit.RemoveAllListeners();input.onEndEdit.AddListener(value=>player.story.EditTodo(todo.id,value,todo.due,todo.completedDate));}
                foreach(var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if(t.name=="DateTime")
                    {
                        t.gameObject.SetActive(true);
                        string rawDate=todo.done?(todo.completedDate??""):(todo.due??"");
                        bool hasDate=!string.IsNullOrEmpty(rawDate);
                        var expireIcon=t.Find("ExpireIcon");
                        if(expireIcon)expireIcon.gameObject.SetActive(!todo.done&&!hasDate);
                        var label=t.GetComponentInChildren<TMP_Text>(true);
                        if(label)
                        {
                            if(DateTime.TryParse(rawDate,out var parsedDate))
                            {
                                label.text=parsedDate.ToString("MM/dd");
                                label.color=!todo.done&&parsedDate.Date<DateTime.Now.Date?new Color(1f,.35f,.35f,1f):Color.white;
                            }
                            else
                            {
                                label.text=hasDate?rawDate:"";
                                label.color=Color.white;
                            }
                            label.enableAutoSizing=true;label.fontSizeMin=9;label.fontSizeMax=16;
                        }
                        foreach(var extra in t.GetComponentsInChildren<TMP_Text>(true))if(extra!=label)extra.gameObject.SetActive(false);
                        Bind(t.gameObject,()=>OpenTodoMiniCalendar(todo));
                    }
                    if(t.name=="DragButton"){t.gameObject.SetActive(!todo.done);if(!todo.done){var drag=t.gameObject.GetComponent<ChillNativeRowDrag>()??t.gameObject.AddComponent<ChillNativeRowDrag>();drag.rowHeight=64f;drag.moved=step=>player.story.MoveTodo(index,step);}}
                    if(t.name=="BackCompleteImage"||t.name=="TodoSwitchFinishButtonImage")t.gameObject.SetActive(todo.done);
                    if(t.name=="TodoSwitchFinishButton")Bind(t.gameObject,()=>{if(todo.done||!string.IsNullOrWhiteSpace(todo.title))Action("todo-toggle",index);});if(t.name=="TodoRemoveButton")Bind(t.gameObject,()=>Action("todo-delete",index));
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
            Write("StorySystemUI","_heroineNameTextLocalizationBehavior",state.speaker);Write("StorySystemUI","_auteText","Auto");Write("StorySystemUI","_skipText","Skip");
            var autoCaption=Get<TMP_Text>("StorySystemUI","_auteText");if(autoCaption){autoCaption.color=state.autoRead?Color.white:new Color(.65f,.63f,.75f);autoCaption.enableWordWrapping=false;}
            // Source StorySystemUI uses NormalTextParent for greetings/reactions,
            // and MainStoryTextMessage only for main/after/DLC/extra/special stories.
            Set(RefOf("StorySystemUI","_mainStoryTextParent"),main);
            Show(RefOf("StorySystemUI","_normalTextParent")?.gameObject,state.playing && !main);
            var bottom=RefOf("StorySystemUI","_bottomBackImage") as RectTransform;if(bottom)bottom.sizeDelta=new Vector2(bottom.sizeDelta.x,main?308:180);
            var target=RefOf("StorySystemUI",main?"_mainStoryTextMessage":"_normalTextMessage");var label=target ? target.GetComponent<TMP_Text>() ?? target.GetComponentInChildren<TMP_Text>(true) : null;
            if(label){label.richText=true;label.text=state.text;}
            string signature=JsonUtility.ToJson(new ChillStoryUiState{choices=state.choices});if(signature==choiceSignature)return;choiceSignature=signature;Clear(choiceRows);
            var parent=RefOf("SentenceSelectionButtonsUI","self");if(!parent || !selectionCell)return;
            var selectionRect=parent as RectTransform;var prefabRect=selectionCell.transform as RectTransform;
            if(selectionRect&&prefabRect)selectionRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,prefabRect.rect.width);
            Layout(parent,prefabRect?prefabRect.rect.height:74);
            Show(parent.gameObject,state.playing && state.choices.Length>0);
            if(state.choices.Length>0)parent.SetAsLastSibling();
            for(int i=0;i<state.choices.Length;i++){int index=i;var choice=state.choices[i];var go=Cell(selectionCell,parent);var text=go.GetComponentInChildren<TMP_Text>(true);if(text){text.richText=false;text.text=choice.title;text.color=choice.enabled?Color.white:new Color32(77,72,72,81);}Bind(go,()=>Action("choice",index));go.GetComponent<Button>().interactable=choice.enabled;choiceRows.Add(go);}
        }
        static void SetText(GameObject go,string name,string value){var t=go.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name==name);if(t){t.richText=false;t.text=value;}}
        void Layout(Transform parent,float height)
        {
            rowHeights[parent]=height;
            var rt=parent as RectTransform;
            if(rt&&rt.parent&&rt.parent.name=="Viewport")
            {
                var csf=parent.GetComponent<ContentSizeFitter>();
                bool hFit=csf&&csf.horizontalFit!=ContentSizeFitter.FitMode.Unconstrained;
                rt.anchorMin=new Vector2(0f,1f);rt.anchorMax=new Vector2(hFit?0f:1f,1f);
                rt.pivot=new Vector2(0f,1f);rt.anchoredPosition=Vector2.zero;
                if(!hFit)rt.sizeDelta=new Vector2(0f,rt.sizeDelta.y);
            }
            var layout=parent.GetComponent<VerticalLayoutGroup>() ?? parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.enabled=true;
            if(PathOf(parent).Contains("UI_FacilityNote/SelectNotes"))layout.padding.left=5;
            var size=parent.GetComponent<ContentSizeFitter>() ?? parent.gameObject.AddComponent<ContentSizeFitter>();
            size.enabled=true;
            size.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        }
        GameObject Cell(GameObject prefab,Transform parent)
        {var go=Instantiate(prefab,parent,false);Show(go,true);FixSprites(go);CleanEvents(go);foreach(var a in go.GetComponentsInChildren<Animator>(true))a.enabled=false;return go;}
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
            foreach(string tab in new[]{"_generalInteractableUI","_graphicInteractableUI","_audioInteractableUI","_creditsInteractableUI"}){var button=RefOf("SettingUI",tab);var style=button?button.GetComponent<ChillNativeInteraction>():null;if(style)style.SetUsing(tab==key);}
            Set(RefOf("SettingUI","_generalParent"),key=="_generalInteractableUI");Set(RefOf("SettingUI","_graphicParent"),key=="_graphicInteractableUI");Set(RefOf("SettingUI","_audioParent"),key=="_audioInteractableUI");Set(RefOf("SettingUI","_creditsParent"),key=="_creditsInteractableUI");
            string parentKey=key=="_generalInteractableUI"?"_generalParent":key=="_graphicInteractableUI"?"_graphicParent":key=="_audioInteractableUI"?"_audioParent":"_creditsParent";
            var parent=RefOf("SettingUI",parentKey);
            var scroll=parent?parent.GetComponentInChildren<ScrollRect>(true):null;
            if(scroll)
            {
                if(scroll.content)LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition=1f;
                if(scroll.content)scroll.content.anchoredPosition=new Vector2(scroll.content.anchoredPosition.x,0f);
            }
        }
        void OnDisable(){if(initialized)ChillGameplayUI.UseNativeUI(false);}
    }
}
