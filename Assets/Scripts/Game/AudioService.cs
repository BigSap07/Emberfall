using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Emberfall.Core.Data;
using Random = UnityEngine.Random;

namespace Emberfall.Game
{
    /// <summary>
    /// THE ONE AUDIO SERVICE (R10-AUDIO, Docs/DESIGN-AUDIO.md). Plays the cues in audio.json on
    /// three channels: music (two sources and a crossfade), sfx and ui (a voice pool each, with a
    /// slider each in AudioPrefs).
    ///
    /// It is a player for names, nothing more. A screen or the battle view says "ui.tap" or
    /// "battle.crit"; which file that is, how loud, how it layers and how many may sound at once
    /// are all data in audio.json. No game logic is decided here, and combat is untouched: the
    /// battle view calls in at the moment a BattleEvent lands, which is already the impact beat.
    ///
    /// MAKING STOCK SOUNDS OURS happens here and never in the files: every sfx and ui play gets a
    /// small random pitch (default plus or minus 4 percent) and a small volume dip, and a cue can
    /// carry layers, extra sources started with it, which is how a hit gets an impact under it.
    ///
    /// A FULL POOL NEVER STACKS SOUNDS: a cue is held to PerCueVoices at once (the oldest play of
    /// that cue is cut), the pool is held to SfxVoices (a lower priority voice is cut for a higher
    /// one, otherwise the new sound is dropped), and a cue can carry a cooldown for an area hit
    /// that lands five Damage events in one frame.
    ///
    /// Sfx owns this and is the only thing that reaches it, so the old synthesised sounds stay one
    /// flag away: with audio.json's realAudio false, or no audio.json, Enabled is false and Sfx
    /// plays what it always played.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        private sealed class Voice : VoiceSlot
        {
            public AudioSource Source;
            public float StopAt;
            public float Volume;
        }

        private sealed class Pending
        {
            public float At;
            public AudioChannel Channel;
            public string Cue;
            public AudioClip Clip;
            public float Volume;
            public float Pitch;
            public float Trim;
            public int Priority;
        }

        /// <summary>One music source, or one loop: a level that moves toward a target.</summary>
        private sealed class Fader
        {
            public AudioSource Source;
            public float Level;
            public float Target;
            public float Rate;
            public float Base = 1f;
            public string Cue = "";
            public AudioChannel Channel;
        }

        private AudioCueConfig _config = new AudioCueConfig();
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> _missing = new HashSet<string>();
        private readonly CueCooldowns _cooldowns = new CueCooldowns();
        private readonly Dictionary<string, int> _lastPick = new Dictionary<string, int>();
        private readonly List<Voice> _sfxVoices = new List<Voice>();
        private readonly List<Voice> _uiVoices = new List<Voice>();
        private readonly List<Pending> _pending = new List<Pending>();
        private readonly Fader[] _music = new Fader[2];
        private readonly Dictionary<string, Fader> _loops = new Dictionary<string, Fader>();
        private string _musicCue = "";

        public AudioCueConfig Config { get { return _config; } }

        /// <summary>True when sounds come from audio.json. False is the rollback: Sfx plays its
        /// synthesised set and never calls in here.</summary>
        public bool Enabled { get { return _config.UsesRealAudio; } }

        /// <summary>The music cue that was last asked for, silent or not. Empty before any.</summary>
        public string MusicCue { get { return _musicCue; } }

        /// <summary>Plays dropped because the pool was full, for the play proof.</summary>
        public int Dropped { get; private set; }

        /// <summary>Resources.Load key of audio.json, under Resources.</summary>
        public const string ConfigResource = "Content/audio";

        /// <summary>Seconds of fade at the end of a trimmed sound.</summary>
        private const float TrimFade = 0.12f;

        public void Init()
        {
            TextAsset asset = Resources.Load<TextAsset>(ConfigResource);
            AudioCueConfig config = AudioCueConfig.FromJson(asset != null ? asset.text : null);
            // One launch with the rollback on, without editing audio.json: set EMBERFALL_REAL_AUDIO=0.
            // Editor and development builds only; a release build reads the file alone.
            if ((Application.isEditor || Debug.isDebugBuild)
                && System.Environment.GetEnvironmentVariable(RealAudioEnvVar) == "0")
            {
                config.RealAudio = false;
                Debug.Log("Emberfall audio: " + RealAudioEnvVar + "=0, real audio is OFF for this run (the original tones play).");
            }
            Init(config);
        }

        /// <summary>Set to 0 to force the rollback for one run. See Init.</summary>
        public const string RealAudioEnvVar = "EMBERFALL_REAL_AUDIO";

        /// <summary>Test and capture seam: start from a config the caller built.</summary>
        public void Init(AudioCueConfig config)
        {
            _config = config ?? new AudioCueConfig();
            for (int i = 0; i < _config.Warnings.Count; i++) Debug.LogWarning("Emberfall audio: " + _config.Warnings[i]);

            AudioPrefs.DefaultMusic = Mathf.RoundToInt(_config.DefaultMusicVolume * 100f);
            AudioPrefs.DefaultSfx = Mathf.RoundToInt(_config.DefaultSfxVolume * 100f);
            AudioPrefs.DefaultUi = Mathf.RoundToInt(_config.DefaultUiVolume * 100f);

            if (!Enabled) return;

            for (int i = 0; i < _config.SfxVoices; i++) _sfxVoices.Add(NewVoice("Sfx" + i));
            for (int i = 0; i < _config.UiVoices; i++) _uiVoices.Add(NewVoice("Ui" + i));
            for (int i = 0; i < _music.Length; i++)
            {
                Fader f = new Fader();
                f.Source = NewSource("Music" + i);
                f.Source.loop = true;
                f.Channel = AudioChannel.Music;
                _music[i] = f;
            }
            StartCoroutine(PreloadClips());
        }

        // ------------------------------------------------------------------ cues

        /// <summary>
        /// Plays a cue. Returns true when a sound was started, false when it was silent, muted,
        /// inside its cooldown, dropped for a full pool, or had no usable file. Every path that
        /// is a defect (an id audio.json does not name, a file that will not load) is written to
        /// AudioCueLog, so the play proof can find it.
        /// </summary>
        public bool Play(string cueId, float volumeScale)
        {
            if (!Enabled || string.IsNullOrEmpty(cueId)) return false;
            AudioCueDef def = _config.Cue(cueId);
            if (def == null)
            {
                AudioCueLog.Record("UNKNOWN", cueId, "");
                return false;
            }

            // A looping music cue is a track, not a one-shot: it takes over the music channel.
            if (def.Channel == AudioChannel.Music && def.Loop) { SetMusic(cueId); return false; }

            float now = Time.unscaledTime;
            if (!_cooldowns.TryFire(cueId, now, def.Cooldown)) return false;

            if (def.StopMusic >= 0f) StopMusic(def.StopMusic);

            if (def.IsSilent)
            {
                AudioCueLog.Record(def.Channel == AudioChannel.Music ? "SILENT" : "NOFILE", cueId, "no files in audio.json");
                return false;
            }

            float channelVolume = ChannelVolume(def.Channel);
            if (channelVolume <= 0.001f)
            {
                AudioCueLog.Record("MUTED", cueId, "");
                return false;
            }

            string file = Pick(cueId, def.Files);
            AudioClip clip = Clip(file);
            if (clip == null)
            {
                AudioCueLog.Record("NOFILE", cueId, "file=" + file);
                return false;
            }

            float pitchJitter = def.PitchJitter >= 0f ? def.PitchJitter : _config.DefaultPitchJitter;
            float volumeJitter = def.VolumeJitter >= 0f ? def.VolumeJitter : _config.DefaultVolumeJitter;
            float pitch = def.Pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
            float volume = def.Volume * volumeScale * (1f - Random.Range(0f, volumeJitter)) * channelVolume;

            if (!StartVoice(def.Channel, cueId, clip, volume, pitch, def.Priority, def.Trim))
            {
                Dropped++;
                AudioCueLog.Record("DROP", cueId, "pool full");
                return false;
            }

            // layers= counts the EXTRA sources a cue started under its main one (battle.hit carries one:
            // layers=1 is two sounds playing, the stone hit and the arcane snap). sources= is the sum.
            int layersStarted = 0;
            for (int l = 0; l < def.Layers.Count; l++)
            {
                AudioLayerDef layer = def.Layers[l];
                if (layer.Files.Count == 0) continue;
                AudioClip layerClip = Clip(Pick(cueId + "#" + l, layer.Files));
                if (layerClip == null) { AudioCueLog.Record("NOFILE", cueId, "layer " + l); continue; }
                // Each layer gets its own pitch, so two layers never sit at the same detune.
                float layerPitch = layer.Pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
                float layerVolume = def.Volume * layer.Volume * volumeScale
                                    * (1f - Random.Range(0f, volumeJitter)) * channelVolume;
                if (layer.Delay > 0.001f)
                {
                    Pending p = new Pending();
                    p.At = now + layer.Delay;
                    p.Channel = def.Channel;
                    p.Cue = cueId;
                    p.Clip = layerClip;
                    p.Volume = layerVolume;
                    p.Pitch = layerPitch;
                    p.Trim = layer.Trim;
                    p.Priority = def.Priority;
                    _pending.Add(p);
                    layersStarted++;
                }
                else if (StartVoice(def.Channel, cueId, layerClip, layerVolume, layerPitch, def.Priority, layer.Trim))
                {
                    layersStarted++;
                }
            }

            AudioCueLog.Record("PLAY", cueId, "ch=" + ChannelName(def.Channel) + " file=" + file
                               + " vol=" + volume.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                               + " pitch=" + pitch.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                               + " layers=" + layersStarted + " sources=" + (1 + layersStarted));
            return true;
        }

        public bool Play(string cueId) { return Play(cueId, 1f); }

        private bool StartVoice(AudioChannel channel, string cueId, AudioClip clip, float volume, float pitch, int priority, float trim)
        {
            List<Voice> pool = channel == AudioChannel.Ui ? _uiVoices : _sfxVoices;
            if (pool.Count == 0) return false;
            float now = Time.unscaledTime;

            int chosen = AudioVoicePool.Choose(pool, cueId, priority, _config.PerCueVoices, now);
            if (chosen < 0) return false;
            Voice use = pool[chosen];

            if (use.Source.clip != null && now < use.BusyUntil) use.Source.Stop();
            use.Source.clip = clip;
            use.Volume = Mathf.Clamp01(volume);
            use.Source.volume = use.Volume;
            use.Source.pitch = pitch;
            use.Source.loop = false;
            use.Source.Play();
            use.Cue = cueId;
            use.Priority = priority;
            use.Started = now;
            float length = clip.length / Mathf.Max(0.1f, pitch);
            bool trimmed = trim > 0f && trim < length;
            use.StopAt = trimmed ? now + trim : 0f;
            use.BusyUntil = now + (trimmed ? trim : length) + 0.05f;
            return true;
        }

        // ------------------------------------------------------------------ music

        /// <summary>
        /// Crossfades to a music cue. The same cue again does nothing. A cue with no files fades
        /// the current track out and plays nothing: that is what every music cue does while
        /// audio-in holds no music, and it is logged as SILENT so the proof can show the cue fired.
        /// </summary>
        public void SetMusic(string cueId)
        {
            if (!Enabled || string.IsNullOrEmpty(cueId) || cueId == _musicCue) return;
            AudioCueDef def = _config.Cue(cueId);
            if (def == null)
            {
                AudioCueLog.Record("UNKNOWN", cueId, "music");
                return;
            }
            _musicCue = cueId;

            float fade = def.Fade >= 0f ? def.Fade : _config.MusicFade;
            for (int i = 0; i < _music.Length; i++) BeginFade(_music[i], 0f, fade);

            if (def.IsSilent)
            {
                AudioCueLog.Record("SILENT", cueId, "music, no files in audio.json");
                return;
            }

            string file = Pick(cueId, def.Files);
            AudioClip clip = Clip(file);
            if (clip == null)
            {
                AudioCueLog.Record("NOFILE", cueId, "music file=" + file);
                return;
            }

            Fader next = _music[0].Level <= _music[1].Level ? _music[0] : _music[1];
            next.Source.Stop();
            next.Source.clip = clip;
            next.Source.loop = true;
            next.Source.pitch = 1f;
            next.Cue = cueId;
            next.Base = def.Volume;
            next.Level = 0f;
            next.Source.volume = 0f;
            next.Source.Play();
            BeginFade(next, 1f, fade);
            AudioCueLog.Record("MUSIC", cueId, "file=" + file + " fade=" + fade.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Fades the music out and forgets the cue, so asking for it again plays it.</summary>
        public void StopMusic(float fadeSeconds)
        {
            if (!Enabled) return;
            _musicCue = "";
            for (int i = 0; i < _music.Length; i++) BeginFade(_music[i], 0f, fadeSeconds);
        }

        /// <summary>The ScreenId name just shown. A screen with no entry in musicByScreen leaves
        /// the music as it is.</summary>
        public void ScreenShown(string screenName)
        {
            if (!Enabled) return;
            // A safety net: no loop (the summon charge) outlives the screen that started it, whatever
            // way that screen was left.
            foreach (KeyValuePair<string, Fader> pair in _loops)
                if (pair.Value.Target > 0.5f) StopLoop(pair.Key, 0.1f);
            string cue;
            if (_config.MusicByScreen.TryGetValue(screenName, out cue)) SetMusic(cue);
        }

        /// <summary>The battle screen's music, by kind: campaign, boss, recital, proving, prologue,
        /// guild. Looked up as "Battle.kind" in musicByScreen, then "Battle".</summary>
        public void EnterBattle(string kind)
        {
            if (!Enabled) return;
            string cue;
            if (_config.MusicByScreen.TryGetValue("Battle." + kind, out cue)
                || _config.MusicByScreen.TryGetValue("Battle", out cue)) SetMusic(cue);
        }

        private void BeginFade(Fader f, float target, float seconds)
        {
            f.Target = target;
            if (seconds <= 0.01f)
            {
                f.Level = target;
                f.Rate = 1000f;
            }
            else
            {
                f.Rate = 1f / seconds;
            }
        }

        // ------------------------------------------------------------------ loops

        /// <summary>Starts a looping sfx cue (the summon charge). One loop per cue id; asking again
        /// while it plays does nothing. Stop it with StopLoop.</summary>
        public void StartLoop(string cueId)
        {
            if (!Enabled || string.IsNullOrEmpty(cueId)) return;
            AudioCueDef def = _config.Cue(cueId);
            if (def == null) { AudioCueLog.Record("UNKNOWN", cueId, "loop"); return; }
            Fader f;
            if (_loops.TryGetValue(cueId, out f) && f.Target > 0.5f) return;
            if (def.IsSilent) { AudioCueLog.Record("NOFILE", cueId, "loop, no files in audio.json"); return; }
            string file = Pick(cueId, def.Files);
            AudioClip clip = Clip(file);
            if (clip == null) { AudioCueLog.Record("NOFILE", cueId, "loop file=" + file); return; }

            if (f == null)
            {
                f = new Fader();
                f.Source = NewSource("Loop " + cueId);
                f.Channel = def.Channel;
                _loops[cueId] = f;
            }
            f.Cue = cueId;
            f.Base = def.Volume;
            f.Channel = def.Channel;
            f.Source.clip = clip;
            f.Source.loop = true;
            f.Source.pitch = 1f;
            f.Level = 0f;
            f.Source.volume = 0f;
            f.Source.Play();
            BeginFade(f, 1f, def.Fade >= 0f ? def.Fade : 0.15f);
            AudioCueLog.Record("LOOP+", cueId, "file=" + file);
        }

        public void StopLoop(string cueId, float fadeSeconds)
        {
            if (!Enabled || string.IsNullOrEmpty(cueId)) return;
            Fader f;
            if (!_loops.TryGetValue(cueId, out f) || f.Target <= 0.5f) return;
            BeginFade(f, 0f, fadeSeconds);
            AudioCueLog.Record("LOOP-", cueId, "");
        }

        // ------------------------------------------------------------------ frame

        private void Update()
        {
            if (!Enabled) return;
            float dt = Time.unscaledDeltaTime;

            for (int i = 0; i < _music.Length; i++) StepFader(_music[i], dt, AudioPrefs.Music);
            foreach (KeyValuePair<string, Fader> pair in _loops)
                StepFader(pair.Value, dt, pair.Value.Channel == AudioChannel.Ui ? AudioPrefs.Ui : AudioPrefs.Sfx);

            if (_pending.Count > 0)
            {
                float now = Time.unscaledTime;
                for (int i = _pending.Count - 1; i >= 0; i--)
                {
                    Pending p = _pending[i];
                    if (now < p.At) continue;
                    _pending.RemoveAt(i);
                    StartVoice(p.Channel, p.Cue, p.Clip, p.Volume, p.Pitch, p.Priority, p.Trim);
                }
            }

            StepTrims(_sfxVoices);
            StepTrims(_uiVoices);
        }

        /// <summary>The tail of a trimmed voice: a short linear fade, then stop.</summary>
        private static void StepTrims(List<Voice> pool)
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < pool.Count; i++)
            {
                Voice v = pool[i];
                if (v.StopAt <= 0f) continue;
                if (now >= v.StopAt)
                {
                    v.Source.Stop();
                    v.StopAt = 0f;
                    v.BusyUntil = 0f;
                }
                else if (now > v.StopAt - TrimFade)
                {
                    v.Source.volume = v.Volume * Mathf.Clamp01((v.StopAt - now) / TrimFade);
                }
            }
        }

        private static void StepFader(Fader f, float dt, float channelVolume)
        {
            if (f == null || f.Source == null) return;
            f.Level = Mathf.MoveTowards(f.Level, f.Target, f.Rate * dt);
            f.Source.volume = Mathf.Clamp01(f.Level * f.Base * channelVolume);
            if (f.Target <= 0f && f.Level <= 0f && f.Source.clip != null)
            {
                f.Source.Stop();
                f.Source.clip = null;
            }
        }

        private void OnDestroy()
        {
            AudioCueLog.WriteSummary();
        }

        // ------------------------------------------------------------------ helpers

        private static float ChannelVolume(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Music: return AudioPrefs.Music;
                case AudioChannel.Ui: return AudioPrefs.Ui;
            }
            return AudioPrefs.Sfx;
        }

        private static string ChannelName(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Music: return "music";
                case AudioChannel.Ui: return "ui";
            }
            return "sfx";
        }

        /// <summary>One of the files, never the same one twice running when there is a choice.</summary>
        private string Pick(string key, List<string> files)
        {
            if (files.Count == 1) return files[0];
            int last;
            _lastPick.TryGetValue(key, out last);
            int index = Random.Range(0, files.Count);
            if (index == last && _lastPick.ContainsKey(key)) index = (index + 1) % files.Count;
            _lastPick[key] = index;
            return files[index];
        }

        private AudioClip Clip(string path)
        {
            AudioClip clip;
            if (_clips.TryGetValue(path, out clip)) return clip;
            if (_missing.Contains(path)) return null;
            clip = Resources.Load<AudioClip>(path);
            if (clip == null)
            {
                _missing.Add(path);
                Debug.LogWarning("Emberfall audio: no clip at Resources/" + path);
                return null;
            }
            _clips[path] = clip;
            return clip;
        }

        private AudioSource NewSource(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }

        private Voice NewVoice(string name)
        {
            Voice v = new Voice();
            v.Source = NewSource(name);
            return v;
        }

        /// <summary>Loads every sfx and ui clip a few at a time after startup, so the first hit of
        /// a fight does not wait on the disk.</summary>
        private IEnumerator PreloadClips()
        {
            int sinceYield = 0;
            List<string> files = _config.AllFiles();
            for (int i = 0; i < files.Count; i++)
            {
                if (files[i].StartsWith("music/")) continue;
                Clip(files[i]);
                if (++sinceYield >= 6) { sinceYield = 0; yield return null; }
            }
        }
    }
}
