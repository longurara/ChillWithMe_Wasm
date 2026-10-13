using UnityEngine;
using UnityEngine.UI;
namespace Hlc.ChillPrototype
{
    public sealed class ChillTimerGuide:MonoBehaviour
    {
        public Graphic image;
        public ChillNativeUI.TimerGuide options;
        Vector3 originalScale;
        float elapsed,fadeElapsed,from,target;
        bool visible;
        public void Configure(){originalScale=image.transform.localScale;var c=image.color;c.a=0;image.color=c;image.raycastTarget=false;image.gameObject.SetActive(false);}
        public void SetVisible(bool value)
        {
            if(value==visible)return;visible=value;fadeElapsed=0;from=image.color.a;target=value?1:0;
            if(value){elapsed=0;image.gameObject.SetActive(true);}
        }
        void Update(){Advance(Time.deltaTime);}
        public void Advance(float seconds)
        {
            if(!image||!image.gameObject.activeSelf)return;elapsed+=seconds;fadeElapsed+=seconds;
            var c=image.color;float t=Mathf.Clamp01(fadeElapsed/Mathf.Max(.001f,visible?options.fadeIn:options.fadeOut));t=1-(1-t)*(1-t);c.a=Mathf.Lerp(from,target,t);image.color=c;
            image.transform.localRotation=Quaternion.Euler(0,0,-360*elapsed/options.rotate);
            image.transform.localScale=Vector3.Scale(originalScale,new Vector3(Mathf.Lerp(1,options.multiple,Mathf.PingPong(elapsed/options.scale,1)),Mathf.Lerp(1,options.multiple,Mathf.PingPong(elapsed/options.scale,1)),1));
            if(!visible&&t>=1){image.gameObject.SetActive(false);image.transform.localRotation=Quaternion.identity;image.transform.localScale=originalScale;}
        }
    }
}
