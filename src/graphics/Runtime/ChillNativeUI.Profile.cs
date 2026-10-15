using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillNativeUI
    {
        const string BirthdayDialogPath="Paremt/PCPlatform/Canvas/UI/SetBirthdayDialog_PC";
        const string BirthdayConfirmPath="Paremt/PCPlatform/Canvas/UI/SetBirthdayConfirmDialog_PC";
        string avatarSignature;
        Sprite customAvatar;
        Texture2D customAvatarTexture;
        GameObject birthdayDialog;
        TMP_InputField birthdayMonthInput,birthdayDayInput;
        int pendingBirthdayMonth,pendingBirthdayDay;
        bool birthdayConfirmChecked;

        public bool IsBirthdaySet()=>player?.story?.Settings!=null&&player.story.Settings.birthdayMonth>0&&player.story.Settings.birthdayDay>0;
        public void CloseBirthdayDialogs()
        {
            Set(Find(BirthdayDialogPath),false);
            Set(Find(BirthdayConfirmPath),false);
        }
        public void OpenBirthdayDialog()
        {
            if(IsBirthdaySet())return;
            var dialog=Find(BirthdayDialogPath);if(!dialog)return;
            var s=player.story.Settings;
            if(birthdayMonthInput)birthdayMonthInput.SetTextWithoutNotify(s.birthdayMonth>0?s.birthdayMonth.ToString():"");
            if(birthdayDayInput)birthdayDayInput.SetTextWithoutNotify(s.birthdayDay>0?s.birthdayDay.ToString():"");
            UpdateBirthdayInputValidation();
            Set(dialog.Find("Content/BirthdayWarning"),false);
            Set(Find(BirthdayConfirmPath),false);
            Show(dialog.gameObject,true);
            dialog.SetAsLastSibling();
        }
        void InitializeBirthdayDialogs()
        {
            var dialog=Find(BirthdayDialogPath);
            var confirm=Find(BirthdayConfirmPath);
            Set(dialog,false);
            Set(confirm,false);
            if(dialog)
            {
                var bodText=dialog.Find("Content/BodText")?.GetComponent<TMP_Text>();
                if(bodText)localized.Remove(bodText);
                birthdayMonthInput=dialog.Find("Content/BirthdayInputs/MonthInputField")?.GetComponent<TMP_InputField>();
                birthdayDayInput=dialog.Find("Content/BirthdayInputs/DayInputField")?.GetComponent<TMP_InputField>();
                foreach(var input in new[]{birthdayMonthInput,birthdayDayInput})
                {
                    if(!input)continue;
                    input.readOnly=false;input.characterLimit=2;
                    input.contentType=TMP_InputField.ContentType.IntegerNumber;
                    input.onValueChanged.RemoveAllListeners();
                    input.onValueChanged.AddListener(_=>UpdateBirthdayInputValidation());
                }
                var cancel=dialog.Find("Content/Buttons/CancelButton");
                if(cancel)Bind(cancel.gameObject,CloseBirthdayDialogs);
                var close=dialog.Find("Content/Title/CloseButton");
                if(close)Bind(close.gameObject,CloseBirthdayDialogs);
                var register=dialog.Find("Content/Buttons/RegisterButton");
                if(register)
                {
                    Bind(register.gameObject,()=>{
                        if(!TryParseBirthdayInputs(out int m,out int d))
                        {
                            Set(dialog.Find("Content/BirthdayWarning"),true);
                            return;
                        }
                        pendingBirthdayMonth=m;pendingBirthdayDay=d;
                        OpenBirthdayConfirmDialog();
                    });
                }
            }
            if(confirm)
            {
                var bodText=confirm.Find("Content/BodText")?.GetComponent<TMP_Text>();
                if(bodText)localized.Remove(bodText);
                var cancel=confirm.Find("Content/Buttons/CancelButton");
                if(cancel)Bind(cancel.gameObject,()=>{Set(confirm,false);});
                var close=confirm.Find("Content/Title/CloseButton");
                if(close)Bind(close.gameObject,CloseBirthdayDialogs);
                var toggleRoot=confirm.Find("Content/ConfirmationRow/ConfirmToggle");
                if(toggleRoot)
                {
                    Bind(toggleRoot.gameObject,()=>{
                        birthdayConfirmChecked=!birthdayConfirmChecked;
                        UpdateBirthdayConfirmState();
                    });
                }
                var register=confirm.Find("Content/Buttons/RegisterButton");
                if(register)
                {
                    Bind(register.gameObject,()=>{
                        if(!birthdayConfirmChecked)return;
                        player.story.Settings.birthdayMonth=pendingBirthdayMonth;
                        player.story.Settings.birthdayDay=pendingBirthdayDay;
                        player.story.CommitSettings();
                        CloseBirthdayDialogs();
                        RefreshProfileControls();
                    });
                }
            }
        }
        bool TryParseBirthdayInputs(out int month,out int day)
        {
            month=0;day=0;
            if(!birthdayMonthInput||!birthdayDayInput)return false;
            if(!int.TryParse(birthdayMonthInput.text,out month)||!int.TryParse(birthdayDayInput.text,out day))return false;
            return month>=1&&month<=12&&day>=1&&day<=DateTime.DaysInMonth(2004,month);
        }
        void UpdateBirthdayInputValidation()
        {
            var dialog=Find(BirthdayDialogPath);if(!dialog)return;
            bool hasInput=!string.IsNullOrEmpty(birthdayMonthInput?.text)||!string.IsNullOrEmpty(birthdayDayInput?.text);
            bool valid=TryParseBirthdayInputs(out int m,out int d);
            Set(dialog.Find("Content/BirthdayWarning"),hasInput&&!valid);
            var bodText=dialog.Find("Content/BodText")?.GetComponent<TMP_Text>();
            if(bodText)
            {
                if(valid)
                {
                    string tpl=player.story.UITranslate("ui_profile_birthday_setting_comfirm");
                    try{bodText.text=string.Format(tpl,m,d);}
                    catch{bodText.text=m+"/"+d;}
                }
                else bodText.text=player.story.UITranslate("ui_profile_birthday_setting_body");
            }
        }
        void OpenBirthdayConfirmDialog()
        {
            var confirm=Find(BirthdayConfirmPath);if(!confirm)return;
            birthdayConfirmChecked=false;
            var body=confirm.Find("Content/BodText")?.GetComponent<TMP_Text>();
            if(body)
            {
                string template=player.story.UITranslate("ui_profile_birthday_confirm_body");
                try{body.text=string.Format(template,pendingBirthdayMonth,pendingBirthdayDay);}
                catch{body.text=pendingBirthdayMonth.ToString("00")+"/"+pendingBirthdayDay.ToString("00");}
            }
            UpdateBirthdayConfirmState();
            Show(confirm.gameObject,true);
            confirm.SetAsLastSibling();
        }
        void UpdateBirthdayConfirmState()
        {
            var confirm=Find(BirthdayConfirmPath);if(!confirm)return;
            Set(confirm.Find("Content/ConfirmationRow/ConfirmToggle/Background/Checkmark"),birthdayConfirmChecked);
            var regBtn=confirm.Find("Content/Buttons/RegisterButton")?.GetComponent<Button>();
            if(regBtn)regBtn.interactable=birthdayConfirmChecked;
        }
        void InitializeProfileControls()
        {
            var story=player.story;
            InitializeBirthdayDialogs();
            Wire("FacilityProfile","_playerIconButton",()=>Open("profile"));Wire("FacilityProfile","_heroineIconButton",()=>Open("heroine-profile"));
            Wire("PlayerProfileView","_iconSettingButton",()=>ProfilePopup("ProfileIconSettingView"));Wire("PlayerProfileView","_statusChangeButton",()=>ProfilePopup("StatusChangeView"));
            Wire("ProfileIconSettingView","_closeButton",()=>Set(RefOf("ProfileIconSettingView","self"),false));Wire("ProfileIconSettingView","_customIconButton",()=>ChillBrowser.ImportAvatar(story.gameObject.name));
            Wire("PlayerProfileView","_birthdaySettingButton",OpenBirthdayDialog);
            Wire("HeroineProfileView","_closeButton",CloseExclusivePane);Wire("HeroineProfileView","_selfTalkFilterOnButton",()=>{story.Settings.selfTalk=false;story.CommitSettings();});Wire("HeroineProfileView","_selfTalkFilterOffButton",()=>{story.Settings.selfTalk=true;story.CommitSettings();});
            Wire("HeroineProfileView","_audioInitButton",()=>{story.Settings.voice=story.Settings.ambient=1;story.Settings.selfTalk=true;story.CommitSettings();});
            foreach(var field in new[]{"_voiceVolumeChangeUI","_ambientSeVolumeChangeUI"}){
                bool voice=field.Contains("voice");var root=RefOf("HeroineProfileView",field);
                if(root)
                {
                    var texts=root.GetComponentsInChildren<TMP_Text>(true);
                    var current=texts.FirstOrDefault(t=>t.text.Contains("%"))??texts.OrderBy(t=>t.transform.position.y).FirstOrDefault();
                    var title=texts.FirstOrDefault(t=>t!=current);
                    if(title)
                    {
                        title.name="HeroineVolumeTitle";
                        string key=voice?"ui_setting_audio_voice":"ui_setting_audio_ambient_se";
                        localized[title]=key;
                        title.text=story.UITranslate(key);
                    }
                    if(current)current.name="CurrentVolumeText";
                }
                var slider=root?.GetComponentInChildren<Slider>(true);if(slider){slider.minValue=0;slider.maxValue=1;slider.onValueChanged.AddListener(value=>{if(voice)story.Settings.voice=value;else story.Settings.ambient=value;story.CommitSettings();});featureSliders.Add(Tuple.Create(slider,(Func<float>)(()=>voice?story.Settings.voice:story.Settings.ambient)));}
                var mute=root?.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name.Contains("Mute"));if(mute)Bind(mute.gameObject,()=>{if(voice)story.Settings.voice=story.Settings.voice==0?1:0;else story.Settings.ambient=story.Settings.ambient==0?1:0;story.CommitSettings();});
            }
        }
        void ProfilePopup(string script)
        {
            foreach(string other in new[]{"ProfileIconSettingView","StatusChangeView"})Set(RefOf(other,"self"),false);
            var root=RefOf(script,"self");if(!root)return;Show(root.gameObject,true);root.SetAsLastSibling();
        }
        void RefreshProfileControls()
        {
            var story=player.story;
            bool connLost=story.ConnectionLost;
            Set(RefOf("FacilityProfile","_heroineIconButton"),!connLost);
            if(connLost&&IsPaneOpen("heroine-profile"))CloseExclusivePane();
            bool overlap=IsPaneOpen("story")||IsPaneOpen("decorations")||IsPaneOpen("environment")||IsPaneOpen("music")||IsPaneOpen("special")||IsPaneOpen("history");
            var profileGroup=RefOf("FacilityProfile","_canvasGroup")?.GetComponent<CanvasGroup>();
            if(profileGroup){profileGroup.alpha=overlap?0f:1f;profileGroup.interactable=profileGroup.blocksRaycasts=!overlap;}
            var dateRoot=RefOf("FacilityCurrentDateAndTime","_canvasGroup")??Find("Paremt/PCPlatform/Canvas/UI/UI_FacilityCurrentDateAndTime");
            if(dateRoot){var dateGroup=dateRoot.GetComponent<CanvasGroup>()??dateRoot.gameObject.AddComponent<CanvasGroup>();dateGroup.alpha=overlap?0f:1f;dateGroup.interactable=dateGroup.blocksRaycasts=!overlap;}
            if(!story.FeatureReady)return;
            if(Find(BirthdayDialogPath)?.gameObject.activeSelf==true)UpdateBirthdayInputValidation();
            bool bdaySet=IsBirthdaySet();
            Write("PlayerProfileView","_birthdayText",bdaySet?story.Settings.birthdayMonth.ToString("00")+"/"+story.Settings.birthdayDay.ToString("00"):"--/--");
            var bdayBtn=RefOf("PlayerProfileView","_birthdaySettingButton");
            if(bdayBtn)
            {
                var btn=bdayBtn.GetComponent<Button>();if(btn)btn.interactable=!bdaySet;
                var inter=bdayBtn.GetComponent<ChillNativeInteraction>();if(inter)inter.enabled=!bdaySet;
            }
            Write("HeroineProfileView","heroineBirthdayText",state?.language=="Pt"||state?.language=="Ru"?"11/12":"12/11");
            bool filtering=!story.Settings.selfTalk;
            Get<ChillNativeInteraction>("HeroineProfileView","_selfTalkFilterOnInteractableUI")?.SetUsing(filtering);
            Get<ChillNativeInteraction>("HeroineProfileView","_selfTalkFilterOffInteractableUI")?.SetUsing(!filtering);
            if(avatarSignature==story.Workspace.profileIcon)return;avatarSignature=story.Workspace.profileIcon;
            Sprite selected=null;
            if(avatarSignature.StartsWith("data:image/png;base64,")){
                try{var tex=new Texture2D(2,2);if(tex.LoadImage(Convert.FromBase64String(avatarSignature.Substring(22)))){if(customAvatar)Destroy(customAvatar);if(customAvatarTexture)Destroy(customAvatarTexture);customAvatarTexture=tex;customAvatar=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,.5f));selected=customAvatar;}else Destroy(tex);}catch(Exception){story.Notify("Could not read saved avatar.",true);}
            }else{int id=0;if(avatarSignature.StartsWith("preset:"))int.TryParse(avatarSignature.Substring(7),out id);var choice=story.Features.profileButtons.FirstOrDefault(p=>p.id==id);selected=choice==null?null:Find(choice.image)?.GetComponent<Image>()?.sprite;}
            if(selected){foreach(string field in new[]{"_playerIconButton"}){var button=RefOf("FacilityProfile",field);var buttonImage=button?.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.name=="IconImage");if(buttonImage)buttonImage.sprite=selected;}var image=RefOf("PlayerProfileView","_iconSettingButton")?.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.name=="IconImage");if(image)image.sprite=selected;}
        }
    }
}
