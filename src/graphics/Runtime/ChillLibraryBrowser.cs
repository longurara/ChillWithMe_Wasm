using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Hlc.ChillPrototype
{
    // Asset inspection harness, separate from the original game's story systems.
    public sealed class ChillLibraryBrowser : MonoBehaviour
    {
        public Animator character;
        public AudioSource music;
        public Camera view;
        public Material previewMaterial;
        public ChillBonePhysics physics;
        public Font uiFont;
        public bool visible;
        class Pack { public AssetBundle bundle; public int users; }
        readonly Dictionary<string, Pack> packs = new Dictionary<string, Pack>();
        readonly List<string> musicPacks = new List<string>(), previewPacks = new List<string>();
        readonly Dictionary<string, List<string>> externalPacks = new Dictionary<string, List<string>>();
        public bool Ready => metadata != null;
        public bool Busy => busy;
        public string Status => status;
        public ChillAssetEntry[] MusicEntries => catalog == null ? Array.Empty<ChillAssetEntry>() : catalog.assets.Where(a => a.type == "AudioClip" && a.category == "music").OrderBy(a => a.name,StringComparer.OrdinalIgnoreCase).ToArray();
        public void SelectMusic(string guid)
        {
            if (!Ready || busy) return;
            var entry=MusicEntries.FirstOrDefault(a => a.guid == guid);
            if (entry != null) StartCoroutine(SelectMusicAndPlay(entry));
        }
        IEnumerator SelectMusicAndPlay(ChillAssetEntry entry)
        { yield return Select(entry); if (music && music.clip) music.Play(); }
        public bool AssetGuidMatches(string name, string guid) => catalog != null && catalog.assets.Any(a => a.guid == guid && a.name == name);
        public void LoadExternal(string slot, string assetName, Action<UnityEngine.Object> complete)
        { StartCoroutine(LoadExternalRoutine(slot, assetName, complete)); }

        IEnumerator LoadExternalRoutine(string slot, string assetName, Action<UnityEngine.Object> complete)
        {
            while (!Ready || busy) yield return null;
            busy = true;
            if (!externalPacks.TryGetValue(slot, out var held)) externalPacks[slot] = held = new List<string>();
            Release(held);
            var entry = catalog.assets.FirstOrDefault(a => string.Equals(a.name, assetName, StringComparison.OrdinalIgnoreCase) || a.guid == assetName);
            if (entry == null) { busy = false; complete(null); yield break; }
            var root = metadata[entry.bundle];
            foreach (string name in root.dependencies.Concat(new[] { entry.bundle }).Distinct())
            {
                if (packs.TryGetValue(name, out var existing)) { existing.users++; held.Add(name); continue; }
                var info = metadata[name];
                using (var request = UnityWebRequestAssetBundle.GetAssetBundle(BaseUrl + name, Hash128.Parse(info.hash), info.crc))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    { status = request.error; Release(held); busy = false; complete(null); yield break; }
                    var bundle = DownloadHandlerAssetBundle.GetContent(request);
                    if (!bundle) { Release(held); busy = false; complete(null); yield break; }
                    packs[name] = new Pack { bundle = bundle, users = 1 }; held.Add(name);
                }
            }
            var load = packs[entry.bundle].bundle.LoadAssetAsync(entry.assetName);
            yield return load;
            busy = false; complete(load.asset);
        }

        public void ReleaseExternal(string slot)
        {
            // Never interrupt an in-flight load; each callback owns the loaded
            // slot until a later request or component teardown releases it.
            if (!busy && externalPacks.TryGetValue(slot, out var held)) Release(held);
        }
        ChillAssetCatalog catalog;
        Dictionary<string, ChillBundleEntry> metadata;
        ChillAssetEntry[] results = Array.Empty<ChillAssetEntry>();
        RuntimeAnimatorController originalController;
        AnimatorOverrideController overrideController;
        AudioSource voice;
        UnityEngine.Object inspected;
        GameObject previewRoot;
        Camera previewCamera;
        RenderTexture previewImage;
        string status = "Đang đọc danh sách asset…", search = "", category = "music", dataText = "";
        Vector2 listScroll, detailScroll;
        int page;
        bool busy;
        bool previewRenderable;
        readonly string[] categories = { "music", "voice", "sfx", "animation", "texture", "sprite", "mesh", "material", "prefab", "font", "data", "all" };
        string BaseUrl => Application.streamingAssetsPath.Substring(0, Application.streamingAssetsPath.LastIndexOf('/')) + "/assetpacks/";

        IEnumerator Start()
        {
            originalController = character.runtimeAnimatorController;
            voice = gameObject.AddComponent<AudioSource>(); voice.playOnAwake = false;
            using (var request = UnityWebRequest.Get(Application.streamingAssetsPath + "/chill-library.json"))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) { status = "Không tải được danh sách: " + request.error; yield break; }
                catalog = JsonUtility.FromJson<ChillAssetCatalog>(request.downloadHandler.text);
            }
            metadata = catalog.bundles.ToDictionary(b => b.name);
            status = catalog.assets.Length + " asset · chọn một mục để tải và xem";
            Filter();
        }

        void Filter()
        {
            if (catalog == null) return;
            results = catalog.assets.Where(a => (category == "all" || CategoryOf(a) == category) &&
                (string.IsNullOrEmpty(search) || a.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(a => a.name, StringComparer.OrdinalIgnoreCase).ToArray();
            page = 0; listScroll = Vector2.zero;
        }

        static string CategoryOf(ChillAssetEntry a)
        {
            switch (a.type)
            {
                case "AnimationClip": return "animation";
                case "Texture2D": case "Cubemap": case "RenderTexture": return "texture";
                case "Sprite": return "sprite";
                case "Mesh": return "mesh";
                case "Material": return "material";
                case "GameObject": return "prefab";
                case "Font": case "TMP_FontAsset": case "TMP_SpriteAsset": return "font";
                case "TextAsset": return "data";
                case "AudioClip": return a.category == "sound" ? "sfx" : a.category;
                default: return a.category;
            }
        }

        public void RestoreCharacter()
        {
            if (!overrideController) return;
            character.runtimeAnimatorController = originalController;
            Destroy(overrideController); overrideController = null;
            if (physics) physics.simulation = true;
        }

        void ClearPreview()
        {
            RestoreCharacter();
            if (voice) { voice.Stop(); voice.clip = null; }
            if (previewRoot) { previewRoot.SetActive(false); Destroy(previewRoot); previewRoot = null; }
            if (previewCamera) previewCamera.enabled = false;
            previewRenderable = false;
            inspected = null; dataText = "";
            Release(previewPacks);
        }

        void Release(List<string> held)
        {
            foreach (string name in held)
                if (packs.TryGetValue(name, out var pack) && --pack.users == 0)
                { pack.bundle.Unload(true); packs.Remove(name); }
            held.Clear();
        }

        IEnumerator Select(ChillAssetEntry entry)
        {
            if (busy) yield break;
            busy = true;
            bool isMusic = entry.category == "music";
            var held = isMusic ? musicPacks : previewPacks;
            if (isMusic) { music.Stop(); music.clip = null; Release(held); } else ClearPreview();
            if (!metadata.TryGetValue(entry.bundle, out var root)) { status = "Thiếu bundle: " + entry.bundle; busy = false; yield break; }
            foreach (string name in root.dependencies.Concat(new[] { entry.bundle }).Distinct())
            {
                if (packs.TryGetValue(name, out var existing)) { existing.users++; held.Add(name); continue; }
                var info = metadata[name];
                using (var request = UnityWebRequestAssetBundle.GetAssetBundle(BaseUrl + name, Hash128.Parse(info.hash), info.crc))
                {
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                    { status = "Tải " + entry.name + " · " + (request.downloadProgress * 100).ToString("F0") + "%"; yield return null; }
                    if (request.result != UnityWebRequest.Result.Success)
                    { status = "Không tải được " + name + ": " + request.error; Release(held); busy = false; yield break; }
                    var bundle = DownloadHandlerAssetBundle.GetContent(request);
                    if (!bundle) { status = "Bundle không hợp lệ: " + name; Release(held); busy = false; yield break; }
                    packs.Add(name, new Pack { bundle = bundle, users = 1 }); held.Add(name);
                }
            }
            var loading = packs[entry.bundle].bundle.LoadAssetAsync(entry.assetName);
            yield return loading;
            var asset = loading.asset;
            if (!asset) { status = "Không đọc được asset: " + entry.name; Release(held); busy = false; yield break; }
            status = entry.name + " · " + entry.type;
            if (isMusic)
            { music.clip = asset as AudioClip; music.loop = true; status += " · bấm Phát nhạc để nghe"; }
            else
            {
                inspected = asset;
                if (asset is AudioClip audio) { voice.clip = audio; status += " · bấm Phát âm thanh để nghe"; }
                else if (asset is AnimationClip clip)
                {
                    if (clip.humanMotion)
                    {
                        overrideController = new AnimatorOverrideController(originalController);
                        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                        overrideController.GetOverrides(overrides);
                        for (int i = 0; i < overrides.Count; i++) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, clip);
                        overrideController.ApplyOverrides(overrides);
                        if (physics) physics.simulation = false;
                        character.runtimeAnimatorController = overrideController;
                        character.Play("anim_work_book_base_loop", 0, 0);
                    }
                    else status += " · animation đồ vật; xem cùng prefab tương ứng";
                }
                else if (asset is TextAsset text) dataText = text.text.Length > 16000 ? text.text.Substring(0, 16000) + "\n…" : text.text;
                else if (asset is GameObject || asset is Mesh || asset is Material) ShowObject(asset);
            }
            busy = false;
        }

        void ShowObject(UnityEngine.Object asset)
        {
            if (asset is GameObject prefab) previewRoot = Instantiate(prefab);
            else
            {
                previewRoot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                if (asset is Mesh mesh) previewRoot.GetComponent<MeshFilter>().sharedMesh = mesh;
                previewRoot.GetComponent<Renderer>().sharedMaterial = asset as Material ? (Material)asset : previewMaterial;
                Destroy(previewRoot.GetComponent<Collider>());
            }
            previewRoot.name = "Asset preview"; previewRoot.transform.position = new Vector3(1000, 0, 0);
            foreach (var t in previewRoot.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
            foreach (var camera in previewRoot.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            // A recovered screen-space Canvas must not cover the inspection UI.
            foreach (var canvas in previewRoot.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
            foreach (var light in previewRoot.GetComponentsInChildren<Light>(true)) light.cullingMask = 1 << 30;
            foreach (var listener in previewRoot.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (var audio in previewRoot.GetComponentsInChildren<AudioSource>(true)) { audio.Stop(); audio.enabled = false; }
            if (!previewCamera)
            {
                previewCamera = new GameObject("Asset preview camera").AddComponent<Camera>();
                previewCamera.cullingMask = 1 << 30; previewCamera.clearFlags = CameraClearFlags.SolidColor;
                previewCamera.backgroundColor = new Color(.08f, .09f, .12f); previewCamera.fieldOfView = 35;
                previewImage = new RenderTexture(512, 384, 16);
                previewCamera.targetTexture = previewImage;
            }
            var renderers = previewRoot.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            if (renderers.Length == 0) { status += " · prefab UI hoặc không có mesh hoạt động"; return; }
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            float distance = Mathf.Max(.2f, bounds.extents.magnitude * 3.8f);
            previewCamera.transform.position = bounds.center + new Vector3(0, distance * .15f, -distance);
            previewCamera.transform.LookAt(bounds.center); previewCamera.farClipPlane = Mathf.Max(100, distance * 3);
            previewCamera.nearClipPlane = Mathf.Max(.001f, distance / 1000); previewCamera.enabled = true;
            previewRenderable = true;
        }

        void Update()
        {
            if (previewCamera) previewCamera.enabled = visible && previewRenderable;
            if (voice) voice.volume = music ? music.volume : .35f;
        }

        void OnGUI()
        {
            if (!visible) return;
            if (uiFont) GUI.skin.font = uiFont;
            float scale = Mathf.Clamp(Screen.width / 1000f, .65f, 1.5f);
            var previous = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            GUILayout.BeginArea(new Rect(280, 12, Mathf.Max(260, width - 292), Mathf.Max(220, height - 24)), GUI.skin.box);
            GUILayout.Label("Thư viện asset · bản thử độc lập");
            GUILayout.Label(status);
            string nextSearch = GUILayout.TextField(search);
            if (nextSearch != search) { search = nextSearch; Filter(); }
            int current = Array.IndexOf(categories, category);
            int next = GUILayout.SelectionGrid(current, categories, 6);
            if (next != current) { category = categories[next]; Filter(); }
            GUILayout.BeginHorizontal();
            GUI.enabled = page > 0;
            if (GUILayout.Button("←", GUILayout.Width(40))) { page--; listScroll = Vector2.zero; }
            GUI.enabled = true;
            GUILayout.Label(results.Length + " mục · trang " + (page + 1));
            GUI.enabled = (page + 1) * 40 < results.Length;
            if (GUILayout.Button("→", GUILayout.Width(40))) { page++; listScroll = Vector2.zero; }
            GUI.enabled = true; GUILayout.EndHorizontal();
            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(Mathf.Max(100, height * .32f)));
            GUI.enabled = !busy;
            foreach (var entry in results.Skip(page * 40).Take(40))
                if (GUILayout.Button(entry.name, GUI.skin.button)) StartCoroutine(Select(entry));
            GUI.enabled = true; GUILayout.EndScrollView();
            if (voice && voice.clip && GUILayout.Button(voice.isPlaying ? "Dừng âm thanh" : "Phát âm thanh"))
            { if (voice.isPlaying) voice.Stop(); else voice.Play(); }
            if (overrideController && GUILayout.Button("Trở lại animation bàn làm việc")) RestoreCharacter();
            if (inspected is Texture texture) GUILayout.Label(texture, GUILayout.Height(Mathf.Min(240, height * .28f)));
            else if (inspected is Sprite sprite)
            {
                Rect rect = GUILayoutUtility.GetRect(240, Mathf.Min(240, height * .28f));
                var r = sprite.textureRect; var t = sprite.texture;
                GUI.DrawTextureWithTexCoords(rect, t, new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height));
            }
            else if (previewRoot && previewCamera && previewCamera.enabled) GUILayout.Label(previewImage, GUILayout.Height(Mathf.Min(240, height * .28f)));
            if (!string.IsNullOrEmpty(dataText))
            { detailScroll = GUILayout.BeginScrollView(detailScroll); GUILayout.TextArea(dataText); GUILayout.EndScrollView(); }
            GUILayout.EndArea(); GUI.matrix = previous;
        }

        void OnDestroy()
        {
            ClearPreview();
            if (music) { music.Stop(); music.clip = null; }
            Release(musicPacks);
            foreach (var held in externalPacks.Values) Release(held);
            if (previewCamera) Destroy(previewCamera.gameObject);
            if (previewImage) { previewImage.Release(); Destroy(previewImage); }
        }
    }
}
