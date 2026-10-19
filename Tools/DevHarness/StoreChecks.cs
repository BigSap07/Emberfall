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
    /// THE STORE'S PURCHASE PATH (R19, Docs/DESIGN-STORE.md): the rules that decide a grant, proven without
    /// Unity. A test double stands in for the offer rules and for the store; every rule here lives in Core
    /// (PurchaseGrants, PurchaseLedger, StoreState, WayfarersRoad.ClaimPaid).
    ///
    /// THE RULE (Belgium): everything a purchase grants is Sealed, and no purchase path reaches a random
    /// mechanic. Proven four ways: each grant kind marks what it grants (CheckStoreGrants); a profile that
    /// holds only purchased goods is refused by the summon, the Quartermaster, the forge and the conversion
    /// (CheckStoreRandomMechanicsRefuse); the grant kinds are a closed list of fixed lines and the files of the
    /// purchase path name no random mechanic (CheckStoreNoRandomPath); and the same grant applied twice gives
    /// the same save (CheckStoreGrants).
    ///
    /// A harness check must never leave shared content or a switch changed: StoreSwitch is put back, content
    /// is only read.
    /// </summary>
    internal static partial class Program
    {
        private const long StoreBase = 1760000000L;

        private static void CheckStore(ContentDatabase db, string contentDir)
        {
            Line("");
            Line("STORE (R19: a purchase grants fixed Sealed goods once per receipt, writes nothing until the store completes it, and reaches no random mechanic)");
            bool shipped = StoreSwitch.Enabled, contractShipped = HeroContractSwitch.Enabled;
            Check(!shipped && !contractShipped, "SHIPPED OFF: StoreSwitch and HeroContractSwitch are off unless the build defines EMBERFALL_STORE_ON / EMBERFALL_HERO_CONTRACT");
            try
            {
                StoreSwitch.Enabled = true;
                CheckStoreGrants(db);
                CheckStoreAllOrNothing(db);
                CheckStoreLedger(db);
                CheckStoreRestore(db);
                CheckStoreRandomMechanicsRefuse(db);
                CheckStorePass(db);
                CheckStoreSave(db);
                CheckStoreModes();
                CheckStoreFlow(db);
                CheckStoreOff(db);
            }
            finally
            {
                StoreSwitch.Enabled = shipped;
            }
            Check(StoreSwitch.Enabled == shipped && HeroContractSwitch.Enabled == contractShipped, "and the switches are put back as they were");
            CheckStoreNoRandomPath(contentDir);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>The offer rules, as a test double: a limit of Limit purchases an offer, counted in the
        /// offer's record. The real rules are the Store's; the ledger knows only this interface.</summary>
        private sealed class StoreTestRules : IStoreRules
        {
            public readonly Dictionary<string, StoreOffer> Offers = new Dictionary<string, StoreOffer>();
            public readonly Dictionary<string, PurchaseGrant> Grants = new Dictionary<string, PurchaseGrant>();
            public int Limit = 1;
            public int Recorded;

            public StoreOffer Add(string id, bool restorable, PurchaseGrant grant)
            {
                StoreOffer o = new StoreOffer();
                o.Id = id;
                o.ProductId = "placeholder." + id;
                o.PriceTier = "4.99";
                o.Restorable = restorable;
                o.NeedsChoice = grant.NeedsChoice;
                Offers[id] = o;
                Grants[id] = grant;
                return o;
            }

            public StoreOffer Find(string offerId) { StoreOffer o; return offerId != null && Offers.TryGetValue(offerId, out o) ? o : null; }

            public StoreOffer FindByProduct(string productId)
            {
                foreach (KeyValuePair<string, StoreOffer> kv in Offers) if (kv.Value.ProductId == productId) return kv.Value;
                return null;
            }

            public bool CanBuy(GameSession s, string offerId, string choiceHeroId, long nowUnix, out string reason, out bool badChoice)
            {
                badChoice = false;
                reason = null;
                PurchaseGrant g;
                if (!Grants.TryGetValue(offerId, out g)) { reason = "Not for sale."; return false; }
                for (int i = 0; i < g.Items.Count; i++)
                {
                    GrantItem it = g.Items[i];
                    bool ok = true;
                    if (it.Kind == GrantKind.HeroChoice) ok = !string.IsNullOrEmpty(choiceHeroId) && it.HeroIds.Contains(choiceHeroId);
                    if (it.Kind == GrantKind.OwnedHeroCopy)
                    {
                        HeroDef d = string.IsNullOrEmpty(choiceHeroId) ? null : s.Content.GetHero(choiceHeroId);
                        ok = d != null && d.Stars == it.Stars && s.Profile.FindHeroByDefId(choiceHeroId) != null;
                    }
                    if (!ok) { badChoice = true; reason = "Pick a hero first."; return false; }
                }
                OfferRecord r = s.Profile.Store.Find(offerId);
                if (r != null && r.Count >= Limit) { reason = "Limit reached."; return false; }
                return true;
            }

            public FulfilOutcome Fulfil(GameSession s, string offerId, string choiceHeroId, long nowUnix)
            {
                FulfilOutcome o = new FulfilOutcome();
                string why;
                bool bad;
                if (!CanBuy(s, offerId, choiceHeroId, nowUnix, out why, out bad)) { o.BadChoice = bad; o.Reason = why; return o; }
                GrantResult g = PurchaseGrants.Apply(s, Grants[offerId], choiceHeroId, nowUnix);
                if (!g.Success) { o.BadChoice = g.Reason == GrantRefusal.BadChoice; o.Reason = g.Reason.ToString(); return o; }
                o.Lines.AddRange(g.Lines);
                s.Profile.Store.Record(offerId).Count++;
                Recorded++;
                o.Success = true;
                return o;
            }
        }

        private static PurchaseGrant StoreGrantOf(params GrantItem[] items)
        {
            PurchaseGrant g = new PurchaseGrant();
            for (int i = 0; i < items.Length; i++) g.Add(items[i]);
            return g;
        }

        private static GrantItem StoreChoiceItem(List<string> ids)
        {
            GrantItem it = new GrantItem(GrantKind.HeroChoice, 0);
            it.HeroIds.AddRange(ids);
            return it;
        }

        private static List<HeroDef> StoreFiveStars(ContentDatabase db)
        {
            List<HeroDef> list = new List<HeroDef>();
            List<HeroDef> all = db.HeroesOfStars(5);
            for (int i = 0; i < all.Count; i++) if (all[i].Fusable && !all[i].StartingHero) list.Add(all[i]);
            return list;
        }

        private static long StoreNow(ContentDatabase db) { return PassSeasonStart(db, 5) + 600; }

        private static GameSession StoreSession(ContentDatabase db)
        {
            GameSession s = PassSession(db, 0, 0, 0);
            s.Profile.Gold = 0;
            s.Profile.Shards = 0;
            s.Profile.Slag = 0;
            return s;
        }

        private static string StoreSnapshot(GameSession s) { return SaveSerializer.Serialize(s.Profile); }

        // ------------------------------------------------------------------ 1. each kind marks what it grants

        private static void CheckStoreGrants(ContentDatabase db)
        {
            long now = StoreNow(db);
            List<HeroDef> five = StoreFiveStars(db);
            if (five.Count < 4) { Check(false, "STORE: content has four fusable non-starting 5-stars"); return; }

            // Fixed amounts: Gold and Slag Sealed, Dust plain.
            GameSession s = StoreSession(db);
            PlayerProfile p = s.Profile;
            long dust0 = p.RelicDust;
            GrantResult r = PurchaseGrants.Apply(s, StoreGrantOf(new GrantItem(GrantKind.Gold, 5000), new GrantItem(GrantKind.Slag, 300), new GrantItem(GrantKind.Dust, 70)), null, now);
            Check(r.Success && p.Gold == 5000 && p.Sealed.Gold == 5000 && p.Slag == 300 && p.Sealed.Slag == 300 && p.RelicDust == dust0 + 70
                  && p.Sealed.Shards == 0 && p.Shards == 0 && p.FreeGold == 0 && p.FreeSlag == 0,
                  "GOLD AND SLAG ARE SEALED: 5000 Gold and 300 Slag arrive whole in the purse and whole in the Sealed part (free: " + p.FreeGold + " Gold, " + p.FreeSlag + " Slag), Dust is plain, no Shards move");

            // The buyer's own idle hours: Gold only, never the Shards the same hour pays.
            GameSession h = StoreSession(db);
            int idleGold = h.IdleYield(6 * 3600).Gold, idleShards = h.IdleYield(6 * 3600).Shards;
            GrantResult hr = PurchaseGrants.Apply(h, StoreGrantOf(new GrantItem(GrantKind.IdleGoldHours, 6)), null, now);
            Check(idleGold > 0 && idleShards > 0 && hr.Success && h.Profile.Gold == idleGold && h.Profile.Sealed.Gold == idleGold && h.Profile.Shards == 0 && h.Profile.Sealed.Shards == 0,
                  "IDLE HOURS ARE GOLD ONLY: 6 hours of this buyer's frontier pay " + idleGold + " Sealed Gold and none of the " + idleShards + " Shards the same hours would pay");

            // A hero: a new hero arrives with one Sealed copy; an owned one gains a Sealed copy on top of its earned ones.
            GameSession c = StoreSession(db);
            HeroDef fixedHero = five[0];
            bool hadIt = c.Profile.FindHeroByDefId(fixedHero.Id) != null;
            GrantResult cr = PurchaseGrants.Apply(c, StoreGrantOf(new GrantItem { Kind = GrantKind.HeroCopy, HeroId = fixedHero.Id }), null, now);
            HeroInstance first = c.Profile.FindHeroByDefId(fixedHero.Id);
            bool newIsSealed = first != null && first.Copies == 1 && first.SealedCopiesHeld == 1;
            PurchaseGrants.Apply(c, StoreGrantOf(new GrantItem { Kind = GrantKind.HeroCopy, HeroId = fixedHero.Id }), null, now);
            bool secondIsSealed = first.Copies == 2 && first.SealedCopiesHeld == 2;
            HeroInstance earnedOne = c.GrantHeroDirect(five[1], now);
            PurchaseGrants.Apply(c, StoreGrantOf(new GrantItem { Kind = GrantKind.HeroCopy, HeroId = five[1].Id }), null, now);
            Check(!hadIt && cr.Success && newIsSealed && secondIsSealed && earnedOne.Copies == 2 && earnedOne.SealedCopiesHeld == 1,
                  "A HERO COPY IS SEALED: a new hero arrives as one Sealed body, a second copy raises both counts, and a purchase on top of an earned copy marks only the purchased one (2 copies, 1 Sealed)");

            // A named choice.
            List<string> three = new List<string> { five[0].Id, five[1].Id, five[2].Id };
            PurchaseGrant pick = StoreGrantOf(StoreChoiceItem(three));
            GameSession k = StoreSession(db);
            string before = StoreSnapshot(k);
            GrantResult bad1 = PurchaseGrants.Apply(k, pick, five[3].Id, now);
            GrantResult bad2 = PurchaseGrants.Apply(k, pick, "", now);
            GrantResult bad3 = PurchaseGrants.Apply(k, pick, "no_such_hero", now);
            Check(!bad1.Success && !bad2.Success && !bad3.Success && bad1.Reason == GrantRefusal.BadChoice && StoreSnapshot(k) == before,
                  "A CHOICE IS ONE OF THE NAMED HEROES: a hero outside the three, an empty pick and an unknown id are refused as BadChoice and the save is byte for byte as it was");
            GrantResult good = PurchaseGrants.Apply(k, pick, five[2].Id, now);
            HeroInstance got = k.Profile.FindHeroByDefId(five[2].Id);
            Check(good.Success && got != null && got.SealedCopiesHeld == 1 && good.HeroId == five[2].Id && k.Profile.FindHeroByDefId(five[0].Id) == null,
                  "and the named hero arrives Sealed, the other two do not arrive");

            // The Hero Contract's shape: a copy of an owned hero of the named stars.
            GameSession o = StoreSession(db);
            PurchaseGrant contract = StoreGrantOf(new GrantItem { Kind = GrantKind.OwnedHeroCopy, Stars = 5 });
            before = StoreSnapshot(o);
            GrantResult notOwned = PurchaseGrants.Apply(o, contract, five[0].Id, now);
            HeroDef starter = o.Profile.Heroes[0] == null ? null : db.GetHero(o.Profile.Heroes[0].DefId);
            GrantResult wrongStars = starter != null && starter.Stars != 5 ? PurchaseGrants.Apply(o, contract, starter.Id, now) : null;
            Check(!notOwned.Success && notOwned.Reason == GrantRefusal.BadChoice && (wrongStars == null || !wrongStars.Success) && StoreSnapshot(o) == before,
                  "A CONTRACT NEEDS AN OWNED HERO OF THE NAMED STARS: an unowned 5-star and an owned hero of other stars are refused, nothing written");
            o.GrantHeroDirect(five[0], now);
            GrantResult owned = PurchaseGrants.Apply(o, contract, five[0].Id, now);
            HeroInstance oh = o.Profile.FindHeroByDefId(five[0].Id);
            Check(owned.Success && oh.Copies == 2 && oh.SealedCopiesHeld == 1, "and an owned 5-star gains one Sealed copy");

            // Same grant, same save: nothing in a grant is drawn.
            GameSession d1 = StoreSession(db), d2 = StoreSession(db);
            PurchaseGrant mixed = StoreGrantOf(new GrantItem(GrantKind.Gold, 900), new GrantItem(GrantKind.IdleGoldHours, 3), new GrantItem(GrantKind.Slag, 40), new GrantItem(GrantKind.Dust, 12), StoreChoiceItem(three));
            PurchaseGrants.Apply(d1, mixed, three[1], now);
            PurchaseGrants.Apply(d2, mixed, three[1], now);
            Check(StoreSnapshot(d1) == StoreSnapshot(d2), "DETERMINISTIC: the same grant applied to two equal profiles gives the same save, byte for byte");

            // A grant that changes nothing it should not.
            GameSession a = StoreSession(db);
            before = StoreSnapshot(a);
            GrantResult act = PurchaseGrants.Apply(a, StoreGrantOf(new GrantItem(GrantKind.Activate, 0)), null, now);
            Check(act.Success && StoreSnapshot(a) == before, "ACTIVATE GRANTS NOTHING NOW: an offer that starts a card or a fund changes no purse and no hero");
        }

        // ------------------------------------------------------------------ 2. all or nothing

        private static void CheckStoreAllOrNothing(ContentDatabase db)
        {
            long now = StoreNow(db);
            List<HeroDef> five = StoreFiveStars(db);
            GameSession s = StoreSession(db);
            string before = StoreSnapshot(s);
            PurchaseGrant bad = StoreGrantOf(new GrantItem(GrantKind.Gold, 1000), new GrantItem(GrantKind.Slag, 10), StoreChoiceItem(new List<string> { five[0].Id }));
            GrantResult r = PurchaseGrants.Apply(s, bad, "wrong", now);
            Check(!r.Success && StoreSnapshot(s) == before, "ALL OR NOTHING: a grant whose choice is bad writes none of its Gold and Slag either");

            PurchaseGrant malformed = StoreGrantOf(new GrantItem(GrantKind.Gold, -5));
            PurchaseGrant empty = new PurchaseGrant();
            PurchaseGrant unknown = StoreGrantOf(new GrantItem { Kind = GrantKind.HeroCopy, HeroId = "no_such_hero" });
            PurchaseGrant twoPicks = StoreGrantOf(StoreChoiceItem(new List<string> { five[0].Id }), StoreChoiceItem(new List<string> { five[1].Id }));
            PurchaseGrant fraction = StoreGrantOf(new GrantItem(GrantKind.Gold, 10.5));
            bool allBad = !PurchaseGrants.Apply(s, malformed, null, now).Success && !PurchaseGrants.Apply(s, empty, null, now).Success
                       && !PurchaseGrants.Apply(s, unknown, null, now).Success && !PurchaseGrants.Apply(s, twoPicks, five[0].Id, now).Success
                       && !PurchaseGrants.Apply(s, fraction, null, now).Success && !PurchaseGrants.Apply(s, null, null, now).Success;
            Check(allBad && StoreSnapshot(s) == before && malformed.Problem(db) != null && empty.Problem(db) != null && unknown.Problem(db) != null,
                  "MALFORMED GRANTS ARE NOT GRANTS: a negative amount, an empty grant, an unknown hero, two picks, a fractional Gold and a null grant are refused with the save untouched");
        }

        // ------------------------------------------------------------------ 3. the ledger

        private static StoreTestRules StoreRulesFor(ContentDatabase db)
        {
            StoreTestRules rules = new StoreTestRules();
            List<HeroDef> five = StoreFiveStars(db);
            List<string> three = new List<string> { five[0].Id, five[1].Id, five[2].Id };
            rules.Add("starter", true, StoreGrantOf(StoreChoiceItem(three), new GrantItem(GrantKind.IdleGoldHours, 6)));
            rules.Add("weekly", false, StoreGrantOf(new GrantItem(GrantKind.IdleGoldHours, 24), new GrantItem(GrantKind.Slag, 200), new GrantItem(GrantKind.Dust, 100)));
            rules.Add("pass", false, StoreGrantOf(new GrantItem(GrantKind.PassPremium, 0)));
            rules.Add("fund", true, StoreGrantOf(new GrantItem(GrantKind.Activate, 0)));
            return rules;
        }

        private static void CheckStoreLedger(ContentDatabase db)
        {
            long now = StoreNow(db);
            List<HeroDef> five = StoreFiveStars(db);
            StoreTestRules rules = StoreRulesFor(db);
            PurchaseLedger ledger = new PurchaseLedger(rules);
            GameSession s = StoreSession(db);
            string before = StoreSnapshot(s);

            // A purchase that is started and never completed grants nothing.
            PurchaseResult begun = ledger.Begin(s, "weekly", null, now);
            Check(begun.Success && ledger.IsPending("weekly") && ledger.PendingCount == 1 && StoreSnapshot(s) == before,
                  "A PENDING PURCHASE WRITES NOTHING: Begin remembers the purchase in memory and the save is byte for byte as it was");
            ledger.Abandon("weekly");
            Check(!ledger.IsPending("weekly") && StoreSnapshot(s) == before && s.Profile.Store.IsEmpty,
                  "A PENDING PURCHASE THAT NEVER COMPLETES GRANTS NOTHING: abandoned (cancelled, failed, timed out, still pending at exit) it leaves the save byte for byte as it was and no receipt");
            PurchaseLedger fresh = new PurchaseLedger(rules);
            Check(fresh.PendingCount == 0 && fresh.Begin(s, "weekly", null, now).Success && StoreSnapshot(s) == before,
                  "and a new ledger after a restart knows no pending purchase: pending lives in memory only");

            // Complete grants once.
            ledger.Begin(s, "weekly", null, now);
            PurchaseResult done = ledger.Complete(s, "weekly", "receipt-001", now);
            PlayerProfile p = s.Profile;
            long weeklyGold = s.IdleYield(24 * 3600).Gold;
            Check(done.Success && done.CanConfirm && !ledger.IsPending("weekly") && p.Gold == weeklyGold && p.Sealed.Gold == weeklyGold && p.Slag == 200 && p.Sealed.Slag == 200
                  && p.Store.FindReceipt("receipt-001") != null && p.Store.Record("weekly").Count == 1 && rules.Recorded == 1,
                  "COMPLETE GRANTS: " + weeklyGold + " Sealed Gold and 200 Sealed Slag, one receipt kept, the offer's own rules told once");
            Check(done.Fulfilled != null && done.Fulfilled.Lines.Count == 3 && done.Fulfilled.Lines[0].Contains("Sealed"), "and the result carries the receipt lines the screen shows (" + string.Join(" / ", done.Fulfilled.Lines.ToArray()) + ")");

            string afterOne = StoreSnapshot(s);
            PurchaseResult again = ledger.Complete(s, "weekly", "receipt-001", now);
            Check(!again.Success && again.Reason == PurchaseRefusal.AlreadyGranted && again.CanConfirm && StoreSnapshot(s) == afterOne && rules.Recorded == 1,
                  "ONCE PER RECEIPT: the same receipt delivered again grants nothing, writes nothing, and may still be confirmed so the store stops delivering it");

            // The offer's own rules gate Begin.
            PurchaseResult limited = ledger.Begin(s, "weekly", null, now);
            Check(!limited.Success && limited.Reason == PurchaseRefusal.NotAvailable && limited.Message == "Limit reached." && StoreSnapshot(s) == afterOne,
                  "LIMITS ARE THE OFFER'S RULES: Begin asks them and passes their sentence on (\"" + limited.Message + "\"), nothing is written");
            Check(ledger.Begin(s, "no_such_offer", null, now).Reason == PurchaseRefusal.NoSuchOffer && ledger.Complete(s, "no_such_offer", "receipt-x", now).Reason == PurchaseRefusal.NoSuchOffer
                  && ledger.Complete(s, "pass", "", now).Reason == PurchaseRefusal.NoReceipt && StoreSnapshot(s) == afterOne,
                  "an unknown offer and an empty receipt id are refused, nothing is written");

            // A different receipt is a different purchase.
            rules.Limit = 2;
            ledger.Begin(s, "weekly", null, now);
            PurchaseResult second = ledger.Complete(s, "weekly", "receipt-002", now);
            Check(second.Success && p.Store.Receipts.Count == 2 && p.Store.Record("weekly").Count == 2 && p.Sealed.Slag == 400,
                  "a new receipt is a new purchase (two receipts, Slag 400, all of it Sealed)");
            rules.Limit = 1;

            // A choice offer.
            GameSession c = StoreSession(db);
            before = StoreSnapshot(c);
            PurchaseResult noPick = ledger.Begin(c, "starter", null, now);
            PurchaseResult wrongPick = ledger.Begin(c, "starter", five[3].Id, now);
            Check(!noPick.Success && noPick.Reason == PurchaseRefusal.BadChoice && !wrongPick.Success && !ledger.IsPending("starter") && StoreSnapshot(c) == before,
                  "A CHOICE IS MADE BEFORE THE PURCHASE: Begin without a pick, or with a hero outside the named three, is refused and nothing is pending");
            ledger.Begin(c, "starter", five[1].Id, now);
            PurchaseResult paid = ledger.Complete(c, "starter", "receipt-s1", now);
            HeroInstance hero = c.Profile.FindHeroByDefId(five[1].Id);
            long starterGold = c.IdleYield(6 * 3600).Gold;
            Check(paid.Success && hero != null && hero.SealedCopiesHeld == 1 && c.Profile.Sealed.Gold == starterGold && c.Profile.FindHeroByDefId(five[0].Id) == null,
                  "THE STARTER PACT GRANTS THE NAMED HERO SEALED and 6 hours of idle Gold Sealed (" + starterGold + ")");

            // A delivery after a restart has no pick: refused, not confirmed, nothing guessed.
            GameSession d = StoreSession(db);
            before = StoreSnapshot(d);
            PurchaseLedger restarted = new PurchaseLedger(StoreRulesFor(db));
            PurchaseResult lost = restarted.Complete(d, "starter", "receipt-s2", now);
            Check(!lost.Success && lost.Reason == PurchaseRefusal.BadChoice && !lost.CanConfirm && StoreSnapshot(d) == before && d.Profile.Store.FindReceipt("receipt-s2") == null,
                  "A DELIVERY WITH NO PICK GRANTS NOTHING (DEFAULT TAKEN): refused, not confirmable so the store keeps it open, no receipt, nothing guessed");

            // A grant that cannot apply leaves no receipt.
            GameSession w = StoreSession(db);
            WayfarersRoadSwitch.Enabled = false;
            try
            {
                before = StoreSnapshot(w);
                PurchaseLedger pl = new PurchaseLedger(StoreRulesFor(db));
                pl.Begin(w, "pass", null, now);
                PurchaseResult noSeason = pl.Complete(w, "pass", "receipt-p1", now);
                Check(!noSeason.Success && noSeason.Reason == PurchaseRefusal.BadGrant && !noSeason.CanConfirm && StoreSnapshot(w) == before,
                      "A GRANT THAT CANNOT APPLY LEAVES NO RECEIPT: a pass premium with the road off is refused, nothing is written, the store keeps it open");
            }
            finally
            {
                WayfarersRoadSwitch.Enabled = true;
            }
        }

        // ------------------------------------------------------------------ 4. restore on a new profile

        private static void CheckStoreRestore(ContentDatabase db)
        {
            long now = StoreNow(db);
            List<HeroDef> five = StoreFiveStars(db);
            StoreTestRules rules = StoreRulesFor(db);
            PurchaseLedger ledger = new PurchaseLedger(rules);

            // The old profile bought the Starter Pact. The store account remembers the receipt.
            GameSession oldProfile = StoreSession(db);
            ledger.Begin(oldProfile, "starter", five[0].Id, now);
            ledger.Complete(oldProfile, "starter", "receipt-A", now);
            string productId = rules.Find("starter").ProductId;

            // A new profile on the same account restores it.
            GameSession fresh = StoreSession(db);
            PurchaseResult r = ledger.Restore(fresh, productId, "receipt-A", five[0].Id, now);
            PurchaseReceipt kept = fresh.Profile.Store.FindReceipt("receipt-A");
            HeroInstance hero = fresh.Profile.FindHeroByDefId(five[0].Id);
            Check(r.Success && kept != null && kept.Restored && hero != null && hero.SealedCopiesHeld == 1 && fresh.Profile.Sealed.Gold > 0 && fresh.Profile.Store.Record("starter").Count == 1,
                  "RESTORE ON A NEW PROFILE: the Starter Pact comes back once, Sealed, with its receipt marked restored and the offer counted so it cannot be bought again");

            string afterRestore = StoreSnapshot(fresh);
            PurchaseResult twice = ledger.Restore(fresh, productId, "receipt-A", five[0].Id, now);
            Check(!twice.Success && twice.Reason == PurchaseRefusal.AlreadyGranted && StoreSnapshot(fresh) == afterRestore,
                  "RESTORE IS ONCE: the same receipt restored again grants nothing and writes nothing");

            // Restoring on the profile that bought it changes nothing.
            string oldBefore = StoreSnapshot(oldProfile);
            PurchaseResult same = ledger.Restore(oldProfile, productId, "receipt-A", five[0].Id, now);
            Check(!same.Success && same.Reason == PurchaseRefusal.AlreadyGranted && StoreSnapshot(oldProfile) == oldBefore,
                  "and restoring on the profile that bought it changes nothing");

            // A pack that resets is not restorable.
            GameSession other = StoreSession(db);
            string before = StoreSnapshot(other);
            PurchaseResult pack = ledger.Restore(other, rules.Find("weekly").ProductId, "receipt-W", null, now);
            PurchaseResult unknown = ledger.Restore(other, "placeholder.unknown", "receipt-U", null, now);
            Check(!pack.Success && pack.Reason == PurchaseRefusal.NotRestorable && !unknown.Success && unknown.Reason == PurchaseRefusal.NoSuchOffer && StoreSnapshot(other) == before,
                  "A PACK IS NOT RESTORABLE: a weekly pack and an unknown product are refused, nothing is written");

            // A restore of a pick offer needs the pick.
            GameSession noPick = StoreSession(db);
            before = StoreSnapshot(noPick);
            PurchaseResult lacking = ledger.Restore(noPick, productId, "receipt-B", null, now);
            Check(!lacking.Success && lacking.Reason == PurchaseRefusal.BadChoice && !lacking.CanConfirm && StoreSnapshot(noPick) == before,
                  "a restore of the Starter Pact without a pick is refused and not confirmable: the store keeps it open until the player picks");
        }

        // ------------------------------------------------------------------ 5. nothing bought reaches a random mechanic

        private static void CheckStoreRandomMechanicsRefuse(ContentDatabase db)
        {
            long now = StoreNow(db);
            List<HeroDef> five = StoreFiveStars(db);
            StoreTestRules rules = StoreRulesFor(db);
            rules.Add("everything", false, StoreGrantOf(new GrantItem(GrantKind.Gold, 2000000), new GrantItem(GrantKind.Slag, 100000), new GrantItem(GrantKind.Dust, 50), new GrantItem(GrantKind.IdleGoldHours, 48)));
            PurchaseLedger ledger = new PurchaseLedger(rules);

            // A profile that holds ONLY purchased goods.
            GameSession s = StoreSession(db);
            ledger.Begin(s, "everything", null, now);
            PurchaseResult bought = ledger.Complete(s, "everything", "receipt-E1", now);
            PlayerProfile p = s.Profile;
            Check(bought.Success && p.FreeGold == 0 && p.FreeSlag == 0 && p.FreeShards == 0 && p.Gold > 2000000 && p.Slag == 100000,
                  "EVERYTHING A PURCHASE PAYS IS SEALED: " + p.Gold + " Gold and " + p.Slag + " Slag in the purse, " + p.FreeGold + " Gold, " + p.FreeSlag + " Slag and " + p.FreeShards + " Shards free for a random mechanic");

            // Summon: no free Shards, and a purchase pays none.
            string before = StoreSnapshot(s);
            SummonOutcome summon = s.Summon(4242, now);
            Check(!summon.Success && !s.CanSummon && StoreSnapshot(s) == before, "SUMMON refuses a profile that holds only purchased goods, the save byte for byte as it was");

            // The Quartermaster sells rolled armor for FREE Gold only.
            Quartermaster.Open(s, now);
            before = StoreSnapshot(s);
            QuartermasterResult qm = Quartermaster.Buy(s, 0, now);
            GameSession control = StoreSession(db);
            control.Profile.Gold = 100000000;
            Quartermaster.Open(control, now);
            QuartermasterResult controlBuy = Quartermaster.Buy(control, 0, now);
            Check(!qm.Success && StoreSnapshot(s) == before && controlBuy.Success,
                  "THE QUARTERMASTER refuses " + p.Gold + " Sealed Gold: rolled armor is not sold for purchased Gold (the same shelf sells for earned Gold)");

            // The forge: reroll and combine refuse Sealed Slag.
            ArmorPiece e1 = SealedPiece(db, s, ArmorSlot.Weapon, ArmorTemper.Rough, 31, false);
            ArmorPiece e2 = SealedPiece(db, s, ArmorSlot.Weapon, ArmorTemper.Rough, 32, false);
            ArmorPiece e3 = SealedPiece(db, s, ArmorSlot.Weapon, ArmorTemper.Rough, 33, false);
            before = StoreSnapshot(s);
            bool rr = s.RerollArmor(e1.Uid, now), cc = s.CombineArmor(e1.Uid, e2.Uid, e3.Uid, now);
            Check(!rr && !cc && StoreSnapshot(s) == before, "THE FORGE refuses Sealed Slag: reroll and combine do nothing with 100000 purchased Slag, the save byte for byte as it was");

            // Conversion: a purchased hero copy is never a body.
            GameSession c = StoreSession(db);
            HeroDef bodyDef = five[0];
            rules.Add("copy", false, StoreGrantOf(new GrantItem { Kind = GrantKind.HeroCopy, HeroId = bodyDef.Id }));
            rules.Limit = 10;
            for (int i = 0; i < 4; i++)
            {
                ledger.Begin(c, "copy", null, now);
                ledger.Complete(c, "copy", "receipt-C" + i, now);
            }
            HeroInstance body = c.Profile.FindHeroByDefId(bodyDef.Id);
            Check(body != null && body.Copies == 4 && body.SealedCopiesHeld == 4 && c.ConversionSpare(body) == 0,
                  "CONVERSION never counts a purchased copy: 4 copies bought, 4 Sealed, " + c.ConversionSpare(body) + " free to give");

            // The deterministic sinks still take what was bought.
            GameSession l = StoreSession(db);
            ledger.Begin(l, "everything", null, now);
            ledger.Complete(l, "everything", "receipt-E2", now);
            HeroInstance lead = l.Profile.Heroes[0];
            long sealedBefore = l.Profile.Sealed.Gold;
            LevelUpResult lv = l.LevelUpHero(lead, 1, now);
            Check(lv.Success && l.Profile.Sealed.Gold < sealedBefore, "A LEVEL-UP takes purchased Gold: the deterministic sink accepts what the random ones refuse");
        }

        // ------------------------------------------------------------------ 6. the paid track

        private static void CheckStorePass(ContentDatabase db)
        {
            long start = PassSeasonStart(db, 5), now = start + 600;
            StoreTestRules rules = StoreRulesFor(db);
            PurchaseLedger ledger = new PurchaseLedger(rules);
            WayfarersRoadConfig cfg = db.WayfarersRoad;

            GameSession s = StoreSession(db);
            WayfarersRoad.OnClaim(s, PassPointSource.Order, now);
            s.Profile.WayfarersRoad.Points = 30 * cfg.TierPoints;
            string before = StoreSnapshot(s);
            bool locked = true;
            for (int t = 0; t <= 31; t++)
            {
                PassResult r = WayfarersRoad.ClaimPaid(s, t, null, now);
                if (r.Success || r.Reason != PassRefusal.PaidLocked) locked = false;
            }
            Check(locked && StoreSnapshot(s) == before, "THE PAID TRACK IS LOCKED UNTIL BOUGHT: with the Store on and no premium, every paid claim is PaidLocked and the save is byte for byte as it was");

            ledger.Begin(s, "pass", null, now);
            PurchaseResult bought = ledger.Complete(s, "pass", "receipt-P1", now);
            WayfarersRoadState st = s.Profile.WayfarersRoad;
            Check(bought.Success && st.PaidSeason == 5 && st.SeasonIndex == 5 && st.Points == 30 * cfg.TierPoints,
                  "THE PREMIUM OPENS THIS SEASON'S PAID TRACK: PaidSeason is 5, the points earned so far stay");

            // Claims.
            PassRewardDef t1 = cfg.PaidAt(1), t5 = cfg.PaidAt(5), t30 = cfg.PaidAt(30);
            PassResult c1 = WayfarersRoad.ClaimPaid(s, 1, null, now);
            long goldBefore = s.Profile.Gold;
            PassResult c5 = WayfarersRoad.ClaimPaid(s, 5, null, now);
            long expected = s.IdleYield((long)Math.Round(t5.Amount * 3600.0)).Gold;
            Check(t1 != null && t1.Kind == PassRewardKind.Cosmetic && c1.Success && st.HasClaimedPaid(1) && c1.Gold == 0,
                  "a cosmetic tier is claimed and grants nothing");
            Check(c5.Success && s.Profile.Gold == goldBefore + expected && s.Profile.Sealed.Gold == goldBefore + expected && expected > 0 && s.Profile.Shards == 0,
                  "A PAID HOURGLASS IS SEALED GOLD: tier 5 pays " + t5.Amount + " hours = " + expected + " Gold, all Sealed, no Shards");
            before = StoreSnapshot(s);
            PassResult dup = WayfarersRoad.ClaimPaid(s, 5, null, now);
            Check(!dup.Success && dup.Reason == PassRefusal.Claimed && StoreSnapshot(s) == before, "a paid tier claims once");

            List<HeroDef> pool = WayfarersRoad.SelectorHeroes(s, 5);
            HeroDef pickHero = pool[0];
            PassResult badPick = WayfarersRoad.ClaimPaid(s, 30, "no_such_hero", now);
            Check(t30 != null && t30.Kind == PassRewardKind.Selector && !badPick.Success && badPick.Reason == PassRefusal.BadChoice && StoreSnapshot(s) == before,
                  "tier 30 needs a 5-star from the pass's pool: a bad pick is refused, nothing is written");
            PassResult c30 = WayfarersRoad.ClaimPaid(s, 30, pickHero.Id, now);
            HeroInstance got = s.Profile.FindHeroByDefId(pickHero.Id);
            Check(c30.Success && got != null && got.SealedCopiesHeld >= 1, "THE PAID 5-STAR IS A SEALED COPY (the free track's is a plain earned copy)");

            // Not reached.
            GameSession low = StoreSession(db);
            ledger.Begin(low, "pass", null, now);
            ledger.Complete(low, "pass", "receipt-P2", now);
            before = StoreSnapshot(low);
            PassResult early = WayfarersRoad.ClaimPaid(low, 5, null, now);
            Check(!early.Success && early.Reason == PassRefusal.NotReached && StoreSnapshot(low) == before, "a paid tier needs its tier reached on the track");

            // A later season locks it; a wound-back clock changes nothing.
            long nextSeason = now + cfg.SeasonDays * 86400L;
            before = StoreSnapshot(s);
            PassResult later = WayfarersRoad.ClaimPaid(s, 10, null, nextSeason);
            PassResult wound = WayfarersRoad.ClaimPaid(s, 10, null, now - cfg.SeasonDays * 86400L);
            Check(!later.Success && later.Reason == PassRefusal.PaidLocked && !wound.Success && wound.Reason == PassRefusal.PaidLocked && StoreSnapshot(s) == before,
                  "THE PREMIUM IS PER SEASON: the next season's paid track is locked again with no step to run, and a wound-back clock opens nothing");

            // The switch.
            StoreSwitch.Enabled = false;
            try
            {
                before = StoreSnapshot(s);
                PassResult off = WayfarersRoad.ClaimPaid(s, 10, null, now);
                Check(!off.Success && off.Reason == PassRefusal.PaidLocked && StoreSnapshot(s) == before, "with the Store off even a bought track is locked, and the save is untouched");
            }
            finally
            {
                StoreSwitch.Enabled = true;
            }

            // A new season's roll clears the paid claims.
            WayfarersRoad.Roll(s, nextSeason);
            Check(s.Profile.WayfarersRoad.SeasonIndex == 6 && s.Profile.WayfarersRoad.PaidClaimedMask == 0 && s.Profile.WayfarersRoad.PaidSeason == 5,
                  "a new season clears the paid claims and leaves the old PaidSeason behind, which no longer matches");
        }

        // ------------------------------------------------------------------ 7. the save

        private static void CheckStoreSave(ContentDatabase db)
        {
            long now = StoreNow(db);
            string error;
            StoreTestRules rules = StoreRulesFor(db);
            PurchaseLedger ledger = new PurchaseLedger(rules);
            List<HeroDef> five = StoreFiveStars(db);

            GameSession plain = StoreSession(db);
            string plainJson = StoreSnapshot(plain);
            Check(SaveSerializer.VersionOf(plain.Profile) == SaveSerializer.BaseVersion && !plainJson.Contains("\"store\""),
                  "A PROFILE THAT NEVER BOUGHT claims version " + SaveSerializer.BaseVersion + " and carries no store key: the document the build before wrote");

            GameSession s = StoreSession(db);
            ledger.Begin(s, "starter", five[0].Id, now);
            ledger.Complete(s, "starter", "receipt-V1", now);
            ledger.Begin(s, "pass", null, now);
            ledger.Complete(s, "pass", "receipt-V2", now);
            string json = StoreSnapshot(s);
            PlayerProfile back = SaveSerializer.Deserialize(json, db, out error);
            Check(back != null && error == null && SaveSerializer.Serialize(back) == json && back.Store.Receipts.Count == 2 && back.Store.FindReceipt("receipt-V1") != null
                  && back.Store.Record("starter").Count == 1 && back.WayfarersRoad.PaidSeason == 5,
                  "STORE SAVE ROUND TRIP: two receipts, a counter and a bought pass save, load back identical and save again byte for byte (" + json.Length + " characters)");
            Check(JsonValue.Parse(json).GetInt("version", 0) == 25 && SaveSerializer.VersionOfJson(json) == 25 && SaveSerializer.CurrentVersion == 25 && json.Contains("\"store\"") && json.Contains("\"paid\""),
                  "and the document claims version 25, the version that holds the store");

            // Sealed goods alone stay 24; the store alone, or a bought track alone, is 25.
            GameSession sealedOnly = StoreSession(db);
            SealedGrants.GrantGold(sealedOnly, 10);
            GameSession receiptOnly = StoreSession(db);
            receiptOnly.Profile.Store.Receipts.Add(new PurchaseReceipt { ReceiptId = "r", ProductId = "p", OfferId = "o", GrantedUnix = 1 });
            GameSession counterOnly = StoreSession(db);
            counterOnly.Profile.Store.Record("weekly").Count = 1;
            GameSession paidOnly = StoreSession(db);
            paidOnly.Profile.WayfarersRoad.PaidSeason = 3;
            GameSession seasonOnly = StoreSession(db);
            seasonOnly.Profile.WayfarersRoad.Points = 10;
            seasonOnly.Profile.WayfarersRoad.SeasonIndex = 2;
            Check(SaveSerializer.VersionOf(sealedOnly.Profile) == 24 && SaveSerializer.VersionOf(seasonOnly.Profile) == 24
                  && SaveSerializer.VersionOf(receiptOnly.Profile) == 25 && SaveSerializer.VersionOf(counterOnly.Profile) == 25 && SaveSerializer.VersionOf(paidOnly.Profile) == 25,
                  "A DOCUMENT CLAIMS 25 ONLY WHEN IT HOLDS VERSION 25 DATA: Sealed goods or a free season alone stay 24, a receipt, a counter or a bought track is 25");

            // An empty record is not data.
            GameSession emptyRec = StoreSession(db);
            emptyRec.Profile.Store.Record("weekly");
            Check(StoreSnapshot(emptyRec) == plainJson && SaveSerializer.VersionOf(emptyRec.Profile) == SaveSerializer.BaseVersion, "an empty offer record is not written and does not change the version");

            // Damaged blocks read as empty or skip the bad entry.
            string[] damaged =
            {
                "5", "\"x\"", "[]", "{\"receipts\":\"x\"}", "{\"receipts\":[1,2,\"x\"],\"offers\":[null,5]}",
                "{\"receipts\":[{\"product\":\"p\"},{\"id\":\"\"}],\"offers\":[{\"count\":3},{\"offer\":\"\"}]}"
            };
            bool allEmpty = true;
            for (int i = 0; i < damaged.Length; i++)
            {
                StoreState st = StoreState.Read(JsonValue.Parse(damaged[i]));
                if (!st.IsEmpty) allEmpty = false;
            }
            Check(allEmpty && StoreState.Read(null).IsEmpty, "DAMAGED STORE BLOCKS READ AS EMPTY: a number, text, an array, wrong types and entries with no id are skipped");

            StoreState dup = StoreState.Read(JsonValue.Parse("{\"receipts\":[{\"id\":\"a\",\"at\":5},{\"id\":\"a\",\"at\":9}],\"offers\":[{\"offer\":\"w\",\"count\":2},{\"offer\":\"w\",\"count\":7}]}"));
            Check(dup.Receipts.Count == 1 && dup.Receipts[0].GrantedUnix == 5 && dup.Offers.Count == 1 && dup.Find("w").Count == 2, "a repeated receipt id or offer id keeps its first entry");

            System.Text.StringBuilder big = new System.Text.StringBuilder();
            big.Append("{\"receipts\":[");
            for (int i = 0; i < StoreState.ReceiptsMax + 20; i++) big.Append((i > 0 ? "," : "") + "{\"id\":\"r" + i + "\"}");
            big.Append("]}");
            StoreState clamped = StoreState.Read(JsonValue.Parse(big.ToString()));
            StoreState nums = StoreState.Read(JsonValue.Parse("{\"offers\":[{\"offer\":\"w\",\"count\":-4,\"period\":-1,\"mask\":-1},{\"offer\":\"x\",\"count\":99999999}]}"));
            Check(clamped.Receipts.Count == StoreState.ReceiptsMax && nums.Find("w") == null && nums.Find("x").Count == 100000, "receipts are capped at " + StoreState.ReceiptsMax + " and negative or huge numbers are clamped");

            // The ledger's own cap drops the oldest and never grows past it.
            GameSession cap = StoreSession(db);
            rules.Limit = int.MaxValue;
            rules.Add("tiny", false, StoreGrantOf(new GrantItem(GrantKind.Dust, 1)));
            for (int i = 0; i < StoreState.ReceiptsMax + 3; i++) { ledger.Begin(cap, "tiny", null, now); ledger.Complete(cap, "tiny", "cap-" + i, now); }
            Check(cap.Profile.Store.Receipts.Count == StoreState.ReceiptsMax && cap.Profile.Store.FindReceipt("cap-0") == null && cap.Profile.Store.FindReceipt("cap-" + (StoreState.ReceiptsMax + 2)) != null,
                  "the receipt list never grows past " + StoreState.ReceiptsMax + ": the oldest are dropped");
        }

        // ------------------------------------------------------------------ 8. the build mode

        private static void CheckStoreModes()
        {
            Check(StoreModes.Resolve(false, true, true, true) == StoreMode.Off && StoreModes.Resolve(false, false, false, false) == StoreMode.Off,
                  "STORE OFF is off in every build");
            Check(StoreModes.Resolve(true, true, false, false) == StoreMode.Test && StoreModes.Resolve(true, true, true, true) == StoreMode.Test,
                  "the editor and a development build are the TEST store (the fake store and the debug store), even with the live define");
            Check(StoreModes.Resolve(true, false, false, false) == StoreMode.ComingSoon && StoreModes.Resolve(true, false, true, false) == StoreMode.ComingSoon
                  && StoreModes.Resolve(true, false, false, true) == StoreMode.ComingSoon,
                  "A RELEASE BUILD IS COMING SOON unless it defines the live store AND carries the IAP package, which nothing in this run does");
            Check(StoreModes.CanBuy(StoreMode.Test) && StoreModes.CanBuy(StoreMode.Live) && !StoreModes.CanBuy(StoreMode.ComingSoon) && !StoreModes.CanBuy(StoreMode.Off),
                  "and only the test and live stores draw a buy button: coming soon and off draw none");
        }

        // ------------------------------------------------------------------ 8b. the flow with the debug store

        private sealed class FlowRig
        {
            public StoreTestRules Rules;
            public SimulatedStore Store = new SimulatedStore();
            public PurchaseFlow Flow;
            public GameSession Session;
            public int Saves;
            public readonly List<PurchaseResult> Finished = new List<PurchaseResult>();
            public readonly List<string> Failed = new List<string>();
        }

        private static FlowRig StoreRig(ContentDatabase db, GameSession session, string[] account, StoreMode mode, bool withBackend)
        {
            FlowRig rig = new FlowRig();
            rig.Rules = StoreRulesFor(db);
            rig.Session = session;
            rig.Store.LoadAccount = () => account[0];
            rig.Store.SaveAccount = text => account[0] = text;
            rig.Flow = new PurchaseFlow(rig.Rules, withBackend ? rig.Store : null, mode, () => rig.Session, () => StoreNow(db), () => rig.Saves++);
            rig.Flow.Finished += r => rig.Finished.Add(r);
            rig.Flow.Failed += (id, text) => rig.Failed.Add(id + ": " + text);
            List<StoreOffer> offers = new List<StoreOffer>(rig.Rules.Offers.Values);
            rig.Flow.Start(offers);
            return rig;
        }

        private static void CheckStoreFlow(ContentDatabase db)
        {
            List<HeroDef> five = StoreFiveStars(db);
            string[] account = { null };

            // A purchase end to end in the debug store.
            GameSession s = StoreSession(db);
            FlowRig a = StoreRig(db, s, account, StoreMode.Test, true);
            string before = StoreSnapshot(s);
            PurchaseResult begun = a.Flow.Buy("weekly", null);
            Check(begun.Success && a.Flow.IsPending("weekly") && StoreSnapshot(s) == before && a.Saves == 0,
                  "FLOW: Buy starts the purchase in the store and writes nothing to the save");
            a.Flow.Tick(0.1);
            Check(a.Finished.Count == 0 && StoreSnapshot(s) == before, "and the store has not answered yet, so nothing is granted");
            a.Flow.Tick(0.5);
            Check(a.Finished.Count == 1 && a.Finished[0].Success && s.Profile.Store.FindReceipt("DEBUG-000001") != null && s.Profile.Sealed.Slag == 200 && a.Saves == 1
                  && a.Store.OpenCount == 0 && !a.Flow.IsPending("weekly"),
                  "the store completes it: granted once with receipt DEBUG-000001, saved once, and the store told to close it (open purchases: " + a.Store.OpenCount + ")");

            // A purchase that stays pending past the timeout grants nothing.
            GameSession h = StoreSession(db);
            string[] account2 = { null };
            FlowRig held = StoreRig(db, h, account2, StoreMode.Test, true);
            held.Store.AutoApprove = false;
            string heldBefore = StoreSnapshot(h);
            held.Flow.Buy("weekly", null);
            held.Flow.Tick(PurchaseFlow.PendingTimeoutSeconds - 1.0);
            bool stillPending = held.Flow.IsPending("weekly");
            held.Flow.Tick(2.0);
            Check(stillPending && !held.Flow.IsPending("weekly") && held.Failed.Count == 1 && held.Failed[0].Contains("Nothing was bought") && StoreSnapshot(h) == heldBefore && held.Saves == 0,
                  "A PENDING PURCHASE THAT NEVER COMPLETES GRANTS NOTHING: held by the store for " + PurchaseFlow.PendingTimeoutSeconds + " seconds it is abandoned, reported, and the save is byte for byte as it was");

            // Declined.
            GameSession d = StoreSession(db);
            FlowRig declined = StoreRig(db, d, new string[] { null }, StoreMode.Test, true);
            declined.Store.AutoApprove = false;
            string declinedBefore = StoreSnapshot(d);
            declined.Flow.Buy("weekly", null);
            declined.Store.Decline();
            Check(declined.Failed.Count == 1 && declined.Failed[0].Contains("cancelled") && !declined.Flow.IsPending("weekly") && StoreSnapshot(d) == declinedBefore && declined.Saves == 0,
                  "a cancelled purchase grants nothing and writes nothing");
            Check(declined.Flow.Buy("weekly", null).Success, "and the same offer can be started again after a cancel");

            // A second tap while one waits.
            PurchaseResult twice = declined.Flow.Buy("weekly", null);
            Check(!twice.Success && twice.Reason == PurchaseRefusal.NotAvailable, "a second Buy while the first waits for the store is refused");

            // A late answer after the timeout still grants once: the player was charged.
            held.Store.Approve();
            Check(held.Finished.Count == 1 && held.Finished[0].Success && h.Profile.Store.Receipts.Count == 1 && h.Profile.Sealed.Slag == 200 && held.Store.OpenCount == 0,
                  "a late answer after the timeout still grants once and closes (the player was charged), Sealed like any other");

            // A purchase the store finished and nobody confirmed is delivered again, and granted once.
            string[] acct3 = { null };
            GameSession g = StoreSession(db);
            SimulatedStore bare = new SimulatedStore();
            bare.LoadAccount = () => acct3[0];
            bare.SaveAccount = t => acct3[0] = t;
            StoreTestRules r3 = StoreRulesFor(db);
            bare.Initialize(new List<StoreOffer>(r3.Offers.Values), new SilentListener());
            bare.Purchase(r3.Find("weekly").ProductId);
            bare.Tick(1.0);
            FlowRig again = StoreRig(db, g, acct3, StoreMode.Test, true);
            Check(again.Finished.Count == 1 && again.Finished[0].Success && g.Profile.Sealed.Slag == 200 && again.Store.OpenCount == 0 && again.Saves == 1,
                  "AN OPEN PURCHASE IS DELIVERED AGAIN AT THE NEXT START and granted once (the app was closed before the grant)");
            FlowRig third = StoreRig(db, g, acct3, StoreMode.Test, true);
            Check(third.Finished.Count == 0 && g.Profile.Sealed.Slag == 200, "and once it is confirmed it is not delivered a third time");

            // Restore on a new profile, through the account.
            string[] acct4 = { null };
            GameSession oldP = StoreSession(db);
            FlowRig bought = StoreRig(db, oldP, acct4, StoreMode.Test, true);
            bought.Flow.Buy("starter", five[1].Id);
            bought.Flow.Tick(1.0);
            Check(bought.Finished.Count == 1 && bought.Finished[0].Success && bought.Store.RestorableCount == 1, "FLOW RESTORE: the Starter Pact is bought and the account remembers it");
            GameSession newP = StoreSession(db);
            FlowRig restored = StoreRig(db, newP, acct4, StoreMode.Test, true);
            restored.Flow.Restore(offerId => "");
            Check(restored.Flow.RestoreWaiting.Count == 1 && newP.Profile.Store.Receipts.Count == 0 && newP.Profile.FindHeroByDefId(five[1].Id) == null,
                  "a restore of a pick offer waits for the pick: nothing is granted yet and the screen is told");
            PurchaseResult finish = restored.Flow.FinishRestore(restored.Flow.RestoreWaiting[0][0], five[1].Id);
            HeroInstance back = newP.Profile.FindHeroByDefId(five[1].Id);
            Check(finish.Success && back != null && back.SealedCopiesHeld == 1 && restored.Flow.RestoreWaiting.Count == 0 && newP.Profile.Store.Receipts[0].Restored,
                  "RESTORE ON A NEW PROFILE: after the pick the hero comes back Sealed, once, the receipt marked restored");
            FlowRig restoredAgain = StoreRig(db, newP, acct4, StoreMode.Test, true);
            string restoredSnapshot = StoreSnapshot(newP);
            restoredAgain.Flow.Restore(offerId => five[1].Id);
            Check(StoreSnapshot(newP) == restoredSnapshot, "and a second restore on that profile changes nothing");

            // A weekly pack is not in the account after it is confirmed (consumable).
            Check(a.Store.RestorableCount == 0, "a confirmed pack is closed and forgotten by the store: nothing to restore");

            // Coming soon: no backend, no button, no purchase.
            GameSession c = StoreSession(db);
            FlowRig soon = StoreRig(db, c, new string[] { null }, StoreMode.ComingSoon, false);
            string soonBefore = StoreSnapshot(c);
            PurchaseResult tap = soon.Flow.Buy("weekly", null);
            soon.Flow.Tick(10);
            soon.Flow.Restore(offerId => "");
            Check(!soon.Flow.CanBuy && !tap.Success && tap.Reason == PurchaseRefusal.ComingSoon && !soon.Flow.IsPending("weekly") && StoreSnapshot(c) == soonBefore && soon.Saves == 0,
                  "COMING SOON: a release build has no buy path (CanBuy is false), a tap is refused as coming soon, and the save is byte for byte as it was");
            FlowRig soonWithStore = StoreRig(db, c, new string[] { null }, StoreMode.ComingSoon, true);
            soonWithStore.Flow.Buy("weekly", null);
            soonWithStore.Flow.Tick(10);
            Check(soonWithStore.Store.PendingCount == 0 && !soonWithStore.Store.IsReady && StoreSnapshot(c) == soonBefore,
                  "and even with a store object present, coming soon never starts it and never asks it to purchase");
        }

        private sealed class SilentListener : IStoreBackendListener
        {
            public void OnPurchaseCompleted(string productId, string receiptId) { }
            public void OnPurchaseFailed(string productId, StoreFailure reason, string detail) { }
            public void OnRestoredPurchase(string productId, string receiptId) { }
            public void OnRestoreFinished(bool success, string detail) { }
        }

        // ------------------------------------------------------------------ 9. rollback

        private static void CheckStoreOff(ContentDatabase db)
        {
            long now = StoreNow(db);
            List<HeroDef> five = StoreFiveStars(db);
            StoreTestRules rules = StoreRulesFor(db);
            PurchaseLedger ledger = new PurchaseLedger(rules);
            bool was = StoreSwitch.Enabled;
            try
            {
                StoreSwitch.Enabled = false;
                GameSession off = StoreSession(db);
                for (int d = 0; d < 3; d++) PassPlayDay(off, now + d * 86400L);
                string offJson = StoreSnapshot(off);
                PurchaseResult b = ledger.Begin(off, "weekly", null, now);
                PurchaseResult c = ledger.Complete(off, "weekly", "receipt-off", now);
                PurchaseResult r = ledger.Restore(off, rules.Find("starter").ProductId, "receipt-off2", five[0].Id, now);
                Check(b.Reason == PurchaseRefusal.Off && c.Reason == PurchaseRefusal.Off && r.Reason == PurchaseRefusal.Off && !ledger.IsPending("weekly") && StoreSnapshot(off) == offJson,
                      "OFF IS INERT: with StoreSwitch off Begin, Complete and Restore refuse at once and the save is byte for byte as it was");

                StoreSwitch.Enabled = true;
                GameSession on = StoreSession(db);
                for (int d = 0; d < 3; d++) PassPlayDay(on, now + d * 86400L);
                Check(StoreSnapshot(on) == offJson, "ON WITH NOTHING BOUGHT IS OFF: three days of play with the Store on is the off save, byte for byte, version included");
            }
            finally
            {
                StoreSwitch.Enabled = was;
            }
        }

        // ------------------------------------------------------------------ 10. no random path

        private static void CheckStoreNoRandomPath(string contentDir)
        {
            string[] kinds = Enum.GetNames(typeof(GrantKind));
            string expected = "Gold,Slag,Dust,IdleGoldHours,HeroCopy,HeroChoice,OwnedHeroCopy,PassPremium,Activate";
            Check(string.Join(",", kinds) == expected,
                  "THE GRANT KINDS ARE A CLOSED LIST OF FIXED LINES (" + string.Join(", ", kinds) + "): adding one is a decision for review, and none rolls, draws or summons");

            string root = SealedRepoRoot(contentDir);
            if (root == null) { Check(false, "STORE: the repository root was not found from " + contentDir); return; }

            // The purchase path's files, comments and strings removed, name no random mechanic.
            string[] core =
            {
                Path.Combine("Assets", "Scripts", "Core", "Data", "PurchaseGrant.cs"),
                Path.Combine("Assets", "Scripts", "Core", "Progression", "PurchaseGrants.cs"),
                Path.Combine("Assets", "Scripts", "Core", "Progression", "PurchaseLedger.cs"),
                Path.Combine("Assets", "Scripts", "Core", "Progression", "PurchaseFlow.cs"),
                Path.Combine("Assets", "Scripts", "Core", "Progression", "SimulatedStore.cs"),
                Path.Combine("Assets", "Scripts", "Core", "Progression", "StoreBackend.cs"),
                Path.Combine("Assets", "Scripts", "Core", "Model", "StoreState.cs")
            };
            List<string> files = new List<string>(core);
            string storeDir = Path.Combine(root, "Assets", "Scripts", "Game", "Store");
            if (Directory.Exists(storeDir))
            {
                string[] wrapper = Directory.GetFiles(storeDir, "*.cs", SearchOption.AllDirectories);
                for (int i = 0; i < wrapper.Length; i++) files.Add(wrapper[i].Substring(root.Length + 1));
            }
            Regex banned = new Regex(@"\b(Random|DeterministicRng|ArmorRoller|Summon\w*|Reroll\w*|CombineArmor|Quartermaster\w*|Conversion\w*|Convert\w*Hero\w*|Generator|ForgeGamble|Gamble)\b");
            List<string> hits = new List<string>();
            for (int i = 0; i < files.Count; i++)
            {
                string path = Path.Combine(root, files[i]);
                if (!File.Exists(path)) { hits.Add(files[i] + " is missing"); continue; }
                string code = StoreStripCommentsAndStrings(File.ReadAllText(path));
                MatchCollection m = banned.Matches(code);
                for (int k = 0; k < m.Count; k++) hits.Add(Path.GetFileName(files[i]) + ": " + m[k].Value);
            }
            Check(hits.Count == 0, "NO PURCHASE GRANT PATH REACHES A RANDOM MECHANIC: " + files.Count + " files of the purchase path (grant, ledger, state, store wrapper) name no random number, roll, summon, reroll, forge, quartermaster or conversion"
                  + (hits.Count > 0 ? " (" + string.Join(", ", hits.ToArray()) + ")" : ""));

            // Who may make a Sealed mark, and who may apply a grant.
            List<string> sealers = new List<string>(), appliers = new List<string>();
            string[] all = Directory.GetFiles(Path.Combine(root, "Assets"), "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < all.Length; i++)
            {
                string name = Path.GetFileName(all[i]);
                string code = StoreStripCommentsAndStrings(File.ReadAllText(all[i]));
                if (name != "SealedGrants.cs" && code.Contains("SealedGrants.")) sealers.Add(name);
                if (name != "PurchaseGrants.cs" && code.Contains("PurchaseGrants.Apply(")) appliers.Add(name);
            }
            sealers.Sort(); appliers.Sort();
            Check(sealers.Count == 1 && sealers[0] == "PurchaseGrants.cs",
                  "ONE PLACE MAKES A SEALED MARK: SealedGrants is called by PurchaseGrants alone (" + string.Join(", ", sealers.ToArray()) + ")");
            bool appliersOk = true;
            for (int i = 0; i < appliers.Count; i++)
                if (appliers[i] != "WayfarersRoad.cs" && !appliers[i].StartsWith("Store")) appliersOk = false;
            Check(appliersOk && appliers.Count > 0,
                  "and a grant is applied only by the Store's own rules and the paid track (" + string.Join(", ", appliers.ToArray()) + ")");
        }

        /// <summary>Source with comments and string literals blanked, so a word in prose or a message is not read as code.</summary>
        private static string StoreStripCommentsAndStrings(string text)
        {
            text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            text = Regex.Replace(text, @"//[^\r\n]*", " ");
            text = Regex.Replace(text, "\"(\\\\.|[^\"\\\\\\r\\n])*\"", "\"\"");
            return text;
        }
    }
}
