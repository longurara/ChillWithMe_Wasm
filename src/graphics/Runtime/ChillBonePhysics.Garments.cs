using System;
using System.Linq;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public sealed partial class ChillBonePhysics
    {
        int[] bakeBones=Array.Empty<int>();
        public int RecordedBoneCount=>bake==null?0:bakeBones.Count(index=>index>=0);
        public int LiveGarmentBoneCount=>bakeBones.Count(index=>index<0);
        void BindBakeBones()
        {
            var paths=BonePaths();bakeBones=Enumerable.Repeat(-1,paths.Length).ToArray();
            if(bake==null||bake.profileHash!=BakeProfileHash){bake=null;return;}
            var recorded=bake.paths.Select((path,index)=>(path,index)).ToDictionary(value=>value.path,value=>value.index);
            for(int index=0;index<paths.Length;index++)if(recorded.TryGetValue(paths[index],out int target))bakeBones[index]=target;
            // Shared authored bones can replay while added garment bones stay live.
            // Never accept a different base rig just because some names match.
            if(bakeBones.Count(index=>index>=0)!=bake.paths.Length){bake=null;Array.Fill(bakeBones,-1);}
            else if(DynamicRootCount>0)for(int index=0;index<nodes.Count;index++)if(nodes[index].coat||garmentRoots.Any(root=>root&&nodes[index].bone.IsChildOf(root)))bakeBones[index]=-1;
        }
        void ResolveGarmentContacts()
        {
            if(lowQuality)return;
            for(int a=0;a<nodes.Count;a++)
            {
                var first=nodes[a];if(first.pinned||!first.coat||!first.bone||!first.bone.gameObject.activeInHierarchy)continue;
                for(int b=a+1;b<nodes.Count;b++)
                {
                    var second=nodes[b];if(second.pinned||!second.coat||first.root==second.root||!second.bone||!second.bone.gameObject.activeInHierarchy)continue;
                    float distance=Mathf.Clamp(Mathf.Min(first.radius,second.radius),.002f,.008f)*2;
                    // Preserve pre-existing authored seams rather than inflating them.
                    if(Vector3.Distance(first.tickGoal,second.tickGoal)<distance)continue;
                    var edge=second.position-first.position;float length=edge.magnitude;if(length>=distance)continue;
                    var normal=length>1e-6f?edge/length:(second.tickGoal-first.tickGoal).normalized;
                    var correction=normal*((distance-length)*.5f);
                    first.position-=correction;second.position+=correction;
                    first.previous-=correction;second.previous+=correction;
                }
            }
        }
    }
}
