using System;
using System.Collections;
using System.Linq;
using UnityEngine;
namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        public ChillChapter[] SpecialChapters=>data==null?Array.Empty<ChillChapter>():data.groups.Where(g=>g.Scenario>=50&&g.Scenario<=56&&chapters.ContainsKey(g.ID)).OrderBy(g=>g.Scenario).ThenBy(g=>g.EpisodeNumber).ToArray();
        Coroutine roomRoutine;
        int roomVersion;
        void CancelRoom(){roomVersion++;if(roomRoutine!=null)StopCoroutine(roomRoutine);roomRoutine=null;}
        public ChillUnlockRule UnlockRule(string kind,string item)=>Features?.unlocks?.FirstOrDefault(r=>r.kind==kind&&string.Equals(r.item,item,StringComparison.OrdinalIgnoreCase));
        public bool ItemAvailable(string kind,string item)
        {
            var rule=UnlockRule(kind,item);if(save.previewUnlock||rule==null||rule.condition=="None")return true;
            if(rule.condition=="PointPurchase")return Workspace.purchases.Contains(kind+":"+item);
            if(rule.condition=="Scenario")return rule.argument=="None"||save.completed.Contains(rule.argument);
            if(rule.condition=="Level")return int.TryParse(rule.argument,out int level)&&save.level>=level;
            return false;
        }
        public string ItemCondition(string kind,string item)
        {var rule=UnlockRule(kind,item);return ItemAvailable(kind,item)?"":rule==null?"":rule.condition=="PointPurchase"?rule.argument+" points":rule.argument;}
        public string WindowItem(int index)=>index==0?"":Features?.unlocks?.FirstOrDefault(r=>r.kind=="environment"&&player.environment.FindEnvironment(r.item)==index)?.item??"";
        public bool ChangeTimeAllowed(int index)=>Purchase("environment",new[]{"Day","Sunset","Night","Cloudy"}[Mathf.Clamp(index,0,3)]);
        public bool ChangeWindowAllowed(int index)=>index==0||Purchase("environment",WindowItem(index));
        public bool Purchase(string kind,string item)
        {
            if(ItemAvailable(kind,item))return true;var rule=UnlockRule(kind,item);
            if(rule==null||rule.condition!="PointPurchase"||!int.TryParse(rule.argument,out int price)||price<0||Workspace.points<price){Notify("Locked: "+ItemCondition(kind,item),true);return false;}
            Workspace.points-=price;Workspace.purchases.Add(kind+":"+item);WorkspaceChanged();return true;
        }
        public void ChooseDecoration(string skin)
        {
            if(Playing||!decorations)return;var item=decorations.Skins.FirstOrDefault(s=>s.name==skin);if(item==null||!Purchase("decoration",skin))return;
            Workspace.room.RemoveAll(r=>r.category==item.category);Workspace.room.Add(new ChillRoomChoice{category=item.category,skin=skin});WorkspaceChanged();RestoreRoom();
        }
        public void ClearDecoration(int category)
        {if(Playing)return;Workspace.room.RemoveAll(r=>r.category==category);WorkspaceChanged();RestoreRoom();}
        public void ResetRoom()
        {if(Playing)return;Workspace.room.Clear();Workspace.hair=Workspace.costume=0;WorkspaceChanged();RestoreRoom();}
        public void ChooseHair(int hair)
        {if(Playing)return;Workspace.hair=Mathf.Clamp(hair,0,decorations.Data.hairPins.Length);decorations.SetHair(Workspace.hair);WorkspaceChanged();}
        public void ChooseCostume(int index)
        {if(Playing)return;Workspace.costume=Mathf.Clamp(index,0,4);GetComponent<ChillWardrobe>()?.Apply(Workspace.costume);WorkspaceChanged();}
        public void SaveRoomPreset(string name)
        {name=Trim(name,80).Trim();if(name=="")return;var w=Workspace;var existing=w.presets.FirstOrDefault(p=>p.name==name);if(existing!=null)w.presets.Remove(existing);if(w.presets.Count>=20)return;w.presets.Add(new ChillRoomPreset{name=name,time=player.environment.selectedTime,window=player.environment.selectedEnvironment,hair=w.hair,costume=w.costume,choices=w.room.Select(r=>new ChillRoomChoice{category=r.category,skin=r.skin}).ToList()});WorkspaceChanged();}
        public void LoadRoomPreset(string name)
        {if(Playing)return;var preset=Workspace.presets.FirstOrDefault(p=>p.name==name);if(preset==null)return;Workspace.room=preset.choices.Where(r=>ItemAvailable("decoration",r.skin)).Select(r=>new ChillRoomChoice{category=r.category,skin=r.skin}).ToList();Workspace.hair=preset.hair;Workspace.costume=preset.costume;player.environment.SelectTime(preset.time);player.environment.SelectEnvironment(preset.window);WorkspaceChanged();RestoreRoom();}
        public void DeleteRoomPreset(string name){Workspace.presets.RemoveAll(p=>p.name==name);WorkspaceChanged();}
        void RestoreRoom()
        {if(!Application.isPlaying||VerificationOnly||Playing||!decorations)return;if(roomRoutine!=null)StopCoroutine(roomRoutine);roomRoutine=StartCoroutine(ApplyRoom(++roomVersion));}
        IEnumerator ApplyRoom(int token)
        {decorations.Restore();foreach(var item in Workspace.room.ToArray()){if(token!=roomVersion||Playing)yield break;yield return decorations.Apply(item.skin);if(token!=roomVersion||Playing)yield break;}decorations.SetHair(Workspace.hair);GetComponent<ChillWardrobe>()?.Apply(Workspace.costume);roomRoutine=null;}
        void CompletedAchievement(ChillChapter group)
        {
            if(group.Scenario==18)AwardAchievement("COMPLETE_TUTORIAL_COUNT");
            if(group.Scenario==9||group.Scenario>=500&&group.Scenario<=503)AwardAchievement("TALK_WORKING_COUNT");
            if(group.Scenario==10||group.Scenario>=550&&group.Scenario<=553)AwardAchievement("TALK_BREAKING_COUNT");
            if(group.Scenario==7)AwardAchievement("TALK_AFTER_WORK_COUNT");
            if(group.Scenario==1){if(group.EpisodeNumber==33)AwardAchievement("READ_GLASSES_STORY");if(group.EpisodeNumber==34)AwardAchievement("PUBLISH_SUMMERTIME_OVERDRIVE");if(group.EpisodeNumber==36)AwardAchievement("READ_ALL_MAIN_STORY");}
            var ids=new[]{"smalltalk_01","smalltalk_02","smalltalk_03","smalltalk_04"};var awards=new[]{"SMALL_TALK_HEATSTROKE","SMALL_TALK_GALAXY_EXPRESS","SMALL_TALK_CAT_HEADPHONE","SMALL_TALK_CHICADA"};int index=Array.IndexOf(ids,group.ID);if(index>=0)AwardAchievement(awards[index]);
        }
    }
}
