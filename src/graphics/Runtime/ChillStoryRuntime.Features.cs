using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hlc.ChillPrototype
{
    public sealed partial class ChillStoryRuntime
    {
        public TextAsset featureConfiguration;
        public ChillFeatureData Features {get;private set;}
        public ChillWebSettings Settings {get {EnsureFeaturesSave();return save.settings;}}
        public ChillMusicState MusicState {get {EnsureFeaturesSave();return save.musicState;}}
        public ChillTrack CurrentTrack => Tracks.FirstOrDefault(t=>t.guid==MusicState.selected);
        public ChillTrack[] Tracks => Features==null?Array.Empty<ChillTrack>():Features.music.Concat(MusicState.imports).ToArray();
        public bool TrackAvailable(ChillTrack track)=>save.previewUnlock||string.IsNullOrEmpty(track.requiredChapter)||save.completed.Contains(track.requiredChapter);
        public ChillTrack[] Queue => Tracks.Where(TrackAvailable).Where(t=>MusicState.playlist=="ALL" || MusicState.playlist=="Favorites" && MusicState.favorites.Contains(t.guid) || MusicState.playlist=="Imported" && t.personal || MusicState.playlists.Any(p=>p.name==MusicState.playlist && p.tracks.Contains(t.guid))).OrderBy(t=>PlaylistOrder(t.guid)).ToArray();
        public int MusicRevision {get;private set;}
        public bool MusicLoading {get;private set;}
        public string MusicError {get;private set;}="";
        public bool FeatureReady => Features!=null && data!=null;
        AudioClip managedMusic,personalClip;
        bool wantsMusic,musicStarted,wasFocused=true;
        bool voicePausedForFocus,storyMusicPausedForFocus;
        int musicToken,seekFrame=-1;
        readonly HashSet<int> ambiencePending=new HashSet<int>();
        float cachedStoryVoice=.8f,cachedStorySound=1,cachedStoryAmbient=.3f,cachedStoryMusic=.35f;
        double nextClock,sessionStarted,nextReminder;
        readonly List<string> shuffleBag=new List<string>();
        readonly Dictionary<int,AudioSource> ambienceSources=new Dictionary<int,AudioSource>();
        readonly Dictionary<int,int> ambienceTokens=new Dictionary<int,int>();
        bool automaticDialogue,talkLog=true,suspendedStoryLive;
        int tutorialCommand;
        string suspendedChapter="";int suspendedCursor;List<int> suspendedScene;

        void EnsureFeaturesSave()
        {
            EnsureWorkspace();
            if(save.settings==null)save.settings=new ChillWebSettings();
            if(save.settings.ambience==null)save.settings.ambience=new List<ChillAmbientLevel>();
            if(save.musicState==null)save.musicState=new ChillMusicState();
            if(save.musicState.favorites==null)save.musicState.favorites=new List<string>();
            if(save.musicState.playlists==null)save.musicState.playlists=new List<ChillPlaylist>();
            if(save.musicState.imports==null)save.musicState.imports=new List<ChillTrack>();
            if(save.conversationHistory==null)save.conversationHistory=new List<string>();
        }
        void ValidateFeatures(ChillSave value)
        {
            ValidateWorkspace(value);
            if(value.settings==null)value.settings=new ChillWebSettings();
            if(value.musicState==null)value.musicState=new ChillMusicState();
            var s=value.settings;var m=value.musicState;
            if(s.cameraView<0||s.cameraView>2)throw new ArgumentException("Invalid room viewpoint.");
            if(s.ambience==null)s.ambience=new List<ChillAmbientLevel>();
            if(m.imports==null)m.imports=new List<ChillTrack>();if(m.favorites==null)m.favorites=new List<string>();if(m.playlists==null)m.playlists=new List<ChillPlaylist>();
            if(value.conversationHistory==null)value.conversationHistory=new List<string>();
            float[] volumes={s.master,s.music,s.voice,s.sfx,s.ambient,s.timer};
            if(volumes.Any(v=>float.IsNaN(v)||float.IsInfinity(v)||v<0||v>1) || float.IsNaN(s.renderScale)||s.renderScale<.5f||s.renderScale>1.5f || s.quality<0||s.quality>2 || s.activeFps<15||s.activeFps>120||s.inactiveFps<5||s.inactiveFps>60)throw new ArgumentException("Invalid audio/graphics settings.");
            if(s.ambience.Count>30||s.ambience.Any(a=>a==null||a.id<0||a.id>29||float.IsNaN(a.volume)||a.volume<0||a.volume>1)||m.imports.Count>200||m.imports.Any(t=>t==null||!t.personal||!(t.guid??"").StartsWith("local-")||(t.title??"").Length>240)||m.playlists.Count>20||m.playlists.Any(p=>p==null||(p.name??"").Length>60||p.tracks==null||p.tracks.Count>500)||m.favorites.Count>500||value.conversationHistory.Count>200||value.conversationHistory.Any(t=>t==null||t.Length>4000)||float.IsNaN(m.position)||float.IsInfinity(m.position)||m.position<0)throw new ArgumentException("Invalid music/history data.");
            if(float.IsNaN(s.dayStart)||float.IsNaN(s.sunsetStart)||float.IsNaN(s.nightStart)||s.dayStart<0||s.nightStart>24||s.dayStart>s.sunsetStart||s.sunsetStart>s.nightStart)throw new ArgumentException("Invalid automatic day boundaries.");
            if(s.birthdayMonth<0||s.birthdayMonth>12||s.birthdayDay<0||s.birthdayDay>31)throw new ArgumentException("Invalid birthday.");
        }
        IEnumerator InitializeFeatures()
        {
            EnsureFeaturesSave();
            if(!featureConfiguration)yield break;
            Features=JsonUtility.FromJson<ChillFeatureData>(featureConfiguration.text);
            history=new List<string>(save.conversationHistory);
            sessionStarted=Now;var previousVisit=Settings.lastVisit;
            ApplyFeatureSettings();InitializeAutonomy();
            foreach(var level in Settings.ambience.ToArray())if(level.volume>0)SetAmbience(level.id,level.volume,false);
            ChillPersonalMusic.Attach(gameObject.name);
            while(!player.library.Ready)yield return null;
            DailyAppearance();RestoreRoom();
            if(!string.IsNullOrEmpty(MusicState.selected))SelectTrack(MusicState.selected,false,MusicState.position);
            // An interrupted story stays resumable; greetings never overwrite it.
            if(Settings.autoGreeting && string.IsNullOrEmpty(save.chapter))
            {
                yield return new WaitForSecondsRealtime(1);
                if(!Playing && !TryCalendarEvent())
                {
                    int scenario=previousVisit==0?11:Now-previousVisit>365*86400?17:Now-previousVisit>30*86400?16:Now-previousVisit>15*86400?15:Now-previousVisit>2*86400?14:13;
                    StartAutomaticScenario(scenario);
                }
            }
        }
        void CaptureFeatures()
        {
            EnsureFeaturesSave();Settings.lastVisit=Now;
            if(!Playing)Settings.music=player.Volume;
            if(managedMusic && player.music.clip==managedMusic && Time.frameCount!=seekFrame)MusicState.position=player.music.time;
            save.conversationHistory=history.Skip(Math.Max(0,history.Count-200)).ToList();
        }
        public void CommitSettings(){ApplyFeatureSettings();SaveNow();}
        public void ApplyFeatureSettings()
        {
            var s=Settings;AudioListener.volume=s.master;if(!Playing)player.SetVolume(s.music);
            player.SetLowQuality(s.quality==0);
            Application.targetFrameRate=wasFocused?s.activeFps:s.inactiveFps;
            if(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
            {pipeline.renderScale=s.renderScale;pipeline.msaaSampleCount=s.quality==2?4:2;pipeline.shadowDistance=s.quality==0?8:15;}
            var cameraData=player.view.GetUniversalAdditionalCameraData();cameraData.renderPostProcessing=s.postProcessing;
            foreach(var source in ambienceSources)source.Value.volume=AmbientLevel(source.Key)*s.ambient;
            nextClock=0;
        }
        void FeatureAudioGain(AudioSource source,float value)
        {if(source==voice)cachedStoryVoice=value;else if(source==sound)cachedStorySound=value;else if(source==ambient)cachedStoryAmbient=value;else if(source==storyMusic)cachedStoryMusic=value;}
        void UpdateFeatures()
        {
            if(!FeatureReady)return;
            var s=Settings;
            if(storyMusic)storyMusic.volume=Mathf.Clamp01(cachedStoryMusic*(s.music/.35f));
            voice.volume=cachedStoryVoice*s.voice;sound.volume=cachedStorySound*s.sfx;
            keyboard.volume=writing.volume=.15f*s.sfx;ambient.volume=cachedStoryAmbient*s.ambient;
            if(uiSound)uiSound.volume=s.sfx*uiCueGain;if(timerSound)timerSound.volume=s.timer*timerCueGain;
            if(!Playing && s.autoTime && Now>=nextClock)
            {nextClock=Now+30;float h=(float)DateTime.Now.TimeOfDay.TotalHours;int mode=h>=s.dayStart&&h<s.sunsetStart?0:h>=s.sunsetStart&&h<s.nightStart?1:2;if(player.environment.selectedTime!=mode)player.environment.SelectTime(mode);}
            if(musicboxActive)player.music.volume=Settings.music*.15f;
            if(managedMusic && !Playing && player.music.clip==managedMusic && !MusicLoading)
            {
                player.music.loop=MusicState.repeat;
                if(player.music.isPlaying)musicStarted=true;
                // A paused track cannot accidentally trigger next. A completed clip can.
                if(wantsMusic && musicStarted && !player.music.isPlaying && !MusicState.repeat){musicStarted=false;NextTrack(1);}
            }
            if(!Playing&&Now>=nextReminder){nextReminder=Now+30;DailyAppearance();if(Settings.autoGreeting)TryCalendarEvent();if(Workspace.reminders){var today=DateKey(DateTime.Now);var key="due:"+today;var count=Workspace.lists.SelectMany(l=>l.items).Count(t=>!t.done&&t.due==today);if(count>0&&!Workspace.calendarEvents.Contains(key)){Workspace.calendarEvents.Add(key);Notify(count+" tasks are due today.");ChillBrowser.Notify("Chill with You",count+" tasks are due today.");SaveNow();}}}
            UpdateAutonomy();
        }
        int PlaylistOrder(string guid)
        {var p=MusicState.playlists.FirstOrDefault(p=>p.name==MusicState.playlist);return p==null?0:p.tracks.IndexOf(guid);}
        public void SetPlaylist(string name)
        {MusicState.playlist=name;shuffleBag.Clear();MusicRevision++;SaveNow();}
        public string[] PlaylistNames=>new[]{"ALL","Favorites","Imported"}.Concat(MusicState.playlists.Select(p=>p.name)).ToArray();
        public void CreatePlaylist(string name)
        {
            name=(name??"").Trim();if(name.Length<1||name.Length>60||PlaylistNames.Contains(name)||MusicState.playlists.Count>=20){Notify("Playlist name must be unique (1–60 characters).",true);return;}
            MusicState.playlists.Add(new ChillPlaylist{name=name});SetPlaylist(name);
        }
        public void DeletePlaylist()
        {MusicState.playlists.RemoveAll(p=>p.name==MusicState.playlist);SetPlaylist("ALL");}
        public void AddTrackToPlaylist(string guid)
        {var list=MusicState.playlists.FirstOrDefault(p=>p.name==MusicState.playlist);if(list==null){Notify("Select a custom playlist first.");return;}if(!list.tracks.Contains(guid)&&list.tracks.Count<500)list.tracks.Add(guid);MusicRevision++;SaveNow();}
        public void MoveTrack(string guid,int offset)
        {var list=MusicState.playlists.FirstOrDefault(p=>p.name==MusicState.playlist);if(list==null)return;int from=list.tracks.IndexOf(guid),to=Mathf.Clamp(from+offset,0,list.tracks.Count-1);if(from<0||to==from)return;list.tracks.RemoveAt(from);list.tracks.Insert(to,guid);MusicRevision++;SaveNow();}
        public void RemoveTrack(string guid)
        {
            var list=MusicState.playlists.FirstOrDefault(p=>p.name==MusicState.playlist);
            if(list!=null)list.tracks.Remove(guid);
            else if(MusicState.imports.Any(t=>t.guid==guid))
            {
                ChillPersonalMusic.Remove(gameObject.name,guid);return;
            }
            MusicRevision++;SaveNow();
        }
        public void ToggleFavorite(string guid)
        {if(!MusicState.favorites.Remove(guid))MusicState.favorites.Add(guid);MusicRevision++;SaveNow();}
        public void ToggleShuffle(){MusicState.shuffle=!MusicState.shuffle;shuffleBag.Clear();MusicRevision++;SaveNow();}
        public void ToggleRepeat(){MusicState.repeat=!MusicState.repeat;if(!Playing)player.music.loop=MusicState.repeat;MusicRevision++;SaveNow();}
        public void ToggleMusic()
        {
            if(Playing)return;
            if(MusicLoading){wantsMusic=!wantsMusic;return;}
            if(player.music.isPlaying){wantsMusic=false;player.music.Pause();}
            else if(managedMusic){wantsMusic=true;player.music.Play();musicStarted=true;}
            else if(!string.IsNullOrEmpty(MusicState.selected))SelectTrack(MusicState.selected,true,MusicState.position);else if(Queue.Length>0)SelectTrack(Queue[0].guid,true);
            SaveNow();
        }
        public void SeekMusic(float seconds)
        {if(!Playing && managedMusic && player.music.clip==managedMusic){float target=Mathf.Clamp(seconds,0,Mathf.Max(0,managedMusic.length-.05f));player.music.timeSamples=Mathf.RoundToInt(target*managedMusic.frequency);MusicState.position=target;seekFrame=Time.frameCount;SaveNow();}}
        public void NextTrack(int direction)
        {
            if(Playing)return;var queue=Queue;if(queue.Length==0){wantsMusic=false;return;}
            string id;
            if(direction>0 && MusicState.shuffle)
            {
                shuffleBag.RemoveAll(g=>!queue.Any(t=>t.guid==g));
                if(shuffleBag.Count==0)shuffleBag.AddRange(queue.Where(t=>queue.Length==1||t.guid!=MusicState.selected).OrderBy(_=>UnityEngine.Random.value).Select(t=>t.guid));
                id=shuffleBag[0];shuffleBag.RemoveAt(0);
            }
            else {int index=Array.FindIndex(queue,t=>t.guid==MusicState.selected);id=queue[(index+direction+queue.Length)%queue.Length].guid;}
            SelectTrack(id,true);
        }
        public void SelectTrack(string guid,bool play=true,float seek=0)
        {
            if(Playing)return;var track=Tracks.FirstOrDefault(t=>t.guid==guid);if(track==null||!TrackAvailable(track))return;
            int token=++musicToken;MusicLoading=true;MusicError="";wantsMusic=play;musicStarted=false;
            if(Application.isPlaying&&!VerificationOnly)StartCoroutine(ChangeTrack(track,seek,token));else RequestTrack(track,seek,token);
        }
        IEnumerator ChangeTrack(ChillTrack track,float seek,int token)
        {
            float initial=player.music.volume;
            if(player.music.isPlaying)for(float elapsed=0;elapsed<.22f;elapsed+=Time.unscaledDeltaTime){if(token!=musicToken||Playing)yield break;player.music.volume=initial*(1-elapsed/.22f);yield return null;}
            if(token==musicToken&&!Playing)RequestTrack(track,seek,token);
        }
        void RequestTrack(ChillTrack track,float seek,int token)
        {
            string guid=track.guid;
            player.music.Stop();player.music.clip=null;managedMusic=null;
            if(personalClip){Destroy(personalClip);personalClip=null;}
            MusicState.selected=guid;MusicState.position=seek;MusicRevision++;
            if(track.personal)ChillPersonalMusic.Load(gameObject.name,guid,token);
            else player.library.LoadExternal("player-music",guid,asset=>{
                if(token!=musicToken)return;
                if(asset is AudioClip clip)FinishTrack(clip,seek);else MusicFailed("Music asset could not be loaded.");
            });
        }
        void FinishTrack(AudioClip clip,float seek)
        {MusicLoading=false;managedMusic=clip;player.music.clip=clip;player.music.loop=MusicState.repeat;player.music.time=Mathf.Clamp(seek,0,Mathf.Max(0,clip.length-.05f));player.SetVolume(Settings.music);if(wantsMusic){player.music.Play();musicStarted=true;if(Application.isPlaying&&!VerificationOnly)StartCoroutine(FadeInMusic(musicToken));}MusicRevision++;SaveNow();}
        IEnumerator FadeInMusic(int token){for(float elapsed=0;elapsed<.22f;elapsed+=Time.unscaledDeltaTime){if(token!=musicToken||Playing)yield break;player.music.volume=Settings.music*Mathf.Clamp01(elapsed/.22f);yield return null;}if(token==musicToken&&!Playing)player.music.volume=Settings.music;}
        void MusicFailed(string text){MusicLoading=false;wantsMusic=musicStarted=false;MusicError=text;Notify(text,true);MusicRevision++;}
        [Serializable] class ImportedTrackResult {public string guid,title,artist,error,url;public int token;}
        public void PersonalTrackRemoved(string json)
        {
            var result=JsonUtility.FromJson<ImportedTrackResult>(json);if(!string.IsNullOrEmpty(result.error)){Notify(result.error,true);return;}
            if(!MusicState.imports.Any(t=>t.guid==result.guid))return;
            if(MusicState.selected==result.guid){musicToken++;player.music.Stop();player.music.clip=null;managedMusic=null;wantsMusic=false;MusicLoading=false;MusicState.selected="";if(personalClip){Destroy(personalClip);personalClip=null;}}
            MusicState.imports.RemoveAll(t=>t.guid==result.guid);MusicState.favorites.Remove(result.guid);foreach(var p in MusicState.playlists)p.tracks.Remove(result.guid);MusicRevision++;SaveNow();
        }
        public void PersonalTrackImported(string json)
        {var result=JsonUtility.FromJson<ImportedTrackResult>(json);if(!string.IsNullOrEmpty(result.error)){Notify(result.error,true);return;}var track=MusicState.imports.FirstOrDefault(t=>t.guid==result.guid);if(track==null){if(MusicState.imports.Count>=200){Notify("Maximum 200 imported tracks.",true);return;}track=new ChillTrack{guid=result.guid,personal=true};MusicState.imports.Add(track);}track.title=Trim(result.title,240);track.artist=Trim(string.IsNullOrEmpty(result.artist)?"Personal music":result.artist,240);AwardAchievement("IMPORT_MUSIC_COUNT");MusicRevision++;SaveNow();}
        public void PersonalTrackReady(string json)
        {var result=JsonUtility.FromJson<ImportedTrackResult>(json);if(result.token!=musicToken){ChillPersonalMusic.Revoke(result.url);return;}if(!string.IsNullOrEmpty(result.error)){MusicFailed(result.error);return;}StartCoroutine(LoadPersonalClip(result));}
        IEnumerator LoadPersonalClip(ImportedTrackResult result)
        {
            using(var request=UnityWebRequestMultimedia.GetAudioClip(result.url,AudioType.WAV))
            {
                yield return request.SendWebRequest();
                if(result.token==musicToken)
                {if(request.result!=UnityWebRequest.Result.Success)MusicFailed(request.error);else {personalClip=DownloadHandlerAudioClip.GetContent(request);personalClip.name=CurrentTrack?.title??"Personal music";FinishTrack(personalClip,MusicState.position);}}
            }
            ChillPersonalMusic.Revoke(result.url);
        }
        public float AmbientLevel(int id)=>Settings.ambience.FirstOrDefault(a=>a.id==id)?.volume??0;
        public void SetAmbience(int id,float value,bool persist=true)
        {
            var track=Features?.ambient.FirstOrDefault(t=>t.id==id);if(track==null)return;value=Mathf.Clamp01(value);
            var level=Settings.ambience.FirstOrDefault(a=>a.id==id);if(level==null){level=new ChillAmbientLevel{id=id};Settings.ambience.Add(level);}level.volume=value;
            int token=ambienceTokens.TryGetValue(id,out var version)?version+1:1;ambienceTokens[id]=token;
            if(!ambienceSources.TryGetValue(id,out var source)){source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=true;ambienceSources[id]=source;}
            source.volume=value*Settings.ambient;
            if(value==0){source.Stop();source.clip=null;player.library.ReleaseExternal("ambience-"+id);}
            else if(source.clip){if(!source.isPlaying)source.Play();}
            else if(!ambiencePending.Contains(id))
            {
                ambiencePending.Add(id);
                player.library.LoadExternal("ambience-"+id,track.guid,asset=>{ambiencePending.Remove(id);if(asset is AudioClip clip){if(AmbientLevel(id)>0){source.clip=clip;source.volume=AmbientLevel(id)*Settings.ambient;source.Play();}else player.library.ReleaseExternal("ambience-"+id);}else Notify("Ambient track unavailable: "+track.name,true);});
            }
            if(persist)SaveNow();
        }
        public void StopAllAmbience(){foreach(var a in Features.ambient)if(AmbientLevel(a.id)>0)SetAmbience(a.id,0,false);SaveNow();}
        public void SaveFromBrowser(){if(data!=null)SaveNow();}
        void OnApplicationFocus(bool focused)
        {
            bool previously=wasFocused;wasFocused=focused;
            if(previously!=focused)
            {
                if(!focused){voicePausedForFocus=voice&&voice.isPlaying;storyMusicPausedForFocus=storyMusic&&storyMusic.isPlaying;if(voicePausedForFocus)voice.Pause();if(storyMusicPausedForFocus)storyMusic.Pause();}
                else{if(Playing&&voicePausedForFocus&&voice)voice.UnPause();if(Playing&&storyMusicPausedForFocus&&storyMusic)storyMusic.UnPause();voicePausedForFocus=storyMusicPausedForFocus=false;}
            }
            if(!FeatureReady)return;Application.targetFrameRate=focused?Settings.activeFps:Settings.inactiveFps;SaveNow();
            if(previously!=focused && Settings.selfTalk && !Playing && sessionStarted>0 && Now-sessionStarted>10)StartAutomaticScenario(focused?(Resting?2751:2752):(Resting?2701:2702));
        }
        public void BrowserVisibility(string value)=>OnApplicationFocus(value=="visible");
        public bool StartAutomaticScenario(int scenario)
        {
            if(!FeatureReady || Playing || autonomousSequence!=null)return false;
            var list=data.groups.Where(g=>g.Scenario==scenario&&Unlocked(g)&&chapters.ContainsKey(g.ID)).ToArray();if(list.Length==0)return false;
            var unseen=list.Where(g=>!save.completed.Contains(g.ID)).ToArray();if(unseen.Length>0)list=unseen;
            var selected=list[UnityEngine.Random.Range(0,list.Length)];
            suspendedChapter=save.chapter;suspendedCursor=save.cursor;suspendedScene=new List<int>(save.sceneHistory);suspendedStoryLive=save.storyLive;
            Begin(selected,false,true);return true;
        }
        void RestoreAutomaticResume()
        {if(!automaticDialogue)return;automaticDialogue=false;save.chapter=suspendedChapter;save.cursor=suspendedCursor;save.sceneHistory=suspendedScene??new List<int>();save.storyLive=suspendedStoryLive;suspendedChapter="";talkLog=!save.storyLive;ResetAutonomy();}
        bool TryCalendarEvent()
        {
            var date=DateTime.Now;
            if(Settings.birthdayMonth==date.Month&&Settings.birthdayDay==date.Day&&CalendarScenario(103,"birthday:"+DateKey(date)))return true;
            // Source scenarios are date-specific releases, not generic annual events.
            if(date.Year==2025&&date.Month==12&&date.Day==25)return CalendarScenario(100,"event:"+DateKey(date));
            if(date.Year==2026&&date.Month==4&&date.Day==1)return CalendarScenario(102,"event:"+DateKey(date));
            if(date.Month==1&&date.Day==1)return CalendarScenario(101,"newyear:"+date.Year);
            return false;
        }
        bool CalendarScenario(int scenario,string key){if(Workspace.calendarEvents.Contains(key)||!StartAutomaticScenario(scenario))return false;Workspace.calendarEvents.Add(key);SaveNow();return true;}
        public void Farewell(){if(Playing){Notify("Finish or skip the current conversation first.");return;}double hours=(Now-sessionStarted)/3600;int scenario=19;
            if(hours>=Policy("GameEndTalkData.gameLongTimeEndTalkData.judgeLongHours",4)&&Chance(Policy("GameEndTalkData.gameLongTimeEndTalkData.selectionProbability",50)))scenario=1500;
            else if(hours<=Policy("GameEndTalkData.gameShortTimeEndTalkData.judgeShortHours",1)&&Chance(Policy("GameEndTalkData.gameShortTimeEndTalkData.selectionProbability",65)))scenario=1501;
            AwardAchievement("EXIT_GAME_COUNT");if(!StartAutomaticScenario(scenario))StartAutomaticScenario(19);SaveNow();}
        public void Tutorial(){if(!Playing){var item=data.groups.FirstOrDefault(g=>g.Scenario==18);if(item!=null)Begin(item,false);}}
        public int TutorialCommand=>tutorialCommand;
        public void TutorialSignal(string action)
        {if(tutorialCommand==50 && action=="satone")ReadNextForScript();else if(tutorialCommand==14&&action=="satone" || tutorialCommand==16&&action=="timer-toggle" || tutorialCommand==17&&action=="todos" || tutorialCommand==18&&action=="notes" || tutorialCommand==19&&action=="environment")next=true;}
        IEnumerator RunTutorial(int command)
        {
            tutorialCommand=command;next=false;typing=false;message="";shownChars=0;
            var ui=GetComponent<ChillNativeUI>();
            if(command==16||command==17||command==18){if(ui)yield return ui.FocusTutorial(command);}
            else if(command==19){if(ui)yield return ui.ShowNativeTutorial();}
            else {message="Click Satone to continue.";shownChars=message.Length;while(Playing&&!next)yield return null;}
            next=false;tutorialCommand=0;
        }
        public IEnumerator PlayCredits()
        {
            var ui=GetComponent<ChillNativeUI>();if(!ui)yield break;
            yield return ui.PlayNativeCredits(Features);
        }
    }
}
