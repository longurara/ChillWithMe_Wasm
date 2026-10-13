using System.Collections;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        Coroutine activityTransition;
        int requestedWorkMode=-1;
        bool applyingWorkMode;
        public bool ActivityTransitionPending => activityTransition!=null||requestedWorkMode>=0;
        public bool TryRequestWorkMode(int index)
        {
            if(index<0||index>2||applyingWorkMode||VerificationOnly||!Application.isPlaying||!FeatureReady||!autonomyInstalled)return false;
            if(Playing)return true;
            requestedWorkMode=index;return true;
        }
        void ApplyWorkMode(int index)
        {
            applyingWorkMode=true;
            try{player.PlayState(index);save.deskMode=index;previousMode=index;}
            finally{applyingWorkMode=false;}
        }
        void CancelActivityTransition()
        {if(activityTransition!=null)StopCoroutine(activityTransition);activityTransition=null;requestedWorkMode=-1;}
        IEnumerator TransitionActivity()
        {
            // StartCoroutine executes immediately. Always yield before publishing
            // completion, and keep a running object/leave-chair sequence intact.
            yield return null;
            while(autonomousSequence!=null&&!Playing)yield return null;
            yield return WaitNative(30);
            if(Playing){activityTransition=null;yield break;}
            while(requestedWorkMode>=0)
            {
                int target=requestedWorkMode;requestedWorkMode=-1;
                if(target!=player.SelectedState)
                {
                    string[] names={"Book","Report","Pc"};
                    if(SetNativeMotion("Wild002_From"+names[player.SelectedState]+"To"+names[target]))yield return WaitNative(10);
                    if(Playing){activityTransition=null;yield break;}
                    ApplyWorkMode(target);
                }
            }
            previousRest=Resting;previousMode=player.SelectedState;
            if(musicboxActive&&!previousRest)CloseMusicbox();
            weightGroup="";selfTalkCount=0;ScheduleSelfTalk();
            if(previousRest)SelectBreak();
            else{player.deskObjects.SelectSmooth(previousMode,.25f);SetNativeMotion(workBases[previousMode]);nextAction=Now+Policy("_changeWorkLayoutDelaySeconds",300);nextVariant=Now+VariantDelay();}
            activityTransition=null;SaveNow();
        }
    }
}
