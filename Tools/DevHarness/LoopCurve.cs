using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// WHAT TURNING THE DAILY LOOP ON WOULD DO TO THE CURVE (content pass, 2026-10-22). A measurement, not a gate:
    /// run by DevHarness.exe --loop-curve [walks] [scale], inert in every other run (_walkLoop is false).
    ///
    /// The daily loop (daily orders, the daily and weekly chests, the login calendar) is built and off by default
    /// (DailyLoopSwitch, EMBERFALL_DAILY_LOOP_ON), and Wayfarer's Road's free track earns its points only from the
    /// loop's claims. Neither is in the reference walk that recommendedPower and the power band are derived from,
    /// so switching the loop on is a change to the economy the curve was solved against. This study measures how
    /// big a change, on the reference walk itself:
    ///
    ///   off         the reference walk on the season calendar's clock, loop off (the control; the same clock
    ///               --store-curve's non-spender uses, so weeks and seasons are real)
    ///   loop        the same walk with the loop on and Wayfarer's Road off: the hours alone
    ///   loop+road   the loop on and Wayfarer's Road on: the hours plus the free track's hourglasses, Slag, Dust
    ///               and its two hero selectors a season
    ///
    /// THE LOOP PLAYER IS A DILIGENT ONE. At the end of every sitting they claim every finished order, the daily
    /// chest, the weekly chest and the day's calendar reward. Orders are progressed by the walk's own actions (its
    /// waves, stage wins, level ups and summons count through GameSession exactly as in the game), except the arena
    /// order: the walk never fights the Proving or the Recital, so this player is credited with the one or two fights
    /// a day that order asks for, and with the level order, which the walk's capped line cannot finish on its own (see
    /// LoopClaimAll). That makes the loop's income an upper bound for a player who plays twice a day.
    ///
    /// THE ROAD PLAYER claims every free tier as soon as it is reached; a selector names the first hero of the pool
    /// the walk does not own yet (else the first of the pool), which is a fair reading of "a 5-star of your choice"
    /// for a player who never converts.
    ///
    /// `scale` multiplies every hour in orders.json (orders, both chests, the calendar) in memory, so a candidate
    /// size can be measured without editing the file. 1 measures the file as it is.
    /// </summary>
    internal static partial class Program
    {
        private static bool _walkLoop;

        /// <summary>True for all three columns of --loop-curve, so the control walks the same clock as the loop player.</summary>
        private static bool _walkLoopClock;

        /// <summary>
        /// THE STUDY'S CLOCK STARTS FAR IN THE FUTURE, ON PURPOSE. A few calls the walk makes have a form that reads the
        /// device clock instead of taking the walk's (GameSession.LevelUpHero(hero, times) is one: its own comment says
        /// "The two-argument form reads the device clock"). With the loop on, that recorded an order at today's real date,
        /// which rolled the board months ahead of the walk's clock on the first sitting and froze it there, so after day
        /// one no order could ever be claimed again. Measured, not guessed: the first run of this study claimed one board
        /// in 134 reward days. Starting the walk's clock a couple of decades ahead makes any device-clock call "an earlier
        /// day", which DailyLoop.Roll ignores (a wound-back clock reopens nothing), while its progress still counts on the
        /// board of the walk's own day. Every column walks this clock, the control included.
        /// </summary>
        private const int LoopClockSeason = 300;
        private static bool _walkLoopRoad;

        // Accumulated over the walks of one column, read by the report.
        private static double _loopGold, _loopHours;
        private static long _loopDays;
        private static int _roadTiers, _roadSelectors, _roadSeasons;
        // Claims by source, and the reward days the walks lived through (distinct day starts).
        private static readonly Dictionary<string, int> _loopClaims = new Dictionary<string, int>();
        private static readonly HashSet<long> _loopDaySeen = new HashSet<long>();
        private static long _roadPointsEnd;
        private static int _loopDebugLines;

        private static void LoopCount(string what)
        {
            int n;
            _loopClaims.TryGetValue(what, out n);
            _loopClaims[what] = n + 1;
        }

        // The walk's clock at the end of the previous sitting, per walk (the walks of a column run in order).
        private static long _loopLastSittingEnd;

        /// <summary>Called by the walk at the start of every sitting, right after the store hook and before the idle
        /// claim. Claims what the previous sitting finished, at that sitting's own time, so a day rolling over between
        /// two sittings cannot throw away an evening's progress.</summary>
        private static void LoopWalkStep(ContentDatabase db, GameSession s, long now)
        {
            if (!_walkLoop) return;
            long at = _loopLastSittingEnd > 0 ? _loopLastSittingEnd : now;
            _loopLastSittingEnd = now;
            LoopClaimAll(db, s, at);
            LoopClaimAll(db, s, now);   // a fresh day's calendar reward, claimable at the start of a sitting
        }

        private static void LoopClaimAll(ContentDatabase db, GameSession s, long at)
        {
            DailyLoop.Roll(s, at);
            if (_loopDaySeen.Add(s.DayStartUnix(at))) _loopDays++;
            DailyLoop.Record(s, OrderKind.Arena, 2, at);   // the Proving or Recital fight the walk does not play
            // The level order, likewise. The walk spends Gold only on its deployed line, which sits at its level cap on most
            // days (the reference player never ascends), so on its own it finished the level order on 17 of 135 days and
            // the daily chest with it. A player levels a cheap bench hero for the order; that costs a sliver of Gold the walk
            // does not spend, which makes this an upper bound, as the arena credit is.
            DailyLoop.Record(s, OrderKind.Level, 3, at);

            DailyLoopState st = s.Profile.DailyLoop;
            if (Environment.GetEnvironmentVariable("EMBERFALL_LOOP_DEBUG") == "1" && _loopDebugLines < 40)
            {
                _loopDebugLines++;
                StringBuilder sb = new StringBuilder("  dbg at " + at + " day " + s.DayStartUnix(at) + " board day " + st.DayUnix + ":");
                for (int i = 0; i < st.Orders.Count; i++)
                {
                    OrderDef dd = db.Orders.Order(st.Orders[i].Id);
                    sb.Append(" " + st.Orders[i].Id + "=" + st.Orders[i].Progress + "/" + (dd != null ? dd.Target : -1) + (st.Orders[i].Claimed ? "c" : ""));
                }
                Line(sb.ToString());
            }
            for (int i = 0; i < st.Orders.Count; i++)
            {
                if (st.Orders[i].Claimed) continue;
                OrderDef d = db.Orders.Order(st.Orders[i].Id);
                LoopPayout p = DailyLoop.ClaimOrder(s, st.Orders[i].Id, at);
                if (p.Success) { _loopGold += p.Gold; _loopHours += d != null ? d.Hours : 0; LoopCount("order:" + (d != null ? d.Kind.ToString() : "?")); }
            }
            LoopPayout chest = DailyLoop.ClaimDailyChest(s, at);
            if (chest.Success) { _loopGold += chest.Gold; _loopHours += db.Orders.DailyChestHours; LoopCount("daily chest"); }
            LoopPayout week = DailyLoop.ClaimWeeklyChest(s, at);
            if (week.Success) { _loopGold += week.Gold; _loopHours += db.Orders.WeeklyChestHours; LoopCount("weekly chest"); }
            if (DailyLoop.CanClaimCalendar(s, at))
            {
                LoopPayout cal = DailyLoop.ClaimCalendar(s, at);
                if (cal.Success) { _loopGold += cal.Gold; _loopHours += cal.Hours; LoopCount("calendar"); }
            }

            if (!_walkLoopRoad) return;
            WayfarersRoadConfig road = db.WayfarersRoad;
            for (int i = 0; i < road.Tiers.Count; i++)
            {
                PassRewardDef t = road.Tiers[i];
                string hero = null;
                if (t.Kind == PassRewardKind.Selector)
                {
                    List<HeroDef> pool = WayfarersRoad.SelectorHeroes(s, t.Stars);
                    for (int k = 0; k < pool.Count && hero == null; k++)
                        if (s.Profile.FindHeroByDefId(pool[k].Id) == null) hero = pool[k].Id;
                    if (hero == null && pool.Count > 0) hero = pool[0].Id;
                }
                PassResult r = WayfarersRoad.Claim(s, t.Tier, hero, at);
                if (!r.Success) continue;
                _roadTiers++;
                if (t.Kind == PassRewardKind.Selector) _roadSelectors++;
                if (t.Tier == road.Tiers[road.Tiers.Count - 1].Tier) _roadSeasons++;
            }
        }

        /// <summary>Every hour in orders.json multiplied by scale, in memory. Returns what to put back.</summary>
        private static double[] LoopScaleHours(ContentDatabase db, double scale)
        {
            OrdersConfig c = db.Orders;
            List<double> was = new List<double>();
            for (int i = 0; i < c.Orders.Count; i++) { was.Add(c.Orders[i].Hours); c.Orders[i].Hours *= scale; }
            for (int i = 0; i < c.Calendar.Count; i++) { was.Add(c.Calendar[i].Hours); c.Calendar[i].Hours *= scale; }
            was.Add(c.DailyChestHours); c.DailyChestHours *= scale;
            was.Add(c.WeeklyChestHours); c.WeeklyChestHours *= scale;
            return was.ToArray();
        }

        private static void LoopRestoreHours(ContentDatabase db, double[] was)
        {
            OrdersConfig c = db.Orders;
            int k = 0;
            for (int i = 0; i < c.Orders.Count; i++) c.Orders[i].Hours = was[k++];
            for (int i = 0; i < c.Calendar.Count; i++) c.Calendar[i].Hours = was[k++];
            c.DailyChestHours = was[k++];
            c.WeeklyChestHours = was[k++];
        }

        private sealed class LoopColumn
        {
            public string Name;
            public List<EconomyRun> Runs;
            public double Gold, Hours;
            public long Days;
            public int RoadTiers, RoadSelectors, RoadSeasons;
            public string Claims = "";
            public long RoadPointsEnd;
        }

        private static LoopColumn LoopRuns(ContentDatabase db, string name, int walks, bool loop, bool road)
        {
            bool loopWas = DailyLoopSwitch.Enabled, roadWas = WayfarersRoadSwitch.Enabled;
            _walkLoopClock = true;
            _walkLoop = loop;
            _walkLoopRoad = road;
            _loopGold = 0; _loopHours = 0; _loopDays = 0;
            _roadTiers = 0; _roadSelectors = 0; _roadSeasons = 0;
            _loopClaims.Clear(); _roadPointsEnd = 0;
            try
            {
                DailyLoopSwitch.Enabled = loop;
                WayfarersRoadSwitch.Enabled = road;
                LoopColumn col = new LoopColumn();
                col.Name = name;
                col.Runs = new List<EconomyRun>();
                for (int k = 0; k < walks; k++)
                {
                    _loopLastSittingEnd = 0;
                    _loopDaySeen.Clear();
                    EconomyRun r = SimulateEconomyUnder(db, WalkSeed(k), false, false);
                    col.Runs.Add(r);
                    if (r.Profile != null && r.Profile.WayfarersRoad != null) _roadPointsEnd += r.Profile.WayfarersRoad.Points;
                }
                col.Gold = _loopGold; col.Hours = _loopHours; col.Days = _loopDays;
                col.RoadTiers = _roadTiers; col.RoadSelectors = _roadSelectors; col.RoadSeasons = _roadSeasons;
                List<string> parts = new List<string>();
                foreach (KeyValuePair<string, int> kv in _loopClaims) parts.Add(kv.Key + " " + kv.Value);
                parts.Sort(StringComparer.Ordinal);
                col.Claims = string.Join(", ", parts.ToArray());
                col.RoadPointsEnd = _roadPointsEnd;
                return col;
            }
            finally
            {
                _walkLoop = false;
                _walkLoopRoad = false;
                _walkLoopClock = false;
                DailyLoopSwitch.Enabled = loopWas;
                WayfarersRoadSwitch.Enabled = roadWas;
            }
        }

        /// <summary>The median entry ratio over recommendedPower at one stage, as a number (0 when unread).</summary>
        private static float LoopRatio(ContentDatabase db, List<EconomyRun> runs, int stageIndex)
        {
            StageDef st = db.AllStages[stageIndex];
            int med = StoreMedianEntry(runs, stageIndex);
            return st.RecommendedPower > 0 && med > 0 ? med / (float)st.RecommendedPower : 0f;
        }

        /// <summary>DevHarness.exe --loop-curve [walks] [scale].</summary>
        private static int RunLoopCurve(string contentDir, int walks, double scale)
        {
            Console.OutputEncoding = Encoding.UTF8;
            ContentDatabase db = LoadContent(contentDir);
            if (db == null) return 1;
            if (!db.Orders.IsUsable) { Line("orders.json is not usable; nothing to measure."); return 1; }
            Line("");
            Line("LOOP CURVE - " + walks + " reference walks per column (never converts, never ascends), orders.json hours x"
                 + scale.ToString("0.###", CultureInfo.InvariantCulture) + ". Median entry power over recommendedPower.");
            Line("  off = loop off on the season clock (control). loop = daily loop on, Wayfarer's Road off. loop+road = both on.");

            double[] was = LoopScaleHours(db, scale);
            LoopColumn off, loop, both;
            try
            {
                off = LoopRuns(db, "off", walks, false, false);
                loop = LoopRuns(db, "loop", walks, true, false);
                both = LoopRuns(db, "loop+road", walks, true, true);
            }
            finally { LoopRestoreHours(db, was); }

            Line("");
            Line("  stage   rec.Pwr  band hi     off     loop  loop+road   (* = median over the band's top)");
            int overOff = 0, overLoop = 0, overBoth = 0, graded = 0;
            float maxOff = 0, maxLoop = 0, maxBoth = 0;
            string maxOffAt = "-", maxLoopAt = "-", maxBothAt = "-";
            for (int s = 0; s < db.AllStages.Count; s++)
            {
                StageDef st = db.AllStages[s];
                float lo, hi;
                bool banded = PowerBandFor(st, out lo, out hi);
                float a = LoopRatio(db, off.Runs, s), b = LoopRatio(db, loop.Runs, s), c = LoopRatio(db, both.Runs, s);
                if (banded)
                {
                    graded++;
                    if (a > hi) overOff++;
                    if (b > hi) overLoop++;
                    if (c > hi) overBoth++;
                    if (a > maxOff) { maxOff = a; maxOffAt = st.DisplayCode; }
                    if (b > maxLoop) { maxLoop = b; maxLoopAt = st.DisplayCode; }
                    if (c > maxBoth) { maxBoth = c; maxBothAt = st.DisplayCode; }
                }
                bool wall = st.IsMiniboss || st.IsBoss;
                if (!wall && st.Index != 1) continue;
                Line("  " + st.DisplayCode.PadRight(7) + st.RecommendedPower.ToString().PadLeft(7)
                     + (banded ? ("x" + Ratio3(hi)).PadLeft(9) : "-".PadLeft(9))
                     + LoopCell(a, banded && a > hi).PadLeft(9) + LoopCell(b, banded && b > hi).PadLeft(9)
                     + LoopCell(c, banded && c > hi).PadLeft(11)
                     + (wall ? "   WALL extra sittings " + StoreExtra(off.Runs, s) + " / " + StoreExtra(loop.Runs, s) + " / " + StoreExtra(both.Runs, s) : ""));
            }
            Line("");
            Line("  banded stages whose median entry is over the band's top, of " + graded + ": off " + overOff + ", loop " + overLoop + ", loop+road " + overBoth);
            Line("  highest median entry ratio: off x" + Ratio3(maxOff) + " at " + maxOffAt + ", loop x" + Ratio3(maxLoop) + " at " + maxLoopAt
                 + ", loop+road x" + Ratio3(maxBoth) + " at " + maxBothAt);
            Line("  stuck walks of " + walks + ": off " + StoreStuck(off.Runs) + ", loop " + StoreStuck(loop.Runs) + ", loop+road " + StoreStuck(both.Runs));
            Line("  total sittings (median): off " + StoreSittings(off.Runs) + ", loop " + StoreSittings(loop.Runs) + ", loop+road " + StoreSittings(both.Runs));
            foreach (LoopColumn col in new[] { loop, both })
            {
                double perDay = col.Days > 0 ? col.Hours / col.Days : 0;
                Line("  " + col.Name + ": " + col.Days + " reward days over " + walks + " walks, loop paid "
                     + perDay.ToString("0.00", CultureInfo.InvariantCulture) + " hours of frontier idle income a day ("
                     + (perDay * 7).ToString("0.0", CultureInfo.InvariantCulture) + " a week; one full idle chest a day at the "
                     + db.Balance.IdleCapHours.ToString("0", CultureInfo.InvariantCulture) + " hour cap is "
                     + (db.Balance.IdleCapHours * 7).ToString("0", CultureInfo.InvariantCulture) + " a week), "
                     + ((long)col.Gold).ToString("N0", CultureInfo.InvariantCulture) + " Gold in all");
            }
            Line("  claims, loop: " + loop.Claims);
            Line("  claims, loop+road: " + both.Claims + "; road points held at the end of the walks " + both.RoadPointsEnd);
            Line("  loop+road: " + both.RoadTiers + " free tiers claimed, " + both.RoadSelectors + " hero selectors, "
                 + both.RoadSeasons + " seasons finished, over " + walks + " walks");
            return 0;
        }

        private static string LoopCell(float ratio, bool over)
        {
            if (ratio <= 0) return "-";
            return "x" + Ratio3(ratio) + (over ? "*" : " ");
        }
    }
}
