using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Hlc.ChillPrototype
{
    // Reuses InteractableUI's source image arrays; replaces its R3/DOTween
    // pointer subscription with Unity events and immediate final alpha states.
    public sealed class ChillNativeInteraction : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        public Graphic[] hover,use,baseline;
        public float hoverAlpha=1,useAlpha=1,baseAlpha=1;
        public bool hideHoverOnUse;
        bool over,usingUI;
        public void SetUsing(bool value){usingUI=value;Apply();}
        public void OnPointerEnter(PointerEventData e){var button=GetComponent<Button>();over=!button || button.IsInteractable();Apply();}
        public void OnPointerExit(PointerEventData e){over=false;Apply();}
        void OnDisable(){over=false;}
        void Apply()
        {Alpha(hover,over && !(hideHoverOnUse && usingUI)?hoverAlpha:0);Alpha(use,usingUI?useAlpha:0);Alpha(baseline,!over && !usingUI?baseAlpha:0);}
        static void Alpha(Graphic[] images,float alpha){if(images==null)return;foreach(var image in images)if(image){var color=image.color;color.a=alpha;image.color=color;}}
    }
}
