using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Hlc.ChillPrototype;

// Editor rig checks. Does not launch or automate a browser.
public static class ChillMotionAudit
{
    [Serializable] class Report
    {
        public bool passed;
        public int storyMotions,propActions,cameras,eventMethods,clothNodes,simulationFrames;
        public float maxSecondaryRotation,maxPositionError,maxHumanoidRotationError;
        public List<string> errors=new List<string>(),previews=new List<string>();
    }
    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
    public static void PrepareAndCheck() { ChillBuild.Prepare(); Check(); }
    public static void CheckAndBuild() { ChillBuild.PrepareAndPreview(); BuildPrepared(); }
    public static void BuildPrepared() { Check(); EditorSceneManager.OpenScene("Assets/Prototype/Room.unity"); ChillBuild.BuildWebGL(); }
    public static void Check()
    {
        EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");
        var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();
        var presentation=player.story.presentation;
        var report=new Report();
        void Require(bool condition,string error) { if (!condition) report.errors.Add(error); }
        var data=JsonUtility.FromJson<ChillStoryData>(File.ReadAllText("Assets/StreamingAssets/chill-story.json"));
        var used=data.novels.Where(n => n.BodyMotion >= 0).Select(n => n.BodyMotion).Distinct().ToArray();
        report.storyMotions=used.Length;
        var original=player.character.runtimeAnimatorController;
        var deskLayers=(original as AnimatorController).layers.Select(l => l.name).ToArray();
        foreach (string layer in new[] { "LookPlayer","Facial","Talk" }) Require(deskLayers.Contains(layer),"Missing desk layer "+layer);
        player.character.runtimeAnimatorController=player.story.storyController; player.character.Rebind(); player.character.Update(0);
        foreach (int value in used)
        {
            var state=presentation.statePaths.FirstOrDefault(s => s.motion == value);
            if (state != null && !player.character.HasState(0,Animator.StringToHash(state.path)))
            {
                var alternative=(player.story.storyController as AnimatorController).layers[0].stateMachine.name+state.path.Substring(state.path.IndexOf('.'));
                Debug.Log("HLC_CHILL_STATE_PATH "+value+" current="+state.path+" alternative="+alternative+" valid="+player.character.HasState(0,Animator.StringToHash(alternative))+" short="+player.character.HasState(0,Animator.StringToHash(state.name)));
            }
            Require(state != null && player.character.HasState(0,Animator.StringToHash(state.path)),"No native state for story motion "+value);
        }
        report.propActions=player.deskObjects.motionActions.Length;
        foreach (var action in player.deskObjects.motionActions)
        {
            var animator=action.kind == "Chair" ? player.deskObjects.chairAnimator : player.deskObjects.penAnimator;
            Require(animator,"Missing prop animator "+action.kind);
            if (!animator) continue;
            animator.enabled=true; animator.Rebind(); animator.Update(0);
            if (action.trigger) Require(animator.parameters.Any(p => p.name == action.state && p.type == AnimatorControllerParameterType.Trigger),"Missing prop trigger "+action.name);
            else Require(animator.HasState(0,Animator.StringToHash("Base Layer."+(action.state == "Idle" ? "Chair_Idle" : action.state))),"Missing prop state "+action.name+" / "+action.state);
        }
        Require(player.deskObjects.breakBookAnimator && player.deskObjects.windowAnimator && player.deskObjects.musicboxAnimator && player.deskObjects.coffeeDrop,"Missing book/window/musicbox/coffee binding");
        foreach (var items in new[] { player.deskObjects.cupcake,player.deskObjects.cookie,player.deskObjects.breadA,player.deskObjects.breadB }) Require(items != null && items.All(g => g),"Missing snack stage binding");
        report.cameras=presentation.cameras.poses.Length;
        Require(report.cameras == 15 && presentation.cameras.poses.All(p => p.transform),"Missing native camera viewpoint");
        Require(presentation.face && presentation.head && presentation.eyes && presentation.lookTarget,"Missing facial/look binding");
        var effects=player.story.effects;
        bool originalHat=effects.hat && effects.hat.activeSelf;
        float originalHair=effects.hair && effects.hair.sharedMesh.blendShapeCount > 1 ? effects.hair.GetBlendShapeWeight(1) : 0;
        effects.SetHat(true); effects.ResetEffects();
        Require(!effects.hat || effects.hat.activeSelf == originalHat,"Story hat did not restore");
        Require(!effects.hair || effects.hair.sharedMesh.blendShapeCount <= 1 || Mathf.Abs(effects.hair.GetBlendShapeWeight(1)-originalHair) < .01f,"Story hat left hair flattened");
        var actions=UnityEngine.Object.FindObjectsOfType<ChillAnimationActions>(true);
        var methods=new HashSet<string>();
        foreach (var receiver in actions)
        {
            receiver.previewEvents=true;
            if (!receiver.character || !receiver.character.runtimeAnimatorController) continue;
            foreach (var clip in receiver.character.runtimeAnimatorController.animationClips)
                foreach (var item in AnimationUtility.GetAnimationEvents(clip))
                { methods.Add(item.functionName); Require(typeof(ChillAnimationActions).GetMethod(item.functionName) != null,"Missing event receiver "+item.functionName); }
        }
        report.eventMethods=methods.Count;
        player.bones.InitializeForPreview(); report.clothNodes=player.bones.NodeCount;
        Require(report.clothNodes > 0,"No secondary motion bones bound");
        player.deskObjects.Select(0);
        var propTransforms=new[] { player.deskObjects.chairAnimator,player.deskObjects.penAnimator,player.deskObjects.breakBookAnimator,player.deskObjects.musicboxAnimator }
            .Where(a => a).SelectMany(a => a.GetComponentsInChildren<Transform>(true)).Distinct().ToArray();
        var propPositions=propTransforms.Select(t => t.localPosition).ToArray();
        var propRotations=propTransforms.Select(t => t.localRotation).ToArray();
        var skins=player.deskObjects.chairAnimator.GetComponentsInChildren<Renderer>(true);
        var materials=skins.Select(r => r.sharedMaterials).ToArray();
        var transforms=player.character.GetComponentsInChildren<Transform>(true);
        var humans=Enumerable.Range(0,(int)HumanBodyBones.LastBone).Select(i => player.character.GetBoneTransform((HumanBodyBones)i)).Where(t => t).ToArray();
        var representatives=new[] { 200,250,300,50,71,1702 }.Where(value => presentation.statePaths.Any(s => s.motion == value)).ToArray();
        foreach (int fps in new[] { 30,60 }) foreach (int value in representatives)
        {
            player.bones.ResetDynamics(); player.deskObjects.Select(0); player.character.Rebind();
            string name=data.motions.First(m => m.value == value).name;
            presentation.Motion(new ChillSentence { BodyMotion=value,FacialMotion=0,LookScale=0 },name,true);
            player.character.Update(0);
            for (int frame=0;frame<fps*2;frame++)
            {
                player.bones.BeforeAnimator(); player.character.Update(1f/fps); player.deskObjects.AdvanceProps(1f/fps);
                var positions=transforms.Select(t => t.localPosition).ToArray();
                var rotations=transforms.Select(t => t.localRotation).ToArray();
                var humanRotations=humans.Select(t => t.localRotation).ToArray();
                player.bones.Step(1f/fps);
                for (int i=0;i<transforms.Length;i++)
                {
                    var t=transforms[i]; float angle=Quaternion.Angle(rotations[i],t.localRotation);
                    report.maxSecondaryRotation=Mathf.Max(report.maxSecondaryRotation,angle);
                    report.maxPositionError=Mathf.Max(report.maxPositionError,Vector3.Distance(positions[i],t.localPosition));
                    Require(Finite(t.position) && Finite(t.localRotation),"Non-finite pose "+name+" / "+t.name);
                }
                for (int i=0;i<humans.Length;i++) report.maxHumanoidRotationError=Mathf.Max(report.maxHumanoidRotationError,Quaternion.Angle(humanRotations[i],humans[i].localRotation));
                report.simulationFrames++;
            }
            if (fps == 60 && (value == 300 || value == 50 || value == 1702))
            {
                string filename="preview-motion-"+value+".png";
                Render(player.view,Path.Combine(Root,filename)); report.previews.Add(filename);
            }
        }
        Require(report.maxPositionError < .00001f,"Physics changed authored local positions");
        Require(report.maxHumanoidRotationError < .1f,"Physics changed humanoid joint rotations");
        Require(report.maxSecondaryRotation <= 90.1f,"Secondary motion exceeded WebGL safety limits");
        player.bones.ResetDynamics(); player.deskObjects.Select(0);
        for (int i=0;i<propTransforms.Length;i++) Require(Vector3.Distance(propTransforms[i].localPosition,propPositions[i]) < .00001f && Quaternion.Angle(propTransforms[i].localRotation,propRotations[i]) < .1f,"Prop did not return to desk pose: "+propTransforms[i].name);
        for (int i=0;i<skins.Length;i++) Require(skins[i].sharedMaterials.SequenceEqual(materials[i]),"Chair animation changed skin material");
        presentation.ResetPresentation(); player.character.runtimeAnimatorController=original;
        report.errors=report.errors.Distinct().ToList(); report.passed=report.errors.Count == 0;
        File.WriteAllText(Path.Combine(Root,"research/motion-presentation-audit.json"),JsonUtility.ToJson(report,true));
        Debug.Log("HLC_CHILL_MOTION_AUDIT "+JsonUtility.ToJson(report));
        if (!report.passed) throw new InvalidOperationException("Motion audit failed: "+string.Join("; ",report.errors));
    }
    static bool Finite(Vector3 v) => !(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z));
    static bool Finite(Quaternion v) => Finite(new Vector3(v.x,v.y,v.z)) && !float.IsNaN(v.w) && !float.IsInfinity(v.w);
    static void Render(Camera camera,string path)
    {
        var rt=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var previous=RenderTexture.active; bool previousAsync=ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation=false; camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) { var pixels=image.GetPixels(); for (int i=0;i<pixels.Length;i++) pixels[i]=pixels[i].gamma; image.SetPixels(pixels); image.Apply(); }
            File.WriteAllBytes(path,image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
        }
        finally { camera.targetTexture=null; RenderTexture.active=previous; ShaderUtil.allowAsyncCompilation=previousAsync; RenderTexture.ReleaseTemporary(rt); }
    }
}
