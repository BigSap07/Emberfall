using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Emberfall.Battle;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Json;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE R15 FEEL PASS (Docs/DESIGN-JUICE.md), as checks: the switch and the rollback.
    ///
    /// "feelPreset": "v1" in vfx.json restores the R12 battle exactly, and a missing key reads "v1".
    /// What is proved here is everything around the picture that can be computed or read: the shipped
    /// preset and numbers, that v1 is the R12 numbers field by field and the R12 source lines are
    /// still there, that every game-code line that reaches the pass sits behind Feel.V2, that the
    /// simulation never mentions the pass, and the numbers of each v2 behaviour. How it looks is for
    /// the capture batch (Screen:BattleMoment=<stage>.<kind>.v1 and .v2).
    /// </summary>
    internal static partial class Program
    {
        private static void CheckFeel(ContentDatabase db, string contentDir, string jsonText, VfxConfig cfg, string root)
        {
            Line("");
            Line("FEEL (R15 feel pass: vfx.json feelPreset and v2 block, v1 restores R12, simulation untouched)");

            CheckFeelConfig(cfg, jsonText);
            CheckFeelRollback(jsonText);
            CheckFeelFlash(cfg, root);
            CheckFeelRestraint(cfg, root);
            CheckFeelLooks(db, cfg, root);
            CheckFeelCues(cfg, contentDir, root);
            CheckFeelSound(cfg, contentDir, root);
            CheckFeelSimulationUntouched(root);
            CheckFeelHookTripwire(root);
            CheckFeelWiring(root);
        }

        // ------------------------------------------------------------------ FE1 config

        private static void CheckFeelConfig(VfxConfig cfg, string jsonText)
        {
            Check(cfg.IsUsable && cfg.Warnings.Count == 0 && cfg.Feel.V2 && cfg.FeelV2.V2 && jsonText.Contains("\"feelPreset\": \"v2\""),
                  "FE1 vfx.json loads usable with no warnings and ships feelPreset \"v2\"");
            FeelProfile p = cfg.Feel;
            Check(Near(p.FlashPeakNormal, 0.35f) && Near(p.FlashPeakCrit, 0.55f) && Near(p.FlashSeconds, 0.06f),
                  "FE1 the v2 hit flash is a 35 percent tint (55 on a crit) that lasts 0.06 s");
        }

        // ------------------------------------------------------------------ FE2 rollback

        /// <summary>Public fields of FeelProfile that differ between two profiles.</summary>
        private static List<string> FeelDiff(FeelProfile a, FeelProfile b)
        {
            List<string> diff = new List<string>();
            FieldInfo[] fields = typeof(FeelProfile).GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                object x = fields[i].GetValue(a);
                object y = fields[i].GetValue(b);
                if (!x.Equals(y)) diff.Add(fields[i].Name + " " + x + " vs " + y);
            }
            string sa = string.Join(",", new List<string>(a.SingleSoundCues).ToArray());
            string sb = string.Join(",", new List<string>(b.SingleSoundCues).ToArray());
            if (sa != sb) diff.Add("SingleSoundCues " + sa + " vs " + sb);
            return diff;
        }

        private static void CheckFeelRollback(string jsonText)
        {
            FeelProfile v1 = FeelProfile.V1;
            string asV1 = jsonText.Replace("\"feelPreset\": \"v2\"", "\"feelPreset\": \"v1\"");
            string noKey = jsonText.Replace("\"feelPreset\": \"v2\",", "");
            string odd = jsonText.Replace("\"feelPreset\": \"v2\"", "\"feelPreset\": 2");
            string shout = jsonText.Replace("\"feelPreset\": \"v2\"", "\"feelPreset\": \"V3\"");

            List<string> d1 = FeelDiff(VfxConfig.FromJson(asV1).Feel, v1);
            List<string> d2 = FeelDiff(VfxConfig.FromJson(noKey).Feel, v1);
            List<string> d3 = FeelDiff(VfxConfig.FromJson(null).Feel, v1);
            List<string> d4 = FeelDiff(VfxConfig.FromJson("{ \"types\": { } }").Feel, v1);
            List<string> d5 = FeelDiff(VfxConfig.FromJson(odd).Feel, v1);
            List<string> d6 = FeelDiff(VfxConfig.FromJson(shout).Feel, v1);
            Check(asV1 != jsonText && noKey != jsonText && !v1.V2
                  && d1.Count == 0 && d2.Count == 0 && d3.Count == 0 && d4.Count == 0 && d5.Count == 0 && d6.Count == 0,
                  "FE2 ROLLBACK: \"feelPreset\": \"v1\", a missing key, an absent file, an absent vfx.json block, a key that is not a string and an unknown word all parse to exactly the V1 profile, field by field"
                  + (d1.Count + d2.Count + d3.Count + d4.Count + d5.Count + d6.Count > 0 ? " (differs: " + string.Join("; ", d1.ToArray()) + ")" : ""));

            // A capture or an env var may ask for v2 while the file says v1: the v2 block is still read.
            VfxConfig asked = VfxConfig.FromJson(asV1);
            Check(!asked.Feel.V2 && asked.FeelV2.V2 && Near(asked.FeelV2.FlashPeakNormal, 0.35f),
                  "FE2 with the file at v1 the v2 numbers are still parsed, so a .v2 capture or EMBERFALL_FEEL=v2 can show them");

            // The block overrides field by field; what it leaves out keeps the shipped value.
            VfxConfig partial = VfxConfig.FromJson("{ \"feelPreset\": \"v2\", \"v2\": { \"flash\": { \"peakNormal\": 0.5 } } }");
            VfxConfig bare = VfxConfig.FromJson("{ \"feelPreset\": \"v2\" }");
            Check(partial.Feel.V2 && Near(partial.Feel.FlashPeakNormal, 0.5f) && Near(partial.Feel.FlashPeakCrit, 0.55f) && Near(partial.Feel.FlashSeconds, 0.06f)
                  && FeelDiff(bare.Feel, FeelProfile.Shipped).Count == 0,
                  "FE2 anything the v2 block leaves out keeps the shipped value, and no block at all is the shipped profile");

            // V1 holds the R12 numbers it replaces.
            JuiceProfile r12 = JuiceProfile.Juiced;
            Check(Near(v1.FlashPeakNormal, r12.FlashPeakNormal) && Near(v1.FlashPeakCrit, r12.FlashPeakCrit) && Near(v1.FlashSeconds, r12.FlashSeconds),
                  "FE2 V1 holds the R12 flash (full white, " + Fmt(r12.FlashSeconds) + " s), field for field the Juiced profile");

            // The preset needs the R12 pass: juice off is the pre-R12 battle, whatever the preset.
            string src = ReadJuiceSource(Path.Combine(FeelRoot(), "Assets", "Scripts"), "Battle", "Feel.cs");
            Check(Regex.IsMatch(src, @"public static bool V2 \{ get \{ return Juice\.On && Profile\.V2; \} \}"),
                  "FE2 WIRING (reads source): Feel.V2 is Juice.On and the profile, so the R12 rollback takes the feel pass with it");
        }

        private static string FeelRoot()
        {
            return Path.GetFullPath(Path.Combine(FindToolDir("DevHarness"), "..", ".."));
        }

        // ------------------------------------------------------------------ FE3 hit flash

        private static void CheckFeelFlash(VfxConfig cfg, string root)
        {
            FeelProfile p = cfg.Feel;
            JuiceProfile r12 = cfg.Juice;
            float normalEnd = FeelMath.FlashAt(p, false, p.FlashSeconds);
            float critEnd = FeelMath.FlashAt(p, true, p.FlashSeconds);
            Check(Near(FeelMath.FlashAt(p, false, 0f), 0.35f) && Near(FeelMath.FlashAt(p, true, 0f), 0.55f)
                  && normalEnd == 0f && critEnd == 0f && FeelMath.FlashAt(p, false, p.FlashSeconds * 0.5f) > 0f,
                  "FE3 FLASH: a hit starts at a 0.35 lerp toward white (0.55 on a crit) and is gone at exactly " + Fmt(p.FlashSeconds) + " s, crit included");
            Check(FeelMath.FlashPeak(p, false) < 0.5f && FeelMath.FlashPeak(p, true) < 0.7f && FeelMath.FlashPeak(p, true) > FeelMath.FlashPeak(p, false)
                  && FeelMath.FlashPeak(p, false) < r12.FlashPeakNormal && FeelMath.FlashPeak(p, true) < r12.FlashPeakCrit,
                  "FE3 the white-out is gone: even a crit tints under " + Fmt(0.7f) + " where R12 filled to " + Fmt(r12.FlashPeakCrit));

            // Hit stop must never hold the flash. A frame loop: the stop freezes motion for its whole
            // length, the flash still falls on the play clock, and is gone by FlashSeconds, which is
            // inside the longest stop of a crit (0.09) or a skill (0.12).
            float dt = 1f / 60f;
            float flash = FeelMath.FlashPeak(p, true);
            float rate = FeelMath.FlashDecayRate(p, true);
            float t = 0f;
            while (flash > 0f && t < 1f) { flash = Math.Max(0f, flash - dt * rate); t += dt; }
            float longestStop = JuiceMath.HitStopMax(cfg.Juice);
            Check(t <= p.FlashSeconds + dt + 0.0001f && p.FlashSeconds <= longestStop,
                  "FE3 HIT STOP: a crit flash falls to zero in " + Fmt(t) + " s at 60 fps, inside the longest hit stop (" + Fmt(longestStop) + " s), so a stop never shows a held flash");

            // And the actor does it in that order: the flash falls before the freeze test.
            string actor = ReadJuiceSource(Path.Combine(root, "Assets", "Scripts"), "Battle", "UnitActor.cs");
            int fall = actor.IndexOf("if (_flashFree && _flash > 0f)", StringComparison.Ordinal);
            int freeze = actor.IndexOf("if (_hitStop > 0f)", StringComparison.Ordinal);
            int legacy = actor.IndexOf("if (_flash > 0f && !_flashFree)", StringComparison.Ordinal);
            Check(fall > 0 && freeze > fall && legacy > freeze && actor.Contains("_flashFree = false;                  // R15: only a blow's flash runs on its own clock"),
                  "FE3 WIRING (reads source): UnitActor lets a blow's flash fall before the hit-stop test, keeps the R12 decay after it, and gives the shield and status flashes back to R12");
        }

        // ------------------------------------------------------------------ FE4 simulation untouched

        private static void CheckFeelSimulationUntouched(string root)
        {
            string core = Path.Combine(root, "Assets", "Scripts", "Core");
            Regex pass = new Regex(@"\bFeel(Profile|Math)\b|\bFeel\.");
            List<string> mentions = new List<string>();
            int scanned = 0;
            foreach (string file in Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (name == "FeelProfile.cs" || name == "VfxConfig.cs") continue;
                scanned++;
                if (pass.IsMatch(File.ReadAllText(file))) mentions.Add(name);
            }
            Check(mentions.Count == 0 && scanned > 0,
                  "FE4 SIMULATION UNTOUCHED (reads source): none of the " + scanned + " Core files other than the profile and its loader mention the feel pass"
                  + (mentions.Count > 0 ? ": " + string.Join(", ", mentions.ToArray()) : "")
                  + " (the fingerprint and sweep baselines then run in this same pass)");
        }

        // ------------------------------------------------------------------ FE5 hook tripwire

        private static readonly Regex FeelHook = new Regex(@"(\bFeel\.|\bFeelMath\.)");

        /// <summary>
        /// FE5. Every line of game code that reaches the feel pass either names Feel.V2, or sits in a
        /// block that Feel.V2 opened (an if or its else), or in a method whose first statement is
        /// if (!Feel.V2) return. A hook that is not one of those would run with the preset at v1, and
        /// then "v1 is R12" is a hope. Same rule as J5 for the R12 pass, with the other flag.
        /// </summary>
        private static void CheckFeelHookTripwire(string root)
        {
            string scripts = Path.Combine(root, "Assets", "Scripts");
            int hooks = 0, captureLines = 0;
            List<string> bad = new List<string>();
            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                string rel = file.Substring(scripts.Length + 1).Replace('\\', '/');
                if (rel.StartsWith("Core/", StringComparison.Ordinal)) continue;
                if (name == "Feel.cs" || name == "FeelMath.cs") continue;
                string[] lines = File.ReadAllText(file).Replace("\r\n", "\n").Split('\n');
                bool[] guarded = JuiceGuardedLines(lines, "Feel.V2");
                bool editorOnly = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = JuiceCodeOf(lines[i]);
                    string trimmed = code.Trim();
                    if (trimmed.StartsWith("#if UNITY_EDITOR", StringComparison.Ordinal)) editorOnly = true;
                    else if (editorOnly && trimmed.StartsWith("#endif", StringComparison.Ordinal)) editorOnly = false;
                    if (code.Length == 0 || !FeelHook.IsMatch(code)) continue;
                    if (editorOnly) { captureLines++; continue; }
                    hooks++;
                    if (code.Contains("Feel.V2") || guarded[i]) continue;
                    bad.Add(name + ":" + (i + 1) + " " + code.Trim());
                }
            }
            Check(bad.Count == 0 && hooks > 0,
                  "FE5 TRIPWIRE (reads source): every game-code line that reaches the feel pass is behind Feel.V2 (" + hooks + " hook lines, plus " + captureLines + " read-only capture lines inside #if UNITY_EDITOR)"
                  + (bad.Count > 0 ? ": UNGUARDED " + string.Join(" | ", bad.ToArray()) : ""));
        }

        // ------------------------------------------------------------------ FE6 wiring

        private static void CheckFeelWiring(string root)
        {
            string scripts = Path.Combine(root, "Assets", "Scripts");
            string feel = ReadJuiceSource(scripts, "Battle", "Feel.cs");
            Check(feel.Contains("\"EMBERFALL_FEEL\"") && feel.Contains("Application.isEditor || Debug.isDebugBuild") && feel.Contains("public static void ForceForCapture(bool? v2)"),
                  "FE6 WIRING (reads source): EMBERFALL_FEEL overrides the preset in the editor and development builds only, and Feel.ForceForCapture is the capture seam");

            string shots = File.Exists(Path.Combine(root, "Assets", "Editor", "PlayModeScreenshot.cs"))
                ? File.ReadAllText(Path.Combine(root, "Assets", "Editor", "PlayModeScreenshot.cs")) : "";
            Check(shots.Contains("MomentHasToken(momentSpec, \"v1\")") && shots.Contains("MomentHasToken(momentSpec, \"v2\")")
                  && Regex.Matches(shots, @"Feel\.ForceForCapture\(null\);").Count >= 4,
                  "FE6 WIRING (reads source): Screen:BattleMoment takes a .v1 and a .v2 suffix, and every place that resets the juice capture override resets the feel one too");
        }

        // ------------------------------------------------------------------ FE7 restraint and the per-unit cap

        private static string FeelSlice(string text, string from, string to)
        {
            int a = text.IndexOf(from, StringComparison.Ordinal);
            if (a < 0) return "";
            int b = to == null ? -1 : text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
            return b < 0 ? text.Substring(a) : text.Substring(a, b - a);
        }

        private static void CheckFeelRestraint(VfxConfig cfg, string root)
        {
            FeelProfile p = cfg.Feel;
            Check(p.VfxMaxPerUnit == 2 && p.VfxMaxLive >= 4 && p.VfxMaxLive < cfg.Juice.MaxQuads,
                  "FE7 the v2 caps: " + p.VfxMaxPerUnit + " effects at once per unit, " + p.VfxMaxLive + " live in all (on top of the R12 quad cap of " + cfg.Juice.MaxQuads + ")");

            // The cap decision: free below the cap, a stronger class retires the oldest weaker one,
            // a weaker one is dropped, no cap is no cap.
            List<int> pr = new List<int>();
            List<float> born = new List<float>();
            Func<int, int[], float[], int> decide = (prio, live, at) =>
            {
                pr.Clear(); born.Clear();
                for (int i = 0; i < live.Length; i++) { pr.Add(live[i]); born.Add(at[i]); }
                return FeelMath.CapDecision(2, pr, born, prio);
            };
            int hit = FeelMath.ClassPriority("hit"), crit = FeelMath.ClassPriority("crit"), skill = FeelMath.ClassPriority("skill");
            bool order = skill > crit && crit > hit && FeelMath.ClassPriority("heal") == hit && FeelMath.ClassPriority("shield") == hit;
            bool free = decide(hit, new int[] { hit }, new float[] { 0f }) == -1 && decide(hit, new int[0], new float[0]) == -1;
            bool evictOldest = decide(hit, new int[] { hit, hit }, new float[] { 5f, 3f }) == 1;
            bool evictWeakest = decide(crit, new int[] { crit, hit }, new float[] { 1f, 9f }) == 1;
            bool dropped = decide(hit, new int[] { crit, skill }, new float[] { 1f, 2f }) == -2;
            bool strongWins = decide(skill, new int[] { crit, crit }, new float[] { 4f, 2f }) == 1;
            bool noCap = FeelMath.CapDecision(0, new int[] { 3, 3, 3 }, new float[] { 0f, 0f, 0f }, 1) == -1;
            Check(order && free && evictOldest && evictWeakest && dropped && strongWins && noCap,
                  "FE7 CAP DECISION: skill beats crit beats the rest; below the cap an effect starts, at it a stronger class retires the oldest weaker one, a weaker one is dropped, and 0 is no cap");

            // Under any schedule of spawns a unit never holds more than the cap.
            Random rng = new Random(15015);
            List<int> livePr = new List<int>();
            List<float> liveBorn = new List<float>();
            List<float> liveEnd = new List<float>();
            int over = 0, spawns = 0, retired = 0, refused = 0;
            string[] kinds = { "hit", "hit", "hit", "crit", "skill", "heal", "buff", "debuff", "shield" };
            for (float now = 0f; now < 30f; now += 0.03f)
            {
                for (int i = liveEnd.Count - 1; i >= 0; i--) if (liveEnd[i] <= now) { livePr.RemoveAt(i); liveBorn.RemoveAt(i); liveEnd.RemoveAt(i); }
                int burst = rng.Next(4);
                for (int b = 0; b < burst; b++)
                {
                    string k = kinds[rng.Next(kinds.Length)];
                    int d = FeelMath.CapDecision(p.VfxMaxPerUnit, livePr, liveBorn, FeelMath.ClassPriority(k));
                    spawns++;
                    if (d == -2) { refused++; continue; }
                    if (d >= 0) { livePr.RemoveAt(d); liveBorn.RemoveAt(d); liveEnd.RemoveAt(d); retired++; }
                    livePr.Add(FeelMath.ClassPriority(k));
                    liveBorn.Add(now);
                    liveEnd.Add(now + 0.15f + (float)rng.NextDouble() * 0.6f);
                    if (livePr.Count > p.VfxMaxPerUnit) over++;
                }
            }
            Check(over == 0 && spawns > 500 && retired > 0,
                  "FE7 CAP: " + spawns + " random effect starts on one unit never leave more than " + p.VfxMaxPerUnit + " alive (" + retired + " retired for a stronger one, " + refused + " dropped)");

            // Restraint, read from the source: the v2 look code draws no ring and no streak, and the R12
            // calls it steps around are still in the source behind Feel.V2.
            string scripts = Path.Combine(root, "Assets", "Scripts");
            string director = ReadJuiceSource(scripts, "Battle", "VfxDirector.cs");
            string region = FeelSlice(director, "// ------------------------------------------------------------------ R15 v2: one look per class", "// ------------------------------------------------------------------ resolution");
            string[] banned = { "EmitRing(", "EmitStreaks(", "RingTexture", "BattleAssets.Streak", ".Slash(", "Direction.Radial" };
            List<string> found = new List<string>();
            for (int i = 0; i < banned.Length; i++) if (region.Contains(banned[i])) found.Add(banned[i]);
            Check(region.Length > 2000 && found.Count == 0,
                  "FE7 RESTRAINT (reads source): the v2 look code draws no ring, no streak and no slash (" + region.Length + " characters read)" + (found.Count > 0 ? ": FOUND " + string.Join(", ", found.ToArray()) : ""));

            string battle = ReadJuiceSource(scripts, "UI", "Screens", "BattleScreen.cs");
            string[] stillThere =
            {
                "if (hit != null && !Feel.V2) PlayAbilityVfx(e.Vfx, source, hit, AspectTint(source));",
                "&& !Feel.V2) _world.Vfx.Impact(target.CenterPosition, AspectTint(source), e.Critical ? 1.3f : 0.9f);",
                "if (!Feel.V2) _world.Vfx.Aura(source.transform.position, AspectTint(source), 2.2f);",
                "if (!Feel.V2) _world.Vfx.Motes(target.transform.position, UITheme.Success, 6, 2.2f);",
                "if (!Feel.V2) _world.Vfx.Aura(target.transform.position, UITheme.Shield, 2f);",
                "if (!Feel.V2) _world.Vfx.Aura(target.transform.position, tint, 1.8f);"
            };
            List<string> lost = new List<string>();
            for (int i = 0; i < stillThere.Length; i++) if (!battle.Contains(stillThere[i])) lost.Add(stillThere[i]);
            Check(lost.Count == 0 && Regex.IsMatch(battle, @"private void PlayTargetedVfx\(string vfx, UnitActor source, List<int> targetIds, Color colour\)\s*\{[^}]*if \(Feel\.V2\) return;"),
                  "FE7 ROLLBACK (reads source): the R12 slash, impact, cast ring, motes and auras are still in BattleScreen, each behind !Feel.V2" + (lost.Count > 0 ? ": GONE " + string.Join(" | ", lost.ToArray()) : ""));
            string oldDirector = "EmitRing(point, shield, 2.0f, 0.34f);" + "|" + "EmitRing(point, tint, harmful ? 0.9f : 1.1f, 0.30f);" + "|" + "EmitRing(point, tint, 1.8f, 0.30f);" + "|" + "_stage.Vfx.Glow(behind, accent, p.RingRadius * 0.75f, p.RingSeconds, 1f, true, true);";
            List<string> goneDirector = new List<string>();
            foreach (string line in oldDirector.Split('|')) if (!director.Contains(line)) goneDirector.Add(line);
            Check(goneDirector.Count == 0,
                  "FE7 ROLLBACK (reads source): the R12 shield, status, crit and skill rings are still in VfxDirector" + (goneDirector.Count > 0 ? ": GONE " + string.Join(" | ", goneDirector.ToArray()) : ""));
        }

        // ------------------------------------------------------------------ FE8 one look per class

        private static void CheckFeelLooks(ContentDatabase db, VfxConfig cfg, string root)
        {
            FeelProfile p = cfg.Feel;
            float skillSeconds = p.SkillFrames / Math.Max(1f, p.SkillFps);
            Check(p.HitPuffSize < p.CritPuffSize && p.CritPuffSize < p.SkillScale && p.HitPuffSeconds < p.CritPuffSeconds && p.CritPuffSeconds < skillSeconds,
                  "FE8 LOOKS: a basic puff (" + Fmt(p.HitPuffSize) + ", " + Fmt(p.HitPuffSeconds) + " s) is smaller and faster than a crit (" + Fmt(p.CritPuffSize) + ", " + Fmt(p.CritPuffSeconds)
                  + " s), which is smaller and faster than the skill burst (" + Fmt(p.SkillScale) + ", " + Fmt(skillSeconds) + " s)");
            Check(p.PuffWhite >= 0f && p.PuffWhite <= 0.6f && p.PulseTravel > 0f && p.ArrowTravel > 0f && p.ShellSeconds > 0f && p.ShellSeconds < 0.5f && p.HealSparks > 0,
                  "FE8 the buff and debuff pulses travel, the shield shell is a short flash (" + Fmt(p.ShellSeconds) + " s), and the heal throws sparks (" + p.HealSparks + ")");

            // The skill burst is one of the existing sheets, the grid holds its frames, and nothing is
            // element-literal: a neutral burst, tinted by the attacker's aspect.
            string sheetsDir = Path.Combine(root, "Assets", "Resources", "Art", "Vfx");
            string sheetFile = Path.Combine(sheetsDir, Path.GetFileName(p.SkillSheet) + ".png");
            string[] six = { "bolt_crackle", "burst_puff", "heal_ring", "impact_spikes", "shadow_gather", "slash_sweep" };
            bool isOne = Array.IndexOf(six, Path.GetFileName(p.SkillSheet)) >= 0 && p.SkillSheet.StartsWith("Art/Vfx/", StringComparison.Ordinal);
            bool inGrid = p.SkillColumns * p.SkillRows >= p.SkillFrames && p.SkillFrames > 0 && p.SkillFps > 0f;
            Check(isOne && File.Exists(sheetFile) && inGrid,
                  "FE8 the skill burst is " + p.SkillSheet + ", one of the six existing sheets (file on disk), with " + p.SkillFrames + " frames in a " + p.SkillColumns + "x" + p.SkillRows + " grid");

            // The moment kinds the tester shoots, one per class, and where the campaign holds them.
            List<JuiceFight> fights = JuiceFights(db);
            MomentKind[] classKinds = { MomentKind.Hit, MomentKind.Crit, MomentKind.Skill, MomentKind.Heal, MomentKind.Buff, MomentKind.Debuff, MomentKind.Shield, MomentKind.Death };
            int[] total = new int[classKinds.Length];
            int[] early = new int[classKinds.Length];
            for (int f = 0; f < fights.Count; f++)
            {
                StageDef stage = db.AllStages[f];
                MomentKind[] all = JuiceMath.ClassifyAll(fights[f].Result.Events);
                for (int i = 0; i < all.Length; i++)
                {
                    int k = Array.IndexOf(classKinds, all[i]);
                    if (k < 0) continue;
                    total[k]++;
                    if (stage.ChapterNumber >= 1 && stage.ChapterNumber <= 2) early[k]++;
                }
            }
            List<string> absent = new List<string>();
            StringBuilder counts = new StringBuilder();
            for (int k = 0; k < classKinds.Length; k++)
            {
                if (total[k] == 0) absent.Add(classKinds[k].ToString());
                counts.Append(classKinds[k]).Append(' ').Append(total[k]).Append(" (").Append(early[k]).Append(" in chapters 1 and 2)");
                if (k + 1 < classKinds.Length) counts.Append(", ");
            }
            Line("         moments by class across the campaign openings: " + counts);
            Check(absent.Count == 0,
                  "FE8 the record holds every one of the eight classes, so one capture per class can be shot" + (absent.Count > 0 ? " (absent: " + string.Join(", ", absent.ToArray()) + ")" : ""));

            bool mapped = true;
            for (int k = 0; k < classKinds.Length; k++) if (Array.IndexOf(FeelMath.Classes, FeelMath.ClassOf(classKinds[k])) < 0) mapped = false;
            BattleEvent ward = BattleEvent.Make(BattleEventType.StatusApplied, 1);
            ward.Status = StatusKind.Shield;
            BattleEvent haste = BattleEvent.Make(BattleEventType.StatusApplied, 1);
            haste.Status = StatusKind.AttackUp;
            BattleEvent weak = BattleEvent.Make(BattleEventType.StatusApplied, 1);
            weak.Status = StatusKind.AttackDown;
            BattleEvent gain = BattleEvent.Make(BattleEventType.ShieldGain, 1);
            gain.Value = 20;
            Check(mapped && JuiceMath.Classify(ward, false) == MomentKind.None && JuiceMath.Classify(haste, false) == MomentKind.Buff
                  && JuiceMath.Classify(weak, false) == MomentKind.Debuff && JuiceMath.Classify(gain, false) == MomentKind.Shield,
                  "FE8 a ward is a Shield moment and never a Buff, AttackUp is a Buff, AttackDown a Debuff, and each of the eight kinds maps to a class");

            string shots = File.Exists(Path.Combine(root, "Assets", "Editor", "PlayModeScreenshot.cs"))
                ? File.ReadAllText(Path.Combine(root, "Assets", "Editor", "PlayModeScreenshot.cs")) : "";
            Check(shots.Contains("p == \"buff\"") && shots.Contains("p == \"debuff\"") && shots.Contains("p == \"shield\"") && shots.Contains("with kind hit, crit, skill, heal, buff, debuff, shield or death"),
                  "FE8 WIRING (reads source): Screen:BattleMoment takes the kinds buff, debuff and shield");
        }

        // ------------------------------------------------------------------ FE9 one cue per class

        private static void CheckFeelCues(VfxConfig cfg, string contentDir, string root)
        {
            string audioPath = Path.Combine(contentDir, "audio.json");
            AudioCueConfig audio = AudioCueConfig.FromJson(File.Exists(audioPath) ? File.ReadAllText(audioPath) : null);
            string[] aspects = { "ember", "tide", "verdant", "gale", "umbra", "lumen" };
            List<string> classCues = new List<string> { "battle.hit", "battle.crit", "battle.heal", "battle.buff", "battle.debuff", "battle.shield", "battle.death" };
            for (int i = 0; i < aspects.Length; i++) classCues.Add("battle.impact." + aspects[i]);

            // Each class has its own cue, and no two classes share a main file: a class cannot borrow another's sound.
            List<string> missing = new List<string>();
            Dictionary<string, string> owner = new Dictionary<string, string>();
            List<string> shared = new List<string>();
            for (int i = 0; i < classCues.Count; i++)
            {
                AudioCueDef def = audio.Cue(classCues[i]);
                if (def == null || def.Files.Count == 0) { missing.Add(classCues[i]); continue; }
                string cls = FeelMath.CueClass(classCues[i]);
                for (int f = 0; f < def.Files.Count; f++)
                {
                    string other;
                    if (owner.TryGetValue(def.Files[f], out other) && other != cls) shared.Add(def.Files[f] + " (" + other + " and " + cls + ")");
                    else owner[def.Files[f]] = cls;
                }
            }
            Check(missing.Count == 0 && shared.Count == 0,
                  "FE9 CUES: the eight classes each play their own cue from audio.json (battle.hit, crit, impact per aspect, heal, buff, debuff, shield, death), and no two classes share a main file"
                  + (missing.Count > 0 ? " (no cue: " + string.Join(", ", missing.ToArray()) + ")" : "") + (shared.Count > 0 ? " (shared: " + string.Join("; ", shared.ToArray()) + ")" : ""));

            Check(FeelMath.CueClass("battle.hit") == "hit" && FeelMath.CueClass("battle.crit") == "crit" && FeelMath.CueClass("battle.impact.tide") == "skill"
                  && FeelMath.CueClass("battle.cast.umbra") == "cast" && FeelMath.CueClass("battle.cast") == "cast" && FeelMath.CueClass("battle.heal") == "heal"
                  && FeelMath.CueClass("battle.buff") == "buff" && FeelMath.CueClass("battle.debuff") == "debuff" && FeelMath.CueClass("battle.shield") == "shield"
                  && FeelMath.CueClass("battle.death") == "death" && FeelMath.CueClass("ui.tap") == "",
                  "FE9 the cue log's class= tag names the class of every battle cue (hit, crit, skill for battle.impact.*, cast, heal, buff, debuff, shield, death)");

            // The calls: each class event plays its own Sfx call, and a critical skill no longer borrows the crit cue in v2.
            string battle = ReadJuiceSource(Path.Combine(root, "Assets", "Scripts"), "UI", "Screens", "BattleScreen.cs");
            string sfx = ReadJuiceSource(Path.Combine(root, "Assets", "Scripts"), "Game", "Sfx.cs");
            bool calls = Regex.IsMatch(battle, @"SpawnFloatingText\(target\.HeadPosition, ""\+"" \+ e\.Value, UITheme\.Success, 1f\);[^}]*App\.Audio\.Heal\(\);")
                         && Regex.IsMatch(battle, @"App\.Audio\.Shield\(\);") && battle.Contains("if (StatusInfo.IsHarmful(e.Status)) App.Audio.Debuff();")
                         && battle.Contains("else App.Audio.Buff();") && battle.Contains("App.Audio.Death();")
                         && sfx.Contains("Cue(\"battle.heal\"") && sfx.Contains("Cue(\"battle.buff\")") && sfx.Contains("Cue(\"battle.debuff\")")
                         && sfx.Contains("Cue(\"battle.shield\")") && sfx.Contains("Cue(\"battle.death\"") && sfx.Contains("Cue(\"battle.impact.\" + AspectKey(aspect))");
            Check(calls && battle.Contains("if (e.Critical && !Feel.V2) App.Audio.Crit();"),
                  "FE9 WIRING (reads source): heal, shield, buff, debuff, death and skill impact each call their own cue, and in v2 a critical skill plays the skill impact alone");
        }

        // ------------------------------------------------------------------ FE10 numbers and sound

        private static void CheckFeelSound(VfxConfig cfg, string contentDir, string root)
        {
            FeelProfile p = cfg.Feel;
            FeelProfile v1 = FeelProfile.V1;
            Check(Near(p.NumberScale, 1.25f) && Near(FeelMath.NumberScale(p, 1f), 1.25f) && Near(FeelMath.NumberScale(v1, 1.5f), 1.5f),
                  "FE10 NUMBERS: v2 grows every floating number by 1.25 (a crit 1.5 becomes " + Fmt(FeelMath.NumberScale(p, 1.5f)) + "), v1 leaves it alone");

            string audioPath = Path.Combine(contentDir, "audio.json");
            string audioText = File.Exists(audioPath) ? File.ReadAllText(audioPath) : "";
            AudioCueConfig audio = AudioCueConfig.FromJson(audioText);
            Check(p.AudioSfxVoices >= 1 && p.AudioSfxVoices <= 6 && p.AudioSfxVoices < audio.SfxVoices && v1.AudioSfxVoices == 0,
                  "FE10 SOUND: v2 holds the battle to " + p.AudioSfxVoices + " sfx voices at once (audio.json allows " + audio.SfxVoices + " and v1 keeps all of them)");
            AudioCueDef hit = audio.Cue("battle.hit");
            AudioCueDef crit = audio.Cue("battle.crit");
            bool layersStay = hit != null && hit.Layers.Count > 0 && crit != null && crit.Layers.Count > 0;
            bool skillLayers = true;
            string[] aspects = { "ember", "tide", "verdant", "gale", "umbra", "lumen" };
            for (int i = 0; i < aspects.Length; i++)
            {
                AudioCueDef impact = audio.Cue("battle.impact." + aspects[i]);
                if (impact == null || impact.Layers.Count == 0) { skillLayers = false; }
            }
            Check(p.IsSingleSound("battle.hit") && !p.IsSingleSound("battle.crit") && !p.IsSingleSound("battle.impact.ember") && !v1.IsSingleSound("battle.hit")
                  && p.SingleSoundCues.Count == 1 && v1.SingleSoundCues.Count == 0 && layersStay && skillLayers,
                  "FE10 SOUND: in v2 a basic hit plays one sound with its layers skipped; crits and skills keep their layers (battle.hit keeps its layer in audio.json for v1); v1 skips nothing");

            // Music sits under the effects: its effective gain (channel volume times cue volume) is below every battle effect of the eight classes.
            float musicGain = audio.DefaultMusicVolume * 0.5f;
            float quietest = float.MaxValue;
            string quietestCue = "";
            string[] classCues = { "battle.hit", "battle.crit", "battle.heal", "battle.buff", "battle.debuff", "battle.shield", "battle.death" };
            for (int i = 0; i < classCues.Length; i++)
            {
                AudioCueDef def = audio.Cue(classCues[i]);
                if (def == null) continue;
                float gain = audio.DefaultSfxVolume * def.Volume;
                if (gain < quietest) { quietest = gain; quietestCue = classCues[i]; }
            }
            AudioCueDef battleMusic = audio.Cue("music.battle");
            if (battleMusic != null) musicGain = audio.DefaultMusicVolume * battleMusic.Volume;
            Check(battleMusic != null && musicGain < quietest,
                  "FE10 MUSIC UNDER EFFECTS: the battle track plays at " + Fmt(musicGain) + " of full (music " + Fmt(audio.DefaultMusicVolume) + " x cue " + Fmt(battleMusic != null ? battleMusic.Volume : 0f)
                  + "), under the quietest class effect " + quietestCue + " at " + Fmt(quietest));

            string service = ReadJuiceSource(Path.Combine(root, "Assets", "Scripts"), "Game", "AudioService.cs");
            Check(service.Contains("bool alone = Feel.V2 && Feel.Profile.IsSingleSound(cueId);") && service.Contains("l < def.Layers.Count && !alone")
                  && service.Contains("List<Voice> pool = channel == AudioChannel.Ui ? _uiVoices : SfxPool();")
                  && Regex.IsMatch(service, @"private List<Voice> SfxPool\(\)\s*\{\s*if \(!Feel\.V2\) return _sfxVoices;"),
                  "FE10 WIRING (reads source): AudioService skips the layers of a single-sound cue and draws sfx voices from the first few, both only when Feel.V2");
        }
    }
}
