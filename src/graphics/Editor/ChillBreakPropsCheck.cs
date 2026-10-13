using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Hlc.ChillPrototype;
using UnityEngine;
public static class ChillBreakPropsCheck
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
    static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,Flags).GetValue(o);
    static void Require(bool condition,string why){if(!condition)throw new InvalidOperationException(why);}
    public static void Run(ChillStoryRuntime story,ChillGraphicsPlayer player,ChillStoryData data)
    {
        var save=Get<ChillSave>(story,"save");var snapshot=JsonUtility.ToJson(save);var random=UnityEngine.Random.state;var desk=player.deskObjects;
        var flip=desk.breakBookAnimator.transform.Find("root/spine/flip");Require(flip,"Break book page bone is missing");
        desk.Action("ActivateBook");desk.breakBookAnimator.Play("anim_item_novel_loop",0,0);desk.breakBookAnimator.Update(0);var idle=flip.localRotation;
        desk.Select(0);Require(!desk.handNote.activeSelf&&!desk.breakBookAnimator.enabled,"Work desk did not stow break book");
        desk.Action("ActivateBook");Require(desk.handNote.activeSelf&&desk.breakBookAnimator.enabled,"Taking book does not start its page controller");Require(Quaternion.Angle(idle,flip.localRotation)<.1f,"Book opens in exported folded page pose");
        desk.Action("BreakBookNextPage");for(int i=0;i<240;i++)desk.AdvanceProps(1f/60);Require(desk.breakBookAnimator.enabled&&desk.handNote.activeSelf,"Page turn loses book");
        story.Settings.autonomy=true;story.Settings.resting=true;
        Set(story,"chapter",data.groups.First(g=>g.Scenario==5));Set(story,"automaticDialogue",true);Set(story,"autonomyInstalled",true);Set(story,"previousRest",true);Set(story,"breakMode",1);Set(story,"breakBaseMotion","BreakBase002");Set(story,"nextAction",12345678900d);Set(story,"suspendedChapter",save.chapter);Set(story,"suspendedScene",new System.Collections.Generic.List<int>(save.sceneHistory));
        desk.Select(0);story.StopStory();Require(desk.handNote.activeSelf&&desk.breakBookAnimator.enabled,"Break-start reaction returned to reading with empty hands");Require(Get<int>(story,"breakMode")==1,"Same-phase reaction restarted the break");
        Set(story,"chapter",data.groups.First(g=>g.Scenario==5));Set(story,"automaticDialogue",true);Set(story,"autonomyInstalled",true);Set(story,"previousRest",false);Set(story,"breakMode",-1);Set(story,"nextAction",1d);Set(story,"suspendedChapter",save.chapter);Set(story,"suspendedScene",new System.Collections.Generic.List<int>(save.sceneHistory));
        story.StopStory();Require(Get<int>(story,"breakMode")>=0&&Get<bool>(story,"previousRest"),"Timer phase change resumed the old work activity");Require(Get<double>(story,"nextAction")>1,"New break retained old work deadline");
        story.ResetAutonomy();Set(story,"save",JsonUtility.FromJson<ChillSave>(snapshot));UnityEngine.Random.state=random;player.PlayState(save.deskMode);
        File.WriteAllText(Path.GetFullPath("../research/break-props-check.json"),"{\"passed\":true,\"bookIdlePose\":true,\"pageTurn\":true,\"readingPropAfterReaction\":true,\"workToBreakInstallsNewActivity\":true,\"userSaveWritten\":false,\"browserGameplayRun\":false}");Debug.Log("HLC_BREAK_PROPS_CHECK_PASSED");
    }
}
