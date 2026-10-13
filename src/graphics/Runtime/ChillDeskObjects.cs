using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public sealed class ChillDeskObjects : MonoBehaviour
    {
        public Renderer handPen, handPenForSpin, roomPen;
        public GameObject handNote, handCop, roomCop, coffeeMakerCop;
        public Animator deskObjectAnimator, workBookAnimator, chairAnimator, bearsRestaurantBookAnimator;
        public Animator breakBookAnimator, penAnimator, windowAnimator, musicboxAnimator;
        public ParticleSystem coffeeDrop;
        public GameObject[] cupcake, cookie, breadA, breadB;
        [Serializable] public class MotionAction { public string name, kind, state; public int value; public bool trigger; }
        public MotionAction[] motionActions;
        readonly Dictionary<string,MotionAction> actionMap = new Dictionary<string,MotionAction>();
        struct Pose { public Transform bone; public Vector3 position,scale; public Quaternion rotation; }
        readonly List<Pose> defaults = new List<Pose>();
        readonly HashSet<Transform> captured = new HashSet<Transform>();
        bool windowOpen;
        public bool restoring;
        int breadKind = 1;
        void Start() { CaptureDefaults(); }
        public void CaptureDefaults()
        {
            foreach (var animator in new[] { chairAnimator,penAnimator,breakBookAnimator,musicboxAnimator })
                if (animator) foreach (var t in animator.GetComponentsInChildren<Transform>(true))
                    if (captured.Add(t)) defaults.Add(new Pose { bone=t,position=t.localPosition,rotation=t.localRotation,scale=t.localScale });
        }
        public void HideSnacks() { Hide(cupcake); Hide(cookie); Hide(breadA); Hide(breadB); }
        static void Hide(GameObject[] items) { if (items != null) foreach (var item in items) if (item) item.SetActive(false); }
        static void Show(GameObject[] items, int index, bool active) { if (items != null && index < items.Length && items[index]) items[index].SetActive(active); }
        GameObject[] Bread => breadKind == 0 ? breadA : breadB;
        public void PrepareMotion(string name)
        {
            if (name.Contains("Cupcake_Comeback")) { Hide(cupcake); Show(cupcake,0,true); Show(cupcake,2,true); }
            if (name.Contains("Cookie_Comeback")) { Hide(cookie); Show(cookie,0,true); }
            if (name.Contains("Bread") && name.Contains("Comeback"))
            { breadKind = name.Contains("BreadA") ? 0 : 1; Hide(breadA); Hide(breadB); Show(Bread,0,true); }
        }
        public void HideStoryBook()
        { if (bearsRestaurantBookAnimator) bearsRestaurantBookAnimator.gameObject.SetActive(false); }
        public void Action(string name, string parameter = "", int integer = 0)
        {
            if (actionMap.Count == 0 && motionActions != null) foreach (var item in motionActions) actionMap[item.name] = item;
            if (actionMap.TryGetValue(name, out var motion))
            {
                var animator = motion.kind == "Chair" ? chairAnimator : penAnimator;
                if (!animator) return;
                animator.enabled = true; animator.speed = 1;
                foreach (var p in animator.parameters) if (p.type == AnimatorControllerParameterType.Trigger) animator.ResetTrigger(p.name);
                if (motion.trigger) animator.SetTrigger(motion.state);
                else { if (motion.kind == "Chair") animator.SetInteger("MotionType",motion.value); animator.Play(motion.state == "Idle" ? "Chair_Idle" : motion.state,0,0); }
                return;
            }
            switch (name)
            {
                case "BreakBookNextPage": Play(breakBookAnimator,"NextPage"); break;
                case "BreakBookBackPage": Play(breakBookAnimator,"BackPage"); break;
                case "EatCake1": Show(cupcake,0,false); Show(cupcake,1,true); break;
                case "EatCake2": Show(cupcake,1,false); break;
                case "CrashCup": Show(cupcake,2,false); break;
                case "EatCookie1": Show(cookie,0,false); Show(cookie,1,true); break;
                case "EatCookie2": Hide(cookie); break;
                case "TakeBreadForStory": Hide(breadA); Hide(breadB); breadKind = 1; Show(Bread,0,true); break;
                case "StartEat1": Show(Bread,1,true); Show(Bread,4,true); break;
                case "TidyingEat1": Show(Bread,0,false); Show(Bread,1,false); break;
                case "StartEat2": Show(Bread,2,true); Show(Bread,3,true); break;
                case "TidyingEat2": Show(Bread,4,false); Show(Bread,2,false); break;
                case "TidyingEat3": Show(Bread,3,false); break;
                case "PlayWindowAnimationOpenWindow": windowOpen=true; Trigger(windowAnimator,"PlayOpenWindow"); break;
                case "PlayWindowAnimationCloseWindow": windowOpen=false; Trigger(windowAnimator,"PlayCloseWindow"); break;
                case "PlayDropCoffeeFromCoffeeMaker": if (coffeeDrop) { coffeeDrop.gameObject.SetActive(true); coffeeDrop.Play(); } break;
                case "DeactivateDropCoffee": if (coffeeDrop) coffeeDrop.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear); break;
                case "WindingMusicbox": Trigger(musicboxAnimator,"PlayOpenMemorie"); break;
                case "PlayMusicbox": if (musicboxAnimator) musicboxAnimator.speed = 0; break;
                case "PourDrinksFromCoffeeMaker": if (Application.isPlaying && !restoring) StartCoroutine(StopPouring()); break;
                case "PlayBearsRestaurantBookAnim":
                    if (bearsRestaurantBookAnimator && !string.IsNullOrEmpty(parameter))
                    {
                        bearsRestaurantBookAnimator.gameObject.SetActive(true);
                        bearsRestaurantBookAnimator.enabled = true;
                        bearsRestaurantBookAnimator.speed = 1;
                        bearsRestaurantBookAnimator.Play(parameter, 0, 0f);
                    }
                    break;
                case "ActivatePen": if (handPen) handPen.enabled = true; if (handPenForSpin) handPenForSpin.enabled = false; if (roomPen) roomPen.enabled = false; break;
                case "ActivatePenForSpin": if (handPen) handPen.enabled = false; if (handPenForSpin) handPenForSpin.enabled = true; if (roomPen) roomPen.enabled = false; break;
                case "DeactivatePen": if (handPen) handPen.enabled = false; if (handPenForSpin) handPenForSpin.enabled = false; if (roomPen) roomPen.enabled = true; break;
                case "ActivateBook": if (handNote) handNote.SetActive(true); break;
                case "DeactivateBook": if (handNote) handNote.SetActive(false); break;
                case "ActivateCop": case "TakeCopFromCoffeeMaker": if (handCop) handCop.SetActive(true); if (roomCop) roomCop.SetActive(false); if (coffeeMakerCop) coffeeMakerCop.SetActive(false); break;
                case "DeactivateCop": if (handCop) handCop.SetActive(false); if (roomCop) roomCop.SetActive(true); if (coffeeMakerCop) coffeeMakerCop.SetActive(false); break;
                case "PutCopOnCoffeeMaker": if (handCop) handCop.SetActive(false); if (roomCop) roomCop.SetActive(false); if (coffeeMakerCop) coffeeMakerCop.SetActive(true); break;
                case "WorkBookNextPage": if (workBookAnimator) { workBookAnimator.enabled = true; workBookAnimator.speed = 1; workBookAnimator.SetTrigger("NextPage"); } break;
                case "TakeWorkBook": DeskMotion(8); break;
                case "PutWorkBook": DeskMotion(7); break;
                case "FromBookToPc": DeskMotion(1); break;
                case "FromReportToPc": DeskMotion(2); break;
                case "FromPcToBook": DeskMotion(3); break;
                case "FromReportToBook": DeskMotion(4); break;
                case "FromPcToReport": DeskMotion(5); break;
                case "FromBookToReport": DeskMotion(6); break;
            }
        }
        static void Play(Animator animator, string state)
        { if (animator) { animator.enabled = true; animator.speed = 1; animator.Play(state,0,0); } }
        static void Trigger(Animator animator, string name)
        { if (animator) { animator.enabled = true; animator.speed = 1; animator.SetTrigger(name); } }
        IEnumerator StopPouring() { yield return new WaitForSecondsRealtime(2); Action("DeactivateDropCoffee"); }
        public void AdvanceProps(float seconds)
        {
            foreach (var animator in new[] { deskObjectAnimator,workBookAnimator,chairAnimator,breakBookAnimator,penAnimator,bearsRestaurantBookAnimator,windowAnimator,musicboxAnimator })
                if (animator && animator.enabled && animator.gameObject.activeInHierarchy && animator.speed > 0) animator.Update(seconds);
        }
        void DeskMotion(int motion)
        { if (deskObjectAnimator) { deskObjectAnimator.enabled = true; deskObjectAnimator.speed = 1; deskObjectAnimator.SetInteger("MotionType", motion); } }

        public void Select(int mode)
        {
            CaptureDefaults();
            foreach (var animator in new[] { chairAnimator,penAnimator,breakBookAnimator,musicboxAnimator }) if (animator) animator.enabled=false;
            foreach (var pose in defaults) if (pose.bone) { pose.bone.localPosition=pose.position; pose.bone.localRotation=pose.rotation; pose.bone.localScale=pose.scale; }
            if (windowAnimator) { Play(windowAnimator,windowOpen ? "OpenWindow" : "CloseWindow"); windowAnimator.Update(0); windowAnimator.Play(windowOpen ? "OpenWindow" : "CloseWindow",0,1); windowAnimator.Update(0); windowAnimator.speed=0; }
            HideStoryBook();
            StopAllCoroutines(); HideSnacks(); Action("DeactivateDropCoffee");
            if (handPen) handPen.enabled = mode == 1;
            if (handPenForSpin) handPenForSpin.enabled = false;
            if (roomPen) roomPen.enabled = mode != 1;
            if (handNote) handNote.SetActive(false);
            if (handCop) handCop.SetActive(false);
            if (coffeeMakerCop) coffeeMakerCop.SetActive(false);
            if (roomCop) roomCop.SetActive(true);
            if (deskObjectAnimator)
            {
                deskObjectAnimator.enabled = true;
                deskObjectAnimator.Rebind();
                int motion = mode == 0 ? 3 : mode == 1 ? 5 : 1;
                deskObjectAnimator.SetInteger("MotionType", motion);
                deskObjectAnimator.Play(mode == 0 ? "FromPcToBook" : mode == 1 ? "FromPcToReport" : "FromBookToPc", 0, 1f);
                deskObjectAnimator.Update(0); deskObjectAnimator.speed = 0;
            }
            if (workBookAnimator)
            { workBookAnimator.enabled = true; workBookAnimator.Rebind(); workBookAnimator.Update(0); workBookAnimator.speed = 0; }
            if (chairAnimator)
            { chairAnimator.enabled = false; }
        }
    }
}
