using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillNativeUI
    {
        GameObject[] storyHudObjects=Array.Empty<GameObject>();
        readonly Dictionary<GameObject,bool> storyHudBefore=new Dictionary<GameObject,bool>();
        bool storyHudHidden,storyUiWasActive,storyRevealWasActive;
        public bool StoryHudHidden=>storyHudHidden;
        void InitializeStoryHud(Configuration config)
        {storyHudObjects=(config.storyHiddenObjects ?? Array.Empty<string>()).Select(p=>Find(p)?.gameObject).Where(o=>o).Distinct().ToArray();}
        void ApplyStoryHud(bool active)
        {
            if(active!=storyHudHidden)
            {
                storyHudHidden=active;
                if(active)
                {
                    storyHudBefore.Clear();foreach(var go in storyHudObjects)storyHudBefore[go]=go.activeSelf;
                    storyUiWasActive=nativeUiRoot && nativeUiRoot.gameObject.activeSelf;storyRevealWasActive=reveal && reveal.activeSelf;
                }
                else
                {
                    if(pane=="history")Close();
                    foreach(var pair in storyHudBefore)if(pair.Key)pair.Key.SetActive(pair.Value);
                    storyHudBefore.Clear();if(nativeUiRoot)nativeUiRoot.gameObject.SetActive(storyUiWasActive);if(reveal)reveal.SetActive(storyRevealWasActive);
                }
            }
            if(active)
            {
                // UIShowManagerForPC's exact story list excludes BottomBackImage
                // and StorySystemUI. Keep that background even if the player had
                // manually hidden their room HUD before accepting the story.
                if(nativeUiRoot)nativeUiRoot.gameObject.SetActive(true);
                foreach(var go in storyHudObjects)if(go)go.SetActive(false);
                if(reveal)reveal.SetActive(false);
            }
        }
    }
}
