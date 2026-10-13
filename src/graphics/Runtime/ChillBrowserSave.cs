using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    public static class ChillBrowserSave
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern string ChillReadSave();
        [DllImport("__Internal")] static extern string ChillReadBackup();
        [DllImport("__Internal")] static extern int ChillWriteSave(string json);
        [DllImport("__Internal")] static extern int ChillCanWriteSave();
        [DllImport("__Internal")] static extern void ChillExportSave(string json);
        [DllImport("__Internal")] static extern void ChillImportSave(string receiver);
        [DllImport("__Internal")] static extern void ChillImportPCSave(string receiver);
#endif
        public static bool CanWrite
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return ChillCanWriteSave()==1;
#else
                return true;
#endif
            }
        }
        public static string Load()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return ChillReadSave();
#else
            return PlayerPrefs.GetString("chill-standalone-save", "");
#endif
        }
        public static bool Write(ChillSave save)
        {
            string json = JsonUtility.ToJson(save);
#if UNITY_WEBGL && !UNITY_EDITOR
            return ChillWriteSave(json) == 1;
#else
            PlayerPrefs.SetString("chill-standalone-save", json); PlayerPrefs.Save(); return true;
#endif
        }
        public static string Backup()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return ChillReadBackup();
#else
            return PlayerPrefs.GetString("chill-standalone-save.backup", "");
#endif
        }
        public static void Export(ChillSave save)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillExportSave(JsonUtility.ToJson(save, true));
#else
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath,"chill-save.json"), JsonUtility.ToJson(save,true));
#endif
        }
        public static void Import(string receiver)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillImportSave(receiver);
#endif
        }
        public static void ImportPC(string receiver)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillImportPCSave(receiver);
#endif
        }
    }
}
