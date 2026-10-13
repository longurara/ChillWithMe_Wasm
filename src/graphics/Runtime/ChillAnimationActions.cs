using UnityEngine;

namespace Hlc.ChillPrototype
{
    public sealed partial class ChillAnimationActions : MonoBehaviour
    {
        public Animator character;
        public ChillDeskObjects objects;
        public ChillStoryRuntime story;
        public ChillStoryPresentation presentation;
        public bool previewEvents;
        void Start() { if (character) character.fireEvents = true; }
        public void Dispatch(string name, AnimationEvent item)
        {
            if (!Application.isPlaying && !previewEvents) return;
            if (objects) objects.Action(name, item.stringParameter, item.intParameter);
            if (name == "OnChangeHeroineLookCamera" && presentation) presentation.cameras.Switch(item.intParameter);
            if (presentation && name.StartsWith("AdjustWhen")) presentation.AdjustSleeve(name);
            if (story && Application.isPlaying) story.MotionSound(name);
        }
        void OnAnimatorIK(int layer) { if (presentation && layer == 0) presentation.ApplyLookIK(); }
    }
}
