using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using Hlc.ChillPrototype;
public static class ChillHoverCheck
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    public static void Run(ChillStoryRuntime story,ChillNativeUI ui)
    {
        var config=JsonUtility.FromJson<ChillNativeUI.Configuration>(ui.configuration.text);string[] keys={"ui_guide_note","ui_guide_todo","ui_guide_habit","ui_guide_calendar","ui_guide_display_switching","ui_guide_setting","ui_guide_end_game","ui_guide_conversation_log","ui_guide_decoration_list","ui_guide_enviroment_list","ui_guide_music"};
        Require(config.interactions.Length>500&&config.tooltips.Length>100,"Only timer hover bindings were extracted");
        int changed=0;
        foreach(var key in keys)
        {
            var binding=config.tooltips.First(t=>t.key==key);var target=ui.Find(binding.path);var marker=target.GetComponent<ChillTooltipTarget>();Require(marker&&marker.tooltip,"Toolbar tooltip is missing: "+key);
            var style=target.GetComponent<ChillNativeInteraction>()??target.GetComponentInChildren<ChillNativeInteraction>(true);Require(style&&style.hover.Length>0,"Toolbar has no original hover graphics: "+key);
            style.SetUsing(false);style.OnPointerExit(null);var before=style.hover.Select(g=>g.color.a).ToArray();style.OnPointerEnter(new PointerEventData(null){pointerId=-1});Require(style.hover.Select((g,i)=>g.color.a>before[i]+.01f).Any(v=>v),"Toolbar hover does not reveal its glow: "+key);changed++;style.OnPointerExit(null);
        }
        var tooltip=ui.canvas.GetComponentInChildren<ChillNativeTooltip>(true);var group=tooltip.GetComponent<CanvasGroup>();tooltip.Dismiss();tooltip.Advance(1);
        var first=ui.Find(config.tooltips.First(t=>t.key=="ui_guide_note").path).GetComponent<ChillTooltipTarget>();tooltip.Begin(first);tooltip.Advance(.1f);Require(group.alpha<.01f,"Tooltip skips the source hover delay");tooltip.Advance(.13f);tooltip.Advance(.2f);Require(group.alpha>.99f&&!tooltip.label.text.StartsWith("ui_"),"Tooltip does not show localized source text");Require(!group.blocksRaycasts,"Tooltip intercepts its own hovered button");tooltip.End(first);tooltip.Advance(.19f);Require(group.alpha>.99f,"Tooltip skips its exit delay");tooltip.Advance(.03f);tooltip.Advance(.2f);Require(group.alpha<.01f,"Tooltip stays visible after leaving button");
        var guide=ui.GetComponent<ChillTimerGuide>();Require(guide&&guide.image is UnityEngine.UI.Image ringImage&&ringImage.sprite,"Source green timer-guide sprite is missing");guide.SetVisible(false);guide.Advance(1);float baseScale=guide.image.transform.localScale.x;guide.SetVisible(true);guide.Advance(.25f);Require(guide.image.color.a>0&&guide.image.color.a<1,"Timer guide has no .5-second fade");var rotation=guide.image.transform.localRotation;guide.Advance(.45f);Require(Quaternion.Angle(rotation,guide.image.transform.localRotation)>80,"Green timer guide does not rotate");Require(guide.image.transform.localScale.x>baseScale&&guide.image.transform.localScale.x<=baseScale*1.131f,"Timer guide scale pulse differs from source");guide.SetVisible(false);guide.Advance(.5f);Require(!guide.image.gameObject.activeSelf,"Timer guide never deactivates");
        File.WriteAllText(Path.GetFullPath("../research/hover-check.json"),"{\"passed\":true,\"hoverBindings\":"+config.interactions.Length+",\"tooltipTargets\":"+config.tooltips.Length+",\"toolbarButtons\":"+changed+",\"nativeTooltipDelayAndDismiss\":true,\"greenGuideRotationFadePulse\":true,\"userSaveWritten\":false,\"browserGameplayRun\":false}");Debug.Log("HLC_HOVER_CHECK_PASSED buttons="+changed);
    }
}
