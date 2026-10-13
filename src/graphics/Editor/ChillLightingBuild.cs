using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using System;
using System.IO;
using System.Linq;
using Hlc.ChillPrototype;

public static class ChillLightingBuild
{
    static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
    public static void CheckDeskLamp(ChillGraphicsPlayer player,Canvas canvas)
    {
        player.environment.SelectTime(2);
        var lamps=UnityEngine.Object.FindObjectsOfType<Light>().Where(l=>PathOf(l.transform).Contains("/Platform_PC/m_standlight_mobile") && l.name=="Point Light").OrderBy(l=>l.intensity).ToArray();
        if(lamps.Length!=2 || lamps.Any(l=>l.type!=LightType.Point || Mathf.Abs(l.range-1)>.0001f) || Mathf.Abs(lamps[0].intensity-.13f)>.0001f || Mathf.Abs(lamps[1].intensity-.24f)>.0001f)
            throw new InvalidOperationException("Desk task lights differ from original PC lights");
        var color=new Color(.8627451f,.7581753f,.3490196f,1);var lamp=lamps[1];
        if((new Vector4(lamp.color.r,lamp.color.g,lamp.color.b,lamp.color.a)-new Vector4(color.r,color.g,color.b,color.a)).sqrMagnitude>.000001f)throw new InvalidOperationException("Desk lamp color differs from original");
        ChillNativeUIPreview.RenderCurrent(player,canvas,"preview-native-desk-lamp-on.png",2,0,.4f);
        try{foreach(var light in lamps)light.enabled=false;ChillNativeUIPreview.RenderCurrent(player,canvas,"preview-native-desk-lamp-off.png",2,0,.4f);}
        finally{foreach(var light in lamps)light.enabled=true;}
        File.WriteAllText(Path.GetFullPath("../research/desk-lamp-render-check.json"),JsonUtility.ToJson(new LampReport{path=PathOf(lamp.transform),intensity=lamp.intensity,range=lamp.range,color=lamp.color,perPixelAdditionalLights=true,originalLightParameters=true},true));
        Debug.Log("HLC_NATIVE_DESK_LAMP_CHECK parameters=true onOffCaptures=true");
    }
    public static void CaptureDeskLamp()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();ChillAppearanceBuild.Configure(player);var ui=ChillNativeUIBuild.Configure(player);ui.Initialize();CheckDeskLamp(player,ui.canvas);
    }
    [Serializable]class LampReport{public string path;public float intensity,range;public Color color;public bool perPixelAdditionalLights,originalLightParameters;}
    public static void Configure()
    {
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Prototype/ChillURP.asset");
        var settings=new SerializedObject(pipeline);
        settings.FindProperty("m_AdditionalLightsRenderingMode").intValue=(int)LightRenderingMode.PerPixel;
        settings.FindProperty("m_AdditionalLightsPerObjectLimit").intValue=4;
        settings.FindProperty("m_AdditionalLightShadowsSupported").boolValue=false;
        settings.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);
        Debug.Log("HLC_NATIVE_ADDITIONAL_LIGHTS perPixel=true perObject=4 additionalShadows=false originalLightTransforms=true");
    }
}
