using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace Hlc.ChillPrototype
{
    // Reuses InteractableUI's source image arrays; replaces its R3/DOTween
    // pointer subscription with Unity events and its .15/.2-second alpha fades.
    public sealed class ChillNativeInteraction : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler
    {
        public Graphic[] hover,use,baseline;
        public float hoverAlpha=1,useAlpha=1,baseAlpha=1;
        public bool hideHoverOnUse;
        bool over,usingUI;
        sealed class Fade{public float from,to,time,duration;}
        readonly Dictionary<Graphic,Fade> fades=new Dictionary<Graphic,Fade>();
        void Update(){Advance(Time.unscaledDeltaTime);}
        public void Advance(float seconds)
        {
            foreach(var pair in fades){if(!pair.Key)continue;var f=pair.Value;f.time+=seconds;float t=Mathf.Clamp01(f.time/f.duration);t=1-(1-t)*(1-t);var c=pair.Key.color;c.a=Mathf.Lerp(f.from,f.to,t);pair.Key.color=c;}
        }
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
            foreach(var pair in layers)
            {
                var color=pair.Key.color;
                if(!Application.isPlaying||!isActiveAndEnabled){color.a=pair.Value;pair.Key.color=color;fades.Remove(pair.Key);}
                else if(!fades.TryGetValue(pair.Key,out var prior)||prior.to!=pair.Value)fades[pair.Key]=new Fade{from=color.a,to=pair.Value,duration=pair.Value>color.a?.15f:.2f};
            }
        }
    }
}
