using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hlc.ChillPrototype
{
    public sealed class ChillGraphicsPlayer : MonoBehaviour
    {
        public Animator character;
        public AudioSource music;
        public string[] states;
        public Renderer[] lightmappedRenderers;
        public int[] lightmapIndices;
        public Vector4[] lightmapOffsets;
        public Texture2D[] dayLightmaps;
        public Camera view;
        public ChillBonePhysics bones;
        public ChillLibraryBrowser library;
        public ChillEnvironment environment;
        public ChillDeskObjects deskObjects;
        public Font uiFont;
        public ChillStoryRuntime story;
        public void StopAnimationAudio(){if(story)story.StopMotionSounds();}
        public int SelectedState => selected;
        public float Volume => volume;
        public bool LowQuality => lowQuality;
        public void SetLowQuality(bool value) { lowQuality=value; SetQuality(); }
        public void SetVolume(float value) { volume = Mathf.Clamp01(value); if (music) music.volume = volume; }
        bool showEnvironments;
        Vector2 environmentScroll;
        public Color ambientLight = new Color(0.38f, 0.41f, 0.46f);
        int selected;
        bool lowQuality;
        float volume = 0.35f;

        void Start()
        {
            lowQuality = ChillPlatform.Mobile;
            SetVolume(volume);
            SetQuality();
            ApplyDayLighting();
            PlayState(0);
        }

        public void ApplyDayLighting()
        {
            // Trilight/Skybox modes regenerate the ambient probe after Start,
            // replacing this preset and making runtime brighter than captures.
            RenderSettings.ambientMode = AmbientMode.Custom;
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(ambientLight);
            RenderSettings.ambientProbe = ambient;
            if (dayLightmaps != null && dayLightmaps.Length > 0)
            {
                var maps = new LightmapData[dayLightmaps.Length];
                for (int i = 0; i < maps.Length; i++) maps[i] = new LightmapData { lightmapColor = dayLightmaps[i] };
                LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
                LightmapSettings.lightmaps = maps;
                for (int i = 0; i < lightmappedRenderers.Length; i++)
                {
                    if (!lightmappedRenderers[i]) continue;
                    lightmappedRenderers[i].lightmapIndex = lightmapIndices[i];
                    lightmappedRenderers[i].lightmapScaleOffset = lightmapOffsets[i];
                }
            }
        }

        void SetQuality()
        {
            Application.targetFrameRate = lowQuality ? 30 : 60;
            QualitySettings.vSyncCount = 0;
            if (bones) bones.lowQuality = lowQuality;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
            {
                pipeline.renderScale = lowQuality ? 0.8f : 1f;
                pipeline.msaaSampleCount = 2;
                pipeline.shadowDistance = lowQuality ? 8f : 10f;
            }
        }

        public void SetStoryDesk(int index)
        {
            if(index<0||index>2)return;
            selected=index;deskObjects?.SetStoryLayout(index);
        }
        public void PlayState(int index)
        {
            if(index<0||index>2)return;
            if(story&&story.TryRequestWorkMode(index))return;
            if (story) story.StopMotionSounds();
            if (library) library.RestoreCharacter();
            selected = index;
            if(story && story.TryPlayRoomMotion(index))
            {
                if(deskObjects)deskObjects.SelectSmooth(index,.25f);
                character.SetBool("Enable_Talk",false);
                return;
            }
            if (bones) bones.ResetDynamics();
            if (deskObjects) deskObjects.Select(index);
            if (character && states != null && states.Length > index)
            {
                character.SetInteger("MotionType",index == 0 ? 250 : index == 1 ? 300 : 200);
                character.SetBool("Enable_Talk",false);
                character.CrossFadeInFixedTime(states[index], 0.25f, 0);
            }
        }

        void OnGUI()
        {
            if (ChillGameplayUI.Active) return;
            if (uiFont) GUI.skin.font = uiFont;
            GUI.skin.label.fontSize = 14; GUI.skin.button.fontSize = 13; GUI.skin.toggle.fontSize = 13;
            float scale = Mathf.Clamp(Screen.width / 1000f, 0.65f, 1.5f);
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUILayout.BeginArea(new Rect(12, 12, 255, Mathf.Min(Screen.height / scale - 24, showEnvironments ? 540 : 330)), GUI.skin.box);
            GUILayout.Label("Chill with You · Đồ họa");
            GUI.enabled = !story || !story.Playing;
            int next = GUILayout.Toolbar(selected, new[] { "Đọc sách", "Viết", "Gõ phím" });
            if (next != selected) PlayState(next);
            GUI.enabled = true;
            GUILayout.Space(8);
            if (music && GUILayout.Button(music.isPlaying ? "Tạm dừng nhạc" : "Phát nhạc"))
            {
                // Audio starts only from a user gesture on WebGL.
                if (music.isPlaying) music.Pause(); else music.Play();
            }
            volume = GUILayout.HorizontalSlider(volume, 0f, 1f);
            if (music) music.volume = volume;
            bool nextQuality = GUILayout.Toggle(lowQuality, "Chế độ nhẹ · 30 FPS");
            if (nextQuality != lowQuality) { lowQuality = nextQuality; SetQuality(); }
            GUI.enabled = !story || !story.Playing;
            if (library && GUILayout.Button(library.visible ? "Đóng thư viện asset" : "Mở thư viện asset")) { library.visible = !library.visible; if (library.visible && story) story.visible = false; }
            GUI.enabled = true;
            if (story && GUILayout.Button(story.visible ? "Đóng bảng cốt truyện" : "Cốt truyện · công việc · save"))
            { story.visible = !story.visible; if (story.visible) library.visible = false; }
            if (environment)
            {
                int time = GUILayout.Toolbar(environment.selectedTime, new[] { "Ngày", "Chiều", "Đêm", "Mây" });
                if (time != environment.selectedTime) environment.SelectTime(time);
                showEnvironments = GUILayout.Toggle(showEnvironments, "Cảnh ngoài cửa sổ");
                if (showEnvironments)
                {
                    environmentScroll = GUILayout.BeginScrollView(environmentScroll);
                    if (GUILayout.Button("Bầu trời")) environment.SelectEnvironment(0);
                    for (int i = 0; i < environment.environments.Length; i++)
                        if (GUILayout.Button(environment.environments[i].name.Replace("Env_", ""))) environment.SelectEnvironment(i + 1);
                    GUILayout.EndScrollView();
                }
            }
            GUILayout.EndArea();
            GUI.matrix = previous;
        }
    }
}
