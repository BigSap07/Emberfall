using System;
using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Data
{
    /// <summary>Which mixer group a cue plays on. Music has two sources and a crossfade; sfx and ui
    /// are voice pools with their own volume slider each.</summary>
    public enum AudioChannel
    {
        Sfx,
        Ui,
        Music
    }

    /// <summary>One extra source played with a cue: an impact under a cast, a tail over a hit.</summary>
    public sealed class AudioLayerDef
    {
        /// <summary>Resource paths under Audio/Resources, no extension. One is picked at random.</summary>
        public readonly List<string> Files = new List<string>();
        public float Volume = 1f;
        /// <summary>Seconds after the cue fires before this layer starts.</summary>
        public float Delay;
        /// <summary>Cut the layer to this many seconds with a short fade. 0 plays the whole file.</summary>
        public float Trim;
        /// <summary>Base pitch multiplier before the random jitter.</summary>
        public float Pitch = 1f;
    }

    /// <summary>One game event and the sounds it plays.</summary>
    public sealed class AudioCueDef
    {
        public string Id = "";
        public AudioChannel Channel = AudioChannel.Sfx;
        /// <summary>Resource paths under Audio/Resources, no extension ("sfx/Pack/File"). One is
        /// picked at random per play. Empty means the cue is silent; for a music cue that is the
        /// designed state while no music has been supplied.</summary>
        public readonly List<string> Files = new List<string>();
        public readonly List<AudioLayerDef> Layers = new List<AudioLayerDef>();
        public float Volume = 1f;
        /// <summary>Plus or minus this much random pitch per play (0.04 is 4 percent). Negative
        /// means "use the file default".</summary>
        public float PitchJitter = -1f;
        public float VolumeJitter = -1f;
        /// <summary>Minimum seconds between two plays of this cue. Absorbs a double fire and an
        /// area hit that lands five Damage events in one frame.</summary>
        public float Cooldown;
        /// <summary>Higher wins a voice when the pool is full.</summary>
        public int Priority = 1;
        /// <summary>Cut the sound to this many seconds with a short fade. 0 plays the whole file.
        /// The packs' spell tails run two to seven seconds; a fight turn is under one.</summary>
        public float Trim;
        /// <summary>Base pitch multiplier before the random jitter. Lets two cues share a family of
        /// files and still sound like two things (a refusal is the tap, lower).</summary>
        public float Pitch = 1f;
        /// <summary>Music only: seconds of crossfade into this cue.</summary>
        public float Fade = -1f;
        public bool Loop;
        /// <summary>Music only: a file played once before the loop starts, then the loop follows
        /// without a gap. Empty means the cue starts straight in the loop.</summary>
        public string Intro = "";
        /// <summary>Negative leaves the music alone. Zero or more fades the music out over that many
        /// seconds when this cue fires: the victory and defeat stingers end the battle track, whether
        /// or not they have a file of their own.</summary>
        public float StopMusic = -1f;
        /// <summary>Why this cue sounds like it does, and what was reused where nothing fit.</summary>
        public string Note = "";

        public bool IsSilent { get { return Files.Count == 0; } }
    }

    /// <summary>
    /// audio.json: which file each game event plays, and the audio feature flag.
    ///
    /// NOT HASHED INTO ContentDatabase.ContentVersion, for orders.json's reason: no fight reads it.
    /// It is loaded by AudioService straight from Resources and parsed here, so the parser stays
    /// engine free and the DevHarness can prove every entry resolves to a file that exists
    /// (AudioChecks).
    /// </summary>
    public sealed class AudioCueConfig
    {
        public readonly Dictionary<string, AudioCueDef> Cues = new Dictionary<string, AudioCueDef>();
        /// <summary>Cue ids in file order, for stable reports.</summary>
        public readonly List<string> Order = new List<string>();
        /// <summary>ScreenId name to music cue id. A screen with no entry keeps whatever plays.</summary>
        public readonly Dictionary<string, string> MusicByScreen = new Dictionary<string, string>();
        public readonly List<string> Warnings = new List<string>();

        /// <summary>THE ROLLBACK FLAG. False plays the synthesised sounds the game shipped with.</summary>
        public bool RealAudio = true;

        public float DefaultPitchJitter = 0.04f;
        public float DefaultVolumeJitter = 0.10f;
        public float MusicFade = 1.2f;
        public int SfxVoices = 10;
        public int UiVoices = 4;
        /// <summary>Most plays of one cue that may be sounding at once (a big turn must not stack).</summary>
        public int PerCueVoices = 3;
        /// <summary>
        /// THE MUSIC FLAG (R14-MUSIC). True: a music cue plays its files, tracks crossfade, a track
        /// the next screen shares carries on, and the music ducks under a skill or the victory. False:
        /// every music cue is silent, exactly as it was before any music shipped (EffectiveFiles is
        /// empty, so each one logs SILENT). A missing key reads as true, because an audio.json
        /// written before R14 lists no music files, so true plays nothing there either.
        /// </summary>
        public bool Music = true;

        /// <summary>How far the music steps back for a skill moment or the victory, in dB.</summary>
        public float DuckDb = 6f;
        /// <summary>Seconds to reach the ducked level.</summary>
        public float DuckIn = 0.08f;
        /// <summary>Seconds to come back to full level once the moment is over.</summary>
        public float DuckOut = 0.5f;
        /// <summary>Seconds the music stays ducked for the victory moment. A skill moment holds for
        /// the length of its backdrop dim, which the caller knows.</summary>
        public float DuckVictoryHold = 0.8f;

        public float DefaultMusicVolume = 0.6f;
        public float DefaultSfxVolume = 0.8f;
        public float DefaultUiVolume = 0.7f;

        public bool IsUsable { get { return Cues.Count > 0; } }

        /// <summary>True when sounds come from audio.json. False means the old synthesised set: the
        /// flag is off, or the file never loaded.</summary>
        public bool UsesRealAudio { get { return RealAudio && IsUsable; } }

        public AudioCueDef Cue(string id)
        {
            AudioCueDef d;
            if (string.IsNullOrEmpty(id) || !Cues.TryGetValue(id, out d)) return null;
            return d;
        }

        private static readonly List<string> NoFiles = new List<string>();

        /// <summary>
        /// The files a cue may play right now. A music cue with the music flag off has none, which
        /// is what makes the rollback exact: AudioService reads this and never the cue's own list,
        /// so every music cue is silent and logs SILENT. The returned list must not be changed.
        /// </summary>
        public List<string> EffectiveFiles(AudioCueDef cue)
        {
            if (cue == null) return NoFiles;
            if (cue.Channel == AudioChannel.Music && !Music) return NoFiles;
            return cue.Files;
        }

        /// <summary>True when the cue plays nothing right now: no files, or a music cue under the
        /// music flag off.</summary>
        public bool IsSilentNow(AudioCueDef cue)
        {
            return EffectiveFiles(cue).Count == 0;
        }

        /// <summary>
        /// Whether firing this cue ends the music (the victory and defeat stingers). With the music flag
        /// on, a stinger that plays no file leaves the battle track under the result screen, so the
        /// music only ducks and comes back; a stinger with a file ends the track as before. With the
        /// flag off nothing sounds, so the old rule stands and the cue's stopMusic applies as it always did.
        /// </summary>
        public bool StopsMusic(AudioCueDef cue)
        {
            if (cue == null || cue.StopMusic < 0f) return false;
            return !Music || !IsSilentNow(cue);
        }

        /// <summary>The intro file of a music cue, or empty. Only a cue that plays has one.</summary>
        public string EffectiveIntro(AudioCueDef cue)
        {
            if (cue == null || cue.Channel != AudioChannel.Music || IsSilentNow(cue)) return "";
            return cue.Intro;
        }

        /// <summary>
        /// True when the track already playing is one the cue would start. The comparison is on the
        /// FILE, never on the cue id: music.hub and music.map share one file, so going from the hub
        /// to the map carries on instead of restarting the track.
        /// </summary>
        public bool CarriesOn(string playingFile, AudioCueDef next)
        {
            if (string.IsNullOrEmpty(playingFile)) return false;
            List<string> files = EffectiveFiles(next);
            for (int i = 0; i < files.Count; i++) if (files[i] == playingFile) return true;
            return false;
        }

        /// <summary>The linear gain the music is ducked to (6 dB is 0.501).</summary>
        public float DuckGain
        {
            get { return (float)Math.Pow(10.0, -DuckDb / 20.0); }
        }

        /// <summary>The music cue for a screen name, or null when the screen leaves the music alone.</summary>
        public string ScreenMusicCue(string screenName)
        {
            string cue;
            return MusicByScreen.TryGetValue(screenName, out cue) ? cue : null;
        }

        /// <summary>
        /// The music cue for a battle kind. The key "Battle.kind" is looked up first, then the key
        /// with its last dot segment taken off, down to "Battle": "Battle.campaign.ch3" falls back to
        /// "Battle.campaign" and then to "Battle". A chapter with no entry of its own therefore plays
        /// the campaign track. Null when nothing matches.
        /// </summary>
        public string BattleMusicCue(string kind)
        {
            string key = "Battle." + kind;
            while (true)
            {
                string cue;
                if (MusicByScreen.TryGetValue(key, out cue)) return cue;
                int dot = key.LastIndexOf('.');
                if (dot < 0) return null;
                key = key.Substring(0, dot);
            }
        }

        /// <summary>
        /// The battle kind of a campaign stage, as the suffix of a "Battle." key. A wall (the
        /// chapter's miniboss stage) and a chapter finale (its boss stage) are "boss". Any other
        /// stage is "campaign.chN" for its chapter, which falls back to "campaign". Reads the stage's
        /// flags only; it decides nothing about the fight.
        /// </summary>
        public static string CampaignBattleKind(StageDef stage)
        {
            if (stage == null) return "campaign";
            if (stage.IsBoss || stage.IsMiniboss) return "boss";
            return stage.ChapterNumber > 0 ? "campaign.ch" + stage.ChapterNumber : "campaign";
        }

        public static AudioCueConfig FromJson(string text)
        {
            AudioCueConfig c = new AudioCueConfig();
            if (string.IsNullOrEmpty(text)) return c;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { c.Warnings.Add("audio.json: " + err); return c; }

            c.RealAudio = root.GetBool("realAudio", true);
            c.Music = root.GetBool("music", true);

            JsonValue d = root["defaults"];
            c.DefaultPitchJitter = Clamp(d.GetFloat("pitchJitter", c.DefaultPitchJitter), 0f, 0.5f);
            c.DefaultVolumeJitter = Clamp(d.GetFloat("volumeJitter", c.DefaultVolumeJitter), 0f, 0.9f);
            c.MusicFade = Clamp(d.GetFloat("musicFade", c.MusicFade), 0f, 10f);
            c.DuckDb = Clamp(d.GetFloat("duckDb", c.DuckDb), 0f, 24f);
            c.DuckIn = Clamp(d.GetFloat("duckIn", c.DuckIn), 0f, 2f);
            c.DuckOut = Clamp(d.GetFloat("duckOut", c.DuckOut), 0f, 5f);
            c.DuckVictoryHold = Clamp(d.GetFloat("duckVictoryHold", c.DuckVictoryHold), 0f, 5f);
            c.SfxVoices = Math.Max(1, Math.Min(32, d.GetInt("sfxVoices", c.SfxVoices)));
            c.UiVoices = Math.Max(1, Math.Min(16, d.GetInt("uiVoices", c.UiVoices)));
            c.PerCueVoices = Math.Max(1, Math.Min(16, d.GetInt("perCueVoices", c.PerCueVoices)));
            JsonValue v = root["volumes"];
            c.DefaultMusicVolume = Clamp(v.GetFloat("music", c.DefaultMusicVolume), 0f, 1f);
            c.DefaultSfxVolume = Clamp(v.GetFloat("sfx", c.DefaultSfxVolume), 0f, 1f);
            c.DefaultUiVolume = Clamp(v.GetFloat("ui", c.DefaultUiVolume), 0f, 1f);

            List<JsonValue> cues = root.GetList("cues");
            for (int i = 0; i < cues.Count; i++)
            {
                AudioCueDef cue = new AudioCueDef();
                cue.Id = cues[i].GetString("id", "");
                if (cue.Id.Length == 0 || c.Cues.ContainsKey(cue.Id))
                {
                    c.Warnings.Add("audio.json: a cue has no id or repeats one ('" + cue.Id + "'); the first is kept");
                    continue;
                }
                string channel = cues[i].GetString("channel", "sfx");
                if (!TryChannel(channel, out cue.Channel))
                {
                    c.Warnings.Add("audio.json: cue '" + cue.Id + "' names an unknown channel '" + channel + "'");
                    continue;
                }
                ReadFiles(cues[i].GetList("files"), cue.Files);
                cue.Volume = Clamp(cues[i].GetFloat("volume", 1f), 0f, 2f);
                cue.PitchJitter = cues[i].GetFloat("pitchJitter", -1f);
                cue.VolumeJitter = cues[i].GetFloat("volumeJitter", -1f);
                cue.Cooldown = Math.Max(0f, cues[i].GetFloat("cooldown", 0f));
                cue.Priority = cues[i].GetInt("priority", 1);
                cue.Trim = Math.Max(0f, cues[i].GetFloat("trim", 0f));
                cue.Pitch = Clamp(cues[i].GetFloat("pitch", 1f), 0.5f, 2f);
                cue.Fade = cues[i].GetFloat("fade", -1f);
                cue.Loop = cues[i].GetBool("loop", false);
                cue.Intro = cues[i].GetString("intro", "");
                cue.StopMusic = cues[i].GetFloat("stopMusic", -1f);
                cue.Note = cues[i].GetString("note", "");

                List<JsonValue> layers = cues[i].GetList("layers");
                for (int l = 0; l < layers.Count; l++)
                {
                    AudioLayerDef layer = new AudioLayerDef();
                    ReadFiles(layers[l].GetList("files"), layer.Files);
                    layer.Volume = Clamp(layers[l].GetFloat("volume", 1f), 0f, 2f);
                    layer.Delay = Math.Max(0f, layers[l].GetFloat("delay", 0f));
                    layer.Trim = Math.Max(0f, layers[l].GetFloat("trim", 0f));
                    layer.Pitch = Clamp(layers[l].GetFloat("pitch", 1f), 0.5f, 2f);
                    cue.Layers.Add(layer);
                }
                c.Cues[cue.Id] = cue;
                c.Order.Add(cue.Id);
            }

            JsonValue screens = root["musicByScreen"];
            if (screens.IsObject)
            {
                foreach (string key in screens.Keys) c.MusicByScreen[key] = screens[key].AsString("");
            }
            return c;
        }

        public static bool TryChannel(string text, out AudioChannel channel)
        {
            switch (text)
            {
                case "sfx": channel = AudioChannel.Sfx; return true;
                case "ui": channel = AudioChannel.Ui; return true;
                case "music": channel = AudioChannel.Music; return true;
            }
            channel = AudioChannel.Sfx;
            return false;
        }

        /// <summary>Every file path a cue or layer names, for the resolve check and the importer.</summary>
        public List<string> AllFiles()
        {
            List<string> all = new List<string>();
            for (int i = 0; i < Order.Count; i++)
            {
                AudioCueDef cue = Cues[Order[i]];
                for (int f = 0; f < cue.Files.Count; f++) if (!all.Contains(cue.Files[f])) all.Add(cue.Files[f]);
                if (cue.Intro.Length > 0 && !all.Contains(cue.Intro)) all.Add(cue.Intro);
                for (int l = 0; l < cue.Layers.Count; l++)
                    for (int f = 0; f < cue.Layers[l].Files.Count; f++)
                        if (!all.Contains(cue.Layers[l].Files[f])) all.Add(cue.Layers[l].Files[f]);
            }
            return all;
        }

        private static void ReadFiles(List<JsonValue> list, List<string> into)
        {
            for (int i = 0; i < list.Count; i++)
            {
                string path = list[i].AsString("");
                if (path.Length > 0) into.Add(path);
            }
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }
    }

    /// <summary>
    /// The duck envelope: the music steps back for a moment and returns. Engine free so the harness
    /// can run it. Trigger it with a hold time and call Step each frame with the frame time; the
    /// return value is the multiplier for the music (1 is untouched). It falls to
    /// AudioCueConfig.DuckGain over DuckIn, stays there for the hold, and rises back over DuckOut.
    /// With the music flag off it never moves.
    /// </summary>
    public sealed class MusicDuck
    {
        private float _depth;      // 0 untouched, 1 fully ducked
        private float _holdLeft;

        /// <summary>0 when the music is untouched, 1 when fully ducked.</summary>
        public float Depth { get { return _depth; } }

        /// <summary>Hold the duck for at least this many seconds from now.</summary>
        public void Trigger(AudioCueConfig config, float holdSeconds)
        {
            if (config == null || !config.Music || config.DuckDb <= 0f) return;
            float hold = holdSeconds > 0.001f ? holdSeconds : 0.001f;
            if (hold > _holdLeft) _holdLeft = hold;
        }

        /// <summary>Advances by dt seconds and returns the gain to apply to the music.</summary>
        public float Step(AudioCueConfig config, float dt)
        {
            if (config == null || !config.Music || config.DuckDb <= 0f)
            {
                _depth = 0f;
                _holdLeft = 0f;
                return 1f;
            }
            if (dt < 0f) dt = 0f;
            if (_holdLeft > 0f)
            {
                _holdLeft = Math.Max(0f, _holdLeft - dt);
                _depth = config.DuckIn <= 0.0001f ? 1f : Math.Min(1f, _depth + dt / config.DuckIn);
            }
            else
            {
                _depth = config.DuckOut <= 0.0001f ? 0f : Math.Max(0f, _depth - dt / config.DuckOut);
            }
            return 1f + (config.DuckGain - 1f) * _depth;
        }
    }

    /// <summary>
    /// Reads the length of a PCM wav from its header, with no engine. The resolve check uses it to
    /// prove a file is a real wav and how long it is; the importer uses the same number to decide
    /// which clips are short enough to force to mono.
    /// </summary>
    public static class WavInfo
    {
        /// <summary>Seconds of audio in the wav bytes, or -1 when they are not a readable RIFF wav.</summary>
        public static double Seconds(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 44) return -1;
            if (bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F') return -1;
            if (bytes[8] != 'W' || bytes[9] != 'A' || bytes[10] != 'V' || bytes[11] != 'E') return -1;
            int pos = 12;
            int byteRate = 0;
            long dataBytes = -1;
            while (pos + 8 <= bytes.Length)
            {
                string id = "" + (char)bytes[pos] + (char)bytes[pos + 1] + (char)bytes[pos + 2] + (char)bytes[pos + 3];
                long size = BitConverter.ToUInt32(bytes, pos + 4);
                if (id == "fmt " && pos + 8 + 12 <= bytes.Length) byteRate = BitConverter.ToInt32(bytes, pos + 16);
                if (id == "data") { dataBytes = size; break; }
                pos += 8 + (int)size + (int)(size & 1);
            }
            if (byteRate <= 0 || dataBytes < 0) return -1;
            // A data chunk whose size field overruns the file (a streamed wav) is capped at the file.
            long remaining = bytes.Length - (pos + 8);
            if (dataBytes > remaining) dataBytes = remaining;
            return dataBytes / (double)byteRate;
        }

        /// <summary>What a wav's header says: format, channels, sample rate, bit depth, seconds.</summary>
        public sealed class Header
        {
            public bool Ok;
            /// <summary>1 is integer PCM.</summary>
            public int Format;
            public int Channels;
            public int SampleRate;
            public int Bits;
            public double Seconds;
        }

        /// <summary>
        /// Reads the fmt and data chunks of a wav from its first bytes, so a long music file need not
        /// be read whole. fileLength is the size on disk, which caps a data chunk that overruns it.
        /// Ok is false when the bytes are not a RIFF wav with both chunks inside them.
        /// </summary>
        public static Header ReadHeader(byte[] head, long fileLength)
        {
            Header h = new Header();
            if (head == null || head.Length < 44) return h;
            if (head[0] != 'R' || head[1] != 'I' || head[2] != 'F' || head[3] != 'F') return h;
            if (head[8] != 'W' || head[9] != 'A' || head[10] != 'V' || head[11] != 'E') return h;
            long pos = 12;
            int byteRate = 0;
            bool haveFmt = false;
            while (pos + 8 <= head.Length)
            {
                string id = "" + (char)head[pos] + (char)head[pos + 1] + (char)head[pos + 2] + (char)head[pos + 3];
                long size = BitConverter.ToUInt32(head, (int)pos + 4);
                if (id == "fmt " && pos + 8 + 16 <= head.Length)
                {
                    h.Format = BitConverter.ToUInt16(head, (int)pos + 8);
                    h.Channels = BitConverter.ToUInt16(head, (int)pos + 10);
                    h.SampleRate = BitConverter.ToInt32(head, (int)pos + 12);
                    byteRate = BitConverter.ToInt32(head, (int)pos + 16);
                    h.Bits = BitConverter.ToUInt16(head, (int)pos + 22);
                    haveFmt = true;
                }
                if (id == "data")
                {
                    long remaining = fileLength - (pos + 8);
                    long dataBytes = size > remaining ? remaining : size;
                    if (!haveFmt || byteRate <= 0 || dataBytes < 0) return h;
                    h.Seconds = dataBytes / (double)byteRate;
                    h.Ok = true;
                    return h;
                }
                pos += 8 + size + (size & 1);
            }
            return h;
        }

        /// <summary>Clips at or under this length are forced to mono on import. Longer ones (a charge
        /// loop, a reveal swell) keep their stereo width.</summary>
        public const double MonoMaxSeconds = 3.0;
    }
}
