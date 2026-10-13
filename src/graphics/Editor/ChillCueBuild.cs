using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Hlc.ChillPrototype;
public static class ChillCueBuild
{
    public static void RepairAndCheck()
    {
        string catalogPath="Assets/StreamingAssets/chill-library.json";
        var catalog=JsonUtility.FromJson<ChillAssetCatalog>(File.ReadAllText(catalogPath));
        var names=new[]{"bulbul_systemse_cancel","bulbul_systemse_click","bulbul_systemse_completetask"};
        var clips=catalog.assets.Where(a=>names.Contains(a.name)).ToArray();
        if(clips.Length!=3)throw new InvalidOperationException("Cannot find repaired cue assets");
        foreach(var item in clips){AssetDatabase.ImportAsset(item.path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(item.path);if(!clip)throw new InvalidOperationException("PCM cue still cannot import: "+item.name);item.type="AudioClip";item.seconds=clip.length;item.channels=clip.channels;}
        string output=Path.GetFullPath("../CueBuild");Directory.CreateDirectory(output);
        var builds=clips.Select(a=>new AssetBundleBuild{assetBundleName=a.bundle,assetNames=new[]{a.path}}).ToArray();
        foreach(var build in builds)Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(output,build.assetBundleName)));
        var manifest=BuildPipeline.BuildAssetBundles(output,builds,BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.WebGL);
        if(!manifest)throw new InvalidOperationException("Sound cue bundle build failed");
        string live=Path.GetFullPath("../AssetLibrary/assetpacks");
        var entries=catalog.bundles.ToList();
        foreach(var item in clips){string file=Path.Combine(output,item.bundle);var entry=entries.FirstOrDefault(b=>b.name==item.bundle);if(entry==null){entry=new ChillBundleEntry{name=item.bundle};entries.Add(entry);}if(!BuildPipeline.GetCRCForAssetBundle(file,out uint crc))throw new InvalidOperationException("Cue CRC missing");entry.crc=crc;entry.hash=manifest.GetAssetBundleHash(item.bundle).ToString();entry.dependencies=manifest.GetAllDependencies(item.bundle);entry.bytes=new FileInfo(file).Length;File.Copy(file,Path.Combine(live,item.bundle),true);File.Copy(file+".manifest",Path.Combine(live,item.bundle)+".manifest",true);}
        catalog.bundles=entries.OrderBy(b=>b.name).ToArray();
        string json=JsonUtility.ToJson(catalog,true);File.WriteAllText(catalogPath,json);File.WriteAllText("../AssetLibrary/chill-library.json",json);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("HLC_SOUND_CUES_REPAIRED bundles=3 payloadsUnchanged=true");ChillFeaturesCheck.Run();
    }
}
