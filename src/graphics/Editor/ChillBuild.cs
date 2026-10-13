using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Hlc.ChillPrototype;

public static class ChillBuild
{
    const string Recovered = "Assets/Recovered/";
    const string SourceScene = Recovered + "App/Scenes/RoomScene.unity";
    const string ScenePath = "Assets/Prototype/Room.unity";
    [Serializable] class Binding { public string path; public int index; public float[] offset; }
    [Serializable] class Lighting { public string[] textures; public Binding[] renderers; }
    [Serializable] class Weather { public string[] materials; }
    [Serializable] class DeskBindings { public string handPen, handPenForSpin, roomPen, handNote, handCop, roomCop, coffeeMakerCop, workBookAnimator, deskObjectAnimator, chairAnimator, bearsRestaurantBookAnimator; }

    static T[] All<T>() where T : Component => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    static string Hierarchy(Transform t) => t.parent ? Hierarchy(t.parent) + "/" + t.name : t.name;
    static void ActivateParents(Transform t) { while (t) { t.gameObject.SetActive(true); t = t.parent; } }
    static void SetInt(SerializedObject so, string name, int value) { var p = so.FindProperty(name); if (p != null) p.intValue = value; }
    static void SetBool(SerializedObject so, string name, bool value) { var p = so.FindProperty(name); if (p != null) p.boolValue = value; }
    static bool toonPrepared;
    public static void PrepareToonForWeb()
    {
        if (toonPrepared) return;
        var type = typeof(lilToonSetting);
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        object[] settings = { null };
        type.GetMethod("InitializeShaderSetting", flags).Invoke(null, settings);
        var toonSettings = (lilToonSetting)settings[0];
        ChillToonWeb.Configure(ref toonSettings);
        type.GetMethods(flags).Single(m => m.Name == "ApplyShaderSetting" && m.GetParameters().Length == 2).Invoke(null, new object[] { toonSettings, "HLC_CHILL_TOON_WEB_SM35" });
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (File.ReadAllText("Assets/lilToon/Shader/ltspass_opaque.shader").Contains("#pragma target 4.5"))
            throw new InvalidOperationException("Run prepare_toon_web.py before building: SM4.5 UsePass is unsupported by WebGL.");
        toonPrepared = true;
        ChillToonWeb.GenerateProfiles();
        ChillToonWeb.CheckTextureBudget();
    }

    [MenuItem("HLC/Chill/Prepare graphics prototype")]
    public static void Prepare()
    {
        PrepareToonForWeb();
        Directory.CreateDirectory("Assets/Prototype");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var scene = EditorSceneManager.OpenScene(SourceScene);
        foreach (var go in All<Transform>()) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go.gameObject);
        foreach (var canvas in All<Canvas>()) canvas.gameObject.SetActive(false);
        foreach (var listener in All<AudioListener>()) listener.enabled = false;
        foreach (var source in All<AudioSource>()) { source.playOnAwake = false; source.enabled = false; }
        foreach (var camera in All<Camera>()) camera.enabled = false;
        var view = All<Camera>().FirstOrDefault(c => c.name == "MainCamera");
        if (!view) throw new InvalidOperationException("Recovered MainCamera is missing.");
        ActivateParents(view.transform); view.enabled = true;
        view.targetTexture = null; view.rect = new Rect(0, 0, 1, 1);
        var cameraData = view.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = false; cameraData.renderType = CameraRenderType.Base;
        var listenerOnView = view.GetComponent<AudioListener>() ?? view.gameObject.AddComponent<AudioListener>();
        listenerOnView.enabled = true;
        var oldCameraAnimation = view.GetComponentInParent<Animator>();
        if (oldCameraAnimation) oldCameraAnimation.enabled = false;

        var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        rendererData.name = "ChillRenderer";
        var rendererSO = new SerializedObject(rendererData);
        SetInt(rendererSO, "m_RenderingMode", 0); rendererSO.ApplyModifiedPropertiesWithoutUndo();
        SetInt(rendererSO, "m_IntermediateTextureMode", 1); rendererSO.ApplyModifiedPropertiesWithoutUndo();
        SaveNew(rendererData, "Assets/Prototype/ChillRenderer.asset");
        var screenMaterial = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Prototype/WebShaders/StoryScreen.shader"));
        SaveNew(screenMaterial, "Assets/Prototype/StoryScreen.mat");
        var feature = ScriptableObject.CreateInstance<ChillStoryRenderFeature>(); feature.name = "Chill story effects"; feature.effectMaterial = screenMaterial; feature.Create();
        rendererData.rendererFeatures.Add(feature); AssetDatabase.AddObjectToAsset(feature, rendererData); EditorUtility.SetDirty(rendererData);
        var pipeline = UniversalRenderPipelineAsset.Create(rendererData);
        pipeline.name = "ChillURP"; pipeline.msaaSampleCount = 2; pipeline.renderScale = 1;
        pipeline.supportsHDR = false; pipeline.supportsCameraDepthTexture = false; pipeline.supportsCameraOpaqueTexture = false;
        pipeline.shadowDistance = 15;
        var pipelineSO = new SerializedObject(pipeline);
        SetInt(pipelineSO, "m_MainLightShadowmapResolution", 1024);
        SetInt(pipelineSO, "m_AdditionalLightsRenderingMode", (int)LightRenderingMode.PerPixel);
        SetBool(pipelineSO, "m_AdditionalLightShadowsSupported", false);
        pipelineSO.ApplyModifiedPropertiesWithoutUndo();
        SaveNew(pipeline, "Assets/Prototype/ChillURP.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        // Recovered defaults reference quality indices/pipelines that are absent
        // from this project. Every target must use the same standalone renderer.
        var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var levels = quality.FindProperty("m_QualitySettings");
        for (int i = 0; i < levels.arraySize; i++)
            levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
        var defaults = quality.FindProperty("m_PerPlatformDefaultQuality");
        for (int i = 0; i < defaults.arraySize; i++)
            defaults.GetArrayElementAtIndex(i).FindPropertyRelative("second").intValue = levels.arraySize - 1;
        quality.ApplyModifiedPropertiesWithoutUndo();
        QualitySettings.SetQualityLevel(levels.arraySize - 1, true);
        QualitySettings.renderPipeline = pipeline;
        Debug.Log("HLC_CHILL_RENDER_PROFILE quality=" + QualitySettings.GetQualityLevel() + " pipeline=" + pipeline.name + " colorSpace=Linear");
        SimplifyWeather();

        RestoreStaticBatches();
        var actor = All<Animator>().SingleOrDefault(a => a.name == "Character");
        if (!actor || !actor.avatar || !actor.avatar.isValid) throw new InvalidOperationException("Recovered Satone avatar is not valid.");
        ActivateParents(actor.transform); actor.enabled = true; actor.applyRootMotion = false;
        actor.fireEvents = false;
        actor.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        // Keep the recovered desk placement. HeroineTransform0 belongs to a
        // different viewpoint and would move Satone out of this camera frame.

        var names = new[] { "anim_work_book_base_loop", "anim_work_report_base_loop", "anim_work_pc_base_loop" };
        const string controllerPath = "Assets/Prototype/ChillDesk.controller";
        if (AssetDatabase.LoadMainAssetAtPath(controllerPath)) AssetDatabase.DeleteAsset(controllerPath);
        AssetDatabase.CopyAsset(Recovered + "AnimatorController/Character_Animator_ForScript.controller", controllerPath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        var layers = controller.layers;
        var deskMachine = new AnimatorStateMachine { name = "Desk work loops" };
        AssetDatabase.AddObjectToAsset(deskMachine,controller);
        layers[0].stateMachine = deskMachine; layers[0].iKPass = true; layers[0].defaultWeight = 1; controller.layers = layers;
        var states = new List<string>();
        foreach (string name in names)
        {
            var guid = AssetDatabase.FindAssets(name + " t:AnimationClip", new[] { Recovered + "AnimationClip" })
                .FirstOrDefault(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == name);
            var clip = guid == null ? null : AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (!clip) throw new InvalidOperationException("Recovered animation missing: " + name);
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            var state = controller.layers[0].stateMachine.AddState(name); state.motion = clip;
            if (states.Count == 0) controller.layers[0].stateMachine.defaultState = state;
            states.Add(name);
        }
        actor.runtimeAnimatorController = controller;
        actor.Rebind(); actor.Update(0);
        foreach (var transform in All<Transform>())
        {
            // These copies are held/poured by gameplay actions outside the
            // three desk loops. Keep the stationary desk cup for this stage.
            bool actionCup = transform.name == "m_cup" &&
                (transform.IsChildOf(actor.transform) || Hierarchy(transform).Contains("/m_coffeemaker/"));
            if (actionCup || (transform.name == "m_cupcake_01" && transform.IsChildOf(actor.transform)))
                transform.gameObject.SetActive(false);
        }

        var player = new GameObject("ChillPrototype").AddComponent<ChillGraphicsPlayer>();
        player.character = actor; player.states = states.ToArray(); player.view = view;
        player.music = player.gameObject.AddComponent<AudioSource>();
        player.music.playOnAwake = false; player.music.loop = true; player.music.spatialBlend = 0;
        player.music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Recovered + "AudioClip/aqua room.ogg");
        player.bones = player.gameObject.AddComponent<ChillBonePhysics>();
        player.bones.character = actor;
        player.bones.configuration = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/bone-physics.json");
        player.bones.bakedConfiguration = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/bone-bakes.bytes")??AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/bone-bakes.json");
        player.bones.enabled = true;
        player.library = player.gameObject.AddComponent<ChillLibraryBrowser>();
        player.library.character = actor; player.library.music = player.music; player.library.view = view;
        player.library.physics = player.bones;
        player.uiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Prototype/Fonts/NotoSans-Regular.ttf");
        player.library.uiFont = player.uiFont;
        var previewMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        previewMaterial.SetColor("_BaseColor", new Color(.72f, .74f, .79f));
        SaveNew(previewMaterial, "Assets/Prototype/PreviewMaterial.mat");
        player.library.previewMaterial = previewMaterial;
        RestoreLighting(player);
        ConfigureEnvironments(player);
        ConfigureDesk(player);
        player.story = player.gameObject.AddComponent<ChillStoryRuntime>();
        player.story.player = player;
        player.story.storyController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Recovered + "AnimatorController/Character_Animator_ForScript.controller");
        player.story.japaneseFont = AssetDatabase.LoadAssetAtPath<Font>(Recovered + "Font/IBMPlexSansJP-Medium.otf");
        ConfigureStoryScene(player);
        ChillMotionBuild.Configure(player);
        ChillAppearanceBuild.Configure(player);
        ChillNativeUIBuild.Configure(player);
        PlayerSettings.companyName = "HLC"; PlayerSettings.productName = "Chill Graphics Prototype";
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.WebGL, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.Low);
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
        PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.External;
        PlayerSettings.WebGL.template = "APPLICATION:Default";
        PlayerSettings.bundleVersion = "1.24.0";
        PlayerSettings.WebGL.dataCaching = false;
        PlayerSettings.WebGL.memorySize = 512;
        PlayerSettings.WebGL.initialMemorySize = 256;
        PlayerSettings.WebGL.maximumMemorySize = 2048;
        PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
        PlayerSettings.WebGL.geometricMemoryGrowthStep = 0.2f;
        PlayerSettings.WebGL.memoryGeometricGrowthCap = 96;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("HLC_CHILL_PREPARED " + ScenePath);
    }

    static void RestoreLighting(ChillGraphicsPlayer player)
    {
        var data = JsonUtility.FromJson<Lighting>(File.ReadAllText("Assets/Prototype/day-lighting.json"));
        player.dayLightmaps = data.textures.Select(p => AssetDatabase.LoadAssetAtPath<Texture2D>(p)).ToArray();
        var renderers = All<Renderer>().GroupBy(r => Hierarchy(r.transform)).ToDictionary(g => g.Key, g => g.First());
        var resolved = data.renderers.Where(b => renderers.ContainsKey(b.path)).ToArray();
        player.lightmappedRenderers = resolved.Select(b => renderers[b.path]).ToArray();
        player.lightmapIndices = resolved.Select(b => b.index).ToArray();
        player.lightmapOffsets = resolved.Select(b => new Vector4(b.offset[0], b.offset[1], b.offset[2], b.offset[3])).ToArray();
        Debug.Log("HLC_CHILL_LIGHTMAP_BINDINGS " + resolved.Length);
    }

    static void ConfigureEnvironments(ChillGraphicsPlayer player)
    {
        var environment = player.gameObject.AddComponent<ChillEnvironment>();
        environment.player = player; player.environment = environment;
        environment.dateTime = All<Transform>().FirstOrDefault(t => t.name == "DateTime");
        environment.window = All<Transform>().FirstOrDefault(t => t.name == "WindowView");
        environment.environments = environment.window ? environment.window.Cast<Transform>().Select(t => t.gameObject).ToArray() : Array.Empty<GameObject>();
        var renderers = All<Renderer>().GroupBy(r => Hierarchy(r.transform)).ToDictionary(g => g.Key, g => g.First());
        var names = new[] { "day", "sunset", "night", "cloudy" };
        var cozy=JsonUtility.FromJson<ChillCozyLighting.Configuration>(File.ReadAllText("Assets/Prototype/cozy-lighting.json"));
        var ambient = new[] { new Color(.38f,.41f,.46f), new Color(.4f,.27f,.25f), new Color(.16f,.18f,.3f), new Color(.32f,.35f,.39f) };
        var sky = new[] { new Color(.43f,.62f,.8f), new Color(.9f,.43f,.35f), new Color(.08f,.1f,.23f), new Color(.44f,.48f,.55f) };
        environment.presets = names.Select((name,index) => {
            var lighting = JsonUtility.FromJson<Lighting>(File.ReadAllText("Assets/Prototype/" + name + "-lighting.json"));
            var rows = lighting.renderers.Where(b => renderers.ContainsKey(b.path)).ToArray();
            return new ChillEnvironment.Preset { name=name, textures=lighting.textures.Select(p => AssetDatabase.LoadAssetAtPath<Texture2D>(p)).ToArray(),
                renderers=rows.Select(b => renderers[b.path]).ToArray(), indices=rows.Select(b => b.index).ToArray(),
                offsets=rows.Select(b => new Vector4(b.offset[0],b.offset[1],b.offset[2],b.offset[3])).ToArray(), ambient=ambient[index],
                sky=sky[index], sunColor=index==1 ? new Color(1,.65f,.4f) : index==2 ? new Color(.5f,.6f,1) : new Color(1,.95f,.88f),
                sunIntensity=index==2 ? .15f : index==3 ? .4f : .75f,
                cozy=cozy.presets[index],originalSun=All<Light>().FirstOrDefault(l=>Hierarchy(l.transform).Contains("/DateTime/"+cozy.presets[index].name+"/Cozy Weather Sphere/Sky/Sun Offset/")&&l.name=="Sun Light") };
        }).ToArray();
        Debug.Log("HLC_CHILL_ENVIRONMENTS " + environment.environments.Length + " lighting=" + environment.presets.Length);
    }

    static void ConfigureDesk(ChillGraphicsPlayer player)
    {
        var bindings = JsonUtility.FromJson<DeskBindings>(File.ReadAllText("Assets/Prototype/desk-controls.json"));
        var objects = All<Transform>().GroupBy(Hierarchy).ToDictionary(g => g.Key, g => g.First());
        Transform Find(string path) => path != null && objects.TryGetValue(path, out var item) ? item : null;
        Renderer RendererAt(string path) => Find(path)?.GetComponent<Renderer>();
        GameObject ObjectAt(string path) => Find(path)?.gameObject;
        Animator AnimatorAt(string path) => Find(path)?.GetComponent<Animator>();
        var desk = player.gameObject.AddComponent<ChillDeskObjects>(); player.deskObjects = desk;
        desk.handPen = RendererAt(bindings.handPen); desk.handPenForSpin = RendererAt(bindings.handPenForSpin); desk.roomPen = RendererAt(bindings.roomPen);
        desk.handNote = ObjectAt(bindings.handNote); desk.handCop = ObjectAt(bindings.handCop); desk.roomCop = ObjectAt(bindings.roomCop); desk.coffeeMakerCop = ObjectAt(bindings.coffeeMakerCop);
        desk.deskObjectAnimator = AnimatorAt(bindings.deskObjectAnimator); desk.workBookAnimator = AnimatorAt(bindings.workBookAnimator); desk.chairAnimator = AnimatorAt(bindings.chairAnimator);
        desk.bearsRestaurantBookAnimator = AnimatorAt(bindings.bearsRestaurantBookAnimator);
        if (!desk.bearsRestaurantBookAnimator) throw new InvalidOperationException("Held Bears Restaurant story book binding is missing.");
        desk.Select(0);
        Debug.Log("HLC_CHILL_STORY_BOOK hidden=" + !desk.bearsRestaurantBookAnimator.gameObject.activeSelf + " path=" + bindings.bearsRestaurantBookAnimator);
        Debug.Log("HLC_CHILL_DESK_BINDINGS " + (desk.deskObjectAnimator ? desk.deskObjectAnimator.name : "missing"));
    }

    static void ConfigureStoryScene(ChillGraphicsPlayer player)
    {
        var configuration = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Prototype/story-scene.json");
        var data = JsonUtility.FromJson<ChillDecorations.Configuration>(configuration.text);
        var transforms = All<Transform>().GroupBy(Hierarchy).ToDictionary(g => g.Key,g => g.First());
        Transform Find(string path) => path != null && transforms.TryGetValue(path,out var t) ? t : null;
        player.story.decorations = player.gameObject.AddComponent<ChillDecorations>();
        player.story.decorations.configuration = configuration; player.story.decorations.library = player.library;
        var effects = player.gameObject.AddComponent<ChillStoryEffects>(); player.story.effects = effects;
        effects.hat = Find(data.hat)?.gameObject; effects.hair = Find(data.hair)?.GetComponent<SkinnedMeshRenderer>();
        var sharing = Find(data.screenSharing)?.GetComponent<Animator>();
        if (sharing)
        {
            var canvasObject = new GameObject("Chill narrative overlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280,720);
            var clone = UnityEngine.Object.Instantiate(sharing.gameObject,canvasObject.transform,false); clone.name = "ScreenSharing";
            effects.screenSharing = clone.GetComponent<Animator>(); effects.screenSharing.fireEvents = false; clone.SetActive(false);
        }
        var actions = player.character.gameObject.AddComponent<ChillAnimationActions>();
        actions.character = player.character; actions.objects = player.deskObjects; actions.story = player.story;
        player.character.fireEvents = true;
        Debug.Log("HLC_CHILL_STORY_SCENE decorations=" + data.bindings.Length + " screenSharing=" + (effects.screenSharing ? effects.screenSharing.name : "missing"));
    }

    static void SimplifyWeather()
    {
        var data = JsonUtility.FromJson<Weather>(File.ReadAllText("Assets/Prototype/simplified-weather.json"));
        var materials = new HashSet<Material>(data.materials.Select(p => AssetDatabase.LoadAssetAtPath<Material>(p)));
        foreach (var renderer in All<Renderer>())
            if (renderer.sharedMaterials.Any(m => m && materials.Contains(m))) renderer.enabled = false;
        // The recovered scene stores every selectable window environment.
        // Its startup scripts normally select one and configure alpha-mask
        // graphs. Keep the first prototype on a plain day sky instead.
        foreach (var transform in All<Transform>())
        {
            if (transform.name == "WindowView" || transform.name == "WindowTransition")
                transform.gameObject.SetActive(false);
            // Original startup selects exactly one lighting setup and one
            // platform mesh group. Both variants survive a scene-only export.
            if (transform.parent && transform.parent.name == "DateTime")
                transform.gameObject.SetActive(string.Equals(transform.name, "day", StringComparison.OrdinalIgnoreCase));
            if (transform.name == "Platform_mobile") transform.gameObject.SetActive(false);
            if (transform.name == "Platform_PC") transform.gameObject.SetActive(true);
        }
        var skyShader = Shader.Find("Skybox/Procedural");
        if (skyShader)
        {
            var sky = new Material(skyShader) { name = "ChillDaySky" };
            sky.SetColor("_SkyTint", new Color(0.43f, 0.62f, 0.8f));
            sky.SetColor("_GroundColor", new Color(0.34f, 0.32f, 0.3f));
            sky.SetFloat("_Exposure", 1f);
            SaveNew(sky, "Assets/Prototype/DaySky.mat"); RenderSettings.skybox = sky;
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.68f, 0.74f, 0.83f);
        RenderSettings.ambientEquatorColor = new Color(0.5f, 0.53f, 0.59f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.29f, 0.31f);
        var ambient = new SphericalHarmonicsL2();
        ambient.AddAmbientLight(new Color(0.38f, 0.41f, 0.46f));
        RenderSettings.ambientProbe = ambient;
        // Keep authored directional lights and their source hierarchy. COZY's
        // sampled atmosphere sets the active sunlight and ambient gradient.
    }

    static void RestoreStaticBatches()
    {
        Directory.CreateDirectory("Assets/Prototype/StaticMeshes"); int repaired = 0;
        foreach (var renderer in All<MeshRenderer>())
        {
            var filter = renderer.GetComponent<MeshFilter>(); if (!filter || !filter.sharedMesh) continue;
            var so = new SerializedObject(renderer); var info = so.FindProperty("m_StaticBatchInfo");
            int first = info?.FindPropertyRelative("firstSubMesh")?.intValue ?? 0;
            int count = info?.FindPropertyRelative("subMeshCount")?.intValue ?? 0;
            if (count < 1) continue;
            var src = filter.sharedMesh;
            if (first + count > src.subMeshCount) throw new InvalidOperationException("Invalid static batch range on " + renderer.name);
            var root = so.FindProperty("m_StaticBatchRoot")?.objectReferenceValue as Transform;
            var matrix = renderer.transform.worldToLocalMatrix * (root ? root.localToWorldMatrix : Matrix4x4.identity);
            var mesh = CompactSubmeshes(src, first, count, matrix);
            mesh.name = renderer.name + "_unbatched";
            string meshPath = "Assets/Prototype/StaticMeshes/" + repaired + ".asset";
            SaveNew(mesh, meshPath); filter.sharedMesh = mesh;
            // Editing the serialized batch range alone leaves the native
            // renderer's cached batching state alive. A fresh renderer uses
            // only the compact mesh's own index buffer and local bounds.
            var go = renderer.gameObject;
            var materials = renderer.sharedMaterials;
            bool enabled = renderer.enabled, receiveShadows = renderer.receiveShadows;
            var casting = renderer.shadowCastingMode;
            var probes = renderer.lightProbeUsage;
            var reflections = renderer.reflectionProbeUsage;
            var mask = renderer.renderingLayerMask;
            int sortingLayer = renderer.sortingLayerID, sortingOrder = renderer.sortingOrder;
            UnityEngine.Object.DestroyImmediate(renderer);
            var restored = go.AddComponent<MeshRenderer>();
            restored.sharedMaterials = materials; restored.enabled = enabled;
            restored.receiveShadows = receiveShadows; restored.shadowCastingMode = casting;
            restored.lightProbeUsage = probes; restored.reflectionProbeUsage = reflections;
            restored.renderingLayerMask = mask;
            restored.sortingLayerID = sortingLayer; restored.sortingOrder = sortingOrder;
            repaired++;
        }
        Debug.Log("HLC_CHILL_STATIC_MESHES " + repaired);
    }

    static Mesh CompactSubmeshes(Mesh source, int first, int count, Matrix4x4 matrix)
    {
        // A static-batch mesh contains many objects. Copying its whole vertex
        // buffer for every renderer would multiply RAM and GPU memory.
        var remap = new Dictionary<int, int>(); var used = new List<int>();
        var indices = new int[count][];
        for (int n = 0; n < count; n++)
        {
            var group = source.GetIndices(first + n);
            for (int i = 0; i < group.Length; i++)
            {
                int old = group[i];
                if (!remap.TryGetValue(old, out int mapped)) { mapped = used.Count; remap.Add(old, mapped); used.Add(old); }
                group[i] = mapped;
            }
            indices[n] = group;
        }
        var result = new Mesh { indexFormat = used.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        var positions = source.vertices;
        result.SetVertices(used.Select(i => matrix.MultiplyPoint3x4(positions[i])).ToList());
        var normals = source.normals; var normalMatrix = matrix.inverse.transpose;
        if (normals.Length == source.vertexCount) result.SetNormals(used.Select(i => normalMatrix.MultiplyVector(normals[i]).normalized).ToList());
        var tangents = source.tangents;
        if (tangents.Length == source.vertexCount)
            result.SetTangents(used.Select(i => { var t = tangents[i]; var v = matrix.MultiplyVector(new Vector3(t.x,t.y,t.z)).normalized;
                return new Vector4(v.x,v.y,v.z,t.w * Mathf.Sign(matrix.determinant)); }).ToList());
        for (int channel = 0; channel < 8; channel++)
        {
            var uv = new List<Vector4>(); source.GetUVs(channel, uv);
            if (uv.Count == source.vertexCount) result.SetUVs(channel, used.Select(i => uv[i]).ToList());
        }
        var colors = source.colors32;
        if (colors.Length == source.vertexCount) result.SetColors(used.Select(i => colors[i]).ToList());
        result.subMeshCount = count;
        for (int n = 0; n < count; n++) result.SetIndices(indices[n], source.GetTopology(first + n), n, false);
        result.RecalculateBounds();
        return result;
    }

    static void SaveNew(UnityEngine.Object asset, string path)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path)) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(asset, path);
    }

    [MenuItem("HLC/Chill/Build WebGL prototype")]
    public static void BuildWebGL()
    {
        if (!File.Exists(ScenePath)) Prepare();
        PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
        PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.External;
        PlayerSettings.bundleVersion = "1.24.0";
        PlayerSettings.WebGL.template = "APPLICATION:Default";
        PlayerSettings.WebGL.initialMemorySize = 256;
        PlayerSettings.WebGL.maximumMemorySize = 2048;
        PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
        PlayerSettings.WebGL.geometricMemoryGrowthStep = 0.2f;
        PlayerSettings.WebGL.memoryGeometricGrowthCap = 96;
        AssetDatabase.SaveAssets();
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../WebPlayer"));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath }, locationPathName = output, target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("WebGL build failed: " + report.summary.result);
        ChillAssets.CheckShaders();
        Debug.Log("HLC_CHILL_WEBGL_BUILT " + output + " bytes=" + report.summary.totalSize);
    }

    [MenuItem("HLC/Chill/Render preview image")]
    public static void CapturePreview()
    { CapturePose(0, "preview.png"); }

    static void CapturePose(int mode, string filename, int time = 0)
    {
        EditorSceneManager.OpenScene(ScenePath);
        var player = All<ChillGraphicsPlayer>().Single();
        RestoreLighting(player);
        player.environment.SelectTime(time);
        player.deskObjects.Select(mode);
        player.character.Rebind(); player.character.Play(player.states[mode], 0, 0); player.character.Update(0);
        Debug.Log("HLC_ACTOR " + player.character.transform.position + " scale=" + player.character.transform.lossyScale + " camera=" + player.view.transform.position);
        foreach (var skin in player.character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            Debug.Log("HLC_SKIN " + Hierarchy(skin.transform) + " enabled=" + skin.enabled + " active=" + skin.gameObject.activeInHierarchy + " bounds=" + skin.bounds + " materials=" + string.Join(",", skin.sharedMaterials.Select(m => m ? m.name : "null")));
        var rt = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var previousRT = RenderTexture.active; bool previousAsync = ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            player.view.targetTexture = rt; player.view.Render(); RenderTexture.active = rt;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            // The offscreen target stores linear lighting. PNG viewers expect
            // display colors, matching the player's sRGB browser backbuffer.
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                var pixels = texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++) pixels[i] = pixels[i].gamma;
                texture.SetPixels(pixels); texture.Apply();
            }
            string image = Path.GetFullPath(Path.Combine(Application.dataPath, "../../", filename));
            File.WriteAllBytes(image, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            Debug.Log("HLC_CHILL_PREVIEW " + image);
        }
        finally
        {
            player.view.targetTexture = null; RenderTexture.active = previousRT;
            ShaderUtil.allowAsyncCompilation = previousAsync; RenderTexture.ReleaseTemporary(rt);
        }
    }

    public static void PrepareAndPreview()
    {
        Prepare();
        CapturePreview();
        CapturePose(2, "preview-typing.png");
        CapturePose(2, "preview-typing-night.png", 2);
    }

    public static void PreparePreviewAndBuild()
    {
        PrepareAndPreview();
        BuildWebGL();
    }

}
