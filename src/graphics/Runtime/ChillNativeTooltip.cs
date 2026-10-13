using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
namespace Hlc.ChillPrototype
{
    public sealed class ChillNativeTooltip:MonoBehaviour
    {
        public ChillStoryRuntime story;
        public Canvas canvas;
        public RectTransform panel;
        public TMP_Text label;
        public ChillNativeUI.TooltipPresentation options;
        CanvasGroup group;
        ChillTooltipTarget target;
        float delay,fadeTime,fromAlpha,toAlpha;
        bool waiting,show;
        void Awake(){Configure();}
        public void Configure(){group=GetComponent<CanvasGroup>()??gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;}
        public void Begin(ChillTooltipTarget value)
        {
            target=value;delay=0;waiting=true;show=true;
            label.text=value.direct?value.key:story.UITranslate(value.key);
        }
        public void End(ChillTooltipTarget value){if(value!=target)return;target=null;delay=0;waiting=true;show=false;}
        public void Dismiss(){target=null;waiting=false;show=false;Fade(false);}
        void Fade(bool visible){fromAlpha=group.alpha;toAlpha=visible?1:0;fadeTime=0;if(visible)panel.gameObject.SetActive(true);}
        void Update()
        {
            if(Input.GetMouseButtonUp(0)){Dismiss();return;}
            Advance(Time.unscaledDeltaTime);
        }
        public void Advance(float seconds)
        {
            if(waiting){delay+=seconds;if(delay>=(show?options.activateDelay:options.deactivateDelay)){waiting=false;Fade(show);}}
            fadeTime+=seconds;float t=Mathf.Clamp01(fadeTime/Mathf.Max(.001f,toAlpha>0?options.fadeIn:options.fadeOut));t=1-(1-t)*(1-t);group.alpha=Mathf.Lerp(fromAlpha,toAlpha,t);
            if(group.alpha<=0)return;
            var space=(RectTransform)canvas.transform;RectTransformUtility.ScreenPointToLocalPointInRectangle(space,Input.mousePosition,null,out var position);
            label.ForceMeshUpdate();float width=Mathf.Clamp(label.preferredWidth+18,80,500);float height=Mathf.Max(34,label.GetPreferredValues(label.text,width-18,0).y+16);panel.sizeDelta=new Vector2(width,height);panel.pivot=Vector2.zero;panel.anchorMin=panel.anchorMax=new Vector2(.5f,.5f);
            position+=new Vector2(options.offset[0],options.offset[1]);position.x=Mathf.Clamp(position.x,space.rect.xMin+2,space.rect.xMax-width-2);position.y=Mathf.Clamp(position.y,space.rect.yMin+2,space.rect.yMax-height-2);panel.anchoredPosition=position;
            panel.SetAsLastSibling();
        }
    }
    public sealed class ChillTooltipTarget:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        public ChillNativeTooltip tooltip;public string key;public bool direct;
        public void OnPointerEnter(PointerEventData data){if(data.pointerId<0&&tooltip)tooltip.Begin(this);}
        public void OnPointerExit(PointerEventData data){if(tooltip)tooltip.End(this);}
        void OnDisable(){if(tooltip)tooltip.End(this);}
    }
}
