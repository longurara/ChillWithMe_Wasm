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
            fromPosition=view.transform.position; fromRotation=view.transform.rotation; fromFov=view.fieldOfView; target=index; blend=0;
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
