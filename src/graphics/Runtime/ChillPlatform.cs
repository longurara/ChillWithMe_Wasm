using System.Runtime.InteropServices;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public static class ChillPlatform
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int ChillIsMobileBrowser();
        static bool? mobile;
        public static bool Mobile => mobile ?? (mobile=ChillIsMobileBrowser() != 0).Value;
#else
        public static bool Mobile => Application.isMobilePlatform;
#endif
    }
}
