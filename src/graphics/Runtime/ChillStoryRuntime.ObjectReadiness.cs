using System.Collections;
using System.Linq;
using UnityEngine;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        readonly ChillActivityObjects activityObjects=new ChillActivityObjects();
        bool musicboxActive;
        AudioSource musicboxAudio;
        int musicboxVersion;
        public void OnActivityObjectEvent(string name)
        {
            if(Replaying)return;
            if(name=="Drink")activityObjects.Drink();
            else if(name=="PourDrinksFromCoffeeMaker")activityObjects.Pour(Now,false);
            else if(name=="PlayWindowAnimationOpenWindow")activityObjects.Window(true,Now,ChillRuntimeContext.Value);
            else if(name=="PlayWindowAnimationCloseWindow")activityObjects.Window(false,Now,ChillRuntimeContext.Value);
            else if(name=="PlayMusicbox")StartMusicbox();
        }
        string ReadyObjectMotion()
        {
            if(Resting&&Workspace.musicboxCountdown<=0&&Now>=activityObjects.nextMusicbox&&!musicboxActive&&player.deskObjects.musicboxAnimator&&player.deskObjects.musicboxAnimator.gameObject.activeInHierarchy)return "Wild001_Motion25_Memorie";
            if(Now>=activityObjects.nextWindow&&player.deskObjects.windowAnimator&&player.deskObjects.windowAnimator.gameObject.activeInHierarchy)return player.deskObjects.WindowOpen?"Wild001_Motion9_CloseWindow":"Wild001_Motion8_OpenWindow";
            return null;
        }
        IEnumerator ObjectAction(string motion)
        {
            if(motion.Contains("Memorie")){musicboxActive=true;player.deskObjects.Action("WindingMusicbox");activityObjects.Musicbox(Now,ChillRuntimeContext.Value);}
            yield return Variant(motion);
        }
        void StartMusicbox()
        {
            if(!musicboxActive||!Application.isPlaying||VerificationOnly)return;
            Workspace.musicboxCountdown=2;Workspace.musicboxUpdatedDate=DateKey(ChillRuntimeContext.LocalNow);SaveNow();
            int version=++musicboxVersion;
            if(!musicboxAudio){musicboxAudio=gameObject.AddComponent<AudioSource>();musicboxAudio.playOnAwake=false;}
            var track=Features?.musicboxSound;
            if(string.IsNullOrEmpty(track)){CloseMusicbox();return;}
            player.library.LoadExternal("activity-musicbox",track,asset=>{if(version!=musicboxVersion)return;if(asset is AudioClip clip){musicboxAudio.clip=clip;musicboxAudio.volume=Settings.sfx;musicboxAudio.Play();StartCoroutine(FinishMusicbox(version));}else CloseMusicbox();});
        }
        IEnumerator FinishMusicbox(int version)
        {yield return null;while(version==musicboxVersion&&musicboxAudio&&musicboxAudio.isPlaying&&!Playing&&Resting){musicboxAudio.volume=Settings.sfx;yield return null;}if(version==musicboxVersion)CloseMusicbox();}
        void CloseMusicbox()
        {
            musicboxVersion++;musicboxActive=false;player.music.volume=Settings.music;if(musicboxAudio){musicboxAudio.Stop();musicboxAudio.clip=null;}
            var animator=player.deskObjects.musicboxAnimator;if(animator){animator.enabled=true;animator.speed=1;animator.Play("EndMemorie",0,0);}
            player.library.ReleaseExternal("activity-musicbox");
        }
        void UpdateMusicboxCalendar()
        {
            var today=DateKey(ChillRuntimeContext.LocalNow);
            if(Workspace.musicboxCountdown>0&&Workspace.musicboxUpdatedDate!=today){Workspace.musicboxCountdown--;Workspace.musicboxUpdatedDate=today;SaveNow();}
        }
    }
}
