using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Hlc.ChillPrototype;
using UnityEngine;

public static class ChillMotionContinuityCheck
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
    static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,Flags).GetValue(o);
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    public static void Run(ChillStoryRuntime story,ChillGraphicsPlayer player,ChillStoryData data)
    {
        var settings=story.Settings;bool oldAI=settings.autonomy,oldRest=settings.resting;
        var save=Get<ChillSave>(story,"save");int oldMode=save.deskMode;bool oldRunning=save.timerRunning;double oldRemaining=save.pausedRemaining;string oldChapter=save.chapter;
        settings.resting=false;save.timerRunning=false;save.pausedRemaining=0;
        var animator=player.character;var bones=new[]{HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Head,HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm}.Select(animator.GetBoneTransform).Where(t=>t).ToArray();
        var sceneState=story.presentation.statePaths.First(p=>p.motion==1000).path;
        var firstFrameDeltas=new List<float>();var random=UnityEngine.Random.state;
        foreach(int sceneMotion in new[]{1000,1500})foreach(bool ai in new[]{false,true})for(int mode=0;mode<3;mode++)
        {
            sceneState=story.presentation.statePaths.First(p=>p.motion==sceneMotion).path;
            story.ResetAutonomy();settings.autonomy=ai;save.deskMode=mode;player.PlayState(mode);
            animator.Play(sceneState,0,.6f);animator.Update(0);
            Set(story,"chapter",data.groups.First(g=>g.Scenario==1));Set(story,"automaticDialogue",false);
            story.presentation.manualLook=true;Set(story.presentation,"lookWeight",.7f);
            var positions=bones.Select(t=>t.localPosition).ToArray();var rotations=bones.Select(t=>t.localRotation).ToArray();
            var controller=animator.runtimeAnimatorController;var state=animator.GetCurrentAnimatorStateInfo(0);
            Set(player.bones,"needsReset",false);
            story.StopStory();
            Require(animator.runtimeAnimatorController==controller,"Story return replaces the live character controller");
            Require(animator.GetCurrentAnimatorStateInfo(0).fullPathHash==state.fullPathHash&&Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime-state.normalizedTime)<.001f,"Story return restarts the character animation");
            Require(bones.Select((t,i)=>Vector3.Distance(t.localPosition,positions[i])<.001f&&Quaternion.Angle(t.localRotation,rotations[i])<.1f).All(v=>v),"Story return writes a reset pose before the next animation frame");
            Require(story.presentation.manualLook&&Mathf.Abs(Get<float>(story.presentation,"lookWeight")-.7f)<.001f,"Story return drops look IK immediately");
            Require(!Get<bool>(player.bones,"needsReset"),"Ordinary story return resets hair/clothing dynamics");
            story.presentation.AdvanceLook(.125f);Require(story.presentation.manualLook&&Get<float>(story.presentation,"lookWeight")>0&&Get<float>(story.presentation,"lookWeight")<.7f,"Return look does not fade");story.presentation.AdvanceLook(.125f);Require(!story.presentation.manualLook&&Get<float>(story.presentation,"lookWeight")==0,"Return look remains active after its fade");
            Require(animator.GetInteger("MotionType")==new[]{250,300,200}[mode],"Story return selects the wrong work motion");
            float worst=0;
            for(int tick=0;tick<1200;tick++)
            {
                var previous=bones.Select(t=>t.localRotation).ToArray();animator.Update(1f/60);story.presentation.TrackMotion();player.deskObjects.BlendReturn(1f/60);
                worst=Mathf.Max(worst,bones.Select((t,i)=>Quaternion.Angle(t.localRotation,previous[i])).Max());
            }
            firstFrameDeltas.Add(worst);
            Require(story.presentation.MotionCompleted,"Native story exit does not reach the selected work loop: mode="+mode+" ai="+ai+" state="+story.presentation.statePaths.FirstOrDefault(p=>Animator.StringToHash(p.path)==animator.GetCurrentAnimatorStateInfo(0).fullPathHash)?.path+" requested="+story.presentation.requestedState+" time="+animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
            Require(worst<35,"Story exit contains a one-frame bone rotation jump: "+worst);
        }
        settings.resting=settings.autonomy=true;Set(story,"autonomyInstalled",true);Set(story,"breakMode",2);Set(story,"breakBaseMotion","BreakBase003_ListenMusicHigh");Set(story,"selfTalkCount",2);Set(story,"nextAction",123456d);Set(story,"nextVariant",123457d);
        animator.Play(sceneState,0,.6f);animator.Update(0);var shortPose=bones.Select(t=>t.localRotation).ToArray();Set(story,"chapter",data.groups.First(g=>g.Scenario==13));Set(story,"automaticDialogue",true);story.StopStory();
        Require(Get<int>(story,"breakMode")==2&&Get<int>(story,"selfTalkCount")==2&&Get<double>(story,"nextAction")==123456&&Get<double>(story,"nextVariant")==123457&&story.AutonomyState=="BreakBase003_ListenMusicHigh","Short reaction changes the activity/posture or restarts its schedule");
        Require(bones.Select((t,i)=>Quaternion.Angle(t.localRotation,shortPose[i])<.1f).All(v=>v),"Short reaction resets the visible pose");
        var chair=player.deskObjects.chairAnimator.transform;player.deskObjects.Select(oldMode);var target=chair.localRotation;chair.localRotation=target*Quaternion.Euler(0,30,0);var from=chair.localRotation;
        player.deskObjects.SelectSmooth(oldMode,.25f);Require(Quaternion.Angle(chair.localRotation,from)<.1f,"Desk return snaps the chair immediately");player.deskObjects.BlendReturn(.125f);Require(Quaternion.Angle(chair.localRotation,from)>1&&Quaternion.Angle(chair.localRotation,target)>1,"Chair return does not interpolate");player.deskObjects.BlendReturn(.125f);Require(Quaternion.Angle(chair.localRotation,target)<.1f,"Chair return never settles");
        story.ResetAutonomy();settings.autonomy=oldAI;settings.resting=oldRest;save.deskMode=oldMode;save.timerRunning=oldRunning;save.pausedRemaining=oldRemaining;save.chapter=oldChapter;Set(story,"chapter",null);player.PlayState(oldMode);player.deskObjects.BlendReturn(1);UnityEngine.Random.state=random;
        File.WriteAllText(Path.GetFullPath("../research/motion-continuity-check.json"),"{\"passed\":true,\"deskModes\":3,\"sceneMotions\":[1000,1500],\"storyBoundaryBlendSeconds\":0.25,\"autonomyOnAndOff\":true,\"controllerPreserved\":true,\"exitPosePreserved\":true,\"nativeWorkLoopReached\":true,\"lookFadesOnReturn\":true,\"chairInterpolates\":true,\"shortReactionActivityPreserved\":true,\"clothDynamicsPreserved\":true,\"maximumFrameRotationDegrees\":["+string.Join(",",firstFrameDeltas.Select(v=>v.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)))+"],\"userSaveWritten\":false}");
        Debug.Log("HLC_MOTION_CONTINUITY_CHECK_PASSED deskModes=3 controllerPreserved=true chairBlend=true");
    }
}
