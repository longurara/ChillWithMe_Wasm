using System;
using System.Collections;
using System.Linq;
using UnityEngine;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        IEnumerator SnackSkin(string kind)
        {
            bool rare=Chance(Policy("LeaveChairData."+(kind=="cookie"?"Cookie":"Cupcake")+"RareProbability",30));
            var choices=Features.snacks.Where(s=>s.kind==kind&&s.rarity==(rare?1:0)).ToArray();if(choices.Length==0)choices=Features.snacks.Where(s=>s.kind==kind).ToArray();if(choices.Length==0)yield break;
            var skin=choices[ChillRuntimeContext.Range(0,choices.Length)];
            for(int i=0;i<skin.parts.Length;i++){
                var part=skin.parts[i];var target=FindObjectsOfType<Transform>(true).FirstOrDefault(t=>TransformPath(t)==part.path);if(!target)continue;
                UnityEngine.Object mesh=null,material=null;bool done=false;
                player.library.LoadExternal("snack-mesh-"+i,part.mesh,o=>{mesh=o;done=true;});while(!done&&!Playing)yield return null;if(Playing)yield break;done=false;
                player.library.LoadExternal("snack-material-"+i,part.material,o=>{material=o;done=true;});while(!done&&!Playing)yield return null;if(Playing)yield break;
                var filter=target.GetComponent<MeshFilter>();if(filter&&mesh is Mesh m){filter.sharedMesh=null;filter.sharedMesh=ChillDecorations.SanitizeMesh(m);ChillDecorations.RefreshRenderer(filter.gameObject,player);}var renderer=target.GetComponent<Renderer>();if(renderer&&material is Material mat)renderer.sharedMaterial=mat;
            }
        }
        static string TransformPath(Transform t)=>t.parent?TransformPath(t.parent)+"/"+t.name:t.name;
    }
}
