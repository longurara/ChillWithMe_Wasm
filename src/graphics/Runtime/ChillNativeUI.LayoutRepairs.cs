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
        static void PlaceBottomRect(RectTransform rect,float x,float y,float width,float height)
        {if(!rect)return;rect.localScale=Vector3.one;rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(width,height);}
        static void DisableLayouts(Transform root)
        {foreach(var l in root.GetComponents<LayoutGroup>())l.enabled=false;foreach(var f in root.GetComponents<ContentSizeFitter>())f.enabled=false;}
        static void NormalizeScrollViewport(ScrollRect sr,float rightInset=-17f)
        {
            if(!sr)return;
            var scroll=(RectTransform)sr.transform;
            var vp=sr.viewport?sr.viewport:scroll.Find("Viewport") as RectTransform;
            if(vp){vp.anchorMin=Vector2.zero;vp.anchorMax=Vector2.one;vp.pivot=new Vector2(0f,1f);vp.offsetMin=Vector2.zero;vp.offsetMax=new Vector2(rightInset,0f);}
            var ct=sr.content?sr.content:vp?vp.Find("Content") as RectTransform:null;
            if(ct)
            {
                var csf=ct.GetComponent<ContentSizeFitter>();
                bool hFit=csf&&csf.horizontalFit!=ContentSizeFitter.FitMode.Unconstrained;
                ct.anchorMin=new Vector2(0f,1f);ct.anchorMax=new Vector2(hFit?0f:1f,1f);
                ct.pivot=new Vector2(0f,1f);ct.anchoredPosition=Vector2.zero;
                if(!hFit)ct.sizeDelta=new Vector2(0f,ct.sizeDelta.y);
            }
        }
        void RepairPaneLayouts()
        {
            var settings=RefOf("SettingUI","self");
            foreach(string name in new[]{"General","Graphics","MusicAudio"})
            {
                var parent=settings?settings.Find(name) as RectTransform:null;if(!parent)continue;
                if(name=="MusicAudio")
                {
                    parent.anchorMin=parent.anchorMax=new Vector2(0f,.5f);parent.pivot=new Vector2(.5f,.5f);
                    parent.anchoredPosition=new Vector2(672f,0f);parent.sizeDelta=new Vector2(1344f,756f);
                }
                else
                {
                    parent.anchorMin=Vector2.zero;parent.anchorMax=Vector2.one;parent.pivot=new Vector2(.5f,.5f);
                    parent.anchoredPosition=Vector2.zero;parent.sizeDelta=Vector2.zero;
                }
                var scroll=parent.Find("ScrollView") as RectTransform;
                if(scroll)
                {
                    scroll.anchorMin=scroll.anchorMax=scroll.pivot=new Vector2(1f,1f);
                    scroll.anchoredPosition=name=="MusicAudio"?new Vector2(-156f,-78.7f):new Vector2(-22f,-146.2f);
                    scroll.sizeDelta=new Vector2(1152f,590.1f);
                    var sr=scroll.GetComponent<ScrollRect>();
                    NormalizeScrollViewport(sr,-11.5f);
                    var ct=sr?sr.content??scroll.Find("Viewport/Content") as RectTransform:null;
                    if(ct){ct.anchorMin=ct.anchorMax=ct.pivot=new Vector2(1f,1f);ct.anchoredPosition=Vector2.zero;}
                    if(sr)sr.verticalNormalizedPosition=1f;
                }
            }
            if(settings)
            {
                foreach(var label in settings.GetComponentsInChildren<TMP_Text>(true))if(label.name=="AppVersionText")label.text="Ver "+Application.version;
                var reset=settings.Find("Graphics/GraphicInitButton")??settings.Find("Graphics/GraphicsInitButton");
                Set(reset,true);
            }
            var decor=RefOf("DecorationListUI","self");
            if(decor)
            {
                var preset=decor.Find("LayoutPresetUI") as RectTransform;
                if(preset)
                {
                    preset.gameObject.SetActive(true);
                    preset.anchorMin=preset.anchorMax=new Vector2(1f,1f);preset.pivot=new Vector2(.5f,.5f);
                    preset.anchoredPosition=new Vector2(-68.9f,-122.5f);preset.sizeDelta=Vector2.zero;
                }
                var line1=decor.Find("BackLine (1)") as RectTransform;
                if(line1){line1.gameObject.SetActive(true);line1.anchoredPosition=new Vector2(0f,-155.9f);}
                var scroll=Get<ScrollRect>("DecorationListUI","_scrollRect");
                if(scroll)
                {
                    var r=(RectTransform)scroll.transform;
                    r.anchorMin=new Vector2(1f,0f);r.anchorMax=new Vector2(1f,1f);r.pivot=new Vector2(1f,1f);
                    r.anchoredPosition=new Vector2(0f,-155.6f);r.sizeDelta=new Vector2(603.5f,-300.5f);
                    NormalizeScrollViewport(scroll,-17f);
                }
            }
            var envRoot=RefOf("EnvironmentUI","self");
            if(envRoot)
            {
                var preset=envRoot.Find("LayoutPresetUI") as RectTransform;
                if(preset)
                {
                    preset.gameObject.SetActive(true);
                    preset.anchorMin=preset.anchorMax=new Vector2(1f,1f);preset.pivot=new Vector2(1f,1f);
                    preset.anchoredPosition=new Vector2(-69.1f,-121.1f);preset.sizeDelta=Vector2.zero;
                }
                var envScroll=envRoot.Find("EnviromentScrollView")?.GetComponent<ScrollRect>();
                if(envScroll)
                {
                    var sr=(RectTransform)envScroll.transform;
                    sr.anchorMin=new Vector2(1f,0f);sr.anchorMax=new Vector2(1f,1f);sr.pivot=new Vector2(1f,1f);
                    sr.anchoredPosition=new Vector2(0f,-240f);sr.sizeDelta=new Vector2(603.5f,-384.9f);
                    NormalizeScrollViewport(envScroll,-17f);
                    var ct=envScroll.content??sr.Find("Viewport/Content") as RectTransform;
                    if(ct)
                    {
                        foreach(var l in ct.GetComponents<LayoutGroup>())l.enabled=true;
                        foreach(var f in ct.GetComponents<ContentSizeFitter>())f.enabled=true;
                        LayoutRebuilder.ForceRebuildLayoutImmediate(ct);
                    }
                    envScroll.horizontal=false;envScroll.vertical=true;envScroll.verticalNormalizedPosition=1f;
                }
            }
            var musicScroll=Get<ScrollRect>("MusicPlayListView","_scrollRect");
            if(musicScroll)
            {
                var mr=(RectTransform)musicScroll.transform;
                mr.anchorMin=new Vector2(.5f,0f);mr.anchorMax=new Vector2(.5f,1f);mr.pivot=new Vector2(.5f,1f);
                mr.anchoredPosition=new Vector2(7.25f,-210.73f);mr.sizeDelta=new Vector2(584.5f,-353.73f);
                NormalizeScrollViewport(musicScroll,-11.5f);
            }
            var specialScroll=RefOf("SpecialSelectListUI","_scrollRect")?.GetComponent<ScrollRect>();
            if(specialScroll)
            {
                var sr=(RectTransform)specialScroll.transform;
                sr.anchorMin=new Vector2(.5f,0f);sr.anchorMax=new Vector2(.5f,1f);sr.pivot=new Vector2(.5f,1f);
                sr.anchoredPosition=new Vector2(0f,-93.42f);sr.sizeDelta=new Vector2(599.4f,-239.7f);
                NormalizeScrollViewport(specialScroll,-11.5f);
                var sbar=sr.Find("Scrollbar Vertical") as RectTransform;
                if(sbar){sbar.anchorMin=new Vector2(1f,0f);sbar.anchorMax=Vector2.one;sbar.pivot=Vector2.one;sbar.anchoredPosition=new Vector2(-9.9f,-7f);sbar.sizeDelta=new Vector2(14.5f,-7f);}
            }
            var storyScroll=RefOf("StorySelectUI","_scrollRect")?.GetComponent<ScrollRect>();
            if(storyScroll)NormalizeScrollViewport(storyScroll,-11.5f);
            var habitScroll=RefOf("HabitListUI","_scrollRect")?.GetComponent<ScrollRect>();
            if(habitScroll)NormalizeScrollViewport(habitScroll,-11.5f);
            var calScroll=RefOf("CalendarUI","_completeTodoParent")?.GetComponentInParent<ScrollRect>();
            if(calScroll)NormalizeScrollViewport(calScroll,-11.5f);
            var noteListScroll=RefOf("SelectPageListUI","_selectPageUIParent")?.GetComponentInParent<ScrollRect>();
            if(noteListScroll)NormalizeScrollViewport(noteListScroll,-11.5f);
            var edit=RefOf("HabitListUI","_editModeButton");if(edit){Set(edit.Find("Buttons"),false);Set(edit.Find("IconActive"),false);var text=edit.GetComponentInChildren<TMP_Text>(true);if(text){text.enableAutoSizing=true;text.fontSizeMin=14;text.fontSizeMax=22;}}
            RepairTodoLayout();
            RepairProfileLayout();
            if(noticePanel)
            {
                var graphic=noticePanel.GetComponent<Graphic>();if(graphic)graphic.raycastTarget=true;
                Bind(noticePanel,()=>{noticeUntil=0;Show(noticePanel,false);});
            }
            // FacilityCall invokes the native game-end conversation before exiting.
            var phone=canvas.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="IconExit_Button");
            if(phone)Bind(phone.gameObject,()=>{Close();player.story.Farewell();});
        }
        void RepairProfileLayout()
        {
            var profile=RefOf("PlayerProfileView","self") as RectTransform;if(!profile)return;
            profile.anchorMin=profile.anchorMax=new Vector2(0f,.5f);profile.pivot=new Vector2(0f,1f);
            profile.anchoredPosition=new Vector2(0f,118.2f);profile.sizeDelta=new Vector2(372.8f,100.4f);
            var iconBtn=RefOf("PlayerProfileView","_iconSettingButton") as RectTransform;
            if(iconBtn){iconBtn.anchorMin=iconBtn.anchorMax=new Vector2(.5f,.5f);iconBtn.pivot=new Vector2(.5f,.5f);iconBtn.anchoredPosition=new Vector2(-141.9f,0f);iconBtn.sizeDelta=new Vector2(59.85f,59.85f);}
            var bdayBtn=RefOf("PlayerProfileView","_birthdaySettingButton") as RectTransform;
            if(bdayBtn){bdayBtn.anchorMin=bdayBtn.anchorMax=new Vector2(.5f,.5f);bdayBtn.pivot=new Vector2(.5f,.5f);bdayBtn.anchoredPosition=new Vector2(-72.2f,0f);bdayBtn.sizeDelta=new Vector2(54f,54f);}
            var statusBtn=RefOf("PlayerProfileView","_statusChangeButton") as RectTransform;
            if(statusBtn){statusBtn.anchorMin=statusBtn.anchorMax=new Vector2(.5f,.5f);statusBtn.pivot=new Vector2(.5f,.5f);statusBtn.anchoredPosition=new Vector2(116.2f,0f);statusBtn.sizeDelta=new Vector2(54f,54f);}
            var closeBtn=RefOf("PlayerProfileView","_closeButton") as RectTransform;
            if(closeBtn){closeBtn.anchorMin=closeBtn.anchorMax=new Vector2(1f,1f);closeBtn.pivot=new Vector2(.5f,.5f);closeBtn.anchoredPosition=new Vector2(-23.11f,-23.59f);closeBtn.sizeDelta=new Vector2(28f,28f);}
        }
        void RepairTodoLayout()
        {
            var root=RefOf("TodoListUI","self");if(!root)return;
            var groupSelect=Find(ChangeRoot+"UI_FacilityTodo/TodoGroupSelect") as RectTransform;
            if(groupSelect)
            {
                groupSelect.gameObject.SetActive(true);
                groupSelect.anchorMin=groupSelect.anchorMax=new Vector2(.5f,1f);groupSelect.pivot=new Vector2(.5f,.5f);
                groupSelect.anchoredPosition=new Vector2(0f,-83.2002f);groupSelect.sizeDelta=new Vector2(100f,100f);
                groupSelect.SetAsLastSibling();
                var listScroll=groupSelect.Find("PullDown/Scroll View")?.GetComponent<ScrollRect>();
                if(listScroll)NormalizeScrollViewport(listScroll,-11.5f);
            }
            var add=Find(ChangeRoot+"UI_FacilityTodo/AddTodoUI") as RectTransform;
            if(add)
            {
                add.anchorMin=add.anchorMax=new Vector2(.5f,1f);add.pivot=new Vector2(.5f,.5f);
                add.anchoredPosition=new Vector2(0f,-150.5f);add.sizeDelta=new Vector2(160f,40f);
            }
            var count=RefOf("TodoListUI","_remainTodoCountText") as RectTransform;
            if(count)
            {
                count.anchorMin=count.anchorMax=new Vector2(.5f,1f);count.pivot=new Vector2(0f,.5f);
                count.anchoredPosition=new Vector2(-148.9f,-150.5f);count.sizeDelta=new Vector2(67.2f,35.1f);
            }
            var scroll=Get<ScrollRect>("TodoListUI","_todoScrollRect");
            if(scroll)
            {
                var r=(RectTransform)scroll.transform;
                r.anchorMin=new Vector2(0f,0f);r.anchorMax=new Vector2(0f,1f);r.pivot=new Vector2(0f,1f);
                r.anchoredPosition=new Vector2(0f,-174.81f);r.sizeDelta=new Vector2(367.2f,-214.11f);
                NormalizeScrollViewport(scroll,-11.5f);
            }
            Set(root.Find("CompleteList/CompleteList"),false);
            var completed=RefOf("TodoListUI","_completedSection") as RectTransform;
            if(completed)
            {
                completed.anchorMin=new Vector2(1f,0f);completed.anchorMax=new Vector2(1f,1f);completed.pivot=new Vector2(0f,.5f);completed.anchoredPosition=new Vector2(0f,-60.85f);completed.sizeDelta=new Vector2(-3.3f,-121.7f);
                var compScroll=completed.Find("CompleteList/Scroll View")?.GetComponent<ScrollRect>();
                if(compScroll)
                {
                    var csr=(RectTransform)compScroll.transform;
                    csr.anchorMin=new Vector2(0f,0f);csr.anchorMax=new Vector2(0f,1f);csr.pivot=new Vector2(0f,1f);
                    csr.anchoredPosition=new Vector2(0f,-53.11f);csr.sizeDelta=new Vector2(367.2f,-92.41f);
                    NormalizeScrollViewport(compScroll,-11.7f);
                    var sbar=csr.Find("Scrollbar Vertical") as RectTransform;
                    if(sbar){sbar.anchorMin=new Vector2(1f,0f);sbar.anchorMax=Vector2.one;sbar.pivot=Vector2.one;sbar.anchoredPosition=new Vector2(-8.3f,0f);sbar.sizeDelta=new Vector2(14.5f,0f);}
                }
            }
            if(groupSelect)groupSelect.SetAsLastSibling();
            var mini=Find(ChangeRoot+"UI_FacilityTodo/MiniCalender");
            if(mini)mini.SetAsLastSibling();
        }
    }
}
