using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Hlc.ChillPrototype;

public static class ChillMotionBuild
{
    [Serializable] class Config { public Desk desk; public CameraConfig camera; public Look look; public Shine[] sunglasses; }
    [Serializable] class Desk
    {
        public string breakBookAnimator,workBookAnimator,deskObjectAnimator,chairAnimator,penAnimator,windowAnimator,musicboxAnimator,coffeeDropParticle;
        public string[] cupcake,cookie,breadA,breadB;
        public ChillDeskObjects.MotionAction[] actions;
        public Sleeve[] sleeves;
    }
    [Serializable] class Sleeve { public string renderer,bone; }
    [Serializable] class CameraConfig { public string animator; public string[] fixedViews; public Pose[] poses; }
    [Serializable] class Pose { public string path,follow,lookAt; public float[] followOffset,lookOffset; public float fov; public bool useFollow; }
    [Serializable] class Look { public string face,head,eyes,target; }
    [Serializable] class Shine { public string @object,material; }
    static string PathOf(Transform t) => t.parent ? PathOf(t.parent)+"/"+t.name : t.name;
    static Vector3 Vec(float[] v) => v != null && v.Length == 3 ? new Vector3(v[0],v[1],v[2]) : Vector3.zero;

    public static void Configure(ChillGraphicsPlayer player)
    {
        var config=JsonUtility.FromJson<Config>(File.ReadAllText("Assets/Prototype/motion-scene.json"));
        var transforms=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).GroupBy(PathOf).ToDictionary(g => g.Key,g => g.First());
        Transform Find(string path) => path != null && transforms.TryGetValue(path,out var t) ? t : null;
        Animator Anim(string path) => Find(path)?.GetComponent<Animator>();
        GameObject[] Objects(string[] paths) => (paths ?? Array.Empty<string>()).Select(p => Find(p)?.gameObject).ToArray();
        var desk=player.deskObjects; var d=config.desk;
        desk.breakBookAnimator=Anim(d.breakBookAnimator); desk.penAnimator=Anim(d.penAnimator); desk.windowAnimator=Anim(d.windowAnimator); desk.musicboxAnimator=Anim(d.musicboxAnimator);
        desk.coffeeDrop=Find(d.coffeeDropParticle)?.GetComponent<ParticleSystem>();
        desk.cupcake=Objects(d.cupcake); desk.cookie=Objects(d.cookie); desk.breadA=Objects(d.breadA); desk.breadB=Objects(d.breadB); desk.motionActions=d.actions;
        if (desk.chairAnimator) desk.chairAnimator.runtimeAnimatorController=PoseOnlyChair(desk.chairAnimator.runtimeAnimatorController as AnimatorController);
        desk.CaptureDefaults();
        desk.HideSnacks();
        var presentation=player.gameObject.AddComponent<ChillStoryPresentation>(); player.story.presentation=presentation; presentation.player=player;
        var paths=new List<ChillStoryPresentation.NamedState>();
        var stateObjects=new Dictionary<AnimatorState,string>();
        void StatePaths(AnimatorStateMachine machine,string prefix)
        {
            foreach (var state in machine.states)
            { string path=prefix+"."+state.state.name; paths.Add(new ChillStoryPresentation.NamedState { name=state.state.name,path=path }); stateObjects[state.state]=path; }
            foreach (var child in machine.stateMachines) StatePaths(child.stateMachine,prefix+"."+child.stateMachine.name);
        }
        var storyMachine=(player.story.storyController as AnimatorController).layers[0].stateMachine;
        StatePaths(storyMachine,storyMachine.name);
        var storyData=JsonUtility.FromJson<ChillStoryData>(File.ReadAllText("Assets/StreamingAssets/chill-story.json"));
        var byInteger=new Dictionary<int,AnimatorState>();
        void Transition(AnimatorTransitionBase transition)
        {
            if (!transition.destinationState) return;
            foreach (var condition in transition.conditions)
                if (condition.parameter == "MotionType" && condition.mode == AnimatorConditionMode.Equals && !byInteger.ContainsKey((int)condition.threshold)) byInteger[(int)condition.threshold]=transition.destinationState;
        }
        void Transitions(AnimatorStateMachine machine)
        {
            foreach (var transition in machine.anyStateTransitions) Transition(transition);
            foreach (var transition in machine.entryTransitions) Transition(transition);
            foreach (var state in machine.states) foreach (var transition in state.state.transitions) Transition(transition);
            foreach (var child in machine.stateMachines)
            { foreach (var transition in machine.GetStateMachineTransitions(child.stateMachine)) Transition(transition); Transitions(child.stateMachine); }
        }
        Transitions((player.story.storyController as AnimatorController).layers[0].stateMachine);
        foreach (int value in storyData.motions.Select(m => m.value).Concat(byInteger.Keys).Distinct())
        {
            var motion=storyData.motions.FirstOrDefault(m => m.value == value);
            string name=motion?.name;
            string path=null;
            if (byInteger.TryGetValue(value,out var target) && stateObjects.TryGetValue(target,out path)) name=name ?? target.name;
            else path=paths.FirstOrDefault(p => p.name == name)?.path;
            if (path != null) paths.Add(new ChillStoryPresentation.NamedState { name=name,path=path,motion=value });
        }
        presentation.statePaths=paths.ToArray();
        presentation.face=Find(config.look.face)?.GetComponent<SkinnedMeshRenderer>(); presentation.head=Find(config.look.head); presentation.eyes=Find(config.look.eyes); presentation.lookTarget=Find(config.look.target);
        presentation.sleeves=(d.sleeves ?? Array.Empty<Sleeve>()).Select(s => new ChillStoryPresentation.Sleeve { renderer=Find(s.renderer)?.GetComponent<SkinnedMeshRenderer>(),bone=Find(s.bone) }).ToArray();
        var cameras=player.gameObject.AddComponent<ChillStoryCamera>(); presentation.cameras=cameras; cameras.view=player.view; cameras.cameraAnimator=Anim(config.camera.animator);
        cameras.fixedViews=config.camera.fixedViews.Select(Find).ToArray();
        cameras.actor=player.character;
        cameras.poses=config.camera.poses.Select(p => new ChillStoryCamera.Pose { transform=Find(p.path),follow=Find(p.follow),lookAt=Find(p.lookAt),followOffset=Vec(p.followOffset),lookOffset=Vec(p.lookOffset),useFollow=p.useFollow,fov=p.fov }).ToArray();
        player.story.effects.presentation=presentation;
        player.story.effects.sunglasses=config.sunglasses.Select(s => Find(s.@object)?.GetComponentInChildren<Renderer>(true)).Where(r => r).ToArray();
        foreach (var animator in new[] { player.character,desk.deskObjectAnimator,desk.workBookAnimator,desk.breakBookAnimator,desk.chairAnimator,desk.penAnimator,desk.bearsRestaurantBookAnimator,desk.windowAnimator,desk.musicboxAnimator,cameras.cameraAnimator })
        {
            if (!animator) continue;
            var receiver=animator.GetComponent<ChillAnimationActions>() ?? animator.gameObject.AddComponent<ChillAnimationActions>();
            receiver.character=animator; receiver.objects=desk; receiver.story=player.story; receiver.presentation=animator == player.character ? presentation : null; animator.fireEvents=true;
        }
        cameras.Capture();
        Debug.Log("HLC_CHILL_MOTION_BINDINGS props="+d.actions.Length+" cameras="+cameras.poses.Length+" face="+(presentation.face ? presentation.face.name : "missing")+" sleeves="+presentation.sleeves.Length);
    }

    static RuntimeAnimatorController PoseOnlyChair(AnimatorController source)
    {
        if (!source) return null;
        const string path="Assets/Prototype/ChairPose.controller";
        if (AssetDatabase.LoadMainAssetAtPath(path)) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source),path);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        Directory.CreateDirectory("Assets/Prototype/PoseClips");
        var clips=new Dictionary<AnimationClip,AnimationClip>();
        foreach (var original in source.animationClips.Distinct())
        {
            string clipPath="Assets/Prototype/PoseClips/"+original.name+".anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (!clip) { clip=UnityEngine.Object.Instantiate(original); AssetDatabase.CreateAsset(clip,clipPath); }
            else EditorUtility.CopySerialized(original,clip);
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                if (binding.type != typeof(Transform) && !(binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape."))) AnimationUtility.SetEditorCurve(clip,binding,null);
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) AnimationUtility.SetObjectReferenceCurve(clip,binding,null);
            clips[original]=clip; EditorUtility.SetDirty(clip);
        }
        void Rewrite(AnimatorStateMachine machine)
        {
            foreach (var state in machine.states) if (state.state.motion is AnimationClip clip && clips.TryGetValue(clip,out var pose)) state.state.motion=pose;
            foreach (var child in machine.stateMachines) Rewrite(child.stateMachine);
        }
        foreach (var layer in controller.layers) Rewrite(layer.stateMachine);
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
        Debug.Log("HLC_CHILL_CHAIR_POSE_CLIPS "+clips.Count);
        return controller;
    }
}
