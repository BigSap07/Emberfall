using System;
using System.Collections.Generic;
using System.IO;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Save;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// THE LIVE EVENTS (R24, the events section of Docs/DESIGN-STORE.md): a weekly and a monthly event with quests,
    /// event points, a free track, a paid Event Track and one fixed pack. One named check per rule: the shipped
    /// file and its store offers, the quests counted at the game's own call sites, the points, both tracks, the paid
    /// track and the pack Sealed and refused by every random mechanic, the fixed contents, the pack once per event,
    /// the weekly and monthly rollover across clock changes, a wound back clock refused, the save, the file staying
    /// out of ContentVersion, the switch rolling back to today, and the wiring.
    ///
    /// The checks that need a small event of known size run on a throwaway database (LiveDb): the shipped
    /// store.json with a test live-events.json whose aspect is the starting heroes' own. The shared content object
    /// is never changed.
    /// </summary>
    internal static partial class Program
    {
        private static string _liveContentDir;

        private static void CheckLiveEvents(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("THE LIVE EVENTS (Docs/DESIGN-STORE.md: weekly and monthly quests, points, a free and a paid track, one pack per event, all Sealed when bought)");
            _liveContentDir = contentDir;
            bool was = LiveEventsSwitch.Enabled, storeWas = StoreSwitch.Enabled, loopWas = DailyLoopSwitch.Enabled;
            bool roadWas = WayfarersRoadSwitch.Enabled, contractWas = HeroContractSwitch.Enabled;
            try
            {
                LiveEventsSwitch.Enabled = true;
                StoreSwitch.Enabled = true;
                DailyLoopSwitch.Enabled = true;
                WayfarersRoadSwitch.Enabled = true;
                HeroContractSwitch.Enabled = false;
                CheckLiveContent(db, contentDir);
                CheckLiveQuests(db);
                CheckLiveTracks(db);
                CheckLivePack(db);
                CheckLivePurchasePath(db);
                CheckLivePaymentInFlight(db);
                CheckLiveSealedAndRefused(db);
                CheckLiveRollover(db);
                CheckLiveWoundBack(db);
                CheckLiveSave(db);
                CheckLiveNotHashed(db, contentDir);
                CheckLiveOff(db);
                CheckLiveNothingRandom(contentDir);
                CheckLiveWiring(contentDir);
            }
            finally
            {
                LiveEventsSwitch.Enabled = was;
                StoreSwitch.Enabled = storeWas;
                DailyLoopSwitch.Enabled = loopWas;
                WayfarersRoadSwitch.Enabled = roadWas;
                HeroContractSwitch.Enabled = contractWas;
            }
            Check(LiveEventsSwitch.Enabled == was && StoreSwitch.Enabled == storeWas && DailyLoopSwitch.Enabled == loopWas && WayfarersRoadSwitch.Enabled == roadWas && HeroContractSwitch.Enabled == contractWas,
                  "the switches are put back as the run found them (live events " + (was ? "on" : "off") + ")");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A throwaway database: the shipped content with a different live-events.json.</summary>
        private static ContentDatabase LiveDb(string liveJson)
        {
            ContentDatabase fresh = LoadContent(_liveContentDir);
            fresh.LoadLiveEvents(liveJson);
            return fresh;
        }

        /// <summary>A small test calendar of known size, on the starting heroes' own aspect so the aspect quest can be won.
        /// Weekly: two events that take turns in file order. t_week2 is listed first on purpose: the week in which monthly
        /// period 100 begins has turn index 1, which is the SECOND listed event, so t_week runs there and t_week2 the
        /// week after (CheckLiveRollover asserts it). Monthly: one.</summary>
        private static string LiveTestJson(ContentDatabase db)
        {
            string asp = db.StartingHeroes[0].Aspect.ToString();
            string t = "{ 'epoch': '2026-01-05', 'monthlyDays': 28, 'events': ["
                + "{ 'id': 't_week2', 'slot': 'weekly', 'name': 'Second Test Week', 'aspect': 'Tide', 'quests': ["
                + "{ 'id': 'q2_stage', 'kind': 'stage', 'target': 1, 'points': 10 } ],"
                + "'tiers': [ { 'points': 10, 'free': { 'kind': 'slag', 'amount': 1 } } ] },"
                + "{ 'id': 't_week', 'slot': 'weekly', 'name': 'Test Week', 'aspect': '" + asp + "', 'quests': ["
                + "{ 'id': 'q_stage', 'kind': 'stage', 'target': 1, 'points': 5 },"
                + "{ 'id': 'q_aspect', 'kind': 'aspectWin', 'target': 1, 'minHeroes': 1, 'points': 5 },"
                + "{ 'id': 'q_aspect4', 'kind': 'aspectWin', 'target': 1, 'minHeroes': 4, 'points': 5 },"
                + "{ 'id': 'q_waves', 'kind': 'waves', 'target': 2, 'points': 5 },"
                + "{ 'id': 'q_level', 'kind': 'level', 'target': 1, 'points': 5 },"
                + "{ 'id': 'q_gold', 'kind': 'spendGold', 'target': 100, 'points': 5 },"
                + "{ 'id': 'q_orders', 'kind': 'claimOrder', 'target': 1, 'points': 5 },"
                + "{ 'id': 'q_proving', 'kind': 'proving', 'target': 1, 'points': 5 },"
                + "{ 'id': 'q_arena', 'kind': 'arena', 'target': 2, 'points': 5 },"
                + "{ 'id': 'q_summon', 'kind': 'summon', 'target': 1, 'points': 5 },"
                + "{ 'id': 'q_ascend', 'kind': 'ascend', 'target': 1, 'points': 5 } ],"
                + "'tiers': ["
                + "{ 'points': 10, 'free': { 'kind': 'hourglass', 'amount': 1 }, 'paid': { 'kind': 'hourglass', 'amount': 2 } },"
                + "{ 'points': 20, 'free': { 'kind': 'dust', 'amount': 1 }, 'paid': { 'kind': 'dust', 'amount': 2 } },"
                + "{ 'points': 30, 'free': { 'kind': 'slag', 'amount': 5 }, 'paid': { 'kind': 'slag', 'amount': 10 } },"
                + "{ 'points': 50, 'free': { 'kind': 'hourglass', 'amount': 2 }, 'paid': { 'kind': 'dust', 'amount': 3 } } ],"
                + "'pack': { 'name': 'Test Pack', 'hours': 12, 'dustSweeps': 6 } },"
                + "{ 'id': 't_month', 'slot': 'monthly', 'name': 'Test Month', 'aspect': 'Tide', 'quests': ["
                + "{ 'id': 'm_stage', 'kind': 'stage', 'target': 3, 'points': 10 },"
                + "{ 'id': 'm_level', 'kind': 'level', 'target': 1, 'points': 10 } ],"
                + "'tiers': ["
                + "{ 'points': 10, 'free': { 'kind': 'hourglass', 'amount': 1 }, 'paid': { 'kind': 'hourglass', 'amount': 3 } },"
                + "{ 'points': 20, 'free': { 'kind': 'dust', 'amount': 1 }, 'paid': { 'kind': 'dust', 'amount': 3 } } ],"
                + "'pack': { 'name': 'Test Month Pack', 'hours': 24, 'dustSweeps': 12 } } ] }";
            return t.Replace('\'', '"');
        }

        private static GameSession LiveSession(ContentDatabase d)
        {
            return new GameSession(d, GameSession.CreateNewProfile(d, LiveBase));
        }

        private const long LiveBase = 1760000000L;

        private static long LiveMonthStart(ContentDatabase d, long period)
        {
            return (d.LiveEvents.EpochDay + (period - 1) * d.LiveEvents.MonthlyDays) * 86400L + (long)d.Balance.DailyResetHourUtc * 3600L;
        }

        /// <summary>The first reward day of week n, as DailyLoop.WeekOf counts weeks.</summary>
        private static long LiveWeekStart(ContentDatabase d, long week)
        {
            return ((week - 1) * 7L - 3L) * 86400L + (long)d.Balance.DailyResetHourUtc * 3600L;
        }

        /// <summary>Ten minutes into the first day of monthly period 100, which is also the first day of a week.</summary>
        private static long LiveT0(ContentDatabase d) { return LiveMonthStart(d, 100) + 600L; }

        private static string LiveSnapshot(GameSession s) { return SaveSerializer.Serialize(s.Profile); }

        private static LiveQuestView LiveQ(LiveEventView v, string id)
        {
            for (int i = 0; i < v.Quests.Count; i++) if (v.Quests[i].Id == id) return v.Quests[i];
            return null;
        }

        private static int LiveProgress(GameSession s, LiveEventSlot slot, string id, long now)
        {
            LiveQuestView q = LiveQ(LiveEvents.View(s, slot, now), id);
            return q == null ? -1 : q.Progress;
        }

        /// <summary>Finishes every reachable quest of the weekly test event through the engine's own counters and claims
        /// each. The aspect4 quest cannot be won with three heroes, so the event ends on 50 points.</summary>
        private static void LiveFillWeekly(GameSession s, long now)
        {
            LiveEvents.Record(s, EventQuestKind.Stage, 1, now);
            LiveEvents.RecordStageWin(s, now);
            LiveEvents.Record(s, EventQuestKind.Waves, 2, now);
            LiveEvents.Record(s, EventQuestKind.Level, 1, now);
            LiveEvents.Record(s, EventQuestKind.SpendGold, 100, now);
            LiveEvents.Record(s, EventQuestKind.ClaimOrder, 1, now);
            LiveEvents.Record(s, EventQuestKind.Proving, 1, now);
            LiveEvents.Record(s, EventQuestKind.Arena, 2, now);
            LiveEvents.Record(s, EventQuestKind.Summon, 1, now);
            LiveEvents.Record(s, EventQuestKind.Ascend, 1, now);
            string[] ids = { "q_stage", "q_aspect", "q_aspect4", "q_waves", "q_level", "q_gold", "q_orders", "q_proving", "q_arena", "q_summon", "q_ascend" };
            for (int i = 0; i < ids.Length; i++) LiveEvents.ClaimQuest(s, LiveEventSlot.Weekly, ids[i], now);
        }

        // ------------------------------------------------------------------ 1. content

        private static void CheckLiveContent(ContentDatabase db, string contentDir)
        {
            LiveEventsConfig c = db.LiveEvents;
            Check(c.IsUsable && c.Warnings.Count == 0 && c.CountOf(LiveEventSlot.Weekly) >= 1 && c.CountOf(LiveEventSlot.Monthly) >= 1,
                  "the shipped live-events.json loads clean: " + c.Events.Count + " events (" + c.CountOf(LiveEventSlot.Weekly) + " weekly, " + c.CountOf(LiveEventSlot.Monthly) + " monthly), no warnings");

            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(c.EpochDay);
            Check(epoch.DayOfWeek == DayOfWeek.Monday && c.MonthlyDays % 7 == 0 && c.MonthlyDays == 28,
                  "the epoch (" + epoch.ToString("yyyy-MM-dd") + ") is a Monday and a monthly period is " + c.MonthlyDays + " days, whole weeks");

            bool tidy = true, reachable = true, packs = true, fixedNumbers = true;
            for (int i = 0; i < c.Events.Count; i++)
            {
                LiveEventDef e = c.Events[i];
                if (e.Quests.Count < 6 || e.Tiers.Count < 6) tidy = false;
                int last = 0;
                for (int t = 0; t < e.Tiers.Count; t++)
                {
                    if (e.Tiers[t].Points <= last) tidy = false;
                    last = e.Tiers[t].Points;
                    if (e.Tiers[t].Free == null || e.Tiers[t].Paid == null) tidy = false;
                    if (e.Tiers[t].Free != null && !(e.Tiers[t].Free.Amount > 0.0)) fixedNumbers = false;
                    if (e.Tiers[t].Paid != null && !(e.Tiers[t].Paid.Amount > 0.0)) fixedNumbers = false;
                }
                if (last > e.TotalQuestPoints) reachable = false;
                if (e.Pack == null || e.Pack.Hours + e.Pack.DustSweeps <= 0.0) packs = false;
            }
            Check(tidy && reachable && fixedNumbers, "every shipped event has quests, tiers in rising order each with a free and a paid reward, a top tier its quests can reach, and only fixed positive amounts");
            Check(packs, "every shipped event has one fixed pack (hours and sweeps, no hero, no roll)");

            LiveEventDef w = c.EventFor(LiveEventSlot.Weekly, 1000), m = c.EventFor(LiveEventSlot.Monthly, 1000);
            Check(w != null && m != null && w.Aspect != m.Aspect, "one example of each kind is themed on an aspect: " + w.Name + " (" + w.Aspect + ") and " + m.Name + " (" + m.Aspect + ")");
            bool wQuestKinds = false;
            List<EventQuestKind> kinds = new List<EventQuestKind>();
            for (int i = 0; i < w.Quests.Count; i++) if (!kinds.Contains(w.Quests[i].Kind)) kinds.Add(w.Quests[i].Kind);
            wQuestKinds = kinds.Contains(EventQuestKind.Stage) && kinds.Contains(EventQuestKind.AspectWin) && kinds.Contains(EventQuestKind.SpendGold)
                          && kinds.Contains(EventQuestKind.ClaimOrder) && kinds.Contains(EventQuestKind.Proving);
            Check(wQuestKinds, "the weekly quests cover clearing stages, winning with the aspect, spending Gold, claiming orders and the Proving");

            // The store sells them: four offers, the right prices, kind event.
            string[] ids = { "event_track_weekly", "event_track_monthly", "event_pack_weekly", "event_pack_monthly" };
            double[] eur = { 4.99, 9.99, 9.99, 9.99 };
            bool offers = true;
            for (int i = 0; i < ids.Length; i++)
            {
                StoreOfferDef d = db.Store.Offer(ids[i]);
                if (d == null || d.Kind != StoreOfferKind.Event || Math.Abs(d.PriceEur - eur[i]) > 0.001) offers = false;
            }
            Check(offers && db.Store.Warnings.Count == 0, "store.json sells the weekly track at EUR 4.99, the monthly track at EUR 9.99 and a pack of each kind at EUR 9.99");

            // Two weekly events take turns, one per period, in file order.
            ContentDatabase d2 = LiveDb(LiveTestJson(db));
            LiveEventDef a = d2.LiveEvents.EventFor(LiveEventSlot.Weekly, 10), b = d2.LiveEvents.EventFor(LiveEventSlot.Weekly, 11), a2 = d2.LiveEvents.EventFor(LiveEventSlot.Weekly, 12);
            Check(d2.LiveEvents.IsUsable && d2.LiveEvents.Warnings.Count == 0 && a != null && b != null && a.Id != b.Id && a2.Id == a.Id,
                  "events of a slot take turns, one per period, in file order (" + a.Id + ", " + b.Id + ", " + a2.Id + ")");

            // Bad rows are dropped with a warning, never guessed.
            string bad = "{ 'epoch': '2026-01-05', 'events': ["
                + "{ 'id': 'x1', 'slot': 'daily', 'quests': [], 'tiers': [] },"
                + "{ 'id': 'x2', 'slot': 'weekly', 'quests': [ { 'id': 'q', 'kind': 'roll', 'target': 1, 'points': 1 } ], 'tiers': [ { 'points': 1, 'free': { 'kind': 'hourglass', 'amount': 1 } } ] },"
                + "{ 'id': 'x3', 'slot': 'weekly', 'quests': [ { 'id': 'q', 'kind': 'stage', 'target': 1, 'points': 1 } ], 'tiers': [ { 'points': 1, 'free': { 'kind': 'summon', 'amount': 1 } } ] } ] }";
            ContentDatabase d3 = LiveDb(bad.Replace('\'', '"'));
            Check(!d3.LiveEvents.IsUsable && d3.LiveEvents.Events.Count == 0 && d3.LiveEvents.Warnings.Count >= 3,
                  "AN UNKNOWN SLOT, QUEST KIND OR REWARD KIND IS DROPPED with a warning (" + d3.LiveEvents.Warnings.Count + "), so a roll or a summon cannot be written into the file");
            Check(!LiveEventsConfig.FromJson(null).IsUsable && !LiveEventsConfig.FromJson("{ not json").IsUsable, "a missing or damaged file runs no event");

            // A store offer of kind event with an unknown part is dropped.
            ContentDatabase d4 = LoadContent(contentDir);
            d4.LoadStore("{ 'tiers': { 'mid': 4.99 }, 'offers': [ { 'id': 'e1', 'kind': 'event', 'tier': 'mid', 'productId': 'placeholder.e1', 'eventSlot': 'weekly', 'eventPart': 'jackpot' } ] }".Replace('\'', '"'));
            Check(d4.Store.Offers.Count == 0 && d4.Store.Warnings.Count >= 1, "a store offer of kind event with an unknown part is dropped with a warning");
        }

        // ------------------------------------------------------------------ 2. quests

        private static void CheckLiveQuests(ContentDatabase db)
        {
            ContentDatabase d = LiveDb(LiveTestJson(db));
            GameSession s = LiveSession(d);
            long t0 = LiveT0(d);
            string pristine = LiveSnapshot(s);

            LiveEventView v0 = LiveEvents.View(s, LiveEventSlot.Weekly, t0);
            bool allZero = true;
            for (int i = 0; i < v0.Quests.Count; i++) if (v0.Quests[i].Progress != 0 || v0.Quests[i].CanClaim) allZero = false;
            Check(v0.Active && v0.EventId == "t_week" && v0.Quests.Count == 11 && allZero && v0.Points == 0 && v0.MaxPoints == 50 && v0.DaysLeft == 7,
                  "the weekly view lists " + v0.Quests.Count + " quests at zero, 0 of " + v0.MaxPoints + " points and " + v0.DaysLeft + " days left");
            Check(!LiveEvents.AnythingToClaim(s, t0) && LiveSnapshot(s) == pristine && s.Profile.LiveEvents.IsEmpty,
                  "SEEING THE EVENTS WRITES NOTHING: the view and the Hub's dot read the period without rolling it, and the save is byte for byte as it was");

            // ---- a stage cleared through the game's own wave resolver: stage, waves and the aspect quest all count
            StageDef first = d.AllStages[0];
            int resolves = 0;
            bool cleared = false;
            while (!cleared && resolves < 60)
            {
                WaveResolution res = s.ResolveWave(first, 7100 + resolves * 101, t0 + resolves);
                resolves++;
                if (res.StageCleared) cleared = true;
            }
            Check(cleared && LiveProgress(s, LiveEventSlot.Weekly, "q_stage", t0 + 100) == 1 && LiveProgress(s, LiveEventSlot.Weekly, "q_waves", t0 + 100) == 2,
                  "STAGES AND WAVES COUNT: clearing " + first.DisplayCode + " moves 'win a stage' to " + LiveProgress(s, LiveEventSlot.Weekly, "q_stage", t0 + 100) + " of 1 and 'clear 2 waves' to " + LiveProgress(s, LiveEventSlot.Weekly, "q_waves", t0 + 100) + " of 2");
            Check(LiveProgress(s, LiveEventSlot.Weekly, "q_aspect", t0 + 100) == 1 && LiveProgress(s, LiveEventSlot.Weekly, "q_aspect4", t0 + 100) == 0,
                  "AN ASPECT WIN COUNTS ONLY WITH ENOUGH HEROES OF THE ASPECT: the starting team holds " + HeroesOfAspectForCheck(s, d.StartingHeroes[0].Aspect) + " of " + d.StartingHeroes[0].Aspect + ", so the 1 hero quest is done and the 4 hero quest is not");

            // ---- a level bought: levels and Gold spent; a refused one counts nothing
            HeroInstance hero = s.Profile.Heroes[0];
            s.Profile.Gold = 0;
            int levelBefore = LiveProgress(s, LiveEventSlot.Weekly, "q_level", t0 + 200), goldBefore = LiveProgress(s, LiveEventSlot.Weekly, "q_gold", t0 + 200);
            LevelUpResult broke = s.LevelUpHero(hero, 1, t0 + 200);
            Check(!broke.Success && LiveProgress(s, LiveEventSlot.Weekly, "q_level", t0 + 200) == levelBefore && LiveProgress(s, LiveEventSlot.Weekly, "q_gold", t0 + 200) == goldBefore,
                  "a level-up the Gold cannot pay for counts nothing");
            s.Profile.Gold = 1000000000L;
            LevelUpResult bought = s.LevelUpHero(hero, 2, t0 + 201);
            Check(bought.Success && LiveProgress(s, LiveEventSlot.Weekly, "q_level", t0 + 201) == 1 && LiveProgress(s, LiveEventSlot.Weekly, "q_gold", t0 + 201) == 100,
                  "A LEVEL-UP COUNTS ITS LEVELS AND ITS GOLD: " + bought.LevelsGained + " levels for " + bought.GoldSpent + " Gold finish 'buy a level' and 'spend 100 Gold'");

            // ---- a daily order claimed, through the daily loop's own claim
            LoopSetBoard(s, t0 + 300, "level_3");
            DailyLoop.Record(s, OrderKind.Level, 3, t0 + 300);
            LoopPayout claimed = DailyLoop.ClaimOrder(s, "level_3", t0 + 301);
            Check(claimed.Success && LiveProgress(s, LiveEventSlot.Weekly, "q_orders", t0 + 301) == 1, "A CLAIMED ORDER COUNTS: claiming a daily order finishes 'claim an order'");

            // ---- the Proving counts as a Proving fight and as an arena fight, the Recital as an arena fight only
            s.GrantProvingResult(false, t0 + 400);
            Check(LiveProgress(s, LiveEventSlot.Weekly, "q_proving", t0 + 400) == 1 && LiveProgress(s, LiveEventSlot.Weekly, "q_arena", t0 + 400) == 1,
                  "A PROVING FIGHT COUNTS, lost or won: 'fight in the Proving' reads 1 of 1 and 'fight twice' 1 of 2");
            s.GrantRecitalResult(1, false, t0 + 401);
            Check(LiveProgress(s, LiveEventSlot.Weekly, "q_proving", t0 + 401) == 1 && LiveProgress(s, LiveEventSlot.Weekly, "q_arena", t0 + 401) == 2,
                  "A RECITAL FIGHT COUNTS AS AN ARENA FIGHT ONLY: the Proving quest stays at 1 and 'fight twice' reaches 2");

            // ---- a summon; a refused one counts nothing
            s.Profile.Shards = 0;
            s.Summon(9301, t0 + 500);
            Check(LiveProgress(s, LiveEventSlot.Weekly, "q_summon", t0 + 500) == 0, "a summon the Shards cannot pay for counts nothing");
            s.Profile.Shards = d.Balance.SummonCost;
            SummonOutcome summoned = s.Summon(9302, t0 + 501);
            Check(summoned.Success && LiveProgress(s, LiveEventSlot.Weekly, "q_summon", t0 + 501) == 1, "A SUMMON COUNTS");

            // ---- an ascension, through the game's own Ascend with the clock handed in
            HeroDef five = null;
            for (int i = 0; i < d.Heroes.Count && five == null; i++) if (d.Heroes[i].Stars == 5 && s.Profile.FindHeroByDefId(d.Heroes[i].Id) == null) five = d.Heroes[i];
            HeroInstance star = s.GrantHeroDirect(five, t0 + 600);
            star.Copies = 1 + Math.Max(1, StatMath.AscensionCopiesFor(d.Balance, star.Ascension));
            int ascBefore = LiveProgress(s, LiveEventSlot.Weekly, "q_ascend", t0 + 600);
            AscensionResult asc = s.Ascend(star, t0 + 601);
            Check(ascBefore == 0 && asc.Success && LiveProgress(s, LiveEventSlot.Weekly, "q_ascend", t0 + 602) == 1, "AN ASCENSION COUNTS: 'ascend a hero' reads 1 of 1");
            AscensionResult refusedAsc = s.Ascend(star, t0 + 603);
            Check(!refusedAsc.Success && LiveProgress(s, LiveEventSlot.Weekly, "q_ascend", t0 + 603) == 1, "an ascension that is refused counts nothing and the quest stays capped at its target");

            // ---- capped at the target
            LiveEvents.Record(s, EventQuestKind.Stage, 50, t0 + 700);
            Check(LiveProgress(s, LiveEventSlot.Weekly, "q_stage", t0 + 700) == 1, "progress is capped at the target");

            // ---- claims
            string before = LiveSnapshot(s);
            LiveResult notDone = LiveEvents.ClaimQuest(s, LiveEventSlot.Weekly, "q_aspect4", t0 + 800);
            LiveResult unknown = LiveEvents.ClaimQuest(s, LiveEventSlot.Weekly, "q_nothing", t0 + 800);
            Check(!notDone.Success && notDone.Reason == LiveRefusal.NotReached && !unknown.Success && unknown.Reason == LiveRefusal.Unknown && LiveSnapshot(s) == before,
                  "an unfinished quest and an unknown quest are refused and write nothing");
            Check(LiveEvents.AnythingToClaim(s, t0 + 800), "with finished quests unclaimed the Hub's dot is lit");
            string[] ids = { "q_stage", "q_aspect", "q_waves", "q_level", "q_gold", "q_orders", "q_proving", "q_arena", "q_summon", "q_ascend" };
            int total = 0;
            bool allPaid = true;
            for (int i = 0; i < ids.Length; i++)
            {
                LiveResult r = LiveEvents.ClaimQuest(s, LiveEventSlot.Weekly, ids[i], t0 + 801);
                if (!r.Success || r.Points != 5) allPaid = false;
                total += r.Points;
            }
            LiveEventView v = LiveEvents.View(s, LiveEventSlot.Weekly, t0 + 802);
            Check(allPaid && total == 50 && v.Points == 50, "CLAIMING A QUEST PAYS ITS EVENT POINTS: ten finished quests pay " + total + " and the event reads " + v.Points + " of " + v.MaxPoints);
            string afterClaims = LiveSnapshot(s);
            LiveResult again = LiveEvents.ClaimQuest(s, LiveEventSlot.Weekly, "q_stage", t0 + 803);
            Check(!again.Success && again.Reason == LiveRefusal.Claimed && LiveSnapshot(s) == afterClaims, "A QUEST PAYS ONCE: a second claim is refused and writes nothing");
            LiveEvents.Record(s, EventQuestKind.Stage, 1, t0 + 804);
            Check(LiveProgress(s, LiveEventSlot.Weekly, "q_stage", t0 + 805) == 1 && LiveSnapshot(s) == afterClaims, "and nothing counts toward a quest already claimed");

            // ---- an action no quest counts writes nothing: the monthly test event has no Ascend quest
            Check(s.Profile.LiveEvents.Monthly.Period == 0 || LiveProgress(s, LiveEventSlot.Monthly, "m_stage", t0 + 900) >= 0, "the monthly slot has its own quest list");
            GameSession lone = LiveSession(d);
            LiveEvents.Record(lone, EventQuestKind.Ascend, 1, t0);
            Check(lone.Profile.LiveEvents.Monthly.IsEmpty && !lone.Profile.LiveEvents.Weekly.IsEmpty, "an action only one slot counts writes only that slot");
            GameSession untouched = LiveSession(d);
            LiveEvents.Record(untouched, EventQuestKind.Arena, 0, t0);
            LiveEvents.Record(untouched, EventQuestKind.Arena, -3, t0);
            Check(untouched.Profile.LiveEvents.IsEmpty, "a zero or negative amount counts nothing and writes nothing");
        }

        private static int HeroesOfAspectForCheck(GameSession s, Aspect aspect)
        {
            int n = 0;
            List<HeroInstance> team = s.Profile.TeamHeroes();
            for (int i = 0; i < team.Count; i++)
            {
                HeroDef def = s.Content.GetHero(team[i].DefId);
                if (def != null && def.Aspect == aspect) n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ 3. both tracks

        private static void CheckLiveTracks(ContentDatabase db)
        {
            ContentDatabase d = LiveDb(LiveTestJson(db));
            long t0 = LiveT0(d);

            // ---- a tier not reached is refused
            GameSession s = LiveSession(d);
            LiveEvents.Record(s, EventQuestKind.Stage, 1, t0);
            string before = LiveSnapshot(s);
            LiveResult early = LiveEvents.ClaimFree(s, LiveEventSlot.Weekly, 1, t0 + 5);
            Check(!early.Success && early.Reason == LiveRefusal.NotReached && LiveSnapshot(s) == before, "A TIER NOT REACHED IS REFUSED on the free track and writes nothing");
            LiveResult none = LiveEvents.ClaimFree(s, LiveEventSlot.Weekly, 9, t0 + 5);
            Check(!none.Success && none.Reason == LiveRefusal.Unknown, "a tier that does not exist is refused");

            // ---- the free track pays plain earned goods
            LiveFillWeekly(s, t0 + 10);
            LiveEventView v = LiveEvents.View(s, LiveEventSlot.Weekly, t0 + 11);
            Check(v.Points == 50 && v.Tiers.Count == 4 && v.Tiers[3].Reached && v.Tiers[3].FreeCanClaim && !v.Tiers[3].PaidCanClaim && v.AnythingToClaim,
                  "with 50 points all four tiers are reached; the free ones can be claimed and the paid ones cannot (the track is not bought)");
            long gold0 = s.Profile.Gold, shards0 = s.Profile.Shards, dust0 = s.Profile.RelicDust, slag0 = s.Profile.Slag;
            IdleReport hour = s.IdleYield(3600L);
            LiveResult f1 = LiveEvents.ClaimFree(s, LiveEventSlot.Weekly, 1, t0 + 12);
            Check(f1.Success && s.Profile.Gold - gold0 == hour.Gold && s.Profile.Shards - shards0 == hour.Shards && f1.Gold == hour.Gold,
                  "FREE TIER 1 PAYS ONE HOUR OF IDLE INCOME: " + f1.Gold + " Gold and " + f1.Shards + " Shards, as the idle formula says");
            LiveResult f2 = LiveEvents.ClaimFree(s, LiveEventSlot.Weekly, 2, t0 + 13);
            long sweep = WayfarersRoad.DustPerSweep(s);
            Check(f2.Success && s.Profile.RelicDust - dust0 == sweep, "FREE TIER 2 PAYS ONE SWEEP OF DUST: " + (s.Profile.RelicDust - dust0) + " Dust = one repeat clear of the frontier stage");
            LiveResult f3 = LiveEvents.ClaimFree(s, LiveEventSlot.Weekly, 3, t0 + 14);
            Check(f3.Success && s.Profile.Slag - slag0 == 5, "FREE TIER 3 PAYS 5 SLAG");
            LiveResult f4 = LiveEvents.ClaimFree(s, LiveEventSlot.Weekly, 4, t0 + 15);
            Check(f4.Success && s.Profile.Sealed.Gold == 0 && s.Profile.Sealed.Shards == 0 && s.Profile.Sealed.Slag == 0 && s.Profile.FreeSlag == s.Profile.Slag,
                  "THE FREE TRACK IS EARNED, NEVER SEALED: after four claims no Gold, Shard or Slag is marked");
            string afterFree = LiveSnapshot(s);
            LiveResult dup = LiveEvents.ClaimFree(s, LiveEventSlot.Weekly, 1, t0 + 16);
            Check(!dup.Success && dup.Reason == LiveRefusal.Claimed && LiveSnapshot(s) == afterFree, "A FREE TIER PAYS ONCE: the second claim is refused and writes nothing");

            // ---- the paid track is locked until the Event Track is bought
            string lockedBefore = LiveSnapshot(s);
            LiveResult locked = LiveEvents.ClaimPaid(s, LiveEventSlot.Weekly, 1, t0 + 20);
            Check(!locked.Success && locked.Reason == LiveRefusal.PaidLocked && LiveSnapshot(s) == lockedBefore, "THE PAID TRACK IS LOCKED until the Event Track is bought: the claim is refused and writes nothing");
            Check(LiveEvents.View(s, LiveEventSlot.Weekly, t0 + 21).TrackCanBuy && !LiveEvents.View(s, LiveEventSlot.Weekly, t0 + 21).TrackOwned, "and the view offers the track for sale");

            // ---- buy it through the store's own direct fulfilment, then claim every paid tier
            StoreFulfilment bought = StoreOffers.Fulfil(s, "event_track_weekly", null, t0 + 22);
            Check(bought.Success && s.Profile.LiveEvents.Weekly.TrackBought && LiveEvents.View(s, LiveEventSlot.Weekly, t0 + 23).TrackOwned, "THE EVENT TRACK IS BOUGHT through the store path and the event remembers it");
            StoreFulfilment twice = StoreOffers.Fulfil(s, "event_track_weekly", null, t0 + 24);
            Check(!twice.Success && twice.Reason == StoreRefusal.SoldOut, "the track is sold once per event: a second purchase is refused SoldOut");
            Check(LiveEvents.View(s, LiveEventSlot.Weekly, t0 + 25).Tiers[0].PaidCanClaim, "with the track bought the reached paid tiers can be claimed");

            long g1 = s.Profile.Gold, sg1 = s.Profile.Sealed.Gold, dust1 = s.Profile.RelicDust, slag1 = s.Profile.Slag, sslag1 = s.Profile.Sealed.Slag, free1 = s.Profile.FreeGold, freeSlag1 = s.Profile.FreeSlag;
            IdleReport twoHours = s.IdleYield(7200L);
            LiveResult p1 = LiveEvents.ClaimPaid(s, LiveEventSlot.Weekly, 1, t0 + 30);
            Check(p1.Success && s.Profile.Gold - g1 == twoHours.Gold && s.Profile.Sealed.Gold - sg1 == twoHours.Gold && s.Profile.FreeGold == free1,
                  "PAID TIER 1 PAYS TWO HOURS OF IDLE GOLD AS SEALED GOLD: " + (s.Profile.Gold - g1) + " Gold arrives and all of it is marked Sealed; the Gold a random mechanic may spend did not move");
            LiveResult p2 = LiveEvents.ClaimPaid(s, LiveEventSlot.Weekly, 2, t0 + 31);
            Check(p2.Success && s.Profile.RelicDust - dust1 == 2 * sweep, "PAID TIER 2 PAYS TWO SWEEPS OF DUST");
            LiveResult p3 = LiveEvents.ClaimPaid(s, LiveEventSlot.Weekly, 3, t0 + 32);
            Check(p3.Success && s.Profile.Slag - slag1 == 10 && s.Profile.Sealed.Slag - sslag1 == 10 && s.Profile.FreeSlag == freeSlag1,
                  "PAID TIER 3 PAYS 10 SEALED SLAG: the Slag a reroll or a combine may spend did not move");
            LiveResult p4 = LiveEvents.ClaimPaid(s, LiveEventSlot.Weekly, 4, t0 + 33);
            Check(p4.Success && s.Profile.RelicDust - dust1 == 5 * sweep, "PAID TIER 4 PAYS THREE SWEEPS OF DUST");
            string afterPaid = LiveSnapshot(s);
            LiveResult dupPaid = LiveEvents.ClaimPaid(s, LiveEventSlot.Weekly, 1, t0 + 34);
            Check(!dupPaid.Success && dupPaid.Reason == LiveRefusal.Claimed && LiveSnapshot(s) == afterPaid, "A PAID TIER PAYS ONCE");
            LiveEventView done = LiveEvents.View(s, LiveEventSlot.Weekly, t0 + 35);
            Check(!done.AnythingToClaim && LiveEvents.AnythingToClaim(s, t0 + 35) == LiveEvents.View(s, LiveEventSlot.Monthly, t0 + 35).AnythingToClaim,
                  "with everything claimed the weekly slot has nothing to claim, and the Hub's dot follows the monthly slot alone (its level quest is finished and unclaimed)");

            // ---- a paid tier not reached is refused even on a bought track
            GameSession low = LiveSession(d);
            LiveEvents.Record(low, EventQuestKind.Stage, 1, t0);
            LiveEvents.ClaimQuest(low, LiveEventSlot.Weekly, "q_stage", t0 + 1);
            StoreOffers.Fulfil(low, "event_track_weekly", null, t0 + 2);
            string lowBefore = LiveSnapshot(low);
            LiveResult notYet = LiveEvents.ClaimPaid(low, LiveEventSlot.Weekly, 2, t0 + 3);
            Check(!notYet.Success && notYet.Reason == LiveRefusal.NotReached && LiveSnapshot(low) == lowBefore, "a paid tier whose points are not reached is refused even with the track bought");

            // ---- the paid track needs the Store
            StoreSwitch.Enabled = false;
            GameSession noStore = LiveSession(d);
            LiveFillWeekly(noStore, t0);
            noStore.Profile.LiveEvents.Weekly.TrackBought = true;
            LiveResult storeOff = LiveEvents.ClaimPaid(noStore, LiveEventSlot.Weekly, 1, t0 + 1);
            StoreSwitch.Enabled = true;
            Check(!storeOff.Success && storeOff.Reason == LiveRefusal.PaidLocked, "with the Store switch off the paid track pays nothing");
        }

        // ------------------------------------------------------------------ 4. the pack

        private static void CheckLivePack(ContentDatabase db)
        {
            ContentDatabase d = LiveDb(LiveTestJson(db));
            long t0 = LiveT0(d);
            GameSession s = LiveSession(d);
            string pristine = LiveSnapshot(s);

            LiveEventView v = LiveEvents.View(s, LiveEventSlot.Weekly, t0);
            Check(v.PackOfferId == "event_pack_weekly" && v.PackPrice == "EUR 9.99" && v.PackCanBuy && v.PackLines.Count >= 3
                  && v.PackLines[0].Contains("12 hours") && v.PackLines[0].Contains("Sealed") && v.PackLines[1].Contains("6 sweeps"),
                  "THE PACK'S CONTENTS ARE FIXED AND SHOWN IN FULL WITH THE PRICE BEFORE IT IS BOUGHT: " + string.Join(" / ", v.PackLines.ToArray()) + " at " + v.PackPrice);
            Check(LiveSnapshot(s) == pristine, "and reading them writes nothing");

            GameSession twin = LiveSession(d);
            long gold0 = s.Profile.Gold, sealed0 = s.Profile.Sealed.Gold, dust0 = s.Profile.RelicDust, freeGold0 = s.Profile.FreeGold, shards0 = s.Profile.Shards;
            long hoursGold = s.IdleYield(12 * 3600L).Gold, sweep = WayfarersRoad.DustPerSweep(s);
            StoreFulfilment f = StoreOffers.Fulfil(s, "event_pack_weekly", null, t0 + 10);
            Check(f.Success && s.Profile.Gold - gold0 == hoursGold && s.Profile.Sealed.Gold - sealed0 == hoursGold && s.Profile.RelicDust - dust0 == 6 * sweep,
                  "THE PACK PAYS WHAT IT SHOWED: " + hoursGold + " Sealed Gold (12 hours of idle income) and " + (s.Profile.RelicDust - dust0) + " Dust (6 sweeps)");
            Check(s.Profile.FreeGold == freeGold0 && s.Profile.Shards == shards0 && !s.Profile.LiveEvents.Weekly.TrackBought && s.Profile.LiveEvents.Weekly.PackBought,
                  "and none of it is Gold or Shards a random mechanic may spend, the pack does not unlock the track, and the event remembers the pack");

            StoreFulfilment again = StoreOffers.Fulfil(s, "event_pack_weekly", null, t0 + 11);
            string afterOne = LiveSnapshot(s);
            Check(!again.Success && again.Reason == StoreRefusal.SoldOut, "THE PACK IS ONCE PER EVENT: a second purchase is refused SoldOut");
            StoreFulfilment lateWeek = StoreOffers.Fulfil(s, "event_pack_weekly", null, t0 + 6 * 86400L);
            Check(!lateWeek.Success && lateWeek.Reason == StoreRefusal.SoldOut && LiveSnapshot(s) == afterOne, "and still refused on the last day of the same week");
            LiveEventView after = LiveEvents.View(s, LiveEventSlot.Weekly, t0 + 12);
            Check(!after.PackCanBuy && after.PackOwned, "the view says the pack is owned");

            // ---- the monthly pack is its own, once per month
            StoreFulfilment m = StoreOffers.Fulfil(s, "event_pack_monthly", null, t0 + 13);
            StoreFulfilment mWeekLater = StoreOffers.Fulfil(s, "event_pack_monthly", null, t0 + 8 * 86400L);
            Check(m.Success && !mWeekLater.Success && mWeekLater.Reason == StoreRefusal.SoldOut, "THE MONTHLY PACK IS ONCE PER MONTHLY EVENT: bought on day one, refused a week later although the weekly event has turned over");

            // ---- an event with no pack sells none
            ContentDatabase noPack = LiveDb(LiveTestJson(db).Replace("'", "\"").Replace("\"pack\": { \"name\": \"Test Pack\", \"hours\": 12, \"dustSweeps\": 6 }", "\"x\": 0"));
            GameSession np = LiveSession(noPack);
            StoreFulfilment none = StoreOffers.Fulfil(np, "event_pack_weekly", null, t0);
            Check(noPack.LiveEvents.Event("t_week").Pack == null && !none.Success && none.Reason == StoreRefusal.NoEvent && np.Profile.LiveEvents.IsEmpty,
                  "an event with no pack in its data sells none: the offer is refused and nothing is written");

            // ---- deterministic: the same state and the same purchases give byte-identical saves
            GameSession a = LiveSession(d), b = LiveSession(d);
            string[] script = { "event_track_weekly", "event_pack_weekly", "event_track_monthly", "event_pack_monthly" };
            for (int i = 0; i < script.Length; i++) { StoreOffers.Fulfil(a, script[i], null, t0 + 40 + i); StoreOffers.Fulfil(b, script[i], null, t0 + 40 + i); }
            Check(LiveSnapshot(a) == LiveSnapshot(b) && a.Profile.LiveEvents.Weekly.PackBought && a.Profile.LiveEvents.Monthly.TrackBought,
                  "DETERMINISTIC: two sessions in the same state buying the same four offers hold byte-identical saves");
            Check(LiveSnapshot(twin) == pristine, "(control) a session nobody bought for is untouched");
        }

        // ------------------------------------------------------------------ 5. the real purchase path

        private static void CheckLivePurchasePath(ContentDatabase db)
        {
            ContentDatabase d = LiveDb(LiveTestJson(db));
            long t0 = LiveT0(d);
            GameSession s = LiveSession(d);
            StoreOffers.Bind(s);
            PurchaseLedger ledger = new PurchaseLedger(StoreRules.Shared);

            string pristine = LiveSnapshot(s);
            PurchaseResult begun = ledger.Begin(s, "event_track_weekly", null, t0);
            Check(begun.Success && ledger.IsPending("event_track_weekly") && LiveSnapshot(s) == pristine,
                  "A PENDING EVENT PURCHASE WRITES NOTHING: Begin remembers it in memory and the save is as it was");
            ledger.Abandon("event_track_weekly");
            Check(!ledger.IsPending("event_track_weekly") && LiveSnapshot(s) == pristine && s.Profile.LiveEvents.IsEmpty, "and an abandoned one grants nothing");

            ledger.Begin(s, "event_track_weekly", null, t0);
            PurchaseResult done = ledger.Complete(s, "event_track_weekly", "receipt-ev-1", t0);
            Check(done.Success && done.CanConfirm && s.Profile.LiveEvents.Weekly.TrackBought && s.Profile.Store.FindReceipt("receipt-ev-1") != null
                  && done.Grant != null && done.Grant.Lines.Count == 1 && done.Grant.Lines[0].Contains("Event Track"),
                  "COMPLETE GRANTS THE TRACK ONCE: the receipt is kept and the result says what was bought (" + (done.Grant != null ? string.Join(" / ", done.Grant.Lines.ToArray()) : "no grant") + ")");
            string afterOne = LiveSnapshot(s);
            PurchaseResult redelivered = ledger.Complete(s, "event_track_weekly", "receipt-ev-1", t0 + 1);
            Check(!redelivered.Success && redelivered.Reason == PurchaseRefusal.AlreadyGranted && LiveSnapshot(s) == afterOne, "ONCE PER RECEIPT: the same receipt delivered again grants nothing");
            PurchaseResult limited = ledger.Begin(s, "event_track_weekly", null, t0 + 2);
            Check(!limited.Success && limited.Reason == PurchaseRefusal.NotAvailable, "a second purchase of the track in the same event is refused at Begin");

            ledger.Begin(s, "event_pack_weekly", null, t0 + 3);
            long goldBefore = s.Profile.Sealed.Gold;
            PurchaseResult pack = ledger.Complete(s, "event_pack_weekly", "receipt-ev-2", t0 + 3);
            Check(pack.Success && s.Profile.LiveEvents.Weekly.PackBought && s.Profile.Sealed.Gold - goldBefore == s.IdleYield(12 * 3600L).Gold && pack.Grant.Lines.Count == 2,
                  "THE PACK COMES THROUGH THE SAME PATH, SEALED, with a receipt line for each part (" + string.Join(" / ", pack.Grant.Lines.ToArray()) + ")");
        }

        // ------------------------------------------------------------------ 5b. a payment in flight across a turnover

        /// <summary>One purchase of an event offer through the real ledger: begun at beginAt, delivered at deliverAt.</summary>
        private static PurchaseResult LiveFlight(GameSession s, PurchaseLedger ledger, string offerId, string receipt, long beginAt, long deliverAt)
        {
            if (beginAt >= 0) ledger.Begin(s, offerId, null, beginAt);
            return ledger.Complete(s, offerId, receipt, deliverAt);
        }

        private static void CheckLivePaymentInFlight(ContentDatabase db)
        {
            ContentDatabase d = LiveDb(LiveTestJson(db));
            long t0 = LiveT0(d);
            long week = 7 * 86400L;
            // t_week (a pack, a paid track) runs at t0, t_week2 (no pack, no paid rewards) at t0 + 1 week, t_week again at + 2 weeks.

            // ---- begun and delivered in one period: normal, and the memory is gone afterwards
            GameSession a = LiveSession(d);
            StoreOffers.Bind(a);
            PurchaseLedger ledgerA = new PurchaseLedger(StoreRules.Shared);
            string pristine = LiveSnapshot(a);
            ledgerA.Begin(a, "event_pack_weekly", null, t0);
            Check(a.Profile.LiveEvents.Pending.Count == 1 && LiveSnapshot(a) == pristine,
                  "BEGINNING AN EVENT PURCHASE REMEMBERS THE EVENT IN MEMORY ONLY: the save is byte for byte as it was");
            long goldBefore = a.Profile.Sealed.Gold;
            PurchaseResult normal = ledgerA.Complete(a, "event_pack_weekly", "flight-1", t0 + 5);
            Check(normal.Success && normal.CanConfirm && a.Profile.LiveEvents.Weekly.PackBought && a.Profile.LiveEvents.Pending.Count == 0
                  && a.Profile.Sealed.Gold - goldBefore == a.IdleYield(12 * 3600L).Gold,
                  "BEGUN AND DELIVERED IN ONE PERIOD the pack is applied once, marked bought, and the memory is cleared");

            // ---- a pack paid for in week 1, delivered after the turnover into an event that has NO pack
            GameSession b = LiveSession(d);
            StoreOffers.Bind(b);
            PurchaseLedger ledgerB = new PurchaseLedger(StoreRules.Shared);
            long gB = b.Profile.Sealed.Gold, dB = b.Profile.RelicDust;
            long hours12 = b.IdleYield(12 * 3600L).Gold, sweeps6 = 6 * WayfarersRoad.DustPerSweep(b);
            PurchaseResult stalePack = LiveFlight(b, ledgerB, "event_pack_weekly", "flight-2", t0, t0 + week + 10);
            Check(stalePack.Success && stalePack.CanConfirm && b.Profile.Store.FindReceipt("flight-2") != null
                  && b.Profile.Sealed.Gold - gB == hours12 && b.Profile.RelicDust - dB == sweeps6 && !b.Profile.LiveEvents.Weekly.PackBought,
                  "A PACK PAID FOR BEFORE THE TURNOVER IS DELIVERED IN FULL AFTER IT, though the running event has no pack: " + (b.Profile.Sealed.Gold - gB) + " Sealed Gold and " + (b.Profile.RelicDust - dB)
                  + " Dust arrive, the payment is confirmed, and nothing is marked, so the running event is untouched");
            Check(stalePack.Grant != null && stalePack.Grant.Lines.Count == 3 && stalePack.Grant.Lines[2].Contains("paid out in full"),
                  "and the receipt says so (" + (stalePack.Grant != null ? string.Join(" / ", stalePack.Grant.Lines.ToArray()) : "") + ")");
            string settled = LiveSnapshot(b);
            PurchaseResult again = ledgerB.Complete(b, "event_pack_weekly", "flight-2", t0 + week + 20);
            Check(!again.Success && again.Reason == PurchaseRefusal.AlreadyGranted && again.CanConfirm && LiveSnapshot(b) == settled,
                  "NEVER TWICE: the same receipt delivered again grants nothing, writes nothing and is confirmed so the store stops");

            // ---- the same payment delivered after a restart (no memory of the event): the running event has no pack, so the first event that has one is delivered
            GameSession c = LiveSession(d);
            StoreOffers.Bind(c);
            PurchaseLedger ledgerC = new PurchaseLedger(StoreRules.Shared);
            long gC = c.Profile.Sealed.Gold;
            PurchaseResult restart = ledgerC.Complete(c, "event_pack_weekly", "flight-3", t0 + week + 10);
            Check(restart.Success && restart.CanConfirm && c.Profile.Sealed.Gold - gC == hours12 && !c.Profile.LiveEvents.Weekly.PackBought,
                  "A PAYMENT DELIVERED AFTER A RESTART with no memory of its event is still delivered, from the first event of the slot that has a pack");

            // ---- two payments for one event: the second is delivered in full too, and the event stays bought once
            GameSession e = LiveSession(d);
            StoreOffers.Bind(e);
            PurchaseLedger ledgerE = new PurchaseLedger(StoreRules.Shared);
            PurchaseResult first = LiveFlight(e, ledgerE, "event_pack_weekly", "flight-4", t0, t0 + 5);
            long gE = e.Profile.Sealed.Gold;
            PurchaseResult second = ledgerE.Complete(e, "event_pack_weekly", "flight-5", t0 + 6);
            Check(first.Success && second.Success && second.CanConfirm && e.Profile.Sealed.Gold - gE == hours12 && e.Profile.LiveEvents.Weekly.PackBought,
                  "TWO PAYMENTS FOR ONE EVENT: the second receipt is a charged payment and is delivered in full, the pack stays marked bought once");

            // ---- a track paid for in week 1 and delivered in week 2: the paid rewards paid out in full, the new event's track still for sale
            GameSession f = LiveSession(d);
            StoreOffers.Bind(f);
            PurchaseLedger ledgerF = new PurchaseLedger(StoreRules.Shared);
            long gF = f.Profile.Sealed.Gold, dF = f.Profile.RelicDust, slagF = f.Profile.Sealed.Slag, freeF = f.Profile.FreeGold;
            long paidHours = f.IdleYield(2 * 3600L).Gold, paidSweeps = 5 * WayfarersRoad.DustPerSweep(f);
            PurchaseResult staleTrack = LiveFlight(f, ledgerF, "event_track_weekly", "flight-6", t0, t0 + week + 10);
            Check(staleTrack.Success && staleTrack.CanConfirm && f.Profile.Sealed.Gold - gF == paidHours && f.Profile.RelicDust - dF == paidSweeps && f.Profile.Sealed.Slag - slagF == 10
                  && f.Profile.FreeGold == freeF && !f.Profile.LiveEvents.Weekly.TrackBought,
                  "A TRACK PAID FOR BEFORE THE TURNOVER PAYS ITS PAID REWARDS OUT IN FULL, Sealed (2 hours, 5 sweeps, 10 Slag, the totals the card showed), because its quests belong to an event that is over");
            Check(LiveEvents.PurchaseRefusal(f, LiveEventSlot.Weekly, LiveEventPart.Track, t0 + week + 11) == StoreRefusal.None,
                  "and the running event's own track is still for sale");

            // ---- the monthly slot
            GameSession g = LiveSession(d);
            StoreOffers.Bind(g);
            PurchaseLedger ledgerG = new PurchaseLedger(StoreRules.Shared);
            long gG = g.Profile.Sealed.Gold;
            long monthTwo = LiveMonthStart(d, 101) + 30;
            PurchaseResult month = LiveFlight(g, ledgerG, "event_pack_monthly", "flight-7", t0, monthTwo);
            Check(month.Success && month.CanConfirm && g.Profile.Sealed.Gold - gG == g.IdleYield(24 * 3600L).Gold && !g.Profile.LiveEvents.Monthly.PackBought,
                  "A MONTHLY PACK PAID FOR IN MONTH 100 AND DELIVERED IN MONTH 101 is delivered in full and the new month's pack stays for sale");

            // ---- every case above confirmed a charged payment, and every delivered good is a Sealed fixed line
            bool allConfirmed = stalePack.CanConfirm && restart.CanConfirm && second.CanConfirm && staleTrack.CanConfirm && month.CanConfirm && normal.CanConfirm;
            Check(allConfirmed && b.Profile.FreeGold == b.Profile.Gold - b.Profile.Sealed.Gold,
                  "NO CHARGED PAYMENT IS LEFT UNCONFIRMED, and everything delivered is Sealed (no Gold a random mechanic may spend arrived)");

            // ---- an event offer nothing in the file can deliver is still refused, and writes nothing
            ContentDatabase none = LiveDb(null);
            GameSession h = LiveSession(none);
            string hBefore = LiveSnapshot(h);
            PurchaseResult nothing = new PurchaseLedger(StoreRules.Shared).Complete(h, "event_pack_weekly", "flight-8", t0);
            Check(!nothing.Success && LiveSnapshot(h) == hBefore, "with no live-events.json there is nothing to deliver: refused, nothing written");
        }

        // ------------------------------------------------------------------ 6. Sealed and refused by every random path

        private static void CheckLiveSealedAndRefused(ContentDatabase db)
        {
            ContentDatabase d = LiveDb(LiveTestJson(db));
            long t0 = LiveT0(d);
            GameSession s = LiveSession(d);
            LiveFillWeekly(s, t0);
            // Everything free first, so the free part is the baseline the bought part must not touch.
            for (int tier = 1; tier <= 4; tier++) LiveEvents.ClaimFree(s, LiveEventSlot.Weekly, tier, t0 + 1);
            long freeGold = s.Profile.FreeGold, freeShards = s.Profile.FreeShards, freeSlag = s.Profile.FreeSlag, shards = s.Profile.Shards, summons = s.Profile.TotalSummons;
            int copies = 0;
            for (int i = 0; i < s.Profile.Heroes.Count; i++) copies += s.Profile.Heroes[i].Copies;
            long dust = s.Profile.RelicDust;

            StoreOffers.Fulfil(s, "event_track_weekly", null, t0 + 2);
            StoreOffers.Fulfil(s, "event_pack_weekly", null, t0 + 3);
            for (int tier = 1; tier <= 4; tier++) LiveEvents.ClaimPaid(s, LiveEventSlot.Weekly, tier, t0 + 4);
            int copiesAfter = 0;
            for (int i = 0; i < s.Profile.Heroes.Count; i++) copiesAfter += s.Profile.Heroes[i].Copies;

            Check(s.Profile.Sealed.Gold > 0 && s.Profile.FreeGold == freeGold && s.Profile.FreeShards == freeShards && s.Profile.FreeSlag == freeSlag && s.Profile.Shards == shards,
                  "A BASKET OF THE TRACK, ITS FOUR PAID TIERS AND THE PACK adds " + s.Profile.Sealed.Gold + " Sealed Gold and leaves the Gold, Shards and Slag a summon, a Conversion, a reroll or a combine may spend exactly where they were");
            Check(s.Profile.TotalSummons == summons && copiesAfter == copies, "and no hero copy arrives and no summon is counted: nothing bought can reach a random mechanic");
            Check(s.Profile.RelicDust > dust, "(Dust arrives, which only buys the relic shop's fixed items)");

            // A summon with the bought Gold alone is refused: Shards are what a summon spends, and none were granted.
            s.Profile.Shards = 0;
            SummonOutcome poor = s.Summon(5151, t0 + 5);
            Check(!poor.Success, "a summon with no Shards is refused however much bought Gold the purse holds");

            // The reward kinds are a closed set with no hero in it.
            string[] names = Enum.GetNames(typeof(EventRewardKind));
            Check(names.Length == 3 && Array.IndexOf(names, "Hourglass") >= 0 && Array.IndexOf(names, "Dust") >= 0 && Array.IndexOf(names, "Slag") >= 0,
                  "THE EVENT REWARD KINDS ARE A CLOSED SET OF THREE (hours of idle income, Dust, Slag): an event cannot pay a hero, a pull or a pool");
            string[] grantNames = Enum.GetNames(typeof(GrantKind));
            bool clean = true;
            for (int i = 0; i < grantNames.Length; i++)
                if (grantNames[i].IndexOf("Roll", StringComparison.OrdinalIgnoreCase) >= 0 || grantNames[i].IndexOf("Random", StringComparison.OrdinalIgnoreCase) >= 0
                    || grantNames[i].IndexOf("Summon", StringComparison.OrdinalIgnoreCase) >= 0 || grantNames[i].IndexOf("Pity", StringComparison.OrdinalIgnoreCase) >= 0
                    || grantNames[i].IndexOf("Pool", StringComparison.OrdinalIgnoreCase) >= 0) clean = false;
            PurchaseGrant track = d.Store.Offer("event_track_weekly").Built.Grant, packGrant = d.Store.Offer("event_pack_weekly").Built.Grant;
            Check(clean && track.Items.Count == 1 && track.Items[0].Kind == GrantKind.EventTrack && packGrant.Items.Count == 1 && packGrant.Items[0].Kind == GrantKind.EventPack
                  && track.Problem(d) == null && packGrant.Problem(d) == null,
                  "the track and the pack are each ONE fixed grant line of a kind that names no roll, and both validate");
        }

        // ------------------------------------------------------------------ 7. rollover

        private static void CheckLiveRollover(ContentDatabase db)
        {
            ContentDatabase d = LiveDb(LiveTestJson(db));
            long t0 = LiveT0(d);
            long w0 = LiveEvents.PeriodOf(LiveSession(d), LiveEventSlot.Weekly, t0);
            long m0 = LiveEvents.PeriodOf(LiveSession(d), LiveEventSlot.Monthly, t0);
            Check(m0 == 100 && LiveWeekStart(d, w0) + 600L == t0, "(setup) t0 is the first minute of monthly period 100 and of week " + w0);

            GameSession s = LiveSession(d);
            LiveEvents.Record(s, EventQuestKind.Stage, 1, t0);
            LiveEvents.ClaimQuest(s, LiveEventSlot.Weekly, "q_stage", t0 + 1);
            LiveEvents.Record(s, EventQuestKind.Stage, 3, t0 + 2);
            LiveEvents.ClaimQuest(s, LiveEventSlot.Monthly, "m_stage", t0 + 3);
            StoreOffers.Fulfil(s, "event_track_weekly", null, t0 + 4);
            StoreOffers.Fulfil(s, "event_pack_weekly", null, t0 + 5);
            StoreOffers.Fulfil(s, "event_track_monthly", null, t0 + 6);
            StoreOffers.Fulfil(s, "event_pack_monthly", null, t0 + 7);
            LiveEventSlotState wk = s.Profile.LiveEvents.Weekly, mo = s.Profile.LiveEvents.Monthly;
            Check(wk.Period == w0 && wk.Points == 5 && wk.TrackBought && wk.PackBought && mo.Period == m0 && mo.Points == 10 && mo.TrackBought && mo.PackBought && wk.EventId == "t_week",
                  "(setup) week " + w0 + " holds 5 points, the track and the pack; month " + m0 + " holds 10 points, the track and the pack");

            // ---- the last second of the week changes nothing
            long lastSecond = LiveWeekStart(d, w0 + 1) - 1L;
            string beforeEdge = LiveSnapshot(s);
            Check(!LiveEvents.Roll(s, LiveEventSlot.Weekly, lastSecond) && LiveSnapshot(s) == beforeEdge, "THE LAST SECOND OF THE WEEK IS STILL THE WEEK: nothing rolls");

            // ---- the first second of the next week: the weekly slot turns over, the monthly one does not
            long nextWeek = LiveWeekStart(d, w0 + 1);
            Check(LiveEvents.Roll(s, LiveEventSlot.Weekly, nextWeek) && !LiveEvents.Roll(s, LiveEventSlot.Monthly, nextWeek), "THE FIRST SECOND OF THE NEXT WEEK rolls the weekly slot and not the monthly one");
            wk = s.Profile.LiveEvents.Weekly;
            mo = s.Profile.LiveEvents.Monthly;
            Check(wk.Period == w0 + 1 && wk.EventId == "t_week2" && wk.Points == 0 && wk.QuestMask == 0 && wk.FreeMask == 0 && wk.PaidMask == 0 && !wk.TrackBought && !wk.PackBought,
                  "WEEKLY ROLLOVER: the next event takes its turn (" + wk.EventId + "), points, claims, the track and the pack are all new");
            Check(mo.Period == m0 && mo.Points == 10 && mo.TrackBought && mo.PackBought, "and the monthly event is untouched by a week turning over");
            LiveEventView freshWeek = LiveEvents.View(s, LiveEventSlot.Weekly, nextWeek + 5);
            Check(freshWeek.PackCanBuy || freshWeek.PackOfferId.Length == 0, "(the second test event has no pack; the first has, and sells again next time round)");
            StoreFulfilment trackAgain = StoreOffers.Fulfil(s, "event_track_weekly", null, nextWeek + 6);
            Check(trackAgain.Success && s.Profile.LiveEvents.Weekly.TrackBought, "THE TRACK IS FOR SALE AGAIN in the new event");

            // ---- the week after that is the first event again, and its pack sells again
            long twoWeeks = LiveWeekStart(d, w0 + 2);
            LiveEvents.Record(s, EventQuestKind.Stage, 1, twoWeeks);
            Check(s.Profile.LiveEvents.Weekly.Period == w0 + 2 && s.Profile.LiveEvents.Weekly.EventId == "t_week" && StoreOffers.Fulfil(s, "event_pack_weekly", null, twoWeeks + 1).Success,
                  "two weeks on the first event is back, and its pack is once per event, not once ever");

            // ---- 28 days on the monthly event turns over
            long nextMonth = LiveMonthStart(d, m0 + 1);
            LiveEvents.Record(s, EventQuestKind.Level, 1, nextMonth + 10);
            mo = s.Profile.LiveEvents.Monthly;
            Check(mo.Period == m0 + 1 && mo.Points == 0 && mo.QuestMask == 0 && !mo.TrackBought && !mo.PackBought && LiveEvents.PeriodEndUnix(s, LiveEventSlot.Monthly, m0) == nextMonth,
                  "MONTHLY ROLLOVER: 28 days later the monthly event starts new, its track and pack are for sale again");

            // ---- a clock jumped a year forward lands in the period it names and pays nothing for the ones it skipped
            GameSession jump = LiveSession(d);
            LiveEvents.Record(jump, EventQuestKind.Stage, 1, t0);
            long later = t0 + 365L * 86400L;
            LiveEvents.Record(jump, EventQuestKind.Stage, 1, later);
            LiveEventSlotState jw = jump.Profile.LiveEvents.Weekly;
            Check(jw.Period == LiveEvents.PeriodOf(jump, LiveEventSlot.Weekly, later) && jw.Period > w0 + 40 && LiveEvents.View(jump, LiveEventSlot.Weekly, later).Points == 0
                  && Math.Max(LiveProgress(jump, LiveEventSlot.Weekly, "q_stage", later), LiveProgress(jump, LiveEventSlot.Weekly, "q2_stage", later)) == 1,
                  "A CLOCK JUMPED A YEAR FORWARD lands in the week it names (" + jw.Period + ") with that week's quests at one stage and the skipped weeks paid nothing");

            // ---- the days left follow the reward day
            LiveEventView dv = LiveEvents.View(LiveSession(d), LiveEventSlot.Weekly, LiveWeekStart(d, w0) + 6 * 86400L + 3600L);
            Check(dv.DaysLeft == 1 && dv.ResetInSeconds == LiveWeekStart(d, w0 + 1) - (LiveWeekStart(d, w0) + 6 * 86400L + 3600L), "the days left on the last day of the week read 1");

            // ---- before the monthly epoch no monthly event runs
            long beforeEpoch = d.LiveEvents.EpochDay * 86400L - 86400L * 10;
            GameSession early = LiveSession(d);
            LiveEvents.Record(early, EventQuestKind.Level, 1, beforeEpoch);
            LiveEvents.Record(early, EventQuestKind.Stage, 1, beforeEpoch);
            Check(!LiveEvents.View(early, LiveEventSlot.Monthly, beforeEpoch).Active && !LiveEvents.View(early, LiveEventSlot.Weekly, beforeEpoch).Active && early.Profile.LiveEvents.IsEmpty
                  && StoreOffers.Fulfil(early, "event_track_monthly", null, beforeEpoch).Reason == StoreRefusal.NoEvent
                  && StoreOffers.Fulfil(early, "event_pack_weekly", null, beforeEpoch).Reason == StoreRefusal.NoEvent,
                  "BEFORE THE EPOCH NO EVENT RUNS in either slot: no view, no quest, nothing written, and the tracks and packs cannot be bought");
        }

        // ------------------------------------------------------------------ 8. a wound back clock

        private static void CheckLiveWoundBack(ContentDatabase db)
        {
            ContentDatabase d = LiveDb(LiveTestJson(db));
            long t0 = LiveT0(d);
            long w0 = LiveEvents.PeriodOf(LiveSession(d), LiveEventSlot.Weekly, t0);
            GameSession s = LiveSession(d);

            // The player earns, claims and buys in week w0, then the week turns and they earn again.
            LiveFillWeekly(s, t0);
            LiveEvents.ClaimFree(s, LiveEventSlot.Weekly, 1, t0 + 1);
            StoreOffers.Fulfil(s, "event_track_weekly", null, t0 + 2);
            StoreOffers.Fulfil(s, "event_pack_weekly", null, t0 + 3);
            long next = LiveWeekStart(d, w0 + 1) + 600L;
            LiveEvents.Record(s, EventQuestKind.Stage, 1, next);
            LiveEvents.ClaimQuest(s, LiveEventSlot.Weekly, "q2_stage", next + 1);
            StoreOffers.Fulfil(s, "event_track_weekly", null, next + 2);
            LiveEvents.ClaimFree(s, LiveEventSlot.Weekly, 1, next + 3);
            string settled = LiveSnapshot(s);
            LiveEventSlotState st = s.Profile.LiveEvents.Weekly;
            Check(st.Period == w0 + 1 && st.Points == 10 && st.TrackBought && st.EventId == "t_week2", "(setup) the player is in week " + (w0 + 1) + " with 10 points, the track bought and everything claimable claimed");

            // ---- wound back one week, a day, a year, and to 1970
            long[] clocks = { t0, next - 86400L, next - 365L * 86400L, 0L, 1000L };
            string[] names = { "to last week", "by one day", "by a year", "to 1970", "to 1000 seconds" };
            for (int i = 0; i < clocks.Length; i++)
            {
                long c = clocks[i];
                bool rolled = LiveEvents.Roll(s, LiveEventSlot.Weekly, c) || LiveEvents.Roll(s, LiveEventSlot.Monthly, c);
                LiveResult q = LiveEvents.ClaimQuest(s, LiveEventSlot.Weekly, "q2_stage", c);
                LiveResult free = LiveEvents.ClaimFree(s, LiveEventSlot.Weekly, 1, c);
                LiveResult paid = LiveEvents.ClaimPaid(s, LiveEventSlot.Weekly, 1, c);
                StoreFulfilment track = StoreOffers.Fulfil(s, "event_track_weekly", null, c);
                LiveEventView view = LiveEvents.View(s, LiveEventSlot.Weekly, c);
                Check(!rolled && !q.Success && !free.Success && !track.Success && track.Reason == StoreRefusal.SoldOut && view.Points == 10 && view.EventId == "t_week2" && view.TrackOwned
                      && LiveSnapshot(s) == settled && !paid.Success,
                      "A CLOCK WOUND BACK " + names[i].ToUpperInvariant() + " reopens nothing: no roll, no second quest claim, no second free tier, no second track, the view still reads week " + (w0 + 1) + " and the save is byte for byte as it was");
            }

            // ---- last week's pack and tiers do not come back either: they are gone, and the new period has its own
            StoreFulfilment oldPack = StoreOffers.Fulfil(s, "event_pack_weekly", null, t0);
            Check(!oldPack.Success && oldPack.Reason == StoreRefusal.NoEvent && LiveSnapshot(s) == settled, "last week's pack cannot be bought by winding the clock back (this week's event has none)");

            // ---- the wound back clock counts into the period already stored, never into the past
            LiveEvents.Record(s, EventQuestKind.Stage, 1, t0 + 5);
            Check(s.Profile.LiveEvents.Weekly.Period == w0 + 1 && s.Profile.LiveEvents.Weekly.Points == 10, "an action at a wound back clock counts into the period already stored and moves the stamp nowhere");

            // ---- the monthly slot too
            GameSession m = LiveSession(d);
            long mNext = LiveMonthStart(d, 101) + 600L;
            LiveEvents.Record(m, EventQuestKind.Level, 1, mNext);
            StoreOffers.Fulfil(m, "event_pack_monthly", null, mNext + 1);
            string mSettled = LiveSnapshot(m);
            bool monthlyRolled = LiveEvents.Roll(m, LiveEventSlot.Monthly, t0);
            StoreFulfilment mAgain = StoreOffers.Fulfil(m, "event_pack_monthly", null, t0);
            Check(!monthlyRolled && !mAgain.Success && mAgain.Reason == StoreRefusal.SoldOut && LiveSnapshot(m) == mSettled && m.Profile.LiveEvents.Monthly.Period == 101,
                  "THE MONTHLY SLOT REFUSES A WOUND BACK CLOCK TOO: the pack stays sold out for the stored month");
        }

        // ------------------------------------------------------------------ 9. the save

        private static void CheckLiveSave(ContentDatabase db)
        {
            ContentDatabase d = LiveDb(LiveTestJson(db));
            long t0 = LiveT0(d);

            GameSession plain = LiveSession(d);
            string plainJson = LiveSnapshot(plain);
            Check(!plainJson.Contains("liveEvents") && JsonValue.Parse(plainJson).GetInt("version", 0) < 26 && plain.Profile.LiveEvents.IsEmpty,
                  "A PROFILE THAT NEVER TOUCHED AN EVENT writes no liveEvents block and does not claim version 26");

            GameSession s = LiveSession(d);
            LiveFillWeekly(s, t0);
            LiveEvents.ClaimFree(s, LiveEventSlot.Weekly, 1, t0 + 1);
            StoreOffers.Fulfil(s, "event_track_weekly", null, t0 + 2);
            LiveEvents.ClaimPaid(s, LiveEventSlot.Weekly, 1, t0 + 3);
            StoreOffers.Fulfil(s, "event_pack_monthly", null, t0 + 4);
            string json = LiveSnapshot(s);
            string error;
            PlayerProfile back = SaveSerializer.Deserialize(json, d, out error);
            Check(back != null && error == null && SaveSerializer.Serialize(back) == json
                  && back.LiveEvents.Weekly.Points == 50 && back.LiveEvents.Weekly.TrackBought && back.LiveEvents.Weekly.PaidMask == 1 && back.LiveEvents.Weekly.FreeMask == 1
                  && back.LiveEvents.Monthly.PackBought && back.LiveEvents.Weekly.Progress.Count == 11,
                  "LIVE EVENTS SAVE ROUND TRIP: points, quest progress, claims, the track and the pack load back identical and save again byte for byte (" + json.Length + " characters)");
            Check(JsonValue.Parse(json).GetInt("version", 0) == 26 && SaveSerializer.VersionOfJson(json) == 26 && SaveSerializer.HoldsVersion26Data(s.Profile),
                  "and the document claims version 26, the version that holds the live events");

            // The claim is one the cloud limit has to know: a version 26 document stays under the size the server accepts.
            Check(JsonValue.Parse(json)["liveEvents"].Count == 2 && s.Profile.LiveEvents.Write().ToJson(true).Length < 1024,
                  "the block is two small objects, under 1 KB");

            // Damaged numbers are clamped to what the game could have written.
            JsonValue tampered = JsonValue.Parse(json);
            JsonValue w = tampered["liveEvents"]["weekly"];
            w.Set("points", 999999999);
            w.Set("quests", -1);
            w.Set("free", 4294967295.0);
            w.Set("period", -5);
            PlayerProfile hurt = SaveSerializer.Deserialize(tampered.ToJson(true), d, out error);
            LiveEventSlotState hw = hurt == null ? null : hurt.LiveEvents.Weekly;
            Check(hurt != null && hw.Points <= 100000 && hw.QuestMask == 0 && (hw.FreeMask & ~((1 << 30) - 1)) == 0 && hw.Period == 0,
                  "DAMAGED NUMBERS ARE CLAMPED on load: points to the cap, a negative mask to none, a mask to 30 bits, a negative period to none");

            // A save written with the file loaded still loads without it: the data is kept, the engine is inert.
            ContentDatabase noFile = LiveDb(null);
            PlayerProfile kept = SaveSerializer.Deserialize(json, noFile, out error);
            Check(kept != null && SaveSerializer.Serialize(kept) == json, "a save loaded by a build with no live-events.json keeps its block and writes it back untouched");
        }

        // ------------------------------------------------------------------ 10. not hashed

        private static void CheckLiveNotHashed(ContentDatabase db, string contentDir)
        {
            string text = File.ReadAllText(Path.Combine(contentDir, "live-events.json"));
            ContentDatabase a = LiveDb(text);
            ContentDatabase b = LiveDb(text.Replace("\"hours\": 48", "\"hours\": 49"));
            ContentDatabase c = LiveDb(null);
            Check(a.ContentVersion == b.ContentVersion && a.ContentVersion == c.ContentVersion && a.ContentVersion == db.ContentVersion
                  && a.LiveEvents.EventFor(LiveEventSlot.Weekly, 1).Pack.Hours != b.LiveEvents.EventFor(LiveEventSlot.Weekly, 1).Pack.Hours && !c.LiveEvents.IsUsable,
                  "live-events.json IS NOT HASHED: retuning a pack, or loading no file at all, leaves ContentVersion (" + db.ContentVersion + ") as it was, so no recorded guild dungeon replay is retired");
            Check(db.Events.Count == a.Events.Count && db.Events.Count >= 2, "and the limited-time events.json engine is untouched (" + db.Events.Count + " events)");
        }

        // ------------------------------------------------------------------ 11. the switch rolls back to today

        private static void CheckLiveOff(ContentDatabase db)
        {
            ContentDatabase d = LiveDb(LiveTestJson(db));
            ContentDatabase none = LiveDb(null);
            long t0 = LiveT0(d);

            // The same play on three builds: the events on, the events switched off, and a build that never had the file.
            Func<ContentDatabase, bool, string> play = delegate (ContentDatabase cd, bool on)
            {
                bool was = LiveEventsSwitch.Enabled;
                LiveEventsSwitch.Enabled = on;
                try
                {
                    GameSession s = LiveSession(cd);
                    StageDef first = cd.AllStages[0];
                    for (int i = 0; i < 40; i++) if (s.ResolveWave(first, 7100 + i * 101, t0 + i).StageCleared) break;
                    s.Profile.Gold = 1000000000L;
                    s.LevelUpHero(s.Profile.Heroes[0], 2, t0 + 100);
                    LoopSetBoard(s, t0 + 150, "level_3");
                    DailyLoop.Record(s, OrderKind.Level, 3, t0 + 150);
                    DailyLoop.ClaimOrder(s, "level_3", t0 + 151);
                    s.GrantProvingResult(true, t0 + 200);
                    s.Profile.Shards = cd.Balance.SummonCost;
                    s.Summon(9302, t0 + 300);
                    StoreOffers.Fulfil(s, "event_track_weekly", null, t0 + 400);
                    StoreOffers.Fulfil(s, "event_pack_weekly", null, t0 + 401);
                    LiveEvents.ClaimQuest(s, LiveEventSlot.Weekly, "q_stage", t0 + 402);
                    LiveEvents.ClaimFree(s, LiveEventSlot.Weekly, 1, t0 + 403);
                    LiveEvents.ClaimPaid(s, LiveEventSlot.Weekly, 1, t0 + 404);
                    return LiveSnapshot(s);
                }
                finally { LiveEventsSwitch.Enabled = was; }
            };
            string onSave = play(d, true), offSave = play(d, false), noFileSave = play(none, true);
            Check(offSave == noFileSave && !offSave.Contains("liveEvents") && JsonValue.Parse(offSave).GetInt("version", 0) < 26,
                  "FLAG OFF EQUALS TODAY: the same play with the switch off and with no live-events.json at all gives byte-identical saves, with no liveEvents block and no version 26");
            Check(onSave != offSave && onSave.Contains("liveEvents") && JsonValue.Parse(onSave).GetInt("version", 0) == 26, "(control) with the switch on the same play does write the block");

            LiveEventsSwitch.Enabled = false;
            GameSession s2 = LiveSession(d);
            string before = LiveSnapshot(s2);
            LiveEventView view = LiveEvents.View(s2, LiveEventSlot.Weekly, t0);
            LiveResult claim = LiveEvents.ClaimQuest(s2, LiveEventSlot.Weekly, "q_stage", t0);
            LiveResult free = LiveEvents.ClaimFree(s2, LiveEventSlot.Weekly, 1, t0);
            LiveResult paid = LiveEvents.ClaimPaid(s2, LiveEventSlot.Weekly, 1, t0);
            StoreFulfilment buy = StoreOffers.Fulfil(s2, "event_track_weekly", null, t0);
            StoreFulfilment pack = StoreOffers.Fulfil(s2, "event_pack_monthly", null, t0);
            bool dot = LiveEvents.AnythingToClaim(s2, t0);
            LiveEventsSwitch.Enabled = true;
            Check(!view.Active && claim.Reason == LiveRefusal.Off && free.Reason == LiveRefusal.Off && paid.Reason == LiveRefusal.Off
                  && buy.Reason == StoreRefusal.Off && pack.Reason == StoreRefusal.Off && !dot && LiveSnapshot(s2) == before,
                  "WITH THE SWITCH OFF every call refuses Off, the Hub's dot is dark, an event offer cannot be bought, and nothing is written");

            // State already in a save is kept when the switch is off.
            GameSession kept = LiveSession(d);
            LiveEvents.Record(kept, EventQuestKind.Stage, 1, t0);
            string keptJson = LiveSnapshot(kept);
            LiveEventsSwitch.Enabled = false;
            string error;
            PlayerProfile back = SaveSerializer.Deserialize(keptJson, d, out error);
            string backJson = back == null ? "" : SaveSerializer.Serialize(back);
            LiveEventsSwitch.Enabled = true;
            Check(backJson == keptJson, "STATE ALREADY IN A SAVE IS KEPT with the switch off: it loads and is written back untouched");

            // The store lists no event offer in its Packs tab: the Events screen sells them.
            GameSession store = LiveSession(d);
            StoreView sv = StoreOffers.View(store, t0);
            bool listed = false;
            for (int i = 0; i < sv.Offers.Count; i++) if (sv.Offers[i].Id.StartsWith("event_")) listed = true;
            Check(sv.Active && !listed && d.Store.Offer("event_pack_weekly") != null, "the Packs tab lists no event offer; the Events screen sells them with the event's own words");
        }

        // ------------------------------------------------------------------ 12. nothing random

        private static void CheckLiveNothingRandom(string contentDir)
        {
            string root = SealedRepoRoot(contentDir);
            if (root == null) { Check(false, "LIVE EVENTS: the repository root was not found from " + contentDir); return; }
            string[] files =
            {
                "Assets/Scripts/Core/Progression/LiveEvents.cs",
                "Assets/Scripts/Core/Data/LiveEventsConfig.cs",
                "Assets/Scripts/Core/Model/LiveEventsState.cs"
            };
            string[] banned = { "Random", "DeterministicRng", "NextDouble", "NextInt", ".Range(", "Shuffle", "Guid.NewGuid", "Environment.TickCount", ".Summon(", "SummonOutcome", "ArmorRoller", "RerollArmor", "CombineArmor", ".Convert(", "Pity", "TotalSummons", "SealedGrants" };
            List<string> hits = new List<string>();
            for (int i = 0; i < files.Length; i++)
            {
                string path = Path.Combine(root, files[i].Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) { hits.Add(files[i] + " (missing)"); continue; }
                string[] lines = File.ReadAllText(path).Split('\n');
                for (int l = 0; l < lines.Length; l++)
                {
                    string t = lines[l].TrimStart();
                    if (t.StartsWith("//") || t.StartsWith("*")) continue;
                    for (int b = 0; b < banned.Length; b++)
                        if (lines[l].Contains(banned[b])) hits.Add(Path.GetFileName(files[i]) + ": " + banned[b]);
                }
            }
            Check(hits.Count == 0,
                  "NO LIVE EVENT CODE REACHES A RANDOM MECHANIC: LiveEvents, LiveEventsConfig and LiveEventsState hold no random call, no summon, no roll, no conversion, no pity, and never call SealedGrants (the bought goods go through PurchaseGrants.Apply)"
                  + (hits.Count > 0 ? " (" + string.Join("; ", hits.ToArray()) + ")" : ""));

            string text = File.ReadAllText(Path.Combine(contentDir, "live-events.json"));
            string[] forbidden = { "chance", "random", "roll", "pity", "weight", "pool", "gacha", "lootbox", "probability" };
            string lower = OfferStripJsonComments(text).ToLowerInvariant();
            List<string> words = new List<string>();
            for (int i = 0; i < forbidden.Length; i++) if (lower.Contains(forbidden[i])) words.Add(forbidden[i]);
            Check(words.Count == 0, "live-events.json carries no field named for a chance, a roll, a pool or a pity" + (words.Count > 0 ? " (" + string.Join(", ", words.ToArray()) + ")" : ""));

            // The one grant path: the Event Track and the pack are applied by PurchaseGrants.Apply and by nothing else.
            string grants = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Core/Progression/PurchaseGrants.cs".Replace('/', Path.DirectorySeparatorChar)));
            string events = File.ReadAllText(Path.Combine(root, files[0].Replace('/', Path.DirectorySeparatorChar)));
            Check(grants.Contains("LiveEvents.MarkTrackBought") && grants.Contains("LiveEvents.MarkPackBought") && events.Contains("PurchaseGrants.Apply"),
                  "the track is unlocked and the pack's lines are applied inside PurchaseGrants.Apply, and a paid tier is claimed through it");
        }

        // ------------------------------------------------------------------ 13. the wiring

        private static void CheckLiveWiring(string contentDir)
        {
            string root = SealedRepoRoot(contentDir);
            if (root == null) { Check(false, "LIVE EVENTS WIRING: the repository root was not found from " + contentDir); return; }
            string screenPath = Path.Combine(root, "Assets/Scripts/UI/Screens/LiveEventsScreen.cs".Replace('/', Path.DirectorySeparatorChar));
            string routerPath = Path.Combine(root, "Assets/Scripts/UI/ScreenRouter.cs".Replace('/', Path.DirectorySeparatorChar));
            string hubPath = Path.Combine(root, "Assets/Scripts/UI/Screens/MainMenuScreen.cs".Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(screenPath) || !File.Exists(routerPath) || !File.Exists(hubPath)) { Check(false, "LIVE EVENTS WIRING: the screen, the router or the Hub was not found"); return; }
            string screen = StoreStripCommentsAndStrings(File.ReadAllText(screenPath));
            string[] banned = { "PurchaseGrants", "SealedGrants", "PurchaseGrant ", "GrantItem", "StoreOffers.Fulfil", ".Gold +=", ".Gold -=", ".RelicDust +=", "Random", "DeterministicRng", ".Summon(", "GrantHeroDirect" };
            List<string> hits = new List<string>();
            for (int i = 0; i < banned.Length; i++) if (screen.Contains(banned[i])) hits.Add(banned[i]);
            Check(hits.Count == 0 && screen.Contains("LiveEvents.View") && screen.Contains("LiveEvents.ClaimQuest") && screen.Contains("LiveEvents.ClaimFree") && screen.Contains("LiveEvents.ClaimPaid") && screen.Contains("Flow.Buy("),
                  "THE EVENTS SCREEN DECIDES AND GRANTS NOTHING: it reads LiveEvents.View, claims through the engine's claims and buys through the purchase flow, and names no grant, no purchase, no purse and no random call"
                  + (hits.Count > 0 ? " (" + string.Join(", ", hits.ToArray()) + ")" : ""));
            string router = File.ReadAllText(routerPath), hub = File.ReadAllText(hubPath);
            Check(router.Contains("LiveEventsSwitch.Enabled) LiveEvents = Create<Screens.LiveEventsScreen>()") && hub.Contains("Router.LiveEvents != null ? ScreenId.LiveEvents : ScreenId.Events")
                  && hub.Contains("LiveEvents.AnythingToClaim"),
                  "the screen is built only with the switch on, the Hub's Events door falls back to the limited-time events page without it, and the door's dot reads LiveEvents.AnythingToClaim");
            Check(Array.IndexOf(Emberfall.Core.Progression.FeatureEntryPoints.OpenScreens, "LiveEvents") >= 0, "the Events screen is an open screen in the unlock table");
        }
    }
}
