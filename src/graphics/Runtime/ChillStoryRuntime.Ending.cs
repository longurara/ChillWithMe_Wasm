using System.Collections;
using System.Linq;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        ChillLoadDirection LoadDirection=>GetComponent<ChillLoadDirection>()??gameObject.AddComponent<ChillLoadDirection>();
        IEnumerator FinishCreditsDirection()
        {
            GetComponent<ChillNativeUI>()?.StopNativeCredits();
            yield return LoadDirection.Play("credits-finish");
            effects.ResetEffects();decorations.Restore();
            player.environment.SelectTime(save.timeMode);player.environment.SelectEnvironment(save.windowMode);
            foreach(var choice in Workspace.room.ToArray())yield return decorations.Apply(choice.skin);
            decorations.SetHair(Workspace.hair);GetComponent<ChillWardrobe>()?.Apply(Workspace.costume);
            if(presentation.cameras)presentation.cameras.ResetCamera();
            foreach(var source in pausedStoryAmbience)if(source)source.UnPause();
            yield return LoadDirection.Play("fade");
        }
        IEnumerator FarewellScreen()
        {yield return LoadDirection.Play("gameEnd");}
    }
}
