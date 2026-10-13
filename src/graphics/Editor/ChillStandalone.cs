using UnityEditor;

namespace Hlc.ChillPrototype
{
    public static class ChillStandalone
    {
        [MenuItem("HLC/Chill/Build standalone browser preview")]
        public static void Build()
        {
            ChillAssets.BuildLibrary();
            ChillBuild.PrepareAndPreview();
            ChillMotionAudit.Check();
            ChillFeaturesCheck.BuildChecked();
        }
    }
}
