using System;
using System.Linq;
using UnityEngine;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        double workSessionStart,lastWorkEnd,sessionWorkBase,timerTotalBase,pendingTimerUntil,choiceTalkUntil;
        int pendingTimerScenario;
        float choiceTalkProbability;
        public bool ChoiceTalkAvailable=>Now<choiceTalkUntil&&choiceTalkProbability>0f;
        public float ChoiceTalkProbability=>ChoiceTalkAvailable?choiceTalkProbability:0f;
        public void EnableChoiceTalk(double workedSeconds)
        {
            float maxProb=Policy("ChoiceTalkData.maxTalkProbability",50f);
            float maxMin=Mathf.Max(1f,Policy("ChoiceTalkData.maxWorkedMinutes",60f));
            float workedMin=(float)(Math.Max(0.0,workedSeconds)/60.0);
            choiceTalkProbability=workedMin>maxMin?maxProb:(workedMin/maxMin*maxProb);
            choiceTalkUntil=Now+Policy("ChoiceTalkData.enableChoiceTalkDurationSeconds",300f);
        }
        public void ResetChoiceTalk(){choiceTalkUntil=0;choiceTalkProbability=0f;}
        public bool TryChoiceTalk()
        {
            if(!ChoiceTalkAvailable)return false;
            float prob=choiceTalkProbability;
            ResetChoiceTalk();
            return Chance(prob)&&StartReaction(7);
        }
        public ChillTimerAudio TimerAudio(bool countup)=>countup?Workspace.countupAudio:Workspace.pomodoroAudio;
        public void SaveTimerAudio()=>SaveNow();
        public void PreviewTimerSound(bool countup,bool finish)
        {var option=TimerAudio(countup);PlayCue(true,finish?option.finish:option.start,finish?option.finishVolume:option.startVolume);}
        void TimerSound(int id)
        {
            var option=TimerAudio(save.timerMode==1);
            if(id==0){if(!option.startMute)PlayCue(true,option.start,option.startVolume);}
            else if(id==3){if(!option.finishMute)PlayCue(true,option.finish,option.finishVolume);}
            else PlayCue(true,id);
        }
        void TimerVoice(string phase,bool countup,bool midway=false)
        {
            if(!FeatureReady)return;
            if(phase=="start")
            {ResetChoiceTalk();if(countup||save.rounds==0)timerTotalBase=save.workedSeconds;workSessionStart=Now;sessionWorkBase=save.workedSeconds;}
            if(phase=="break")EnableChoiceTalk(Math.Max(0,save.workedSeconds-sessionWorkBase));
            if(phase=="finish")
            {
                EnableChoiceTalk(Math.Max(0,save.workedSeconds-sessionWorkBase));
                save.timerComplete=true;
                if(save.level>=33&&save.mainEpisode<32&&!save.connectionLostNextEpisode)
                {save.lastStoryUnlockWorkSeconds+=Math.Max(0,save.workedSeconds-timerTotalBase);if(save.lastStoryUnlockWorkSeconds>=50*60)save.connectionLostNextEpisode=true;}
                if(ConnectionLost&&save.level>=33&&!midway&&!skippingTimer)save.connectionLostNextEpisode=true;
            }
            if(Workspace.reminders&&phase!="start")ChillBrowser.Notify("Chill with You",phase=="break"?"Time for a break.":"Your work session is complete.");
            if(!TimerAudio(countup).voice)return;
            float Value(string key,float fallback)=>Policy("PomodoroVoiceData."+key,fallback);
            bool Roll(string key,float fallback)=>Chance(Value(key,fallback));
            int scenario;
            if(phase=="start")
            {
                scenario=countup?3000:4;
                if(!countup&&save.workMinutes>=Value("longWorkStartVoiceData.judgeLongMinutes",120)&&Roll("longWorkStartVoiceData.selectionProbability",60))scenario=1001;
                else if(lastWorkEnd>0&&Now-lastWorkEnd<=Value("continuousWorkStartVoiceData.continuousMinutes",5)*60&&Roll("continuousWorkStartVoiceData.selectionProbability",90))scenario=countup?3001:1000;
            }
            else if(phase=="break")
            {
                double minutes=(save.workedSeconds-sessionWorkBase)/60;scenario=countup?3100:5;lastWorkEnd=Now;
                if(minutes<=Value("shortWorkedBreakStartVoiceData._shortWorkMinutes",10)&&Roll("shortWorkedBreakStartVoiceData.selectionProbability",20))scenario=countup?3101:1100;
                else if(minutes>=Value("longWorkedBreakStartVoiceData.judgeLongMinutes",50)&&Roll("longWorkedBreakStartVoiceData.selectionProbability",20))scenario=countup?3102:1101;
            }
            else
            {
                double minutes=(save.workedSeconds-timerTotalBase)/60;scenario=countup?3200:6;lastWorkEnd=Now;
                if(minutes>=Value("longWorkFinishVoiceData.judgeLongMinutes",240)&&Roll("longWorkFinishVoiceData.selectionProbability",60))scenario=countup?3202:1200;
                else if(minutes<=Value("shortWorkFinishVoiceData.judgeShortMinutes",40)&&Roll("shortWorkFinishVoiceData.selectionProbability",20))scenario=countup?3201:1201;
                else if(!countup&&midway&&Roll("midwayFinishVoiceData.selectionProbability",50))scenario=1202;
            }
            pendingTimerScenario=scenario;pendingTimerUntil=Now+30;UpdateTimerVoice();
        }
        void UpdateTimerVoice()
        {if(pendingTimerScenario==0)return;if(Now>pendingTimerUntil){pendingTimerScenario=0;return;}if(!Playing&&Application.isPlaying&&!VerificationOnly&&StartReaction(pendingTimerScenario))pendingTimerScenario=0;}
    }
}
