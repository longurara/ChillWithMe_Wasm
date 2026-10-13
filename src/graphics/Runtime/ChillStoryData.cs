using System;
using System.Collections.Generic;

namespace Hlc.ChillPrototype
{
    [Serializable] public class ChillStoryData
    {
        public int version;
        public ChillChapter[] groups;
        public ChillSentence[] novels;
        public ChillText[] localizations;
        public ChillMusic[] music;
        public ChillNamedValue[] motions, commands, scenarios;
        public ChillParameters parameters;
    }
    [Serializable] public class ChillNamedValue { public string name; public int value; }
    [Serializable] public class ChillChapter
    {
        public string ID, TitleLocalizationID, SubtitleLocalizationID, UnlockStoryEpisode, PlayTiming, ContentType, Arg1, Arg2, Arg3;
        public int Scenario, UnlockLevel;
        public float EpisodeNumber;
    }
    [Serializable] public class ChillSentence
    {
        public string ID, ScenarioGroupID, Arg1, Arg3, Arg4, LocalizationID;
        public int Command, Talker, BodyMotion, FacialMotion, LookEaseType;
        public float Arg2, LookScale, LookSpeedSeconds;
    }
    [Serializable] public class ChillText { public string ID, En, Ja, ZhHans, ZhHant, Pt, Ko, Ru, Voice; }
    [Serializable] public class ChillMusic { public string title, clip, credit; }
    [Serializable] public class ChillParameters { public ChillLevelParameters LevelUpInfoData; }
    [Serializable] public class ChillLevelParameters
    {
        public float pomodoroExpBaseMinutes, pomodoroExpBaseMax, pomodoroExpBaseMaxHour, nextLevelNecessaryExpBase;
        public float[] nextLevelNecessaryExpArray;
    }
    [Serializable] public class ChillTodo { public string title,id,due="",completedDate=""; public bool done; }
    [Serializable] public class ChillSave
    {
        public string format = "chill-with-you-web";
        public int version = 1, level = 1, cursor, deskMode, timeMode, windowMode;
        public string language = "En", chapter = "", note = "";
        public bool autoRead, previewUnlock;
        public List<string> completed = new List<string>();
        public List<float> usedChoices = new List<float>();
        public List<int> sceneHistory = new List<int>();
        public List<ChillTodo> todos = new List<ChillTodo>();
        public ChillWebSettings settings=new ChillWebSettings();
        public ChillWorkspace workspace=new ChillWorkspace();
        public ChillMusicState musicState=new ChillMusicState();
        public List<string> conversationHistory=new List<string>();
        public double exp, workedSeconds, mainEpisode, timerEnd, pausedRemaining, lastTimerTick;
        public bool timerRunning, breakTime;
          public int workMinutes = 25, breakMinutes = 5, rounds, loopCount=2, timerMode;
          public bool countupRunning,countupActive;
          public double countupSeconds,countupStarted,lastCountupTick;
          public List<ChillTimerPreset> timerPresets=new List<ChillTimerPreset>();
    }
    [Serializable] public class ChillTimerPreset
    {public string name="";public int loopCount=2,workMinutes=25,breakMinutes=5;}
}
