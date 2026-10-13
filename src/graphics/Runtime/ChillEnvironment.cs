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
                foreach (Transform child in dateTime)
                    child.gameObject.SetActive(string.Equals(child.name, preset.name, StringComparison.OrdinalIgnoreCase));
            player.dayLightmaps = preset.textures;
            player.lightmappedRenderers = preset.renderers;
            player.lightmapIndices = preset.indices; player.lightmapOffsets = preset.offsets;
            player.ambientLight = preset.ambient; player.ApplyDayLighting();
            if (RenderSettings.sun && preset.cozy==null)
            { RenderSettings.sun.color = preset.sunColor; RenderSettings.sun.intensity = preset.sunIntensity; }
            if (RenderSettings.skybox && RenderSettings.skybox.HasProperty("_SkyTint")) RenderSettings.skybox.SetColor("_SkyTint", preset.sky);
            if(preset.cozy!=null)ChillCozyLighting.Apply(preset.cozy,preset.originalSun);
        }

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
        }
    }
}
