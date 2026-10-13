using System;
using System.Globalization;
using System.Linq;

namespace Hlc.ChillPrototype
{
    public static class ChillHabitHistory
    {
        public static string Today => ChillRuntimeContext.LocalNow.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        public static bool Alive(ChillHabit habit,string date)
        {
            if(!ChillStoryRuntime.ValidDate(date))return false;
            if(!string.IsNullOrEmpty(habit.createdDate)&&string.CompareOrdinal(date,habit.createdDate)<0)return false;
            return !habit.deadPeriods.Any(p=>string.CompareOrdinal(date,p.start)>=0&&(string.IsNullOrEmpty(p.end)||string.CompareOrdinal(date,p.end)<=0));
        }
        public static bool Scheduled(ChillHabit habit,string date)
        {return Alive(habit,date)&&(habit.weekdays&(1<<(int)DateTime.ParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture).DayOfWeek))!=0;}
        public static void SetPaused(ChillHabit habit,bool paused,string today)
        {
            var periods=habit.deadPeriods;
            if(!paused)
            {
                periods.RemoveAll(p=>string.CompareOrdinal(p.start,today)>=0);
                var active=periods.FirstOrDefault(p=>string.CompareOrdinal(p.start,today)<=0&&(string.IsNullOrEmpty(p.end)||string.CompareOrdinal(p.end,today)>=0));
                if(active!=null)active.end=DateTime.ParseExact(today,"yyyy-MM-dd",CultureInfo.InvariantCulture).AddDays(-1).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
            }
            else
            {
                periods.RemoveAll(p=>string.CompareOrdinal(p.start,today)>0);
                string yesterday=DateTime.ParseExact(today,"yyyy-MM-dd",CultureInfo.InvariantCulture).AddDays(-1).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
                var active=periods.FirstOrDefault(p=>string.CompareOrdinal(p.start,today)<=0&&(string.IsNullOrEmpty(p.end)||string.CompareOrdinal(p.end,today)>=0))
                    ?? periods.FirstOrDefault(p=>p.end==yesterday);
                if(active!=null)active.end="";else periods.Add(new ChillHabitPeriod{start=today});
            }
            habit.paused=paused;
        }
    }
}
