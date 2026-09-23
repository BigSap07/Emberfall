using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE TEMPO STUDY: does turn order move outcomes in this engine, and does any of the speed
    /// and turn-manipulation family? Run with --tempo-study. Not a check and not run by the gate:
    /// it asserts nothing, writes nothing and changes no content. Every ablation is made on a
    /// PRIVATE ContentDatabase loaded for the purpose, so the live one - and the files - are never
    /// touched. Docs/TEMPO-STUDY.md is the reading.
    ///
    /// A partial of Program for the same reason ArenaMatrix and WallResolve are: its arenas are
    /// the instruments the rest of the harness already uses (the pinned line at a stage's
    /// recommendedPower against its CEIL battle, the hero-sweep arena, a loadout duel), not
    /// copies of them.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>One fight reduced to what the study compares.</summary>
        private struct TempoObs
        {
            public float Win;
            public float Margin;
            public float Output;
            public float Rounds;
            /// <summary>Turns each side actually opened - TurnStart events, a stunned turn included.
            /// The engine gives every living unit one entry per round, so these differ between two
            /// orderings of one fight only through who died before their entry came up.</summary>
            public float HeroActs;
            public float EnemyActs;
            /// <summary>TurnOrderShift events the subject (player side) caused whose shift actually
            /// moved a body, and delays it banked for the next round - zero in both is a mechanic that
            /// never reached the order in this arena.</summary>
            public float Shifts;
            /// <summary>1 when the subject (player side) died in the fight.</summary>
            public float SubjectDied;
        }

        private static int _tempoScale = 1;

        private static int RunTempoStudy(string contentDir, string mode)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            ContentDatabase live = LoadContent(contentDir);
            if (live == null) return 1;
            _tempoScale = mode == "quick" ? 1 : 8;
            Stopwatch clock = Stopwatch.StartNew();

            Line("TEMPO STUDY (--tempo-study " + (mode ?? "full") + ")");
            Line("margin = share of HP the heroes kept minus share the enemies kept, in [-1, 1]; a win is a margin above 0");
            Line("deltas are PAIRED: the same seeds in both arms, mean of the per-seed difference, +/- one standard error");
            Line("");

            if (mode == null || mode == "quick" || mode == "speed") TempoSpeedCurves(live);
            if (mode == null || mode == "quick" || mode == "first") TempoActingFirst(live);
            if (mode == null || mode == "quick" || mode == "price") TempoPowerPrice(live);
            if (mode == null || mode == "quick" || mode == "mech") TempoMechanics(live, contentDir);
            if (mode == null || mode == "quick" || mode == "probe") TempoProbes(live, contentDir);

            Line("");
            Line("tempo study done in " + (clock.ElapsedMilliseconds / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + "s");
            return 0;
        }

        // ================================================================== fights

        private static TempoObs TempoRead(BattleResult r, string subjectId)
        {
            TempoObs o = new TempoObs();
            o.Win = r.Victory ? 1f : 0f;
            o.Margin = r.PlayerHpRatio - r.EnemyHpRatio;
            o.Output = subjectId == null ? 0f : OutputOf(r, subjectId);
            o.Rounds = r.Rounds;
            Dictionary<int, bool> isPlayer = new Dictionary<int, bool>();
            for (int i = 0; i < r.Units.Count; i++) isPlayer[r.Units[i].Id] = r.Units[i].IsPlayer;
            for (int i = 0; i < r.Events.Count; i++)
            {
                if (r.Events[i].Type == BattleEventType.TurnOrderShift && subjectId != null)
                {
                    for (int u = 0; u < r.Units.Count; u++)
                        if (r.Units[u].Id == r.Events[i].SourceId && r.Units[u].IsPlayer && r.Units[u].DefId == subjectId) { o.Shifts++; break; }
                }
                if (r.Events[i].Type == BattleEventType.Death && subjectId != null)
                {
                    for (int u = 0; u < r.Units.Count; u++)
                        if (r.Units[u].Id == r.Events[i].TargetId && r.Units[u].IsPlayer && r.Units[u].DefId == subjectId) { o.SubjectDied = 1f; break; }
                }
                if (r.Events[i].Type != BattleEventType.TurnStart) continue;
                bool p;
                if (!isPlayer.TryGetValue(r.Events[i].SourceId, out p)) continue;
                if (p) o.HeroActs++; else o.EnemyActs++;
            }
            return o;
        }

        /// <summary>The stage's CEIL battle - the last battle of its last wave, the one the
        /// power-band guard grades and the only one of 144 that is close by design.</summary>
        private static StageDef TempoCeil(StageDef stage)
        {
            return CampaignWaveGenerator.BuildBattle(stage, Math.Max(1, stage.WaveCount), Math.Max(1, stage.BattleCount));
        }

        private static int TempoCampaignSeed(StageDef stage, int s) { return 73000000 + stage.GlobalIndex * 1009 + s * 31; }

        /// <summary>
        /// The session's line against one battle, n seeds, with each deployed hero's Def swapped
        /// for defOf(hero) for the duration. The Def is the whole of what the simulation reads
        /// from a hero besides its Level and its armor, and armor is keyed by the instance, so
        /// the swap changes exactly the stat or the kit the variant changes and nothing else.
        /// </summary>
        private static TempoObs[] TempoCampaign(GameSession session, StageDef battle, StageDef stage, int n,
                                                Func<HeroInstance, HeroDef> defOf, BattleRules rules, string subjectId)
        {
            List<HeroInstance> team = session.Profile.TeamHeroes();
            HeroDef[] was = new HeroDef[team.Count];
            for (int i = 0; i < team.Count; i++) was[i] = team[i].Def;
            TempoObs[] obs = new TempoObs[n];
            try
            {
                if (defOf != null)
                    for (int i = 0; i < team.Count; i++) team[i].Def = defOf(team[i]) ?? was[i];
                for (int s = 0; s < n; s++)
                {
                    BattleRequest req = session.BuildBattleRequest(battle, TempoCampaignSeed(stage, s));
                    req.Rules = rules;
                    obs[s] = TempoRead(CombatSimulation.Run(req), subjectId);
                }
            }
            finally
            {
                for (int i = 0; i < team.Count; i++) team[i].Def = was[i];
            }
            return obs;
        }

        /// <summary>
        /// A loadout duel: the player line (instances, so each Def can be a variant) against the
        /// opponent line resolved from `content` by id, both unarmoured and without relics or
        /// talents. The stage is only the environment; Opponent replaces its enemies.
        /// </summary>
        private static TempoObs[] TempoDuel(ContentDatabase content, List<HeroInstance> player, string[] opponent,
                                            int opponentLevel, int n, int seedBase, string subjectId)
        {
            StageDef arena = CampaignWaveGenerator.BuildBattle(content.AllStages[0], 1, 1);
            BattleLoadout line = new BattleLoadout();
            line.Label = "tempo duel";
            for (int i = 0; i < opponent.Length; i++)
            {
                HeroDef d = content.GetHero(opponent[i]);
                LoadoutUnit u = new LoadoutUnit();
                u.DefId = d.Id;
                u.Level = opponentLevel;
                u.Stars = d.Stars;
                line.Units.Add(u);
            }
            TempoObs[] obs = new TempoObs[n];
            for (int s = 0; s < n; s++)
            {
                BattleRequest req = new BattleRequest();
                req.Content = content;
                req.Stage = arena;
                req.Opponent = line;
                req.Seed = seedBase + s * 131;
                req.Team = player;
                obs[s] = TempoRead(CombatSimulation.Run(req), subjectId);
            }
            return obs;
        }

        private static List<HeroInstance> TempoLine(ContentDatabase db, string[] ids, int level, Func<HeroDef, HeroDef> variant)
        {
            List<HeroInstance> team = new List<HeroInstance>();
            for (int i = 0; i < ids.Length; i++)
            {
                HeroDef d = db.GetHero(ids[i]);
                team.Add(MakeInstance(variant == null ? d : variant(d), level));
            }
            return team;
        }

        // ================================================================== variants

        private static HeroDef TempoClone(HeroDef d) { return (HeroDef)_memberwiseClone.Invoke(d, null); }

        /// <summary>The hero with its built Speed multiplied by f at this Level and every other
        /// stat untouched. Speed is BaseSpeed + 0.35 per Level, so the base is solved for.</summary>
        private static HeroDef SpeedTimes(HeroDef d, int level, float f)
        {
            HeroDef c = TempoClone(d);
            float grown = (Math.Max(1, level) - 1) * 0.35f;
            c.BaseSpeed = (d.BaseSpeed + grown) * f - grown;
            return c;
        }

        private static HeroDef AttackTimes(HeroDef d, float m)
        {
            HeroDef c = TempoClone(d);
            c.BaseAttack = d.BaseAttack * m;
            return c;
        }

        // ================================================================== statistics

        private static double Mean(TempoObs[] a, Func<TempoObs, float> f)
        {
            double s = 0;
            for (int i = 0; i < a.Length; i++) s += f(a[i]);
            return a.Length == 0 ? 0 : s / a.Length;
        }

        /// <summary>Mean and standard error of the per-seed difference b - a.</summary>
        private static void Paired(TempoObs[] a, TempoObs[] b, Func<TempoObs, float> f, out double mean, out double se)
        {
            int n = Math.Min(a.Length, b.Length);
            double s = 0;
            for (int i = 0; i < n; i++) s += f(b[i]) - f(a[i]);
            mean = n == 0 ? 0 : s / n;
            double ss = 0;
            for (int i = 0; i < n; i++) { double d = f(b[i]) - f(a[i]) - mean; ss += d * d; }
            se = n > 1 ? Math.Sqrt(ss / (n - 1) / n) : 0;
        }

        private static TempoObs[] Concat(List<TempoObs[]> parts)
        {
            int n = 0;
            for (int i = 0; i < parts.Count; i++) n += parts[i].Length;
            TempoObs[] all = new TempoObs[n];
            int k = 0;
            for (int i = 0; i < parts.Count; i++)
                for (int j = 0; j < parts[i].Length; j++) all[k++] = parts[i][j];
            return all;
        }

        private static string F(double v, string fmt) { return v.ToString(fmt, CultureInfo.InvariantCulture); }
        private static string Pct(double v) { return (v >= 0 ? "+" : "") + F(v * 100, "0.0"); }
        private static string Sgn(double v) { return (v >= 0 ? "+" : "") + F(v, "0.000"); }

        private static string DeltaText(TempoObs[] control, TempoObs[] treated, bool output)
        {
            double wm, ws, mm, ms;
            Paired(control, treated, o => o.Win, out wm, out ws);
            Paired(control, treated, o => o.Margin, out mm, out ms);
            string t = "dWR " + Pct(wm) + "pp +/-" + F(ws * 100, "0.0") + "  dMargin " + Sgn(mm) + " +/-" + F(ms, "0.000");
            double hm, hs, em, es;
            Paired(control, treated, o => o.HeroActs, out hm, out hs);
            Paired(control, treated, o => o.EnemyActs, out em, out es);
            t += "  dTurns heroes " + (hm >= 0 ? "+" : "") + F(hm, "0.00") + " enemies " + (em >= 0 ? "+" : "") + F(em, "0.00");
            if (output)
            {
                double base0 = Mean(control, o => o.Output);
                double om, os;
                Paired(control, treated, o => o.Output, out om, out os);
                t += "  dOutput " + (base0 > 0 ? Pct(om / base0) + "% +/-" + F(os / base0 * 100, "0.0") : "n/a");
            }
            return t;
        }

        // ================================================================== Q1: raw Speed

        private static readonly float[] SpeedFactors = { 0.01f, 0.7f, 0.8f, 0.9f, 0.95f, 1f, 1.05f, 1.1f, 1.2f, 1.3f, 100f };

        private static string FactorName(float f)
        {
            if (f <= 0.011f) return "last  ";
            if (f >= 99f) return "first ";
            return "x" + F(f, "0.00") + " ";
        }

        private static void TempoSpeedCurves(ContentDatabase live)
        {
            int nStage = 25 * _tempoScale;
            Line("Q1a. RAW SPEED ON THE CAMPAIGN LADDER - the instrument line at each stage's recommendedPower, geared,");
            Line("     against the stage's CEIL battle; every hero's Speed scaled, enemies and every other stat untouched.");
            Line("     'first' is Speed x100 (heroes always act first), 'last' is x0.01 (always last).");
            Line("     " + live.AllStages.Count + " stages x " + nStage + " seeds = " + (live.AllStages.Count * nStage) + " fights per row");

            // Hero and enemy Speed as fielded, for the ratio the curve is read against.
            List<TempoObs[]>[] perF = new List<TempoObs[]>[SpeedFactors.Length];
            for (int k = 0; k < perF.Length; k++) perF[k] = new List<TempoObs[]>();
            List<string> perStage = new List<string>();
            double heroSpd = 0, enemySpd = 0; int heroN = 0, enemyN = 0;

            for (int si = 0; si < live.AllStages.Count; si++)
            {
                StageDef stage = live.AllStages[si];
                StageDef ceil = TempoCeil(stage);
                GameSession session = BuildPinnedLine(live, stage, true);
                List<HeroInstance> team = session.Profile.TeamHeroes();
                for (int i = 0; i < team.Count; i++) { heroSpd += StatMath.ForHero(team[i].Def, team[i].Level).Speed; heroN++; }
                for (int i = 0; i < ceil.Enemies.Count; i++)
                {
                    EnemyDef e = live.GetEnemy(ceil.Enemies[i].EnemyId);
                    if (e != null) { enemySpd += StatMath.ForEnemy(e, ceil.Enemies[i].Level).Speed; enemyN++; }
                }
                TempoObs[] baseObs = null, first = null, last = null;
                for (int k = 0; k < SpeedFactors.Length; k++)
                {
                    float f = SpeedFactors[k];
                    TempoObs[] o = TempoCampaign(session, ceil, stage, nStage,
                                                 h => f == 1f ? h.Def : SpeedTimes(h.Def, h.Level, f), null, null);
                    perF[k].Add(o);
                    if (f == 1f) baseObs = o;
                    if (f >= 99f) first = o;
                    if (f <= 0.011f) last = o;
                }
                double wm, ws, mm, ms;
                Paired(last, first, o => o.Win, out wm, out ws);
                Paired(last, first, o => o.Margin, out mm, out ms);
                perStage.Add("     " + stage.DisplayCode.PadRight(5) + " base WR " + F(Mean(baseObs, o => o.Win) * 100, "0").PadLeft(3)
                    + "%  margin " + Sgn(Mean(baseObs, o => o.Margin)) + "  rounds " + F(Mean(baseObs, o => o.Rounds), "0.0").PadLeft(4)
                    + "  | last->first: dWR " + Pct(wm).PadLeft(6) + "pp  dMargin " + Sgn(mm)
                    + "  (WR last " + F(Mean(last, o => o.Win) * 100, "0") + "%, first " + F(Mean(first, o => o.Win) * 100, "0") + "%)");
            }

            Line("     mean fielded Speed: heroes " + F(heroSpd / Math.Max(1, heroN), "0.0") + ", CEIL enemies " + F(enemySpd / Math.Max(1, enemyN), "0.0"));
            Line("     all stages pooled, paired against x1.00:");
            int baseK = Array.IndexOf(SpeedFactors, 1f);
            TempoObs[] pooledBase = Concat(perF[baseK]);
            for (int k = 0; k < SpeedFactors.Length; k++)
            {
                TempoObs[] all = Concat(perF[k]);
                Line("       " + FactorName(SpeedFactors[k]) + " WR " + F(Mean(all, o => o.Win) * 100, "0.0").PadLeft(5) + "%  margin "
                     + Sgn(Mean(all, o => o.Margin)) + "   " + (k == baseK ? "(control)" : DeltaText(pooledBase, all, false)));
            }
            Line("     per stage, the whole range of Speed (always last against always first):");
            for (int i = 0; i < perStage.Count; i++) Line(perStage[i]);
            Line("");

            // --- closeness: the same instrument moved off its band.
            Line("Q1b. THE SAME, WITH THE LINE LEVELLED TO 85% / 100% / 115% OF recommendedPower (always last vs always first)");
            int nClose = 20 * _tempoScale;
            float[] powers = { 0.85f, 1f, 1.15f };
            for (int p = 0; p < powers.Length; p++)
            {
                List<TempoObs[]> lasts = new List<TempoObs[]>(), firsts = new List<TempoObs[]>(), bases = new List<TempoObs[]>();
                for (int si = 0; si < live.AllStages.Count; si++)
                {
                    StageDef stage = live.AllStages[si];
                    StageDef ceil = TempoCeil(stage);
                    GameSession session = BuildPinnedLineAt(live, stage, true, (int)(stage.RecommendedPower * powers[p]));
                    bases.Add(TempoCampaign(session, ceil, stage, nClose, null, null, null));
                    lasts.Add(TempoCampaign(session, ceil, stage, nClose, h => SpeedTimes(h.Def, h.Level, 0.01f), null, null));
                    firsts.Add(TempoCampaign(session, ceil, stage, nClose, h => SpeedTimes(h.Def, h.Level, 100f), null, null));
                }
                TempoObs[] b = Concat(bases), l = Concat(lasts), fi = Concat(firsts);
                Line("     x" + F(powers[p], "0.00") + " power, n=" + b.Length + ": base WR " + F(Mean(b, o => o.Win) * 100, "0.0") + "%  "
                     + "last " + F(Mean(l, o => o.Win) * 100, "0.0") + "%  first " + F(Mean(fi, o => o.Win) * 100, "0.0")
                     + "%   last->first " + DeltaText(l, fi, false));
            }
            Line("");

            // --- duels.
            int nDuel = 150 * _tempoScale;
            Line("Q1c. RAW SPEED IN HERO-VERSUS-HERO DUELS - a line against ITSELF, player side's Speed scaled, level 40,");
            Line("     no armor, no relics, no talents. n=" + nDuel + " seeds per cell. The x1.00 column is the mirror.");
            string[][] lines = { PinnedLine, ChapterOneLine, NormalPlayBalanceLine, TempoFastLine(live, true), TempoFastLine(live, false) };
            string[] names = { "pinned", "ch1 3-star", "balance", "fastest 5", "slowest 5" };
            List<TempoObs[]>[] duelF = new List<TempoObs[]>[SpeedFactors.Length];
            for (int k = 0; k < duelF.Length; k++) duelF[k] = new List<TempoObs[]>();
            for (int li = 0; li < lines.Length; li++)
            {
                string row = "     " + names[li].PadRight(11);
                for (int k = 0; k < SpeedFactors.Length; k++)
                {
                    float f = SpeedFactors[k];
                    List<HeroInstance> team = TempoLine(live, lines[li], 40, d => f == 1f ? d : SpeedTimes(d, 40, f));
                    TempoObs[] o = TempoDuel(live, team, lines[li], 40, nDuel, 5100000 + li * 7777, null);
                    duelF[k].Add(o);
                    row += " " + FactorName(f).Trim() + " " + F(Mean(o, x => x.Win) * 100, "0");
                }
                Line(row);
            }
            TempoObs[] duelBase = Concat(duelF[baseK]);
            Line("     pooled over the five lines, paired against the mirror:");
            for (int k = 0; k < SpeedFactors.Length; k++)
            {
                if (k == baseK) { Line("       x1.00  WR " + F(Mean(duelBase, o => o.Win) * 100, "0.0") + "% (mirror)"); continue; }
                Line("       " + FactorName(SpeedFactors[k]) + DeltaText(duelBase, Concat(duelF[k]), false));
            }
            Line("");

            // --- the mirror itself: how far a line against itself sits from 50%, which every duel
            // delta below is read against.
            {
                List<TempoObs[]> mir = new List<TempoObs[]>();
                int lines5 = 0;
                for (int i = 0; i < live.Heroes.Count; i += 9)
                {
                    string how;
                    string[] l = NormalPlayLineFor(live, PinnedLine, live.Heroes[i], out how);
                    mir.Add(TempoDuel(live, TempoLine(live, l, 40, null), l, 40, nDuel, 5400000 + i * 17, null));
                    lines5++;
                }
                TempoObs[] m = Concat(mir);
                double wr = Mean(m, o => o.Win);
                Line("     MIRROR BIAS: " + lines5 + " lines (the pinned line with every ninth hero of the roster in its role's place), n=" + m.Length
                     + ": the player side wins " + F(wr * 100, "0.0") + "% +/-" + F(Math.Sqrt(wr * (1 - wr) / m.Length) * 100, "0.0")
                     + " against the identical line; mean margin " + Sgn(Mean(m, o => o.Margin)));
            }
            Line("");

            // --- one body at a time: which hero's place in the order the duel curve is made of.
            Line("Q1c'. ONE HERO AT A TIME in the mirror duels: that hero's Speed at x0.01 or x100, the other four as authored, n=" + nDuel);
            string[][] probe = { PinnedLine, ChapterOneLine, NormalPlayBalanceLine };
            string[] probeNames = { "pinned", "ch1 3-star", "balance" };
            for (int li = 0; li < probe.Length; li++)
            {
                TempoObs[] mirror = TempoDuel(live, TempoLine(live, probe[li], 40, null), probe[li], 40, nDuel, 5100000 + li * 7777, null);
                Line("     " + probeNames[li] + " (mirror WR " + F(Mean(mirror, o => o.Win) * 100, "0.0") + "%)");
                for (int h = 0; h < probe[li].Length; h++)
                {
                    string who = probe[li][h];
                    TempoObs[] l = TempoDuel(live, TempoLine(live, probe[li], 40, d => d.Id == who ? SpeedTimes(d, 40, 0.01f) : d), probe[li], 40, nDuel, 5100000 + li * 7777, null);
                    TempoObs[] fi = TempoDuel(live, TempoLine(live, probe[li], 40, d => d.Id == who ? SpeedTimes(d, 40, 100f) : d), probe[li], 40, nDuel, 5100000 + li * 7777, null);
                    Line("       " + who.PadRight(30) + " Speed " + F(StatMath.ForHero(live.GetHero(who), 40).Speed, "0").PadLeft(3)
                         + "  last: " + DeltaText(mirror, l, false) + "   first: " + DeltaText(mirror, fi, false));
                }
            }
            Line("");

            // --- duels at a level gap: the closeness question from the other end.
            Line("Q1d. DUELS AT A LEVEL GAP (player line level 40, the mirror opponent at 34..46), always last vs always first, pinned + balance lines");
            int[] oppLevels = { 34, 37, 40, 43, 46 };
            for (int g = 0; g < oppLevels.Length; g++)
            {
                List<TempoObs[]> bs = new List<TempoObs[]>(), ls = new List<TempoObs[]>(), fs = new List<TempoObs[]>();
                string[][] two = { PinnedLine, NormalPlayBalanceLine };
                for (int li = 0; li < two.Length; li++)
                {
                    bs.Add(TempoDuel(live, TempoLine(live, two[li], 40, null), two[li], oppLevels[g], nDuel, 5200000 + li * 7777, null));
                    ls.Add(TempoDuel(live, TempoLine(live, two[li], 40, d => SpeedTimes(d, 40, 0.01f)), two[li], oppLevels[g], nDuel, 5200000 + li * 7777, null));
                    fs.Add(TempoDuel(live, TempoLine(live, two[li], 40, d => SpeedTimes(d, 40, 100f)), two[li], oppLevels[g], nDuel, 5200000 + li * 7777, null));
                }
                TempoObs[] b = Concat(bs), l = Concat(ls), fi = Concat(fs);
                Line("     opp L" + oppLevels[g] + " n=" + b.Length + ": base WR " + F(Mean(b, o => o.Win) * 100, "0.0") + "%  last "
                     + F(Mean(l, o => o.Win) * 100, "0.0") + "%  first " + F(Mean(fi, o => o.Win) * 100, "0.0") + "%   last->first " + DeltaText(l, fi, false));
            }
            Line("");

            // --- the sweep arena: what Speed does to one hero's own output.
            TempoSweepSpeed(live);
        }

        /// <summary>The five fastest or slowest five-star heroes by base Speed, one per role where
        /// the roster allows, so the duel curve is not read only on lines of middling Speed.</summary>
        private static string[] TempoFastLine(ContentDatabase db, bool fastest)
        {
            List<HeroDef> five = new List<HeroDef>();
            for (int i = 0; i < db.Heroes.Count; i++) if (db.Heroes[i].Stars == 5) five.Add(db.Heroes[i]);
            five.Sort((a, b) => fastest ? b.BaseSpeed.CompareTo(a.BaseSpeed) : a.BaseSpeed.CompareTo(b.BaseSpeed));
            List<string> pick = new List<string>();
            Dictionary<string, bool> roles = new Dictionary<string, bool>();
            for (int i = 0; i < five.Count && pick.Count < 5; i++)
            {
                string role = five[i].Role.ToString();
                if (roles.ContainsKey(role)) continue;
                roles[role] = true;
                pick.Add(five[i].Id);
            }
            for (int i = 0; i < five.Count && pick.Count < 5; i++) if (!pick.Contains(five[i].Id)) pick.Add(five[i].Id);
            return pick.ToArray();
        }

        private static void TempoSweepSpeed(ContentDatabase live)
        {
            ContentDatabase arena = BuildHeroSweepArena(live);
            List<HeroDef> fillers = BuildHeroSweepFillers();
            StageDef stage = arena == null ? null : arena.GetStage("sweep_arena");
            if (stage == null || fillers == null) { Line("Q1e. sweep arena unavailable"); return; }
            int n = 10 * _tempoScale;
            Line("Q1e. RAW SPEED IN THE SWEEP ARENA - each of the " + arena.Heroes.Count + " heroes at level " + HeroSweepLevel
                 + " beside the four fillers, its OWN Speed at x0.01 / x1 / x100,");
            Line("     n=" + n + " seeds per cell. Reads the subject's own output (damage + healing + shielding credited to it).");
            List<string> rows = new List<string>();
            double sumAbsFirst = 0, sumAbsLast = 0;
            int moved5 = 0;
            List<KeyValuePair<double, string>> ranked = new List<KeyValuePair<double, string>>();
            for (int h = 0; h < arena.Heroes.Count; h++)
            {
                HeroDef d = arena.Heroes[h];
                TempoObs[] b = TempoSweep(arena, stage, fillers, d, n);
                TempoObs[] l = TempoSweep(arena, stage, fillers, SpeedTimes(d, HeroSweepLevel, 0.01f), n);
                TempoObs[] fi = TempoSweep(arena, stage, fillers, SpeedTimes(d, HeroSweepLevel, 100f), n);
                double base0 = Mean(b, o => o.Output);
                if (base0 <= 0) continue;
                double dl = Mean(l, o => o.Output) / base0 - 1, df = Mean(fi, o => o.Output) / base0 - 1;
                sumAbsFirst += Math.Abs(df); sumAbsLast += Math.Abs(dl);
                if (Math.Abs(df - dl) >= 0.05) moved5++;
                ranked.Add(new KeyValuePair<double, string>(df - dl, d.Id + "  last " + Pct(dl) + "%  first " + Pct(df) + "%"));
            }
            ranked.Sort((a, b) => Math.Abs(b.Key).CompareTo(Math.Abs(a.Key)));
            Line("     mean |output change|: always first " + F(sumAbsFirst / Math.Max(1, ranked.Count) * 100, "0.0") + "%, always last "
                 + F(sumAbsLast / Math.Max(1, ranked.Count) * 100, "0.0") + "%; " + moved5 + " of " + ranked.Count
                 + " heroes move 5% or more between last and first");
            Line("     the fifteen most Speed-sensitive kits:");
            for (int i = 0; i < ranked.Count && i < 15; i++) Line("       " + ranked[i].Value);
            Line("");
        }

        private static TempoObs[] TempoSweep(ContentDatabase arena, StageDef stage, List<HeroDef> fillers, HeroDef subject, int n)
        {
            TempoObs[] obs = new TempoObs[n];
            for (int s = 0; s < n; s++)
            {
                List<HeroInstance> team = new List<HeroInstance>();
                team.Add(MakeInstance(subject, HeroSweepLevel));
                for (int f = 0; f < fillers.Count; f++) team.Add(MakeInstance(fillers[f], HeroSweepLevel));
                obs[s] = TempoRead(RunWith(arena, stage, HeroSweepSeed(subject.Id, s + 1), team), subject.Id);
            }
            return obs;
        }

        // ================================================================== Q2: acting first

        private static void TempoActingFirst(ContentDatabase live)
        {
            int n = 25 * _tempoScale;
            Line("Q2. ACTING FIRST, SEPARATELY FROM ACTING MORE - campaign CEIL battles, instrument line at recommendedPower,");
            Line("    n=" + (live.AllStages.Count * n) + " per row. Rows are orderings of the SAME fight; nobody gets an extra action in any row.");
            string[] names = { "as authored (speed sort)", "enemies first, round 1 only", "enemies first, rounds 1-3",
                               "enemies first, every round", "heroes first, every round" };
            List<TempoObs[]>[] rows = new List<TempoObs[]>[names.Length];
            for (int k = 0; k < rows.Length; k++) rows[k] = new List<TempoObs[]>();
            List<TempoObs[]> attack = new List<TempoObs[]>();
            float[] atk = { 1.03f, 1.06f, 1.10f, 1.15f };
            List<TempoObs[]>[] atkRows = new List<TempoObs[]>[atk.Length];
            for (int k = 0; k < atk.Length; k++) atkRows[k] = new List<TempoObs[]>();

            for (int si = 0; si < live.AllStages.Count; si++)
            {
                StageDef stage = live.AllStages[si];
                StageDef ceil = TempoCeil(stage);
                GameSession session = BuildPinnedLine(live, stage, true);
                rows[0].Add(TempoCampaign(session, ceil, stage, n, null, null, null));
                rows[1].Add(TempoCampaign(session, ceil, stage, n, null, EnemiesFirst(1), null));
                rows[2].Add(TempoCampaign(session, ceil, stage, n, null, EnemiesFirst(3), null));
                rows[3].Add(TempoCampaign(session, ceil, stage, n, null, EnemiesFirst(999), null));
                rows[4].Add(TempoCampaign(session, ceil, stage, n, h => SpeedTimes(h.Def, h.Level, 100f), null, null));
                for (int k = 0; k < atk.Length; k++)
                {
                    float m = atk[k];
                    atkRows[k].Add(TempoCampaign(session, ceil, stage, n, h => AttackTimes(h.Def, m), null, null));
                }
            }
            TempoObs[] b = Concat(rows[0]);
            for (int k = 0; k < names.Length; k++)
            {
                TempoObs[] a = Concat(rows[k]);
                Line("    " + names[k].PadRight(30) + " WR " + F(Mean(a, o => o.Win) * 100, "0.0").PadLeft(5) + "%  margin " + Sgn(Mean(a, o => o.Margin))
                     + "  turns/fight heroes " + F(Mean(a, o => o.HeroActs), "0.00") + " enemies " + F(Mean(a, o => o.EnemyActs), "0.00")
                     + "  " + (k == 0 ? "(control)" : DeltaText(b, a, false)));
            }
            Line("    for scale, the same fights with every hero's Attack raised and turn order as authored:");
            for (int k = 0; k < atk.Length; k++)
            {
                TempoObs[] a = Concat(atkRows[k]);
                Line("    Attack x" + F(atk[k], "0.00") + "                   WR " + F(Mean(a, o => o.Win) * 100, "0.0").PadLeft(5) + "%  margin "
                     + Sgn(Mean(a, o => o.Margin)) + "  " + DeltaText(b, a, false));
            }
            Line("");
        }

        private static BattleRules EnemiesFirst(int rounds)
        {
            BattleRules r = new BattleRules();
            r.Name = "tempo study: enemies first";
            r.EnemiesActFirstRounds = rounds;
            return r;
        }

        // ================================================================== Q5: the price of Speed

        private static void TempoPowerPrice(ContentDatabase live)
        {
            int n = 25 * _tempoScale;
            Line("Q5. WHAT THE POWER FORMULA CHARGES FOR SPEED, AND WHAT THE SAME POWER BUYS AS ATTACK");
            Line("    PowerScore = 0.11 HP + 2.4 Atk + 1.35 Def + 1.9 Spd + 260 Crit + 120 (CritDmg - 1). Instrument line, geared, at recommendedPower.");
            double spdShareSum = 0; int stages = 0;
            List<TempoObs[]> b = new List<TempoObs[]>(), s10 = new List<TempoObs[]>(), a10 = new List<TempoObs[]>();
            List<TempoObs[]> s20 = new List<TempoObs[]>(), a20 = new List<TempoObs[]>();
            for (int si = 0; si < live.AllStages.Count; si++)
            {
                StageDef stage = live.AllStages[si];
                StageDef ceil = TempoCeil(stage);
                GameSession session = BuildPinnedLine(live, stage, true);
                List<HeroInstance> team = session.Profile.TeamHeroes();
                double spdPower = 0, total = 0;
                Dictionary<string, float> atkFor10 = new Dictionary<string, float>(), atkFor20 = new Dictionary<string, float>();
                for (int i = 0; i < team.Count; i++)
                {
                    HeroInstance h = team[i];
                    UnitStats st = StatMath.ForHero(h.Def, h.Level, null, session.Profile.EquippedArmorOf(h), live.Armor);
                    spdPower += st.Speed * 1.9;
                    total += StatMath.PowerScore(st);
                    UnitStats bare = StatMath.ForHero(h.Def, h.Level);
                    // The Attack that costs the same power as +10% / +20% Speed.
                    atkFor10[h.Uid] = 1f + (float)(0.10 * st.Speed * 1.9 / 2.4 / Math.Max(1f, bare.Attack));
                    atkFor20[h.Uid] = 1f + (float)(0.20 * st.Speed * 1.9 / 2.4 / Math.Max(1f, bare.Attack));
                }
                spdShareSum += spdPower / Math.Max(1, total);
                stages++;
                b.Add(TempoCampaign(session, ceil, stage, n, null, null, null));
                s10.Add(TempoCampaign(session, ceil, stage, n, h => SpeedTimes(h.Def, h.Level, 1.1f), null, null));
                a10.Add(TempoCampaign(session, ceil, stage, n, h => AttackTimes(h.Def, atkFor10[h.Uid]), null, null));
                s20.Add(TempoCampaign(session, ceil, stage, n, h => SpeedTimes(h.Def, h.Level, 1.2f), null, null));
                a20.Add(TempoCampaign(session, ceil, stage, n, h => AttackTimes(h.Def, atkFor20[h.Uid]), null, null));
                if (si == 0 || si == live.AllStages.Count - 1 || si == live.AllStages.Count / 2)
                    Line("    " + stage.DisplayCode + ": Speed is " + F(spdPower / Math.Max(1, total) * 100, "0.0") + "% of the line's power ("
                         + F(spdPower, "0") + " of " + F(total, "0") + "); the Attack costing the same as +10% Speed is x"
                         + F(atkFor10[team[0].Uid], "0.000") + " on " + team[0].Def.Id);
            }
            Line("    Speed's mean share of the line's power across the " + stages + " stages: " + F(spdShareSum / Math.Max(1, stages) * 100, "0.0") + "%");
            TempoObs[] bb = Concat(b);
            Line("    +10% Speed                 " + DeltaText(bb, Concat(s10), false) + "   n=" + bb.Length);
            Line("    the same power as Attack   " + DeltaText(bb, Concat(a10), false));
            Line("    +20% Speed                 " + DeltaText(bb, Concat(s20), false));
            Line("    the same power as Attack   " + DeltaText(bb, Concat(a20), false));
            Line("");
        }

        // ================================================================== Q3: each mechanic

        /// <summary>
        /// One mechanic's ablation: which effects it touches and how each is made inert. Every
        /// neutralisation keeps the effect in its list and its ability in its slot, so the energy
        /// clock, the cast and every other effect of the same ability resolve as authored; only
        /// the tempo the effect carries is gone. Where a passive hangs its payload on the tempo
        /// event itself (OnTurnDelayed, the Turn Debt triggers) that is a separate mechanic,
        /// because removing the shift silences the payload too and the two have to be told apart.
        /// </summary>
        private sealed class TempoMech
        {
            public string Id, What;
            public Func<EffectDef, bool> Touches;
            public Action<EffectDef> Neutralise;
            public Func<PassiveDef, bool> SilencesPassive;
        }

        private static List<TempoMech> TempoMechs()
        {
            List<TempoMech> m = new List<TempoMech>();
            m.Add(new TempoMech { Id = "shift-all", What = "every authored TurnShift, magnitude to 0 (no move, no debt, no OnTurnDelayed)",
                Touches = e => e.Kind == EffectKind.TurnShift && e.Magnitude != 0f, Neutralise = e => e.Magnitude = 0f });
            m.Add(new TempoMech { Id = "shift-delay", What = "TurnShift delays only (negative magnitude)",
                Touches = e => e.Kind == EffectKind.TurnShift && e.Magnitude < 0f, Neutralise = e => e.Magnitude = 0f });
            m.Add(new TempoMech { Id = "shift-advance", What = "TurnShift advances only (positive magnitude)",
                Touches = e => e.Kind == EffectKind.TurnShift && e.Magnitude > 0f, Neutralise = e => e.Magnitude = 0f });
            m.Add(new TempoMech { Id = "on-delayed", What = "passives triggered OnTurnDelayed emptied, the shifts kept",
                SilencesPassive = p => p.Trigger == PassiveTrigger.OnTurnDelayed });
            m.Add(new TempoMech { Id = "turndebt", What = "shiftBanksAs TurnDebt to None: the delay lands, no debt is banked",
                Touches = e => e.ShiftBanksAs == StatusKind.TurnDebt, Neutralise = e => e.ShiftBanksAs = StatusKind.None });
            m.Add(new TempoMech { Id = "disrupted-shift", What = "Disrupted's turn shift (storedCap) to 0, the energy drain kept",
                Touches = e => e.Kind == EffectKind.ApplyStatus && e.Status == StatusKind.Disrupted && e.StoredCap > 0f,
                Neutralise = e => e.StoredCap = 0f });
            m.Add(new TempoMech { Id = "disrupted-all", What = "Disrupted never applied (drain, shift and everything conditioned on it)",
                Touches = e => e.Kind == EffectKind.ApplyStatus && e.Status == StatusKind.Disrupted,
                Neutralise = e => { e.Kind = EffectKind.TurnShift; e.Magnitude = 0f; } });
            m.Add(new TempoMech { Id = "slow", What = "Slow lands with magnitude 0 (the status still stands, it moves no Speed)",
                Touches = e => e.Kind == EffectKind.ApplyStatus && e.Status == StatusKind.Slow && e.Magnitude != 0f,
                Neutralise = e => e.Magnitude = 0f });
            m.Add(new TempoMech { Id = "quickened", What = "Quickened (SpeedUp) lands with magnitude 0",
                Touches = e => e.Kind == EffectKind.ApplyStatus && e.Status == StatusKind.SpeedUp && e.Magnitude != 0f,
                Neutralise = e => e.Magnitude = 0f });
            m.Add(new TempoMech { Id = "swap", What = "SwapTurnOrder made a no-op (its SwapAdvanced/SwapDelayed payloads then resolve to nobody)",
                Touches = e => e.Kind == EffectKind.SwapTurnOrder, Neutralise = e => { e.Kind = EffectKind.TurnShift; e.Magnitude = 0f; } });
            m.Add(new TempoMech { Id = "swap-payload", What = "effects aimed at SwapAdvancedUnit / SwapDelayedUnit made no-ops, the swap kept",
                Touches = e => e.Target == TargetMode.SwapAdvancedUnit || e.Target == TargetMode.SwapDelayedUnit,
                Neutralise = e => { e.Kind = EffectKind.TurnShift; e.Magnitude = 0f; } });
            return m;
        }

        /// <summary>Applies a mechanic's ablation to every hero and enemy in a private database,
        /// in place, and returns the ids of the heroes it touched (talent effects counted separately).</summary>
        private static Dictionary<string, List<string>> TempoAblate(ContentDatabase db, TempoMech mech)
        {
            Dictionary<string, List<string>> touched = new Dictionary<string, List<string>>();
            for (int i = 0; i < db.Heroes.Count; i++) TempoAblateUnit(db.Heroes[i], mech, touched, true);
            for (int i = 0; i < db.Enemies.Count; i++) TempoAblateUnit(db.Enemies[i], mech, touched, false);
            return touched;
        }

        private static void TempoAblateUnit(UnitDef u, TempoMech mech, Dictionary<string, List<string>> touched, bool hero)
        {
            string key = (hero ? "" : "enemy:") + u.Id;
            TempoAblateList(u.BasicAttack == null ? null : u.BasicAttack.Effects, mech, touched, key, "basic");
            TempoAblateList(u.Active == null ? null : u.Active.Effects, mech, touched, key, "active");
            for (int p = 0; p < u.Passives.Count; p++) TempoAblatePassive(u.Passives[p], mech, touched, key, u.Passives[p].Name);
            HeroDef h = u as HeroDef;
            if (h != null)
                for (int t = 0; t < h.Talents.Count; t++)
                {
                    PassiveDef tal = h.Talents[t];
                    TempoAblatePassive(tal, mech, touched, key, "talent " + RomanOf(t + 1));
                    for (int a = 0; a < tal.Appends.Count; a++)
                        if (tal.Appends[a].Effect != null && mech.Touches != null && mech.Touches(tal.Appends[a].Effect))
                        {
                            mech.Neutralise(tal.Appends[a].Effect);
                            TempoMark(touched, key, "talent " + RomanOf(t + 1));
                        }
                }
        }

        private static void TempoAblatePassive(PassiveDef p, TempoMech mech, Dictionary<string, List<string>> touched, string key, string where)
        {
            if (mech.SilencesPassive != null && mech.SilencesPassive(p) && p.Effects.Count > 0)
            {
                p.Effects = new List<EffectDef>();
                TempoMark(touched, key, where);
            }
            TempoAblateList(p.Effects, mech, touched, key, where);
        }

        private static void TempoAblateList(List<EffectDef> effects, TempoMech mech, Dictionary<string, List<string>> touched, string key, string where)
        {
            if (effects == null || mech.Touches == null) return;
            for (int i = 0; i < effects.Count; i++)
                if (mech.Touches(effects[i])) { mech.Neutralise(effects[i]); TempoMark(touched, key, where); }
        }

        private static void TempoMark(Dictionary<string, List<string>> touched, string key, string where)
        {
            List<string> l;
            if (!touched.TryGetValue(key, out l)) { l = new List<string>(); touched[key] = l; }
            if (!l.Contains(where)) l.Add(where);
        }

        private static void TempoMechanics(ContentDatabase live, string contentDir)
        {
            List<TempoMech> mechs = TempoMechs();
            int nSweep = 25 * _tempoScale, nDuel = 100 * _tempoScale, nStage = 5 * _tempoScale;
            Line("Q3. EACH MECHANIC, MEASURED AGAINST A CONTROL WITH IT REMOVED");
            Line("    Per kit, only the subject carries the ablation; its line-mates and the enemies are as authored. No talent is selected");
            Line("    in any fight, so a mechanic carried only by a talent is listed and not measured. Three arenas per kit:");
            Line("      sweep    - the subject beside the four fillers in the hero-sweep arena, n=" + nSweep + "; reads its own output");
            Line("      duel     - the pinned line with the subject in its role's place, level 40, against the same line as authored, n=" + nDuel);
            Line("      campaign - that line geared at each stage's recommendedPower against the stage's CEIL battle, "
                 + live.AllStages.Count + " stages x " + nStage + " = " + (live.AllStages.Count * nStage));
            Line("    Mechanic-wide rows ablate the mechanic in every hero AND enemy at once and field the pinned line on the ladder.");
            Line("");

            ContentDatabase arenaLive = BuildHeroSweepArena(live);
            List<HeroDef> fillers = BuildHeroSweepFillers();
            StageDef sweepStage = arenaLive.GetStage("sweep_arena");

            // Controls are per kit and shared by every mechanic that touches that kit.
            Dictionary<string, TempoObs[]> sweepCtl = new Dictionary<string, TempoObs[]>();
            Dictionary<string, TempoObs[]> duelCtl = new Dictionary<string, TempoObs[]>();
            Dictionary<string, TempoObs[]> campCtl = new Dictionary<string, TempoObs[]>();
            Dictionary<string, List<GameSession>> sessions = new Dictionary<string, List<GameSession>>();

            for (int mi = 0; mi < mechs.Count; mi++)
            {
                TempoMech mech = mechs[mi];
                ContentDatabase abl = LoadContent(contentDir);
                Dictionary<string, List<string>> touched = TempoAblate(abl, mech);
                ContentDatabase arenaAbl = BuildHeroSweepArena(live);
                TempoAblate(arenaAbl, mech);

                Line("  [" + mech.Id + "] " + mech.What);
                List<string> heroIds = new List<string>(), enemyIds = new List<string>();
                foreach (KeyValuePair<string, List<string>> kv in touched)
                {
                    if (kv.Key.StartsWith("enemy:")) enemyIds.Add(kv.Key.Substring(6) + " (" + string.Join(", ", kv.Value.ToArray()) + ")");
                    else heroIds.Add(kv.Key);
                }
                heroIds.Sort(string.CompareOrdinal);
                if (enemyIds.Count > 0) Line("    enemies carrying it: " + string.Join("; ", enemyIds.ToArray()));
                if (heroIds.Count == 0) Line("    no hero carries it");

                List<TempoObs[]> allSweepC = new List<TempoObs[]>(), allSweepT = new List<TempoObs[]>();
                List<TempoObs[]> allDuelC = new List<TempoObs[]>(), allDuelT = new List<TempoObs[]>();
                List<TempoObs[]> allCampC = new List<TempoObs[]>(), allCampT = new List<TempoObs[]>();
                for (int hi = 0; hi < heroIds.Count; hi++)
                {
                    string id = heroIds[hi];
                    List<string> where = touched[id];
                    bool talentOnly = true;
                    for (int w = 0; w < where.Count; w++) if (!where[w].StartsWith("talent")) talentOnly = false;
                    if (talentOnly)
                    {
                        Line("    " + id.PadRight(30) + " talent only (" + string.Join(", ", where.ToArray()) + ") - not measured");
                        continue;
                    }
                    HeroDef liveDef = live.GetHero(id), ablDef = abl.GetHero(id);

                    // sweep
                    TempoObs[] sc;
                    if (!sweepCtl.TryGetValue(id, out sc))
                    {
                        sc = TempoSweep(arenaLive, sweepStage, fillers, arenaLive.GetHero(id), nSweep);
                        sweepCtl[id] = sc;
                    }
                    TempoObs[] st = TempoSweep(arenaAbl, arenaAbl.GetStage("sweep_arena"), fillers, arenaAbl.GetHero(id), nSweep);

                    // duel
                    string how;
                    string[] line = NormalPlayLineFor(live, PinnedLine, liveDef, out how);
                    TempoObs[] dc;
                    if (!duelCtl.TryGetValue(id, out dc))
                    {
                        dc = TempoDuel(live, TempoLine(live, line, 40, null), line, 40, nDuel, 5300000, id);
                        duelCtl[id] = dc;
                    }
                    TempoObs[] dt = TempoDuel(live, TempoLine(live, line, 40, d => d.Id == id ? ablDef : d), line, 40, nDuel, 5300000, id);

                    // campaign
                    List<GameSession> ss;
                    if (!sessions.TryGetValue(id, out ss))
                    {
                        ss = new List<GameSession>();
                        for (int si = 0; si < live.AllStages.Count; si++)
                            ss.Add(NormalPlaySession(live, live.AllStages[si], line));
                        sessions[id] = ss;
                    }
                    TempoObs[] cc;
                    if (!campCtl.TryGetValue(id, out cc))
                    {
                        List<TempoObs[]> parts = new List<TempoObs[]>();
                        for (int si = 0; si < live.AllStages.Count; si++)
                            parts.Add(TempoCampaign(ss[si], TempoCeil(live.AllStages[si]), live.AllStages[si], nStage, null, null, id));
                        cc = Concat(parts);
                        campCtl[id] = cc;
                    }
                    List<TempoObs[]> tparts = new List<TempoObs[]>();
                    for (int si = 0; si < live.AllStages.Count; si++)
                        tparts.Add(TempoCampaign(ss[si], TempoCeil(live.AllStages[si]), live.AllStages[si], nStage,
                                                 h => h.Def.Id == id ? ablDef : h.Def, null, id));
                    TempoObs[] ct = Concat(tparts);

                    allSweepC.Add(sc); allSweepT.Add(st); allDuelC.Add(dc); allDuelT.Add(dt); allCampC.Add(cc); allCampT.Add(ct);

                    // Sign convention: removed minus present, then negated, so a positive number is
                    // what the mechanic is WORTH to the side carrying it.
                    Line("    " + id.PadRight(30) + " (" + string.Join(", ", where.ToArray()) + ")   turn-order moves by the subject per fight, as authored: sweep "
                         + F(Mean(sc, o => o.Shifts), "0.0") + ", duel " + F(Mean(dc, o => o.Shifts), "0.0") + ", campaign " + F(Mean(cc, o => o.Shifts), "0.0")
                         + "; ablated: " + F(Mean(st, o => o.Shifts), "0.0") + " / " + F(Mean(dt, o => o.Shifts), "0.0") + " / " + F(Mean(ct, o => o.Shifts), "0.0"));
                    Line("      sweep    " + DeltaText(st, sc, true));
                    Line("      duel     " + DeltaText(dt, dc, false) + "   [mirror WR " + F(Mean(dc, o => o.Win) * 100, "0.0") + "%; subject dies "
                         + F(Mean(dc, o => o.SubjectDied) * 100, "0") + "% with it, " + F(Mean(dt, o => o.SubjectDied) * 100, "0") + "% without]");
                    Line("      campaign " + DeltaText(ct, cc, false) + "   [WR with it " + F(Mean(cc, o => o.Win) * 100, "0.0") + "%]");
                }
                if (allSweepC.Count > 1)
                {
                    Line("    pooled over the " + allSweepC.Count + " measured kits (worth of the mechanic, present minus removed):");
                    Line("      sweep    " + DeltaText(Concat(allSweepT), Concat(allSweepC), true));
                    Line("      duel     " + DeltaText(Concat(allDuelT), Concat(allDuelC), false));
                    Line("      campaign " + DeltaText(Concat(allCampT), Concat(allCampC), false));
                }

                // Mechanic-wide on the ladder: the pinned line, every carrier ablated at once.
                if (enemyIds.Count > 0 || TempoLineCarries(touched, PinnedLine) || TempoLineCarries(touched, ChapterOneLine))
                {
                    List<TempoObs[]> c = new List<TempoObs[]>(), t = new List<TempoObs[]>();
                    for (int si = 0; si < live.AllStages.Count; si++)
                    {
                        StageDef stage = live.AllStages[si];
                        GameSession sl = BuildPinnedLine(live, stage, true);
                        c.Add(TempoCampaign(sl, TempoCeil(stage), stage, nStage * 2, null, null, null));
                        GameSession sa = BuildPinnedLine(abl, abl.AllStages[si], true);
                        t.Add(TempoCampaign(sa, TempoCeil(abl.AllStages[si]), abl.AllStages[si], nStage * 2, null, null, null));
                    }
                    Line("    mechanic-wide, instrument lines on the ladder (heroes AND enemies ablated), n=" + Concat(c).Length + ": "
                         + DeltaText(Concat(t), Concat(c), false));
                }
                Line("");
            }
        }

        /// <summary>
        /// Two readings in Q3 that have an explanation other than tempo, each put to a direct test
        /// instead of being argued: Oris's Quickened (does it cost him only when the far side
        /// carries a HighestSpeedEnemy strike?) and the delays that read negative in the duel (is
        /// that the delay landing on the opposing Kael, whose kit pays more the later he acts?).
        /// </summary>
        private static void TempoProbes(ContentDatabase live, string contentDir)
        {
            int n = 100 * _tempoScale;
            TempoMech quick = null, delay = null;
            List<TempoMech> all = TempoMechs();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Id == "quickened") quick = all[i];
                if (all[i].Id == "shift-delay") delay = all[i];
            }

            Line("Q4a. ORIS'S QUICKENED against opponents with and without an Oris of their own, n=" + n + " per row");
            ContentDatabase q = LoadContent(contentDir);
            TempoAblate(q, quick);
            const string oris = "oris_the_unwritten_path";
            string how;
            string[] orisLine = NormalPlayLineFor(live, PinnedLine, live.GetHero(oris), out how);
            string[][] opps = { orisLine, PinnedLine, NormalPlayBalanceLine };
            string[] oppNames = { "mirror (opposing Oris)", "pinned line (no Oris)", "balance line (no Oris)" };
            for (int o = 0; o < opps.Length; o++)
            {
                TempoObs[] c = TempoDuel(live, TempoLine(live, orisLine, 40, null), opps[o], 40, n, 5500000 + o * 911, oris);
                TempoObs[] t = TempoDuel(live, TempoLine(live, orisLine, 40, d => d.Id == oris ? q.GetHero(oris) : d), opps[o], 40, n, 5500000 + o * 911, oris);
                Line("     vs " + oppNames[o].PadRight(24) + " WR with it " + F(Mean(c, x => x.Win) * 100, "0.0") + "%  worth: " + DeltaText(t, c, false)
                     + "  [Oris dies " + F(Mean(c, x => x.SubjectDied) * 100, "0") + "% with it, " + F(Mean(t, x => x.SubjectDied) * 100, "0") + "% without]");
            }
            Line("");

            Line("Q4b. DELAYS AGAINST THE OPPOSING BULWARK: the delay kits' duel reading against their line as authored (Kael at the");
            Line("     front) and against the same line with Kael replaced by another five-star Bulwark, n=" + n + " per row");
            ContentDatabase dl = LoadContent(contentDir);
            TempoAblate(dl, delay);
            string[] subjects = { "till_the_shifting_target", "jax_the_clockwork_rat", "tamsin_the_loose_thread",
                                  "sable_the_queue_cutter", "aeron_the_unbroken_stride" };
            string[] walls = { "kael_the_unyielding", "rovan_the_adaptive_commander", "brenna_the_remaining_wall" };
            for (int si = 0; si < subjects.Length; si++)
            {
                string id = subjects[si];
                string[] line = NormalPlayLineFor(live, PinnedLine, live.GetHero(id), out how);
                string row = "     " + id.PadRight(28);
                for (int w = 0; w < walls.Length; w++)
                {
                    string[] opp = (string[])line.Clone();
                    for (int k = 0; k < opp.Length; k++) if (opp[k] == "kael_the_unyielding") opp[k] = walls[w];
                    TempoObs[] c = TempoDuel(live, TempoLine(live, line, 40, null), opp, 40, n, 5600000 + si * 313, id);
                    TempoObs[] t = TempoDuel(live, TempoLine(live, line, 40, d => d.Id == id ? dl.GetHero(id) : d), opp, 40, n, 5600000 + si * 313, id);
                    double wm, ws;
                    Paired(t, c, x => x.Win, out wm, out ws);
                    row += "  vs " + walls[w].Substring(0, walls[w].IndexOf('_')) + " " + Pct(wm) + "pp +/-" + F(ws * 100, "0.0");
                }
                Line(row);
            }
            Line("");
        }

        private static bool TempoLineCarries(Dictionary<string, List<string>> touched, string[] line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                List<string> w;
                if (!touched.TryGetValue(line[i], out w)) continue;
                for (int k = 0; k < w.Count; k++) if (!w[k].StartsWith("talent")) return true;
            }
            return false;
        }
    }
}
