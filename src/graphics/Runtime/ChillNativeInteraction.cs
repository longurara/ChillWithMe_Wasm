using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace Hlc.ChillPrototype
{
    // Reuses InteractableUI's source image arrays; replaces its R3/DOTween
    // pointer subscription with Unity events and immediate final alpha states.
    public sealed class ChillNativeInteraction : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler
    {
        public Graphic[] hover,use,baseline;
        public float hoverAlpha=1,useAlpha=1,baseAlpha=1;
        public bool hideHoverOnUse;
        bool over,usingUI;
        public void SetUsing(bool value){usingUI=value;Apply();}
        public void DeactivateAll(){over=usingUI=false;Apply();}
        public void OnPointerEnter(PointerEventData e){if(e==null||e.pointerId<0)Enter();}
        void Enter(){var button=GetComponent<Button>();over=!button || button.IsInteractable();Apply();}
        public void OnPointerExit(PointerEventData e){over=false;Apply();}
        public void OnPointerDown(PointerEventData e){if(e!=null&&e.pointerId>=0)Enter();}
        public void OnPointerUp(PointerEventData e){if(e!=null&&e.pointerId>=0){over=false;Apply();}}
        void OnEnable(){Apply();}
        void OnDisable(){over=false;Apply();}
        void Apply()
        {
            // Source hover/use arrays can contain the same Image. An inactive
            // layer must not erase a visible layer's alpha on that shared asset.
            var layers=new Dictionary<Graphic,float>();
            void Layer(Graphic[] images,float alpha){if(images==null)return;foreach(var image in images)if(image)layers[image]=layers.TryGetValue(image,out float existing)?Mathf.Max(existing,alpha):alpha;}
            Layer(hover,over && !(hideHoverOnUse && usingUI)?hoverAlpha:0);Layer(use,usingUI?useAlpha:0);Layer(baseline,!over && !usingUI?baseAlpha:0);
            foreach(var pair in layers){var color=pair.Key.color;color.a=pair.Value;pair.Key.color=color;}
        }
    }
}
