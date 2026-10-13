using System;
using System.Linq;
using UnityEngine;
using Hlc.ChillPrototype;

// Reproduce verified startup visibility rules without replacing the already
// converted room meshes/materials with unused source skin variants.
public static class ChillAppearanceBuild
{
    static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
    public static void Configure(ChillGraphicsPlayer player)
    {
        var data=JsonUtility.FromJson<ChillDecorations.Configuration>(player.story.decorations.configuration.text);
        if(data.defaultSkins==null || data.defaultSkins.Length!=9)throw new InvalidOperationException("Decoration master defaults are missing.");
        var all=UnityEngine.Object.FindObjectsOfType<Transform>(true).GroupBy(PathOf).ToDictionary(g=>g.Key,g=>g.First());
        Transform Find(string path)=>path!=null && all.TryGetValue(path,out var t)?t:null;
        foreach(var path in data.inactiveOnStart ?? Array.Empty<string>())if(Find(path))Find(path).gameObject.SetActive(false);
        if(Find(data.hat))Find(data.hat).gameObject.SetActive(false);
        // ChangeHairService.ForceNormal runs for a new player's work-time total.
        foreach(var path in data.hairNormal ?? Array.Empty<string>())if(Find(path))Find(path).gameObject.SetActive(true);
        foreach(var path in (data.hairAlternates ?? Array.Empty<string>()).Concat(data.hairPins ?? Array.Empty<string>()))if(Find(path))Find(path).gameObject.SetActive(false);
        var hair=Find(data.hair)?.GetComponent<SkinnedMeshRenderer>();if(hair && hair.sharedMesh.blendShapeCount>1)hair.SetBlendShapeWeight(1,0);
        int hidden=0;
        foreach(var binding in data.bindings.Where(b=>b.kind=="active"))
        {
            // Category 5 story book, 11 badge and 12 music box have no active
            // skin in a new source save. Story commands can enable them later.
            if(data.skins.Any(s=>s.category==binding.category && data.defaultSkins.Contains(s.name)))continue;
            foreach(var model in binding.models)foreach(var path in model.objects)
                if(Find(path)){Find(path).gameObject.SetActive(false);hidden++;}
            foreach(var state in binding.states)foreach(var path in state.objects ?? Array.Empty<string>())
                if(Find(path)){Find(path).gameObject.SetActive(false);hidden++;}
        }
        Debug.Log("HLC_APPEARANCE_STARTUP defaultsAvailable="+data.defaultSkins.Length+" optionalPropsHidden="+hidden+" hat=off lantern=off hair=normal convertedGeometry=preserved");
    }
}
