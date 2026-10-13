using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Hlc.ChillPrototype;
public static class ChillFeaturesBuild
{
    public static void Configure(ChillGraphicsPlayer player)
    {
        var cursor=player.GetComponent<ChillCursor>()??player.gameObject.AddComponent<ChillCursor>();cursor.player=player;
        Texture2D CursorTexture(string name)
        {
            string path="Assets/Recovered/Texture2D/"+name+".png";var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new InvalidOperationException("Missing native cursor "+name);
            if(!importer.isReadable||importer.textureCompression!=TextureImporterCompression.Uncompressed){importer.isReadable=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.SaveAndReimport();}
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        cursor.normal=CursorTexture("t_ui_cursor");cursor.talk=CursorTexture("t_ui_cursor_talk");cursor.blocked=CursorTexture("t_ui_cursor_talkblock");
        ResolveNativeMotions(player);
        player.story.featureConfiguration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/chill-features.json");
        if(!player.story.featureConfiguration)throw new InvalidOperationException("Run prepare_features.py before building.");
        var source=JsonUtility.FromJson<ChillFeatureData>(player.story.featureConfiguration.text);
        if(source.wardrobe!=null)
        {
            var wardrobe=player.GetComponent<ChillWardrobe>()??player.gameObject.AddComponent<ChillWardrobe>();var paths=UnityEngine.Object.FindObjectsOfType<Transform>(true).GroupBy(t=>{string path=t.name;for(var parent=t.parent;parent;parent=parent.parent)path=parent.name+"/"+path;return path;}).ToDictionary(g=>g.Key,g=>g.First());
            Transform Find(string path)=>paths.TryGetValue(path,out var value)?value:null;
            wardrobe.defaults=source.wardrobe.defaults.Select(Find).Where(t=>t).Select(t=>t.gameObject).ToArray();wardrobe.costumeParent=Find(source.wardrobe.parent);wardrobe.boneParent=Find(source.wardrobe.bones);
            wardrobe.prefabs=new[]{"Costume_Polo_001","Costume_Tee_001"}.Select(name=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Recovered/GameObject/"+name+".prefab")).ToArray();wardrobe.skins=source.wardrobe.materials.Select(guid=>AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
            if(wardrobe.prefabs.Any(p=>!p)||wardrobe.skins.Any(s=>!s))throw new InvalidOperationException("Native summer costumes are missing.");
        }
        var volume=player.GetComponent<Volume>()??player.gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=20;volume.weight=1;player.environment.postVolume=volume;
        player.view.GetUniversalAdditionalCameraData().renderPostProcessing=true;player.view.allowHDR=true;
        if(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)pipeline.supportsHDR=true;
        for(int i=0;i<source.post.Length;i++)
        {
            var data=source.post[i];string path="Assets/Prototype/WebPost_"+i+".asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);}
            T Component<T>() where T:VolumeComponent {if(!profile.TryGet<T>(out var item)){item=profile.Add<T>(true);AssetDatabase.AddObjectToAsset(item,profile);}return item;}
            var color=Component<ColorAdjustments>();color.postExposure.Override(Mathf.Log(Mathf.Max(.001f,data.brightness),2));color.contrast.Override((data.contrast-1)*100);color.saturation.Override((data.saturation-1)*100);color.colorFilter.Override(Color.white);
            var bloom=Component<Bloom>();bloom.intensity.Override(data.bloom);bloom.threshold.Override(data.threshold);bloom.scatter.Override(.7f);bloom.highQualityFiltering.Override(false);
            var grade=Component<LiftGammaGain>();grade.lift.overrideState=data.liftOverride;grade.lift.value=data.lift;grade.gamma.overrideState=data.gammaOverride;grade.gamma.value=data.gamma;grade.gain.overrideState=data.gainOverride;grade.gain.value=data.gain;
            EditorUtility.SetDirty(color);EditorUtility.SetDirty(bloom);EditorUtility.SetDirty(grade);EditorUtility.SetDirty(profile);
            player.environment.presets[i].postProfile=profile;
        }
        volume.sharedProfile=player.environment.presets[player.environment.selectedTime].postProfile;
        Debug.Log("HLC_SOURCE_POST_PROFILES day/sunset/night/cloud originalLiftGammaGain=true colorAndBloom=URP-adapted");
    }
    static void ResolveNativeMotions(ChillGraphicsPlayer player)
    {
        var controller=player.story.storyController as AnimatorController;
        var source=JsonUtility.FromJson<ChillStoryData>(File.ReadAllText("Assets/StreamingAssets/chill-story.json"));
        var paths=new Dictionary<AnimatorState,string>();var transitions=new List<AnimatorTransitionBase>();
        void Walk(AnimatorStateMachine machine,string path)
        {
            transitions.AddRange(machine.anyStateTransitions);transitions.AddRange(machine.entryTransitions);
            foreach(var state in machine.states){paths[state.state]=path+"."+state.state.name;transitions.AddRange(state.state.transitions);}
            foreach(var child in machine.stateMachines){transitions.AddRange(machine.GetStateMachineTransitions(child.stateMachine));Walk(child.stateMachine,path+"."+child.stateMachine.name);}
        }
        Walk(controller.layers[0].stateMachine,controller.layers[0].stateMachine.name);
        bool Matches(AnimatorTransitionBase transition,int value)
        {return transition.conditions.All(c=>c.parameter!="MotionType" || c.mode==AnimatorConditionMode.Equals&&value==c.threshold || c.mode==AnimatorConditionMode.NotEqual&&value!=c.threshold || c.mode==AnimatorConditionMode.Greater&&value>c.threshold || c.mode==AnimatorConditionMode.Less&&value<c.threshold);}
        AnimatorState Destination(AnimatorTransitionBase transition,int value)
        {
            var result=transition.destinationState;
            if(!result&&transition.destinationStateMachine)
            {var machine=transition.destinationStateMachine;var entry=machine.entryTransitions.FirstOrDefault(t=>Matches(t,value));result=entry!=null?Destination(entry,value):machine.defaultState;}
            for(int i=0;i<8&&result&&!result.motion;i++)
            {var next=result.transitions.FirstOrDefault(t=>!t.isExit&&!t.hasExitTime&&Matches(t,value));if(next==null)break;var candidate=next.destinationState;if(!candidate)break;result=candidate;}
            return result;
        }
        var list=player.story.presentation.statePaths.ToList();
        foreach(var motion in source.motions.Where(m=>!list.Any(s=>s.motion==m.value)))
        {
            var transition=transitions.FirstOrDefault(t=>t.conditions.Any(c=>c.parameter=="MotionType"&&c.mode==AnimatorConditionMode.Equals&&c.threshold==motion.value)&&Matches(t,motion.value));
            if(transition==null)transition=transitions.Where(t=>(t.destinationState||t.destinationStateMachine)&&t.conditions.Any(c=>c.parameter=="MotionType")&&Matches(t,motion.value))
                .OrderBy(t=>{float lower=t.conditions.Where(c=>c.parameter=="MotionType"&&c.mode==AnimatorConditionMode.Greater).Select(c=>c.threshold).DefaultIfEmpty(-10000).Max();float upper=t.conditions.Where(c=>c.parameter=="MotionType"&&c.mode==AnimatorConditionMode.Less).Select(c=>c.threshold).DefaultIfEmpty(10000).Min();return upper-lower;}).FirstOrDefault();
            var target=transition!=null?Destination(transition,motion.value):null;
            if(target&&paths.TryGetValue(target,out var path)){list.Add(new ChillStoryPresentation.NamedState{name=motion.name,motion=motion.value,path=path});Debug.Log("HLC_NATIVE_MOTION_ENTRY "+motion.name+" => "+path);}
        }
        player.story.presentation.statePaths=list.ToArray();
    }
}
