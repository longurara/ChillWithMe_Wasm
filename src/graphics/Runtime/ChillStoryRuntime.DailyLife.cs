using System.Collections;
using System.Linq;
using UnityEngine;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        ChillChapter invitedChapter;
        public bool WantsToTalk=>invitedChapter!=null;
        ChillChapter NextInvitedChapter()
        {
            if(!string.IsNullOrEmpty(save.chapter))return null;
            var available=data.groups.Where(g=>chapters.ContainsKey(g.ID) && !save.completed.Contains(g.ID) && Unlocked(g));
            if(!save.timerRunning&&!save.countupRunning)
            {
                var main=Workspace.collaboration>0?available.Where(g=>g.Scenario==49+Workspace.collaboration).OrderBy(g=>g.EpisodeNumber).FirstOrDefault():available.Where(g=>g.Scenario==1&&g.EpisodeNumber>=save.mainEpisode&&g.EpisodeNumber<=save.mainEpisode+1).OrderBy(g=>g.EpisodeNumber).FirstOrDefault();
                if(main!=null)return main;
                if(Workspace.collaboration==0&&!(save.mainEpisode>=30&&save.mainEpisode<32)){var extra=available.FirstOrDefault(g=>g.Scenario==24);if(extra!=null)return extra;}
            }
            if(save.needsTutorial||ConnectionLost)return null;
            var date=ChillRuntimeContext.LocalNow;
            int scenario=SeasonalEvent==102?102:BirthdayMatches(date,Settings.birthdayMonth,Settings.birthdayDay)?103:0;
            return scenario==0?null:available.FirstOrDefault(g=>g.Scenario==scenario&&g.EpisodeNumber==1);
        }
        bool InviteNextChapter()
        {
            invitedChapter=NextInvitedChapter();if(invitedChapter==null)return false;
            SetNativeMotion("WantTalk_Base_001");return true;
        }
        bool AcceptStoryInvitation()
        {
            if(invitedChapter==null)return false;
            var selected=NextInvitedChapter();invitedChapter=null;
            if(selected==null){SetNativeMotion(CurrentBaseMotion);return true;}
            if(selected.Scenario==102||selected.Scenario==103){suspendedChapter=save.chapter;suspendedCursor=save.cursor;suspendedScene=new System.Collections.Generic.List<int>(save.sceneHistory);suspendedStoryLive=save.storyLive;Begin(selected,false,true,false);}
            else Begin(selected,false,false,false);return true;
        }
        IEnumerator FinishSnackActivity()
        {
            SetNativeMotion("Base001");yield return WaitNative();
            if(!Playing){player.deskObjects.HideSnacks();player.deskObjects.SelectSmooth(player.SelectedState,.25f);SetNativeMotion(CurrentBaseMotion);}
            autonomousSequence=null;
        }
        IEnumerator SofaStay(float seconds)
        {
            const string group="HeroineStateLeaveChairGoSofa";
            double end=Now+seconds;
            weightGroup="";SetWeightGroup(group);
            nextVariant=Now+Delay(group+"._baseDelaySecondsMin",group+"._baseDelaySecondsMax",5,10);
            bool variation=false;
            while(!Playing && Now<end)
            {
                if(Now>=nextVariant && presentation.MotionCompleted)
                {
                    nextVariant=Now+Delay(group+"._baseDelaySecondsMin",group+"._baseDelaySecondsMax",5,10);
                    if(variation){SetNativeMotion("Wild003_LeaveChair_Sofa");variation=false;}
                    else
                    {
                        var motion=PickWeightedVariant(group);
                        if(!string.IsNullOrEmpty(motion)){SetNativeMotion(motion);variation=true;}
                    }
                }
                yield return null;
            }
            weightGroup="";
        }
    }
}
