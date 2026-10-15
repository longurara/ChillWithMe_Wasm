using UnityEngine;
using UnityEngine.EventSystems;
namespace Hlc.ChillPrototype
{
    public sealed class ChillNativeRowDrag:MonoBehaviour,IBeginDragHandler,IEndDragHandler,IDragHandler
    {
        public System.Action<int> moved;
        public float rowHeight=78f;
        Vector2 start;
        public void OnBeginDrag(PointerEventData pointer){start=pointer.position;}
        public void OnDrag(PointerEventData pointer)
        {
            var canvas=GetComponentInParent<Canvas>();float unit=(canvas?canvas.scaleFactor:1f)*Mathf.Max(20f,rowHeight);
            int step=Mathf.RoundToInt((start.y-pointer.position.y)/unit);
            if(step!=0){start.y-=step*unit;moved?.Invoke(step);}
        }
        public void OnEndDrag(PointerEventData pointer)
        {
            var canvas=GetComponentInParent<Canvas>();float unit=(canvas?canvas.scaleFactor:1f)*Mathf.Max(20f,rowHeight);
            int step=Mathf.RoundToInt((start.y-pointer.position.y)/unit);
            if(step!=0){start=pointer.position;moved?.Invoke(step);}
        }
    }
}
