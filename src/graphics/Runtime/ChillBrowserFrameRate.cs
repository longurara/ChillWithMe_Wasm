using System.Runtime.InteropServices;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public static class ChillBrowserFrameRate
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void ChillSetBrowserFrameRate(int fps);
#endif
        public static void Apply(int fps)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillSetBrowserFrameRate(Mathf.Clamp(fps,1,240));
#endif
        }
    }
}
