using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Hlc.ChillPrototype;

public static class ChillStoryFlowCheck
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(object o,string key,object value)=>o.GetType().GetField(key,Flags).SetValue(o,value);
    static T Get<T>(object o,string key)=>(T)o.GetType().GetField(key,Flags).GetValue(o);
    static object Call(object o,string key,params object[] values)=>o.GetType().GetMethod(key,Flags).Invoke(o,values);
    static void Require(bool value,string why){if(!value)throw new InvalidOperationException("Story flow: "+why);}
    public static void Run(ChillStoryRuntime story,ChillGraphicsPlayer player,ChillStoryData data)
    {
        var original=Get<ChillSave>(story,"save");var isolated=JsonUtility.FromJson<ChillSave>(JsonUtility.ToJson(original));Set(story,"save",isolated);
        int choiceCursors=0,choiceCommits=0,groups=0,manualChoicePrompts=0;
        foreach(var group in data.groups)
        {
            var rows=data.novels.Where(r=>r.ScenarioGroupID==group.ID).ToArray();if(rows.Length==0)continue;groups++;
            Set(story,"chapter",group);Set(story,"sentences",rows);isolated.chapter=group.ID;
            for(int index=0;index<rows.Length;index++)
            {
                Set(story,"current",rows[index]);Set(story,"cursor",index);isolated.cursor=index;
                if(rows[index].Command==0||rows[index].Command==20)
                {bool expected=index+1<rows.Length&&rows[index+1].Command==3;Require((bool)Call(story,"NextSentenceIsSelection")==expected,"manual selection prompt policy mismatch");if(expected)manualChoicePrompts++;}
                if(rows[index].Command!=3&&rows[index].Command!=15)continue;
                int start=index,end=index;while(start>0&&rows[start-1].Command==rows[index].Command)start--;while(end+1<rows.Length&&rows[end+1].Command==rows[index].Command)end++;
                Call(story,"PrepareChoices",index,rows[index].Command);
                var options=Get<List<ChillSentence>>(story,"choices");Require(options.Count==end-start+1&&options[0]==rows[start],"resume loses earlier options at "+group.ID+":"+index);choiceCursors++;
                int optionCount=options.Count;
                for(int option=0;option<optionCount;option++)
                {
                    Call(story,"PrepareChoices",index,rows[index].Command);isolated.usedChoices.Clear();var selected=Get<List<ChillSentence>>(story,"choices")[option];Set(story,"chosen",option);
                    Call(story,"CommitChoice",rows[index].Command);
                    Require(Get<int>(story,"cursor")==Array.FindIndex(rows,r=>r.ID==selected.Arg1),"choice jumps to wrong source label");Require(Get<int>(story,"chosen")==-1,"selected index leaks into later speech");choiceCommits++;
                }
                if(rows[index].Command==15)
                {
                    Call(story,"PrepareChoices",index,15);isolated.usedChoices.Clear();isolated.usedChoices.Add(Get<List<ChillSentence>>(story,"choices")[0].Arg2);Set(story,"chosen",0);int before=Get<int>(story,"cursor");Call(story,"CommitChoice",15);Require(Get<int>(story,"cursor")==before,"used tutorial choice can be committed again");
                }
            }
        }
        // Source VoiceText does not change body, face or look, even when its row has a body value.
        var voiceRow=JsonUtility.FromJson<ChillSentence>(JsonUtility.ToJson(data.novels.First(r=>r.Command==20&&data.localizations.Any(t=>t.ID==r.LocalizationID&&!string.IsNullOrEmpty(t.En)))));
        voiceRow.BodyMotion=752; // Inject a conflicting body value to verify that VoiceText ignores it.
        Set(story,"chapter",data.groups.First(g=>g.ID==voiceRow.ScenarioGroupID));Set(story,"current",voiceRow);Get<List<ChillSentence>>(story,"choices").Clear();Set(story,"chosen",3);
        int motion=player.character.GetInteger("MotionType"),face=player.character.GetInteger("Facial");float look=player.character.GetFloat("LookScale");
        var speak=(IEnumerator)Call(story,"Speak",voiceRow,false,true);Require(speak.MoveNext(),"voice-only speech did not start");
        Require(player.character.GetInteger("MotionType")==motion&&player.character.GetInteger("Facial")==face&&Mathf.Abs(player.character.GetFloat("LookScale")-look)<.001f,"VoiceText overwrites an authored pose");
        Require(speak.MoveNext()&&speak.MoveNext(),"old choice skips an unrelated voice/typewriter wait");(speak as IDisposable)?.Dispose();
        foreach(string id in new[]{"selftalk_normal_001","motion_guts_001","motion_stretch_001","motion_gymnastics_001"})Require((string)Call(story,"Text",id)=="","Missing source caption leaks a debug placeholder into dialogue");
        var loop=data.novels.First(r=>r.Command==81);Set(story,"current",loop);Set(story,"loopChoiceVersion",7);
        var delayed=(IEnumerator)Call(story,"ShowLoopChoices",loop,Get<int>(story,"generation"),7);
        Require(delayed.MoveNext()&&delayed.Current is WaitForSecondsRealtime wait&&Mathf.Abs(wait.waitTime-.3f)<.001f,"loop ignores the source Arg4 selection delay");Call(story,"ResetCommandWaits");Require(!delayed.MoveNext(),"cancelled loop publishes stale choices");
        var restoreRows=new[]{new ChillSentence{Command=240,Arg1="0.2"},voiceRow,new ChillSentence{Command=2}};
        Set(story,"sentences",restoreRows);Set(story,"cursor",2);isolated.sceneHistory=new List<int>{0,1};Set(story,"voiceDefect",0f);motion=player.character.GetInteger("MotionType");
        var restore=(IEnumerator)Call(story,"ReadFromSavedScene",true);Require(restore.MoveNext(),"resume did not reconstruct state");Require(Mathf.Abs(Get<float>(story,"voiceDefect")-.2f)<.001f&&player.character.GetInteger("MotionType")==motion,"resume loses voice state or applies a VoiceText body motion");(restore as IDisposable)?.Dispose();
        Set(story,"chapter",null);Set(story,"current",null);Set(story,"sentences",null);Set(story,"typing",false);Set(story,"loadingAudio",false);Set(story,"chosen",-1);Set(story,"voiceDefect",0f);Get<List<ChillSentence>>(story,"choices").Clear();Set(story,"save",original);
        File.WriteAllText(Path.GetFullPath("../research/story-flow-check.json"),"{\"passed\":true,\"groups\":"+groups+",\"mainEpisodes\":36,\"choiceCursors\":"+choiceCursors+",\"choiceCommits\":"+choiceCommits+",\"manualSelectionPrompts\":"+manualChoicePrompts+",\"choiceResumeRestoresEntireBlock\":true,\"staleChoiceCannotSkipVoice\":true,\"voiceOnlyPreservesPose\":true,\"loopArg4DelayAndCancellation\":true,\"resumeRestoresVoiceState\":true,\"userSaveWritten\":false}");
        Debug.Log("HLC_STORY_FLOW_CHECK_PASSED groups="+groups+" choices="+choiceCommits);
    }
}
