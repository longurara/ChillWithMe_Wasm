using System.Collections.Generic;
using UnityEngine;
namespace Hlc.ChillPrototype
{
    public sealed class ChillCharacterPicking:MonoBehaviour
    {
        readonly Dictionary<SkinnedMeshRenderer,Mesh> meshes=new Dictionary<SkinnedMeshRenderer,Mesh>();
        public bool Hit(Animator actor,Ray worldRay)
        {
            foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(!renderer.enabled||!renderer.sharedMesh||!renderer.bounds.IntersectRay(worldRay))continue;
                if(!meshes.TryGetValue(renderer,out var mesh)){mesh=new Mesh{name="Character click mesh"};meshes[renderer]=mesh;}
                renderer.BakeMesh(mesh);
                var ray=new Ray(renderer.transform.InverseTransformPoint(worldRay.origin),renderer.transform.InverseTransformVector(worldRay.direction));
                if(Intersects(mesh,ray))return true;
            }
            return false;
        }
        public static bool Intersects(Mesh mesh,Ray ray)
        {
            var vertices=mesh.vertices;var triangles=mesh.triangles;
            for(int i=0;i+2<triangles.Length;i+=3)
            {
                Vector3 a=vertices[triangles[i]],edge1=vertices[triangles[i+1]]-a,edge2=vertices[triangles[i+2]]-a;
                var p=Vector3.Cross(ray.direction,edge2);float determinant=Vector3.Dot(edge1,p);if(Mathf.Abs(determinant)<1e-8f)continue;
                float inverse=1/determinant;var offset=ray.origin-a;float u=Vector3.Dot(offset,p)*inverse;if(u<0||u>1)continue;
                var q=Vector3.Cross(offset,edge1);float v=Vector3.Dot(ray.direction,q)*inverse;if(v<0||u+v>1)continue;
                if(Vector3.Dot(edge2,q)*inverse>0)return true;
            }
            return false;
        }
        void OnDestroy(){foreach(var mesh in meshes.Values)if(mesh)Destroy(mesh);meshes.Clear();}
    }
}
