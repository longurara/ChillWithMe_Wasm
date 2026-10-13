using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Local reference tooling only. The PC plugin is loaded from the user's game;
// it is neither copied into WebGL nor distributed with the repository.
public static class ChillNativePhysicsReference
{
    static string Managed=>Environment.GetEnvironmentVariable("CHILL_PC_MANAGED")??throw new InvalidOperationException("Set CHILL_PC_MANAGED to your game's Managed folder");
    static Assembly native;
    static Assembly Resolve(object sender,ResolveEventArgs args)
    {
        string name=new AssemblyName(args.Name).Name;
        var loaded=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name==name);if(loaded!=null)return loaded;
        string file=Path.Combine(Managed,name+".dll");return File.Exists(file)?Assembly.LoadFrom(file):null;
    }
    public static void Inspect()
    {
        AppDomain.CurrentDomain.AssemblyResolve+=Resolve;
        native=Assembly.LoadFrom(Path.Combine(Managed,"MagicaClothV2.dll"));
        using(var output=new StreamWriter("../.tools/native-physics-api.txt"))
        foreach(string name in new[]{"MagicaCloth","MagicaManager","ClothSerializeData","MagicaCapsuleCollider","MagicaSphereCollider"})
        {
            var type=native.GetType("MagicaCloth2."+name,true);output.WriteLine(type.FullName);
            foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static))output.WriteLine("FIELD "+field.Name+" "+field.FieldType.FullName);
            foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly))output.WriteLine("METHOD "+method);
        }
        Debug.Log("HLC_NATIVE_PHYSICS_REFERENCE_API_LOADED");
    }
    public static void Capture()
    {
        throw new InvalidOperationException("The release plugin is incompatible with Editor safety-check containers. Use capture-native-physics.ps1 and the isolated Windows player.");
    }
    public static void BuildPlayer()
    {
        EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");
        foreach(var behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))behaviour.enabled=false;
        var bootstrap=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("ChillNativeReferenceBootstrap")).FirstOrDefault(t=>t!=null);
        if(bootstrap==null)throw new InvalidOperationException("Run this build with -buildTarget StandaloneWindows64");
        new GameObject("Local native physics reference").AddComponent(bootstrap);
        string scene="Assets/Prototype/NativeReference.unity";EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),scene);
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x);
        Directory.CreateDirectory("../.tools/native-reference-player");
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scene},locationPathName="../.tools/native-reference-player/Reference.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Native reference player build failed");
        Debug.Log("HLC_NATIVE_REFERENCE_PLAYER_BUILT");
    }
}
