using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor.SceneManagement;
using Hlc.ChillPrototype;
public static class ChillWindowCoatCheck
{
    [Serializable] class Report
    {
        public bool passed,userSaveWritten;
        public int[] motions={57,58};public int samples,settleFrames=180,crossEdges;public bool nativeCapsuleFlags;
        public float maximumBackHemHipPenetrationMeters,maximumSecondaryAngle;
        public string scope="Actual transformed back-hem edges against the native hip capsule in eight fixed window poses. Web solver, not PC runtime parity.";
    }
    public static void Check()
    {
        EditorSceneManager.OpenScene("Assets/Prototype/Room.unity");var player=UnityEngine.Object.FindObjectOfType<ChillGraphicsPlayer>();ChillFeaturesBuild.Configure(player);
        Run(player);
    }
    public static void Run(ChillGraphicsPlayer player)
    {
        var animator=player.character;var physics=player.bones;var wardrobe=player.GetComponent<ChillWardrobe>();wardrobe.Apply(0);
        animator.runtimeAnimatorController=player.story.storyController;animator.Rebind();animator.Update(0);physics.RebindGarment(null);
        bool baked=physics.useBakedFallback;physics.useBakedFallback=false;
        var transforms=animator.GetComponentsInChildren<Transform>(true);
        var left=transforms.First(t=>t.name=="Character_Paka_LB5");var right=transforms.First(t=>t.name=="Character_Paka_RB5");
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var links=(System.Collections.IList)typeof(ChillBonePhysics).GetField("crossLinks",flags).GetValue(physics);
        var report=new Report{crossEdges=links.Count};
        var shapes=(Array)typeof(ChillBonePhysics).GetField("frameBodies",flags).GetValue(physics);
        report.nativeCapsuleFlags=shapes.Cast<object>().Where(body=>body.GetType().GetField("end").GetValue(body)==null).All(body=>{
            var type=body.GetType();return Mathf.Abs((float)type.GetField("radius").GetValue(body)-(float)type.GetField("radiusEnd").GetValue(body))<1e-6f;
        });
        foreach(int value in report.motions)for(int sample=1;sample<=4;sample++)
        {
            float at=sample*.2f;var motion=player.story.presentation.statePaths.First(p=>p.motion==value);
            physics.ResetDynamics();animator.Play(motion.path,0,at);animator.Update(0);physics.Step(0);
            for(int frame=0;frame<180;frame++){physics.BeforeAnimator();animator.Play(motion.path,0,at);animator.Update(0);physics.Step(1f/60);}
            var bodies=(Array)typeof(ChillBonePhysics).GetField("frameBodies",flags).GetValue(physics);
            var body=bodies.Cast<object>().First(b=>((Transform)b.GetType().GetField("transform").GetValue(b)).name=="Magica Capsule Collider (Character_Hips)");
            var type=body.GetType();var a=(Vector3)type.GetField("a").GetValue(body);var bpos=(Vector3)type.GetField("b").GetValue(body);
            float ra=(float)type.GetField("ra").GetValue(body),rb=(float)type.GetField("rb").GetValue(body);
            for(int point=1;point<=3;point++)
            {
                var position=Vector3.Lerp(left.position,right.position,point*.25f);var closest=ChillBonePhysics.Closest(position,a,bpos,out float along);
                report.maximumBackHemHipPenetrationMeters=Mathf.Max(report.maximumBackHemHipPenetrationMeters,Mathf.Lerp(ra,rb,along)-Vector3.Distance(position,closest));
            }
            report.maximumSecondaryAngle=Mathf.Max(report.maximumSecondaryAngle,physics.RotationOffsets().Max(q=>Quaternion.Angle(Quaternion.identity,q)));report.samples++;
            if(!physics.StateFinite())throw new InvalidOperationException("Non-finite coat in window motion "+value);
        }
        report.passed=report.nativeCapsuleFlags&&report.crossEdges==20&&report.maximumBackHemHipPenetrationMeters<.005f&&report.maximumSecondaryAngle<=90.1f;
        File.WriteAllText(Path.GetFullPath("../research/window-coat-check.json"),JsonUtility.ToJson(report,true));physics.useBakedFallback=baked;physics.ResetDynamics();
        if(!report.passed)throw new InvalidOperationException("Window coat intersects hip: "+JsonUtility.ToJson(report));
        Debug.Log("HLC_WINDOW_COAT_CHECK_PASSED "+JsonUtility.ToJson(report));
    }
}
