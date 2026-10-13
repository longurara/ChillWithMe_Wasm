using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillNativeUI
    {
        static void PlaceRect(RectTransform rect,float x,float y,float width,float height)
        {if(!rect)return;rect.localScale=Vector3.one;rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(width,height);}
        static void DisableLayouts(Transform root)
        {foreach(var l in root.GetComponents<LayoutGroup>())l.enabled=false;foreach(var f in root.GetComponents<ContentSizeFitter>())f.enabled=false;}
        void PlaceFeature(string key,float x,float y,float width)
        {if(featureButtons.TryGetValue(key,out var button))PlaceRect(button.transform as RectTransform,x,y,width,48);}
        void RepairPaneLayouts()
        {
            var settings=RefOf("SettingUI","self");
            foreach(string name in new[]{"General","Graphics","MusicAudio"})
            {
                var parent=settings?settings.Find(name) as RectTransform:null;if(!parent)continue;
                parent.anchorMin=Vector2.zero;parent.anchorMax=Vector2.one;parent.offsetMin=parent.offsetMax=Vector2.zero;
                var title=parent.Find("Title") as RectTransform;if(title){title.anchorMin=title.anchorMax=new Vector2(0,1);title.pivot=new Vector2(0,1);title.anchoredPosition=new Vector2(210,-130);title.sizeDelta=new Vector2(740,55);}
                if(title&&title.GetComponent<TMP_Text>() is TMP_Text titleText)titleText.alignment=TextAlignmentOptions.MidlineLeft;
                var scroll=parent.Find("ScrollView") as RectTransform;
                if(scroll){scroll.anchorMin=Vector2.zero;scroll.anchorMax=Vector2.one;scroll.offsetMin=new Vector2(205,105);scroll.offsetMax=new Vector2(-35,-200);}
            }
            var audio=RefOf("SettingUI","_audioParent");var content=audio?audio.Find("ScrollView/Viewport/Content"):null;
            if(content)
            {
                DisableLayouts(content);
                var rows=content.Cast<Transform>().Where(t=>t.GetComponentInChildren<Slider>(true)).ToArray();
                for(int i=0;i<rows.Length;i++)RepairAudioRow(rows[i],-20-i*90);
                var r=(RectTransform)content;r.sizeDelta=new Vector2(r.sizeDelta.x,rows.Length*90+30);
            }
            if(settings)
            {
                foreach(var label in settings.GetComponentsInChildren<TMP_Text>(true))if(label.name=="AppVersionText")label.text="Ver "+Application.version;
                var reset=settings.Find("Graphics/GraphicsInitButton");if(!reset)reset=settings.Find("Graphics/GraphicInitButton");Set(reset,false);
            }
            var decor=RefOf("DecorationListUI","self");
            if(decor)
            {
                Set(decor.Find("LayoutPresetUI"),false);Set(decor.Find("BackLine (1)"),false);
                PlaceFeature("roomReset",-185,-100,175);PlaceFeature("hair",0,-100,175);PlaceFeature("costume",185,-100,175);
                PlaceRect(roomPresetName?.transform as RectTransform,-145,-160,275,48);PlaceFeature("saveRoomPreset",145,-160,245);
                PlaceFeature("loadRoomPreset",-145,-220,275);PlaceFeature("deleteRoomPreset",145,-220,245);PlaceFeature("dailyAppearance",0,-280,420);
                var scroll=Get<ScrollRect>("DecorationListUI","_scrollRect");if(scroll){var r=(RectTransform)scroll.transform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(15,140);r.offsetMax=new Vector2(-15,-350);}
            }
            var edit=RefOf("HabitListUI","_editModeButton");if(edit){Set(edit.Find("Buttons"),false);Set(edit.Find("IconActive"),false);var text=edit.GetComponentInChildren<TMP_Text>(true);if(text){text.enableAutoSizing=true;text.fontSizeMin=14;text.fontSizeMax=22;}}
            RepairTodoLayout();
            if(noticePanel)
            {
                var graphic=noticePanel.GetComponent<Graphic>();if(graphic)graphic.raycastTarget=true;
                Bind(noticePanel,()=>{noticeUntil=0;Show(noticePanel,false);});
            }
            // FacilityCall invokes the native game-end conversation before exiting.
            var phone=canvas.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="IconExit_Button");
            if(phone)Bind(phone.gameObject,()=>{Close();player.story.Farewell();});
        }
        void RepairAudioRow(Transform row,float y)
        {
            DisableLayouts(row);PlaceRect(row as RectTransform,0,y,850,68);
            var labels=row.GetComponentsInChildren<TMP_Text>(true);
            var title=labels.FirstOrDefault(t=>t.text.StartsWith("ui_")||t.name.Contains("Title"))??labels.FirstOrDefault();
            if(row.name=="WebVoiceVolume"&&title)title.text="Voice";
            var number=labels.FirstOrDefault(t=>t!=title);
            if(title){title.transform.SetParent(row,false);title.name="WebVolumeTitle";PlaceRect((RectTransform)title.transform,-310,-12,220,42);title.alignment=TextAlignmentOptions.MidlineLeft;title.enableAutoSizing=true;title.fontSizeMin=16;title.fontSizeMax=26;}
            if(number){number.transform.SetParent(row,false);number.name="CurrentVolumeText";PlaceRect((RectTransform)number.transform,-150,-12,60,42);number.alignment=TextAlignmentOptions.MidlineRight;}
            var slider=row.GetComponentInChildren<Slider>(true);if(slider){slider.transform.SetParent(row,false);PlaceRect((RectTransform)slider.transform,150,-28,450,22);}
            var mute=row.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name.Contains("Mute")||b.name.Contains("AudioButton"));if(mute){mute.transform.SetParent(row,false);PlaceRect((RectTransform)mute.transform,-205,-14,38,38);}
        }
        void RepairTodoLayout()
        {
            var root=RefOf("TodoListUI","self");if(!root)return;
            PlaceRect(todoListTitle?.transform as RectTransform,-60,-95,190,42);PlaceFeature("addList",65,-95,45);PlaceFeature("deleteList",130,-95,80);PlaceFeature("nextList",0,-150,320);
            var add=Find(ChangeRoot+"UI_FacilityTodo/AddTodoUI") as RectTransform;if(add)PlaceRect(add,0,-210,330,45);
            var count=RefOf("TodoListUI","_remainTodoCountText") as RectTransform;if(count)PlaceRect(count,-130,-215,50,35);
            var scroll=Get<ScrollRect>("TodoListUI","_todoScrollRect");if(scroll){var r=(RectTransform)scroll.transform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(10,40);r.offsetMax=new Vector2(-10,-270);}
            Set(root.Find("CompleteList/CompleteList"),false);
            var completed=RefOf("TodoListUI","_completedSection") as RectTransform;
            if(completed){completed.anchorMin=new Vector2(1,0);completed.anchorMax=new Vector2(1,1);completed.pivot=new Vector2(0,.5f);completed.anchoredPosition=new Vector2(0,-70);completed.sizeDelta=new Vector2(-3.2f,-140);}
        }
    }
}
