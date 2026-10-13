using UnityEngine;
using UnityEngine.EventSystems;

namespace Hlc.ChillPrototype
{
    public sealed class ChillNativeDrag : MonoBehaviour,IDragHandler,IEndDragHandler
    {
        public RectTransform target;
        public System.Action<Vector2> saved;
        public void OnEndDrag(PointerEventData pointer){if(target)saved?.Invoke(target.anchoredPosition);}
        public void OnDrag(PointerEventData pointer)
        {if(target){var canvas=target.GetComponentInParent<Canvas>();target.anchoredPosition+=pointer.delta/(canvas?canvas.scaleFactor:1);}}
    }
}
