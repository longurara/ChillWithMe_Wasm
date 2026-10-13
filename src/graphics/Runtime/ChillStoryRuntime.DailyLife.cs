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
            if(save.timerRunning || save.countupRunning || !string.IsNullOrEmpty(save.chapter))return null;
            var available=data.groups.Where(g=>chapters.ContainsKey(g.ID) && !save.completed.Contains(g.ID) && Unlocked(g));
            if(Workspace.collaboration>0)return available.Where(g=>g.Scenario==49+Workspace.collaboration).OrderBy(g=>g.EpisodeNumber).FirstOrDefault();
            return available.Where(g=>g.Scenario==1 && g.EpisodeNumber>=save.mainEpisode && g.EpisodeNumber<=save.mainEpisode+1).OrderBy(g=>g.EpisodeNumber).FirstOrDefault()
                ?? available.Where(g=>g.Scenario==2 || g.Scenario==3).OrderBy(g=>g.UnlockLevel).ThenBy(g=>g.EpisodeNumber).FirstOrDefault();
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
            Begin(selected,false,false,false);return true;
        }
        IEnumerator FinishSnackActivity()
        {
            SetNativeMotion("Base001");yield return WaitNative();
            if(!Playing){player.deskObjects.HideSnacks();player.deskObjects.Select(player.SelectedState);SetNativeMotion(CurrentBaseMotion);}
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
