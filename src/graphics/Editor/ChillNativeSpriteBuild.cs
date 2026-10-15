using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Hlc.ChillPrototype;

// The exported sprites retain packed UVs, but their logical rect was replaced
// by the trimmed polygon bounds. Recover the centered UI rect and trim padding
// from the original atlas offset/UV transform; keep the original atlas pixels.
public static class ChillNativeSpriteBuild
{
    static readonly Dictionary<Sprite,Sprite> recovered=new Dictionary<Sprite,Sprite>();
    static float Number(string value)=>float.Parse(value,CultureInfo.InvariantCulture);
    static string Value(float value)=>value.ToString("R",CultureInfo.InvariantCulture);
    static Sprite Recover(Sprite source)
    {
        if(!source)return null;
        string path=AssetDatabase.GetAssetPath(source);
        if(path.StartsWith("Assets/Prototype/NativeSprites/"))
        {
            var orig=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Recovered/Sprite/"+source.name+".asset");
            if(orig)recovered[orig]=source;
            return source;
        }
        if(recovered.TryGetValue(source,out var cached))return cached;
        recovered[source]=source;
        if(!path.StartsWith("Assets/Recovered/Sprite/") || !source.name.StartsWith("t_ui_"))return source;
        string yaml=File.ReadAllText(path);
        var origin=Regex.Match(yaml,@"atlasRectOffset: \{x: ([^,]+), y: ([^}]+)\}");
        var uv=Regex.Match(yaml,@"uvTransform: \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}");
        var packed=Regex.Match(yaml,@"settingsRaw: (\d+)");
        if(!origin.Success || !uv.Success || !packed.Success)return source;
        int settings=int.Parse(packed.Groups[1].Value);
        if((settings&1)==0 || ((settings>>2)&15)!=0)return source;
        float x=Number(origin.Groups[1].Value),y=Number(origin.Groups[2].Value);
        float rawWidth=2*(Number(uv.Groups[2].Value)-x),rawHeight=2*(Number(uv.Groups[4].Value)-y);
        float width=Mathf.Round(rawWidth),height=Mathf.Round(rawHeight);
        // The level animation export retains the full 256 x 3840 strip
        // extent for a single square frame. Its UV center identifies the
        // packed frame, while the strip's Y origin lies outside the atlas.
        if(Regex.IsMatch(source.name,@"^t_ui_level_anim_\d+$") && width==256 && height==3840)
        {height=rawHeight=width;y=Number(uv.Groups[4].Value)-height*.5f;}
        if(source.name=="t_ui_todo_circle_anim_0" && width==52 && height==220)
        {height=rawHeight=44;y=Number(uv.Groups[4].Value)-height*.5f;}
        var textureRect=Regex.Match(yaml,@"    textureRect:\s*\n      serializedVersion: 2\s*\n      x: ([^\n]+)\n      y: ([^\n]+)\n      width: ([^\n]+)\n      height: ([^\n]+)");
        if(!textureRect.Success)return source;
        Rect trim=new Rect(Number(textureRect.Groups[1].Value),Number(textureRect.Groups[2].Value),Number(textureRect.Groups[3].Value),Number(textureRect.Groups[4].Value));
        if(width<=0 || height<=0 || Mathf.Abs(width-rawWidth)>.01f || Mathf.Abs(height-rawHeight)>.01f ||
           width>source.texture.width || height>source.texture.height ||
           trim.x<x-.1f || trim.y<y-.1f || trim.xMax>x+width+.1f || trim.yMax>y+height+.1f ||
           width<source.rect.width-.1f || height<source.rect.height-.1f)return source;
        string directory="Assets/Prototype/NativeSprites";Directory.CreateDirectory(directory);
        string output=directory+"/"+source.name+".asset";
        var result=AssetDatabase.LoadAssetAtPath<Sprite>(output);
        if(!result)
        {
            if(Mathf.Abs(width-source.rect.width)<.05f && Mathf.Abs(height-source.rect.height)<.05f && Mathf.Abs(trim.x-x)<.05f && Mathf.Abs(trim.y-y)<.05f)return source;
            float rectX=Mathf.Clamp(x,0,source.texture.width-width),rectY=Mathf.Clamp(y,0,source.texture.height-height);
            yaml=new Regex(@"  m_Rect:\s*\n    serializedVersion: 2\s*\n    x: [^\n]+\n    y: [^\n]+\n    width: [^\n]+\n    height: [^\n]+").Replace(yaml,
                "  m_Rect:\n    serializedVersion: 2\n    x: "+Value(rectX)+"\n    y: "+Value(rectY)+"\n    width: "+Value(width)+"\n    height: "+Value(height),1);
            yaml=new Regex(@"  m_Pivot: \{[^}]+\}").Replace(yaml,"  m_Pivot: {x: 0.5, y: 0.5}",1);
            yaml=new Regex(@"    textureRectOffset: \{[^}]+\}").Replace(yaml,
                "    textureRectOffset: {x: "+Value(trim.x-x)+", y: "+Value(trim.y-y)+"}",1);
            File.WriteAllText(output,yaml);AssetDatabase.ImportAsset(output,ImportAssetOptions.ForceSynchronousImport);
            result=AssetDatabase.LoadAssetAtPath<Sprite>(output);
        }
        if(!result)throw new InvalidOperationException("Cannot recover native UI sprite: "+source.name);
        recovered[source]=result;
        Debug.Log("HLC_NATIVE_SPRITE "+source.name+" trimmed="+source.rect.size+" logical="+result.rect.size+" padding="+UnityEngine.Sprites.DataUtility.GetPadding(result));
        return result;
    }
    public static void Configure(ChillNativeUI ui)
    {
        recovered.Clear();
        foreach(var image in ui.canvas.GetComponentsInChildren<Image>(true))
        {
            if(image.type!=Image.Type.Simple && image.type!=Image.Type.Filled)continue;
            image.sprite=Recover(image.sprite);
        }
        var prefabs=new[]{ui.musicCell,ui.storyCell,ui.todoCell,ui.todoListCell,ui.selectionCell,ui.storyCategoryTitlePrefab,ui.calendarHabitCell,ui.specialReadIconPrefab}
            .Concat(ui.specialCells??Array.Empty<GameObject>()).Where(p=>p).ToArray();
        foreach(var prefab in prefabs)
            foreach(var image in prefab.GetComponentsInChildren<Image>(true))
                if(image.type==Image.Type.Simple || image.type==Image.Type.Filled)Recover(image.sprite);
        foreach(var button in ui.canvas.GetComponentsInChildren<Button>(true))
        {
            var state=button.spriteState;
            state.highlightedSprite=Recover(state.highlightedSprite);
            state.pressedSprite=Recover(state.pressedSprite);
            state.selectedSprite=Recover(state.selectedSprite);
            state.disabledSprite=Recover(state.disabledSprite);
            button.spriteState=state;
        }
        foreach(var prefab in prefabs)
            foreach(var button in prefab.GetComponentsInChildren<Button>(true))
            {var state=button.spriteState;Recover(state.highlightedSprite);Recover(state.pressedSprite);Recover(state.selectedSprite);Recover(state.disabledSprite);}
        ui.musicPlay=Recover(ui.musicPlay);ui.musicPause=Recover(ui.musicPause);ui.timerPlay=Recover(ui.timerPlay);ui.timerPause=Recover(ui.timerPause);
        ui.countupPlay=Recover(ui.countupPlay);ui.countupPause=Recover(ui.countupPause);ui.wantTalkNormal=Recover(ui.wantTalkNormal);
        if(ui.skinThumbnails!=null)foreach(var st in ui.skinThumbnails)if(st!=null)st.sprite=Recover(st.sprite);
        if(ui.environmentChoices!=null)foreach(var ec in ui.environmentChoices)if(ec!=null)ec.shopThumbnail=Recover(ec.shopThumbnail);
        var data=JsonUtility.FromJson<ChillNativeUI.Configuration>(ui.configuration.text);
        Sprite VolumeSprite(string name)
        {
            var guid=data.bindings.First(b=>b.script=="MusicVolumeUI").refs.First(r=>r.name==name).asset;
            return Recover(AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid)));
        }
        ui.musicAudible=VolumeSprite("nonMuteIconSprite");ui.musicMuted=VolumeSprite("muteIconSprite");
        ui.spriteReplacements=recovered.Where(p=>p.Key!=p.Value).Select(p=>new ChillNativeUI.SpriteReplacement{original=p.Key,recovered=p.Value}).ToArray();
        Debug.Log("HLC_NATIVE_SPRITE_RECOVERY count="+ui.spriteReplacements.Length+" originalPixels=true");
    }
    static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
}
