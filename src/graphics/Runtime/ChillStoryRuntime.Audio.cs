using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        AudioSource uiSound,timerSound;
        int uiSoundVersion,timerSoundVersion;
        float uiCueGain=1,timerCueGain=1;
        readonly Dictionary<string,AudioClip> cueClips=new Dictionary<string,AudioClip>();
        readonly HashSet<string> cuePending=new HashSet<string>();
        public void UISelectSound(int id=2)=>PlayCue(false,id);
        void PlayCue(bool timer,int id,float multiplier=1)
        {
            if(!Application.isPlaying||VerificationOnly||!FeatureReady)return;
            var track=(timer?Features.timerSounds:Features.systemSounds)?.FirstOrDefault(t=>t.id==id);
            float gain=(timer?Settings.timer:Settings.sfx)*Mathf.Clamp01(multiplier);if(track==null||gain<=0||Settings.master<=0)return;
            var source=timer?timerSound:uiSound;
            if(timer)timerCueGain=multiplier;else uiCueGain=multiplier;
            if(!source){source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;if(timer)timerSound=source;else uiSound=source;}
            int token=timer?++timerSoundVersion:++uiSoundVersion;string key=(timer?"timer":"ui")+"-"+id;float requested=Time.realtimeSinceStartup;
            if(cueClips.TryGetValue(key,out var cached)&&cached){source.volume=gain;source.PlayOneShot(cached);return;}
            // A slider or repeated click must never enqueue the same bundle many times.
            if(!cuePending.Add(key))return;
            player.library.LoadExternal("cue-"+key,track.guid,asset=>{
                cuePending.Remove(key);if(!(asset is AudioClip clip))return;cueClips[key]=clip;
                if(token!=(timer?timerSoundVersion:uiSoundVersion)||Time.realtimeSinceStartup-requested>2)return;
                source.volume=(timer?Settings.timer:Settings.sfx)*Mathf.Clamp01(multiplier);source.PlayOneShot(clip);
            });
        }
    }
}
