#if UNITY_EDITOR || UNITY_STANDALONE_WIN
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hlc.ChillPrototype;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public sealed class ChillReferenceRecorder:MonoBehaviour
{
    int[] RecordedMotions={0,50,57,58,100,101,102,103,104,105,200,250,300,600,650,750,752,1500};
    [Serializable]class Configuration{public Group[] groups;public Collision[] colliders;}
    [Serializable]class Group{public string path,parameters;public string[] roots,colliders;}
    [Serializable]class Collision{public string id,path;public bool sphere,radiusSeparation,reverseDirection,alignedOnCenter;public int axis;public float[] center,size;}
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    Assembly native;ChillGraphicsPlayer player;Animator animator;readonly List<Component> cloths=new List<Component>();
    Transform[] bones;Quaternion[] authored;int frame,warmup,simulationFrames;float until;string state;bool recording;ChillBoneBake.Clip clip;ChillBoneBake output;int nextClip;
    static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
    static FieldInfo Field(object o,string name)=>o.GetType().GetField(name,Flags)??throw new MissingFieldException(o.GetType().FullName,name);
    static object Get(object o,string name)=>Field(o,name).GetValue(o);
    static void Set(object o,string name,object value){var f=Field(o,name);f.SetValue(o,f.FieldType.IsEnum?Enum.ToObject(f.FieldType,value):value);}
    static Vector3 Vec(float[] v)=>new Vector3(v[0],v[1],v[2]);
    public void Configure(Assembly assembly)
    {
        native=assembly;player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();animator=player.character;
        animator.runtimeAnimatorController=player.story.storyController;animator.Rebind();animator.Update(0);animator.speed=0;
        if(Environment.GetEnvironmentVariable("CHILL_NATIVE_ALL_MOTIONS")=="1")RecordedMotions=player.story.presentation.statePaths.Where(p=>animator.HasState(0,Animator.StringToHash(p.path))).Select(p=>p.motion).Distinct().OrderBy(m=>m).ToArray();
        var config=JsonUtility.FromJson<Configuration>(File.ReadAllText("../.tools/native-reference.json"));
        var transforms=UnityEngine.Object.FindObjectsOfType<Transform>(true).GroupBy(PathOf).ToDictionary(g=>g.Key,g=>g.First());
        var colliders=new Dictionary<string,Component>();
        foreach(var source in config.colliders)
        {
            var type=native.GetType("MagicaCloth2."+(source.sphere?"MagicaSphereCollider":"MagicaCapsuleCollider"),true);
            var c=transforms[source.path].gameObject.AddComponent(type);Set(c,"center",Vec(source.center));Set(c,"size",Vec(source.size));
            if(!source.sphere){Set(c,"direction",source.axis);Set(c,"radiusSeparation",source.radiusSeparation);Set(c,"reverseDirection",source.reverseDirection);Set(c,"alignedOnCenter",source.alignedOnCenter);}
            colliders.Add(source.id,c);
        }
        foreach(var source in config.groups)
        {
            var c=transforms[source.path].gameObject.AddComponent(native.GetType("MagicaCloth2.MagicaCloth",true));
            var data=Get(c,"serializeData");JsonUtility.FromJsonOverwrite(source.parameters,data);
            var roots=(IList)Get(data,"rootBones");roots.Clear();foreach(string path in source.roots)roots.Add(transforms[path]);
            var collision=Get(data,"colliderCollisionConstraint");var list=(IList)Get(collision,"colliderList");list.Clear();foreach(string id in source.colliders)list.Add(colliders[id]);
            c.GetType().GetMethod("BuildAndRun").Invoke(c,null);cloths.Add(c);
        }
        player.bones.InitializeForPreview();output=new ChillBoneBake{profileHash=player.bones.BakeProfileHash,paths=player.bones.BonePaths(),clips=new ChillBoneBake.Clip[RecordedMotions.Length]};
        bones=output.paths.Select(p=>animator.transform.Find(p)).ToArray();if(bones.Any(t=>!t))throw new InvalidOperationException("Reference rig paths differ from the web rig");authored=new Quaternion[bones.Length];until=Time.realtimeSinceStartup+60;
        var manager=native.GetType("MagicaCloth2.MagicaManager");
        manager.GetField("OnPreSimulation",BindingFlags.Static|BindingFlags.Public).SetValue(null,(Action)BeforeSimulation);
        manager.GetField("OnPostSimulation",BindingFlags.Static|BindingFlags.Public).SetValue(null,(Action)AfterSimulation);
        Time.captureDeltaTime=1f/60;
        Debug.Log("HLC_NATIVE_REFERENCE_BUILDING groups="+cloths.Count);
    }
    void Update()
    {
        try
        {
            if(!recording)
            {
                if(!cloths.All(c=>(bool)c.GetType().GetMethod("IsValid").Invoke(c,null)))
                {if(Time.realtimeSinceStartup>until)throw new InvalidOperationException("Native cloth failed to build");return;}
                BeginClip();
            }
            animator.Play(state,0,clip.loop?(float)frame/simulationFrames:Mathf.Clamp01((float)(frame-warmup)/simulationFrames));animator.Update(0);
        }
        catch(Exception error){Debug.LogException(error);Exit(1);}
    }
    void BeginClip()
    {
        int motion=RecordedMotions[nextClip];
        state=player.story.presentation.statePaths.First(p=>p.motion==motion).path;animator.speed=1;animator.Play(state,0,0);animator.Update(0);
        var clips=animator.GetCurrentAnimatorClipInfo(0);float duration=clips.Length>0?clips[0].clip.length:animator.GetCurrentAnimatorStateInfo(0).length;animator.speed=0;
        if(float.IsNaN(duration)||float.IsInfinity(duration)||duration<=0||duration>120)throw new InvalidOperationException("Invalid native reference clip duration: "+state+" "+duration);
        clip=new ChillBoneBake.Clip{motion=motion,state=state,seconds=duration,loop=animator.GetCurrentAnimatorStateInfo(0).loop};
        // Simulate at 60 Hz, retain 30 Hz samples, and settle looping cloth for two complete cycles.
        clip.frames=new ChillBoneBake.Frame[Mathf.CeilToInt(clip.seconds*30)+1];simulationFrames=(clip.frames.Length-1)*2;warmup=clip.loop?simulationFrames*2:120;output.clips[nextClip]=clip;frame=0;recording=true;
        foreach(var c in cloths)c.GetType().GetMethod("ResetCloth").Invoke(c,new object[]{true});
        Debug.Log("HLC_NATIVE_REFERENCE_RECORDING motion="+motion+" frames="+clip.frames.Length);
    }
    void BeforeSimulation(){if(!recording)return;for(int i=0;i<bones.Length;i++)authored[i]=bones[i].localRotation;}
    void AfterSimulation()
    {
        if(!recording)return;
        try
        {
            if(frame>=warmup&&(frame-warmup)%2==0)
            {
                var offsets=bones.Select((t,i)=>Quaternion.Inverse(authored[i])*t.localRotation).ToArray();
                if(offsets.Any(q=>float.IsNaN(q.x)||float.IsInfinity(q.x)))throw new InvalidOperationException("Native reference produced invalid rotations");
                clip.frames[(frame-warmup)/2]=new ChillBoneBake.Frame{offsets=offsets};
            }
            frame++;
            if(frame<warmup+simulationFrames+1)return;
            recording=false;nextClip++;if(nextClip<output.clips.Length)return;
            float maximum=output.clips.SelectMany(c=>c.frames).SelectMany(f=>f.offsets).Max(q=>Quaternion.Angle(Quaternion.identity,q));
            if(maximum<.01f)throw new InvalidOperationException("Native callback did not capture simulated transforms");
            File.WriteAllText("Assets/Prototype/native-bone-bakes.json",JsonUtility.ToJson(output));
            File.WriteAllText("../research/native-physics-reference-check.json","{\"passed\":true,\"solver\":\"source MagicaClothV2 DLL\",\"groups\":"+cloths.Count+",\"bones\":"+bones.Length+",\"motions\":["+string.Join(",",RecordedMotions)+"],\"maximumOffsetDegrees\":"+maximum.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+",\"PCScreenshotParity\":false}");
            Debug.Log("HLC_NATIVE_REFERENCE_CAPTURED maximum="+maximum);Exit(0);
        }
        catch(Exception error){Debug.LogException(error);Exit(1);}
    }
    static void Exit(int code)
    {
#if UNITY_EDITOR
        EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
}
#endif
