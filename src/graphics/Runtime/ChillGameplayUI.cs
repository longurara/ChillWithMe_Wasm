using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Hlc.ChillPrototype
{
    [Serializable] public class ChillUiCommand { public string action,text,id; public float value; }
    [Serializable] public class ChillUiItem { public string id,title,detail; public bool enabled,done; }
    [Serializable] public class ChillStoryUiState
    {
        public bool ready,playing,typing,loadingAudio,autoRead,unlock,timerRunning,timerActive,breakTime,saveAllowed,countupRunning,countupActive;
        public bool mainStoryText,automaticDialogue;
        public string status,language,chapter,title,speaker,text,note,timerText,workedText,countupText;
        public int level,completed,workMinutes,breakMinutes,rounds,page,chapterCount,kind,loopCount,timerMode;
        public ChillTimerPreset[] timerPresets;
        public float exp,requiredExp,timerMeter;
        public ChillTodo[] todos;
        public ChillUiItem[] chapters,choices;
        public string[] history;
        public string noticeMessage;
        public bool noticeError;
        public int noticeRevision;
    }
    [Serializable] public class ChillUiState
    {
        public string panel,musicTitle,musicStatus;
        public bool musicPlaying,musicBusy,lowQuality;
        public float volume,musicTime,musicDuration;
        public int mode,timeMode,windowMode;
        public ChillUiItem[] tracks,environments;
        public ChillStoryUiState story;
    }
    public sealed class ChillGameplayUI : MonoBehaviour
    {
        public ChillGraphicsPlayer player;
        public static bool Active { get; private set; }
        public static void UseNativeUI(bool value) { Active=value; }
        bool connected;
        float publishAt;
        string panel="none",previous;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void ChillBindUI(string receiver);
        [DllImport("__Internal")] static extern void ChillPublishUI(string state);
#endif
        void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillBindUI(gameObject.name);
#endif
        }
        public void ConnectUI(string unused)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput=false;
#endif
            connected=Active=true; previous=null; Publish();
        }
        void Update() { if (connected && Time.unscaledTime >= publishAt) { publishAt=Time.unscaledTime+.25f; Publish(); } }
        void OnDestroy() { if (connected) Active=false; }
        public ChillUiState Snapshot()
        {
            var story=player.story;
            var music=player.music;
            return new ChillUiState {
                panel=panel,mode=player.SelectedState,volume=player.Volume,lowQuality=player.LowQuality,
                timeMode=player.environment.selectedTime,windowMode=player.environment.selectedEnvironment,
                musicPlaying=music && music.isPlaying,musicBusy=player.library.Busy,musicStatus=player.library.Busy ? player.library.Status : "",
                musicTitle=music && music.clip ? music.clip.name : "Chọn một bản nhạc",
                musicTime=music && music.clip ? music.time : 0,musicDuration=music && music.clip ? music.clip.length : 0,
                tracks=panel == "music" ? player.library.MusicEntries.Select(a => new ChillUiItem { id=a.guid,title=a.name,detail=TimeSpan.FromSeconds(a.seconds).ToString(@"mm\:ss"),enabled=!story.Playing }).ToArray() : Array.Empty<ChillUiItem>(),
                environments=panel == "settings" ? new[] { new ChillUiItem { id="0",title="Bầu trời",enabled=true } }.Concat(player.environment.environments.Select((e,i) => new ChillUiItem { id=(i+1).ToString(),title=e.name.Replace("Env_",""),enabled=true })).ToArray() : Array.Empty<ChillUiItem>(),
                story=story.UISnapshot(panel)
            };
        }
        void Publish()
        {
            if (!connected) return;
            string json=JsonUtility.ToJson(Snapshot());
            if (json == previous) return; previous=json;
#if UNITY_WEBGL && !UNITY_EDITOR
            ChillPublishUI(json);
#endif
        }
        public void UICommand(string json)
        {
            try
            {
                var command=JsonUtility.FromJson<ChillUiCommand>(json);
                if (command == null || string.IsNullOrEmpty(command.action)) return;
                switch (command.action)
                {
                    case "panel": panel=new[] { "none","music","timer","notes","story","settings","save" }.Contains(command.text) ? command.text : "none"; player.library.visible=false; break;
                    case "mode": if (!player.story.Playing) player.PlayState(Mathf.Clamp((int)command.value,0,2)); break;
                    case "music-toggle": if (player.music && player.music.clip) { if (player.music.isPlaying) player.music.Pause(); else player.music.Play(); } break;
                    case "music-select": if (!player.story.Playing) player.library.SelectMusic(command.id); break;
                    case "volume": player.SetVolume(command.value); break;
                    case "quality": player.SetLowQuality(command.value != 0); break;
                    case "time": if (!player.story.Playing) player.environment.SelectTime(Mathf.Clamp((int)command.value,0,3)); break;
                    case "environment": if (!player.story.Playing) player.environment.SelectEnvironment(Mathf.Clamp((int)command.value,0,player.environment.environments.Length)); break;
                    default: player.story.UIAction(command); break;
                }
                previous=null; Publish();
            }
            catch (Exception error) { Debug.LogWarning("Gameplay UI command failed: "+error.Message); }
        }
    }
}
