using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillNativeUI
    {
        string avatarSignature;
        Sprite customAvatar;
        Texture2D customAvatarTexture;
        void ConfigurePlayerProfile(Transform root)
        {
            var rect=(RectTransform)root;rect.sizeDelta=new Vector2(450,440);
            var background=root.GetComponent<Image>();if(!background)background=root.gameObject.AddComponent<Image>();background.color=new Color(.08f,.07f,.15f,.97f);
            void Place(string field,Vector2 position,Vector2 size){var button=RefOf("PlayerProfileView",field) as RectTransform;if(!button)return;button.anchorMin=button.anchorMax=new Vector2(.5f,1);button.pivot=new Vector2(.5f,1);button.anchoredPosition=position;button.sizeDelta=size;}
            Place("_iconSettingButton",new Vector2(-150,-30),new Vector2(90,90));
            Place("_birthdaySettingButton",new Vector2(45,-35),new Vector2(120,65));
            Place("_statusChangeButton",new Vector2(155,-210),new Vector2(60,48));
            var birthdayIcon=RefOf("PlayerProfileView","_birthdaySettingButton")?.GetComponent<Image>();if(birthdayIcon)birthdayIcon.preserveAspect=true;
            var birthday=Get<TMP_Text>("PlayerProfileView","_birthdayText");if(birthday){birthday.enableAutoSizing=true;birthday.fontSizeMin=16;birthday.fontSizeMax=24;birthday.overflowMode=TextOverflowModes.Ellipsis;var labelRect=(RectTransform)birthday.transform;labelRect.anchorMin=labelRect.anchorMax=new Vector2(.5f,0);labelRect.pivot=new Vector2(.5f,1);labelRect.anchoredPosition=Vector2.zero;labelRect.sizeDelta=new Vector2(180,32);}
            var close=RefOf("PlayerProfileView","_closeButton") as RectTransform;if(close){close.anchorMin=close.anchorMax=Vector2.one;close.pivot=Vector2.one;close.anchoredPosition=new Vector2(-12,-12);close.sizeDelta=new Vector2(35,35);}
        }
        void InitializeProfileControls()
        {
            var story=player.story;
            Wire("FacilityProfile","_playerIconButton",()=>Open("profile"));Wire("FacilityProfile","_heroineIconButton",()=>Open("heroine-profile"));
            Wire("PlayerProfileView","_iconSettingButton",()=>ProfilePopup("ProfileIconSettingView"));Wire("PlayerProfileView","_statusChangeButton",()=>ProfilePopup("StatusChangeView"));
            Wire("ProfileIconSettingView","_closeButton",()=>Set(RefOf("ProfileIconSettingView","self"),false));Wire("ProfileIconSettingView","_customIconButton",()=>ChillBrowser.ImportAvatar(story.gameObject.name));
            Wire("PlayerProfileView","_birthdaySettingButton",()=>{
                var root=RefOf("PlayerProfileView","self");var old=root.Find("WebFeature_profileBirthday");if(old){Show(old.gameObject,!old.gameObject.activeSelf);return;}
                var input=FeatureInput(root,"profileBirthday",story.Settings.birthdayMonth.ToString("00")+"-"+story.Settings.birthdayDay.ToString("00"),-105,230);
                input.characterLimit=5;input.onEndEdit.AddListener(value=>{var parts=value.Split('-');if(parts.Length==2&&int.TryParse(parts[0],out int m)&&int.TryParse(parts[1],out int d)&&m>=1&&m<=12&&d>=1&&d<=DateTime.DaysInMonth(2000,m)){story.Settings.birthdayMonth=m;story.Settings.birthdayDay=d;story.CommitSettings();Show(input.gameObject,false);}else story.Notify("Use MM-DD.",true);});
            });
            Wire("HeroineProfileView","_closeButton",Close);Wire("HeroineProfileView","_selfTalkFilterOnButton",()=>{story.Settings.selfTalk=false;story.CommitSettings();});Wire("HeroineProfileView","_selfTalkFilterOffButton",()=>{story.Settings.selfTalk=true;story.CommitSettings();});
            Wire("HeroineProfileView","_audioInitButton",()=>{story.Settings.voice=story.Settings.ambient=1;story.Settings.selfTalk=true;story.CommitSettings();});
            foreach(var field in new[]{"_voiceVolumeChangeUI","_ambientSeVolumeChangeUI"}){
                bool voice=field.Contains("voice");var root=RefOf("HeroineProfileView",field);var slider=root?.GetComponentInChildren<Slider>(true);if(slider){slider.minValue=0;slider.maxValue=1;slider.onValueChanged.AddListener(value=>{if(voice)story.Settings.voice=value;else story.Settings.ambient=value;story.CommitSettings();});featureSliders.Add(Tuple.Create(slider,(Func<float>)(()=>voice?story.Settings.voice:story.Settings.ambient)));}
                var mute=root?.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name.Contains("Mute"));if(mute)Bind(mute.gameObject,()=>{if(voice)story.Settings.voice=story.Settings.voice==0?1:0;else story.Settings.ambient=story.Settings.ambient==0?1:0;story.CommitSettings();});
            }
            var decor=RefOf("DecorationListUI","self");if(decor)FeatureHook(FeatureButton(decor,"dailyAppearance","Daily appearance: OFF",-195,320),()=>{story.Workspace.appearanceDaily=!story.Workspace.appearanceDaily;story.SaveTimerAudio();});
            var profile=RefOf("PlayerProfileView","self");if(profile){foreach(var t in profile.GetComponentsInChildren<TMP_Text>(true))if(t.text=="ui_note_placeholder")t.gameObject.SetActive(false);FeatureHook(FeatureButton(profile,"reminders","Reminders",-350,360),()=>{story.Workspace.reminders=!story.Workspace.reminders;if(story.Workspace.reminders)ChillBrowser.RequestNotifications();story.SaveTimerAudio();});}if(profileStatus)profileStatus.readOnly=true;
        }
        void ProfilePopup(string script)
        {
            foreach(string other in new[]{"ProfileIconSettingView","StatusChangeView"})Set(RefOf(other,"self"),false);
            var root=RefOf(script,"self");if(!root)return;Show(root.gameObject,true);root.SetAsLastSibling();
        }
        void RefreshProfileControls()
        {
            var story=player.story;FeatureCaption("reminders","Reminders: "+(story.Workspace.reminders?"ON":"OFF"));FeatureCaption("dailyAppearance","Daily appearance: "+(story.Workspace.appearanceDaily?"ON":"OFF"));
            Write("PlayerProfileView","_birthdayText",story.Settings.birthdayMonth>0?story.Settings.birthdayMonth.ToString("00")+"/"+story.Settings.birthdayDay.ToString("00"):"Birthday");
            if(profileStatus&&!profileStatus.isFocused)profileStatus.SetTextWithoutNotify(story.Workspace.profileStatus=="1"?"Away":story.Workspace.profileStatus=="2"?"Super Focus":"Active");
            if(avatarSignature==story.Workspace.profileIcon)return;avatarSignature=story.Workspace.profileIcon;
            Sprite selected=null;
            if(avatarSignature.StartsWith("data:image/png;base64,")){
                try{var tex=new Texture2D(2,2);if(tex.LoadImage(Convert.FromBase64String(avatarSignature.Substring(22)))){if(customAvatar)Destroy(customAvatar);if(customAvatarTexture)Destroy(customAvatarTexture);customAvatarTexture=tex;customAvatar=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,.5f));selected=customAvatar;}else Destroy(tex);}catch(Exception){story.Notify("Could not read saved avatar.",true);}
            }else{int id=0;if(avatarSignature.StartsWith("preset:"))int.TryParse(avatarSignature.Substring(7),out id);var choice=story.Features.profileButtons.FirstOrDefault(p=>p.id==id);selected=choice==null?null:Find(choice.image)?.GetComponent<Image>()?.sprite;}
            if(selected){foreach(string field in new[]{"_playerIconButton"}){var button=RefOf("FacilityProfile",field);var buttonImage=button?.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.name=="IconImage");if(buttonImage)buttonImage.sprite=selected;}var image=RefOf("PlayerProfileView","_iconSettingButton")?.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.name=="IconImage");if(image)image.sprite=selected;}
        }
    }
}
