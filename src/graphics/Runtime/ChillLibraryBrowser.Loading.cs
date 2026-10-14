using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Hlc.ChillPrototype
{
    public sealed partial class ChillLibraryBrowser
    {
        readonly Dictionary<string,int> slotVersions=new Dictionary<string,int>();
        readonly Dictionary<string,UnityWebRequest> slotRequests=new Dictionary<string,UnityWebRequest>();
        readonly HashSet<string> activeSlots=new HashSet<string>();
        public int PendingAssetLoads {get;private set;}
        public int AssetRetries {get;private set;}
        bool stoppingLoads;
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
                    request.timeout=30;slotRequests[slot]=request;
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
            PendingAssetLoads++;bool acquired=false,success=false;var held=new List<string>();UnityEngine.Object result=null;
            try
            {
                float deadline=Time.realtimeSinceStartup+120;
                while((!Ready||busy)&&!InitializationFailed&&SlotCurrent(slot,version)&&Time.realtimeSinceStartup<deadline)yield return null;
                if(!Ready||busy||InitializationFailed||!SlotCurrent(slot,version))yield break;
                busy=true;acquired=true;activeSlots.Add(slot);
                if(externalPacks.TryGetValue(slot,out var previous))Release(previous);
                externalPacks[slot]=held;
                var entry=catalog.assets.FirstOrDefault(a=>string.Equals(a.name,assetName,StringComparison.OrdinalIgnoreCase)||a.guid==assetName);
                if(entry==null||!metadata.TryGetValue(entry.bundle,out var root))yield break;
                foreach(string name in root.dependencies.Concat(new[]{entry.bundle}).Distinct())
                {
                    if(!SlotCurrent(slot,version))yield break;
                    if(packs.TryGetValue(name,out var existing)){existing.users++;held.Add(name);continue;}
                    AssetBundle bundle=null;yield return DownloadPack(name,slot,version,value=>bundle=value);
                    if(!bundle||!SlotCurrent(slot,version)){if(bundle)bundle.Unload(true);yield break;}
                    packs[name]=new Pack{bundle=bundle,users=1};held.Add(name);
                }
                var load=packs[entry.bundle].bundle.LoadAssetAsync(entry.assetName);
                yield return load;
                if(!SlotCurrent(slot,version))yield break;
                result=load.asset;success=result;
            }
            finally
            {
                if(!success){Release(held);if(externalPacks.TryGetValue(slot,out var owned)&&owned==held)externalPacks.Remove(slot);}
                if(acquired){busy=false;activeSlots.Remove(slot);}
                PendingAssetLoads--;
                try{complete?.Invoke(result);}catch(Exception error){Debug.LogException(error);}
            }
        }
        void CancelAssetLoads()
        {
            stoppingLoads=true;
            foreach(var request in slotRequests.Values.ToArray())request.Abort();
            StopAllCoroutines();slotRequests.Clear();busy=false;PendingAssetLoads=0;
        }
    }
}
