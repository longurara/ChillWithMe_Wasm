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
    public static void Build()
    {
        EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");
        var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();
        var physics=player.bones;var animator=player.character;
        physics.configuration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/bone-physics.json");
        physics.useBakedFallback=false;physics.bakedConfiguration=null;
        animator.runtimeAnimatorController=player.story.storyController;animator.Rebind();animator.Update(0);physics.RebindGarment(null);
        var values=new[]{50,752,1500};
        var result=new ChillBoneBake{profileHash=Hash128.Compute(physics.configuration.text).ToString(),paths=physics.BonePaths(),clips=new ChillBoneBake.Clip[values.Length]};
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
                if(!physics.StateFinite()||offsets.Any(q=>float.IsNaN(q.x)||Quaternion.Angle(Quaternion.identity,q)>40.1f))throw new InvalidOperationException("Invalid physics bake: "+source.path);
                clip.frames[frame]=new ChillBoneBake.Frame{offsets=offsets};
            }
        }
        File.WriteAllText("Assets/Prototype/bone-bakes.json",JsonUtility.ToJson(result));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        // Check eligibility, matching sample, and crossfade back to live dynamics.
        physics.bakedConfiguration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/bone-bakes.json");physics.useBakedFallback=true;physics.RebindGarment(null);
        animator.Play(result.clips[0].state,0,.4f);animator.Update(0);physics.ResetDynamics();physics.Step(0);
        for(int frame=0;frame<10;frame++){physics.BeforeAnimator();animator.Play(result.clips[0].state,0,.4f);animator.Update(0);physics.Step(1f/60);}
        if(!physics.BakedFallbackActive)throw new InvalidOperationException("Bake is not eligible in its recorded state");
        var actual=physics.RotationOffsets();
        if(actual.Length!=result.paths.Length)throw new InvalidOperationException("Bake rig mapping differs");
        float error=actual.Select((q,i)=>Quaternion.Angle(q,result.clips[0].Sample(i,.4f))).Max();
        if(error>.2f)throw new InvalidOperationException("Bake playback does not match recorded offsets: "+error);
        physics.useBakedFallback=false;
        for(int frame=0;frame<10;frame++){physics.BeforeAnimator();animator.Update(0);physics.Step(1f/60);}
        if(physics.BakedFallbackActive||!physics.StateFinite())throw new InvalidOperationException("Bake does not return to live physics");
        result.profileHash="changed profile";var invalid=new TextAsset(JsonUtility.ToJson(result));physics.bakedConfiguration=invalid;physics.useBakedFallback=true;physics.RebindGarment(null);
        for(int frame=0;frame<10;frame++){physics.BeforeAnimator();animator.Play(result.clips[0].state,0,.4f);animator.Update(0);physics.Step(1f/60);}
        if(physics.BakedFallbackActive)throw new InvalidOperationException("Stale physics profile accepts recorded offsets");
        UnityEngine.Object.DestroyImmediate(invalid);
        File.WriteAllText(Path.GetFullPath("../research/physics-bake-check.json"),"{\"passed\":true,\"solver\":\"web CPU solver, not PC Magica\",\"motions\":[50,752,1500],\"bones\":"+result.paths.Length+",\"bytes\":"+new FileInfo("Assets/Prototype/bone-bakes.json").Length+",\"profileHashGuard\":true,\"stateAndRigGuard\":true,\"crossfadeSeconds\":0.12,\"customCostumeFallback\":false}");
        Debug.Log("HLC_PHYSICS_BAKE_PASSED motions=3 bones="+result.paths.Length);
    }
}
