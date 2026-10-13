using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace Hlc.ChillPrototype
{
    public sealed class ChillWardrobe:MonoBehaviour
    {
        public GameObject[] defaults,prefabs;
        public Material[] skins;
        public Transform costumeParent,boneParent;
        GameObject current;
        readonly List<GameObject> attached=new List<GameObject>();
        int index;
        ChillStoryPresentation.Sleeve[] baseSleeves;
        public void Apply(int selection)
        {
            if(selection==index)return;
            var presentation=GetComponent<ChillStoryRuntime>()?.presentation;if(baseSleeves==null&&presentation)baseSleeves=presentation.sleeves;
            if(presentation)presentation.sleeves=baseSleeves;
            if(selection<0||selection>4||!costumeParent||!boneParent)return;
            if(selection>0&&(prefabs==null||prefabs.Length<2||!prefabs[(selection-1)/2]||skins==null||skins.Length<selection||!skins[selection-1]))return;
            foreach(var go in attached)if(go){go.SetActive(false);if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}attached.Clear();
            if(current){current.SetActive(false);if(Application.isPlaying)Destroy(current);else DestroyImmediate(current);}foreach(var go in defaults??Array.Empty<GameObject>())if(go)go.SetActive(selection==0);index=selection;if(selection==0)return;
            var bones=boneParent.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            current=Instantiate(prefabs[(selection-1)/2],costumeParent,false);current.transform.localPosition=Vector3.zero;current.transform.localRotation=Quaternion.identity;
            var character=current.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Character");
            if(character){var children=character.Cast<Transform>().ToArray();character.SetParent(boneParent);character.localPosition=Vector3.zero;character.localRotation=Quaternion.identity;attached.Add(character.gameObject);foreach(var child in children)Attach(child);}
            void Attach(Transform bone){var children=bone.Cast<Transform>().ToArray();attached.Add(bone.gameObject);if(bones.TryGetValue(bone.name,out var mapped)&&bone!=mapped){bone.SetParent(mapped);bone.localPosition=Vector3.zero;bone.localRotation=Quaternion.identity;}foreach(var child in children)Attach(child);}
            foreach(var renderer in current.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(renderer.name==((selection-1)/2==0?"Top3":"Top2")){
                    renderer.sharedMaterial=skins[selection-1];
                    if(presentation){var sleeve=attached.Where(g=>g&&g.name=="Character_LeftSode").Select(g=>g.transform).FirstOrDefault();presentation.sleeves=(baseSleeves??Array.Empty<ChillStoryPresentation.Sleeve>()).Concat(new[]{new ChillStoryPresentation.Sleeve{renderer=renderer,bone=sleeve}}).ToArray();}
                }
            }
        }
    }
}
