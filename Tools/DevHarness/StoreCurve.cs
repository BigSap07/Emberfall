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
    /// THE CURVE CHECK FOR THE STORE (R19 task 4, Docs/DESIGN-STORE.md): the R16 reference walk, once as a
    /// non-spender and once as a player who buys the Starter Pact, the Growth Fund, the Ember Ledger, the
    /// Weekly Pack, the Monthly Pack and the pass premium every period, claims everything they bought and
    /// puts every Sealed 5-star copy into one hero. Run by DevHarness.exe --store-curve [walks]; a
    /// measurement, not a gate, and inert in every other run (_walkStoreMode is 0).
    ///
    /// The spender buys through StoreOffers (the harness's direct fulfilment), so every good is granted by
    /// PurchaseGrants.Apply and marked Sealed exactly as a real purchase is. The walk's clock starts at a
    /// Monday reward day in the season calendar, so weeks, months and seasons are real; the start is
    /// 20 hours into a reward day because the walk's own clock starts 20 hours into one (unix 0), which
    /// keeps the walk's repeat-clear days aligned. CHECKED, not assumed: the clock-only mode runs the same
    /// walk at that start with no purchases, and its numbers must equal the plain walk's.
    ///
    /// The pass premium is walked as a diligent player's: the road is filled each season (the walk plays
    /// no daily orders) and every paid tier is claimed, so the premium's Gold and its Sealed 5-star copy
    /// land once a season.
    ///
    /// REWARDED ADS (Docs/DESIGN-REWARDED-ADS.md): --store-ads lean|light|full|heavy replaces the spender columns with an
    /// AD WATCHER on the same calendar clock: the non-spender who also watches every rewarded ad the variant allows
    /// in a reward day. The ad rewards go through PurchaseGrants.Apply as fixed lines (Gold, Dust sweeps), so they
    /// are Sealed exactly as the design proposes, and nothing in the step reads a random number.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>0 = the plain walk. 1 = the walk on the season calendar's clock with no purchase (the control).
        /// 2 = the spender. 3 = the spender who also buys the Hero Contract (its define is OFF in the build; the
        /// walk turns it on for itself to measure what turning it on would do).</summary>
        private static int _walkStoreMode = 0;

        private static ContentDatabase _walkStoreDb;

        /// <summary>--store-contracts also walks the spender who buys the Hero Contract (a fifth column).</summary>
        private static bool _storeContracts;

        private static long StoreWalkClockStart(ContentDatabase db)
        {
            if (_walkStoreMode == 0 && _walkLoopMode == 0) return 0L;   // the daily loop curve (DailyLoopCurve.cs) walks this clock too
            return PassSeasonStart(db, 5) + 20L * 3600L;
        }

        private static readonly string[] SpenderOrder = { "starter_pact", "growth_fund", "ember_ledger", "weekly_pack", "monthly_pack", "pass_premium" };

        /// <summary>One sitting's shopping: claim what was bought, buy what the periods have reopened, fill the
        /// road and claim the paid track. Called by the walk before it claims the idle chest.</summary>
        private static void StoreSpenderStep(ContentDatabase db, GameSession s, long now)
        {
            if (_walkStoreMode < 1) return;
            // the control buys nothing; the whale study (R24WhaleCurve.cs) still lets it spend its own Dust on relics
            WhaleFreeEvents(db, s, now);
            if (_walkStoreMode < 2) { WhaleRelicStep(db, s, now); return; }
            bool storeWas = StoreSwitch.Enabled, loopWas = DailyLoopSwitch.Enabled, roadWas = WayfarersRoadSwitch.Enabled, contractWas = HeroContractSwitch.Enabled;
            try
            {
                StoreSwitch.Enabled = true;
                DailyLoopSwitch.Enabled = true;
                WayfarersRoadSwitch.Enabled = true;
                HeroContractSwitch.Enabled = _walkStoreMode >= 3;
                long dustBefore = s.Profile.RelicDust;
                StoreOffers.ClaimLedger(s, now);
                StoreOfferDef growth = db.Store.Offer("growth_fund");
                for (int i = 0; growth != null && i < growth.Milestones.Count; i++) StoreOffers.ClaimGrowth(s, i, now);

                for (int k = 0; k < SpenderOrder.Length; k++)
                {
                    string id = SpenderOrder[k];
                    string choice = id == "starter_pact" ? db.Store.Offer(id).HeroChoices[0] : null;
                    if (StoreOffers.Check(s, id, choice, now) == StoreRefusal.None && StoreOffers.Fulfil(s, id, choice, now).Success) WhaleNoteBought(db, id, now);
                }

                if (_walkStoreMode == 3)
                {
                    // two contracts a month for the hero the spender concentrates on (limit 2 per hero per month)
                    string target = StoreBestSealedHero(s, 5);
                    for (int c = 0; c < 2 && target != null && s.Profile.FindHeroByDefId(target) != null; c++)
                        if (StoreOffers.Check(s, "hero_contract", target, now) == StoreRefusal.None) StoreOffers.Fulfil(s, "hero_contract", target, now);
                }

                if (StoreOffers.PassOwned(s, now))
                {
                    s.Profile.WayfarersRoad.Points = WayfarersRoad.TotalPoints(s);
                    WayfarersRoadConfig road = db.WayfarersRoad;
                    for (int i = 0; i < road.Paid.Count; i++)
                    {
                        PassRewardDef p = road.Paid[i];
                        string hero = p.Kind == PassRewardKind.Selector ? StoreBestSealedHero(s, p.Stars) : null;
                        WayfarersRoad.ClaimPaid(s, p.Tier, hero, now);
                    }
                }

                // R24 whale study: the mode-4 buyer's extra shopping, the Dust every purchase put in the purse, and the relic policy
                if (_walkStoreMode == 4) WhaleStep(db, s, now);
                WhaleNoteDust(now, s.Profile.RelicDust - dustBefore);
                WhaleRelicStep(db, s, now);
            }
            finally
            {
                StoreSwitch.Enabled = storeWas;
                DailyLoopSwitch.Enabled = loopWas;
                WayfarersRoadSwitch.Enabled = roadWas;
                HeroContractSwitch.Enabled = contractWas;
            }
        }

        /// <summary>The selector hero the spender concentrates copies on: the one with the most Sealed copies, else the first listed.</summary>
        private static string StoreBestSealedHero(GameSession s, int stars)
        {
            List<HeroDef> pool = WayfarersRoad.SelectorHeroes(s, stars);
            string best = pool.Count > 0 ? pool[0].Id : null;
            int most = -1;
            for (int i = 0; i < pool.Count; i++)
            {
                HeroInstance h = s.Profile.FindHeroByDefId(pool[i].Id);
                int sealedCopies = h == null ? 0 : h.SealedCopiesHeld;
                if (sealedCopies > most) { most = sealedCopies; best = pool[i].Id; }
            }
            return best;
        }

        private static List<EconomyRun> StoreCurveRuns(ContentDatabase db, int walks, bool converts, bool ascends, int mode)
        {
            return StoreCurveRuns(db, walks, converts, ascends, mode, false);
        }

        private static List<EconomyRun> StoreCurveRuns(ContentDatabase db, int walks, bool converts, bool ascends, int mode, bool ads)
        {
            int was = _walkStoreMode;
            bool adsWas = _walkAds;
            _walkStoreMode = mode;
            _walkAds = ads;
            try
            {
                List<EconomyRun> runs = new List<EconomyRun>();
                for (int k = 0; k < walks; k++) runs.Add(SimulateEconomyUnder(db, WalkSeed(k), converts, ascends));
                return runs;
            }
            finally { _walkStoreMode = was; _walkAds = adsWas; }
        }

        // ------------------------------------------------------------------ the ad watcher

        /// <summary>--store-ads: the variant the ad watcher columns walk (lean, light, full or heavy), null when not asked.</summary>
        private static string _storeAds;

        /// <summary>True only inside an ad watcher column; every other walk is untouched.</summary>
        private static bool _walkAds;

        /// <summary>One walk's ad counters for its current reward day, and the totals the report reads.</summary>
        private sealed class AdTally
        {
            public long Day = long.MinValue;
            public long FirstSeen = -1;
            public int IdleToday, SweepsToday;
            public long LastFullSweepGold;
            public long Gold, Dust;
            public int Watched;
        }

        private static readonly Dictionary<GameSession, AdTally> _adTallies = new Dictionary<GameSession, AdTally>();

        /// <summary>Double-idle ads a reward day allows: lean, light and full 1, heavy 2.</summary>
        private static int AdIdlePerDay { get { return _storeAds == "heavy" ? 2 : 1; } }

        /// <summary>The most one double-idle ad may add, in hours of the watcher's own idle Gold. Lean caps it at 2 and
        /// light at 4; full and heavy pay the whole claim again (the cap is the game's own idle cap).</summary>
        private static double AdIdleCapHours { get { return _storeAds == "lean" ? 2.0 : _storeAds == "light" ? 4.0 : 0.0; } }

        /// <summary>Extra full-pay repeat clears a reward day allows: lean, light and full 1, heavy 2.</summary>
        private static int AdSweepsPerDay { get { return _storeAds == "heavy" ? 2 : 1; } }

        private static AdTally AdTallyOf(GameSession s, long now)
        {
            AdTally t;
            if (!_adTallies.TryGetValue(s, out t)) { t = new AdTally(); t.FirstSeen = now; _adTallies[s] = t; }
            long day = s.DayStartUnix(now);
            if (day != t.Day) { t.Day = day; t.IdleToday = 0; t.SweepsToday = 0; }
            return t;
        }

        /// <summary>Called by the walk right after its idle claim. Ad A1: the same Gold again, Sealed, once (lean, light,
        /// full) or twice (heavy) a reward day; lean caps the bonus at 2 hours of the watcher's idle Gold and light at 4.</summary>
        private static void AdWatcherIdleStep(GameSession s, IdleReport idle, long now)
        {
            if (!_walkAds || idle == null || idle.Gold <= 0) return;
            AdTally t = AdTallyOf(s, now);
            if (t.IdleToday >= AdIdlePerDay) return;
            long bonus = idle.Gold;
            if (AdIdleCapHours > 0.0) bonus = Math.Min(bonus, s.IdleYield((long)Math.Round(AdIdleCapHours * 3600.0)).Gold);
            if (bonus <= 0) return;
            PurchaseGrant grant = new PurchaseGrant().Add(new GrantItem(GrantKind.Gold, bonus));
            if (!PurchaseGrants.Apply(s, grant, null, now).Success) return;
            t.IdleToday++;
            t.Watched++;
            t.Gold += bonus;
        }

        /// <summary>Called by the walk after a walled sitting's repeat clears. Ad A3: one more full-pay repeat clear of
        /// the farmed stage (lean, light, full; heavy two), paid as what the last full clear of it paid in Gold, Sealed,
        /// plus one Dust sweep. lastFullGold is this sitting's last paid clear, 0 when the day's clears were used up.</summary>
        private static void AdWatcherSweepStep(GameSession s, long lastFullGold, long now)
        {
            if (!_walkAds) return;
            AdTally t = AdTallyOf(s, now);
            if (lastFullGold > 0) t.LastFullSweepGold = lastFullGold;
            while (t.SweepsToday < AdSweepsPerDay && t.LastFullSweepGold > 0)
            {
                PurchaseGrant grant = new PurchaseGrant()
                    .Add(new GrantItem(GrantKind.Gold, t.LastFullSweepGold))
                    .Add(new GrantItem(GrantKind.DustSweeps, 1));
                GrantResult r = PurchaseGrants.Apply(s, grant, null, now);
                if (!r.Success) return;
                t.SweepsToday++;
                t.Watched++;
                t.Gold += r.Gold;
                t.Dust += r.Dust;
            }
        }

        /// <summary>The median, over walks, of the ad watcher's ad Gold as a percent of all the Gold that walk earned.</summary>
        private static string AdGoldShare(List<EconomyRun> runs)
        {
            List<int> share = new List<int>();
            List<int> perDay = new List<int>();
            for (int k = 0; k < runs.Count; k++)
            {
                AdTally t;
                if (!_adTallies.TryGetValue(runs[k].Session, out t)) continue;
                long total = runs[k].Ledger.GoldTotal + t.Gold;
                share.Add(total > 0 ? (int)Math.Round(1000.0 * t.Gold / total) : 0);
                double days = Math.Max(1.0, (runs[k].Now - t.FirstSeen) / 86400.0);
                perDay.Add((int)Math.Round(10.0 * t.Watched / days));
            }
            if (share.Count == 0) return "-";
            return (Median(share) / 10.0).ToString("0.0", CultureInfo.InvariantCulture) + " percent of all Gold, "
                   + (Median(perDay) / 10.0).ToString("0.0", CultureInfo.InvariantCulture) + " ads a day (median over walks)";
        }

        private static int StoreMedianEntry(List<EconomyRun> runs, int stageIndex)
        {
            List<int> entry = new List<int>();
            for (int k = 0; k < runs.Count; k++)
                if (stageIndex < runs[k].Stages.Count) entry.Add(runs[k].Stages[stageIndex].EntryPower);
            return entry.Count == 0 ? 0 : Median(entry);
        }

        /// <summary>The median extra sittings a stage cost beyond its own ladder floor (WallExtraSittings): what a wall costs.</summary>
        private static string StoreExtra(List<EconomyRun> runs, int stageIndex)
        {
            List<int> extra = new List<int>();
            for (int k = 0; k < runs.Count; k++)
            {
                if (stageIndex >= runs[k].Stages.Count) continue;
                int ladder, floor;
                extra.Add(WallExtraSittings(runs[k].Stages[stageIndex], out ladder, out floor));
            }
            return extra.Count == 0 ? "-" : Median(extra).ToString();
        }

        private static int StoreStuck(List<EconomyRun> runs)
        {
            int stuck = 0;
            for (int k = 0; k < runs.Count; k++) if (runs[k].StuckAt >= 0) stuck++;
            return stuck;
        }

        private static string StoreRatio(ContentDatabase db, List<EconomyRun> runs, int stageIndex)
        {
            StageDef st = db.AllStages[stageIndex];
            int med = StoreMedianEntry(runs, stageIndex);
            return st.RecommendedPower > 0 && med > 0 ? "x" + Ratio3(med / (float)st.RecommendedPower) : "-";
        }

        /// <summary>DevHarness.exe --store-curve [walks]: the reference player and the fusing player, each as a non-spender and as the spender.</summary>
        private static int RunStoreCurve(string contentDir, int walks)
        {
            Console.OutputEncoding = Encoding.UTF8;
            ContentDatabase db = LoadContent(contentDir);
            if (db == null) return 1;
            _walkStoreDb = db;
            if (_storeAds != null) return RunAdsCurve(db, walks);
            Line("");
            Line("STORE CURVE - " + walks + " walks per column. Median entry power over recommendedPower. Reference = never converts, never ascends. Fusing = does both.");
            Line("  spender buys: " + string.Join(", ", SpenderOrder) + " as each period reopens; the walk's clock starts on the season calendar.");

            // The non-spender is the control on the same calendar clock, so the only difference between a column
            // and its spender is the purchases. (The plain walk on unix 0 reads within about one percent of it:
            // the armor drops are timed by the clock.)
            List<EconomyRun> refNon = StoreCurveRuns(db, walks, false, false, 1);
            List<EconomyRun> refSpend = StoreCurveRuns(db, walks, false, false, 2);
            List<EconomyRun> fuseNon = StoreCurveRuns(db, walks, true, true, 1);
            List<EconomyRun> fuseSpend = StoreCurveRuns(db, walks, true, true, 2);
            List<EconomyRun> fuseContract = _storeContracts ? StoreCurveRuns(db, walks, true, true, 3) : null;

            Line("");
            Line("  stage   rec.Pwr  ref non   ref spender   fusing non   fusing spender" + (fuseContract != null ? "   + contracts" : "") + "   extra sittings, median (ref non / ref spender / fusing non / fusing spender)");
            for (int s = 0; s < db.AllStages.Count; s++)
            {
                StageDef st = db.AllStages[s];
                bool wall = st.IsMiniboss || st.IsBoss;
                if (!wall && st.Index != 1) continue;
                Line("  " + st.DisplayCode.PadRight(7) + st.RecommendedPower.ToString().PadLeft(7)
                     + StoreRatio(db, refNon, s).PadLeft(10) + StoreRatio(db, refSpend, s).PadLeft(14)
                     + StoreRatio(db, fuseNon, s).PadLeft(13) + StoreRatio(db, fuseSpend, s).PadLeft(17)
                     + (fuseContract != null ? StoreRatio(db, fuseContract, s).PadLeft(14) : "")
                     + (wall ? "   WALL   " + StoreExtra(refNon, s) + " / " + StoreExtra(refSpend, s) + " / " + StoreExtra(fuseNon, s) + " / " + StoreExtra(fuseSpend, s) : ""));
            }
            Line("");
            Line("  stuck walks of " + walks + ": reference non " + StoreStuck(refNon) + ", reference spender " + StoreStuck(refSpend)
                 + ", fusing non " + StoreStuck(fuseNon) + ", fusing spender " + StoreStuck(fuseSpend));
            Line("  total sittings (median over walks): reference non " + StoreSittings(refNon) + ", spender " + StoreSittings(refSpend)
                 + "; fusing non " + StoreSittings(fuseNon) + ", spender " + StoreSittings(fuseSpend));
            return 0;
        }

        private static int StoreSittings(List<EconomyRun> runs)
        {
            List<int> v = new List<int>();
            for (int k = 0; k < runs.Count; k++) v.Add(runs[k].TotalSessions);
            return Median(v);
        }

        /// <summary>DevHarness.exe --store-curve [walks] --store-ads lean|light|full|heavy: the reference and the fusing
        /// non-spender, each without and with the ad watcher of that variant, on the same calendar clock.</summary>
        private static int RunAdsCurve(ContentDatabase db, int walks)
        {
            if (_storeAds != "lean" && _storeAds != "light" && _storeAds != "full" && _storeAds != "heavy")
            {
                Line("--store-ads takes lean, light, full or heavy, not " + _storeAds);
                return 1;
            }
            Line("");
            Line("ADS CURVE (" + _storeAds + ") - " + walks + " walks per column. Median entry power over recommendedPower. Reference = never converts, never ascends. Fusing = does both.");
            Line("  ad watcher: " + AdIdlePerDay + " double-idle ad(s) a reward day"
                 + (AdIdleCapHours > 0.0 ? " (bonus capped at " + AdIdleCapHours.ToString("0", CultureInfo.InvariantCulture) + " hours of idle Gold)" : " (the whole claim again)")
                 + ", " + AdSweepsPerDay + " extra full-pay repeat clear(s) a reward day while walled. Every ad reward is Sealed, through PurchaseGrants.Apply.");

            _adTallies.Clear();
            List<EconomyRun> refNon = StoreCurveRuns(db, walks, false, false, 1, false);
            List<EconomyRun> refAds = StoreCurveRuns(db, walks, false, false, 1, true);
            List<EconomyRun> fuseNon = StoreCurveRuns(db, walks, true, true, 1, false);
            List<EconomyRun> fuseAds = StoreCurveRuns(db, walks, true, true, 1, true);

            Line("");
            Line("  stage   rec.Pwr  ref non   ref + ads   fusing non   fusing + ads   extra sittings, median (ref non / ref + ads / fusing non / fusing + ads)");
            for (int s = 0; s < db.AllStages.Count; s++)
            {
                StageDef st = db.AllStages[s];
                bool wall = st.IsMiniboss || st.IsBoss;
                if (!wall && st.Index != 1) continue;
                Line("  " + st.DisplayCode.PadRight(7) + st.RecommendedPower.ToString().PadLeft(7)
                     + StoreRatio(db, refNon, s).PadLeft(10) + StoreRatio(db, refAds, s).PadLeft(12)
                     + StoreRatio(db, fuseNon, s).PadLeft(13) + StoreRatio(db, fuseAds, s).PadLeft(15)
                     + (wall ? "   WALL   " + StoreExtra(refNon, s) + " / " + StoreExtra(refAds, s) + " / " + StoreExtra(fuseNon, s) + " / " + StoreExtra(fuseAds, s) : ""));
            }
            Line("");
            Line("  stuck walks of " + walks + ": reference non " + StoreStuck(refNon) + ", reference + ads " + StoreStuck(refAds)
                 + ", fusing non " + StoreStuck(fuseNon) + ", fusing + ads " + StoreStuck(fuseAds));
            Line("  total sittings (median over walks): reference non " + StoreSittings(refNon) + ", + ads " + StoreSittings(refAds)
                 + "; fusing non " + StoreSittings(fuseNon) + ", + ads " + StoreSittings(fuseAds));
            Line("  ad income: reference " + AdGoldShare(refAds) + "; fusing " + AdGoldShare(fuseAds));
            return 0;
        }
    }
}
