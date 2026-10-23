using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Progression;
using Emberfall.Core.Session;

namespace Emberfall.DevHarness
{
    /// <summary>
    /// R26 TEST STORE (Docs/DESIGN-STORE.md, "The test store define"): EMBERFALL_TESTSTORE makes a release build the
    /// test store, so purchases are free and grant the offer's FIXED contents through PurchaseGrants.Apply (Sealed,
    /// the Belgian rule unchanged). Without the define nothing changes: a release build is COMING SOON. Both the live
    /// define and the test define resolve to the safe state, never to free goods.
    /// </summary>
    internal static partial class Program
    {
        private static void CheckR26Store(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("R26 TEST STORE (EMBERFALL_TESTSTORE)");
            R26ResolveTable();
            R26ShippedCompile();
            R26WiringText(contentDir);
            R26EndToEnd(db);
            R26SafeStateRefuses(db);
        }

        private static void R26ResolveTable()
        {
            // The five-argument table with the define OFF is today's table, in every one of the 16 combinations.
            bool same = true;
            for (int bits = 0; bits < 16; bits++)
            {
                bool on = (bits & 1) != 0, dev = (bits & 2) != 0, live = (bits & 4) != 0, iap = (bits & 8) != 0;
                if (StoreModes.Resolve(on, dev, live, iap, false) != StoreModes.Resolve(on, dev, live, iap)) same = false;
            }
            Check(same, "TEST DEFINE OFF IS TODAY'S TABLE: the five-argument Resolve equals the four-argument one in all 16 combinations (a release build stays COMING SOON)");
            Check(StoreModes.Resolve(true, false, false, false, true) == StoreMode.Test && StoreModes.Resolve(true, false, false, true, true) == StoreMode.Test,
                  "the test define makes a RELEASE build the TEST store (no live define, with or without the IAP package)");
            Check(StoreModes.Resolve(true, true, false, false, true) == StoreMode.Test, "and the editor and a development build stay the test store with it");
            Check(StoreModes.Resolve(false, false, false, false, true) == StoreMode.Off && StoreModes.Resolve(false, true, true, true, true) == StoreMode.Off,
                  "the Store switch off stays OFF whatever the test define says");
            bool safe = true;
            for (int bits = 0; bits < 4; bits++)
            {
                bool dev = (bits & 1) != 0, iap = (bits & 2) != 0;
                if (StoreModes.Resolve(true, dev, true, iap, true) != StoreMode.ComingSoon) safe = false;
            }
            Check(safe, "BOTH THE LIVE DEFINE AND THE TEST DEFINE RESOLVE TO COMING SOON, never to free goods (release, editor, development build, with and without the IAP package)");
            Check(!StoreModes.CanBuy(StoreModes.Resolve(true, false, true, true, true)), "and that safe state draws no buy button");
        }

        private static void R26ShippedCompile()
        {
            Check(!TestStoreSwitch.Enabled, "TestStoreSwitch.Enabled is FALSE in the shipped compile: the define is the only way to turn it on");
        }

        private static void R26WiringText(string contentDir)
        {
            string root = SealedRepoRoot(contentDir);
            if (root == null) { Check(false, "R26 STORE: the repository root was not found from " + contentDir); return; }
            string service = File.ReadAllText(Path.Combine(root, "Assets", "Scripts", "Game", "Store", "PurchaseService.cs"));
            Check(service.Contains("TestStoreSwitch.Enabled") && service.Contains("UNITY_EDITOR || DEVELOPMENT_BUILD || EMBERFALL_TESTSTORE"),
                  "PurchaseService passes the test define to StoreModes.Resolve and builds the debug store under it");
            string store = File.ReadAllText(Path.Combine(root, "Assets", "Scripts", "UI", "Screens", "StoreScreen.cs"));
            string events = File.ReadAllText(Path.Combine(root, "Assets", "Scripts", "UI", "Screens", "LiveEventsScreen.cs"));
            Check(store.Contains("TestStoreBanner.Create") && store.Contains("TestStoreBanner.Shown") && events.Contains("TestStoreBanner.Create") && events.Contains("TestStoreBanner.Shown"),
                  "the Store and the Events screen both draw the TEST STORE chip (TestStoreBanner)");
        }

        private static string R26Signature(GameSession s)
        {
            PlayerProfile p = s.Profile;
            StringBuilder b = new StringBuilder();
            b.Append(p.Gold).Append('|').Append(p.Slag).Append('|').Append(p.Shards).Append('|').Append(p.RelicDust)
             .Append('|').Append(p.Sealed.Gold).Append('|').Append(p.Sealed.Slag).Append('|').Append(p.Sealed.Shards);
            for (int i = 0; i < p.Heroes.Count; i++) b.Append('|').Append(p.Heroes[i].DefId).Append(':').Append(p.Heroes[i].Copies).Append(':').Append(p.Heroes[i].SealedCopiesHeld);
            return b.ToString();
        }

        private static void R26EndToEnd(ContentDatabase db)
        {
            bool roadWas = WayfarersRoadSwitch.Enabled, loopWas = DailyLoopSwitch.Enabled, contractWas = HeroContractSwitch.Enabled;
            try
            {
                WayfarersRoadSwitch.Enabled = true;
                DailyLoopSwitch.Enabled = true;
                HeroContractSwitch.Enabled = true;
                long now = OfferMonday(db);
                GameSession probe = OfferSession(db, 6);
                StoreOffers.Bind(probe);
                StoreView view = StoreOffers.View(probe, now);

                int bought = 0, sealedOk = 0, contentsOk = 0, shownFirst = 0, limitOk = 0, receipts = 0;
                List<string> skippedIds = new List<string>(), boughtIds = new List<string>(), bad = new List<string>();
                for (int i = 0; i < view.Offers.Count; i++)
                {
                    StoreOfferView o = view.Offers[i];
                    GameSession s = OfferSession(db, 6);
                    StoreOffers.Bind(s);
                    StoreOfferView fresh = null;
                    StoreView v = StoreOffers.View(s, now);
                    for (int k = 0; k < v.Offers.Count; k++) if (v.Offers[k].Id == o.Id) fresh = v.Offers[k];
                    if (fresh == null || !fresh.Available) { skippedIds.Add(o.Id); continue; }
                    string choice = fresh.NeedsChoice && fresh.Choices.Count > 0 ? fresh.Choices[0].HeroId : null;
                    StoreOffer offer = StoreOffers.BuiltOffer(o.Id);
                    if (offer == null) { bad.Add(o.Id + " has no built offer"); continue; }

                    // What the offer's fixed contents give, applied directly through the one grant path to a twin.
                    GameSession twin = OfferSession(db, 6);
                    StoreOffers.Bind(twin);
                    GrantResult direct = PurchaseGrants.Apply(twin, offer.Grant, choice, now);
                    string expected = R26Signature(twin);
                    StoreOffers.Bind(s);

                    long freeGold = s.Profile.FreeGold, freeSlag = s.Profile.FreeSlag, freeShards = s.Profile.FreeShards;
                    SimulatedStore store = new SimulatedStore();
                    string[] account = { null };
                    store.LoadAccount = () => account[0];
                    store.SaveAccount = t => account[0] = t;
                    int saves = 0;
                    PurchaseResult finished = null;
                    PurchaseFlow flow = new PurchaseFlow(StoreRules.Shared, store, StoreMode.Test, () => s, () => now, () => saves++);
                    flow.Finished += r => finished = r;
                    flow.Start(new List<StoreOffer> { offer });
                    bool priced = !string.IsNullOrEmpty(fresh.PriceLabel) && fresh.Lines.Count > 0;
                    PurchaseResult begun = flow.Buy(o.Id, choice);
                    flow.Tick(0.1);
                    flow.Tick(1.0);
                    if (!begun.Success || finished == null || !finished.Success) { bad.Add(o.Id + " did not complete (" + (begun.Success ? "store" : begun.Reason.ToString()) + ")"); continue; }
                    bought++;
                    boughtIds.Add(o.Id);
                    if (priced) shownFirst++;
                    if (s.Profile.FreeGold == freeGold && s.Profile.FreeSlag == freeSlag && s.Profile.FreeShards == freeShards) sealedOk++;
                    else bad.Add(o.Id + " left free goods");
                    if (direct.Success && R26Signature(s) == expected) contentsOk++;
                    else bad.Add(o.Id + " contents differ from the offer's fixed grant");
                    if (s.Profile.Store.Receipts.Count == 1 && saves == 1) receipts++;
                    // The limit holds: bought again and again (a contract and a Dust chest allow more than one a month),
                    // the offer is refused within a handful of tries and never sells without end.
                    bool refused = false;
                    for (int again = 0; again < 6 && !refused; again++)
                    {
                        PurchaseResult next = flow.Buy(o.Id, choice);
                        if (!next.Success) refused = true;
                        else { flow.Tick(0.1); flow.Tick(1.0); }
                    }
                    if (refused) limitOk++; else bad.Add(o.Id + " sold without end");
                }
                string detail = " (bought " + string.Join(", ", boughtIds.ToArray()) + "; not buyable for this buyer: " + (skippedIds.Count == 0 ? "none" : string.Join(", ", skippedIds.ToArray())) + ")";
                Check(bought >= 6 && bad.Count == 0, "TEST STORE END TO END: " + bought + " shipped offers bought through PurchaseFlow and SimulatedStore with no money" + detail
                      + (bad.Count == 0 ? "" : " PROBLEMS: " + string.Join("; ", bad.ToArray())));
                Check(sealedOk == bought, "every test purchase is SEALED: no free Gold, Slag or Shards appear (" + sealedOk + " of " + bought + ")");
                Check(contentsOk == bought, "and the goods equal the offer's FIXED contents applied through PurchaseGrants.Apply (" + contentsOk + " of " + bought + ")");
                Check(shownFirst == bought, "each offer showed its price tier and its full contents before the purchase (" + shownFirst + " of " + bought + ")");
                Check(receipts == bought && limitOk == bought, "one receipt and one save a purchase, and the limits are honoured: the offer is refused again within its limit (" + limitOk + " of " + bought + ")");

                // Everything bought in one profile: still nothing free, so no random mechanic has anything to take.
                GameSession all = OfferSession(db, 6);
                StoreOffers.Bind(all);
                long g0 = all.Profile.FreeGold, sl0 = all.Profile.FreeSlag, sh0 = all.Profile.FreeShards;
                PurchaseLedger ledger = new PurchaseLedger(StoreRules.Shared);
                int n = 0;
                for (int i = 0; i < boughtIds.Count; i++)
                {
                    StoreView av = StoreOffers.View(all, now);
                    StoreOfferView o = null;
                    for (int k = 0; k < av.Offers.Count; k++) if (av.Offers[k].Id == boughtIds[i]) o = av.Offers[k];
                    if (o == null || !o.Available) continue;
                    string choice = o.NeedsChoice && o.Choices.Count > 0 ? o.Choices[0].HeroId : null;
                    if (ledger.Begin(all, o.Id, choice, now).Success && ledger.Complete(all, o.Id, "r26-" + i, now).Success) n++;
                }
                Check(n >= 1 && all.Profile.FreeGold == g0 && all.Profile.FreeSlag == sl0 && all.Profile.FreeShards == sh0,
                      "AFTER " + n + " test purchases in one profile the free parts of Gold, Slag and Shards are as they were: nothing bought is spendable in a random mechanic");
            }
            finally
            {
                WayfarersRoadSwitch.Enabled = roadWas;
                DailyLoopSwitch.Enabled = loopWas;
                HeroContractSwitch.Enabled = contractWas;
                StoreOffers.Bind(null);
            }
        }

        /// <summary>The contradictory build (live define AND test define) is a flow in COMING SOON: Buy is refused and
        /// the save is untouched, even with a store behind it.</summary>
        private static void R26SafeStateRefuses(ContentDatabase db)
        {
            bool roadWas = WayfarersRoadSwitch.Enabled, loopWas = DailyLoopSwitch.Enabled;
            try
            {
                WayfarersRoadSwitch.Enabled = true;
                DailyLoopSwitch.Enabled = true;
                long now = OfferMonday(db);
                GameSession s = OfferSession(db, 6);
                StoreOffers.Bind(s);
                StoreMode mode = StoreModes.Resolve(true, false, true, true, true);
                SimulatedStore store = new SimulatedStore();
                store.LoadAccount = () => null;
                store.SaveAccount = t => { };
                PurchaseFlow flow = new PurchaseFlow(StoreRules.Shared, store, mode, () => s, () => now, () => { });
                flow.Start(new List<StoreOffer> { StoreOffers.BuiltOffer("weekly_pack") });
                string before = R26Signature(s);
                PurchaseResult r = flow.Buy("weekly_pack", null);
                flow.Tick(1.0);
                Check(mode == StoreMode.ComingSoon && !r.Success && r.Reason == PurchaseRefusal.ComingSoon && R26Signature(s) == before && !flow.CanBuy && store.OpenCount == 0,
                      "LIVE + TESTSTORE: the flow refuses with COMING SOON, grants nothing and opens nothing in the store");
            }
            finally
            {
                WayfarersRoadSwitch.Enabled = roadWas;
                DailyLoopSwitch.Enabled = loopWas;
                StoreOffers.Bind(null);
            }
        }
    }
}
