using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Hlc.ChillPrototype;
public static class ChillPaneLayoutCheck
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,Flags).Invoke(o,args);
    static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,Flags).GetValue(o);
    static void Require(bool value,string why){if(!value)throw new InvalidOperationException(why);}
    static Transform Ref(ChillNativeUI ui,string script,string field)=>(Transform)Call(ui,"RefOf",script,field);
    static void Click(ChillNativeUI ui,string script,string field)=>Ref(ui,script,field).GetComponent<Button>().onClick.Invoke();
    public static void Run(ChillStoryRuntime story,ChillNativeUI ui,ChillGraphicsPlayer player)
    {
        var settingsRoot=Ref(ui,"SettingUI","self");
        var version=settingsRoot.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="AppVersionText");
        foreach(string pane in new[]{"General","Graphics","MusicAudio"})
        {
            var scroll=settingsRoot.Find(pane+"/ScrollView").GetComponent<ScrollRect>();
            var bodyCorners=new Vector3[4];var footerCorners=new Vector3[4];
            scroll.viewport.GetWorldCorners(bodyCorners);version.rectTransform.GetWorldCorners(footerCorners);
            Require(bodyCorners.Min(p=>settingsRoot.InverseTransformPoint(p).y)>footerCorners.Max(p=>settingsRoot.InverseTransformPoint(p).y),"Settings scroll overlaps the shared footer: "+pane);
        }
        ui.Open("settings");
        foreach(var tab in new[]{"_generalInteractableUI","_graphicInteractableUI","_audioInteractableUI"})
        {
            Click(ui,"SettingUI",tab);ui.RefreshNow();
            ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-layout-"+tab+".png",1,0,0,true);
        }
        var audio=Ref(ui,"SettingUI","_audioParent");var rows=audio.Find("ScrollView/Viewport/Content").Cast<Transform>().Where(t=>t.GetComponentInChildren<Slider>(true)).ToArray();
        Require(rows.Length==5,"Audio settings lost a volume row");
        foreach(var row in rows)
        {
            var slider=row.GetComponentInChildren<Slider>(true);var labels=row.GetComponentsInChildren<TMP_Text>(true);
            Require(labels.Count(t=>t.name=="CurrentVolumeText")==1&&labels.Count(t=>t.name=="WebVolumeTitle")==1,"Audio row has duplicate or missing title/value: "+row.name);
            Require(labels.All(t=>t.transform.parent==row)&&slider.transform.parent==row,"Audio label depends on the source nested slider layout");
        }
        Require(rows.First(r=>r.name=="WebVoiceVolume").GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="WebVolumeTitle").text=="Voice","Voice caption is numeric or untranslated");
        ui.Close();ui.Open("decorations");ui.RefreshNow();ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-layout-decoration.png",1,0,0,true);
        Require(!Ref(ui,"DecorationListUI","self").Find("LayoutPresetUI").gameObject.activeSelf,"Native and web preset controls overlap");
        ui.Close();ui.Open("profile");Click(ui,"PlayerProfileView","_birthdaySettingButton");
        var month=Get<TMP_InputField>(ui,"birthdayMonthInput");var day=Get<TMP_InputField>(ui,"birthdayDayInput");var buttons=Get<System.Collections.Generic.Dictionary<string,Button>>(ui,"featureButtons");
        int oldMonth=story.Settings.birthdayMonth,oldDay=story.Settings.birthdayDay;
        month.SetTextWithoutNotify("02");day.SetTextWithoutNotify("30");buttons["confirmBirthday"].onClick.Invoke();Require(story.Settings.birthdayMonth==oldMonth&&story.Settings.birthdayDay==oldDay,"Invalid birthday changed the save");
        day.SetTextWithoutNotify("29");buttons["confirmBirthday"].onClick.Invoke();Require(story.Settings.birthdayMonth==2&&story.Settings.birthdayDay==29,"Leap-day birthday rejected");
        Require(ChillStoryRuntime.BirthdayMatches(new DateTime(2027,3,1),2,29)&&ChillStoryRuntime.BirthdayMatches(new DateTime(2028,2,29),2,29)&&!ChillStoryRuntime.BirthdayMatches(new DateTime(2028,3,1),2,29),"Birthday leap-year behavior differs from source");
        Click(ui,"PlayerProfileView","_birthdaySettingButton");month.SetTextWithoutNotify("12");buttons["cancelBirthday"].onClick.Invoke();Require(story.Settings.birthdayMonth==2,"Cancel applied a birthday edit");
        Click(ui,"PlayerProfileView","_birthdaySettingButton");ui.RefreshNow();ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-layout-birthday.png",1,0,0,true);buttons["cancelBirthday"].onClick.Invoke();story.Settings.birthdayMonth=oldMonth;story.Settings.birthdayDay=oldDay;
        ui.Close();ui.Open("habits");ui.RefreshNow();var mode=Ref(ui,"HabitListUI","_editModeButton");Require(!mode.Find("Buttons").gameObject.activeSelf,"Habit save/cancel overlays idle caption");
        Click(ui,"HabitListUI","_editModeButton");ui.RefreshNow();Require(mode.Find("Buttons").gameObject.activeSelf,"Habit edit actions are missing");mode.Find("Buttons/DiscardButton").GetComponent<Button>().onClick.Invoke();ui.RefreshNow();Require(!mode.Find("Buttons").gameObject.activeSelf,"Discard leaves edit controls open");
        ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-layout-habits.png",1,0,0,true);
        ui.Close();ui.Open("todos");ui.RefreshNow();var section=Ref(ui,"TodoListUI","_completedSection");Require(section.gameObject.activeSelf&&!section.Find("CompleteList").gameObject.activeSelf,"Completed-task drawer starts open or its tab is hidden");
        Click(ui,"TodoListUI","_toggleCompletedSectionButton");ui.RefreshNow();Require(section.Find("CompleteList").gameObject.activeSelf,"Completed-task drawer cannot open");
        ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-layout-todos-open.png",1,0,0,true);Click(ui,"TodoListUI","_toggleCompletedSectionButton");ui.RefreshNow();ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-layout-todos.png",1,0,0,true);
        story.Notify("Error expiry fixture",true);ui.RefreshNow();var notice=Get<GameObject>(ui,"noticePanel");Require(notice.activeSelf,"Error notice was never displayed");typeof(ChillNativeUI).GetField("noticeUntil",Flags).SetValue(ui,-1f);ui.RefreshNow();Require(!notice.activeSelf,"Error notice never expires");ui.Close();
        File.WriteAllText(Path.GetFullPath("../research/pane-layout-check.json"),"{\"passed\":true,\"audioRows\":5,\"birthdayConfirmCancelAndLeapDay\":true,\"habitEditActions\":true,\"completedTodoDrawer\":true,\"errorNoticeExpiry\":true,\"browserGameplayRun\":false,\"userSaveWritten\":false}");
        Debug.Log("HLC_PANE_LAYOUT_CHECK_PASSED");
    }
}
