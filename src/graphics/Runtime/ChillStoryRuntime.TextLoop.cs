using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        IEnumerator RunTextLoop(ChillSentence setup)
        {
            chosen=-1;
            if(loops.Count==0){Notify("This dialogue loop has no source lines.",true);cursor++;yield break;}
            var bag=new List<ChillSentence>();ChillSentence previous=null;
            ChillSentence Pick()
            {
                if(bag.Count==0)bag.AddRange(loops.OrderBy(_=>UnityEngine.Random.value));
                if(bag.Count>1&&bag[0]==previous){var first=bag[0];bag.RemoveAt(0);bag.Add(first);}
                var row=bag[0];bag.RemoveAt(0);previous=row;return row;
            }
            // Source shows choices only after the first loop voice/text finishes.
            yield return LoopSpeech(Pick(),Mathf.Max(0,Number(setup.Arg1)));
            if(!Playing)yield break;
            PrepareChoices(Jump(setup.Arg3),3);
            while(Playing&&chosen<0)
            {
                float min=Mathf.Max(0,Number(setup.Arg1)),max=Mathf.Max(min,setup.Arg2);
                double end=Now+UnityEngine.Random.Range(min,max);
                while(Playing&&chosen<0&&Now<end)yield return null;
                if(!Playing||chosen>=0)break;
                yield return LoopSpeech(Pick(),min);
            }
            if(Playing)CommitChoice(3);
        }
        IEnumerator LoopSpeech(ChillSentence row,float delay)
        {
            double until=Now+delay;while(Playing&&chosen<0&&Now<until)yield return null;
            if(!Playing||chosen>=0)yield break;
            yield return Speak(row,false);
        }
    }
}
