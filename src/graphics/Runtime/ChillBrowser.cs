using System.Runtime.InteropServices;
namespace Hlc.ChillPrototype
{
    public static class ChillBrowser
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]static extern void ChillAvatarImport(string receiver);
        [DllImport("__Internal")]static extern void ChillNotify(string title,string body,int request);
#endif
        public static void ImportAvatar(string receiver){
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillAvatarImport(receiver);
#endif
        }
        public static void RequestNotifications(){
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillNotify("","",1);
#endif
        }
        public static void Notify(string title,string body){
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillNotify(title,body,0);
#endif
        }
    }
}
