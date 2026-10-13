using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        int loopChoiceVersion;
        IEnumerator ShowLoopChoices(ChillSentence setup,int token,int version)
        {
            float delay=Mathf.Max(0,Number(setup.Arg4));
            if(delay>0)yield return new WaitForSecondsRealtime(delay);
            if(Playing&&token==generation&&version==loopChoiceVersion&&current==setup)PrepareChoices(Jump(setup.Arg3),3);
        }
        IEnumerator RunTextLoop(ChillSentence setup)
        {
            chosen=-1;
            if(loops.Count==0){Notify("This dialogue loop has no source lines.",true);cursor++;yield break;}
            var bag=new List<ChillSentence>();ChillSentence previous=null;
            ChillSentence Pick()
            {
                if(bag.Count==0)bag.AddRange(loops.OrderBy(_=>ChillRuntimeContext.Value));
                if(bag.Count>1&&bag[0]==previous){var first=bag[0];bag.RemoveAt(0);bag.Add(first);}
                var row=bag[0];bag.RemoveAt(0);previous=row;return row;
            }
            // The first line has its own Arg1. The setup's min/max only separate repeats.
            yield return Speak(Pick(),false);
            if(!Playing)yield break;
            int version=++loopChoiceVersion;
            StartCoroutine(ShowLoopChoices(setup,generation,version));
            while(Playing&&chosen<0)
            {
                float min=Mathf.Max(0,Number(setup.Arg1)),max=Mathf.Max(min,setup.Arg2);
                double end=Now+ChillRuntimeContext.Range(min,max);
                while(Playing&&chosen<0&&Now<end)yield return null;
                if(!Playing||chosen>=0)break;
                yield return Speak(Pick(),false);
            }
            loopChoiceVersion++;
            if(Playing)CommitChoice(3);
        }
    }
}
