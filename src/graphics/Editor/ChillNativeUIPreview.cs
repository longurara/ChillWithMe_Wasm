using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering.Universal;
using Hlc.ChillPrototype;

public static class ChillNativeUIPreview
{
    public static void RenderCurrent(ChillGraphicsPlayer player,Canvas canvas,string filename,int timeMode=0,int deskMode=0,float animationTime=0,bool keepCharacterPose=false,Action<Camera> afterRender=null)
    {
        player.ApplyDayLighting();player.environment.SelectTime(timeMode);
        if(!keepCharacterPose){player.character.Rebind();player.character.Play(player.states[deskMode],0,animationTime);player.character.Update(0);}
        player.view.cullingMask &= ~(1<<5);
        var uiObject=new GameObject("Native HUD preview camera");var uiCamera=uiObject.AddComponent<Camera>();
        uiCamera.transform.SetPositionAndRotation(player.view.transform.position,player.view.transform.rotation);
        uiCamera.fieldOfView=player.view.fieldOfView;uiCamera.nearClipPlane=.01f;uiCamera.farClipPlane=10;uiCamera.cullingMask=1<<5;uiCamera.clearFlags=CameraClearFlags.Depth;
        uiCamera.GetUniversalAdditionalCameraData().renderType=CameraRenderType.Base;uiCamera.clearFlags=CameraClearFlags.SolidColor;uiCamera.backgroundColor=Color.clear;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=uiCamera;canvas.planeDistance=1;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;scaler.scaleFactor=1280f/1920;
        canvas.scaleFactor=scaler.scaleFactor;
        var rt=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);var previous=RenderTexture.active;
        var uiTarget=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var composite=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Prototype/Editor/NativeUIPreviewComposite.shader"));
        try{
            player.view.targetTexture=rt;uiCamera.targetTexture=uiTarget;Canvas.ForceUpdateCanvases();ShaderUtil.allowAsyncCompilation=false;player.view.Render();uiCamera.Render();Graphics.Blit(uiTarget,rt,composite);RenderTexture.active=rt;
            afterRender?.Invoke(uiCamera);
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            if(QualitySettings.activeColorSpace==ColorSpace.Linear){var pixels=image.GetPixels();for(int i=0;i<pixels.Length;i++)pixels[i]=pixels[i].gamma;image.SetPixels(pixels);image.Apply();}
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,"../../",filename)),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }finally{player.view.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);RenderTexture.ReleaseTemporary(uiTarget);UnityEngine.Object.DestroyImmediate(composite);UnityEngine.Object.DestroyImmediate(uiObject);canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;}
    }
    static string PathOf(Transform t) => t.parent ? PathOf(t.parent)+"/"+t.name : t.name;
    public static void Capture()
    {
        EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");
        var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();
        player.ApplyDayLighting(); player.environment.SelectTime(0);
        player.character.Rebind();player.character.Play(player.states[0],0,0);player.character.Update(0);
        var all=UnityEngine.Object.FindObjectsOfType<Transform>(true);
        var canvas=all.First(t => PathOf(t)=="Paremt/PCPlatform/Canvas").GetComponent<Canvas>();
        for (var t=canvas.transform;t;t=t.parent) t.gameObject.SetActive(true);
        canvas.enabled=true; canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=player.view;canvas.planeDistance=1;
        player.view.cullingMask |= 1<<5;
        foreach(var group in canvas.GetComponentsInParent<CanvasGroup>()) group.alpha=1;
        Debug.Log("HLC_NATIVE_UI_CANVAS size="+((RectTransform)canvas.transform).rect+" scale="+canvas.scaleFactor+" mask="+player.view.cullingMask);
        foreach (var animator in canvas.GetComponentsInChildren<Animator>(true)) animator.enabled=false;
        Debug.Log("HLC_NATIVE_UI_COUNTS texts="+canvas.GetComponentsInChildren<TMP_Text>(true).Length+" buttons="+canvas.GetComponentsInChildren<Button>(true).Length+" images="+canvas.GetComponentsInChildren<Image>(true).Length);
        foreach (var t in canvas.GetComponentsInChildren<RectTransform>(true).Where(t => t.parent == canvas.transform || t.parent.name == "UI" || t.parent.name == "MostFrontArea"))
            Debug.Log("HLC_NATIVE_UI_ROOT "+PathOf(t)+" active="+t.gameObject.activeSelf+" pos="+t.anchoredPosition+" alpha="+(t.GetComponent<CanvasGroup>() ? t.GetComponent<CanvasGroup>().alpha.ToString() : "none"));
        var rt=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        player.view.targetTexture=rt;Canvas.ForceUpdateCanvases();
        var previous=RenderTexture.active;
        try {
            ShaderUtil.allowAsyncCompilation=false; player.view.Render();RenderTexture.active=rt;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) { var pixels=image.GetPixels();for(int i=0;i<pixels.Length;i++)pixels[i]=pixels[i].gamma;image.SetPixels(pixels);image.Apply(); }
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,"../../preview-native-ui.png")),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
        finally {player.view.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
    }
}
