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
            if (_walkStoreMode == 0) return 0L;
            return PassSeasonStart(db, 5) + 20L * 3600L;
        }

        private static readonly string[] SpenderOrder = { "starter_pact", "growth_fund", "ember_ledger", "weekly_pack", "monthly_pack", "pass_premium" };

        /// <summary>One sitting's shopping: claim what was bought, buy what the periods have reopened, fill the
        /// road and claim the paid track. Called by the walk before it claims the idle chest.</summary>
        private static void StoreSpenderStep(ContentDatabase db, GameSession s, long now)
        {
            if (_walkStoreMode < 2) return;
            bool storeWas = StoreSwitch.Enabled, loopWas = DailyLoopSwitch.Enabled, roadWas = WayfarersRoadSwitch.Enabled, contractWas = HeroContractSwitch.Enabled;
            try
            {
                StoreSwitch.Enabled = true;
                DailyLoopSwitch.Enabled = true;
                WayfarersRoadSwitch.Enabled = true;
                HeroContractSwitch.Enabled = _walkStoreMode == 3;
                StoreOffers.ClaimLedger(s, now);
                StoreOfferDef growth = db.Store.Offer("growth_fund");
                for (int i = 0; growth != null && i < growth.Milestones.Count; i++) StoreOffers.ClaimGrowth(s, i, now);

                for (int k = 0; k < SpenderOrder.Length; k++)
                {
                    string id = SpenderOrder[k];
                    string choice = id == "starter_pact" ? db.Store.Offer(id).HeroChoices[0] : null;
                    if (StoreOffers.Check(s, id, choice, now) == StoreRefusal.None) StoreOffers.Fulfil(s, id, choice, now);
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
            int was = _walkStoreMode;
            _walkStoreMode = mode;
            try
            {
                List<EconomyRun> runs = new List<EconomyRun>();
                for (int k = 0; k < walks; k++) runs.Add(SimulateEconomyUnder(db, WalkSeed(k), converts, ascends));
                return runs;
            }
            finally { _walkStoreMode = was; }
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
    }
}
