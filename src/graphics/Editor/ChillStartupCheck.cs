using System;
using System.IO;
using Hlc.ChillPrototype;
using UnityEngine;
public static class ChillStartupCheck
{
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException("Startup: "+message);}
    public static void Run()
    {
        var root=new GameObject("Startup fixture");var gate=root.AddComponent<ChillStartupGate>();
        gate.StartupComplete();Require(!gate.Complete,"an early browser callback bypasses room readiness");
        var reveal=gate.Reveal();Require(reveal.MoveNext()&&!gate.Complete,"reveal does not wait for the screen animation");
        gate.StartupComplete();Require(gate.Complete&&!gate.Failed,"ready callback does not release gameplay");
        Require(!reveal.MoveNext(),"completed reveal is still waiting");
        Require(!gate.Reveal().MoveNext()&&gate.Complete,"a repeated reveal resets startup");
        UnityEngine.Object.DestroyImmediate(root);
        root=new GameObject("Failed startup fixture");gate=root.AddComponent<ChillStartupGate>();reveal=gate.Reveal();reveal.MoveNext();gate.Fail("fixture");gate.StartupComplete();
        Require(!reveal.MoveNext()&&gate.Failed&&!gate.Complete,"an error or late callback reveals an incomplete room");UnityEngine.Object.DestroyImmediate(root);
        File.WriteAllText(Path.GetFullPath("../research/startup-gate-check.json"),"{\"passed\":true,\"earlyCallbackRejected\":true,\"animationWait\":true,\"duplicateReadySafe\":true,\"failedRoomNotRevealed\":true,\"userSaveWritten\":false}");
        Debug.Log("HLC_STARTUP_CHECK_PASSED");
    }
}
