using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

namespace Hlc.ChillPrototype
{
    public sealed partial class ChillLibraryBrowser
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void ChillStartupWaitPreload(string receiver);
        [DllImport("__Internal")] static extern void ChillStartupMountProgress(int done, int total);
        [DllImport("__Internal")] static extern void ChillStartupSetPreloadMode(string mode);
#endif
        readonly Dictionary<string,int> slotVersions=new Dictionary<string,int>();
        readonly Dictionary<string,UnityWebRequest> slotRequests=new Dictionary<string,UnityWebRequest>();
        readonly HashSet<string> activeSlots=new HashSet<string>();
        readonly HashSet<string> downloadingBundles=new HashSet<string>();
        public int PendingAssetLoads {get;private set;}
        public int AssetRetries {get;private set;}
        bool stoppingLoads,preloadChoiceReceived;

        public void OnPreloadReady(string mode)
        {
            PreloadMode=string.IsNullOrEmpty(mode)?"core":mode;
            preloadChoiceReceived=true;
        }

        public void ChangePreloadMode(string mode)
        {
            PreloadMode=string.IsNullOrEmpty(mode)?"core":mode;
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillStartupSetPreloadMode(PreloadMode);
#endif
            if(PreloadMode!="live"&&metadata!=null)StartCoroutine(MountCoreBundlesRoutine(false));
        }

        IEnumerator WaitAndMountPreload()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            preloadChoiceReceived=false;
            ChillStartupWaitPreload(gameObject.name);
            while(!preloadChoiceReceived&&!stoppingLoads)yield return null;
#else
            PreloadMode="core";
#endif
            if(PreloadMode!="live")yield return MountCoreBundlesRoutine(true);
        }

        IEnumerator MountCoreBundlesRoutine(bool reportToStartup)
        {
            if(catalog?.bundles==null)yield break;
            var coreNames=catalog.bundles.Select(b=>b.name).Where(IsCoreBundle)
                .OrderBy(n=>n.StartsWith("texture2d/")||n.StartsWith("sprite/")||n.StartsWith("rendertexture/")||n.StartsWith("mesh/")?0:1)
                .ToArray();
            int total=coreNames.Length,done=0,inFlight=0;
            const int MaxConcurrency=4;
            for(int i=0;i<total&&!stoppingLoads;i++)
            {
                string bundleName=coreNames[i];
                if(packs.TryGetValue(bundleName,out var existing)&&existing.bundle)
                {
                    existing.pinned=true;done++;
#if UNITY_WEBGL && !UNITY_EDITOR
                    if(reportToStartup)ChillStartupMountProgress(done,total);
#endif
                    continue;
                }
                while(inFlight>=MaxConcurrency&&!stoppingLoads)yield return null;
                if(stoppingLoads)yield break;
                inFlight++;
                StartCoroutine(WarmSingleBundle(bundleName,()=>{
                    inFlight--;done++;
#if UNITY_WEBGL && !UNITY_EDITOR
                    if(reportToStartup)ChillStartupMountProgress(done,total);
#endif
                }));
            }
            while(inFlight>0&&!stoppingLoads)yield return null;
        }

        IEnumerator WarmSingleBundle(string name,Action onDone)
        {
            try
            {
                while(downloadingBundles.Contains(name)&&!stoppingLoads)yield return null;
                if(stoppingLoads)yield break;
                if(packs.TryGetValue(name,out var existing)&&existing.bundle)
                {
                    existing.pinned=true;
                    yield break;
                }
                if(!metadata.TryGetValue(name,out var info))yield break;
                downloadingBundles.Add(name);
                try
                {
                    using(var request=UnityWebRequestAssetBundle.GetAssetBundle(BaseUrl+name,Hash128.Parse(info.hash),info.crc))
                    {
                        request.timeout=60;
                        yield return request.SendWebRequest();
                        if(request.result==UnityWebRequest.Result.Success)
                        {
                            AssetBundle bundle=null;
                            try{bundle=DownloadHandlerAssetBundle.GetContent(request);}catch{}
                            if(bundle)packs[name]=new Pack{bundle=bundle,users=1,pinned=true};
                        }
                    }
                }
                finally{downloadingBundles.Remove(name);}
            }
            finally{onDone?.Invoke();}
        }

        int AdvanceSlot(string slot)
        {
            slotVersions.TryGetValue(slot,out int version);slotVersions[slot]=++version;
            if(slotRequests.TryGetValue(slot,out var request))request.Abort();
            return version;
        }
        bool SlotCurrent(string slot,int version)=>!stoppingLoads&&slotVersions.TryGetValue(slot,out int current)&&current==version;
        public void LoadExternal(string slot,string assetName,Action<UnityEngine.Object> complete)
        {
            if(stoppingLoads||string.IsNullOrEmpty(slot)){complete?.Invoke(null);return;}
            if(Ready&&!InitializationFailed)
            {
                var entry=FindAssetEntry(assetName);
                if(entry!=null&&metadata.TryGetValue(entry.bundle,out var root))
                {
                    bool allMounted=packs.TryGetValue(entry.bundle,out var mainPack)&&mainPack.bundle;
                    if(allMounted&&root.dependencies!=null)
                    {
                        for(int i=0;i<root.dependencies.Length;i++)
                            if(!packs.TryGetValue(root.dependencies[i],out var depPack)||!depPack.bundle){allMounted=false;break;}
                    }
                    if(allMounted)
                    {
                        AdvanceSlot(slot);
                        externalPacks.TryGetValue(slot,out var previous);
                        var held=new List<string>();
                        if(root.dependencies!=null)
                        {
                            for(int i=0;i<root.dependencies.Length;i++)
                            {
                                string dep=root.dependencies[i];
                                if(packs.TryGetValue(dep,out var dp)){if(!dp.pinned)dp.users++;held.Add(dep);}
                            }
                        }
                        if(!mainPack.pinned)mainPack.users++;
                        held.Add(entry.bundle);
                        externalPacks[slot]=held;
                        if(previous!=null)Release(previous);
                        if(!pinnedAssetCache.TryGetValue(entry.guid,out var fastAsset)||!fastAsset)
                        {
                            fastAsset=mainPack.bundle.LoadAsset(entry.assetName);
                            if(mainPack.pinned&&fastAsset)pinnedAssetCache[entry.guid]=fastAsset;
                        }
                        try{complete?.Invoke(fastAsset);}catch(Exception error){Debug.LogException(error);}
                        return;
                    }
                }
            }
            StartCoroutine(LoadExternalRoutine(slot,assetName,complete,AdvanceSlot(slot)));
        }
        public void ReleaseExternal(string slot)
        {
            AdvanceSlot(slot);
            if(!activeSlots.Contains(slot)&&externalPacks.TryGetValue(slot,out var held)){Release(held);externalPacks.Remove(slot);}
        }
        IEnumerator DownloadPack(string name,string slot,int version,Action<AssetBundle> complete)
        {
            if(!metadata.TryGetValue(name,out var info)){complete(null);yield break;}
            for(int attempt=0;attempt<3&&SlotCurrent(slot,version);attempt++)
            {
                AssetBundle bundle=null;
                using(var request=UnityWebRequestAssetBundle.GetAssetBundle(BaseUrl+name,Hash128.Parse(info.hash),info.crc))
                {
                    request.timeout=60;slotRequests[slot]=request;
                    yield return request.SendWebRequest();
                    if(slotRequests.TryGetValue(slot,out var active)&&active==request)slotRequests.Remove(slot);
                    if(!SlotCurrent(slot,version))yield break;
                    if(request.result==UnityWebRequest.Result.Success)
                    {
                        try{bundle=DownloadHandlerAssetBundle.GetContent(request);}
                        catch(Exception error){status=error.Message;}
                    }
                    else status=request.error;
                    // A missing asset or corrupt catalog cannot be repaired by retrying.
                    if(request.responseCode>=400&&request.responseCode<500&&request.responseCode!=408&&request.responseCode!=429){complete(null);yield break;}
                }
                if(bundle){complete(bundle);yield break;}
                if(attempt<2){AssetRetries++;float until=Time.realtimeSinceStartup+Mathf.Pow(2,attempt);
                    while(SlotCurrent(slot,version)&&Time.realtimeSinceStartup<until)yield return null;}
            }
            complete(null);
        }
        IEnumerator LoadExternalRoutine(string slot,string assetName,Action<UnityEngine.Object> complete,int version)
        {
            PendingAssetLoads++;bool acquired=false,success=false;var held=new List<string>();List<string> previous=null;UnityEngine.Object result=null;
            try
            {
                while(!Ready&&!InitializationFailed&&SlotCurrent(slot,version))yield return null;
                if(!Ready||InitializationFailed||!SlotCurrent(slot,version))yield break;
                acquired=true;activeSlots.Add(slot);
                externalPacks.TryGetValue(slot,out previous);
                externalPacks[slot]=held;
                var entry=FindAssetEntry(assetName);
                if(entry==null||!metadata.TryGetValue(entry.bundle,out var root))yield break;
                foreach(string name in root.dependencies.Concat(new[]{entry.bundle}).Distinct())
                {
                    while(downloadingBundles.Contains(name)&&SlotCurrent(slot,version))yield return null;
                    if(!SlotCurrent(slot,version))yield break;
                    if(packs.TryGetValue(name,out var existing)&&existing.bundle){if(!existing.pinned)existing.users++;held.Add(name);continue;}
                    AssetBundle bundle=null;
                    downloadingBundles.Add(name);
                    try{yield return DownloadPack(name,slot,version,value=>bundle=value);}
                    finally{downloadingBundles.Remove(name);}
                    if(!bundle||!SlotCurrent(slot,version)){if(bundle&&!packs.ContainsKey(name))bundle.Unload(true);yield break;}
                    bool pin=IsCoreBundle(name);
                    packs[name]=new Pack{bundle=bundle,users=1,pinned=pin};held.Add(name);
                }
                var mainPack=packs[entry.bundle];
                if(mainPack.pinned&&pinnedAssetCache.TryGetValue(entry.guid,out var cachedObj)&&cachedObj)
                {
                    result=cachedObj;success=true;
                }
                else
                {
                    var load=mainPack.bundle.LoadAssetAsync(entry.assetName);
                    yield return load;
                    if(!SlotCurrent(slot,version))yield break;
                    result=load.asset;success=result;
                    if(mainPack.pinned&&result)pinnedAssetCache[entry.guid]=result;
                }
            }
            finally
            {
                if(!success){Release(held);if(externalPacks.TryGetValue(slot,out var owned)&&owned==held)externalPacks.Remove(slot);}
                if(previous!=null)Release(previous);
                if(acquired){activeSlots.Remove(slot);}
                PendingAssetLoads--;
                try{complete?.Invoke(result);}catch(Exception error){Debug.LogException(error);}
            }
        }
        void CancelAssetLoads()
        {
            stoppingLoads=true;
            foreach(var request in slotRequests.Values.ToArray())request.Abort();
            StopAllCoroutines();slotRequests.Clear();downloadingBundles.Clear();busy=false;PendingAssetLoads=0;
        }
    }
}
