using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        double aprilShineStarted=-1;
        int SeasonalEvent
        {
            get
            {
                if(save.needsTutorial||save.mainEpisode==31)return 0;
                var date=ChillRuntimeContext.LocalNow.Date;
                if(date==new DateTime(2025,12,25))return 100;
                if(date==new DateTime(2026,4,1)&&save.mainEpisode>=3)return 102;
                return 0;
            }
        }
        void UpdateSeasonalPresentation()
        {
            if(!decorations)return;
            int season=SeasonalEvent;
            bool wearingSunglasses=Workspace.room.Any(r=>r.category==3&&r.skin=="Glasses_4");
            bool christmas=season==100&&!Playing,april=season==102&&(!Playing||chapter?.Scenario==102);
            bool shine=(april&&(WantsToTalk||!save.completed.Contains("event_2026_aprilfool_001")))||(wearingSunglasses&&WantsToTalk);
            if(!shine)aprilShineStarted=-1;else if(aprilShineStarted<0)aprilShineStarted=Now+2.5;
            float alpha=0;
            if(shine&&Now>=aprilShineStarted){float time=(float)(Now-aprilShineStarted)%5.3f;alpha=time<2?1-Mathf.Pow(1-time/2,2):time<2.5f?1:time<3.3f?Mathf.Pow(1-(time-2.5f)/.8f,2):0;}
            decorations.SeasonalObjects(christmas,april,alpha,wearingSunglasses);
        }
        IEnumerator ApplySeasonalDecorations()
        {
            int season=SeasonalEvent;
            if(season==100)
            {
                var headphone=Workspace.room.FirstOrDefault(r=>r.category==2);
                if(headphone!=null&&headphone.skin.StartsWith("Headphone_2",StringComparison.Ordinal))yield return decorations.Apply("Headphone_1");
            }
            if(season==102)yield return decorations.Apply("Grasses_2");
            UpdateSeasonalPresentation();
        }
    }
}
