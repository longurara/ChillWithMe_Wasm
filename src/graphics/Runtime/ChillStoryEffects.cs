using System.Collections;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public sealed class ChillStoryEffects : MonoBehaviour
    {
        public static ChillStoryEffects Active;
        public float glitch, shake, blackout, monochrome, chromatic, flash;
        public bool frozen, blackoutBlind, hatHairLocked;
        public Animator screenSharing;
        public GameObject hat;
        public SkinnedMeshRenderer hair;
        public ChillStoryPresentation presentation;
        public Renderer[] sunglasses;
        class Glow { public Renderer renderer; public int slot; public MaterialPropertyBlock previous; public Color color; }
        readonly List<Glow> glows = new List<Glow>();
        readonly Dictionary<int,int> versions = new Dictionary<int,int>();
        bool costumeCaptured,previousHatActive;
        int previousHairStyle;
        public string hatReplacedHeadphone { get; private set; } = "";
        float previousHairShape;
        float delay;
        public bool HasEffect => frozen || glitch > .001f || shake > .001f || blackout > .001f || monochrome > .001f || chromatic > .001f || flash > .001f;
        void OnEnable() { Active = this; }
        void OnDisable() { if (Active == this) Active = null; ResetEffects(); }
        void NotifyNoise()
        {
            var story = presentation && presentation.player ? presentation.player.story : GetComponentInParent<ChillGraphicsPlayer>()?.story;
            if (story) story.UpdateEffectNoise(glitch, monochrome, shake, blackout);
        }
        public void ResetEffects()
        {
            StopAllCoroutines(); glitch = shake = blackout = monochrome = chromatic = flash = delay = 0; frozen = blackoutBlind = hatHairLocked = false;
            versions.Clear();
            NotifyNoise();
            if (costumeCaptured)
            {
                if (hat) hat.SetActive(previousHatActive);
                if (hair && hair.sharedMesh.blendShapeCount > 1) hair.SetBlendShapeWeight(1,previousHairShape);
                var story = presentation && presentation.player ? presentation.player.story : GetComponentInParent<ChillGraphicsPlayer>()?.story;
                if (story && story.decorations && !story.VerificationOnly)
                {
                    story.decorations.SetHair(previousHairStyle);
                    var hp = story.Workspace.room.Find(r => r.category == 2);
                    if (hp != null && !string.IsNullOrEmpty(hp.skin) && gameObject.activeInHierarchy) StartCoroutine(story.decorations.Apply(hp.skin));
                }
                hatReplacedHeadphone = "";
                costumeCaptured=false;
            }
            foreach (var glow in glows) if (glow.renderer) glow.renderer.SetPropertyBlock(glow.previous,glow.slot);
            glows.Clear();
            if (screenSharing) screenSharing.gameObject.SetActive(false);
        }
        static float Value(string text) => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0;
        public void SetHat(bool enable)
        {
            var story = presentation && presentation.player ? presentation.player.story : GetComponentInParent<ChillGraphicsPlayer>()?.story;
            if (!costumeCaptured)
            {
                previousHatActive=hat && hat.activeSelf;
                previousHairShape=hair && hair.sharedMesh.blendShapeCount > 1 ? hair.GetBlendShapeWeight(1) : 0;
                previousHairStyle=story ? story.Workspace.hair : 0;
                costumeCaptured=true;
            }
            hatHairLocked = enable;
            if (hat) hat.SetActive(enable);
            if (hair && hair.sharedMesh.blendShapeCount > 1) hair.SetBlendShapeWeight(1,enable ? 100 : 0);
            if (story && story.decorations)
            {
                var hp = story.Workspace.room.Find(r => r.category == 2);
                hatReplacedHeadphone = enable && hp != null && hp.skin != null && hp.skin.StartsWith("Headphone_2") ? "Headphone_1" : "";
                if (!story.VerificationOnly)
                {
                    story.decorations.SetHair(enable ? 0 : story.Workspace.hair);
                    if (gameObject.activeInHierarchy)
                    {
                        if (!string.IsNullOrEmpty(hatReplacedHeadphone)) StartCoroutine(story.decorations.Apply(hatReplacedHeadphone));
                        else if (hp != null && !string.IsNullOrEmpty(hp.skin)) StartCoroutine(story.decorations.Apply(hp.skin));
                    }
                }
            }
        }
        public void Apply(ChillSentence row)
        {
            if (row.Command == 100) { delay = Mathf.Max(0, Value(row.Arg1)); return; }
            float pendingDelay = delay; delay = 0;
            if (row.Command == 219)
            {
                foreach (int channel in new[] { 200,205,216 })
                {
                    versions.TryGetValue(channel,out int token); versions[channel]=++token;
                    StartCoroutine(ApplyLater(new ChillSentence { Command=channel,Arg1=row.Arg1,Arg2=row.Arg2,Arg3=row.Arg3 },pendingDelay,channel,token,true));
                }
                return;
            }
            int key = row.Command == 90 ? 210 : row.Command == 231 ? 230 : row.Command;
            versions.TryGetValue(key,out int version); versions[key]=++version;
            StartCoroutine(ApplyLater(row,pendingDelay,key,version));
        }
        IEnumerator ApplyLater(ChillSentence row,float pendingDelay,int key,int version,bool sustainedTween=false)
        {
            if (pendingDelay > 0) yield return new WaitForSecondsRealtime(pendingDelay);
            if (!Current(key,version)) yield break;
            if((row.Command==200 || row.Command==205) && !sustainedTween)
            {
                Set(row.Command,Mathf.Clamp01(Value(row.Arg1)));
                if(row.Arg2>=0){yield return new WaitForSecondsRealtime(row.Arg2);if(Current(key,version))Set(row.Command,0);}
                yield break;
            }
            if (row.Command == 220)
            {
                frozen = true;
                if (row.Arg2 >= 0) { yield return new WaitForSecondsRealtime(row.Arg2); if (Current(key,version)) frozen=false; }
                yield break;
            }
            if (row.Command == 210)
            {
                blackout=Mathf.Clamp01(Value(row.Arg1));
                blackoutBlind=blackout>0 && Value(row.Arg3)==1;
                NotifyNoise();
                if (row.Arg2 >= 0) { yield return new WaitForSecondsRealtime(row.Arg2); if (Current(key,version)) { blackout=0; blackoutBlind=false; NotifyNoise(); } }
                yield break;
            }
            if (row.Command == 230 || row.Command == 231)
            {
                if (screenSharing)
                {
                    screenSharing.gameObject.SetActive(true); screenSharing.enabled = true;
                    screenSharing.SetBool("Active", row.Command == 230);
                    screenSharing.Play(row.Command == 230 ? "anim_ui_screensharing_In" : "anim_ui_screensharing_Out", 0, 0);
                    if (row.Command == 231) { yield return new WaitForSecondsRealtime(1); if (Current(key,version)) screenSharing.gameObject.SetActive(false); }
                }
                yield break;
            }
            if (row.Command == 1000)
            {
                SetHat(row.Arg1 != "None");
                yield break;
            }
            if (row.Command == 250)
            {
                if (presentation)
                {
                    presentation.Motion(new ChillSentence { BodyMotion=400,FacialMotion=-1 },"GameStart_Start",true);
                    presentation.cameras.PlayStartCamera();
                }
                yield break;
            }
            if (row.Command == 500 || row.Command == 501)
            { yield return Shine(row,key,version); yield break; }
            if (row.Command == 240) yield break; // Audio distortion is handled by the voice source.
            float target = row.Command == 90 ? 1 : Mathf.Clamp01(Value(row.Arg1));
            float duration = Mathf.Clamp(row.Command == 90 ? Value(row.Arg1) : row.Arg2, 0, 30);
            float initial = Get(row.Command), elapsed = 0;
            do
            {
                elapsed += Time.unscaledDeltaTime;
                if (!Current(key,version)) yield break;
                Set(row.Command, Mathf.Lerp(initial, target, duration > 0 ? ChillEase.Evaluate(elapsed/duration,ChillEase.Parse(row.Arg3)) : 1));
                yield return null;
            } while (elapsed < duration);
        }
        bool Current(int key,int version) => versions.TryGetValue(key,out int current) && current == version;
        IEnumerator Shine(ChillSentence row,int key,int version)
        {
            if (Value(row.Arg1) > 0) yield return new WaitForSecondsRealtime(Value(row.Arg1));
            if (!Current(key,version) || sunglasses == null || !presentation) yield break;
            foreach (var previous in glows.ToArray())
                if (previous.renderer && previous.renderer.transform.IsChildOf(presentation.player.character.transform) == (row.Command == 500))
                { previous.renderer.SetPropertyBlock(previous.previous,previous.slot); glows.Remove(previous); }
            var targets = new List<Glow>();
            foreach (var renderer in sunglasses)
            {
                if (!renderer || !renderer.gameObject.activeInHierarchy || renderer.transform.IsChildOf(presentation.player.character.transform) != (row.Command == 500)) continue;
                var materials = renderer.sharedMaterials;
                for (int i=0;i<materials.Length;i++) if (materials[i] && materials[i].HasProperty("_EmissionColor"))
                {
                    var previous = new MaterialPropertyBlock(); renderer.GetPropertyBlock(previous,i);
                    var glow = new Glow { renderer=renderer,slot=i,previous=previous,color=materials[i].GetColor("_EmissionColor") };
                    targets.Add(glow); glows.Add(glow);
                }
            }
            var block = new MaterialPropertyBlock();
            for (int phase=0;phase<3;phase++)
            {
                float duration = phase == 0 ? Mathf.Max(0,row.Arg2) : phase == 1 ? Mathf.Max(0,Value(row.Arg3)) : Mathf.Max(0,Value(row.Arg4));
                float elapsed=0;
                do
                {
                    if (!Current(key,version)) yield break;
                    float t = duration > 0 ? ChillEase.Evaluate(elapsed/duration,6) : 1;
                    float alpha = phase == 0 ? t : phase == 1 ? 1 : 1-t;
                    foreach (var glow in targets) if (glow.renderer)
                    {
                        glow.renderer.GetPropertyBlock(block,glow.slot); var color=glow.color; color.a=alpha;
                        block.SetColor("_EmissionColor",color); glow.renderer.SetPropertyBlock(block,glow.slot);
                    }
                    yield return null; elapsed+=Time.unscaledDeltaTime;
                } while (elapsed < duration);
            }
            foreach (var glow in targets) if (glow.renderer) { glow.renderer.SetPropertyBlock(glow.previous,glow.slot); glows.Remove(glow); }
        }
        public void RestoreFinal(ChillSentence row)
        {
            if (row.Command == 220) { frozen=row.Arg2 < 0; return; }
            if (row.Command == 200 || row.Command == 205) {Set(row.Command,row.Arg2<0?Mathf.Clamp01(Value(row.Arg1)):0);return;}
            if (row.Command == 210) { blackout=row.Arg2 < 0 ? Mathf.Clamp01(Value(row.Arg1)) : 0; blackoutBlind=blackout>0 && Value(row.Arg3)==1; NotifyNoise(); return; }
            if (row.Command == 1000) { Apply(row); return; }
            if (row.Command == 200 || row.Command == 205 || row.Command == 210 || row.Command == 215 || row.Command == 216 || row.Command == 219 || row.Command == 90)
                Set(row.Command,row.Command == 90 ? 1 : Mathf.Clamp01(Value(row.Arg1)));
        }
        float Get(int command) => command == 200 || command == 219 ? glitch : command == 205 ? shake : command == 210 || command == 90 ? blackout : command == 215 ? monochrome : chromatic;
        void Set(int command, float value)
        {
            switch (command)
            {
                case 200: glitch = value; break;
                case 205: shake = value; break;
                case 210: case 90: blackout = value; break;
                case 215: monochrome = value; break;
                case 216: chromatic = value; break;
                case 219: glitch = shake = chromatic = value; break;
            }
            NotifyNoise();
        }
    }
}
