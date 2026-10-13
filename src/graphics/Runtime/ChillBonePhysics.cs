using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    // Bounded secondary rotations, using recovered cloth roots and colliders.
    // Animator owns the humanoid pose and every local bone position.
    [DefaultExecutionOrder(200)]
    public sealed class ChillBonePhysics : MonoBehaviour
    {
        [Serializable] class Profile { public Group[] groups; public Collision[] colliders; }
        [Serializable] class Group { public string name; public string[] roots,colliders; public float gravity,damping,radius; }
        [Serializable] class Collision { public string id,path; public bool sphere; public float[] center,size; public int axis; }
        class Body { public Transform transform; public Vector3 center; public float radius,height; public int axis; public bool sphere; }
        class Node
        {
            public Transform bone;
            public Group group;
            public Body[] bodies;
            public int parent=-1;
            public List<int> children=new List<int>();
            public bool pinned;
            public Vector3 restPosition,position,previous,goal;
            public Quaternion restRotation,frameRotation,frameLocalRotation;
            public Quaternion outputLocalRotation;
            public bool applied;
            public float angleLimit;
        }
        public TextAsset configuration;
        public Animator character;
        public bool simulation=true;
        public bool lowQuality;
        readonly List<Node> nodes=new List<Node>();
        Vector3 previousAnchor;
        bool initialized;
        bool needsReset = true;
        public int NodeCount => nodes.Count;
        public void ResetDynamics() { BeforeAnimator(); needsReset = true; }
        public void InitializeForPreview() { if (!initialized) Initialize(); }

        static string PathOf(Transform t) => t.parent ? PathOf(t.parent)+"/"+t.name : t.name;
        static Vector3 Vec(float[] v) => new Vector3(v[0],v[1],v[2]);
        void Start() { Initialize(); }
        void Initialize()
        {
            if (!configuration || !character) return;
            var profile=JsonUtility.FromJson<Profile>(configuration.text);
            var transforms=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .GroupBy(PathOf).ToDictionary(g => g.Key,g => g.First());
            var bodies=new Dictionary<string,Body>();
            foreach(var source in profile.colliders)
            {
                if (!transforms.TryGetValue(source.path,out var transform)) continue;
                bodies[source.id]=new Body { transform=transform,center=Vec(source.center),radius=source.size[0],
                    height=source.size[2],axis=source.axis,sphere=source.sphere };
            }
            var humanBones=new HashSet<Transform>();
            if (character.isHuman)
                for(int i=0;i<(int)HumanBodyBones.LastBone;i++)
                { var bone=character.GetBoneTransform((HumanBodyBones)i); if(bone) humanBones.Add(bone); }
            var indices=new Dictionary<Transform,int>();
            var skinnedBones = new HashSet<Transform>(character.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.bones).Where(b => b));
            foreach(var group in profile.groups)
                foreach(string path in group.roots)
                {
                    if(!transforms.TryGetValue(path,out var root) || !root.IsChildOf(character.transform)) continue;
                    foreach(var bone in root.GetComponentsInChildren<Transform>(true))
                    {
                        if(humanBones.Contains(bone) || indices.ContainsKey(bone) || (bone != root && !skinnedBones.Contains(bone))) continue;
                        var node=new Node { bone=bone,group=group,pinned=bone==root,
                            restPosition=bone.localPosition,restRotation=bone.localRotation,
                            position=bone.position,previous=bone.position,
                            bodies=group.colliders.Where(bodies.ContainsKey).Select(id => bodies[id]).ToArray(),
                            angleLimit=group.name.Contains("Hair") ? 25 : 15 };
                        if(bone.parent && indices.TryGetValue(bone.parent,out int parent) && !node.pinned) node.parent=parent;
                        else node.pinned=true;
                        int index=nodes.Count;nodes.Add(node);indices[bone]=index;
                        if(node.parent>=0) nodes[node.parent].children.Add(index);
                    }
                }
            previousAnchor=character.transform.position;
            initialized=true;
        }
        void Update() { BeforeAnimator(); }
        public void BeforeAnimator()
        {
            if(!initialized) return;
            // Restore only our own last output, never a newly authored pose.
            foreach(var node in nodes)
                if(node.bone && node.applied)
                {
                    if (Quaternion.Angle(node.bone.localRotation,node.outputLocalRotation) < .01f)
                        node.bone.localRotation=node.frameLocalRotation;
                    node.applied=false;
                }
        }
        void LateUpdate() { Step(Time.deltaTime); }
        public void Step(float deltaTime)
        {
            if (!initialized) Initialize();
            if(!initialized || !character) return;
            bool reset=needsReset || !simulation || Vector3.Distance(previousAnchor,character.transform.position)>0.5f || deltaTime > .1f;
            needsReset=false;
            previousAnchor=character.transform.position;
            foreach(var node in nodes)
            {
                node.goal=node.bone.position;node.frameRotation=node.bone.rotation;node.frameLocalRotation=node.bone.localRotation;
                if(reset || node.pinned || Vector3.Distance(node.position,node.goal)>0.6f)
                    node.position=node.previous=node.goal;
            }
            if(reset) return;
            float dt=Mathf.Min(deltaTime,1f/20f);
            if(dt<=0) return;
            int steps=Mathf.Max(1,Mathf.CeilToInt(dt*60)); float step=dt/steps;
            for(int tick=0;tick<steps;tick++)
            {
                foreach(var node in nodes)
                {
                    if(node.pinned || !node.bone.gameObject.activeInHierarchy) continue;
                    float damping=Mathf.Pow(1-Mathf.Clamp(node.group.damping,0.03f,0.85f),step*60);
                    var next=node.position+(node.position-node.previous)*damping;
                    next += Vector3.down*(node.group.gravity*step*step);
                    next += (node.goal-node.position)*Mathf.Min(0.25f,step*18);
                    node.previous=node.position;node.position=next;
                }
                for(int iteration=0;iteration<(lowQuality?2:4);iteration++)
                    foreach(var node in nodes)
                    {
                        if(node.pinned || node.parent<0 || !node.bone.gameObject.activeInHierarchy) continue;
                        var parent=nodes[node.parent];
                        float length=Vector3.Distance(node.goal,parent.goal);
                        var direction=node.position-parent.position;
                        if(direction.sqrMagnitude<1e-10f) direction=node.goal-parent.goal;
                        node.position=parent.position+direction.normalized*length;
                        foreach(var body in node.bodies) ResolveBody(node,body);
                    }
            }
            foreach(var node in nodes)
            {
                if(!node.bone.gameObject.activeInHierarchy) continue;
                if(node.children.Count==1)
                {
                    var child=nodes[node.children[0]];
                    var authored=child.goal-node.goal;var solved=child.position-node.position;
                    if (authored.sqrMagnitude > 1e-9f && solved.sqrMagnitude > 1e-9f)
                    {
                        var world = Quaternion.FromToRotation(authored,solved)*node.frameRotation;
                        var local = node.bone.parent ? Quaternion.Inverse(node.bone.parent.rotation)*world : world;
                        node.outputLocalRotation=Quaternion.RotateTowards(node.frameLocalRotation,local,node.angleLimit);
                        node.bone.localRotation=node.outputLocalRotation; node.applied=true;
                    }
                }
                // No position writes, including tips and pinned roots.
            }
        }
        static void ResolveBody(Node node,Body body)
        {
            if(!body.transform || !body.transform.gameObject.activeInHierarchy) return;
            var center=body.transform.TransformPoint(body.center);
            var scale=body.transform.lossyScale;
            float largest=Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            float radius=Mathf.Max(0,body.radius*largest)+node.group.radius;
            var closest=center;
            if(!body.sphere)
            {
                var axis=body.axis==0?Vector3.right:body.axis==2?Vector3.forward:Vector3.up;
                float half=Mathf.Max(0,body.height*0.5f-body.radius);
                var a=body.transform.TransformPoint(body.center-axis*half);
                var b=body.transform.TransformPoint(body.center+axis*half);
                var ab=b-a;
                closest=a+ab*Mathf.Clamp01(Vector3.Dot(node.position-a,ab)/Mathf.Max(ab.sqrMagnitude,1e-10f));
            }
            var delta=node.position-closest;
            // Respect intersections that are already part of the authored rig.
            // Collision must not push bangs out of the head on the first frame.
            radius = Mathf.Min(radius,Vector3.Distance(node.goal,closest));
            if(delta.sqrMagnitude<radius*radius)
            {
                var normal=delta.sqrMagnitude>1e-10f?delta.normalized:(node.goal-center).normalized;
                if(normal.sqrMagnitude<0.1f)normal=Vector3.forward;
                node.position=closest+normal*radius;
            }
        }
        void OnDisable()
        {
            BeforeAnimator(); needsReset=true;
        }
    }
}
