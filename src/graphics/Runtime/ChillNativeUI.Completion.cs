using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillNativeUI
    {
        bool completionReady,timerAudioCountup,presetSaveMode,presetWarnUnsavedMode;
        Transform skinPopup,skinContent,purchasePopoverTarget;GameObject timerAudioPane;
        readonly List<GameObject> skinRows=new List<GameObject>(),specialRows=new List<GameObject>();
        TMP_InputField roomPresetName,profileName,profileStatus;
        string selectedRoomPreset="",specialSignature,skinSelectorLayoutSig="";
        int openSkinModel=-1,selectedLayoutPresetSlot,pendingSavePresetSlot=-1,warnUnsavedMoveToSlot=-1;
        RectTransform activePurchasePopover;
        ScrollRect purchasePopoverScrollRect;
        Vector2 purchasePopoverOffset;
        int purchasePopoverOpenedFrame=-1;
        RectTransform DecorationContent=>Get<ScrollRect>("DecorationListUI","_scrollRect")?.content;
        Transform SkinSelectorAnim(int category)=>DecorationContent?.Find(category==1?"ItemSkinSelectorAnim":"ItemSkinSelectorAnim ("+(category-1)+")");
        bool IsDecorationPurchasable(string skinName){var rule=player.story.UnlockRule("decoration",skinName);return rule!=null&&rule.condition=="PointPurchase";}
        public void ClosePurchasePopover()
        {
            if(activePurchasePopover)Set(activePurchasePopover,false);
            Set(RefOf("DecorationListUI","_purchasePopover"),false);
            Set(Find(EnvRootPath+"/PurchasePopover"),false);
            activePurchasePopover=null;
            purchasePopoverTarget=null;
            purchasePopoverScrollRect=null;
        }
        void UpdatePurchasePopover()
        {
            if(!activePurchasePopover||!activePurchasePopover.gameObject.activeInHierarchy)
            {
                activePurchasePopover=null;
                purchasePopoverTarget=null;
                return;
            }
            if(!purchasePopoverTarget||!purchasePopoverTarget.gameObject.activeInHierarchy)
            {
                ClosePurchasePopover();
                return;
            }
            if(purchasePopoverScrollRect&&purchasePopoverScrollRect.viewport)
            {
                var vp=purchasePopoverScrollRect.viewport;
                var targetRt=purchasePopoverTarget as RectTransform;
                if(targetRt)
                {
                    var vpCorners=new Vector3[4];
                    var targetCorners=new Vector3[4];
                    vp.GetWorldCorners(vpCorners);
                    targetRt.GetWorldCorners(targetCorners);
                    if(targetCorners[2].y<vpCorners[0].y||targetCorners[0].y>vpCorners[2].y)
                    {
                        ClosePurchasePopover();
                        return;
                    }
                }
            }
            float scaleX=(float)Screen.width/1920f;
            float scaleY=(float)Screen.height/1080f;
            activePurchasePopover.position=purchasePopoverTarget.position+new Vector3(purchasePopoverOffset.x*scaleX,purchasePopoverOffset.y*scaleY,0f);
            if(Time.frameCount>purchasePopoverOpenedFrame&&Input.GetMouseButtonDown(0)&&!Contains(activePurchasePopover)&&!(purchasePopoverTarget is RectTransform tr&&Contains(tr)))
            {
                ClosePurchasePopover();
            }
        }
        public bool IsNewSkin(string skinName)
        {
            var story=player.story;var rule=story.UnlockRule("decoration",skinName);
            if(rule==null||string.IsNullOrEmpty(rule.condition)||rule.condition=="None")return false;
            return story.ItemAvailable("decoration",skinName,true)&&!story.Workspace.playedDecorations.Contains(skinName);
        }
        public string ActiveDecorationSkin(int category)
        {
            var story=player.story;var choice=story.Workspace.room.FirstOrDefault(r=>r.category==category);
            if(choice!=null&&!string.IsNullOrEmpty(choice.skin))return choice.skin;
            var defaults=story.decorations?.Data?.defaultSkins??Array.Empty<string>();
            var defaultSkin=story.decorations?.Skins?.FirstOrDefault(s=>s.category==category&&defaults.Contains(s.name));
            return defaultSkin!=null?defaultSkin.name:"";
        }
        bool IsDecorationCategoryKnown(int category)
        {
            var story=player.story;var skins=(story.decorations?.Skins??Array.Empty<ChillDecorations.Skin>()).Where(s=>s.category==category).ToArray();
            return skins.Length>0&&(skins.Any(s=>story.ItemAvailable("decoration",s.name))||skins.All(s=>IsDecorationPurchasable(s.name)));
        }
        bool IsDecorationModelKnown(int model)
        {
            var story=player.story;var skins=(story.decorations?.Skins??Array.Empty<ChillDecorations.Skin>()).Where(s=>s.model==model).ToArray();
            return skins.Length>0&&(skins.Any(s=>story.ItemAvailable("decoration",s.name))||skins.All(s=>IsDecorationPurchasable(s.name)));
        }
        bool IsDecorationModelTemporarilyLocked(int model)
        {
            if(model==12001&&player.story.IsMusicBoxPlaying)return true;
            var date=ChillRuntimeContext.LocalNow.Date;
            int season=player.story.Playing?0:date==new DateTime(2025,12,25)?100:date==new DateTime(2026,4,1)?102:0;
            if(model==2002)return season==100;
            if(model==3001||model==3002||model==3003||model==3004||model==3999)return season==102;
            return false;
        }
        public void HideSkinSelectors()
        {
            openSkinModel=-1;skinSelectorLayoutSig="";Clear(skinRows);
            if(skinPopup)Show(skinPopup.gameObject,false);
            ClosePurchasePopover();
            CloseLayoutPresetConfirm(RefOf("DecorationListUI","self")?.Find("LayoutPresetUI"));
            var content=DecorationContent;if(!content)return;
            for(int cat=1;cat<=12;cat++)
            {
                var anim=SkinSelectorAnim(cat) as RectTransform;if(!anim)continue;
                Set(anim.Find("ItemSkinSelector/SkinItem"),false);
                anim.sizeDelta=new Vector2(anim.sizeDelta.x,0f);
                var cg=anim.GetComponent<CanvasGroup>();if(cg)cg.alpha=0f;
                anim.gameObject.SetActive(false);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
        static readonly string[] PresetSlotNames={"LayoutPresetChangeButton With Arrow","LayoutPresetChangeButton With Arrow (1)","LayoutPresetChangeButton With Arrow (2)","LayoutPresetChangeButton With Arrow (3)","LayoutPresetChangeButton With Arrow (4)"};
        public bool HasDifferenceFromPreset(Transform presetRoot,int index)
        {
            var story=player.story;if(!story.FeatureReady)return false;
            string presetKey="Preset "+(index+1);
            var preset=story.Workspace.presets.FirstOrDefault(p=>p.name==presetKey);
            bool isEnv=presetRoot&&PathOf(presetRoot).Contains("UI_FacilityEnviroment");
            if(isEnv)
            {
                int expTime=preset!=null?preset.time:0;
                int expWin=preset!=null?preset.window:0;
                if(player.environment.selectedTime!=expTime||player.environment.selectedEnvironment!=expWin)return true;
                foreach(var choice in environmentChoices??Array.Empty<EnvironmentChoice>())
                {
                    if(choice.ambientId<0)continue;
                    float cur=story.AmbientLevel(choice.ambientId);
                    float exp=preset?.ambience?.FirstOrDefault(a=>a.id==choice.ambientId)?.volume??0f;
                    if(Mathf.Abs(cur-exp)>0.01f)return true;
                }
                return false;
            }
            var defaults=story.decorations?.Data?.defaultSkins??Array.Empty<string>();
            for(int cat=1;cat<=12;cat++)
            {
                string cur=ActiveDecorationSkin(cat);
                string exp;
                var presetChoice=preset?.choices?.FirstOrDefault(r=>r.category==cat);
                if(presetChoice!=null&&!string.IsNullOrEmpty(presetChoice.skin))exp=presetChoice.skin;
                else
                {
                    var defaultSkin=story.decorations?.Skins?.FirstOrDefault(s=>s.category==cat&&defaults.Contains(s.name));
                    exp=defaultSkin!=null?defaultSkin.name:"";
                }
                if(cur!=exp)return true;
            }
            return false;
        }
        void ApplyPresetSlot(Transform presetRoot,int slot)
        {
            selectedLayoutPresetSlot=slot;
            selectedRoomPreset="Preset "+(slot+1);
            HideSkinSelectors();
            bool isEnv=presetRoot&&PathOf(presetRoot).Contains("UI_FacilityEnviroment");
            if(player.story.Workspace.presets.Any(p=>p.name==selectedRoomPreset))
                player.story.LoadRoomPreset(selectedRoomPreset);
            else if(isEnv)
            {
                player.environment.SelectTime(0);
                player.environment.SelectEnvironment(0);
                foreach(var choice in environmentChoices??Array.Empty<EnvironmentChoice>())
                    if(choice.ambientId>=0)SetCardAmbience(choice.ambientId,0f);
            }
            else player.story.ResetRoom();
            RefreshDecorationControls();
            RefreshEnvironmentCards(true);
        }
        public void CloseLayoutPresetConfirm(Transform presetRoot)
        {
            presetSaveMode=false;presetWarnUnsavedMode=false;
            pendingSavePresetSlot=-1;warnUnsavedMoveToSlot=-1;
            if(!presetRoot)return;
            Set(presetRoot.Find("Select Save Text"),false);
            Set(presetRoot.Find("Confirm Save"),false);
            Set(presetRoot.Find("Warn Unsaved"),false);
            RefreshLayoutPresetUI(presetRoot);
        }
        public void WireLayoutPresetUI(Transform presetRoot)
        {
            if(!presetRoot)return;
            Set(presetRoot,true);
            CloseLayoutPresetConfirm(presetRoot);
            var buttonsRoot=presetRoot.Find("Buttons");
            if(buttonsRoot)
            {
                for(int i=0;i<PresetSlotNames.Length;i++)
                {
                    int slot=i;
                    var wrapper=buttonsRoot.Find(PresetSlotNames[i]);if(!wrapper)continue;
                    var slotBtn=wrapper.Find("LayoutPresetChangeButton")??wrapper;
                    Bind(slotBtn.gameObject,()=>{
                        if(presetWarnUnsavedMode)return;
                        if(presetSaveMode)
                        {
                            pendingSavePresetSlot=slot;
                            Set(presetRoot.Find("Select Save Text"),false);
                            var confirm=presetRoot.Find("Confirm Save");
                            if(confirm){Show(confirm.gameObject,true);confirm.SetAsLastSibling();}
                            RefreshLayoutPresetUI(presetRoot);
                        }
                        else
                        {
                            int prevIndex=selectedLayoutPresetSlot;
                            bool hasDiff=HasDifferenceFromPreset(presetRoot,prevIndex);
                            if(slot==prevIndex&&!hasDiff)return;
                            if(hasDiff)
                            {
                                presetWarnUnsavedMode=true;
                                warnUnsavedMoveToSlot=slot;
                                var warnObj=presetRoot.Find("Warn Unsaved");
                                if(warnObj)
                                {
                                    var warnYesBtn=warnObj.Find("YesButton")??warnObj.Find("Buttons/YesButton");
                                    var warnNoBtn=warnObj.Find("NoButton")??warnObj.Find("Buttons/NoButton");
                                    var warnText=warnObj.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>(!warnYesBtn||!t.transform.IsChildOf(warnYesBtn))&&(!warnNoBtn||!t.transform.IsChildOf(warnNoBtn)));
                                    if(warnText)
                                    {
                                        localized.Remove(warnText);
                                        string tpl=player.story.UITranslate("ui_changed_check");
                                        try{warnText.text=string.Format(tpl,prevIndex+1);}
                                        catch{warnText.text=tpl;}
                                    }
                                    Show(warnObj.gameObject,true);
                                    warnObj.SetAsLastSibling();
                                }
                                RefreshLayoutPresetUI(presetRoot);
                                return;
                            }
                            ApplyPresetSlot(presetRoot,slot);
                        }
                    });
                }
            }
            var saveBtn=presetRoot.Find("SaveButton");
            if(saveBtn)
            {
                Bind(saveBtn.gameObject,()=>{
                    presetSaveMode=!presetSaveMode;
                    presetWarnUnsavedMode=false;
                    pendingSavePresetSlot=-1;
                    warnUnsavedMoveToSlot=-1;
                    Set(presetRoot.Find("Confirm Save"),false);
                    Set(presetRoot.Find("Warn Unsaved"),false);
                    var selectText=presetRoot.Find("Select Save Text");
                    if(selectText){Show(selectText.gameObject,presetSaveMode);if(presetSaveMode)selectText.SetAsLastSibling();}
                    RefreshLayoutPresetUI(presetRoot);
                });
            }
            var yesBtn=presetRoot.Find("Confirm Save/YesButton")??presetRoot.Find("Confirm Save/Buttons/YesButton");
            if(yesBtn)
            {
                Bind(yesBtn.gameObject,()=>{
                    int slot=pendingSavePresetSlot>=0?pendingSavePresetSlot:selectedLayoutPresetSlot;
                    selectedLayoutPresetSlot=slot;
                    selectedRoomPreset="Preset "+(slot+1);
                    player.story.SaveRoomPreset(selectedRoomPreset);
                    CloseLayoutPresetConfirm(presetRoot);
                    RefreshLayoutPresetUI(presetRoot);
                });
            }
            var noBtn=presetRoot.Find("Confirm Save/NoButton")??presetRoot.Find("Confirm Save/Buttons/NoButton");
            if(noBtn)Bind(noBtn.gameObject,()=>{CloseLayoutPresetConfirm(presetRoot);RefreshLayoutPresetUI(presetRoot);});
            var warnYes=presetRoot.Find("Warn Unsaved/YesButton")??presetRoot.Find("Warn Unsaved/Buttons/YesButton");
            if(warnYes)Bind(warnYes.gameObject,()=>{
                int prevSlot=selectedLayoutPresetSlot;
                int nextSlot=warnUnsavedMoveToSlot>=0?warnUnsavedMoveToSlot:prevSlot;
                player.story.SaveRoomPreset("Preset "+(prevSlot+1));
                CloseLayoutPresetConfirm(presetRoot);
                ApplyPresetSlot(presetRoot,nextSlot);
            });
            var warnNo=presetRoot.Find("Warn Unsaved/NoButton")??presetRoot.Find("Warn Unsaved/Buttons/NoButton");
            if(warnNo)Bind(warnNo.gameObject,()=>{
                int nextSlot=warnUnsavedMoveToSlot>=0?warnUnsavedMoveToSlot:selectedLayoutPresetSlot;
                CloseLayoutPresetConfirm(presetRoot);
                ApplyPresetSlot(presetRoot,nextSlot);
            });
        }
        public void RefreshLayoutPresetUI(Transform presetRoot)
        {
            if(!presetRoot)return;
            bool presetLocked=player.story.IsMusicBoxPlaying&&PathOf(presetRoot).Contains("UI_FacilityDecoration");
            var buttonsRoot=presetRoot.Find("Buttons");
            if(buttonsRoot)
            {
                for(int i=0;i<PresetSlotNames.Length;i++)
                {
                    var wrapper=buttonsRoot.Find(PresetSlotNames[i]);if(!wrapper)continue;
                    bool showArrow=presetWarnUnsavedMode?(i==selectedLayoutPresetSlot):presetSaveMode&&(pendingSavePresetSlot<0||pendingSavePresetSlot==i);
                    bool selected=presetWarnUnsavedMode?(i==selectedLayoutPresetSlot||i==warnUnsavedMoveToSlot):presetSaveMode?(pendingSavePresetSlot==i):(i==selectedLayoutPresetSlot);
                    Set(wrapper.Find("Arrow"),showArrow);
                    Set(wrapper.Find("PresetSaveLockImage"),presetLocked);
                    var slotBtn=wrapper.Find("LayoutPresetChangeButton")??wrapper;
                    var btnComp=slotBtn.GetComponent<Button>();if(btnComp)btnComp.interactable=!presetLocked;
                    Set(slotBtn.Find("ChengeCompletedEffectImage"),false);
                    Set(slotBtn.Find("ActiveBackImage"),selected);
                    Set(slotBtn.Find("Active Icon"),selected);
                    slotBtn.GetComponent<ChillNativeInteraction>()?.SetUsing(selected);
                }
            }
            var saveBtn=presetRoot.Find("SaveButton");
            if(saveBtn)
            {
                bool saveUiActive=presetSaveMode||presetWarnUnsavedMode;
                Set(saveBtn.Find("PresetSaveLockImage"),presetLocked);
                var sBtn=saveBtn.GetComponent<Button>();if(sBtn)sBtn.interactable=!presetLocked;
                Set(saveBtn.Find("SaveBackImage"),saveUiActive);
                Set(saveBtn.Find("ActiveAroundImage"),saveUiActive);
                Set(saveBtn.Find("Active Icon"),saveUiActive);
                saveBtn.GetComponent<ChillNativeInteraction>()?.SetUsing(saveUiActive);
            }
        }
        void InitializeCompletionControls()
        {
            var story=player.story;
            Wire("TimerSoundSettingView","_settingOpenButton",()=>Open("timer-audio"));
            Wire("TimerSoundSettingView","_pomodoroTabButton",()=>{timerAudioCountup=false;RefreshTimerAudio();});Wire("TimerSoundSettingView","_countupTabButton",()=>{timerAudioCountup=true;RefreshTimerAudio();});
            foreach(bool countup in new[]{false,true})foreach(bool finish in new[]{false,true})
            {
                string prefix="_"+(countup?"countup":"pomodoro")+(finish?"Finish":"Start");var slider=Get<Slider>("TimerSoundSettingView",prefix+"VolumeSlider");
                if(slider){slider.minValue=0;slider.maxValue=1;slider.wholeNumbers=false;slider.onValueChanged.RemoveAllListeners();slider.onValueChanged.AddListener(value=>{var option=story.TimerAudio(countup);if(finish)option.finishVolume=value;else option.startVolume=value;story.SaveTimerAudio();});}
                Wire("TimerSoundSettingView",prefix+"SoundMuteButton",()=>{var option=story.TimerAudio(countup);if(finish)option.finishMute=!option.finishMute;else option.startMute=!option.startMute;story.SaveTimerAudio();});
            }
            Set(RefOf("TimerSoundSettingView","self"),false);
            Wire("DecorationListUI","_facilityOpenButton",()=>Open("decorations"));Wire("DecorationListUI","_closeButton",CloseExclusivePane);Wire("DecorationListUI","_hideButton",ToggleHidden);
            var popover=RefOf("DecorationListUI","_purchasePopover");Set(popover,false);
            var popClose=popover?.Find("CloseButtonBase/CloseButtonUI");if(popClose)Bind(popClose.gameObject,()=>Set(popover,false));
            var root=RefOf("DecorationListUI","self");
            if(root)
            {
                WireLayoutPresetUI(root.Find("LayoutPresetUI"));
                var content=DecorationContent;
                if(content)
                {
                    foreach(var pair in new[]{(5,"GridLayout (5)/ChangeBadgeButton Disable/MainButton"),(11,"GridLayout (11)/ChangeBadgeButton Disable/MainButton"),(12,"GridLayout (12)/ChangeMusicBoxButton Disable/MainButton")})
                    {int cat=pair.Item1;var disableBtn=content.Find(pair.Item2);if(disableBtn)Bind(disableBtn.gameObject,()=>{if(cat==12&&story.IsMusicBoxPlaying)return;HideSkinSelectors();story.ClearDecoration(cat);RefreshDecorationControls();});}
                }
                HideSkinSelectors();
                PreparePane(root);
            }
            Wire("PlayerProfileView","_closeButton",CloseExclusivePane);
            foreach(var button in canvas.GetComponentsInChildren<Button>(true).Where(b=>b.name.Contains("PlayerProfile")&&PathOf(b.transform).Contains("LeftIcons")))Bind(button.gameObject,()=>Open("profile"));
            InitializeProfileControls();
            Wire("SpecialView","_openSpecialListButton",()=>Open("special"));Wire("SpecialView","_closeButton",CloseExclusivePane);
            Wire("HabitWeekLabelUI","_prevWeekButton",()=>{habitWeek=habitWeek.AddDays(-7);workspaceSignature=null;});Wire("HabitWeekLabelUI","_nextWeekButton",()=>{habitWeek=habitWeek.AddDays(7);workspaceSignature=null;});
        }
        void RefreshCompletionControls()
        {
            RefreshProfileControls();
            var story=player.story;if(!story.FeatureReady)return;
            if(!completionReady)
            {
                completionReady=true;
                foreach(var choice in story.Features.statisticsButtons??Array.Empty<ChillTimerButton>()){int id=choice.id;WirePath(choice.path,()=>{statisticsSpan=id;workspaceSignature=null;});}
                foreach(var choice in story.Features.profileButtons??Array.Empty<ChillProfileButton>()){int id=choice.id;WirePath(choice.path,()=>{if(choice.path.Contains("Import")){ChillBrowser.ImportAvatar(story.gameObject.name);return;}story.SelectProfileIcon(id);Set(RefOf("ProfileIconSettingView","self"),false);});}
                foreach(var choice in story.Features.statusButtons??Array.Empty<ChillProfileButton>()){int id=choice.id;WirePath(choice.path,()=>{story.SelectPlayerStatus(id);Set(RefOf("StatusChangeView","self"),false);});}
                foreach(var choice in story.Features.timerButtons??Array.Empty<ChillTimerButton>())
                {int id=choice.id;bool countup=choice.path.Contains("CountupSoundSetting");WirePath(choice.path,()=>{var option=story.TimerAudio(countup);if(id<3)option.start=id;else option.finish=id;story.SaveTimerAudio();story.PreviewTimerSound(countup,id>=3);});}
                foreach(var choice in story.Features.decorationButtons??Array.Empty<ChillTimerButton>()){int model=choice.id;WirePath(choice.path,()=>ShowSkinSelector(model));}
                HideSkinSelectors();
            }
            RefreshDecorationControls();
            if(IsPaneOpen("timer-audio"))RefreshTimerAudio();
            var point=RefOf("DecorationListUI","_ownPointView")?.GetComponentInChildren<TMP_Text>(true);if(point)point.text=story.Workspace.points.ToString();
            string specSig=story.WorkspaceRevision+":"+story.UISnapshot("story").completed+":"+story.Workspace.collaboration+":"+story.Language;
            if(IsPaneOpen("special")&&specialSignature!=specSig){specialSignature=specSig;Clear(specialRows);CreateSpecialRows();}
        }
        void RefreshDecorationControls()
        {
            var story=player.story;if(!story.FeatureReady||!story.decorations)return;
            RefreshLayoutPresetUI(RefOf("DecorationListUI","self")?.Find("LayoutPresetUI"));
            for(int cat=1;cat<=12;cat++)
            {
                string active=ActiveDecorationSkin(cat);
                if(!string.IsNullOrEmpty(active)&&!story.Workspace.playedDecorations.Contains(active))story.Workspace.playedDecorations.Add(active);
            }
            bool anyNew=false;
            foreach(var choice in story.Features.decorationButtons??Array.Empty<ChillTimerButton>())
            {
                int model=choice.id;var main=Find(choice.path);if(!main)continue;
                var buttonRoot=main.parent;
                var skins=story.decorations.Skins.Where(s=>s.model==model).ToArray();if(skins.Length==0)continue;
                int cat=skins[0].category;
                bool known=IsDecorationModelKnown(model);
                if(buttonRoot)buttonRoot.gameObject.SetActive(known);
                bool hasNew=known&&skins.Any(s=>IsNewSkin(s.name));
                if(hasNew)anyNew=true;
                Set(buttonRoot?.Find("newImage"),hasNew);
                bool tempLocked=IsDecorationModelTemporarilyLocked(model);
                Set(buttonRoot?.Find("DecorationLockImage"),tempLocked);
                var btn=main.GetComponent<Button>();if(btn)btn.interactable=!tempLocked;
                string activeSkin=ActiveDecorationSkin(cat);
                main.GetComponent<ChillNativeInteraction>()?.SetUsing(skins.Any(s=>s.name==activeSkin));
            }
            var content=DecorationContent;
            if(content)
            {
                foreach(var pair in new[]{(5,"GridLayout (5)/ChangeBadgeButton Disable"),(11,"GridLayout (11)/ChangeBadgeButton Disable"),(12,"GridLayout (12)/ChangeMusicBoxButton Disable")})
                {
                    var disableRoot=content.Find(pair.Item2);if(!disableRoot)continue;
                    bool known=IsDecorationCategoryKnown(pair.Item1);
                    disableRoot.gameObject.SetActive(known);
                    bool musicBoxLocked=pair.Item1==12&&story.IsMusicBoxPlaying;
                    Set(disableRoot.Find("DecorationLockImage"),musicBoxLocked);
                    var main=disableRoot.Find("MainButton");
                    var mainBtn=main?.GetComponent<Button>();if(mainBtn)mainBtn.interactable=!musicBoxLocked;
                    main?.GetComponent<ChillNativeInteraction>()?.SetUsing(string.IsNullOrEmpty(ActiveDecorationSkin(pair.Item1)));
                }
                for(int cat=1;cat<=12;cat++)
                {
                    var grid=content.Find(cat==1?"GridLayout(1)":"GridLayout ("+cat+")");
                    if(!grid||grid.GetSiblingIndex()==0)continue;
                    var titleObj=content.GetChild(grid.GetSiblingIndex()-1);
                    var titleText=titleObj.GetComponent<TMP_Text>()??titleObj.GetComponentInChildren<TMP_Text>(true);
                    if(!titleText)continue;
                    bool known=IsDecorationCategoryKnown(cat);
                    string key=localized.TryGetValue(titleText,out var origKey)?origKey:titleText.text;
                    titleText.text=story.UITranslate(known?key:"ui_lock_title");
                    titleText.color=known?Color.white:(Color)new Color32(128,128,128,128);
                }
            }
            Set(RefOf("DecorationListUI","_facilityOpenButton")?.Find("NewMark"),anyNew);
            if(openSkinModel>0)PopulateSkinSelector(openSkinModel);
        }
        void RefreshTimerAudio()
        {
            var story=player.story;Set(RefOf("TimerSoundSettingView","_pomodoroSoundSettingParent"),!timerAudioCountup);Set(RefOf("TimerSoundSettingView","_countupSoundSettingParent"),timerAudioCountup);
            foreach(bool countup in new[]{false,true})foreach(bool finish in new[]{false,true}){string prefix="_"+(countup?"countup":"pomodoro")+(finish?"Finish":"Start");var option=story.TimerAudio(countup);var slider=Get<Slider>("TimerSoundSettingView",prefix+"VolumeSlider");if(slider)slider.SetValueWithoutNotify(finish?option.finishVolume:option.startVolume);Write("TimerSoundSettingView",prefix+"VolumeValueText",Mathf.RoundToInt((finish?option.finishVolume:option.startVolume)*100).ToString());var mute=RefOf("TimerSoundSettingView",prefix+"SoundMuteButton")?.GetComponent<Image>();if(mute)mute.color=(finish?option.finishMute:option.startMute)?new Color(.5f,.5f,.6f):Color.white;}
            foreach(var choice in story.Features.timerButtons??Array.Empty<ChillTimerButton>()){var option=story.TimerAudio(choice.path.Contains("CountupSoundSetting"));var image=Find(choice.path)?.GetComponent<Image>();if(image)image.color=choice.id==(choice.id<3?option.start:option.finish)?new Color(.3f,1,.9f):Color.white;}
        }
        public void ShowSkinSelector(int model)
        {
            if(IsDecorationModelTemporarilyLocked(model))return;
            var story=player.story;
            var skins=story.decorations.Skins.Where(s=>s.model==model).ToArray();
            if(skins.Length==0){HideSkinSelectors();return;}
            if(skins.Length==1)
            {
                HideSkinSelectors();
                story.ChooseDecoration(skins[0].name);
                RefreshDecorationControls();
                return;
            }
            if(openSkinModel==model)
            {
                HideSkinSelectors();
                return;
            }
            HideSkinSelectors();
            openSkinModel=model;
            PopulateSkinSelector(model);
        }
        void PopulateSkinSelector(int model)
        {
            var story=player.story;
            var skins=story.decorations.Skins.Where(s=>s.model==model).ToArray();
            if(skins.Length<=1)return;
            int cat=skins[0].category;
            var anim=SkinSelectorAnim(cat) as RectTransform;if(!anim)return;
            var selector=anim.Find("ItemSkinSelector") as RectTransform;
            var template=selector?.Find("SkinItem");if(!template)return;
            string activeSkin=ActiveDecorationSkin(cat);
            var visibleSkins=skins.Where(s=>story.ItemAvailable("decoration",s.name)||IsDecorationPurchasable(s.name)).ToArray();
            string layoutSig=model+":"+string.Join("|",visibleSkins.Select(s=>s.name+":"+story.ItemAvailable("decoration",s.name)));
            if(skinSelectorLayoutSig==layoutSig&&skinRows.Count==visibleSkins.Length&&skinRows.All(r=>r))
            {
                for(int i=0;i<visibleSkins.Length;i++)
                {
                    var skin=visibleSkins[i];
                    var go=skinRows[i];
                    bool unlocked=story.ItemAvailable("decoration",skin.name);
                    var main=go.transform.Find("MainButton");
                    main?.GetComponent<ChillNativeInteraction>()?.SetUsing(unlocked&&activeSkin==skin.name);
                    Set(go.transform.Find("NewImage"),IsNewSkin(skin.name));
                }
                return;
            }
            skinSelectorLayoutSig=layoutSig;
            Set(template,false);
            Clear(skinRows);
            anim.gameObject.SetActive(true);
            anim.sizeDelta=new Vector2(anim.sizeDelta.x,100f);
            var cg=anim.GetComponent<CanvasGroup>();if(cg){cg.alpha=1f;cg.interactable=cg.blocksRaycasts=true;}
            void BindSkinInteraction(Transform btn,bool isUsing)
            {
                if(!btn)return;
                var inter=btn.GetComponent<ChillNativeInteraction>()??btn.gameObject.AddComponent<ChillNativeInteraction>();
                var activeGraphic=btn.Find("Active")?.GetComponent<Graphic>();
                if(activeGraphic){var c=activeGraphic.color;c.a=isUsing?1f:0f;activeGraphic.color=c;}
                inter.hover=activeGraphic?new[]{activeGraphic}:Array.Empty<Graphic>();
                inter.use=activeGraphic?new[]{activeGraphic}:Array.Empty<Graphic>();
                inter.baseline=Array.Empty<Graphic>();
                inter.hoverAlpha=inter.useAlpha=inter.baseAlpha=1f;
                inter.DeactivateAll();
                inter.SetUsing(isUsing);
            }
            foreach(var skin in visibleSkins)
            {
                bool unlocked=story.ItemAvailable("decoration",skin.name);
                var go=Instantiate(template.gameObject,selector,false);
                Show(go,true);FixSprites(go);CleanEvents(go);foreach(var a in go.GetComponentsInChildren<Animator>(true))a.enabled=false;
                go.transform.SetAsLastSibling();
                var colors=story.Features.skinColors.FirstOrDefault(c=>c.id==skin.id);
                if(colors!=null){var upper=go.transform.Find("MainButton/Back Upper")?.GetComponent<Image>();var lower=go.transform.Find("MainButton/Back Lower")?.GetComponent<Image>();if(upper)upper.color=colors.upper;if(lower)lower.color=colors.lower;}
                var main=go.transform.Find("MainButton");var shop=go.transform.Find("ShopButton");
                BindSkinInteraction(main,unlocked&&activeSkin==skin.name);
                BindSkinInteraction(shop,false);
                var mainCg=main?.GetComponent<CanvasGroup>();var shopCg=shop?.GetComponent<CanvasGroup>();
                string skinName=skin.name;var skinRef=skin;
                if(unlocked)
                {
                    if(main)main.gameObject.SetActive(true);
                    if(mainCg){mainCg.alpha=1f;mainCg.interactable=mainCg.blocksRaycasts=true;}
                    if(shop)shop.gameObject.SetActive(false);
                    if(shopCg)shopCg.blocksRaycasts=false;
                    if(main)Bind(main.gameObject,()=>{ClosePurchasePopover();story.ChooseDecoration(skinName);RefreshDecorationControls();});
                }
                else
                {
                    if(main)main.gameObject.SetActive(true);
                    if(mainCg){mainCg.alpha=1f;mainCg.blocksRaycasts=false;}
                    if(shop)shop.gameObject.SetActive(true);
                    if(shopCg){shopCg.alpha=1f;shopCg.interactable=shopCg.blocksRaycasts=true;}
                    if(shop)Bind(shop.gameObject,()=>ShowDecorationPurchasePopover(skinRef,model,go.transform));
                }
                Set(go.transform.Find("NewImage"),IsNewSkin(skinName));
                skinRows.Add(go);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(selector);
            LayoutRebuilder.ForceRebuildLayoutImmediate(anim);
            if(DecorationContent)LayoutRebuilder.ForceRebuildLayoutImmediate(DecorationContent as RectTransform);
        }
        public void ShowDecorationPurchasePopover(ChillDecorations.Skin skin,int model,Transform targetButton)
        {
            var popover=RefOf("DecorationListUI","_purchasePopover") as RectTransform;if(!popover||!targetButton)return;
            var story=player.story;
            var rule=story.UnlockRule("decoration",skin.name);
            int price=rule!=null&&int.TryParse(rule.argument,out int p)?p:0;
            bool canPurchase=story.Workspace.points>=price;
            var priceText=popover.Find("PurchaseButton/PriceText")?.GetComponent<TMP_Text>();
            if(priceText){priceText.text=price.ToString();priceText.color=canPurchase?Color.white:(Color)new Color32(255,99,99,255);}
            var itemImage=popover.Find("MainImageFrame/MainImage")?.GetComponent<Image>();
            var skinThumb=(skinThumbnails??Array.Empty<SkinThumbnail>()).FirstOrDefault(t=>t!=null&&t.skinId==skin.id)?.sprite;
            if(!skinThumb)
            {
                var modelChoice=story.Features.decorationButtons?.FirstOrDefault(b=>b.id==model);
                var modelMain=modelChoice!=null?Find(modelChoice.path):null;
                skinThumb=modelMain?.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.sprite&&i.name!="Active"&&i.transform!=modelMain)?.sprite;
            }
            if(itemImage&&skinThumb)itemImage.sprite=RecoveredSprite(skinThumb);
            Set(popover.Find("MainImageFrame/PurchaseCompletedAnimationImage"),false);
            activePurchasePopover=popover;
            purchasePopoverTarget=targetButton;
            purchasePopoverOffset=new Vector2(0f,-34.5f);
            purchasePopoverScrollRect=Get<ScrollRect>("DecorationListUI","_scrollRect");
            purchasePopoverOpenedFrame=Time.frameCount;
            float scaleY=(float)Screen.height/1080f;
            popover.position=targetButton.position+new Vector3(0f,-34.5f*scaleY,0f);
            Show(popover.gameObject,true);popover.SetAsLastSibling();
            var purchaseBtn=popover.Find("PurchaseButton");
            var btnCg=purchaseBtn?.GetComponent<CanvasGroup>();
            if(btnCg){btnCg.alpha=canPurchase?1f:.5f;btnCg.interactable=btnCg.blocksRaycasts=canPurchase;}
            if(purchaseBtn)
            {
                Bind(purchaseBtn.gameObject,()=>{if(!story.Purchase("decoration",skin.name))return;ClosePurchasePopover();story.ChooseDecoration(skin.name);RefreshDecorationControls();});
                purchaseBtn.GetComponent<Button>().interactable=canPurchase;
            }
            var closeBtn=popover.Find("CloseButtonBase/CloseButtonUI");
            if(closeBtn)Bind(closeBtn.gameObject,ClosePurchasePopover);
        }
        void CreateSpecialRows()
        {
            var scroll=RefOf("SpecialSelectListUI","_scrollRect")?.GetComponent<ScrollRect>();if(!scroll||!scroll.content)return;
            var sr=(RectTransform)scroll.transform;
            sr.anchorMin=new Vector2(.5f,0f);sr.anchorMax=new Vector2(.5f,1f);sr.pivot=new Vector2(.5f,1f);
            sr.anchoredPosition=new Vector2(0f,-93.42f);sr.sizeDelta=new Vector2(599.4f,-239.7f);
            NormalizeScrollViewport(scroll,-11.5f);
            var sbar=sr.Find("Scrollbar Vertical") as RectTransform;
            if(sbar){sbar.anchorMin=new Vector2(1f,0f);sbar.anchorMax=Vector2.one;sbar.pivot=Vector2.one;sbar.anchoredPosition=new Vector2(-9.9f,-7f);sbar.sizeDelta=new Vector2(14.5f,-7f);}
            var parent=scroll.content;
            var inProgressParent=RefOf("SpecialSelectListUI","_inProgressSpecialCellsParent")??parent.Find("InProgressSpecialsParent");
            var readParent=RefOf("SpecialSelectListUI","_readSpecialCellsParent")??parent.Find("ReadSpecialsParent");
            if(!inProgressParent||!readParent||specialCells==null||specialCells.Length<7)return;
            foreach(Transform child in parent)child.gameObject.SetActive(true);
            foreach(var tr in new[]{parent,inProgressParent,readParent})
            {
                var vlg=tr.GetComponent<VerticalLayoutGroup>();if(vlg)vlg.enabled=true;
                var csf=tr.GetComponent<ContentSizeFitter>();if(csf)csf.enabled=true;
            }
            var story=player.story;
            var allSpecial=story.SpecialChapters;
            var specs=new(int collab,int scenario,int maxEp,string titleKey)[]
            {
                (4,53,1,"special_lunaNewYear2026_title"),
                (5,54,1,"special_season_base_title"),
                (3,52,2,"special_valentine2026_title"),
                (7,56,4,"special_statisticsdev_title"),
                (6,55,2,"special_magicalprincess_title"),
                (1,50,2,"special_alterego_title"),
                (2,51,2,"special_bearsrestaurant_title")
            };
            foreach(var sp in specs)
            {
                var prefab=specialCells[sp.collab-1];if(!prefab)continue;
                int readCount=allSpecial.Count(c=>c.Scenario==sp.scenario&&story.ChapterCompleted(c.ID));
                bool inProgress=readCount<sp.maxEp;
                var targetParent=inProgress?inProgressParent:readParent;
                var go=Instantiate(prefab,targetParent,false);
                Show(go,true);FixSprites(go);CleanEvents(go);
                foreach(var a in go.GetComponentsInChildren<Animator>(true))a.enabled=false;
                SetText(go,"MainTitleText",story.UITranslate(sp.titleKey));
                var readIconParent=go.transform.Find("ReadIconParent");
                if(readIconParent&&specialReadIconPrefab)
                {
                    foreach(Transform old in readIconParent)Destroy(old.gameObject);
                    for(int i=0;i<sp.maxEp;i++)
                    {
                        var icon=Instantiate(specialReadIconPrefab,readIconParent,false);
                        Show(icon,true);FixSprites(icon);
                        Set(icon.transform.Find("NormalImage"),i>=readCount);
                        Set(icon.transform.Find("ReadImage"),i<readCount);
                        specialRows.Add(icon);
                    }
                }
                var progBar=go.transform.Find("Progress/ProgressBarImage") as RectTransform;
                if(progBar)
                {
                    float p01=!inProgress?1f:story.Workspace.collaboration==sp.collab?Mathf.Clamp01((float)(story.DisplayExp/Mathf.Max(1f,story.DisplayRequiredExp))):Mathf.Clamp01((float)readCount/Mathf.Max(1,sp.maxEp));
                    progBar.sizeDelta=new Vector2(406f*p01,progBar.sizeDelta.y);
                }
                bool activeCollab=inProgress&&story.Workspace.collaboration==sp.collab;
                Set(go.transform.Find("BackUseImage"),activeCollab);
                Set(go.transform.Find("SpecialIcon/ActiveImage"),activeCollab);
                Set(go.transform.Find("NewMark"),inProgress&&readCount==0);
                var btn=go.GetComponent<Button>();
                var inter=go.GetComponent<ChillNativeInteraction>();
                if(!inProgress)
                {
                    var titleTmp=go.transform.Find("MainTitleText")?.GetComponent<TMP_Text>();
                    if(titleTmp)titleTmp.color=new Color32(124,124,124,124);
                    Set(go.transform.Find("EndDate"),false);
                    Set(go.transform.Find("LimitedBg"),false);
                    if(btn)btn.interactable=false;
                    if(inter)inter.enabled=false;
                }
                else
                {
                    if(inter)inter.SetUsing(activeCollab);
                    int collabId=sp.collab;
                    Bind(go,()=>{
                        story.SelectCollaboration(story.Workspace.collaboration==collabId?0:collabId);
                        specialSignature=null;
                        RefreshNow();
                    });
                }
                specialRows.Add(go);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(inProgressParent as RectTransform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(readParent as RectTransform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition=1f;
            parent.anchoredPosition=new Vector2(parent.anchoredPosition.x,0f);
        }
    }
}
