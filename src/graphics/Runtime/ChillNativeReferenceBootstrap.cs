#if UNITY_STANDALONE_WIN
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public sealed class ChillNativeReferenceBootstrap:MonoBehaviour
{
    void Start()
    {
        try
        {
            string managed=Environment.GetEnvironmentVariable("CHILL_PC_MANAGED");
            if(string.IsNullOrEmpty(managed))throw new InvalidOperationException("CHILL_PC_MANAGED must point to the original PC game's Managed folder");
            AppDomain.CurrentDomain.AssemblyResolve+=(sender,args)=>{
                string name=new AssemblyName(args.Name).Name;
                var loaded=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name==name);if(loaded!=null)return loaded;
                string file=Path.Combine(managed,name+".dll");return File.Exists(file)?Assembly.LoadFrom(file):null;
            };
            var native=Assembly.LoadFrom(Path.Combine(managed,"MagicaClothV2.dll"));var manager=native.GetType("MagicaCloth2.MagicaManager",true);
            manager.GetMethod("Initialize",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,null);
            manager.GetMethod("InitCustomGameLoop",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,null);
            gameObject.AddComponent<ChillReferenceRecorder>().Configure(native);
        }
        catch(Exception error){Debug.LogException(error);Application.Quit(1);}
    }
}
#endif
