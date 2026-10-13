using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public static class ChillEase
    {
        public static float Evaluate(float t, int ease)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case 2: return 1-Mathf.Cos(t*Mathf.PI/2);
                case 3: return Mathf.Sin(t*Mathf.PI/2);
                case 4: return (1-Mathf.Cos(Mathf.PI*t))/2;
                case 5: return t*t;
                case 6: case 0: return 1-(1-t)*(1-t);
                case 7: return t < .5f ? 2*t*t : 1-Mathf.Pow(-2*t+2,2)/2;
                case 8: return t*t*t;
                case 9: return 1-Mathf.Pow(1-t,3);
                case 10: return t < .5f ? 4*t*t*t : 1-Mathf.Pow(-2*t+2,3)/2;
                case 11: return Mathf.Pow(t,4);
                case 12: return 1-Mathf.Pow(1-t,4);
                case 13: return t < .5f ? 8*Mathf.Pow(t,4) : 1-Mathf.Pow(-2*t+2,4)/2;
                case 14: return Mathf.Pow(t,5);
                case 15: return 1-Mathf.Pow(1-t,5);
                case 16: return t < .5f ? 16*Mathf.Pow(t,5) : 1-Mathf.Pow(-2*t+2,5)/2;
                case 17: return t == 0 ? 0 : Mathf.Pow(2,10*t-10);
                case 18: return t == 1 ? 1 : 1-Mathf.Pow(2,-10*t);
                case 19: return t == 0 || t == 1 ? t : t < .5f ? Mathf.Pow(2,20*t-10)/2 : (2-Mathf.Pow(2,-20*t+10))/2;
                case 20: return 1-Mathf.Sqrt(1-t*t);
                case 21: return Mathf.Sqrt(1-(t-1)*(t-1));
                case 22: return t < .5f ? (1-Mathf.Sqrt(1-4*t*t))/2 : (Mathf.Sqrt(1-Mathf.Pow(-2*t+2,2))+1)/2;
                default: return t;
            }
        }
        public static int Parse(string name) => name == "InSine" ? 2 : name == "OutSine" ? 3 : name == "InOutSine" ? 4 : name == "InQuad" ? 5 : name == "OutQuad" ? 6 : name == "InOutQuad" ? 7 : name == "InCubic" ? 8 : name == "OutCubic" ? 9 : name == "InOutCubic" ? 10 : 1;
    }

    [DefaultExecutionOrder(160)]
    public sealed class ChillStoryPresentation : MonoBehaviour
    {
        [Serializable] public class Sleeve { public SkinnedMeshRenderer renderer; public Transform bone; }
        [Serializable] public class NamedState { public string name,path; public int motion = -1; }
        public ChillGraphicsPlayer player;
        public ChillStoryCamera cameras;
        public SkinnedMeshRenderer face;
        public Transform head, eyes, lookTarget;
        public Sleeve[] sleeves;
        public NamedState[] statePaths;
        public bool manualLook;
        public string requestedState;
        public int requestedMotion = -1;
        bool motionEntered, motionCompleted;
        int motionStartHash,motionTypeAtRequest;
        float motionRequestedAt;
        public bool MotionCompleted => motionCompleted;
        float lookWeight, lookTargetWeight, lookInitial, lookDuration, lookElapsed;
        int lookEase;

        public void Motion(ChillSentence row, string name, bool immediate = false)
        {
            var animator = player.character;
            if (row.BodyMotion != -1)
            {
                var mapped=statePaths?.FirstOrDefault(s => s.motion == row.BodyMotion);
                name=name ?? mapped?.name;
                requestedState = mapped?.path ?? statePaths?.FirstOrDefault(s => s.name == name)?.path ?? name; requestedMotion = row.BodyMotion;
                motionStartHash=animator.GetCurrentAnimatorStateInfo(0).fullPathHash;motionTypeAtRequest=animator.GetInteger("MotionType");motionRequestedAt=Time.realtimeSinceStartup;
                motionEntered=motionCompleted=false;
                if (player.bones) player.bones.ResetDynamics();
                if (!string.IsNullOrEmpty(name)) player.deskObjects.PrepareMotion(name);
                foreach (var p in animator.parameters)
                    if (p.type == AnimatorControllerParameterType.Trigger) animator.ResetTrigger(p.name);
                foreach (var p in animator.parameters)
                    if (p.type == AnimatorControllerParameterType.Int && p.name == "MotionType") animator.SetInteger(p.name,row.BodyMotion);
                    else if (p.type == AnimatorControllerParameterType.Trigger && p.name == name) animator.SetTrigger(p.name);
                if (immediate && animator.HasState(0,Animator.StringToHash(requestedState))) animator.Play(requestedState,0,0);
            }
            if (row.FacialMotion != -1)
                foreach (var p in animator.parameters)
                    if (p.name == "Facial" && p.type == AnimatorControllerParameterType.Int) animator.SetInteger(p.name,row.FacialMotion == 0 && requestedState != null && (requestedState.Contains("Sleep") || requestedState.Contains("Listen_CloseEyes")) ? 18 : row.FacialMotion);
            lookInitial = manualLook ? lookWeight : animator.GetFloat("LookScale");
            lookTargetWeight = Mathf.Clamp01(row.LookScale); lookDuration = immediate ? 0 : Mathf.Max(0,row.LookSpeedSeconds);
            lookEase = row.LookEaseType; lookElapsed = 0;
            if (lookDuration == 0) SetLook(lookTargetWeight);
        }
        void SetLook(float value)
        {
            if (manualLook) { lookWeight = value; player.character.SetFloat("LookScale",0); }
            else { lookWeight = 0; player.character.SetFloat("LookScale",value); }
        }
        void Update()
        {
            TrackMotion();
            if (lookElapsed < lookDuration)
            { lookElapsed += Time.unscaledDeltaTime; SetLook(Mathf.Lerp(lookInitial,lookTargetWeight,ChillEase.Evaluate(lookElapsed/lookDuration,lookEase))); }
        }
        public void TrackMotion()
        {
            if (requestedMotion == -1 || !player || !player.character) return;
            var animator=player.character;
            var state=animator.GetCurrentAnimatorStateInfo(0);
            bool current=state.IsName(requestedState);
            bool next=animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(requestedState);
            if (current || next) motionEntered=true;
            if (current && state.normalizedTime >= 1 || motionEntered && !current && !next) motionCompleted=true;
            // Native sub-state machines can enter their loop without visiting the
            // exported motion's named entry (for example Movie/Tea/Sofa). Match
            // the active sibling loop, while excluding a previous unrelated
            // loop or a transition that has not begun yet.
            if(!motionCompleted && !animator.IsInTransition(0) && state.loop && state.normalizedTime>=1 && !string.IsNullOrEmpty(requestedState))
            {
                int separator=requestedState.LastIndexOf('.');string parent=separator<0?"":requestedState.Substring(0,separator+1);
                bool related=!string.IsNullOrEmpty(parent) && statePaths.Any(s=>s.path.StartsWith(parent,StringComparison.Ordinal) && state.fullPathHash==Animator.StringToHash(s.path));
                if(related && (state.fullPathHash!=motionStartHash || motionTypeAtRequest==requestedMotion && Time.realtimeSinceStartup-motionRequestedAt>.25f))motionCompleted=true;
            }
        }
        public void ApplyLookIK()
        {
            if (!manualLook || !lookTarget) { player.character.SetLookAtWeight(0); return; }
            float weight = player.SelectedState == 0 ? .426f : player.SelectedState == 1 ? .397f : .333f;
            float offset = player.SelectedState == 0 ? .13f : player.SelectedState == 1 ? .09f : 0;
            player.character.SetLookAtPosition(lookTarget.position+Vector3.up*offset);
            player.character.SetLookAtWeight(lookWeight*weight);
        }
        void LateUpdate()
        {
            if (!manualLook || lookWeight <= 0 || !face || !head || !eyes || !lookTarget || face.sharedMesh.blendShapeCount < 39) return;
            var direction = Vector3.Slerp(eyes.forward,(lookTarget.position-eyes.position).normalized,ChillEase.Evaluate(lookWeight,5));
            var local = head.InverseTransformDirection(direction);
            float horizontal = -Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
            float vertical = Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg;
            face.SetBlendShapeWeight(36,Mathf.InverseLerp(0,22,horizontal)*100);
            face.SetBlendShapeWeight(37,Mathf.InverseLerp(0,3,vertical)*100);
            face.SetBlendShapeWeight(38,Mathf.InverseLerp(0,11,-vertical)*100);
        }
        public void ResetPresentation()
        {
            StopAllCoroutines(); manualLook = false; lookWeight = lookDuration = lookElapsed = 0; requestedMotion = -1; requestedState = "";
            motionEntered=motionCompleted=false;
            if (player.character) player.character.SetFloat("LookScale",0);
            if (cameras) cameras.ResetCamera();
            if (player.bones) player.bones.ResetDynamics();
        }
        public void AdjustSleeve(string action)
        {
            if (sleeves == null) return;
            foreach (var sleeve in sleeves)
                if (sleeve.renderer && sleeve.renderer.gameObject.activeInHierarchy)
                    StartCoroutine(SleeveTween(sleeve,action));
        }
        IEnumerator SleeveTween(Sleeve sleeve, string action)
        {
            bool elbow = action.Contains("Elbow");
            float initial = sleeve.renderer.sharedMesh.blendShapeCount > 0 ? sleeve.renderer.GetBlendShapeWeight(0) : 0;
            float target = action.Contains("Placing") ? 100 : 0;
            var rotation = sleeve.bone ? sleeve.bone.localRotation : Quaternion.identity;
            var goal = Quaternion.Euler(action.Contains("StartShow") ? new Vector3(-.11f,4.013f,3.191f) : Vector3.zero);
            float duration = elbow ? .2f : .4f;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = ChillEase.Evaluate(elapsed/duration,6);
                if (elbow && sleeve.renderer.sharedMesh.blendShapeCount > 0) sleeve.renderer.SetBlendShapeWeight(0,Mathf.Lerp(initial,target,t));
                else if (sleeve.bone) sleeve.bone.localRotation = Quaternion.Slerp(rotation,goal,t);
                yield return null;
            }
            if (elbow && sleeve.renderer.sharedMesh.blendShapeCount > 0) sleeve.renderer.SetBlendShapeWeight(0,target);
            else if (sleeve.bone) sleeve.bone.localRotation = goal;
        }
    }
}
