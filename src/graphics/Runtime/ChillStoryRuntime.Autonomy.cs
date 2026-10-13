using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        public bool Resting=>Settings.resting || PomodoroActive && save.breakTime;
        public string AutonomyState {get;private set;}="Work";
        double nextAction,nextVariant,nextSelfTalk,nextLeave,nextLeaveLottery,nextWild,clickCooldown;
        int breakMode=-1,selfTalkCount,previousMode=-1;
        bool previousRest,autonomyInstalled;
        Coroutine autonomousSequence;
        readonly List<int> breakBag=new List<int>(),workBag=new List<int>();
        readonly Dictionary<int,List<string>> reactionBags=new Dictionary<int,List<string>>();
        static readonly string[] breakGroups={"HeroineStateBreakMovie","HeroineStateBreakReadBook","HeroineStateBreakListenMusic","HeroineStateBreakTeaTime","HeroineStateBreakSleep","HeroineStateBreakForward"};
        static readonly string[] breakBases={"BreakBase001","BreakBase002","BreakBase003_ListenMusicLow","BreakBase004","BreakBase005","BreakBase006"};
        static readonly string[] workGroups={"HeroineStateWorkBook","HeroineStateWorkReport","HeroineStateWorkPC"};
        static readonly string[] workBases={"WorkBase002","WorkBase003","WorkBase001"};
        static readonly string[] durationKeys={"Movie","Book","Music","Tea","Sleep","Forward"};
        float Policy(string key,float fallback)=>Features?.policies.FirstOrDefault(p=>p.name==key)?.value??fallback;
        double Delay(string min,string max,float fallbackMin,float fallbackMax)=>UnityEngine.Random.Range(Policy(min,fallbackMin),Policy(max,fallbackMax));
        bool Chance(float percent)=>UnityEngine.Random.value*100<percent;
        void InitializeAutonomy()
        {
            nextLeave=Now+60*Delay("LeaveChairData.FirstWorkMinutesMin","LeaveChairData.FirstWorkMinutesMax",20,60);
            nextLeaveLottery=nextLeave;nextWild=Now;ScheduleSelfTalk();ResetAutonomy();
        }
        public void ResetAutonomy()
        {
            if(autonomousSequence!=null){StopCoroutine(autonomousSequence);autonomousSequence=null;}
            autonomyInstalled=false;previousMode=-1;breakMode=-1;nextAction=nextVariant=0;weightGroup="";
            if(player && player.deskObjects)player.deskObjects.restoring=false;
        }
        void ScheduleSelfTalk()=>nextSelfTalk=Now+Delay("_talkLotteryDelaySecondsMin","_talkLotteryDelaySecondsMax",30,160);
        bool SetNativeMotion(string name)
        {
            var motion=data.motions.FirstOrDefault(m=>m.name==name);
            if(motion==null || !presentation.statePaths.Any(s=>s.motion==motion.value))return false;
            if(player.character.runtimeAnimatorController!=storyController){player.character.runtimeAnimatorController=storyController;player.character.Rebind();}
            player.StopAnimationAudio();
            presentation.Motion(new ChillSentence{BodyMotion=motion.value,FacialMotion=0,LookScale=0},name,false);
            AutonomyState=name;return true;
        }
        void InstallAutonomy()
        {
            if(!storyController)return;
            player.character.runtimeAnimatorController=storyController;player.character.Rebind();
            player.deskObjects.Select(player.SelectedState);autonomyInstalled=true;
            previousMode=player.SelectedState;previousRest=Resting;
            if(Resting)SelectBreak();else SetNativeMotion(workBases[player.SelectedState]);
            nextAction=Now+Policy("_changeWorkLayoutDelaySeconds",300);
            nextVariant=Now+VariantDelay();
        }
        void UpdateAutonomy()
        {
            bool pointer=Input.GetMouseButtonDown(0) || Input.touchCount>0&&Input.GetTouch(0).phase==TouchPhase.Began;
            if(pointer)TrySatoneClick();
            if(Playing)return;
            if(!Settings.autonomy)
            {if(autonomyInstalled){ResetAutonomy();player.character.runtimeAnimatorController=deskController;player.character.Rebind();player.PlayState(save.deskMode);}return;}
            if(!autonomyInstalled || previousMode!=player.SelectedState || previousRest!=Resting)
            {ResetAutonomy();InstallAutonomy();selfTalkCount=0;ScheduleSelfTalk();}
            if(autonomousSequence!=null)return;
            if(Now>=nextLeaveLottery)
            {
                nextLeaveLottery=Now+Policy("LeaveChairData.LotteryIntervalMinutes",5)*60;
                if(Now>=nextLeave && Chance(Policy("LeaveChairData.OccurrenceProbabilityPercent",30)))
                {autonomousSequence=StartCoroutine(LeaveChair());return;}
            }
            if(Resting)
            {
                if(Now>=nextAction){SelectBreak();return;}
                if(Settings.selfTalk && Workspace.profileStatus!="2" && breakMode!=4 && Now>=nextSelfTalk)
                {ScheduleSelfTalk();if(selfTalkCount<Policy("_oneBreakSelfTalkMax",4) && Chance(Policy("_selfTalkProbability",75))){if(StartReaction(Chance(50)?21:2000+TimeOfDayIndex()))selfTalkCount++;}}
            }
            else if(Now>=nextAction)
            {
                nextAction=Now+Policy("_changeWorkLayoutDelaySeconds",300);
                if(Chance(Policy("_changeWorkLayoutRatio",50)))
                {
                    if(workBag.Count==0)workBag.AddRange(Enumerable.Range(0,3).Where(i=>i!=player.SelectedState).OrderBy(_=>UnityEngine.Random.value));
                    int selected=workBag[0];workBag.RemoveAt(0);autonomousSequence=StartCoroutine(ChangeWork(selected));return;
                }
            }
            if(Now>=nextVariant)
            {
                nextVariant=Now+VariantDelay();
                if(Now>=nextWild && Chance(Policy("WildMotionOccurrenceProbability",25)) && breakMode!=4)
                {nextWild=Now+Policy("WildMotionDelayMinutes",30)*60;autonomousSequence=StartCoroutine(WildAction());return;}
                var variant=WeightedVariant();if(!string.IsNullOrEmpty(variant))autonomousSequence=StartCoroutine(Variant(variant));
            }
        }
        float VariantDelay()
        {
            string group=Resting?breakGroups[Mathf.Max(0,breakMode)]:workGroups[player.SelectedState];
            return Mathf.Max(1,(float)Delay(group+".BaseDelaySecondsMin",group+".BaseDelaySecondsMax",30,60));
        }
        int TimeOfDayIndex(){int h=DateTime.Now.Hour;return h>5&&h<11?0:h>=11&&h<=16?1:h>=17&&h<=19?2:3;}
        bool StartReaction(int scenario)
        {
            if(!reactionBags.TryGetValue(scenario,out var bag))reactionBags[scenario]=bag=new List<string>();
            if(bag.Count==0)bag.AddRange(data.groups.Where(g=>g.Scenario==scenario&&Unlocked(g)&&chapters.ContainsKey(g.ID)).OrderBy(_=>UnityEngine.Random.value).Select(g=>g.ID));
            if(bag.Count==0)return false;string id=bag[0];bag.RemoveAt(0);
            if(Playing)return false;var item=data.groups.First(g=>g.ID==id);
            suspendedChapter=save.chapter;suspendedCursor=save.cursor;suspendedScene=new List<int>(save.sceneHistory);
            Begin(item,false,true);return true;
        }
        void TrySatoneClick()
        {
            if(Playing && tutorialCommand!=14 && tutorialCommand!=50)return;
            if(Now<clickCooldown)return;
            if(tutorialCommand!=14 && tutorialCommand!=50 && EventSystem.current && (EventSystem.current.IsPointerOverGameObject() || Input.touchCount>0&&EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)))return;
            Vector3 point=Input.touchCount>0?(Vector3)Input.GetTouch(0).position:Input.mousePosition;
            var ray=player.view.ScreenPointToRay(point);
            bool hit=player.character.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.enabled&&r.bounds.IntersectRay(ray,out float distance)&&distance>0);
            if(!hit)return;TutorialSignal("satone");if(Playing)return;
            clickCooldown=Now+Policy("PlayerStatusReactionData.CooldownSeconds",5);
            if(autonomousSequence!=null)return;
            if(Resting && breakMode==4){autonomousSequence=StartCoroutine(WakeAndReact());return;}
            StartReaction(RestingScenario());
        }
        int RestingScenario()=>Chance(50)?(Resting?10:9):(Resting?550:500)+TimeOfDayIndex();
        public void SetResting(bool value){Settings.resting=value;ResetAutonomy();SaveNow();}
        void SelectBreak()
        {
            if(breakBag.Count==0)breakBag.AddRange((breakMode<0?new[]{0,1,3}:Enumerable.Range(0,6)).Where(i=>i!=breakMode&&(i!=4||Chance(Policy("_breakSleepOccurrenceProbability",10)))).OrderBy(_=>UnityEngine.Random.value));
            breakMode=breakBag[0];breakBag.RemoveAt(0);player.StopAnimationAudio();SetNativeMotion(breakBases[breakMode]);
            if(breakMode==2)SetNativeMotion(Chance(50)?"BreakBase003_ListenMusicLow":"BreakBase003_ListenMusicHigh");
            nextAction=Now+60*Delay("_changeBreak"+durationKeys[breakMode]+"TimeMinutesMin","_changeBreak"+durationKeys[breakMode]+"TimeMinutesMax",1,3);
            nextVariant=Now+VariantDelay();selfTalkCount=0;
            if(breakMode==4)autonomousSequence=StartCoroutine(Sleep());
        }
        IEnumerator WaitNative(float limit=30)
        {double end=Now+limit;yield return null;while(!Playing&&!presentation.MotionCompleted&&Now<end)yield return null;}
        IEnumerator Variant(string name)
        {
            if(SetNativeMotion(name))yield return WaitNative();
            if(!Playing)SetNativeMotion(Resting?breakBases[Mathf.Max(0,breakMode)]:workBases[player.SelectedState]);autonomousSequence=null;
        }
        IEnumerator Sleep()
        {yield return new WaitForSecondsRealtime(2);if(!Playing)SetNativeMotion("BreakBase005_Sleep");autonomousSequence=null;}
        IEnumerator WakeAndReact()
        {AwardAchievement("WAKE_UP_HEROINE");SetNativeMotion("BreakBase005_JumpUp");yield return WaitNative(10);SetNativeMotion("BreakBase005_JumpUp_End");yield return WaitNative(10);autonomousSequence=null;breakMode=0;SetNativeMotion(breakBases[0]);StartReaction(10);}
        IEnumerator WildAction()
        {
            var group=Features.actions.Where(a=>a.name.StartsWith("HeroineStateWild")).SelectMany(a=>a.motions).Distinct().Where(m=>presentation.statePaths.Any(s=>data.motions.Any(d=>d.name==m&&d.value==s.motion))).ToArray();if(group.Length==0){autonomousSequence=null;yield break;}
            yield return Variant(group[UnityEngine.Random.Range(0,group.Length)]);
        }
        IEnumerator ChangeWork(int selected)
        {
            int before=player.SelectedState;string[] names={"Book","Report","Pc"};
            SetNativeMotion("Wild002_From"+names[before]+"To"+names[selected]);yield return WaitNative();
            if(!Playing){player.PlayState(selected);save.deskMode=selected;previousMode=selected;SetNativeMotion(workBases[selected]);}
            nextAction=Now+Policy("_changeWorkLayoutDelaySeconds",300);autonomousSequence=null;
        }
        IEnumerator LeaveChair()
        {
            bool kitchen=Chance(Policy("LeaveChairData."+(TimeOfDayIndex()==1?"NoonKitchenDestinationProbabilityPercent":"KitchenDestinationProbabilityPercent"),60));
            bool cup=kitchen&&Chance(Policy("LeaveChairData.PrepareColdDrinkLeaveProbability",50));
            SetNativeMotion(kitchen?(cup?"Wild003_LeaveChair_Cup":"Wild003_LeaveChair_Normal"):"Wild003_LeaveChair_Sofa");
            yield return WaitNative(30);
            yield return new WaitForSecondsRealtime((float)Delay("LeaveChairData."+(kitchen?"Kitchen":"Sofa")+"StaySecondsMin","LeaveChairData."+(kitchen?"Kitchen":"Sofa")+"StaySecondsMax",kitchen?15:90,kitchen?25:180));
            bool snack=kitchen&&!cup&&Chance(Policy("LeaveChairData."+(TimeOfDayIndex()==1?"NoonSnackTakeProbability":"SnackTakeProbability"),60));
            if(snack)
            {
                float[] weights={Policy("LeaveChairData.CookieTakeWeight",1),Policy("LeaveChairData.CupcakeTakeWeight",1),Policy("LeaveChairData.BreadTakeWeight",1)};float draw=UnityEngine.Random.value*weights.Sum();int snackKind=draw<weights[0]?0:draw<weights[0]+weights[1]?1:2;if(snackKind<2)yield return SnackSkin(snackKind==0?"cookie":"cupcake");if(Playing){autonomousSequence=null;yield break;}bool rareBread=snackKind==2&&Chance(Policy("LeaveChairData.BreadRareProbability",30));string snackMotion=snackKind==0?"Wild003_LeaveChair_Cookie_Comeback":snackKind==1?"Wild003_LeaveChair_Cupcake_Comeback":(rareBread?"Wild003_LeaveChair_BreadB_Comeback":"Wild003_LeaveChair_BreadA_Comeback");SetNativeMotion(snackMotion);yield return WaitNative(25);
                autonomousSequence=null;nextLeave=Now+60*Delay("LeaveChairData.WorkMinutesMin","LeaveChairData.WorkMinutesMax",80,120);nextLeaveLottery=nextLeave;
                if(!StartReaction(snackKind==2?2601:2600))SetNativeMotion("Base001");yield break;
            }
            SetNativeMotion("Base001");yield return WaitNative(30);
            if(!Playing){player.deskObjects.Select(player.SelectedState);SetNativeMotion(Resting?breakBases[Mathf.Max(0,breakMode)]:workBases[player.SelectedState]);}
            nextLeave=Now+60*Delay("LeaveChairData."+(Resting?"Break":"Work")+"MinutesMin","LeaveChairData."+(Resting?"Break":"Work")+"MinutesMax",80,120);nextLeaveLottery=nextLeave;autonomousSequence=null;
        }
    }
}
