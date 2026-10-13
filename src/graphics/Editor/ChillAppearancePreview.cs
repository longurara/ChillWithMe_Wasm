using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Hlc.ChillPrototype;

// Samples native work/rest poses at the same camera. No scene/save writes.
public static class ChillAppearancePreview
{
    public static void Capture()
    {
        EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");
        var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();
        ChillAppearanceBuild.Configure(player);
        var ui=ChillNativeUIBuild.Configure(player);ui.Initialize();
        ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-appearance-work.png",1,0,.4f);
        Debug.Log("HLC_APPEARANCE_HAIR afterWork="+player.story.effects.hair.GetBlendShapeWeight(1));
        var workHead=player.view.WorldToViewportPoint(player.story.presentation.head.position);
        player.deskObjects.CaptureDefaults();player.deskObjects.Select(0);
        player.character.runtimeAnimatorController=player.story.storyController;
        player.character.Rebind();player.character.fireEvents=false;
        const string state="HeroineBaseAnimation.BreakBase_002_ReadBook.BreakBase002";
        if(!player.character.HasState(0,Animator.StringToHash(state)))throw new InvalidOperationException("Native rest reading state missing");
        player.character.SetInteger("MotionType",650);
        player.character.Play(state,0,.4f);player.character.Update(0);
        // Let secondary layers settle without advancing the native base pose.
        for(int i=0;i<5;i++)player.character.Update(1f/60);
        player.deskObjects.Action("ActivateBook");
        var chair=player.deskObjects.chairAnimator;
        chair.enabled=true;chair.SetInteger("MotionType",650);chair.Play("StartBreakReadBook",0,1);chair.Update(0);
        ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-appearance-rest.png",1,0,0,true);
        Debug.Log("HLC_APPEARANCE_HAIR afterRest="+player.story.effects.hair.GetBlendShapeWeight(1));
        player.story.effects.hair.SetBlendShapeWeight(1,0);
        ChillNativeUIPreview.RenderCurrent(player,ui.canvas,"preview-appearance-rest-normal.png",1,0,0,true);
        var restHead=player.view.WorldToViewportPoint(player.story.presentation.head.position);
        Debug.Log("HLC_APPEARANCE_POSES workHead="+workHead+" restHead="+restHead+" characterScale="+player.character.transform.lossyScale+" cameraFov="+player.view.fieldOfView);
        File.WriteAllText(Path.GetFullPath("../research/appearance-pose-check.json"),JsonUtility.ToJson(new Report{workHead=workHead,restHead=restHead,scale=player.character.transform.lossyScale,fov=player.view.fieldOfView,sameCamera=true,userSaveWritten=false},true));
    }
    [Serializable]class Report{public Vector3 workHead,restHead,scale;public float fov;public bool sameCamera,userSaveWritten;}
}
