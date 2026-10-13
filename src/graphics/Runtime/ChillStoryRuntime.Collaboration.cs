using System;
using System.Linq;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        ChillCollaboration CurrentCollaboration=>Features?.collaborations?.FirstOrDefault(c=>c.scenario==Workspace.collaboration+49);
        int CollaborationLevel(int scenario){var config=Features?.collaborations?.FirstOrDefault(c=>c.scenario==scenario);if(config==null)return 1;double exp=Workspace.collaborationExp[scenario-50];int level=1;foreach(var threshold in config.thresholds){if(exp<threshold)break;exp-=threshold;level++;}return level;}
        public int DisplayLevel=>Workspace.collaboration>0?CollaborationLevel(Workspace.collaboration+49):save.level;
        public double DisplayExp {get{var c=CurrentCollaboration;if(c==null)return save.exp;double exp=Workspace.collaborationExp[c.scenario-50];foreach(var threshold in c.thresholds){if(exp<threshold)break;exp-=threshold;}return exp;}}
        public float DisplayRequiredExp {get{var c=CurrentCollaboration;if(c==null)return data!=null?RequiredExp():1;int level=DisplayLevel;return level<=c.thresholds.Length?c.thresholds[level-1]:1;}}
        void CreditCollaboration(double exp){var c=CurrentCollaboration;if(c==null)return;int previous=DisplayLevel;var w=Workspace;w.collaborationExp[c.scenario-50]=Math.Min(c.thresholds.Sum(v=>(double)v),w.collaborationExp[c.scenario-50]+exp);if(DisplayLevel>previous){UISelectSound(3);Notify(c.name+": episode unlocked");WorkspaceRevision++;}}
        bool CollaborationUnlocked(ChillChapter chapter)
        {if(chapter.EpisodeNumber>CollaborationLevel(chapter.Scenario))return false;if(chapter.EpisodeNumber<=1)return true;return data.groups.Where(c=>c.Scenario==chapter.Scenario&&c.EpisodeNumber<chapter.EpisodeNumber).All(c=>save.completed.Contains(c.ID));}
        public bool ChapterAvailable(ChillChapter chapter)=>Unlocked(chapter);
        public void NormalProgression(){Workspace.collaboration=0;WorkspaceChanged();}
    }
}
