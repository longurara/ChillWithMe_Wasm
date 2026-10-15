using System.Linq;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        bool skippingTimer;
        int appliedRoomDefect=-1;
        GameObject connectionLostOverlay;
        public bool ConnectionLost=>save.mainEpisode>=31&&save.mainEpisode<32;
        public bool IsStorySpecialLocked=>!save.previewUnlock&&save.mainEpisode>=30&&save.mainEpisode<32;
        public bool IsFacilityLocked=>IsStorySpecialLocked||newYearCountdownReady||Playing&&chapter!=null&&(chapter.Scenario==22||chapter.Scenario==101||chapter.Scenario==102||chapter.Scenario==103);
        public bool IsAlwaysNoise=>appliedRoomDefect==1;
        bool defectAudioEffectActive;
        public bool DefectAudioEffectActive => defectAudioEffectActive;
        readonly System.Collections.Generic.Dictionary<string,AudioSource> effectNoiseSources=new System.Collections.Generic.Dictionary<string,AudioSource>();
        public void UpdateEffectNoise(float glitchVal,float monochromeVal,float shakeVal,float blackoutVal)
        {
            if(VerificationOnly||Replaying||data==null)return;
            bool allow=!IsAlwaysNoise&&Playing;
            SetEffectNoiseChannel("GlitchNoise",allow&&glitchVal>0.001f);
            SetEffectNoiseChannel("MonochoromeNoise",allow&&monochromeVal>0.001f);
            SetEffectNoiseChannel("ScreenMove",allow&&shakeVal>0.001f);
            SetEffectNoiseChannel("BlackOut",allow&&blackoutVal>0.001f);
        }
        void SetEffectNoiseChannel(string clipTitle,bool active)
        {
            if(!active)
            {
                if(effectNoiseSources.TryGetValue(clipTitle,out var s)&&s&&s.isPlaying)s.Stop();
                return;
            }
            if(!effectNoiseSources.TryGetValue(clipTitle,out var src)||!src)
            {
                src=gameObject.AddComponent<AudioSource>();
                src.playOnAwake=false;src.loop=true;src.spatialBlend=0f;
                effectNoiseSources[clipTitle]=src;
            }
            src.volume=.25f*Settings.sfx;
            if(src.clip){if(!src.isPlaying)src.Play();return;}
            var alias=data.music.FirstOrDefault(m=>m.title==clipTitle);
            if(alias==null||string.IsNullOrEmpty(alias.clip))return;
            player.library.LoadExternal("effect-noise-"+clipTitle,alias.clip,asset=>{
                if(asset is AudioClip clip&&src){src.clip=clip;src.volume=.25f*Settings.sfx;if(Playing&&!IsAlwaysNoise)src.Play();}
            });
        }
        void SetDefectAudioEffect(bool enable)
        {
            defectAudioEffectActive = enable;
            var listener = FindObjectOfType<AudioListener>();
            if (!listener) return;
            var filter = listener.GetComponent<AudioLowPassFilter>();
            if (enable && !filter) filter = listener.gameObject.AddComponent<AudioLowPassFilter>();
            if (filter)
            {
                filter.cutoffFrequency = 1800f;
                filter.lowpassResonanceQ = 1.2f;
                filter.enabled = enable;
            }
        }
        void ClearRoomDefect()
        {
            if(appliedRoomDefect<1)return;
            appliedRoomDefect=-1;if(connectionLostOverlay)connectionLostOverlay.SetActive(false);
            if(player.character)player.character.speed=1;
            SetDefectAudioEffect(false);
            effects.ResetEffects();
        }
        void UpdateRoomDefect()
        {
            if(Playing||StartupBlocking)return;
            int mode=ConnectionLost?2:save.mainEpisode>=30&&save.mainEpisode<31?1:0;
            if(appliedRoomDefect!=mode)
            {
                ClearRoomDefect();appliedRoomDefect=mode;
                if(mode==1){effects.Apply(new ChillSentence{Command=219,Arg1="0.17",Arg2=0});effects.Apply(new ChillSentence{Command=215,Arg1="0.15",Arg2=0});SetDefectAudioEffect(true);}
                if(mode==2)
                {
                    ResetAutonomy();
                    if(!connectionLostOverlay)connectionLostOverlay=FindObjectsOfType<Transform>(true).FirstOrDefault(t=>t.name=="ConnectionLost")?.gameObject;
                    if(connectionLostOverlay)connectionLostOverlay.SetActive(true);
                    SetNativeMotion("Base001");player.character.speed=0;
                }
            }
            if(mode==2&&save.connectionLostNextEpisode&&save.level>=33)
            {
                var next=data.groups.FirstOrDefault(g=>g.Scenario==1&&g.EpisodeNumber==32);
                if(next!=null)Begin(next,false,false,false);
            }
        }
    }
}
