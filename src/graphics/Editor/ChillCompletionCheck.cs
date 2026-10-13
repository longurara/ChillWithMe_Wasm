using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hlc.ChillPrototype;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public static class ChillCompletionCheck
{
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Require(bool condition,string text){if(!condition)throw new InvalidOperationException(text);}
    static object Invoke(object target,string method,params object[] values)=>target.GetType().GetMethod(method,Flags).Invoke(target,values);
    [Serializable]class Report{public bool passed,oldSaveMigration,noteIsolation,todoIsolation,reorder,calendarMidnight,diary,habitSchedule,pointsAndPurchases,roomPresets,saveRoundTrip,invalidSaveRejected,weightedActions,timerOptions,nativePanes,userSaveWritten;public int panes,hooks;}
    public static void Run(ChillStoryRuntime story,ChillNativeUI ui,ChillGraphicsPlayer player,ChillSave original)
    {
        var save=new ChillSave{note="Legacy note",todos=new List<ChillTodo>{new ChillTodo{title="Legacy task"}},previewUnlock=true};
        var field=story.GetType().GetField("save",Flags);field.SetValue(story,save);var workspace=story.Workspace;
        Require(story.CurrentNote.text=="Legacy note"&&story.CurrentList.items[0].title=="Legacy task","Old note/task migration failed");
        var first=story.CurrentNote.id;story.AddNote("Second");story.UpdateNote("Second body");story.SelectNote(first);Require(story.CurrentNote.text=="Legacy note","Editing a new page changed the old page");story.SelectNote(workspace.notes[1].id);story.MoveNote(-1);Require(workspace.notes[0].title=="Second","Note reorder failed");
        var list=story.CurrentList.id;story.AddTodoList("Another list");story.AddTodo("Another task","2026-10-15");story.ToggleTodo(0);Require(story.CurrentList.items[0].done&&ChillStoryRuntime.ValidDate(story.CurrentList.items[0].completedDate),"Task completion date missing");story.SelectTodoList(list);Require(story.CurrentList.items.Count==1&&story.CurrentList.items[0].title=="Legacy task","Todo lists share items");story.AddTodo("Two");story.MoveTodo(1,-1);Require(story.CurrentList.items[0].title=="Two","Task reorder failed");
        var midnight=new DateTimeOffset(new DateTime(2026,10,14,0,0,0,DateTimeKind.Local)).ToUnixTimeMilliseconds()/1000d;
        Invoke(story,"RecordWork",60d,midnight);Require(Math.Abs(story.Day("2026-10-13").seconds-60)<.001&&story.Day("2026-10-14").seconds==0,"Work interval at exact midnight assigned to wrong day");Invoke(story,"RecordWork",120d,midnight+60);Require(story.Day("2026-10-13").seconds==120&&story.Day("2026-10-14").seconds==60,"Work crossing midnight not split");
        story.SetDiary("2026-10-13","Diary");story.SetDiary("2026-10-14","Other");Require(story.Day("2026-10-13").diary=="Diary","Diary changes another day");story.AddHabit("Read");var habit=workspace.habits[0];story.EditHabit(habit.id,habit.title,1<<2,false);story.ToggleHabit(habit.id,"2026-10-13");story.ToggleHabit(habit.id,"2026-10-14");Require(habit.completed.SequenceEqual(new[]{"2026-10-13"}),"Habit allows disabled weekday");story.EditHabit(habit.id,habit.title,127,true);story.ToggleHabit(habit.id,"2026-10-14");Require(habit.completed.Count==1,"Paused habit accepted check");story.EditHabit(habit.id,habit.title,127,false);
        Invoke(story,"CreditWork",60d,midnight+120);Require(workspace.points==10,"Source point rate mismatch");var rule=story.Features.unlocks.First(r=>r.condition=="PointPurchase");save.previewUnlock=false;workspace.points=int.Parse(rule.argument)-1;Require(!story.Purchase(rule.kind,rule.item),"Purchase allowed insufficient balance");workspace.points++;Require(story.Purchase(rule.kind,rule.item)&&workspace.points==0,"Purchase did not consume exact cost");Require(story.Purchase(rule.kind,rule.item)&&workspace.points==0,"Repeat purchase charged twice");save.previewUnlock=true;
        var skin=story.decorations.Skins.First();story.ChooseDecoration(skin.name);story.ChooseHair(1);story.ChooseCostume(2);story.SaveRoomPreset("Desk");story.ResetRoom();story.LoadRoomPreset("Desk");Require(workspace.room.Any(c=>c.skin==skin.name)&&workspace.hair==1&&workspace.costume==2,"Room preset lost appearance");
        story.TimerAudio(false).start=2;story.TimerAudio(false).finish=5;story.TimerAudio(false).startVolume=.2f;story.TimerAudio(true).finishMute=true;
        var roundtrip=JsonUtility.FromJson<ChillSave>(JsonUtility.ToJson(save));Invoke(story,"Validate",roundtrip);Require(roundtrip.workspace.notes.Count==2&&roundtrip.workspace.lists.Count==2&&roundtrip.workspace.days.Count>=2&&roundtrip.workspace.pomodoroAudio.start==2&&roundtrip.workspace.countupAudio.finishMute,"Workspace save round trip lost values");
        roundtrip.workspace.days[0].date="invalid";bool rejected=false;try{Invoke(story,"Validate",roundtrip);}catch(TargetInvocationException){rejected=true;}Require(rejected,"Invalid date accepted from imported save");
        story.Settings.resting=false;player.PlayState(2);Invoke(story,"SetWeightGroup","HeroineStateWorkPC");Invoke(story,"WeightedVariant");var weights=story.GetType().GetField("actionWeights",Flags).GetValue(story) as System.Collections.IList;Require(weights.Count==4,"Source PC weights missing");Require(weights.Cast<object>().Any(w=>(float)w.GetType().GetField("amount").GetValue(w)>0),"Base action did not accumulate weights");
        foreach(var pane in new[]{"notes","todos","calendar","habits","statistics","timer-audio","decorations","profile","heroine-profile","special"})
        {ui.Open(pane);ui.RefreshNow();Canvas.ForceUpdateCanvases();var root=(GameObject)ui.GetType().GetField("openPane",Flags).GetValue(ui);Require(root&&root.activeInHierarchy,"Native pane did not open: "+pane);Require(root.GetComponentsInChildren<TMP_InputField>(true).All(i=>i.onEndEdit.GetPersistentEventCount()==0),"Native input retains Windows callback: "+pane);ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-completion-"+pane+".png",1,0,.3f);ui.Close();}
        player.GetComponent<ChillWardrobe>()?.Apply(0);field.SetValue(story,original);ui.RefreshNow();
        File.WriteAllText(Path.GetFullPath("../research/completion-check.json"),JsonUtility.ToJson(new Report{passed=true,oldSaveMigration=true,noteIsolation=true,todoIsolation=true,reorder=true,calendarMidnight=true,diary=true,habitSchedule=true,pointsAndPurchases=true,roomPresets=true,saveRoundTrip=true,invalidSaveRejected=true,weightedActions=true,timerOptions=true,nativePanes=true,userSaveWritten=false,panes=10,hooks=ui.HookCount},true));Debug.Log("HLC_COMPLETION_CHECK_PASSED panes=10 userSaveWritten=false");
    }
}
