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
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE R12 HIT FEEDBACK PASS (Docs/DESIGN-JUICE.md), as checks.
    ///
    /// The picture cannot be seen here, so what is proved is everything around it that can be
    /// computed or read: the shipped numbers are the ones the prompt names, the rollback is the
    /// old numbers and the old source lines, the simulation never mentions the pass, every damage
    /// event in the campaign lands in exactly one juice kind, the hold happens once per cast, the
    /// curves have the right endpoints, number lanes never overlap at spawn, and the effect cap
    /// holds under the worst schedule. How it looks is for the capture batch.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The camera shake BattleFraming budgets and BattleStage.FitBackdrop pads for.</summary>
        private const float JuiceShakeReserve = 0.18f;

        private static void CheckJuice(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("JUICE (R12 hit feedback: vfx.json juice block behind one flag, simulation untouched)");

            string root = Path.GetFullPath(Path.Combine(contentDir, "..", "..", ".."));
            string jsonPath = Path.Combine(contentDir, "vfx.json");
            if (!File.Exists(jsonPath))
            {
                Check(false, "vfx.json is where the harness expects it");
                return;
            }
            string jsonText = File.ReadAllText(jsonPath);
            VfxConfig cfg = VfxConfig.FromJson(jsonText);

            CheckJuiceConfig(cfg);
            CheckJuiceRollback(jsonText, root);
            CheckJuiceNotHashed(db, contentDir);
            CheckJuiceSimulationUntouched(root);
            CheckJuiceClassification(db, cfg.Juice);
            CheckJuiceCurves(cfg.Juice);
            CheckJuiceLanes(cfg.Juice);
            CheckJuiceCap(cfg.Juice);
            CheckJuiceWiring(root);
        }

        // ------------------------------------------------------------------ J1 config

        private static bool Near(float a, float b) { return Math.Abs(a - b) < 0.0001f; }

        private static void CheckJuiceConfig(VfxConfig cfg)
        {
            JuiceProfile p = cfg.Juice;
            Check(cfg.IsUsable && cfg.Warnings.Count == 0 && p.On,
                  "J1 vfx.json loads usable with no warnings and the juice flag is ON by default");
            Check(Near(p.HitStopNormal, 0.05f) && Near(p.HitStopCrit, 0.09f) && Near(p.HitStopSkill, 0.12f),
                  "J1 hit-stop is 0.05 s normal, 0.09 s crit, 0.12 s skill impact");
            Check(Near(p.FlashPeakNormal, 1f) && Near(p.FlashPeakCrit, 1f) && Near(p.FlashSeconds, 0.08f),
                  "J1 the hit flash is white at full strength and lasts 0.08 s");
            Check(p.ShakeCritStrength > 0f && p.ShakeCritStrength < p.ShakeSkillStrength,
                  "J1 the crit shake is small and the skill shake is medium (" + Fmt(p.ShakeCritStrength) + " under " + Fmt(p.ShakeSkillStrength) + ")");
            Check(Near(p.PopScale, 1.4f) && Near(p.PopSeconds, 0.15f),
                  "J1 damage numbers pop to 1.4x and settle in 0.15 s");
            Check(Near(p.DimTo, 0.70f) && Near(JuiceMath.DimSeconds(p), 0.30f),
                  "J1 the skill moment dims the backdrop to 70 percent for 0.30 s");
            Check(Near(p.KnockPxNormal, 10f) && Near(p.KnockPxSkill, 13f) && Near(p.KnockPxCrit, 16f),
                  "J1 knockback is 10 px on a hit, 13 on a skill, 16 on a crit");
            Check(p.MaxQuads == 40 && p.MaxTotal == 62 && p.MaxQuads + p.MaxFlipbooks + p.MaxNumbers == p.MaxTotal,
                  "J1 the effect cap is 40 quads and 62 drawn things in all (40 + " + p.MaxFlipbooks + " flipbooks + " + p.MaxNumbers + " numbers)");
        }

        // ------------------------------------------------------------------ J2 to J5 rollback

        /// <summary>Public fields of JuiceProfile that differ between two profiles.</summary>
        private static List<string> JuiceDiff(JuiceProfile a, JuiceProfile b)
        {
            List<string> diff = new List<string>();
            FieldInfo[] fields = typeof(JuiceProfile).GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                object x = fields[i].GetValue(a);
                object y = fields[i].GetValue(b);
                if (!x.Equals(y)) diff.Add(fields[i].Name + " " + x + " vs " + y);
            }
            return diff;
        }

        private static void CheckJuiceRollback(string jsonText, string root)
        {
            JuiceProfile legacy = JuiceProfile.Legacy;
            string off = jsonText.Replace("\"enabled\": true", "\"enabled\": false");
            List<string> offDiff = JuiceDiff(VfxConfig.FromJson(off).Juice, legacy);
            List<string> nullDiff = JuiceDiff(VfxConfig.FromJson(null).Juice, legacy);
            List<string> absentDiff = JuiceDiff(VfxConfig.FromJson("{ \"types\": { } }").Juice, legacy);
            List<string> nodeDiff = JuiceDiff(JuiceProfile.FromJson(null), legacy);
            Check(off != jsonText && !legacy.On && offDiff.Count == 0 && nullDiff.Count == 0 && absentDiff.Count == 0 && nodeDiff.Count == 0,
                  "J2 ROLLBACK: \"enabled\": false, an absent juice block, an absent file and a null node all parse to exactly the Legacy profile, field by field"
                  + (offDiff.Count + nullDiff.Count + absentDiff.Count + nodeDiff.Count > 0 ? " (differs: " + string.Join("; ", offDiff.ToArray()) + ")" : ""));
            Check(JuiceDiff(JuiceProfile.FromJson(JsonValue.Parse("{ \"enabled\": true }")), JuiceProfile.Juiced).Count == 0
                  && JuiceDiff(VfxConfig.FromJson(jsonText).Juice, JuiceProfile.Juiced).Count == 0,
                  "J2 the shipped juice block is the Juiced profile, and an empty enabled block falls back to it");

            // The Legacy profile is the numbers that are still in the source, pinned here.
            Check(Near(legacy.HitStopNormal, 0.05f) && Near(legacy.HitStopCrit, 0.09f) && legacy.HitStopSkill == 0f
                  && Near(legacy.FlashPeakNormal, 0.75f) && Near(legacy.FlashPeakCrit, 1f)
                  && Near(JuiceMath.FlashDecayRate(legacy), 5.5f)
                  && Near(legacy.ShakeCritStrength, 0.14f) && Near(legacy.ShakeCritSeconds, 0.2f)
                  && Near(legacy.ShakeSkillStrength, 0.18f) && Near(legacy.ShakeSkillSeconds, 0.26f)
                  && Near(legacy.ShakeKillStrength, 0.12f) && Near(legacy.ShakeKillSeconds, 0.22f)
                  && Near(legacy.DeathSeconds, 0.75f) && Near(legacy.DeathFadeStart, 0.25f) && Near(legacy.DeathSink, 0.18f),
                  "J3 Legacy holds the literals the battle used before the pass");

            // And those literals still exist where they were: a refactor of the old lines would
            // make "flag off is today" a claim about arithmetic instead of about untouched code.
            string scripts = Path.Combine(root, "Assets", "Scripts");
            string actor = ReadJuiceSource(scripts, "Battle", "UnitActor.cs");
            string director = ReadJuiceSource(scripts, "Battle", "VfxDirector.cs");
            string screen = ReadJuiceSource(scripts, "UI", "Screens", "BattleScreen.cs");
            string[] missing = new string[]
            {
                actor.Contains("critical ? 1f : 0.75f") ? null : "UnitActor: critical ? 1f : 0.75f",
                actor.Contains("Time.deltaTime * 5.5f") ? null : "UnitActor: Time.deltaTime * 5.5f",
                actor.Contains("0.75f / speed") ? null : "UnitActor: 0.75f / speed",
                director.Contains("HitStopSeconds = 0.05f") ? null : "VfxDirector: HitStopSeconds = 0.05f",
                director.Contains("CritHitStopSeconds = 0.09f") ? null : "VfxDirector: CritHitStopSeconds = 0.09f",
                director.Contains("ShakeAtLeast(0.14f, 0.2f)") ? null : "VfxDirector: ShakeAtLeast(0.14f, 0.2f)",
                director.Contains("ShakeAtLeast(0.12f, 0.22f)") ? null : "VfxDirector: ShakeAtLeast(0.12f, 0.22f)",
                screen.Contains("Shake(0.14f, 0.2f)") ? null : "BattleScreen: Shake(0.14f, 0.2f)",
                screen.Contains("Shake(0.18f, 0.26f)") ? null : "BattleScreen: Shake(0.18f, 0.26f)",
                screen.Contains("0.75f / _speed") ? null : "BattleScreen: 0.75f / _speed"
            };
            List<string> gone = new List<string>();
            for (int i = 0; i < missing.Length; i++) if (missing[i] != null) gone.Add(missing[i]);
            Check(gone.Count == 0,
                  "J3 TRIPWIRE (reads source): every legacy literal is still in the source, untouched" + (gone.Count > 0 ? ": GONE " + string.Join(", ", gone.ToArray()) : ""));

            // Every decision, given Legacy, gives the legacy answer.
            float s, t;
            JuiceMath.ShakeFor(legacy, false, false, false, out s, out t);
            bool normalNone = s == 0f;
            Check(Near(JuiceMath.HitStopSeconds(legacy, false, false), 0.05f) && Near(JuiceMath.HitStopSeconds(legacy, true, false), 0.09f)
                  && Near(JuiceMath.HitStopSeconds(legacy, false, true), 0.05f) && Near(JuiceMath.HitStopSeconds(legacy, true, true), 0.09f)
                  && JuiceMath.HoldFor(legacy, false, false, true, true) == 0f
                  && Near(JuiceMath.FlashPeak(legacy, false), 0.75f) && Near(JuiceMath.FlashPeak(legacy, true), 1f)
                  && JuiceMath.KnockPx(legacy, true, true) == 0f && normalNone
                  && JuiceMath.PopScale(legacy, 0f) == 1f && JuiceMath.DimAlpha(legacy, 0.1f) == 1f
                  && LegacyOffset(legacy) == 0f
                  && !JuiceMath.RetireOldestOnTarget(legacy, 99) && !JuiceMath.RetireOldestOverall(legacy, 999)
                  && JuiceMath.Admit(legacy, BudgetKind.Spark, 5000),
                  "J4 every JuiceMath decision, given Legacy, returns the answer the battle gave before the pass (no skill kind, no hold, 0.75 flash, no knock, no pop, no dim, no lanes, no cap)");

            CheckJuiceHookTripwire(root);
        }

        private static float LegacyOffset(JuiceProfile legacy)
        {
            int retire;
            return JuiceMath.NextNumberOffset(legacy, new float[] { 0f, 10f }, out retire);
        }

        private static string ReadJuiceSource(string scripts, params string[] parts)
        {
            string path = scripts;
            for (int i = 0; i < parts.Length; i++) path = Path.Combine(path, parts[i]);
            return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : "";
        }

        private static readonly Regex JuiceHook =
            new Regex(@"(\bJuiceMath\.|\bJuice\.|\bSkillMoment\b|\bDimBackdrop\b|\bHitHold\b|\bBattleVfx\.Glow\b|\bVfx\.Glow\()");

        /// <summary>
        /// J5. Every line of game code that reaches the pass either names Juice.On, or sits in a
        /// block that Juice.On opened (an if or its else), or in a method whose first statement is
        /// if (!Juice.On) return. A hook that is not one of those would run with the flag off, and
        /// then "flag off is today" is a hope. The count of hook lines is printed for the reviewer.
        /// </summary>
        private static void CheckJuiceHookTripwire(string root)
        {
            string scripts = Path.Combine(root, "Assets", "Scripts");
            int hooks = 0, captureLines = 0;
            List<string> bad = new List<string>();
            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                string rel = file.Substring(scripts.Length + 1).Replace('\\', '/');
                if (rel.StartsWith("Core/", StringComparison.Ordinal)) continue;
                if (name == "Juice.cs" || name == "JuiceMath.cs") continue;
                string[] lines = File.ReadAllText(file).Replace("\r\n", "\n").Split('\n');
                bool[] guarded = JuiceGuardedLines(lines);
                bool editorOnly = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = JuiceCodeOf(lines[i]);
                    string trimmed = code.Trim();
                    if (trimmed.StartsWith("#if UNITY_EDITOR", StringComparison.Ordinal)) editorOnly = true;
                    else if (editorOnly && trimmed.StartsWith("#endif", StringComparison.Ordinal)) editorOnly = false;
                    if (code.Length == 0 || !JuiceHook.IsMatch(code)) continue;
                    // The capture target's helpers read the record and change nothing in a fight; they
                    // are compiled out of a player and must work with the flag off (the .off shot).
                    if (editorOnly) { captureLines++; continue; }
                    hooks++;
                    if (code.Contains("Juice.On") || guarded[i]) continue;
                    bad.Add(name + ":" + (i + 1) + " " + code.Trim());
                }
            }
            Check(bad.Count == 0,
                  "J5 TRIPWIRE (reads source): every game-code line that reaches the juice pass is behind Juice.On (" + hooks + " hook lines, plus " + captureLines + " read-only capture lines inside #if UNITY_EDITOR)"
                  + (bad.Count > 0 ? ": UNGUARDED " + string.Join(" | ", bad.ToArray()) : ""));
        }

        /// <summary>The code of a line: comments and string-free trailing comments removed.</summary>
        private static string JuiceCodeOf(string line)
        {
            string t = line.TrimStart();
            if (t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith("*", StringComparison.Ordinal) || t.StartsWith("/*", StringComparison.Ordinal))
                return "";
            int at = line.IndexOf("//", StringComparison.Ordinal);
            return at >= 0 ? line.Substring(0, at) : line;
        }

        /// <summary>Lines inside a block opened by a Juice.On condition (its if and its else), or
        /// inside a method that begins with if (!Juice.On) return/yield break.</summary>
        private static bool[] JuiceGuardedLines(string[] lines)
        {
            bool[] guarded = new bool[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                string code = JuiceCodeOf(lines[i]);
                if (code.Contains("Juice.On"))
                {
                    int open = -1;
                    if (code.Contains("{")) open = i;
                    else if (i + 1 < lines.Length && lines[i + 1].Trim() == "{") open = i + 1;
                    if (open >= 0)
                    {
                        int end = JuiceBlockEnd(lines, open, lines[open].IndexOf('{'));
                        for (int k = i; k <= end && k < lines.Length; k++) guarded[k] = true;
                        // an else (or else if) hanging off the closing brace is the other branch
                        int elseLine = -1;
                        string closeCode = JuiceCodeOf(lines[end]);
                        int closeAt = closeCode.LastIndexOf('}');
                        if (closeAt >= 0 && closeCode.Substring(closeAt).Contains("else") && closeCode.Contains("{") && closeCode.LastIndexOf('{') > closeAt)
                            elseLine = end;
                        else if (end + 1 < lines.Length && JuiceCodeOf(lines[end + 1]).TrimStart().StartsWith("else", StringComparison.Ordinal))
                            elseLine = end + 1;
                        if (elseLine >= 0)
                        {
                            int elseOpen = elseLine;
                            int brace = JuiceCodeOf(lines[elseOpen]).LastIndexOf('{');
                            if (brace < 0 && elseOpen + 1 < lines.Length && lines[elseOpen + 1].Trim() == "{") { elseOpen++; brace = lines[elseOpen].IndexOf('{'); }
                            if (brace >= 0)
                            {
                                int elseEnd = JuiceBlockEnd(lines, elseOpen, brace);
                                for (int k = elseLine; k <= elseEnd && k < lines.Length; k++) guarded[k] = true;
                            }
                        }
                    }
                }
                // method whose first statement is the guard
                if (code.Contains("Juice.On") && code.Contains("!Juice.On") && (code.Contains("return") || code.Contains("yield break")))
                {
                    int decl = i - 1;
                    while (decl >= 0 && (lines[decl].Trim() == "{" || lines[decl].Trim().Length == 0)) decl--;
                    if (decl >= 0 && i >= 1 && lines[i - 1].Trim() == "{")
                    {
                        int open = i - 1;
                        int end = JuiceBlockEnd(lines, open, lines[open].IndexOf('{'));
                        for (int k = decl; k <= end && k < lines.Length; k++) guarded[k] = true;
                    }
                }
            }
            return guarded;
        }

        /// <summary>Line on which the block whose '{' is at (line, col) closes.</summary>
        private static int JuiceBlockEnd(string[] lines, int line, int col)
        {
            int depth = 0;
            for (int i = line; i < lines.Length; i++)
            {
                string code = JuiceCodeOf(lines[i]);
                for (int c = (i == line ? col : 0); c < code.Length; c++)
                {
                    if (code[c] == '{') depth++;
                    else if (code[c] == '}')
                    {
                        depth--;
                        if (depth == 0) return i;
                    }
                }
            }
            return lines.Length - 1;
        }

        // ------------------------------------------------------------------ J6 and J7

        private static void CheckJuiceNotHashed(ContentDatabase db, string contentDir)
        {
            string[] hashed = { "heroes", "enemies", "campaign", "balance", "relics", "events", "armor", "floor-conditions" };
            string[] texts = new string[hashed.Length];
            for (int i = 0; i < hashed.Length; i++)
            {
                string path = Path.Combine(contentDir, hashed[i] + ".json");
                texts[i] = File.Exists(path) ? File.ReadAllText(path) : null;
            }
            Check(ContentDatabase.VersionOf(texts) == db.ContentVersion,
                  "J6 VFX.JSON IS NOT HASHED: ContentVersion is exactly the hash of the eight fight files (" + db.ContentVersion
                  + "), so flipping the juice flag retires no guild replay");
        }

        private static void CheckJuiceSimulationUntouched(string root)
        {
            string core = Path.Combine(root, "Assets", "Scripts", "Core");
            List<string> mentions = new List<string>();
            int scanned = 0;
            foreach (string file in Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (name == "JuiceProfile.cs" || name == "VfxConfig.cs") continue;
                scanned++;
                if (File.ReadAllText(file).IndexOf("Juice", StringComparison.Ordinal) >= 0) mentions.Add(name);
            }
            Check(mentions.Count == 0 && scanned > 0,
                  "J7 SIMULATION UNTOUCHED (reads source): none of the " + scanned + " Core files other than the profile and its loader mention the juice pass"
                  + (mentions.Count > 0 ? ": " + string.Join(", ", mentions.ToArray()) : "")
                  + " (the fingerprint and sweep baselines then run in this same pass)");
        }

        // ------------------------------------------------------------------ J8, J9, J10

        private sealed class JuiceFight
        {
            public string StageId;
            public BattleResult Result;
        }

        private static List<JuiceFight> JuiceFights(ContentDatabase db)
        {
            List<JuiceFight> fights = new List<JuiceFight>();
            GameSession session = new GameSession(db, GameSession.CreateNewProfile(db, 0));
            for (int s = 0; s < db.AllStages.Count; s++)
            {
                StageDef stage = db.AllStages[s];
                BattleResult r = CombatSimulation.Run(session.BuildBattleRequest(Opening(stage), 3000000 + s * 13 + 1));
                JuiceFight f = new JuiceFight();
                f.StageId = stage.Id;
                f.Result = r;
                fights.Add(f);
            }
            return fights;
        }

        private static Aspect JuiceAspectOf(BattleResult r, int id)
        {
            UnitSnapshot u = r.FindUnit(id);
            return u != null ? u.Aspect : Aspect.Ember;
        }

        private static void CheckJuiceClassification(ContentDatabase db, JuiceProfile p)
        {
            List<JuiceFight> fights = JuiceFights(db);
            int damage = 0, ticks = 0, classified = 0, mismatch = 0;
            int crits = 0, heals = 0, deaths = 0;
            int[] skillByAspect = new int[6];
            int holdBad = 0, holdsSpent = 0, castsHeld = 0, tickHeld = 0;
            float holdMax = 0f;
            for (int f = 0; f < fights.Count; f++)
            {
                BattleResult r = fights[f].Result;
                MomentKind[] kinds = JuiceMath.ClassifyAll(r.Events);
                float[] holds = JuiceMath.HoldsFor(r.Events, p);
                bool skill = false;           // the audio rule, written out again on purpose
                int heldThisAction = 0;
                for (int i = 0; i < r.Events.Count; i++)
                {
                    BattleEvent e = r.Events[i];
                    if (e.Type == BattleEventType.AbilityCast) { skill = true; heldThisAction = 0; }
                    if (e.Type == BattleEventType.BasicAttack || e.Type == BattleEventType.TurnStart) { skill = false; heldThisAction = 0; }
                    if (e.Type == BattleEventType.Damage)
                    {
                        if (e.Status != StatusKind.None)
                        {
                            ticks++;
                            if (kinds[i] != MomentKind.None || holds[i] != 0f) tickHeld++;
                        }
                        else
                        {
                            damage++;
                            MomentKind expected = skill ? MomentKind.Skill : (e.Critical ? MomentKind.Crit : MomentKind.Hit);
                            if (kinds[i] == expected && (kinds[i] == MomentKind.Hit || kinds[i] == MomentKind.Crit || kinds[i] == MomentKind.Skill)) classified++;
                            else mismatch++;
                            if (kinds[i] == MomentKind.Crit) crits++;
                            if (kinds[i] == MomentKind.Skill) skillByAspect[(int)JuiceAspectOf(r, e.SourceId)]++;
                            if (holds[i] > 0f)
                            {
                                heldThisAction++;
                                holdsSpent++;
                                if (heldThisAction > 1) holdBad++;
                                if (holds[i] > holdMax) holdMax = holds[i];
                            }
                        }
                    }
                    else if (kinds[i] == MomentKind.Heal) heals++;
                    else if (kinds[i] == MomentKind.Death) deaths++;
                }
                for (int i = 0; i < r.Events.Count; i++) if (r.Events[i].Type == BattleEventType.AbilityCast) castsHeld++;
            }

            Check(damage > 0 && mismatch == 0 && classified == damage,
                  "J8 CLASSIFICATION: all " + damage + " non-tick Damage events of " + fights.Count + " stage openings land in exactly one of Hit, Crit, Skill, and Skill is the audio rule ("
                  + ticks + " ticks are none)");
            BattleEvent tick = BattleEvent.Make(BattleEventType.Damage, 1);
            tick.Status = StatusKind.Burn;
            tick.Value = 5;
            Check(tickHeld == 0 && JuiceMath.IsTick(tick) && JuiceMath.Classify(tick, true) == MomentKind.None
                  && JuiceMath.HoldFor(p, true, false, true, true) == 0f,
                  "J8 a damage-over-time tick (a synthetic Burn tick; the openings hold none) is no juice kind and earns no hold");

            List<string> missing = new List<string>();
            if (crits == 0) missing.Add("crit");
            if (heals == 0) missing.Add("heal");
            if (deaths == 0) missing.Add("death");
            string[] names = Enum.GetNames(typeof(Aspect));
            for (int a = 0; a < 6; a++) if (skillByAspect[a] == 0) missing.Add("skill:" + names[a]);
            Line("         moments across the campaign openings: crit " + crits + ", heal " + heals + ", death " + deaths + ", skill by aspect "
                 + names[0] + " " + skillByAspect[0] + ", " + names[1] + " " + skillByAspect[1] + ", " + names[2] + " " + skillByAspect[2] + ", "
                 + names[3] + " " + skillByAspect[3] + ", " + names[4] + " " + skillByAspect[4] + ", " + names[5] + " " + skillByAspect[5]);
            Check(crits > 0 && heals > 0 && deaths > 0 && missing.Count == 0,
                  "J8 the campaign holds a crit, a heal, a death and a skill impact for every aspect, so the capture plan can find them"
                  + (missing.Count > 0 ? " (absent: " + string.Join(", ", missing.ToArray()) + ")" : ""));

            Check(holdBad == 0 && holdMax <= 0.12f + 0.0001f && holdsSpent > 0,
                  "J9 HOLD: at most one non-zero hold per cast or attack, never above 0.12 s (" + holdsSpent + " holds over " + castsHeld + " casts, longest " + Fmt(holdMax) + " s)");

            float cs, ct, ks, kt, ns, nt;
            JuiceMath.ShakeFor(p, true, false, false, out cs, out ct);
            float ss, st;
            JuiceMath.ShakeFor(p, false, true, false, out ss, out st);
            JuiceMath.ShakeFor(p, false, false, true, out ks, out kt);
            JuiceMath.ShakeFor(p, false, false, false, out ns, out nt);
            Check(cs <= JuiceShakeReserve && ss <= JuiceShakeReserve && cs > 0f && ss > cs && ns == 0f && ks == 0f,
                  "J10 SHAKE: crit " + Fmt(cs) + " and skill " + Fmt(ss) + " stay under the " + Fmt(JuiceShakeReserve) + " reserve BattleFraming budgets; a normal hit and a kill ask for none");
            float cks, ckt;
            JuiceMath.ShakeFor(p, true, true, true, out cks, out ckt);
            Check(cks == ss, "J10 a critical skill that kills takes the skill shake once, not a sum (" + Fmt(cks) + ")");
        }

        // ------------------------------------------------------------------ J11 curves

        private static void CheckJuiceCurves(JuiceProfile p)
        {
            bool monotone = true;
            float prev = JuiceMath.PopScale(p, 0f);
            for (int i = 1; i <= 100; i++)
            {
                float v = JuiceMath.PopScale(p, p.PopSeconds * i / 100f);
                if (v > prev + 1e-6f || v < 1f) monotone = false;
                prev = v;
            }
            Check(Near(JuiceMath.PopScale(p, 0f), 1.4f) && Near(JuiceMath.PopScale(p, 0.15f), 1f) && Near(JuiceMath.PopScale(p, 1f), 1f) && monotone,
                  "J11 the pop starts at 1.4x, never dips under 1x, only falls, and is 1x at 0.15 s");

            float low = 1f;
            for (int i = 0; i <= 300; i++) low = Math.Min(low, JuiceMath.DimAlpha(p, i / 1000f));
            Check(Near(low, 0.70f) && Near(JuiceMath.DimAlpha(p, 0f), 1f) && Near(JuiceMath.DimAlpha(p, 0.30f), 1f) && Near(JuiceMath.DimAlpha(p, 0.10f), 0.70f),
                  "J11 the dim reaches 0.70 of white, holds, and is back to 1.0 at 0.30 s");

            float lo = JuiceMath.PixelsToWorld(10f, 0.907f, 3.65f);
            float hi = JuiceMath.PixelsToWorld(16f, 0.907f, 3.65f);
            Check(lo >= 0.07f && hi <= 0.13f && hi > lo,
                  "J11 10 to 16 px of knock is " + Fmt(lo) + " to " + Fmt(hi) + " world units at the battle camera");

            float rate = JuiceMath.FlashDecayRate(p);
            Check(Near(1f / rate, 0.08f) && JuiceMath.FlashPeak(p, false) >= 1f,
                  "J11 the flash is full and reaches zero in 0.08 s (rate " + Fmt(rate) + " per second)");

            Check(Near(JuiceMath.HitStopSeconds(p, false, false), 0.05f) && Near(JuiceMath.HitStopSeconds(p, true, false), 0.09f)
                  && Near(JuiceMath.HitStopSeconds(p, false, true), 0.12f) && Near(JuiceMath.HitStopSeconds(p, true, true), 0.12f),
                  "J11 hit-stop by kind: 0.05 hit, 0.09 crit, 0.12 skill, a crit skill takes the larger");
        }

        // ------------------------------------------------------------------ J12 lanes

        private sealed class JuiceNumber
        {
            public float Born;
            public float Offset;
        }

        private static void CheckJuiceLanes(JuiceProfile p)
        {
            const float Life = 0.75f;
            const float Rise = 120f;
            Random rng = new Random(12012);
            List<JuiceNumber> live = new List<JuiceNumber>();
            float now = 0f;
            int spawned = 0, tooClose = 0, overCap = 0, retired = 0;
            float maxOffset = 0f;
            for (int n = 0; n < 300; n++)
            {
                now += (float)rng.NextDouble() * (n % 7 == 0 ? 0.01f : 0.35f);
                for (int i = live.Count - 1; i >= 0; i--) if (now - live[i].Born >= Life) live.RemoveAt(i);
                if (JuiceMath.RetireOldestOnTarget(p, live.Count)) { live.RemoveAt(0); retired++; }
                List<float> ys = new List<float>();
                for (int i = 0; i < live.Count; i++) ys.Add(live[i].Offset + Rise * (float)Math.Sqrt((now - live[i].Born) / Life));
                int retire;
                float offset = JuiceMath.NextNumberOffset(p, ys, out retire);
                if (retire > 0)
                {
                    live.RemoveRange(0, retire);
                    retired += retire;
                    ys.RemoveRange(0, retire);
                }
                for (int i = 0; i < ys.Count; i++) if (Math.Abs(offset - ys[i]) < p.NumberLineHeight - 0.001f) tooClose++;
                JuiceNumber num = new JuiceNumber();
                num.Born = now;
                num.Offset = offset;
                live.Add(num);
                if (live.Count > p.MaxNumbersPerTarget) overCap++;
                if (offset > maxOffset) maxOffset = offset;
                spawned++;
            }
            Check(tooClose == 0 && overCap == 0,
                  "J12 LANES: " + spawned + " random spawns on one unit, each starts a full line (" + Fmt(p.NumberLineHeight) + " px) clear of every live number, never more than "
                  + p.MaxNumbersPerTarget + " live (" + retired + " retired early)");
            Check(maxOffset <= (p.MaxNumbersPerTarget - 1) * p.NumberLineHeight + 0.001f,
                  "J12 a start offset never passes " + (p.MaxNumbersPerTarget - 1) + " lines (largest " + Fmt(maxOffset) + " px), so the climb ceiling keeps room");
            int r0, r1, r2;
            Check(JuiceMath.NextNumberOffset(p, new float[0], out r0) == 0f && Near(JuiceMath.NextNumberOffset(p, new float[] { 5f }, out r1), 5f + p.NumberLineHeight)
                  && Near(JuiceMath.NextNumberOffset(p, new float[] { 90f }, out r2), 0f) && r0 == 0 && r1 == 0 && r2 == 0,
                  "J12 an empty unit starts at 0, a number at the start pushes the next up one line, one far above does not");
        }

        // ------------------------------------------------------------------ J13 cap

        private static void CheckJuiceCap(JuiceProfile p)
        {
            // Lifetimes the routines really use (BattleVfx and VfxDirector), in seconds.
            List<float> quads = new List<float>();
            List<float> flips = new List<float>();
            List<float> numbers = new List<float>();
            int maxQuads = 0, maxTotal = 0, priorityRefused = 0, sparkBeforeImpact = 0, refusedSparks = 0, refused = 0;
            int step = 0;
            for (float now = 0f; now < 2f; now += 0.02f, step++)
            {
                quads.RemoveAll(d => d <= now);
                flips.RemoveAll(d => d <= now);
                numbers.RemoveAll(d => d <= now);

                // a 5 target crit AoE every 100 ms: per target a crit impact, a glow, sparks, a ring
                if (step % 5 == 0)
                {
                    for (int t = 0; t < 5; t++)
                    {
                        TryQuad(p, quads, BudgetKind.Priority, now + 0.38f, ref priorityRefused, ref refused, true);
                        TryQuad(p, quads, BudgetKind.Priority, now + p.GlowSeconds, ref priorityRefused, ref refused, true);
                        for (int s = 0; s < 3; s++) TryQuad(p, quads, BudgetKind.Spark, now + 0.25f, ref priorityRefused, ref refusedSparks, false);
                        TryQuad(p, quads, BudgetKind.Ring, now + 0.45f, ref priorityRefused, ref refused, false);
                        if (flips.Count < p.MaxFlipbooks) flips.Add(now + 0.6f);
                        if (numbers.Count >= p.MaxNumbers) numbers.RemoveAt(0);
                        numbers.Add(now + 0.75f);
                    }
                }
                // 4 damage-over-time ticks every 20 ms: a mote and a spark each
                for (int d = 0; d < 4; d++)
                {
                    TryQuad(p, quads, BudgetKind.Mote, now + 0.5f, ref priorityRefused, ref refused, false);
                    TryQuad(p, quads, BudgetKind.Spark, now + 0.25f, ref priorityRefused, ref refusedSparks, false);
                    if (numbers.Count >= p.MaxNumbers) numbers.RemoveAt(0);
                    numbers.Add(now + 0.75f);
                }
                // 2 kills every 20 ms: a ring and a burst of 6 sparks
                for (int k = 0; k < 2; k++)
                {
                    TryQuad(p, quads, BudgetKind.Priority, now + 0.5f, ref priorityRefused, ref refused, true);
                    for (int s = 0; s < 6; s++) TryQuad(p, quads, BudgetKind.Spark, now + 0.3f, ref priorityRefused, ref refusedSparks, false);
                }
                // slashes, bolts and auras from the host
                if (step % 3 == 0)
                {
                    TryQuad(p, quads, BudgetKind.Slash, now + 0.3f, ref priorityRefused, ref refused, false);
                    TryQuad(p, quads, BudgetKind.Bolt, now + 0.34f, ref priorityRefused, ref refused, false);
                    TryQuad(p, quads, BudgetKind.Aura, now + 0.6f, ref priorityRefused, ref refused, false);
                }
                maxQuads = Math.Max(maxQuads, quads.Count);
                maxTotal = Math.Max(maxTotal, quads.Count + flips.Count + numbers.Count);
            }
            for (int live = 0; live < p.MaxQuads; live++)
                if (live >= p.MaxQuads * 13 / 20 && live < p.MaxQuads * 17 / 20 && (JuiceMath.Admit(p, BudgetKind.Spark, live) || !JuiceMath.Admit(p, BudgetKind.Impact, live)))
                    sparkBeforeImpact++;
            Check(maxQuads <= p.MaxQuads && maxTotal <= p.MaxTotal,
                  "J13 CAP: the worst schedule (5 target crit AoE every 100 ms, 4 ticks and 2 kills every 20 ms, for 2 s) peaks at " + maxQuads + " of " + p.MaxQuads
                  + " quads and " + maxTotal + " of " + p.MaxTotal + " drawn things in all");
            Check(priorityRefused == 0,
                  "J13 a crit impact, a kill or a skill ring is never refused while the pool has room (" + refusedSparks + " sparks and " + refused + " other quads were thinned instead)");
            Check(sparkBeforeImpact == 0 && !JuiceMath.Admit(p, BudgetKind.Spark, p.MaxQuads * 13 / 20) && JuiceMath.Admit(p, BudgetKind.Impact, p.MaxQuads * 13 / 20)
                  && !JuiceMath.Admit(p, BudgetKind.Priority, p.MaxQuads) && JuiceMath.Admit(p, BudgetKind.Priority, p.MaxQuads - 1),
                  "J13 a spark is refused before an impact, and only a full pool refuses a priority quad");
        }

        private static void TryQuad(JuiceProfile p, List<float> quads, BudgetKind kind, float dies,
                                    ref int priorityRefused, ref int refused, bool priority)
        {
            if (JuiceMath.Admit(p, kind, quads.Count)) { quads.Add(dies); return; }
            if (priority && quads.Count < p.MaxQuads) priorityRefused++;
            refused++;
        }

        // ------------------------------------------------------------------ J14 wiring

        private static void CheckJuiceWiring(string root)
        {
            string scripts = Path.Combine(root, "Assets", "Scripts");
            string battle = ReadJuiceSource(scripts, "UI", "Screens", "BattleScreen.cs");
            int damageAt = battle.IndexOf("case BattleEventType.Damage:", StringComparison.Ordinal);
            int soundAt = battle.IndexOf("PlayDamageSound(e, source);", StringComparison.Ordinal);
            int numberAt = battle.IndexOf("number floats one frame after impact", StringComparison.Ordinal);
            Check(damageAt >= 0 && soundAt > damageAt && numberAt > soundAt,
                  "J14 WIRING (reads source): the hit sound is still played in the Damage event before the number floats, so the pass cannot delay a sound");

            string stage = ReadJuiceSource(scripts, "Battle", "BattleStage.cs");
            string[] forbidden = { "3.65f", "5.95f", "1.4f", "3.95f", "4.95f", "3.25f" };
            List<string> found = new List<string>();
            for (int i = 0; i < forbidden.Length; i++) if (stage.Contains(forbidden[i])) found.Add(forbidden[i]);
            Check(stage.Length > 0 && found.Count == 0,
                  "J14 WIRING (reads source): BattleStage still reads its camera from BattleFraming and gained none of its literals" + (found.Count > 0 ? ": " + string.Join(", ", found.ToArray()) : ""));

            string shots = File.Exists(Path.Combine(root, "Assets", "Editor", "PlayModeScreenshot.cs"))
                ? File.ReadAllText(Path.Combine(root, "Assets", "Editor", "PlayModeScreenshot.cs")) : "";
            Check(shots.Contains("case \"BattleMoment\": id = ScreenId.Battle;") && shots.Contains("PumpBattleMoment(") && shots.Contains("MOMENT-ABSENT")
                  && !shots.Contains("Begin(\"Screen:BattleMoment") && !shots.Contains("Begin(\"Screen:AudioFight"),
                  "J14 WIRING (reads source): PlayModeScreenshot has the BattleMoment target, reports MOMENT-ABSENT, and has no literal Begin for it, so the nightly sweep never runs it");

            string settings = ReadJuiceSource(scripts, "UI", "Screens", "SettingsScreen.cs");
            Check(Regex.Matches(settings, "\"Screen shake\"").Count == 1 && settings.Contains("ScreenShakePrefs.Enabled") && settings.Contains("BuildBattleCard(content);"),
                  "J14 WIRING (reads source): the settings screen has one Screen shake card, wired to ScreenShakePrefs");

            // The shake toggle: one choke point, on by default, in both flag states.
            string prefs = ReadJuiceSource(scripts, "Battle", "ScreenShakePrefs.cs");
            Check(Regex.IsMatch(stage, @"public void Shake\(float strength, float duration\)\s*\{\s*if \(!ScreenShakePrefs\.Enabled\) return;")
                  && Regex.IsMatch(stage, @"public void ShakeAtLeast\(float strength, float duration\)\s*\{\s*if \(!ScreenShakePrefs\.Enabled\) return;")
                  && Regex.IsMatch(prefs, @"GetInt\(Key, 1\) == 1"),
                  "J14 WIRING (reads source): BattleStage.Shake and ShakeAtLeast both start with the ScreenShakePrefs gate, and the preference defaults to ON");
        }

        // ------------------------------------------------------------------ --moment-survey

        private const int SurveySeeds = 8;

        /// <summary>
        /// Not a check. For each moment kind and aspect, the stage openings that hold one most
        /// often over SurveySeeds seeds (the game rolls its own seed from the clock, so one seed
        /// proves little), read through the same JuiceMath.FindMoment the capture target uses. The
        /// capture batch takes its stage ids from here. A target the live fight does not hold says
        /// MOMENT-ABSENT and is retried on another stage.
        /// </summary>
        private static int RunMomentSurvey(ContentDatabase db)
        {
            Line("MOMENT SURVEY (stage openings holding each moment most often over " + SurveySeeds + " seeds, as hits/" + SurveySeeds
                 + "; aspect is that of the unit that makes the moment)");
            GameSession session = new GameSession(db, GameSession.CreateNewProfile(db, 0));
            List<StageDef> stages = db.AllStages;
            List<BattleResult[]> fights = new List<BattleResult[]>();
            for (int s = 0; s < stages.Count; s++)
            {
                BattleResult[] row = new BattleResult[SurveySeeds];
                for (int k = 0; k < SurveySeeds; k++)
                    row[k] = CombatSimulation.Run(session.BuildBattleRequest(Opening(stages[s]), 4100000 + s * 101 + k));
                fights.Add(row);
            }
            MomentKind[] kinds = { MomentKind.Hit, MomentKind.Crit, MomentKind.Skill, MomentKind.Heal, MomentKind.Death };
            string[] aspects = Enum.GetNames(typeof(Aspect));
            for (int k = 0; k < kinds.Length; k++)
            {
                bool byAspect = kinds[k] != MomentKind.Hit && kinds[k] != MomentKind.Crit;
                if (!byAspect) { Line(kinds[k] + ": " + JuiceSurveyStages(stages, fights, kinds[k], null)); continue; }
                for (int a = 0; a < aspects.Length; a++)
                    Line(kinds[k] + "." + aspects[a].ToLowerInvariant() + ": " + JuiceSurveyStages(stages, fights, kinds[k], (Aspect)a));
            }
            return 0;
        }

        private static string JuiceSurveyStages(List<StageDef> stages, List<BattleResult[]> fights, MomentKind kind, Aspect? aspect)
        {
            List<KeyValuePair<int, string>> ranked = new List<KeyValuePair<int, string>>();
            for (int s = 0; s < stages.Count; s++)
            {
                int hits = 0;
                for (int k = 0; k < fights[s].Length; k++)
                {
                    BattleResult r = fights[s][k];
                    if (JuiceMath.FindMoment(r.Events, id => JuiceAspectOf(r, id), kind, aspect) >= 0) hits++;
                }
                if (hits > 0) ranked.Add(new KeyValuePair<int, string>(hits, stages[s].Id));
            }
            // most often first; ties keep campaign order (an early stage is a shorter fight)
            for (int i = 1; i < ranked.Count; i++)
            {
                KeyValuePair<int, string> x = ranked[i];
                int j = i - 1;
                while (j >= 0 && ranked[j].Key < x.Key) { ranked[j + 1] = ranked[j]; j--; }
                ranked[j + 1] = x;
            }
            if (ranked.Count == 0) return "(none)";
            List<string> top = new List<string>();
            for (int i = 0; i < ranked.Count && i < 3; i++) top.Add(ranked[i].Value + " " + ranked[i].Key + "/" + SurveySeeds);
            return string.Join(", ", top.ToArray());
        }
    }
}
