using UnityEngine;
using UnityEngine.EventSystems;

namespace Hlc.ChillPrototype
{
    public sealed class ChillNativeDrag : MonoBehaviour,IDragHandler,IEndDragHandler
    {
        public RectTransform target;
        public System.Action<Vector2> saved;
        public void OnEndDrag(PointerEventData pointer){if(target){target.anchoredPosition=AdjustPos(target,target.anchoredPosition);saved?.Invoke(target.anchoredPosition);}}
        public void OnDrag(PointerEventData pointer)
        {if(target){var canvas=target.GetComponentInParent<Canvas>();target.anchoredPosition=AdjustPos(target,target.anchoredPosition+pointer.delta/(canvas?canvas.scaleFactor:1));}}
        public static Vector2 AdjustPos(RectTransform rect,Vector2 pos)
        {
            if(!rect)return pos;
            var parent=rect.parent as RectTransform;
            var bounds=parent&&parent.rect.width>0&&parent.rect.height>0?parent.rect:new Rect(-960,-540,1920,1080);
            float halfW=Mathf.Min(rect.rect.width*.5f,bounds.width*.5f),halfH=Mathf.Min(rect.rect.height*.5f,bounds.height*.5f);
            float minX=bounds.xMin+halfW,maxX=bounds.xMax-halfW,minY=bounds.yMin+halfH,maxY=bounds.yMax-halfH;
            return new Vector2(minX<=maxX?Mathf.Clamp(pos.x,minX,maxX):pos.x,minY<=maxY?Mathf.Clamp(pos.y,minY,maxY):pos.y);
        }
    }
}
