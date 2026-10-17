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
            "music.hub", "music.map", "music.battle", "music.battle.boss", "music.summon", "music.recital",
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
            CheckAudioWiring(cfg, root);
            CheckAudioCredits(root, audioRoot, contentDir, jsonText);
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
                  + " music cues are silent while audio-in holds no music)" + (musicFromSfx.Count > 0 ? ": " + string.Join(", ", musicFromSfx.ToArray()) : ""));

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
            Check(different.Count == 0, "every copied file is byte for byte the owner's file in audio-in (" + compared + " compared)"
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
            Check(cfg.SfxVoices <= 12 && cfg.UiVoices <= 6 && cfg.PerCueVoices <= 4,
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
        }

        // ------------------------------------------------------------------ credits

        private static void CheckAudioCredits(string root, string audioRoot, string contentDir, string jsonText)
        {
            string creditsPath = Path.Combine(contentDir, "credits.txt");
            string credits = File.Exists(creditsPath) ? File.ReadAllText(creditsPath) : "";
            Check(credits.Length > 0 && credits.Contains("UNKAI SOUND"), "credits.txt exists and credits UNKAI SOUND");

            List<string> uncredited = new List<string>();
            string sfxRoot = Path.Combine(audioRoot, "sfx");
            if (Directory.Exists(sfxRoot))
            {
                foreach (string dir in Directory.GetDirectories(sfxRoot))
                {
                    string pack = Path.GetFileName(dir);
                    string family = Regex.Replace(pack, @"v\d+$", "");
                    if (!credits.Contains(family)) uncredited.Add(pack);
                }
            }
            Check(uncredited.Count == 0, "every sound pack folder in use is named in credits.txt" + (uncredited.Count > 0 ? ": NOT NAMED " + string.Join(", ", uncredited.ToArray()) : ""));

            string settingsPath = Path.Combine(root, "Assets", "Scripts", "UI", "Screens", "SettingsScreen.cs");
            string settings = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : "";
            Check(settings.Contains("Content/credits") && settings.Contains("\"CREDITS\""), "WIRING (reads source): the settings screen has a Credits card that shows credits.txt");

            char emDash = (char)0x2014, enDash = (char)0x2013;
            Check(jsonText.IndexOf(emDash) < 0 && jsonText.IndexOf(enDash) < 0 && credits.IndexOf(emDash) < 0 && credits.IndexOf(enDash) < 0,
                  "no em or en dash in audio.json or credits.txt");
        }
    }
}
