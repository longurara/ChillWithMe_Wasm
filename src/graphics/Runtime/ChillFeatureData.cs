using System;
using System.Collections.Generic;
namespace Hlc.ChillPrototype
{
    [Serializable] public class ChillFeatureData
    {
        public int version;
        public ChillTrack[] music;
        public ChillAmbientTrack[] ambient;
        public ChillAmbientTrack[] systemSounds,timerSounds;
        public ChillPolicy[] policies;
        public ChillActionSet[] actions;
        public ChillUnlockRule[] unlocks;
        public ChillWardrobePaths wardrobe;
        public ChillTimerButton[] timerButtons;
        public ChillTimerButton[] decorationButtons;
        public ChillProfileButton[] profileButtons,statusButtons;
        public ChillSkinColor[] skinColors;
        public ChillSnack[] snacks;
        public ChillCollaboration[] collaborations;public ChillDailyCostume[] dailyCostumes;public ChillTimerButton[] statisticsButtons;
        public ChillPostPreset[] post;
        public ChillTutorialFocus[] tutorialFocus;
        public string musicboxSound;
        public string creditsParent;
        public string[] creditsGroups;
        public float creditsFadeIn,creditsHold,creditsFadeOut,creditsGap;
    }
    [Serializable] public class ChillTrack {public string guid,uuid,fileName,title,artist,requiredChapter;public int tag;public bool personal;}
    [Serializable] public class ChillAmbientTrack {public int id;public string name,guid;}
    [Serializable] public class ChillPolicy {public string name;public float value;}
    [Serializable] public class ChillTutorialFocus {public string path,image,mask;}
    [Serializable] public class ChillPostPreset
    {public string name;public float brightness=1,contrast=1,saturation=1,bloom,threshold=1;public UnityEngine.Vector4 lift,gamma,gain;public bool liftOverride,gammaOverride,gainOverride;}
    [Serializable] public class ChillActionSet {public string name;public string[] motions;}
    [Serializable] public class ChillUnlockRule {public string kind,item,condition,argument;public int expiry;}
    [Serializable] public class ChillWardrobePaths {public string parent,bones;public string[] defaults,materials;}
    [Serializable] public class ChillTimerButton {public string path;public int id;}
    [Serializable] public class ChillCollaboration {public int scenario;public string name;public float[] thresholds;}
    [Serializable] public class ChillDailyCostume {public int id;public float[] weights;}
    [Serializable] public class ChillProfileButton {public string path,image;public int id;}
    [Serializable] public class ChillSkinColor {public int id;public UnityEngine.Color upper,lower;}
    [Serializable] public class ChillSnack {public string kind;public int id,rarity;public ChillSnackPart[] parts;}
    [Serializable] public class ChillSnackPart {public string path,mesh,material;}
    [Serializable] public class ChillAmbientLevel {public int id;public float volume;}
    [Serializable] public class ChillPlaylist {public string name="Playlist";public List<string> tracks=new List<string>();}
    [Serializable] public class ChillGreetingBag {public int scenario,lastTail=-1;public List<int> episodes=new List<int>();}
    [Serializable] public class ChillWebSettings
    {
        public bool autonomy=true,selfTalk=true,resting,autoGreeting=true,autoTime,clock24,mobileFit=true;
        public int quality=1,activeFps=60,inactiveFps=15,birthdayMonth,birthdayDay,cameraView,firstDayInWeek;
        public int totalLoginCount;public List<ChillGreetingBag> greetingBags=new List<ChillGreetingBag>();
        public float master=1,music=.35f,voice=1,sfx=1,ambient=1,timer=1,renderScale=1;
        public float dayStart=6,sunsetStart=17,nightStart=19;
        public bool postProcessing=true;
        public List<ChillAmbientLevel> ambience=new List<ChillAmbientLevel>();
        public double lastVisit;
    }
    [Serializable] public class ChillMusicState
    {
        public string selected="",playlist="ALL";
        public float position;
        public bool shuffle,repeat;
        public List<string> favorites=new List<string>();
        public List<string> excluded=new List<string>();
        public List<ChillPlaylist> playlists=new List<ChillPlaylist>();
        public List<ChillTrack> imports=new List<ChillTrack>();
    }
}
