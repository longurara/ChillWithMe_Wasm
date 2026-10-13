using System.Linq;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        bool NextSentenceIsSelection()=>sentences!=null&&cursor+1<sentences.Length&&sentences[cursor+1].Command==3;
        void PrepareStoryEntry()
        {
            // Native OnStoryStartReady selects the first scripted pose explicitly.
            // Keep the same controller and blend the boundary instead of rebinding.
            var first=sentences.FirstOrDefault(r=>(r.Command==0||r.Command==21||r.Command==22||r.Command==80||r.Command==1000)&&r.BodyMotion>=0);
            if(first==null)return;
            Motion(first);presentation.BlendToRequested(.25f);
        }
    }
}
