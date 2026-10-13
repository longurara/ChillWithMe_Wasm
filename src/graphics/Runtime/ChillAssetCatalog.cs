using System;

namespace Hlc.ChillPrototype
{
    [Serializable] public class ChillAssetCatalog
    {
        public int version;
        public ChillAssetEntry[] assets;
        public ChillBundleEntry[] bundles;
    }
    [Serializable] public class ChillAssetEntry
    {
        public string path,name,category,guid,bundle,assetName,type;
        public long sourceBytes;
        public float seconds;
        public bool humanMotion;
        public int channels,width,height,vertices;
    }
    [Serializable] public class ChillBundleEntry
    {
        public string name,hash;
        public string[] dependencies;
        public uint crc;
        public long bytes;
    }
}
