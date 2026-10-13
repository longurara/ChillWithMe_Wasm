using System.Collections;
using System.Linq;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        bool waitingForScript,scriptReady,pomodoroVoiceReady;
        int pomodoroVoiceType=-1;
        int storyMusicVersion;
        public bool WaitingForScript=>waitingForScript;

        // RoomGameManager advances command 50 after the character click controller
        // becomes ready. A generic Next/Auto action is not that controller signal.
        public void ReadNextForScript()
        {if(waitingForScript && current!=null && current.Command==50)scriptReady=true;}

        IEnumerator WaitForScript()
        {
            waitingForScript=true;scriptReady=false;tutorialCommand=50;next=false;
            message="";shownChars=0;typing=false;
            SetNativeMotion("WantTalk_Base_001");
            while(Playing && !scriptReady)yield return null;
            waitingForScript=false;scriptReady=false;tutorialCommand=0;next=false;
            if(Playing)Motion(current);
        }

        // Recovered clips emit PlayPomodoroVoice(type) at the intended voice cue.
        // Ignore events from other commands or an unrelated voice type.
        public void OnPomodoroVoiceEvent(int type)
        {if(current!=null && current.Command==22 && pomodoroVoiceType==type)pomodoroVoiceReady=true;}

        void PreparePomodoroVoice(ChillSentence row)
        {
            pomodoroVoiceType=(int)Number(row.Arg1);
            pomodoroVoiceReady=row.BodyMotion==-1 || pomodoroVoiceType==0;
            Motion(row);
        }

        IEnumerator SpeakPomodoro(ChillSentence row)
        {
            if(row.Arg2!=-1)
            {
                PreparePomodoroVoice(row);double deadline=Now+30;
                while(Playing && !pomodoroVoiceReady && Now<deadline)yield return null;
                if(!Playing || !pomodoroVoiceReady)
                {pomodoroVoiceType=-1;pomodoroVoiceReady=false;Notify("Timer voice animation cue timed out.",true);yield break;}
            }
            else
            {
                // Source Arg2=-1 speaks before changing body motion.
                Motion(new ChillSentence{BodyMotion=-1,FacialMotion=row.FacialMotion,LookScale=row.LookScale,LookSpeedSeconds=row.LookSpeedSeconds,LookEaseType=row.LookEaseType});
            }
            yield return Speak(row,false,false);
            if(row.Arg2==-1 && Playing)Motion(row);
            if(row.BodyMotion!=-1 && Playing)yield return WaitForMotion();
            pomodoroVoiceType=-1;pomodoroVoiceReady=false;
        }

        bool AppliesStoryMusic(ChillSentence row)=>row.Command==5 || row.Command==25 && talkLog;

        IEnumerator PlayPlaylistMusic(ChillSentence row)
        {
            var track=Tracks.FirstOrDefault(t=>t.title==row.Arg1 && !t.personal);
            if(track==null){Notify("Story playlist track is missing: "+row.Arg1,true);yield break;}
            int token=generation,request=++musicToken;bool done=false;MusicLoading=true;
            player.library.LoadExternal("player-music",track.guid,asset=>{
                if(token==generation && request==musicToken)
                {
                    if(asset is AudioClip clip)
                    {
                        MusicState.selected=track.guid;MusicState.position=0;wantsMusic=true;
                        FinishTrack(clip,0);
                        // MusicService selection survives the end of the scene.
                        previousMusic=clip;previousMusicPlaying=true;previousMusicLoop=MusicState.repeat;previousMusicTime=0;
                    }
                    else MusicFailed("Story playlist track could not be loaded.");
                }
                done=true;
            });
            while(!done && token==generation && request==musicToken)yield return null;
            if(request==musicToken)MusicLoading=false;
        }

        float StoryMusicGain(ChillSentence row)=>.35f*(row.Arg2==0?1:Mathf.Clamp01(row.Arg2));
        float StoryMusicStart(ChillSentence row,float length)=>Mathf.Clamp(Number(row.Arg3),0,Mathf.Max(0,length-.05f));
        void RestoreMusicPosition(ChillSentence row)
        {if(StoryMusic.clip)StoryMusic.timeSamples=Mathf.RoundToInt(StoryMusicStart(row,StoryMusic.clip.length)*StoryMusic.clip.frequency);}

        IEnumerator ChangeStoryMusicVolume(ChillSentence row)
        {
            var alias=data.music.FirstOrDefault(m=>m.title==row.Arg1);
            if(alias==null || !StoryMusic.clip || StoryMusic.clip.name!=alias.clip)yield break;
            int version=++storyMusicVersion;
            yield return FadeStoryMusic(cachedStoryMusic,.35f*Mathf.Clamp01(row.Arg2),Mathf.Max(0,Number(row.Arg3)),generation,version);
        }

        IEnumerator PlayStoryMusic(ChillSentence row)
        {
            int token=generation,version=++storyMusicVersion;
            float target=StoryMusicGain(row);
            if(StoryMusic.isPlaying)yield return FadeStoryMusic(cachedStoryMusic,0,1.5f,token,version);
            if(token!=generation || version!=storyMusicVersion)yield break;
            yield return LoadAudio("story-music",row.Arg1,StoryMusic,chapter!=null && chapter.ID!="main_32",0,version);
            if(token!=generation || version!=storyMusicVersion || !StoryMusic.clip)yield break;
            RestoreMusicPosition(row);
            yield return FadeStoryMusic(0,target,Number(row.Arg4)>0?Number(row.Arg4):1.5f,token,version);
        }

        IEnumerator FadeStoryMusic(float from,float to,float seconds,int token,int version)
        {
            float started=Time.realtimeSinceStartup;
            while(token==generation && version==storyMusicVersion)
            {
                float t=Mathf.Clamp01((Time.realtimeSinceStartup-started)/Mathf.Max(.01f,seconds));
                cachedStoryMusic=Mathf.Lerp(from,to,t);StoryMusic.volume=Mathf.Clamp01(cachedStoryMusic*(Settings.music/.35f));
                if(t>=1)yield break;yield return null;
            }
        }

        void StopStoryMusic(ChillSentence row)
        {
            if(!string.IsNullOrEmpty(row.Arg1)&&StoryMusic.clip&&StoryMusic.clip.name!=row.Arg1)return;
            storyMusicVersion++;StoryMusic.Stop();StoryMusic.clip=null;player.library.ReleaseExternal("story-music");
        }

        void ResetCommandWaits()
        {
            loopChoiceVersion++;
            waitingForScript=scriptReady=pomodoroVoiceReady=false;pomodoroVoiceType=-1;storyMusicVersion++;
            if(Playing && MusicLoading){musicToken++;MusicLoading=false;}
        }
    }
}
