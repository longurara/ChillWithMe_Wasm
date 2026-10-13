using System;
using System.Collections.Generic;
namespace Hlc.ChillPrototype
{
    [Serializable] public class ChillNotePage {public string id,title="Note",text="";public float scroll=1;}
    [Serializable] public class ChillTodoGroup {public string id,title="Tasks";public List<ChillTodo> items=new List<ChillTodo>();}
    [Serializable] public class ChillHabit {public string id,title;public int weekdays=127;public bool paused;public List<string> completed=new List<string>();}
    [Serializable] public class ChillDayRecord {public string date,diary="";public double seconds;}
    [Serializable] public class ChillPanelPosition {public string path;public float x,y;}
    [Serializable] public class ChillRoomChoice {public int category;public string skin;}
    [Serializable] public class ChillRoomPreset {public string name;public int time,window,hair,costume;public List<ChillRoomChoice> choices=new List<ChillRoomChoice>();}
    [Serializable] public class ChillWorkspace
    {
        public List<ChillNotePage> notes=new List<ChillNotePage>();
        public List<ChillTodoGroup> lists=new List<ChillTodoGroup>();
        public List<ChillHabit> habits=new List<ChillHabit>();
        public List<ChillDayRecord> days=new List<ChillDayRecord>();
        public List<ChillPanelPosition> panels=new List<ChillPanelPosition>();
        public List<string> purchases=new List<string>(),achievements=new List<string>(),calendarEvents=new List<string>();
        public List<ChillRoomChoice> room=new List<ChillRoomChoice>();
        public List<ChillRoomPreset> presets=new List<ChillRoomPreset>();
        public string noteId,listId,profileName="Player",profileStatus="",profileIcon="",lastAppearanceDate="";
        public int points,hair,costume,collaboration;public double[] collaborationExp=new double[7];public double pointRemainder,legacyWorkSeconds;public bool workMigrated;
        public bool appearanceDaily,reminders;
        public ChillTimerAudio pomodoroAudio=new ChillTimerAudio(),countupAudio=new ChillTimerAudio();
    }
    [Serializable] public class ChillTimerAudio {public int start,finish=3;public float startVolume=.5f,finishVolume=.5f;public bool startMute,finishMute,voice=true;}
}
