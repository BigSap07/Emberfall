using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Emberfall.Core.Data;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE AUDIO CUE MAP (R10-AUDIO, Docs/DESIGN-AUDIO.md), as checks.
    ///
    /// The playback itself cannot run here (it needs an engine and speakers), so what is proved is
    /// everything around it that can be read: every event the prompt lists has a cue, every cue
    /// resolves to a file that exists and is a real wav, the files are byte for byte the owner's,
    /// the import settings follow the policy, audio.json is not hashed into ContentVersion, the
    /// rollback flag parses both ways, and the code only ever names cues the map holds. What the
    /// game then sounds like is for the owner's ears and the Play queue's cue log.
    /// </summary>
    internal static partial class Program
    {
        private static readonly string[] AudioAspects = { "ember", "tide", "verdant", "gale", "umbra", "lumen" };

        /// <summary>Every event the R10-AUDIO prompt lists. A cue renamed or dropped fails here.</summary>
        private static readonly string[] AudioRequiredCues =
        {
            // music, and the two stingers
            "music.hub", "music.map", "music.battle", "music.battle.ch2", "music.battle.boss", "music.summon", "music.recital",
            "music.proving", "music.guild", "music.prologue", "music.stinger.victory", "music.stinger.defeat",
            // interface
            "ui.tap", "ui.confirm", "ui.back", "ui.locked", "ui.reward", "ui.levelup", "ui.unlock",
            // summon: start, charge loop, a reveal per tier, a star ping
            "summon.start", "summon.charge", "summon.reveal.1", "summon.reveal.2", "summon.reveal.3",
            "summon.reveal.4", "summon.reveal.5", "summon.star",
            // battle
            "battle.hit", "battle.crit", "battle.death", "battle.heal", "battle.buff", "battle.debuff",
            "battle.shield", "battle.cast", "battle.dot", "battle.victory", "battle.defeat"
        };

        private static void CheckAudio(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("AUDIO (DESIGN-AUDIO.md: audio.json maps every game event to the owner's sound files)");

            string root = Path.GetFullPath(Path.Combine(contentDir, "..", "..", ".."));
            string audioRoot = Path.Combine(root, "Assets", "Audio", "Resources");
            string jsonPath = Path.Combine(contentDir, "audio.json");
            if (!File.Exists(jsonPath))
            {
                Check(false, "audio.json is where the harness expects it");
                return;
            }
            string jsonText = File.ReadAllText(jsonPath);
            AudioCueConfig cfg = AudioCueConfig.FromJson(jsonText);

            Check(cfg.IsUsable && cfg.Warnings.Count == 0,
                  "audio.json loads usable with no warnings (" + cfg.Cues.Count + " cues, " + cfg.Warnings.Count + " warnings)");
            Check(cfg.RealAudio && cfg.UsesRealAudio, "the real-audio flag is ON by default");

            CheckAudioCoverage(cfg);
            CheckAudioFiles(cfg, root, audioRoot);
            CheckAudioImport(cfg, audioRoot);
            CheckAudioNotHashed(db, contentDir);
            CheckAudioRollback(jsonText);
            CheckAudioPlayback(cfg);
            CheckAudioVoiceRules(cfg);
            CheckAudioWiring(cfg, root);
            CheckAudioCredits(root, audioRoot, contentDir, jsonText);
            CheckMusic(db, cfg, root, audioRoot, jsonText);
        }

        // ------------------------------------------------------------------ the map

        private static void CheckAudioCoverage(AudioCueConfig cfg)
        {
            List<string> required = new List<string>(AudioRequiredCues);
            for (int i = 0; i < AudioAspects.Length; i++)
            {
                required.Add("battle.cast." + AudioAspects[i]);
                required.Add("battle.impact." + AudioAspects[i]);
            }
            List<string> missing = new List<string>();
            for (int i = 0; i < required.Count; i++) if (cfg.Cue(required[i]) == null) missing.Add(required[i]);
            Check(missing.Count == 0,
                  "every event the prompt lists has a cue: " + required.Count + " required"
                  + (missing.Count > 0 ? ", MISSING " + string.Join(", ", missing.ToArray()) : ""));

            List<string> empty = new List<string>();
            int silentMusic = 0;
            List<string> musicFromSfx = new List<string>();
            foreach (string id in cfg.Order)
            {
                AudioCueDef cue = cfg.Cues[id];
                if (cue.Channel == AudioChannel.Music)
                {
                    if (cue.IsSilent) silentMusic++;
                    for (int f = 0; f < cue.Files.Count; f++)
                        if (!cue.Files[f].StartsWith("music/", StringComparison.Ordinal)) musicFromSfx.Add(id);
                }
                else if (cue.IsSilent) empty.Add(id);
            }
            Check(empty.Count == 0, "every sfx and ui cue has at least one file" + (empty.Count > 0 ? ": EMPTY " + string.Join(", ", empty.ToArray()) : ""));
            Check(musicFromSfx.Count == 0,
                  "a sound effect is never used as music: every file on a music cue is under music/ (" + silentMusic
                  + " music cues are silent: the stingers, or every one with the music flag off)" + (musicFromSfx.Count > 0 ? ": " + string.Join(", ", musicFromSfx.ToArray()) : ""));

            // The summon tiers must not all be the same sound.
            HashSet<string> tierSounds = new HashSet<string>();
            for (int s = 1; s <= 5; s++)
            {
                AudioCueDef c = cfg.Cue("summon.reveal." + s);
                if (c != null && c.Files.Count > 0) tierSounds.Add(c.Files[0]);
            }
            Check(tierSounds.Count == 5, "each summon rarity tier sounds different: " + tierSounds.Count + " distinct first files for 5 tiers");

            // The aspects must not share a cast or an impact.
            HashSet<string> casts = new HashSet<string>();
            HashSet<string> impacts = new HashSet<string>();
            for (int i = 0; i < AudioAspects.Length; i++)
            {
                AudioCueDef c = cfg.Cue("battle.cast." + AudioAspects[i]);
                AudioCueDef im = cfg.Cue("battle.impact." + AudioAspects[i]);
                if (c != null && c.Files.Count > 0) casts.Add(c.Files[0]);
                if (im != null && im.Files.Count > 0) impacts.Add(im.Files[0]);
            }
            Check(casts.Count == 6 && impacts.Count == 6,
                  "the six aspects each have their own cast and impact (" + casts.Count + " distinct casts, " + impacts.Count + " distinct impacts)");

            List<string> unlayered = new List<string>();
            List<string> layerIds = new List<string> { "battle.hit", "battle.crit" };
            for (int i = 0; i < AudioAspects.Length; i++) layerIds.Add("battle.impact." + AudioAspects[i]);
            for (int i = 0; i < layerIds.Count; i++)
            {
                AudioCueDef c = cfg.Cue(layerIds[i]);
                bool layered = false;
                if (c != null) for (int l = 0; l < c.Layers.Count; l++) if (c.Layers[l].Files.Count > 0) layered = true;
                if (!layered) unlayered.Add(layerIds[i]);
            }
            Check(unlayered.Count == 0,
                  "hit, crit and every skill impact layer a second source" + (unlayered.Count > 0 ? ": NOT LAYERED " + string.Join(", ", unlayered.ToArray()) : ""));
        }

        // ------------------------------------------------------------------ the files

        private static void CheckAudioFiles(AudioCueConfig cfg, string root, string audioRoot)
        {
            List<string> all = cfg.AllFiles();
            List<string> missing = new List<string>();
            List<string> notWav = new List<string>();
            double longest = 0;
            for (int i = 0; i < all.Count; i++)
            {
                string wav = Path.Combine(audioRoot, all[i].Replace('/', Path.DirectorySeparatorChar) + ".wav");
                string ogg = Path.Combine(audioRoot, all[i].Replace('/', Path.DirectorySeparatorChar) + ".ogg");
                // A music file is minutes of audio: CheckMusic reads its header and does not load it whole.
                if (all[i].StartsWith("music/", StringComparison.Ordinal))
                {
                    if (!File.Exists(wav) && !File.Exists(ogg)) missing.Add(all[i]);
                    continue;
                }
                if (File.Exists(wav))
                {
                    double seconds = WavInfo.Seconds(File.ReadAllBytes(wav));
                    if (seconds < 0.05) notWav.Add(all[i]);
                    if (seconds > longest) longest = seconds;
                }
                else if (!File.Exists(ogg)) missing.Add(all[i]);
            }
            Check(missing.Count == 0, "every cue entry resolves to a file that exists: " + all.Count + " files"
                  + (missing.Count > 0 ? ", MISSING " + string.Join(", ", missing.ToArray()) : ""));
            Check(notWav.Count == 0, "every wav is a readable RIFF wav with sound in it (longest " + longest.ToString("0.0") + " s)"
                  + (notWav.Count > 0 ? ": BAD " + string.Join(", ", notWav.ToArray()) : ""));

            // Nothing is copied that no cue plays, and nothing is imported twice.
            HashSet<string> used = new HashSet<string>(all);
            List<string> orphans = new List<string>();
            List<string> doubles = new List<string>();
            string sfxRoot = Path.Combine(audioRoot, "sfx");
            if (Directory.Exists(sfxRoot))
            {
                foreach (string file in Directory.GetFiles(sfxRoot, "*.*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext != ".wav" && ext != ".ogg" && ext != ".mp3") continue;
                    string rel = "sfx/" + file.Substring(sfxRoot.Length + 1).Replace('\\', '/');
                    string noExt = rel.Substring(0, rel.Length - ext.Length);
                    if (!used.Contains(noExt)) orphans.Add(rel);
                    if (ext != ".wav" && File.Exists(Path.ChangeExtension(file, ".wav"))) doubles.Add(rel);
                }
            }
            Check(orphans.Count == 0, "no copied sound is unused" + (orphans.Count > 0 ? ": " + string.Join(", ", orphans.ToArray()) : ""));
            Check(doubles.Count == 0, "where a pack ships the same sound as wav and ogg only the wav is imported");

            // Byte for byte the owner's, when the owner's folder is on this machine.
            string audioIn = Environment.GetEnvironmentVariable("EMBERFALL_AUDIO_IN");
            if (string.IsNullOrEmpty(audioIn)) audioIn = @"D:\Emberfall\audio-in";
            if (!Directory.Exists(audioIn))
            {
                Line("  [skip] the owner's audio-in folder is not on this machine; the copy comparison did not run");
                return;
            }
            List<string> different = new List<string>();
            int compared = 0;
            for (int i = 0; i < all.Count; i++)
            {
                // Music is converted from FLAC and cut into loops, so it is never a byte copy; CheckMusic
                // proves each music file is named after one of the owner's tracks instead.
                if (all[i].StartsWith("music/", StringComparison.Ordinal)) continue;
                string mine = Path.Combine(audioRoot, all[i].Replace('/', Path.DirectorySeparatorChar) + ".wav");
                string theirs = Path.Combine(audioIn, all[i].Replace('/', Path.DirectorySeparatorChar) + ".wav");
                if (!File.Exists(mine) || !File.Exists(theirs)) { different.Add(all[i]); continue; }
                byte[] a = File.ReadAllBytes(mine);
                byte[] b = File.ReadAllBytes(theirs);
                bool same = a.Length == b.Length;
                for (int k = 0; same && k < a.Length; k++) if (a[k] != b[k]) same = false;
                if (!same) different.Add(all[i]);
                compared++;
            }
            Check(different.Count == 0, "every copied sound effect is byte for byte the owner's file in audio-in (" + compared + " compared; music is checked by the MUSIC checks)"
                  + (different.Count > 0 ? ": DIFFERENT " + string.Join(", ", different.ToArray()) : ""));
        }

        // ------------------------------------------------------------------ import settings

        /// <summary>Tools/audio-import-policy.py writes these; this proves the committed .meta files
        /// say what the policy says, per file, so "import settings listed per folder" is a check
        /// and not a claim.</summary>
        private static void CheckAudioImport(AudioCueConfig cfg, string audioRoot)
        {
            int checkedFiles = 0, bad = 0, mono = 0, stereo = 0;
            List<string> examples = new List<string>();
            if (Directory.Exists(audioRoot))
            {
                foreach (string file in Directory.GetFiles(audioRoot, "*.wav", SearchOption.AllDirectories))
                {
                    string metaPath = file + ".meta";
                    string rel = file.Substring(audioRoot.Length + 1).Replace('\\', '/');
                    bool music = rel.StartsWith("music/", StringComparison.Ordinal);
                    string problem = null;
                    if (!File.Exists(metaPath)) problem = "no .meta";
                    else
                    {
                        string meta = File.ReadAllText(metaPath);
                        double seconds = WavInfo.Seconds(File.ReadAllBytes(file));
                        int expectMono = (!music && seconds <= WavInfo.MonoMaxSeconds) ? 1 : 0;
                        int loadType = music ? 2 : 0;
                        int format = music ? 1 : 2;
                        if (!meta.Contains("AudioImporter:")) problem = "not an AudioImporter";
                        else if (!meta.Contains("platformSettingOverrides:") || meta.Contains("platformSettingsOverride")) problem = "wrong platform override key (Unity 6 writes platformSettingOverrides)";
                        else if (MetaInt(meta, "loadType") != loadType) problem = "loadType " + MetaInt(meta, "loadType") + " not " + loadType;
                        else if (MetaInt(meta, "compressionFormat") != format) problem = "compressionFormat " + MetaInt(meta, "compressionFormat") + " not " + format;
                        else if (MetaInt(meta, "forceToMono") != expectMono) problem = "forceToMono " + MetaInt(meta, "forceToMono") + " not " + expectMono + " at " + seconds.ToString("0.0") + " s";
                        if (expectMono == 1) mono++; else stereo++;
                    }
                    checkedFiles++;
                    if (problem != null) { bad++; if (examples.Count < 4) examples.Add(rel + " (" + problem + ")"); }
                }
            }
            Check(checkedFiles > 0 && bad == 0,
                  "import settings follow the policy in every .meta: sfx decompress on load, clips of " + WavInfo.MonoMaxSeconds.ToString("0.0")
                  + " s or less mono (" + mono + " mono, " + stereo + " stereo, " + checkedFiles + " files), music streaming Vorbis"
                  + (bad > 0 ? "; WRONG " + bad + ": " + string.Join("; ", examples.ToArray()) : ""));
        }

        private static int MetaInt(string meta, string key)
        {
            Match m = Regex.Match(meta, @"^\s*" + Regex.Escape(key) + @":\s*(-?\d+)\s*$", RegexOptions.Multiline);
            return m.Success ? int.Parse(m.Groups[1].Value) : -999;
        }

        // ------------------------------------------------------------------ not hashed, rollback

        private static void CheckAudioNotHashed(ContentDatabase db, string contentDir)
        {
            string[] hashed = { "heroes", "enemies", "campaign", "balance", "relics", "events", "armor", "floor-conditions" };
            string[] texts = new string[hashed.Length];
            for (int i = 0; i < hashed.Length; i++)
            {
                string path = Path.Combine(contentDir, hashed[i] + ".json");
                texts[i] = File.Exists(path) ? File.ReadAllText(path) : null;
            }
            Check(ContentDatabase.VersionOf(texts) == db.ContentVersion,
                  "AUDIO.JSON IS NOT HASHED: ContentVersion is exactly the hash of the eight fight files (" + db.ContentVersion
                  + "), and audio.json is not among them");
        }

        private static void CheckAudioRollback(string jsonText)
        {
            // The music flag has its own rollback proof in CheckMusic.
            string off = jsonText.Replace("\"realAudio\": true", "\"realAudio\": false");
            AudioCueConfig on = AudioCueConfig.FromJson(jsonText);
            AudioCueConfig flagOff = AudioCueConfig.FromJson(off);
            AudioCueConfig none = AudioCueConfig.FromJson(null);
            Check(off != jsonText && on.UsesRealAudio && !flagOff.UsesRealAudio && flagOff.IsUsable && !none.UsesRealAudio,
                  "ROLLBACK: realAudio false (or no audio.json) means the original synthesised sounds play, the same map true plays the real ones");
        }

        // ------------------------------------------------------------------ playback rules

        private static void CheckAudioPlayback(AudioCueConfig cfg)
        {
            Check(Math.Abs(cfg.DefaultPitchJitter - 0.04f) < 0.0001f,
                  "every sfx and ui cue plays with a random pitch of plus or minus 4 percent by default (" + cfg.DefaultPitchJitter + ")");
            List<string> loud = new List<string>();
            foreach (string id in cfg.Order)
            {
                AudioCueDef c = cfg.Cues[id];
                if (c.Channel != AudioChannel.Music && c.PitchJitter > 0.04f) loud.Add(id);
            }
            Check(loud.Count == 0, "no cue overrides the pitch variation above 4 percent" + (loud.Count > 0 ? ": " + string.Join(", ", loud.ToArray()) : ""));
            Check(cfg.DefaultVolumeJitter > 0f && cfg.DefaultVolumeJitter <= 0.2f, "and a small volume variation (" + cfg.DefaultVolumeJitter + ")");
            Check(cfg.SfxVoices <= 12 && cfg.UiVoices <= 6 && cfg.PerCueVoices <= 8,
                  "overlap is capped: " + cfg.SfxVoices + " sfx voices, " + cfg.UiVoices + " ui voices, " + cfg.PerCueVoices + " of one cue");

            List<string> noCooldown = new List<string>();
            List<string> burst = new List<string> { "battle.hit", "battle.crit", "battle.dot", "battle.heal", "battle.buff", "battle.debuff", "battle.shield" };
            for (int i = 0; i < AudioAspects.Length; i++) { burst.Add("battle.cast." + AudioAspects[i]); burst.Add("battle.impact." + AudioAspects[i]); }
            for (int i = 0; i < burst.Count; i++)
            {
                AudioCueDef c = cfg.Cue(burst[i]);
                if (c == null || c.Cooldown <= 0f) noCooldown.Add(burst[i]);
            }
            Check(noCooldown.Count == 0, "every battle cue a big turn can repeat has a cooldown" + (noCooldown.Count > 0 ? ": NONE ON " + string.Join(", ", noCooldown.ToArray()) : ""));

            AudioCueDef charge = cfg.Cue("summon.charge");
            Check(charge != null && charge.Loop && charge.Files.Count > 0, "the summon charge is a loop");
            AudioCueDef vic = cfg.Cue("music.stinger.victory");
            AudioCueDef def = cfg.Cue("music.stinger.defeat");
            Check(vic != null && def != null && vic.StopMusic >= 0f && def.StopMusic >= 0f && !vic.Loop && !def.Loop,
                  "the victory and defeat stingers end the battle track and do not loop");
            Check(cfg.DefaultMusicVolume == 0.6f && cfg.DefaultSfxVolume == 0.8f && cfg.DefaultUiVolume == 0.7f,
                  "default volumes are music 60, sfx 80, ui 70 percent (" + cfg.DefaultMusicVolume + ", " + cfg.DefaultSfxVolume + ", " + cfg.DefaultUiVolume + ")");

            // The screen to music map: every value is a music cue.
            List<string> badTargets = new List<string>();
            foreach (KeyValuePair<string, string> pair in cfg.MusicByScreen)
            {
                AudioCueDef c = cfg.Cue(pair.Value);
                if (c == null || c.Channel != AudioChannel.Music || !c.Loop) badTargets.Add(pair.Key + "=" + pair.Value);
            }
            Check(badTargets.Count == 0 && cfg.MusicByScreen.Count > 0,
                  "musicByScreen names a looping music cue for each of its " + cfg.MusicByScreen.Count + " screens"
                  + (badTargets.Count > 0 ? ": BAD " + string.Join(", ", badTargets.ToArray()) : ""));
        }

        // ------------------------------------------------------------------ the overlap cap, executed

        /// <summary>
        /// AudioVoicePool and CueCooldowns are the rules AudioService plays by, run here on their own
        /// with the numbers audio.json holds. A worst-case turn is thrown at them: every battle cue,
        /// every 20 ms for two seconds, far faster than a fight produces events.
        /// </summary>
        private static void CheckAudioVoiceRules(AudioCueConfig cfg)
        {
            // A. one cue, hammered every 20 ms for 3 s, plays at most 8 times in any second.
            string[] hammer = { "ui.tap", "battle.hit", "battle.crit", "battle.heal", "battle.buff", "battle.debuff", "battle.shield",
                                "battle.impact.ember", "battle.cast.gale", "battle.dot", "ui.reward" };
            int worstWindow = 0;
            string worstCue = "";
            for (int c = 0; c < hammer.Length; c++)
            {
                AudioCueDef def = cfg.Cue(hammer[c]);
                CueCooldowns gaps = new CueCooldowns();
                List<float> fired = new List<float>();
                for (int step = 0; step < 150; step++)
                {
                    float t = step * 0.02f;
                    if (gaps.TryFire(hammer[c], t, def == null ? 0f : def.Cooldown)) fired.Add(t);
                }
                for (int i = 0; i < fired.Count; i++)
                {
                    int inWindow = 0;
                    for (int k = i; k < fired.Count && fired[k] - fired[i] <= 1.0f; k++) inWindow++;
                    if (inWindow > worstWindow) { worstWindow = inWindow; worstCue = hammer[c]; }
                }
            }
            Check(worstWindow <= AudioCueLog_RateLimit,
                  "a cue hammered every 20 ms for 3 seconds sounds at most " + AudioCueLog_RateLimit + " times in any second (worst " + worstWindow + ", " + worstCue + ")");

            // B. a worst-case turn never has more voices sounding than the pool holds, and no cue holds more than its share.
            List<VoiceSlot> pool = new List<VoiceSlot>();
            for (int i = 0; i < cfg.SfxVoices; i++) pool.Add(new VoiceSlot());
            string[] turn = { "battle.hit", "battle.crit", "battle.impact.ember", "battle.impact.tide", "battle.impact.gale",
                              "battle.impact.umbra", "battle.impact.lumen", "battle.impact.verdant", "battle.cast.ember", "battle.heal",
                              "battle.buff", "battle.debuff", "battle.shield", "battle.death", "battle.dot" };
            CueCooldowns turnGaps = new CueCooldowns();
            int mostSounding = 0, mostOfOne = 0, played = 0, dropped = 0, asked = 0;
            for (int step = 0; step < 100; step++)
            {
                float now = step * 0.02f;
                for (int k = 0; k < turn.Length; k++)
                {
                    AudioCueDef def = cfg.Cue(turn[k]);
                    if (def == null) continue;
                    asked++;
                    if (!turnGaps.TryFire(turn[k], now, def.Cooldown)) continue;
                    // The main file and each layer, as AudioService starts them.
                    int voices = 1 + def.Layers.Count;
                    for (int v = 0; v < voices; v++)
                    {
                        int slot = AudioVoicePool.Choose(pool, turn[k], def.Priority, cfg.PerCueVoices, now);
                        if (slot < 0) { dropped++; continue; }
                        pool[slot].Cue = turn[k];
                        pool[slot].Priority = def.Priority;
                        pool[slot].Started = now;
                        pool[slot].BusyUntil = now + (def.Trim > 0f ? def.Trim : 1.5f);
                        played++;
                    }
                    int sounding = AudioVoicePool.Busy(pool, now);
                    if (sounding > mostSounding) mostSounding = sounding;
                    int ofOne = 0;
                    for (int s = 0; s < pool.Count; s++) if (now < pool[s].BusyUntil && pool[s].Cue == turn[k]) ofOne++;
                    if (ofOne > mostOfOne) mostOfOne = ofOne;
                }
            }
            Check(mostSounding <= cfg.SfxVoices && mostOfOne <= cfg.PerCueVoices && played > 0,
                  "a worst-case turn (" + turn.Length + " cues asked every 20 ms for 2 s, " + asked + " requests) never sounds more than "
                  + mostSounding + " voices of " + cfg.SfxVoices + " or more than " + mostOfOne + " of one cue (cap " + cfg.PerCueVoices + "); "
                  + played + " sounded, " + dropped + " dropped");

            // C. priority: a full pool of priority 1 gives way to priority 2, a full pool of priority 2 does not give way to priority 1.
            List<VoiceSlot> small = new List<VoiceSlot>();
            for (int i = 0; i < 4; i++)
            {
                VoiceSlot s = new VoiceSlot();
                s.Cue = "c" + i; s.Priority = 1; s.Started = i; s.BusyUntil = 10f;
                small.Add(s);
            }
            int steals = AudioVoicePool.Choose(small, "new", 2, 3, 5f);
            for (int i = 0; i < small.Count; i++) small[i].Priority = 2;
            int refused = AudioVoicePool.Choose(small, "new", 1, 3, 5f);
            Check(steals == 0 && refused == -1,
                  "a full pool gives its oldest weaker voice to a more important sound and drops a less important one (took " + steals + ", then " + refused + ")");

            // D. a cue at its share takes its own oldest voice back, not somebody else's.
            List<VoiceSlot> own = new List<VoiceSlot>();
            for (int i = 0; i < 4; i++)
            {
                VoiceSlot s = new VoiceSlot();
                s.Cue = i < 3 ? "x" : "y"; s.Priority = 1; s.Started = 3 - i; s.BusyUntil = 10f;
                own.Add(s);
            }
            int took = AudioVoicePool.Choose(own, "x", 1, 3, 5f);
            Check(took == 2, "a cue holding its whole share takes back its own oldest voice (slot " + took + ")");
        }

        private const int AudioCueLog_RateLimit = 8;

        // ------------------------------------------------------------------ wiring tripwires

        private static void CheckAudioWiring(AudioCueConfig cfg, string root)
        {
            string scripts = Path.Combine(root, "Assets", "Scripts");

            // Every cue id the code names exists. A literal that ends in a dot is a prefix the code
            // completes with an aspect or a tier.
            List<string> unknown = new List<string>();
            int literals = 0;
            Regex quoted = new Regex("\"((?:ui|battle|summon|music)\\.[a-z0-9.]*)\"");
            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (name == "AudioChecks.cs") continue;
                string text = File.ReadAllText(file);
                foreach (Match m in quoted.Matches(text))
                {
                    string id = m.Groups[1].Value;
                    literals++;
                    if (id.EndsWith(".", StringComparison.Ordinal))
                    {
                        bool any = false;
                        foreach (string known in cfg.Order) if (known.StartsWith(id, StringComparison.Ordinal)) any = true;
                        if (!any) unknown.Add(name + ": " + id);
                    }
                    else if (cfg.Cue(id) == null) unknown.Add(name + ": " + id);
                }
            }
            Check(unknown.Count == 0 && literals > 0,
                  "WIRING (reads source): every cue id the code names is in audio.json (" + literals + " names)"
                  + (unknown.Count > 0 ? ": UNKNOWN " + string.Join(", ", unknown.ToArray()) : ""));

            // The rollback keeps the old sounds: Sfx still carries the synthesised set.
            string sfxPath = Path.Combine(scripts, "Game", "Sfx.cs");
            string sfx = File.Exists(sfxPath) ? File.ReadAllText(sfxPath) : "";
            Check(sfx.Contains("private void BuildOld()") && sfx.Contains("Tone(\"hit\"") && sfx.Contains("Chord(\"victory\"")
                  && sfx.Contains("RealAudio") && sfx.Contains("PlayOld("),
                  "WIRING (reads source): Sfx still carries the original synthesised tones behind the RealAudio flag");

            // Battle sounds play on the strike. The Damage event is handled after the wait for
            // ImpactFraction, so a sound played in its case lands on the strike, not the wind-up.
            string battlePath = Path.Combine(scripts, "UI", "Screens", "BattleScreen.cs");
            string battle = File.Exists(battlePath) ? File.ReadAllText(battlePath) : "";
            int damageAt = battle.IndexOf("case BattleEventType.Damage:", StringComparison.Ordinal);
            int soundAt = battle.IndexOf("PlayDamageSound(e, source);", StringComparison.Ordinal);
            int numberAt = battle.IndexOf("number floats one frame after impact", StringComparison.Ordinal);
            Check(damageAt >= 0 && soundAt > damageAt && numberAt > soundAt
                  && battle.Contains("if (!App.Audio.RealAudio)") && battle.Contains("App.Audio.Cast(source.Snapshot.Aspect)")
                  && battle.Contains("App.Audio.SkillImpact(source.Snapshot.Aspect)"),
                  "WIRING (reads source): the hit sound is played in the Damage event itself, before the number floats, so it lands on the strike; casts and skill impacts key off the caster's aspect; the old tones keep their one-sound-per-hit rule");

            // Sounds that outlive their screen, and sounds the old game never made.
            string summonPath = Path.Combine(scripts, "UI", "Screens", "SummonScreen.cs");
            string summon = File.Exists(summonPath) ? File.ReadAllText(summonPath) : "";
            int hideAt = summon.IndexOf("public override void OnHide()", StringComparison.Ordinal);
            int hideEnd = hideAt < 0 ? -1 : summon.IndexOf("}", hideAt, StringComparison.Ordinal);
            string servicePath = Path.Combine(scripts, "Game", "AudioService.cs");
            string service = File.Exists(servicePath) ? File.ReadAllText(servicePath) : "";
            int shownAt = service.IndexOf("public void ScreenShown(", StringComparison.Ordinal);
            int shownEnd = shownAt < 0 ? -1 : service.IndexOf("SetMusic(cue);", shownAt, StringComparison.Ordinal);
            Check(hideAt >= 0 && hideEnd > hideAt && summon.Substring(hideAt, hideEnd - hideAt).Contains("StopLoop(\"summon.charge\"")
                  && shownEnd > shownAt && service.Substring(shownAt, shownEnd - shownAt).Contains("StopLoop("),
                  "WIRING (reads source): the summon charge loop is stopped when the summon screen is left (an abort fires no beat), and every screen change stops any running loop");
            Check(sfx.Contains("public void Unlock() { Cue(\"ui.unlock\"); }") && sfx.Contains("public void Confirm() { Cue(\"ui.confirm\"); }"),
                  "WIRING (reads source): unlock and confirm have no old sound, so they are silent with the flag off, as the original game was");
            string overlayPath = Path.Combine(scripts, "UI", "Overlays.cs");
            string overlays = File.Exists(overlayPath) ? File.ReadAllText(overlayPath) : "";
            Check(overlays.Contains(".Audio.Confirm()"), "WIRING (reads source): ui.confirm is played by a dialog's confirm button");
            string logPath = Path.Combine(scripts, "Game", "AudioCueLog.cs");
            string log = File.Exists(logPath) ? File.ReadAllText(logPath) : "";
            Check(log.Contains("Application.isEditor && !Debug.isDebugBuild") && log.Contains("MaxFileBytes"),
                  "WIRING (reads source): the cue log writes a file only in the editor or a development build, and stops at a size cap");

            // The screen holds no audio decisions: it names cues and calls the service.
            string cardPath = Path.Combine(scripts, "UI", "Screens", "AudioSettingsCard.cs");
            string card = File.Exists(cardPath) ? File.ReadAllText(cardPath) : "";
            Check(card.Contains("AudioPrefs.MusicPercent =") && card.Contains("AudioPrefs.SfxPercent =") && card.Contains("AudioPrefs.UiPercent =")
                  && !card.Contains("PlayerPrefs") && !card.Contains("AudioSource"),
                  "WIRING (reads source): the settings card writes only AudioPrefs and never PlayerPrefs, the save or a sound source");

            string prefsPath = Path.Combine(scripts, "Game", "AudioPrefs.cs");
            string prefs = File.Exists(prefsPath) ? File.ReadAllText(prefsPath) : "";
            Check(prefs.Contains("DefaultMusic = 60") && prefs.Contains("DefaultSfx = 80") && prefs.Contains("DefaultUi = 70")
                  && prefs.Contains("PlayerPrefs") && !prefs.Contains("Profile") && !prefs.Contains("SaveService"),
                  "WIRING (reads source): the volumes default to 60, 80 and 70 and live in PlayerPrefs, not in the save");

            // A script or content file without a .meta gets a fresh guid on every machine's import.
            List<string> noMeta = new List<string>();
            foreach (string cs in Directory.GetFiles(scripts, "Audio*.cs", SearchOption.AllDirectories))
                if (!File.Exists(cs + ".meta")) noMeta.Add(Path.GetFileName(cs));
            foreach (string name in new string[] { "audio.json", "credits.txt" })
                if (!File.Exists(Path.Combine(root, "Assets", "Resources", "Content", name + ".meta"))) noMeta.Add(name);
            Check(noMeta.Count == 0, "every Audio*.cs under Assets/Scripts, audio.json and credits.txt has a .meta"
                  + (noMeta.Count == 0 ? "" : ": missing for " + string.Join(", ", noMeta.ToArray())));

            // Saved to disk when it is changed, not only when the page is left.
            int mutedAt = prefs.IndexOf("public static bool Muted", StringComparison.Ordinal);
            int mutedEnd = mutedAt < 0 ? -1 : prefs.IndexOf("/// <summary>0 to 1", mutedAt, StringComparison.Ordinal);
            Check(mutedAt >= 0 && mutedEnd > mutedAt && prefs.Substring(mutedAt, mutedEnd - mutedAt).Contains("Flush();")
                  && card.Contains("AudioPrefs.Flush()") && card.Contains("IPointerUpHandler") && card.Contains("AddComponent<SliderRelease>()"),
                  "WIRING (reads source): the mute switch writes to disk when flipped and each slider writes when it is let go");

            // The slider fill: Unity's Slider resets the anchors of the fill and the handle to the full
            // height of their parent, so the parents carry the thickness and the fill has no offsets.
            Check(card.Contains("Band(fillArea, trackHeight") && card.Contains("Band(handleArea, HandleSize")
                  && card.Contains("fr.anchorMax = Vector2.one;") && !card.Contains("fr.offsetMin = new Vector2(0f, -9f)"),
                  "WIRING (reads source): each slider fill stretches inside a fill area exactly as thick as the track, so it cannot draw as a block");

            // The one-launch rollback, and the play-proof targets that are never swept by the nightly.
            string serviceText = File.Exists(Path.Combine(scripts, "Game", "AudioService.cs"))
                ? File.ReadAllText(Path.Combine(scripts, "Game", "AudioService.cs")) : "";
            Check(serviceText.Contains("EMBERFALL_REAL_AUDIO") && serviceText.Contains("Application.isEditor || Debug.isDebugBuild"),
                  "WIRING (reads source): EMBERFALL_REAL_AUDIO=0 turns real audio off for one launch, in the editor and development builds only");
            string shotsPath = Path.Combine(root, "Assets", "Editor", "PlayModeScreenshot.cs");
            string shots = File.Exists(shotsPath) ? File.ReadAllText(shotsPath) : "";
            Check(shots.Contains("name == \"AudioSummon\"") && shots.Contains("name == \"AudioFight\"")
                  && !shots.Contains("Begin(\"Screen:AudioSummon\")") && !shots.Contains("Begin(\"Screen:AudioFight"),
                  "WIRING (reads source): the AudioSummon and AudioFight capture targets exist and have no literal Begin call, so the nightly does not sweep them");
        }

        // ------------------------------------------------------------------ music (R14-MUSIC)

        /// <summary>The cues that loop a track. The two stingers are one-shots and may stay empty.</summary>
        private static readonly string[] MusicLoopCues =
        {
            "music.hub", "music.map", "music.battle", "music.battle.ch2", "music.battle.boss", "music.summon",
            "music.recital", "music.proving", "music.guild", "music.prologue"
        };

        /// <summary>
        /// THE MUSIC, as checks (R14-MUSIC). Playback needs speakers, so what is proved is everything
        /// around it that can be read: every loop cue has a real 48 kHz 16 bit file of the right length,
        /// each stage resolves to the track the cue map promises, the music flag false really means
        /// every music cue is silent, the duck envelope has the numbers the owner asked for, and the
        /// service and the screens call the engine-free logic. What it then sounds like is for the Play
        /// proof's cue log and the owner's ears.
        /// </summary>
        private static void CheckMusic(ContentDatabase db, AudioCueConfig cfg, string root, string audioRoot, string jsonText)
        {
            Line("");
            Line("MUSIC (R14-MUSIC: the owner's five tracks, the cue map, crossfade, carry on, duck)");

            // 1. every loop cue has files, and they are loops under music/
            List<string> noFiles = new List<string>();
            List<string> badName = new List<string>();
            for (int i = 0; i < MusicLoopCues.Length; i++)
            {
                AudioCueDef c = cfg.Cue(MusicLoopCues[i]);
                if (c == null || c.Channel != AudioChannel.Music || !c.Loop || c.Files.Count == 0) { noFiles.Add(MusicLoopCues[i]); continue; }
                for (int f = 0; f < c.Files.Count; f++)
                    if (!c.Files[f].StartsWith("music/", StringComparison.Ordinal) || !c.Files[f].EndsWith("_loop", StringComparison.Ordinal))
                        badName.Add(MusicLoopCues[i] + "=" + c.Files[f]);
            }
            Check(noFiles.Count == 0 && badName.Count == 0,
                  "every music cue except the two stingers has a looping file under music/ named <track>_loop (" + MusicLoopCues.Length + " cues)"
                  + (noFiles.Count > 0 ? ": NO FILES " + string.Join(", ", noFiles.ToArray()) : "")
                  + (badName.Count > 0 ? ": BAD NAME " + string.Join(", ", badName.ToArray()) : ""));

            // 2. the files: real wav, 48 kHz, 16 bit, stereo, the right length
            string summonFile = cfg.Cue("music.summon") != null && cfg.Cue("music.summon").Files.Count > 0 ? cfg.Cue("music.summon").Files[0] : "";
            HashSet<string> musicFiles = new HashSet<string>();
            HashSet<string> loopFiles = new HashSet<string>();
            HashSet<string> introFiles = new HashSet<string>();
            foreach (string id in cfg.Order)
            {
                AudioCueDef c = cfg.Cues[id];
                if (c.Channel != AudioChannel.Music) continue;
                for (int f = 0; f < c.Files.Count; f++) { musicFiles.Add(c.Files[f]); loopFiles.Add(c.Files[f]); }
                if (c.Intro.Length > 0) { musicFiles.Add(c.Intro); introFiles.Add(c.Intro); }
            }
            List<string> headerBad = new List<string>();
            List<string> lengthBad = new List<string>();
            List<string> lengths = new List<string>();
            foreach (string file in musicFiles)
            {
                string path = Path.Combine(audioRoot, file.Replace('/', Path.DirectorySeparatorChar) + ".wav");
                if (!File.Exists(path)) { headerBad.Add(file + " (missing)"); continue; }
                byte[] head = new byte[65536];
                int got;
                long length = new FileInfo(path).Length;
                using (FileStream fs = File.OpenRead(path)) got = fs.Read(head, 0, head.Length);
                if (got < head.Length) Array.Resize(ref head, got);
                WavInfo.Header h = WavInfo.ReadHeader(head, length);
                if (!h.Ok || h.Format != 1 || h.SampleRate != 48000 || h.Bits != 16 || h.Channels != 2)
                {
                    headerBad.Add(file + " (" + (h.Ok ? h.Format + "/" + h.Channels + "ch/" + h.SampleRate + "Hz/" + h.Bits + "bit" : "not a wav") + ")");
                    continue;
                }
                lengths.Add(Path.GetFileName(file) + " " + h.Seconds.ToString("0.0") + " s");
                if (introFiles.Contains(file) && !loopFiles.Contains(file)) { if (h.Seconds < 0.5) lengthBad.Add(file + " " + h.Seconds.ToString("0.0")); continue; }
                bool summon = file == summonFile;
                double lo = summon ? 30.0 : 60.0;
                double hi = summon ? 60.0 : 1e9;
                if (h.Seconds < lo || h.Seconds > hi) lengthBad.Add(file + " " + h.Seconds.ToString("0.0") + " s not in " + lo + " to " + (summon ? "60" : "any"));
            }
            Check(musicFiles.Count > 0 && headerBad.Count == 0,
                  "every music file exists as a PCM wav at 48 kHz, 16 bit, stereo (" + musicFiles.Count + " files)"
                  + (headerBad.Count > 0 ? ": BAD " + string.Join(", ", headerBad.ToArray()) : ""));
            Check(musicFiles.Count > 0 && lengthBad.Count == 0,
                  "each loop is at least 60 s, the summon loop 30 to 60 s: " + string.Join(", ", lengths.ToArray())
                  + (lengthBad.Count > 0 ? "; WRONG " + string.Join(", ", lengthBad.ToArray()) : ""));

            // 3. named after the owner's tracks, nothing unused, streaming import
            string audioIn = Environment.GetEnvironmentVariable("EMBERFALL_AUDIO_IN");
            if (string.IsNullOrEmpty(audioIn)) audioIn = @"D:\Emberfall\audio-in";
            string flacDir = Path.Combine(audioIn, "music");
            if (!Directory.Exists(flacDir))
            {
                Line("  [skip] the owner's audio-in/music folder is not on this machine; the track name comparison did not run");
            }
            else
            {
                List<string> unnamed = new List<string>();
                foreach (string file in musicFiles)
                {
                    string baseName = file.Substring("music/".Length);
                    if (baseName.EndsWith("_loop", StringComparison.Ordinal)) baseName = baseName.Substring(0, baseName.Length - "_loop".Length);
                    else if (baseName.EndsWith("_intro", StringComparison.Ordinal)) baseName = baseName.Substring(0, baseName.Length - "_intro".Length);
                    else { unnamed.Add(file); continue; }
                    if (!File.Exists(Path.Combine(flacDir, baseName + ".flac"))) unnamed.Add(file);
                }
                Check(unnamed.Count == 0, "every music file is named after one of the owner's FLAC tracks, plus _loop or _intro (music is converted and cut, so it is not a byte copy)"
                      + (unnamed.Count > 0 ? ": NO TRACK FOR " + string.Join(", ", unnamed.ToArray()) : ""));
            }

            List<string> orphanMusic = new List<string>();
            List<string> notStreaming = new List<string>();
            string musicRoot = Path.Combine(audioRoot, "music");
            if (Directory.Exists(musicRoot))
            {
                foreach (string file in Directory.GetFiles(musicRoot, "*.wav", SearchOption.TopDirectoryOnly))
                {
                    string rel = "music/" + Path.GetFileNameWithoutExtension(file);
                    if (!musicFiles.Contains(rel)) orphanMusic.Add(rel);
                    string metaPath = file + ".meta";
                    string meta = File.Exists(metaPath) ? File.ReadAllText(metaPath) : "";
                    if (MetaInt(meta, "loadType") != 2 || MetaInt(meta, "compressionFormat") != 1 || MetaInt(meta, "forceToMono") != 0)
                        notStreaming.Add(rel);
                }
            }
            Check(orphanMusic.Count == 0, "no music file is unused" + (orphanMusic.Count > 0 ? ": " + string.Join(", ", orphanMusic.ToArray()) : ""));
            Check(notStreaming.Count == 0 && Directory.Exists(musicRoot),
                  "the import meta of every music file is streaming Vorbis, stereo" + (notStreaming.Count > 0 ? ": WRONG " + string.Join(", ", notStreaming.ToArray()) : ""));

            // 4. the cue map: which stage plays which track
            CheckMusicMap(db, cfg);

            // 5. the music flag and its rollback
            CheckMusicFlag(cfg, jsonText);

            // 6. crossfade, carry on, duck
            CheckMusicTransitions(cfg, jsonText);

            // 7. the source: the service, the screens and the credits use the logic proved above
            CheckMusicWiring(cfg, root);
        }

        private static string MusicFileOf(AudioCueConfig cfg, string cueId)
        {
            AudioCueDef c = cfg.Cue(cueId);
            return c != null && c.Files.Count > 0 ? c.Files[0] : "";
        }

        private static void CheckMusicMap(ContentDatabase db, AudioCueConfig cfg)
        {
            // chapter 1 and 2 battles, and the rest, through the same function BattleScreen calls
            int ch1 = 0, ch2 = 0, ch1Wrong = 0, ch2Wrong = 0, otherChapters = 0, otherWrong = 0;
            int walls = 0, finales = 0, bossWrong = 0;
            for (int i = 0; i < db.AllStages.Count; i++)
            {
                StageDef s = db.AllStages[i];
                string kind = AudioCueConfig.CampaignBattleKind(s);
                string cue = cfg.BattleMusicCue(kind);
                string file = MusicFileOf(cfg, cue);
                if (s.IsBoss || s.IsMiniboss)
                {
                    if (s.IsMiniboss) walls++;
                    if (s.IsBoss) finales++;
                    if (kind != "boss" || cue != "music.battle.boss" || file != "music/boss_loop") bossWrong++;
                    continue;
                }
                if (s.ChapterNumber == 1) { ch1++; if (cue != "music.battle" || file != "music/battle_ch1_loop") ch1Wrong++; }
                else if (s.ChapterNumber == 2) { ch2++; if (cue != "music.battle.ch2" || file != "music/battle_ch2_loop") ch2Wrong++; }
                else
                {
                    otherChapters++;
                    if (file != "music/battle_ch1_loop" && file != "music/battle_ch2_loop") otherWrong++;
                }
            }
            Check(ch1 > 0 && ch1Wrong == 0, "every chapter 1 campaign battle plays battle_ch1_loop (" + ch1 + " stages)");
            Check(ch2 > 0 && ch2Wrong == 0, "every chapter 2 campaign battle plays battle_ch2_loop (" + ch2 + " stages)");
            Check(otherChapters > 0 && otherWrong == 0,
                  "chapters 3 and later play one of the two battle tracks, alternating (" + otherChapters + " stages; DEFAULT TAKEN)");
            Check(walls == db.Chapters.Count && finales == db.Chapters.Count && bossWrong == 0,
                  "every wall (the miniboss stage, " + walls + ") and every chapter finale (the boss stage, " + finales + ") plays music.battle.boss, boss_loop"
                  + (bossWrong > 0 ? ": WRONG " + bossWrong : ""));
            int lastIsBoss = 0;
            for (int c = 0; c < db.Chapters.Count; c++)
            {
                List<StageDef> st = db.Chapters[c].Stages;
                if (st.Count > 0 && st[st.Count - 1].IsBoss) lastIsBoss++;
            }
            Check(lastIsBoss == db.Chapters.Count, "a chapter finale is the last stage of its chapter and is flagged boss (" + lastIsBoss + " of " + db.Chapters.Count + " chapters)");

            // the fallback lookup stays: a chapter the map does not name plays the campaign track
            Check(cfg.BattleMusicCue("campaign.ch99") == "music.battle" && cfg.BattleMusicCue("campaign") == "music.battle",
                  "the fallback lookup stays: Battle.campaign.chN falls back to Battle.campaign (chapter 99 plays " + cfg.BattleMusicCue("campaign.ch99") + ")");
            Check(cfg.BattleMusicCue("prologue") == "music.prologue" && cfg.BattleMusicCue("boss") == "music.battle.boss"
                  && cfg.BattleMusicCue("recital") == "music.battle" && cfg.BattleMusicCue("proving") == "music.battle"
                  && cfg.BattleMusicCue("guild") == "music.battle",
                  "the other battle kinds keep their cues: prologue, boss, recital, proving, guild");

            // the screens
            string hub = MusicFileOf(cfg, cfg.ScreenMusicCue("MainMenu"));
            string map = MusicFileOf(cfg, cfg.ScreenMusicCue("Campaign"));
            Check(hub == "music/hub_loop" && map == hub && cfg.ScreenMusicCue("MainMenu") != cfg.ScreenMusicCue("Campaign"),
                  "the hub and the map are two cues that share one file, hub_loop (" + hub + ", " + map + ")");
            Check(MusicFileOf(cfg, cfg.ScreenMusicCue("Summon")) == "music/summon_loop", "the summon screen plays summon_loop");
            bool guildHub = true;
            string[] guildScreens = { "Guild", "GuildRequests", "GuildChat", "GuildDungeon", "GuildFloor", "GuildDungeonResult", "GuildDungeonBoards", "GuildTech", "Recital", "Proving" };
            for (int i = 0; i < guildScreens.Length; i++)
                if (MusicFileOf(cfg, cfg.ScreenMusicCue(guildScreens[i])) != "music/hub_loop") guildHub = false;
            Check(guildHub, "recital, proving and the guild screens play the hub track, so going there from the hub carries on (DEFAULT TAKEN)");
            Check(MusicFileOf(cfg, "music.prologue") == "music/battle_ch1_loop", "the prologue fight plays battle_ch1_loop (DEFAULT TAKEN)");
        }

        private static void CheckMusicFlag(AudioCueConfig cfg, string jsonText)
        {
            string offText = jsonText.Replace("\"music\": true", "\"music\": false");
            string missingText = jsonText.Replace("  \"music\": true,\n", "").Replace("  \"music\": true,\r\n", "");
            AudioCueConfig off = AudioCueConfig.FromJson(offText);
            AudioCueConfig missing = AudioCueConfig.FromJson(missingText);
            Check(cfg.Music && offText != jsonText && !off.Music && missingText != jsonText && missing.Music,
                  "THE MUSIC FLAG: audio.json carries \"music\": true; false parses as off; a file with no key reads as on (an old file lists no music files, so on plays nothing there)");

            // ROLLBACK. The same json with every music file taken away is what the game shipped before R14.
            string preText = Regex.Replace(jsonText, "\"files\": \\[\"music/[^\\]]*\\]", "\"files\": []");
            preText = Regex.Replace(preText, "\"intro\": \"[^\"]*\",?\\s*", "");
            AudioCueConfig pre = AudioCueConfig.FromJson(preText);
            int cues = 0, differs = 0, silentOff = 0, musicCues = 0, sfxChanged = 0;
            foreach (string id in cfg.Order)
            {
                AudioCueDef now = off.Cue(id);
                AudioCueDef before = pre.Cue(id);
                cues++;
                if (now == null || before == null) { differs++; continue; }
                if (now.Channel == AudioChannel.Music)
                {
                    musicCues++;
                    if (off.IsSilentNow(now) && off.EffectiveFiles(now).Count == 0 && off.EffectiveIntro(now).Length == 0) silentOff++;
                    if (off.IsSilentNow(now) != pre.IsSilentNow(before)) differs++;
                }
                else
                {
                    List<string> a = off.EffectiveFiles(now);
                    List<string> b = pre.EffectiveFiles(before);
                    bool same = a.Count == b.Count;
                    for (int k = 0; same && k < a.Count; k++) if (a[k] != b[k]) same = false;
                    if (!same) sfxChanged++;
                }
            }
            Check(musicCues > 0 && silentOff == musicCues && differs == 0 && sfxChanged == 0,
                  "ROLLBACK: with \"music\": false every music cue is silent (" + silentOff + " of " + musicCues + ") exactly as when no music file was listed, and the " + (cues - musicCues) + " other cues are untouched");

            // The screens resolve to the same cue ids as before R14 for the old kinds, and all of them are silent.
            string[] oldKinds = { "campaign", "boss", "recital", "proving", "prologue", "guild" };
            int sameResolve = 0, silentResolve = 0;
            for (int i = 0; i < oldKinds.Length; i++)
            {
                string expect;
                if (!off.MusicByScreen.TryGetValue("Battle." + oldKinds[i], out expect)) off.MusicByScreen.TryGetValue("Battle", out expect);
                string got = off.BattleMusicCue(oldKinds[i]);
                if (got == expect) sameResolve++;
                if (got != null && off.IsSilentNow(off.Cue(got))) silentResolve++;
            }
            int screensSilent = 0;
            foreach (KeyValuePair<string, string> pair in off.MusicByScreen)
                if (off.IsSilentNow(off.Cue(pair.Value))) screensSilent++;
            Check(sameResolve == oldKinds.Length && silentResolve == oldKinds.Length && screensSilent == off.MusicByScreen.Count,
                  "ROLLBACK: with the flag off every old battle kind still resolves to its old cue (" + sameResolve + " of " + oldKinds.Length
                  + ") and every one of the " + off.MusicByScreen.Count + " screen entries is silent");

            // The duck does nothing, and no carry is possible, with the flag off.
            MusicDuck duck = new MusicDuck();
            duck.Trigger(off, 1f);
            float gain = 1f;
            for (int i = 0; i < 100; i++) gain = Math.Min(gain, duck.Step(off, 0.01f));
            AudioCueDef hubOff = off.Cue("music.map");
            Check(gain == 1f && duck.Depth == 0f && !off.CarriesOn("music/hub_loop", hubOff),
                  "ROLLBACK: with the flag off the duck never moves the music and no track carries on");
        }

        private static void CheckMusicTransitions(AudioCueConfig cfg, string jsonText)
        {
            // crossfade of about a second
            List<string> fades = new List<string>();
            bool fadesOk = cfg.MusicFade >= 0.8f && cfg.MusicFade <= 1.5f;
            for (int i = 0; i < MusicLoopCues.Length; i++)
            {
                AudioCueDef c = cfg.Cue(MusicLoopCues[i]);
                float f = c == null ? -1f : (c.Fade >= 0f ? c.Fade : cfg.MusicFade);
                if (f < 0.8f || f > 1.5f) { fadesOk = false; fades.Add(MusicLoopCues[i] + "=" + f); }
            }
            Check(fadesOk, "a screen change crossfades in about 1 second (musicFade " + cfg.MusicFade + ", every music loop cue 0.8 to 1.5 s)"
                  + (fades.Count > 0 ? ": WRONG " + string.Join(", ", fades.ToArray()) : ""));

            // carry on compares the file, not the cue id
            AudioCueDef hub = cfg.Cue("music.hub");
            AudioCueDef map = cfg.Cue("music.map");
            AudioCueDef battle = cfg.Cue("music.battle");
            AudioCueDef ch2 = cfg.Cue("music.battle.ch2");
            string hubFile = MusicFileOf(cfg, "music.hub");
            Check(hub != null && map != null && battle != null && ch2 != null
                  && cfg.CarriesOn(hubFile, map) && cfg.CarriesOn(MusicFileOf(cfg, "music.map"), hub)
                  && !cfg.CarriesOn(hubFile, battle) && !cfg.CarriesOn(MusicFileOf(cfg, "music.battle"), ch2)
                  && cfg.CarriesOn(MusicFileOf(cfg, "music.battle"), battle) && !cfg.CarriesOn("", map),
                  "the same track carries on without a restart under another cue id (hub to map, and the chapter 1 battle track is not the chapter 2 one), and a different track never does");
            Check(cfg.CarriesOn(MusicFileOf(cfg, "music.battle"), cfg.Cue("music.prologue")),
                  "the prologue fight carries on from a chapter 1 battle (same file, other cue)");

            // an intro plays once, then the loop: parsed, listed for the file checks, gated by the flag
            // The summon cue ships without an intro, so a synthetic one proves the plumbing on its own.
            string withIntro = jsonText.Replace("\"id\": \"music.summon\",", "\"id\": \"music.summon\",\n      \"intro\": \"music/summon_intro\",");
            AudioCueConfig intro = AudioCueConfig.FromJson(withIntro);
            AudioCueConfig introOff = AudioCueConfig.FromJson(withIntro.Replace("\"music\": true", "\"music\": false"));
            AudioCueDef isum = intro.Cue("music.summon");
            Check(withIntro != jsonText && isum != null && isum.Intro == "music/summon_intro" && intro.EffectiveIntro(isum) == "music/summon_intro"
                  && intro.AllFiles().Contains("music/summon_intro") && introOff.EffectiveIntro(introOff.Cue("music.summon")) == ""
                  && intro.EffectiveIntro(intro.Cue("music.hub")) == "music/hub_intro" && cfg.EffectiveIntro(cfg.Cue("music.summon")) == "",
                  "a cue may name an intro file: it is parsed, it joins the file checks, it belongs to that cue alone, and the flag off drops it");

            // every shipped intro sits beside the loop it opens, with the same base name, on every cue that uses that loop
            List<string> mismatched = new List<string>();
            int withIntros = 0;
            for (int i = 0; i < MusicLoopCues.Length; i++)
            {
                AudioCueDef c = cfg.Cue(MusicLoopCues[i]);
                if (c == null || c.Intro.Length == 0) continue;
                withIntros++;
                string loopName = c.Files[0];
                if (c.Intro != loopName.Substring(0, loopName.Length - "_loop".Length) + "_intro") mismatched.Add(c.Id);
            }
            Check(withIntros > 0 && mismatched.Count == 0,
                  "every intro is the _intro of the loop it opens (" + withIntros + " cues have one)" + (mismatched.Count > 0 ? ": MISMATCH " + string.Join(", ", mismatched.ToArray()) : ""));

            // the duck: the numbers
            Check(cfg.DuckDb >= 4f && cfg.DuckDb <= 8f && cfg.DuckIn >= 0.02f && cfg.DuckIn <= 0.25f && cfg.DuckOut >= 0.3f && cfg.DuckOut <= 0.8f
                  && cfg.DuckVictoryHold >= 0.3f && cfg.DuckVictoryHold <= 3f,
                  "the duck values are in range: " + cfg.DuckDb + " dB down over " + cfg.DuckIn + " s, back over " + cfg.DuckOut + " s, held " + cfg.DuckVictoryHold + " s for the victory");

            // the duck: the envelope, run
            MusicDuck duck = new MusicDuck();
            const float dt = 0.005f;
            duck.Trigger(cfg, 0.30f);
            float minGain = 1f, tDown = -1f, tBack = -1f, t = 0f;
            float gain = 1f;
            for (int step = 0; step < 400; step++)
            {
                t += dt;
                gain = duck.Step(cfg, dt);
                if (gain < minGain) minGain = gain;
                if (tDown < 0f && duck.Depth >= 0.999f) tDown = t;
                if (tDown >= 0f && tBack < 0f && t > 0.30f && gain >= 0.995f) tBack = t;
            }
            double db = 20.0 * Math.Log10(minGain);
            Check(Math.Abs(db + cfg.DuckDb) < 0.1 && tDown > 0f && tDown <= cfg.DuckIn + 0.02f && tBack > 0.30f && tBack - 0.30f <= cfg.DuckOut + 0.03f && gain >= 0.999f,
                  "the duck envelope steps back " + db.ToString("0.0") + " dB in " + tDown.ToString("0.000") + " s, holds the 0.30 s of a skill moment, and is back in "
                  + (tBack - 0.30f).ToString("0.000") + " s (the config says " + cfg.DuckOut + ")");

            // a second skill inside the first extends the hold and never ducks deeper
            MusicDuck again = new MusicDuck();
            again.Trigger(cfg, 0.30f);
            float deepest = 1f;
            for (int step = 0; step < 100; step++)
            {
                if (step == 40) again.Trigger(cfg, 0.30f);
                deepest = Math.Min(deepest, again.Step(cfg, dt));
            }
            Check(deepest >= cfg.DuckGain - 0.0001f, "two skill moments in a row never duck deeper than " + cfg.DuckDb + " dB (deepest " + (20.0 * Math.Log10(deepest)).ToString("0.00") + " dB)");

            AudioCueDef vic = cfg.Cue("music.stinger.victory");
            AudioCueDef lose = cfg.Cue("music.stinger.defeat");
            Check(vic != null && lose != null && Math.Abs(vic.StopMusic - 0.8f) < 0.0001f && Math.Abs(lose.StopMusic - 0.8f) < 0.0001f,
                  "the stingers keep stopMusic 0.8 s (victory " + (vic == null ? -1f : vic.StopMusic) + ", defeat " + (lose == null ? -1f : lose.StopMusic) + ")");

            // A stinger ends the battle track only when it plays a file. Today both are empty, so the
            // track plays on under the result and the victory duck steps it back and returns.
            string stingerFile = Regex.Replace(jsonText, "\"files\": \\[\\](\\s*,\\s*\"note\": \"victory stinger)", "\"files\": [\"music/boss_loop\"]$1");
            AudioCueConfig withFile = AudioCueConfig.FromJson(stingerFile);
            AudioCueConfig withFileOff = AudioCueConfig.FromJson(stingerFile.Replace("\"music\": true", "\"music\": false"));
            AudioCueDef vicFile = withFile.Cue("music.stinger.victory");
            Check(stingerFile != jsonText && vicFile != null && vicFile.Files.Count == 1
                  && !cfg.StopsMusic(vic) && !cfg.StopsMusic(lose)
                  && withFile.StopsMusic(vicFile) && !withFile.StopsMusic(withFile.Cue("music.stinger.defeat"))
                  && withFileOff.StopsMusic(withFileOff.Cue("music.stinger.victory")) && !cfg.StopsMusic(cfg.Cue("music.hub")),
                  "a stinger with no file leaves the battle track playing under the result (only the duck moves it); one with a file ends the track over stopMusic; with the flag off the old rule stands");
        }

        private static void CheckMusicWiring(AudioCueConfig cfg, string root)
        {
            string scripts = Path.Combine(root, "Assets", "Scripts");
            string service = ReadIfThere(Path.Combine(scripts, "Game", "AudioService.cs"));
            string sfx = ReadIfThere(Path.Combine(scripts, "Game", "Sfx.cs"));
            string vfx = ReadIfThere(Path.Combine(scripts, "Battle", "VfxDirector.cs"));
            string battle = ReadIfThere(Path.Combine(scripts, "UI", "Screens", "BattleScreen.cs"));
            string credits = ReadIfThere(Path.Combine(root, "Assets", "Resources", "Content", "credits.txt"));

            int setAt = service.IndexOf("public void SetMusic(", StringComparison.Ordinal);
            int setEnd = setAt < 0 ? -1 : service.IndexOf("private Fader PlayingFader(", setAt, StringComparison.Ordinal);
            string setBody = setAt >= 0 && setEnd > setAt ? service.Substring(setAt, setEnd - setAt) : "";
            Check(setBody.Contains("_config.EffectiveFiles(def)") && setBody.Contains("_config.CarriesOn(") && setBody.Contains("same-track carry")
                  && setBody.Contains("PlayScheduled") && !setBody.Contains("def.Files") && !setBody.Contains("def.IsSilent"),
                  "WIRING (reads source): SetMusic reads the files through the music flag, carries on by file, logs the carry, and starts an intro on the audio clock; it never reads a cue's own file list");

            int duckAt = service.IndexOf("public void Duck(", StringComparison.Ordinal);
            int duckEnd = duckAt < 0 ? -1 : service.IndexOf("VictoryDuckHold", duckAt, StringComparison.Ordinal);
            string duckBody = duckAt >= 0 && duckEnd > duckAt ? service.Substring(duckAt, duckEnd - duckAt) : "";
            Check(duckBody.Contains("!_config.Music") && duckBody.Contains("AudioCueLog.Record(\"DUCK\"") && service.Contains("_duck.Step(_config, dt)"),
                  "WIRING (reads source): AudioService.Duck does nothing with the music flag off, writes a DUCK line every time, and the music volume follows the envelope");

            // A music slot is one fader over two sources (the intro and the loop). Fades, the duck and
            // a stinger's stop all go through the fader, so whichever source is sounding follows.
            int stepAt = service.IndexOf("private static void StepFader(", StringComparison.Ordinal);
            int stopAt = service.IndexOf("public void StopMusic(", StringComparison.Ordinal);
            string stepBody = stepAt >= 0 ? service.Substring(stepAt, Math.Min(900, service.Length - stepAt)) : "";
            string stopBody = stopAt >= 0 ? service.Substring(stopAt, Math.Min(400, service.Length - stopAt)) : "";
            Check(stepBody.Contains("f.Source.volume = volume;") && stepBody.Contains("f.Lead.volume = volume;") && stepBody.Contains("f.Lead.Stop();")
                  && stopBody.Contains("BeginFade(_music[i], 0f, fadeSeconds)"),
                  "WIRING (reads source): the intro and the loop of a music slot share one fader, so a stinger's stop, a crossfade and the duck all reach whichever source is sounding");

            int playAt = service.IndexOf("public bool Play(string cueId, float volumeScale)", StringComparison.Ordinal);
            int playEnd = playAt < 0 ? -1 : service.IndexOf("public bool Play(string cueId) {", playAt, StringComparison.Ordinal);
            string playBody = playAt >= 0 && playEnd > playAt ? service.Substring(playAt, playEnd - playAt) : "";
            Check(playBody.Contains("_config.IsSilentNow(def)") && playBody.Contains("_config.EffectiveFiles(def)") && !playBody.Contains("def.IsSilent"),
                  "WIRING (reads source): a stinger obeys the music flag too, so a music one-shot is silent with the flag off");
            Check(playBody.Contains("if (_config.StopsMusic(def)) StopMusic(def.StopMusic);") && playBody.Contains("\"STOP\"") && !playBody.Contains("if (def.StopMusic >= 0f) StopMusic("),
                  "WIRING (reads source): a stinger stops the music only through StopsMusic, and a silent stinger writes a STOP skipped line");

            int skillAt = vfx.IndexOf("public void SkillMoment(", StringComparison.Ordinal);
            int skillEnd = skillAt < 0 ? -1 : vfx.IndexOf("// ----", skillAt, StringComparison.Ordinal);
            string skillBody = skillAt >= 0 && skillEnd > skillAt ? vfx.Substring(skillAt, skillEnd - skillAt) : "";
            int gateAt = skillBody.IndexOf("if (!Juice.On) return;", StringComparison.Ordinal);
            int duckCall = skillBody.IndexOf(".Audio.Duck(\"skill\"", StringComparison.Ordinal);
            Check(gateAt >= 0 && duckCall > gateAt && skillBody.Contains("JuiceMath.DimSeconds(p)"),
                  "WIRING (reads source): the skill moment ducks the music for the length of its backdrop dim, behind the Juice.On gate");

            int victoryAt = sfx.IndexOf("public void Victory()", StringComparison.Ordinal);
            int defeatAt = victoryAt < 0 ? -1 : sfx.IndexOf("public void Defeat()", victoryAt, StringComparison.Ordinal);
            string victoryBody = victoryAt >= 0 && defeatAt > victoryAt ? sfx.Substring(victoryAt, defeatAt - victoryAt) : "";
            int vDuck = victoryBody.IndexOf("Service.Duck(\"victory\"", StringComparison.Ordinal);
            int vStinger = victoryBody.IndexOf("Service.Play(\"music.stinger.victory\")", StringComparison.Ordinal);
            Check(vDuck >= 0 && vStinger > vDuck, "WIRING (reads source): the victory ducks the music, then plays the stinger that ends the battle track");

            Check(battle.Contains("AudioCueConfig.CampaignBattleKind(_stage)") && battle.Contains("return \"prologue\";") && battle.Contains("return \"guild\";"),
                  "WIRING (reads source): BattleMusicKind names the chapter through AudioCueConfig.CampaignBattleKind and keeps its prologue, guild, proving and recital kinds");

            string shots = ReadIfThere(Path.Combine(root, "Assets", "Editor", "PlayModeScreenshot.cs"));
            Check(shots.Contains("name == \"AudioRoundTrip\"") && shots.Contains("PumpAudioRoundTrip(") && !shots.Contains("Begin(\"Screen:AudioRoundTrip"),
                  "WIRING (reads source): the AudioRoundTrip capture target (hub, map, fight, hub) exists and has no literal Begin call, so the nightly does not sweep it");

            Check(credits.Contains("Suno") && credits.IndexOf("no music", StringComparison.OrdinalIgnoreCase) < 0,
                  "credits.txt credits the music to the owner, made with Suno, and no longer says there is no music");
        }

        // ------------------------------------------------------------------ credits

        private static void CheckAudioCredits(string root, string audioRoot, string contentDir, string jsonText)
        {
            string creditsPath = Path.Combine(contentDir, "credits.txt");
            string credits = File.Exists(creditsPath) ? File.ReadAllText(creditsPath) : "";
            // The owner named exactly two packs: every spell and summon folder is Combat Magic Spells, the UI folder is the other.
            const string spellPack = "\"Combat Magic Spells\" by AD Sounds";
            const string uiPack = "\"Fantasy Inventory & UI Sounds Pack\" by Daydream Sound";
            Check(credits.Contains(spellPack) && credits.Contains(uiPack), "credits.txt names the two packs and their makers: Combat Magic Spells by AD Sounds, Fantasy Inventory & UI Sounds Pack by Daydream Sound");
            Check(credits.IndexOf("UNKAI", StringComparison.OrdinalIgnoreCase) < 0, "credits.txt does not credit UNKAI SOUND (never bought)");

            List<string> uncredited = new List<string>();
            string sfxRoot = Path.Combine(audioRoot, "sfx");
            if (Directory.Exists(sfxRoot))
            {
                foreach (string dir in Directory.GetDirectories(sfxRoot))
                {
                    string pack = Path.GetFileName(dir);
                    bool ui = pack == "Fantasy Inventory & UI Sounds Pack";
                    bool spell = Regex.IsMatch(pack, @"(Spell|Summon|Cast|Crystal|Frost|Thunder)[A-Za-z]*v\d+$");
                    if (!ui && !spell) uncredited.Add(pack);
                }
            }
            Check(uncredited.Count == 0, "every sound folder in use belongs to one of the two credited packs" + (uncredited.Count > 0 ? ": UNKNOWN " + string.Join(", ", uncredited.ToArray()) : ""));

            string settingsPath = Path.Combine(root, "Assets", "Scripts", "UI", "Screens", "SettingsScreen.cs");
            string settings = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : "";
            Check(settings.Contains("Content/credits") && settings.Contains("\"CREDITS\""), "WIRING (reads source): the settings screen has a Credits card that shows credits.txt");

            char emDash = (char)0x2014, enDash = (char)0x2013;
            Check(jsonText.IndexOf(emDash) < 0 && jsonText.IndexOf(enDash) < 0 && credits.IndexOf(emDash) < 0 && credits.IndexOf(enDash) < 0,
                  "no em or en dash in audio.json or credits.txt");
        }
    }
}
