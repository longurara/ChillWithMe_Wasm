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
        void ClearRoomDefect()
        {
            if(appliedRoomDefect<1)return;
            appliedRoomDefect=-1;if(connectionLostOverlay)connectionLostOverlay.SetActive(false);
            if(player.character)player.character.speed=1;
            effects.ResetEffects();
        }
        void UpdateRoomDefect()
        {
            if(Playing||StartupBlocking)return;
            int mode=ConnectionLost?2:save.mainEpisode>=30&&save.mainEpisode<31?1:0;
            if(appliedRoomDefect!=mode)
            {
                ClearRoomDefect();appliedRoomDefect=mode;
                if(mode==1){effects.Apply(new ChillSentence{Command=219,Arg1="0.17",Arg2=0});effects.Apply(new ChillSentence{Command=215,Arg1="0.15",Arg2=0});}
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
