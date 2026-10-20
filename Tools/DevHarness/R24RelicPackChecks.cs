using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Emberfall.Core.Data;
using Emberfall.Core.Json;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Save;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// R24 RELIC PACKS (Docs/DESIGN-STORE.md): the Relic Dust Satchel, the Relic Dust Chest and the Relic of Choice.
    /// Fixed contents, limits and resets, unlocked with the Relic Shop, refused by every gamble, the purchase path,
    /// the sizing report that offer-values.md quotes, and the rollback.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckR24RelicPacks(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("R24 RELIC PACKS (Docs/DESIGN-STORE.md: Satchel, Chest and Relic of Choice, fixed, Sealed in effect, unlocked with the Relic Shop)");
            _offerContentDir = contentDir;
            bool packsWas = RelicPacksSwitch.Enabled, storeWas = StoreSwitch.Enabled, roadWas = WayfarersRoadSwitch.Enabled;
            bool loopWas = DailyLoopSwitch.Enabled, contractWas = HeroContractSwitch.Enabled, endgameWas = RelicEndgameSwitch.Enabled;
            try
            {
                StoreSwitch.Enabled = true;
                RelicPacksSwitch.Enabled = true;
                WayfarersRoadSwitch.Enabled = true;
                DailyLoopSwitch.Enabled = true;
                RelicEndgameSwitch.Enabled = true;
                CheckR24RelicPackContent(db);
                CheckR24RelicPackLocked(db);
                CheckR24RelicPackDustPacks(db);
                CheckR24RelicPackLimits(db);
                CheckR24RelicOfChoice(db);
                CheckR24RelicPackRefusedByGambles(db, contentDir);
                CheckR24RelicPackPurchasePath(db);
                CheckR24RelicPackSizing(db);
                CheckR24RelicPackRollback(db);
            }
            finally
            {
                RelicPacksSwitch.Enabled = packsWas;
                StoreSwitch.Enabled = storeWas;
                WayfarersRoadSwitch.Enabled = roadWas;
                DailyLoopSwitch.Enabled = loopWas;
                HeroContractSwitch.Enabled = contractWas;
                RelicEndgameSwitch.Enabled = endgameWas;
                StoreOffers.Bind(null);
            }
            Check(RelicPacksSwitch.Enabled == packsWas && RelicEndgameSwitch.Enabled == endgameWas, "R24 RELIC PACKS: the switches are put back as the run found them");
        }

        // ------------------------------------------------------------------ helpers

        private static readonly string[] R24PackIds = { "relic_dust_satchel", "relic_dust_chest", "relic_of_choice" };

        /// <summary>A session with the Relic Shop open (everything to 3-12 cleared).</summary>
        private static GameSession R24RelicSession(ContentDatabase db, int chapter)
        {
            return OfferSession(db, chapter);
        }

        /// <summary>Relic Dust a day for a committed player: the 12 highest cleared stages, each swept as often as the repeat cap allows
        /// (R17 relic-pricing.md). The yardstick the pack sizes are quoted against.</summary>
        private static long R24CommittedDustDay(GameSession s)
        {
            List<StageDef> cleared = new List<StageDef>();
            for (int i = 0; i < s.Content.AllStages.Count; i++)
                if (s.Profile.HasClearedStage(s.Content.AllStages[i].Id)) cleared.Add(s.Content.AllStages[i]);
            cleared.Sort(delegate (StageDef a, StageDef b) { return b.GlobalIndex.CompareTo(a.GlobalIndex); });
            long total = 0;
            for (int i = 0; i < 12 && i < cleared.Count; i++) total += s.RelicDustForRepeatClear(cleared[i]);
            return total * Math.Max(1, s.Balance.RepeatFullClearsPerDay);
        }

        private static StoreOfferView R24OfferView(GameSession s, string offerId, long now)
        {
            StoreView v = StoreOffers.View(s, now);
            for (int i = 0; i < v.Offers.Count; i++) if (v.Offers[i].Id == offerId) return v.Offers[i];
            return null;
        }

        private static string R24FirstRelic(GameSession s)
        {
            List<RelicPick> picks = RelicPacks.Picks(s, s.Content.Store.Offer("relic_of_choice").PickCapDust);
            return picks.Count > 0 ? picks[0].RelicId : null;
        }

        // ------------------------------------------------------------------ 1. content

        private static void CheckR24RelicPackContent(ContentDatabase db)
        {
            StoreConfig c = db.Store;
            StoreOfferDef sat = c.Offer("relic_dust_satchel"), chest = c.Offer("relic_dust_chest"), choice = c.Offer("relic_of_choice");
            Check(c.Warnings.Count == 0 && sat != null && chest != null && choice != null,
                  "the shipped store.json loads clean and lists the Satchel, the Chest and the Relic of Choice");
            Check(sat.PriceEur == 4.99 && sat.Limit == 1 && sat.Reset == StoreReset.Week && sat.Dust == 5000 && sat.Kind == StoreOfferKind.Pack,
                  "RELIC DUST SATCHEL: EUR 4.99, 1 a week, 5,000 Relic Dust (one Legendary relic's price)");
            Check(chest.PriceEur == 19.99 && chest.Limit == 2 && chest.Reset == StoreReset.Month && chest.Dust == 5 * sat.Dust,
                  "RELIC DUST CHEST: EUR 19.99, 2 a month (the whale read cut it from 1 a week), five times the Satchel (" + chest.Dust + " Relic Dust)");
            Check(choice.PriceEur == 29.99 && choice.Limit == 1 && choice.Reset == StoreReset.Month && choice.Kind == StoreOfferKind.RelicChoice && choice.PickCapDust == 20000,
                  "RELIC OF CHOICE: EUR 29.99, 1 a month, the buyer names the relic, worth at most 20,000 Relic Dust at the shop");
            Check(sat.HasRequires && sat.Requires == Feature.Relics && chest.Requires == Feature.Relics && choice.Requires == Feature.Relics
                  && sat.Group == "relicPacks" && chest.Group == "relicPacks" && choice.Group == "relicPacks",
                  "all three open with the Relic Shop (requires Relics) and sit behind the relic packs switch");
            Check(db.Onboarding != null && FeatureGate.UnlockStage(db, Feature.Relics) != null,
                  "and the Relic Shop's unlock stage is read from onboarding.json's table, not repeated in store.json");
            // the grants are the closed fixed kinds
            Check(sat.Built.Grant.Items.Count == 1 && sat.Built.Grant.Items[0].Kind == GrantKind.Dust && sat.Built.Grant.Items[0].Amount == 5000
                  && chest.Built.Grant.Items.Count == 1 && chest.Built.Grant.Items[0].Kind == GrantKind.Dust
                  && choice.Built.Grant.Items.Count == 1 && choice.Built.Grant.Items[0].Kind == GrantKind.RelicChoice
                  && choice.Built.Grant.NeedsChoice && !sat.Built.Grant.NeedsChoice,
                  "the grants are one Dust line each and one RelicChoice line, built by StoreConfig.Build, through PurchaseGrants.Apply");

            string bad = "{ \"tiers\": { \"mid\": 4.99 }, \"offers\": [ { \"id\": \"x\", \"name\": \"x\", \"kind\": \"pack\", \"tier\": \"mid\", \"productId\": \"p\", \"dust\": 10, \"requires\": \"Nonsense\" } ] }";
            StoreConfig parsed = StoreConfig.FromJson(bad);
            Check(parsed.Offers.Count == 0 && parsed.Warnings.Count == 1, "an unknown required feature drops the offer with a warning");
        }

        // ------------------------------------------------------------------ 2. locked until the Relic Shop opens

        private static void CheckR24RelicPackLocked(ContentDatabase db)
        {
            long now = OfferMonday(db);
            GameSession s = R24RelicSession(db, 2);                          // 2-12 cleared, 3-1 not
            StageDef unlock = FeatureGate.UnlockStage(db, Feature.Relics);
            Check(unlock != null && !s.Profile.HasClearedStage(unlock.Id), "setup: the player is at 2-12, before the Relic Shop's stage " + (unlock != null ? unlock.Id : "?"));
            string before = OfferSnapshot(s);
            int listed = 0, locked = 0;
            for (int i = 0; i < R24PackIds.Length; i++)
            {
                StoreOfferView o = R24OfferView(s, R24PackIds[i], now);
                if (o == null) continue;
                listed++;
                if (!o.Available && o.Reason == StoreRefusal.Locked && o.LockLine.StartsWith("UNLOCKS AT ")) locked++;
            }
            Check(listed == 3 && locked == 3, "LOCKED, NOT HIDDEN: before the Relic Shop opens the three packs are listed with the lock line (" + FeatureGate.LockLine(db, Feature.Relics) + ")");
            Check(StoreOffers.Check(s, "relic_dust_satchel", null, now) == StoreRefusal.Locked && StoreOffers.CheckAny(s, "relic_of_choice", now) == StoreRefusal.Locked
                  && !StoreOffers.Fulfil(s, "relic_dust_chest", null, now).Success && OfferSnapshot(s) == before,
                  "and they cannot be bought, and the refusal writes nothing");
            StoreOffers.Bind(s);
            PurchaseLedger ledger = new PurchaseLedger(StoreRules.Shared);
            Check(!ledger.Begin(s, "relic_dust_satchel", null, now).Success && ledger.Begin(s, "relic_dust_satchel", null, now).Reason == PurchaseRefusal.NotAvailable,
                  "the purchase path refuses at Begin too");
            s.Profile.ClearedStageIds.Add(unlock.Id);
            Check(StoreOffers.Check(s, "relic_dust_satchel", null, now) == StoreRefusal.None && StoreOffers.CheckAny(s, "relic_of_choice", now) == StoreRefusal.None,
                  "clearing " + unlock.Id + " opens all three");
        }

        // ------------------------------------------------------------------ 3. the Dust packs

        private static void CheckR24RelicPackDustPacks(ContentDatabase db)
        {
            long now = OfferMonday(db);
            GameSession s = R24RelicSession(db, 4);
            long dust0 = s.Profile.RelicDust, gold0 = s.Profile.Gold, shards0 = s.Profile.Shards, slag0 = s.Profile.Slag;
            long freeGold = s.Profile.FreeGold, freeShards = s.Profile.FreeShards, freeSlag = s.Profile.FreeSlag;
            StoreFulfilment a = StoreOffers.Fulfil(s, "relic_dust_satchel", null, now);
            StoreFulfilment b = StoreOffers.Fulfil(s, "relic_dust_chest", null, now);
            Check(a.Success && b.Success && s.Profile.RelicDust == dust0 + 5000 + 25000 && a.Grant.Dust == 5000 && b.Grant.Dust == 25000,
                  "FIXED CONTENTS: the Satchel pays exactly 5,000 Relic Dust and the Chest exactly 25,000, whatever the buyer's frontier");
            Check(s.Profile.Gold == gold0 && s.Profile.Shards == shards0 && s.Profile.Slag == slag0
                  && s.Profile.FreeGold == freeGold && s.Profile.FreeShards == freeShards && s.Profile.FreeSlag == freeSlag && s.Profile.Sealed.IsEmpty,
                  "and they pay nothing else: no Gold, Shards or Slag moved, so the purses a random mechanic spends are exactly as they were");

            GameSession early = R24RelicSession(db, 3), late = R24RelicSession(db, 6);
            long earlyDust = early.Profile.RelicDust, lateDust = late.Profile.RelicDust;
            StoreOffers.Fulfil(early, "relic_dust_satchel", null, now);
            StoreOffers.Fulfil(late, "relic_dust_satchel", null, now);
            Check(early.Profile.RelicDust - earlyDust == 5000 && late.Profile.RelicDust - lateDust == 5000,
                  "the amount is the same at 3-12 and at 6-12 (a fixed number, never a function of the frontier)");

            // the card shows it in full, with the price, before the purchase
            StoreOfferView o = R24OfferView(R24RelicSession(db, 3), "relic_dust_chest", now);
            Check(o != null && o.PriceLabel == "EUR 19.99" && o.Lines.Count == 1 && o.Lines[0] == "25,000 Relic Dust" && o.LimitLabel == "2 a month",
                  "THE CARD SHOWS IT IN FULL: '" + (o != null && o.Lines.Count > 0 ? o.Lines[0] : "") + "' for " + (o != null ? o.PriceLabel : "?") + ", " + (o != null ? o.LimitLabel : "?"));

            // deterministic: two equal sessions buying the same thing hold the same save
            GameSession d1 = R24RelicSession(db, 4), d2 = R24RelicSession(db, 4);
            StoreOffers.Fulfil(d1, "relic_dust_chest", null, now);
            StoreOffers.Fulfil(d2, "relic_dust_chest", null, now);
            Check(OfferSnapshot(d1) == OfferSnapshot(d2), "DETERMINISTIC: two equal profiles buying the Chest hold byte-identical saves");
        }

        // ------------------------------------------------------------------ 4. limits and resets

        private static void CheckR24RelicPackLimits(ContentDatabase db)
        {
            long monday = OfferMonday(db);
            GameSession s = R24RelicSession(db, 4);
            long may = OfferUnix(2027, 5, 3, 12);   // a Monday
            Check(StoreOffers.Fulfil(s, "relic_dust_satchel", null, may).Success && StoreOffers.Fulfil(s, "relic_dust_chest", null, may).Success,
                  "the Satchel and the Chest are separate offers: both can be bought in the same week");
            string before = OfferSnapshot(s);
            Check(StoreOffers.Fulfil(s, "relic_dust_satchel", null, may + 3 * 86400L).Reason == StoreRefusal.SoldOut && OfferSnapshot(s) == before,
                  "LIMIT: a second Satchel in the same Monday-to-Sunday week is refused and writes nothing");
            Check(StoreOffers.Check(s, "relic_dust_satchel", null, may + 7 * 86400L) == StoreRefusal.None, "RESET: the Satchel is open again the next Monday");
            Check(StoreOffers.Fulfil(s, "relic_dust_chest", null, may + 8 * 86400L).Success
                  && StoreOffers.Fulfil(s, "relic_dust_chest", null, may + 9 * 86400L).Reason == StoreRefusal.SoldOut,
                  "the Chest is 2 a month: a second one later in the month is bought, the third is refused");
            StoreOffers.Fulfil(s, "relic_dust_satchel", null, may + 7 * 86400L);
            Check(StoreOffers.Check(s, "relic_dust_satchel", null, may - 20 * 86400L) == StoreRefusal.SoldOut
                  && StoreOffers.Check(s, "relic_dust_satchel", null, may + 1 * 86400L) == StoreRefusal.SoldOut
                  && StoreOffers.Check(s, "relic_dust_chest", null, OfferUnix(2020, 1, 1, 12)) == StoreRefusal.SoldOut,
                  "A CLOCK WOUND BACK REOPENS NOTHING: weeks and years back read the later clock the store already reached");
            Check(StoreOffers.Check(s, "relic_dust_chest", null, OfferUnix(2027, 6, 1, 12)) == StoreRefusal.None,
                  "RESET: the Chest is open again on the first reward day of the next month");
            StoreOfferView v = R24OfferView(s, "relic_dust_chest", may + 9 * 86400L);
            Check(v != null && !v.Available && v.ResetInSeconds > 0 && v.ResetInSeconds <= 31 * 86400L, "and the card says how long until it comes back");
        }

        // ------------------------------------------------------------------ 5. the Relic of Choice

        private static void CheckR24RelicOfChoice(ContentDatabase db)
        {
            long may = OfferUnix(2027, 5, 10, 12);
            GameSession s = R24RelicSession(db, 3);
            int cap = db.Store.Offer("relic_of_choice").PickCapDust;
            List<RelicPick> picks = RelicPacks.Picks(s, cap);
            Check(picks.Count == db.Relics.Count && picks.Count > 0 && !picks[0].Upgrade && picks[0].FromTier == 0 && picks[0].ToTier == 1,
                  "THE PICK LIST IS THE SHOP'S: with no relic owned every relic of relics.json is on it, in its order, as a new relic (" + picks.Count + ")");
            bool underCap = true;
            for (int i = 0; i < picks.Count; i++) if (picks[i].DustWorth > cap) underCap = false;
            Check(underCap && RelicPacks.Picks(s, 0).Count == picks.Count, "every pick is worth at most " + cap + " Relic Dust at the shop (the dearest new relic is a Mythic at that price)");
            string pickA = picks[0].RelicId, pickB = picks[1].RelicId;
            StoreOfferView o = R24OfferView(s, "relic_of_choice", may);
            Check(o != null && o.NeedsChoice && o.Choices.Count == picks.Count && o.Choices[0].HeroId == pickA && o.Choices[0].Role.Contains("worth") && o.Lines.Count >= 3
                  && StoreOffers.Check(s, "relic_of_choice", null, may) == StoreRefusal.NeedsChoice,
                  "THE CARD NAMES EVERY PICK before the purchase, each with what it is worth at the shop, and a purchase with no pick is refused");

            string before = OfferSnapshot(s);
            Check(StoreOffers.Check(s, "relic_of_choice", "no_such_relic", may) == StoreRefusal.BadChoice
                  && !StoreOffers.Fulfil(s, "relic_of_choice", "no_such_relic", may).Success && OfferSnapshot(s) == before,
                  "a relic id that is not in the content is refused and writes nothing");

            long dust = s.Profile.RelicDust;
            StoreFulfilment f1 = StoreOffers.Fulfil(s, "relic_of_choice", pickA, may);
            Check(f1.Success && s.Profile.OwnsRelic(pickA) && s.Profile.RelicTier(pickA) == 1 && s.Profile.RelicDust == dust && f1.Grant.RelicId == pickA,
                  "A NEW RELIC: the named relic is owned at tier 1, the buyer's Dust is untouched (the pack is not a shop purchase)");
            Check(RelicPacks.Picks(s, cap)[0].Upgrade && RelicPacks.Picks(s, cap)[0].RelicId == pickA && RelicPacks.Picks(s, cap)[0].FromTier == 1 && RelicPacks.Picks(s, cap)[0].ToTier == 2,
                  "the owned relic is now offered as an upgrade, tier 1 to 2");
            before = OfferSnapshot(s);
            Check(StoreOffers.Fulfil(s, "relic_of_choice", pickB, may + 86400L).Reason == StoreRefusal.SoldOut && OfferSnapshot(s) == before
                  && StoreOffers.Check(s, "relic_of_choice", pickB, OfferUnix(2020, 1, 1, 12)) == StoreRefusal.SoldOut,
                  "LIMIT: once a month, and a clock wound back reopens nothing");

            long june = OfferUnix(2027, 6, 2, 12), july = OfferUnix(2027, 7, 2, 12), august = OfferUnix(2027, 8, 2, 12);
            StoreFulfilment f2 = StoreOffers.Fulfil(s, "relic_of_choice", pickA, june);
            Check(f2.Success && s.Profile.RelicTier(pickA) == 2 && f2.Grant.Lines[0].Contains("raised to tier 2"),
                  "AN UPGRADE: next month the same pick raises the owned relic one tier (to 2)");
            StoreFulfilment f3 = StoreOffers.Fulfil(s, "relic_of_choice", pickA, july);
            Check(f3.Success && s.Profile.RelicTier(pickA) == s.RelicMaxTier && s.RelicMaxTier == 3, "and the month after to the top tier (3)");
            before = OfferSnapshot(s);
            Check(StoreOffers.Check(s, "relic_of_choice", pickA, august) == StoreRefusal.BadChoice && StoreOffers.Fulfil(s, "relic_of_choice", pickA, august).Success == false
                  && OfferSnapshot(s) == before && RelicPacks.Find(s, pickA, cap) == null,
                  "a relic at the top tier cannot be picked: one tier at most per pick, never past the ceiling");

            // an upgrade tier is worth what the shop charges for it
            GameSession u = R24RelicSession(db, 3);
            u.GrantRelic(pickB);
            RelicPick up = RelicPacks.Find(u, pickB, cap);
            Check(up != null && up.Upgrade && up.DustWorth == u.RelicUpgradePrice(db.GetRelic(pickB), 2),
                  "an upgrade pick's worth is the Relic Shop's own price for that tier (" + (up != null ? up.DustWorth.ToString() : "?") + " Relic Dust)");

            // THE CAP: a pick dearer than the cap is not on the list (a Mythic raised to tier 2 or 3, 40,000 and 80,000)
            GameSession dear = R24RelicSession(db, 3);
            string mythic = null;
            for (int i = 0; i < db.Relics.Count; i++) if (db.Relics[i].Grade == RelicGrade.Mythic) { mythic = db.Relics[i].Id; break; }
            dear.GrantRelic(mythic);
            int mythicTier2 = dear.RelicUpgradePrice(db.GetRelic(mythic), 2);
            Check(mythic != null && mythicTier2 > cap && RelicPacks.Find(dear, mythic, 0) != null && RelicPacks.Find(dear, mythic, cap) == null
                  && StoreOffers.Check(dear, "relic_of_choice", mythic, may) == StoreRefusal.BadChoice,
                  "THE CAP: an owned Mythic's next tier costs " + mythicTier2 + " Relic Dust at the shop, above the cap, so it cannot be named (the shop still sells it for Dust)");

            // nothing to pick: every relic at the top tier
            GameSession all = R24RelicSession(db, 3);
            for (int i = 0; i < db.Relics.Count; i++) { all.GrantRelic(db.Relics[i].Id); all.Profile.FindOwnedRelic(db.Relics[i].Id).Tier = all.RelicMaxTier; }
            Check(StoreOffers.CheckAny(all, "relic_of_choice", may) == StoreRefusal.NoPick && RelicPacks.Picks(all, cap).Count == 0,
                  "with every relic owned at the top tier there is nothing to pick and the offer says so");

            // the endgame off: upgrades do not exist, only new relics can be named
            RelicEndgameSwitch.Enabled = false;
            try
            {
                GameSession noEnd = R24RelicSession(db, 3);
                noEnd.GrantRelic(pickA);
                RelicPick shown = RelicPacks.Find(noEnd, pickA, cap);
                Check(shown == null && RelicPacks.Find(noEnd, pickB, cap) != null && !RelicPacks.Find(noEnd, pickB, cap).Upgrade,
                      "with the relic endgame off an owned relic cannot be raised, so only relics the buyer lacks are on the list");
            }
            finally { RelicEndgameSwitch.Enabled = true; }

            // the save: an upgraded relic is a version 24 document and survives a reload
            string json = SaveSerializer.Serialize(s.Profile);
            string error;
            PlayerProfile back = SaveSerializer.Deserialize(json, db, out error);
            Check(error == null && SaveSerializer.Serialize(back) == json && back.RelicTier(pickA) == 3 && back.OwnsRelic(pickA)
                  && JsonValue.Parse(json).GetInt("version", 0) >= SaveSerializer.CoreGapsVersion,
                  "THE SAVE: relics owned and upgraded by the pack round-trip byte for byte, in a version " + SaveSerializer.VersionOfJson(json) + " document");
        }

        // ------------------------------------------------------------------ 6. refused by every gamble

        private static void CheckR24RelicPackRefusedByGambles(ContentDatabase db, string contentDir)
        {
            long now = OfferMonday(db);
            GameSession s = R24RelicSession(db, 4);
            s.Profile.Shards = 0;
            long free = s.Profile.FreeGold, freeS = s.Profile.FreeShards, freeSlag = s.Profile.FreeSlag;
            StoreOffers.Fulfil(s, "relic_dust_satchel", null, now);
            StoreOffers.Fulfil(s, "relic_dust_chest", null, now);
            StoreOffers.Fulfil(s, "relic_of_choice", R24FirstRelic(s), now);
            Check(s.Profile.Shards == 0 && !s.CanSummon && s.Profile.FreeGold == free && s.Profile.FreeShards == freeS && s.Profile.FreeSlag == freeSlag && s.Profile.Sealed.IsEmpty,
                  "SUMMON AND FORGE: all three bought, no Shard, Gold or Slag appeared in any purse a random mechanic spends");
            string snap = OfferSnapshot(s);
            SummonOutcome refused = s.Summon(4242, now);
            Check(!refused.Success && OfferSnapshot(s) == snap, "and a summon attempt is refused with the save byte for byte as it was");

            // Conversion cannot see a relic or Dust: no hero body was added.
            int heroesBefore = R24RelicSession(db, 4).Profile.Heroes.Count;
            Check(s.Profile.Heroes.Count == heroesBefore, "CONVERSION: no hero copy, spare or Sealed, arrived with the packs");

            // Dust and relics have exactly one way out, and it is a fixed price at the Relic Shop.
            string root = SealedRepoRoot(contentDir);
            if (root == null) { Check(false, "R24 RELIC PACKS: the repository root was not found from " + contentDir); return; }
            string[] core = Directory.GetFiles(Path.Combine(Path.Combine(root, "Assets"), Path.Combine("Scripts", "Core")), "*.cs", SearchOption.AllDirectories);
            int spends = 0, spendCalls = 0, removals = 0;
            List<string> spenders = new List<string>();
            for (int i = 0; i < core.Length; i++)
            {
                string text = File.ReadAllText(core[i]);
                string[] lines = text.Split('\n');
                for (int l = 0; l < lines.Length; l++)
                {
                    string t = lines[l].TrimStart();
                    if (t.StartsWith("//") || t.StartsWith("*") || t.StartsWith("///")) continue;
                    if (lines[l].Contains("RelicDust -=")) { spends++; spenders.Add(Path.GetFileName(core[i])); }
                    if (lines[l].Contains("SpendRelicDust(") && !lines[l].Contains("private void SpendRelicDust")) { spendCalls++; spenders.Add(Path.GetFileName(core[i])); }
                    if (lines[l].Contains("OwnedRelics.Remove") || lines[l].Contains("OwnedRelics.Clear")) removals++;
                }
            }
            Check(spends == 1 && spendCalls == 2 && removals == 0,
                  "RELIC DUST AND RELICS HAVE NO RANDOM SINK: Dust is lowered in exactly one place (" + spends + ") called only by BuyRelic and UpgradeRelic (" + spendCalls + " calls), and no code removes an owned relic (" + removals + ")");

            string[] files = { "Assets/Scripts/Core/Progression/RelicPacks.cs" };
            string[] banned = { "Random", "DeterministicRng", "NextDouble", "NextInt", ".Range(", "Shuffle", "Guid.NewGuid", "Environment.TickCount", ".Summon(", "SummonOutcome", "ArmorRoller", "RerollArmor", "CombineArmor", ".Convert(", "Pity", "TotalSummons" };
            List<string> hits = new List<string>();
            for (int i = 0; i < files.Length; i++)
            {
                string[] lines = File.ReadAllText(Path.Combine(root, files[i].Replace('/', Path.DirectorySeparatorChar))).Split('\n');
                for (int l = 0; l < lines.Length; l++)
                {
                    string t = lines[l].TrimStart();
                    if (t.StartsWith("//") || t.StartsWith("*")) continue;
                    for (int b = 0; b < banned.Length; b++) if (lines[l].Contains(banned[b])) hits.Add(Path.GetFileName(files[i]) + ": " + banned[b]);
                }
            }
            Check(hits.Count == 0, "RelicPacks.cs, the pick list and the grant, holds no random call, no summon, no roll, no conversion and no pity" + (hits.Count > 0 ? " (" + string.Join("; ", hits.ToArray()) + ")" : ""));

            string storeText = File.ReadAllText(Path.Combine(contentDir, "store.json"));
            string[] words = { "chance", "roll", "pool", "pity", "weight", "probability" };
            List<string> wordHits = new List<string>();
            foreach (string line in storeText.Split('\n'))
            {
                string t = line.TrimStart();
                if (t.StartsWith("//")) continue;
                for (int w = 0; w < words.Length; w++) if (t.ToLowerInvariant().Contains("\"" + words[w])) wordHits.Add(words[w]);
            }
            Check(wordHits.Count == 0, "and store.json carries no key named chance, roll, pool, pity, weight or probability");
        }

        // ------------------------------------------------------------------ 7. the purchase path

        private static void CheckR24RelicPackPurchasePath(ContentDatabase db)
        {
            long now = OfferMonday(db);
            GameSession s = R24RelicSession(db, 4);
            StoreOffers.Bind(s);
            PurchaseLedger ledger = new PurchaseLedger(StoreRules.Shared);
            long dust0 = s.Profile.RelicDust;
            PurchaseResult b1 = ledger.Begin(s, "relic_dust_satchel", null, now);
            PurchaseResult c1 = ledger.Complete(s, "relic_dust_satchel", "r24-sat-1", now);
            Check(b1.Success && c1.Success && s.Profile.RelicDust == dust0 + 5000 && s.Profile.Store.FindReceipt("r24-sat-1") != null,
                  "THE PURCHASE PATH: Begin and Complete grant the Satchel once and keep the receipt");
            PurchaseResult again = ledger.Complete(s, "relic_dust_satchel", "r24-sat-1", now);
            Check(!again.Success && again.Reason == PurchaseRefusal.AlreadyGranted && s.Profile.RelicDust == dust0 + 5000,
                  "ONCE PER RECEIPT: the same receipt delivered again grants nothing");
            Check(!ledger.Begin(s, "relic_dust_satchel", null, now + 86400L).Success && ledger.Begin(s, "relic_dust_satchel", null, now - 9 * 86400L).Reason == PurchaseRefusal.NotAvailable
                  && ledger.Begin(s, "relic_dust_satchel", null, now + 7 * 86400L).Success,
                  "and Begin is refused later in the week and with the clock wound back, and opens the next Monday");
            ledger.Abandon("relic_dust_satchel");

            string before = OfferSnapshot(s);
            PurchaseResult noPick = ledger.Begin(s, "relic_of_choice", null, now);
            PurchaseResult badPick = ledger.Begin(s, "relic_of_choice", "no_such_relic", now);
            Check(!noPick.Success && noPick.Reason == PurchaseRefusal.BadChoice && noPick.Message == "Pick a relic first." && !badPick.Success && badPick.Reason == PurchaseRefusal.NotAvailable && OfferSnapshot(s) == before,
                  "THE CHOICE IS MADE BEFORE THE PURCHASE: no pick and an unknown relic are refused at Begin with the save untouched");
            string pick = R24FirstRelic(s);
            PurchaseResult b2 = ledger.Begin(s, "relic_of_choice", pick, now);
            PurchaseResult c2 = ledger.Complete(s, "relic_of_choice", "r24-choice-1", now);
            Check(b2.Success && c2.Success && s.Profile.OwnsRelic(pick) && c2.Grant.RelicId == pick,
                  "a named relic is granted by Complete");
            PurchaseResult noChoiceDelivered = ledger.Complete(s, "relic_of_choice", "r24-choice-2", "", now + 40 * 86400L);
            Check(!noChoiceDelivered.Success && noChoiceDelivered.Reason == PurchaseRefusal.BadChoice && noChoiceDelivered.Message == "Pick a relic first." && !noChoiceDelivered.CanConfirm,
                  "a Relic of Choice the store delivers again with no pick is refused as 'Pick a relic first.' and left unconfirmed, so nothing is lost and nothing is guessed");
            PurchaseResult finished = ledger.Complete(s, "relic_of_choice", "r24-choice-2", R24FirstRelic(s), now + 40 * 86400L);
            Check(finished.Success && s.Profile.RelicTier(pick) == 2, "and finishes once the player has chosen (the first relic, now owned, is raised to tier 2)");
            Check(!StoreRules.Shared.Find("relic_dust_satchel").Restorable && !StoreRules.Shared.Find("relic_of_choice").Restorable,
                  "none of the three is restorable: a pack is used up");
        }

        // ------------------------------------------------------------------ 8. sizing, in days

        private static void CheckR24RelicPackSizing(ContentDatabase db)
        {
            Line("");
            Line("  RELIC PACK SIZING in days of a committed player's own Relic Dust income (12 highest stages, 3 sweeps each; R17 relic-pricing.md). PROVISIONAL until the whale curve.");
            string[] stages = { "ch3_1", "ch3_12", "ch4_12", "ch5_12", "ch6_12" };
            int[] chapters = { 3, 3, 4, 5, 6 };
            double weeks62 = 0.0, weeks64 = 0.0;
            for (int i = 0; i < stages.Length; i++)
            {
                GameSession s = R24RelicSession(db, chapters[i]);
                if (i == 0) { s = R24RelicSession(db, 2); s.Profile.ClearedStageIds.Add("ch3_1"); }
                long day = R24CommittedDustDay(s);
                List<RelicPick> picks = RelicPacks.Picks(s, db.Store.Offer("relic_of_choice").PickCapDust);
                int least, most;
                RelicPacks.WorthRange(picks, out least, out most);
                // a Mythic's last tier is the dearest single pick any player can name
                int dearest = 0;
                for (int k = 0; k < db.Relics.Count; k++)
                {
                    int price = s.RelicPrice(db.Relics[k]);
                    int top = s.RelicUpgradePrice(db.Relics[k], s.RelicMaxTier);
                    if (price > dearest) dearest = price;
                    if (top > dearest) dearest = top;
                }
                double satDays = 5000.0 / day, chestDays = 25000.0 / day;
                // The cost of one tier STEP across the roster: every relic bought (tier 1), then raised to 2, then to 3.
                long buyAll = 0, tier2All = 0, tier3All = 0;
                for (int k = 0; k < db.Relics.Count; k++)
                {
                    buyAll += s.RelicPrice(db.Relics[k]);
                    tier2All += s.RelicUpgradePrice(db.Relics[k], 2);
                    tier3All += s.RelicUpgradePrice(db.Relics[k], 3);
                }
                // A whale buys every Satchel (a week) and every Chest (two a month) and the Choice every month. In the weeks a
                // step takes, the Choice gives ONE relic's share of that step a month (the step's cost over the ten relics),
                // which is the honest amortisation: it cannot name a tier-3 Mythic before the steps under it are done.
                StoreOfferDef satchel = db.Store.Offer("relic_dust_satchel"), chestOffer = db.Store.Offer("relic_dust_chest");
                double packWeek = satchel.Dust + chestOffer.Dust * chestOffer.Limit / 4.345;
                double wBuy = buyAll / (7.0 * day + packWeek + buyAll / (double)db.Relics.Count / 4.345);
                double w2 = tier2All / (7.0 * day + packWeek + tier2All / (double)db.Relics.Count / 4.345);
                double w3 = tier3All / (7.0 * day + packWeek + tier3All / (double)db.Relics.Count / 4.345);
                double nBuy = buyAll / (7.0 * day), n2 = tier2All / (7.0 * day), n3 = tier3All / (7.0 * day);
                Line("    " + stages[i] + ": committed " + day.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " Dust/day.  Satchel " + satDays.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " d, Chest " + chestDays.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " d, Choice worth "
                     + (least / (double)day).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " to " + (most / (double)day).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " d (dearest tier-3 Mythic " + (dearest / (double)day).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " d).");
                Line("           roster weeks, own income -> whale (all three every period): buy all " + nBuy.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " -> " + wBuy.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ", tier 2 " + n2.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " -> " + w2.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                     + ", tier 3 " + n3.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " -> " + w3.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
                if (stages[i] == "ch6_12") { weeks62 = w2; weeks64 = w3; }
            }
            Check(weeks62 >= 2.5 && weeks64 >= 5.0,
                  "A WHALE BUYING ALL THREE EVERY PERIOD STILL NEEDS WEEKS PER RELIC TIER AT 6-12: tier 2 across the roster " + weeks62.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " weeks, tier 3 " + weeks64.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " weeks");
        }

        // ------------------------------------------------------------------ 9. rollback

        private static void CheckR24RelicPackRollback(ContentDatabase db)
        {
            long now = OfferMonday(db);
            RelicPacksSwitch.Enabled = false;
            HeroContractSwitch.Enabled = false;
            GameSession s = R24RelicSession(db, 4);
            StoreOffers.Bind(s);
            string before = OfferSnapshot(s);
            StoreView v = StoreOffers.View(s, now);
            List<string> ids = new List<string>();
            for (int i = 0; i < v.Offers.Count; i++) ids.Add(v.Offers[i].Id);
            string expected = "starter_pact,weekly_pack,monthly_pack,ember_ledger,growth_fund,pass_premium";
            string joined = string.Join(",", ids.ToArray());
            Check(joined.StartsWith(expected, StringComparison.Ordinal) && !ids.Contains("relic_dust_satchel") && !ids.Contains("relic_dust_chest") && !ids.Contains("relic_of_choice"),
                  "ROLLBACK: with the relic packs switch off the Store lists the offers it listed before R24 and none of the three");
            Check(StoreOffers.Check(s, "relic_dust_satchel", null, now) == StoreRefusal.Unknown && StoreOffers.CheckAny(s, "relic_of_choice", now) == StoreRefusal.Unknown
                  && !StoreOffers.Fulfil(s, "relic_dust_chest", null, now).Success && OfferSnapshot(s) == before,
                  "they cannot be bought and a refused attempt writes nothing");
            PurchaseLedger ledger = new PurchaseLedger(StoreRules.Shared);
            Check(ledger.Begin(s, "relic_dust_satchel", null, now).Reason == PurchaseRefusal.NoSuchOffer && StoreRules.Shared.FindByProduct("placeholder.emberfall.store.relic_dust_chest") == null,
                  "and the purchase path does not find them, by id or by product");
            RelicPacksSwitch.Enabled = true;
            Check(StoreOffers.Check(s, "relic_dust_satchel", null, now) == StoreRefusal.None, "switching them back on lists them again");
        }
    }
}
