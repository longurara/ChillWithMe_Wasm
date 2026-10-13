using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    // Opt-in diagnostic capture; does not draw an overlay or allocate during normal play.
    public sealed class ChillPerformanceCapture : MonoBehaviour
    {
        [Serializable] public class Report
        {
            public string renderer,backend;
            public int frames,targetFps;
            public float seconds,framesPerSecond,medianFrameIntervalMs,p95FrameIntervalMs,maximumFrameIntervalMs;
            public long unityAllocatedBytes,managedBytes;
        }
        readonly List<float> intervals=new List<float>();float started,finish;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void ChillPerformanceResult(string json);
#endif
        public void CapturePerformance(string duration)
        {
            if(!float.TryParse(duration,NumberStyles.Float,CultureInfo.InvariantCulture,out float seconds))seconds=10;
            intervals.Clear();started=Time.realtimeSinceStartup;finish=started+Mathf.Clamp(seconds,1,60);
        }
        void Update()
        {
            if(finish<=0)return;
            intervals.Add(Time.unscaledDeltaTime*1000);
            if(Time.realtimeSinceStartup<finish)return;
            finish=0;var sorted=intervals.OrderBy(x=>x).ToArray();float seconds=Time.realtimeSinceStartup-started;
            var report=new Report{renderer=SystemInfo.graphicsDeviceName,backend=SystemInfo.graphicsDeviceType.ToString(),frames=sorted.Length,targetFps=Application.targetFrameRate,seconds=seconds,framesPerSecond=sorted.Length/seconds,
                medianFrameIntervalMs=sorted[sorted.Length/2],p95FrameIntervalMs=sorted[Mathf.Min(sorted.Length-1,(int)(sorted.Length*.95f))],maximumFrameIntervalMs=sorted[sorted.Length-1],unityAllocatedBytes=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),managedBytes=GC.GetTotalMemory(false)};
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillPerformanceResult(JsonUtility.ToJson(report));
#else
            Debug.Log("HLC_PERFORMANCE_CAPTURE "+JsonUtility.ToJson(report));
#endif
        }
    }
}
