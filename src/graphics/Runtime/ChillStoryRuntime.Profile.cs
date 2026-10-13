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
        void DailyAppearance()
        {
            if(!Workspace.appearanceDaily||Workspace.lastAppearanceDate==DateKey(ChillRuntimeContext.LocalNow))return;
            Workspace.lastAppearanceDate=DateKey(ChillRuntimeContext.LocalNow);Workspace.hair=ChillRuntimeContext.Range(0,4);var weights=Features.dailyCostumes;float total=0;foreach(var item in weights)total+=item.weights[ChillRuntimeContext.LocalNow.Month-1];float draw=ChillRuntimeContext.Value*total;Workspace.costume=0;foreach(var item in weights){draw-=item.weights[ChillRuntimeContext.LocalNow.Month-1];if(draw<0){Workspace.costume=item.id;break;}}WorkspaceChanged();RestoreRoom();
        }
    }
}
