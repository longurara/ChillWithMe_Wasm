using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillNativeUI
    {
        const string ChangeRoot="Paremt/PCPlatform/Canvas/UI/ChangeOrderObjects/";
        readonly List<GameObject> notePages=new List<GameObject>(),habitRows=new List<GameObject>(),calendarRows=new List<GameObject>();
        DateTime calendarMonth=DateTime.Today,calendarDate=DateTime.Today,habitWeek=DateTime.Today;
        string workspaceSignature;int statisticsSpan=1;
        TMP_InputField todoListTitle;
        bool completedExpanded=true,habitEdit,restoringNoteScroll;string noteScrollId;
        Coroutine paneAnimation;
        void InitializeWorkspaceControls()
        {
            var story=player.story;
            foreach(var pair in new[]{("HabitListUI","_habitParent"),("TodoListUI","_uncompleteTodoUIParent"),("TodoListUI","_completeTodoUIParent"),("CalendarUI","_completeTodoParent"),("CalendarUI","_completeHabitParent")}){
                var content=RefOf(pair.Item1,pair.Item2);if(content)foreach(Transform child in content)child.gameObject.SetActive(false);
            }
            var title=Get<TMP_InputField>("CurrentPageUI","_titleInputField");
            if(title){title.readOnly=false;title.characterLimit=120;title.onEndEdit.RemoveAllListeners();title.onEndEdit.AddListener(story.RenameNote);}
            Wire("SelectPageListUI","_addSelectPageUIButton",()=>{story.AddNote();workspaceSignature=null;});
            var noteRoot=RefOf("NoteUI","self");

            var todoRoot=RefOf("TodoListUI","self");
            Set(RefOf("TodoListUI","_pulldown"),false);
            Set(Find(ChangeRoot+"UI_FacilityTodo/TodoGroupSelect"),false);
            Wire("TodoListUI","addTodoButton",()=>{story.AddTodo("New task");todoSignature=null;});
            if(todoRoot)
            {
                todoListTitle=FeatureInput(todoRoot,"listName","Tasks",-80,150,-115);if(todoListTitle){todoListTitle.characterLimit=120;todoListTitle.onEndEdit.AddListener(story.RenameTodoList);}
                FeatureHook(FeatureButton(todoRoot,"nextList","Choose list",-130,180,-90),()=>{var lists=story.Workspace.lists;int i=lists.FindIndex(l=>l.id==story.Workspace.listId);story.SelectTodoList(lists[(i+1)%lists.Count].id);todoSignature=null;});
                FeatureHook(FeatureButton(todoRoot,"addList","+",-80,60,10),()=>{story.AddTodoList();todoSignature=null;});
                FeatureHook(FeatureButton(todoRoot,"deleteList","Delete",-80,100,100),()=>{story.DeleteTodoList();todoSignature=null;});
            }
            var notesScroll=Get<TMP_InputField>("CurrentPageUI","_mainInputField")?.GetComponentInParent<ScrollRect>();if(notesScroll)notesScroll.onValueChanged.AddListener(value=>{if(!restoringNoteScroll){story.CurrentNote.scroll=notesScroll.verticalNormalizedPosition;}});
            var addRow=Find(ChangeRoot+"UI_FacilityTodo/AddTodoUI") as RectTransform;if(addRow)addRow.anchoredPosition+=new Vector2(0,-80);var countText=RefOf("TodoListUI","_remainTodoCountText") as RectTransform;if(countText)countText.anchoredPosition+=new Vector2(0,-80);var taskScroll=Get<ScrollRect>("TodoListUI","_todoScrollRect");if(taskScroll){var sr=(RectTransform)taskScroll.transform;sr.offsetMax+=new Vector2(0,-80);}
            Wire("TodoListUI","_toggleCompletedSectionButton",()=>{completedExpanded=!completedExpanded;todoSignature=null;});
            Wire("CalendarUI","_facilityOpenButton",()=>Open("calendar"));Wire("CalendarUI","_closeButton",Close);
            Wire("HabitTrackerUI","_facilityOpenButton",()=>Open("habits"));Wire("HabitTrackerUI","_closeButton",Close);
            Wire("TotalWorkStatisticsUI","_openButton",()=>Open("statistics"));Wire("TotalWorkStatisticsUI","_closeButton",Close);Wire("TotalWorkStatisticsUI","_returnToCalendarButton",()=>Open("calendar"));
            Wire("StatisticsCoreUI","nextSpanButton",()=>{calendarMonth=statisticsSpan==0?calendarMonth.AddDays(7):statisticsSpan==2?calendarMonth.AddYears(1):calendarMonth.AddMonths(1);workspaceSignature=null;});Wire("StatisticsCoreUI","prevSpanButton",()=>{calendarMonth=statisticsSpan==0?calendarMonth.AddDays(-7):statisticsSpan==2?calendarMonth.AddYears(-1):calendarMonth.AddMonths(-1);workspaceSignature=null;});
            Set(Find(ChangeRoot+"UI_FacilityCalendar/MiniCalender"),false);
            Set(Find(ChangeRoot+"UI_FacilityTodo/MiniCalender"),false);
            var cal=Find(ChangeRoot+"UI_FacilityCalendar/Calender");
            if(cal){WirePath(PathOf(cal)+"/YearMonth/NextButtonUI/NextButton",()=>{calendarMonth=calendarMonth.AddMonths(1);workspaceSignature=null;});WirePath(PathOf(cal)+"/YearMonth/BackButtonUI/BackButton",()=>{calendarMonth=calendarMonth.AddMonths(-1);workspaceSignature=null;});}
            var diary=Get<TMP_InputField>("CalendarUI","_diaryInputField");if(diary){diary.readOnly=false;diary.richText=false;diary.characterLimit=64000;diary.onEndEdit.RemoveAllListeners();diary.onEndEdit.AddListener(value=>story.SetDiary(ChillStoryRuntime.DateKey(calendarDate),value));}
            foreach(string key in new[]{"_workTimeHour","_workTimeMinutes","_workTimeSeconds"}){var input=Get<TMP_InputField>("CalendarUI",key);if(input){input.readOnly=false;input.contentType=TMP_InputField.ContentType.IntegerNumber;input.onEndEdit.RemoveAllListeners();input.onEndEdit.AddListener(_=>SaveCalendarTime());}}
            Wire("HabitListUI","_addHabitUIButton",()=>{story.AddHabit("New habit");workspaceSignature=null;});
            Wire("HabitListUI","_editModeButton",()=>{habitEdit=!habitEdit;workspaceSignature=null;});
            Set(RefOf("HabitListUI","_deleteWarningDialog"),false);
            var habitRoot=RefOf("HabitTrackerUI","self");
            if(habitRoot){FeatureHook(FeatureButton(habitRoot,"previousHabitWeek","Previous week",-60,185,-200),()=>{habitWeek=habitWeek.AddDays(-7);workspaceSignature=null;});FeatureHook(FeatureButton(habitRoot,"nextHabitWeek","Next week",-60,185,200),()=>{habitWeek=habitWeek.AddDays(7);workspaceSignature=null;});}
            foreach(string kind in new[]{"CalendarUI","HabitTrackerUI","TotalWorkStatisticsUI"})PreparePane(RefOf(kind,"self"));
        }
        void PreparePane(Transform root)
        {
            if(!root)return;var handle=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="DragButton");
            if(handle){var drag=handle.GetComponent<ChillNativeDrag>()??handle.gameObject.AddComponent<ChillNativeDrag>();drag.target=root as RectTransform;drag.saved=position=>player.story.SavePanel(PathOf(root),position);}
        }
        void ShowWorkspacePane()
        {
            if(!openPane)return;
            for(var root=openPane.transform;root&&root!=canvas.transform;root=root.parent)root.gameObject.SetActive(true);
            var rect=openPane.transform as RectTransform;var position=player.story.PanelPosition(PathOf(rect));if(position.HasValue)rect.anchoredPosition=position.Value;
            PreparePane(rect);
            if(Application.isPlaying){if(paneAnimation!=null)StopCoroutine(paneAnimation);paneAnimation=StartCoroutine(FadePane(openPane));}workspaceSignature=null;
        }
        IEnumerator FadePane(GameObject root)
        {var group=root.GetComponent<CanvasGroup>()??root.AddComponent<CanvasGroup>();for(float elapsed=0;elapsed<.14f;elapsed+=Time.unscaledDeltaTime){if(!root||!root.activeSelf)yield break;group.alpha=Mathf.Clamp01(elapsed/.14f);yield return null;}group.alpha=1;}
        void SaveCalendarTime()
        {int Number(string key){var input=Get<TMP_InputField>("CalendarUI",key);return input&&int.TryParse(input.text,out int value)?Mathf.Max(0,value):0;}player.story.SetWorkTime(ChillStoryRuntime.DateKey(calendarDate),Number("_workTimeHour")*3600d+Mathf.Min(59,Number("_workTimeMinutes"))*60+Mathf.Min(59,Number("_workTimeSeconds")));workspaceSignature=null;}
        void RefreshWorkspaceControls()
        {
            if(!player.story.FeatureReady)return;var story=player.story;var w=story.Workspace;
            var title=Get<TMP_InputField>("CurrentPageUI","_titleInputField");if(title&&!title.isFocused)title.SetTextWithoutNotify(story.CurrentNote.title);
            if(todoListTitle&&!todoListTitle.isFocused)todoListTitle.SetTextWithoutNotify(story.CurrentList.title);
            FeatureCaption("nextList",story.CurrentList.title+" ▾");
            string signature=pane+story.WorkspaceRevision+calendarMonth.ToString("yyyyMM")+calendarDate.ToString("yyyyMMdd")+habitWeek.ToString("yyyyMMdd")+habitEdit+statisticsSpan;
            if(signature==workspaceSignature)return;workspaceSignature=signature;
            if(pane=="notes"){RefreshNotePages();if(noteScrollId!=w.noteId){noteScrollId=w.noteId;var scroll=Get<TMP_InputField>("CurrentPageUI","_mainInputField")?.GetComponentInParent<ScrollRect>();if(scroll){restoringNoteScroll=true;Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=story.CurrentNote.scroll;restoringNoteScroll=false;}}}if(pane=="calendar")RefreshCalendar();if(pane=="habits")RefreshHabits();if(pane=="statistics")RefreshStatistics();
        }
        void RefreshNotePages()
        {
            Clear(notePages);var parent=RefOf("SelectPageListUI","_selectPageUIParent");var source=RefOf("SelectPageListUI","dummyButton");if(!parent||!source)return;Set(source,false);Layout(parent,62);
            foreach(var page in player.story.Workspace.notes)
            {
                var go=Cell(source.gameObject,parent);var input=go.GetComponentInChildren<TMP_InputField>(true);if(input){input.SetTextWithoutNotify(page.title);input.readOnly=false;input.characterLimit=120;input.onEndEdit.RemoveAllListeners();input.onEndEdit.AddListener(value=>{player.story.SelectNote(page.id);player.story.RenameNote(value);});}
                foreach(var button in go.GetComponentsInChildren<Button>(true)){if(button.name=="RemoveButton")Bind(button.gameObject,()=>{player.story.SelectNote(page.id);player.story.DeleteNote();workspaceSignature=null;});else if(button.name=="SelectPageButton")Bind(button.gameObject,()=>{player.story.SelectNote(page.id);workspaceSignature=null;});else if(button.name=="DragButton"){var drag=button.gameObject.GetComponent<ChillNativeRowDrag>()??button.gameObject.AddComponent<ChillNativeRowDrag>();drag.moved=step=>{player.story.SelectNote(page.id);player.story.MoveNote(step);};}}notePages.Add(go);
            }
        }
        void RefreshCalendar()
        {
            var story=player.story;var cal=Find(ChangeRoot+"UI_FacilityCalendar/Calender");if(!cal)return;
            var yearLabel=cal.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="YearNoText");if(yearLabel)yearLabel.gameObject.SetActive(false);
            var monthLabel=cal.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="MonthNoText");if(monthLabel){monthLabel.text=calendarMonth.ToString("MMMM yyyy",CultureInfo.InvariantCulture);monthLabel.alignment=TextAlignmentOptions.Center;monthLabel.enableAutoSizing=true;monthLabel.fontSizeMin=18;monthLabel.fontSizeMax=30;var header=(RectTransform)monthLabel.transform;header.anchorMin=header.anchorMax=new Vector2(.5f,.5f);header.pivot=new Vector2(.5f,.5f);header.anchoredPosition=Vector2.zero;header.sizeDelta=new Vector2(245,45);}

            var first=new DateTime(calendarMonth.Year,calendarMonth.Month,1);var start=first.AddDays(-(int)first.DayOfWeek);var buttons=cal.Find("DayButtons");
            if(buttons){int i=0;foreach(Transform cell in buttons){if(!cell.name.StartsWith("CalenderDayButton"))continue;var date=start.AddDays(i++);var key=ChillStoryRuntime.DateKey(date);Show(cell.gameObject,true);SetText(cell.gameObject,"DayText",date.Day.ToString());var text=cell.GetComponentInChildren<TMP_Text>(true);if(text)text.color=date.Month==first.Month?Color.white:new Color(.6f,.6f,.7f);Bind(cell.gameObject,()=>{calendarDate=date;workspaceSignature=null;});var check=cell.Find("CheckImage");Set(check,story.Workspace.days.Any(d=>d.date==key&&d.seconds>0)||story.Workspace.lists.SelectMany(l=>l.items).Any(t=>t.completedDate==key));var selected=cell.Find("DayShowBase");Set(selected,date.Date==calendarDate.Date);}}
            var day=story.Day(ChillStoryRuntime.DateKey(calendarDate));var seconds=(long)day.seconds;
            foreach(var pair in new[]{("_workTimeHour",seconds/3600),("_workTimeMinutes",seconds/60%60),("_workTimeSeconds",seconds%60)}){var input=Get<TMP_InputField>("CalendarUI",pair.Item1);if(input&&!input.isFocused)input.SetTextWithoutNotify(pair.Item2.ToString("00"));}
            var diary=Get<TMP_InputField>("CalendarUI","_diaryInputField");if(diary&&!diary.isFocused)diary.SetTextWithoutNotify(day.diary);
            Clear(calendarRows);var parent=RefOf("CalendarUI","_completeTodoParent");if(!parent||!todoCell)return;Layout(parent,64);
            foreach(var task in story.Workspace.lists.SelectMany(l=>l.items).Where(t=>t.completedDate==day.date||!t.done&&t.due==day.date)){
                var go=Cell(todoCell,parent);var input=go.GetComponentInChildren<TMP_InputField>(true);if(input){input.SetTextWithoutNotify((task.done?"✓ ":"Due: ")+task.title);input.readOnly=true;}foreach(var button in go.GetComponentsInChildren<Button>(true)){if(button.name=="TodoSwitchFinishButton")Bind(button.gameObject,()=>story.ToggleTodoId(task.id));else if(button.name=="DateTime")Bind(button.gameObject,()=>{Open("todos");EditTodoDate(task);});else button.gameObject.SetActive(false);}calendarRows.Add(go);
            }
            foreach(var habit in story.Workspace.habits.Where(h=>h.completed.Contains(day.date))){var go=Cell(todoCell,parent);var input=go.GetComponentInChildren<TMP_InputField>(true);if(input){input.SetTextWithoutNotify("✓ "+habit.title);input.readOnly=true;}foreach(var button in go.GetComponentsInChildren<Button>(true))button.gameObject.SetActive(false);calendarRows.Add(go);}

        }
        void RefreshHabits()
        {
            Clear(habitRows);var parent=RefOf("HabitListUI","_habitParent");var source=RefOf("HabitListUI","dummyButton");if(!parent||!source)return;Set(source,false);Layout(parent,100);
            var start=habitWeek.Date.AddDays(-(int)habitWeek.DayOfWeek);var weekRoot=RefOf("HabitListUI","_weekLabelUI");
            if(weekRoot){var rows=JsonUtility.FromJson<Configuration>(configuration.text).bindings.Where(b=>b.script=="HabitDateLabelUI"&&b.path.StartsWith(PathOf(weekRoot)+"/")).OrderBy(b=>b.path,StringComparer.Ordinal).ToArray();for(int i=0;i<Math.Min(7,rows.Length);i++){var binding=rows[i];var date=start.AddDays(i);foreach(var r in binding.refs){var target=Find(r.path);if(!target)continue;var text=target.GetComponent<TMP_Text>();if(r.name=="_dayNumText"&&text)text.text=date.Day.ToString();if(r.name=="_dayOfWeekText"&&text)text.text=date.ToString("ddd",CultureInfo.InvariantCulture);if(r.name=="_dayOfWeekTextForEditMode")Set(target,habitEdit);if(r.name=="_dayOfWeekText")Set(target,!habitEdit);if(r.name=="_monthObj")Set(target,false);if(r.name=="_todayObj")Set(target,date.Date==DateTime.Today&&!habitEdit);if(r.name=="_completedImage")Set(target,false);}}}
            foreach(var habit in player.story.Workspace.habits)
            {
                var go=Cell(source.gameObject,parent);var input=go.GetComponentInChildren<TMP_InputField>(true);if(input){input.readOnly=false;input.characterLimit=120;input.SetTextWithoutNotify(habit.title);input.onEndEdit.RemoveAllListeners();input.onEndEdit.AddListener(value=>player.story.EditHabit(habit.id,value,habit.weekdays,habit.paused));}
                foreach(var button in go.GetComponentsInChildren<Button>(true)){if(button.name=="RemoveButton"){Show(button.gameObject,habitEdit);Bind(button.gameObject,()=>player.story.DeleteHabit(habit.id));}else if(button.name=="PlayPauseButton")Bind(button.gameObject,()=>player.story.EditHabit(habit.id,habit.title,habit.weekdays,!habit.paused));else if(button.name.Contains("Drag")){button.gameObject.SetActive(true);var drag=button.gameObject.GetComponent<ChillNativeRowDrag>()??button.gameObject.AddComponent<ChillNativeRowDrag>();drag.moved=step=>player.story.MoveHabit(habit.id,step);}}
                Set(go.transform.Find("PauseMask"),habit.paused);
                var days=go.transform.Find("Days");if(days)for(int i=0;i<Math.Min(7,days.childCount);i++){var cell=days.GetChild(i);var key=ChillStoryRuntime.DateKey(start.AddDays(i));int day=i;var button=cell.Find("BaseCircle")?.GetComponent<Button>();var edit=cell.Find("EditEnableButton");Set(edit,habitEdit);if(edit)Bind(edit.gameObject,()=>player.story.EditHabit(habit.id,habit.title,habit.weekdays^(1<<day),habit.paused));if(button){Bind(button.gameObject,()=>player.story.ToggleHabit(habit.id,key));button.interactable=player.story.HabitScheduled(habit,key);button.GetComponent<Image>().color=button.interactable?Color.white:new Color(1,1,1,.3f);}Set(cell.Find("Check"),habit.completed.Contains(key));Set(cell.Find("CheckBg"),habit.completed.Contains(key));}
                habitRows.Add(go);
            }
        }
        void RefreshStatistics()
        {
            var story=player.story;var first=statisticsSpan==0?calendarMonth.Date.AddDays(-(int)calendarMonth.DayOfWeek):statisticsSpan==2?new DateTime(calendarMonth.Year,1,1):new DateTime(calendarMonth.Year,calendarMonth.Month,1);var last=statisticsSpan==0?first.AddDays(6):statisticsSpan==2?first.AddYears(1).AddDays(-1):first.AddMonths(1).AddDays(-1);
            Write("TotalWorkStatisticsUI","_grandTotalWorkText",TimeSpan.FromSeconds(story.Workspace.legacyWorkSeconds+story.Workspace.days.Sum(d=>d.seconds)).TotalHours.ToString("0.0")+" h");Write("TotalWorkStatisticsUI","_spanTotalWorkText",TimeSpan.FromSeconds(story.WorkTotal(ChillStoryRuntime.DateKey(first),ChillStoryRuntime.DateKey(last))).TotalHours.ToString("0.0")+" h");
            Write("TotalWorkStatisticsUI","titleText",statisticsSpan==0?first.ToString("dd MMM")+" – "+last.ToString("dd MMM yyyy"):statisticsSpan==2?first.Year.ToString():first.ToString("MMMM yyyy"));Set(RefOf("TotalWorkStatisticsUI","invalidTitleText"),false);
            Write("StatisticsCoreUI","_centerSpanText",statisticsSpan==0?first.ToString("dd MMM")+" – "+last.ToString("dd MMM yyyy"):statisticsSpan==2?first.Year.ToString():first.ToString("MMMM yyyy"));Write("StatisticsCoreUI","_sinceText","");Write("StatisticsCoreUI","_untilText","");Write("StatisticsCoreUI","_slashText","");
            var parent=RefOf("TotalWorkStatisticsUI","_barGraphViewUI");if(!parent)return;foreach(Transform child in parent)Show(child.gameObject,false);
            int days=statisticsSpan==2?12:(last-first).Days+1;double Amount(int i){var from=statisticsSpan==2?first.AddMonths(i):first.AddDays(i);var to=statisticsSpan==2?from.AddMonths(1).AddDays(-1):from;return story.WorkTotal(ChillStoryRuntime.DateKey(from),ChillStoryRuntime.DateKey(to));}double max=1;for(int i=0;i<days;i++)max=Math.Max(max,Amount(i));
            for(int i=0;i<days;i++){var amount=Amount(i);var name="WebDay"+i;var existing=parent.Find(name);var go=existing?existing.gameObject:new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);Show(go,true);var rect=(RectTransform)go.transform;rect.anchorMin=new Vector2((float)i/days,0);rect.anchorMax=new Vector2((float)(i+1)/days,(float)(amount/max));rect.offsetMin=new Vector2(2,0);rect.offsetMax=new Vector2(-2,0);go.GetComponent<Image>().color=new Color(.25f,.9f,.85f,.8f);go.GetComponent<Image>().raycastTarget=false;}
        }
        void EditTodoDate(ChillTodo todo)
        {
            var root=RefOf("TodoListUI","self");if(!root)return;var old=root.Find("WebFeature_dueDate");if(old)Destroy(old.gameObject);
            var input=FeatureInput(root,"dueDate",todo.done?todo.completedDate:todo.due,-185,280,0);if(!input)return;input.characterLimit=10;input.onEndEdit.AddListener(value=>{if(value!=""&&!ChillStoryRuntime.ValidDate(value)){player.story.Notify("Use YYYY-MM-DD, or clear the date.",true);return;}player.story.EditTodo(todo.id,todo.title,todo.done?todo.due:value,todo.done?value:todo.completedDate);Destroy(input.gameObject);todoSignature=null;});input.ActivateInputField();
        }
    }
}
