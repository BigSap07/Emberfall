using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Emberfall.Core.Combat;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE DAILY LOOP CURVE (Docs/DAILY-LOOP-CURVE.md): what turning the daily orders (DailyLoopSwitch) and the
    /// free Wayfarer's Road track on by default does to the reference walk. Both are ON by default since the owner's
    /// decision of 2026-10-23; the off arm is the build before that. A measurement, never part of the gate, and inert
    /// in every other run (_walkLoopMode is 0). The switches are set for the length of one arm's batch and put back.
    ///
    ///   DevHarness.exe --daily-loop-curve [walks]      the reference player (never converts, never ascends)
    ///   add --daily-loop-fuse                          to walk the fusing player (converts and ascends) as well
    ///
    /// THE ARMS, on the same seeds (WalkSeed(0) up) and the same clock, the season calendar's (StoreCurve.cs), so
    /// the Road has a season to run in and the only difference between arms is the loop:
    ///   off   the build before 2026-10-23: DailyLoopSwitch off, the Road on and therefore inert.
    ///   on    DailyLoopSwitch on and the Road on, played by a diligent player (DailyLoopWalkStep).
    ///   loop  DailyLoopSwitch on and the Road OFF, to split what the orders pay from what the Road pays.
    ///
    /// THE DILIGENT PLAYER claims at the start and at the end of every sitting: every finished order, the daily
    /// chest, the weekly chest, the calendar, and every reached free tier of the Road. The walk's own play does the
    /// orders (waves, stage wins, levels, summons); the arena order is done by Proving fights from the Brand bank,
    /// once the Proving is open, and a spend-Gold order the line could not finish (a line at its star cap) by the
    /// cheapest bench level or Quartermaster piece at the end of the sitting (LoopFinishSpendGold), as a player
    /// clearing the board would. A selector picks the hero of its star count
    /// with the highest level-1 power score, ties broken by id: a fixed rule, never a draw.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>0 = inert. 1 = off (control, calendar clock). 2 = loop and Road on. 3 = loop on, Road off.</summary>
        private static int _walkLoopMode = 0;
        private static bool _loopCurveFuse;
        /// <summary>--daily-loop-no-bench: the diligent player does NOT level a bench hero for the level order (a diagnostic).</summary>
        private static bool _loopCurveNoBench;

        private sealed class LoopTally
        {
            public long LoopGold, LoopShards, RoadGold, RoadShards, RoadDust, RoadSlag;
            public long BenchLevelGold;
            public int BenchLevels;
            public int QuartermasterPieces;
            public int Orders, DailyChests, WeeklyChests, CalendarDays, ProvingFights, Selector4, Selector5;
            public long FirstFiveStarAt = -1, FirstFourStarAt = -1;
            public readonly List<int> TiersPerSeason = new List<int>();
            public long Season;
            public int TierThisSeason;
            public readonly List<string> SelectorPicks = new List<string>();
            /// <summary>Per order id, the days it was on a board and the days it was claimed before the board lapsed.</summary>
            public readonly Dictionary<string, int[]> ByOrder = new Dictionary<string, int[]>();
        }

        /// <summary>Counts a board that is about to be replaced: which of its orders were claimed.</summary>
        private static void LoopNoteLapsingBoard(GameSession s, LoopTally t, long now)
        {
            DailyLoopState st = s.Profile.DailyLoop;
            if (st.DayUnix == 0 || s.DayStartUnix(now) <= st.DayUnix) return;
            for (int i = 0; i < st.Orders.Count; i++)
            {
                int[] c;
                if (!t.ByOrder.TryGetValue(st.Orders[i].Id, out c)) { c = new int[2]; t.ByOrder[st.Orders[i].Id] = c; }
                c[0]++;
                if (st.Orders[i].Claimed) c[1]++;
            }
        }

        private static readonly Dictionary<GameSession, LoopTally> _loopTallies = new Dictionary<GameSession, LoopTally>();

        private static LoopTally TallyOf(GameSession s)
        {
            lock (_loopTallies)
            {
                LoopTally t;
                if (!_loopTallies.TryGetValue(s, out t)) { t = new LoopTally(); _loopTallies[s] = t; }
                return t;
            }
        }

        /// <summary>The diligent player's claims, called by the walk at the start and the end of every sitting. Inert
        /// unless --daily-loop-curve set a loop arm.</summary>
        private static void DailyLoopWalkStep(ContentDatabase db, GameSession s, long now, bool endOfSitting)
        {
            if (_walkLoopMode < 2) return;
            LoopTally t = TallyOf(s);
            PlayerProfile p = s.Profile;
            LoopNoteLapsingBoard(s, t, now);

            // The arena order, from the Brand bank, once the Proving is open.
            if (FeatureGate.IsUnlocked(db, p, Feature.Proving))
            {
                LoopBoard peek = DailyLoop.Board(s, now);
                for (int i = 0; i < peek.Orders.Count; i++)
                {
                    OrderDef def = db.Orders.Order(peek.Orders[i].Id);
                    if (def == null || def.Kind != OrderKind.Arena) continue;
                    int need = peek.Orders[i].Target - peek.Orders[i].Progress;
                    for (int k = 0; k < need && s.TryStartProvingAttempt(now); k++)
                    {
                        BattleLoadout opp = s.CurrentProvingOpponent(now);
                        int seed = unchecked((int)(now * 31L) ^ 0x5eed ^ k);
                        BattleResult r = CombatSimulation.Run(s.BuildProvingRequest(ProvingGenerator.BuildStage(), opp, seed));
                        s.GrantProvingResult(r.Victory, now);
                        t.ProvingFights++;
                    }
                }
            }

            // The spend-Gold order (it replaced the level order, owner 2026-10-23). The walk's own line levels count
            // toward it; when the line is at its cap, at the END of a sitting, after the walk has spent its Gold on the
            // line, the cheapest level in the roster and then a Quartermaster piece, as a player clearing the board
            // would. Never at the start, where it would take Gold the line was about to use.
            if (endOfSitting && !_loopCurveNoBench)
            {
                long spent; int lv, pieces;
                LoopFinishSpendGold(db, s, now, out spent, out lv, out pieces);
                t.BenchLevelGold += spent;
                t.BenchLevels += lv;
                t.QuartermasterPieces += pieces;
            }

            long gold = p.Gold, shards = p.Shards;
            LoopBoard b = DailyLoop.Board(s, now);
            for (int i = 0; i < b.Orders.Count; i++)
                if (b.Orders[i].CanClaim && DailyLoop.ClaimOrder(s, b.Orders[i].Id, now).Success) t.Orders++;
            if (DailyLoop.ClaimDailyChest(s, now).Success) t.DailyChests++;
            if (DailyLoop.ClaimWeeklyChest(s, now).Success) t.WeeklyChests++;
            if (DailyLoop.ClaimCalendar(s, now).Success) t.CalendarDays++;
            t.LoopGold += p.Gold - gold;
            t.LoopShards += p.Shards - shards;

            if (_walkLoopMode != 2) return;
            PassView v = WayfarersRoad.View(s, now);
            if (!v.Active) return;
            if (v.Season != t.Season)
            {
                if (t.Season != 0) t.TiersPerSeason.Add(t.TierThisSeason);
                t.Season = v.Season;
            }
            t.TierThisSeason = v.Reached;
            gold = p.Gold; shards = p.Shards;
            long dust = p.RelicDust, slag = p.Slag;
            for (int i = 0; i < v.Tiers.Count; i++)
            {
                PassTierView tier = v.Tiers[i];
                if (!tier.CanClaim) continue;
                string pick = tier.Kind == PassRewardKind.Selector ? LoopSelectorPick(s, tier.Stars) : null;
                PassResult r = WayfarersRoad.Claim(s, tier.Tier, pick, now);
                if (!r.Success || tier.Kind != PassRewardKind.Selector) continue;
                if (tier.Stars >= 5) { t.Selector5++; if (t.FirstFiveStarAt < 0) t.FirstFiveStarAt = now; }
                else { t.Selector4++; if (t.FirstFourStarAt < 0) t.FirstFourStarAt = now; }
                t.SelectorPicks.Add(tier.Stars + ":" + pick);
            }
            t.RoadGold += p.Gold - gold;
            t.RoadShards += p.Shards - shards;
            t.RoadDust += p.RelicDust - dust;
            t.RoadSlag += p.Slag - slag;
        }

        /// <summary>Finishes today's spend-Gold order the way a player clearing the board would: the cheapest level of any
        /// hero below its cap, one at a time, and when no hero can level, the cheapest Quartermaster piece on the shelf. A
        /// fixed rule, never a draw. Shared by both daily loop studies (this file and LoopCurve.cs).</summary>
        private static void LoopFinishSpendGold(ContentDatabase db, GameSession s, long now, out long goldSpent, out int levels, out int pieces)
        {
            goldSpent = 0; levels = 0; pieces = 0;
            PlayerProfile p = s.Profile;
            LoopBoard board = DailyLoop.Board(s, now);
            for (int i = 0; i < board.Orders.Count; i++)
            {
                OrderDef def = db.Orders.Order(board.Orders[i].Id);
                if (def == null || def.Kind != OrderKind.Gold) continue;
                for (int guard = 0; guard < 50 && DailyLoop.Board(s, now).Orders[i].Progress < def.Target; guard++)
                {
                    HeroInstance cheapest = null;
                    int cost = int.MaxValue;
                    for (int h = 0; h < p.Heroes.Count; h++)
                    {
                        if (p.Heroes[h].Level >= s.HeroLevelCapOf(p.Heroes[h])) continue;
                        int c = s.LevelUpCost(p.Heroes[h]);
                        if (c < cost) { cost = c; cheapest = p.Heroes[h]; }
                    }
                    long before = p.Gold;
                    if (cheapest != null && p.Gold >= cost)
                    {
                        if (!s.LevelUpHero(cheapest, 1, now).Success) break;
                        levels++;
                    }
                    else
                    {
                        QuartermasterView shelf = Quartermaster.View(s, now);
                        int pick = -1;
                        long price = long.MaxValue;
                        for (int k = 0; shelf != null && k < shelf.Slots.Count; k++)
                            if (!shelf.Slots[k].Sold && shelf.Slots[k].Price < price && shelf.Slots[k].Price <= p.FreeGold) { price = shelf.Slots[k].Price; pick = k; }
                        if (pick < 0 || !Quartermaster.Buy(s, pick, now).Success) break;
                        pieces++;
                    }
                    goldSpent += before - p.Gold;
                }
            }
        }

        /// <summary>The selector's fixed rule: the highest level-1 power score of that star count, ties by id.</summary>
        private static string LoopSelectorPick(GameSession s, int stars)
        {
            List<HeroDef> pool = WayfarersRoad.SelectorHeroes(s, stars);
            HeroDef best = null;
            int bestScore = -1;
            for (int i = 0; i < pool.Count; i++)
            {
                int score = StatMath.PowerScore(StatMath.ForHero(pool[i], 1));
                if (best == null || score > bestScore || (score == bestScore && string.CompareOrdinal(pool[i].Id, best.Id) < 0))
                {
                    best = pool[i];
                    bestScore = score;
                }
            }
            return best != null ? best.Id : null;
        }

        private sealed class LoopArm
        {
            public string Name;
            public int Mode;
            public EconomyRun[] Runs;
            public long Start;
        }

        private static LoopArm LoopRunArm(ContentDatabase db, string name, int mode, int walks, bool fuse)
        {
            bool loopWas = DailyLoopSwitch.Enabled, roadWas = WayfarersRoadSwitch.Enabled;
            int modeWas = _walkLoopMode;
            try
            {
                _walkLoopMode = mode;
                DailyLoopSwitch.Enabled = mode >= 2;   // the off arm is the loop OFF, whatever the build's default (ON since 2026-10-23)
                WayfarersRoadSwitch.Enabled = mode == 3 ? false : roadWas;
                LoopArm arm = new LoopArm();
                arm.Name = name;
                arm.Mode = mode;
                arm.Start = StoreWalkClockStart(db);
                arm.Runs = SimulateEconomies(db, walks, fuse, fuse);
                return arm;
            }
            finally
            {
                _walkLoopMode = modeWas;
                DailyLoopSwitch.Enabled = loopWas;
                WayfarersRoadSwitch.Enabled = roadWas;
            }
        }

        /// <summary>The day (from the walk's first sitting) a stage was first attempted, median over the arm.</summary>
        private static float LoopEntryDay(LoopArm arm, int stageIndex)
        {
            List<int> hours = new List<int>();
            for (int k = 0; k < arm.Runs.Length; k++)
            {
                EconomyRun r = arm.Runs[k];
                if (stageIndex >= r.Stages.Count) continue;
                long elapsed = 0;
                for (int i = 0; i < stageIndex; i++) elapsed += r.Stages[i].ElapsedSeconds;
                hours.Add((int)(elapsed / 3600L));
            }
            return hours.Count == 0 ? -1f : Median(hours) / 24f;
        }

        private static float LoopFinishDay(LoopArm arm)
        {
            List<int> hours = new List<int>();
            for (int k = 0; k < arm.Runs.Length; k++)
            {
                EconomyRun r = arm.Runs[k];
                if (r.StuckAt >= 0) continue;
                long elapsed = 0;
                for (int i = 0; i < r.Stages.Count; i++) elapsed += r.Stages[i].ElapsedSeconds;
                hours.Add((int)(elapsed / 3600L));
            }
            return hours.Count == 0 ? -1f : Median(hours) / 24f;
        }

        private static string LoopDay(float d) { return d < 0 ? "-" : d.ToString("0.0", CultureInfo.InvariantCulture); }

        private static string LoopPct(long part, long whole)
        {
            return whole <= 0 ? "-" : (100.0 * part / whole).ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>DevHarness.exe --daily-loop-curve [walks]: the reference walk with the daily loop and the Road off,
        /// on, and on without the Road; per wall the median entry power over recommendedPower, the extra sittings and
        /// the day it was reached, then what the loop and the Road paid.</summary>
        private static int RunDailyLoopCurve(string contentDir, int walks)
        {
            Console.OutputEncoding = Encoding.UTF8;
            ContentDatabase db = LoadContent(contentDir);
            if (db == null) return 1;
            _walkStoreDb = db;
            Line("");
            Line("DAILY LOOP CURVE - " + walks + " walks per arm, seeds WalkSeed(0) up, the season calendar's clock. Shipped defaults: daily loop "
                 + (DailyLoopSwitch.Enabled ? "ON" : "off") + ", Road " + (WayfarersRoadSwitch.Enabled ? "on" : "off") + ".");
            Line("  off = loop off (the build before 2026-10-23). on = loop and Road on, everything claimed every sitting. loop = loop on, Road off."
                 + (_loopCurveNoBench ? " DIAGNOSTIC: nothing spent off the line for the spend-Gold order." : ""));

            bool[] players = _loopCurveFuse ? new[] { false, true } : new[] { false };
            for (int pl = 0; pl < players.Length; pl++)
            {
                bool fuse = players[pl];
                lock (_loopTallies) _loopTallies.Clear();
                LoopArm off = LoopRunArm(db, "off", 1, walks, fuse);
                LoopArm on = LoopRunArm(db, "on", 2, walks, fuse);
                LoopArm loop = LoopRunArm(db, "loop", 3, walks, fuse);
                LoopArm[] arms = { off, on, loop };

                Line("");
                Line(fuse ? "THE FUSING PLAYER (converts and ascends)" : "THE REFERENCE PLAYER (never converts, never ascends)");
                Line("  stage   rec.Pwr   entry/rec: off    on      loop   |  extra sittings: off on loop  |  day reached: off    on    loop");
                for (int si = 0; si < db.AllStages.Count; si++)
                {
                    StageDef st = db.AllStages[si];
                    if (!(st.IsMiniboss || st.IsBoss) && st.Index != 1) continue;
                    StringBuilder row = new StringBuilder();
                    row.Append("  ").Append(st.DisplayCode.PadRight(6)).Append(" ").Append(st.RecommendedPower.ToString().PadLeft(8)).Append("            ");
                    for (int a = 0; a < arms.Length; a++) row.Append(StoreRatio(db, new List<EconomyRun>(arms[a].Runs), si).PadRight(8));
                    row.Append("|        ");
                    for (int a = 0; a < arms.Length; a++) row.Append(StoreExtra(new List<EconomyRun>(arms[a].Runs), si).PadLeft(4));
                    row.Append("           |      ");
                    for (int a = 0; a < arms.Length; a++) row.Append(LoopDay(LoopEntryDay(arms[a], si)).PadLeft(6));
                    Line(row.ToString());
                }

                Line("");
                for (int a = 0; a < arms.Length; a++)
                {
                    LoopArm arm = arms[a];
                    List<int> sit = new List<int>(), summons = new List<int>(), levels = new List<int>(), power = new List<int>();
                    int fielded = 0;
                    List<string> stuckAt = new List<string>();
                    for (int k = 0; k < arm.Runs.Length; k++)
                    {
                        EconomyRun r = arm.Runs[k];
                        if (r.StuckAt >= 0) stuckAt.Add(db.AllStages[r.StuckAt].DisplayCode + " (walk " + k + ")");
                        sit.Add(r.TotalSessions);
                        summons.Add(r.Ledger.Summons);
                        levels.Add(r.Ledger.HeroLevelsBought);
                        power.Add(r.Profile.EffectiveTeamPower(db));
                        LoopTally t = TallyOf(r.Session);
                        bool any = false;
                        foreach (HeroInstance h in r.Profile.TeamHeroes())
                            for (int i = 0; i < t.SelectorPicks.Count && !any; i++) any = t.SelectorPicks[i].EndsWith(":" + h.DefId);
                        if (any) fielded++;
                    }
                    Line("  " + arm.Name.PadRight(5) + " stuck " + StoreStuck(new List<EconomyRun>(arm.Runs)) + " of " + arm.Runs.Length
                         + (stuckAt.Count > 0 ? " at " + string.Join(", ", stuckAt.ToArray()) : "") + ", campaign finished on day " + LoopDay(LoopFinishDay(arm)) + ", " + Median(sit) + " sittings, " + Median(summons) + " summons, "
                         + Median(levels) + " line levels bought with Gold, final team power " + Median(power) + " (medians)"
                         + (arm.Mode == 2 ? "; a selector hero is in the final line in " + fielded + " of " + arm.Runs.Length + " walks" : ""));
                }

                for (int a = 1; a < arms.Length; a++)
                {
                    LoopArm arm = arms[a];
                    long lg = 0, ls = 0, rg = 0, rs = 0, rd = 0, rsl = 0, totalGold = 0, totalShards = 0;
                    List<int> orders = new List<int>(), daily = new List<int>(), weekly = new List<int>(), cal = new List<int>(), fights = new List<int>();
                    List<int> s4 = new List<int>(), s5 = new List<int>(), first5 = new List<int>(), first4 = new List<int>(), tiers = new List<int>();
                    Dictionary<string, int> picks = new Dictionary<string, int>();
                    SortedDictionary<string, long[]> byOrder = new SortedDictionary<string, long[]>(StringComparer.Ordinal);
                    for (int k = 0; k < arm.Runs.Length; k++)
                    {
                        EconomyRun r = arm.Runs[k];
                        LoopTally t = TallyOf(r.Session);
                        lg += t.LoopGold; ls += t.LoopShards; rg += t.RoadGold; rs += t.RoadShards; rd += t.RoadDust; rsl += t.RoadSlag;
                        totalGold += r.Ledger.GoldTotal + t.LoopGold + t.RoadGold;
                        totalShards += r.Ledger.ShardTotal + t.LoopShards + t.RoadShards;
                        orders.Add(t.Orders); daily.Add(t.DailyChests); weekly.Add(t.WeeklyChests); cal.Add(t.CalendarDays); fights.Add(t.ProvingFights);
                        s4.Add(t.Selector4); s5.Add(t.Selector5);
                        if (t.FirstFiveStarAt >= 0) first5.Add((int)((t.FirstFiveStarAt - arm.Start) / 3600L));
                        if (t.FirstFourStarAt >= 0) first4.Add((int)((t.FirstFourStarAt - arm.Start) / 3600L));
                        for (int i = 0; i < t.TiersPerSeason.Count; i++) tiers.Add(t.TiersPerSeason[i]);
                        foreach (KeyValuePair<string, int[]> kv in t.ByOrder)
                        {
                            long[] c;
                            if (!byOrder.TryGetValue(kv.Key, out c)) { c = new long[2]; byOrder[kv.Key] = c; }
                            c[0] += kv.Value[0];
                            c[1] += kv.Value[1];
                        }
                        for (int i = 0; i < t.SelectorPicks.Count; i++)
                        {
                            int c;
                            picks.TryGetValue(t.SelectorPicks[i], out c);
                            picks[t.SelectorPicks[i]] = c + 1;
                        }
                    }
                    Line("");
                    Line("  " + arm.Name + ": over the whole walk, medians per walk: " + Median(orders) + " orders, " + Median(daily) + " daily chests, "
                         + Median(weekly) + " weekly chests, " + Median(cal) + " calendar days, " + Median(fights) + " Proving fights for the arena order");
                    List<int> benchLv = new List<int>();
                    List<int> qmPieces = new List<int>();
                    long benchGold = 0;
                    for (int k = 0; k < arm.Runs.Length; k++) { LoopTally t = TallyOf(arm.Runs[k].Session); benchLv.Add(t.BenchLevels); qmPieces.Add(t.QuartermasterPieces); benchGold += t.BenchLevelGold; }
                    Line("    spend-Gold orders finished off the line: " + Median(benchLv) + " bench levels and " + Median(qmPieces) + " Quartermaster pieces per walk (median), costing " + LoopPct(benchGold, totalGold) + " of all Gold");
                    List<string> orderText = new List<string>();
                    foreach (KeyValuePair<string, long[]> kv in byOrder) orderText.Add(kv.Key + " " + LoopPct(kv.Value[1], kv.Value[0]) + " of " + (kv.Value[0] / Math.Max(1, arm.Runs.Length)) + " days");
                    Line("    each order, claimed before its board lapsed (days on a board per walk): " + string.Join(", ", orderText.ToArray()));
                    Line("    the loop paid " + LoopPct(lg, totalGold) + " of all Gold and " + LoopPct(ls, totalShards) + " of all Shards the walks earned");
                    if (arm.Mode == 2)
                    {
                        Line("    the Road paid " + LoopPct(rg, totalGold) + " of all Gold and " + LoopPct(rs, totalShards) + " of all Shards, plus "
                             + (rd / Math.Max(1, arm.Runs.Length)) + " Dust and " + (rsl / Math.Max(1, arm.Runs.Length)) + " Slag per walk");
                        Line("    selectors per walk: " + Median(s4) + " four-star and " + Median(s5) + " five-star (median); first four-star on day "
                             + (first4.Count == 0 ? "-" : LoopDay(Median(first4) / 24f)) + ", first five-star on day " + (first5.Count == 0 ? "-" : LoopDay(Median(first5) / 24f))
                             + "; tiers reached in completed seasons: " + (tiers.Count == 0 ? "-" : "median " + Median(tiers) + ", min " + Percentile(tiers, 0) + ", max " + Percentile(tiers, 1.0)));
                        List<string> pickText = new List<string>();
                        foreach (KeyValuePair<string, int> kv in picks) pickText.Add(kv.Key + " x" + kv.Value);
                        pickText.Sort(StringComparer.Ordinal);
                        Line("    selector picks (stars:hero x times, all walks): " + (pickText.Count == 0 ? "none" : string.Join(", ", pickText.ToArray())));
                    }
                }
            }
            return 0;
        }
    }
}
