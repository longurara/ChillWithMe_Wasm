using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        class ActionWeight{public string key,add,subtract,cooldown,delay;public string[] motions;public float amount;public double available;public int previous=-1;}
        string weightGroup="";float baseWeight;readonly List<ActionWeight> actionWeights=new List<ActionWeight>();
        ActionWeight Weight(string key,string[] motions,string add=null,string subtract=null,string cooldown=null,string delay=null)
        =>new ActionWeight{key=key,motions=motions,add=add??key+"ChangingAmount",subtract=subtract??key+"SubtractRatio",cooldown=cooldown??key+"InvalidSeconds",delay=delay??key};
        void SetWeightGroup(string group)
        {
            if(weightGroup==group)return;weightGroup=group;baseWeight=Policy(group+".InitBaseAmount",10000);actionWeights.Clear();
            if(group=="HeroineStateWorkPC")actionWeights.AddRange(new[]{Weight("Problem",new[]{"WorkBase001_Thinking","WorkBase001_Stop"}),Weight("Fatigue",new[]{"WorkBase001_Stop"}),Weight("DrinkTea",new[]{"WorkBase001_DrinkTea"}),Weight("Break",new[]{"WorkBase001_Break","WorkBase001_Break2"})});
            else if(group=="HeroineStateWorkBook")actionWeights.AddRange(new[]{Weight("Problem",new[]{"WorkBase002_Thinking"}),Weight("Understand",new[]{"WorkBase002_PageFlip"}),Weight("Output",new[]{"WorkBase002_KeyType"}),Weight("Loop2",new[]{"WorkBase002_Loop2"}),Weight("DrinkTea",new[]{"WorkBase002_DrinkTea"})});
            else if(group=="HeroineStateWorkReport")actionWeights.AddRange(new[]{Weight("SmallProblem",new[]{"WorkBase003_SmallThinking"}),Weight("BigProblem",new[]{"WorkBase003_BigThinking"}),Weight("Loop2",new[]{"WorkBase003_Loop2"}),Weight("DrinkTea",new[]{"WorkBase003_DrinkTea"}),Weight("BrushAwayTrash",new[]{"WorkBase003_BrushAwayTrash"})});
            else if(group=="HeroineStateBreakMovie")actionWeights.AddRange(new[]{Weight("Enjoy",new[]{"BreakBase001_Laugh"},"EnjoyAddRatio"),Weight("Suspenseful",new[]{"BreakBase001_Suspenseful"},"SuspensefulAddRatio"),Weight("ToMindTrash",new[]{"BreakBase001_OutTrash"},"ToMindTrashAddRatio"),Weight("Loop2",new[]{"BreakBase001_Loop2"})});
            else if(group=="HeroineStateBreakReadBook")actionWeights.AddRange(new[]{Weight("NextPage",new[]{"BreakBase002_NextPage"},"WantNextPageAddRatio"),Weight("PreviousPage",new[]{"BreakBase002_PreviousPage"},"WantPreviousPageAddRatio",cooldown:"PreviousInvalidSeconds"),Weight("Interest",new[]{"BreakBase002_Interest"},"InterestAddRatio")});
            else if(group=="HeroineStateBreakTeaTime")actionWeights.Add(Weight("Drink",new[]{"BreakBase004_DrinkTea"},"WantDrinkAddRatio"));
            else if(group=="HeroineStateBreakForward")actionWeights.AddRange(new[]{Weight("Next",new[]{"BreakBase006_Keyboard"},"NextAddRatio"),Weight("Suspenseful",new[]{"BreakBase006_Interest"},"SuspensefulAddRatio"),Weight("PlayPen",new[]{"BreakBase006_PlayPenLoop"},"PlayPenAddRatio")});
            else if(group=="HeroineStateLeaveChairGoSofa")
            {
                baseWeight=Policy(group+"._initBaseAmount",10000);
                actionWeights.AddRange(new[]{Weight("Thinking",new[]{"Wild003_LeaveChair_Sofa_Thinking"},"_thinkingChangingAmount","_thinkingSubtractRatio","_thinkingInvalidSeconds","_thinking"),Weight("LookOutside",new[]{"Wild003_LeaveChair_Sofa_Look_Outside"},"_lookOutsideChangingAmount","_lookOutsideSubtractRatio","_lookOutsideInvalidSeconds","_lookOutside"),Weight("ControlHeadphone",new[]{"Wild003_LeaveChair_Sofa_Control_Headphone"},"_controlHeadphoneChangingAmount","_controlHeadphoneSubtractRatio","_controlHeadphoneInvalidSeconds","_controlHeadphone")});
            }
            foreach(var weight in actionWeights)weight.available=Now+(weight.key=="Break"?Policy(group+".StateStartBreakInvalidSeconds",0):0);
        }
        string WeightedVariant()
        {
            return PickWeightedVariant(Resting?breakGroups[Mathf.Max(0,breakMode)]:workGroups[player.SelectedState]);
        }
        string PickWeightedVariant(string group)
        {
            SetWeightGroup(group);
            if(actionWeights.Count==0)return null;
            float draw=ChillRuntimeContext.Value*(baseWeight+actionWeights.Sum(w=>w.amount));
            if(draw<baseWeight)
            {
                float original=baseWeight;foreach(var weight in actionWeights){if(Now<weight.available)continue;float transfer=original*Mathf.Clamp01(Policy(group+"."+weight.add,0));weight.amount+=transfer;if(group!="HeroineStateWorkPC"||weight.key!="DrinkTea")baseWeight-=transfer;}baseWeight=Mathf.Max(0,baseWeight);return null;
            }
            draw-=baseWeight;var selected=actionWeights.Last();foreach(var weight in actionWeights){if(draw<weight.amount){selected=weight;break;}draw-=weight.amount;}
            if(Now<selected.available)return null;
            float returned=selected.amount*Mathf.Clamp01(Policy(group+"."+selected.subtract,1));selected.amount-=returned;baseWeight+=returned;selected.available=Now+Policy(group+"."+selected.cooldown,0);
            nextVariant=Now+Delay(group+"."+selected.delay+"DelaySecondsMin",group+"."+selected.delay+"DelaySecondsMax",30,60);
            if(selected.key=="DrinkTea"||selected.key=="Drink")
            {
                if(activityObjects.Empty)return activityObjects.NeedHot(Now,ChillRuntimeContext.Value*100,Policy("LeaveChairData.ColdDrinkPourProbability",50))?"Wild001_Motion10_UseCoffeeMaker":null;
                if(group=="HeroineStateBreakTeaTime"&&activityObjects.Hot(Now)&&Chance(100*Policy(group+".DrinkHotTeaProbability",.1f)))return "BreakBase004_DrinkHot";
            }
            selected.previous=(selected.previous+1)%selected.motions.Length;return selected.motions[selected.previous];
        }
    }
}
