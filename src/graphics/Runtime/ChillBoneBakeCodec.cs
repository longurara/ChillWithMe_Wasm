using System;
using System.IO;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    // Float32 samples retain the reference rotations without shipping a large JSON string.
    public static class ChillBoneBakeCodec
    {
        const uint Magic=0x32424348;
        public static ChillBoneBake Read(TextAsset asset)
        {
            if(!asset)return null;
            var bytes=asset.bytes;
            if(bytes.Length<4||BitConverter.ToUInt32(bytes,0)!=Magic)return JsonUtility.FromJson<ChillBoneBake>(asset.text);
            using(var stream=new MemoryStream(bytes,false))
            using(var reader=new BinaryReader(stream))
            {
                reader.ReadUInt32();
                var bake=new ChillBoneBake{producer=ReadText(reader),profileHash=ReadText(reader)};
                int bones=Count(reader,1024);bake.paths=new string[bones];
                for(int i=0;i<bones;i++)bake.paths[i]=ReadText(reader);
                int clips=Count(reader,512);bake.clips=new ChillBoneBake.Clip[clips];long offsets=0;
                for(int i=0;i<clips;i++)
                {
                    var clip=new ChillBoneBake.Clip{state=ReadText(reader),motion=reader.ReadInt32(),seconds=reader.ReadSingle(),loop=reader.ReadBoolean()};
                    if(float.IsNaN(clip.seconds)||float.IsInfinity(clip.seconds)||clip.seconds<=0||clip.seconds>3600)throw new InvalidDataException("Invalid bake duration");
                    int frames=Count(reader,65536);offsets+=(long)frames*bones;
                    if(frames<2||offsets>8000000||stream.Length-stream.Position<(long)frames*bones*16)throw new InvalidDataException("Invalid bake frame count");
                    clip.frames=new ChillBoneBake.Frame[frames];
                    for(int frame=0;frame<frames;frame++)
                    {
                        var values=new Quaternion[bones];
                        for(int bone=0;bone<bones;bone++)
                        {
                            var q=new Quaternion(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                            float norm=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;
                            if(float.IsNaN(norm)||float.IsInfinity(norm)||Mathf.Abs(norm-1)>.01f)throw new InvalidDataException("Invalid bake rotation");
                            values[bone]=q;
                        }
                        clip.frames[frame]=new ChillBoneBake.Frame{offsets=values};
                    }
                    bake.clips[i]=clip;
                }
                if(stream.Position!=stream.Length)throw new InvalidDataException("Trailing bake data");
                return bake;
            }
        }
        static int Count(BinaryReader reader,int maximum)
        {int value=reader.ReadInt32();if(value<=0||value>maximum)throw new InvalidDataException("Invalid bake count");return value;}
        static string ReadText(BinaryReader reader)
        {var text=reader.ReadString();if(text.Length>4096)throw new InvalidDataException("Invalid bake text");return text;}
        public static byte[] Write(ChillBoneBake bake)
        {
            using(var stream=new MemoryStream())
            {
                using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
                {
                    writer.Write(Magic);writer.Write(bake.producer);writer.Write(bake.profileHash);writer.Write(bake.paths.Length);
                    foreach(var path in bake.paths)writer.Write(path);
                    writer.Write(bake.clips.Length);
                    foreach(var clip in bake.clips)
                    {
                        writer.Write(clip.state);writer.Write(clip.motion);writer.Write(clip.seconds);writer.Write(clip.loop);writer.Write(clip.frames.Length);
                        foreach(var frame in clip.frames)foreach(var q in frame.offsets){writer.Write(q.x);writer.Write(q.y);writer.Write(q.z);writer.Write(q.w);}
                    }
                }
                return stream.ToArray();
            }
        }
    }
}
