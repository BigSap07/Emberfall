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
    /// R24 STORE ADDITIONS (Docs/DESIGN-STORE.md): the Hero Contract turned ON with an account cap, then the relic
    /// packs. Each section ends with its rollback: the flag off is the store the build before R24 sold.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckR24Store(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("R24 STORE (Docs/DESIGN-STORE.md: the Hero Contract on with an account cap)");
            _offerContentDir = contentDir;
            bool contractShipped = HeroContractSwitch.Enabled;
            bool storeWas = StoreSwitch.Enabled, roadWas = WayfarersRoadSwitch.Enabled, loopWas = DailyLoopSwitch.Enabled;
            try
            {
                StoreSwitch.Enabled = true;
                WayfarersRoadSwitch.Enabled = true;
                DailyLoopSwitch.Enabled = true;
                CheckR24ContractShipped(db, contractShipped);
                CheckR24ContractCaps(db);
                CheckR24ContractClock(db);
                CheckR24ContractPurchasePath(db);
                CheckR24ContractSave(db);
                CheckR24ContractRollback(db);
            }
            finally
            {
                HeroContractSwitch.Enabled = contractShipped;
                StoreSwitch.Enabled = storeWas;
                WayfarersRoadSwitch.Enabled = roadWas;
                DailyLoopSwitch.Enabled = loopWas;
                StoreOffers.Bind(null);
            }
            Check(HeroContractSwitch.Enabled == contractShipped && StoreSwitch.Enabled == storeWas, "R24 STORE: the switches are put back as the run found them");
        }

        // ------------------------------------------------------------------ the Hero Contract

        private static GameSession R24ContractSession(ContentDatabase db, long now, int owned)
        {
            GameSession s = OfferSession(db, 2);
            List<string> picks = db.Store.Offer("starter_pact").HeroChoices;
            for (int i = 0; i < owned && i < picks.Count; i++) s.GrantHeroDirect(db.GetHero(picks[i]), now);
            return s;
        }

        private static void CheckR24ContractShipped(ContentDatabase db, bool shipped)
        {
            StoreOfferDef c = db.Store.Offer("hero_contract");
            Check(shipped, "SHIPPED: HeroContractSwitch is ON unless the build defines EMBERFALL_HERO_CONTRACT_OFF");
            Check(c != null && c.Limit == 2 && c.Reset == StoreReset.Month && c.AccountLimit == 4 && c.Stars == 5,
                  "the Hero Contract keeps its 2 a month per hero and store.json adds accountLimit 4 a month across the account");
            StoreOfferDef starter = db.Store.Offer("starter_pact");
            Check(starter != null && starter.AccountLimit == 0 && db.Store.Offer("weekly_pack").AccountLimit == 0,
                  "no other offer carries an account cap");
        }

        private static void CheckR24ContractCaps(ContentDatabase db)
        {
            HeroContractSwitch.Enabled = true;
            long now = OfferMonday(db);
            GameSession s = R24ContractSession(db, now, 3);
            List<string> picks = db.Store.Offer("starter_pact").HeroChoices;
            string a = picks[0], b = picks[1], c = picks[2];

            bool a1 = StoreOffers.Fulfil(s, "hero_contract", a, now).Success;
            bool a2 = StoreOffers.Fulfil(s, "hero_contract", a, now + 60).Success;
            StoreRefusal a3 = StoreOffers.Fulfil(s, "hero_contract", a, now + 120).Reason;
            Check(a1 && a2 && a3 == StoreRefusal.SoldOut, "THE PER HERO LIMIT STANDS: two contracts for one hero, the third is refused as sold out");

            bool b1 = StoreOffers.Fulfil(s, "hero_contract", b, now + 180).Success;
            Check(b1 && StoreOffers.ContractsInPeriod(s, SinkShop.MonthKeyOf(StoreOffers.EffectiveDay(s, now))) == 3,
                  "a second hero's contract is open and the account has used 3 of 4");
            bool b2 = StoreOffers.Fulfil(s, "hero_contract", b, now + 240).Success;
            string before = OfferSnapshot(s);
            StoreFulfilment capped = StoreOffers.Fulfil(s, "hero_contract", c, now + 300);
            Check(b2 && !capped.Success && capped.Reason == StoreRefusal.AccountCap && StoreOffers.Check(s, "hero_contract", c, now + 300) == StoreRefusal.AccountCap
                  && StoreOffers.CheckAny(s, "hero_contract", now + 300) == StoreRefusal.AccountCap && OfferSnapshot(s) == before,
                  "THE ACCOUNT CAP: the fifth contract of the month, for a third hero with none bought, is refused as AccountCap and writes nothing");

            HeroInstance hc = s.Profile.FindHeroByDefId(c);
            Check(hc.Copies == 1 && hc.SealedCopiesHeld == 0
                  && s.Profile.FindHeroByDefId(a).SealedCopiesHeld == 2 && s.Profile.FindHeroByDefId(b).SealedCopiesHeld == 2,
                  "every bought contract is a Sealed copy (2 + 2) and the refused one gave nothing");

            StoreView v = StoreOffers.View(s, now + 300);
            StoreOfferView ov = null;
            for (int i = 0; i < v.Offers.Count; i++) if (v.Offers[i].Id == "hero_contract") ov = v.Offers[i];
            Check(ov != null && !ov.Available && ov.Reason == StoreRefusal.AccountCap && ov.ResetInSeconds > 0
                  && ov.LimitLabel == "2 a month per hero, 4 a month in all" && StoreOffers.ReasonText(StoreRefusal.AccountCap).Length > 0,
                  "THE CARD SAYS IT: the limit line reads '2 a month per hero, 4 a month in all', it is not available and says when the month turns");

            // The cap is the account's, so it also stops a month of single contracts spread over heroes.
            GameSession t = R24ContractSession(db, now, 3);
            string[] spread = { a, b, c, a };
            bool allFour = true;
            for (int i = 0; i < spread.Length; i++) allFour &= StoreOffers.Fulfil(t, "hero_contract", spread[i], now + i * 60).Success;
            Check(allFour && StoreOffers.Fulfil(t, "hero_contract", b, now + 600).Reason == StoreRefusal.AccountCap
                  && StoreOffers.Fulfil(t, "hero_contract", c, now + 660).Reason == StoreRefusal.AccountCap,
                  "four spread over three heroes (2 + 1 + 1) use the account up, and then no hero can be bought for, however few they have had");
        }

        private static void CheckR24ContractClock(ContentDatabase db)
        {
            HeroContractSwitch.Enabled = true;
            long now = OfferUnix(2027, 3, 10, 12);
            GameSession s = R24ContractSession(db, now, 3);
            List<string> picks = db.Store.Offer("starter_pact").HeroChoices;
            string[] order = { picks[0], picks[0], picks[1], picks[1] };
            for (int i = 0; i < order.Length; i++) StoreOffers.Fulfil(s, "hero_contract", order[i], now + i * 60);

            Check(StoreOffers.Check(s, "hero_contract", picks[2], now + 3600) == StoreRefusal.AccountCap
                  && StoreOffers.Check(s, "hero_contract", picks[2], OfferUnix(2027, 3, 31, 23)) == StoreRefusal.AccountCap,
                  "the cap holds to the end of the month");
            long april = OfferUnix(2027, 4, 1, 12);
            Check(StoreOffers.Check(s, "hero_contract", picks[2], april) == StoreRefusal.None && StoreOffers.Check(s, "hero_contract", picks[0], april) == StoreRefusal.None,
                  "MONTHLY RESET: on the first reward day of the next month both the account cap and the per hero limit are open again");
            Check(StoreOffers.Check(s, "hero_contract", picks[2], now - 40 * 86400L) == StoreRefusal.AccountCap
                  && StoreOffers.Check(s, "hero_contract", picks[2], OfferUnix(2020, 1, 1, 12)) == StoreRefusal.AccountCap
                  && StoreOffers.Check(s, "hero_contract", picks[2], 0L) == StoreRefusal.AccountCap,
                  "A CLOCK WOUND BACK REOPENS NOTHING: a month, a year and the epoch back all read the store's own later clock, so the cap stands");

            // Spend April's cap, wind back into March: still closed. The old March counters never count for April.
            for (int i = 0; i < 4; i++) StoreOffers.Fulfil(s, "hero_contract", order[i], april + i * 60);
            Check(StoreOffers.Check(s, "hero_contract", picks[2], april + 3600) == StoreRefusal.AccountCap
                  && StoreOffers.Check(s, "hero_contract", picks[2], now) == StoreRefusal.AccountCap,
                  "after a new month is spent, winding the clock back into the old month does not hand its cap back");
        }

        private static void CheckR24ContractPurchasePath(ContentDatabase db)
        {
            HeroContractSwitch.Enabled = true;
            long now = OfferMonday(db);
            GameSession s = R24ContractSession(db, now, 3);
            StoreOffers.Bind(s);
            PurchaseLedger ledger = new PurchaseLedger(StoreRules.Shared);
            List<string> picks = db.Store.Offer("starter_pact").HeroChoices;
            string[] order = { picks[0], picks[0], picks[1], picks[1] };
            int bought = 0;
            for (int i = 0; i < order.Length; i++)
            {
                if (!ledger.Begin(s, "hero_contract", order[i], now + i * 60).Success) continue;
                if (ledger.Complete(s, "hero_contract", "r24-c" + i, now + i * 60).Success) bought++;
            }
            string before = OfferSnapshot(s);
            PurchaseResult refused = ledger.Begin(s, "hero_contract", picks[2], now + 600);
            Check(bought == 4 && !refused.Success && refused.Reason == PurchaseRefusal.NotAvailable && refused.Message == StoreOffers.ReasonText(StoreRefusal.AccountCap) && OfferSnapshot(s) == before,
                  "ON THE PURCHASE PATH: four Complete calls grant, the fifth Begin is refused with the cap's sentence and the save is untouched");
            Check(StoreRules.Shared.Find("hero_contract") != null && StoreRules.Shared.Find("hero_contract").Grant.Items.Count == 1
                  && StoreRules.Shared.Find("hero_contract").Grant.Items[0].Kind == GrantKind.OwnedHeroCopy,
                  "the grant is still the one OwnedHeroCopy line through PurchaseGrants.Apply");
        }

        private static void CheckR24ContractSave(ContentDatabase db)
        {
            HeroContractSwitch.Enabled = true;
            long now = OfferMonday(db);
            GameSession s = R24ContractSession(db, now, 3);
            List<string> picks = db.Store.Offer("starter_pact").HeroChoices;
            string[] order = { picks[0], picks[0], picks[1], picks[1] };
            for (int i = 0; i < order.Length; i++) StoreOffers.Fulfil(s, "hero_contract", order[i], now + i * 60);
            string json = SaveSerializer.Serialize(s.Profile);
            string error;
            PlayerProfile back = SaveSerializer.Deserialize(json, db, out error);
            GameSession reloaded = new GameSession(db, back);
            Check(error == null && SaveSerializer.Serialize(back) == json && JsonValue.Parse(json).GetInt("version", 0) == SaveSerializer.StoreVersion
                  && StoreOffers.Check(reloaded, "hero_contract", picks[2], now + 3600) == StoreRefusal.AccountCap,
                  "THE CAP SURVIVES THE SAVE: no new key (the account total is read from the per hero counters), the document is still version 25 and a reload is still capped");
        }

        private static void CheckR24ContractRollback(ContentDatabase db)
        {
            long now = OfferMonday(db);
            HeroContractSwitch.Enabled = false;
            GameSession s = R24ContractSession(db, now, 3);
            string hero = db.Store.Offer("starter_pact").HeroChoices[0];
            StoreView off = StoreOffers.View(s, now);
            string before = OfferSnapshot(s);
            List<string> ids = new List<string>();
            for (int i = 0; i < off.Offers.Count; i++) ids.Add(off.Offers[i].Id);
            string expected = "starter_pact,weekly_pack,monthly_pack,ember_ledger,growth_fund,pass_premium";
            Check(!ids.Contains("hero_contract") && string.Join(",", ids.ToArray()).StartsWith(expected, StringComparison.Ordinal)
                  && StoreOffers.Check(s, "hero_contract", hero, now) == StoreRefusal.Unknown
                  && StoreOffers.CheckAny(s, "hero_contract", now) == StoreRefusal.Unknown
                  && !StoreOffers.Fulfil(s, "hero_contract", hero, now).Success && OfferSnapshot(s) == before,
                  "ROLLBACK: with the switch off the Store lists the offers it listed before R24, the contract is not listed, cannot be bought and writes nothing");
            StoreOffers.Bind(s);
            PurchaseLedger ledger = new PurchaseLedger(StoreRules.Shared);
            Check(ledger.Begin(s, "hero_contract", hero, now).Reason == PurchaseRefusal.NoSuchOffer && OfferSnapshot(s) == before,
                  "and the purchase path does not find it");
            HeroContractSwitch.Enabled = true;
        }
    }
}
