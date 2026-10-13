using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public sealed partial class ChillNativeUI
    {
        // Recovered PC atlases remain read-only. New glyphs need an atlas created
        // by this Unity runtime, rather than mutating the exported native texture.
        void PrepareRuntimeFonts()
        {
            if(!Application.isPlaying)return;
            var fonts=new HashSet<TMP_FontAsset>();
            void Visit(TMP_FontAsset font)
            {
                if(!font || !fonts.Add(font))return;
                foreach(var child in font.fallbackFontAssetTable)Visit(child);
            }
            foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true))Visit(text.font);
            Visit(fallbackFont);
            foreach(var font in fonts)
            {
                var wasDynamic=font.atlasPopulationMode!=AtlasPopulationMode.Static;
                var sourceFont=font.sourceFontFile;
                font.atlasPopulationMode=AtlasPopulationMode.Static;
                font.fallbackFontAssetTable.RemoveAll(child=>!child || child==font);
                if(wasDynamic && sourceFont)
                {
                    var dynamicFont=TMP_FontAsset.CreateFontAsset(sourceFont,
                        Mathf.Max(20,Mathf.RoundToInt(font.faceInfo.pointSize)),6,
                        UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,
                        AtlasPopulationMode.Dynamic,true);
                    dynamicFont.name=font.name+" runtime glyphs";
                    font.fallbackFontAssetTable.Insert(0,dynamicFont);
                }
                if(fallbackFont && font!=fallbackFont && !font.fallbackFontAssetTable.Contains(fallbackFont))
                    font.fallbackFontAssetTable.Add(fallbackFont);
            }
        }
    }
}
