using System;
using UnityEngine;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        bool farewellPending;
        public void SelectProfileIcon(int index){Workspace.profileIcon="preset:"+index;WorkspaceChanged();}
        public void ProfileIconImported(string data)
        {if(!data.StartsWith("data:image/png;base64,")||data.Length>90000){Notify("Avatar image is too large.",true);return;}Workspace.profileIcon=data;WorkspaceChanged();}
        public void SelectPlayerStatus(int status)
        {
            int previous=int.TryParse(Workspace.profileStatus,out var value)?value:0;
            status=Mathf.Clamp(status,0,2);Workspace.profileStatus=status.ToString();WorkspaceChanged();
            if(previous==status||Playing)return;
            if(status==1)StartReaction(Chance(Policy("PlayerStatusReactionData.CommonProbabilityPercent",50))?2700:Resting?2701:2702);
            else if(previous==1)StartReaction(Chance(Policy("PlayerStatusReactionData.CommonProbabilityPercent",50))?2750:Resting?2751:2752);
        }
        void DailyAppearance()
        {
            if(!Workspace.appearanceDaily||Workspace.lastAppearanceDate==DateKey(DateTime.Now))return;
            Workspace.lastAppearanceDate=DateKey(DateTime.Now);Workspace.hair=UnityEngine.Random.Range(0,4);var weights=Features.dailyCostumes;float total=0;foreach(var item in weights)total+=item.weights[DateTime.Now.Month-1];float draw=UnityEngine.Random.value*total;Workspace.costume=0;foreach(var item in weights){draw-=item.weights[DateTime.Now.Month-1];if(draw<0){Workspace.costume=item.id;break;}}WorkspaceChanged();RestoreRoom();
        }
    }
}
