using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillNativeUI
    {
        ChillNativeTooltip tooltip;
        ChillTimerGuide firstGuide;
        bool guideArmed=true,previousLongStory;
        void InitializeTooltips(Configuration config)
        {
            if(config.firstTimerGuide!=null){var focus=Find(config.firstTimerGuide.focus)?.GetComponent<Graphic>();if(focus){firstGuide=gameObject.GetComponent<ChillTimerGuide>()??gameObject.AddComponent<ChillTimerGuide>();firstGuide.image=focus;firstGuide.options=config.firstTimerGuide;firstGuide.Configure();}}
            var options=config.tooltipPresentation;if(options==null)return;
            var root=Find(options.root);var text=Find(options.text);if(!root||!text)return;
            root.SetParent(canvas.transform,false);root.SetAsLastSibling();
            DisableLayouts(root);
            tooltip=root.GetComponent<ChillNativeTooltip>()??root.gameObject.AddComponent<ChillNativeTooltip>();tooltip.story=player.story;tooltip.canvas=canvas;tooltip.panel=(RectTransform)root;tooltip.label=text.GetComponent<TMP_Text>();tooltip.options=options;
            root.gameObject.SetActive(true);var tipGroup=root.GetComponent<CanvasGroup>()??root.gameObject.AddComponent<CanvasGroup>();tipGroup.alpha=0;tipGroup.blocksRaycasts=false;
            tooltip.Configure();
            foreach(var graphic in root.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
            var labelRect=(RectTransform)tooltip.label.transform;labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.offsetMin=new Vector2(9,8);labelRect.offsetMax=new Vector2(-9,-8);tooltip.label.enableWordWrapping=true;
            foreach(var binding in config.tooltips??Array.Empty<Tooltip>()){var target=Find(binding.path);if(!target)continue;var handler=target.GetComponent<ChillTooltipTarget>()??target.gameObject.AddComponent<ChillTooltipTarget>();handler.tooltip=tooltip;handler.key=binding.key;handler.direct=binding.direct;}
        }
        void SetPaneUsing(string selected)
        {
            foreach(var pair in new[]{("settings","SettingUI"),("story","StorySelectUI"),("music","MusicPlayListView"),("notes","NoteUI"),("todos","TodoListUI"),("calendar","CalendarUI"),("habits","HabitTrackerUI"),("environment","EnvironmentUI"),("decorations","DecorationListUI")})
            {
                var button=RefOf(pair.Item2,"_facilityOpenButton");
                var style=button?button.GetComponent<ChillNativeInteraction>():null;
                bool usingPane=IsFloatingPane(pair.Item1)?activeFloatingPanes.Contains(pair.Item1):(exclusivePane==pair.Item1||selected==pair.Item1);
                if(style)style.SetUsing(usingPane);
            }
        }
        void RefreshFirstGuide()
        {
            if(!firstGuide||!state.ready)return;
            bool longStory=state.playing&&!state.automaticDialogue;
            if(state.timerRunning||state.countupRunning)guideArmed=false;
            if(previousLongStory&&!longStory)guideArmed=true;previousLongStory=longStory;
            firstGuide.SetVisible(guideArmed&&player.story.RoomLevel<2&&!longStory&&!state.timerRunning&&!state.countupRunning);
        }
    }
}
