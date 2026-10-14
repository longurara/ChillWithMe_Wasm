using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public sealed partial class ChillNativeUI
    {
        readonly Dictionary<TMP_Text,string> portLabels=new Dictionary<TMP_Text,string>();
        string portLanguage;
        float nextPortSweep;
        void PortCaption(TMP_Text label,string text)
        {
            if(!label)return;
            portLabels[label]=text;label.text=ChillPortI18n.Translate(text,player.story.Language);
        }
        void RefreshPortLanguage()
        {
            if(Time.unscaledTime>=nextPortSweep){nextPortSweep=Time.unscaledTime+3;foreach(var label in portLabels.Keys.Where(label=>!label).ToArray())portLabels.Remove(label);}
            string language=player.story.Language;if(language==portLanguage)return;portLanguage=language;
            foreach(var entry in portLabels.ToArray())if(entry.Key)entry.Key.text=ChillPortI18n.Translate(entry.Value,language);else portLabels.Remove(entry.Key);
        }
    }
}
