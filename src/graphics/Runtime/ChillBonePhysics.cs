using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    // Rotation-only CPU adaptation. Animator owns human joints and local positions.
    // Source Magica parameters are retained; topology and solver differ on WebGL.
    [DefaultExecutionOrder(200)]
    public sealed partial class ChillBonePhysics : MonoBehaviour
    {
        [Serializable] class Profile { public Group[] groups; public Collision[] colliders; }
        [Serializable] class Key { public float time,value,inSlope,outSlope; }
        [Serializable] class Curve
        {
            public float value; public bool useCurve; public Key[] keys;
            public float At(float depth)
            {
                if(!useCurve||keys==null||keys.Length==0)return value;
                if(depth<=keys[0].time)return value*keys[0].value;
                for(int i=1;i<keys.Length;i++)if(depth<=keys[i].time)
                {
                    var a=keys[i-1];var b=keys[i];float span=b.time-a.time;
                    float t=Mathf.Clamp01((depth-a.time)/Mathf.Max(span,1e-6f)),t2=t*t,t3=t2*t;
                    return value*((2*t3-3*t2+1)*a.value+(t3-2*t2+t)*span*a.outSlope+(-2*t3+3*t2)*b.value+(t3-t2)*span*b.inSlope);
                }
                return value*keys[keys.Length-1].value;
            }
        }
        [Serializable] class Group
        {
            public int connectionMode; public Curve distanceCurve;
            public string name; public string[] roots,colliders;
            public float gravity,damping,radius,worldInertia=1,localInertia=1,velocityAttenuation=.7f;
            public float teleportDistance=.5f,teleportRotation=90;
            public float[] gravityDirection;
            public bool useAngleLimit,useRestoration,useMaxDistance;
            public Curve dampingCurve,radiusCurve,angleCurve,restorationCurve,speedLimit,maxDistance;
        }
        [Serializable] class Collision { public string id,path; public bool sphere,radiusSeparation,reverseDirection,alignedOnCenter=true; public float[] center,size; public int axis; }
        class Body
        {
            public Transform transform,end; public Vector3 center,a,b; public float radius,radiusEnd,height,ra,rb;
            public int axis; public bool sphere,active,reverseDirection,alignedOnCenter=true;
        }
        class Node
        {
            public Transform bone; public Group group; public Body[] bodies;
            public float[] allowances; public int parent=-1,root,depth; public readonly List<int> children=new List<int>();
            public bool pinned,applied,coat,bakedSynchronized;
            public Vector3 lastBakedPosition;
            public Vector3 position,previous,goal,previousGoal,tickGoal,lastTickGoal,renderFrom,anchorPosition;
            public Quaternion anchorRotation,frameRotation,frameLocalRotation,outputLocalRotation;
            public float angleLimit,radius,damping,restoration,speedLimit,maxDistance;
        }
        public TextAsset configuration;
        public TextAsset bakedConfiguration;
        public bool useBakedFallback=true;
        public Animator character;
        public bool simulation=true,lowQuality;
        readonly List<Node> nodes=new List<Node>();
        readonly List<(int a,int b,float stiffness)> crossLinks=new List<(int,int,float)>();
        readonly List<Transform> garmentRoots=new List<Transform>();
        Body[] frameBodies=Array.Empty<Body>();
        bool initialized,needsReset=true,suspended,resuming;
        double accumulator;
        ChillBoneBake bake;
        ChillBoneBake cachedBake;
        TextAsset loadedBake;
        ChillBoneBake.Clip activeBake;
        ChillStoryPresentation presentation;
        float bakeWeight,bakeTime;
        const float FixedStep=1f/60f;
        const double TickDuration=1d/60d;
        const int MaxCatchUp=6;
        public int NodeCount=>nodes.Count;
        public int LastStepCount { get; private set; }
        public long SimulationTicks { get; private set; }
        public float DroppedSeconds { get; private set; }
        public int DynamicRootCount=>garmentRoots.Count(t=>t);
        public bool IsSuspended=>suspended;
        public bool BakedFallbackActive=>activeBake!=null&&bakeWeight>0;
        public string BakeProfileHash=>Hash128.Compute(configuration.text+"|coat-cross-edges-v5").ToString();
        public void ResetDynamics(){BeforeAnimator();needsReset=true;accumulator=0;activeBake=null;bakeWeight=0;}
        public void InitializeForPreview(){if(!initialized)Initialize();}
        public void SetSuspended(bool value){if(value==suspended)return;suspended=value;accumulator=0;resuming=true;}
        // Called before deferred destruction, then after attachment of a costume.
        public void RebindGarment(IEnumerable<Transform> roots)
        {BeforeAnimator();garmentRoots.Clear();if(roots!=null)garmentRoots.AddRange(roots.Where(t=>t).Distinct());Initialize();}
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
        static Vector3 Vec(float[] v)=>v!=null&&v.Length>=3?new Vector3(v[0],v[1],v[2]):Vector3.zero;
        static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);
        void Start(){if(!initialized)Initialize();}
        void Initialize()
        {
            if(!configuration||!character)return;
            var retained=nodes.Where(n=>n.bone&&n.bone.gameObject.activeInHierarchy).ToDictionary(n=>n.bone);
            nodes.Clear();crossLinks.Clear();var profile=JsonUtility.FromJson<Profile>(configuration.text);
            var transforms=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).GroupBy(PathOf).ToDictionary(g=>g.Key,g=>g.First());
            var bodies=new Dictionary<string,Body>();
            foreach(var source in profile.colliders??Array.Empty<Collision>())if(transforms.TryGetValue(source.path,out var t))
                bodies[source.id]=new Body{transform=t,center=Vec(source.center),radius=source.size[0],radiusEnd=source.radiusSeparation?source.size[1]:source.size[0],height=source.size[2],axis=source.axis,sphere=source.sphere,reverseDirection=source.reverseDirection,alignedOnCenter=source.alignedOnCenter};
            var humanBones=new HashSet<Transform>();
            if(character.isHuman)for(int i=0;i<(int)HumanBodyBones.LastBone;i++){var t=character.GetBoneTransform((HumanBodyBones)i);if(t)humanBones.Add(t);}
            var supplements=new List<Body>();
            void Limb(HumanBodyBones a,HumanBodyBones b,float radius)
            {if(!character.isHuman)return;var start=character.GetBoneTransform(a);var end=character.GetBoneTransform(b);if(start&&end)supplements.Add(new Body{transform=start,end=end,radius=radius,radiusEnd=radius*.8f});}
            // Original profile has head/torso/legs; supplement arm and neck crossings.
            Limb(HumanBodyBones.Neck,HumanBodyBones.Head,.035f);
            Limb(HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,.035f);Limb(HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,.035f);
            Limb(HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,.025f);Limb(HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,.025f);
            var indices=new Dictionary<Transform,int>();
            var skinnedBones=new HashSet<Transform>(character.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r=>r.bones).Where(b=>b));
            void AddRoot(Group group,Transform root,bool dynamic)
            {
                if(!root||!root.IsChildOf(character.transform))return;int rootIndex=nodes.Count;
                foreach(var bone in root.GetComponentsInChildren<Transform>(true))
                {
                    if(humanBones.Contains(bone)||indices.ContainsKey(bone)||(!dynamic&&bone!=root&&!skinnedBones.Contains(bone)))continue;
                    bool isCoat=group.roots.Any(r=>r.Contains("Character_Paka_"));
                    var contacts=(group.colliders??Array.Empty<string>()).Where(bodies.ContainsKey).Select(id=>bodies[id]).Concat(isCoat?Enumerable.Empty<Body>():supplements).ToArray();
                    var node=new Node{bone=bone,group=group,pinned=bone==root,root=rootIndex,bodies=contacts,coat=isCoat};
                    if(!node.pinned&&bone.parent&&indices.TryGetValue(bone.parent,out int parent)){node.parent=parent;node.depth=nodes[parent].depth+1;}
                    else{node.pinned=true;node.root=nodes.Count;}
                    int index=nodes.Count;nodes.Add(node);indices[bone]=index;if(node.parent>=0)nodes[node.parent].children.Add(index);
                    if(retained.TryGetValue(bone,out var old))
                    {node.position=old.position;node.previous=old.previous;node.previousGoal=old.previousGoal;node.tickGoal=old.tickGoal;node.lastTickGoal=old.lastTickGoal;node.renderFrom=old.renderFrom;node.anchorPosition=old.anchorPosition;node.anchorRotation=old.anchorRotation;}
                    else{node.position=node.previous=node.previousGoal=node.tickGoal=node.lastTickGoal=node.renderFrom=bone.position;node.anchorPosition=root.position;node.anchorRotation=root.rotation;}
                }
            }
            foreach(var group in profile.groups??Array.Empty<Group>())foreach(var path in group.roots??Array.Empty<string>())if(transforms.TryGetValue(path,out var root))AddRoot(group,root,false);
            // Source custom sleeves are tweens. Only unmatched secondary chains get
            // a small additive offset; shared/default bones are never registered twice.
            var sleeve=new Group{name="Web sleeve",gravity=2,damping=.35f,radius=.006f,worldInertia=.35f,localInertia=.4f,teleportDistance=.5f,teleportRotation=90,colliders=bodies.Keys.ToArray(),roots=Array.Empty<string>()};
            foreach(var root in garmentRoots)AddRoot(sleeve,root,true);
            foreach(var node in nodes)
            {
                int maxDepth=nodes.Where(n=>n.root==node.root).Max(n=>n.depth);float depth=maxDepth>0?(float)node.depth/maxDepth:0;
                // Source coat limits rise from 0 to 120 degrees down the hem.
                // A 40-degree ceiling on every joint kept the distal hem rigid
                // when the hips bend. Retain the depth curve with a 90-degree
                // web ceiling; human joints and translations remain Animator-owned.
                float cap=node.group.name.Contains("Hair")?30:node.group.name=="Web sleeve"?12:node.group.roots.Any(r=>r.Contains("christmashat"))?25:node.coat?38:90;
                node.angleLimit=node.group.useAngleLimit&&node.group.angleCurve!=null?Mathf.Clamp(node.group.angleCurve.At(depth),0,cap):cap;
                node.radius=Mathf.Max(0,node.group.radiusCurve!=null&&node.group.radiusCurve.value>0?node.group.radiusCurve.At(depth):node.group.radius);
                node.damping=Mathf.Clamp(node.group.dampingCurve!=null&&node.group.dampingCurve.value>0?node.group.dampingCurve.At(depth):node.group.damping,.03f,.85f);
                node.restoration=node.group.useRestoration?Mathf.Clamp01(node.group.restorationCurve?.At(depth)??0):0;
                node.speedLimit=Mathf.Clamp(node.group.speedLimit!=null&&node.group.speedLimit.value>0?node.group.speedLimit.At(depth):2,.1f,4);
                node.maxDistance=node.group.useMaxDistance?Mathf.Max(.001f,node.group.maxDistance?.At(depth)??.2f):.3f;
                node.allowances=new float[node.bodies.Length];
                // Rest intersection allowance is captured once, never widened by a pose.
                for(int j=0;j<node.bodies.Length;j++)
                {
                    var body=node.bodies[j];
                    if(retained.TryGetValue(node.bone,out var old))
                    {
                        int prior=Array.FindIndex(old.bodies,b=>b.transform==body.transform&&b.end==body.end);
                        if(prior>=0){node.allowances[j]=old.allowances[prior];continue;}
                    }
                    BodyShape(body,out var a,out var b,out float ra,out float rb);var p=Closest(node.bone.position,a,b,out float t);
                    node.allowances[j]=Mathf.Max(0,Mathf.Lerp(ra,rb,t)+node.radius-Vector3.Distance(node.bone.position,p));
                }
            }
            // Native coat uses Sequential Non Loop Mesh (connectionMode 3).
            // Retain the open front and connect neighboring strips in source order.
            foreach(var group in profile.groups??Array.Empty<Group>())if(group.connectionMode==3)
            {
                var roots=(group.roots??Array.Empty<string>()).Select(path=>transforms.TryGetValue(path,out var t)&&indices.TryGetValue(t,out int i)?i:-1).ToArray();
                for(int strip=1;strip<roots.Length;strip++)if(roots[strip-1]>=0&&roots[strip]>=0)
                {
                    int a=roots[strip-1],b=roots[strip];
                    while(nodes[a].children.Count==1&&nodes[b].children.Count==1)
                    {
                        a=nodes[a].children[0];b=nodes[b].children[0];
                        int depth=nodes.Where(n=>n.root==nodes[a].root).Max(n=>n.depth);
                        crossLinks.Add((a,b,Mathf.Clamp01(group.distanceCurve?.At((float)nodes[a].depth/depth)??.3f)));
                    }
                }
            }
            frameBodies=nodes.SelectMany(n=>n.bodies).Distinct().ToArray();
            // Outfit changes rebuild the small rig map, not the multi-MB recording.
            if(loadedBake!=bakedConfiguration)
            {loadedBake=bakedConfiguration;cachedBake=ChillBoneBakeCodec.Read(bakedConfiguration);}
            bake=cachedBake;
            BindBakeBones();
            presentation=GetComponent<ChillGraphicsPlayer>()?.story?.presentation;
            activeBake=null;bakeWeight=0;initialized=true;
        }
        void Update(){BeforeAnimator();}
        public void BeforeAnimator()
        {
            foreach(var node in nodes)if(node.bone&&node.applied)
            {if(Quaternion.Angle(node.bone.localRotation,node.outputLocalRotation)<.01f)node.bone.localRotation=node.frameLocalRotation;node.applied=false;}
        }
        void LateUpdate(){Step(Time.deltaTime);}
        public void Step(float deltaTime)
        {
            if(!initialized)Initialize();LastStepCount=0;if(!initialized||!character)return;
            foreach(var body in frameBodies)
            {
                body.active=body.transform&&body.transform.gameObject.activeInHierarchy;
                if(body.active)BodyShape(body,out body.a,out body.b,out body.ra,out body.rb);
            }
            if(float.IsNaN(deltaTime)||float.IsInfinity(deltaTime)||deltaTime<0)deltaTime=0;
            bool reset=needsReset||!simulation;needsReset=false;
            foreach(var node in nodes)if(node.bone){node.goal=node.bone.position;node.frameRotation=node.bone.rotation;node.frameLocalRotation=node.bone.localRotation;}
            foreach(var node in nodes)if(node.bone)
            {
                var anchor=nodes[node.root];var rotation=anchor.bone.rotation;
                bool teleport=Vector3.Distance(node.anchorPosition,anchor.goal)>node.group.teleportDistance||Quaternion.Angle(node.anchorRotation,rotation)>node.group.teleportRotation;
                float world=teleport||resuming||suspended?1:1-Mathf.Clamp01(node.group.worldInertia),local=teleport||resuming||suspended?1:1-Mathf.Clamp01(node.group.localInertia);
                var turn=Quaternion.Slerp(Quaternion.identity,rotation*Quaternion.Inverse(node.anchorRotation),local);var shift=(anchor.goal-node.anchorPosition)*world;
                Vector3 Move(Vector3 p)=>node.anchorPosition+turn*(p-node.anchorPosition)+shift;
                node.position=Move(node.position);node.previous=Move(node.previous);node.renderFrom=Move(node.renderFrom);
                node.lastTickGoal=Move(node.lastTickGoal);node.tickGoal=Move(node.tickGoal);node.previousGoal=Move(node.previousGoal);
                node.anchorPosition=anchor.goal;node.anchorRotation=rotation;
                if(reset||!Finite(node.position)||!Finite(node.previous))node.position=node.previous=node.previousGoal=node.tickGoal=node.lastTickGoal=node.renderFrom=node.goal;
                if(node.pinned)node.position=node.previous=node.goal;
            }
            resuming=false;
            if(reset){accumulator=0;return;}
            if(suspended){foreach(var n in nodes)n.previousGoal=n.goal;ApplyOutput();return;}
            float elapsed=Mathf.Min(deltaTime,MaxCatchUp*FixedStep);DroppedSeconds+=Mathf.Max(0,deltaTime-elapsed);
            double prior=accumulator;accumulator+=elapsed;
            while(accumulator+1e-8>=TickDuration&&LastStepCount<MaxCatchUp)
            {
                float fraction=elapsed>0?Mathf.Clamp01((float)((LastStepCount+1)*TickDuration-prior)/elapsed):1;
                foreach(var node in nodes)
                {
                    node.renderFrom=node.position;node.lastTickGoal=node.tickGoal;node.tickGoal=Vector3.Lerp(node.previousGoal,node.goal,fraction);
                    if(node.pinned){node.position=node.previous=node.tickGoal;continue;}
                    if(!node.bone||!node.bone.gameObject.activeInHierarchy)continue;
                    float attenuation=1-node.restoration*(1-Mathf.Clamp01(node.group.velocityAttenuation));
                    var velocity=Vector3.ClampMagnitude((node.position-node.previous)*(1-node.damping)*attenuation,node.speedLimit*FixedStep);
                    var next=node.position+velocity+Gravity(node.group)*FixedStep*FixedStep;
                    // Source coat restoration is zero. A baseline spring kept
                    // pulling its hem back to the stiff exported pose.
                    float stiffness=(node.coat?0:2)+node.restoration*30;
                    next+=(node.tickGoal-node.position)*(1-Mathf.Exp(-stiffness*FixedStep));node.previous=node.position;node.position=next;
                }
                for(int iteration=0;iteration<(lowQuality?2:4);iteration++)
                {
                  foreach(var node in nodes)if(node.bone&&!node.pinned&&node.parent>=0&&node.bone.gameObject.activeInHierarchy)
                  {
                    var parent=nodes[node.parent];var authored=node.tickGoal-parent.tickGoal;var direction=node.position-parent.position;
                    if(direction.sqrMagnitude<1e-10f)direction=authored;
                    direction=Vector3.RotateTowards(authored,direction,node.angleLimit*Mathf.Deg2Rad,0).normalized;
                    node.position=parent.position+direction*authored.magnitude;
                    node.position=node.tickGoal+Vector3.ClampMagnitude(node.position-node.tickGoal,node.maxDistance);
                    for(int j=0;j<node.bodies.Length;j++)ResolveBody(node,parent,node.bodies[j],node.allowances[j]);
                  }
                  foreach(var link in crossLinks)
                  {
                    var a=nodes[link.a];var b=nodes[link.b];if(!a.bone.gameObject.activeInHierarchy||!b.bone.gameObject.activeInHierarchy)continue;
                    var edge=b.position-a.position;float length=edge.magnitude;
                    if(length>1e-6f){var correction=edge*((length-Vector3.Distance(a.tickGoal,b.tickGoal))/length*link.stiffness*.5f);a.position+=correction;b.position-=correction;}
                    for(int j=0;j<a.bodies.Length;j++)ResolveCrossEdge(a,b,a.bodies[j],Mathf.Min(a.allowances[j],b.allowances[j]));
                  }
                  ResolveGarmentContacts();
                }
                accumulator=Math.Max(0,accumulator-TickDuration);LastStepCount++;SimulationTicks++;
            }
            foreach(var node in nodes)node.previousGoal=node.goal;UpdateBake(elapsed);ApplyOutput();
        }
        void UpdateBake(float elapsed)
        {
            if(bake==null){activeBake=null;bakeWeight=0;return;}
            ChillBoneBake.Clip requested=null;
            var state=character.GetCurrentAnimatorStateInfo(0);
            if(useBakedFallback&&bake!=null&&simulation&&!character.IsInTransition(0))
                requested=Array.Find(bake.clips,c=>Animator.StringToHash(c.state)==state.fullPathHash&&state.normalizedTime>=0&&(c.loop||state.normalizedTime<=1));
            if(requested!=activeBake&&activeBake!=null&&bakeWeight>0)requested=null;
            if(requested!=null&&requested!=activeBake){activeBake=requested;bakeWeight=0;}
            bakeWeight=Mathf.MoveTowards(bakeWeight,requested!=null?1:0,elapsed/.25f);
            if(requested!=null)bakeTime=state.normalizedTime;
            if(bakeWeight<=0)activeBake=null;
        }
        static Vector3 Gravity(Group group){var direction=Vec(group.gravityDirection);return (direction.sqrMagnitude>.01f?direction.normalized:Vector3.down)*group.gravity;}
        void ApplyOutput()
        {
            float alpha=Mathf.Clamp01((float)(accumulator/TickDuration));
            Vector3 Render(Node n)=>n.pinned?n.goal:n.goal+Vector3.Lerp(n.renderFrom-n.lastTickGoal,n.position-n.tickGoal,alpha);
            for(int index=0;index<nodes.Count;index++)
            {
                var node=nodes[index];
                if(!node.bone||!node.bone.gameObject.activeInHierarchy)continue;
                Quaternion local=node.frameLocalRotation;float limit=node.angleLimit;
                if(node.children.Count==1)
                {
                    var child=nodes[node.children[0]];var authored=child.goal-node.goal;var solved=Render(child)-Render(node);
                    if(authored.sqrMagnitude<=1e-9f||solved.sqrMagnitude<=1e-9f)continue;
                    var world=Quaternion.FromToRotation(authored,solved)*node.frameRotation;
                    local=node.bone.parent?Quaternion.Inverse(node.bone.parent.rotation)*world:world;limit=child.angleLimit;
                }
                else if(activeBake==null||bakeWeight<=0||bakeBones[index]<0)continue;
                bool recorded=activeBake!=null&&bakeWeight>0&&bakeBones[index]>=0;
                if(recorded)local=Quaternion.Slerp(local,node.frameLocalRotation*activeBake.Sample(bakeBones[index],bakeTime),Mathf.SmoothStep(0,1,bakeWeight));
                node.outputLocalRotation=recorded&&(activeBake.nativeReference||bake?.producer=="source-magica")?local:Quaternion.RotateTowards(node.frameLocalRotation,local,limit);
                node.bone.localRotation=node.outputLocalRotation;node.applied=true;
            }
            // Keep live particles on the displayed native pose, so leaving a recording
            // does not expose an independently drifting CPU simulation.
            for(int index=0;index<nodes.Count;index++)
            {
                var node=nodes[index];
                if(activeBake!=null&&bakeWeight>=.999f&&bakeBones[index]>=0&&node.bone&&node.bone.gameObject.activeInHierarchy)
                {
                    var position=node.bone.position;node.previous=node.bakedSynchronized?node.lastBakedPosition:position;
                    node.position=node.renderFrom=node.lastBakedPosition=position;node.tickGoal=node.lastTickGoal=node.goal;node.bakedSynchronized=true;
                }
                else node.bakedSynchronized=false;
            }
        }
        public static Vector3 Closest(Vector3 point,Vector3 a,Vector3 b,out float t){var ab=b-a;t=Mathf.Clamp01(Vector3.Dot(point-a,ab)/Mathf.Max(ab.sqrMagnitude,1e-10f));return a+ab*t;}
        static void BodyShape(Body body,out Vector3 a,out Vector3 b,out float ra,out float rb)
        {
            a=b=body.transform.TransformPoint(body.center);var scale=body.transform.lossyScale;float largest=Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            ra=Mathf.Max(0,body.radius*largest);rb=body.sphere?ra:Mathf.Max(0,body.radiusEnd*largest);
            if(body.end){a=body.transform.position;b=body.end.position;return;}if(body.sphere)return;
            var axis=body.axis==0?Vector3.right:body.axis==2?Vector3.forward:Vector3.up;float half=Mathf.Max(0,body.height*.5f-Mathf.Max(body.radius,body.radiusEnd));
            if(body.reverseDirection)axis=-axis;
            var origin=body.alignedOnCenter?body.center:body.center-axis*body.height*.5f;
            a=body.transform.TransformPoint(origin+axis*half);b=body.transform.TransformPoint(origin-axis*half);
        }
        static void ResolveBody(Node node,Node parent,Body body,float allowance)
        {
            if(!body.active)return;
            // Point and interior edge contacts catch crossings of thin limbs.
            for(int sample=body.end!=null?0:2;sample<3;sample++)
            {
                float weight=sample==0?.5f:sample==1?.75f:1;var point=Vector3.Lerp(parent.position,node.position,weight);
                var closest=Closest(point,body.a,body.b,out float t);float radius=Mathf.Max(0,Mathf.Lerp(body.ra,body.rb,t)+node.radius-allowance);
                var delta=point-closest;if(delta.sqrMagnitude>=radius*radius)continue;
                var normal=delta.sqrMagnitude>1e-10f?delta.normalized:(node.tickGoal-closest).normalized;if(normal.sqrMagnitude<.1f)normal=Vector3.forward;
                node.position+=normal*(radius-delta.magnitude)/weight;
                var velocity=node.position-node.previous;float inward=Vector3.Dot(velocity,normal);if(inward<0)velocity-=normal*inward;
                node.previous=node.position-velocity*.9f;
            }
        }
        static void ResolveCrossEdge(Node a,Node b,Body body,float allowance)
        {
            if(!body.active)return;
            for(int sample=1;sample<=3;sample++)
            {
                float t=sample*.25f;var point=Vector3.Lerp(a.position,b.position,t);
                var closest=Closest(point,body.a,body.b,out float along);
                float chordAllowance=allowance;
                if(a.depth<3)
                {
                    var authored=Vector3.Lerp(a.tickGoal,b.tickGoal,t);var authoredClosest=Closest(authored,body.a,body.b,out float authoredAlong);
                    chordAllowance=Mathf.Max(allowance,Mathf.Lerp(body.ra,body.rb,authoredAlong)+Mathf.Lerp(a.radius,b.radius,t)-Vector3.Distance(authored,authoredClosest));
                }
                float radius=Mathf.Max(0,Mathf.Lerp(body.ra,body.rb,along)+Mathf.Lerp(a.radius,b.radius,t)-chordAllowance);
                var delta=point-closest;if(delta.sqrMagnitude>=radius*radius)continue;
                var normal=delta.sqrMagnitude>1e-10f?delta.normalized:(Vector3.Lerp(a.tickGoal,b.tickGoal,t)-closest).normalized;
                if(normal.sqrMagnitude<.1f)normal=Vector3.forward;
                float wa=1-t,wb=t,denominator=wa*wa+wb*wb;var correction=normal*(radius-delta.magnitude)/denominator;
                a.position+=correction*wa;b.position+=correction*wb;
                // Contact must not inject velocity into both strips.
                a.previous+=correction*wa;b.previous+=correction*wb;
            }
        }
        public string[] BonePaths()=>nodes.Where(n=>n.bone).Select(n=>RelativePath(n.bone)).ToArray();
        string RelativePath(Transform bone)=>bone==character.transform?"":bone.parent==character.transform?bone.name:RelativePath(bone.parent)+"/"+bone.name;
        public Quaternion[] RotationOffsets()=>nodes.Where(n=>n.bone).Select(n=>n.applied?Quaternion.Inverse(n.frameLocalRotation)*n.bone.localRotation:Quaternion.identity).ToArray();
        public bool StateFinite()=>nodes.All(n=>!n.bone||(Finite(n.position)&&Finite(n.previous)));
        void OnDisable(){BeforeAnimator();resuming=true;accumulator=0;}
    }
}
