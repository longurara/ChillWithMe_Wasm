using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Hlc.ChillPrototype;
public static class ChillCoatGravityCheck
{
    [Serializable] class Report{public bool passed,userSaveWritten;public float meanHemDropMeters,maxSecondaryAngle;public int hems,settleFrames;public string scope="Web secondary bone solver in a fixed leave-chair pose; not a PC MagicaCloth comparison.";}
    public static void Run(ChillGraphicsPlayer player)
    {
        var physics=player.bones;bool baked=physics.useBakedFallback;var animator=player.character;var motion=player.story.presentation.statePaths.First(p=>p.name=="Wild003_LeaveChair_Normal");
        player.GetComponent<ChillWardrobe>().Apply(0);physics.useBakedFallback=false;physics.ResetDynamics();animator.Play(motion.path,0,.22f);animator.Update(0);physics.Step(0);
        var tips=animator.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Character_Paka_")&&t.name.EndsWith("5")).ToArray();var before=tips.Select(t=>t.position.y).ToArray();
        for(int frame=0;frame<180;frame++){physics.BeforeAnimator();animator.Play(motion.path,0,.22f);animator.Update(0);physics.Step(1f/60);}
        var report=new Report{hems=tips.Length,settleFrames=180,meanHemDropMeters=tips.Select((t,i)=>before[i]-t.position.y).Average(),maxSecondaryAngle=physics.RotationOffsets().Max(q=>Quaternion.Angle(Quaternion.identity,q))};
        report.passed=physics.StateFinite()&&tips.Length==6&&report.meanHemDropMeters>.015f&&report.maxSecondaryAngle<=90.1f;
        File.WriteAllText(Path.GetFullPath("../research/coat-gravity-check.json"),JsonUtility.ToJson(report,true));physics.useBakedFallback=baked;physics.ResetDynamics();
        if(!report.passed)throw new InvalidOperationException("Coat hem does not respond to source gravity");Debug.Log("HLC_COAT_GRAVITY_CHECK_PASSED meanDrop="+report.meanHemDropMeters);
    }
}
