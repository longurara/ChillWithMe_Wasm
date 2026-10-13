using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        public ChillWorkspace Workspace {get{EnsureWorkspace();return save.workspace;}}
        public int WorkspaceRevision{get;private set;}
        public static string DateKey(DateTime date)=>date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        public static bool ValidDate(string key)=>DateTime.TryParseExact(key,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _);
        static string NewId()=>Guid.NewGuid().ToString("N");
        public ChillNotePage CurrentNote=>Workspace.notes.First(n=>n.id==Workspace.noteId);
        public ChillTodoGroup CurrentList=>Workspace.lists.First(n=>n.id==Workspace.listId);
        void EnsureWorkspace()
        {
            if(save.workspace==null)save.workspace=new ChillWorkspace();var w=save.workspace;w.profileIcon=w.profileIcon??"";w.profileName=w.profileName??"Player";w.profileStatus=w.profileStatus??"0";if(w.collaborationExp==null||w.collaborationExp.Length!=7)w.collaborationExp=new double[7];
            if(w.notes==null)w.notes=new List<ChillNotePage>();if(w.lists==null)w.lists=new List<ChillTodoGroup>();
            if(w.habits==null)w.habits=new List<ChillHabit>();if(w.days==null)w.days=new List<ChillDayRecord>();if(w.panels==null)w.panels=new List<ChillPanelPosition>();
            if(w.purchases==null)w.purchases=new List<string>();if(w.achievements==null)w.achievements=new List<string>();if(w.calendarEvents==null)w.calendarEvents=new List<string>();
            if(w.room==null)w.room=new List<ChillRoomChoice>();if(w.presets==null)w.presets=new List<ChillRoomPreset>();
            if(w.pomodoroAudio==null)w.pomodoroAudio=new ChillTimerAudio();if(w.countupAudio==null)w.countupAudio=new ChillTimerAudio();
            if(!w.workMigrated){w.workMigrated=true;w.legacyWorkSeconds=Math.Max(0,save.workedSeconds-w.days.Sum(d=>d.seconds));}
            if(w.notes.Count==0)w.notes.Add(new ChillNotePage{id=NewId(),text=save.note??""});
            if(w.lists.Count==0)w.lists.Add(new ChillTodoGroup{id=NewId(),items=save.todos??new List<ChillTodo>()});
            foreach(var list in w.lists){if(list.items==null)list.items=new List<ChillTodo>();foreach(var todo in list.items)if(string.IsNullOrEmpty(todo.id))todo.id=NewId();}
            if(!w.notes.Any(n=>n.id==w.noteId))w.noteId=w.notes[0].id;if(!w.lists.Any(n=>n.id==w.listId))w.listId=w.lists[0].id;
            save.note=w.notes.First(n=>n.id==w.noteId).text;save.todos=w.lists.First(l=>l.id==w.listId).items;
        }
        void ValidateWorkspace(ChillSave candidate)
        {
            if(candidate.workspace==null)return;var w=candidate.workspace;
            if((w.notes?.Count??0)>100||(w.lists?.Count??0)>50||(w.habits?.Count??0)>100||(w.days?.Count??0)>12000||(w.presets?.Count??0)>20||(w.panels?.Count??0)>100||w.collaboration<0||w.collaboration>7||w.collaborationExp!=null&&(w.collaborationExp.Length!=7||w.collaborationExp.Any(v=>double.IsNaN(v)||double.IsInfinity(v)||v<0||v>100000000))||w.points<0||double.IsNaN(w.legacyWorkSeconds)||double.IsInfinity(w.legacyWorkSeconds)||w.legacyWorkSeconds<0||double.IsNaN(w.pointRemainder)||double.IsInfinity(w.pointRemainder)||w.pointRemainder<0||w.hair<0||w.hair>3||w.costume<0||w.costume>4)throw new ArgumentException("Invalid workspace size or room data.");
            if((w.profileIcon??"").Length>90000||(w.profileName??"").Length>120||(w.profileStatus??"").Length>120||(w.calendarEvents?.Count??0)>12000||(w.achievements?.Count??0)>100||(w.purchases?.Count??0)>500)throw new ArgumentException("Invalid profile or progress data.");
            if(w.notes!=null&&w.notes.Any(n=>n==null||string.IsNullOrEmpty(n.id)||(n.title??"").Length>120||(n.text??"").Length>64000||float.IsNaN(n.scroll)||n.scroll<0||n.scroll>1)||w.lists!=null&&w.lists.Any(l=>l==null||string.IsNullOrEmpty(l.id)||(l.title??"").Length>120||l.items==null||l.items.Count>500||l.items.Any(t=>t==null||(t.title??"").Length>200||!string.IsNullOrEmpty(t.due)&&!ValidDate(t.due)||!string.IsNullOrEmpty(t.completedDate)&&!ValidDate(t.completedDate)))||w.habits!=null&&w.habits.Any(h=>h==null||string.IsNullOrEmpty(h.id)||(h.title??"").Length>120||h.weekdays<0||h.weekdays>127||h.completed==null||h.completed.Count>12000||h.completed.Any(d=>!ValidDate(d)))||w.days!=null&&w.days.Any(d=>d==null||!ValidDate(d.date)||(d.diary??"").Length>64000||double.IsNaN(d.seconds)||double.IsInfinity(d.seconds)||d.seconds<0||d.seconds>86400*10)||w.panels!=null&&w.panels.Any(p=>p==null||float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.y)||float.IsInfinity(p.y)))throw new ArgumentException("Invalid notes, tasks, dates or panel positions.");
            foreach(var a in new[]{w.pomodoroAudio,w.countupAudio})if(a!=null&&(a.start<0||a.start>2||a.finish<3||a.finish>5||float.IsNaN(a.startVolume)||float.IsNaN(a.finishVolume)||a.startVolume<0||a.startVolume>1||a.finishVolume<0||a.finishVolume>1))throw new ArgumentException("Invalid timer audio.");
            if(w.notes!=null&&w.notes.Select(n=>n.id).Distinct().Count()!=w.notes.Count||w.lists!=null&&w.lists.Select(l=>l.id).Distinct().Count()!=w.lists.Count||w.habits!=null&&w.habits.Select(h=>h.id).Distinct().Count()!=w.habits.Count||w.days!=null&&w.days.Select(d=>d.date).Distinct().Count()!=w.days.Count||w.room!=null&&(w.room.Count>12||w.room.Any(r=>r==null||r.category<1||r.category>12||string.IsNullOrEmpty(r.skin)||r.skin.Length>100)||w.room.Select(r=>r.category).Distinct().Count()!=w.room.Count)||w.presets!=null&&w.presets.Any(p=>p==null||(p.name??"").Length>80||p.time<0||p.time>3||p.window<0||p.window>24||p.hair<0||p.hair>3||p.costume<0||p.costume>4||p.choices==null||p.choices.Count>12||p.choices.Any(c=>c==null||c.category<1||c.category>12||(c.skin??"").Length>100)))throw new ArgumentException("Duplicate workspace keys or invalid room presets.");
        }
        public void SelectNote(string id){if(Workspace.notes.Any(n=>n.id==id)){Workspace.noteId=id;EnsureWorkspace();WorkspaceRevision++;SaveNow();}}
        public void AddNote(string title="Note"){if(Workspace.notes.Count>=100)return;var page=new ChillNotePage{id=NewId(),title=Trim(title,120)};Workspace.notes.Add(page);SelectNote(page.id);}
        public void DeleteNote(){var page=CurrentNote;if(Workspace.notes.Count==1){page.text="";page.title="Note";}else Workspace.notes.Remove(page);Workspace.noteId=Workspace.notes[0].id;WorkspaceChanged();}
        public void UpdateNote(string text){CurrentNote.text=Trim(text,64000);save.note=CurrentNote.text;AwardAchievement("END_EDIT_NOTE_COUNT");SaveNow();}
        public void RenameNote(string title){CurrentNote.title=Trim(title,120);WorkspaceChanged();}
        public void MoveNote(int step){var w=Workspace;int from=w.notes.FindIndex(n=>n.id==w.noteId),to=Mathf.Clamp(from+step,0,w.notes.Count-1);var item=w.notes[from];w.notes.RemoveAt(from);w.notes.Insert(to,item);WorkspaceChanged();}
        public void SelectTodoList(string id){if(Workspace.lists.Any(n=>n.id==id)){Workspace.listId=id;WorkspaceChanged();}}
        public void AddTodoList(string title="Tasks"){if(Workspace.lists.Count>=50)return;var list=new ChillTodoGroup{id=NewId(),title=Trim(title,120)};Workspace.lists.Add(list);SelectTodoList(list.id);}
        public void DeleteTodoList(){if(Workspace.lists.Count==1)CurrentList.items.Clear();else Workspace.lists.Remove(CurrentList);Workspace.listId=Workspace.lists[0].id;WorkspaceChanged();}
        public void RenameTodoList(string title){CurrentList.title=Trim(title,120);WorkspaceChanged();}
        public void AddTodo(string title,string due=""){title=Trim(title,200).Trim();if(title.Length==0||CurrentList.items.Count>=500||due!=""&&!ValidDate(due))return;CurrentList.items.Add(new ChillTodo{id=NewId(),title=title,due=due});WorkspaceChanged();}
        public void ToggleTodoId(string id){var todo=Workspace.lists.SelectMany(l=>l.items).FirstOrDefault(t=>t.id==id);if(todo==null)return;todo.done=!todo.done;todo.completedDate=todo.done?DateKey(DateTime.Now):"";if(todo.done)AwardAchievement("COMPLETE_TODO_COUNT");WorkspaceChanged();}
        public void ToggleTodo(int index){if(index<0||index>=CurrentList.items.Count)return;var todo=CurrentList.items[index];todo.done=!todo.done;todo.completedDate=todo.done?DateKey(DateTime.Now):"";if(todo.done)AwardAchievement("COMPLETE_TODO_COUNT");WorkspaceChanged();}
        public void EditTodo(string id,string title,string due,string completedDate)
        {var todo=Workspace.lists.SelectMany(l=>l.items).FirstOrDefault(t=>t.id==id);if(todo==null||due!=""&&!ValidDate(due)||completedDate!=""&&!ValidDate(completedDate))return;todo.title=Trim(title,200);todo.due=due;todo.completedDate=completedDate;todo.done=completedDate!="";WorkspaceChanged();}
        public void MoveTodo(int index,int step){if(index<0||index>=CurrentList.items.Count)return;var items=CurrentList.items;var item=items[index];items.RemoveAt(index);items.Insert(Mathf.Clamp(index+step,0,items.Count),item);WorkspaceChanged();}
        public void DeleteTodo(int index){if(index>=0&&index<CurrentList.items.Count){CurrentList.items.RemoveAt(index);WorkspaceChanged();}}
        public ChillDayRecord Day(string date){if(!ValidDate(date))throw new ArgumentException("Invalid date");var day=Workspace.days.FirstOrDefault(d=>d.date==date);if(day==null){day=new ChillDayRecord{date=date};Workspace.days.Add(day);}return day;}
        public void SetDiary(string date,string text){Day(date).diary=Trim(text,64000);AwardAchievement("END_EDIT_DIARY_COUNT");WorkspaceChanged();}
        public void SetWorkTime(string date,double seconds){if(double.IsNaN(seconds)||double.IsInfinity(seconds))return;Day(date).seconds=Math.Max(0,Math.Min(86400*10,seconds));WorkspaceChanged();}
        void RecordWork(double seconds,double end)
        {
            var until=DateTimeOffset.FromUnixTimeMilliseconds((long)(end*1000)).LocalDateTime;
            for(int guard=0;seconds>0&&guard<32;guard++){var day=until.AddTicks(-1).Date;double chunk=Math.Min(seconds,(until-day).TotalSeconds);Day(DateKey(day)).seconds+=chunk;seconds-=chunk;until=until.AddSeconds(-chunk);}
            WorkspaceRevision++;
        }
        public void AddHabit(string title){title=Trim(title,120).Trim();if(title.Length>0&&Workspace.habits.Count<100){Workspace.habits.Add(new ChillHabit{id=NewId(),title=title});WorkspaceChanged();}}
        public void EditHabit(string id,string title,int weekdays,bool paused){var h=Workspace.habits.FirstOrDefault(x=>x.id==id);if(h==null)return;h.title=Trim(title,120);h.weekdays=Mathf.Clamp(weekdays,0,127);h.paused=paused;WorkspaceChanged();}
        public bool HabitScheduled(ChillHabit habit,string date)=>!habit.paused&&(habit.weekdays&(1<<(int)DateTime.ParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture).DayOfWeek))!=0;
        public void ToggleHabit(string id,string date){var h=Workspace.habits.FirstOrDefault(x=>x.id==id);if(h==null||!ValidDate(date)||!HabitScheduled(h,date))return;if(!h.completed.Remove(date))h.completed.Add(date);WorkspaceChanged();}
        public void MoveHabit(string id,int step){var list=Workspace.habits;int from=list.FindIndex(h=>h.id==id);if(from<0)return;var item=list[from];list.RemoveAt(from);list.Insert(Mathf.Clamp(from+step,0,list.Count),item);WorkspaceChanged();}
        public void DeleteHabit(string id){Workspace.habits.RemoveAll(h=>h.id==id);WorkspaceChanged();}
        public double WorkTotal(string first,string last)=>Workspace.days.Where(d=>string.CompareOrdinal(d.date,first)>=0&&string.CompareOrdinal(d.date,last)<=0).Sum(d=>d.seconds);
        public void SavePanel(string path,Vector2 position){var p=Workspace.panels.FirstOrDefault(x=>x.path==path);if(p==null){p=new ChillPanelPosition{path=path};Workspace.panels.Add(p);}p.x=position.x;p.y=position.y;SaveNow();}
        public Vector2? PanelPosition(string path){var p=Workspace.panels.FirstOrDefault(x=>x.path==path);return p==null?(Vector2?)null:new Vector2(p.x,p.y);}
        public void AwardAchievement(string key){if(!Workspace.achievements.Contains(key)){Workspace.achievements.Add(key);Notify("Achievement: "+key.Replace('_',' '));WorkspaceRevision++;}}
        static string Trim(string value,int count)=>(value??"").Substring(0,Math.Min(count,(value??"").Length));
        void WorkspaceChanged(){EnsureWorkspace();WorkspaceRevision++;SaveNow();}
    }
}
