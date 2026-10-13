using System;
using System.Linq;
using UnityEngine;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        double workSessionStart,lastWorkEnd,sessionWorkBase,timerTotalBase,pendingTimerUntil;
        int pendingTimerScenario;
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
            if(Workspace.reminders&&phase!="start")ChillBrowser.Notify("Chill with You",phase=="break"?"Time for a break.":"Your work session is complete.");
            if(!TimerAudio(countup).voice)return;
            float Value(string key,float fallback)=>Policy("PomodoroVoiceData."+key,fallback);
            bool Roll(string key,float fallback)=>Chance(Value(key,fallback));
            int scenario;
            if(phase=="start")
            {
                if(countup||save.rounds==0)timerTotalBase=save.workedSeconds;workSessionStart=Now;sessionWorkBase=save.workedSeconds;scenario=countup?3000:4;
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
            pendingTimerScenario=scenario;pendingTimerUntil=Now+20;UpdateTimerVoice();
        }
        void UpdateTimerVoice()
        {if(pendingTimerScenario==0)return;if(Now>pendingTimerUntil){pendingTimerScenario=0;return;}if(!Playing&&Application.isPlaying&&!VerificationOnly&&StartReaction(pendingTimerScenario))pendingTimerScenario=0;}
    }
}
