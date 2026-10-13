using System.Runtime.InteropServices;
namespace Hlc.ChillPrototype
{
    public static class ChillPersonalMusic
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void ChillMusicAttach(string target);
        [DllImport("__Internal")] static extern void ChillMusicImport(string target,int folder);
        [DllImport("__Internal")] static extern void ChillMusicLoad(string target,string id,int token);
        [DllImport("__Internal")] static extern void ChillMusicRemove(string target,string id);
        [DllImport("__Internal")] static extern void ChillMusicRevoke(string url);
        [DllImport("__Internal")] static extern void ChillEnterFullscreen();
#endif
        public static void Attach(string receiver){
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillMusicAttach(receiver);
#endif
        }
        public static void Import(string receiver,bool folder){
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillMusicImport(receiver,folder?1:0);
#endif
        }
        public static void Load(string receiver,string id,int token){
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillMusicLoad(receiver,id,token);
#endif
        }
        public static void Remove(string receiver,string id){
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillMusicRemove(receiver,id);
#endif
        }
        public static void Revoke(string url){
#if UNITY_WEBGL && !UNITY_EDITOR
            if(!string.IsNullOrEmpty(url))ChillMusicRevoke(url);
#endif
        }
        public static void Fullscreen(){
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillEnterFullscreen();
#else
            UnityEngine.Screen.fullScreen=!UnityEngine.Screen.fullScreen;
#endif
        }
    }
}
