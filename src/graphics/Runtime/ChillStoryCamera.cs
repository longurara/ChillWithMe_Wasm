using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public sealed class ChillStoryCamera : MonoBehaviour
    {
        [Serializable] public class Pose { public Transform transform,follow,lookAt; public Vector3 followOffset,lookOffset; public float fov; public bool useFollow; }
        struct Saved { public Transform transform; public Vector3 position,scale; public Quaternion rotation; }
        public Camera view;
        public Animator cameraAnimator;
        public Transform[] fixedViews;
        public Pose[] poses;
        readonly List<Saved> saved = new List<Saved>();
        Vector3 viewPosition,fromPosition;
        Quaternion viewRotation,fromRotation;
        float viewFov,fromFov,blend;
        int target = -1;
        public Animator actor;
        bool frameMobileRoom;int mobileFrameDelay;
        Vector3 mobileTarget;
        public bool AprilFoolCamera { get; private set; }
        public void ChangeToAprilFoolCamera() => SetAprilFoolCamera(true);
        public void ChangeToDefaultCamera() => SetAprilFoolCamera(false);
        public void SetAprilFoolCamera(bool active)
        {
            AprilFoolCamera = active;
            SelectRoomView(active && fixedViews != null && fixedViews.Length > 1 ? 1 : 0);
        }
        public bool SelectRoomView(int index)
        {
            if(fixedViews==null||index<0||index>=fixedViews.Length||!fixedViews[index])return false;
            target=-1;if(cameraAnimator)cameraAnimator.enabled=false;
            view.transform.SetPositionAndRotation(fixedViews[index].position,fixedViews[index].rotation);view.fieldOfView=ChillPlatform.Mobile?45:35;
            frameMobileRoom=ChillPlatform.Mobile&&index==0;mobileFrameDelay=3;
            return true;
        }
        public void Capture()
        {
            saved.Clear();
            if (cameraAnimator) foreach (var t in cameraAnimator.GetComponentsInChildren<Transform>(true))
                saved.Add(new Saved { transform=t,position=t.localPosition,rotation=t.localRotation,scale=t.localScale });
            viewPosition=view.transform.position; viewRotation=view.transform.rotation; viewFov=view.fieldOfView;
            target=-1;
        }
        public void Switch(int index)
        {
            // The desktop game keeps its fixed framing; these events select
            // Cinemachine viewpoints only for the original mobile presentation.
            if (!ChillPlatform.Mobile || poses == null || index < 0 || index >= poses.Length || !poses[index].transform) return;
            // The recovered mobile placeholders have no injected tracking target.
            // Preserve usable room framing until a tracked viewpoint is available.
            if(!poses[index].follow&&!poses[index].lookAt)return;
            fromPosition=view.transform.position; fromRotation=view.transform.rotation; fromFov=view.fieldOfView; target=index; blend=0;
            frameMobileRoom=false;
        }
        public void PlayStartCamera()
        {
            target=-1;
            if (fixedViews != null && fixedViews.Length > 0 && fixedViews[0]) view.transform.SetPositionAndRotation(fixedViews[0].position,fixedViews[0].rotation);
            if (cameraAnimator)
            { cameraAnimator.enabled=true; cameraAnimator.speed=1; cameraAnimator.Play(ChillPlatform.Mobile ? "anim_startcamera_mobile" : "anim_startcamera_pc",0,0); }
        }
        void LateUpdate()
        {
            if(frameMobileRoom&&actor&&(!cameraAnimator||!cameraAnimator.enabled))
            {
                if(mobileFrameDelay>0&&--mobileFrameDelay==0)
                {var renderers=actor.GetComponentsInChildren<SkinnedMeshRenderer>();if(renderers.Length>0){var bounds=renderers[0].bounds;foreach(var renderer in renderers)if(renderer.enabled)bounds.Encapsulate(renderer.bounds);mobileTarget=bounds.center;}}
                if(mobileFrameDelay==0&&Screen.width<Screen.height)view.transform.rotation=Quaternion.LookRotation(mobileTarget-view.transform.position,Vector3.up);
            }
            if (target < 0) return;
            var pose=poses[target]; var position=pose.useFollow && pose.follow ? pose.follow.position+pose.followOffset : pose.transform.position;
            var rotation=pose.lookAt ? Quaternion.LookRotation(pose.lookAt.TransformPoint(pose.lookOffset)-position,Vector3.up) : pose.transform.rotation;
            blend=Mathf.Min(1,blend+Time.unscaledDeltaTime/.75f); float t=ChillEase.Evaluate(blend,4);
            view.transform.SetPositionAndRotation(Vector3.Lerp(fromPosition,position,t),Quaternion.Slerp(fromRotation,rotation,t));
            view.fieldOfView=Mathf.Lerp(fromFov,pose.fov,t);
        }
        public void ResetCamera()
        {
            if (cameraAnimator) { cameraAnimator.enabled=false; cameraAnimator.speed=1; }
            if (saved.Count == 0) return;
            foreach (var item in saved) if (item.transform)
            { item.transform.localPosition=item.position; item.transform.localRotation=item.rotation; item.transform.localScale=item.scale; }
            view.transform.SetPositionAndRotation(viewPosition,viewRotation); view.fieldOfView=viewFov; target=-1;
        }
    }
}
