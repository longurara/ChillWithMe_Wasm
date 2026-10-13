using System;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    [Serializable] public class ChillBoneBake
    {
        [Serializable] public class Frame { public Quaternion[] offsets; }
        [Serializable] public class Clip
        {
            public string state;public int motion;public float seconds;public Frame[] frames;
            public Quaternion Sample(int bone,float normalized)
            {
                float index=Mathf.Clamp01(normalized)*(frames.Length-1);int a=Mathf.FloorToInt(index),b=Mathf.Min(a+1,frames.Length-1);
                return Quaternion.Slerp(frames[a].offsets[bone],frames[b].offsets[bone],index-a);
            }
        }
        public string profileHash;public string[] paths;public Clip[] clips;
    }
}
