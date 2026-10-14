using System;

namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        public static readonly string[] LocalAchievementKeys={"COMPLETE_TUTORIAL_COUNT","COMPLETE_TODO_COUNT","COMPLETE_POMODORO_COUNT","END_EDIT_NOTE_COUNT","END_EDIT_DIARY_COUNT","IMPORT_MUSIC_COUNT","CHANGE_ENVIROMENT_COUNT","FINISH_GAME_FROM_EXIT_CALL_BUTTON_COUNT","TALK_WORKING_COUNT","TALK_BREAKING_COUNT","TALK_AFTER_WORK_COUNT","SMALL_TALK_CAT_HEADPHONE","SMALL_TALK_GALAXY_EXPRESS","SMALL_TALK_HEATSTROKE","SMALL_TALK_CHICADA","POMODORO_WORK_HOURS_COUNT","READ_ALL_MAIN_STORY","READ_GLASSES_STORY","CLICK_KOU_COUNT","DRINK_HOT","WAKE_UP_HEROINE","PUBLISH_SUMMERTIME_OVERDRIVE"};
        public int AchievementProgress(string key)
        {
            if(key=="POMODORO_WORK_HOURS_COUNT")return (int)Math.Min(1000000,Math.Max(0,Math.Floor(WorkedSeconds/3600)));
            if(key=="CLICK_KOU_COUNT")return Workspace.penguinClicks;
            return Workspace.achievements.Contains(key)?1:0;
        }
        void MigrateAchievementKeys(ChillWorkspace workspace)
        {
            if(!workspace.achievements.Remove("EXIT_GAME_COUNT"))return;
            if(!workspace.achievements.Contains("FINISH_GAME_FROM_EXIT_CALL_BUTTON_COUNT"))workspace.achievements.Add("FINISH_GAME_FROM_EXIT_CALL_BUTTON_COUNT");
        }
    }
}
