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
    /// THE WHALE STUDY (R24 task 5, report only, never part of the gate). Three instruments on the R16 reference walk:
    ///   --whale-dust [walks]       the Relic Dust income per day at each chapter frontier (WhaleDustStudy).
    ///   --whale-curve [walks]      one COLUMN of the whale curve, written to a TSV (WhaleCurveColumn), selected by
    ///                              --whale-col player:buyer (player ref or fuse; buyer non, spend or whale),
    ///                              --whale-range a b (the walks a to b - 1, so a column can run in several processes),
    ///                              --whale-out file.
    ///   --whale-report dir         reads every TSV in dir and prints the curve per chapter (WhaleReport).
    /// A column is a process of its own because the store switches are process-wide and a spender walk cannot share
    /// them with another (SimulateEconomies runs the spender serially for that reason).
    ///
    /// THE BUYERS. non = the control on the season calendar's clock (StoreCurve.cs mode 1). spend = the R19 spender
    /// (mode 2). whale = the spender plus everything R24 adds (mode 4, WhaleStep): the Hero Contract with its cap,
    /// the relic packs, the paid Event Tracks, the event packs and a skin for every hero who reaches the line.
    /// The R24 offers named in "PLACEHOLDER" below are read from this file until the builders' store.json entries
    /// land; the report prints which contents it ran on.
    ///
    /// RELICS. The R16 reference walk owns no relic, so a relic pack could not move it. With relics on (default,
    /// --whale-no-relics turns it off) EVERY buyer spends the Relic Dust they hold by the same policy
    /// (WhaleRelicStep: fill the line with Legendary relics, then raise them tier by tier, then buy the Mythics),
    /// so the lead the packs buy shows in EffectiveTeamPower and in every fight. --whale-committed tops every buyer's
    /// own Dust up to the committed player's (12 stages x 3 sweeps a day, R17) instead of the walk's own floor
    /// (3 sweeps of one stage), because the relic prices were sized on the committed player.
    ///
    /// SKINS. The skin system lands in builder-b's branch; until it does a skin is modelled as what it is, +2 percent
    /// Attack and +3 percent HP on that hero, by scaling the HeroDef's BaseAttack and BaseHp for the rest of the
    /// walk (and put back at its end). Combat and power read exactly what a skinned hero would read.
    /// </summary>
    internal static partial class Program
    {
        private static bool WhaleRequested;
        private static string WhaleMode = "";
        private static int WhaleWalks = 20;
        private static int WhaleFrom = 0, WhaleTo = -1;
        private static string WhaleCol = "ref:non";
        private static string WhaleOut = null, WhaleDir = null;
        private static bool _whaleRelics = true, _whaleCommitted = false, _whaleFreeEvents = true, _whaleChoiceMax = true;
        private static int _whaleContractFrom = 37;
        /// <summary>As the store has it now (lead's default, log 09:14; builder-b c8d987c): the Chest comes 2 a month instead of 1 a week.</summary>
        private static bool _whaleChestMonthly = true;
        /// <summary>The most Dust a Relic of Choice pick may be worth (builder-b: the picker is capped at 20,000).</summary>
        private static int _whaleChoiceCap = 20000;
        private static readonly HashSet<string> _whaleSkip = new HashSet<string>();
        private static readonly Dictionary<string, double> _whaleScale = new Dictionary<string, double>();

        /// <summary>Main's loop hands each argument here first; true means it was a whale flag and has been consumed.</summary>
        private static bool WhaleTakeArg(string[] args, ref int i)
        {
            string a = args[i];
            if (a == "--whale-dust" || a == "--whale-curve")
            {
                WhaleRequested = true;
                WhaleMode = a.Substring("--whale-".Length);
                int n;
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out n)) { WhaleWalks = n; i++; }
                return true;
            }
            if (a == "--whale-report" && i + 1 < args.Length) { WhaleRequested = true; WhaleMode = "report"; WhaleDir = args[++i]; return true; }
            if (a == "--whale-col" && i + 1 < args.Length) { WhaleCol = args[++i]; return true; }
            if (a == "--whale-out" && i + 1 < args.Length) { WhaleOut = args[++i]; return true; }
            if (a == "--whale-range" && i + 2 < args.Length) { WhaleFrom = int.Parse(args[++i]); WhaleTo = int.Parse(args[++i]); return true; }
            if (a == "--whale-no-relics") { _whaleRelics = false; return true; }
            if (a == "--whale-committed") { _whaleCommitted = true; return true; }
            if (a == "--whale-weekly-chest") { _whaleChestMonthly = false; return true; }
            if (a == "--whale-choice-cap" && i + 1 < args.Length) { _whaleChoiceCap = int.Parse(args[++i]); return true; }
            if (a == "--whale-contract-from" && i + 1 < args.Length) { _whaleContractFrom = int.Parse(args[++i]); return true; }
            if (a == "--whale-events-from" && i + 1 < args.Length) { WhaleEventsFromCleared = int.Parse(args[++i]); return true; }
            if (a == "--whale-no-free-events") { _whaleFreeEvents = false; return true; }
            if (a == "--whale-choice-20k") { _whaleChoiceMax = false; return true; }
            if (a == "--whale-skip" && i + 1 < args.Length) { foreach (string t in args[++i].Split(',')) _whaleSkip.Add(t.Trim()); return true; }
            if (a == "--whale-scale" && i + 1 < args.Length)
            {
                foreach (string kv in args[++i].Split(','))
                {
                    string[] p = kv.Split('=');
                    if (p.Length == 2) _whaleScale[p[0].Trim()] = double.Parse(p[1], CultureInfo.InvariantCulture);
                }
                return true;
            }
            return false;
        }

        private static int RunWhaleStudy(string contentDir)
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (WhaleMode == "report") return WhaleReport(contentDir, WhaleDir);
            ContentDatabase db = LoadContent(contentDir);
            if (db == null) return 1;
            if (WhaleMode == "dust") return WhaleDustStudy(db, WhaleWalks);
            if (WhaleMode == "curve") return WhaleCurveColumn(db);
            Line("unknown whale mode " + WhaleMode);
            return 1;
        }

        // ------------------------------------------------------------------ dust income

        private static int WhaleStageIndex(ContentDatabase db, string code)
        {
            for (int i = 0; i < db.AllStages.Count; i++) if (db.AllStages[i].DisplayCode == code) return i;
            return -1;
        }

        private static string Spread(List<int> xs)
        {
            if (xs.Count == 0) return "-";
            return Median(xs) + " (p10 " + Percentile(xs, 0.1) + ", p90 " + Percentile(xs, 0.9) + ", min " + Percentile(xs, 0) + ", max " + Percentile(xs, 1.0) + ")";
        }

        /// <summary>The model Dust a day (R17 relic-pricing.md): the top n cleared stages, repeatFullClearsPerDay sweeps each.</summary>
        private static long WhaleModelDust(ContentDatabase db, GameSession s, int frontierIdx, int topStages)
        {
            long total = 0;
            for (int i = frontierIdx; i >= 0 && i > frontierIdx - topStages; i--)
                total += s.RelicDustForRepeatClear(db.AllStages[i]) * (long)db.Balance.RepeatFullClearsPerDay;
            return total;
        }

        private static int WhaleDustStudy(ContentDatabase db, int walks)
        {
            Line("");
            Line("WHALE DUST - " + walks + " reference walks (never converts, never ascends), plain clock. The walk buys no relic, so its Dust only accrues.");
            EconomyRun[] runs = SimulateEconomies(db, walks, false, false);
            string[] codes = { "3-12", "4-12", "5-12", "6-12" };
            for (int c = 0; c < codes.Length; c++)
            {
                int idx = WhaleStageIndex(db, codes[c]);
                int chapterFirst = idx - 11;
                List<int> dayCleared = new List<int>(), heldPerDay = new List<int>(), chapterRate = new List<int>(), stageRate = new List<int>(), stageDays = new List<int>();
                int reached = 0;
                for (int k = 0; k < runs.Length; k++)
                {
                    EconomyRun r = runs[k];
                    if (idx >= r.Stages.Count || !r.Stages[idx].Cleared) continue;
                    reached++;
                    long secs = 0;
                    for (int s = 0; s <= idx; s++) secs += r.Stages[s].ElapsedSeconds;
                    double days = secs / 86400.0;
                    dayCleared.Add((int)Math.Round(days));
                    heldPerDay.Add((int)Math.Round(r.Stages[idx].ExitRelicDust / days));
                    long chSecs = 0;
                    for (int s = chapterFirst; s <= idx; s++) chSecs += r.Stages[s].ElapsedSeconds;
                    chapterRate.Add((int)Math.Round((r.Stages[idx].ExitRelicDust - r.Stages[chapterFirst].EntryRelicDust) / (chSecs / 86400.0)));
                    double stDays = Math.Max(0.01, r.Stages[idx].ElapsedSeconds / 86400.0);
                    stageDays.Add((int)Math.Round(stDays * 10));
                    stageRate.Add((int)Math.Round((r.Stages[idx].ExitRelicDust - r.Stages[idx].EntryRelicDust) / stDays));
                }
                Line("");
                Line("  frontier " + codes[c] + " (cleared, every earlier stage cleared): reached by " + reached + " of " + walks);
                Line("    walk day the frontier is cleared:        " + Spread(dayCleared));
                Line("    walk's Dust a day over its whole campaign: " + Spread(heldPerDay));
                Line("    walk's Dust a day over chapter " + codes[c][0] + " alone:    " + Spread(chapterRate));
                Line("    walk's Dust a day while sitting at " + codes[c] + ": " + Spread(stageRate) + "; days spent there x10: " + Spread(stageDays));
                EconomyRun any = runs[0];
                Line("    model Dust a day: committed (12 stages x3) " + WhaleModelDust(db, any.Session, idx, 12)
                     + ", casual (4 stages x3) " + WhaleModelDust(db, any.Session, idx, 4)
                     + ", ceiling (every stage x3) " + WhaleModelDust(db, any.Session, idx, idx + 1)
                     + "; one frontier sweep " + any.Session.RelicDustForRepeatClear(db.AllStages[idx]));
            }
            return 0;
        }

        // ------------------------------------------------------------------ the offers (PLACEHOLDER contents)

        /// <summary>One thing a study buyer can buy that the Store does not yet carry (or that its builder has not landed).
        /// Contents are fixed and shown in full, as the rule demands. Amounts are scaled by --whale-scale name=factor.</summary>
        private sealed class WhaleOffer
        {
            public string Name;
            public double Eur;
            /// <summary>week, month or event28 (a 28 day event); the reset the limit of 1 counts in.</summary>
            public string Period;
            /// <summary>Cleared stages (a count) needed before it is on sale; 0 for always.</summary>
            public int NeedsCleared;
            public bool NeedsRelicShop;
            public double GoldHours, DustSweeps, FixedDust;
            /// <summary>How many a buyer may take in one period (1 unless the offer says more).</summary>
            public int Limit = 1;
            public string Source;
        }

        /// <summary>The R24 offers as the builders have written them. Event contents are builder-a's live-events.json
        /// (ember_trials weekly, tidecallers_month monthly) as committed: the weekly paid track is 30 hours and 22 sweeps
        /// over its ten tiers, the monthly paid track 120 hours and 80 sweeps over twelve, the weekly pack 48 hours and
        /// 24 sweeps, the monthly pack 96 hours and 60 sweeps; the free tracks are 7 hours and 8 sweeps a week and 24 hours
        /// and 18 sweeps over 28 days. The whale is a diligent player (every quest done, every tier claimed). Relic packs
        /// are builder-b's store.json: Satchel 5,000 Dust, Chest 25,000 Dust, Relic of Choice (WhaleChoice). Prices are the
        /// shipped tiers (mid 4.99, high 9.99, top 19.99, premium 29.99).</summary>
        private static List<WhaleOffer> WhaleOffers()
        {
            List<WhaleOffer> l = new List<WhaleOffer>();
            l.Add(new WhaleOffer { Name = "satchel", Eur = 4.99, Period = "week", NeedsRelicShop = true, FixedDust = 5000, Source = "builder-b store.json relic_dust_satchel" });
            l.Add(new WhaleOffer { Name = "chest", Eur = 19.99, Period = _whaleChestMonthly ? "month" : "week", Limit = _whaleChestMonthly ? 2 : 1, NeedsRelicShop = true, FixedDust = 25000, Source = "builder-b store.json relic_dust_chest" });
            l.Add(new WhaleOffer { Name = "wtrack", Eur = 4.99, Period = "week", NeedsCleared = WhaleEventsFromCleared, GoldHours = 30, DustSweeps = 22, Source = "builder-a live-events.json ember_trials paid track" });
            l.Add(new WhaleOffer { Name = "mtrack", Eur = 9.99, Period = "event28", NeedsCleared = WhaleEventsFromCleared, GoldHours = 120, DustSweeps = 80, Source = "builder-a live-events.json tidecallers_month paid track" });
            l.Add(new WhaleOffer { Name = "wpack", Eur = 9.99, Period = "week", NeedsCleared = WhaleEventsFromCleared, GoldHours = 48, DustSweeps = 24, Source = "builder-a live-events.json ember_trials pack" });
            l.Add(new WhaleOffer { Name = "mpack", Eur = 9.99, Period = "event28", NeedsCleared = WhaleEventsFromCleared, GoldHours = 96, DustSweeps = 60, Source = "builder-a live-events.json tidecallers_month pack" });
            return l;
        }

        /// <summary>Stages cleared before the PAID event offers (tracks and packs) are on: 25 is 3-1 cleared (the lead's decision, requiresStage
        /// ch3_1). The free events, quests and free tracks are open from day one.</summary>
        private static int WhaleEventsFromCleared = 25;

        /// <summary>The free tracks every buyer claims (the quests done, the tiers claimed): plain earned goods, here applied through
        /// the same grant path because the walk plays no quest.</summary>
        private static List<WhaleOffer> WhaleFreeTracks()
        {
            List<WhaleOffer> l = new List<WhaleOffer>();
            l.Add(new WhaleOffer { Name = "free_weekly", Period = "week", NeedsCleared = 0, GoldHours = 7, DustSweeps = 8, Source = "builder-a live-events.json free track" });
            l.Add(new WhaleOffer { Name = "free_monthly", Period = "event28", NeedsCleared = 0, GoldHours = 24, DustSweeps = 18, Source = "builder-a live-events.json free track" });
            return l;
        }

        /// <summary>Every buyer's free event tracks, once per period.</summary>
        private static void WhaleFreeEvents(ContentDatabase db, GameSession s, long now)
        {
            WhaleWalk w = _whaleWalk;
            if (w == null || !_whaleFreeEvents) return;
            int cleared = s.Profile.ClearedStageIds.Count;
            List<WhaleOffer> offers = WhaleFreeTracks();
            for (int i = 0; i < offers.Count; i++)
            {
                WhaleOffer o = offers[i];
                if (cleared < o.NeedsCleared) continue;
                long key = PeriodKey(db, o.Period, now), last;
                if (w.LastPeriod.TryGetValue(o.Name, out last) && last == key) continue;
                PurchaseGrant g = new PurchaseGrant();
                g.Add(new GrantItem(GrantKind.IdleGoldHours, o.GoldHours));
                g.Add(new GrantItem(GrantKind.DustSweeps, o.DustSweeps));
                if (PurchaseGrants.Apply(s, g, null, now).Success) w.LastPeriod[o.Name] = key;
            }
        }

        private static double WhaleScaleOf(string name)
        {
            double f;
            return _whaleScale.TryGetValue(name, out f) ? f : 1.0;
        }

        // ------------------------------------------------------------------ one walk's study state

        private sealed class WhaleWalk
        {
            public List<KeyValuePair<long, KeyValuePair<double, string>>> Spend = new List<KeyValuePair<long, KeyValuePair<double, string>>>();
            public List<KeyValuePair<long, long>> DustBought = new List<KeyValuePair<long, long>>();
            public List<long[]> RelicTimeline = new List<long[]>();
            public Dictionary<string, long> LastPeriod = new Dictionary<string, long>();
            public Dictionary<string, int> PeriodCount = new Dictionary<string, int>();
            public HashSet<string> Skinned = new HashSet<string>();
            public Dictionary<string, float[]> OriginalStats = new Dictionary<string, float[]>();
            public Dictionary<string, HeroDef> SkinnedDefs = new Dictionary<string, HeroDef>();
            public long SpentRelicDust, GrantedDust, LastPlayEarned, LastTopUpNow = -1;
            public long ContractMonth = -1;
            public int ContractsThisMonth;
            public int LastOwned = -1, LastTierSum = -1, LastWorn = -1;
        }

        private static WhaleWalk _whaleWalk;

        private static void WhaleBeginWalk() { _whaleWalk = new WhaleWalk(); }

        /// <summary>Puts every skinned hero's stats back, so the next walk reads the shipped content.</summary>
        private static void WhaleEndWalk()
        {
            WhaleWalk w = _whaleWalk;
            if (w == null) return;
            foreach (KeyValuePair<string, HeroDef> kv in w.SkinnedDefs)
            {
                float[] o = w.OriginalStats[kv.Key];
                kv.Value.BaseAttack = o[0];
                kv.Value.BaseHp = o[1];
            }
        }

        private static void WhaleNoteSpend(long now, double eur, string label)
        {
            if (_whaleWalk != null) _whaleWalk.Spend.Add(new KeyValuePair<long, KeyValuePair<double, string>>(now, new KeyValuePair<double, string>(eur, label)));
        }

        /// <summary>Called by the R19 spender's loop after a purchase went through: its price in euros.</summary>
        private static void WhaleNoteBought(ContentDatabase db, string offerId, long now)
        {
            StoreOfferDef d = db.Store.Offer(offerId);
            if (d != null) WhaleNoteSpend(now, d.PriceEur, offerId);
        }

        /// <summary>Dust that paid purchases put in the purse this sitting (everything a purchase or a claim of one grants).</summary>
        private static void WhaleNoteDust(long now, long amount)
        {
            if (_whaleWalk == null || amount <= 0) return;
            _whaleWalk.DustBought.Add(new KeyValuePair<long, long>(now, amount));
            _whaleWalk.GrantedDust += amount;
        }

        // ------------------------------------------------------------------ periods

        private static long WeekIndex(ContentDatabase db, long now) { return (now - StoreWalkClockStart(db)) / (7L * 86400L); }
        private static long Event28Index(ContentDatabase db, long now)
        {
            long epoch = (long)(new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            return (now - epoch) / (28L * 86400L);
        }
        private static long MonthIndex(long now)
        {
            DateTime d = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(now);
            return d.Year * 12L + d.Month;
        }

        private static long PeriodKey(ContentDatabase db, string period, long now)
        {
            if (period == "week") return WeekIndex(db, now);
            if (period == "month") return MonthIndex(now);
            return Event28Index(db, now);
        }

        // ------------------------------------------------------------------ the whale's shopping

        /// <summary>Everything R24 adds, bought every period by the whale, after the R19 spender has bought his.</summary>
        private static void WhaleStep(ContentDatabase db, GameSession s, long now)
        {
            WhaleWalk w = _whaleWalk;
            if (w == null) return;
            PlayerProfile p = s.Profile;
            int cleared = p.ClearedStageIds.Count;
            bool shop = FeatureGate.IsUnlocked(db, p, Feature.Relics);

            if (!_whaleSkip.Contains("contract")) WhaleContracts(db, s, now);
            if (!_whaleSkip.Contains("choice") && shop) WhaleChoice(db, s, now);

            List<WhaleOffer> offers = WhaleOffers();
            for (int i = 0; i < offers.Count; i++)
            {
                WhaleOffer o = offers[i];
                if (_whaleSkip.Contains(o.Name)) continue;
                if (o.NeedsRelicShop && !shop) continue;
                if (cleared < o.NeedsCleared) continue;
                long key = PeriodKey(db, o.Period, now);
                long last;
                int taken = 0;
                if (w.LastPeriod.TryGetValue(o.Name, out last) && last == key) w.PeriodCount.TryGetValue(o.Name, out taken);
                if (taken >= o.Limit) continue;
                double f = WhaleScaleOf(o.Name);
                PurchaseGrant g = new PurchaseGrant();
                if (o.GoldHours > 0) g.Add(new GrantItem(GrantKind.IdleGoldHours, o.GoldHours * f));
                if (o.DustSweeps > 0) g.Add(new GrantItem(GrantKind.DustSweeps, o.DustSweeps * f));
                if (o.FixedDust > 0) g.Add(new GrantItem(GrantKind.Dust, Math.Round(o.FixedDust * f)));
                if (g.Items.Count == 0) continue;
                GrantResult r = PurchaseGrants.Apply(s, g, null, now);
                if (!r.Success) continue;
                w.LastPeriod[o.Name] = key;
                w.PeriodCount[o.Name] = taken + 1;
                WhaleNoteSpend(now, o.Eur, o.Name);
                // the Dust of this grant is counted by the caller's before/after reading of the purse (WhaleNoteDust)
            }

            if (!_whaleSkip.Contains("skins")) WhaleSkins(db, s, now);
        }

        /// <summary>The Relic of Choice (EUR 29.99, once a month): the buyer names one relic they do not own, or one more tier on one they
        /// own, for nothing but the price. The whale names the dearest step that helps the line: the next tier of a relic a hero on the line
        /// wears (by its Dust price), else a Mythic they lack, else a Legendary. --whale-choice-20k reads it as a plain 20,000 Dust instead.</summary>
        private static void WhaleChoice(ContentDatabase db, GameSession s, long now)
        {
            WhaleWalk w = _whaleWalk;
            long key = MonthIndex(now), last;
            if (w.LastPeriod.TryGetValue("choice", out last) && last == key) return;
            PlayerProfile p = s.Profile;
            if (!_whaleChoiceMax)
            {
                PurchaseGrant g = new PurchaseGrant();
                g.Add(new GrantItem(GrantKind.Dust, Math.Round(20000 * WhaleScaleOf("choice"))));
                if (!PurchaseGrants.Apply(s, g, null, now).Success) return;
                w.LastPeriod["choice"] = key;
                WhaleNoteSpend(now, 29.99, "choice");
                return;
            }
            OwnedRelic bestUp = null;
            int bestUpPrice = 0;
            List<HeroInstance> team = p.TeamHeroes();
            for (int i = 0; i < team.Count; i++)
            {
                OwnedRelic o = string.IsNullOrEmpty(team[i].EquippedRelicId) ? null : p.FindOwnedRelic(team[i].EquippedRelicId);
                if (o == null || o.Tier >= s.RelicMaxTier) continue;
                int price = s.RelicUpgradePrice(db.GetRelic(o.RelicId), o.Tier + 1);
                if (price <= _whaleChoiceCap && price > bestUpPrice) { bestUp = o; bestUpPrice = price; }
            }
            long worth = bestUpPrice;
            if (bestUp != null) bestUp.Tier++;
            else
            {
                RelicDef pick = null;
                for (int k = 0; k < db.Relics.Count; k++)
                {
                    RelicDef r = db.Relics[k];
                    if (p.OwnsRelic(r.Id) || s.RelicPrice(r) > _whaleChoiceCap) continue;
                    if (pick == null || s.RelicPrice(r) > s.RelicPrice(pick)) pick = r;
                }
                if (pick == null || !s.GrantRelic(pick.Id)) return;
                worth = s.RelicPrice(pick);
            }
            // the pick is worth its Dust price to the buyer: counted in the Dust bought, but not in GrantedDust (no Dust entered the purse)
            w.DustBought.Add(new KeyValuePair<long, long>(now, worth));
            w.LastPeriod["choice"] = key;
            WhaleNoteSpend(now, 29.99, "choice");
            WhaleRelicTimeline(now, p);
        }

        /// <summary>The Hero Contract with its cap: 2 per hero a month (the offer's own limit) and 4 a month in all (the
        /// account cap, counted here until the store carries it). Two heroes, the two owned 5-stars with the most Sealed copies.</summary>
        private static void WhaleContracts(ContentDatabase db, GameSession s, long now)
        {
            WhaleWalk w = _whaleWalk;
            if (s.Profile.ClearedStageIds.Count < _whaleContractFrom) return;
            long mk = MonthIndex(now);
            if (mk != w.ContractMonth) { w.ContractMonth = mk; w.ContractsThisMonth = 0; }
            int cap = (int)Math.Round(4 * WhaleScaleOf("contract"));
            List<HeroInstance> owned = new List<HeroInstance>();
            for (int i = 0; i < s.Profile.Heroes.Count; i++)
            {
                HeroInstance h = s.Profile.Heroes[i];
                if (h.Def != null && h.Def.Stars == 5) owned.Add(h);
            }
            owned.Sort(delegate(HeroInstance a, HeroInstance b)
            {
                int c = b.SealedCopiesHeld.CompareTo(a.SealedCopiesHeld);
                return c != 0 ? c : b.Level.CompareTo(a.Level);
            });
            for (int t = 0; t < owned.Count && t < 2; t++)
                for (int c = 0; c < 2 && w.ContractsThisMonth < cap; c++)
                    if (StoreOffers.Check(s, "hero_contract", owned[t].DefId, now) == StoreRefusal.None)
                    {
                        StoreFulfilment f = StoreOffers.Fulfil(s, "hero_contract", owned[t].DefId, now);
                        if (!f.Success) continue;
                        w.ContractsThisMonth++;
                        WhaleNoteBought(db, "hero_contract", now);
                    }
        }

        /// <summary>Skins: one for each hero the first sitting he is on the line, EUR 4.99 to 9.99 by rarity (the hero's stars),
        /// +2 percent Attack and +3 percent HP on that hero.</summary>
        private static void WhaleSkins(ContentDatabase db, GameSession s, long now)
        {
            WhaleWalk w = _whaleWalk;
            List<HeroInstance> team = s.Profile.TeamHeroes();
            for (int i = 0; i < team.Count; i++)
            {
                HeroDef d = team[i].Def;
                if (d == null || w.Skinned.Contains(d.Id)) continue;
                w.Skinned.Add(d.Id);
                w.OriginalStats[d.Id] = new float[] { d.BaseAttack, d.BaseHp };
                w.SkinnedDefs[d.Id] = d;
                d.BaseAttack = d.BaseAttack * (1f + 0.02f * (float)WhaleScaleOf("skins"));
                d.BaseHp = d.BaseHp * (1f + 0.03f * (float)WhaleScaleOf("skins"));
                WhaleNoteSpend(now, d.Stars >= 5 ? 9.99 : d.Stars == 4 ? 7.99 : 4.99, "skin");
            }
        }

        // ------------------------------------------------------------------ relics, for every buyer alike

        private static readonly string[] WhaleLegendaryOrder = { "warband_standard", "bastion_core", "warlords_momentum", "hollow_reserve", "cutting_edge", "marksmans_coil" };

        private static void WhaleRelicTimeline(long now, PlayerProfile p)
        {
            WhaleWalk w = _whaleWalk;
            int owned = p.OwnedRelics.Count, tiers = 0, worn = 0;
            for (int i = 0; i < p.OwnedRelics.Count; i++) tiers += p.OwnedRelics[i].Tier;
            List<HeroInstance> line = p.TeamHeroes();
            for (int i = 0; i < line.Count; i++)
            {
                OwnedRelic o = string.IsNullOrEmpty(line[i].EquippedRelicId) ? null : p.FindOwnedRelic(line[i].EquippedRelicId);
                if (o != null) worn += o.Tier;
            }
            if (owned != w.LastOwned || tiers != w.LastTierSum || worn != w.LastWorn)
            {
                w.LastWorn = worn;
                w.RelicTimeline.Add(new long[] { now, owned, tiers, worn });
                w.LastOwned = owned;
                w.LastTierSum = tiers;
            }
        }

        private static void WhaleRelicStep(ContentDatabase db, GameSession s, long now)
        {
            WhaleWalk w = _whaleWalk;
            if (!_whaleRelics || w == null) return;
            PlayerProfile p = s.Profile;
            if (!FeatureGate.IsUnlocked(db, p, Feature.Relics)) return;

            if (_whaleCommitted)
            {
                long playEarned = p.RelicDust + w.SpentRelicDust - w.GrantedDust;
                if (w.LastTopUpNow >= 0)
                {
                    double days = (now - w.LastTopUpNow) / 86400.0;
                    int frontier = p.ClearedStageIds.Count - 1;
                    long committed = WhaleModelDust(db, s, frontier, 12);
                    long extra = (long)Math.Round(committed * days) - (playEarned - w.LastPlayEarned);
                    if (extra > 0) { p.RelicDust += extra; w.GrantedDust += extra; playEarned += extra; }
                }
                w.LastTopUpNow = now;
                w.LastPlayEarned = playEarned;
            }

            for (int guard = 0; guard < 200; guard++)
            {
                if (!WhaleOneRelicMove(db, s, now)) break;
            }
            WhaleRelicTimeline(now, p);
        }

        /// <summary>One purchase of the shared relic policy; false when nothing is affordable. The line is filled with
        /// Legendary relics first (cheapest), then every worn relic is raised a tier at a time (the lowest tier, then the
        /// cheapest), then the Mythics are bought and worn over the weakest.</summary>
        private static bool WhaleOneRelicMove(ContentDatabase db, GameSession s, long now)
        {
            PlayerProfile p = s.Profile;
            List<HeroInstance> team = p.TeamHeroes();
            // a relic on every hero on the line: give each the best free one
            for (int i = 0; i < team.Count; i++)
            {
                if (!string.IsNullOrEmpty(team[i].EquippedRelicId) && p.OwnsRelic(team[i].EquippedRelicId)) continue;
                List<OwnedRelic> free = p.UnassignedRelics();
                if (free.Count > 0) s.AssignRelic(team[i], free[0].RelicId);
            }

            int wornCount = 0;
            for (int i = 0; i < team.Count; i++) if (!string.IsNullOrEmpty(team[i].EquippedRelicId)) wornCount++;

            // 1. buy a Legendary for a bare hero
            if (wornCount < team.Count)
            {
                for (int k = 0; k < WhaleLegendaryOrder.Length; k++)
                {
                    RelicDef r = db.GetRelic(WhaleLegendaryOrder[k]);
                    if (r == null || p.OwnsRelic(r.Id)) continue;
                    if (p.RelicDust < s.RelicPrice(r)) return false;   // the cheapest first, and none is affordable
                    long before = p.RelicDust;
                    if (!s.BuyRelic(r.Id, now)) return false;
                    w_Spent(before - p.RelicDust);
                    for (int i = 0; i < team.Count; i++)
                        if (string.IsNullOrEmpty(team[i].EquippedRelicId)) { s.AssignRelic(team[i], r.Id); break; }
                    return true;
                }
            }

            // 2. raise the worn relic with the lowest tier (then the cheapest)
            OwnedRelic best = null;
            int bestPrice = int.MaxValue;
            for (int i = 0; i < team.Count; i++)
            {
                if (string.IsNullOrEmpty(team[i].EquippedRelicId)) continue;
                OwnedRelic o = p.FindOwnedRelic(team[i].EquippedRelicId);
                if (o == null || o.Tier >= s.RelicMaxTier) continue;
                RelicDef r = db.GetRelic(o.RelicId);
                int price = s.RelicUpgradePrice(r, o.Tier + 1);
                if (price <= 0) continue;
                if (best == null || o.Tier < best.Tier || (o.Tier == best.Tier && price < bestPrice)) { best = o; bestPrice = price; }
            }
            if (best != null)
            {
                if (p.RelicDust < bestPrice) return false;
                long before = p.RelicDust;
                if (!s.UpgradeRelic(best.RelicId, now)) return false;
                w_Spent(before - p.RelicDust);
                return true;
            }

            // 3. every worn relic is at the top tier: buy a Mythic and wear it over the weakest Legendary
            for (int k = 0; k < db.Relics.Count; k++)
            {
                RelicDef r = db.Relics[k];
                if (r.Grade != RelicGrade.Mythic || p.OwnsRelic(r.Id)) continue;
                if (p.RelicDust < s.RelicPrice(r)) return false;
                long before = p.RelicDust;
                if (!s.BuyRelic(r.Id, now)) return false;
                w_Spent(before - p.RelicDust);
                for (int i = team.Count - 1; i >= 0; i--)
                {
                    RelicDef worn = db.GetRelic(team[i].EquippedRelicId);
                    if (worn != null && worn.Grade == RelicGrade.Legendary) { s.AssignRelic(team[i], r.Id); break; }
                }
                return true;
            }
            return false;
        }

        private static void w_Spent(long amount) { if (_whaleWalk != null) _whaleWalk.SpentRelicDust += amount; }

        // ------------------------------------------------------------------ one column of the curve

        private static int WhaleCurveColumn(ContentDatabase db)
        {
            string[] col = WhaleCol.Split(':');
            bool fuse = col[0] == "fuse";
            int mode = col[1] == "non" ? 1 : col[1] == "spend" ? 2 : 4;
            int to = WhaleTo >= 0 ? WhaleTo : WhaleWalks;
            if (WhaleOut == null) { Line("--whale-out is required"); return 1; }
            _walkStoreDb = db;
            StringBuilder sb = new StringBuilder();
            sb.Append("# column ").Append(WhaleCol).Append(" walks ").Append(WhaleFrom).Append("..").Append(to - 1)
              .Append(" relics ").Append(_whaleRelics ? "on" : "off").Append(" committed ").Append(_whaleCommitted ? "on" : "off")
              .Append(" skip ").Append(string.Join(",", new List<string>(_whaleSkip).ToArray())).Append(" scale");
            foreach (KeyValuePair<string, double> kv in _whaleScale) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value.ToString(CultureInfo.InvariantCulture));
            sb.Append('\n');
            int was = _walkStoreMode;
            _walkStoreMode = mode;
            try
            {
                for (int k = WhaleFrom; k < to; k++)
                {
                    WhaleBeginWalk();
                    EconomyRun r;
                    try { r = SimulateEconomyUnder(db, WalkSeed(k), fuse, fuse); }
                    finally { WhaleEndWalk(); }
                    WhaleWrite(sb, db, r, WhaleCol, k);
                    Console.Error.WriteLine("walk " + k + " " + WhaleCol + " done");
                }
            }
            finally { _walkStoreMode = was; }
            File.WriteAllText(WhaleOut, sb.ToString(), new UTF8Encoding(false));
            return 0;
        }

        private static string Inv(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

        private static void WhaleWrite(StringBuilder sb, ContentDatabase db, EconomyRun r, string col, int k)
        {
            WhaleWalk w = _whaleWalk;
            sb.Append("W\t").Append(col).Append('\t').Append(k).Append('\t').Append(r.StuckAt).Append('\t').Append(r.TotalSessions)
              .Append('\t').Append(StoreWalkClockStart(db)).Append('\n');
            for (int i = 0; i < r.Stages.Count; i++)
            {
                EconomyStageOutcome o = r.Stages[i];
                int ladder, floor;
                int extra = o.Cleared ? WallExtraSittings(o, out ladder, out floor) : -1;
                sb.Append("S\t").Append(col).Append('\t').Append(k).Append('\t').Append(i).Append('\t').Append(o.EntryPower)
                  .Append('\t').Append(o.Sessions).Append('\t').Append(extra).Append('\t').Append(o.Cleared ? 1 : 0)
                  .Append('\t').Append(o.ElapsedSeconds).Append('\t').Append(o.EntryRelicDust).Append('\n');
            }
            for (int i = 0; i < w.Spend.Count; i++)
                sb.Append("E\t").Append(col).Append('\t').Append(k).Append('\t').Append(w.Spend[i].Key).Append('\t')
                  .Append(Inv(w.Spend[i].Value.Key)).Append('\t').Append(w.Spend[i].Value.Value).Append('\n');
            for (int i = 0; i < w.DustBought.Count; i++)
                sb.Append("D\t").Append(col).Append('\t').Append(k).Append('\t').Append(w.DustBought[i].Key).Append('\t').Append(w.DustBought[i].Value).Append('\n');
            for (int i = 0; i < w.RelicTimeline.Count; i++)
                sb.Append("R\t").Append(col).Append('\t').Append(k).Append('\t').Append(w.RelicTimeline[i][0]).Append('\t')
                  .Append(w.RelicTimeline[i][1]).Append('\t').Append(w.RelicTimeline[i][2]).Append('\t').Append(w.RelicTimeline[i][3]).Append('\n');
        }

        // ------------------------------------------------------------------ the report

        private sealed class WhaleSpendRow { public long T; public double Eur; public string Label; }

        private sealed class WhaleStageRow { public int Idx, Power, Sessions, Extra, Cleared; public long Elapsed, Dust; }

        private sealed class WhaleWalkData
        {
            public int Walk, StuckAt, Sessions;
            public long Start;
            public List<WhaleStageRow> Stages = new List<WhaleStageRow>();
            public List<WhaleSpendRow> Spend = new List<WhaleSpendRow>();
            public List<KeyValuePair<long, long>> Dust = new List<KeyValuePair<long, long>>();
            public List<long[]> Relics = new List<long[]>();
            public long[] EntryT;

            public void Finish()
            {
                EntryT = new long[Stages.Count + 1];
                long t = Start;
                for (int i = 0; i < Stages.Count; i++) { EntryT[i] = t; t += Stages[i].Elapsed; }
                EntryT[Stages.Count] = t;
            }

            public double SpendBetween(long t0, long t1)
            {
                double sum = 0;
                for (int i = 0; i < Spend.Count; i++) if (Spend[i].T >= t0 && Spend[i].T < t1) sum += Spend[i].Eur;
                return sum;
            }

            public long DustBetween(long t0, long t1)
            {
                long sum = 0;
                for (int i = 0; i < Dust.Count; i++) if (Dust[i].Key >= t0 && Dust[i].Key < t1) sum += Dust[i].Value;
                return sum;
            }

            public long[] RelicsAt(long t)
            {
                long[] last = new long[] { 0, 0, 0 };
                for (int i = 0; i < Relics.Count; i++) { if (Relics[i][0] > t) break; last = Relics[i]; }
                return last;
            }
        }

        private static Dictionary<string, List<WhaleWalkData>> WhaleLoad(string dir, List<string> headers)
        {
            Dictionary<string, Dictionary<int, WhaleWalkData>> by = new Dictionary<string, Dictionary<int, WhaleWalkData>>();
            string[] files = Directory.GetFiles(dir, "*.tsv");
            Array.Sort(files);
            for (int f = 0; f < files.Length; f++)
            {
                string[] lines = File.ReadAllLines(files[f]);
                for (int i = 0; i < lines.Length; i++)
                {
                    string ln = lines[i];
                    if (ln.StartsWith("#")) { headers.Add(Path.GetFileName(files[f]) + ": " + ln); continue; }
                    string[] c = ln.Split('\t');
                    if (c.Length < 4) continue;
                    string col = c[1];
                    int walk = int.Parse(c[2]);
                    if (!by.ContainsKey(col)) by[col] = new Dictionary<int, WhaleWalkData>();
                    Dictionary<int, WhaleWalkData> m = by[col];
                    if (!m.ContainsKey(walk)) { WhaleWalkData n = new WhaleWalkData(); n.Walk = walk; m[walk] = n; }
                    WhaleWalkData d = m[walk];
                    switch (c[0])
                    {
                        case "W": d.StuckAt = int.Parse(c[3]); d.Sessions = int.Parse(c[4]); d.Start = long.Parse(c[5]); break;
                        case "S":
                            WhaleStageRow s = new WhaleStageRow();
                            s.Idx = int.Parse(c[3]); s.Power = int.Parse(c[4]); s.Sessions = int.Parse(c[5]); s.Extra = int.Parse(c[6]);
                            s.Cleared = int.Parse(c[7]); s.Elapsed = long.Parse(c[8]); s.Dust = long.Parse(c[9]);
                            d.Stages.Add(s); break;
                        case "E": d.Spend.Add(new WhaleSpendRow { T = long.Parse(c[3]), Eur = double.Parse(c[4], CultureInfo.InvariantCulture), Label = c[5] }); break;
                        case "D": d.Dust.Add(new KeyValuePair<long, long>(long.Parse(c[3]), long.Parse(c[4]))); break;
                        case "R": d.Relics.Add(new long[] { long.Parse(c[3]), long.Parse(c[4]), long.Parse(c[5]), c.Length > 6 ? long.Parse(c[6]) : 0 }); break;
                    }
                }
            }
            Dictionary<string, List<WhaleWalkData>> result = new Dictionary<string, List<WhaleWalkData>>();
            foreach (KeyValuePair<string, Dictionary<int, WhaleWalkData>> kv in by)
            {
                List<WhaleWalkData> l = new List<WhaleWalkData>(kv.Value.Values);
                l.Sort(delegate(WhaleWalkData a, WhaleWalkData b) { return a.Walk.CompareTo(b.Walk); });
                for (int i = 0; i < l.Count; i++) { l[i].Stages.Sort(delegate(WhaleStageRow a, WhaleStageRow b) { return a.Idx.CompareTo(b.Idx); }); l[i].Finish(); }
                result[kv.Key] = l;
            }
            return result;
        }

        private static double MedianD(List<double> xs)
        {
            if (xs.Count == 0) return 0;
            List<double> s = new List<double>(xs);
            s.Sort();
            return s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2.0;
        }

        private static double PercentileD(List<double> xs, double p)
        {
            if (xs.Count == 0) return 0;
            List<double> s = new List<double>(xs);
            s.Sort();
            int rank = Math.Max(1, (int)Math.Ceiling(p * s.Count));
            return s[rank - 1];
        }

        private static string Pc(double v) { return (v >= 0 ? "+" : "") + (v * 100.0).ToString("0.0", CultureInfo.InvariantCulture) + "%"; }

        private static int WhaleMedianPower(List<WhaleWalkData> walks, int idx)
        {
            List<int> xs = new List<int>();
            for (int i = 0; i < walks.Count; i++) if (idx < walks[i].Stages.Count) xs.Add(walks[i].Stages[idx].Power);
            return xs.Count == 0 ? 0 : Median(xs);
        }

        private static readonly string[] WhaleOneTime = { "starter_pact", "growth_fund", "skin" };

        private static bool IsOneTime(string label) { return Array.IndexOf(WhaleOneTime, label) >= 0; }

        /// <summary>Dust a day of a committed player at a frontier stage, the basis the relic prices were sized on.</summary>
        private static long WhaleCommittedAt(ContentDatabase db, string code)
        {
            GameSession gs = new GameSession(db, GameSession.CreateNewProfile(db, 0));
            return WhaleModelDust(db, gs, WhaleStageIndex(db, code), 12);
        }

        private static int WhaleReport(string contentDir, string dir)
        {
            ContentDatabase db = LoadContent(contentDir);
            if (db == null) return 1;
            List<string> headers = new List<string>();
            Dictionary<string, List<WhaleWalkData>> cols = WhaleLoad(dir, headers);
            Line("");
            Line("WHALE CURVE REPORT from " + dir);
            foreach (string h in headers) Line("  " + h);
            string[] players = { "ref", "fuse" };
            foreach (string pl in players)
            {
                string non = pl + ":non", sp = pl + ":spend", wh = pl + ":whale";
                if (!cols.ContainsKey(non) || !cols.ContainsKey(sp) || !cols.ContainsKey(wh)) continue;
                List<WhaleWalkData> A = cols[non], B = cols[sp], C = cols[wh];
                Line("");
                Line("PLAYER " + pl.ToUpper() + " (" + (pl == "ref" ? "never converts, never ascends" : "converts and ascends") + "), walks non/spender/whale = " + A.Count + "/" + B.Count + "/" + C.Count);
                int stuckA = 0, stuckB = 0, stuckC = 0;
                foreach (WhaleWalkData d in A) if (d.StuckAt >= 0) stuckA++;
                foreach (WhaleWalkData d in B) if (d.StuckAt >= 0) stuckB++;
                foreach (WhaleWalkData d in C) if (d.StuckAt >= 0) stuckC++;
                Line("  stuck walks: non " + stuckA + ", spender " + stuckB + ", whale " + stuckC);
                Line("");
                Line("  POWER at each wall (entry power over recommendedPower; lead = median power over the non-spender's; paired = per walk, same seed, median [p10 to p90])");
                Line("  stage   rec.Pwr | non      spender  whale  | lead spender  lead whale | whale paired lead [p10 to p90]");
                for (int s = 0; s < db.AllStages.Count; s++)
                {
                    StageDef st = db.AllStages[s];
                    if (!(st.IsMiniboss || st.IsBoss)) continue;
                    int pa = WhaleMedianPower(A, s), pb = WhaleMedianPower(B, s), pc = WhaleMedianPower(C, s);
                    if (pa == 0) continue;
                    List<double> pairC = new List<double>();
                    for (int k = 0; k < A.Count && k < C.Count; k++)
                        if (s < A[k].Stages.Count && s < C[k].Stages.Count) pairC.Add(C[k].Stages[s].Power / (double)A[k].Stages[s].Power - 1.0);
                    Line("  " + st.DisplayCode.PadRight(7) + st.RecommendedPower.ToString().PadLeft(7) + " | "
                         + ("x" + Ratio3(pa / (float)st.RecommendedPower)).PadRight(9) + ("x" + Ratio3(pb / (float)st.RecommendedPower)).PadRight(9) + ("x" + Ratio3(pc / (float)st.RecommendedPower)).PadRight(7)
                         + " | " + Pc(pb / (double)pa - 1.0).PadLeft(11) + Pc(pc / (double)pa - 1.0).PadLeft(12)
                         + " | " + Pc(MedianD(pairC)) + " [" + Pc(PercentileD(pairC, 0.1)) + " to " + Pc(PercentileD(pairC, 0.9)) + "]");
                }
                Line("");
                Line("  WALLS (a wall binds when it still costs its ladder sittings: sittings at the stage, median; and the walks that needed extra sittings beyond the ladder)");
                Line("  stage   | sittings med non  spender  whale | walks with extra sittings non  spender  whale");
                for (int s = 0; s < db.AllStages.Count; s++)
                {
                    StageDef st = db.AllStages[s];
                    if (!(st.IsMiniboss || st.IsBoss)) continue;
                    Line("  " + st.DisplayCode.PadRight(7) + " | " + WhaleMedSessions(A, s).PadLeft(14) + WhaleMedSessions(B, s).PadLeft(9) + WhaleMedSessions(C, s).PadLeft(7)
                         + " | " + WhaleExtraShare(A, s).PadLeft(30) + "   " + WhaleExtraShare(B, s).PadLeft(8) + "   " + WhaleExtraShare(C, s).PadLeft(8));
                }
                WhaleSpendAndRelics(db, A, B, C);
            }
            return 0;
        }

        private static string WhaleMedSessions(List<WhaleWalkData> walks, int idx)
        {
            List<int> xs = new List<int>();
            for (int i = 0; i < walks.Count; i++) if (idx < walks[i].Stages.Count) xs.Add(walks[i].Stages[idx].Sessions);
            return xs.Count == 0 ? "-" : Median(xs).ToString();
        }

        private static string WhaleExtraShare(List<WhaleWalkData> walks, int idx)
        {
            int n = 0, m = 0;
            for (int i = 0; i < walks.Count; i++)
                if (idx < walks[i].Stages.Count) { n++; if (walks[i].Stages[idx].Extra > 0 || walks[i].Stages[idx].Cleared == 0) m++; }
            return n == 0 ? "-" : m + " of " + n;
        }

        private sealed class WhaleChapterRow { public double Days, EurTotal, EurRecurring30, EurOneTime; public double DustPerDay; public Dictionary<string, double> ByOffer30 = new Dictionary<string, double>(); }

        private static WhaleChapterRow WhaleChapter(List<WhaleWalkData> walks, int first, int last)
        {
            List<double> days = new List<double>(), tot = new List<double>(), rec = new List<double>(), one = new List<double>(), dust = new List<double>();
            Dictionary<string, List<double>> by = new Dictionary<string, List<double>>();
            for (int k = 0; k < walks.Count; k++)
            {
                WhaleWalkData d = walks[k];
                if (last >= d.Stages.Count) continue;
                long t0 = d.EntryT[first], t1 = d.EntryT[last + 1];
                double dd = Math.Max(0.01, (t1 - t0) / 86400.0);
                days.Add(dd);
                double total = 0, recurring = 0, oneTime = 0;
                Dictionary<string, double> mine = new Dictionary<string, double>();
                for (int i = 0; i < d.Spend.Count; i++)
                {
                    WhaleSpendRow e = d.Spend[i];
                    if (e.T < t0 || e.T >= t1) continue;
                    total += e.Eur;
                    if (IsOneTime(e.Label)) oneTime += e.Eur;
                    else { recurring += e.Eur; double v; mine.TryGetValue(e.Label, out v); mine[e.Label] = v + e.Eur; }
                }
                tot.Add(total); one.Add(oneTime); rec.Add(recurring / dd * 30.0);
                dust.Add(d.DustBetween(t0, t1) / dd);
                foreach (KeyValuePair<string, double> kv in mine)
                {
                    if (!by.ContainsKey(kv.Key)) by[kv.Key] = new List<double>();
                    by[kv.Key].Add(kv.Value / dd * 30.0);
                }
            }
            WhaleChapterRow r = new WhaleChapterRow();
            r.Days = MeanD(days); r.EurTotal = MeanD(tot); r.EurRecurring30 = MeanD(rec); r.EurOneTime = MeanD(one); r.DustPerDay = MeanD(dust);
            foreach (KeyValuePair<string, List<double>> kv in by)
            {
                // a walk that never bought it that chapter counts as 0, so the median is over every walk
                List<double> all = new List<double>(kv.Value);
                while (all.Count < days.Count) all.Add(0);
                r.ByOffer30[kv.Key] = MeanD(all);
            }
            return r;
        }

        private static double MeanD(List<double> xs)
        {
            if (xs.Count == 0) return 0;
            double t = 0;
            foreach (double v in xs) t += v;
            return t / xs.Count;
        }

        private static string F1(double v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }

        private static void WhaleSpendAndRelics(ContentDatabase db, List<WhaleWalkData> A, List<WhaleWalkData> B, List<WhaleWalkData> C)
        {
            Line("");
            Line("  SPEND per chapter window (first stage entry to last stage exit), mean over walks. Recurring = EUR per 30 days of everything but the one-time buys (Starter Pact, Growth Fund, skins); one-time = their total in the window.");
            Line("  chapter | days | spender: recurring/30d, one-time | whale: recurring/30d, one-time | Dust bought a day: spender, whale");
            Dictionary<int, WhaleChapterRow> rowB = new Dictionary<int, WhaleChapterRow>(), rowC = new Dictionary<int, WhaleChapterRow>();
            string[] names = { "1", "2", "3", "4", "5", "6", "2-3", "4-6", "2-6", "3-6" };
            int[] firsts = { 0, 12, 24, 36, 48, 60, 12, 36, 12, 24 };
            int[] lasts = { 11, 23, 35, 47, 59, 71, 35, 71, 71, 71 };
            for (int ch = 1; ch <= 10; ch++)
            {
                WhaleChapterRow b = WhaleChapter(B, firsts[ch - 1], lasts[ch - 1]), c = WhaleChapter(C, firsts[ch - 1], lasts[ch - 1]);
                rowB[ch] = b; rowC[ch] = c;
                Line("  " + names[ch - 1].PadRight(8) + "| " + F1(c.Days).PadLeft(4) + " | " + (F1(b.EurRecurring30) + ", " + F1(b.EurOneTime)).PadLeft(29) + " | " + (F1(c.EurRecurring30) + ", " + F1(c.EurOneTime)).PadLeft(27)
                     + " | " + ((long)b.DustPerDay).ToString().PadLeft(8) + ((long)c.DustPerDay).ToString().PadLeft(8));
            }
            Line("");
            Line("  WHALE recurring EUR per 30 days by offer (mean over walks, windows as above):");
            for (int ch = 1; ch <= 10; ch++)
            {
                List<KeyValuePair<string, double>> items = new List<KeyValuePair<string, double>>(rowC[ch].ByOffer30);
                items.Sort(delegate(KeyValuePair<string, double> x, KeyValuePair<string, double> y) { return y.Value.CompareTo(x.Value); });
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<string, double> kv in items) if (kv.Value >= 0.05) sb.Append(kv.Key).Append(' ').Append(F1(kv.Value)).Append(", ");
                Line("    " + names[ch - 1] + ": " + sb);
            }
            Line("");
            Line("  RELICS worn at the wall (median owned relics, median tier sum; a full line at tier 3 is 15, all ten at tier 3 is 30) and DAYS PER RELIC STEP");
            Line("  Dust a day is the player's own plus what the packs bought that chapter; own = the walk's floor (3 sweeps, from --whale-dust: 285 at 4-12, 426 at 6-12) or the committed player (12 stages x 3).");
            string[] codes = { "4-12", "6-12" };
            long[] floor = { 285, 426 };
            for (int q = 0; q < codes.Length; q++)
            {
                string code = codes[q];
                int idx = WhaleStageIndex(db, code);
                int ch = code[0] - '0';
                long committed = WhaleCommittedAt(db, code);
                Line("  " + code + ":");
                WhaleRelicLine("non", A, idx);
                WhaleRelicLine("spender", B, idx);
                WhaleRelicLine("whale", C, idx);
                string[] who = { "non", "spender", "whale" };
                double[] bought = { 0, rowB[ch].DustPerDay, rowC[ch].DustPerDay };
                Line("    days per step                  Legendary t3 (20,000) | Mythic t2 (40,000) | Mythic t3 (80,000) | every relic to tier 3 from none (560,000)");
                for (int w = 0; w < 3; w++)
                {
                    Line("      " + who[w].PadRight(8) + " floor     " + Steps(floor[q] + bought[w]));
                    Line("      " + who[w].PadRight(8) + " committed " + Steps(committed + bought[w]));
                }
            }
        }

        private static string Steps(double perDay)
        {
            return (F1(20000 / perDay) + " | " + F1(40000 / perDay) + " | " + F1(80000 / perDay) + " | " + F1(560000 / perDay)).PadRight(34) + " (" + ((long)perDay) + " Dust a day)";
        }

        private static void WhaleRelicLine(string label, List<WhaleWalkData> walks, int idx)
        {
            List<int> owned = new List<int>(), tiers = new List<int>();
            List<double> day = new List<double>();
            for (int k = 0; k < walks.Count; k++)
            {
                WhaleWalkData d = walks[k];
                if (idx >= d.Stages.Count) continue;
                long[] r = d.RelicsAt(d.EntryT[idx]);
                owned.Add((int)r[1]); tiers.Add((int)r[2]);
                day.Add((d.EntryT[idx] - d.Start) / 86400.0);
            }
            List<double> sat = new List<double>();
            int never = 0;
            for (int k = 0; k < walks.Count; k++)
            {
                WhaleWalkData d = walks[k];
                double found = -1;
                for (int i = 0; i < d.Relics.Count; i++)
                    if (d.Relics[i][3] >= 15) { found = (d.Relics[i][0] - d.Start) / 86400.0; break; }
                if (found < 0) never++; else sat.Add(found);
            }
            Line("    " + label.PadRight(8) + " owned " + (owned.Count == 0 ? 0 : Median(owned)) + ", tier sum " + (tiers.Count == 0 ? 0 : Median(tiers)) + ", at walk day " + F1(MedianD(day))
                 + "; line saturated (worn tier sum 15) on walk day " + (sat.Count == 0 ? "never" : F1(MedianD(sat))) + " median (" + sat.Count + " of " + walks.Count + " walks reach it" + (never > 0 ? ", " + never + " never" : "") + ")");
        }
    }
}
