using UnityEngine;
using UnityEngine.EventSystems;
namespace Hlc.ChillPrototype
{
    public sealed class ChillNativeRowDrag:MonoBehaviour,IBeginDragHandler,IEndDragHandler,IDragHandler
    {
        public System.Action<int> moved;Vector2 start;
        public void OnBeginDrag(PointerEventData pointer){start=pointer.position;}
        public void OnDrag(PointerEventData pointer){}
        public void OnEndDrag(PointerEventData pointer){var canvas=GetComponentInParent<Canvas>();int step=Mathf.RoundToInt((start.y-pointer.position.y)/((canvas?canvas.scaleFactor:1)*78));if(step!=0)moved?.Invoke(step);}
    }
}
