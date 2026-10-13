using System;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    [Serializable] public class ChillBoneBake
    {
        [Serializable] public class Frame { public Quaternion[] offsets; }
        [Serializable] public class Clip
        {
            public string state;public int motion;public float seconds;public bool loop;public Frame[] frames;
            public Quaternion Sample(int bone,float normalized)
            {
                float index=(loop?Mathf.Repeat(normalized,1):Mathf.Clamp01(normalized))*(frames.Length-1);int a=Mathf.FloorToInt(index),b=Mathf.Min(a+1,frames.Length-1);
                var rotation=Quaternion.Slerp(frames[a].offsets[bone],frames[b].offsets[bone],index-a);
                if(loop){float remaining=(1-Mathf.Repeat(normalized,1))*seconds;if(remaining<.12f)rotation=Quaternion.Slerp(rotation,frames[0].offsets[bone],1-remaining/.12f);}
                return rotation;
            }
        }
        public string producer="web-cpu",profileHash;public string[] paths;public Clip[] clips;
    }
}
