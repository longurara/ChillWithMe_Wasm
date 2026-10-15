using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public sealed class ChillDecorations : MonoBehaviour
    {
        [Serializable] public class Configuration { public Skin[] skins; public Binding[] bindings; public string[] defaultSkins,inactiveOnStart,hairNormal,hairAlternates,hairPins; public string hat, hair, screenSharing,santaHat,christmasObjects,christmasHair,satoneSunglasses,kouchanSunglasses; }
        [Serializable] public class Skin { public string name; public int id, model, category; }
        [Serializable] public class State { public int id; public string mesh, lens, material, texture, emission; public string[] objects; }
        [Serializable] public class Model { public int id; public string[] objects; public string renderer; }
        [Serializable] public class Binding { public int category; public string kind, material, modelRoot, lens; public string[] meshes, renderers; public Model[] models; public State[] states; }
        public TextAsset configuration;
        public ChillLibraryBrowser library;
        Configuration data;
        public Configuration Data=>data??(data=JsonUtility.FromJson<Configuration>(configuration.text));
        public Skin[] Skins=>Data.skins;
        MaterialPropertyBlock seasonalBlock;
        bool christmasWasVisible;
        Renderer[] seasonalRenderers;
        void SeasonalActive(string path,bool active){var item=Find(path);if(item&&item.gameObject.activeSelf!=active)item.gameObject.SetActive(active);}
        public void SeasonalObjects(bool christmas,bool april,float shine,bool wearingSunglasses=false)
        {
            if(transforms==null)return;
            if(seasonalBlock==null)seasonalBlock=new MaterialPropertyBlock();
            SeasonalActive(Data.santaHat,christmas);SeasonalActive(Data.christmasObjects,christmas);SeasonalActive(Data.satoneSunglasses,april||wearingSunglasses);SeasonalActive(Data.kouchanSunglasses,april);
            var hair=Find(Data.christmasHair)?.GetComponent<SkinnedMeshRenderer>();if((christmas||christmasWasVisible)&&hair&&hair.sharedMesh){int index=hair.sharedMesh.GetBlendShapeIndex("blendShape5.Hair_hat");if(index>=0)hair.SetBlendShapeWeight(index,christmas?100:0);}christmasWasVisible=christmas;
            var glasses=Find(Data.satoneSunglasses);if(seasonalRenderers==null&&glasses)seasonalRenderers=glasses.GetComponentsInChildren<Renderer>(true);
            if(seasonalRenderers!=null)foreach(var renderer in seasonalRenderers){renderer.GetPropertyBlock(seasonalBlock);var material=renderer.sharedMaterial;if(material&&material.HasProperty("_EmissionColor")){var color=material.GetColor("_EmissionColor");color.a=(april||wearingSunglasses)?shine:0;seasonalBlock.SetColor("_EmissionColor",color);renderer.SetPropertyBlock(seasonalBlock);}}
        }
        public void SetHair(int style)
        {
            var d=Data;if(transforms==null)return;
            foreach(var go in Objects(d.hairNormal))go.SetActive(style==0);
            foreach(var go in Objects(d.hairAlternates))go.SetActive(style>0);
            int index=0;foreach(var go in Objects(d.hairPins))go.SetActive(style>0&&index++==style-1);
        }
        Dictionary<string, Transform> transforms;
        Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        Dictionary<MeshFilter, Mesh> originalMeshes = new Dictionary<MeshFilter, Mesh>();
        Dictionary<GameObject, bool> originalActive = new Dictionary<GameObject, bool>();
        readonly Dictionary<string, UnityEngine.Object> assetCache = new Dictionary<string, UnityEngine.Object>();
        readonly Dictionary<int, List<Material>> categoryClones = new Dictionary<int, List<Material>>();
        readonly Dictionary<int, string> appliedCategorySkins = new Dictionary<int, string>();
        List<Material> clones = new List<Material>();
        int version;
        static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
        void EnsureInitialized()
        {
            if (transforms != null && data != null) return;
            data = JsonUtility.FromJson<Configuration>(configuration.text);
            transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).GroupBy(PathOf).ToDictionary(g => g.Key, g => g.First());
            // Only decoration objects belong to this snapshot. Restoring every
            // scene object reopens source debug/windows after native UI Start.
            var activePaths=new HashSet<string>(data.bindings.SelectMany(b=>
                (b.models??Array.Empty<Model>()).SelectMany(m=>m.objects??Array.Empty<string>())
                .Concat((b.states??Array.Empty<State>()).SelectMany(state=>state.objects??Array.Empty<string>()))
                .Concat(new[]{b.modelRoot}))
                .Concat(data.hairNormal??Array.Empty<string>()).Concat(data.hairAlternates??Array.Empty<string>())
                .Concat(data.hairPins??Array.Empty<string>()).Concat(data.inactiveOnStart??Array.Empty<string>())
                .Concat(new[]{data.hat,data.hair,data.screenSharing,data.satoneSunglasses}).Where(path=>!string.IsNullOrEmpty(path)));
            foreach (var t in transforms.Values)
            {
                var renderer=t.GetComponent<Renderer>();if(renderer)originalMaterials[renderer] = renderer.sharedMaterials;
                var filter=t.GetComponent<MeshFilter>();if(filter)originalMeshes[filter] = filter.sharedMesh;
                if(activePaths.Contains(PathOf(t)))originalActive[t.gameObject] = t.gameObject.activeSelf;
            }
        }
        void Start() => EnsureInitialized();
        Transform Find(string path) => path != null && transforms != null && transforms.TryGetValue(path, out var item) ? item : null;
        IEnumerable<GameObject> Objects(string[] paths) => (paths ?? Array.Empty<string>()).Select(Find).Where(t => t).Select(t => t.gameObject);
        static readonly Dictionary<Mesh, Mesh> sanitizedMeshes = new Dictionary<Mesh, Mesh>();
        public static Mesh SanitizeMesh(Mesh src)
        {
            if (!src) return null;
            if (sanitizedMeshes.TryGetValue(src, out var cached) && cached) return cached;
            if (!src.isReadable) return src;
            var clean = new Mesh { name = src.name, indexFormat = src.indexFormat };
            var vs = src.vertices;
            clean.SetVertices(vs);
            var ns = src.normals; if (ns != null && ns.Length == vs.Length) clean.SetNormals(ns);
            var ts = src.tangents; if (ts != null && ts.Length == vs.Length) clean.SetTangents(ts);
            var cs = src.colors32; if (cs != null && cs.Length == vs.Length) clean.SetColors(cs);
            for (int ch = 0; ch < 4; ch++)
            {
                var uvs = new List<Vector4>();
                src.GetUVs(ch, uvs);
                if (uvs.Count == vs.Length) clean.SetUVs(ch, uvs);
            }
            clean.subMeshCount = src.subMeshCount;
            for (int sm = 0; sm < src.subMeshCount; sm++)
                clean.SetIndices(src.GetIndices(sm), src.GetTopology(sm), sm, false);
            clean.RecalculateBounds();
            clean.UploadMeshData(false);
            sanitizedMeshes[src] = clean;
            sanitizedMeshes[clean] = clean;
            return clean;
        }
        IEnumerator Load(string guid, Action<UnityEngine.Object> loaded)
        {
            if (string.IsNullOrEmpty(guid)) { loaded(null); yield break; }
            if (assetCache.TryGetValue(guid, out var cached) && cached) { loaded(cached is Mesh cm ? SanitizeMesh(cm) : cached); yield break; }
            bool done = false;
            library.LoadExternal("decoration-" + guid, guid, asset => { if (asset is Mesh m) asset = SanitizeMesh(m); if (asset) assetCache[guid] = asset; loaded(asset); done = true; });
            while (!done) yield return null;
        }
        bool TryGetCached(string guid, out UnityEngine.Object asset)
        {
            if (string.IsNullOrEmpty(guid)) { asset = null; return true; }
            if (assetCache.TryGetValue(guid, out asset) && asset) { if (asset is Mesh m) assetCache[guid] = asset = SanitizeMesh(m); return true; }
            asset = null; return false;
        }
        public static void RefreshRenderer(GameObject go, ChillGraphicsPlayer player = null, Dictionary<Renderer, Material[]> originalMaterials = null, Renderer[] seasonalRenderers = null)
        {
            if (!go || !(go.GetComponent<MeshRenderer>() is MeshRenderer r)) return;
            var mats = r.sharedMaterials;
            bool en = r.enabled, rec = r.receiveShadows;
            var cast = r.shadowCastingMode;
            var lp = r.lightProbeUsage;
            var rp = r.reflectionProbeUsage;
            var mask = r.renderingLayerMask;
            int sl = r.sortingLayerID, so = r.sortingOrder;
            int lmIdx = r.lightmapIndex;
            var lmOff = r.lightmapScaleOffset;
            Material[] origMats = null;
            bool hadOrig = originalMaterials != null && originalMaterials.TryGetValue(r, out origMats);
            if (hadOrig) originalMaterials.Remove(r);
            DestroyImmediate(r);
            var nr = go.AddComponent<MeshRenderer>();
            nr.sharedMaterials = mats;
            nr.enabled = en; nr.receiveShadows = rec;
            nr.shadowCastingMode = cast;
            nr.lightProbeUsage = lp; nr.reflectionProbeUsage = rp;
            nr.renderingLayerMask = mask;
            nr.sortingLayerID = sl; nr.sortingOrder = so;
            if (lmIdx >= 0) { nr.lightmapIndex = lmIdx; nr.lightmapScaleOffset = lmOff; }
            if (hadOrig) originalMaterials[nr] = origMats;
            if (seasonalRenderers != null)
                for (int i = 0; i < seasonalRenderers.Length; i++)
                    if (seasonalRenderers[i] == r) seasonalRenderers[i] = nr;
            if (player)
            {
                if (player.lightmappedRenderers != null)
                    for (int i = 0; i < player.lightmappedRenderers.Length; i++)
                        if (player.lightmappedRenderers[i] == r) player.lightmappedRenderers[i] = nr;
                if (player.environment && player.environment.presets != null)
                    foreach (var preset in player.environment.presets)
                        if (preset.renderers != null)
                            for (int i = 0; i < preset.renderers.Length; i++)
                                if (preset.renderers[i] == r) preset.renderers[i] = nr;
            }
        }
        void SetMesh(MeshFilter filter, Mesh mesh)
        {
            var target = SanitizeMesh(mesh);
            if (!filter || filter.sharedMesh == target) return;
            filter.sharedMesh = null;
            filter.sharedMesh = target;
            RefreshRenderer(filter.gameObject, GetComponent<ChillGraphicsPlayer>(), originalMaterials, seasonalRenderers);
        }
        public void RestoreCategory(int category) => RestoreCategoryInternal(category, null);
        void RestoreCategoryInternal(int category, HashSet<MeshFilter> skipMeshes)
        {
            EnsureInitialized();
            if (data == null || !appliedCategorySkins.Remove(category)) return;
            foreach (var binding in data.bindings)
            {
                if (binding.category != category) continue;
                foreach (string path in binding.meshes ?? Array.Empty<string>())
                    if (Find(path)?.GetComponent<MeshFilter>() is MeshFilter mf && (skipMeshes == null || !skipMeshes.Contains(mf)) && originalMeshes.TryGetValue(mf, out var origMesh)) SetMesh(mf, origMesh);
                if (Find(binding.lens)?.GetComponent<MeshFilter>() is MeshFilter lf && (skipMeshes == null || !skipMeshes.Contains(lf)) && originalMeshes.TryGetValue(lf, out var origLens)) SetMesh(lf, origLens);
                foreach (string path in binding.renderers ?? Array.Empty<string>())
                    if (Find(path)?.GetComponent<Renderer>() is Renderer r && originalMaterials.TryGetValue(r, out var origMats)) r.sharedMaterials = origMats;
                if (binding.models != null)
                {
                    foreach (var model in binding.models)
                    {
                        foreach (var go in Objects(model.objects))
                            if (originalActive.TryGetValue(go, out bool act)) go.SetActive(act);
                        if (Find(model.renderer)?.GetComponent<Renderer>() is Renderer mr && originalMaterials.TryGetValue(mr, out var origMats)) mr.sharedMaterials = origMats;
                    }
                }
                if (binding.states != null)
                    foreach (var option in binding.states)
                        foreach (var go in Objects(option.objects))
                            if (originalActive.TryGetValue(go, out bool act)) go.SetActive(act);
                if (Find(binding.modelRoot)?.gameObject is GameObject rootGo && originalActive.TryGetValue(rootGo, out bool rootAct)) rootGo.SetActive(rootAct);
                if (binding.kind == "glasses" && Find(Data.satoneSunglasses)?.gameObject is GameObject sgGo && originalActive.TryGetValue(sgGo, out bool sgAct)) sgGo.SetActive(sgAct);
                if (binding.kind == "texture" && !string.IsNullOrEmpty(binding.material))
                    foreach (var item in originalMaterials)
                        if (item.Key && item.Value.Any(m => m && library.AssetGuidMatches(m.name, binding.material))) item.Key.sharedMaterials = item.Value;
            }
            if (categoryClones.TryGetValue(category, out var list))
            {
                foreach (var mat in list) { if (mat) { if (Application.isPlaying) Destroy(mat); else DestroyImmediate(mat); } clones.Remove(mat); }
                list.Clear();
            }
        }
        class PendingBinding
        {
            public Binding binding;
            public State state;
            public Mesh mesh, lens;
            public Material source;
            public Texture texture, emission;
        }
        public IEnumerator Apply(string name)
        {
            EnsureInitialized();
            if (data == null) yield break;
            var skin = data.skins.FirstOrDefault(s => s.name == name);
            if (skin == null) { Debug.LogWarning("Unknown decoration skin: " + name); yield break; }
            if (appliedCategorySkins.TryGetValue(skin.category, out string currentSkin) && currentSkin == skin.name) yield break;
            int token = version;
            var pending = new List<PendingBinding>();
            for (int i = 0; i < data.bindings.Length; i++)
            {
                var binding = data.bindings[i]; if (binding.category != skin.category) continue;
                var state = binding.states.FirstOrDefault(s => s.id == skin.id);
                var entry = new PendingBinding { binding = binding, state = state };
                pending.Add(entry);
                if (state == null) continue;
                bool hasSceneTarget = (binding.renderers ?? Array.Empty<string>()).Any(p => Find(p)) ||
                                      (binding.meshes ?? Array.Empty<string>()).Any(p => Find(p)) ||
                                      Find(binding.lens) != null ||
                                      (binding.models ?? Array.Empty<Model>()).Any(m => Find(m.renderer) != null) ||
                                      binding.kind == "texture";
                if (!hasSceneTarget) continue;
                if (TryGetCached(state.mesh, out var mObj)) entry.mesh = mObj as Mesh;
                else yield return Load(state.mesh, asset => entry.mesh = asset as Mesh);
                if (TryGetCached(state.lens, out var lObj)) entry.lens = lObj as Mesh;
                else yield return Load(state.lens, asset => entry.lens = asset as Mesh);
                string matGuid = state.material ?? binding.material;
                if (TryGetCached(matGuid, out var matObj)) entry.source = matObj as Material;
                else yield return Load(matGuid, asset => entry.source = asset as Material);
                if (TryGetCached(state.texture, out var texObj)) entry.texture = texObj as Texture;
                else yield return Load(state.texture, asset => entry.texture = asset as Texture);
                if (TryGetCached(state.emission, out var emObj)) entry.emission = emObj as Texture;
                else yield return Load(state.emission, asset => entry.emission = asset as Texture);
                if (token != version) yield break;
            }
            var skipMeshes = new HashSet<MeshFilter>();
            foreach (var entry in pending)
            {
                if (entry.state == null) continue;
                if (entry.mesh) foreach (string path in entry.binding.meshes ?? Array.Empty<string>()) if (Find(path)?.GetComponent<MeshFilter>() is MeshFilter filter) skipMeshes.Add(filter);
                if (entry.lens && Find(entry.binding.lens)?.GetComponent<MeshFilter>() is MeshFilter lensFilter) skipMeshes.Add(lensFilter);
            }
            RestoreCategoryInternal(skin.category, skipMeshes);
            appliedCategorySkins[skin.category] = skin.name;
            if (!categoryClones.TryGetValue(skin.category, out var catList)) categoryClones[skin.category] = catList = new List<Material>();
            foreach (var entry in pending)
            {
                var binding = entry.binding;
                var state = entry.state;
                if (state != null)
                {
                    if (entry.mesh) foreach (string path in binding.meshes ?? Array.Empty<string>()) if (Find(path)?.GetComponent<MeshFilter>() is MeshFilter filter) SetMesh(filter, entry.mesh);
                    if (entry.lens && Find(binding.lens)?.GetComponent<MeshFilter>() is MeshFilter lensFilter) SetMesh(lensFilter, entry.lens);
                }
                var targets = (binding.renderers ?? Array.Empty<string>()).Select(Find).Where(t => t).Select(t => t.GetComponent<Renderer>()).Where(r => r).ToList();
                if (binding.kind == "headphone" || binding.kind == "active")
                {
                    foreach (var model in binding.models)
                    {
                        foreach (var go in Objects(model.objects)) go.SetActive(model.id == skin.model);
                        if (model.id == skin.model && Find(model.renderer)?.GetComponent<Renderer>() is Renderer renderer) targets.Add(renderer);
                    }
                    if (binding.kind == "active")
                        foreach (var option in binding.states) foreach (var go in Objects(option.objects)) go.SetActive(option.id == skin.id);
                }
                if (binding.kind == "glasses")
                {
                    bool isSunglasses = skin.model == 3004;
                    if (Find(binding.modelRoot)) Find(binding.modelRoot).gameObject.SetActive(!isSunglasses && state != null);
                    if (Find(Data.satoneSunglasses)) Find(Data.satoneSunglasses).gameObject.SetActive(isSunglasses);
                }
                if (state == null) continue;
                if (binding.kind == "texture" && targets.Count == 0)
                    foreach (var item in originalMaterials)
                        if (item.Key && item.Value.Any(m => m && library.AssetGuidMatches(m.name, binding.material))) targets.Add(item.Key);
                foreach (var target in targets)
                {
                    var materials = target.sharedMaterials;
                    for (int n = 0; n < materials.Length; n++)
                    {
                        var material = entry.source ? entry.source : materials[n]; if (!material) continue;
                        var clone = new Material(material); clones.Add(clone); catList.Add(clone);
                        if (entry.texture) { if (clone.HasProperty("_MainTex")) clone.SetTexture("_MainTex", entry.texture); if (clone.HasProperty("_BaseMap")) clone.SetTexture("_BaseMap", entry.texture); }
                        if (entry.emission && clone.HasProperty("_EmissionMap")) clone.SetTexture("_EmissionMap", entry.emission);
                        materials[n] = clone;
                    }
                    target.sharedMaterials = materials;
                }
            }
        }
        public void Restore()
        {
            EnsureInitialized();
            version++;
            appliedCategorySkins.Clear();
            foreach (var item in originalMeshes) if (item.Key) SetMesh(item.Key, item.Value);
            foreach (var item in originalMaterials) if (item.Key) item.Key.sharedMaterials = item.Value;
            foreach (var item in originalActive) if (item.Key) item.Key.SetActive(item.Value);
            foreach (var material in clones) if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            clones.Clear();
            foreach (var list in categoryClones.Values) list.Clear();
        }
    }
}
