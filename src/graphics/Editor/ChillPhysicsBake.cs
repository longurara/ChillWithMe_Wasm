using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Hlc.ChillPrototype;

// Records the web solver, not Windows Magica output. Re-run after profile changes.
public static class ChillPhysicsBake
{
    public static string SolverFingerprint()
    {
        var source=File.ReadAllText("../src/graphics/Runtime/ChillBonePhysics.cs").Replace("\r\n","\n");
        var garment=File.ReadAllText("../src/graphics/Runtime/ChillBonePhysics.Garments.cs").Replace("\r\n","\n");
        garment=garment.Substring(garment.IndexOf("void ResolveGarmentContacts()",StringComparison.Ordinal));
        using(var hash=System.Security.Cryptography.SHA256.Create())return string.Concat(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(source+"\n"+garment)).Select(b=>b.ToString("x2")));
    }
    public static void VerifyExistingBake()
    {
        var report=File.ReadAllText("../research/physics-coverage-check.json");
        if(!report.Contains("\"solverFingerprint\":\""+SolverFingerprint()+"\""))throw new InvalidOperationException("Physics solver changed; regenerate the bake");
        var asset=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/bone-bakes.bytes");var data=ChillBoneBakeCodec.Read(asset);
        if(data==null||data.clips.Length<18||data.clips.Any(c=>c.frames.Length<2))throw new InvalidOperationException("Invalid existing physics coverage");
    }
    public static void Build()
    {
        EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");
        var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();
        var physics=player.bones;var animator=player.character;
        physics.configuration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/bone-physics.json");
        physics.useBakedFallback=false;physics.bakedConfiguration=null;
        animator.runtimeAnimatorController=player.story.storyController;animator.Rebind();animator.Update(0);physics.RebindGarment(null);
        var values=new[]{0,50,57,58,100,101,102,103,104,105,200,250,300,600,650,750,752,1500};
        var result=new ChillBoneBake{profileHash=physics.BakeProfileHash,paths=physics.BonePaths(),clips=new ChillBoneBake.Clip[values.Length]};
        if(File.Exists("Assets/Prototype/native-bone-bakes.json"))
        {
            var native=JsonUtility.FromJson<ChillBoneBake>(File.ReadAllText("Assets/Prototype/native-bone-bakes.json"));
            if(native.profileHash!=physics.BakeProfileHash||!native.paths.SequenceEqual(result.paths)||native.clips.Any(c=>!player.story.presentation.statePaths.Any(p=>p.motion==c.motion&&p.path==c.state)||c.frames==null||c.frames.Length<2||c.frames.Any(f=>f.offsets.Length!=native.paths.Length||f.offsets.Any(q=>new[]{q.x,q.y,q.z,q.w}.Any(v=>float.IsNaN(v)||float.IsInfinity(v))||Mathf.Abs(q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w-1)>.01f))))
                throw new InvalidOperationException("Native physics reference does not match this profile/rig");
            native.producer="source-magica";
            File.WriteAllBytes("Assets/Prototype/bone-bakes.bytes",ChillBoneBakeCodec.Write(native));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            physics.bakedConfiguration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/bone-bakes.bytes");physics.useBakedFallback=true;physics.RebindGarment(null);
            float nativeError=0,maximumReturnStep=0;
            foreach(var reference in native.clips){animator.Play(reference.state,0,.4f);animator.Update(0);physics.ResetDynamics();physics.Step(0);
                if(reference.loop&&Enumerable.Range(0,native.paths.Length).Any(i=>Quaternion.Angle(reference.Sample(i,.99999f),reference.Sample(i,1.00001f))>.5f))throw new InvalidOperationException("Native loop seam is discontinuous: "+reference.state);
                for(int frame=0;frame<20;frame++){physics.BeforeAnimator();animator.Play(reference.state,0,.4f);animator.Update(0);physics.Step(1f/60);}
                nativeError=Mathf.Max(nativeError,physics.RotationOffsets().Select((q,i)=>Quaternion.Angle(q,reference.Sample(i,.4f))).Max());
                if(!physics.BakedFallbackActive||nativeError>.2f)throw new InvalidOperationException("Native bake playback differs from reference: "+reference.state+" "+nativeError);
                var prior=physics.RotationOffsets();physics.useBakedFallback=false;physics.BeforeAnimator();animator.Play(reference.state,0,.4f);animator.Update(0);physics.Step(1f/60);
                maximumReturnStep=Mathf.Max(maximumReturnStep,physics.RotationOffsets().Select((q,i)=>Quaternion.Angle(q,prior[i])).Max());
                if(!physics.StateFinite()||maximumReturnStep>3)throw new InvalidOperationException("Native-to-live transition jumps: "+reference.state+" "+maximumReturnStep);
                physics.useBakedFallback=true;
            }
            File.WriteAllText("../research/physics-bake-check.json","{\"passed\":true,\"solver\":\"source MagicaClothV2 reference\",\"motions\":["+string.Join(",",values)+"],\"bones\":"+native.paths.Length+",\"maximumPlaybackErrorDegrees\":"+nativeError.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+",\"maximumReturnStepDegrees\":"+maximumReturnStep.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+",\"loopSeamGuardDegrees\":0.5,\"PCScreenshotParity\":false}");
            ExpandAllStates(player,physics,native);
            Debug.Log("HLC_NATIVE_PHYSICS_BAKE_PASSED error="+nativeError);return;
        }
        for(int clipIndex=0;clipIndex<values.Length;clipIndex++)
        {
            var source=player.story.presentation.statePaths.First(p=>p.motion==values[clipIndex]);
            physics.ResetDynamics();animator.Play(source.path,0,0);animator.Update(0);physics.Step(0);
            float seconds=animator.GetCurrentAnimatorStateInfo(0).length;
            if(seconds<=0||seconds>30)throw new InvalidOperationException("Invalid bake length: "+source.path);
            int frames=Mathf.CeilToInt(seconds*60)+1;
            var clip=new ChillBoneBake.Clip{state=source.path,motion=values[clipIndex],seconds=seconds,frames=new ChillBoneBake.Frame[frames]};result.clips[clipIndex]=clip;
            for(int frame=0;frame<frames;frame++)
            {
                physics.BeforeAnimator();animator.Play(source.path,0,(float)frame/(frames-1));animator.Update(0);physics.Step(frame==0?0:seconds/(frames-1));
                var offsets=physics.RotationOffsets();
                if(!physics.StateFinite()||offsets.Any(q=>float.IsNaN(q.x)||Quaternion.Angle(Quaternion.identity,q)>90.1f))throw new InvalidOperationException("Invalid physics bake: "+source.path);
                clip.frames[frame]=new ChillBoneBake.Frame{offsets=offsets};
            }
        }
        ExpandAllStates(player,physics,result);
        File.WriteAllBytes("Assets/Prototype/bone-bakes.bytes",ChillBoneBakeCodec.Write(result));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        // Check eligibility, matching sample, and crossfade back to live dynamics.
        physics.bakedConfiguration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/bone-bakes.bytes");physics.useBakedFallback=true;physics.RebindGarment(null);
        animator.Play(result.clips[0].state,0,.4f);animator.Update(0);physics.ResetDynamics();physics.Step(0);
        for(int frame=0;frame<20;frame++){physics.BeforeAnimator();animator.Play(result.clips[0].state,0,.4f);animator.Update(0);physics.Step(1f/60);}
        if(!physics.BakedFallbackActive)throw new InvalidOperationException("Bake is not eligible in its recorded state");
        var actual=physics.RotationOffsets();
        if(actual.Length!=result.paths.Length)throw new InvalidOperationException("Bake rig mapping differs");
        float error=actual.Select((q,i)=>Quaternion.Angle(q,result.clips[0].Sample(i,.4f))).Max();
        if(error>.2f)throw new InvalidOperationException("Bake playback does not match recorded offsets: "+error);
        physics.useBakedFallback=false;
        for(int frame=0;frame<20;frame++){physics.BeforeAnimator();animator.Update(0);physics.Step(1f/60);}
        if(physics.BakedFallbackActive||!physics.StateFinite())throw new InvalidOperationException("Bake does not return to live physics");
        result.profileHash="changed profile";var invalid=new TextAsset(JsonUtility.ToJson(result));physics.bakedConfiguration=invalid;physics.useBakedFallback=true;physics.RebindGarment(null);
        for(int frame=0;frame<10;frame++){physics.BeforeAnimator();animator.Play(result.clips[0].state,0,.4f);animator.Update(0);physics.Step(1f/60);}
        if(physics.BakedFallbackActive)throw new InvalidOperationException("Stale physics profile accepts recorded offsets");
        UnityEngine.Object.DestroyImmediate(invalid);
        File.WriteAllText(Path.GetFullPath("../research/physics-bake-check.json"),"{\"passed\":true,\"solver\":\"web CPU solver, not PC Magica\",\"bones\":"+result.paths.Length+",\"bytes\":"+new FileInfo("Assets/Prototype/bone-bakes.bytes").Length+",\"profileHashGuard\":true,\"stateAndRigGuard\":true,\"crossfadeSeconds\":0.25,\"customCostumeFallback\":true}");
        Debug.Log("HLC_PHYSICS_BAKE_PASSED motions="+values.Length+" bones="+result.paths.Length);
    }
    static void ExpandAllStates(ChillGraphicsPlayer player,ChillBonePhysics physics,ChillBoneBake result)
    {
        var animator=player.character;bool native=result.producer=="source-magica";
        foreach(var clip in result.clips)clip.nativeReference=native;
        var clips=result.clips.ToList();physics.useBakedFallback=false;
        var states=player.story.presentation.statePaths.GroupBy(p=>p.path).Select(g=>g.First()).Where(p=>animator.HasState(0,Animator.StringToHash(p.path))).ToArray();
        foreach(var source in states)
        {
            if(clips.Any(c=>c.state==source.path))continue;
            physics.ResetDynamics();animator.Play(source.path,0,0);animator.Update(0);physics.Step(0);
            var state=animator.GetCurrentAnimatorStateInfo(0);float seconds=state.length;
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0||seconds>120)throw new InvalidOperationException("Invalid expanded bake duration: "+source.path);
            int frames=Mathf.CeilToInt(seconds*30)+1;
            var clip=new ChillBoneBake.Clip{state=source.path,motion=source.motion,seconds=seconds,loop=state.loop,frames=new ChillBoneBake.Frame[frames]};
            int warmup=clip.loop?(frames-1)*4:120;
            for(int frame=0;frame<warmup;frame++){physics.BeforeAnimator();animator.Play(source.path,0,clip.loop?(float)frame/((frames-1)*2):0);animator.Update(0);physics.Step(1f/60);}
            for(int frame=0;frame<frames;frame++)
            {
                physics.BeforeAnimator();animator.Play(source.path,0,(float)frame/(frames-1));animator.Update(0);physics.Step(frame==0?0:seconds/(frames-1));
                var offsets=physics.RotationOffsets();if(!physics.StateFinite()||offsets.Any(q=>float.IsNaN(q.x)||Quaternion.Angle(Quaternion.identity,q)>90.1f))throw new InvalidOperationException("Invalid expanded bake: "+source.path);
                clip.frames[frame]=new ChillBoneBake.Frame{offsets=offsets};
            }
            clips.Add(clip);
        }
        result.clips=clips.ToArray();if(native&&clips.Any(c=>!c.nativeReference))result.producer="source-magica+web-cpu";
        var bytes=ChillBoneBakeCodec.Write(result);File.WriteAllBytes("Assets/Prototype/bone-bakes.bytes",bytes);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        physics.bakedConfiguration=null;physics.RebindGarment(null);physics.bakedConfiguration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/bone-bakes.bytes");physics.useBakedFallback=true;physics.RebindGarment(null);
        File.WriteAllText("../research/physics-coverage-check.json","{\"passed\":true,\"solverFingerprint\":\""+SolverFingerprint()+"\",\"playableStates\":"+states.Length+",\"recordedStates\":"+clips.Count+",\"nativeStates\":"+clips.Count(c=>c.nativeReference)+",\"cpuStates\":"+clips.Count(c=>!c.nativeReference)+",\"bytes\":"+bytes.Length+",\"hybridGarmentMapping\":true,\"PCPixelParity\":false}");
        Debug.Log("HLC_PHYSICS_COVERAGE_PASSED states="+clips.Count+" native="+clips.Count(c=>c.nativeReference));
    }
}
