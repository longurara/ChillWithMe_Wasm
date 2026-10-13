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
        bool completionReady,timerAudioCountup;
        Transform skinPopup,skinContent;GameObject timerAudioPane;
        readonly List<GameObject> skinRows=new List<GameObject>(),specialRows=new List<GameObject>();
        TMP_InputField roomPresetName,profileName,profileStatus;
        string selectedRoomPreset="",specialSignature;
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
            var nativeSound=RefOf("TimerSoundSettingView","self");Transform soundRoot=nativeSound;
            if(nativeSound){var original=(RectTransform)nativeSound;float width=original.rect.width,height=original.rect.height;timerAudioPane=new GameObject("WebTimerAudioWindow",typeof(RectTransform),typeof(Image));var rect=(RectTransform)timerAudioPane.transform;rect.SetParent(original.parent,false);rect.anchorMin=original.anchorMin;rect.anchorMax=original.anchorMax;rect.pivot=original.pivot;rect.anchoredPosition=original.anchoredPosition;rect.sizeDelta=new Vector2(width,height+160);timerAudioPane.GetComponent<Image>().color=new Color(.05f,.04f,.1f,.97f);original.SetParent(rect,false);original.anchorMin=original.anchorMax=new Vector2(.5f,1);original.pivot=new Vector2(.5f,1);original.anchoredPosition=Vector2.zero;original.sizeDelta=new Vector2(width,height);soundRoot=rect;timerAudioPane.SetActive(false);}
            if(soundRoot){FeatureHook(FeatureButton(soundRoot,"timerVoice","Timer voice",-490,300),()=>{var audio=story.TimerAudio(timerAudioCountup);audio.voice=!audio.voice;story.SaveTimerAudio();});FeatureHook(FeatureButton(soundRoot,"closeTimerAudio","Close",-555,200),Close);}
            Wire("DecorationListUI","_facilityOpenButton",()=>Open("decorations"));Wire("DecorationListUI","_closeButton",Close);Wire("DecorationListUI","_hideButton",ToggleHidden);Set(RefOf("DecorationListUI","_purchasePopover"),false);
            var root=RefOf("DecorationListUI","self");
            Set(RefOf("DecorationPresetUI","self"),false);
            if(root)
            {
                FeatureHook(FeatureButton(root,"roomReset","Reset room",-72,140,-245),story.ResetRoom);
                FeatureHook(FeatureButton(root,"hair","Hair",-72,140,-85),()=>story.ChooseHair((story.Workspace.hair+1)%4));FeatureHook(FeatureButton(root,"costume","Costume",-72,140,85),()=>story.ChooseCostume((story.Workspace.costume+1)%5));
                roomPresetName=FeatureInput(root,"roomPresetName","Preset name",-135,220,-165);if(roomPresetName)roomPresetName.characterLimit=80;
                FeatureHook(FeatureButton(root,"saveRoomPreset","Save preset",-135,150,50),()=>{story.SaveRoomPreset(roomPresetName?.text??"");selectedRoomPreset=roomPresetName?.text??"";});
                FeatureHook(FeatureButton(root,"loadRoomPreset","Choose preset",-135,190,230),()=>{var names=story.Workspace.presets.Select(p=>p.name).ToArray();if(names.Length==0)return;selectedRoomPreset=names[(Array.IndexOf(names,selectedRoomPreset)+1)%names.Length];story.LoadRoomPreset(selectedRoomPreset);});
                FeatureHook(FeatureButton(root,"deleteRoomPreset","Delete preset",-72,150,245),()=>story.DeleteRoomPreset(selectedRoomPreset));
                var scroll=Get<ScrollRect>("DecorationListUI","_scrollRect");if(scroll){var rect=(RectTransform)scroll.transform;rect.offsetMax=new Vector2(rect.offsetMax.x,-275);}
                PreparePane(root);
            }
            Wire("PlayerProfileView","_closeButton",Close);
            var profile=RefOf("PlayerProfileView","self");if(profile){ConfigurePlayerProfile(profile);profileName=FeatureInput(profile,"profileName","Player",-145,360);profileStatus=FeatureInput(profile,"profileStatus","",-210,260,-50);if(profileName)profileName.onEndEdit.AddListener(value=>{story.Workspace.profileName=value;story.SaveTimerAudio();});if(profileStatus)profileStatus.onEndEdit.AddListener(value=>{story.Workspace.profileStatus=value;story.SaveTimerAudio();});FeatureHook(FeatureButton(profile,"achievements","Achievements",-280,360),()=>Open("achievements"));}
            foreach(var button in canvas.GetComponentsInChildren<Button>(true).Where(b=>b.name.Contains("PlayerProfile")&&PathOf(b.transform).Contains("LeftIcons")))Bind(button.gameObject,()=>Open("profile"));
            InitializeProfileControls();
            var special=RefOf("SpecialView","self");if(special)FeatureHook(FeatureButton(special,"normalProgress","Normal progression",-70,320),story.NormalProgression);
            Wire("SpecialView","_openSpecialListButton",()=>Open("special"));Wire("SpecialView","_closeButton",Close);
            Wire("HabitWeekLabelUI","_prevWeekButton",()=>{habitWeek=habitWeek.AddDays(-7);workspaceSignature=null;});Wire("HabitWeekLabelUI","_nextWeekButton",()=>{habitWeek=habitWeek.AddDays(7);workspaceSignature=null;});
            foreach(string name in new[]{"previousHabitWeek","nextHabitWeek"})if(featureButtons.TryGetValue(name,out var button)&&button)button.gameObject.SetActive(false);
        }
        void RefreshCompletionControls()
        {
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
            }
            RefreshProfileControls();
            if(pane=="timer-audio")RefreshTimerAudio();
            var point=RefOf("DecorationListUI","_ownPointView")?.GetComponentInChildren<TMP_Text>(true);if(point)point.text=story.Workspace.points.ToString();
            FeatureCaption("hair","Hair: "+story.Workspace.hair);FeatureCaption("costume","Costume: "+new[]{"Default","Polo 1","Polo 2","Tee 1","Tee 2"}[Mathf.Clamp(story.Workspace.costume,0,4)]);FeatureCaption("loadRoomPreset",selectedRoomPreset==""?"Choose preset":selectedRoomPreset);
            if(profileName&&!profileName.isFocused)profileName.SetTextWithoutNotify(story.Workspace.profileName);
            if(pane=="special"&&specialSignature!=story.WorkspaceRevision+":"+story.UISnapshot("story").completed){specialSignature=story.WorkspaceRevision+":"+story.UISnapshot("story").completed;Clear(specialRows);CreateSpecialRows();}
        }
        void RefreshTimerAudio()
        {
            var story=player.story;Set(RefOf("TimerSoundSettingView","_pomodoroSoundSettingParent"),!timerAudioCountup);Set(RefOf("TimerSoundSettingView","_countupSoundSettingParent"),timerAudioCountup);
            foreach(bool countup in new[]{false,true})foreach(bool finish in new[]{false,true}){string prefix="_"+(countup?"countup":"pomodoro")+(finish?"Finish":"Start");var option=story.TimerAudio(countup);var slider=Get<Slider>("TimerSoundSettingView",prefix+"VolumeSlider");if(slider)slider.SetValueWithoutNotify(finish?option.finishVolume:option.startVolume);Write("TimerSoundSettingView",prefix+"VolumeValueText",Mathf.RoundToInt((finish?option.finishVolume:option.startVolume)*100).ToString());var mute=RefOf("TimerSoundSettingView",prefix+"SoundMuteButton")?.GetComponent<Image>();if(mute)mute.color=(finish?option.finishMute:option.startMute)?new Color(.5f,.5f,.6f):Color.white;}
            FeatureCaption("timerVoice","Timer voice: "+(story.TimerAudio(timerAudioCountup).voice?"ON":"OFF"));
            foreach(var choice in story.Features.timerButtons??Array.Empty<ChillTimerButton>()){var option=story.TimerAudio(choice.path.Contains("CountupSoundSetting"));var image=Find(choice.path)?.GetComponent<Image>();if(image)image.color=choice.id==(choice.id<3?option.start:option.finish)?new Color(.3f,1,.9f):Color.white;}
        }
        void ShowSkinSelector(int model)
        {
            var root=RefOf("DecorationListUI","self");if(!root)return;var story=player.story;Clear(skinRows);
            if(!skinPopup){
                var go=new GameObject("WebSkinSelector",typeof(RectTransform),typeof(Image),typeof(ScrollRect));go.transform.SetParent(root,false);skinPopup=go.transform;var rect=(RectTransform)skinPopup;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(400,520);rect.pivot=new Vector2(.5f,.5f);go.GetComponent<Image>().color=new Color(.1f,.09f,.18f,.98f);
                var viewport=new GameObject("Viewport",typeof(RectTransform),typeof(RectMask2D));viewport.transform.SetParent(rect,false);var vr=(RectTransform)viewport.transform;vr.anchorMin=Vector2.zero;vr.anchorMax=Vector2.one;vr.offsetMin=new Vector2(10,10);vr.offsetMax=new Vector2(-10,-10);
                var content=new GameObject("Content",typeof(RectTransform),typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));content.transform.SetParent(vr,false);skinContent=content.transform;var cr=(RectTransform)content.transform;cr.anchorMin=new Vector2(0,1);cr.anchorMax=Vector2.one;cr.pivot=new Vector2(.5f,1);cr.sizeDelta=new Vector2(0,0);cr.anchoredPosition=Vector2.zero;
                var layout=content.GetComponent<VerticalLayoutGroup>();layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.spacing=5;content.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;var scroll=go.GetComponent<ScrollRect>();scroll.viewport=vr;scroll.content=cr;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            }
            Show(skinPopup.gameObject,true);skinPopup.SetAsLastSibling();
            var template=RefOf("ItemSkinSelectorUI","_itemButtonOriginal");
            foreach(var skin in story.decorations.Skins.Where(s=>s.model==model)){
                if(!template)continue;var go=Cell(template.gameObject,skinContent);var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(380,70);var element=go.GetComponent<LayoutElement>();element.preferredHeight=70;
                var colors=story.Features.skinColors.FirstOrDefault(c=>c.id==skin.id);if(colors!=null){var upper=go.transform.Find("MainButton/Back Upper")?.GetComponent<Image>();var lower=go.transform.Find("MainButton/Back Lower")?.GetComponent<Image>();if(upper)upper.color=colors.upper;if(lower)lower.color=colors.lower;}
                bool unlocked=story.ItemAvailable("decoration",skin.name);var main=go.transform.Find("MainButton");var shop=go.transform.Find("ShopButton");Show(main?.gameObject,true);Show(shop?.gameObject,!unlocked);Set(go.transform.Find("NewImage"),false);
                void Select(){if(!story.Purchase("decoration",skin.name))return;story.ChooseDecoration(skin.name);skinPopup.gameObject.SetActive(false);}
                if(main)Bind(main.gameObject,Select);if(shop)Bind(shop.gameObject,Select);
                var labelGo=new GameObject("SkinLabel",typeof(RectTransform),typeof(TextMeshProUGUI));labelGo.transform.SetParent(go.transform,false);var lr=(RectTransform)labelGo.transform;lr.anchorMin=new Vector2(.3f,0);lr.anchorMax=Vector2.one;lr.offsetMin=new Vector2(5,0);lr.offsetMax=new Vector2(-10,0);var label=labelGo.GetComponent<TextMeshProUGUI>();label.font=profileName?profileName.textComponent.font:fallbackFont;label.fontSize=20;label.color=Color.white;label.raycastTarget=false;label.text=skin.name+(unlocked?"":" · "+story.ItemCondition("decoration",skin.name));label.enableAutoSizing=true;label.fontSizeMin=12;label.fontSizeMax=20;skinRows.Add(go);
            }
            var reset=FeatureButton(skinContent,"resetCategory","Use default",0,380);if(reset){reset.gameObject.AddComponent<LayoutElement>().preferredHeight=48;FeatureHook(reset,()=>{var item=story.decorations.Skins.FirstOrDefault(s=>s.model==model);if(item!=null)story.ClearDecoration(item.category);skinPopup.gameObject.SetActive(false);});skinRows.Add(reset.gameObject);}
            var close=FeatureButton(skinContent,"closeSkin","Close",0,380);if(close){close.gameObject.AddComponent<LayoutElement>().preferredHeight=48;FeatureHook(close,()=>skinPopup.gameObject.SetActive(false));skinRows.Add(close.gameObject);}
        }
        void CreateSpecialRows()
        {
            var scroll=RefOf("SpecialSelectListUI","_scrollRect")?.GetComponent<ScrollRect>();if(!scroll||!scroll.content||!storyCell)return;var sr=(RectTransform)scroll.transform;sr.offsetMax=new Vector2(sr.offsetMax.x,-145);var parent=scroll.content;foreach(Transform child in parent)child.gameObject.SetActive(false);Layout(parent,105);
            foreach(var chapter in player.story.SpecialChapters){var go=Cell(storyCell,parent);SetText(go,"MainTitleText",player.story.UITranslate(chapter.TitleLocalizationID));SetText(go,"SubTitleText",chapter.ID+(player.story.ChapterAvailable(chapter)?"":" · Work to unlock this episode"));foreach(var button in go.GetComponentsInChildren<Button>(true)){Bind(button.gameObject,()=>{Action("story-start",id:chapter.ID);Close();});button.interactable=player.story.ChapterAvailable(chapter);}specialRows.Add(go);}
        }
    }
}
