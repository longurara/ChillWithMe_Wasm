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
        readonly List<GameObject> notePages=new List<GameObject>(),habitRows=new List<GameObject>(),calendarRows=new List<GameObject>(),todoListCells=new List<GameObject>();
        DateTime calendarMonth=DateTime.Today,calendarDate=DateTime.Today,habitWeek=DateTime.Today,todoMiniMonth=DateTime.Today,calendarMiniMonth=DateTime.Today;
        string workspaceSignature,todoListCellsSignature;int statisticsSpan=1;
        TMP_InputField todoListTitle;
        GameObject todoListPicker;
        bool todoPulldownOpen;
        ChillTodo editingTodoDateTarget,editingCalendarTodoTarget;

        public void SetTodoPulldownOpen(bool open)
        {
            todoPulldownOpen=open;
            var groupSelect=Find(ChangeRoot+"UI_FacilityTodo/TodoGroupSelect");
            var pulldown=groupSelect?groupSelect.Find("PullDown") as RectTransform:RefOf("TodoListUI","_pulldown") as RectTransform;
            if(!pulldown)return;
            Set(pulldown.Find("AddTodoListUI"),open);
            Set(pulldown.Find("Scroll View"),open);
            pulldown.sizeDelta=new Vector2(pulldown.sizeDelta.x,open?260f:40f);
            if(open){todoListCellsSignature=null;RefreshTodoListPulldown();}
        }
        void RefreshTodoListPulldown()
        {
            var story=player.story;if(!story.FeatureReady)return;
            var groupSelect=Find(ChangeRoot+"UI_FacilityTodo/TodoGroupSelect");
            var currentText=groupSelect?.Find("PullDown/TodoGroupText (TMP)")?.GetComponent<TMP_Text>();
            if(currentText)currentText.text=story.CurrentList.title;
            if(!todoPulldownOpen)return;
            string sig=story.Workspace.listId+"|"+string.Join("|",story.Workspace.lists.Select(l=>l.id+":"+l.title));
            if(sig==todoListCellsSignature)return;
            todoListCellsSignature=sig;
            Clear(todoListCells);
            var parent=RefOf("TodoListUI","_todoListUIParent")
                ??groupSelect?.Find("PullDown/Scroll View/Viewport/Content");
            if(!parent||!todoListCell)return;
            Set(RefOf("TodoListUI","_dummyTodoListButton"),false);
            Layout(parent,54f);
            for(int i=0;i<story.Workspace.lists.Count;i++)
            {
                int listIndex=i;
                var list=story.Workspace.lists[i];
                string listId=list.id;
                var go=Cell(todoListCell,parent);
                var input=go.GetComponentInChildren<TMP_InputField>(true);
                if(input)
                {
                    input.richText=false;input.readOnly=false;input.characterLimit=120;
                    input.SetTextWithoutNotify(list.title);
                    input.onEndEdit.RemoveAllListeners();
                    input.onEndEdit.AddListener(val=>{
                        story.SelectTodoList(listId);
                        story.RenameTodoList(val);
                        todoListCellsSignature=null;
                        todoSignature=null;
                    });
                }
                foreach(var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if(t.name=="SelectTodoListButton")
                        Bind(t.gameObject,()=>{story.SelectTodoList(listId);todoSignature=null;SetTodoPulldownOpen(false);});
                    else if(t.name=="TodoListRemoveButton")
                    {
                        t.gameObject.SetActive(story.Workspace.lists.Count>1);
                        Bind(t.gameObject,()=>{if(story.Workspace.lists.Count>1){story.SelectTodoList(listId);story.DeleteTodoList();todoListCellsSignature=null;todoSignature=null;}});
                    }
                    else if(t.name=="DragButton")
                        t.gameObject.SetActive(false);
                }
                todoListCells.Add(go);
            }
        }
        public void OpenTodoMiniCalendar(ChillTodo todo)
        {
            if(todo==null)return;
            editingTodoDateTarget=todo;
            string raw=todo.done?(todo.completedDate??""):(todo.due??"");
            todoMiniMonth=DateTime.TryParse(raw,out var parsed)?new DateTime(parsed.Year,parsed.Month,1):new DateTime(DateTime.Today.Year,DateTime.Today.Month,1);
            var mini=Find(ChangeRoot+"UI_FacilityTodo/MiniCalender");
            if(!mini)return;
            Show(mini.gameObject,true);
            mini.SetAsLastSibling();
            RefreshTodoMiniCalendar();
        }
        public void CloseTodoMiniCalendar()
        {
            editingTodoDateTarget=null;
            Set(Find(ChangeRoot+"UI_FacilityTodo/MiniCalender"),false);
        }
        void RefreshTodoMiniCalendar()
        {
            var mini=Find(ChangeRoot+"UI_FacilityTodo/MiniCalender");
            if(!mini||!mini.gameObject.activeSelf||editingTodoDateTarget==null)return;
            var story=player.story;
            todoMiniMonth=ClampCalendarDate(todoMiniMonth);
            var yearLabel=mini.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="YearNoText");
            if(yearLabel)yearLabel.text=todoMiniMonth.Year.ToString();
            var monthLabel=mini.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="MonthNoText");
            if(monthLabel)monthLabel.text=todoMiniMonth.Month.ToString("00");
            var first=new DateTime(todoMiniMonth.Year,todoMiniMonth.Month,1);
            var start=story.StartOfWeek(first);
            var weekdayLabels=mini.GetComponentsInChildren<TMP_Text>(true);
            for(int i=0;i<7;i++)
            {
                string name=((DayOfWeek)((story.Settings.firstDayInWeek+i)%7))+"Text";
                var label=weekdayLabels.FirstOrDefault(t=>t.name==name);
                if(label)label.transform.SetAsLastSibling();
            }
            string activeDate=editingTodoDateTarget.done?(editingTodoDateTarget.completedDate??""):(editingTodoDateTarget.due??"");
            var buttons=mini.Find("DayButtons");
            if(buttons)
            {
                int i=0;
                foreach(Transform cell in buttons)
                {
                    if(!cell.name.StartsWith("CalenderDayButton"))continue;
                    var date=start.AddDays(i++);
                    string key=ChillStoryRuntime.DateKey(date);
                    Show(cell.gameObject,true);
                    SetText(cell.gameObject,"DayText",date.Day.ToString());
                    var text=cell.GetComponentInChildren<TMP_Text>(true);
                    if(text)text.color=date.Month==first.Month?Color.white:new Color(.6f,.6f,.7f);
                    Set(cell.Find("CheckImage"),false);
                    Set(cell.Find("DayShowBase"),key==activeDate);
                    var targetTodo=editingTodoDateTarget;
                    Bind(cell.gameObject,()=>{
                        string nextVal=key==activeDate?"":key;
                        story.EditTodo(targetTodo.id,targetTodo.title,targetTodo.done?targetTodo.due:nextVal,targetTodo.done?nextVal:targetTodo.completedDate);
                        todoSignature=null;
                        workspaceSignature=null;
                        CloseTodoMiniCalendar();
                    });
                }
            }
        }
        public void OpenCalendarMiniCalendar(ChillTodo todo)
        {
            if(todo==null)return;
            var mini=Find(ChangeRoot+"UI_FacilityCalendar/MiniCalender");
            if(!mini)return;
            if(editingCalendarTodoTarget!=null&&editingCalendarTodoTarget.id==todo.id&&mini.gameObject.activeSelf)
            {
                CloseCalendarMiniCalendar();
                return;
            }
            editingCalendarTodoTarget=todo;
            string raw=todo.done?(todo.completedDate??""):(todo.due??"");
            calendarMiniMonth=DateTime.TryParse(raw,out var parsed)?new DateTime(parsed.Year,parsed.Month,1):new DateTime(calendarDate.Year,calendarDate.Month,1);
            Show(mini.gameObject,true);
            mini.SetAsLastSibling();
            RefreshCalendarMiniCalendar();
        }
        public void CloseCalendarMiniCalendar()
        {
            editingCalendarTodoTarget=null;
            Set(Find(ChangeRoot+"UI_FacilityCalendar/MiniCalender"),false);
        }
        void RefreshCalendarMiniCalendar()
        {
            var mini=Find(ChangeRoot+"UI_FacilityCalendar/MiniCalender");
            if(!mini||!mini.gameObject.activeSelf||editingCalendarTodoTarget==null)return;
            var story=player.story;
            calendarMiniMonth=ClampCalendarDate(calendarMiniMonth);
            var yearLabel=mini.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="YearNoText");
            if(yearLabel)yearLabel.text=calendarMiniMonth.Year.ToString();
            var monthLabel=mini.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="MonthNoText");
            if(monthLabel)monthLabel.text=calendarMiniMonth.Month.ToString("00");
            var first=new DateTime(calendarMiniMonth.Year,calendarMiniMonth.Month,1);
            var start=story.StartOfWeek(first);
            var weekdayLabels=mini.GetComponentsInChildren<TMP_Text>(true);
            for(int i=0;i<7;i++)
            {
                string name=((DayOfWeek)((story.Settings.firstDayInWeek+i)%7))+"Text";
                var label=weekdayLabels.FirstOrDefault(t=>t.name==name);
                if(label)label.transform.SetAsLastSibling();
            }
            string activeDate=editingCalendarTodoTarget.done?(editingCalendarTodoTarget.completedDate??""):(editingCalendarTodoTarget.due??"");
            var buttons=mini.Find("DayButtons");
            if(buttons)
            {
                int i=0;
                foreach(Transform cell in buttons)
                {
                    if(!cell.name.StartsWith("CalenderDayButton"))continue;
                    var date=start.AddDays(i++);
                    string key=ChillStoryRuntime.DateKey(date);
                    Show(cell.gameObject,true);
                    SetText(cell.gameObject,"DayText",date.Day.ToString());
                    var text=cell.GetComponentInChildren<TMP_Text>(true);
                    if(text)text.color=date.Month==first.Month?Color.white:new Color(.6f,.6f,.7f);
                    Set(cell.Find("CheckImage"),false);
                    Set(cell.Find("DayShowBase"),key==activeDate);
                    var targetTodo=editingCalendarTodoTarget;
                    Bind(cell.gameObject,()=>{
                        story.EditTodo(targetTodo.id,targetTodo.title,targetTodo.done?targetTodo.due:key,targetTodo.done?key:targetTodo.completedDate);
                        todoSignature=null;
                        workspaceSignature=null;
                        CloseCalendarMiniCalendar();
                        RefreshNow();
                    });
                }
            }
        }
        public static DateTime ClampCalendarDate(DateTime date)=>date.Year<2025?new DateTime(2025,1,1):date.Year>=2999?new DateTime(2998,12,1):date;
        public void StepCalendarMonth(int months){calendarMonth=ClampCalendarDate(calendarMonth.AddMonths(months));workspaceSignature=null;}
        public int GetStatisticsProgress()
        {
            var story=player.story;
            if(story.PreviewUnlock||story.Workspace.collaboration>=4)return 4;
            var completed=story.UISnapshot("notes");
            for(int ep=4;ep>=1;ep--)if(story.ChapterCompleted("special_statisticsDev_scenario_00"+ep))return ep;
            return 0;
        }
        bool completedExpanded,habitEdit,restoringNoteScroll;string noteScrollId;
        string pendingDeleteHabitId;
        Transform pendingDeleteHabitTarget;
        int habitDeleteOpenedFrame=-1;
        Coroutine paneAnimation;
        [Serializable] class HabitSnapshot{public List<ChillHabit> habits,deletedHabits;}
        string habitSnapshot;
        public void ShowHabitDeleteConfirm(string habitId,Transform targetButton)
        {
            var dialog=RefOf("HabitListUI","_deleteWarningDialog") as RectTransform;
            if(!dialog||string.IsNullOrEmpty(habitId))return;
            pendingDeleteHabitId=habitId;
            pendingDeleteHabitTarget=targetButton;
            habitDeleteOpenedFrame=Time.frameCount;
            if(targetButton)
            {
                float scaleX=(float)Screen.width/1920f;
                float scaleY=(float)Screen.height/1080f;
                dialog.position=targetButton.position+new Vector3(315f*scaleX,-135f*scaleY,0f);
            }
            Show(dialog.gameObject,true);
            dialog.SetAsLastSibling();
        }
        public void CloseHabitDeleteConfirm()
        {
            pendingDeleteHabitId=null;
            pendingDeleteHabitTarget=null;
            Set(RefOf("HabitListUI","_deleteWarningDialog"),false);
        }
        public void ConfirmHabitDelete()
        {
            if(!string.IsNullOrEmpty(pendingDeleteHabitId))
            {
                player.story.DeleteHabit(pendingDeleteHabitId);
                workspaceSignature=null;
            }
            CloseHabitDeleteConfirm();
        }
        void UpdateHabitDeletePopover()
        {
            var dialog=RefOf("HabitListUI","_deleteWarningDialog") as RectTransform;
            if(!dialog||!dialog.gameObject.activeInHierarchy)return;
            if(!habitEdit||pendingDeleteHabitTarget&&!pendingDeleteHabitTarget.gameObject.activeInHierarchy)
            {
                CloseHabitDeleteConfirm();
                return;
            }
            if(pendingDeleteHabitTarget)
            {
                float scaleX=(float)Screen.width/1920f;
                float scaleY=(float)Screen.height/1080f;
                dialog.position=pendingDeleteHabitTarget.position+new Vector3(315f*scaleX,-135f*scaleY,0f);
            }
            if(Time.frameCount>habitDeleteOpenedFrame&&Input.GetMouseButtonDown(0)&&!Contains(dialog)&&!(pendingDeleteHabitTarget is RectTransform tr&&Contains(tr)))
            {
                CloseHabitDeleteConfirm();
            }
        }
        void FinishHabitEdit(bool saveChanges)
        {
            CloseHabitDeleteConfirm();
            if(!saveChanges&&habitSnapshot!=null){var snapshot=JsonUtility.FromJson<HabitSnapshot>(habitSnapshot);player.story.Workspace.habits=snapshot.habits;player.story.Workspace.deletedHabits=snapshot.deletedHabits;}
            habitEdit=false;habitSnapshot=null;player.story.SaveTimerAudio();workspaceSignature=null;
        }
        void InitializeWorkspaceControls()
        {
            var story=player.story;
            foreach(var pair in new[]{("HabitListUI","_habitParent"),("TodoListUI","_uncompleteTodoUIParent"),("TodoListUI","_completeTodoUIParent"),("CalendarUI","_completeTodoParent"),("CalendarUI","_completeHabitParent")}){
                var content=RefOf(pair.Item1,pair.Item2);if(content)foreach(Transform child in content)child.gameObject.SetActive(false);
            }
            var title=Get<TMP_InputField>("CurrentPageUI","_titleInputField");
            if(title){title.readOnly=false;title.characterLimit=120;title.onEndEdit.RemoveAllListeners();title.onEndEdit.AddListener(story.RenameNote);}
            Wire("SelectPageListUI","_addSelectPageUIButton",()=>{story.AddNote();workspaceSignature=null;});

            Set(RefOf("TodoListUI","_pulldown"),true);
            var groupSelect=Find(ChangeRoot+"UI_FacilityTodo/TodoGroupSelect");
            Set(groupSelect,true);
            var pulldownBtn=groupSelect?.Find("PulldownButton");
            if(pulldownBtn)Bind(pulldownBtn.gameObject,()=>SetTodoPulldownOpen(!todoPulldownOpen));
            Wire("TodoListUI","_addTodoListButton",()=>{story.AddTodoList();todoListCellsSignature=null;todoSignature=null;});
            SetTodoPulldownOpen(false);
            Wire("TodoListUI","addTodoButton",()=>{story.AddTodo("New task");todoSignature=null;});
            var notesScroll=Get<TMP_InputField>("CurrentPageUI","_mainInputField")?.GetComponentInParent<ScrollRect>();if(notesScroll)notesScroll.onValueChanged.AddListener(value=>{if(!restoringNoteScroll){story.CurrentNote.scroll=notesScroll.verticalNormalizedPosition;}});
            Wire("TodoListUI","_toggleCompletedSectionButton",()=>{completedExpanded=!completedExpanded;todoSignature=null;RefreshNow();FitOpenPanel();});
            Wire("CalendarUI","_facilityOpenButton",()=>Open("calendar"));Wire("CalendarUI","_closeButton",CloseExclusivePane);
            Wire("HabitTrackerUI","_facilityOpenButton",()=>Open("habits"));Wire("HabitTrackerUI","_closeButton",()=>CloseFloatingPane("habits"));
            Wire("TotalWorkStatisticsUI","_openButton",()=>Open("statistics"));Wire("TotalWorkStatisticsUI","_closeButton",CloseExclusivePane);Wire("TotalWorkStatisticsUI","_returnToCalendarButton",()=>Open("calendar"));
            Wire("StatisticsCoreUI","nextSpanButton",()=>{calendarMonth=ClampCalendarDate(statisticsSpan==0?calendarMonth.AddDays(7):statisticsSpan==2?calendarMonth.AddYears(1):calendarMonth.AddMonths(1));workspaceSignature=null;});Wire("StatisticsCoreUI","prevSpanButton",()=>{calendarMonth=ClampCalendarDate(statisticsSpan==0?calendarMonth.AddDays(-7):statisticsSpan==2?calendarMonth.AddYears(-1):calendarMonth.AddMonths(-1));workspaceSignature=null;});
            var calMini=Find(ChangeRoot+"UI_FacilityCalendar/MiniCalender");
            Set(calMini,false);
            if(calMini)
            {
                WirePath(PathOf(calMini)+"/YearMonth/NextButtonUI/NextButton",()=>{calendarMiniMonth=ClampCalendarDate(calendarMiniMonth.AddMonths(1));RefreshCalendarMiniCalendar();});
                WirePath(PathOf(calMini)+"/YearMonth/BackButtonUI/BackButton",()=>{calendarMiniMonth=ClampCalendarDate(calendarMiniMonth.AddMonths(-1));RefreshCalendarMiniCalendar();});
            }
            var todoMini=Find(ChangeRoot+"UI_FacilityTodo/MiniCalender");
            Set(todoMini,false);
            if(todoMini)
            {
                WirePath(PathOf(todoMini)+"/YearMonth/NextButtonUI/NextButton",()=>{todoMiniMonth=ClampCalendarDate(todoMiniMonth.AddMonths(1));RefreshTodoMiniCalendar();});
                WirePath(PathOf(todoMini)+"/YearMonth/BackButtonUI/BackButton",()=>{todoMiniMonth=ClampCalendarDate(todoMiniMonth.AddMonths(-1));RefreshTodoMiniCalendar();});
            }
            var cal=Find(ChangeRoot+"UI_FacilityCalendar/Calender");
            if(cal){WirePath(PathOf(cal)+"/YearMonth/NextButtonUI/NextButton",()=>StepCalendarMonth(1));WirePath(PathOf(cal)+"/YearMonth/BackButtonUI/BackButton",()=>StepCalendarMonth(-1));}
            var diary=Get<TMP_InputField>("CalendarUI","_diaryInputField");if(diary){diary.readOnly=false;diary.richText=false;diary.characterLimit=64000;diary.onEndEdit.RemoveAllListeners();diary.onEndEdit.AddListener(value=>story.SetDiary(ChillStoryRuntime.DateKey(calendarDate),value));}
            foreach(string key in new[]{"_workTimeHour","_workTimeMinutes","_workTimeSeconds"}){var input=Get<TMP_InputField>("CalendarUI",key);if(input){input.readOnly=false;input.contentType=TMP_InputField.ContentType.IntegerNumber;input.onEndEdit.RemoveAllListeners();input.onEndEdit.AddListener(_=>SaveCalendarTime());}}
            Wire("HabitListUI","_addHabitUIButton",()=>{story.AddHabit("New habit");workspaceSignature=null;});
            Wire("HabitListUI","_editModeButton",()=>{if(habitEdit)return;habitSnapshot=JsonUtility.ToJson(new HabitSnapshot{habits=story.Workspace.habits,deletedHabits=story.Workspace.deletedHabits});habitEdit=true;workspaceSignature=null;});
            var editMode=RefOf("HabitListUI","_editModeButton");if(editMode){var discard=editMode.Find("Buttons/DiscardButton");var accept=editMode.Find("Buttons/SaveButton");if(discard)Bind(discard.gameObject,()=>FinishHabitEdit(false));if(accept)Bind(accept.gameObject,()=>FinishHabitEdit(true));}
            var delDialog=RefOf("HabitListUI","_deleteWarningDialog");
            Set(delDialog,false);
            if(delDialog)
            {
                var yesBtn=delDialog.Find("Buttons/YesButton")??delDialog.Find("YesButton");
                var cancelBtn=delDialog.Find("Buttons/CancelButton")??delDialog.Find("CancelButton")??delDialog.Find("Buttons/NoButton")??delDialog.Find("NoButton");
                if(yesBtn)Bind(yesBtn.gameObject,ConfirmHabitDelete);
                if(cancelBtn)Bind(cancelBtn.gameObject,CloseHabitDeleteConfirm);
            }
            var habitScroll=RefOf("HabitListUI","_habitParent")?.GetComponentInParent<ScrollRect>();
            if(habitScroll)habitScroll.onValueChanged.AddListener(_=>CloseHabitDeleteConfirm());
            foreach(string kind in new[]{"CalendarUI","HabitTrackerUI","TotalWorkStatisticsUI"})PreparePane(RefOf(kind,"self"));
        }
        void PreparePane(Transform root)
        {
            if(!root)return;var handle=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="DragButton");
            if(handle){var drag=handle.GetComponent<ChillNativeDrag>()??handle.gameObject.AddComponent<ChillNativeDrag>();drag.target=root as RectTransform;drag.saved=position=>player.story.SavePanel(PathOf(root),position);}
        }
        readonly Dictionary<string,float> diaryScrollPositions=new Dictionary<string,float>();
        string lastNoteClickId="";float lastNoteClickTime;
        void ShowWorkspacePane()
        {
            if(!openPane)return;
            for(var root=openPane.transform;root&&root!=canvas.transform;root=root.parent)root.gameObject.SetActive(true);
            var rect=openPane.transform as RectTransform;var position=player.story.PanelPosition(PathOf(rect));if(position.HasValue)rect.anchoredPosition=ChillNativeDrag.AdjustPos(rect,position.Value);
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
            RefreshTodoListPulldown();
            string signature=pane+exclusivePane+string.Join(",",activeFloatingPanes)+story.WorkspaceRevision+calendarMonth.ToString("yyyyMM")+calendarDate.ToString("yyyyMMdd")+habitWeek.ToString("yyyyMMdd")+habitEdit+statisticsSpan;
            if(signature==workspaceSignature)return;workspaceSignature=signature;
            if(IsPaneOpen("notes")){RefreshNotePages();if(noteScrollId!=w.noteId){noteScrollId=w.noteId;var scroll=Get<TMP_InputField>("CurrentPageUI","_mainInputField")?.GetComponentInParent<ScrollRect>();if(scroll){restoringNoteScroll=true;Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=story.CurrentNote.scroll;restoringNoteScroll=false;}}}
            if(IsPaneOpen("calendar"))RefreshCalendar();
            if(IsPaneOpen("habits"))RefreshHabits();
            if(IsPaneOpen("statistics"))RefreshStatistics();
        }
        void RefreshNotePages()
        {
            Clear(notePages);var parent=RefOf("SelectPageListUI","_selectPageUIParent");var source=RefOf("SelectPageListUI","dummyButton");if(!parent||!source)return;Set(source,false);Layout(parent,37.8f);
            bool canRemove=player.story.Workspace.notes.Count>1;
            foreach(var page in player.story.Workspace.notes)
            {
                var go=Cell(source.gameObject,parent);var input=go.GetComponentInChildren<TMP_InputField>(true);
                if(input){input.SetTextWithoutNotify(page.title);input.readOnly=true;input.characterLimit=120;input.onEndEdit.RemoveAllListeners();input.onEndEdit.AddListener(value=>{input.readOnly=true;player.story.SelectNote(page.id);player.story.RenameNote(value);});}
                foreach(var button in go.GetComponentsInChildren<Button>(true))
                {
                    if(button.name=="RemoveButton")
                    {
                        button.gameObject.SetActive(canRemove);
                        Bind(button.gameObject,()=>{if(player.story.Workspace.notes.Count>1){player.story.SelectNote(page.id);player.story.DeleteNote();workspaceSignature=null;}});
                    }
                    else if(button.name=="SelectPageButton")
                    {
                        Bind(button.gameObject,()=>{
                            float now=Time.unscaledTime;
                            if(lastNoteClickId==page.id&&now-lastNoteClickTime<0.5f&&input){input.readOnly=false;input.ActivateInputField();lastNoteClickId="";return;}
                            lastNoteClickId=page.id;lastNoteClickTime=now;
                            player.story.SelectNote(page.id);workspaceSignature=null;
                        });
                    }
                    else if(button.name=="DragButton"){var drag=button.gameObject.GetComponent<ChillNativeRowDrag>()??button.gameObject.AddComponent<ChillNativeRowDrag>();drag.rowHeight=38.3f;drag.moved=step=>{player.story.SelectNote(page.id);player.story.MoveNote(step);};}
                }
                notePages.Add(go);
            }
        }
        void RefreshCalendar()
        {
            var story=player.story;var cal=Find(ChangeRoot+"UI_FacilityCalendar/Calender");if(!cal)return;
            calendarMonth=ClampCalendarDate(calendarMonth);
            var yearLabel=cal.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="YearNoText");
            if(yearLabel){yearLabel.gameObject.SetActive(true);yearLabel.text=calendarMonth.Year.ToString();}
            var monthLabel=cal.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="MonthNoText");
            if(monthLabel)monthLabel.text=calendarMonth.Month.ToString("00");
            var first=new DateTime(calendarMonth.Year,calendarMonth.Month,1);var start=story.StartOfWeek(first);var buttons=cal.Find("DayButtons");
            var weekdayLabels=cal.GetComponentsInChildren<TMP_Text>(true);for(int i=0;i<7;i++){string name=((DayOfWeek)((story.Settings.firstDayInWeek+i)%7))+"Text";var label=weekdayLabels.FirstOrDefault(t=>t.name==name);if(label)label.transform.SetAsLastSibling();}
            if(buttons){int i=0;foreach(Transform cell in buttons){if(!cell.name.StartsWith("CalenderDayButton"))continue;var date=start.AddDays(i++);var key=ChillStoryRuntime.DateKey(date);Show(cell.gameObject,true);SetText(cell.gameObject,"DayText",date.Day.ToString());var text=cell.GetComponentInChildren<TMP_Text>(true);if(text)text.color=date.Month==first.Month?Color.white:new Color(.6f,.6f,.7f);Bind(cell.gameObject,()=>{var diaryScroll=Get<TMP_InputField>("CalendarUI","_diaryInputField")?.GetComponentInParent<ScrollRect>();if(diaryScroll)diaryScrollPositions[ChillStoryRuntime.DateKey(calendarDate)]=diaryScroll.verticalNormalizedPosition;calendarDate=date;workspaceSignature=null;});var check=cell.Find("CheckImage");Set(check,story.Workspace.days.Any(d=>d.date==key&&d.seconds>0)||story.Workspace.lists.SelectMany(l=>l.items).Any(t=>t.done&&t.completedDate==key)||story.CompletedHabits(key).Any());var selected=cell.Find("DayShowBase");Set(selected,date.Date==calendarDate.Date);}}
            var day=story.Day(ChillStoryRuntime.DateKey(calendarDate));var seconds=(long)day.seconds;
            foreach(var pair in new[]{("_workTimeHour",seconds/3600),("_workTimeMinutes",seconds/60%60),("_workTimeSeconds",seconds%60)}){var input=Get<TMP_InputField>("CalendarUI",pair.Item1);if(input&&!input.isFocused)input.SetTextWithoutNotify(pair.Item2.ToString("00"));}
            var diary=Get<TMP_InputField>("CalendarUI","_diaryInputField");
            if(diary&&!diary.isFocused)
            {
                diary.SetTextWithoutNotify(day.diary);
                var diaryScroll=diary.GetComponentInParent<ScrollRect>();
                if(diaryScroll){Canvas.ForceUpdateCanvases();diaryScroll.verticalNormalizedPosition=diaryScrollPositions.TryGetValue(day.date,out float pos)?pos:1f;}
            }
            Clear(calendarRows);
            var parent=RefOf("CalendarUI","_completeTodoParent");if(!parent||!todoCell)return;Layout(parent,64);
            var habitParent=RefOf("CalendarUI","_completeHabitParent")??parent;
            if(habitParent!=parent)Layout(habitParent,64);
            foreach(var task in story.Workspace.lists.SelectMany(l=>l.items).Where(t=>t.done&&t.completedDate==day.date)){
                var go=Cell(todoCell,parent);var input=go.GetComponentInChildren<TMP_InputField>(true);
                if(input){input.SetTextWithoutNotify(task.title);input.readOnly=false;input.onEndEdit.RemoveAllListeners();input.onEndEdit.AddListener(val=>story.EditTodo(task.id,val,task.due,task.completedDate));}
                foreach(var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if(t.name=="DateTime")
                    {
                        var expireIcon=t.Find("ExpireIcon");if(expireIcon)expireIcon.gameObject.SetActive(false);
                        string rawDate=task.completedDate??"";
                        var label=t.GetComponentInChildren<TMP_Text>(true);
                        if(label&&DateTime.TryParse(rawDate,out var parsedDate))label.text=parsedDate.ToString("MM/dd");
                    }
                    if(t.name=="BackCompleteImage"||t.name=="TodoSwitchFinishButtonImage")t.gameObject.SetActive(true);
                }
                foreach(var button in go.GetComponentsInChildren<Button>(true)){if(button.name=="TodoSwitchFinishButton")Bind(button.gameObject,()=>story.ToggleTodoId(task.id));else if(button.name=="DateTime")Bind(button.gameObject,()=>OpenCalendarMiniCalendar(task));else button.gameObject.SetActive(false);}
                calendarRows.Add(go);
            }
            foreach(var habit in story.CompletedHabits(day.date))
            {
                var prefab=calendarHabitCell?calendarHabitCell:todoCell;
                var go=Cell(prefab,habitParent);
                SetText(go,"DateText",calendarDate.ToString("MM/dd"));
                var input=go.GetComponentInChildren<TMP_InputField>(true);
                if(input){input.SetTextWithoutNotify(habit.title);input.readOnly=false;input.onEndEdit.RemoveAllListeners();input.onEndEdit.AddListener(val=>story.EditHabit(habit.id,val,habit.weekdays,habit.paused));}
                foreach(var button in go.GetComponentsInChildren<Button>(true))
                {
                    if(button.name=="DeleteButton"||button.name=="RemoveButton"||button.name=="TodoRemoveButton")
                    {
                        button.gameObject.SetActive(true);
                        Bind(button.gameObject,()=>story.HideHabitOnCalendar(habit.id,day.date));
                    }
                    else if(prefab==todoCell)button.gameObject.SetActive(false);
                }
                calendarRows.Add(go);
            }
        }
        void RefreshHabits()
        {
            var mode=RefOf("HabitListUI","_editModeButton");if(mode){Set(mode.Find("Buttons"),habitEdit);Set(mode.Find("IconActive"),habitEdit);foreach(var text in mode.GetComponentsInChildren<TMP_Text>(true))text.gameObject.SetActive(!habitEdit);}
            Clear(habitRows);var parent=RefOf("HabitListUI","_habitParent");var source=RefOf("HabitListUI","dummyButton");if(!parent||!source)return;Set(source,false);Layout(parent,63);
            var start=player.story.StartOfWeek(habitWeek);var weekRoot=RefOf("HabitListUI","_weekLabelUI");
            if(weekRoot){var rows=JsonUtility.FromJson<Configuration>(configuration.text).bindings.Where(b=>b.script=="HabitDateLabelUI"&&b.path.StartsWith(PathOf(weekRoot)+"/")).OrderBy(b=>b.path,StringComparer.Ordinal).ToArray();for(int i=0;i<Math.Min(7,rows.Length);i++){var binding=rows[i];var date=start.AddDays(i);var dayKey=ChillStoryRuntime.DateKey(date);bool showMonth=(i==0||date.Day==1)&&!habitEdit;var scheduled=player.story.Workspace.habits.Where(h=>!h.paused&&player.story.HabitScheduled(h,dayKey)).ToArray();bool allCompleted=scheduled.Length>0&&scheduled.All(h=>h.completed.Contains(dayKey))&&!habitEdit;foreach(var r in binding.refs){var target=Find(r.path);if(!target)continue;var text=target.GetComponent<TMP_Text>();if(r.name=="_dayNumText"){Set(target,!habitEdit);if(text)text.text=date.Day.ToString();}if(r.name=="_dayOfWeekText"&&text)text.text=date.ToString("ddd",CultureInfo.InvariantCulture);if(r.name=="_dayOfWeekTextForEditMode")Set(target,habitEdit);if(r.name=="_dayOfWeekText")Set(target,!habitEdit);if(r.name=="_monthText"&&text)text.text=date.Month.ToString();if(r.name=="_monthObj"){Set(target,showMonth);var mText=target.GetComponentInChildren<TMP_Text>(true);if(mText)mText.text=date.Month.ToString();}if(r.name=="_todayObj")Set(target,date.Date==DateTime.Today&&!habitEdit);if(r.name=="_completedImage")Set(target,allCompleted);}}}
            foreach(var habit in player.story.Workspace.habits)
            {
                var go=Cell(source.gameObject,parent);var input=go.GetComponentInChildren<TMP_InputField>(true);if(input){input.readOnly=!habitEdit;input.characterLimit=120;input.SetTextWithoutNotify(habit.title);input.onEndEdit.RemoveAllListeners();input.onEndEdit.AddListener(value=>player.story.EditHabit(habit.id,value,habit.weekdays,habit.paused));}
                foreach(var button in go.GetComponentsInChildren<Button>(true)){if(button.name=="RemoveButton"){Show(button.gameObject,habitEdit);var btnTr=button.transform;string hId=habit.id;Bind(button.gameObject,()=>ShowHabitDeleteConfirm(hId,btnTr));}else if(button.name=="PlayPauseButton")Bind(button.gameObject,()=>player.story.EditHabit(habit.id,habit.title,habit.weekdays,!habit.paused));else if(button.name.Contains("Drag")){button.gameObject.SetActive(true);var drag=button.gameObject.GetComponent<ChillNativeRowDrag>()??button.gameObject.AddComponent<ChillNativeRowDrag>();drag.rowHeight=61f;drag.moved=step=>player.story.MoveHabit(habit.id,step);}}
                Set(go.transform.Find("PauseMask"),habit.paused);
                var days=go.transform.Find("Days");if(days)for(int i=0;i<Math.Min(7,days.childCount);i++){var cell=days.GetChild(i);var key=ChillStoryRuntime.DateKey(start.AddDays(i));int day=(int)start.AddDays(i).DayOfWeek;var button=cell.Find("BaseCircle")?.GetComponent<Button>();var edit=cell.Find("EditEnableButton");Set(edit,habitEdit);if(edit)Bind(edit.gameObject,()=>player.story.EditHabit(habit.id,habit.title,habit.weekdays^(1<<day),habit.paused));if(button){Bind(button.gameObject,()=>player.story.ToggleHabit(habit.id,key));button.interactable=player.story.HabitScheduled(habit,key);button.GetComponent<Image>().color=button.interactable?Color.white:new Color(1,1,1,.3f);}Set(cell.Find("Check"),habit.completed.Contains(key));Set(cell.Find("CheckBg"),habit.completed.Contains(key));}
                habitRows.Add(go);
            }
        }
        void RefreshStatistics()
        {
            var story=player.story;var first=statisticsSpan==0?story.StartOfWeek(calendarMonth):statisticsSpan==2?new DateTime(calendarMonth.Year,1,1):new DateTime(calendarMonth.Year,calendarMonth.Month,1);var last=statisticsSpan==0?first.AddDays(6):statisticsSpan==2?first.AddYears(1).AddDays(-1):first.AddMonths(1).AddDays(-1);
            int progress=GetStatisticsProgress();
            var statsRoot=RefOf("TotalWorkStatisticsUI","self");
            if(statsRoot)
            {
                Set(statsRoot.Find("Phase1"),progress==1);
                Set(statsRoot.Find("Phase2"),progress==2);
                Set(statsRoot.Find("Phase3"),progress==3);
                Set(statsRoot.Find("GraphMain"),progress>=4||progress==0);
            }
            bool validTitle=progress!=1;
            Set(RefOf("TotalWorkStatisticsUI","titleText"),validTitle);Set(RefOf("TotalWorkStatisticsUI","invalidTitleText"),!validTitle);
            Write("TotalWorkStatisticsUI","titleText",story.UITranslate("ui_statistics_title"));
            foreach(var choice in story.Features.statisticsButtons??Array.Empty<ChillTimerButton>())
                Find(choice.path)?.GetComponent<ChillNativeInteraction>()?.SetUsing(choice.id==statisticsSpan);
            var grandTs=TimeSpan.FromSeconds(Math.Floor(story.Workspace.legacyWorkSeconds+story.Workspace.days.Sum(d=>Math.Min(d.seconds,86400d))));
            var spanTs=TimeSpan.FromSeconds(Math.Floor(story.WorkTotal(ChillStoryRuntime.DateKey(first),ChillStoryRuntime.DateKey(last))));
            Write("TotalWorkStatisticsUI","_grandTotalWorkText",(int)grandTs.TotalHours+":"+grandTs.ToString(@"mm\:ss"));
            Write("TotalWorkStatisticsUI","_spanTotalWorkText",(int)spanTs.TotalHours+":"+spanTs.ToString(@"mm\:ss"));
            bool isWeek=statisticsSpan==0;
            foreach(string k in new[]{"_centerSpanText","_sinceText","_untilText","_slashText"}){var t=Get<TMP_Text>("StatisticsCoreUI",k);if(t){t.enabled=k=="_centerSpanText"?!isWeek:isWeek;t.gameObject.SetActive(true);}}
            Write("StatisticsCoreUI","_centerSpanText",statisticsSpan==2?first.ToString("yyyy"):first.ToString("yyyy/MM"));
            Write("StatisticsCoreUI","_sinceText",isWeek?first.ToString("yyyy/MM/dd"):"");
            Write("StatisticsCoreUI","_untilText",isWeek?last.ToString("yyyy/MM/dd"):"");
            Write("StatisticsCoreUI","_slashText",isWeek?"/":"");
            var parent=RefOf("TotalWorkStatisticsUI","_barGraphViewUI");if(!parent)return;
            foreach(Transform child in parent)Show(child.gameObject,!child.name.StartsWith("WebDay")&&!child.name.Contains("Tooltip"));
            int days=statisticsSpan==2?12:(last-first).Days+1;
            double Amount(int i){var from=statisticsSpan==2?first.AddMonths(i):first.AddDays(i);var to=statisticsSpan==2?from.AddMonths(1).AddDays(-1):from;return story.WorkTotal(ChillStoryRuntime.DateKey(from),ChillStoryRuntime.DateKey(to));}
            float maxHours=2f;
            for(int i=0;i<days;i++){float h=(float)(Amount(i)/3600d);if(h>maxHours)maxHours=h>2f?2*((int)h/2+1):2f;}
            if(statisticsSpan!=2&&maxHours>24f)maxHours=24f;
            var graphGroup=parent.Find("GraphGroup");
            if(graphGroup)
            {
                var sampler=graphGroup.Find("GraphSampler");
                var bottomRect=(sampler?.Find("BottomGraph/Graph")??sampler?.Find("BottomGraph")) as RectTransform;
                var topRect=(sampler?.Find("TopGraph/Graph")??sampler?.Find("TopGraph")) as RectTransform;
                if(sampler)Set(sampler,false);
                var bars=graphGroup.Cast<Transform>().Where(t=>t.name.StartsWith("BarGraphContent")).ToArray();
                float maxH=topRect&&bottomRect?topRect.sizeDelta.y-bottomRect.sizeDelta.y:260f;
                float maxAnchorY=topRect&&bottomRect?topRect.anchoredPosition.y-bottomRect.anchoredPosition.y:130f;
                float baseAnchorY=bottomRect?bottomRect.anchoredPosition.y:0f;
                for(int j=0;j<bars.Length;j++)
                {
                    if(j>=days){bars[j].gameObject.SetActive(false);continue;}
                    bars[j].gameObject.SetActive(true);
                    float ratio=Mathf.Clamp01((float)(Amount(j)/3600d)/maxHours);
                    var graphRect=bars[j].Find("Graph") as RectTransform;
                    if(graphRect)
                    {
                        float w=days>12?24f:48f;
                        graphRect.sizeDelta=new Vector2(w,ratio<=0f?0f:maxH*ratio);
                        graphRect.anchoredPosition=new Vector2(0f,baseAnchorY+maxAnchorY*ratio);
                    }
                    var date=statisticsSpan==2?first.AddMonths(j):first.AddDays(j);
                    bool showDash=statisticsSpan==1&&date.DayOfWeek==(DayOfWeek)story.Settings.firstDayInWeek;
                    string labelText=statisticsSpan==2?(j+1).ToString():statisticsSpan==0?date.Day+"\n("+date.ToString("ddd",CultureInfo.InvariantCulture)+")":showDash?date.Day+"\n("+date.ToString("ddd",CultureInfo.InvariantCulture)+")":"";
                    var barLabel=bars[j].GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();
                    if(barLabel)barLabel.text=labelText;
                    foreach(var img in bars[j].GetComponentsInChildren<Image>(true))if(img.name.IndexOf("Dash",StringComparison.OrdinalIgnoreCase)>=0)img.enabled=showDash;
                }
            }
            var vertTexts=parent.GetComponentsInChildren<TMP_Text>(false).Where(t=>!graphGroup||!t.transform.IsChildOf(graphGroup)).OrderBy(t=>t.transform.position.y).ToArray();
            for(int k=0;k<Math.Min(5,vertTexts.Length);k++)
            {
                float mins=maxHours*(0.25f*(k>=4?4:k))*60f;
                var ts=TimeSpan.FromMinutes((int)mins);
                vertTexts[k].text=((int)ts.TotalHours).ToString("00")+":"+ts.Minutes.ToString("00");
            }
        }
    }
}
