using System.Linq;

namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        double lastSmallTalk=-1000000;
        string previousSmallTalk="";
        ChillChapter startupSmallTalk;
        ChillChapter LotterySmallTalk(bool gameStart)
        {
            if(save.mainEpisode>=30&&save.mainEpisode<32||Now-lastSmallTalk<(data.parameters.SmallTalkData?.intervalMinutes??10)*60)return null;
            float draw=ChillRuntimeContext.Value*100;
            var candidates=data.groups.Where(g=>g.Scenario==23&&g.ID!=previousSmallTalk&&chapters.ContainsKey(g.ID)&&Unlocked(g)&&
                (gameStart?g.PlayTiming=="GameStart"||g.PlayTiming=="GameStartAndBreak":g.PlayTiming=="Break"||g.PlayTiming=="GameStartAndBreak"));
            candidates=candidates.Where(g=>g.ContentType!="SmallTalk_CatHeadphone"||(gameStart?!save.completed.Contains(g.ID):save.completed.Contains(g.ID)&&Workspace.room.Any(r=>r.category==2&&r.skin=="Headphone_2")));
            candidates=candidates.Where(g=>g.ContentType!="SmallTalk_Cicada"||save.workedSeconds>=Number(g.Arg3)*3600);
            candidates=candidates.Where(g=>g.ContentType!="SmallTalk_PassageOfTime"||Now-player.environment.LastChangeTime>=Number(g.Arg3)*3600);
            var unseen=candidates.Where(g=>!save.completed.Contains(g.ID)&&Number(g.Arg1)>=draw).ToArray();
            var seen=candidates.Where(g=>save.completed.Contains(g.ID)&&Number(g.Arg2)>=draw).ToArray();
            var list=unseen.Length>0?unseen:seen;
            return list.Length==0?null:list[ChillRuntimeContext.Range(0,list.Length)];
        }
        bool TrySmallTalk()
        {
            var selected=LotterySmallTalk(false);if(selected==null)return false;
            BeginSmallTalk(selected);return true;
        }
        void BeginSmallTalk(ChillChapter selected)
        {
            lastSmallTalk=Now;previousSmallTalk=selected.ID;
            suspendedChapter=save.chapter;suspendedCursor=save.cursor;suspendedScene=new System.Collections.Generic.List<int>(save.sceneHistory);suspendedStoryLive=save.storyLive;
            Begin(selected,false,true);
        }
    }
}
