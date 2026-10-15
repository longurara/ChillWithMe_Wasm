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
        const string EnvRootPath="Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilityEnviroment";
        readonly Dictionary<string,Button> featureButtons=new Dictionary<string,Button>();
        readonly List<Tuple<Slider,Func<float>>> featureSliders=new List<Tuple<Slider,Func<float>>>();
        readonly Dictionary<int,Slider> envVolumeSliders=new Dictionary<int,Slider>();
        GameObject ambientPanel;
        Button playlistSelector;
        TMP_InputField playlistName,timeBoundariesInput;
        Coroutine creditsRoutine;
        Transform creditsParent;
        bool featureDataControls,seekDragging;
        int previousWidth,previousHeight;
        bool firstDayOpen,resolutionOpen,qualityOpen,tagListOpen,autoTimeChangerOpen;
        bool alwaysOnTopUi,fullscreenModeUi=true,vsyncUi=true;
        string selectedResolutionText="1920 × 1080";
        int musicTagMask;
        RectTransform firstDayDropdown,resolutionDropdown,qualityDropdown;
        Transform firstDayOptions,resolutionOptions,qualityOptions,tagListRoot;
        float firstDayClosedHeight=60.9f,resolutionClosedHeight=60.9f,qualityClosedHeight=60.9f;

        Button FeatureButton(Transform parent,string key,string caption,float y,float width=470,float x=0)=>null;
        void FeatureCaption(string key,string text){}
        TMP_InputField FeatureInput(Transform parent,string key,string text,float y,float width=470,float x=0)=>null;
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
        void SetFirstDayOpen(bool value)
        {
            firstDayOpen=value;Set(firstDayOptions,value);
            if(firstDayDropdown)firstDayDropdown.sizeDelta=new Vector2(firstDayDropdown.sizeDelta.x,firstDayClosedHeight+(value?420f:0f));
        }
        void SetResolutionOpen(bool value)
        {
            resolutionOpen=value;Set(resolutionOptions,value);
            if(resolutionDropdown)resolutionDropdown.sizeDelta=new Vector2(resolutionDropdown.sizeDelta.x,resolutionClosedHeight+(value?180f:0f));
        }
        void SetQualityOpen(bool value)
        {
            qualityOpen=value;Set(qualityOptions,value);
            if(qualityDropdown)qualityDropdown.sizeDelta=new Vector2(qualityDropdown.sizeDelta.x,qualityClosedHeight+(value?180f:0f));
        }
        public void SetTagListOpen(bool value)
        {
            tagListOpen=value;
            if(tagListRoot)Set(tagListRoot,value);
        }
        public void CloseEnvironmentPopovers()
        {
            ClosePurchasePopover();
            autoTimeChangerOpen=false;
            Set(Find(EnvRootPath+"/AutoTimeChanger"),false);
            Set(Find(EnvRootPath+"/EnviromentOther/WeatherChangeButtons/AutoTimeChanger"),false);
            CloseLayoutPresetConfirm(Find(EnvRootPath+"/LayoutPresetUI"));
        }
        void SetCardAmbience(int ambientId,float level)
        {
            if(ambientId<0||!player.story.FeatureReady)return;
            player.story.SetAmbience(ambientId,level);
            if(ambientId==5)player.story.SetAmbience(6,level);
        }
        bool IsEnvironmentChoiceKnown(EnvironmentChoice choice)
        {
            var story=player.story;
            if(!story.FeatureReady||string.IsNullOrEmpty(choice.itemName))return true;
            if(story.ItemAvailable("environment",choice.itemName))return true;
            var rule=story.UnlockRule("environment",choice.itemName);
            return rule!=null&&rule.condition=="PointPurchase";
        }
        bool IsEnvironmentChoiceUnlocked(EnvironmentChoice choice)
        {
            var story=player.story;
            if(!story.FeatureReady||string.IsNullOrEmpty(choice.itemName))return true;
            return story.ItemAvailable("environment",choice.itemName);
        }
        bool IsEnvironmentChoicePurchasable(EnvironmentChoice choice)
        {
            var story=player.story;
            if(!story.FeatureReady||string.IsNullOrEmpty(choice.itemName))return false;
            var rule=story.UnlockRule("environment",choice.itemName);
            return rule!=null&&rule.condition=="PointPurchase"&&!story.ItemAvailable("environment",choice.itemName);
        }
        void InitializeEnvironmentCards()
        {
            envVolumeSliders.Clear();
            foreach(var choice in environmentChoices ?? Array.Empty<EnvironmentChoice>())
            {
                if(!choice.root)continue;
                var c=choice;
                Show(c.root,true);
                var nameText=(c.root.transform.Find("EnviromentNameText (TMP)")??c.root.transform.Find("NameText"))?.GetComponent<TMP_Text>();
                if(nameText&&string.IsNullOrEmpty(c.localizeKey)&&nameText.text.StartsWith("ui_"))c.localizeKey=nameText.text;
                if(nameText&&!string.IsNullOrEmpty(c.localizeKey))localized[nameText]=c.localizeKey;
                bool hasView=c.index>0,hasSound=c.ambientId>=0;
                if(c.main)
                {
                    Bind(c.main.gameObject,()=>{
                        if(IsEnvironmentChoicePurchasable(c)){ShowEnvironmentPurchasePopover(c);return;}
                        if(!IsEnvironmentChoiceUnlocked(c))return;
                        ClosePurchasePopover();
                        if(hasView&&hasSound)
                        {
                            bool viewOn=player.environment.selectedEnvironment==c.index;
                            bool soundOn=player.story.FeatureReady&&player.story.AmbientLevel(c.ambientId)>0.001f;
                            if(viewOn||soundOn)
                            {
                                if(viewOn)Action("window-mode",0);
                                if(soundOn)SetCardAmbience(c.ambientId,0f);
                            }
                            else
                            {
                                Action("window-mode",c.index);
                                SetCardAmbience(c.ambientId,0.5f);
                            }
                        }
                        else if(hasView)
                        {
                            Action("window-mode",player.environment.selectedEnvironment==c.index?0:c.index);
                        }
                        else if(hasSound)
                        {
                            float cur=player.story.FeatureReady?player.story.AmbientLevel(c.ambientId):0f;
                            SetCardAmbience(c.ambientId,cur>0.001f?0f:0.5f);
                        }
                    });
                }
                if(c.view)
                {
                    Bind(c.view.gameObject,()=>{
                        if(IsEnvironmentChoicePurchasable(c)){ShowEnvironmentPurchasePopover(c);return;}
                        if(!IsEnvironmentChoiceUnlocked(c))return;
                        ClosePurchasePopover();
                        if(hasView)Action("window-mode",player.environment.selectedEnvironment==c.index?0:c.index);
                    });
                }
                var shopBtn=c.root.transform.Find("ShopButton");
                if(shopBtn)Bind(shopBtn.gameObject,()=>ShowEnvironmentPurchasePopover(c));
                var volRoot=c.root.transform.Find("AmbientSoundVolume");
                if(volRoot)
                {
                    volRoot.gameObject.SetActive(hasSound);
                    var slider=volRoot.GetComponentInChildren<Slider>(true);
                    if(slider&&hasSound)
                    {
                        slider.minValue=0f;slider.maxValue=1f;slider.wholeNumbers=false;slider.interactable=true;
                        slider.onValueChanged.RemoveAllListeners();
                        int ambId=c.ambientId;
                        slider.onValueChanged.AddListener(val=>SetCardAmbience(ambId,val));
                        envVolumeSliders[ambId]=slider;
                    }
                }
            }
            var envRoot=Find(EnvRootPath);
            if(envRoot)
            {
                WireLayoutPresetUI(envRoot.Find("LayoutPresetUI"));
                var popClose=envRoot.Find("PurchasePopover/CloseButtonBase/CloseButtonUI");
                if(popClose)Bind(popClose.gameObject,ClosePurchasePopover);
                var popBuy=envRoot.Find("PurchasePopover/PurchaseButton");
                if(popBuy)Bind(popBuy.gameObject,ClosePurchasePopover);
                Set(envRoot.Find("PurchasePopover"),false);
                var weatherRoot=envRoot.Find("EnviromentOther/WeatherChangeButtons");
                var autoChanger=envRoot.Find("AutoTimeChanger")??weatherRoot?.Find("AutoTimeChanger");
                Set(autoChanger,false);
                if(weatherRoot)
                {
                    var autoBtn=weatherRoot.Find("AutoButton");
                    if(autoBtn)Bind(autoBtn.gameObject,()=>{player.story.Settings.autoTime=!player.story.Settings.autoTime;player.story.CommitSettings();});
                    var autoSetBtn=weatherRoot.Find("AutoSettingButton");
                    if(autoSetBtn&&autoChanger)
                    {
                        Bind(autoSetBtn.gameObject,()=>{
                            autoTimeChangerOpen=!autoTimeChangerOpen;
                            Show(autoChanger.gameObject,autoTimeChangerOpen);
                            if(autoTimeChangerOpen)autoChanger.SetAsLastSibling();
                        });
                    }
                }
                if(autoChanger)InitializeAutoTimeChanger(autoChanger);
            }
        }
        void InitializeAutoTimeChanger(Transform autoChanger)
        {
            var initBtn=autoChanger.Find("AutoTimeChangeInitButton");
            if(initBtn)Bind(initBtn.gameObject,()=>{
                var s=player.story.Settings;s.dayStart=6f;s.sunsetStart=16f;s.nightStart=19f;
                player.story.CommitSettings();
            });
            var pinsRoot=autoChanger.Find("TimePins");
            var gaugeRect=autoChanger.Find("TimeZoneGauge") as RectTransform;
            if(gaugeRect)
            {
                var hlg=gaugeRect.GetComponent<HorizontalOrVerticalLayoutGroup>();if(hlg)hlg.enabled=false;
                var csf=gaugeRect.GetComponent<ContentSizeFitter>();if(csf)csf.enabled=false;
            }
            if(pinsRoot&&gaugeRect)
            {
                int count=Mathf.Min(3,pinsRoot.childCount);
                for(int i=0;i<count;i++)
                {
                    int pinIndex=i;
                    var pin=pinsRoot.GetChild(i);if(!pin)continue;
                    var handle=pin.Find("InteractableUI/PinCollider")??pin.Find("DragArea")??pin;
                    var graphic=handle.GetComponent<Graphic>();if(graphic)graphic.raycastTarget=true;
                    var trigger=handle.GetComponent<EventTrigger>()??handle.gameObject.AddComponent<EventTrigger>();
                    trigger.triggers.Clear();
                    var dragEntry=new EventTrigger.Entry{eventID=EventTriggerType.Drag};
                    dragEntry.callback.AddListener(data=>{
                        var ped=data as PointerEventData;if(ped==null)return;
                        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(gaugeRect,ped.position,ped.pressEventCamera,out var local))return;
                        float width=Mathf.Max(1f,gaugeRect.rect.width);
                        float normalized=Mathf.Clamp01((local.x+width*gaugeRect.pivot.x)/width);
                        int hour=Mathf.Clamp(Mathf.RoundToInt(normalized*23f),0,23);
                        var s=player.story.Settings;
                        if(pinIndex==0)s.dayStart=Mathf.Clamp(hour,1,Mathf.Max(1,(int)s.sunsetStart-1));
                        else if(pinIndex==1)s.sunsetStart=Mathf.Clamp(hour,(int)s.dayStart+1,Mathf.Max((int)s.dayStart+1,(int)s.nightStart-1));
                        else s.nightStart=Mathf.Clamp(hour,(int)s.sunsetStart+1,23);
                        player.story.CommitSettings();
                    });
                    trigger.triggers.Add(dragEntry);
                }
            }
        }
        void RefreshAutoTimeChanger()
        {
            var envRoot=Find(EnvRootPath);if(!envRoot)return;
            var weatherRoot=envRoot.Find("EnviromentOther/WeatherChangeButtons");if(!weatherRoot)return;
            var s=player.story.Settings;
            var autoBtn=weatherRoot.Find("AutoButton");
            autoBtn?.GetComponent<ChillNativeInteraction>()?.SetUsing(s.autoTime);
            var autoSetBtn=weatherRoot.Find("AutoSettingButton");
            autoSetBtn?.GetComponent<ChillNativeInteraction>()?.SetUsing(autoTimeChangerOpen);
            var autoChanger=envRoot.Find("AutoTimeChanger")??weatherRoot.Find("AutoTimeChanger");
            if(!autoChanger||!autoChanger.gameObject.activeInHierarchy)return;
            var gaugeRect=autoChanger.Find("TimeZoneGauge") as RectTransform;if(!gaugeRect)return;
            float width=gaugeRect.rect.width>10f?gaugeRect.rect.width:464.6f;
            float height=gaugeRect.rect.height>4f?gaugeRect.rect.height:36f;
            float fDay=Mathf.Clamp01(s.dayStart/24f),fSunset=Mathf.Clamp01(s.sunsetStart/24f),fNight=Mathf.Clamp01(s.nightStart/24f);
            void SetBar(Transform barTr,float start01,float end01)
            {
                var bar=barTr as RectTransform;if(!bar)return;
                bar.anchorMin=bar.anchorMax=new Vector2(0f,1f);bar.pivot=new Vector2(0f,0f);
                bar.anchoredPosition=new Vector2(start01*width,-height);
                bar.sizeDelta=new Vector2(Mathf.Max(0f,(end01-start01)*width),height);
            }
            SetBar(gaugeRect.Find("NightBar (3)")??gaugeRect.Find("NightGauge1"),0f,fDay);
            SetBar(gaugeRect.Find("DayBar")??gaugeRect.Find("DayGauge"),fDay,fSunset);
            SetBar(gaugeRect.Find("SunsetBar")??gaugeRect.Find("SunsetGauge"),fSunset,fNight);
            SetBar(gaugeRect.Find("NightBar (4)")??gaugeRect.Find("NightGauge2"),fNight,1f);
            var pinsRoot=autoChanger.Find("TimePins");
            if(pinsRoot)
            {
                float[] hours={s.dayStart,s.sunsetStart,s.nightStart};
                for(int i=0;i<Mathf.Min(3,pinsRoot.childCount);i++)
                {
                    var pin=pinsRoot.GetChild(i) as RectTransform;if(!pin)continue;
                    float x=Mathf.Clamp01(hours[i]/24f)*width-width*0.5f+61f;
                    pin.anchoredPosition=new Vector2(x,pin.anchoredPosition.y);
                    var label=pin.GetComponentInChildren<TMP_Text>(true);
                    if(label)label.text=Mathf.RoundToInt(hours[i]).ToString();
                }
            }
        }
        public void ShowEnvironmentPurchasePopover(EnvironmentChoice choice)
        {
            var popover=Find(EnvRootPath+"/PurchasePopover") as RectTransform;if(!popover||choice?.root==null)return;
            var story=player.story;
            var rule=story.UnlockRule("environment",choice.itemName);
            int price=rule!=null&&int.TryParse(rule.argument,out int p)?p:0;
            bool canPurchase=story.Workspace.points>=price;
            var priceText=popover.Find("PurchaseButton/PriceText")?.GetComponent<TMP_Text>();
            if(priceText){priceText.text=price.ToString();priceText.color=canPurchase?Color.white:(Color)new Color32(255,99,99,255);}
            var itemImage=popover.Find("MainImageFrame/MainImage")?.GetComponent<Image>();
            var thumb=choice.shopThumbnail??choice.main?.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.sprite&&i.name!="Active"&&i.transform!=choice.main.transform)?.sprite;
            if(itemImage&&thumb)itemImage.sprite=RecoveredSprite(thumb);
            Set(popover.Find("MainImageFrame/PurchaseCompletedAnimationImage"),false);
            activePurchasePopover=popover;
            purchasePopoverTarget=choice.root.transform;
            purchasePopoverOffset=new Vector2(0f,-72f);
            purchasePopoverScrollRect=Find(EnvRootPath+"/EnviromentScrollView")?.GetComponent<ScrollRect>();
            purchasePopoverOpenedFrame=Time.frameCount;
            float scaleY=(float)Screen.height/1080f;
            popover.position=choice.root.transform.position+new Vector3(0f,-72f*scaleY,0f);
            Show(popover.gameObject,true);popover.SetAsLastSibling();
            var purchaseBtn=popover.Find("PurchaseButton");
            var btnCg=purchaseBtn?.GetComponent<CanvasGroup>();
            if(btnCg){btnCg.alpha=canPurchase?1f:.5f;btnCg.interactable=btnCg.blocksRaycasts=canPurchase;}
            if(purchaseBtn)
            {
                Bind(purchaseBtn.gameObject,()=>{
                    if(!story.Purchase("environment",choice.itemName))return;
                    ClosePurchasePopover();
                    if(choice.index>0)Action("window-mode",choice.index);
                    if(choice.ambientId>=0)SetCardAmbience(choice.ambientId,0.5f);
                    RefreshEnvironmentCards(true);
                });
                purchaseBtn.GetComponent<Button>().interactable=canPurchase;
            }
            var closeBtn=popover.Find("CloseButtonBase/CloseButtonUI");
            if(closeBtn)Bind(closeBtn.gameObject,ClosePurchasePopover);
        }
        public void RefreshEnvironmentCards(bool available)
        {
            var story=player.story;
            var envPoint=RefOf("EnvironmentUI","_ownPointView")?.GetComponentInChildren<TMP_Text>(true)
                ??Find(EnvRootPath+"/PlayerPointUI")?.GetComponentInChildren<TMP_Text>(true);
            if(envPoint&&story.FeatureReady)envPoint.text=story.Workspace.points.ToString();
            RefreshLayoutPresetUI(Find(EnvRootPath+"/LayoutPresetUI"));
            RefreshAutoTimeChanger();
            foreach(var choice in environmentChoices ?? Array.Empty<EnvironmentChoice>())
            {
                if(!choice.root)continue;
                bool unlocked=IsEnvironmentChoiceUnlocked(choice);
                bool purchasable=IsEnvironmentChoicePurchasable(choice);
                var lockImg=choice.root.transform.Find("EnvironmentLockImage")
                    ??choice.root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name.Contains("LockImage"));
                if(lockImg)Set(lockImg,!unlocked&&!purchasable);
                var shopBtn=choice.root.transform.Find("ShopButton");
                if(shopBtn)
                {
                    Set(shopBtn,purchasable);
                    var shopCg=shopBtn.GetComponent<CanvasGroup>();
                    if(shopCg){shopCg.alpha=purchasable?1f:0f;shopCg.interactable=shopCg.blocksRaycasts=purchasable&&available;}
                }
                var nameText=(choice.root.transform.Find("EnviromentNameText (TMP)")??choice.root.transform.Find("NameText"))?.GetComponent<TMP_Text>();
                if(nameText&&story.FeatureReady)
                {
                    localized.Remove(nameText);
                    string key=unlocked&&!string.IsNullOrEmpty(choice.localizeKey)?choice.localizeKey:"ui_lock_title";
                    nameText.text=story.UITranslate(key);
                    nameText.color=unlocked?Color.white:(Color)new Color32(128,128,128,255);
                }
                bool viewActive=choice.index>0&&player.environment.selectedEnvironment==choice.index;
                float soundLevel=choice.ambientId>=0&&story.FeatureReady?story.AmbientLevel(choice.ambientId):0f;
                bool soundActive=choice.ambientId>=0&&soundLevel>0.001f;
                bool mainActive=choice.index>0&&choice.ambientId>=0?(viewActive||soundActive):choice.index>0?viewActive:soundActive;
                if(choice.main)
                {
                    Set(choice.main.transform,!purchasable);
                    choice.main.interactable=available&&unlocked;
                    var mainCg=choice.main.GetComponent<CanvasGroup>();
                    if(mainCg){mainCg.alpha=1f;mainCg.blocksRaycasts=unlocked;}
                    var backImg=choice.main.transform.Find("BackImage")?.GetComponent<Image>();
                    if(backImg)backImg.color=unlocked?Color.white:(Color)new Color32(128,128,128,128);
                    var inter=choice.main.GetComponent<ChillNativeInteraction>();
                    if(inter)inter.SetUsing(unlocked&&mainActive);
                    else Highlight(choice.main,unlocked&&mainActive);
                }
                if(choice.view)
                {
                    Set(choice.view.transform,unlocked&&choice.index>0);
                    choice.view.interactable=available&&unlocked;
                    var inter=choice.view.GetComponent<ChillNativeInteraction>();
                    if(inter)inter.SetUsing(unlocked&&viewActive);
                    else Highlight(choice.view,unlocked&&viewActive);
                }
                var volRoot=choice.root.transform.Find("AmbientSoundVolume");
                if(volRoot)
                {
                    bool showVol=choice.ambientId>=0&&unlocked;
                    Set(volRoot,showVol);
                    if(showVol&&envVolumeSliders.TryGetValue(choice.ambientId,out var slider)&&slider)
                    {
                        slider.interactable=available;
                        slider.SetValueWithoutNotify(soundLevel);
                    }
                }
            }
            var envContent=Find(EnvRootPath+"/EnviromentScrollView/Viewport/Content") as RectTransform;
            if(envContent)
            {
                foreach(string gName in new[]{"GridLayout","GridLayout (1)","GridLayout (2)"})
                {
                    var gr=envContent.Find(gName) as RectTransform;
                    var glg=gr?gr.GetComponent<GridLayoutGroup>():null;
                    if(!gr||!glg)continue;
                    int activeCount=0;
                    for(int i=0;i<gr.childCount;i++)if(gr.GetChild(i).gameObject.activeSelf)activeCount++;
                    int cols=Mathf.Max(1,glg.constraintCount);
                    int rows=(activeCount+cols-1)/cols;
                    gr.sizeDelta=new Vector2(112f,rows*(glg.cellSize.y+glg.spacing.y));
                }
            }
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
            if(tagRoot)
            {
                tagListRoot=tagRoot.Find("TagList");
                Set(tagListRoot,false);
                var tagNameBtn=tagRoot.Find("TagName")??tagRoot;
                Bind(tagNameBtn.gameObject,()=>SetTagListOpen(!tagListOpen));
                if(tagListRoot)
                {
                    string[] cellNames={"TagCell","TagCell (1)","TagCell (2)","TagCell (3)","TagCell (4)"};
                    for(int i=0;i<cellNames.Length;i++)
                    {
                        int bit=1<<i;
                        var cell=tagListRoot.Find(cellNames[i]);if(!cell)continue;
                        Bind(cell.gameObject,()=>{
                            musicTagMask^=bit;
                            musicSignature=null;
                            RefreshMusicTagCheckboxes();
                        });
                    }
                    RefreshMusicTagCheckboxes();
                }
            }
            InitializeNativeGeneralSettings();
            InitializeNativeGraphicsSettings();
            Wire("SettingUI","_creditsInteractableUI",()=>SettingsTab("_creditsInteractableUI"));Set(RefOf("SettingUI","_creditsInteractableUI"),true);
            Wire("SettingUI","_tutorialHelpButton",()=>{Close();story.Tutorial();});
            FeatureSlider(RefOf("SettingUI","_masterVolumeChangeUI"),()=>story.Settings.master,v=>story.Settings.master=v);
            FeatureSlider(RefOf("SettingUI","_systemSEVolumeChangeUI"),()=>story.Settings.sfx,v=>story.Settings.sfx=v);
            FeatureSlider(RefOf("SettingUI","_ambientBGMVolumeChangeUI"),()=>story.Settings.ambient,v=>story.Settings.ambient=v);
            FeatureSlider(RefOf("SettingUI","_timerSEVolumeChangeUI"),()=>story.Settings.timer,v=>story.Settings.timer=v);
            Wire("SettingUI","_audioInitButton",()=>{var s=story.Settings;s.master=s.voice=s.sfx=s.ambient=s.timer=1;s.music=.35f;story.CommitSettings();});
        }
        void InitializeNativeGeneralSettings()
        {
            var story=player.story;
            Wire("SettingUI","_timeFormatAMPMInteractableUI",()=>{story.Settings.clock24=false;story.CommitSettings();});
            Wire("SettingUI","_timeFormatAllInteractableUI",()=>{story.Settings.clock24=true;story.CommitSettings();});
            Wire("SettingUI","_windowTpAlwaysActivateInteractableUI",()=>{alwaysOnTopUi=true;});
            Wire("SettingUI","_windowTpAlwaysDeactivateInteractableUI",()=>{alwaysOnTopUi=false;});
            var firstDayRoot=RefOf("SettingUI","_firstdayPulldownList");
            if(firstDayRoot)
            {
                firstDayDropdown=firstDayRoot.Find("Pulldown") as RectTransform;
                firstDayOptions=firstDayDropdown?firstDayDropdown.Find("TodoGroupText (TMP)/Content"):null;
                if(firstDayDropdown&&firstDayDropdown.sizeDelta.y>10f)firstDayClosedHeight=firstDayDropdown.sizeDelta.y;
                var toggle=firstDayRoot.Find("PulldownButton");
                if(toggle)Bind(toggle.gameObject,()=>SetFirstDayOpen(!firstDayOpen));
                string[] dayKeys={"_firstDaySunDayInteractableUI","_firstDayMondayInteractableUI","_firstDayTuesdayInteractableUI","_firstDayWednesdayInteractableUI","_firstDayThursdayInteractableUI","_firstDayFridayInteractableUI","_firstDaySaturdayInteractableUI"};
                for(int i=0;i<dayKeys.Length;i++)
                {
                    int day=i;
                    Wire("SettingUI",dayKeys[i],()=>{
                        story.Settings.firstDayInWeek=day;
                        story.CommitSettings();
                        workspaceSignature=null;
                        SetFirstDayOpen(false);
                    });
                }
                SetFirstDayOpen(false);
            }
            Wire("SettingUI","_faqButton",()=>Application.OpenURL("https://nestopi.co.jp/en/chill-with-you/faq/"));
            Wire("SettingUI","_generalInitButton",()=>{
                story.Settings.clock24=false;
                story.Settings.firstDayInWeek=0;
                alwaysOnTopUi=false;
                story.CommitSettings();
                workspaceSignature=null;
            });
            var general=Find(SettingsContent);
            if(general)
            {
                foreach(var pair in new[]{("WebExportSaveButton","Export web save"),("WebImportSaveButton","Import web save")})
                {
                    var row=general.Find(pair.Item1);if(!row)continue;
                    foreach(var t in row.GetComponentsInChildren<TMP_Text>(true))PortCaption(t,pair.Item2);
                }
            }
        }
        void InitializeNativeGraphicsSettings()
        {
            var story=player.story;
            Wire("SettingUI","_borderlessFullscreenInteractableUI",()=>{fullscreenModeUi=true;ChillPersonalMusic.Fullscreen();});
            Wire("SettingUI","_windowedInteractableUI",()=>{fullscreenModeUi=false;ChillPersonalMusic.Fullscreen();});
            var resRoot=RefOf("SettingUI","_windowResolutionPulldownList");
            if(resRoot)
            {
                resolutionDropdown=resRoot.Find("Pulldown") as RectTransform;
                resolutionOptions=resolutionDropdown?resolutionDropdown.Find("CurrentSelectText (TMP)/Content"):null;
                if(resolutionDropdown&&resolutionDropdown.sizeDelta.y>10f)resolutionClosedHeight=resolutionDropdown.sizeDelta.y;
                var toggle=resRoot.Find("PulldownButton");
                if(toggle)Bind(toggle.gameObject,()=>SetResolutionOpen(!resolutionOpen));
                if(resolutionOptions)
                {
                    foreach(Transform child in resolutionOptions)
                    {
                        var label=child.GetComponentInChildren<TMP_Text>(true);
                        string caption=label?label.text:"1920 × 1080";
                        Bind(child.gameObject,()=>{selectedResolutionText=caption;SetResolutionOpen(false);});
                    }
                }
                SetResolutionOpen(false);
            }
            var qualRoot=RefOf("SettingUI","_graphicQualityPulldownList");
            if(qualRoot)
            {
                qualityDropdown=qualRoot.Find("Pulldown") as RectTransform;
                qualityOptions=qualityDropdown?qualityDropdown.Find("CurrentSelectText (TMP)/Content"):null;
                if(qualityDropdown&&qualityDropdown.sizeDelta.y>10f)qualityClosedHeight=qualityDropdown.sizeDelta.y;
                var toggle=qualRoot.Find("PulldownButton");
                if(toggle)Bind(toggle.gameObject,()=>SetQualityOpen(!qualityOpen));
                if(qualityOptions)
                {
                    int idx=0;
                    foreach(Transform child in qualityOptions)
                    {
                        int q=idx==0?2:idx==1?1:0;
                        idx++;
                        Bind(child.gameObject,()=>{story.Settings.quality=q;story.CommitSettings();SetQualityOpen(false);});
                    }
                }
                SetQualityOpen(false);
            }
            var renderSlider=Get<Slider>("SettingUI","_renderScaleSlider");
            if(renderSlider)
            {
                renderSlider.minValue=35f;renderSlider.maxValue=100f;renderSlider.wholeNumbers=true;renderSlider.interactable=true;
                renderSlider.onValueChanged.RemoveAllListeners();
                renderSlider.onValueChanged.AddListener(val=>{
                    story.Settings.renderScale=Mathf.Clamp(val/100f,0.35f,1f);
                    story.CommitSettings();
                });
            }
            Wire("SettingUI","_verticalSyncActivateInteractableUI",()=>{vsyncUi=true;QualitySettings.vSyncCount=1;});
            Wire("SettingUI","_verticalSyncDeactivateInteractableUI",()=>{vsyncUi=false;QualitySettings.vSyncCount=0;});
            var activeFps=Get<TMP_InputField>("SettingUI","_activeFrameRateInputField");
            if(activeFps)
            {
                activeFps.readOnly=false;activeFps.contentType=TMP_InputField.ContentType.IntegerNumber;
                activeFps.onEndEdit.RemoveAllListeners();
                activeFps.onEndEdit.AddListener(val=>{
                    if(int.TryParse(val,out int n))
                    {
                        story.Settings.activeFps=Mathf.Clamp(n,15,120);
                        player.SetLowQuality(story.Settings.activeFps<=30);
                        story.CommitSettings();
                    }
                });
            }
            var deactiveFps=Get<TMP_InputField>("SettingUI","_deactiveFrameRateInputField");
            if(deactiveFps)
            {
                deactiveFps.readOnly=false;deactiveFps.contentType=TMP_InputField.ContentType.IntegerNumber;
                deactiveFps.onEndEdit.RemoveAllListeners();
                deactiveFps.onEndEdit.AddListener(val=>{
                    if(int.TryParse(val,out int n))
                    {
                        story.Settings.inactiveFps=Mathf.Clamp(n,5,60);
                        story.CommitSettings();
                    }
                });
            }
            Wire("SettingUI","_graphicInitButton",()=>{
                var s=story.Settings;
                s.quality=1;s.activeFps=60;s.inactiveFps=15;s.renderScale=1f;vsyncUi=true;
                story.CommitSettings();
            });
        }
        void RefreshMusicTagCheckboxes()
        {
            if(!tagListRoot)return;
            string[] cellNames={"TagCell","TagCell (1)","TagCell (2)","TagCell (3)","TagCell (4)"};
            string[] tagKeys={"ui_music_tag_original","ui_music_tag_special","ui_music_tag_other","ui_music_tag_local","ui_music_tag_favorite"};
            int activeCount=0,lastIdx=0;
            for(int i=0;i<cellNames.Length;i++)
            {
                bool on=(musicTagMask&(1<<i))!=0;
                if(on){activeCount++;lastIdx=i;}
                var check=tagListRoot.Find(cellNames[i]+"/CheckBox/Check");
                if(check)Set(check,on);
            }
            var tagTitle=tagListRoot.parent?.Find("TagName/Text (TMP)")?.GetComponent<TMP_Text>()
                ??tagListRoot.parent?.Find("TagName")?.GetComponentInChildren<TMP_Text>(true);
            if(tagTitle&&player&&player.story)
            {
                string key=activeCount==0?"ui_music_tag_all":activeCount==1?tagKeys[lastIdx]:"ui_music_tag_select_tag";
                tagTitle.text=player.story.UITranslate(key);
            }
        }
        bool TrackMatchesTagMask(ChillTrack item)
        {
            if(musicTagMask==0||item==null)return true;
            var story=player.story;
            int itemMask=0;
            if(item.personal)itemMask|=1<<3;
            else if(item.tag>0)
            {
                if((item.tag&1)!=0)itemMask|=1<<0;
                if((item.tag&2)!=0)itemMask|=1<<1;
                if((item.tag&4)!=0)itemMask|=1<<2;
            }
            else if(item.artist!=null&&item.artist.IndexOf(" Sound",StringComparison.OrdinalIgnoreCase)>=0)itemMask|=1<<1;
            else if(item.artist!=null&&item.artist.IndexOf("Satoshi",StringComparison.OrdinalIgnoreCase)<0)itemMask|=1<<2;
            else itemMask|=1<<0;
            if(story.MusicState.favorites.Contains(item.guid))itemMask|=1<<4;
            return (musicTagMask&itemMask)!=0;
        }
        static string LanguageCaption(string language)=>language=="Ja"?"日本語":language=="ZhHans"?"简体中文":language=="ZhHant"?"繁體中文":language=="Pt"?"Português":language=="Ko"?"한국어":language=="Ru"?"Русский":"English";
        public IEnumerator FocusTutorial(int command)
        {
            SetTutorialHudVisible(true);
            string key=command==16?"_pomodoroFocusUI":command==17?"_todoFocusUI":"_noteFocusUI";var focus=RefOf("TutorialView",key);
            Set(focus,true);if(focus){var group=focus.GetComponent<CanvasGroup>();if(group)group.alpha=1;}
            var setup=player.story.Features.tutorialFocus?.FirstOrDefault(t=>focus&&t.path==PathOf(focus));
            var image=setup!=null?Find(setup.image):null;var mask=setup!=null?Find(setup.mask):null;
            if(image)Set(image,true);if(mask)Set(mask,true);
            Vector3 initial=image?image.localScale:Vector3.one;Quaternion rotation=image?image.localRotation:Quaternion.identity;
            float elapsed=0;while(elapsed<3.5f&&player.story.Playing){elapsed+=Time.unscaledDeltaTime;if(image){image.localRotation=Quaternion.Euler(0,0,-360*elapsed/(command==16?1.3f:1));image.localScale=initial*(1+.1f*Mathf.PingPong(elapsed,1));}yield return null;}
            if(image){image.localRotation=rotation;image.localScale=initial;Set(image,false);}if(mask)Set(mask,false);Set(focus,false);SetTutorialHudVisible(false);
        }
        public void EndTutorialPresentation()
        {
            tutorialHudVisible=false;CloseTutorial();
            foreach(var row in player.story.Features?.tutorialFocus??Array.Empty<ChillTutorialFocus>()){Set(Find(row.path),false);Set(Find(row.image),false);Set(Find(row.mask),false);}
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
            RefreshPortLanguage();
            var story=player.story;
            Write("MusicUI","_musicTitleText",story.MusicLoading?"Loading…":story.CurrentTrack?.title ?? (player.music.clip?player.music.clip.name:"Select music"));
            Write("MusicUI","_artistNameText",story.CurrentTrack?.artist??"");
            if(!story.FeatureReady)return;
            var s=story.Settings;
            foreach(var item in featureSliders)if(item.Item1){item.Item1.SetValueWithoutNotify(item.Item2());var label=item.Item1.transform.parent.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name.Contains("Current"));if(label)label.text=Mathf.RoundToInt(item.Item2()*100).ToString();}
            Get<ChillNativeInteraction>("SettingUI","_timeFormatAMPMInteractableUI")?.SetUsing(!s.clock24);
            Get<ChillNativeInteraction>("SettingUI","_timeFormatAllInteractableUI")?.SetUsing(s.clock24);
            Get<ChillNativeInteraction>("SettingUI","_windowTpAlwaysActivateInteractableUI")?.SetUsing(alwaysOnTopUi);
            Get<ChillNativeInteraction>("SettingUI","_windowTpAlwaysDeactivateInteractableUI")?.SetUsing(!alwaysOnTopUi);
            var firstDayText=firstDayDropdown?firstDayDropdown.Find("TodoGroupText (TMP)")?.GetComponent<TMP_Text>():null;
            if(firstDayText)
            {
                string[] dayLocKeys={"ui_calendar_sunday","ui_calendar_monday","ui_calendar_tuesday","ui_calendar_wednesday","ui_calendar_thursday","ui_calendar_friday","ui_calendar_saturday"};
                firstDayText.text=story.UITranslate(dayLocKeys[Mathf.Clamp(s.firstDayInWeek,0,6)]);
            }
            Get<ChillNativeInteraction>("SettingUI","_borderlessFullscreenInteractableUI")?.SetUsing(fullscreenModeUi);
            Get<ChillNativeInteraction>("SettingUI","_windowedInteractableUI")?.SetUsing(!fullscreenModeUi);
            Get<ChillNativeInteraction>("SettingUI","_verticalSyncActivateInteractableUI")?.SetUsing(vsyncUi);
            Get<ChillNativeInteraction>("SettingUI","_verticalSyncDeactivateInteractableUI")?.SetUsing(!vsyncUi);
            var resText=resolutionDropdown?resolutionDropdown.Find("CurrentSelectText (TMP)")?.GetComponent<TMP_Text>():null;
            if(resText)resText.text=selectedResolutionText;
            var qualText=qualityDropdown?qualityDropdown.Find("CurrentSelectText (TMP)")?.GetComponent<TMP_Text>():null;
            if(qualText)qualText.text=story.UITranslate(s.quality>=2?"ui_setting_high":s.quality==1?"ui_setting_middle":"ui_setting_low");
            var renderSlider=Get<Slider>("SettingUI","_renderScaleSlider");
            int scalePct=Mathf.Clamp(Mathf.RoundToInt(s.renderScale*100f),35,100);
            if(renderSlider)renderSlider.SetValueWithoutNotify(scalePct);
            var renderValueText=Find("Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/UI_FacilitySetting/Graphics/ScrollView/Viewport/Content/RenderScale/CurrentValueText")?.GetComponent<TMP_Text>();
            if(renderValueText)renderValueText.text=scalePct+"%";
            var activeFps=Get<TMP_InputField>("SettingUI","_activeFrameRateInputField");
            if(activeFps&&!activeFps.isFocused)activeFps.SetTextWithoutNotify(s.activeFps.ToString());
            var deactiveFps=Get<TMP_InputField>("SettingUI","_deactiveFrameRateInputField");
            if(deactiveFps&&!deactiveFps.isFocused)deactiveFps.SetTextWithoutNotify(s.inactiveFps.ToString());
            var shuffleIcon=Get<Image>("MusicUI","_shuffleChangeButtonImage");if(shuffleIcon)shuffleIcon.color=story.MusicState.shuffle?Color.white:new Color(1,1,1,.4f);
            var repeatIcon=Get<Image>("MusicUI","_loopChangeButtonImage");if(repeatIcon)repeatIcon.color=story.MusicState.repeat?Color.white:new Color(1,1,1,.4f);
            var seek=Get<Slider>("MusicUI","musicProgressSlider");if(seek)seek.interactable=player.music.clip&&!state.playing&&!story.MusicLoading;
            if(languageOpen&&languageDropdown)languageDropdown.sizeDelta=new Vector2(languageDropdown.sizeDelta.x,languageClosedHeight+400);
            if(s.mobileFit){var scaler=canvas.GetComponent<CanvasScaler>();scaler.matchWidthOrHeight=Screen.width/(float)Mathf.Max(1,Screen.height)<1920f/1080?0:1;FitOpenPanel();}
            if(s.clock24)foreach(var label in canvas.GetComponentsInChildren<TMP_Text>(true)){if(label.name=="CurrentTimeText")label.text=DateTime.Now.ToString("HH:mm");if(label.name=="AMPMText")label.text="";}
            UpdateMusicPlayingIcons();
        }
        public void UpdateMusicPlayingIcons(float timeOverride=-1f)
        {
            if(musicRows.Count==0)return;
            bool playing=player&&player.music&&player.music.isPlaying;
            float t=timeOverride>=0f?timeOverride:Time.unscaledTime;
            foreach(var row in musicRows)
            {
                if(!row)continue;
                foreach(var tr in row.GetComponentsInChildren<Transform>(true))
                {
                    if(tr.name!="Playing"||!tr.gameObject.activeSelf)continue;
                    int barIndex=0;
                    for(int i=0;i<tr.childCount;i++)
                    {
                        var child=tr.GetChild(i) as RectTransform;
                        if(!child)continue;
                        float wave=playing?0.2f+0.8f*Mathf.Abs(Mathf.Sin(t*5.5f+barIndex*1.15f)):0.25f;
                        var img=child.GetComponent<Image>();
                        if(img)
                        {
                            img.type=Image.Type.Filled;
                            img.fillMethod=Image.FillMethod.Vertical;
                            img.fillOrigin=(int)Image.OriginVertical.Bottom;
                            img.fillAmount=wave;
                            child.localScale=Vector3.one;
                        }
                        else child.localScale=new Vector3(1f,wave,1f);
                        barIndex++;
                    }
                }
            }
        }
        public void UpdateMusicTextScrollers()
        {
            float t=Time.unscaledTime;
            void ScrollText(TMP_Text text)
            {
                if(!text||!text.gameObject.activeInHierarchy)return;
                var rt=text.rectTransform;
                var viewport=rt.parent as RectTransform;
                if(!viewport||viewport.GetComponent<RectMask2D>()==null&&viewport.GetComponent<Mask>()==null)return;
                float viewW=viewport.rect.width;
                float prefW=text.preferredWidth;
                if(viewW<=10f||prefW<=viewW+4f)
                {
                    if(Mathf.Abs(rt.anchoredPosition.x)>0.5f)rt.anchoredPosition=new Vector2(0f,rt.anchoredPosition.y);
                    return;
                }
                float excess=prefW-viewW+24f;
                float duration=Mathf.Max(2f,excess/32f);
                float cycle=duration+2.4f;
                float phase=t%cycle;
                float offset=phase<1.2f?0f:phase<1.2f+duration?(phase-1.2f)*32f:excess;
                rt.anchoredPosition=new Vector2(-offset,rt.anchoredPosition.y);
            }
            ScrollText(Get<TMP_Text>("MusicUI","_musicTitleText"));
            ScrollText(Get<TMP_Text>("MusicUI","_artistNameText"));
            if(IsPaneOpen("music"))
            {
                foreach(var row in musicRows)
                {
                    if(!row)continue;
                    foreach(var txt in row.GetComponentsInChildren<TMP_Text>(false))
                        if(txt.name=="MusicTitleText"||txt.name=="MusicArtistNameText")ScrollText(txt);
                }
            }
        }
        void FitOpenPanel()
        {
            var panel=openPane;if(!panel)return;
            var rect=panel.transform as RectTransform;var space=canvas.transform as RectTransform;
            if(space.rect.width<200||space.rect.height<100){rect.localScale=Vector3.one;return;}
            float extra=IsPaneOpen("todos")&&completedExpanded?365:0;
            float scale=Mathf.Min(1,space.rect.width*.96f/Mathf.Max(1,rect.rect.width+extra),space.rect.height*.94f/Mathf.Max(1,rect.rect.height));rect.localScale=new Vector3(scale,scale,1);
            var corners=new Vector3[4];rect.GetWorldCorners(corners);var bounds=new Bounds(space.InverseTransformPoint(corners[0]),Vector3.zero);for(int i=1;i<4;i++)bounds.Encapsulate(space.InverseTransformPoint(corners[i]));
            if(extra>0)bounds.Encapsulate(new Vector3(bounds.max.x+extra*scale,bounds.max.y,bounds.max.z));
            Vector3 shift=Vector3.zero;if(bounds.max.x>space.rect.xMax)shift.x=space.rect.xMax-bounds.max.x;if(bounds.min.x<space.rect.xMin)shift.x=space.rect.xMin-bounds.min.x;
            if(bounds.max.y>space.rect.yMax)shift.y=space.rect.yMax-bounds.max.y;if(bounds.min.y<space.rect.yMin)shift.y=space.rect.yMin-bounds.min.y;rect.localPosition+=shift;
        }
        void MusicRows()
        {
            var story=player.story;if(!story.FeatureReady){LegacyMusicRows();return;}
            string signature=story.MusicRevision+"|"+story.MusicState.playlist+"|"+musicTagMask;if(signature==musicSignature)return;bool resetPosition=musicSignature==null;musicSignature=signature;
            var scroll=Get<ScrollRect>("MusicPlayListView","_scrollRect");float position=resetPosition?1:scroll?scroll.verticalNormalizedPosition:1;
            Clear(musicRows);var parent=RefOf("MusicPlayListView","_playListButtonsParent");if(!parent||!musicCell)return;Set(RefOf("MusicPlayListView","_dummyButton"),false);Layout(parent,((RectTransform)musicCell.transform).rect.height);
            foreach(var item in story.PlaylistTracks.Where(TrackMatchesTagMask))
            {
                string guid=item.guid;
                bool isCurrent=story.MusicState.selected==guid;
                bool isExcluded=story.MusicState.excluded.Contains(guid);
                var go=Cell(musicCell,parent);SetText(go,"MusicTitleText",item.title);SetText(go,"MusicArtistNameText",item.artist);
                var play=go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="PlayListMusicPlayButton");Bind(play?play.gameObject:go,()=>story.SelectTrack(guid));
                var playIcon=play?play.Find("PlayStateIcon"):go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="PlayStateIcon");
                if(playIcon)Set(playIcon,!isCurrent);
                var favorite=go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="FavoriteIcon");
                if(favorite){favorite.gameObject.SetActive(true);Bind(favorite.gameObject,()=>story.ToggleFavorite(guid));var hit=favorite.Find("FavoriteIconBase")?.GetComponent<Graphic>();if(hit){favorite.GetComponent<Button>().targetGraphic=hit;hit.raycastTarget=true;}var active=favorite.Find("FavoriteIconActive");if(active)active.gameObject.SetActive(story.MusicState.favorites.Contains(guid));}
                var candidate=go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="PlayCheck");
                if(candidate)
                {
                    candidate.gameObject.SetActive(true);
                    Bind(candidate.gameObject,()=>story.ToggleTrackExcluded(guid));
                    var hit=candidate.Find("PlayCheckIconBase")?.GetComponent<Graphic>();
                    if(hit){var btn=candidate.GetComponent<Button>();if(btn)btn.targetGraphic=hit;hit.raycastTarget=true;}
                    var checkActive=candidate.Find("PlayCheckIconActive");
                    if(checkActive)Set(checkActive,!isExcluded);
                }
                var grey=go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="GreyOutImage");
                if(grey)
                {
                    grey.gameObject.SetActive(isExcluded);
                    var greyImg=grey.GetComponent<Image>();
                    if(greyImg)greyImg.color=new Color(greyImg.color.r,greyImg.color.g,greyImg.color.b,isExcluded?0.5f:0f);
                }
                Color textColor=isExcluded?(Color)new Color32(125,125,125,125):Color.white;
                foreach(var txt in go.GetComponentsInChildren<TMP_Text>(true))
                {
                    if(txt.name=="MusicTitleText"||txt.name=="MusicArtistNameText")txt.color=textColor;
                }
                var remove=go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="RemoveButton");if(remove){remove.gameObject.SetActive(item.personal);Bind(remove.gameObject,()=>story.RemoveTrack(guid));}
                foreach(var t in go.GetComponentsInChildren<Transform>(true))if(t.name=="Playing")t.gameObject.SetActive(isCurrent);
                var handle=go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Doraggable");
                if(handle)
                {
                    handle.gameObject.SetActive(true);
                    var drag=handle.gameObject.GetComponent<ChillNativeRowDrag>()??handle.gameObject.AddComponent<ChillNativeRowDrag>();
                    drag.rowHeight=64f;
                    drag.moved=step=>story.MoveTrack(guid,step);
                }
                musicRows.Add(go);
            }
            if(scroll){Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(parent as RectTransform);scroll.verticalNormalizedPosition=position;}
            UpdateMusicPlayingIcons();
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
