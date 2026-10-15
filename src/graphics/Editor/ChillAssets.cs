using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public static class ChillAssets
    {
        [Serializable] class SourceCatalog { public ChillAssetEntry[] assets; public DataEntry[] data; }
        [Serializable] class DataEntry { public string path, name, guid; }

        public static void CheckShaders()
        {
            var failures = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Prototype/WebShaders" }))
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                        failures.Add(shader.name + ": " + message.message + " (" + message.line + ")");
            }
            if (failures.Count > 0) throw new InvalidOperationException("Web shader compilation failed:\n" + string.Join("\n", failures));
            if (File.Exists(Application.consoleLogPath))
            {
                string log;
                using (var stream = new FileStream(Application.consoleLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream)) log = reader.ReadToEnd();
                foreach (string name in new[] { "lilToon", "Hidden/lilToonCutout", "Hidden/lilToonTransparent" })
                    if (log.Contains("Shader Unsupported: '" + name + "' - All subshaders removed"))
                        throw new InvalidOperationException("WebGL toon shader was removed and would fall back to unlit: " + name);
                if (System.Text.RegularExpressions.Regex.IsMatch(log, "Shader Unsupported: 'HLC/lilToon/[^']+'"))
                    throw new InvalidOperationException("A material-specific WebGL toon shader is unsupported.");
            }
        }

        [MenuItem("HLC/Chill/Build complete WebGL asset library")]
        public static void BuildLibrary()
        { BuildLibrary(true); }
        public static void RebuildForLighting()
        {
            ChillLightingBuild.Configure();AssetDatabase.SaveAssets();BuildLibrary(false, true);
        }
        public static void RebuildMeshBundles()
        {
            BuildLibrary(false, false);
        }
        static void BuildLibrary(bool regenerateToon, bool forceRebuild = false)
        {
            if(regenerateToon)ChillBuild.PrepareToonForWeb();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var input = JsonUtility.FromJson<SourceCatalog>(File.ReadAllText("Assets/Prototype/asset-catalog-source.json"));
            var assets = input.assets.Concat(input.data.Select(d => new ChillAssetEntry {
                path=d.path, name=d.name, guid=d.guid, category="data"
            })).ToArray();
            var buckets = new Dictionary<string,List<ChillAssetEntry>>();
            var counts = new Dictionary<string,int>();
            foreach (var asset in assets)
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(asset.path);
                if (!obj) throw new InvalidOperationException("Asset failed to import: " + asset.path);
                asset.assetName = asset.path.ToLowerInvariant();
                asset.type = obj.GetType().Name;
                if (obj is AudioClip audio) { asset.seconds=audio.length; asset.channels=audio.channels; }
                if (obj is AnimationClip animation) { asset.seconds=animation.length; asset.humanMotion=animation.humanMotion; }
                if (obj is Texture texture) { asset.width=texture.width; asset.height=texture.height; }
                if (obj is Mesh mesh) asset.vertices=mesh.vertexCount;
                if (obj is Material material && (!material.shader || material.shader.name.Contains("InternalError")))
                    throw new InvalidOperationException("Material shader unresolved: " + asset.path);
                if (obj is GameObject prefab)
                    foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject)>0)
                            throw new InvalidOperationException("Prefab has missing scripts: " + asset.path);
                int number = counts.TryGetValue(asset.category,out int previous) ? previous : 0;
                counts[asset.category]=number+1;
                int packSize = asset.category=="music" ? 1 : asset.category=="voice" ? 64 : 64;
                string category=asset.category.ToLowerInvariant();
                asset.bundle=category+"/pack-"+(number/packSize).ToString("D4");
                if (!buckets.TryGetValue(asset.bundle,out var bucket)) buckets[asset.bundle]=bucket=new List<ChillAssetEntry>();
                bucket.Add(asset);
            }
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../AssetLibrary/assetpacks"));
            Directory.CreateDirectory(output);
            var builds=buckets.Select(b => new AssetBundleBuild { assetBundleName=b.Key, assetNames=b.Value.Select(a => a.path).ToArray() }).ToArray();
            foreach(var build in builds) Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(output,build.assetBundleName)));
            var options=BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode;
            // URP strips light keywords when additional lighting is disabled.
            // Rebuild existing bundles so cached materials get the new variants.
            if(!regenerateToon&&forceRebuild)options|=BuildAssetBundleOptions.ForceRebuildAssetBundle;
            var manifest=BuildPipeline.BuildAssetBundles(output,builds,options,BuildTarget.WebGL);
            if (!manifest) throw new InvalidOperationException("WebGL asset library build failed.");
            CheckShaders();
            var bundles=manifest.GetAllAssetBundles().Select(name => {
                string file=Path.Combine(output,name);
                if (!BuildPipeline.GetCRCForAssetBundle(file,out uint crc)) throw new InvalidOperationException("Bundle CRC unavailable: "+name);
                return new ChillBundleEntry { name=name, dependencies=manifest.GetAllDependencies(name), crc=crc,
                    hash=manifest.GetAssetBundleHash(name).ToString(), bytes=new FileInfo(file).Length };
            }).ToArray();
            var catalog=new ChillAssetCatalog { version=1, assets=assets, bundles=bundles };
            string json=JsonUtility.ToJson(catalog,true);
            string parent=Path.GetDirectoryName(output);
            File.WriteAllText(Path.Combine(parent,"chill-library.json"),json);
            Directory.CreateDirectory("Assets/StreamingAssets");
            File.WriteAllText("Assets/StreamingAssets/chill-library.json",json);
            Debug.Log("HLC_CHILL_LIBRARY_BUILT assets="+assets.Length+" bundles="+bundles.Length+" bytes="+bundles.Sum(b => b.bytes));
        }
    }
}
