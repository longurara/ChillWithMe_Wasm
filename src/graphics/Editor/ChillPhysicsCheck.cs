using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Hlc.ChillPrototype;

public static class ChillPhysicsCheck
{
    [Serializable] class Report
    {
        public bool passed,fixedTicks,slowFrameBounded,resumePreservesOffset,teleportFinite,garmentRebindStable,authoredPosePreserved,collisionPushesOutside;
        public int baseNodes,outfits,motions,frames,maximumSubsteps;
        public float fpsRotationDifference,maximumOffset,maximumHumanError,maximumPositionError;
        public string comparison="Source parameters and rig checked; PC runtime and physical mobile comparison pending.";
    }
    static void Require(bool value,string why){if(!value)throw new InvalidOperationException("Physics: "+why);}
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static float Difference(Quaternion[] a,Quaternion[] b)=>a.Select((q,i)=>Quaternion.Angle(q,b[i])).DefaultIfEmpty().Max();
    static Quaternion[] Synthetic(int fps,out long ticks)
    {
        var go=new GameObject("Physics fixture");var actor=go.AddComponent<Animator>();
        var root=new GameObject("Sleeve").transform;root.SetParent(go.transform,false);
        var child=new GameObject("Sleeve1").transform;child.SetParent(root,false);child.localPosition=new Vector3(0,-.12f,0);
        var tip=new GameObject("Sleeve2").transform;tip.SetParent(child,false);tip.localPosition=new Vector3(0,-.12f,0);
        var physics=go.AddComponent<ChillBonePhysics>();physics.character=actor;physics.configuration=new TextAsset("{\"groups\":[],\"colliders\":[]}");
        physics.RebindGarment(new[]{root});physics.Step(0);
        var samples=new List<Quaternion>();
        for(int frame=1;frame<=fps*10;frame++)
        {
            physics.BeforeAnimator();float t=(float)frame/fps;
            root.localPosition=new Vector3(Mathf.Sin(t)*.05f,0,Mathf.Sin(t*.8f)*.02f);root.localRotation=Quaternion.Euler(0,0,Mathf.Sin(t*.7f)*20);
            physics.Step(1f/fps);Require(physics.StateFinite(),"non-finite synthetic chain");
            if(frame%(fps/2)==0)samples.AddRange(physics.RotationOffsets());
        }
        var offsets=physics.RotationOffsets();ticks=physics.SimulationTicks;
        Require(samples.Any(q=>Quaternion.Angle(Quaternion.identity,q)>.1f),"synthetic chain never produces secondary motion");
        physics.BeforeAnimator();physics.SetSuspended(true);physics.Step(10);Require(physics.SimulationTicks==ticks,"suspended simulation advanced");
        var before=physics.RotationOffsets();physics.BeforeAnimator();physics.SetSuspended(false);physics.Step(0);
        Require(Difference(before,physics.RotationOffsets())<.2f,"resume snaps secondary pose");
        physics.BeforeAnimator();go.transform.position+=new Vector3(4,2,0);go.transform.rotation=Quaternion.Euler(0,130,0);physics.Step(0);Require(physics.StateFinite(),"teleport produces non-finite pose");
        Require(Difference(before,physics.RotationOffsets())<.3f,"teleport discards the existing secondary pose");
        physics.BeforeAnimator();physics.Step(2);Require(physics.LastStepCount<=6&&physics.DroppedSeconds>1,"slow-frame catch-up is unbounded");
        Require(physics.RotationOffsets().All(q=>Quaternion.Angle(Quaternion.identity,q)<=12.1f),"synthetic chain exceeds angle cap");
        UnityEngine.Object.DestroyImmediate(physics.configuration);UnityEngine.Object.DestroyImmediate(go);return samples.ToArray();
    }
    static void CollisionCheck()
    {
        var go=new GameObject("Contact fixture");var actor=go.AddComponent<Animator>();
        var root=new GameObject("Chain").transform;root.SetParent(go.transform,false);
        var tip=new GameObject("Tip").transform;tip.SetParent(root,false);tip.localPosition=new Vector3(.2f,0,0);
        var renderer=go.AddComponent<SkinnedMeshRenderer>();renderer.bones=new[]{root,tip};
        var body=new GameObject("Sphere").transform;body.SetParent(go.transform,false);body.localPosition=new Vector3(1,0,0);
        var physics=go.AddComponent<ChillBonePhysics>();physics.character=actor;
        physics.configuration=new TextAsset("{\"groups\":[{\"name\":\"Contact\",\"roots\":[\"Contact fixture/Chain\"],\"colliders\":[\"sphere\"],\"gravity\":0,\"damping\":0.1,\"radius\":0.01,\"worldInertia\":1,\"localInertia\":1,\"teleportDistance\":0.5,\"teleportRotation\":90}],\"colliders\":[{\"id\":\"sphere\",\"path\":\"Contact fixture/Sphere\",\"sphere\":true,\"center\":[0,0,0],\"size\":[0.08,0.08,0],\"axis\":1}]}");
        physics.InitializeForPreview();physics.Step(0);body.localPosition=new Vector3(.19f,.01f,0);
        physics.Step(1f/60);
        var nodes=(System.Collections.IList)typeof(ChillBonePhysics).GetField("nodes",Private).GetValue(physics);
        var point=(Vector3)nodes[1].GetType().GetField("position").GetValue(nodes[1]);
        Require(Vector3.Distance(point,body.position)>=.089f,"sphere contact remains inside body: "+Vector3.Distance(point,body.position)+" point="+point+" radius="+nodes[1].GetType().GetField("radius").GetValue(nodes[1])+" allowance="+string.Join(",",(float[])nodes[1].GetType().GetField("allowances").GetValue(nodes[1])));
        Require(tip.localPosition==new Vector3(.2f,0,0),"contact writes bone translation");
        var closest=ChillBonePhysics.Closest(new Vector3(1,1,0),Vector3.zero,new Vector3(2,0,0),out float t);
        Require(Vector3.Distance(closest,new Vector3(1,0,0))<1e-6f&&Mathf.Abs(t-.5f)<1e-6f,"capsule closest point wrong");
        Require(ChillBonePhysics.Closest(Vector3.one,Vector3.zero,Vector3.zero,out _)==Vector3.zero,"degenerate capsule is invalid");
        UnityEngine.Object.DestroyImmediate(physics.configuration);UnityEngine.Object.DestroyImmediate(go);
    }
    public static void Check()
    {
        EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();
        ChillFeaturesBuild.Configure(player);
        var data=JsonUtility.FromJson<ChillStoryData>(File.ReadAllText("Assets/StreamingAssets/chill-story.json"));Run(player,data);
    }
    public static void Run(ChillGraphicsPlayer player,ChillStoryData data)
    {
        var report=new Report();var baseline=Synthetic(60,out long sixty);
        foreach(int fps in new[]{24,30,120})
        {
            var sample=Synthetic(fps,out long ticks);Require(ticks==600&&sixty==600,"fixed tick count depends on rendering FPS: "+fps+"="+ticks+" 60="+sixty);
            report.fpsRotationDifference=Mathf.Max(report.fpsRotationDifference,Difference(baseline,sample));
        }
        Require(report.fpsRotationDifference<1.5f,"24/30/60/120 FPS secondary motion diverges");CollisionCheck();
        report.fixedTicks=report.slowFrameBounded=report.resumePreservesOffset=report.teleportFinite=report.collisionPushesOutside=true;
        var wardrobe=player.GetComponent<ChillWardrobe>();var animator=player.character;var physics=player.bones;
        physics.configuration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/bone-physics.json");physics.RebindGarment(null);report.baseNodes=physics.NodeCount;
        Require(report.baseNodes>50,"native cloth roots not bound");
        animator.runtimeAnimatorController=player.story.storyController;animator.Rebind();animator.Update(0);
        int[] values={200,250,300,50,57,58,752,1500};var counts=new Dictionary<int,int>();
        for(int pass=0;pass<2;pass++)for(int costume=0;costume<5;costume++)
        {
            wardrobe.Apply(costume);
            if(counts.TryGetValue(costume,out int count))Require(count==physics.NodeCount,"wardrobe accumulates stale/duplicate nodes");else counts[costume]=physics.NodeCount;
            Require(costume==0?physics.DynamicRootCount==0:physics.DynamicRootCount==2,"custom sleeve roots not updated");
            Require(physics.BonePaths().Distinct().Count()==physics.NodeCount,"duplicate bone path after costume change");
            if(pass==1)continue;
            var transforms=animator.GetComponentsInChildren<Transform>(true);
            var humans=Enumerable.Range(0,(int)HumanBodyBones.LastBone).Select(i=>animator.GetBoneTransform((HumanBodyBones)i)).Where(t=>t).ToArray();
            foreach(int fps in new[]{30,60})foreach(int motion in values)
            {
                string path=player.story.presentation.statePaths.First(p=>p.motion==motion).path;
                physics.ResetDynamics();animator.Play(path,0,0);animator.Update(0);physics.Step(0);
                for(int frame=0;frame<fps;frame++)
                {
                    physics.BeforeAnimator();animator.Update(1f/fps);
                    var positions=transforms.Select(t=>t.localPosition).ToArray();var humanRotations=humans.Select(t=>t.localRotation).ToArray();
                    physics.Step(1f/fps);Require(physics.StateFinite(),"non-finite native rig at motion "+motion);
                    report.maximumSubsteps=Mathf.Max(report.maximumSubsteps,physics.LastStepCount);
                    report.maximumOffset=Mathf.Max(report.maximumOffset,physics.RotationOffsets().Select(q=>Quaternion.Angle(Quaternion.identity,q)).Max());
                    report.maximumPositionError=Mathf.Max(report.maximumPositionError,transforms.Select((t,i)=>Vector3.Distance(t.localPosition,positions[i])).Max());
                    report.maximumHumanError=Mathf.Max(report.maximumHumanError,humans.Select((t,i)=>Quaternion.Angle(t.localRotation,humanRotations[i])).Max());report.frames++;
                }
            }
        }
        wardrobe.Apply(0);Require(physics.NodeCount==report.baseNodes,"default costume does not restore native binding count");
        Require(report.maximumPositionError<.00001f&&report.maximumHumanError<.1f,"solver changes authored primary pose");
        Require(report.maximumOffset<90.1f,"secondary rotations exceed safety limit");physics.ResetDynamics();
        report.passed=report.garmentRebindStable=report.authoredPosePreserved=true;report.outfits=5;report.motions=values.Length;
        File.WriteAllText(Path.GetFullPath("../research/physics-check.json"),JsonUtility.ToJson(report,true));
        Debug.Log("HLC_PHYSICS_CHECK_PASSED "+JsonUtility.ToJson(report));
    }
}
