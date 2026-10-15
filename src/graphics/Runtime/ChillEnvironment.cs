using System;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public sealed class ChillEnvironment : MonoBehaviour
    {
        [Serializable] public class Preset
        {
            public string name;
            public Texture2D[] textures;
            public Renderer[] renderers;
            public int[] indices;
            public Vector4[] offsets;
            public Color ambient, sunColor, sky;
            public float sunIntensity;
            public UnityEngine.Rendering.VolumeProfile postProfile;
            public ChillCozyLighting.Preset cozy;
            public Light originalSun;
        }
        public Preset[] presets;
        public Transform dateTime, window;
        public GameObject[] environments;
        public ChillGraphicsPlayer player;
        public int selectedTime, selectedEnvironment;
        public double LastChangeTime {get;private set;}
        void Awake(){LastChangeTime=ChillRuntimeContext.Now;}
        public UnityEngine.Rendering.Volume postVolume;
        static bool probesNormalized;

        public int FindEnvironment(string name)
        {
            switch (name)
            {
                case "Fireworks": name="hanabio_For_Place"; break;
                case "WindBell": name="Furin"; break;
                case "LightRain": name="Rain"; break;
                case "HeavyRain": name="RainHeavy"; break;
                case "ThunderRain": name="Thunder"; break;
                case "HotSpring": name="Onsen"; break;
                case "BlueButterfly": name="AlterEgo"; break;
                case "City": name="Buildings"; break;
                case "ValentineSweets": name="valentine"; break;
                case "FreshGreenery": name="Hometown"; break;
                case "SpaceB": name="Space2"; break;
            }
            int index=Array.FindIndex(environments,e=>string.Equals(e.name.Replace("Env_",""),name,StringComparison.OrdinalIgnoreCase));
            return index < 0 ? -1 : index+1;
        }

        public void SelectTime(int index)
        {
            if (presets == null || index < 0 || index >= presets.Length) return;
            LastChangeTime=ChillRuntimeContext.Now;
            selectedTime = index;
            var preset = presets[index];
            if(postVolume)postVolume.sharedProfile=preset.postProfile;
            if (dateTime)
            {
                foreach (Transform child in dateTime)
                {
                    bool active = string.Equals(child.name, preset.name, StringComparison.OrdinalIgnoreCase);
                    child.gameObject.SetActive(active);
                    if (active)
                        foreach (var l in child.GetComponentsInChildren<Light>(true))
                            if (l.type == LightType.Directional && l != preset.originalSun)
                                l.shadows = LightShadows.None;
                }
            }
            if (!probesNormalized && LightmapSettings.lightProbes != null)
            {
                probesNormalized = true;
                var probes = LightmapSettings.lightProbes.bakedProbes;
                if (probes != null && probes.Length > 0 && probes[0][0, 0] < 0.35f)
                {
                    for (int i = 0; i < probes.Length; i++)
                    {
                        for (int c = 0; c < 3; c++)
                            for (int k = 1; k < 9; k++)
                                probes[i][c, k] *= 0.25f;
                        probes[i].AddAmbientLight(new Color(0.22f, 0.22f, 0.24f));
                    }
                    LightmapSettings.lightProbes.bakedProbes = probes;
                }
            }
            player.dayLightmaps = preset.textures;
            player.lightmappedRenderers = preset.renderers;
            player.lightmapIndices = preset.indices; player.lightmapOffsets = preset.offsets;
            player.ambientLight = preset.ambient; player.ApplyDayLighting();
            if (RenderSettings.sun && preset.cozy==null)
            { RenderSettings.sun.color = preset.sunColor; RenderSettings.sun.intensity = preset.sunIntensity; }
            if (RenderSettings.skybox && RenderSettings.skybox.HasProperty("_SkyTint")) RenderSettings.skybox.SetColor("_SkyTint", preset.sky);
            if(preset.cozy!=null)ChillCozyLighting.Apply(preset.cozy,preset.originalSun);
            ApplyWindowViewAdjusters();
        }

        public float LastJetEmissionAlpha{get;private set;}
        public float LastFerrisWheelEmissionAlpha{get;private set;}
        public float LastSpaceBEnv{get;private set;}

        public void SelectEnvironment(int index)
        {
            if (environments == null || index < 0 || index > environments.Length) return;
            LastChangeTime=ChillRuntimeContext.Now;
            selectedEnvironment = index;
            if (!window) return;
            window.gameObject.SetActive(index > 0);
            for (int i = 0; i < environments.Length; i++) environments[i].SetActive(index == i + 1);
            if (index > 0)
                foreach (var renderer in environments[index - 1].GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
            ApplyWindowViewAdjusters();
        }

        public void ApplyWindowViewAdjusters()
        {
            LastJetEmissionAlpha = selectedTime == 0 ? 0f : selectedTime == 1 ? 0.6f : 1f;
            LastFerrisWheelEmissionAlpha = (selectedTime == 0 || selectedTime == 1) ? 0f : 1f;
            LastSpaceBEnv = Mathf.Clamp(selectedTime, 0, 3);
            if (environments == null || selectedEnvironment <= 0 || selectedEnvironment > environments.Length) return;
            var root = environments[selectedEnvironment - 1];
            if (!root) return;
            bool isSpaceB = root.name.IndexOf("Space2", StringComparison.OrdinalIgnoreCase) >= 0 || root.name.IndexOf("SpaceB", StringComparison.OrdinalIgnoreCase) >= 0;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer) continue;
                bool isJet = renderer.name.IndexOf("Jet", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isFerris = renderer.name.IndexOf("Ferris", StringComparison.OrdinalIgnoreCase) >= 0 || renderer.name.IndexOf("Kanransya", StringComparison.OrdinalIgnoreCase) >= 0 || renderer.name.IndexOf("Wheel", StringComparison.OrdinalIgnoreCase) >= 0;
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (!mat) continue;
                    if (isSpaceB && mat.HasProperty("_Env")) mat.SetFloat("_Env", LastSpaceBEnv);
                    if ((isJet || isFerris) && mat.HasProperty("_EmissionColor"))
                    {
                        var c = mat.GetColor("_EmissionColor");
                        c.a = isJet ? LastJetEmissionAlpha : LastFerrisWheelEmissionAlpha;
                        mat.SetColor("_EmissionColor", c);
                    }
                }
            }
        }
    }
}
