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
        settings.FindProperty("m_MainLightShadowmapResolution").intValue=4096;
        settings.FindProperty("m_ShadowDistance").floatValue=10f;
        settings.FindProperty("m_ShadowCascadeCount").intValue=4;
        settings.FindProperty("m_Cascade2Split").floatValue=0.044999998f;
        settings.FindProperty("m_Cascade3Split").vector2Value=new Vector2(0.033122387f,0.10750001f);
        settings.FindProperty("m_Cascade4Split").vector3Value=new Vector3(0.067f,0.2f,0.467f);
        settings.FindProperty("m_CascadeBorder").floatValue=0.29649597f;
        settings.FindProperty("m_ShadowDepthBias").floatValue=1f;
        settings.FindProperty("m_ShadowNormalBias").floatValue=1f;
        settings.FindProperty("m_SoftShadowsSupported").boolValue=true;
        settings.FindProperty("m_ConservativeEnclosingSphere").boolValue=false;
        settings.FindProperty("m_SoftShadowQuality").intValue=(int)SoftShadowQuality.High;
        settings.FindProperty("m_AdditionalLightsRenderingMode").intValue=(int)LightRenderingMode.PerPixel;
        settings.FindProperty("m_AdditionalLightsPerObjectLimit").intValue=4;
        settings.FindProperty("m_AdditionalLightShadowsSupported").boolValue=false;
        var mainPrefilter=settings.FindProperty("m_PrefilteringModeMainLightShadows");
        if(mainPrefilter!=null)mainPrefilter.intValue=4;
        var softPrefilter=settings.FindProperty("m_PrefilterSoftShadows");
        if(softPrefilter!=null)softPrefilter.boolValue=false;
        settings.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);
        if(File.Exists("Assets/Prototype/Room.unity"))
        {
            var active=UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            bool alreadyOpen=active.path=="Assets/Prototype/Room.unity";
            var scene=alreadyOpen?active:UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");
            ConfigureSceneLights();
            if(!alreadyOpen)UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("HLC_NATIVE_ADDITIONAL_LIGHTS perPixel=true perObject=4 additionalShadows=false mainShadow=4096 cascades=4 softShadows=High");
    }
    public static void ConfigureSceneLights()
    {
        foreach(var texPath in new[]{"Assets/Recovered/Texture2D/t_Tee_shadowmask.png","Assets/Recovered/Texture2D/t_Polo_shadowmask.png","Assets/Recovered/Texture2D/t_Polo_shadowmask_A.png","Assets/Recovered/Texture2D/t_Jeans_shadowmask.png"})
        {
            var imp=AssetImporter.GetAtPath(texPath) as TextureImporter;
            if(imp&&imp.sRGBTexture){imp.sRGBTexture=false;imp.SaveAndReimport();}
        }
        foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            string path=PathOf(light.transform);
            if(path.Contains("BaseCozy Weather Sphere/"))
            {
                light.cullingMask=0;light.intensity=0f;light.enabled=false;light.gameObject.SetActive(false);
                EditorUtility.SetDirty(light);EditorUtility.SetDirty(light.gameObject);
                continue;
            }
            if(light.type==LightType.Directional&&path.Contains("/DateTime/")&&!path.Contains("Sun Offset"))
                light.shadows=LightShadows.None;
            var data=light.GetComponent<UniversalAdditionalLightData>()??light.gameObject.AddComponent<UniversalAdditionalLightData>();
            data.usePipelineSettings=true;
            data.softShadowQuality=SoftShadowQuality.High;
            EditorUtility.SetDirty(light);EditorUtility.SetDirty(data);
        }
        if(LightmapSettings.lightProbes!=null)
        {
            var probes=LightmapSettings.lightProbes.bakedProbes;
            if(probes!=null&&probes.Length>0&&probes[0][0,0]<0.35f)
            {
                for(int i=0;i<probes.Length;i++)
                {
                    for(int c=0;c<3;c++)
                        for(int k=1;k<9;k++)
                            probes[i][c,k]*=0.25f;
                    probes[i].AddAmbientLight(new Color(0.22f,0.22f,0.24f));
                }
                LightmapSettings.lightProbes.bakedProbes=probes;
                EditorUtility.SetDirty(LightmapSettings.lightProbes);
            }
        }
        foreach(var matName in new[]{"mt_Chair.mat","mt_Chair_02.mat","mt_Chair_03.mat"})
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/Recovered/Material/"+matName);
            if(!m)continue;
            m.SetFloat("_RimBorder",0.48f);
            m.SetFloat("_RimBlur",0.20f);
            EditorUtility.SetDirty(m);
        }
        var targetL=new Vector3(-0.42f,0.62f,0.66f).normalized;
        var mainDir=new Vector3(0.56f,0.82f,-0.16f)*0.44f;
        var ov=targetL-mainDir;
        foreach(var matName in new[]{"mt_Tee_A.mat","mt_Tee_B.mat","mt_Outfit.mat","mt_Body.mat","mt_Face.mat","mt_Polo_A.mat","mt_Polo_B.mat","mt_Jeans.mat"})
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/Recovered/Material/"+matName);
            if(!m)continue;
            m.SetVector("_LightDirectionOverride",new Vector4(ov.x,ov.y,ov.z,0f));
            m.SetFloat("_ShadowBorder",0.38f);
            m.SetFloat("_ShadowBlur",0.18f);
            m.SetFloat("_ShadowBorderRange",0.02f);
            EditorUtility.SetDirty(m);
        }
    }
    static void SaveRender(Camera view,string filename)
    {
        var rt=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var previousRT=RenderTexture.active;
        bool previousAsync=ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation=false;
            view.targetTexture=rt;
            view.Render();
            RenderTexture.active=rt;
            var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1280,720),0,0);
            texture.Apply();
            if(QualitySettings.activeColorSpace==ColorSpace.Linear)
            {
                var pixels=texture.GetPixels();
                for(int i=0;i<pixels.Length;i++)pixels[i]=pixels[i].gamma;
                texture.SetPixels(pixels);
                texture.Apply();
            }
            string image=Path.GetFullPath(Path.Combine(Application.dataPath,"../../",filename));
            File.WriteAllBytes(image,texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            Debug.Log("HLC_TALK_LIGHTING_PREVIEW "+image);
        }
        finally
        {
            view.targetTexture=null;
            RenderTexture.active=previousRT;
            ShaderUtil.allowAsyncCompilation=previousAsync;
            RenderTexture.ReleaseTemporary(rt);
        }
    }
    public static void CaptureTalkLightingPreview()
    {
        var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");
        ConfigureSceneLights();
        var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();
        player.ApplyDayLighting();
        player.environment.SelectTime(0);
        player.deskObjects.Select(0);
        var animator=player.character;
        var physics=player.bones;
        animator.runtimeAnimatorController=player.story.storyController;
        animator.Rebind();
        var talkMotion=player.story.presentation.statePaths.FirstOrDefault(p=>p.motion==300);
        string talkState=talkMotion!=null?talkMotion.path:"Anim_Break_Talk_Loop_01";
        physics.bakedConfiguration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/bone-bakes.bytes");
        physics.useBakedFallback=true;
        physics.RebindGarment(null);
        physics.ResetDynamics();
        for(int frame=0;frame<30;frame++)
        {
            physics.BeforeAnimator();
            animator.Play(talkState,0,0f);
            animator.Update(0f);
            physics.Step(1f/60f);
        }
        SaveRender(player.view,"preview-lighting-physics-talk.png");
    }
    public static void RebuildLightingPhysicsAndVerify()
    {
        Configure();
        ChillPhysicsCheck.Check();
        ChillWindowCoatCheck.Check();
        var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();
        ChillCoatGravityCheck.Run(player);
        ChillCozyLightingCheck.Run(player);
        CaptureDeskLamp();
        CaptureTalkLightingPreview();
        var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");
        ConfigureSceneLights();
        player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();
        if(player&&player.environment)player.environment.SelectTime(0);
        if(player&&player.bones)
        {
            player.bones.bakedConfiguration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/bone-bakes.bytes");
            player.bones.useBakedFallback=true;
            EditorUtility.SetDirty(player.bones);
        }
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        ChillBuild.CapturePreview();
        Debug.Log("HLC_LIGHTING_AND_PHYSICS_VERIFIED");
    }
    public static void RebuildLightingPhysicsAndBuildWebGL()
    {
        RebuildLightingPhysicsAndVerify();
        ChillBuild.BuildWebGL();
        Debug.Log("HLC_LIGHTING_PHYSICS_AND_WEBGL_DONE");
    }
}
