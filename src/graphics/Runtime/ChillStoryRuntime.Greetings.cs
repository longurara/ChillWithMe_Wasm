using System;
using System.Collections.Generic;
using System.Linq;

namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        int GreetingScenario(double previousVisit)
        {
            if(previousVisit<=0)return 11;
            var previous=DateTimeOffset.FromUnixTimeMilliseconds((long)(previousVisit*1000)).LocalDateTime;
            var now=ChillRuntimeContext.LocalNow;int days=Math.Max(0,(int)(now-previous).TotalDays);
            bool camera=days>=1||previous.Date!=now.Date;
            if(!camera)return 13;
            if(Settings.totalLoginCount>0&&Settings.totalLoginCount%100==0)return 17;
            if(days<=2)return Chance(35)?1400+2*TimeOfDayIndex():12;
            return days<=14?14:days<=30?15:16;
        }
        bool StartGreeting(double previousVisit)
        {
            if(!FeatureReady||StartupBlocking||ConnectionLost||Playing||autonomousSequence!=null||ActivityTransitionPending)return false;
            int scenario=GreetingScenario(previousVisit);
            var list=data.groups.Where(g=>g.Scenario==scenario&&chapters.ContainsKey(g.ID)&&Unlocked(g)).OrderBy(g=>g.EpisodeNumber).ToArray();
            if(list.Length==0)return false;
            if(Settings.greetingBags==null)Settings.greetingBags=new List<ChillGreetingBag>();
            var bag=Settings.greetingBags.FirstOrDefault(b=>b.scenario==scenario);
            if(bag==null){bag=new ChillGreetingBag{scenario=scenario};Settings.greetingBags.Add(bag);}
            bag.episodes.RemoveAll(n=>!list.Any(g=>(int)g.EpisodeNumber==n));
            if(bag.episodes.Count==0){bag.episodes=list.Select(g=>(int)g.EpisodeNumber).OrderBy(_=>ChillRuntimeContext.Value).ToList();if(bag.episodes.Count>1&&bag.episodes[0]==bag.lastTail){bag.episodes.RemoveAt(0);bag.episodes.Add(bag.lastTail);}bag.lastTail=bag.episodes[bag.episodes.Count-1];}
            int episode=bag.episodes[0];bag.episodes.RemoveAt(0);
            var selected=list.First(g=>(int)g.EpisodeNumber==episode);
            suspendedChapter=save.chapter;suspendedCursor=save.cursor;suspendedScene=new List<int>(save.sceneHistory);suspendedStoryLive=save.storyLive;
            Begin(selected,false,true);return true;
        }
        void RecordLogin(double previousVisit)
        {
            if(previousVisit<=0||DateTimeOffset.FromUnixTimeMilliseconds((long)(previousVisit*1000)).LocalDateTime.Date!=ChillRuntimeContext.LocalNow.Date)Settings.totalLoginCount=Math.Min(1000000,Settings.totalLoginCount+1);
        }
    }
}
