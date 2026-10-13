using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using Hlc.ChillPrototype;

public static class ChillStoryMotionSweep
{
    [Serializable] public class Entry{public int motion;public string name,lastState;public bool entered,completed;public float seconds;}
    [Serializable] public class Report{public bool passed;public int requested,waitingMotions;public List<Entry> motions=new List<Entry>();public string scope="Native Animator entry/readiness sweep. GameStart_End follows GameStart_Start; hair-check uses the runtime story-entry path. No claim of browser playback of all episodes.";}
    public static void Check()
    {
        EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();
        var data=JsonUtility.FromJson<ChillStoryData>(File.ReadAllText("Assets/StreamingAssets/chill-story.json"));Run(player,data);
    }
    public static void Run(ChillGraphicsPlayer player,ChillStoryData data)
    {
        var animator=player.character;var presentation=player.story.presentation;var report=new Report();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;var sentenceField=typeof(ChillStoryRuntime).GetField("sentences",flags);var originalSentences=sentenceField.GetValue(player.story);
        typeof(ChillStoryRuntime).GetField("motions",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player.story,data.motions.GroupBy(m=>m.value).ToDictionary(g=>g.Key,g=>g.First().name));
        animator.runtimeAnimatorController=player.story.storyController;animator.Rebind();animator.Update(0);
        var seed=presentation.statePaths.First(p=>p.name=="WorkBase002");
        var values=data.novels.Where(r=>(r.Command==0||r.Command==21||r.Command==22||r.Command==80)&&r.BodyMotion>=0).Select(r=>r.BodyMotion).Distinct().OrderBy(x=>x).ToArray();report.requested=values.Length;
        var required=data.novels.Where(r=>(r.Command==21&&r.Arg2<0||r.Command==22)&&r.BodyMotion>=0).Select(r=>r.BodyMotion).Distinct().ToArray();report.waitingMotions=required.Length;
        foreach(int motion in values)
        {
            var origin=motion==401?presentation.statePaths.First(p=>p.motion==400):seed;
            animator.SetInteger("MotionType",origin.motion);animator.Play(origin.path,0,.5f);animator.Update(0);
            var source=presentation.statePaths.First(p=>p.motion==motion);var entry=new Entry{motion=motion,name=source.name};report.motions.Add(entry);
            presentation.Motion(new ChillSentence{BodyMotion=motion,FacialMotion=-1,LookScale=0},source.name);
            if(motion==6501)
            {
                typeof(ChillStoryRuntime).GetField("sentences",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player.story,new[]{new ChillSentence{Command=22,BodyMotion=6501,FacialMotion=-1}});
                typeof(ChillStoryRuntime).GetMethod("PrepareStoryEntry",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(player.story,null);
            }
            for(int tick=0;tick<1800;tick++)
            {
                animator.Update(1f/60);presentation.TrackMotion();entry.seconds=(tick+1)/60f;
                var state=animator.GetCurrentAnimatorStateInfo(0);entry.lastState=presentation.statePaths.FirstOrDefault(p=>Animator.StringToHash(p.path)==state.fullPathHash)?.name??state.fullPathHash.ToString();
                if(state.IsName(source.path)||animator.IsInTransition(0)&&animator.GetNextAnimatorStateInfo(0).IsName(source.path))entry.entered=true;
                if(presentation.MotionCompleted){entry.completed=true;break;}
            }
        }
        report.passed=report.motions.All(m=>m.completed);
        sentenceField.SetValue(player.story,originalSentences);
        File.WriteAllText(Path.GetFullPath("../research/story-motion-sweep.json"),JsonUtility.ToJson(report,true));
        Debug.Log("HLC_STORY_MOTION_SWEEP requested="+report.requested+" incomplete="+string.Join(",",report.motions.Where(e=>!e.completed).Select(e=>e.motion+":"+e.name+"=>"+e.lastState)));
        if(!report.passed)throw new InvalidOperationException("Story motion sweep has incomplete states");
    }
}
