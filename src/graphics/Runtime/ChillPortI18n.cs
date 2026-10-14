using System;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public static class ChillPortI18n
    {
        [Serializable] public class Entry
        {
            public string En,Ja,ZhHans,ZhHant,Pt,Ko,Ru;
            public string Get(string language)=>language=="Ja"?Ja:language=="ZhHans"?ZhHans:language=="ZhHant"?ZhHant:language=="Pt"?Pt:language=="Ko"?Ko:language=="Ru"?Ru:En;
        }
        [Serializable] public class Catalog {public Entry[] entries;}
        static Catalog catalog;
        public static string Translate(string text,string language)
        {
            if(string.IsNullOrEmpty(text)||language=="En")return text;
            if(catalog==null){var asset=Resources.Load<TextAsset>("chill-port-i18n");catalog=asset?JsonUtility.FromJson<Catalog>(asset.text):new Catalog{entries=Array.Empty<Entry>()};}
            foreach(var row in catalog.entries??Array.Empty<Entry>())if(row.En==text)return string.IsNullOrEmpty(row.Get(language))?text:row.Get(language);
            int separator=text.IndexOf(": ",StringComparison.Ordinal);
            if(separator>0)return Translate(text.Substring(0,separator),language)+": "+Translate(text.Substring(separator+2),language);
            return text;
        }
    }
}
