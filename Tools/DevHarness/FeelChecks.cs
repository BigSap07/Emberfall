using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Emberfall.Battle;
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
    }
}
