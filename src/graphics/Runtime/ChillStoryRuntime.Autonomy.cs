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
        public bool Resting=>Settings.resting || save.timerComplete || PomodoroActive && save.breakTime;
        public string AutonomyState {get;private set;}="Work";
        double nextAction,nextVariant,nextSelfTalk,nextLeave,nextLeaveLottery,nextWild,clickCooldown;
        int breakMode=-1,selfTalkCount,previousMode=-1;
        bool previousRest,autonomyInstalled;
        float sleepiness;
        string breakBaseMotion="";
        Coroutine autonomousSequence;
        ChillCharacterPicking characterPicking;
        public bool CanReactToPointer=>!ConnectionLost&&Now>=clickCooldown&&autonomousSequence==null&&!ActivityTransitionPending;
        readonly List<int> breakBag=new List<int>(),workBag=new List<int>();
        readonly Dictionary<int,List<string>> reactionBags=new Dictionary<int,List<string>>();
        static readonly string[] breakGroups={"HeroineStateBreakMovie","HeroineStateBreakReadBook","HeroineStateBreakListenMusic","HeroineStateBreakTeaTime","HeroineStateBreakSleep","HeroineStateBreakForward"};
        static readonly string[] breakBases={"BreakBase001","BreakBase002","BreakBase003_ListenMusicLow","BreakBase004","BreakBase005","BreakBase006"};
        static readonly string[] workGroups={"HeroineStateWorkBook","HeroineStateWorkReport","HeroineStateWorkPC"};
        static readonly string[] workBases={"WorkBase002","WorkBase003","WorkBase001"};
        static readonly string[] durationKeys={"Movie","Book","Music","Tea","Sleep","Forward"};
        float Policy(string key,float fallback)=>Features?.policies.FirstOrDefault(p=>p.name==key)?.value??fallback;
        double Delay(string min,string max,float fallbackMin,float fallbackMax)=>ChillRuntimeContext.Range(Policy(min,fallbackMin),Policy(max,fallbackMax));
        bool Chance(float percent)=>ChillRuntimeContext.Value*100<percent;
        void InitializeAutonomy()
        {
            InitializeStatusReaction();
            activityObjects.drinks=ChillRuntimeContext.Range(0,6);if(activityObjects.drinks==5)activityObjects.Pour(Now,Chance(Policy("LeaveChairData.ColdDrinkPourProbability",50)));activityObjects.Musicbox(Now,ChillRuntimeContext.Value);
            nextLeave=Now+60*Delay("LeaveChairData.FirstWorkMinutesMin","LeaveChairData.FirstWorkMinutesMax",20,60);
            nextLeaveLottery=nextLeave;nextWild=Now;ScheduleSelfTalk();ResetAutonomy();
        }
        public void ResetAutonomy()
        {
            CancelActivityTransition();
            if(musicboxActive)CloseMusicbox();
            if(autonomousSequence!=null){StopCoroutine(autonomousSequence);autonomousSequence=null;}
            autonomyInstalled=false;previousMode=-1;breakMode=-1;nextAction=nextVariant=0;weightGroup="";invitedChapter=null;
            if(player && player.deskObjects)player.deskObjects.restoring=false;
        }
        void ScheduleSelfTalk()=>nextSelfTalk=Now+Delay("_talkLotteryDelaySecondsMin","_talkLotteryDelaySecondsMax",30,160);
        public bool TryPlayRoomMotion(int mode)=>data!=null&&presentation&&storyController&&mode>=0&&mode<workBases.Length&&SetNativeMotion(workBases[mode]);
        bool SetNativeMotion(string name)
        {
            var motion=data.motions.FirstOrDefault(m=>m.name==name);
            if(motion==null || !presentation.statePaths.Any(s=>s.motion==motion.value))return false;
            if(player.character.runtimeAnimatorController!=storyController){player.character.runtimeAnimatorController=storyController;player.character.Rebind();}
            player.StopAnimationAudio();
            presentation.Motion(new ChillSentence{BodyMotion=motion.value,FacialMotion=0,LookScale=0,LookSpeedSeconds=.25f},name,false);
            AutonomyState=name;return true;
        }
        void InstallAutonomy()
        {
            if(!storyController)return;
            if(player.character.runtimeAnimatorController!=storyController){player.character.runtimeAnimatorController=storyController;player.character.Rebind();}
            player.deskObjects.SelectSmooth(player.SelectedState,.25f);autonomyInstalled=true;
            previousMode=player.SelectedState;previousRest=Resting;
            if(Resting)SelectBreak();else SetNativeMotion(workBases[player.SelectedState]);
            nextAction=Now+Policy("_changeWorkLayoutDelaySeconds",300);
            nextVariant=Now+VariantDelay();
        }
        void UpdateAutonomy()
        {
            bool pointer=Input.GetMouseButtonDown(0) || Input.touchCount>0&&Input.GetTouch(0).phase==TouchPhase.Began;
            if(pointer)TrySatoneClick();
            if(Playing||ConnectionLost||newYearCountdownReady)return;
            if(!Settings.autonomy)
            {if(autonomyInstalled){ResetAutonomy();player.PlayState(save.deskMode);}return;}
            if(!autonomyInstalled){InstallAutonomy();selfTalkCount=0;ScheduleSelfTalk();}
            if(activityTransition!=null)return;
            if(requestedWorkMode>=0||previousMode!=player.SelectedState||previousRest!=Resting)
            {activityTransition=StartCoroutine(TransitionActivity());return;}
            if(autonomousSequence!=null)return;
            if(WantsToTalk){if(NextInvitedChapter()!=null)return;invitedChapter=null;SetNativeMotion(CurrentBaseMotion);}
            if(breakMode!=4&&presentation.MotionCompleted&&InviteNextChapter())return;
            if(musicboxActive&&!Resting)CloseMusicbox();
            if(Now>=nextLeaveLottery)
            {
                nextLeaveLottery=Now+Policy("LeaveChairData.LotteryIntervalMinutes",5)*60;
                if(Now>=nextLeave && Chance(Policy("LeaveChairData.OccurrenceProbabilityPercent",30)))
                {autonomousSequence=StartCoroutine(LeaveChair());return;}
            }
            if(Resting)
            {
                if(Now>=nextAction&&presentation.MotionCompleted){SelectBreak();return;}
                if(breakMode==4){UpdateSleep();return;}
                if(Settings.selfTalk && Workspace.profileStatus!="2" && breakMode!=4 && Now>=nextSelfTalk)
                {ScheduleSelfTalk();if(selfTalkCount<Policy("_oneBreakSelfTalkMax",4) && Chance(Policy("_selfTalkProbability",75))){if(StartReaction(Chance(50)?21:2000+TimeOfDayIndex()))selfTalkCount++;}}
            }
            else if(Now>=nextAction)
            {
                nextAction=Now+Policy("_changeWorkLayoutDelaySeconds",300);
                if(Chance(Policy("_changeWorkLayoutRatio",50)))
                {
                    workBag.RemoveAll(i=>i==player.SelectedState);
                    if(workBag.Count==0)workBag.AddRange(Enumerable.Range(0,3).Where(i=>i!=player.SelectedState).OrderBy(_=>ChillRuntimeContext.Value));
                    int selected=workBag[0];workBag.RemoveAt(0);autonomousSequence=StartCoroutine(ChangeWork(selected));return;
                }
            }
            if(Playing)return;
            if(Now>=nextVariant)
            {
                if(!presentation.MotionCompleted)return;
                nextVariant=Now+VariantDelay();
                if(Chance(Policy("WildMotionOccurrenceProbability",25)) && breakMode!=4)
                {string ready=ReadyObjectMotion();if(!string.IsNullOrEmpty(ready)){autonomousSequence=StartCoroutine(ObjectAction(ready));return;}if(Now>=nextWild){nextWild=Now+Policy("WildMotionDelayMinutes",30)*60;autonomousSequence=StartCoroutine(WildAction());return;}}
                var variant=WeightedVariant();if(!string.IsNullOrEmpty(variant))autonomousSequence=StartCoroutine(Variant(variant));
            }
        }
        float VariantDelay()
        {
            string group=Resting?breakGroups[Mathf.Max(0,breakMode)]:workGroups[player.SelectedState];
            return Mathf.Max(1,(float)Delay(group+".BaseDelaySecondsMin",group+".BaseDelaySecondsMax",30,60));
        }
        int TimeOfDayIndex(){int h=ChillRuntimeContext.LocalNow.Hour;return h>5&&h<11?0:h>=11&&h<=16?1:h>=17&&h<=19?2:3;}
        bool StartReaction(int scenario)
        {
            if(StartupBlocking || ConnectionLost || Playing || autonomousSequence!=null||ActivityTransitionPending)return false;
            if(!reactionBags.TryGetValue(scenario,out var bag))reactionBags[scenario]=bag=new List<string>();
            if(bag.Count==0)bag.AddRange(data.groups.Where(g=>g.Scenario==scenario&&Unlocked(g)&&chapters.ContainsKey(g.ID)).OrderBy(_=>ChillRuntimeContext.Value).Select(g=>g.ID));
            if(bag.Count==0)return false;string id=bag[0];bag.RemoveAt(0);
            var item=data.groups.First(g=>g.ID==id);
            suspendedChapter=save.chapter;suspendedCursor=save.cursor;suspendedScene=new List<int>(save.sceneHistory);suspendedStoryLive=save.storyLive;
            Begin(item,false,true);return true;
        }
        void TrySatoneClick()
        {
            if(ConnectionLost||ActivityTransitionPending)return;
            if(Playing && tutorialCommand!=14 && tutorialCommand!=50)return;
            if(Now<clickCooldown)return;
            if(tutorialCommand!=14 && tutorialCommand!=50 && EventSystem.current && (EventSystem.current.IsPointerOverGameObject() || Input.touchCount>0&&EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)))return;
            Vector3 point=Input.touchCount>0?(Vector3)Input.GetTouch(0).position:Input.mousePosition;
            var ray=player.view.ScreenPointToRay(point);
            if(!characterPicking)characterPicking=gameObject.GetComponent<ChillCharacterPicking>()??gameObject.AddComponent<ChillCharacterPicking>();
            bool hit=characterPicking.Hit(player.character,ray);
            if(!hit)return;TutorialSignal("satone");if(Playing)return;
            clickCooldown=Now+Policy("PlayerStatusReactionData.CooldownSeconds",5);
            if(autonomousSequence!=null)return;
            if(AcceptStoryInvitation())return;
            if(!save.timerRunning&&!save.countupRunning){if(TrySmallTalk())return;StartReaction(8);return;}
            if(Resting && breakMode==4 && AutonomyState=="BreakBase005_Sleep"){autonomousSequence=StartCoroutine(WakeAndReact());return;}
            StartReaction(RestingScenario());
        }
        int RestingScenario()=>Chance(50)?(Resting?10:9):(Resting?550:500)+TimeOfDayIndex();
        public void SetResting(bool value){Settings.resting=value;SaveNow();}
        void SelectBreak()
        {
            breakBag.RemoveAll(i=>!BreakEligible(i));
            if(breakBag.Count==0)breakBag.AddRange((breakMode<0?new[]{0,1,3}:Enumerable.Range(0,6)).Where(i=>BreakEligible(i)&&(i!=4||Chance(Policy("_breakSleepOccurrenceProbability",10)))).OrderBy(_=>ChillRuntimeContext.Value));
            breakMode=breakBag[0];breakBag.RemoveAt(0);player.StopAnimationAudio();weightGroup="";
            breakBaseMotion=breakMode==2?(Chance(50)?"BreakBase003_ListenMusicLow":"BreakBase003_ListenMusicHigh"):breakBases[breakMode];SetNativeMotion(breakBaseMotion);
            nextAction=Now+60*Delay("_changeBreak"+durationKeys[breakMode]+"TimeMinutesMin","_changeBreak"+durationKeys[breakMode]+"TimeMinutesMax",1,3);
            nextVariant=Now+VariantDelay();selfTalkCount=0;
            sleepiness=0;
        }
        bool BreakEligible(int mode)=>mode>=0 && mode<6 && mode!=breakMode && (mode!=5 || player.SelectedState==2);
        string CurrentBaseMotion=>(autonomyInstalled?previousRest:Resting)?(breakMode==4 && AutonomyState=="BreakBase005_Sleep"?"BreakBase005_Sleep":string.IsNullOrEmpty(breakBaseMotion)?breakBases[Mathf.Max(0,breakMode)]:breakBaseMotion):workBases[player.SelectedState];
        void UpdateSleep()
        {
            if(Now<nextVariant || !presentation.MotionCompleted)return;
            nextVariant=Now+Delay("HeroineStateBreakSleep.BaseDelaySecondsMin","HeroineStateBreakSleep.BaseDelaySecondsMax",1,3);
            if(AutonomyState=="BreakBase005_Sleep")
            {
                sleepiness=Mathf.Max(0,sleepiness-Policy("HeroineStateBreakSleep.SleepinessSubtractAmount",5));
                if(sleepiness<Policy("HeroineStateBreakSleep.CanSleepEndProbabilityMin",50) && Chance(Policy("HeroineStateBreakSleep.SleepEndProbability",30)))SelectBreak();
            }
            else
            {
                sleepiness=Mathf.Min(100,sleepiness+Policy("HeroineStateBreakSleep.SleepinessAddAmount",5));
                if(sleepiness>=100)SetNativeMotion("BreakBase005_Sleep");
            }
        }
        IEnumerator WaitNative(float limit=30)
        {double end=Now+limit;yield return null;while(!Playing&&!presentation.MotionCompleted&&Now<end)yield return null;}
        IEnumerator Variant(string name)
        {
            if(SetNativeMotion(name)){yield return WaitNative();if(name=="BreakBase004_DrinkHot"&&!Playing&&presentation.MotionCompleted)AwardAchievement("DRINK_HOT");}
            if(!Playing)SetNativeMotion(CurrentBaseMotion);autonomousSequence=null;
        }
        IEnumerator WakeAndReact()
        {AwardAchievement("WAKE_UP_HEROINE");SetNativeMotion("BreakBase005_JumpUp");yield return WaitNative(10);SetNativeMotion("BreakBase005_JumpUp_End");yield return WaitNative(10);autonomousSequence=null;breakMode=0;SetNativeMotion(breakBases[0]);StartReaction(10);}
        IEnumerator WildAction()
        {
            // NextActionSelector's ordinary lottery uses states 4..6. Window and
            // musicbox actions have separate object readiness rules in the source.
            var group=Features.actions.Where(a=>a.name=="HeroineStateWildStretchFullBody" || a.name=="HeroineStateWildStretchShoulder" || a.name=="HeroineStateWildTea").SelectMany(a=>a.motions).Distinct().Where(m=>presentation.statePaths.Any(s=>data.motions.Any(d=>d.name==m&&d.value==s.motion))).ToArray();if(group.Length==0){autonomousSequence=null;yield break;}
            yield return Variant(group[ChillRuntimeContext.Range(0,group.Length)]);
        }
        IEnumerator ChangeWork(int selected)
        {
            int before=player.SelectedState;string[] names={"Book","Report","Pc"};
            SetNativeMotion("Wild002_From"+names[before]+"To"+names[selected]);yield return WaitNative();
            if(!Playing){ApplyWorkMode(selected);SetNativeMotion(workBases[selected]);}
            nextAction=Now+Policy("_changeWorkLayoutDelaySeconds",300);autonomousSequence=null;
        }
        IEnumerator LeaveChair()
        {
            bool kitchen=Chance(Policy("LeaveChairData."+(TimeOfDayIndex()==1?"NoonKitchenDestinationProbabilityPercent":"KitchenDestinationProbabilityPercent"),60));
            bool cup=kitchen&&activityObjects.NeedCold(Now,ChillRuntimeContext.Value*100,Policy("LeaveChairData.ColdDrinkPourProbability",50))&&Chance(Policy("LeaveChairData.PrepareColdDrinkLeaveProbability",50));
            SetNativeMotion(kitchen?(cup?"Wild003_LeaveChair_Cup":"Wild003_LeaveChair_Normal"):"Wild003_LeaveChair_Sofa");
            yield return WaitNative(30);
            float stay=(float)Delay("LeaveChairData."+(kitchen?"Kitchen":"Sofa")+"StaySecondsMin","LeaveChairData."+(kitchen?"Kitchen":"Sofa")+"StaySecondsMax",kitchen?15:90,kitchen?25:180);
            if(kitchen)yield return new WaitForSecondsRealtime(stay);else yield return SofaStay(stay);
            if(Playing){autonomousSequence=null;yield break;}
            if(cup)activityObjects.Pour(Now,true);
            bool snack=kitchen&&!cup&&Chance(Policy("LeaveChairData."+(TimeOfDayIndex()==1?"NoonSnackTakeProbability":"SnackTakeProbability"),60));
            if(snack)
            {
                float[] weights={Policy("LeaveChairData.CookieTakeWeight",1),Policy("LeaveChairData.CupcakeTakeWeight",1),Policy("LeaveChairData.BreadTakeWeight",1)};float draw=ChillRuntimeContext.Value*weights.Sum();int snackKind=draw<weights[0]?0:draw<weights[0]+weights[1]?1:2;if(snackKind<2)yield return SnackSkin(snackKind==0?"cookie":"cupcake");if(Playing){autonomousSequence=null;yield break;}bool rareBread=snackKind==2&&Chance(Policy("LeaveChairData.BreadRareProbability",30));string snackMotion=snackKind==0?"Wild003_LeaveChair_Cookie_Comeback":snackKind==1?"Wild003_LeaveChair_Cupcake_Comeback":(rareBread?"Wild003_LeaveChair_BreadB_Comeback":"Wild003_LeaveChair_BreadA_Comeback");SetNativeMotion(snackMotion);yield return WaitNative(25);
                autonomousSequence=null;nextLeave=Now+60*Delay("LeaveChairData.WorkMinutesMin","LeaveChairData.WorkMinutesMax",80,120);nextLeaveLottery=nextLeave;
                if(!StartReaction(snackKind==2?2601:2600))SetNativeMotion("Base001");yield break;
            }
            SetNativeMotion("Base001");yield return WaitNative(30);
            if(!Playing){player.deskObjects.SelectSmooth(player.SelectedState,.25f);SetNativeMotion(CurrentBaseMotion);}
            nextLeave=Now+60*Delay("LeaveChairData."+(Resting?"Break":"Work")+"MinutesMin","LeaveChairData."+(Resting?"Break":"Work")+"MinutesMax",80,120);nextLeaveLottery=nextLeave;autonomousSequence=null;
        }
    }
}
