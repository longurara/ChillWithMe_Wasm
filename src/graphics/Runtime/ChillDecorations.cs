using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public sealed class ChillDecorations : MonoBehaviour
    {
        [Serializable] public class Configuration { public Skin[] skins; public Binding[] bindings; public string[] defaultSkins,inactiveOnStart,hairNormal,hairAlternates,hairPins; public string hat, hair, screenSharing; }
        [Serializable] public class Skin { public string name; public int id, model, category; }
        [Serializable] public class State { public int id; public string mesh, lens, material, texture, emission; public string[] objects; }
        [Serializable] public class Model { public int id; public string[] objects; public string renderer; }
        [Serializable] public class Binding { public int category; public string kind, material, modelRoot, lens; public string[] meshes, renderers; public Model[] models; public State[] states; }
        public TextAsset configuration;
        public ChillLibraryBrowser library;
        Configuration data;
        public Configuration Data=>data??(data=JsonUtility.FromJson<Configuration>(configuration.text));
        public Skin[] Skins=>Data.skins;
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
        List<Material> clones = new List<Material>();
        int version;
        static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
        void Start()
        {
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
                .Concat(new[]{data.hat,data.hair,data.screenSharing}).Where(path=>!string.IsNullOrEmpty(path)));
            foreach (var t in transforms.Values)
            {
                var renderer=t.GetComponent<Renderer>();if(renderer)originalMaterials[renderer] = renderer.sharedMaterials;
                var filter=t.GetComponent<MeshFilter>();if(filter)originalMeshes[filter] = filter.sharedMesh;
                if(activePaths.Contains(PathOf(t)))originalActive[t.gameObject] = t.gameObject.activeSelf;
            }
        }
        Transform Find(string path) => path != null && transforms.TryGetValue(path, out var item) ? item : null;
        IEnumerable<GameObject> Objects(string[] paths) => (paths ?? Array.Empty<string>()).Select(Find).Where(t => t).Select(t => t.gameObject);
        IEnumerator Load(string slot, string guid, Action<UnityEngine.Object> loaded)
        {
            if (string.IsNullOrEmpty(guid)) { loaded(null); yield break; }
            bool done = false;
            library.LoadExternal(slot, guid, asset => { loaded(asset); done = true; });
            while (!done) yield return null;
        }
        public IEnumerator Apply(string name)
        {
            if (data == null) yield break;
            var skin = data.skins.FirstOrDefault(s => s.name == name);
            if (skin == null) { Debug.LogWarning("Unknown decoration skin: " + name); yield break; }
            int token = version;
            for (int i = 0; i < data.bindings.Length; i++)
            {
                var binding = data.bindings[i]; if (binding.category != skin.category) continue;
                var state = binding.states.FirstOrDefault(s => s.id == skin.id);
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
                if (binding.kind == "glasses" && Find(binding.modelRoot)) Find(binding.modelRoot).gameObject.SetActive(state != null);
                if (state == null) continue;
                Mesh mesh = null, lens = null; Material source = null; Texture texture = null, emission = null;
                string prefix = "decoration-" + i + "-";
                yield return Load(prefix + "mesh", state.mesh, asset => mesh = asset as Mesh);
                yield return Load(prefix + "lens", state.lens, asset => lens = asset as Mesh);
                yield return Load(prefix + "material", state.material ?? binding.material, asset => source = asset as Material);
                yield return Load(prefix + "texture", state.texture, asset => texture = asset as Texture);
                yield return Load(prefix + "emission", state.emission, asset => emission = asset as Texture);
                if (token != version) yield break;
                if (mesh) foreach (string path in binding.meshes ?? Array.Empty<string>()) if (Find(path)?.GetComponent<MeshFilter>() is MeshFilter filter) filter.sharedMesh = mesh;
                if (lens && Find(binding.lens)?.GetComponent<MeshFilter>() is MeshFilter lensFilter) lensFilter.sharedMesh = lens;
                if (binding.kind == "texture" && targets.Count == 0)
                    foreach (var item in originalMaterials)
                        if (item.Key && item.Value.Any(m => m && library.AssetGuidMatches(m.name, binding.material))) targets.Add(item.Key);
                foreach (var target in targets)
                {
                    var materials = target.sharedMaterials;
                    for (int n = 0; n < materials.Length; n++)
                    {
                        var material = source ? source : materials[n]; if (!material) continue;
                        var clone = new Material(material); clones.Add(clone);
                        if (texture) { if (clone.HasProperty("_MainTex")) clone.SetTexture("_MainTex", texture); if (clone.HasProperty("_BaseMap")) clone.SetTexture("_BaseMap", texture); }
                        if (emission && clone.HasProperty("_EmissionMap")) clone.SetTexture("_EmissionMap", emission);
                        materials[n] = clone;
                    }
                    target.sharedMaterials = materials;
                }
            }
        }
        public void Restore()
        {
            version++;
            foreach (var item in originalMaterials) if (item.Key) item.Key.sharedMaterials = item.Value;
            foreach (var item in originalMeshes) if (item.Key) item.Key.sharedMesh = item.Value;
            foreach (var item in originalActive) if (item.Key) item.Key.SetActive(item.Value);
            foreach (var material in clones) if (material) Destroy(material);
            clones.Clear();
        }
    }
}
