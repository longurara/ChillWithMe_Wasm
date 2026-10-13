using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace Hlc.ChillPrototype
{
    [Serializable] public class ChillTypingPolicy
    {
        public float waitLong=.125f,waitMiddle=.125f;
        public bool avoidMultiplePunctuactionWait,waitForNewLines=true,waitForLastCharacter=true;
    }
    // Source TypewriterByCharacter waits after punctuation and the last letter.
    // Action tags affect timing; they never appear as literal dialogue text.
    public sealed class ChillTypewriter
    {
        struct Step {public int end;public float before,after;public bool input,normal;}
        readonly List<Step> steps=new List<Step>();
        public string Text {get;private set;}="";
        public int VisibleEnd {get;private set;}
        public bool Complete=>index>=steps.Count;
        public bool WaitingForInput=>!Complete && steps[index].input;
        int index;float clock;
        public void Start(string raw,float normal,ChillTypingPolicy policy)
        {
            steps.Clear();index=0;clock=0;VisibleEnd=0;policy=policy??new ChillTypingPolicy();
            var text=new StringBuilder();float before=0,speed=1;bool input=false,instant=false;
            for(int i=0;i<(raw??"").Length;)
            {
                if(raw[i]=='<' && raw.IndexOf('>',i+1) is int end && end>i)
                {
                    string tag=raw.Substring(i+1,end-i-1),key=tag.Split('=')[0].ToLowerInvariant();
                    string value=tag.Contains("=")?tag.Substring(tag.IndexOf('=')+1).Trim('"','\''):"";
                    float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out float number);
                    if(key=="waitfor")before+=Math.Max(0,number);
                    else if(key=="waitinput")input=true;
                    else if(key=="speed")speed=number>0?number:1;
                    else if(key=="/speed")speed=1;
                    else if(key=="notype")instant=true;
                    else if(key=="/notype")instant=false;
                    else if(key=="br"){text.Append('\n');steps.Add(new Step{end=text.Length,before=before,input=input,after=policy.waitForNewLines?normal/speed:0});before=0;input=false;}
                    else {text.Append(raw.Substring(i,end-i+1));}
                    i=end+1;continue;
                }
                string element=StringInfo.GetNextTextElement(raw,i);i+=element.Length;text.Append(element);
                char character=element[0];float wait=normal;
                if("!?.".IndexOf(character)>=0)wait=policy.waitLong;
                else if(",;):-".IndexOf(character)>=0)wait=policy.waitMiddle;
                if(policy.avoidMultiplePunctuactionWait && char.IsPunctuation(character) && i<raw.Length && char.IsPunctuation(raw[i]))wait=normal;
                if(character=='\n'&&!policy.waitForNewLines)wait=0;
                steps.Add(new Step{end=text.Length,before=before,input=input,normal=wait==normal&&!instant,after=instant?0:wait/speed});before=0;input=false;
            }
            Text=text.ToString();
            if(steps.Count>0&&!policy.waitForLastCharacter){var last=steps[steps.Count-1];last.after=0;steps[steps.Count-1]=last;}
            if(before>0 || input)steps.Add(new Step{end=Text.Length,before=before,input=input});
        }
        public int Advance(float seconds,bool input=false)
        {
            clock+=Math.Max(0,seconds);
            while(!Complete)
            {
                var step=steps[index];
                if(step.input){if(!input){clock=0;return VisibleEnd;}step.input=false;steps[index]=step;input=false;}
                if(clock<step.before)return VisibleEnd;
                VisibleEnd=step.end;
                if(clock<step.before+step.after)return VisibleEnd;
                clock-=step.before+step.after;index++;
            }
            VisibleEnd=Text.Length;return VisibleEnd;
        }
        public void Finish(){index=steps.Count;VisibleEnd=Text.Length;}
        public void Accelerate(float wait){for(int i=index;i<steps.Count;i++){var step=steps[i];if(step.normal)step.after=Math.Min(step.after,wait);steps[i]=step;}}
        public string VisibleText()
        {
            // Closing rich-text scopes prevents partially revealed tags leaking.
            string prefix=Text.Substring(0,Math.Min(VisibleEnd,Text.Length));var scopes=new Stack<string>();
            foreach(Match match in Regex.Matches(prefix,"<(/?)([a-zA-Z]+)(?:=[^>]*)?>"))
            {
                string key=match.Groups[2].Value.ToLowerInvariant();if(key=="br"||key=="sprite"||key=="space")continue;
                if(match.Groups[1].Value=="/"){if(scopes.Count>0&&scopes.Peek()==key)scopes.Pop();}else scopes.Push(key);
            }
            foreach(string key in scopes)prefix+="</"+key+">";
            return prefix;
        }
    }
}
