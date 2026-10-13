using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public sealed class ChillLoadDirection:MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]static extern void ChillPlayLoadDirection(string kind,string receiver,int token);
        [DllImport("__Internal")]static extern void ChillCancelLoadDirection();
#endif
        int issued,completed;
        public void DirectionComplete(string value){if(int.TryParse(value,out var token)&&token==issued)completed=token;}
        public void Cancel()
        {
            issued++;
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillCancelLoadDirection();
#endif
        }
        public IEnumerator Play(string kind)
        {
            int token=++issued;
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillPlayLoadDirection(kind,gameObject.name,token);
            float until=Time.realtimeSinceStartup+30;
            while(completed!=token&&Time.realtimeSinceStartup<until)yield return null;
            if(completed!=token)Debug.LogError("Loading direction timed out: "+kind);
#else
            completed=token;yield break;
#endif
        }
    }
}
