using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hlc.ChillPrototype
{
    public static class ChillCozyLighting
    {
        [Serializable]public class ColorKey{public float time;public Color color;}
        [Serializable]public class AlphaKey{public float time,value;}
        [Serializable]public class CurveKey{public float time,value,inSlope,outSlope;}
        [Serializable]public class Variable
        {
            public int mode,gradientMode;public Color color;public float value;public ColorKey[] colors;public AlphaKey[] alphas;public CurveKey[] curve;
            public Color ColorAt(float time)
            {
                if(mode==1)return color;
                var gradient=new Gradient{mode=gradientMode==1?GradientMode.Fixed:GradientMode.Blend};
                gradient.SetKeys(colors.Select(k=>new GradientColorKey(k.color,k.time)).ToArray(),alphas.Select(k=>new GradientAlphaKey(k.value,k.time)).ToArray());return gradient.Evaluate(time);
            }
            public float FloatAt(float time)=>mode==1?value:new AnimationCurve(curve.Select(k=>new Keyframe(k.time,k.value,k.inSlope,k.outSlope)).ToArray()).Evaluate(time);
        }
        [Serializable]public class Preset{public string name;public float time,cumulus,saturation,brightness;public Color filter,sunFilter;public Variable zenith,horizon,multiplier,sun,sky,direction,pitch;}
        [Serializable]public class Configuration{public Preset[] presets;}
        public static Color Filter(Color color,Preset preset)
        {
            float alpha=color.a;Color.RGBToHSV(color,out float h,out float s,out float v);
            var result=Color.HSVToRGB(h,Mathf.Clamp(s+preset.saturation,0,10),Mathf.Clamp(v+preset.brightness,0,10))*preset.filter;result.a=alpha;return result;
        }
        public static void Apply(Preset preset,Light sun)
        {
            float multiplier=preset.multiplier.FloatAt(preset.time);
            Color zenith=Filter(preset.zenith.ColorAt(preset.time),preset),horizon=Filter(preset.horizon.ColorAt(preset.time),preset);
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=zenith*multiplier;
            RenderSettings.ambientEquatorColor=horizon*(1-preset.cumulus/2)*multiplier;RenderSettings.ambientGroundColor=horizon*Color.gray*(1-preset.cumulus/2)*multiplier;
            if(sun){if(preset.direction!=null&&preset.pitch!=null){sun.transform.parent.eulerAngles=new Vector3(0,preset.direction.FloatAt(preset.time),preset.pitch.FloatAt(preset.time));sun.transform.localEulerAngles=new Vector3(preset.time*360-90,0,0);}sun.color=preset.sun.ColorAt(preset.time)*preset.sunFilter;sun.enabled=sun.color.r+sun.color.g+sun.color.b>0;sun.shadows=sun.enabled?LightShadows.Soft:LightShadows.None;RenderSettings.sun=sun;}
        }
    }
}
