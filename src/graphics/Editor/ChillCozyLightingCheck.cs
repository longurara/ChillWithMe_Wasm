using System;
using System.IO;
using System.Linq;
using Hlc.ChillPrototype;
using UnityEngine;

public static class ChillCozyLightingCheck
{
    static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
    public static void Run(ChillGraphicsPlayer player)
    {
        var source=JsonUtility.FromJson<ChillCozyLighting.Configuration>(File.ReadAllText("Assets/Prototype/cozy-lighting.json"));
        Require(source.presets.Length==4,"Missing source atmosphere presets");
        int previous=player.environment.selectedTime;
        foreach(var preset in source.presets)
        {
            int index=Array.IndexOf(source.presets,preset);var configured=player.environment.presets[index];
            Require(configured.cozy!=null&&Mathf.Abs(configured.cozy.time-preset.time)<1e-6,"COZY time input was replaced by an approximation");
            Require(configured.originalSun&&configured.originalSun.transform.parent.name=="Sun Offset","Source sun hierarchy was lost");
            float multiplier=preset.multiplier.FloatAt(preset.time);var sky=ChillCozyLighting.Filter(preset.zenith.ColorAt(preset.time),preset)*multiplier;
            player.environment.SelectTime(index);
            Require(Vector4.Distance(RenderSettings.ambientSkyColor,sky)<1e-5,"Runtime ambient does not use the source atmosphere gradient");
            Require(Mathf.Abs(configured.originalSun.intensity-2)<1e-4,"Source sunlight intensity was overwritten");
            Require(configured.cozy.direction!=null&&configured.cozy.pitch!=null,"Sun orbit inputs were not bound");
            var expected=Quaternion.Euler(0,preset.direction.FloatAt(preset.time),preset.pitch.FloatAt(preset.time))*Quaternion.Euler(preset.time*360-90,0,0);
            Require(Quaternion.Angle(configured.originalSun.transform.rotation,expected)<.1f,"Sun orbit does not follow the source COZY formula");
        }
        player.environment.SelectTime(previous);
        File.WriteAllText("../research/cozy-lighting-check.json","{\"passed\":true,\"presets\":4,\"scope\":\"Source COZY gradient/time/light bindings; PC pixel parity remains unverified\",\"authoredSunHierarchy\":true,\"sourceSunIntensity\":true,\"runtimeAmbientInputs\":true}");
        Debug.Log("HLC_COZY_LIGHTING_CHECK_PASSED");
    }
}
