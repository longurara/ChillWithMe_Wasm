using System;
using UnityEngine;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        bool farewellPending;
        int settledPlayerStatus;
        double statusReactionAt=-1,lastStatusReaction=-1;
        public void SelectProfileIcon(int index){Workspace.profileIcon="preset:"+index;WorkspaceChanged();}
        public void ProfileIconImported(string data)
        {if(!data.StartsWith("data:image/png;base64,")||data.Length>90000){Notify("Avatar image is too large.",true);return;}Workspace.profileIcon=data;WorkspaceChanged();}
        public void SelectPlayerStatus(int status)
        {
            int previous=int.TryParse(Workspace.profileStatus,out var value)?value:0;
            status=Mathf.Clamp(status,0,2);Workspace.profileStatus=status.ToString();WorkspaceChanged();
            if(previous==status)return;
            statusReactionAt=Now+Delay("PlayerStatusReactionData.DelaySecondsMin","PlayerStatusReactionData.DelaySecondsMax",2,3);
        }
        void InitializeStatusReaction()
        {settledPlayerStatus=int.TryParse(Workspace.profileStatus,out var status)?status:0;statusReactionAt=-1;}
        void TimerUserActivity(){Workspace.profileStatus="0";settledPlayerStatus=0;statusReactionAt=-1;WorkspaceRevision++;}
        void UpdateStatusReaction()
        {
            if(statusReactionAt<0||Now<statusReactionAt)return;
            statusReactionAt=-1;
            int status=int.TryParse(Workspace.profileStatus,out var value)?value:0,previous=settledPlayerStatus;settledPlayerStatus=status;
            if(status==previous||ConnectionLost||Playing||ActivityTransitionPending||autonomousSequence!=null||WantsToTalk||breakMode==4&&AutonomyState=="BreakBase005_Sleep"||
                lastStatusReaction>=0&&Now-lastStatusReaction<Policy("PlayerStatusReactionData.CooldownSeconds",5))return;
            bool leave=status==1&&previous!=1,comeback=previous==1&&status!=1;
            if(!leave&&!comeback)return;
            int scenario=leave?(Chance(Policy("PlayerStatusReactionData.CommonProbabilityPercent",50))?2700:Resting?2701:2702):
                Chance(Policy("PlayerStatusReactionData.CommonProbabilityPercent",50))?2750:Resting?2751:2752;
            if(StartReaction(scenario))lastStatusReaction=Now;
        }
        public static string AppearanceDateKey(DateTime localNow)=>DateKey(localNow.Hour<6?localNow.Date.AddDays(-1):localNow.Date);
        public int SelectDailyCostume(int month)
        {
            var weights=Features?.dailyCostumes;
            if(weights==null||weights.Length==0)return 0;
            int m=Mathf.Clamp(month-1,0,11);
            if(Workspace.costumeWeights==null)Workspace.costumeWeights=new System.Collections.Generic.List<ChillCostumeWeight>();
            ChillCostumeWeight Entry(int id)
            {
                string skin="Costume_"+(id+1);
                var e=Workspace.costumeWeights.Find(w=>w.skin==skin);
                if(e==null){e=new ChillCostumeWeight{skin=skin,weight=100f};Workspace.costumeWeights.Add(e);}
                return e;
            }
            float total=0f;
            foreach(var item in weights)total+=item.weights[m]*Entry(item.id).weight;
            int selected=0;
            if(total>0f)
            {
                float draw=ChillRuntimeContext.Value*total;
                foreach(var item in weights)
                {
                    draw-=item.weights[m]*Entry(item.id).weight;
                    if(draw<0f){selected=item.id;break;}
                }
            }
            foreach(var item in weights)
            {
                var e=Entry(item.id);
                if(item.id==selected)e.weight*=0.5f;
                else if(item.weights[m]>0f)e.weight+=50f;
            }
            return selected;
        }
        public void DailyAppearance()
        {
            var localNow=ChillRuntimeContext.LocalNow;
            string dateKey=AppearanceDateKey(localNow);
            if(!Workspace.appearanceDaily||Workspace.lastAppearanceDate==dateKey)return;
            Workspace.lastAppearanceDate=dateKey;
            bool forceNormalHair=save.workedSeconds<3600||save.mainEpisode>=31&&save.mainEpisode<32;
            Workspace.hair=forceNormalHair?0:ChillRuntimeContext.Range(0,5)==4?ChillRuntimeContext.Range(1,4):0;
            bool forceDefaultCostume=!save.previewUnlock&&save.mainEpisode<4;
            Workspace.costume=forceDefaultCostume?0:SelectDailyCostume(localNow.Month);
            WorkspaceChanged();if(!VerificationOnly)RestoreRoom();
        }
    }
}
