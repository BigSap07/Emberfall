using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// FUSION ACCELERATES A WALL. IT NEVER GATES ONE. The instruments that hold the campaign to
    /// that rule, and the walk policy they need.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>What the economy walk's roster work may do. Both true is the player every
        /// table reads; the rule's reference player converts nothing and ascends nothing, so its
        /// power is pulls, levels to the caps and armor, and a wall that player cannot pass is a
        /// wall fusion gates.</summary>
        private static bool _walkConverts = true;
        private static bool _walkAscends = true;

        private static EconomyRun SimulateEconomyUnder(ContentDatabase db, int firstSeed, bool converts, bool ascends)
        {
            bool c = _walkConverts, a = _walkAscends;
            _walkConverts = converts;
            _walkAscends = ascends;
            try { return SimulateEconomy(db, false, firstSeed); }
            finally { _walkConverts = c; _walkAscends = a; }
        }

        private static string _wallModes = null;
        private static string _relevelSpec = null;
        /// <summary>Per-stage CEIL target set by --relevel (Docs/ECONOMY-PROPOSAL-R4.md, lever R2), consulted by
        /// TargetCeilRate and the CEIL band readers. Empty in every other run.</summary>
        private static Dictionary<string, float> _ceilTargetOverride = new Dictionary<string, float>();

        /// <summary>
        /// Lever R2 of Docs/ECONOMY-PROPOSAL-R4.md: the level search alone, at the recommendedPower the content already carries, with
        /// a per-kind CEIL target, written back into the content directory's campaign.json.
        /// Spec: key=value pairs, comma separated: ord (ordinary target), wall (stage 6 and 12 target),
        /// ch1wall (1-12 target), skipto (first global 1-based stage index to level, default 7),
        /// upto (last global 1-based stage index to level, default 72), wallsonly (1 to level only the
        /// walls: 1-12 and stages 6 and 12 from chapter 2), only (stage codes such as 2-5, separated by
        /// semicolons, to level alone).
        /// </summary>
        private static int RunRelevel(string contentDir, string spec)
        {
            Console.OutputEncoding = Encoding.UTF8;
            ContentDatabase db = LoadContent(contentDir);
            if (db == null) return 1;
            float ord = 0.85f, wall = 0.65f, ch1wall = 0.65f; int skipTo = 7, upTo = 72; string only = null; bool wallsOnly = false;
            foreach (string kv in spec.Split(','))
            {
                string[] p = kv.Split('=');
                if (p.Length != 2) continue;
                if (p[0] == "ord") ord = float.Parse(p[1], CultureInfo.InvariantCulture);
                else if (p[0] == "wall") wall = float.Parse(p[1], CultureInfo.InvariantCulture);
                else if (p[0] == "ch1wall") ch1wall = float.Parse(p[1], CultureInfo.InvariantCulture);
                else if (p[0] == "skipto") skipTo = int.Parse(p[1]);
                else if (p[0] == "upto") upTo = int.Parse(p[1]);
                else if (p[0] == "wallsonly") wallsOnly = p[1] == "1";
                else if (p[0] == "only") only = p[1];
            }
            Line("RELEVEL (scratch): ordinary " + Pct(ord) + ", walls " + Pct(wall) + ", 1-12 " + Pct(ch1wall) + ", from stage " + skipTo);
            Line("  stage   recPwr   was levels            landed levels         CEIL@rec  probes  moved");
            int totalProbes = 0, graded = 0, inBand = 0;
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < db.AllStages.Count; i++)
            {
                StageDef s = db.AllStages[i];
                if (i + 1 < skipTo || i + 1 > upTo) continue;
                if (only != null && Array.IndexOf(only.Split(';'), s.DisplayCode) < 0) continue;
                bool isWall = s.Index == 6 || s.Index == 12;
                if (wallsOnly && !(isWall && !(s.ChapterNumber == 1 && s.Index == 6))) continue;
                float target = s.ChapterNumber == 1 ? (s.Index == 12 ? ch1wall : ord) : (isWall ? wall : ord);
                _ceilTargetOverride[s.Id] = target;
                if (IsFloorPinned(db, s)) { Line("  " + s.DisplayCode + "  PINNED"); continue; }
                int[] roster = new int[s.Enemies.Count];
                for (int e = 0; e < roster.Length; e++) roster[e] = s.Enemies[e].Level;
                string was = SolverRosterText(s) + " (" + s.FloorLevel + "/" + s.CeilingLevel + ")";
                int ramp = s.CeilingLevel - s.FloorLevel;
                int probes = 0;
                LevelConfig landed = SolveStageLevelsAt(db, s, roster, ramp, SolverMaxProbesPerStage, ref probes);
                totalProbes += probes;
                graded++;
                bool ok = CeilBandMargin(db, s, landed.Rate) >= 0f;
                if (ok) inBand++;
                Line("  " + s.DisplayCode.PadRight(6) + s.RecommendedPower.ToString().PadLeft(8) + "   " + was.PadRight(22)
                     + (SolverRosterText(s) + " (" + s.FloorLevel + "/" + s.CeilingLevel + ")").PadRight(22)
                     + Pct(landed.Rate).PadLeft(8) + probes.ToString().PadLeft(8) + landed.UnitsMoved.ToString().PadLeft(7)
                     + (ok ? "" : "   OUT") + (landed.Truncated ? "  BUDGET" : ""));
            }
            Line("  " + graded + " stages levelled, " + inBand + " inside the +-7 band, " + totalProbes + " probes, "
                 + (sw.ElapsedMilliseconds / 1000) + " s");
            string error;
            if (!WriteCampaignJson(db, Path.Combine(contentDir, "campaign.json"), out error))
            { Line("  ABORT: " + error); return 1; }
            Line("  wrote " + Path.Combine(contentDir, "campaign.json"));
            return 0;
        }

        private static int WalkSeed(int k) { return 1 + k * 1000003; }

        /// <summary>Samples behind each wall's at-cap reading inside a walk, 0 for none. Set by
        /// the callers that grade the rule; the ordinary walk leaves it at 0 and pays nothing.</summary>
        private static int _wallProbeSamples = 0;

        /// <summary>The solver's own wall probe: enough to tell a wall the capped line loses from
        /// one it wins, printed each iteration and never written anywhere.</summary>
        private const int SolverWallProbeSamples = 200;

        /// <summary>
        /// What the arriving line is worth, and at a wall what it can clear, with every deployed
        /// hero levelled to its own cap and nothing else changed - no conversion, no ascension, no
        /// swap. That is gold's reach from the line the player brought. The Levels are put back
        /// before the walk goes on, and the battles draw fixed seeds of their own, so the walk is
        /// byte-identical with the probe on or off.
        /// </summary>
        private static void ProbeWallAtCap(ContentDatabase db, GameSession session, EconomyStageOutcome o, int stageIdx)
        {
            PlayerProfile p = session.Profile;
            List<HeroInstance> team = p.TeamHeroes();
            int[] was = new int[team.Count];
            for (int i = 0; i < team.Count; i++)
            {
                was[i] = team[i].Level;
                int cap = session.HeroLevelCapOf(team[i]);
                if (team[i].Level < cap) team[i].Level = cap;
            }
            try
            {
                o.EntryCappedPower = p.EffectiveTeamPower(db);
                if (_wallProbeSamples > 0 && BandKindOf(o.Stage, stageIdx).StartsWith("wall"))
                {
                    StageDef ceil = CampaignWaveGenerator.BuildBattle(
                        o.Stage, Math.Max(1, o.Stage.WaveCount), Math.Max(1, o.Stage.BattleCount));
                    int wins = 0;
                    int seed = 41000000 + o.Stage.GlobalIndex * 211;
                    for (int s = 0; s < _wallProbeSamples; s++)
                        if (Emberfall.Core.Combat.CombatSimulation.Run(session.BuildBattleRequest(ceil, seed + s * 31)).Victory) wins++;
                    o.EntryCappedCeilRate = wins / (float)_wallProbeSamples;
                }
            }
            finally
            {
                for (int i = 0; i < team.Count; i++) team[i].Level = was[i];
            }
        }

        /// <summary>Walks behind the rule's guard: target B's own count, so the rule and the target
        /// are read over the same number of players.</summary>
        private const int FusionNeverGatesWalks = PaceSeeds;

        /// <summary>
        /// FUSION ACCELERATES A WALL. IT NEVER GATES ONE. The rule as a guard: twenty walks by the
        /// reference player - pulls, gold into levels up to the caps, armor, and no conversion and
        /// no ascension at all - and every one of them finishes the campaign. A walk that sticks
        /// is a wall only a conversion roll or a lucky duplicate could open, which is the thing the
        /// rule forbids, so it FAILS rather than reports.
        ///
        /// Beside it, each wall's cost to that player (median, 90th percentile, worst) and the
        /// fusing player's for comparison, because the rule says a good roll makes a wall faster
        /// and that is only a claim until both are on one line.
        /// </summary>
        private static void CheckFusionNeverGates(ContentDatabase db)
        {
            Line("");
            Line("FUSION NEVER GATES A WALL (" + FusionNeverGatesWalks + " walks that never convert and never ascend, against "
                 + FusionNeverGatesWalks + " that do)");
            List<EconomyRun> refs = new List<EconomyRun>(), fused = new List<EconomyRun>();
            for (int k = 0; k < FusionNeverGatesWalks; k++)
            {
                refs.Add(SimulateEconomyUnder(db, WalkSeed(k), false, false));
                fused.Add(SimulateEconomy(db, false, WalkSeed(k)));
            }
            int stuck = 0;
            List<string> where = new List<string>();
            for (int k = 0; k < refs.Count; k++)
                if (refs[k].StuckAt >= 0) { stuck++; where.Add(db.AllStages[refs[k].StuckAt].DisplayCode); }

            Line("  wall    reference: extra sittings med/p90/max   entry xRec med     fusing: extra med/p90/max   entry xRec med");
            for (int s = 0; s < db.AllStages.Count; s++)
            {
                StageDef st = db.AllStages[s];
                if (!BandKindOf(st, s).StartsWith("wall")) continue;
                Line("  " + st.DisplayCode.PadRight(6) + WallCosts(refs, s).PadLeft(35) + WallEntry(refs, s).PadLeft(17)
                     + WallCosts(fused, s).PadLeft(32) + WallEntry(fused, s).PadLeft(17));
            }
            Check(stuck == 0,
                  stuck == 0
                    ? "all " + refs.Count + " walks that never convert and never ascend finish the campaign: no wall waits on a roll"
                    : stuck + " of " + refs.Count + " walks that never convert and never ascend are STUCK (at "
                      + string.Join(", ", where.ToArray()) + "): a wall there opens only for a conversion or a lucky duplicate");
            Line("");
        }

        private static string WallCosts(List<EconomyRun> runs, int s)
        {
            List<int> xs = new List<int>();
            int missing = 0;
            for (int k = 0; k < runs.Count; k++)
            {
                if (s >= runs[k].Stages.Count) { missing++; continue; }
                EconomyStageOutcome o = runs[k].Stages[s];
                int ladder, floor;
                xs.Add(o.Cleared ? WallExtraSittings(o, out ladder, out floor) : int.MaxValue);
            }
            if (xs.Count == 0) return "never reached";
            int p90 = Percentile(xs, 0.9), max = Percentile(xs, 1.0);
            return PaceMedian(xs) + "/" + (p90 == int.MaxValue ? "STUCK" : p90.ToString()) + "/"
                   + (max == int.MaxValue ? "STUCK" : max.ToString()) + (missing > 0 ? " (" + missing + " short)" : "");
        }

        private static string WallEntry(List<EconomyRun> runs, int s)
        {
            List<int> xs = new List<int>();
            for (int k = 0; k < runs.Count; k++)
                if (s < runs[k].Stages.Count) xs.Add((int)Math.Round(runs[k].Stages[s].EntryRatio * 1000));
            return xs.Count == 0 ? "-" : "x" + Ratio3(PaceMedian(xs) / 1000f);
        }

        /// <summary>Target B's tail: no more than this many extra sittings at the 90th percentile of
        /// the walks, beside the 2 to 4 at the median. A wall most players pay 3 for and an
        /// unlucky one pays 21 for has failed, because that player stops and never says why.</summary>
        private const int PaceFinaleExtraP90Max = 6;

        /// <summary>The median of a cost list in which a walk that never cleared reads
        /// int.MaxValue; the lower middle on an even count, so a stuck walk cannot overflow it.</summary>
        private static int PaceMedian(List<int> xs)
        {
            if (xs.Count == 0) return 0;
            List<int> s = new List<int>(xs);
            s.Sort();
            if (s.Count % 2 == 1) return s[s.Count / 2];
            int a = s[s.Count / 2 - 1], b = s[s.Count / 2];
            return b == int.MaxValue ? b : (a + b) / 2;
        }

        private static string CostList(List<int> xs)
        {
            List<int> s = new List<int>(xs);
            s.Sort();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(s[i] == int.MaxValue ? "STUCK" : s[i].ToString());
            }
            return sb.ToString();
        }

        private static string PctRange(List<double> xs)
        {
            if (xs.Count == 0) return "-";
            double lo = double.MaxValue, hi = double.MinValue;
            foreach (double v in xs) { if (v < lo) lo = v; if (v > hi) hi = v; }
            return Pct((float)lo) + " to " + Pct((float)hi);
        }

        private static int CountWhere(List<double> xs, Func<double, bool> pred)
        {
            int n = 0;
            foreach (double v in xs) if (pred(v)) n++;
            return n;
        }

        // ------------------------------------------------------------ target A, by what bought it

        private static Dictionary<string, int> LevelsByUid(PlayerProfile profile)
        {
            Dictionary<string, int> d = new Dictionary<string, int>();
            for (int i = 0; i < profile.Heroes.Count; i++) d[profile.Heroes[i].Uid] = profile.Heroes[i].Level;
            return d;
        }

        private static void Credit(Dictionary<string, int> into, string uid, int levels)
        {
            if (levels == 0) return;
            int had;
            into.TryGetValue(uid, out had);
            into[uid] = had + levels;
        }

        private static void CreditLevels(PlayerProfile profile, Dictionary<string, int> before, Dictionary<string, int> into)
        {
            for (int i = 0; i < profile.Heroes.Count; i++)
            {
                HeroInstance h = profile.Heroes[i];
                int was;
                if (!before.TryGetValue(h.Uid, out was)) was = 1;
                Credit(into, h.Uid, h.Level - was);
            }
        }

        /// <summary>
        /// Splits the deployed line's Levels by what paid for them. Power is linear in Level
        /// (StatMath.Build is base * (1 + growth * (Level - 1))), so a hero's power above Level 1 is
        /// shared between gold and copies exactly in proportion to the Levels each bought.
        /// </summary>
        private static void AttributeEntryLevels(EconomyRun run, EconomyStageOutcome o)
        {
            List<HeroInstance> team = run.Profile.TeamHeroes();
            double gold = 0, copies = 0;
            int levelOne = 0, unattributed = 0;
            for (int i = 0; i < team.Count; i++)
            {
                HeroInstance h = team[i];
                if (h.Def == null) continue;
                int p1 = StatMath.PowerScore(StatMath.ForHero(h.Def, 1));
                int pl = StatMath.PowerScore(StatMath.ForHero(h.Def, h.Level));
                levelOne += p1;
                int g, c;
                run.GoldLevels.TryGetValue(h.Uid, out g);
                run.CopyLevels.TryGetValue(h.Uid, out c);
                unattributed += Math.Abs(h.Level - 1 - g - c);
                if (g + c <= 0) continue;
                gold += (pl - p1) * (double)g / (g + c);
                copies += (pl - p1) * (double)c / (g + c);
            }
            o.EntryLevelOnePower = levelOne;
            o.EntryGoldLevelPower = (int)Math.Round(gold);
            o.EntryCopyLevelPower = (int)Math.Round(copies);
            o.EntryUnattributedLevels = unattributed;
        }

        /// <summary>Target A's two readings for one walk. "Stands" is the committed split: the
        /// line at TIER level 1 is roster, so a Level bought with gold below a tier becomes roster
        /// the moment the hero ascends. "Bought" is the split by payment: the line at Level 1 and
        /// the Levels copies paid for are roster, the Levels gold paid for are levels.</summary>
        private sealed class PaceSplit
        {
            public double LevelsStands, RosterStands, LevelsBought, RosterBought, Armor;
            public int Unattributed;
        }

        private static PaceSplit SplitPace(EconomyStageOutcome first, EconomyStageOutcome last)
        {
            PaceSplit s = new PaceSplit();
            double total = Math.Log(last.EntryPower / (double)Math.Max(1, first.EntryRosterPower));
            if (total <= 0) return s;
            s.RosterStands = Math.Log(last.EntryRosterPower / (double)Math.Max(1, first.EntryRosterPower)) / total;
            s.LevelsStands = Math.Log(last.EntryBarePower / (double)Math.Max(1, last.EntryRosterPower)) / total;
            s.Armor = Math.Log(last.EntryPower / (double)Math.Max(1, last.EntryBarePower)) / total;
            double lineLog = Math.Log(last.EntryLevelOnePower / (double)Math.Max(1, first.EntryRosterPower));
            double levelLog = Math.Log(last.EntryBarePower / (double)Math.Max(1, last.EntryLevelOnePower));
            double paid = last.EntryGoldLevelPower + last.EntryCopyLevelPower;
            double goldFrac = paid > 0 ? last.EntryGoldLevelPower / paid : 0;
            s.LevelsBought = levelLog * goldFrac / total;
            s.RosterBought = (lineLog + levelLog * (1 - goldFrac)) / total;
            s.Unattributed = last.EntryUnattributedLevels;
            return s;
        }

        private static int Median(List<int> xs)
        {
            if (xs.Count == 0) return 0;
            List<int> s = new List<int>(xs);
            s.Sort();
            return s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2;
        }

        /// <summary>Nearest-rank percentile, so the 90th of 20 readings is the 18th.</summary>
        private static int Percentile(List<int> xs, double p)
        {
            if (xs.Count == 0) return 0;
            List<int> s = new List<int>(xs);
            s.Sort();
            int rank = (int)Math.Ceiling(p * s.Count);
            if (rank < 1) rank = 1;
            return s[rank - 1];
        }

        /// <summary>
        /// DevHarness.exe [contentDir] --line-calibration. A development aid: the two geared lines
        /// side by side. For every stage, each line's level-1 geared power (the lowest it can be
        /// placed at), which one InstrumentLineFor reads the stage with, and whether that stage is
        /// graded. Where BOTH lines can be placed at recommendedPower, both are read at the CEIL at
        /// the same seed and GuardCeilSamples, which is what "a different yardstick" is worth.
        /// </summary>
        private static int RunLineCalibration(string contentDir)
        {
            Console.OutputEncoding = Encoding.UTF8;
            ContentDatabase db = LoadContent(contentDir);
            if (db == null) return 1;
            Line("");
            Line("LINE CALIBRATION - ChapterOneLine (3-stars) against PinnedLine (5-stars), geared, at recommendedPower");
            Line("  stage   recPwr   3* min   3* max   5* min   read with   graded   3* CEIL   5* CEIL");
            Dictionary<int, int> graded = new Dictionary<int, int>(), total = new Dictionary<int, int>();
            for (int s = 0; s < db.AllStages.Count; s++)
            {
                StageDef st = db.AllStages[s];
                int min3 = BuildLineAt(db, st, true, 0, ChapterOneLine).Profile.EffectiveTeamPower(db);
                int max3 = BuildLineAt(db, st, true, int.MaxValue, ChapterOneLine).Profile.EffectiveTeamPower(db);
                int min5 = BuildLineAt(db, st, true, 0, PinnedLine).Profile.EffectiveTeamPower(db);
                bool isGraded = !IsFloorPinned(db, st);
                int ch = st.ChapterNumber;
                if (!total.ContainsKey(ch)) { total[ch] = 0; graded[ch] = 0; }
                total[ch]++;
                if (isGraded) graded[ch]++;
                string c3 = "-", c5 = "-";
                if (st.RecommendedPower > min3 && st.RecommendedPower <= max3 && st.RecommendedPower > min5)
                {
                    int w = Math.Max(1, st.WaveCount), b = Math.Max(1, st.BattleCount);
                    int seed = 42000000 + st.GlobalIndex * 101;
                    c3 = Pct(CurveRate(BuildLineAt(db, st, true, st.RecommendedPower, ChapterOneLine), st, w, b, GuardCeilSamples, seed));
                    c5 = Pct(CurveRate(BuildLineAt(db, st, true, st.RecommendedPower, PinnedLine), st, w, b, GuardCeilSamples, seed));
                }
                Line("  " + st.DisplayCode.PadRight(6) + st.RecommendedPower.ToString().PadLeft(8) + min3.ToString().PadLeft(9)
                     + max3.ToString().PadLeft(9) + min5.ToString().PadLeft(9) + (InstrumentLineFor(db, st) == ChapterOneLine ? "3-stars" : "5-stars").PadLeft(12)
                     + (isGraded ? "yes" : "PINNED").PadLeft(9) + c3.PadLeft(10) + c5.PadLeft(10));
            }
            Line("");
            foreach (KeyValuePair<int, int> kv in total)
                Line("  chapter " + kv.Key + ": " + graded[kv.Key] + " of " + kv.Value + " stages CEIL-graded");
            return 0;
        }

        /// <summary>
        /// DevHarness.exe [contentDir] --wall-study [walks]. A development aid: the same economy
        /// walked by three players - the one every table reads, one who pulls and ascends but never
        /// converts, and one who does neither - with entry power per stage and wall cost per walk.
        /// </summary>
        private static int RunWallStudy(string contentDir, int walks)
        {
            Console.OutputEncoding = Encoding.UTF8;
            ContentDatabase db = LoadContent(contentDir);
            if (db == null) return 1;
            _wallProbeSamples = 100;
            string[] names = { "fuses", "no conversion", "no fusion" };
            bool[,] modes = { { true, true }, { false, true }, { false, false } };
            for (int m = 0; m < 3; m++)
            {
                string[] keys = { "fuses", "noconv", "none" };
                if (_wallModes != null && Array.IndexOf(_wallModes.Split(','), keys[m]) < 0) continue;
                Line("");
                Line("WALL STUDY - " + names[m] + ", " + walks + " walks");
                List<EconomyRun> runs = new List<EconomyRun>();
                for (int k = 0; k < walks; k++) runs.Add(SimulateEconomyUnder(db, WalkSeed(k), modes[m, 0], modes[m, 1]));
                Line("  stage   recPwr   entry p10  median   p90   ratio@median  extra med/p90/max  stuck");
                for (int s = 0; s < db.AllStages.Count; s++)
                {
                    StageDef st = db.AllStages[s];
                    List<int> entry = new List<int>(), extra = new List<int>();
                    int stuck = 0;
                    for (int k = 0; k < runs.Count; k++)
                    {
                        if (s < runs[k].Stages.Count)
                        {
                            EconomyStageOutcome o = runs[k].Stages[s];
                            entry.Add(o.EntryPower);
                            int ladder, floor;
                            extra.Add(WallExtraSittings(o, out ladder, out floor));
                            if (!o.Cleared) stuck++;
                        }
                    }
                    int med = Median(entry);
                    Line("  " + st.DisplayCode.PadRight(6) + st.RecommendedPower.ToString().PadLeft(8)
                         + Percentile(entry, 0.1).ToString().PadLeft(10) + med.ToString().PadLeft(8)
                         + Percentile(entry, 0.9).ToString().PadLeft(7)
                         + ("x" + Ratio3(st.RecommendedPower > 0 ? med / (float)st.RecommendedPower : 0f)).PadLeft(14)
                         + (Median(extra) + "/" + Percentile(extra, 0.9) + "/" + Percentile(extra, 1.0)).PadLeft(19)
                         + (entry.Count < runs.Count ? "   reached by " + entry.Count : "")
                         + (stuck > 0 ? "   STUCK " + stuck : "")
                         + (BandKindOf(st, s).StartsWith("wall") ? "   WALL" : ""));
                    if (!BandKindOf(st, s).StartsWith("wall")) continue;
                    List<int> capped = new List<int>(), ratePct = new List<int>();
                    for (int k = 0; k < runs.Count; k++)
                    {
                        if (s >= runs[k].Stages.Count) continue;
                        capped.Add(runs[k].Stages[s].EntryCappedPower);
                        ratePct.Add((int)Math.Round(runs[k].Stages[s].EntryCappedCeilRate * 100f));
                    }
                    Line("          at cap, no conversion: power p10 " + Percentile(capped, 0.1) + " (x"
                         + Ratio3(Percentile(capped, 0.1) / (float)st.RecommendedPower) + "), median " + Median(capped)
                         + "; CEIL win rate min " + Percentile(ratePct, 0) + "% p10 " + Percentile(ratePct, 0.1)
                         + "% median " + Median(ratePct) + "%");
                }
            }
            return 0;
        }
    }
}
