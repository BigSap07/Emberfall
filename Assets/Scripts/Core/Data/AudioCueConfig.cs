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

        public static AudioCueConfig FromJson(string text)
        {
            AudioCueConfig c = new AudioCueConfig();
            if (string.IsNullOrEmpty(text)) return c;
            JsonValue root;
            string err;
            if (!JsonValue.TryParse(text, out root, out err)) { c.Warnings.Add("audio.json: " + err); return c; }

            c.RealAudio = root.GetBool("realAudio", true);

            JsonValue d = root["defaults"];
            c.DefaultPitchJitter = Clamp(d.GetFloat("pitchJitter", c.DefaultPitchJitter), 0f, 0.5f);
            c.DefaultVolumeJitter = Clamp(d.GetFloat("volumeJitter", c.DefaultVolumeJitter), 0f, 0.9f);
            c.MusicFade = Clamp(d.GetFloat("musicFade", c.MusicFade), 0f, 10f);
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

        /// <summary>Clips at or under this length are forced to mono on import. Longer ones (a charge
        /// loop, a reveal swell) keep their stereo width.</summary>
        public const double MonoMaxSeconds = 3.0;
    }
}
