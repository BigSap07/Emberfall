using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE PARITY STUDY: do the harness's fixed-arena instruments (the sweep arena behind the star
    /// band, the authored-talent sweep and TALENTS WORTH PICKING) undervalue what only pays when a
    /// fight is close? Run with --parity-study. Not a check and not run by the gate: it asserts
    /// nothing, writes nothing and changes no content. Every variant is a private clone of a def,
    /// so the live database - and the files - are never touched. Docs/PARITY-STUDY.md is the reading.
    ///
    /// The tempo study (TempoStudy.cs) found that a mechanic's worth in win rate concentrates
    /// where the two sides are even. The sweep arena is the opposite of even by construction:
    /// every enemy there has 190,000 HP or more, so it can only read a hero's own output. This
    /// reads the same talents and the same heroes in arenas where the two sides are matched.
    ///
    /// Fights run in parallel. CombatSimulation is a pure function of its request and the core
    /// holds no mutable static state; the controls section proves that the parallel run
    /// reproduces the serial one fight for fight before anything else is trusted.
    /// </summary>
    internal static partial class Program
    {
        private struct ParObs
        {
            public float Win;
            public float Margin;
            public float Output;
            public string Hash;
        }

        /// <summary>One paired reading of one arm against its control.</summary>
        private sealed class ParDelta
        {
            public int N, Changed;
            public double Wm, Ws, Mm, Ms, Om, Os, BaseWin, BaseMargin, BaseOut;
            public double Tw { get { return ParT(Wm, Ws); } }
            public double Tm { get { return ParT(Mm, Ms); } }
            public double To { get { return ParT(Om, Os); } }
        }

        private static double ParT(double mean, double se)
        {
            if (se > 0) return mean / se;
            return mean < 0 ? -99 : mean > 0 ? 99 : 0;
        }

        private static readonly List<string> _parFaults = new List<string>();
        private static int _parScale = 1;
        private static float[] _parLadderPowers;

        private static int RunParityStudy(string contentDir, string mode)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            ContentDatabase live = LoadContent(contentDir);
            if (live == null) return 1;
            _parScale = mode == "quick" ? 1 : 4;
            Stopwatch clock = Stopwatch.StartNew();

            Line("PARITY STUDY (--parity-study " + (mode ?? "full") + ")");
            Line("margin = share of HP the player side kept minus share the far side kept, in [-1, 1]; a win is a margin above 0");
            Line("deltas are PAIRED: the same seeds in both arms, mean of the per-seed difference, +/- one standard error; t = mean / se");
            Line("worker threads: " + Environment.ProcessorCount);
            Line("");

            bool all = mode == null || mode == "quick";
            if (all || mode == "controls" || mode == "calib" || mode == "talents") ParCalibrateLadder(live);
            if (all || mode == "controls") ParControls(live);
            if (all || mode == "talents") ParTalents(live);
            if (all || mode == "stars") ParStars(live);

            Line("");
            Line("fights that threw in parallel: " + _parFaults.Count + (_parFaults.Count > 0 ? "; the first:\n" + _parFaults[0] : ""));
            Line("parity study done in " + F(clock.ElapsedMilliseconds / 1000.0, "0.0") + "s");
            return 0;
        }

        // ================================================================== fights

        private static ParObs ParRead(BattleResult r, string subjectId)
        {
            ParObs o = new ParObs();
            if (r == null) { o.Hash = "NULL"; return o; }
            o.Win = r.Victory ? 1f : 0f;
            o.Margin = r.PlayerHpRatio - r.EnemyHpRatio;
            o.Output = subjectId == null ? 0f : OutputOf(r, subjectId);
            o.Hash = HeroSweepHash(r);
            return o;
        }

        private static ParObs[] ParRun(int n, Func<int, BattleResult> fight, string subjectId)
        {
            ParObs[] o = new ParObs[n];
            Parallel.For(0, n, i =>
            {
                try { o[i] = ParRead(fight(i), subjectId); }
                catch (Exception e)
                {
                    // Reported, never swallowed: a fight that throws in parallel is re-run serially
                    // below, and the study says whether the serial run throws too.
                    lock (_parFaults) _parFaults.Add(i + ": " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
                    o[i].Hash = "FAULT";
                }
            });
            for (int i = 0; i < n; i++)
            {
                if (o[i].Hash != "FAULT") continue;
                try { o[i] = ParRead(fight(i), subjectId); Line("   [fault] fight " + i + " threw in parallel and ran clean serially: " + _parFaults[_parFaults.Count - 1].Split('\n')[0]); }
                catch (Exception e) { Line("   [fault] fight " + i + " throws serially too: " + e); throw; }
            }
            return o;
        }

        private static int ParSeedBase(string id)
        {
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < id.Length; i++) { hash ^= id[i]; hash *= 1099511628211UL; }
            return 20000000 + (int)(hash % 5000000UL) * 3;
        }

        private static StageDef _parDuelStage;

        private static StageDef ParDuelStage(ContentDatabase db)
        {
            if (_parDuelStage == null) _parDuelStage = CampaignWaveGenerator.BuildBattle(db.AllStages[0], 1, 1);
            return _parDuelStage;
        }

        private static BattleLoadout ParLoadout(ContentDatabase db, string[] ids, int level)
        {
            BattleLoadout line = new BattleLoadout();
            line.Label = "parity duel";
            for (int i = 0; i < ids.Length; i++)
            {
                HeroDef d = db.GetHero(ids[i]);
                LoadoutUnit u = new LoadoutUnit();
                u.DefId = d.Id;
                u.Level = level;
                u.Stars = d.Stars;
                line.Units.Add(u);
            }
            return line;
        }

        /// <summary>
        /// A duel arm: the player line at level 40 with the subject's def and talent as given, the
        /// far side the named line at `oppLevel`, unarmoured, no relics, no talents. `line` may be
        /// shorter than five (the empty-slot baselines of the star reading).
        /// </summary>
        private static ParObs[] ParDuel(ContentDatabase db, string[] line, string subjectId, HeroDef subjectDef, int talent,
                                        string[] opponent, int oppLevel, int n, int seedBase)
        {
            BattleLoadout opp = ParLoadout(db, opponent, oppLevel);
            StageDef arena = ParDuelStage(db);
            return ParRun(n, i =>
            {
                List<HeroInstance> team = new List<HeroInstance>();
                for (int k = 0; k < line.Length; k++)
                {
                    bool me = subjectId != null && line[k] == subjectId;
                    HeroDef d = me && subjectDef != null ? subjectDef : db.GetHero(line[k]);
                    HeroInstance h = MakeInstance(d, 40);
                    if (me) h.SelectedTalent = talent;
                    team.Add(h);
                }
                BattleRequest req = new BattleRequest();
                req.Content = db;
                req.Stage = arena;
                req.Opponent = opp;
                req.Seed = seedBase + i * 131;
                req.Team = team;
                return CombatSimulation.Run(req);
            }, subjectId);
        }

        /// <summary>A line geared and levelled to a fraction of every stage's recommendedPower.</summary>
        private sealed class ParLadder
        {
            public List<GameSession> Sessions = new List<GameSession>();
            public List<StageDef> Stages = new List<StageDef>();
            public List<StageDef> Ceils = new List<StageDef>();
            public List<List<HeroInstance>> Teams = new List<List<HeroInstance>>();
        }

        /// <summary>power = 0 means each stage's own parity power, and the stages without one are skipped.</summary>
        private static ParLadder ParBuildLadder(ContentDatabase db, string[] line, float power)
        {
            ParLadder l = new ParLadder();
            for (int si = 0; si < db.AllStages.Count; si++)
            {
                StageDef stage = db.AllStages[si];
                float f = power > 0 ? power : _parLadderPowers[si];
                if (float.IsNaN(f)) continue;
                GameSession s = BuildLineAt(db, stage, true, (int)(stage.RecommendedPower * f), line);
                l.Sessions.Add(s);
                l.Stages.Add(stage);
                l.Ceils.Add(TempoCeil(stage));
                l.Teams.Add(s.Profile.TeamHeroes());
            }
            return l;
        }

        /// <summary>A ladder arm: every stage's CEIL battle, perStage seeds each, the subject's def
        /// and talent as given and every other hero with no talent selected.</summary>
        private static ParObs[] ParLadderArm(ContentDatabase db, ParLadder l, string subjectId, HeroDef subjectDef, int talent, int perStage)
        {
            int stages = l.Stages.Count;
            return ParRun(stages * perStage, i =>
            {
                int si = i / perStage, k = i % perStage;
                List<HeroInstance> src = l.Teams[si];
                List<HeroInstance> team = new List<HeroInstance>();
                for (int j = 0; j < src.Count; j++)
                {
                    HeroInstance c = (HeroInstance)_memberwiseClone.Invoke(src[j], null);
                    c.SelectedTalent = 0;
                    if (subjectId != null && c.Def != null && c.Def.Id == subjectId)
                    {
                        if (subjectDef != null) c.Def = subjectDef;
                        c.SelectedTalent = talent;
                    }
                    team.Add(c);
                }
                BattleRequest req = new BattleRequest();
                req.Content = db;
                req.Stage = l.Ceils[si];
                req.Seed = TempoCampaignSeed(l.Stages[si], k);
                req.Team = team;
                req.Profile = l.Sessions[si].Profile;
                return CombatSimulation.Run(req);
            }, subjectId);
        }

        // ================================================================== statistics

        private static void ParPaired(ParObs[] a, ParObs[] b, Func<ParObs, float> f, out double mean, out double se)
        {
            int n = Math.Min(a.Length, b.Length);
            double s = 0;
            for (int i = 0; i < n; i++) s += f(b[i]) - f(a[i]);
            mean = n == 0 ? 0 : s / n;
            double ss = 0;
            for (int i = 0; i < n; i++) { double d = f(b[i]) - f(a[i]) - mean; ss += d * d; }
            se = n > 1 ? Math.Sqrt(ss / (n - 1) / n) : 0;
        }

        private static double ParMean(ParObs[] a, Func<ParObs, float> f)
        {
            double s = 0;
            for (int i = 0; i < a.Length; i++) s += f(a[i]);
            return a.Length == 0 ? 0 : s / a.Length;
        }

        private static ParDelta ParCompare(ParObs[] control, ParObs[] treated)
        {
            ParDelta d = new ParDelta();
            d.N = Math.Min(control.Length, treated.Length);
            for (int i = 0; i < d.N; i++) if (control[i].Hash != treated[i].Hash) d.Changed++;
            ParPaired(control, treated, o => o.Win, out d.Wm, out d.Ws);
            ParPaired(control, treated, o => o.Margin, out d.Mm, out d.Ms);
            ParPaired(control, treated, o => o.Output, out d.Om, out d.Os);
            d.BaseWin = ParMean(control, o => o.Win);
            d.BaseMargin = ParMean(control, o => o.Margin);
            d.BaseOut = ParMean(control, o => o.Output);
            return d;
        }

        private static string ParShow(ParDelta d)
        {
            string o = d.BaseOut > 0 ? Pct(d.Om / d.BaseOut) + "% t" + F(d.To, "0.0") : "n/a";
            return "chg " + d.Changed + "/" + d.N
                 + "  dWR " + Pct(d.Wm) + " +/-" + F(d.Ws * 100, "0.0") + " (t" + F(d.Tw, "0.0") + ")"
                 + "  dMrg " + Sgn(d.Mm) + " +/-" + F(d.Ms, "0.000") + " (t" + F(d.Tm, "0.0") + ")"
                 + "  dOut " + o;
        }

        /// <summary>
        /// The class a set of readings puts a talent in, by TALENTS WORTH PICKING's own rule: it
        /// LOSES when any reading sits below zero by two standard errors or more, else GAINS when
        /// any sits above zero by two or more, else it is FLAT.
        /// </summary>
        private static string ParClass(List<double> ts, List<double> means)
        {
            bool gain = false;
            for (int i = 0; i < ts.Count; i++)
            {
                if (means[i] < 0 && ts[i] <= TalentValueLossT) return "lose";
                if (ts[i] >= -TalentValueLossT) gain = true;
            }
            return gain ? "gain" : "flat";
        }

        // ================================================================== ladder calibration

        private static readonly float[] ParLadderFactors = {
            0.70f, 0.74f, 0.78f, 0.82f, 0.86f, 0.88f, 0.90f, 0.92f, 0.94f, 0.96f, 0.98f, 1.00f, 1.03f, 1.06f, 1.10f, 1.15f, 1.20f, 1.30f };

        /// <summary>
        /// PER STAGE, the power (a fraction of recommendedPower) at which the pinned line wins
        /// about half of that stage's CEIL battles. A single factor for the whole ladder is not
        /// parity: pooled, the line bottoms out near a third however far its power is cut, because
        /// some CEILs are won at any power the instrument can level to and others are lost at any.
        /// Half of a pooled ladder is then a mix of fights nobody could lose and fights nobody
        /// could win, which is the opposite of close. A stage whose best factor still reads outside
        /// 25%..75%, or whose best is the grid's lowest factor (where the reading is the level
        /// floor rather than a power), is left out of the parity ladder and named. NaN marks it.
        /// </summary>
        private static void ParCalibrateLadder(ContentDatabase live)
        {
            if (_parLadderPowers != null) return;
            int per = 10 * _parScale;
            Line("L. LADDER CALIBRATION - the pinned line geared and levelled to x of each stage's recommendedPower against that");
            Line("   stage's CEIL battle, " + per + " seeds per cell, " + ParLadderFactors.Length + " factors (MEASURED). The factor nearest 50% is the");
            Line("   stage's parity power; a stage whose nearest reading is outside 25%..75%, or sits at the lowest factor (the");
            Line("   line's level floor), is excluded from the parity ladder.");
            _parLadderPowers = new float[live.AllStages.Count];
            double[] pooled = new double[ParLadderFactors.Length];
            int kept = 0;
            for (int si = 0; si < live.AllStages.Count; si++)
            {
                StageDef stage = live.AllStages[si];
                StageDef ceil = TempoCeil(stage);
                string row = "   " + stage.DisplayCode.PadRight(5);
                float best = float.NaN; double bestGap = 9, bestWr = 0;
                for (int k = 0; k < ParLadderFactors.Length; k++)
                {
                    float f = ParLadderFactors[k];
                    GameSession s = BuildLineAt(live, stage, true, (int)(stage.RecommendedPower * f), PinnedLine);
                    ParLadder l = new ParLadder();
                    l.Sessions.Add(s); l.Stages.Add(stage); l.Ceils.Add(ceil); l.Teams.Add(s.Profile.TeamHeroes());
                    double wr = ParMean(ParLadderArm(live, l, null, null, 0, per), x => x.Win);
                    pooled[k] += wr / live.AllStages.Count;
                    row += " " + F(wr * 100, "0").PadLeft(3);
                    if (Math.Abs(wr - 0.5) < bestGap) { bestGap = Math.Abs(wr - 0.5); best = f; bestWr = wr; }
                }
                // At the grid's lowest factor the reading is the line's level floor, not a choice.
                bool ok = bestGap <= 0.25 && best > ParLadderFactors[0];
                _parLadderPowers[si] = ok ? best : float.NaN;
                if (ok) kept++;
                Line(row + "   -> " + (ok ? "x" + F(best, "0.00") + " (" + F(bestWr * 100, "0") + "%)" : "EXCLUDED, nearest " + F(bestWr * 100, "0") + "%"));
            }
            string head = "   factor";
            string pool = "   pooled";
            for (int k = 0; k < ParLadderFactors.Length; k++) { head += " " + F(ParLadderFactors[k], ".00").PadLeft(3); pool += " " + F(pooled[k] * 100, "0").PadLeft(3); }
            Line(head);
            Line(pool);
            Line("   " + kept + " of " + live.AllStages.Count + " stages have a parity power and form the parity ladder");
            Line("");
        }

        // ================================================================== controls

        private static readonly string[] ParControlHeroes = {
            "kael_the_unyielding", "aeron_the_unbroken_stride", "nereia_the_field_medic",
            "seraphine_the_closed_volume", "isolde_the_slow_fading", "dahlia_the_wayward_hymn" };

        private static readonly FieldInfo _talentsField = typeof(HeroDef).GetField("Talents");

        /// <summary>The def with a fourth, empty talent appended: selecting it runs the whole talent
        /// path (AddPassive, overrides, appends) with nothing in it.</summary>
        private static HeroDef ParPlacebo(HeroDef d)
        {
            HeroDef c = TempoClone(d);
            List<PassiveDef> t = new List<PassiveDef>(d.Talents);
            PassiveDef p = new PassiveDef();
            p.Id = "parity_placebo";
            p.Name = "Placebo";
            t.Add(p);
            _talentsField.SetValue(c, t);
            return c;
        }

        private static HeroDef ParStat(HeroDef d, float atk, float def, float hp, float spdAt40)
        {
            HeroDef c = TempoClone(d);
            c.BaseAttack = d.BaseAttack * atk;
            c.BaseDefense = d.BaseDefense * def;
            c.BaseHp = d.BaseHp * hp;
            if (spdAt40 != 1f)
            {
                float grown = 39 * 0.35f;
                c.BaseSpeed = (d.BaseSpeed + grown) * spdAt40 - grown;
            }
            return c;
        }

        private sealed class ParVariant
        {
            public string Name;
            public Func<HeroDef, HeroDef> Make;
            public bool Placebo;
        }

        private static void ParControls(ContentDatabase live)
        {
            int nDuel = 500 * _parScale, perStage = 15 * _parScale;
            Line("C. CONTROLS - can the parity arenas tell a talent that works from one that does nothing?");
            Line("   duel   : the pinned line with the subject in its role's place, level 40, against the same line with no talent,");
            Line("            n=" + nDuel + " seeds per arm");
            Line("   ladder : that line at each stage's parity power (section L) against its CEIL, " + perStage + " seeds a stage");
            Line("   sweep  : the hero-sweep arena, " + TalentValueSeeds + " seeds (TALENTS WORTH PICKING's own count), own output only");
            Line("");

            // C1. the parallel runner is the serial one.
            {
                string[] line = PinnedLine;
                ParObs[] par = ParDuel(live, line, null, null, 0, line, 40, 400, 9100000);
                int same = 0;
                BattleLoadout opp = ParLoadout(live, line, 40);
                for (int i = 0; i < par.Length; i++)
                {
                    List<HeroInstance> team = TempoLine(live, line, 40, null);
                    BattleRequest req = new BattleRequest();
                    req.Content = live; req.Stage = ParDuelStage(live); req.Opponent = opp; req.Seed = 9100000 + i * 131; req.Team = team;
                    if (HeroSweepHash(CombatSimulation.Run(req)) == par[i].Hash) same++;
                }
                Line("   C1 parallel = serial: " + same + " of " + par.Length + " duel fights byte-identical between the parallel and a serial run"
                     + (same == par.Length ? "" : "   <- THE PARALLEL RUNNER IS NOT TRUSTWORTHY"));
                ParLadder l = ParBuildLadder(live, PinnedLine, 0f);
                ParObs[] a = ParLadderArm(live, l, null, null, 0, 20);
                ParObs[] b = ParLadderArm(live, l, null, null, 0, 20);
                int sameL = 0;
                for (int i = 0; i < a.Length; i++) if (a[i].Hash == b[i].Hash) sameL++;
                Line("      ladder, two parallel runs of the same " + a.Length + " fights: " + sameL + " identical");
            }

            ContentDatabase arena = BuildHeroSweepArena(live);
            List<HeroDef> fillers = BuildHeroSweepFillers();
            StageDef sweepStage = arena.GetStage("sweep_arena");

            List<ParVariant> variants = new List<ParVariant>();
            variants.Add(new ParVariant { Name = "placebo talent (an empty 4th talent, selected)", Make = ParPlacebo, Placebo = true });
            variants.Add(new ParVariant { Name = "sham: Attack x1.002 (diverges fights, worth ~nothing)", Make = d => ParStat(d, 1.002f, 1f, 1f, 1f) });
            variants.Add(new ParVariant { Name = "Attack x1.05", Make = d => ParStat(d, 1.05f, 1f, 1f, 1f) });
            variants.Add(new ParVariant { Name = "Max HP x1.10 (survival only)", Make = d => ParStat(d, 1f, 1f, 1.10f, 1f) });
            variants.Add(new ParVariant { Name = "Defense x1.25 (survival only)", Make = d => ParStat(d, 1f, 1.25f, 1f, 1f) });
            variants.Add(new ParVariant { Name = "Speed x1.10 (tempo only)", Make = d => ParStat(d, 1f, 1f, 1f, 1.10f) });

            Line("   C2 synthetic subjects, per hero and pooled over " + ParControlHeroes.Length + " heroes (worth = variant minus as authored):");
            Dictionary<string, ParObs[]> duelCtl = new Dictionary<string, ParObs[]>(), ladCtl = new Dictionary<string, ParObs[]>();
            Dictionary<string, List<long>> sweepCtl = new Dictionary<string, List<long>>();
            Dictionary<string, ParLadder> ladders = new Dictionary<string, ParLadder>();
            for (int v = 0; v < variants.Count; v++)
            {
                ParVariant pv = variants[v];
                Line("   [" + pv.Name + "]");
                List<ParObs[]> dc = new List<ParObs[]>(), dt = new List<ParObs[]>(), lc = new List<ParObs[]>(), lt = new List<ParObs[]>();
                for (int h = 0; h < ParControlHeroes.Length; h++)
                {
                    string id = ParControlHeroes[h];
                    HeroDef liveDef = live.GetHero(id);
                    string how;
                    string[] line = NormalPlayLineFor(live, PinnedLine, liveDef, out how);
                    int seed = ParSeedBase(id);
                    ParObs[] c;
                    if (!duelCtl.TryGetValue(id, out c)) { c = ParDuel(live, line, id, null, 0, line, 40, nDuel, seed); duelCtl[id] = c; }
                    HeroDef variant = pv.Make(liveDef);
                    int sel = pv.Placebo ? variant.Talents.Count : 0;
                    ParObs[] t = ParDuel(live, line, id, variant, sel, line, 40, nDuel, seed);
                    ParLadder l;
                    if (!ladders.TryGetValue(id, out l)) { l = ParBuildLadder(live, line, 0f); ladders[id] = l; }
                    ParObs[] lcc;
                    if (!ladCtl.TryGetValue(id, out lcc)) { lcc = ParLadderArm(live, l, id, null, 0, perStage); ladCtl[id] = lcc; }
                    ParObs[] ltt = ParLadderArm(live, l, id, variant, sel, perStage);

                    // The sweep arena's reading of the same variant: own output, the TWP seeds.
                    HeroDef sDef = arena.GetHero(id);
                    List<long> sc;
                    if (!sweepCtl.TryGetValue(id, out sc))
                    {
                        sc = new List<long>();
                        for (int s = 1; s <= TalentValueSeeds; s++) sc.Add(OutputOf(RunSweepBattle(arena, sweepStage, fillers, sDef, 0, s), id));
                        sweepCtl[id] = sc;
                    }
                    HeroDef sVar = pv.Make(sDef);
                    List<long> st = new List<long>();
                    for (int s = 1; s <= TalentValueSeeds; s++)
                        st.Add(OutputOf(RunSweepBattle(arena, sweepStage, fillers, sVar, pv.Placebo ? sVar.Talents.Count : 0, s), id));
                    TalentValue sv = PairUp(sc, st);

                    dc.Add(c); dt.Add(t); lc.Add(lcc); lt.Add(ltt);
                    Line("     " + id.PadRight(30) + " sweep " + (sv.Without > 0 ? Pct(sv.Mean / sv.Without) : "n/a") + "% t" + F(sv.T, "0.0"));
                    Line("       duel   " + ParShow(ParCompare(c, t)));
                    Line("       ladder " + ParShow(ParCompare(lcc, ltt)));
                }
                Line("     pooled duel   " + ParShow(ParCompare(Concat(dc), Concat(dt))));
                Line("     pooled ladder " + ParShow(ParCompare(Concat(lc), Concat(lt))));
            }
            {
                ParObs[] m = Concat(new List<ParObs[]>(duelCtl.Values));
                double wr = ParMean(m, o => o.Win);
                Line("   C3 mirror: the control arms of the duel, n=" + m.Length + ", player side wins " + F(wr * 100, "0.0") + "% +/-"
                     + F(Math.Sqrt(wr * (1 - wr) / m.Length) * 100, "0.0"));
            }
            Line("");
        }

        private static ParObs[] Concat(List<ParObs[]> parts)
        {
            int n = 0;
            for (int i = 0; i < parts.Count; i++) n += parts[i].Length;
            ParObs[] all = new ParObs[n];
            int k = 0;
            for (int i = 0; i < parts.Count; i++)
                for (int j = 0; j < parts[i].Length; j++) all[k++] = parts[i][j];
            return all;
        }

        // ================================================================== talents

        private sealed class ParTalentRow
        {
            public string Pair, Name;
            public string Today, TodayText;
            public bool TodayMoved;
            public ParDelta Duel, Ladder, Live;
            public string ByMargin, ByWin, ByOutput;
            public string Hero;
            public double TodayPct, TodayT;
            /// <summary>Worth in % Attack: the reading over what Attack x1.05 reads in the same
            /// arena, pooled over the heroes measured, times five.</summary>
            public double EqToday, EqDuel, EqLadder;
            public string MatToday, MatParity;
        }

        /// <summary>A class counts as MATERIAL when some reading carrying it is worth at least this
        /// much Attack.</summary>
        private const double ParMaterialAtkPct = 1.0;

        private static void ParTalents(ContentDatabase live)
        {
            int nDuel = 500 * _parScale, perStage = 15 * _parScale, perLive = 10 * _parScale;
            Line("T. EVERY AUTHORED TALENT, READ AT PARITY");
            Line("   today  : TALENTS WORTH PICKING recomputed - the sweep arena and the talent's dark room where it has one,");
            Line("            " + TalentValueSeeds + " seeds, the subject's own output (damage + healing + shielding)");
            Line("   duel   : the pinned line with the subject in its role's place against the SAME line with no talent, level 40,");
            Line("            n=" + nDuel + " per arm. Matched power by construction: the only difference between the sides is the talent");
            Line("   ladder : that line geared and levelled to each stage's parity power (section L: where the pinned line wins about");
            Line("            half that stage's CEIL), the stages that have one, " + perStage + " seeds a stage");
            Line("   live   : the same at x1.15, where the simulated player meets its stages, " + live.AllStages.Count + " x " + perLive + " = "
                 + (live.AllStages.Count * perLive) + " per arm (reported, not used to class)");
            Line("   classes use TALENTS WORTH PICKING's rule over the duel and ladder readings: LOSE if any reading has mean < 0 and");
            Line("   t <= -2, else GAIN if any has t >= 2, else FLAT. Classed three ways: by margin, by win rate, and by own output.");
            Line("");

            ContentDatabase arena = BuildHeroSweepArena(live);
            List<HeroDef> fillers = BuildHeroSweepFillers();
            StageDef stage = arena.GetStage("sweep_arena");

            List<HeroDef> authored = new List<HeroDef>();
            for (int i = 0; i < live.Heroes.Count; i++)
            {
                HeroDef h = arena.GetHero(live.Heroes[i].Id);
                if (h != null && h.Talents.Count > 0) authored.Add(h);
            }
            authored.Sort(delegate (HeroDef a, HeroDef b) { return string.CompareOrdinal(a.Id, b.Id); });

            List<ParTalentRow> rows = new List<ParTalentRow>();
            List<ParObs[]> mirrorArms = new List<ParObs[]>();
            List<ParObs[]> shamDc = new List<ParObs[]>(), shamDt = new List<ParObs[]>(), shamLc = new List<ParObs[]>(), shamLt = new List<ParObs[]>();
            List<string> shamClass = new List<string>();
            List<ParDelta> atkDuel = new List<ParDelta>(), atkLadder = new List<ParDelta>();
            List<double> atkSweep = new List<double>();

            for (int h = 0; h < authored.Count; h++)
            {
                HeroDef def = authored[h];
                HeroDef liveDef = live.GetHero(def.Id);
                string how;
                string[] line = NormalPlayLineFor(live, PinnedLine, liveDef, out how);
                int seed = ParSeedBase(def.Id);

                // today's reading, exactly as CheckTalentsAreWorthPicking takes it (serial: the dark
                // rooms are cached in statics).
                List<long> bare = new List<long>();
                string[] bareHash = new string[TalentValueSeeds + 1];
                for (int s = 1; s <= TalentValueSeeds; s++)
                {
                    BattleResult r = RunSweepBattle(arena, stage, fillers, def, 0, s);
                    bare.Add(OutputOf(r, def.Id));
                    bareHash[s] = r == null ? "NULL" : HeroSweepHash(r);
                }

                ParObs[] duelC = ParDuel(live, line, def.Id, null, 0, line, 40, nDuel, seed);
                mirrorArms.Add(duelC);
                ParLadder lad = ParBuildLadder(live, line, 0f);
                ParLadder ladLive = ParBuildLadder(live, line, 1.15f);
                ParObs[] ladC = ParLadderArm(live, lad, def.Id, null, 0, perStage);
                ParObs[] liveC = ParLadderArm(live, ladLive, def.Id, null, 0, perLive);

                // The sham, per hero: Attack x1.002 moves the dice and is worth about nothing. How
                // often it clears the class rule is the false-positive rate of the rule here.
                {
                    HeroDef sham = ParStat(liveDef, 1.002f, 1f, 1f, 1f);
                    ParObs[] sd = ParDuel(live, line, def.Id, sham, 0, line, 40, nDuel, seed);
                    ParObs[] sl = ParLadderArm(live, lad, def.Id, sham, 0, perStage);
                    shamDc.Add(duelC); shamDt.Add(sd); shamLc.Add(ladC); shamLt.Add(sl);
                    ParDelta a = ParCompare(duelC, sd), b = ParCompare(ladC, sl);
                    shamClass.Add(ParClass(new List<double> { a.Tm, b.Tm }, new List<double> { a.Mm, b.Mm }) + "/"
                                  + ParClass(new List<double> { a.Tw, b.Tw }, new List<double> { a.Wm, b.Wm }));
                }

                // The yardstick for how much a reading is worth: Attack x1.05 on the subject, in all three arenas.
                {
                    HeroDef atk = ParStat(liveDef, 1.05f, 1f, 1f, 1f);
                    atkDuel.Add(ParCompare(duelC, ParDuel(live, line, def.Id, atk, 0, line, 40, nDuel, seed)));
                    atkLadder.Add(ParCompare(ladC, ParLadderArm(live, lad, def.Id, atk, 0, perStage)));
                    HeroDef satk = ParStat(def, 1.05f, 1f, 1f, 1f);
                    List<long> sw = new List<long>();
                    for (int s2 = 1; s2 <= TalentValueSeeds; s2++) sw.Add(OutputOf(RunSweepBattle(arena, stage, fillers, satk, 0, s2), def.Id));
                    TalentValue sv = PairUp(bare, sw);
                    atkSweep.Add(sv.Without > 0 ? sv.Mean / sv.Without : 0);
                    Line("  " + def.Name + " yardstick, Attack x1.05: sweep " + Pct(atkSweep[atkSweep.Count - 1]) + "% t" + F(sv.T, "0.0"));
                    Line("      duel   " + ParShow(atkDuel[atkDuel.Count - 1]));
                    Line("      ladder " + ParShow(atkLadder[atkLadder.Count - 1]));
                }

                for (int t = 1; t <= def.Talents.Count; t++)
                {
                    ParTalentRow row = new ParTalentRow();
                    row.Pair = def.Id + " " + RomanOf(t);
                    row.Name = def.Name + " " + RomanOf(t);

                    List<long> with = new List<long>();
                    bool moved = false;
                    for (int s = 1; s <= TalentValueSeeds; s++)
                    {
                        BattleResult r = RunSweepBattle(arena, stage, fillers, def, t, s);
                        with.Add(OutputOf(r, def.Id));
                        if (r != null && HeroSweepHash(r) != bareHash[s]) moved = true;
                    }
                    List<string> parts = new List<string>();
                    bool loses = false, gains = false;
                    double todayPct = 0, todayT = 0;
                    if (moved)
                    {
                        TalentValue v = PairUp(bare, with);
                        parts.Add(v.Show("sweep"));
                        if (v.Loses) loses = true;
                        if (v.T >= -TalentValueLossT) gains = true;
                        if (Math.Abs(v.T) >= Math.Abs(todayT) && v.Without > 0) { todayT = v.T; todayPct = v.Mean / v.Without; }
                    }
                    TalentRoom room = DarkRoomFor(row.Pair);
                    if (room != null)
                    {
                        List<long> a = new List<long>(), b = new List<long>();
                        for (int s = 0; s < TalentValueSeeds; s++)
                        {
                            BattleResult ra = RunDarkBattle(def.Id, 0, room, room.Seed + s);
                            BattleResult rb = RunDarkBattle(def.Id, t, room, room.Seed + s);
                            a.Add(OutputOf(ra, def.Id));
                            b.Add(OutputOf(rb, def.Id));
                            if (ra != null && rb != null && HeroSweepHash(ra) != HeroSweepHash(rb)) moved = true;
                        }
                        TalentValue v = PairUp(a, b);
                        parts.Add(v.Show(room.Stage));
                        if (v.Loses) loses = true;
                        if (v.T >= -TalentValueLossT) gains = true;
                        if (Math.Abs(v.T) >= Math.Abs(todayT) && v.Without > 0) { todayT = v.T; todayPct = v.Mean / v.Without; }
                    }
                    if (parts.Count == 0) parts.Add("unreached");
                    row.Today = loses ? "lose" : gains ? "gain" : "flat";
                    row.Hero = def.Id;
                    row.TodayPct = todayPct; row.TodayT = todayT;
                    row.TodayText = string.Join("; ", parts.ToArray());
                    row.TodayMoved = moved;

                    row.Duel = ParCompare(duelC, ParDuel(live, line, def.Id, null, t, line, 40, nDuel, seed));
                    row.Ladder = ParCompare(ladC, ParLadderArm(live, lad, def.Id, null, t, perStage));
                    row.Live = ParCompare(liveC, ParLadderArm(live, ladLive, def.Id, null, t, perLive));
                    row.ByMargin = ParClass(new List<double> { row.Duel.Tm, row.Ladder.Tm }, new List<double> { row.Duel.Mm, row.Ladder.Mm });
                    row.ByWin = ParClass(new List<double> { row.Duel.Tw, row.Ladder.Tw }, new List<double> { row.Duel.Wm, row.Ladder.Wm });
                    row.ByOutput = ParClass(new List<double> { row.Duel.To, row.Ladder.To }, new List<double> { row.Duel.Om, row.Ladder.Om });
                    rows.Add(row);

                    Line("  " + row.Name + "   today " + row.Today.ToUpperInvariant() + ": " + row.TodayText);
                    Line("      duel   " + ParShow(row.Duel) + "   [base WR " + F(row.Duel.BaseWin * 100, "0") + "%]");
                    Line("      ladder " + ParShow(row.Ladder) + "   [base WR " + F(row.Ladder.BaseWin * 100, "0") + "%]");
                    Line("      live   " + ParShow(row.Live) + "   [base WR " + F(row.Live.BaseWin * 100, "0") + "%]");
                    Line("      at parity: by margin " + row.ByMargin.ToUpperInvariant() + ", by win rate " + row.ByWin.ToUpperInvariant()
                         + ", by own output " + row.ByOutput.ToUpperInvariant());
                }
            }

            // ---- summary
            Line("");
            Line("T-SUMMARY (" + rows.Count + " talents)");
            {
                ParObs[] m = Concat(mirrorArms);
                double wr = ParMean(m, o => o.Win);
                Line("  mirror: duel control arms pooled, n=" + m.Length + ", player side wins " + F(wr * 100, "0.0") + "%");
            }
            ParSplit("today (sweep + dark rooms, own output)", rows, r => r.Today);
            ParSplit("parity, by margin", rows, r => r.ByMargin);
            ParSplit("parity, by win rate", rows, r => r.ByWin);
            ParSplit("parity, by own output", rows, r => r.ByOutput);
            {
                int g = 0, f = 0, l = 0;
                for (int i = 0; i < shamClass.Count; i++)
                {
                    string c = shamClass[i].Split('/')[0];
                    if (c == "gain") g++; else if (c == "lose") l++; else f++;
                }
                Line("  sham (Attack x1.002 on each of the " + shamClass.Count + " heroes), by margin: " + g + " gain / " + f + " flat / " + l + " lose;  "
                     + "per hero margin/WR: " + string.Join(" ", shamClass.ToArray()));
                Line("    pooled sham duel   " + ParShow(ParCompare(Concat(shamDc), Concat(shamDt))));
                Line("    pooled sham ladder " + ParShow(ParCompare(Concat(shamLc), Concat(shamLt))));
            }

            // ---- worth in % Attack, and the material classes
            double cDuel = 0, cLad = 0, cSweep = 0;
            for (int i = 0; i < atkDuel.Count; i++) { cDuel += atkDuel[i].Mm; cLad += atkLadder[i].Mm; cSweep += atkSweep[i]; }
            cDuel /= Math.Max(1, atkDuel.Count); cLad /= Math.Max(1, atkLadder.Count); cSweep /= Math.Max(1, atkSweep.Count);
            Line("  yardstick, Attack x1.05 pooled over " + atkDuel.Count + " heroes: sweep own output " + Pct(cSweep) + "%, duel margin " + Sgn(cDuel)
                 + ", ladder margin " + Sgn(cLad));
            for (int i = 0; i < rows.Count; i++)
            {
                ParTalentRow r = rows[i];
                r.EqToday = cSweep != 0 ? r.TodayPct / cSweep * 5 : 0;
                r.EqDuel = cDuel != 0 ? r.Duel.Mm / cDuel * 5 : 0;
                r.EqLadder = cLad != 0 ? r.Ladder.Mm / cLad * 5 : 0;
                r.MatToday = ParMaterial(r.Today, r.TodayT, r.EqToday, 0, 0);
                r.MatParity = ParMaterial(r.ByMargin, r.Duel.Tm, r.EqDuel, r.Ladder.Tm, r.EqLadder);
            }
            ParSplit("today, material (|worth| >= " + F(ParMaterialAtkPct, "0") + "% Attack)", rows, r => r.MatToday);
            ParSplit("parity by margin, material", rows, r => r.MatParity);
            {
                int shamMat = 0;
                for (int i = 0; i < shamDc.Count; i++)
                {
                    ParDelta a = ParCompare(shamDc[i], shamDt[i]), b = ParCompare(shamLc[i], shamLt[i]);
                    string c = ParClass(new List<double> { a.Tm, b.Tm }, new List<double> { a.Mm, b.Mm });
                    if (ParMaterial(c, a.Tm, cDuel != 0 ? a.Mm / cDuel * 5 : 0, b.Tm, cLad != 0 ? b.Mm / cLad * 5 : 0) != "flat") shamMat++;
                }
                Line("  sham (Attack x1.002), material: " + shamMat + " of " + shamDc.Count + " heroes clear the material rule");
            }
            Line("");
            Line("  EVERY TALENT'S WORTH IN % ATTACK (today's strongest reading | duel | ladder), class today -> parity (margin), material");
            for (int i = 0; i < rows.Count; i++)
            {
                ParTalentRow r = rows[i];
                Line("    " + r.Name.PadRight(18) + " " + ParEq(r.EqToday).PadLeft(7) + " t" + F(r.TodayT, "0.0").PadRight(6)
                     + " | " + ParEq(r.EqDuel).PadLeft(7) + " t" + F(r.Duel.Tm, "0.0").PadRight(6)
                     + " | " + ParEq(r.EqLadder).PadLeft(7) + " t" + F(r.Ladder.Tm, "0.0").PadRight(6)
                     + "  " + r.Today + " -> " + r.ByMargin + ";  material " + r.MatToday + " -> " + r.MatParity
                     + "   dWR duel " + Pct(r.Duel.Wm) + " ladder " + Pct(r.Ladder.Wm) + " live " + Pct(r.Live.Wm));
            }
            {
                double aPar = 0, aLive = 0;
                for (int i = 0; i < rows.Count; i++) { aPar += Math.Abs(rows[i].Ladder.Wm); aLive += Math.Abs(rows[i].Live.Wm); }
                Line("  mean |dWR| per talent: ladder at parity " + F(aPar / rows.Count * 100, "0.00") + "pp, ladder at x1.15 "
                     + F(aLive / rows.Count * 100, "0.00") + "pp");
            }

            Line("");
            Line("  CLASS CHANGES, today -> parity by margin (win-rate class and the strongest parity t in brackets):");
            for (int i = 0; i < rows.Count; i++)
            {
                ParTalentRow r = rows[i];
                if (r.Today == r.ByMargin) continue;
                Line("    " + r.Name.PadRight(18) + " " + r.Today + " -> " + r.ByMargin + "   [WR " + r.ByWin + "; duel t" + F(r.Duel.Tm, "0.0")
                     + " ladder t" + F(r.Ladder.Tm, "0.0") + "; dWR duel " + Pct(r.Duel.Wm) + " ladder " + Pct(r.Ladder.Wm) + " live " + Pct(r.Live.Wm) + "]");
            }

            Line("");
            Line("  TODAY'S FLAT TALENTS AT PARITY:");
            for (int i = 0; i < rows.Count; i++)
            {
                ParTalentRow r = rows[i];
                if (r.Today != "flat") continue;
                bool dark = r.Duel.Changed == 0 && r.Ladder.Changed == 0 && r.Live.Changed == 0 && !r.TodayMoved;
                bool cosmetic = !dark && ParNoHp(r.Duel) && ParNoHp(r.Ladder) && ParNoHp(r.Live) && r.TodayPct == 0;
                double best = Math.Max(Math.Abs(r.Duel.Tm), Math.Abs(r.Ladder.Tm));
                string verdict = r.MatParity != "flat"
                    ? "ALIVE, " + r.MatParity + (best < 3 ? " (weak: no margin reading reaches |t| 3)" : "")
                    : r.ByMargin != "flat" || r.ByWin != "flat"
                    ? "SIGNIFICANT BUT IMMATERIAL (" + (r.ByMargin != "flat" ? r.ByMargin : r.ByWin + " by win rate only") + ", under "
                      + F(ParMaterialAtkPct, "0") + "% Attack)"
                    : dark ? "DEAD, INERT: changes no fight in any arena"
                    : cosmetic ? "DEAD, COSMETIC: changes the event log, not one fight's margin, result or output"
                    : "DEAD, NOISE: changes fights, worth nothing measurable";
                Line("    " + r.Name.PadRight(18) + " " + verdict + "   [worth " + ParEq(r.EqDuel) + " / " + ParEq(r.EqLadder) + " Attack; chg duel " + r.Duel.Changed + "/" + r.Duel.N + " ladder " + r.Ladder.Changed + "/" + r.Ladder.N
                     + " live " + r.Live.Changed + "/" + r.Live.N + "; t margin " + F(r.Duel.Tm, "0.0") + " / " + F(r.Ladder.Tm, "0.0")
                     + "; t WR " + F(r.Duel.Tw, "0.0") + " / " + F(r.Ladder.Tw, "0.0") + "]");
            }
            Line("");
        }

        /// <summary>A significance class kept only where a reading carrying it is also worth
        /// ParMaterialAtkPct of Attack or more in the same direction; otherwise flat.</summary>
        private static string ParMaterial(string cls, double t1, double eq1, double t2, double eq2)
        {
            if (cls == "flat") return "flat";
            int sign = cls == "gain" ? 1 : -1;
            bool m1 = Math.Abs(t1) >= -TalentValueLossT && Math.Abs(eq1) >= ParMaterialAtkPct && Math.Sign(eq1) == sign;
            bool m2 = Math.Abs(t2) >= -TalentValueLossT && Math.Abs(eq2) >= ParMaterialAtkPct && Math.Sign(eq2) == sign;
            return m1 || m2 ? cls : "flat";
        }

        /// <summary>Every fight's margin, result and subject output identical in both arms.</summary>
        private static bool ParNoHp(ParDelta d)
        {
            return d.Mm == 0 && d.Ms == 0 && d.Wm == 0 && d.Ws == 0 && d.Om == 0 && d.Os == 0;
        }

        private static string ParEq(double v) { return (v >= 0 ? "+" : "") + F(v, "0.0") + "%"; }

        private static void ParSplit(string what, List<ParTalentRow> rows, Func<ParTalentRow, string> cls)
        {
            int g = 0, f = 0, l = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                string c = cls(rows[i]);
                if (c == "gain") g++; else if (c == "lose") l++; else f++;
            }
            Line("  " + what.PadRight(40) + " " + g + " gain / " + f + " flat / " + l + " lose");
        }

        // ================================================================== the star band

        /// <summary>
        /// A line's strength at parity: the far side's level at which the line at level 40 wins
        /// half its duels, found by a coarse walk and a logistic fit over the levels around the
        /// crossing. Returned with its standard error (delta method on the fit).
        /// </summary>
        private static double ParL50(ContentDatabase db, string[] line, string[] reference, int seedBase, out double se, out string note)
        {
            return ParL50(db, line, null, null, 0, reference, seedBase, out se, out note);
        }

        private static double ParL50(ContentDatabase db, string[] line, string subjectId, HeroDef subjectDef, int talent, string[] reference,
                                     int seedBase, out double se, out string note)
        {
            int nCoarse = 50 * _parScale, nFine = 150 * _parScale;
            note = "";
            Dictionary<int, double> coarse = new Dictionary<int, double>();
            Func<int, int, int, double> wr = (lvl, n, salt) =>
            {
                ParObs[] o = ParDuel(db, line, subjectId, subjectDef, talent, reference, lvl, n, seedBase + salt * 1000003 + lvl * 7919);
                return ParMean(o, x => x.Win);
            };
            int L = 40;
            double w = wr(L, nCoarse, 1);
            coarse[L] = w;
            int dir = w > 0.5 ? 1 : -1;
            int lo = L, hi = L;
            for (int step = 0; step < 30; step++)
            {
                int next = L + dir * 3;
                if (next < 1 || next > 130) { note = dir > 0 ? "above level 130" : "below level 1"; break; }
                double wn = wr(next, nCoarse, 1);
                coarse[next] = wn;
                if ((wn - 0.5) * (w - 0.5) <= 0) { lo = Math.Min(L, next); hi = Math.Max(L, next); break; }
                L = next; w = wn;
                lo = hi = L;
            }
            List<double> xs = new List<double>(), ks = new List<double>(), ns = new List<double>();
            for (int lvl = Math.Max(1, lo - 2); lvl <= hi + 2; lvl++)
            {
                double f = wr(lvl, nFine, 2);
                xs.Add(lvl); ks.Add(f * nFine); ns.Add(nFine);
            }
            // Logistic fit, Newton-Raphson on (a, b) in logit p = a + b (x - x0).
            double x0 = 0; for (int i = 0; i < xs.Count; i++) x0 += xs[i]; x0 /= xs.Count;
            double a = 0, b = -0.3; // win rate falls as the far side levels up
            double iaa = 0, iab = 0, ibb = 0;
            for (int it = 0; it < 60; it++)
            {
                double ga = 0, gb = 0; iaa = 0; iab = 0; ibb = 0;
                for (int i = 0; i < xs.Count; i++)
                {
                    double x = xs[i] - x0;
                    double p = 1.0 / (1.0 + Math.Exp(-(a + b * x)));
                    double r = ks[i] - ns[i] * p;
                    double v = ns[i] * p * (1 - p);
                    ga += r; gb += r * x;
                    iaa += v; iab += v * x; ibb += v * x * x;
                }
                double det = iaa * ibb - iab * iab;
                if (det <= 1e-12) break;
                double da = (ibb * ga - iab * gb) / det, dB = (-iab * ga + iaa * gb) / det;
                a += da; b += dB;
                if (Math.Abs(da) < 1e-9 && Math.Abs(dB) < 1e-9) break;
            }
            double detI = iaa * ibb - iab * iab;
            if (b >= 0 || detI <= 0) { se = 99; note += (note.Length > 0 ? "; " : "") + "fit failed"; return x0; }
            double vaa = ibb / detI, vbb = iaa / detI, vab = -iab / detI;
            double l50 = x0 - a / b;
            double g1 = -1 / b, g2 = a / (b * b);
            se = Math.Sqrt(Math.Max(0, g1 * g1 * vaa + 2 * g1 * g2 * vab + g2 * g2 * vbb));
            return l50;
        }

        private static double ParLinePower(ContentDatabase db, string[] ids, double level)
        {
            int l0 = Math.Max(1, (int)Math.Floor(level));
            double frac = level - l0;
            double p0 = 0, p1 = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                HeroDef d = db.GetHero(ids[i]);
                p0 += StatMath.PowerScore(StatMath.ForOpponent(d, l0, d.Stars, null, null, db.Armor));
                p1 += StatMath.PowerScore(StatMath.ForOpponent(d, l0 + 1, d.Stars, null, null, db.Armor));
            }
            return p0 + (p1 - p0) * frac;
        }

        private sealed class ParStar
        {
            public HeroDef Def;
            public long Sweep;
            public int Slot;
            public double[] L50 = new double[2], Se = new double[2], Worth = new double[2], Ratio = new double[2];
            public string[] Note = new string[2];
            public double SweepRatio;
            /// <summary>Worth of the hero's own stat line with a token kit (pinned reference): what
            /// a body with these numbers is worth before any rule it carries.</summary>
            public double StatWorth, StatL50, StatSe, KitRatio;
        }

        /// <summary>The hero's stats with the sweep filler's token kit: one 30% strike at the front,
        /// an active it can never afford, no passives, no talents.</summary>
        private static HeroDef ParStatline(HeroDef d, HeroDef filler)
        {
            HeroDef c = TempoClone(d);
            c.BasicAttack = filler.BasicAttack;
            c.Active = filler.Active;
            _passivesField.SetValue(c, new List<PassiveDef>());
            _talentsField.SetValue(c, new List<PassiveDef>());
            return c;
        }

        private static void ParStars(ContentDatabase live)
        {
            string[][] refs = { PinnedLine, NormalPlayBalanceLine };
            string[] refNames = { "pinned", "balance" };
            Line("S. THE STAR BAND AT PARITY");
            Line("   sweep  : the star band's own reading - the hero's own output in the sweep arena, " + HeroSweepSeeds + " seeds, level 40,");
            Line("            as a share of its role's authored 5-star median");
            Line("   parity : the pinned line with the hero in its role's place (NormalPlayLineFor), level 40, duelling a fixed reference");
            Line("            line (pinned, and separately the balance line) whose LEVEL is walked until the hero's line wins half. That");
            Line("            level, as the reference line's PowerScore, is the line's strength at parity. The hero's WORTH is that strength");
            Line("            minus the strength of the same line with the hero's slot left empty; its ratio is worth over its role's 5-star");
            Line("            median worth, the same shape as the band's. Coarse walk " + (50 * _parScale) + " fights a level, then five-plus levels around");
            Line("            the crossing at " + (150 * _parScale) + " each, logistic fit; +/- is one standard error of the fitted level.");
            Line("");

            ContentDatabase arena = BuildHeroSweepArena(live);
            List<HeroDef> fillers = BuildHeroSweepFillers();
            StageDef stage = arena.GetStage("sweep_arena");

            List<ParStar> stars = new List<ParStar>();
            for (int i = 0; i < live.Heroes.Count; i++)
            {
                HeroDef h = live.Heroes[i];
                if (h.BasicAttack != null && h.BasicAttack.Id.Contains("placeholder")) continue;
                HeroDef ad = arena.GetHero(h.Id);
                if (ad == null) continue;
                ParStar s = new ParStar();
                s.Def = h;
                long[] terms = new long[3];
                for (int seed = 1; seed <= HeroSweepSeeds; seed++)
                {
                    List<HeroInstance> team = new List<HeroInstance>();
                    team.Add(MakeInstance(ad, 40));
                    for (int f = 0; f < fillers.Count; f++) team.Add(MakeInstance(fillers[f], 40));
                    BattleResult r = RunWith(arena, stage, HeroSweepSeed(ad.Id, seed), team);
                    if (r != null) SubjectOutput(r, ad.Id, terms);
                }
                s.Sweep = terms[0] + terms[1] + terms[2];
                string how;
                string[] line = NormalPlayLineFor(live, PinnedLine, h, out how);
                s.Slot = -1;
                for (int k = 0; k < line.Length; k++) if (line[k] == h.Id) s.Slot = k;
                stars.Add(s);
            }

            // Empty-slot baselines, one per slot a hero can take, per reference.
            double[,] emptyPow = new double[PinnedLine.Length, 2];
            double[,] emptyL = new double[PinnedLine.Length, 2], emptySe = new double[PinnedLine.Length, 2];
            for (int slot = 0; slot < PinnedLine.Length; slot++)
            {
                bool used = false;
                for (int i = 0; i < stars.Count; i++) if (stars[i].Slot == slot) used = true;
                if (!used) continue;
                List<string> four = new List<string>(PinnedLine);
                four.RemoveAt(slot);
                for (int r = 0; r < refs.Length; r++)
                {
                    double se; string note;
                    double l = ParL50(live, four.ToArray(), refs[r], 31000000 + slot * 100000 + r * 50000, out se, out note);
                    emptyL[slot, r] = l; emptySe[slot, r] = se;
                    emptyPow[slot, r] = ParLinePower(live, refs[r], l);
                    Line("   empty slot " + slot + " (" + PinnedLine[slot] + " removed) vs " + refNames[r] + ": parity level "
                         + F(l, "0.00") + " +/-" + F(se, "0.00") + " = power " + F(emptyPow[slot, r], "0") + (note.Length > 0 ? "  [" + note + "]" : ""));
                }
            }
            for (int r = 0; r < refs.Length; r++)
                Line("   reference " + refNames[r] + " line power at level 40: " + F(ParLinePower(live, refs[r], 40), "0")
                     + "; one level near 40 is " + F(ParLinePower(live, refs[r], 41) - ParLinePower(live, refs[r], 40), "0") + " power");
            Line("");

            // The instrument's own controls, on Kael in his own line against the pinned reference:
            // as authored that is the mirror and must fit at level 40; a placebo talent must fit to
            // the same level exactly; stat lifts of known size must read above it.
            {
                const string kael = "kael_the_unyielding";
                HeroDef k = live.GetHero(kael);
                int seedK = ParSeedBase(kael);
                string[] names = { "as authored (the mirror)", "placebo talent", "Attack x1.05", "Max HP x1.10", "Speed x1.10" };
                HeroDef[] defs = { null, ParPlacebo(k), ParStat(k, 1.05f, 1f, 1f, 1f), ParStat(k, 1f, 1f, 1.10f, 1f), ParStat(k, 1f, 1f, 1f, 1.10f) };
                Line("   SC controls, Kael in the pinned line against the pinned reference (worth = power over the empty slot):");
                for (int v = 0; v < defs.Length; v++)
                {
                    double se; string note;
                    int sel = v == 1 ? defs[v].Talents.Count : 0;
                    double l = ParL50(live, PinnedLine, kael, defs[v], sel, PinnedLine, seedK, out se, out note);
                    Line("     " + names[v].PadRight(26) + " parity level " + F(l, "0.00") + " +/-" + F(se, "0.00") + "  worth "
                         + F(ParLinePower(live, PinnedLine, l) - emptyPow[0, 0], "0") + (note.Length > 0 ? "  [" + note + "]" : ""));
                }
                Line("");
            }

            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < stars.Count; i++)
            {
                ParStar s = stars[i];
                string how;
                string[] line = NormalPlayLineFor(live, PinnedLine, s.Def, out how);
                for (int r = 0; r < refs.Length; r++)
                {
                    double se; string note;
                    s.L50[r] = ParL50(live, line, refs[r], ParSeedBase(s.Def.Id) + r * 50000, out se, out note);
                    s.Se[r] = se; s.Note[r] = note;
                    s.Worth[r] = ParLinePower(live, refs[r], s.L50[r]) - emptyPow[s.Slot, r];
                }
            }
            Line("   (" + stars.Count + " heroes x 2 references read in " + F(sw.ElapsedMilliseconds / 1000.0, "0.0") + "s)");

            // The body effect: the same slot filled by the hero's own stat line with a token kit,
            // against the pinned reference. Worth over the empty slot is body plus kit; this is
            // the body alone, so worth minus this is what the kit adds.
            HeroDef token = fillers[0];
            for (int i = 0; i < stars.Count; i++)
            {
                ParStar s = stars[i];
                string how;
                string[] line = NormalPlayLineFor(live, PinnedLine, s.Def, out how);
                double se; string note;
                s.StatL50 = ParL50(live, line, s.Def.Id, ParStatline(s.Def, token), 0, PinnedLine, ParSeedBase(s.Def.Id) + 70000, out se, out note);
                s.StatSe = se;
                s.StatWorth = ParLinePower(live, PinnedLine, s.StatL50) - emptyPow[s.Slot, 0];
            }
            Line("   KIT OVER STAT LINE (pinned reference): worth = body + kit, where body is the hero's own stats with a token kit.");
            Line("   kit ratio = (worth - body) over the role's 5-star median of the same. Printed per role with the body's share of worth.");
            {
                Array rs = Enum.GetValues(typeof(HeroRole));
                List<KeyValuePair<double, string>> kitMoves = new List<KeyValuePair<double, string>>();
                for (int ri = 0; ri < rs.Length; ri++)
                {
                    HeroRole role = (HeroRole)rs.GetValue(ri);
                    List<ParStar> inRole = new List<ParStar>();
                    for (int i = 0; i < stars.Count; i++) if (stars[i].Def.Role == role) inRole.Add(stars[i]);
                    if (inRole.Count == 0) continue;
                    List<double> fk = new List<double>(), fsw = new List<double>();
                    for (int i = 0; i < inRole.Count; i++)
                        if (inRole[i].Def.Stars >= 5) { fk.Add(inRole[i].Worth[0] - inRole[i].StatWorth); fsw.Add(inRole[i].Sweep); }
                    double mk = ParMedian(fk), msw = ParMedian(fsw);
                    inRole.Sort(delegate (ParStar a, ParStar b)
                    {
                        if (a.Def.Stars != b.Def.Stars) return b.Def.Stars - a.Def.Stars;
                        return string.CompareOrdinal(a.Def.Id, b.Def.Id);
                    });
                    string row = "  " + role + " (5-star median kit worth " + F(mk, "0") + "):";
                    Line(row);
                    for (int i = 0; i < inRole.Count; i++)
                    {
                        ParStar s = inRole[i];
                        double kit = s.Worth[0] - s.StatWorth;
                        s.KitRatio = mk > 0 ? kit / mk : 0;
                        double swr = msw > 0 ? s.Sweep / msw : 0;
                        // Set here as well as below: the rank agreement and the verdict count
                        // that close this block read it before the ratios section runs.
                        s.SweepRatio = swr;
                        Line("    " + (s.Def.Stars + "*").PadRight(3) + s.Def.Id.PadRight(32) + " worth " + F(s.Worth[0], "0").PadLeft(5)
                             + "  body " + F(s.StatWorth, "0").PadLeft(5) + " (level " + F(s.StatL50, "0.0") + "+/-" + F(s.StatSe, "0.0") + ", "
                             + F(s.Worth[0] != 0 ? s.StatWorth / s.Worth[0] * 100 : 0, "0") + "% of worth)  kit " + F(kit, "0").PadLeft(5)
                             + "  kit ratio x" + F(s.KitRatio, "0.00") + "  sweep x" + F(swr, "0.00")
                             + "  band on kit ratio: " + ParBandVerdict(s.Def.Stars, s.KitRatio, fk.Count) + ", sweep " + ParBandVerdict(s.Def.Stars, swr, fk.Count));
                        kitMoves.Add(new KeyValuePair<double, string>(s.KitRatio - swr, s.Def.Id + " (" + s.Def.Stars + "*, " + role + ") sweep x"
                                     + F(swr, "0.00") + " -> kit x" + F(s.KitRatio, "0.00")));
                    }
                }
                double rho = ParSpearman(stars, x => x.SweepRatio, x => x.KitRatio);
                Line("  whole-roster rank agreement, sweep ratio vs kit ratio: Spearman " + F(rho, "0.00"));
                kitMoves.Sort((a, b) => Math.Abs(b.Key).CompareTo(Math.Abs(a.Key)));
                Line("  the twenty largest moves, kit ratio minus sweep ratio:");
                for (int i = 0; i < kitMoves.Count && i < 20; i++) Line("    " + (kitMoves[i].Key >= 0 ? "+" : "") + F(kitMoves[i].Key, "0.00") + "  " + kitMoves[i].Value);
                int kitChanges = 0, kitGraded = 0;
                List<string> changed = new List<string>();
                for (int i = 0; i < stars.Count; i++)
                {
                    int fives = 0;
                    for (int j = 0; j < stars.Count; j++) if (stars[j].Def.Role == stars[i].Def.Role && stars[j].Def.Stars >= 5) fives++;
                    if (fives < StarBandMinFives) continue;
                    kitGraded++;
                    string a = ParBandVerdict(stars[i].Def.Stars, stars[i].SweepRatio, fives), b = ParBandVerdict(stars[i].Def.Stars, stars[i].KitRatio, fives);
                    if (a != b) { kitChanges++; changed.Add(stars[i].Def.Id + " " + a + "->" + b); }
                }
                Line("  band verdicts that change on the kit ratio: " + kitChanges + " of " + kitGraded + ": " + string.Join(", ", changed.ToArray()));
            }
            Line("");

            // Ratios against each role's 5-star median, sweep and parity alike.
            Array roles = Enum.GetValues(typeof(HeroRole));
            List<string> moves = new List<string>();
            List<KeyValuePair<double, string>> ranked = new List<KeyValuePair<double, string>>();
            int verdictChanges = 0, graded = 0;
            List<string> orderSweep = new List<string>(), orderPar = new List<string>();
            for (int ri = 0; ri < roles.Length; ri++)
            {
                HeroRole role = (HeroRole)roles.GetValue(ri);
                List<ParStar> inRole = new List<ParStar>();
                for (int i = 0; i < stars.Count; i++) if (stars[i].Def.Role == role) inRole.Add(stars[i]);
                if (inRole.Count == 0) continue;
                List<double> fs = new List<double>(), fp0 = new List<double>(), fp1 = new List<double>();
                for (int i = 0; i < inRole.Count; i++)
                    if (inRole[i].Def.Stars >= 5) { fs.Add(inRole[i].Sweep); fp0.Add(inRole[i].Worth[0]); fp1.Add(inRole[i].Worth[1]); }
                double ms = ParMedian(fs), mp0 = ParMedian(fp0), mp1 = ParMedian(fp1);
                bool enforced = fs.Count >= StarBandMinFives;
                Line("  " + role + "  (" + fs.Count + " five-star; band " + (enforced ? "enforced" : "NOT enforced") + ")  5-star median: sweep " + F(ms, "0")
                     + ", parity worth " + F(mp0, "0") + " (pinned ref) / " + F(mp1, "0") + " (balance ref)");
                inRole.Sort(delegate (ParStar a, ParStar b)
                {
                    if (a.Def.Stars != b.Def.Stars) return b.Def.Stars - a.Def.Stars;
                    return string.CompareOrdinal(a.Def.Id, b.Def.Id);
                });
                for (int i = 0; i < inRole.Count; i++)
                {
                    ParStar s = inRole[i];
                    s.SweepRatio = ms > 0 ? s.Sweep / ms : 0;
                    s.Ratio[0] = mp0 > 0 ? s.Worth[0] / mp0 : 0;
                    s.Ratio[1] = mp1 > 0 ? s.Worth[1] / mp1 : 0;
                    string vs = ParBandVerdict(s.Def.Stars, s.SweepRatio, fs.Count), vp = ParBandVerdict(s.Def.Stars, s.Ratio[0], fs.Count);
                    string vb = ParBandVerdict(s.Def.Stars, s.Ratio[1], fs.Count);
                    bool changes = enforced && (vs != vp || vs != vb) && vp == vb;
                    if (enforced) graded++;
                    if (changes) verdictChanges++;
                    Line("    " + (s.Def.Stars + "*").PadRight(3) + s.Def.Id.PadRight(32) + " sweep x" + F(s.SweepRatio, "0.00")
                         + "  parity x" + F(s.Ratio[0], "0.00") + " / x" + F(s.Ratio[1], "0.00")
                         + "  (level " + F(s.L50[0], "0.0") + "+/-" + F(s.Se[0], "0.0") + " / " + F(s.L50[1], "0.0") + "+/-" + F(s.Se[1], "0.0") + ")"
                         + "  band: sweep " + vs + ", parity " + vp + " / " + vb
                         + (changes ? "   <- VERDICT CHANGES" : "")
                         + (s.Note[0].Length + s.Note[1].Length > 0 ? "  [" + s.Note[0] + " " + s.Note[1] + "]" : ""));
                    double mv = (s.Ratio[0] + s.Ratio[1]) / 2 - s.SweepRatio;
                    ranked.Add(new KeyValuePair<double, string>(mv, s.Def.Id + " (" + s.Def.Stars + "*, " + role + ") sweep x" + F(s.SweepRatio, "0.00")
                        + " -> parity x" + F(s.Ratio[0], "0.00") + " / x" + F(s.Ratio[1], "0.00")));
                }
                // Tier order, the band's scale-free half: does each tier sit under its role's tier above?
                for (int tier = 1; tier <= 4; tier++)
                {
                    List<double> above = new List<double>(), aboveP = new List<double>();
                    for (int i = 0; i < inRole.Count; i++)
                        if (inRole[i].Def.Stars == tier + 1) { above.Add(inRole[i].Sweep); aboveP.Add((inRole[i].Worth[0] + inRole[i].Worth[1]) / 2); }
                    if (above.Count == 0) continue;
                    double ma = ParMedian(above), mpa = ParMedian(aboveP);
                    for (int i = 0; i < inRole.Count; i++)
                    {
                        if (inRole[i].Def.Stars != tier) continue;
                        if (inRole[i].Sweep >= ma) orderSweep.Add(inRole[i].Def.Id + " (" + tier + "*, " + role + ")");
                        if ((inRole[i].Worth[0] + inRole[i].Worth[1]) / 2 >= mpa) orderPar.Add(inRole[i].Def.Id + " (" + tier + "*, " + role + ")");
                    }
                }
                double rho0 = ParSpearman(inRole, s => s.SweepRatio, s => s.Ratio[0]), rho1 = ParSpearman(inRole, s => s.SweepRatio, s => s.Ratio[1]);
                double rhoRef = ParSpearman(inRole, s => s.Ratio[0], s => s.Ratio[1]);
                Line("    rank agreement (Spearman): sweep vs parity " + F(rho0, "0.00") + " / " + F(rho1, "0.00") + "; parity pinned vs balance ref " + F(rhoRef, "0.00"));
            }
            ranked.Sort((a, b) => Math.Abs(b.Key).CompareTo(Math.Abs(a.Key)));
            Line("");
            Line("  S-SUMMARY");
            Line("  band verdicts that change (both parity references agree with each other and not with the sweep): " + verdictChanges + " of " + graded + " graded heroes");
            Line("  a tier at or above its role's tier-above median: sweep " + orderSweep.Count + " [" + string.Join(", ", orderSweep.ToArray()) + "]");
            Line("                                                  parity " + orderPar.Count + " [" + string.Join(", ", orderPar.ToArray()) + "]");
            double sAll0 = ParSpearmanAll(stars, s => s.SweepRatio, s => (s.Ratio[0] + s.Ratio[1]) / 2);
            Line("  whole-roster rank agreement, sweep ratio vs mean parity ratio: Spearman " + F(sAll0, "0.00"));
            Line("  the twenty-five largest moves (mean of the two parity ratios minus the sweep ratio):");
            for (int i = 0; i < ranked.Count && i < 25; i++) Line("    " + (ranked[i].Key >= 0 ? "+" : "") + F(ranked[i].Key, "0.00") + "  " + ranked[i].Value);
            Line("");
        }

        private static string ParBandVerdict(int stars, double share, int fives)
        {
            if (fives < StarBandMinFives) return "ungraded";
            float lo, hi;
            if (stars >= 5) { if (fives < 2) return "only"; lo = FiveStarFloor; hi = FiveStarCeiling; }
            else if (stars == 4) { lo = StarBandFloor; hi = StarBandCeiling; }
            else if (stars == 3) { lo = StarBandThreeFloor; hi = StarBandThreeCeiling; }
            else if (stars == 2) { lo = StarBandTwoFloor; hi = StarBandTwoCeiling; }
            else { lo = StarBandOneFloor; hi = StarBandOneCeiling; }
            return share < lo ? "BELOW" : share > hi ? "ABOVE" : "in";
        }

        private static double ParMedian(List<double> v)
        {
            if (v.Count == 0) return -1;
            List<double> s = new List<double>(v);
            s.Sort();
            return s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2;
        }

        private static double[] ParRanks(List<double> v)
        {
            int n = v.Count;
            int[] idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = i;
            Array.Sort(idx, (a, b) => v[a].CompareTo(v[b]));
            double[] r = new double[n];
            for (int i = 0; i < n; )
            {
                int j = i;
                while (j + 1 < n && v[idx[j + 1]] == v[idx[i]]) j++;
                for (int k = i; k <= j; k++) r[idx[k]] = (i + j) / 2.0;
                i = j + 1;
            }
            return r;
        }

        private static double ParSpearmanCore(List<double> a, List<double> b)
        {
            if (a.Count < 3) return double.NaN;
            double[] ra = ParRanks(a), rb = ParRanks(b);
            double ma = 0, mb = 0;
            for (int i = 0; i < ra.Length; i++) { ma += ra[i]; mb += rb[i]; }
            ma /= ra.Length; mb /= rb.Length;
            double sab = 0, saa = 0, sbb = 0;
            for (int i = 0; i < ra.Length; i++) { sab += (ra[i] - ma) * (rb[i] - mb); saa += (ra[i] - ma) * (ra[i] - ma); sbb += (rb[i] - mb) * (rb[i] - mb); }
            return saa > 0 && sbb > 0 ? sab / Math.Sqrt(saa * sbb) : double.NaN;
        }

        private static double ParSpearman(List<ParStar> s, Func<ParStar, double> fa, Func<ParStar, double> fb)
        {
            List<double> a = new List<double>(), b = new List<double>();
            for (int i = 0; i < s.Count; i++) { a.Add(fa(s[i])); b.Add(fb(s[i])); }
            return ParSpearmanCore(a, b);
        }

        private static double ParSpearmanAll(List<ParStar> s, Func<ParStar, double> fa, Func<ParStar, double> fb)
        {
            return ParSpearman(s, fa, fb);
        }
    }
}
