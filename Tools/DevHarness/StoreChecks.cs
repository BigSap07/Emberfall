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
    /// THE STORE (Docs/DESIGN-STORE.md): fixed offers, a purchase limit and a reset for each, and one grant path
    /// that marks everything Sealed. One named check per rule: the shipped file, every offer fixed and
    /// deterministic, Sealed on every grant, the limits across a week and a month, the clock wound back, the
    /// ledger card, the growth fund, the paid track, every refusal leaving the save untouched, no random call
    /// anywhere the purchase reaches, the file staying out of ContentVersion, the save block, the switch and
    /// the wiring.
    ///
    /// Every check works on a session built for it and, where it needs different store data, on a throwaway
    /// database (StoreDb): the shared content object is never changed.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckStore(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("THE STORE (Docs/DESIGN-STORE.md: fixed offers, one Sealed grant path, limits and resets that a clock cannot reopen)");
            _storeContentDir = contentDir;
            bool was = StoreSwitch.Enabled, contractWas = HeroContractSwitch.Enabled;
            bool roadWas = WayfarersRoadSwitch.Enabled, loopWas = DailyLoopSwitch.Enabled;
            try
            {
                StoreSwitch.Enabled = true;
                HeroContractSwitch.Enabled = false;
                WayfarersRoadSwitch.Enabled = true;
                DailyLoopSwitch.Enabled = true;
                CheckStoreContent(db, contentDir);
                CheckStoreFixedAndDeterministic(db);
                CheckStoreSealedGrants(db);
                CheckStoreRefusedByRandomMechanics(db);
                CheckStoreLimitsAndResets(db);
                CheckStoreClockWoundBack(db);
                CheckStoreLedger(db);
                CheckStoreGrowthFund(db);
                CheckStorePass(db);
                CheckStoreHeroContract(db);
                CheckStoreRefusalsWriteNothing(db);
                CheckStoreNothingRandom(contentDir);
                CheckStoreNotHashed(db, contentDir);
                CheckStoreSave(db);
                CheckStoreOff(db);
                CheckStoreValueReport(db);
            }
            finally
            {
                StoreSwitch.Enabled = was;
                HeroContractSwitch.Enabled = contractWas;
                WayfarersRoadSwitch.Enabled = roadWas;
                DailyLoopSwitch.Enabled = loopWas;
            }
            Check(StoreSwitch.Enabled == was && HeroContractSwitch.Enabled == contractWas && WayfarersRoadSwitch.Enabled == roadWas && DailyLoopSwitch.Enabled == loopWas,
                  "the switches are put back as the run found them (store " + (was ? "on" : "off") + ", hero contract " + (contractWas ? "on" : "off") + ")");
        }

        // ------------------------------------------------------------------ helpers

        private static readonly string[] StoreOfferIds = { "starter_pact", "weekly_pack", "monthly_pack", "ember_ledger", "growth_fund", "pass_premium" };

        /// <summary>A session whose frontier is the last stage of `chapter` (0 clears nothing, 1 to 6 clear every stage up to the chapter's end).</summary>
        private static GameSession StoreSession(ContentDatabase db, int chapter)
        {
            PlayerProfile p = GameSession.CreateNewProfile(db, SinkBase);
            for (int i = 0; i < db.AllStages.Count; i++)
            {
                StageDef st = db.AllStages[i];
                int ch;
                if (st.ChapterId.Length > 2 && int.TryParse(st.ChapterId.Substring(2), out ch) && ch <= chapter) p.ClearedStageIds.Add(st.Id);
            }
            p.StagesCleared = CampaignProgress.CountCleared(p, db);
            p.ProvingHighestStanding = 5;
            p.RecitalHighestFloor = 10;
            p.RecitalCurrentFloor = 11;
            return new GameSession(db, p);
        }

        private static string StoreSnapshot(GameSession s) { return SaveSerializer.Serialize(s.Profile); }

        /// <summary>A Monday reward day at 04:00 UTC plus ten minutes, used as "now" for week tests.</summary>
        private static long StoreMonday(ContentDatabase db)
        {
            return PassSeasonStart(db, 5) + 600;
        }

        /// <summary>Unix seconds of a UTC calendar moment.</summary>
        private static long StoreUnix(int year, int month, int day, int hour)
        {
            return (long)(new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        /// <summary>A throwaway database built from the shipped files with a different store.json.</summary>
        private static ContentDatabase StoreDb(string contentDir, string storeJson)
        {
            ContentDatabase fresh = LoadContent(contentDir);
            fresh.LoadStore(storeJson);
            return fresh;
        }

        private static string StoreFileText(string contentDir)
        {
            return File.ReadAllText(Path.Combine(contentDir, "store.json"));
        }

        private static string StoreFourStar(ContentDatabase db)
        {
            for (int i = 0; i < db.Heroes.Count; i++) if (db.Heroes[i].Stars == 4 && !db.Heroes[i].StartingHero) return db.Heroes[i].Id;
            return null;
        }

        private static string StoreFirstChoice(GameSession s, string offerId)
        {
            StoreView v = StoreOffers.View(s, SinkBase);
            for (int i = 0; i < v.Offers.Count; i++)
                if (v.Offers[i].Id == offerId && v.Offers[i].Choices.Count > 0) return v.Offers[i].Choices[0].HeroId;
            return null;
        }

        // ------------------------------------------------------------------ 1. content

        private static void CheckStoreContent(ContentDatabase db, string contentDir)
        {
            StoreConfig c = db.Store;
            Check(c.IsUsable && c.Warnings.Count == 0, "the shipped store.json loads clean: " + c.Offers.Count + " offers, " + c.Tiers.Count + " price tiers, no warnings");

            List<string> missing = new List<string>();
            for (int i = 0; i < StoreOfferIds.Length; i++) if (c.Offer(StoreOfferIds[i]) == null) missing.Add(StoreOfferIds[i]);
            Check(missing.Count == 0 && c.Offer("hero_contract") != null,
                  "it lists the Starter Pact, the Weekly and Monthly Packs, the Ember Ledger, the Growth Fund, the pass premium and the Hero Contract" + (missing.Count > 0 ? " (missing " + string.Join(", ", missing.ToArray()) + ")" : ""));

            bool placeholders = true, priced = true;
            for (int i = 0; i < c.Offers.Count; i++)
            {
                if (!c.Offers[i].ProductId.StartsWith("placeholder.")) placeholders = false;
                if (c.Offers[i].PriceEur <= 0.0) priced = false;
            }
            Check(placeholders && priced, "every product id is a placeholder (\"placeholder.\" prefix) and every offer carries a placeholder euro price from a named tier");

            StoreOfferDef starter = c.Offer("starter_pact");
            Check(starter.Limit == 1 && starter.Reset == StoreReset.Never && starter.HeroChoices.Count == 3 && starter.Hours == 6.0,
                  "the Starter Pact is once, three named 5-stars and 6 hourglasses");
            Check(c.Offer("weekly_pack").Reset == StoreReset.Week && c.Offer("weekly_pack").Limit == 1
                  && c.Offer("monthly_pack").Reset == StoreReset.Month && c.Offer("monthly_pack").Limit == 1
                  && c.Offer("monthly_pack").Hours > c.Offer("weekly_pack").Hours,
                  "the Weekly Pack resets weekly and the Monthly Pack monthly, limit 1, and the monthly is the larger");
            StoreOfferDef ledger = c.Offer("ember_ledger");
            Check(ledger.Days == 30 && ledger.HoursPerDay > 0.0 && ledger.IdleBonusPct == 10, "the Ember Ledger is a 30 day card paying hourglasses a day and 10 percent idle");
            StoreOfferDef growth = c.Offer("growth_fund");
            string[] want = { "ch2_12", "ch3_12", "ch4_12", "ch5_12", "ch6_12" };
            bool milestones = growth.Milestones.Count == want.Length;
            for (int i = 0; milestones && i < want.Length; i++) if (growth.Milestones[i].StageId != want[i]) milestones = false;
            Check(milestones, "the Growth Fund pays at 2-12, 3-12, 4-12, 5-12 and 6-12");
            Check(c.Offer("hero_contract").Limit == 2 && c.Offer("hero_contract").Reset == StoreReset.Month && c.Offer("hero_contract").Stars == 5,
                  "the Hero Contract is a 5-star copy, limit 2 a month per hero");

            // The closed kind set: an unknown kind, tier, reset or a hero that is not a 5-star never loads.
            string bad = "{ \"tiers\": { \"mid\": 4.99 }, \"offers\": ["
                + "{ \"id\": \"a\", \"kind\": \"lootbox\", \"tier\": \"mid\", \"productId\": \"placeholder.a\" },"
                + "{ \"id\": \"b\", \"kind\": \"pack\", \"tier\": \"nowhere\", \"productId\": \"placeholder.b\", \"hours\": 1 },"
                + "{ \"id\": \"c\", \"kind\": \"pack\", \"tier\": \"mid\", \"productId\": \"placeholder.c\", \"hours\": 1, \"reset\": \"daily-random\" },"
                + "{ \"id\": \"d\", \"kind\": \"starter\", \"tier\": \"mid\", \"productId\": \"placeholder.d\", \"heroChoices\": [ \"no_such_hero\" ] },"
                + "{ \"id\": \"e\", \"kind\": \"pack\", \"tier\": \"mid\", \"productId\": \"\", \"hours\": 1 },"
                + "{ \"id\": \"f\", \"kind\": \"pack\", \"tier\": \"mid\", \"productId\": \"placeholder.f\" } ] }";
            ContentDatabase throwaway = StoreDb(contentDir, bad);
            Check(!throwaway.Store.IsUsable && throwaway.Store.Offers.Count == 0 && throwaway.Store.Warnings.Count >= 6,
                  "AN UNKNOWN KIND, TIER OR RESET, A MISSING PRODUCT ID, AN EMPTY PACK AND A HERO THAT IS NOT A 5-STAR ARE DROPPED, each with a warning (" + throwaway.Store.Warnings.Count + "), so no roll can be written into the file");
            Check(!StoreConfig.FromJson(null).IsUsable && !StoreConfig.FromJson("{ not json").IsUsable, "a missing or damaged file sells nothing");

            // The shipped data has no field that could hold a chance.
            string text = StoreFileText(contentDir);
            string[] forbidden = { "chance", "random", "roll", "pity", "weight", "pool", "gacha", "lootbox", "probability" };
            List<string> hits = new List<string>();
            string lower = StripJsonComments(text).ToLowerInvariant();
            for (int i = 0; i < forbidden.Length; i++) if (lower.Contains(forbidden[i])) hits.Add(forbidden[i]);
            Check(hits.Count == 0, "store.json carries no field named for a chance, a roll, a pool or a pity" + (hits.Count > 0 ? " (" + string.Join(", ", hits.ToArray()) + ")" : ""));
        }

        private static string StripJsonComments(string text)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].TrimStart();
                if (t.StartsWith("//")) continue;
                sb.Append(lines[i]).Append('\n');
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ 2. fixed and deterministic

        private static void CheckStoreFixedAndDeterministic(ContentDatabase db)
        {
            long now = StoreMonday(db);
            bool allSame = true, allShown = true, noRandom = true;
            int offersChecked = 0;
            for (int chapter = 1; chapter <= 6; chapter += 5)
            {
                GameSession a = StoreSession(db, chapter), b = StoreSession(db, chapter);
                StoreView v1 = StoreOffers.View(a, now), v2 = StoreOffers.View(a, now), v3 = StoreOffers.View(b, now);
                if (v1.Offers.Count != v2.Offers.Count || v1.Offers.Count != v3.Offers.Count) allSame = false;
                for (int i = 0; i < v1.Offers.Count; i++)
                {
                    StoreOfferView o = v1.Offers[i];
                    offersChecked++;
                    if (string.Join("|", o.Lines.ToArray()) != string.Join("|", v2.Offers[i].Lines.ToArray())
                        || string.Join("|", o.Lines.ToArray()) != string.Join("|", v3.Offers[i].Lines.ToArray())) allSame = false;
                    if (o.Lines.Count == 0 || o.PriceLabel.Length == 0 || o.LimitLabel.Length == 0) allShown = false;
                    string choice = o.Choices.Count > 0 ? o.Choices[0].HeroId : null;
                    if (!o.Available) continue;
                    StoreFulfilment fa = StoreOffers.Fulfil(a, o.Id, choice, now);
                    StoreFulfilment fb = StoreOffers.Fulfil(b, o.Id, choice, now);
                    if (!fa.Success || !fb.Success) { noRandom = false; continue; }
                    if (StoreSnapshot(a) != StoreSnapshot(b)) allSame = false;
                }
                if (StoreSnapshot(a) != StoreSnapshot(b)) allSame = false;
            }
            Check(allSame && offersChecked >= 12,
                  "EVERY OFFER IS DETERMINISTIC: two sessions in the same state read the same card and, buying the same offers, hold byte-identical saves (" + offersChecked + " offers read at the 1st and 6th frontier)");
            Check(allShown && noRandom, "every card shows its full contents, its price and its limit before purchase, and every available offer is fulfilled");

            // The fixed contents are the data: the same content read twice gives the same grant lines.
            GameSession g = StoreSession(db, 2);
            StoreOfferDef weekly = db.Store.Offer("weekly_pack");
            List<StoreGrant> g1 = StoreOffers.GrantsOf(g, weekly, null), g2 = StoreOffers.GrantsOf(g, weekly, null);
            bool same = g1.Count == g2.Count && g1.Count > 0;
            for (int i = 0; same && i < g1.Count; i++) if (g1[i].Line != g2[i].Line || g1[i].Amount != g2[i].Amount) same = false;
            Check(same, "the grant of a pack is a pure function of the data and the buyer's frontier: asked twice it is the same lines and amounts");
        }

        // ------------------------------------------------------------------ 3. Sealed

        private static void CheckStoreSealedGrants(ContentDatabase db)
        {
            long now = StoreMonday(db);
            string[] packs = { "starter_pact", "weekly_pack", "monthly_pack" };
            for (int p = 0; p < packs.Length; p++)
            {
                GameSession s = StoreSession(db, 3);
                long goldBefore = s.Profile.Gold, freeGold = s.Profile.FreeGold, freeShards = s.Profile.FreeShards, freeSlag = s.Profile.FreeSlag;
                long shards = s.Profile.Shards, dust = s.Profile.RelicDust;
                string choice = StoreFirstChoice(s, packs[p]);
                StoreFulfilment f = StoreOffers.Fulfil(s, packs[p], choice, now);
                long goldPaid = 0, dustPaid = 0;
                int heroLines = 0;
                for (int i = 0; i < f.Grants.Count; i++)
                {
                    if (f.Grants[i].Kind == StoreGrantKind.Gold) goldPaid += f.Grants[i].Amount;
                    if (f.Grants[i].Kind == StoreGrantKind.Dust) dustPaid += f.Grants[i].Amount;
                    if (f.Grants[i].Kind == StoreGrantKind.Hero) heroLines++;
                }
                bool heroSealed = true;
                if (heroLines > 0)
                {
                    HeroInstance h = s.Profile.FindHeroByDefId(choice);
                    heroSealed = h != null && h.SealedCopiesHeld >= 1;
                }
                Check(f.Success && goldPaid > 0 && s.Profile.Gold == goldBefore + goldPaid && s.Profile.Sealed.Gold == goldPaid
                      && s.Profile.FreeGold == freeGold && s.Profile.FreeShards == freeShards && s.Profile.FreeSlag == freeSlag
                      && s.Profile.Shards == shards && s.Profile.RelicDust == dust + dustPaid && heroSealed,
                      "EVERYTHING " + packs[p].ToUpperInvariant() + " GRANTS IS SEALED: " + goldPaid + " Gold is added to the purse and all of it to the Sealed part, the Gold, Shards and Slag a random mechanic may spend are unchanged"
                      + (heroLines > 0 ? ", and the hero copy is a Sealed copy" : "") + (dustPaid > 0 ? ", Dust (unmarked, its only sink sells fixed relics) " + dustPaid : ""));
            }

            // Sealed Slag, through a throwaway offer: the grant path marks it.
            string slagJson = "{ \"tiers\": { \"mid\": 4.99 }, \"offers\": [ { \"id\": \"slag_test\", \"kind\": \"pack\", \"tier\": \"mid\", \"productId\": \"placeholder.slag\", \"slag\": 50, \"hours\": 1 } ] }";
            ContentDatabase slagDb = StoreDb(_storeContentDir, slagJson);
            GameSession sl = StoreSession(slagDb, 1);
            long freeSlagBefore = sl.Profile.FreeSlag;
            StoreFulfilment sf = StoreOffers.Fulfil(sl, "slag_test", null, now);
            Check(sf.Success && sl.Profile.Sealed.Slag == 50 && sl.Profile.FreeSlag == freeSlagBefore,
                  "Slag, where an offer pays it, is Sealed Slag: the free Slag a reroll or a combine may spend does not move");
        }

        private static string _storeContentDir;

        // ------------------------------------------------------------------ 4. refused by summon and conversion

        private static void CheckStoreRefusedByRandomMechanics(ContentDatabase db)
        {
            long now = StoreMonday(db);

            // Summon: a bought pack leaves the summonable Shards exactly where they were.
            GameSession s = StoreSession(db, 4);
            s.Profile.Shards = 0;
            foreach (string id in new[] { "starter_pact", "weekly_pack", "monthly_pack" })
                StoreOffers.Fulfil(s, id, StoreFirstChoice(s, id), now);
            Check(s.Profile.Shards == 0 && s.Profile.FreeShards == 0 && !s.CanSummon && s.Profile.Sealed.Shards == 0,
                  "SUMMON: a Starter Pact and both packs bought leave no Shard to summon with, and add none to the purse (a Sealed Shard has no use)");
            string beforeSummon = StoreSnapshot(s);
            SummonOutcome refused = s.Summon(4242, now);
            Check(!refused.Success && StoreSnapshot(s) == beforeSummon, "and a summon attempt after buying is refused and the save is byte for byte as it was");

            // Conversion: a Sealed copy never counts as a body.
            GameSession c = StoreSession(db, 1);
            string pick = db.Store.Offer("starter_pact").HeroChoices[0];
            HeroInstance h = null;
            StoreOffers.Fulfil(c, "starter_pact", pick, now);
            h = c.Profile.FindHeroByDefId(pick);
            int spareAfterPurchase = h != null ? c.ConversionSpare(h) : -1;
            Check(h != null && h.Copies == 1 && h.SealedCopiesHeld == 1 && spareAfterPurchase == 0,
                  "CONVERSION: the bought 5-star is one Sealed copy and counts as no spare body (spare " + spareAfterPurchase + ")");

            // An earned copy plus a bought copy: only the earned one is spare (after the deployed one is kept).
            GameSession c2 = StoreSession(db, 1);
            c2.GrantHeroDirect(db.GetHero(pick), now);              // an earned copy
            HeroInstance h2 = c2.Profile.FindHeroByDefId(pick);
            int spareEarnedOnly = c2.ConversionSpare(h2);
            StoreOffers.Fulfil(c2, "starter_pact", pick, now);      // and a bought one
            Check(h2.Copies == 2 && h2.SealedCopiesHeld == 1 && spareEarnedOnly == 0 && c2.ConversionSpare(h2) == h2.Copies - h2.SealedCopiesHeld,
                  "an earned copy and a bought copy: only the EARNED copy can ever be spare for a conversion, never the Sealed one (copies " + h2.Copies + ", sealed " + h2.SealedCopiesHeld + ", spare " + c2.ConversionSpare(h2) + ")");
        }

        // ------------------------------------------------------------------ 5. limits and resets

        private static void CheckStoreLimitsAndResets(ContentDatabase db)
        {
            long monday = StoreMonday(db);
            GameSession s = StoreSession(db, 2);
            string pick = db.Store.Offer("starter_pact").HeroChoices[0];

            // Once.
            Check(StoreOffers.Fulfil(s, "starter_pact", pick, monday).Success
                  && StoreOffers.Check(s, "starter_pact", pick, monday) == StoreRefusal.SoldOut
                  && StoreOffers.Check(s, "starter_pact", pick, monday + 400 * 86400L) == StoreRefusal.SoldOut,
                  "THE STARTER PACT IS ONCE: sold out at once and still sold out a year later");

            // Weekly: sold out all week, back on Monday.
            StoreOffers.Fulfil(s, "weekly_pack", null, monday);
            bool soldAllWeek = true;
            for (int d = 0; d < 7; d++)
                if (StoreOffers.Check(s, "weekly_pack", null, monday + d * 86400L) != StoreRefusal.SoldOut) soldAllWeek = false;
            Check(soldAllWeek && StoreOffers.Check(s, "weekly_pack", null, monday + 7 * 86400L) == StoreRefusal.None,
                  "THE WEEKLY PACK RESETS WEEKLY: sold out on all seven days of the week it was bought and open again the next Monday");
            long before = s.Profile.Gold;
            StoreOffers.Fulfil(s, "weekly_pack", null, monday + 7 * 86400L);
            Check(s.Profile.Gold > before && StoreOffers.Check(s, "weekly_pack", null, monday + 8 * 86400L) == StoreRefusal.SoldOut,
                  "and the next week's pack is bought and is again a limit of one");

            // Monthly: a month boundary at 04:00 UTC.
            GameSession m = StoreSession(db, 2);
            long jan = StoreUnix(2027, 1, 15, 12);
            StoreOffers.Fulfil(m, "monthly_pack", null, jan);
            Check(StoreOffers.Check(m, "monthly_pack", null, StoreUnix(2027, 1, 31, 23)) == StoreRefusal.SoldOut
                  && StoreOffers.Check(m, "monthly_pack", null, StoreUnix(2027, 2, 1, 3)) == StoreRefusal.SoldOut
                  && StoreOffers.Check(m, "monthly_pack", null, StoreUnix(2027, 2, 1, 5)) == StoreRefusal.None,
                  "THE MONTHLY PACK RESETS MONTHLY: sold out to the end of January, still on the night of 1 February before the 04:00 reward day, open on the first reward day of February");

            // The two packs have separate counters.
            GameSession both = StoreSession(db, 2);
            StoreOffers.Fulfil(both, "weekly_pack", null, monday);
            Check(StoreOffers.Check(both, "monthly_pack", null, monday) == StoreRefusal.None, "the Weekly and Monthly Packs count separately");

            // The view says when it comes back.
            StoreView v = StoreOffers.View(s, monday + 8 * 86400L);
            StoreOfferView wv = null;
            for (int i = 0; i < v.Offers.Count; i++) if (v.Offers[i].Id == "weekly_pack") wv = v.Offers[i];
            Check(wv != null && !wv.Available && wv.Reason == StoreRefusal.SoldOut && wv.ResetInSeconds > 0 && wv.ResetInSeconds <= 7 * 86400L,
                  "a sold out weekly card says when it returns (" + (wv != null ? wv.ResetInSeconds : -1) + " seconds, never more than a week)");
        }

        // ------------------------------------------------------------------ 6. the clock wound back

        private static void CheckStoreClockWoundBack(ContentDatabase db)
        {
            long monday = StoreMonday(db);
            GameSession s = StoreSession(db, 2);
            StoreOffers.Fulfil(s, "weekly_pack", null, monday + 8 * 86400L);   // bought in week 2
            string before = StoreSnapshot(s);
            bool reopened = false;
            long[] back = { monday, monday - 30 * 86400L, monday - 400 * 86400L, 0L, monday + 8 * 86400L - 3600L };
            for (int i = 0; i < back.Length; i++)
                if (StoreOffers.Check(s, "weekly_pack", null, back[i]) != StoreRefusal.SoldOut) reopened = true;
            Check(!reopened && StoreSnapshot(s) == before,
                  "A CLOCK WOUND BACK REOPENS NOTHING: after a purchase in week 2, a clock set to week 1, a month before, a year before and 1970 still reads the weekly pack sold out, and the save is unchanged");

            // Wind back, then buy the next week: only the real forward move reopens it.
            Check(StoreOffers.Check(s, "weekly_pack", null, monday + 15 * 86400L) == StoreRefusal.None,
                  "and moving the clock forward to the next real week reopens it");

            // A purchase made with the clock wound back is counted in the period the store already reached.
            GameSession w = StoreSession(db, 2);
            StoreOffers.Fulfil(w, "monthly_pack", null, StoreUnix(2027, 3, 10, 12));
            StoreFulfilment again = StoreOffers.Fulfil(w, "monthly_pack", null, StoreUnix(2027, 1, 10, 12));
            Check(!again.Success && again.Reason == StoreRefusal.SoldOut, "a monthly pack bought in March cannot be bought again by setting the clock to January");

            // The store's own clock stamp only moves forward.
            long stampMarch = w.Profile.Store.ClockUnix;
            StoreOffers.Fulfil(w, "weekly_pack", null, StoreUnix(2027, 1, 12, 12));
            Check(w.Profile.Store.ClockUnix >= stampMarch, "the store's clock stamp never moves backward (" + w.Profile.Store.ClockUnix + " after " + stampMarch + ")");
        }

        // ------------------------------------------------------------------ 7. the Ember Ledger

        private static void CheckStoreLedger(ContentDatabase db)
        {
            long day0 = StoreMonday(db);
            StoreOfferDef def = db.Store.Offer("ember_ledger");
            GameSession s = StoreSession(db, 3);
            Check(StoreOffers.ClaimLedger(s, day0).Reason == StoreRefusal.NotBought, "a ledger that was not bought cannot be claimed");
            StoreFulfilment buy = StoreOffers.Fulfil(s, "ember_ledger", null, day0);
            Check(buy.Success && buy.Grants.Count == 0 && StoreOffers.LedgerActive(s, day0) && StoreOffers.LedgerDaysLeft(s, day0) == def.Days,
                  "buying the Ember Ledger grants nothing yet and starts a " + def.Days + " day card (" + StoreOffers.LedgerDaysLeft(s, day0) + " days left)");
            Check(StoreOffers.Check(s, "ember_ledger", null, day0 + 86400L) == StoreRefusal.SoldOut, "one card at a time: it cannot be bought again while it runs");

            // Claim every day for the whole card: the same hours each day, once a day.
            bool onceADay = true, everyDay = true;
            long paid = 0;
            for (int d = 0; d < def.Days; d++)
            {
                long t = day0 + d * 86400L + 300;
                StoreFulfilment c = StoreOffers.ClaimLedger(s, t);
                if (!c.Success) everyDay = false; else paid += c.Grants[0].Amount;
                if (StoreOffers.ClaimLedger(s, t + 60).Success) onceADay = false;
            }
            long perDay = s.IdleYield((long)Math.Round(def.HoursPerDay * 3600.0)).Gold;
            Check(everyDay && onceADay && paid == perDay * def.Days && s.Profile.Sealed.Gold == paid,
                  "THE LEDGER PAYS " + def.HoursPerDay + " HOURS OF GOLD A DAY, ONCE A DAY, FOR " + def.Days + " DAYS: " + paid + " Gold in all, all of it Sealed");
            long after = day0 + def.Days * 86400L + 300;
            Check(!StoreOffers.LedgerActive(s, after) && StoreOffers.ClaimLedger(s, after).Reason == StoreRefusal.Nothing && StoreOffers.Check(s, "ember_ledger", null, after) == StoreRefusal.None,
                  "after " + def.Days + " days the card has run out: nothing to claim, and a new one may be bought");

            // A missed day is not made up.
            GameSession missed = StoreSession(db, 3);
            StoreOffers.Fulfil(missed, "ember_ledger", null, day0);
            int claims = 0;
            for (int d = 0; d < def.Days; d++)
            {
                if (d >= 10 && d < 20) continue;   // ten days away
                if (StoreOffers.ClaimLedger(missed, day0 + d * 86400L + 300).Success) claims++;
            }
            Check(claims == def.Days - 10, "a missed day is not made up: ten days away pay ten days less (" + claims + " of " + def.Days + " claimed)");

            // The idle share.
            GameSession idle = StoreSession(db, 3);
            idle.Profile.LastIdleClaimUnix = day0;
            IdleReport plain = idle.PeekIdle(day0 + 6 * 3600);
            long goldBefore = idle.Profile.Gold;
            StoreOffers.Fulfil(idle, "ember_ledger", null, day0);
            idle.ClaimIdle(day0 + 6 * 3600);
            long bonus = idle.Profile.Gold - goldBefore - plain.Gold;
            Check(plain.Gold > 0 && bonus == plain.Gold * def.IdleBonusPct / 100L && idle.Profile.Sealed.Gold == bonus,
                  "WHILE THE CARD RUNS EVERY IDLE CLAIM PAYS " + def.IdleBonusPct + " PERCENT MORE GOLD, as Sealed Gold (" + plain.Gold + " claimed, " + bonus + " added)");
            GameSession noCard = StoreSession(db, 3);
            noCard.Profile.LastIdleClaimUnix = day0;
            long g0 = noCard.Profile.Gold;
            noCard.ClaimIdle(day0 + 6 * 3600);
            Check(noCard.Profile.Gold - g0 == plain.Gold && noCard.Profile.Sealed.Gold == 0, "with no card the idle claim is exactly the claim it always was");
            GameSession expired = StoreSession(db, 3);
            expired.Profile.LastIdleClaimUnix = day0;
            StoreOffers.Fulfil(expired, "ember_ledger", null, day0);
            long later = day0 + (def.Days + 2) * 86400L;
            expired.Profile.LastIdleClaimUnix = later - 6 * 3600;
            long ge = expired.Profile.Gold;
            IdleReport er = expired.PeekIdle(later);
            expired.ClaimIdle(later);
            Check(expired.Profile.Gold - ge == er.Gold, "an expired card adds nothing to an idle claim");

            // Wound back: no second claim, no revived card.
            GameSession wb = StoreSession(db, 3);
            StoreOffers.Fulfil(wb, "ember_ledger", null, day0);
            StoreOffers.ClaimLedger(wb, day0 + 5 * 86400L + 300);
            string snap = StoreSnapshot(wb);
            Check(!StoreOffers.ClaimLedger(wb, day0 + 2 * 86400L + 300).Success && !StoreOffers.ClaimLedger(wb, day0 + 5 * 86400L + 600).Success && StoreSnapshot(wb) == snap,
                  "a clock wound back claims no day twice and no day the card has passed, and writes nothing");
            GameSession revive = StoreSession(db, 3);
            StoreOffers.Fulfil(revive, "ember_ledger", null, day0);
            StoreOffers.ClaimLedger(revive, day0 + (def.Days + 3) * 86400L);   // the real clock has passed the card
            Check(!StoreOffers.LedgerActive(revive, day0 + 2 * 86400L), "and a clock wound back into the card's days does not revive a card the real clock has passed");
        }

        // ------------------------------------------------------------------ 8. the Growth Fund

        private static void CheckStoreGrowthFund(ContentDatabase db)
        {
            long now = StoreMonday(db);
            StoreOfferDef def = db.Store.Offer("growth_fund");
            GameSession s = StoreSession(db, 3);   // 2-12 and 3-12 already cleared
            Check(StoreOffers.ClaimGrowth(s, 0, now).Reason == StoreRefusal.NotBought, "a growth fund that was not bought pays nothing");
            Check(StoreOffers.Fulfil(s, "growth_fund", null, now).Success && StoreOffers.Check(s, "growth_fund", null, now + 1000 * 86400L) == StoreRefusal.SoldOut,
                  "THE GROWTH FUND IS ONCE for the life of the profile");
            StoreView v = StoreOffers.View(s, now);
            StoreOfferView gv = null;
            for (int i = 0; i < v.Offers.Count; i++) if (v.Offers[i].Id == "growth_fund") gv = v.Offers[i];
            Check(gv != null && gv.Owned && gv.Milestones.Count == def.Milestones.Count && gv.Milestones[0].CanClaim && gv.Milestones[1].CanClaim
                  && !gv.Milestones[2].Reached && !gv.Milestones[2].CanClaim,
                  "milestones already passed are claimable the moment it is bought (2-12 and 3-12), the rest wait for their stage");

            Check(StoreOffers.ClaimGrowth(s, 2, now).Reason == StoreRefusal.NotReached, "a milestone not reached cannot be claimed");
            long gold0 = s.Profile.Gold, dust0 = s.Profile.RelicDust;
            StoreFulfilment c0 = StoreOffers.ClaimGrowth(s, 0, now);
            long goldPaid = s.Profile.Gold - gold0;
            Check(c0.Success && goldPaid == s.IdleYield((long)Math.Round(def.Milestones[0].Hours * 3600.0)).Gold && s.Profile.Sealed.Gold == goldPaid
                  && s.Profile.RelicDust - dust0 == (long)Math.Round(def.Milestones[0].DustSweeps * WayfarersRoad.DustPerSweep(s)),
                  "a milestone pays its fixed hours as Sealed Gold and its sweeps as Dust (" + goldPaid + " Gold)");
            string snap = StoreSnapshot(s);
            Check(StoreOffers.ClaimGrowth(s, 0, now).Reason == StoreRefusal.Claimed && StoreSnapshot(s) == snap, "and each milestone pays once");

            // Reaching the next stage opens the next claim.
            StageDef next = db.GetStage(def.Milestones[2].StageId);
            for (int i = 0; i < db.AllStages.Count; i++)
                if (db.AllStages[i].ChapterId == next.ChapterId) s.Profile.ClearedStageIds.Add(db.AllStages[i].Id);
            Check(StoreOffers.ClaimGrowth(s, 2, now).Success, "clearing 4-12 opens its milestone, claimed as the player reaches it");
        }

        // ------------------------------------------------------------------ 9. the paid track

        private static void CheckStorePass(ContentDatabase db)
        {
            long now = PassSeasonStart(db, 5) + 600;
            GameSession s = StoreSession(db, 3);
            WayfarersRoad.OnClaim(s, PassPointSource.Order, now);

            // The paid track cannot be claimed before it is bought.
            s.Profile.WayfarersRoad.Points = 30 * db.WayfarersRoad.TierPoints;
            Check(StoreOffers.ClaimPassTier(s, 5, null, now).Reason == StoreRefusal.NotBought, "the paid track cannot be claimed before it is bought");

            // Not sold while the daily orders are off: nothing could be earned.
            DailyLoopSwitch.Enabled = false;
            Check(StoreOffers.Check(s, "pass_premium", null, now) == StoreRefusal.PassInert, "the pass is not sold while the daily orders are off (it could earn no point)");
            DailyLoopSwitch.Enabled = true;

            StoreFulfilment buy = StoreOffers.Fulfil(s, "pass_premium", null, now);
            Check(buy.Success && buy.Grants.Count == 0 && StoreOffers.PassOwned(s, now), "buying the premium pass grants nothing at once and unlocks the paid track for this season");
            Check(StoreOffers.Check(s, "pass_premium", null, now + 86400L) == StoreRefusal.SoldOut, "one pass a season");

            // Every paid tier, with the pick for the selector.
            WayfarersRoadConfig road = db.WayfarersRoad;
            long goldBefore = s.Profile.Gold;
            bool allClaimed = true;
            long goldPaid = 0;
            int selectors = 0;
            for (int i = 0; i < road.Paid.Count; i++)
            {
                PassRewardDef p = road.Paid[i];
                string hero = null;
                if (p.Kind == PassRewardKind.Selector)
                {
                    List<HeroDef> pool = WayfarersRoad.SelectorHeroes(s, p.Stars);
                    hero = pool[0].Id;
                    selectors++;
                }
                StoreFulfilment c = StoreOffers.ClaimPassTier(s, p.Tier, hero, now);
                if (!c.Success) allClaimed = false;
                for (int g = 0; g < c.Grants.Count; g++) if (c.Grants[g].Kind == StoreGrantKind.Gold) goldPaid += c.Grants[g].Amount;
            }
            Check(allClaimed && s.Profile.Gold - goldBefore == goldPaid && s.Profile.Sealed.Gold == goldPaid && goldPaid > 0,
                  "every paid tier is claimed once the road reaches it, and its hourglasses are Sealed Gold (" + goldPaid + ")");
            HeroDef top = WayfarersRoad.SelectorHeroes(s, 5)[0];
            HeroInstance topInst = s.Profile.FindHeroByDefId(top.Id);
            Check(selectors == 1 && topInst != null && topInst.SealedCopiesHeld >= 1, "the 5-star at tier 30 is a SEALED copy (the free track's selector is an earned copy)");
            string snap = StoreSnapshot(s);
            Check(StoreOffers.ClaimPassTier(s, 5, null, now).Reason == StoreRefusal.Claimed && StoreSnapshot(s) == snap, "and each paid tier is claimed once");

            // A selector needs a hero of the right stars.
            GameSession b = StoreSession(db, 3);
            WayfarersRoad.OnClaim(b, PassPointSource.Order, now);
            b.Profile.WayfarersRoad.Points = 30 * db.WayfarersRoad.TierPoints;
            StoreOffers.Fulfil(b, "pass_premium", null, now);
            string snapB = StoreSnapshot(b);
            StoreRefusal bad = StoreOffers.ClaimPassTier(b, 30, null, now).Reason;
            HeroDef four = null;
            for (int i = 0; i < db.Heroes.Count && four == null; i++) if (db.Heroes[i].Stars == 4 && !db.Heroes[i].StartingHero) four = db.Heroes[i];
            StoreRefusal wrongStars = StoreOffers.ClaimPassTier(b, 30, four.Id, now).Reason;
            Check(bad == StoreRefusal.BadChoice && wrongStars == StoreRefusal.BadChoice && StoreSnapshot(b) == snapB,
                  "a selector with no pick or a 4-star pick is refused and writes nothing");

            // A new season: the premium lapses with the season and the paid claims reset.
            long nextSeason = PassSeasonStart(db, 6) + 600;
            Check(!StoreOffers.PassOwned(s, nextSeason) && StoreOffers.Check(s, "pass_premium", null, nextSeason) == StoreRefusal.None,
                  "THE PREMIUM IS PER SEASON: next season the paid track is locked again and the pass can be bought again");
            Check(!StoreOffers.PassOwned(s, now - 28 * 86400L) || StoreOffers.EffectiveDay(s, now - 28 * 86400L) >= StoreOffers.EffectiveDay(s, now),
                  "a clock wound back to an earlier season is read as the later one the store has already reached");
        }

        // ------------------------------------------------------------------ 10. the Hero Contract

        private static void CheckStoreHeroContract(ContentDatabase db)
        {
            long now = StoreMonday(db);
            GameSession s = StoreSession(db, 2);
            string hero = db.Store.Offer("starter_pact").HeroChoices[0];
            s.GrantHeroDirect(db.GetHero(hero), now);

            // OFF: not listed, not sold.
            StoreView off = StoreOffers.View(s, now);   // rolls the road's season stamp forward, as the road's own view does
            string before = StoreSnapshot(s);
            bool listed = false;
            for (int i = 0; i < off.Offers.Count; i++) if (off.Offers[i].Id == "hero_contract") listed = true;
            Check(!HeroContractSwitch.Enabled && !listed && StoreOffers.Check(s, "hero_contract", hero, now) == StoreRefusal.Unknown
                  && !StoreOffers.Fulfil(s, "hero_contract", hero, now).Success && StoreSnapshot(s) == before,
                  "THE HERO CONTRACT IS OFF BY DEFAULT: with its define off it is not listed, cannot be bought and writes nothing");

            HeroContractSwitch.Enabled = true;
            try
            {
                Check(StoreOffers.Check(s, "hero_contract", null, now) == StoreRefusal.NeedsChoice
                      && StoreOffers.Check(s, "hero_contract", StoreFourStar(db), now) == StoreRefusal.BadChoice
                      && StoreOffers.Check(s, "hero_contract", db.Store.Offer("starter_pact").HeroChoices[1], now) == StoreRefusal.NotOwned,
                      "with it on, a contract needs a hero, a 5-star, that the buyer owns");
                HeroInstance h = s.Profile.FindHeroByDefId(hero);
                int copies = h.Copies, sealedBefore = h.SealedCopiesHeld;
                Check(StoreOffers.Fulfil(s, "hero_contract", hero, now).Success && h.Copies == copies + 1 && h.SealedCopiesHeld == sealedBefore + 1,
                      "a contract adds one SEALED copy of the hero");
                Check(StoreOffers.Fulfil(s, "hero_contract", hero, now + 86400L).Success
                      && StoreOffers.Check(s, "hero_contract", hero, now + 2 * 86400L) == StoreRefusal.SoldOut,
                      "the limit is 2 a month for that hero");
                string other = db.Store.Offer("starter_pact").HeroChoices[1];
                s.GrantHeroDirect(db.GetHero(other), now);
                Check(StoreOffers.Check(s, "hero_contract", other, now) == StoreRefusal.None,
                      "and the count is per hero: a second hero's contract is still open");
                Check(StoreOffers.Check(s, "hero_contract", hero, StoreUnix(2030, 1, 1, 12)) == StoreRefusal.None,
                      "and it resets with the month");
            }
            finally
            {
                HeroContractSwitch.Enabled = false;
            }
        }

        // ------------------------------------------------------------------ 11. refusals

        private static void CheckStoreRefusalsWriteNothing(ContentDatabase db)
        {
            long now = StoreMonday(db);
            GameSession s = StoreSession(db, 2);
            string pick = db.Store.Offer("starter_pact").HeroChoices[0];
            StoreOffers.Fulfil(s, "starter_pact", pick, now);
            StoreOffers.Fulfil(s, "weekly_pack", null, now);
            string before = StoreSnapshot(s);
            StoreRefusal r1 = StoreOffers.Fulfil(s, "starter_pact", pick, now).Reason;
            StoreRefusal r2 = StoreOffers.Fulfil(s, "weekly_pack", null, now).Reason;
            StoreRefusal r3 = StoreOffers.Fulfil(s, "nonsense", null, now).Reason;
            GameSession t = StoreSession(db, 2);
            StoreRefusal r4 = StoreOffers.Fulfil(t, "starter_pact", null, now).Reason;
            StoreRefusal r5 = StoreOffers.Fulfil(t, "starter_pact", "kael_the_unyielding", now).Reason;
            GameSession u = StoreSession(db, 2);
            string beforeU = StoreSnapshot(u);
            StoreRefusal r7 = StoreOffers.ClaimGrowth(u, 0, now).Reason, r8 = StoreOffers.ClaimLedger(u, now).Reason, r9 = StoreOffers.ClaimPassTier(u, 5, null, now).Reason;
            Check(r1 == StoreRefusal.SoldOut && r2 == StoreRefusal.SoldOut && r3 == StoreRefusal.Unknown && StoreSnapshot(s) == before,
                  "REFUSALS WRITE NOTHING: a sold out offer and an unknown one are refused and the save is byte for byte as it was");
            Check(r4 == StoreRefusal.NeedsChoice && r5 == StoreRefusal.BadChoice && StoreOffers.Check(t, "weekly_pack", null, now) == StoreRefusal.None,
                  "an offer that needs a pick is refused without one, and with a hero not on its list");
            Check(r7 == StoreRefusal.NotBought && r8 == StoreRefusal.NotBought && r9 == StoreRefusal.NotBought && StoreSnapshot(u) == beforeU,
                  "claims on things that were not bought are refused and write nothing");
        }

        // ------------------------------------------------------------------ 12. nothing random

        private static void CheckStoreNothingRandom(string contentDir)
        {
            string root = SealedRepoRoot(contentDir);
            if (root == null) { Check(false, "THE STORE: the repository root was not found from " + contentDir); return; }
            string[] files =
            {
                "Assets/Scripts/Core/Progression/StoreOffers.cs",
                "Assets/Scripts/Core/Data/StoreConfig.cs",
                "Assets/Scripts/Core/Model/StoreState.cs",
                "Assets/Scripts/Core/Progression/SealedGrants.cs"
            };
            string[] banned = { "Random", "DeterministicRng", "NextDouble", "NextInt", ".Range(", "Shuffle", "Guid.NewGuid", "Environment.TickCount", ".Summon(", "SummonOutcome", "ArmorRoller", "RerollArmor", "CombineArmor", ".Convert(", "Pity", "TotalSummons" };
            List<string> hits = new List<string>();
            for (int i = 0; i < files.Length; i++)
            {
                string path = Path.Combine(root, files[i].Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) { hits.Add(files[i] + " (missing)"); continue; }
                string[] lines = File.ReadAllText(path).Split('\n');
                for (int l = 0; l < lines.Length; l++)
                {
                    string t = lines[l].TrimStart();
                    if (t.StartsWith("//") || t.StartsWith("///") || t.StartsWith("*")) continue;
                    for (int b = 0; b < banned.Length; b++)
                        if (lines[l].Contains(banned[b])) hits.Add(Path.GetFileName(files[i]) + ": " + banned[b]);
                }
            }
            Check(hits.Count == 0,
                  "NO PURCHASE GRANT PATH REACHES A RANDOM MECHANIC: StoreOffers, StoreConfig, StoreState and SealedGrants hold no random call, no summon, no roll, no conversion and no pity" + (hits.Count > 0 ? " (" + string.Join("; ", hits.ToArray()) + ")" : ""));

            // And a purchase leaves every counter a random mechanic reads where it was.
            ContentDatabase db = LoadContent(contentDir);
            GameSession s = StoreSession(db, 3);
            long now = StoreMonday(db);
            int summons = s.Profile.TotalSummons;
            string pick = db.Store.Offer("starter_pact").HeroChoices[0];
            foreach (string id in new[] { "starter_pact", "weekly_pack", "monthly_pack", "growth_fund", "ember_ledger" })
                StoreOffers.Fulfil(s, id, id == "starter_pact" ? pick : null, now);
            JsonValue before = JsonValue.Parse(SaveSerializer.Serialize(StoreSession(db, 3).Profile));
            JsonValue after = JsonValue.Parse(SaveSerializer.Serialize(s.Profile));
            Check(s.Profile.TotalSummons == summons && before.GetInt("totalSummons", -1) == after.GetInt("totalSummons", -1),
                  "a basket of every offer bought leaves the summon count and pity where they were");
        }

        // ------------------------------------------------------------------ 13. not hashed

        private static void CheckStoreNotHashed(ContentDatabase db, string contentDir)
        {
            ContentDatabase a = StoreDb(contentDir, StoreFileText(contentDir));
            ContentDatabase b = StoreDb(contentDir, StoreFileText(contentDir).Replace("\"hours\": 24", "\"hours\": 25"));
            ContentDatabase c = StoreDb(contentDir, null);
            Check(a.ContentVersion == b.ContentVersion && a.ContentVersion == c.ContentVersion && a.ContentVersion == db.ContentVersion
                  && a.Store.Offer("weekly_pack").Hours != b.Store.Offer("weekly_pack").Hours && !c.Store.IsUsable,
                  "store.json IS NOT HASHED: retuning a pack, or loading no file at all, leaves ContentVersion (" + db.ContentVersion + ") as it was");
        }

        // ------------------------------------------------------------------ 14. the save block

        private static void CheckStoreSave(ContentDatabase db)
        {
            long now = StoreMonday(db);
            GameSession s = StoreSession(db, 3);
            s.Profile.WayfarersRoad.Points = 0;
            foreach (string id in new[] { "starter_pact", "weekly_pack", "ember_ledger", "growth_fund" })
                StoreOffers.Fulfil(s, id, id == "starter_pact" ? db.Store.Offer("starter_pact").HeroChoices[0] : null, now);
            StoreOffers.ClaimLedger(s, now);
            StoreOffers.ClaimGrowth(s, 0, now);

            StoreState st = s.Profile.Store;
            string written = st.Write().ToString();
            StoreState back = StoreState.Read(JsonValue.Parse(written));
            Check(!st.IsEmpty && back.Write().ToString() == written, "the store block round-trips byte for byte (" + written.Length + " characters)");
            Check(StoreState.Read(null).IsEmpty && new StoreState().IsEmpty && StoreState.Read(JsonValue.Parse("{}")).IsEmpty && StoreState.Read(JsonValue.Parse("\"junk\"")).IsEmpty,
                  "an absent or damaged block reads as empty");
            StoreState clamp = StoreState.Read(JsonValue.Parse("{ \"clock\": -5, \"counts\": [ { \"key\": \"x\", \"n\": 99999, \"period\": -3 }, { \"key\": \"\", \"n\": 2 }, { \"key\": \"y\", \"n\": 0 } ],"
                + " \"ledgerStart\": 100, \"ledgerDays\": 99999, \"ledgerBonus\": 900, \"growth\": 99999999999, \"paid\": -1 }"));
            Check(clamp.ClockUnix == 0 && clamp.Counts.Count == 1 && clamp.Counts[0].Count == StoreState.CountMax && clamp.Counts[0].Period == 0
                  && clamp.LedgerDays == 366 && clamp.LedgerBonusPct == 100 && clamp.GrowthMask >= 0 && clamp.PaidMask == 0,
                  "every number is clamped to what the game could have written");
            Check(new StoreState().Write().ToString().Length > 0 && StoreSession(db, 1).Profile.Store.IsEmpty,
                  "a profile that never bought anything holds an empty store block, which is the document the build before wrote");
        }

        // ------------------------------------------------------------------ 15. the rollback

        private static void CheckStoreOff(ContentDatabase db)
        {
            long now = StoreMonday(db);
            bool was = StoreSwitch.Enabled;
            try
            {
                StoreSwitch.Enabled = false;
                GameSession off = StoreSession(db, 3);
                off.Profile.LastIdleClaimUnix = now;
                string before = StoreSnapshot(off);
                StoreView view = StoreOffers.View(off, now);
                StoreFulfilment f = StoreOffers.Fulfil(off, "weekly_pack", null, now);
                StoreRefusal c = StoreOffers.Check(off, "weekly_pack", null, now);
                StoreFulfilment l = StoreOffers.ClaimLedger(off, now);
                StoreFulfilment gr = StoreOffers.ClaimGrowth(off, 0, now);
                StoreFulfilment pa = StoreOffers.ClaimPassTier(off, 5, null, now);
                Check(!StoreOffers.Active(off) && !view.Active && view.Offers.Count == 0 && f.Reason == StoreRefusal.Off && c == StoreRefusal.Off
                      && l.Reason == StoreRefusal.Off && gr.Reason == StoreRefusal.Off && pa.Reason == StoreRefusal.Off && StoreSnapshot(off) == before,
                      "ROLLBACK: with the store switch off the view is empty, every call is refused as Off and the save is byte for byte as it was");

                // The idle claim is the claim it always was, even with a card in the save.
                GameSession onSession = StoreSession(db, 3);
                onSession.Profile.LastIdleClaimUnix = now;
                StoreSwitch.Enabled = true;
                StoreOffers.Fulfil(onSession, "ember_ledger", null, now);
                StoreSwitch.Enabled = false;
                long g0 = onSession.Profile.Gold, s0 = onSession.Profile.Sealed.Gold;
                IdleReport r = onSession.PeekIdle(now + 6 * 3600);
                onSession.ClaimIdle(now + 6 * 3600);
                Check(onSession.Profile.Gold - g0 == r.Gold && onSession.Profile.Sealed.Gold == s0,
                      "and with the switch off a card already in the save adds nothing to an idle claim: the claim is the claim it always was");

                // State already in a save is kept untouched.
                string kept = StoreSnapshot(onSession);
                StoreSwitch.Enabled = true;
                Check(!onSession.Profile.Store.IsEmpty && StoreSnapshot(onSession) == kept, "and a card or a purchase already in the save is kept and written back untouched");
            }
            finally
            {
                StoreSwitch.Enabled = was;
            }
        }

        // ------------------------------------------------------------------ 16. the value report

        private static void CheckStoreValueReport(ContentDatabase db)
        {
            Line("");
            Line("  OFFER VALUES (days of frontier income at the end of each chapter; a day = " + (2.0 * db.Balance.IdleCapHours) + " hours of idle, Dust day = " + db.Balance.RepeatFullClearsPerDay + " sweeps, hero = days of Shards for one random 5-star)");
            bool finite = true;
            for (int chapter = 1; chapter <= 6; chapter++)
            {
                GameSession s = StoreSession(db, chapter);
                string row = "  frontier " + chapter + "-12  idle gold/hour " + (long)s.IdleGoldPerHour + "  5-star every " + StoreOffers.DaysPerFiveStarPull(s).ToString("0.00") + " d:";
                for (int i = 0; i < db.Store.Offers.Count; i++)
                {
                    StoreOfferDef d = db.Store.Offers[i];
                    StoreValue v = StoreOffers.ValueOf(s, d);
                    if (double.IsNaN(v.TotalDays) || double.IsInfinity(v.TotalDays) || v.TotalDays < 0.0) finite = false;
                    row += "  " + d.Id + " " + v.TotalDays.ToString("0.0");
                }
                Line(row);
            }
            Check(finite, "every offer has a finite, non-negative value in days of income at every chapter's frontier");
        }
    }
}
