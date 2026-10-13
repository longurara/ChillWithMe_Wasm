using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public static class ChillToonWeb
    {
        const BindingFlags Methods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        [Serializable] class Row { public string shader; public string[] keywords, textures; public int textureCount; }
        [Serializable] class Profile { public string shader, file; public string[] features, materials; }
        [Serializable] class Profiles { public Profile[] profiles; }
        [Serializable] class Report { public int budget = 16; public string[] enabledFeatures; public Row[] variants; }
        static string[] enabledFeatures;
        static List<Profile> profiles;
        static void Normalize(lilToonSetting setting)
        {
            var dependencies = new Dictionary<string, string[]> {
                { "MAIN2ND", new[] { "Main2nd" } }, { "MAIN3RD", new[] { "Main3rd" } },
                { "ALPHAMASK", new[] { "AlphaMask" } }, { "NORMAL_1ST", new[] { "BumpMap" } },
                { "NORMAL_2ND", new[] { "Bump2nd" } }, { "SHADOW", new[] { "Shadow" } },
                { "RIMSHADE", new[] { "RimShade" } }, { "BACKLIGHT", new[] { "Backlight" } },
                { "REFLECTION", new[] { "Smoothness", "Metallic", "Reflection" } },
                { "MATCAP", new[] { "MatCapTex", "MatCapBlend", "MatCapBump" } },
                { "MATCAP_2ND", new[] { "MatCap2nd" } }, { "RIMLIGHT", new[] { "RimColor" } },
                { "EMISSION_1ST", new[] { "EmissionMap", "EmissionBlend", "EmissionGrad" } },
                { "EMISSION_2ND", new[] { "Emission2nd" } }
            };
            var type = typeof(lilToonSetting);
            foreach (var pair in dependencies)
                if (!(bool)type.GetField("LIL_FEATURE_" + pair.Key).GetValue(setting))
                    foreach (var field in type.GetFields())
                        if (field.FieldType == typeof(bool) && pair.Value.Any(prefix => field.Name.StartsWith("LIL_FEATURE_" + prefix))) field.SetValue(setting, false);
        }

        static int BlockEnd(string source, int start)
        {
            int depth = 0;
            foreach (Match token in Regex.Matches(source.Substring(start), @"//[^\n]*|/\*[\s\S]*?\*/|""(?:\\.|[^""\\])*""|[{}]"))
            {
                if (token.Value == "{") depth++;
                else if (token.Value == "}" && --depth == 0) return start + token.Index + 1;
            }
            throw new InvalidOperationException("Unbalanced toon shader pass.");
        }

        public static void GenerateProfiles()
        {
            const string folder = "Assets/Prototype/ToonShaders";
            Directory.CreateDirectory(folder);
            profiles = new List<Profile>();
            var groups = new Dictionary<string, List<Material>>();
            var setup = typeof(lilToonSetting).GetMethod("SetupShaderSettingFromMaterial", Methods);
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Recovered" }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (!material.shader || !material.shader.name.Contains("lilToon")) continue;
                string kind = material.shader.name.Contains("Transparent") ? "Transparent" : material.shader.name.Contains("Cutout") ? "Cutout" : "Opaque";
                var setting = ScriptableObject.CreateInstance<lilToonSetting>();
                lilToonSetting.TurnOffAllShaderSetting(ref setting);
                object[] args = { material, setting }; setup.Invoke(null, args); setting = (lilToonSetting)args[1];
                Normalize(setting);
                string defines = lilToonSetting.BuildShaderSettingString(setting, false);
                defines = Regex.Replace(defines, @"^\s*#pragma lil_skip_variants_[^\r\n]*", "", RegexOptions.Multiline);
                string hash;
                using (var md5 = MD5.Create()) hash = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(kind + defines))).Replace("-", "").ToLowerInvariant();
                string shaderName = "HLC/lilToon/" + kind + "/" + hash;
                if (!groups.TryGetValue(shaderName, out var materials))
                {
                    groups[shaderName] = materials = new List<Material>();
                    string path = folder + "/" + kind + "-" + hash + ".shader";
                    string source = File.ReadAllText("Assets/lilToon/Shader/ltspass_" + kind.ToLowerInvariant() + ".shader");
                    source = Regex.Replace(source, @"^Shader\s+""[^""]+""", "Shader \"" + shaderName + "\"", RegexOptions.Multiline);
                    source = Regex.Replace(source, @"HLSLINCLUDE(?:(?!ENDHLSL)[\s\S])*?(?=\s*#define LIL_SRP_VERSION_MAJOR)", "HLSLINCLUDE\n" + defines);
                    source = source.Replace("#include \"Includes/", "#include \"Assets/lilToon/Shader/Includes/");
                    source = Regex.Replace(source, @"SubShader\s*\{\s*Tags\s*\{\}", "SubShader\n{\n Tags { \"RenderType\"=\"" + (kind == "Transparent" ? "Transparent" : kind == "Cutout" ? "TransparentCutout" : "Opaque") + "\" \"Queue\"=\"" + (kind == "Transparent" ? "Transparent" : kind == "Cutout" ? "AlphaTest" : "Geometry") + "\" }");
                    // These materials use the original wrappers without outline.
                    var outline = Regex.Match(source, @"Pass\s*\{\s*Name\s+""FORWARD_OUTLINE""");
                    if (outline.Success) source = source.Remove(outline.Index, BlockEnd(source, source.IndexOf('{', outline.Index)) - outline.Index);
                    source = Regex.Replace(source, @"Fallback\s+""[^""]+""", "Fallback Off");
                    if (!File.Exists(path) || File.ReadAllText(path) != source) File.WriteAllText(path, source);
                    var featureNames = typeof(lilToonSetting).GetFields().Where(f => f.FieldType == typeof(bool) && f.Name.StartsWith("LIL_FEATURE_") && (bool)f.GetValue(setting)).Select(f => f.Name).ToArray();
                    profiles.Add(new Profile { shader = shaderName, file = path, features = featureNames });
                }
                materials.Add(material); UnityEngine.Object.DestroyImmediate(setting);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var profile in profiles)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(profile.file);
                profile.materials = groups[profile.shader].Select(AssetDatabase.GetAssetPath).ToArray();
                foreach (var material in groups[profile.shader])
                { int queue = material.renderQueue; material.shader = shader; material.renderQueue = queue; EditorUtility.SetDirty(material); }
            }
            AssetDatabase.SaveAssets();
            if (profiles.Count == 0) throw new InvalidOperationException("No toon materials resolved; texture budget validation must not be empty.");
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../research/toon-web-profiles.json")), JsonUtility.ToJson(new Profiles { profiles = profiles.ToArray() }, true));
            Debug.Log("HLC_CHILL_TOON_PROFILES count=" + profiles.Count + " materials=" + groups.Values.Sum(m => m.Count));
        }

        public static void Configure(ref lilToonSetting setting)
        {
            // Feature pruning keeps the original shader behavior and values.
            // Do not enable every optional texture branch in the vendor defaults.
            lilToonSetting.TurnOffAllShaderSetting(ref setting);
            var type = typeof(lilToonSetting);
            var setupMaterial = type.GetMethod("SetupShaderSettingFromMaterial", Methods);
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Recovered" }))
            {
                object[] args = { AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)), setting };
                setupMaterial.Invoke(null, args); setting = (lilToonSetting)args[1];
            }
            var setupClip = type.GetMethod("SetupShaderSettingFromAnimationClip", Methods);
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Recovered" }))
            {
                object[] args = { AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid)), setting, true };
                setupClip.Invoke(null, args); setting = (lilToonSetting)args[1];
            }
            var profile = setting;
            enabledFeatures = type.GetFields().Where(f => f.FieldType == typeof(bool) && f.Name.StartsWith("LIL_FEATURE_") && (bool)f.GetValue(profile)).Select(f => f.Name).ToArray();
            Debug.Log("HLC_CHILL_TOON_FEATURES " + string.Join(",", enabledFeatures));
        }

        public static void CheckAdditionalLightBudget()
        {
            var stored=JsonUtility.FromJson<Profiles>(File.ReadAllText(Path.GetFullPath("../research/toon-web-profiles.json")));
            var rows=new List<Row>();
            foreach(var profile in stored.profiles)
            {
                var shader=AssetDatabase.LoadAssetAtPath<Shader>(profile.file);var subshader=ShaderUtil.GetShaderData(shader).GetSubshader(0);
                var pass=Enumerable.Range(0,subshader.PassCount).Select(subshader.GetPass).First(p=>p.Name=="FORWARD");
                var keywords=new[]{"_ADDITIONAL_LIGHTS","_MAIN_LIGHT_SHADOWS","_SHADOWS_SOFT"};
                foreach(var stage in new[]{ShaderType.Vertex,ShaderType.Fragment})
                {var compiled=pass.CompileVariant(stage,keywords,ShaderCompilerPlatform.GLES3x,BuildTarget.WebGL);
                 if(!compiled.Success)throw new InvalidOperationException("GLES3 additional lights failed: "+shader.name+" "+string.Join("; ",compiled.Messages.Select(m=>m.message)));
                 var textures=compiled.TextureBindings==null?Array.Empty<string>():compiled.TextureBindings.Select(t=>t.Name).Distinct().ToArray();
                 rows.Add(new Row{shader=shader.name+" ["+stage+"]",keywords=keywords,textures=textures,textureCount=textures.Length});}
            }
            File.WriteAllText(Path.GetFullPath("../research/toon-additional-light-budget.json"),JsonUtility.ToJson(new Report{variants=rows.ToArray()},true));
            if(rows.Any(r=>r.textureCount>16))throw new InvalidOperationException("Additional light variant exceeds WebGL texture budget");
            Debug.Log("HLC_ADDITIONAL_LIGHT_GLES_PASSED variants="+rows.Count+" maxTextures="+rows.Max(r=>r.textureCount));
        }
        public static void CheckTextureBudget()
        {
            var rows = new List<Row>();
            foreach (var profile in profiles)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(profile.file);
                var subshader = ShaderUtil.GetShaderData(shader).GetSubshader(0);
                var pass = Enumerable.Range(0, subshader.PassCount).Select(subshader.GetPass).First(p => p.Name == "FORWARD");
                foreach (var keywords in new[] { Array.Empty<string>(), new[] { "_MAIN_LIGHT_SHADOWS", "_SHADOWS_SOFT" }, new[] { "_MAIN_LIGHT_SHADOWS_CASCADE", "_SHADOWS_SOFT" } })
                {
                    var compiled = pass.CompileVariant(ShaderType.Vertex, keywords, ShaderCompilerPlatform.GLES3x, BuildTarget.WebGL);
                    if (!compiled.Success) throw new InvalidOperationException("GLES3 toon compilation failed: " + shader.name + " " + string.Join("; ", compiled.Messages.Select(m => m.message)));
                    if (compiled.TextureBindings == null) throw new InvalidOperationException("No texture binding reflection for " + shader.name);
                    var textures = compiled.TextureBindings.Select(t => t.Name).Distinct().ToArray();
                    rows.Add(new Row { shader = shader.name, keywords = keywords, textures = textures, textureCount = textures.Length });
                    Debug.Log("HLC_CHILL_TOON_TEXTURES " + shader.name + " count=" + textures.Length + " " + string.Join(",", textures));
                }
            }
            var report = new Report { enabledFeatures = enabledFeatures, variants = rows.ToArray() };
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../research/toon-web-texture-budget.json"));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            if (rows.Any(r => r.textureCount > report.budget)) throw new InvalidOperationException("Toon texture budget exceeded; see " + path);
        }
    }
}
