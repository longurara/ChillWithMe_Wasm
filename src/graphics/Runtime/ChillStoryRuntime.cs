using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime : MonoBehaviour
    {
        public ChillGraphicsPlayer player;
        public RuntimeAnimatorController storyController;
        public Font japaneseFont;
        public ChillStoryEffects effects;
        public ChillDecorations decorations;
        public ChillStoryPresentation presentation;
        public bool Replaying { get; private set; }
        public bool visible;
        [NonSerialized] public bool VerificationOnly;
        public bool Playing => chapter != null;
        public bool HistoryVisible {get;set;}
        ChillStoryData data;
        ChillSave save = new ChillSave();
        Dictionary<string, ChillText> texts;
        Dictionary<int, string> motions;
        Dictionary<string, ChillSentence[]> chapters;
        ChillChapter chapter;
        ChillSentence[] sentences;
        ChillSentence current;
        RuntimeAnimatorController deskController;
        AudioSource voice, ambient, sound, keyboard, writing;
        AudioSource storyMusic;
        AudioSource StoryMusic {get{if(!storyMusic){storyMusic=gameObject.AddComponent<AudioSource>();storyMusic.playOnAwake=false;storyMusic.spatialBlend=0;}return storyMusic;}}
        readonly HashSet<AudioSource> pausedStoryAmbience=new HashSet<AudioSource>();
        public bool IsTutorial=>chapter!=null&&chapter.Scenario==18;
        int keyboardVersion,writingVersion;
        Coroutine reader;
        List<ChillSentence> choices = new List<ChillSentence>(), loops = new List<ChillSentence>();
        List<string> history = new List<string>();
        string message = "", status = "Đang tải dữ liệu cốt truyện…", search = "", newTodo = "";
        int cursor, generation, shownChars, selectedTab, listKind, page, chosen = -1;
        bool next, typing, autoRead, loadingAudio, saveAllowed = true;
        double lastSave;
        float voiceDefect;
        AudioClip previousMusic;
        bool previousMusicPlaying,previousMusicLoop;
        float previousMusicTime;
        bool moveMouth;
        readonly Dictionary<string, AudioClip> motionSounds = new Dictionary<string, AudioClip>();
        readonly HashSet<string> pendingSounds = new HashSet<string>();
        float textClock;
        readonly ChillTypewriter typewriter=new ChillTypewriter();
        string noticeMessage="";
        bool noticeError;
        int noticeRevision;
        public void Notify(string text,bool error=false)
        {
            if (error && noticeError && noticeMessage==text) return;
            noticeMessage=text;noticeError=error;noticeRevision++;
        }
        Vector2 listScroll, dialogueScroll, noteScroll;
        GUIStyle storyLabel, storyButton;
        static double Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        static float Number(string value) => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0;

        IEnumerator Start()
        {
            voice = gameObject.AddComponent<AudioSource>(); ambient = gameObject.AddComponent<AudioSource>(); sound = gameObject.AddComponent<AudioSource>();
            keyboard=gameObject.AddComponent<AudioSource>(); writing=gameObject.AddComponent<AudioSource>(); keyboard.loop=writing.loop=true;
            foreach (var source in new[] { voice, ambient, sound,keyboard,writing }) { source.playOnAwake = false; source.spatialBlend = 0; }
            ambient.loop = true; deskController = player.character.runtimeAnimatorController;
            using (var request = UnityWebRequest.Get(Application.streamingAssetsPath + "/chill-story.json"))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) { status = "Không tải được cốt truyện: " + request.error; GetComponent<ChillStartupGate>()?.Fail(status); Notify(status,true); yield break; }
                string parseError=null;
                try{data = JsonUtility.FromJson<ChillStoryData>(request.downloadHandler.text);if(data.groups==null||data.localizations==null||data.novels==null||data.motions==null)throw new ArgumentException("Missing story data");}
                catch(Exception error){parseError=error.Message;}
                if(parseError!=null){status="Không đọc được cốt truyện: "+parseError;GetComponent<ChillStartupGate>()?.Fail(status);Notify(status,true);yield break;}
            }
            texts = data.localizations.GroupBy(t => t.ID).ToDictionary(g => g.Key, g => g.First());
            motions = data.motions.GroupBy(m => m.value).ToDictionary(g => g.Key, g => g.First().name);
            chapters = data.novels.GroupBy(n => n.ScenarioGroupID).ToDictionary(g => g.Key, g => g.ToArray());
            string json = ChillBrowserSave.Load();
            if (!string.IsNullOrEmpty(json))
            {
                try { var loaded = JsonUtility.FromJson<ChillSave>(json); Validate(loaded); save = loaded; }
                catch (Exception error) { saveAllowed = false; status = "Save chưa đọc được; không ghi đè: " + error.Message; Notify(status,true); }
            }
            autoRead = save.autoRead; ApplyPreferences();
            if (saveAllowed) status = data.groups.Length + " đoạn truyện/hội thoại đã sẵn sàng";
            lastSave = Now;
            yield return InitializeFeatures();
        }

        string Text(string id)
        {
            if (string.IsNullOrEmpty(id) || id == "0") return "";
            if (!texts.TryGetValue(id, out var text)) return "[Thiếu lời thoại trong dữ liệu nguồn: " + id + "]";
            string value=save.language=="Ja"?text.Ja:save.language=="ZhHans"?text.ZhHans:save.language=="ZhHant"?text.ZhHant:save.language=="Pt"?text.Pt:save.language=="Ko"?text.Ko:save.language=="Ru"?text.Ru:text.En;
            return string.IsNullOrEmpty(value) ? text.En ?? text.Ja ?? "" : value;
        }

        void ApplyPreferences(bool applyMotion=true)
        {
            if(applyMotion)player.PlayState(save.deskMode);
            player.environment.SelectTime(save.timeMode);
            player.environment.SelectEnvironment(save.windowMode);
            if(presentation&&presentation.cameras)presentation.cameras.SelectRoomView(Settings.cameraView);
        }

        void Validate(ChillSave candidate)
        {
            // Older web saves used four fixed sessions and have no loopCount.
            if(candidate!=null && candidate.loopCount==0)candidate.loopCount=4;
            if (candidate != null && candidate.sceneHistory == null) candidate.sceneHistory = new List<int>();
            if (candidate != null && candidate.sceneHistory.Count > 8192) throw new ArgumentException("Lịch sử cảnh quá dài.");
            if (candidate == null || candidate.format != "chill-with-you-web" || candidate.version != 1) throw new ArgumentException("Sai định dạng/phiên bản.");
            if (candidate.completed == null || candidate.usedChoices == null || candidate.todos == null) throw new ArgumentException("Thiếu dữ liệu tiến trình.");
            if (candidate.level < 1 || candidate.level > 10000 || candidate.exp < 0 || double.IsNaN(candidate.exp) || double.IsInfinity(candidate.exp)) throw new ArgumentException("Cấp độ không hợp lệ.");
            if (candidate.deskMode < 0 || candidate.deskMode > 2 || candidate.timeMode < 0 || candidate.timeMode > 3 || candidate.windowMode < 0 || candidate.windowMode > player.environment.environments.Length) throw new ArgumentException("Thiết lập phòng không hợp lệ.");
            ValidateFeatures(candidate);
            if (!new[]{"En","Ja","ZhHans","ZhHant","Pt","Ko","Ru"}.Contains(candidate.language)) throw new ArgumentException("Ngôn ngữ không hợp lệ.");
            if (candidate.completed.Count > 1000 || candidate.completed.Any(id => !data.groups.Any(g => g.ID == id)) || candidate.todos.Any(t => t == null || (t.title ?? "").Length > 200)) throw new ArgumentException("Danh sách tiến trình không hợp lệ.");
            if (candidate.workedSeconds < 0 || double.IsNaN(candidate.workedSeconds) || double.IsInfinity(candidate.workedSeconds) || candidate.mainEpisode < 0 || candidate.mainEpisode > 1000 || double.IsNaN(candidate.mainEpisode)) throw new ArgumentException("Thời gian/tiến trình không hợp lệ.");
            if (candidate.workMinutes < 1 || candidate.workMinutes > 999 || candidate.breakMinutes < 1 || candidate.breakMinutes > 999 || candidate.loopCount<1 || candidate.loopCount>99 || candidate.timerMode<0 || candidate.timerMode>1 || candidate.todos.Count > 100 || (candidate.note ?? "").Length > 64000) throw new ArgumentException("Dữ liệu công việc quá lớn.");
            if(candidate.countupSeconds<0 || double.IsNaN(candidate.countupSeconds) || double.IsInfinity(candidate.countupSeconds) || candidate.countupStarted<0 || double.IsNaN(candidate.countupStarted) || double.IsInfinity(candidate.countupStarted) || double.IsNaN(candidate.lastCountupTick) || double.IsInfinity(candidate.lastCountupTick))throw new ArgumentException("Đồng hồ đếm lên không hợp lệ.");
            if(candidate.timerPresets!=null && (candidate.timerPresets.Count>5 || candidate.timerPresets.Any(p=>p==null || (p.name ?? "").Length>80 || p.loopCount<1 || p.loopCount>99 || p.workMinutes<1 || p.workMinutes>999 || p.breakMinutes<1 || p.breakMinutes>999)))throw new ArgumentException("Preset không hợp lệ.");
            if (!string.IsNullOrEmpty(candidate.chapter) && (!chapters.TryGetValue(candidate.chapter, out var rows) || candidate.cursor < 0 || candidate.cursor >= rows.Length)) throw new ArgumentException("Vị trí cốt truyện không hợp lệ.");
            if (!string.IsNullOrEmpty(candidate.chapter) && candidate.sceneHistory.Any(i => i < 0 || i >= chapters[candidate.chapter].Length)) throw new ArgumentException("Lịch sử cảnh không hợp lệ.");
            if (double.IsNaN(candidate.timerEnd) || double.IsInfinity(candidate.timerEnd) || candidate.timerEnd < 0) throw new ArgumentException("Đồng hồ không hợp lệ.");
        }

        public void ImportSave(string json)
        {
            try
            {
                var imported = JsonUtility.FromJson<ChillSave>(json); Validate(imported);
                StopStory(); save = imported; autoRead = save.autoRead; saveAllowed = true; ApplyPreferences();ApplyFeatureSettings();ResetAutonomy();history=new List<string>(save.conversationHistory);foreach(var track in Features.ambient)SetAmbience(track.id,AmbientLevel(track.id),false);if(!string.IsNullOrEmpty(MusicState.selected))SelectTrack(MusicState.selected,false,MusicState.position);
                if (SaveNow()) {status = "Đã nhập save. Bấm Tiếp tục truyện để đọc từ vị trí đã lưu.";Notify(status);}
            }
            catch (Exception error) { status = "Không nhập save: " + error.Message; Notify(status,true); }
        }
        public void ImportSaveError(string error) { status = error; Notify(status,true); }

        bool SaveNow()
        {
            if (!saveAllowed) {Notify("Save hiện tại chưa đọc được. Hãy xuất dữ liệu hoặc nhập save hợp lệ trước khi lưu.",true);return false;}
            if(data==null)return false;
            save.autoRead = autoRead;
            if (!Playing) { save.deskMode = player.SelectedState; save.timeMode = player.environment.selectedTime; save.windowMode = player.environment.selectedEnvironment; }
            CaptureFeatures();
            var persisted=save;
            if(automaticDialogue){persisted=JsonUtility.FromJson<ChillSave>(JsonUtility.ToJson(save));persisted.chapter=suspendedChapter;persisted.cursor=suspendedCursor;persisted.sceneHistory=suspendedScene ?? new List<int>();persisted.storyLive=suspendedStoryLive;}
            bool written = VerificationOnly || ChillBrowserSave.Write(persisted);
            if (!written) {status = "Trình duyệt không lưu được. Hãy xuất file save để giữ tiến trình.";Notify(status,true);}
            lastSave = Now;
            return written;
        }

        public void StopStory()
        {
            bool finishedFarewell=farewellPending&&Playing;farewellPending=false;
            bool wasPlaying = Playing;
            bool finishSnack=automaticDialogue && chapter!=null && (chapter.Scenario==2600 || chapter.Scenario==2601);
            if(wasPlaying && previousMusic && player.music.clip==previousMusic)previousMusicTime=player.music.time;
            bool resumeActivity=automaticDialogue&&autonomyInstalled;int activityBreak=breakMode,activityTalks=selfTalkCount;
            // Timer flags can change before their voice line begins. Preserve
            // only an activity that was installed for the current phase.
            double activityEnd=nextAction,variantEnd=nextVariant;bool activityRest=previousRest;
            string activityBase=CurrentBaseMotion;float activitySleep=sleepiness;
            ResetAutonomy();ResetCommandWaits();tutorialCommand=0;RestoreAutomaticResume();GetComponent<ChillNativeUI>()?.StopNativeCredits();
            generation++; if (reader != null) StopCoroutine(reader); reader = null;
            if(HistoryVisible)GetComponent<ChillNativeUI>()?.Close();
            chosen=-1;next=false;HistoryVisible=false;
            StopMotionSounds();
            choices.Clear(); loops.Clear(); voice.Stop(); voice.clip = null; ambient.Stop(); ambient.clip = null; sound.Stop(); sound.clip = null;
            if(storyMusic){storyMusic.Stop();storyMusic.clip=null;}
            foreach(var source in pausedStoryAmbience)if(source&&source.clip&&AmbientLevel(ambienceSources.FirstOrDefault(p=>p.Value==source).Key)>0)source.UnPause();pausedStoryAmbience.Clear();
            voice.pitch = 1; voiceDefect = 0;cachedStorySound=1;
            if (effects) effects.ResetEffects();
            if (decorations) decorations.Restore();
            if (presentation && wasPlaying) presentation.ResetPresentation(true);
            Replaying=false;
            player.deskObjects.restoring=false;
            player.library.ReleaseExternal("story-voice"); player.library.ReleaseExternal("story-ambient"); player.library.ReleaseExternal("story-sound");
            chapter = null; current = null; loadingAudio = false; typing = false;
            GetComponent<ChillNativeUI>()?.EndTutorialPresentation();
            ApplyPreferences(false);
            RestoreRoom();
            if (wasPlaying)
            {
                player.music.Stop(); player.music.clip = previousMusic; player.music.loop = previousMusicLoop;if(previousMusic)player.music.time=Mathf.Clamp(previousMusicTime,0,Mathf.Max(0,previousMusic.length-.05f));
                player.SetVolume(Settings.music);
                if (previousMusicPlaying && previousMusic) player.music.Play();
                player.library.ReleaseExternal("story-music");
            }
            if(resumeActivity&&Settings.autonomy&&FeatureReady&&activityRest==Resting)
            {
                // A short reaction must not start a new break or reset its self-talk limit.
                breakMode=activityBreak;selfTalkCount=activityTalks;previousMode=player.SelectedState;previousRest=Resting;
                autonomyInstalled=true;nextAction=activityEnd;nextVariant=variantEnd;
                sleepiness=activitySleep;
                if(finishSnack && Application.isPlaying && !VerificationOnly)autonomousSequence=StartCoroutine(FinishSnackActivity());
                else SetNativeMotion(activityBase);
            }
            else if(wasPlaying)
            {
                if(Settings.autonomy&&FeatureReady)InstallAutonomy();
                else player.PlayState(save.deskMode);
            }
            // HeroineService has an explicit Play path for story boundaries.
            // Blend into that same destination without resetting the Animator,
            // including script-only poses with no ordinary exit to work.
            if(presentation&&wasPlaying&&!finishSnack)presentation.BlendToRequested(.25f);
            if(finishedFarewell&&SaveNow())Notify("Saved. You can close this tab.");
        }

        void Begin(ChillChapter selected, bool resume,bool ambientDialogue=false,bool fromTalkLog=true)
        {
            if(MusicLoading){musicToken++;MusicLoading=false;wantsMusic=false;}
            int start = resume && save.chapter == selected.ID ? save.cursor : 0;
            if (!Playing) SaveNow();
            if(!ambientDialogue)StopStory();else {generation++;StopMotionSounds();choices.Clear();loops.Clear();}
            if(selected.Scenario>=50&&selected.Scenario<=56)Workspace.collaboration=selected.Scenario-49;
            CancelRoom();automaticDialogue=ambientDialogue;invitedChapter=null;player.library.visible = false; player.library.RestoreCharacter();
            previousMusic = player.music.clip; previousMusicPlaying = player.music.isPlaying;previousMusicLoop=player.music.loop;previousMusicTime=previousMusic?player.music.time:0;talkLog=resume?!save.storyLive:fromTalkLog&&!ambientDialogue;save.storyLive=!talkLog;
            if(!ambientDialogue){player.music.Pause();foreach(var source in ambienceSources.Values)if(source&&source.isPlaying){pausedStoryAmbience.Add(source);source.Pause();}}
            if (!chapters.TryGetValue(selected.ID, out sentences)) { status = "Đoạn này không có kịch bản."; return; }
            chapter = selected; cursor = start; visible = true; selectedTab = 0;
            chosen=-1;next=false;message="";shownChars=0;typing=false;loadingAudio=false;
            save.chapter = selected.ID; save.cursor = cursor;
            if (!resume) save.usedChoices.Clear();
            if (!resume) save.sceneHistory.Clear();
            presentation.cameras.Capture();
            if(!ambientDialogue)presentation.cameras.SelectRoomView(0);
            string scenario=data.scenarios.FirstOrDefault(s => s.value == selected.Scenario)?.name ?? "";
            presentation.manualLook=scenario.StartsWith("HeroineClick") || scenario.StartsWith("HeroineSelfShortTalk") || scenario.StartsWith("SpeakWord_Player_");
            if (storyController && player.character.runtimeAnimatorController!=storyController) { player.character.runtimeAnimatorController = storyController; player.character.Rebind(); }
            if(!resume || start==0)PrepareStoryEntry();
            status = "Đang đọc: " + selected.ID; SaveNow(); reader = StartCoroutine(ReadFromSavedScene(resume));
        }

        void Motion(ChillSentence row)
        {
            motions.TryGetValue(row.BodyMotion,out string name);
            presentation.Motion(row,name,Replaying);
        }
        IEnumerator ReadFromSavedScene(bool resume)
        {
            Replaying=resume && cursor > 0;
            player.deskObjects.restoring=Replaying;
            if (Replaying)
            {
                var indices=save.sceneHistory.Where(i => i >= 0 && i < sentences.Length).ToList();
                if (indices.Count > 0 && indices[indices.Count-1] == cursor) indices.RemoveAt(indices.Count-1);
                if (indices.Count == 0)
                {
                    int previous=Array.FindLastIndex(sentences,Mathf.Min(cursor-1,sentences.Length-1),r => r.BodyMotion != -1);
                    if (previous >= 0) indices.Add(previous);
                }
                ChillSentence lastMusic=null,lastAmbient=null,lastPlaylist=null,lastMusicVolume=null;
                int restored=0;
                foreach (int index in indices)
                {
                    var row=sentences[index];
                    if (row.Command == 0 || row.Command == 21 || row.Command == 22)
                    {
                        Motion(row); player.character.Update(0);
                        if (row.BodyMotion != -1)
                        {
                            var clips=player.character.GetCurrentAnimatorClipInfo(0);
                            if (clips.Length > 0 && !clips[0].clip.isLooping)
                            {
                                float duration=Mathf.Min(60,clips[0].clip.length);
                                // Small steps preserve ordered prop events and transitions.
                                for (float elapsed=0;elapsed<duration;)
                                {
                                    float step=Mathf.Min(.1f,duration-elapsed); player.character.Update(step); player.deskObjects.AdvanceProps(step); elapsed+=step;
                                }
                            }
                        }
                    }
                    else if (row.Command == 11) SelectWindow(row.Arg1,row.Arg2 == 1);
                    else if (row.Command == 30) yield return decorations.Apply(row.Arg1);
                    else if (AppliesStoryMusic(row) || row.Command == 8) {lastMusic=row;lastMusicVolume=null;}
                    else if (row.Command == 26) lastPlaylist=row;
                    else if (row.Command == 40) lastMusicVolume=row;
                    else if (row.Command == 7 || row.Command == 10) lastAmbient=row;
                    else if(row.Command==80&&!loops.Contains(row))loops.Add(row);
                    else if(row.Command==240)voiceDefect=Mathf.Clamp01(Number(row.Arg1));
                    else effects.RestoreFinal(row);
                    if (++restored % 8 == 0) yield return null;
                }
                if(lastPlaylist!=null)yield return PlayPlaylistMusic(lastPlaylist);
                if (lastMusic != null && lastMusic.Command != 8)
                {
                    float gain=lastMusicVolume!=null && lastMusicVolume.Arg1==lastMusic.Arg1 ? .35f*Mathf.Clamp01(lastMusicVolume.Arg2) : StoryMusicGain(lastMusic);
                    yield return LoadAudio("story-music",lastMusic.Arg1,StoryMusic,chapter.ID!="main_32",gain);
                    cachedStoryMusic=gain;RestoreMusicPosition(lastMusic);
                }
                else if (lastMusic != null) StoryMusic.Stop();
                if (lastAmbient != null && lastAmbient.Command == 7) yield return LoadAudio("story-ambient",lastAmbient.Arg1,ambient,true,lastAmbient.Arg2 == 0 ? .3f : lastAmbient.Arg2);
                if (player.bones) player.bones.ResetDynamics();
                player.deskObjects.Action("DeactivateDropCoffee");
            }
            Replaying=false;
            player.deskObjects.restoring=false;
            yield return Read();
        }

        IEnumerator LoadAudio(string slot, string name, AudioSource source, bool loop, float volume,int musicVersion=-1)
        {
            source.Stop(); source.clip = null;
            if (string.IsNullOrEmpty(name) || name == "0") yield break;
            bool done = false; int token = generation;
            var alias = data.music.FirstOrDefault(m => string.Equals(m.title, name, StringComparison.OrdinalIgnoreCase));
            if (alias != null) name = alias.clip;
            player.library.LoadExternal(slot, name, asset => {
                if (token == generation && (musicVersion<0 || musicVersion==storyMusicVersion) && asset is AudioClip clip) { source.clip = clip; source.loop = loop; source.volume = volume;FeatureAudioGain(source,volume); source.Play();if(!wasFocused&&(source==voice||source==storyMusic)){source.Pause();if(source==voice)voicePausedForFocus=true;else storyMusicPausedForFocus=true;} }
                done = true;
            });
            while (!done && token == generation) yield return null;
        }

        IEnumerator Speak(ChillSentence row, bool waitForClick = true,bool applyMotion=true)
        {
            // VoiceText deliberately leaves body/facial/look state unchanged in ScenarioReader.
            if(applyMotion&&row.Command!=20)Motion(row); next = false;
            if ((row.Command == 0 || row.Command==80) && Number(row.Arg1) > 0) yield return new WaitForSecondsRealtime(Mathf.Min(120, Number(row.Arg1)));
            typewriter.Start(Text(row.LocalizationID),(data.parameters?.TalkSpeedData ?? new ChillTalkSpeed()).Delay(save.language),data.parameters?.typing);
            message = typewriter.Text; shownChars = 0; textClock = 0; typing = !typewriter.Complete;
            moveMouth = row.Command == 20 ? row.Arg1 == "1" : row.Command == 22 ? row.Arg3 != "-1" : row.Arg3 == "0";
            if (message.Length > 0) { history.Add(message); if (history.Count > 200) history.RemoveAt(0); }
            loadingAudio = true;
            string name = texts.TryGetValue(row.LocalizationID, out var local) ? local.Voice : "";
            yield return LoadAudio("story-voice", name, voice, false, .8f);
            if(voice.clip && voice.clip.loadState!=AudioDataLoadState.Loaded)
            {
                voice.clip.LoadAudioData();double deadline=Now+30;
                while(Playing && voice.clip && voice.clip.loadState==AudioDataLoadState.Loading && Now<deadline)yield return null;
            }
            yield return null; // Let the audio source apply its queued Play.
            loadingAudio = false;
            bool showSelection=waitForClick&&NextSentenceIsSelection();
            var autoWait=new ChillAutoAdvanceGate();float afterVoice=row.Command==0&&!showSelection?Mathf.Max(0,row.Arg2):0;
            while (Playing)
            {
                if (!waitForClick && choices.Count>0 && chosen >= 0) { voice.Stop(); yield break; }
                if (next)
                {
                    next = false;
                    if (typing) {if(typewriter.WaitingForInput)shownChars=typewriter.Advance(0,true);else typewriter.Accelerate(.01f);typing=!typewriter.Complete;}
                    else { voice.Stop(); yield break; }
                }
                bool ready=wasFocused && !HistoryVisible && !typing && !voice.isPlaying && (autoRead || automaticDialogue || !waitForClick || showSelection);
                if(autoWait.Ready(ready,Now,afterVoice))yield break;
                yield return null;
            }
        }

        int Jump(string target)
        {
            int index = Array.FindIndex(sentences, row => row.ID == target);
            if (index < 0) throw new InvalidOperationException("Không tìm thấy nhánh: " + target);
            return index;
        }

        IEnumerator Choose(int start, int command)
        {
            PrepareChoices(start,command);
            while (Playing&&chosen < 0) yield return null;
            if(!Playing)yield break;
            CommitChoice(command);
        }
        void PrepareChoices(int start,int command)
        {
            choices.Clear();chosen=-1;
            while(start>0&&sentences[start-1].Command==command)start--;
            for(int i=start;i<sentences.Length&&sentences[i].Command==command;i++)choices.Add(sentences[i]);
        }
        void CommitChoice(int command)
        {
            if(chosen<0||chosen>=choices.Count)return;
            if(command==15&&save.usedChoices.Contains(choices[chosen].Arg2)){chosen=-1;return;}
            var selected = choices[chosen]; choices.Clear();
            chosen=-1;next=false;
            if (command == 15) save.usedChoices.Add(selected.Arg2);
            cursor = Jump(selected.Arg1); save.cursor = cursor; SaveNow();
        }

        IEnumerator Read()
        {
            int processed = 0;
            while (Playing && cursor < sentences.Length)
            {
                current = sentences[cursor]; save.cursor = cursor;
                if (save.sceneHistory.Count == 0 || save.sceneHistory[save.sceneHistory.Count-1] != cursor) save.sceneHistory.Add(cursor);
                if (save.sceneHistory.Count > 8192) save.sceneHistory.RemoveRange(0,save.sceneHistory.Count-8192);
                SaveNow();
                bool advance = true;
                switch (current.Command)
                {
                    case 0: case 20: yield return Speak(current); break;
                    case 22: yield return SpeakPomodoro(current); break;
                    case 1: yield return new WaitForSecondsRealtime(Mathf.Clamp(Number(current.Arg1), 0, 120)); break;
                    case 50: yield return WaitForScript();break;
                    case 2:
                        next = false; status = current.Command == 50 ? "Bấm Tiếp để tiếp tục cảnh." : "Chờ thao tác tiếp theo.";
                        while (!next) yield return null; next = false; break;
                    case 3: case 15: yield return Choose(cursor, current.Command); advance = false; break;
                    case 4: cursor = Jump(current.Arg1); advance = false; break;
                    case 25: if(!talkLog)break;goto case 5;
                    case 26: StartCoroutine(PlayPlaylistMusic(current));break;
                    case 5:
                        StartCoroutine(PlayStoryMusic(current));break;
                    case 6: yield return LoadAudio("story-sound", current.Arg1, sound, false, .7f); break;
                    case 7: yield return LoadAudio("story-ambient", current.Arg1, ambient, true, current.Arg2 == 0 ? .3f : Mathf.Clamp01(current.Arg2)); break;
                    case 8: StopStoryMusic(current);break;
                    case 9: sound.Stop(); break;
                    case 10: ambient.Stop(); break;
                    case 11: SelectWindow(current.Arg1, current.Arg2 == 1); break;
                    case 12: Finish(); yield break;
                    case 13: save.usedChoices.Clear(); break;
                    case 14: case 16: case 17: case 18: case 19:
                        yield return RunTutorial(current.Command);break;
                    case 21:
                        yield return new WaitForSecondsRealtime(Mathf.Clamp(Number(current.Arg1),0,120));
                        Motion(current);
                        if (current.Arg2 < 0 && current.BodyMotion != -1) yield return WaitForMotion();
                        else if (current.Arg2 > 0) yield return new WaitForSecondsRealtime(Mathf.Min(current.Arg2,120));
                        break;
                    case 30:
                        yield return decorations.Apply(current.Arg1); break;
                    case 40: StartCoroutine(ChangeStoryMusicVolume(current));break;
                    case 60: message = ""; typing = false; break;
                    case 70:
                        var range = current.Arg1.Split('~');
                        bool inside = range.Length == 2 && DateTime.TryParseExact(range[0], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) && DateTime.TryParseExact(range[1], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to) && DateTime.Now >= from && DateTime.Now <= to;
                        cursor = Jump(inside ? current.Arg3 : current.Arg4); advance = false; break;
                    case 80: loops.Add(current); break;
                    case 81:
                        yield return RunTextLoop(current);
                        advance = false; break;
                    case 90: effects.Apply(current); yield return new WaitForSecondsRealtime(Mathf.Clamp(Number(current.Arg1),0,30)); break;
                    case 91: yield return PlayCredits();break;
                    case 92: GetComponent<ChillNativeUI>()?.StopNativeCredits();effects.ResetEffects();break;
                    case 100: effects.Apply(current); break;
                    case 200: case 205: case 210: case 215: case 216: case 219: case 220: case 230: case 231: case 240: case 250: case 500: case 501: case 1000:
                        if (current.Command == 240) voiceDefect = Mathf.Clamp01(Number(current.Arg1));
                        else effects.Apply(current);
                        break;
                    default: status = "Lệnh chưa được hỗ trợ: " + current.Command; yield break;
                }
                if (advance) cursor++;
                if (++processed % 32 == 0) yield return null;
            }
            if (Playing) Finish();
        }

        IEnumerator WaitForMotion()
        {
            double deadline=Now+30;
            yield return null;
            while (Playing && !presentation.MotionCompleted && Now < deadline) yield return null;
        }

        void SelectWindow(string name, bool active)
        {
            if (new[] { "Day", "Sunset", "Night", "Cloudy" }.Contains(name))
            { player.environment.SelectTime(Array.IndexOf(new[] { "Day", "Sunset", "Night", "Cloudy" }, name)); return; }
            switch (name)
            {
                case "Fireworks": name = "hanabio_For_Place"; break;
                case "WindBell": name = "Furin"; break;
                case "LightRain": name = "Rain"; break;
                case "HeavyRain": name = "RainHeavy"; break;
                case "ThunderRain": name = "Thunder"; break;
                case "HotSpring": name = "Onsen"; break;
                case "BlueButterfly": name = "AlterEgo"; break;
                case "City": name = "Buildings"; break;
                case "ValentineSweets": name = "valentine"; break;
                case "FreshGreenery": name = "Hometown"; break;
                case "SpaceB": name = "Space2"; break;
            }
            int index = Array.FindIndex(player.environment.environments, e => string.Equals(e.name.Replace("Env_", ""), name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) player.environment.SelectEnvironment(active ? index + 1 : 0);
        }

        void Finish()
        {
            var completed = chapter;if(completed==null)return;
            if (!save.completed.Contains(completed.ID)) save.completed.Add(completed.ID);
            CompletedAchievement(completed);
            if (completed.Scenario == 1 && Math.Abs(completed.EpisodeNumber - save.mainEpisode - 1) < .001) save.mainEpisode = completed.EpisodeNumber;
            save.sceneHistory.Clear();
            save.chapter = ""; save.cursor = 0; StopStory(); status = "Đã đọc xong: " + completed.ID; SaveNow();
        }

        bool Unlocked(ChillChapter item)
        {
            if (save.previewUnlock || save.completed.Contains(item.ID) || item.Scenario == 18) return true;
            if(item.Scenario>=50&&item.Scenario<=56)return CollaborationUnlocked(item);
            if (item.UnlockLevel > save.level) return false;
            if (item.Scenario == 1 && item.EpisodeNumber > save.mainEpisode + 1) return false;
            if (float.TryParse(item.UnlockStoryEpisode, NumberStyles.Float, CultureInfo.InvariantCulture, out float prerequisite) && prerequisite > save.mainEpisode) return false;
            return true;
        }

        void Update()
        {
            if (typing&&wasFocused) {shownChars=typewriter.Advance(Time.unscaledDeltaTime);typing=!typewriter.Complete;}
            if (data == null) return;
            player.character.SetBool("Enable_Talk", Playing && !Replaying && voice.isPlaying && moveMouth);
            if (voice && voice.isPlaying && voiceDefect > 0) voice.pitch = 1 + Mathf.Sin(Time.unscaledTime * 24) * voiceDefect * .12f;
            UpdateTimer();UpdateFeatures();
            if (Now - lastSave >= 5) SaveNow();
        }

        public void MotionSound(string name)
        {
            if (Replaying || data == null || !sound) return;
            if (name == "PlayKeyboard" || name == "StopKeyboard") { MotionLoop(true,name == "PlayKeyboard"); return; }
            if (name == "PlayWritePen" || name == "StopWritePen") { MotionLoop(false,name == "PlayWritePen"); return; }
            if (name.StartsWith("Stop")) return;
            if (name == "RubbingClothes") name="RubbingClothes"+UnityEngine.Random.Range(1,5);
            if (pendingSounds.Contains(name)) return;
            if (motionSounds.TryGetValue(name, out var clip)) { if (clip) sound.PlayOneShot(clip, .3f); return; }
            var alias = data.music.FirstOrDefault(m => m.title == name); if (alias == null || string.IsNullOrEmpty(alias.clip) || motionSounds.Count >= 64) return;
            pendingSounds.Add(name); int token = generation;
            player.library.LoadExternal("motion-sound-" + name, alias.clip, asset => {
                pendingSounds.Remove(name); motionSounds[name] = asset as AudioClip;
                if (token == generation && !Replaying && asset is AudioClip loaded) sound.PlayOneShot(loaded,.3f);
            });
        }
        void MotionLoop(bool isKeyboard,bool play)
        {
            var source=isKeyboard ? keyboard : writing;
            if (!source) return;
            int token=isKeyboard ? ++keyboardVersion : ++writingVersion;
            if (!play) { source.Stop(); return; }
            if (source.isPlaying) return;
            if (source.clip) { source.Play(); return; }
            var alias=data.music.FirstOrDefault(m => m.title == (isKeyboard ? "KeyboardTyping" : "WritePen"));
            if (alias == null || string.IsNullOrEmpty(alias.clip)) return;
            player.library.LoadExternal(isKeyboard ? "motion-keyboard" : "motion-writing",alias.clip,asset => {
                if (token != (isKeyboard ? keyboardVersion : writingVersion) || Replaying || !(asset is AudioClip clip)) return;
                source.clip=clip; source.volume=.15f; source.Play();
            });
        }
        public void StopMotionSounds()
        { keyboardVersion++; writingVersion++; if (keyboard) keyboard.Stop(); if (writing) writing.Stop(); }

        float RequiredExp()
        {
            var levels = data.parameters.LevelUpInfoData;
            return save.level - 1 < levels.nextLevelNecessaryExpArray.Length ? levels.nextLevelNecessaryExpArray[save.level - 1] : levels.nextLevelNecessaryExpBase;
        }
        void CreditWork(double seconds,double end=0)
        {
            if (seconds <= 0) return;
            var levels = data.parameters.LevelUpInfoData;
            seconds = Math.Min(seconds, levels.pomodoroExpBaseMaxHour * 3600);
            save.workedSeconds += seconds;
            RecordWork(seconds,end>0?end:Now);
            Workspace.pointRemainder+=seconds/60/Math.Max(.01f,Policy("PointUpInfoData.pomodoroPointBaseMinutes",1))*Policy("PointUpInfoData.pomodoroPointBaseMax",10);
            int earned=(int)Math.Min(int.MaxValue-Workspace.points,Math.Floor(Workspace.pointRemainder));Workspace.points+=earned;Workspace.pointRemainder-=earned;
            if(Workspace.collaboration>0){CreditCollaboration(seconds/(Math.Max(1,levels.pomodoroExpBaseMinutes)*60)*levels.pomodoroExpBaseMax);return;}
            save.exp += seconds / (Math.Max(1, levels.pomodoroExpBaseMinutes) * 60) * levels.pomodoroExpBaseMax;
            int previousLevel=save.level;
            while (save.exp >= RequiredExp() && RequiredExp() > 0 && save.level < 10000) { save.exp -= RequiredExp(); save.level++; }
            if(save.level>previousLevel)UISelectSound(3);
        }
        void UpdateTimer()
        {
            UpdateCountup();UpdateTimerVoice();
            if (!save.timerRunning) return;
            double now = Now;
            for (int catchup = 0; catchup < 8 && save.timerRunning; catchup++)
            {
                double tick = Math.Min(now, save.timerEnd);
                if (!save.breakTime) CreditWork(tick - save.lastTimerTick,tick);
                save.lastTimerTick = tick;
                if (now < save.timerEnd) break;
                if(catchup==0)TimerSound(3);
                if (!save.breakTime)
                {
                    save.rounds++;
                    AwardAchievement("COMPLETE_POMODORO_COUNT");
                    if (save.rounds >= save.loopCount) { save.timerRunning = false; save.pausedRemaining = 0; status = "Đã hoàn thành "+save.loopCount+" phiên làm việc.";TimerVoice("finish",false); SaveNow(); break; }
                }
                save.breakTime = !save.breakTime;
                if(catchup==0)TimerVoice(save.breakTime?"break":"start",false);
                save.lastTimerTick = save.timerEnd;
                save.timerEnd += (save.breakTime ? save.breakMinutes : save.workMinutes) * 60;
                SaveNow();
            }
        }

        void OnApplicationPause(bool paused) { if (paused && data != null) SaveNow(); }
        void OnApplicationQuit() { if (data != null) SaveNow(); }

        void OnGUI()
        {
            if (ChillGameplayUI.Active) return;
            if (!visible && !Playing) return;
            if (player.uiFont) GUI.skin.font = player.uiFont;
            var dialogueFont = save.language == "Ja" && japaneseFont ? japaneseFont : player.uiFont;
            if (storyLabel == null || storyLabel.font != dialogueFont)
            {
                storyLabel = new GUIStyle(GUI.skin.label) { font = dialogueFont, fontSize = 16, wordWrap = true };
                storyButton = new GUIStyle(GUI.skin.button) { font = dialogueFont, fontSize = 14, wordWrap = true };
            }
            float scale = Mathf.Clamp(Screen.width / 1000f, .65f, 1.5f); var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            GUILayout.BeginArea(new Rect(280, 12, Mathf.Max(260, width - 292), height - 24), GUI.skin.box);
            GUILayout.Label("Chill with You · Cốt truyện và tiến trình"); GUILayout.Label(status);
            if (data == null) { GUILayout.EndArea(); GUI.matrix = previous; return; }
            selectedTab = GUILayout.Toolbar(selectedTab, new[] { "Truyện", "Ghi chú", "Save", "Nhật ký", "Pomodoro" });
            if (Playing)
            {
                dialogueScroll = GUILayout.BeginScrollView(dialogueScroll, GUILayout.Height(Mathf.Min(180, height * .35f)));
                GUILayout.Label((current != null && current.Talker == 1 ? "Satone\n" : "") + message.Substring(0, Math.Min(shownChars, message.Length)), storyLabel);
                GUILayout.EndScrollView();
                if (loadingAudio) GUILayout.Label("Đang tải lời thoại…");
                for (int i = 0; i < choices.Count; i++)
                {
                    GUI.enabled = current.Command != 15 || !save.usedChoices.Contains(choices[i].Arg2);
                    if (GUILayout.Button(Text(choices[i].LocalizationID), storyButton)) chosen = i;
                }
                GUI.enabled = true;
                if (choices.Count == 0 && GUILayout.Button(typing ? "Hiện hết chữ" : "Tiếp")) next = true;
                autoRead = GUILayout.Toggle(autoRead, "Tự đọc tiếp");
                if (GUILayout.Button("Dừng truyện · giữ vị trí")) { SaveNow(); StopStory(); }
            }
            else if (selectedTab == 0)
            {
                if (!string.IsNullOrEmpty(save.chapter) && GUILayout.Button("Tiếp tục truyện đang đọc")) Begin(data.groups.First(g => g.ID == save.chapter), true);
                bool unlock = GUILayout.Toggle(save.previewUnlock, "Mở toàn bộ để thử (không bỏ qua tiến trình đã đọc)");
                if (unlock != save.previewUnlock) { save.previewUnlock = unlock; SaveNow(); }
                int language = GUILayout.Toolbar(save.language == "En" ? 0 : 1, new[] { "English", "Japanese" });
                save.language = language == 0 ? "En" : "Ja";
                int kind = GUILayout.Toolbar(listKind, new[] { "Chính", "Ngoại truyện", "Hội thoại", "Tất cả" });
                if (kind != listKind) { listKind = kind; page = 0; }
                string query = GUILayout.TextField(search); if (query != search) { search = query; page = 0; }
                var list = data.groups.Where(g => chapters.ContainsKey(g.ID) && (listKind == 3 || listKind == 0 && (g.Scenario == 1 || g.Scenario == 18) || listKind == 1 && (g.Scenario == 2 || g.Scenario == 3 || g.Scenario >= 24 && g.Scenario < 104) || listKind == 2 && g.Scenario != 1 && g.Scenario != 18))
                    .Where(g => string.IsNullOrEmpty(search) || (g.ID + Text(g.TitleLocalizationID)).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                GUILayout.BeginHorizontal();
                if (page > 0 && GUILayout.Button("←", GUILayout.Width(40))) page--;
                GUILayout.Label(list.Length + " đoạn · trang " + (page + 1));
                if ((page + 1) * 20 < list.Length && GUILayout.Button("→", GUILayout.Width(40))) page++;
                GUILayout.EndHorizontal();
                listScroll = GUILayout.BeginScrollView(listScroll);
                foreach (var item in list.Skip(page * 20).Take(20))
                {
                    GUI.enabled = Unlocked(item);
                    string title = Text(item.TitleLocalizationID); if (string.IsNullOrEmpty(title)) title = item.ID;
                    if (GUILayout.Button((save.completed.Contains(item.ID) ? "✓ " : "") + title + (!GUI.enabled ? " · Lv " + item.UnlockLevel : ""), storyButton)) Begin(item, false);
                }
                GUI.enabled = true; GUILayout.EndScrollView();
            }
            if (selectedTab == 1)
            {
                noteScroll = GUILayout.BeginScrollView(noteScroll); save.note = GUILayout.TextArea(save.note ?? "", 64000, GUILayout.MinHeight(120));
                newTodo = GUILayout.TextField(newTodo, 200);
                if (GUILayout.Button("Thêm công việc") && !string.IsNullOrWhiteSpace(newTodo) && save.todos.Count < 100) { save.todos.Add(new ChillTodo { title = newTodo.Trim() }); newTodo = ""; SaveNow(); }
                for (int i = 0; i < save.todos.Count; i++)
                { GUILayout.BeginHorizontal(); var todo = save.todos[i]; todo.done = GUILayout.Toggle(todo.done, todo.title); if (GUILayout.Button("×", GUILayout.Width(30))) { save.todos.RemoveAt(i--); SaveNow(); } GUILayout.EndHorizontal(); }
                GUILayout.EndScrollView();
            }
            if (selectedTab == 2)
            {
                GUILayout.Label("Cấp " + save.level + " · " + save.completed.Count + " đoạn đã đọc\nSave của bản web được lưu riêng trong trình duyệt này.");
                if (GUILayout.Button("Lưu ngay")) { saveAllowed = true; if (SaveNow()) status = "Đã lưu tiến trình."; }
                if (GUILayout.Button("Xuất file save")) { SaveNow(); ChillBrowserSave.Export(save); }
                if (GUILayout.Button("Nhập file save web")) ChillBrowserSave.Import(gameObject.name);
            }
            if (selectedTab == 3)
            { listScroll = GUILayout.BeginScrollView(listScroll); foreach (string line in history) GUILayout.Label(line, storyLabel); GUILayout.EndScrollView(); }
            if (selectedTab == 4)
            {
                GUILayout.Label("Cấp " + save.level + " · " + save.exp.ToString("F0") + "/" + RequiredExp().ToString("F0") + " EXP\nĐã làm việc: " + TimeSpan.FromSeconds(save.workedSeconds).ToString(@"hh\:mm\:ss"));
                GUI.enabled = !save.timerRunning;
                GUILayout.Label("Làm việc: " + save.workMinutes + " phút"); save.workMinutes = (int)GUILayout.HorizontalSlider(save.workMinutes, 1, 180);
                GUILayout.Label("Nghỉ: " + save.breakMinutes + " phút"); save.breakMinutes = (int)GUILayout.HorizontalSlider(save.breakMinutes, 1, 120);
                GUI.enabled = true;
                double remaining = save.timerRunning ? Math.Max(0, save.timerEnd - Now) : save.pausedRemaining;
                GUILayout.Label((save.breakTime ? "Nghỉ · " : "Làm việc · ") + TimeSpan.FromSeconds(remaining).ToString(@"hh\:mm\:ss"));
                if (GUILayout.Button(save.timerRunning ? "Tạm dừng" : save.pausedRemaining > 0 ? "Tiếp tục" : "Bắt đầu"))
                {
                    if (save.timerRunning) { UpdateTimer(); save.pausedRemaining = Math.Max(0, save.timerEnd - Now); save.timerRunning = false; }
                    else { save.timerEnd = Now + (save.pausedRemaining > 0 ? save.pausedRemaining : save.workMinutes * 60); save.lastTimerTick = Now; save.timerRunning = true; }
                    SaveNow();
                }
                if (GUILayout.Button("Kết thúc phiên")) { UpdateTimer(); save.timerRunning = false; save.pausedRemaining = 0; save.breakTime = false; SaveNow(); }
            }
            GUILayout.EndArea(); GUI.matrix = previous;
        }
    }
}
