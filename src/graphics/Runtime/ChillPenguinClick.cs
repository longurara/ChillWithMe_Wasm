using UnityEngine;
using UnityEngine.EventSystems;

namespace Hlc.ChillPrototype
{
    [RequireComponent(typeof(Collider))]
    public sealed class ChillPenguinClick : MonoBehaviour
    {
        public ChillStoryRuntime story;
        void OnMouseDown()
        {
            if(!story || story.Playing)return;
            if(EventSystem.current && (EventSystem.current.IsPointerOverGameObject() ||
                Input.touchCount>0&&EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)))return;
            story.ClickPenguin();
        }
    }
}
