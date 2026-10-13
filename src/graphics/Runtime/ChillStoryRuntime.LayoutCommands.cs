using System.Collections;
using System.Linq;

namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        IEnumerator ChangeStoryLayout(string name,bool persist)
        {
            var skin=decorations.Skins.FirstOrDefault(s=>s.name==name);
            if(skin!=null)
            {
                yield return decorations.Apply(name);
                if(persist&&chapter?.Scenario==1&&chapter.EpisodeNumber>=save.mainEpisode+1&&skin.id>=7&&skin.id<=11&&!ItemAvailable("decoration","Book_5"))
                {
                    Workspace.room.RemoveAll(r=>r.category==skin.category);Workspace.room.Add(new ChillRoomChoice{category=skin.category,skin=name});SaveNow();
                }
                yield break;
            }
            int desk=name=="Book"?0:name=="Report"?1:name=="Pc"?2:-1;
            if(desk>=0)player.SetStoryDesk(desk);
        }
    }
}
