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
    /// THE SPENDER'S CURVE (R19 task 4): the R16 economy walk twice, once as a non-spender and once as a player who
    /// buys the Starter Pact, the Weekly Pack, the Monthly Pack and the Ember Ledger whenever they are for sale and
    /// has the pass's whole paid track every season. Report only. It is a STUDY, not a check: it is run by name
    /// (--check StoreCurveStudy) and the gate never runs it.
    ///
    /// HOW THE SPENDER IS PLAYED. The walk calls _walkSpender once per sitting, after the sitting's idle claim and
    /// before its roster work, so purchased Gold is levelled into the line the same sitting. The player buys through
    /// the Store's own rules (StoreOffers.Fulfil, which refuses a sold-out offer by itself, so "whenever for sale"
    /// is exactly what the limits and resets allow), claims the Ledger's hours each reward day, and takes the pass
    /// as its paid track at full completion each season (the pass needs the daily loop, which the walk does not
    /// play, so the premium is applied as the grants of every paid tier through PurchaseGrants.Apply: an UPPER BOUND).
    ///
    /// THE CLOCK. The walk starts at a reward day aligned base (_walkClockBase) so Wayfarer's Road has a season and
    /// the Store's weeks and months exist. A base that is a whole number of days changes no difference of two
    /// times, so a non-spender at the base walks exactly as at 0; the study checks that and says so.
    ///
    /// ENV (all optional): EMBERFALL_STORE_WALKS (default 6), EMBERFALL_STORE_SCALE (pack hours multiplier, default 1),
    /// EMBERFALL_STORE_MODE (ref, fusing or both; default both), EMBERFALL_STORE_NOHERO (1: the spender takes no hero
    /// from the Starter Pact or the pass, to tell the heroes' effect from the Gold's), EMBERFALL_STORE_OUT (a file to
    /// write the report to).
    /// </summary>
    internal static partial class Program
    {
        private static Action<EconomyRun> _walkSpender;
        private static long _walkClockBase;
        private static bool _spenderNoHero;

        private sealed class SpendLog
        {
            public readonly Dictionary<string, int> Bought = new Dictionary<string, int>();
            public int PassSeasons;
            public int LedgerDays;
            public double Eur;
        }

        private sealed class CurveWalk
        {
            public EconomyRun Run;
            public SpendLog Spend = new SpendLog();
        }

        private static void StoreCurveStudy(ContentDatabase db, string contentDir)
        {
            int walks = StoreEnvInt("EMBERFALL_STORE_WALKS", 6);
            double scale = StoreEnvDouble("EMBERFALL_STORE_SCALE", 1.0);
            string outPath = Environment.GetEnvironmentVariable("EMBERFALL_STORE_OUT");
            string modeName = Environment.GetEnvironmentVariable("EMBERFALL_STORE_MODE") ?? "both";
            _spenderNoHero = Environment.GetEnvironmentVariable("EMBERFALL_STORE_NOHERO") == "1";
            long baseNow = (db.WayfarersRoad.EpochDay + 4L * db.WayfarersRoad.SeasonDays) * 86400L;
            StringBuilder report = new StringBuilder();
            Action<string> say = text => { Line(text); report.AppendLine(text); };

            bool storeWas = StoreSwitch.Enabled;
            Dictionary<string, double> hoursWas = new Dictionary<string, double>();
            try
            {
                StoreSwitch.Enabled = true;
                if (Math.Abs(scale - 1.0) > 1e-9) ScaleStorePacks(db, scale, hoursWas);

                say("STORE CURVE (R19 task 4): " + walks + " walks per player, seeds WalkSeed(0) to WalkSeed(" + (walks - 1) + "), pack scale x" + scale.ToString("0.###", CultureInfo.InvariantCulture));
                say("  clock base: reward day of season 5 (unix " + baseNow + "); sittings are 12 hours apart, as in every walk");
                for (int mode = 0; mode < 2; mode++)
                {
                    bool fusing = mode == 1;
                    if (modeName == "ref" && fusing || modeName == "fusing" && !fusing) continue;
                    say("");
                    say(fusing ? "== FUSING PLAYER (converts and ascends) ==" : "== REFERENCE PLAYER (never converts, never ascends: the player recommendedPower is derived for) ==");
                    List<CurveWalk> plain = new List<CurveWalk>(), plainAtBase = new List<CurveWalk>(), spender = new List<CurveWalk>();
                    for (int k = 0; k < walks; k++)
                    {
                        plain.Add(StoreCurveWalk(db, k, fusing, 0L, false));
                        plainAtBase.Add(StoreCurveWalk(db, k, fusing, baseNow, false));
                        spender.Add(StoreCurveWalk(db, k, fusing, baseNow, true));
                    }
                    int identical = 0;
                    for (int k = 0; k < walks; k++) if (SameWalk(plain[k].Run, plainAtBase[k].Run)) identical++;
                    say("  CONTROL: a non-spender at the clock base walks exactly as at 0 in " + identical + " of " + walks + " walks (stage by stage entry power and sittings)");
                    StoreCurveTables(db, plainAtBase, spender, say);
                    say("  NOISE FLOOR: the non-spender at the clock base against the non-spender at 0 (the same walks, only the calendar differs):");
                    StoreNoiseFloor(db, plain, plainAtBase, say);
                }
            }
            finally
            {
                RestoreStorePacks(db, hoursWas);
                StoreSwitch.Enabled = storeWas;
                _walkSpender = null;
                _walkClockBase = 0L;
            }
            if (!string.IsNullOrEmpty(outPath)) File.WriteAllText(outPath, report.ToString(), new UTF8Encoding(false));
        }

        // ------------------------------------------------------------------ one walk

        private static CurveWalk StoreCurveWalk(ContentDatabase db, int k, bool fusing, long baseNow, bool spends)
        {
            CurveWalk w = new CurveWalk();
            _walkClockBase = baseNow;
            _walkSpender = spends ? MakeSpender(db, w.Spend) : null;
            try { w.Run = SimulateEconomyUnder(db, WalkSeed(k), fusing, fusing); }
            finally { _walkSpender = null; _walkClockBase = 0L; }
            return w;
        }

        private static bool SameWalk(EconomyRun a, EconomyRun b)
        {
            if (a.Stages.Count != b.Stages.Count || a.StuckAt != b.StuckAt) return false;
            for (int i = 0; i < a.Stages.Count; i++)
            {
                if (a.Stages[i].EntryPower != b.Stages[i].EntryPower || a.Stages[i].Sessions != b.Stages[i].Sessions
                    || a.Stages[i].Losses != b.Stages[i].Losses) return false;
            }
            return true;
        }

        private static Action<EconomyRun> MakeSpender(ContentDatabase db, SpendLog log)
        {
            long lastSeason = -1;
            string hero = null;
            return run =>
            {
                GameSession s = run.Session;
                long now = run.Now;
                StoreOffers.Bind(s);
                StoreConfig cfg = db.Store;

                // The Starter Pact: the first of its three named heroes, once.
                StoreOfferDef starter = cfg.Offer("starter_pact");
                if (starter != null && hero == null)
                {
                    hero = starter.HeroChoices[0];
                    StoreSpend(s, log, cfg, "starter_pact", hero, now);
                    if (_spenderNoHero) ForgetSealedHero(s, hero);
                }

                // Pack, month and card whenever the rules allow it.
                StoreSpend(s, log, cfg, "weekly_pack", "", now);
                StoreSpend(s, log, cfg, "monthly_pack", "", now);
                StoreSpend(s, log, cfg, "ember_ledger", "", now);
                StoreFulfilment claim = StoreOffers.ClaimLedger(s, now);
                if (claim.Success) log.LedgerDays++;

                // The pass, each season: the premium's price and every paid tier of the track at full completion.
                if (WayfarersRoad.Active(s))
                {
                    long season = WayfarersRoad.SeasonOf(s, now);
                    if (season > 0 && season != lastSeason)
                    {
                        lastSeason = season;
                        StoreOfferDef pass = cfg.Offer(StoreOffers.PassOfferId);
                        if (pass != null) log.Eur += pass.PriceEur;
                        log.PassSeasons++;
                        List<PassRewardDef> paid = db.WayfarersRoad.Paid;
                        for (int i = 0; i < paid.Count; i++)
                        {
                            PurchaseGrant g = new PurchaseGrant();
                            string pick = null;
                            switch (paid[i].Kind)
                            {
                                case PassRewardKind.Hourglass: g.Add(new GrantItem(GrantKind.IdleGoldHours, paid[i].Amount)); break;
                                case PassRewardKind.Dust: g.Add(new GrantItem(GrantKind.DustSweeps, paid[i].Amount)); break;
                                case PassRewardKind.Slag: g.Add(new GrantItem(GrantKind.Slag, Math.Round(paid[i].Amount))); break;
                                case PassRewardKind.Selector:
                                    if (_spenderNoHero) continue;
                                {
                                    pick = hero ?? WayfarersRoad.SelectorHeroes(s, paid[i].Stars)[0].Id;
                                    GrantItem it = new GrantItem(GrantKind.HeroChoice, 0);
                                    it.HeroIds.Add(pick);
                                    g.Add(it);
                                    break;
                                }
                                default: continue;
                            }
                            PurchaseGrants.Apply(s, g, pick, now);
                        }
                    }
                }
            };
        }

        /// <summary>The NOHERO reading only: takes the Starter Pact's Sealed hero back out, so what is left is the Gold.</summary>
        private static void ForgetSealedHero(GameSession s, string heroId)
        {
            HeroInstance h = s.Profile.FindHeroByDefId(heroId);
            if (h == null) return;
            s.Profile.Heroes.Remove(h);
            for (int i = 0; i < s.Profile.Team.Length; i++) if (s.Profile.Team[i] == h.Uid) s.Profile.Team[i] = null;
            s.Profile.NormaliseTeam();
        }

        private static void StoreSpend(GameSession s, SpendLog log, StoreConfig cfg, string offerId, string choice, long now)
        {
            StoreOfferDef d = cfg.Offer(offerId);
            if (d == null) return;
            StoreFulfilment f = StoreOffers.Fulfil(s, offerId, choice, now);
            if (!f.Success) return;
            int n;
            log.Bought.TryGetValue(offerId, out n);
            log.Bought[offerId] = n + 1;
            log.Eur += d.PriceEur;
        }

        // ------------------------------------------------------------------ the tables

        private static void StoreCurveTables(ContentDatabase db, List<CurveWalk> plain, List<CurveWalk> spender, Action<string> say)
        {
            int stuckPlain = 0, stuckSpender = 0;
            for (int k = 0; k < plain.Count; k++)
            {
                if (plain[k].Run.StuckAt >= 0) stuckPlain++;
                if (spender[k].Run.StuckAt >= 0) stuckSpender++;
            }
            say("  walks stuck before the end: non-spender " + stuckPlain + " of " + plain.Count + ", spender " + stuckSpender + " of " + spender.Count);

            say("");
            say("  Entry power over recommendedPower at the first attempt of the wall stages (median over the walks that got there).");
            say("  stage    non-spender  spender   ratio shift   sittings (non / spender)   losses (non / spender)   wall still binds?");
            int broken = 0;
            for (int chapter = 1; chapter <= 6; chapter++)
            {
                foreach (int idx in new[] { 6, 12 })
                {
                    string code = chapter + "-" + idx;
                    int g = StageGlobalIndex(db, code);
                    if (g < 0) continue;
                    List<double> rn = new List<double>(), rs = new List<double>();
                    List<int> sn = new List<int>(), ss = new List<int>(), ln = new List<int>(), ls = new List<int>();
                    for (int k = 0; k < plain.Count; k++)
                    {
                        if (g < plain[k].Run.Stages.Count)
                        {
                            EconomyStageOutcome o = plain[k].Run.Stages[g];
                            rn.Add(o.EntryPower / (double)Math.Max(1, o.Stage.RecommendedPower)); sn.Add(o.Sessions); ln.Add(o.Losses);
                        }
                        if (g < spender[k].Run.Stages.Count)
                        {
                            EconomyStageOutcome o = spender[k].Run.Stages[g];
                            rs.Add(o.EntryPower / (double)Math.Max(1, o.Stage.RecommendedPower)); ss.Add(o.Sessions); ls.Add(o.Losses);
                        }
                    }
                    if (rn.Count == 0 || rs.Count == 0) { say("  " + code.PadRight(8) + " not reached by every player"); continue; }
                    double mn = MedianD(rn), ms = MedianD(rs);
                    int msn = Median(sn), mss = Median(ss), mln = Median(ln), mls = Median(ls);
                    bool plainBinds = msn > 1 || mln > 0, spenderBinds = mss > 1 || mls > 0;
                    string verdict = !plainBinds ? "no wall for the non-spender either" : spenderBinds ? "binds" : "BROKEN (the spender walks through)";
                    if (plainBinds && !spenderBinds) broken++;
                    say("  " + code.PadRight(8) + " x" + mn.ToString("0.000", CultureInfo.InvariantCulture).PadRight(10) + " x" + ms.ToString("0.000", CultureInfo.InvariantCulture).PadRight(8)
                        + (ms - mn >= 0 ? "+" : "") + (ms - mn).ToString("0.000", CultureInfo.InvariantCulture).PadRight(11)
                        + (msn + " / " + mss).PadRight(26) + (mln + " / " + mls).PadRight(25) + verdict);
                }
            }
            say("  walls broken by the spender: " + broken);

            say("");
            say("  Days to clear each chapter (median) and what the spender saved.");
            say("  chapter  non-spender days  spender days  saved");
            for (int chapter = 1; chapter <= 6; chapter++)
            {
                List<double> dn = new List<double>(), ds = new List<double>();
                for (int k = 0; k < plain.Count; k++)
                {
                    double a = ChapterDays(db, plain[k].Run, chapter), b = ChapterDays(db, spender[k].Run, chapter);
                    if (a >= 0) dn.Add(a);
                    if (b >= 0) ds.Add(b);
                }
                if (dn.Count == 0 || ds.Count == 0) { say("  " + chapter + "        not reached by every player"); continue; }
                double mdn = MedianD(dn), mds = MedianD(ds);
                say("  " + chapter + "        " + mdn.ToString("0.0", CultureInfo.InvariantCulture).PadRight(17) + mds.ToString("0.0", CultureInfo.InvariantCulture).PadRight(14)
                    + (mdn - mds).ToString("0.0", CultureInfo.InvariantCulture) + " days");
            }

            say("");
            double eur = 0; int pass = 0, ledgerDays = 0;
            Dictionary<string, int> bought = new Dictionary<string, int>();
            for (int k = 0; k < spender.Count; k++)
            {
                eur += spender[k].Spend.Eur; pass += spender[k].Spend.PassSeasons; ledgerDays += spender[k].Spend.LedgerDays;
                foreach (KeyValuePair<string, int> kv in spender[k].Spend.Bought)
                {
                    int n; bought.TryGetValue(kv.Key, out n); bought[kv.Key] = n + kv.Value;
                }
            }
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, int> kv in bought) parts.Add(kv.Key + " " + (kv.Value / (double)spender.Count).ToString("0.0", CultureInfo.InvariantCulture));
            say("  A spender buys, per walk on average: " + string.Join(", ", parts.ToArray()) + ", pass seasons " + (pass / (double)spender.Count).ToString("0.0", CultureInfo.InvariantCulture)
                + ", ledger days claimed " + (ledgerDays / (double)spender.Count).ToString("0.0", CultureInfo.InvariantCulture)
                + "; placeholder euros per walk " + (eur / spender.Count).ToString("0", CultureInfo.InvariantCulture));
        }

        private static void StoreNoiseFloor(ContentDatabase db, List<CurveWalk> at0, List<CurveWalk> atBase, Action<string> say)
        {
            List<string> cells = new List<string>();
            for (int chapter = 1; chapter <= 6; chapter++)
            {
                foreach (int idx in new[] { 6, 12 })
                {
                    int g = StageGlobalIndex(db, chapter + "-" + idx);
                    List<double> a = new List<double>(), b = new List<double>();
                    for (int k = 0; k < at0.Count; k++)
                    {
                        if (g < at0[k].Run.Stages.Count) a.Add(at0[k].Run.Stages[g].EntryPower / (double)Math.Max(1, at0[k].Run.Stages[g].Stage.RecommendedPower));
                        if (g < atBase[k].Run.Stages.Count) b.Add(atBase[k].Run.Stages[g].EntryPower / (double)Math.Max(1, atBase[k].Run.Stages[g].Stage.RecommendedPower));
                    }
                    if (a.Count == 0 || b.Count == 0) continue;
                    cells.Add(chapter + "-" + idx + " " + (MedianD(b) - MedianD(a)).ToString("+0.000;-0.000", CultureInfo.InvariantCulture));
                }
            }
            say("    median shift of the entry ratio: " + string.Join(", ", cells.ToArray()));
        }

        private static int StageGlobalIndex(ContentDatabase db, string code)
        {
            for (int i = 0; i < db.AllStages.Count; i++) if (db.AllStages[i].DisplayCode == code) return i;
            return -1;
        }

        /// <summary>Days from the start of the walk to the clearing of the chapter's last stage, or -1 if the walk never got there.</summary>
        private static double ChapterDays(ContentDatabase db, EconomyRun run, int chapter)
        {
            long elapsed = 0;
            bool reached = false;
            for (int i = 0; i < run.Stages.Count; i++)
            {
                elapsed += run.Stages[i].ElapsedSeconds;
                if (run.Stages[i].Stage.ChapterNumber == chapter && run.Stages[i].Cleared
                    && (i + 1 >= db.AllStages.Count || db.AllStages[i + 1].ChapterNumber != chapter)) { reached = true; break; }
            }
            return reached ? elapsed / 86400.0 : -1.0;
        }

        private static double MedianD(List<double> v)
        {
            List<double> s = new List<double>(v);
            s.Sort();
            return s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2.0;
        }

        // ------------------------------------------------------------------ pack scaling (restored after)

        private static void ScaleStorePacks(ContentDatabase db, double scale, Dictionary<string, double> hoursWas)
        {
            for (int i = 0; i < db.Store.Offers.Count; i++)
            {
                StoreOfferDef d = db.Store.Offers[i];
                if (d.Kind != StoreOfferKind.Pack && d.Kind != StoreOfferKind.Starter) continue;
                hoursWas[d.Id] = d.Hours;
                hoursWas[d.Id + "|sweeps"] = d.DustSweeps;
                d.Hours = d.Hours * scale;
                d.DustSweeps = d.DustSweeps * scale;
                StoreConfig.Build(d);
            }
        }

        private static void RestoreStorePacks(ContentDatabase db, Dictionary<string, double> hoursWas)
        {
            if (hoursWas.Count == 0) return;
            for (int i = 0; i < db.Store.Offers.Count; i++)
            {
                StoreOfferDef d = db.Store.Offers[i];
                double h, s;
                if (!hoursWas.TryGetValue(d.Id, out h)) continue;
                d.Hours = h;
                if (hoursWas.TryGetValue(d.Id + "|sweeps", out s)) d.DustSweeps = s;
                StoreConfig.Build(d);
            }
        }

        private static int StoreEnvInt(string name, int fallback)
        {
            int v;
            return int.TryParse(Environment.GetEnvironmentVariable(name), out v) && v > 0 ? v : fallback;
        }

        private static double StoreEnvDouble(string name, double fallback)
        {
            double v;
            return double.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v > 0 ? v : fallback;
        }
    }
}
