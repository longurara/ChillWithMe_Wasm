using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public static class ChillPCSaveMetadata
{
    public static void Build()
    {
        // Both the source PC game and this Editor use Unity 2022.3 Mono.
        // Verify its actual hash before creating names for untyped JSON records.
        if("PlayerDataV3".GetHashCode()!=-1171499732||"ProfileData".GetHashCode()!=-130152905)
            throw new InvalidOperationException("PC save hash differs from the source Mono runtime.");
        var names=Directory.GetFiles("../Recovered/ExportedProject/Assets/Scripts/Assembly-CSharp","*.cs",SearchOption.AllDirectories)
            .SelectMany(p=>Regex.Matches(File.ReadAllText(p),@"\bclass\s+(\w+)").Cast<Match>().Select(m=>m.Groups[1].Value)).Distinct().ToArray();
        var groups=names.GroupBy(n=>n.GetHashCode());
        string path="Assets/Prototype/pc-save-metadata.json";
        var source=File.ReadAllText(path).Trim();
        int previous=source.LastIndexOf(",\"types\"",StringComparison.Ordinal);if(previous>=0)source=source.Substring(0,previous)+"}";
        string types="\"types\":{"+string.Join(",",groups.Where(g=>g.Count()==1).Select(g=>"\""+g.Key+"\":\""+g.First()+"\""))+"}";
        File.WriteAllText(path,source.Substring(0,source.Length-1)+","+types+"}");
        Debug.Log("HLC_PC_SAVE_METADATA sourceMonoHashVerified=true types="+names.Length);
    }
}
