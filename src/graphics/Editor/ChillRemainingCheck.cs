using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hlc.ChillPrototype;
using UnityEngine;

public static class ChillRemainingCheck
{
    // Use only after the same solver's full bake has already passed. The normal
    // BuildChecked entry always regenerates coverage when physics changes.
    public static void BuildUsingExistingBake(){ChillFeaturesCheck.Run();Run();ChillPhysicsBake.VerifyExistingBake();ChillNativeUIBuild.Build();}
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    public static void Run()
    {
        var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();var story=player.story;
        var field=typeof(ChillStoryRuntime).GetField("save",Flags);var previous=(ChillSave)field.GetValue(story);
        try
        {
            var fixture=new ChillSave{needsTutorial=false,workedSeconds=7199};fixture.workspace.achievements.Add("EXIT_GAME_COUNT");field.SetValue(story,fixture);
            Require(story.Workspace.achievements.Contains("FINISH_GAME_FROM_EXIT_CALL_BUTTON_COUNT")&&!story.Workspace.achievements.Contains("EXIT_GAME_COUNT"),"Achievement alias did not migrate");
            Require(story.AchievementProgress("POMODORO_WORK_HOURS_COUNT")==1,"Work-hour progress rounds upward");fixture.workedSeconds=7200;Require(story.AchievementProgress("POMODORO_WORK_HOURS_COUNT")==2,"Work-hour boundary is wrong");
            int awards=story.Workspace.achievements.Count,completed=fixture.completed.Count,level=fixture.level;
            story.UIAction(new ChillUiCommand{action="unlock",value=1});Require(story.PreviewUnlock&&fixture.completed.Count==completed&&fixture.level==level&&story.Workspace.achievements.Count==awards,"Test unlock grants progress");
            story.UIAction(new ChillUiCommand{action="unlock",value=0});Require(!story.PreviewUnlock,"Test unlock cannot be disabled");
        }
        finally{field.SetValue(story,previous);}
        var catalog=JsonUtility.FromJson<ChillPortI18n.Catalog>(File.ReadAllText("../src/gameplay-ui/i18n.json"));
        Require(catalog.entries.Select(e=>e.En).Distinct().Count()==catalog.entries.Length,"Duplicate UI translation keys");
        foreach(var row in catalog.entries)foreach(var language in new[]{"En","Ja","ZhHans","ZhHant","Pt","Ko","Ru"})Require(!string.IsNullOrEmpty(row.Get(language)),"Missing UI translation: "+row.En+" "+language);
        var library=player.library;var type=typeof(ChillLibraryBrowser);var busy=type.GetField("busy",Flags);bool oldBusy=(bool)busy.GetValue(library);
        var versions=(Dictionary<string,int>)type.GetField("slotVersions",Flags).GetValue(library);bool notified=false;
        try
        {
            busy.SetValue(library,true);versions["verification"]=1;
            var routine=(IEnumerator)type.GetMethod("LoadExternalRoutine",Flags).Invoke(library,new object[]{"verification","unused",(Action<UnityEngine.Object>)(asset=>{Require(asset==null,"Cancelled load returns an asset");notified=true;}),1});
            Require(routine.MoveNext()&&library.PendingAssetLoads==1,"Queued load was not registered");library.ReleaseExternal("verification");Require(!routine.MoveNext()&&notified&&library.PendingAssetLoads==0&&library.Busy,"Canceling a queued load steals another owner's lock");
        }
        finally{busy.SetValue(library,oldBusy);versions.Remove("verification");}
        File.WriteAllText("../research/remaining-code-check.json","{\"passed\":true,\"unlockPreservesProgress\":true,\"achievementAliasMigration\":true,\"workHourBoundary\":true,\"localizedControls\":"+catalog.entries.Length+",\"languages\":7,\"queuedAssetCancellation\":true,\"userSaveWritten\":false}");
        Debug.Log("HLC_REMAINING_CODE_PASSED");
    }
}
