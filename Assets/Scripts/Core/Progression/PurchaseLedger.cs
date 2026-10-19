using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Model;
using Emberfall.Core.Session;

namespace Emberfall.Core.Progression
{
    /// <summary>What the Store's rules did with a delivered purchase.</summary>
    public sealed class FulfilOutcome
    {
        public bool Success;
        /// <summary>True when the refusal is a missing or wrong pick of a hero, so the store should keep
        /// the purchase open until the player picks.</summary>
        public bool BadChoice;
        /// <summary>One plain sentence per line granted, for the receipt: "3,400 Gold (Sealed)".</summary>
        public readonly List<string> Lines = new List<string>();
        public string Reason = "";
    }

    /// <summary>
    /// What the Store's offer rules (limits, weekly and monthly resets, the Ember Ledger, the Growth Fund)
    /// answer to the purchase path. Implemented by the offer rules (StoreRules over StoreOffers); the ledger
    /// knows nothing of a limit and nothing of what an offer contains.
    /// </summary>
    public interface IStoreRules
    {
        /// <summary>True when the offer may be bought now with this pick. On false, reason is a sentence a
        /// player can read and badChoice says the pick is what is missing or wrong. Reads, writes nothing.</summary>
        bool CanBuy(GameSession session, string offerId, string choiceHeroId, long nowUnix, out string reason, out bool badChoice);

        /// <summary>Grants the offer once and counts the purchase (its limit, its period, a card's first
        /// day), all or nothing. Every good it grants must be made through PurchaseGrants.Apply, so it is
        /// Sealed. A refusal writes nothing.</summary>
        FulfilOutcome Fulfil(GameSession session, string offerId, string choiceHeroId, long nowUnix);

        StoreOffer Find(string offerId);
        StoreOffer FindByProduct(string productId);
    }

    /// <summary>Why the ledger refused. None is success.</summary>
    public enum PurchaseRefusal
    {
        None,
        /// <summary>StoreSwitch is off.</summary>
        Off,
        /// <summary>The build sells nothing (a release build): the offer is "coming soon".</summary>
        ComingSoon,
        NoSuchOffer,
        /// <summary>The offer's own rules say not now (a limit, a period).</summary>
        NotAvailable,
        /// <summary>The offer needs a hero from the buyer and none valid was given.</summary>
        BadChoice,
        /// <summary>The offer's grant cannot be applied (a malformed grant, no season for the pass).</summary>
        BadGrant,
        /// <summary>The receipt was granted before. Nothing is written; the store should still be told the
        /// purchase is finished so it stops delivering it.</summary>
        AlreadyGranted,
        /// <summary>Restore asked for a product that is not restorable.</summary>
        NotRestorable,
        /// <summary>The receipt id is empty.</summary>
        NoReceipt
    }

    /// <summary>The outcome of a ledger call.</summary>
    public sealed class PurchaseResult
    {
        public bool Success;
        public PurchaseRefusal Reason = PurchaseRefusal.None;
        public string OfferId = "";
        public string Message = "";
        public FulfilOutcome Fulfilled;

        /// <summary>True when the store may be told the purchase is finished (confirmed): it was granted now
        /// or was granted before. False on every other refusal, so the store keeps the purchase open and
        /// delivers it again.</summary>
        public bool CanConfirm
        {
            get { return Success || Reason == PurchaseRefusal.AlreadyGranted; }
        }
    }

    /// <summary>
    /// THE PURCHASE LEDGER (R19, Docs/DESIGN-STORE.md): the rules of a purchase, in Core, with no store in
    /// them. The Unity IAP wrapper is a thin layer that calls these; the harness proves every rule without
    /// Unity.
    ///
    ///  - A GRANT HAPPENS IN ONE PLACE, Complete (or Restore), and only when the store reports the purchase
    ///    complete with a receipt id. Begin writes nothing to the profile: a purchase that is started and
    ///    never completed, failed, cancelled, or still pending at exit grants nothing and leaves the save
    ///    byte for byte as it was. Pending purchases live in memory only.
    ///  - ONCE PER RECEIPT. The receipt id is kept in PlayerProfile.Store; a receipt seen again grants
    ///    nothing, whatever delivers it (the store re-delivering at startup, a restore, a replay).
    ///  - EVERYTHING GRANTED IS SEALED, because the rules grant through PurchaseGrants.Apply and nowhere else.
    ///  - NOTHING HERE IS RANDOM. See PurchaseGrants and CheckStoreNoRandomPath.
    ///  - RESTORE re-delivers a restorable offer (a non-consumable) to a profile that has no receipt for it,
    ///    once. A pack that resets is not restorable.
    ///  - OFF MEANS INERT. With StoreSwitch off every call refuses at once and writes nothing.
    ///
    /// THE CHOICE IS MADE BEFORE THE PURCHASE (Begin takes it), so a purchase the store delivers again after
    /// the app was closed has no choice to apply. DEFAULT TAKEN: such a delivery for an offer that needs a
    /// hero is refused by the rules as BadChoice and is not confirmed, so the store keeps it open until the player is
    /// asked again; nothing is lost and nothing is guessed.
    /// </summary>
    public sealed class PurchaseLedger
    {
        private readonly IStoreRules _rules;
        private readonly Dictionary<string, string> _pending = new Dictionary<string, string>();

        public PurchaseLedger(IStoreRules rules)
        {
            _rules = rules;
        }

        public IStoreRules Rules { get { return _rules; } }

        /// <summary>Offers started and not finished. Memory only.</summary>
        public int PendingCount { get { return _pending.Count; } }

        public bool IsPending(string offerId)
        {
            return offerId != null && _pending.ContainsKey(offerId);
        }

        /// <summary>The hero the buyer picked for a pending offer, or "".</summary>
        public string PendingChoice(string offerId)
        {
            string c;
            return offerId != null && _pending.TryGetValue(offerId, out c) ? c : "";
        }

        /// <summary>Starts a purchase: checks the switch, the offer, its rules and the choice, remembers the
        /// choice in memory and writes NOTHING to the profile.</summary>
        public PurchaseResult Begin(GameSession session, string offerId, string choiceHeroId, long nowUnix)
        {
            PurchaseResult r = new PurchaseResult();
            r.OfferId = offerId ?? "";
            if (!StoreSwitch.Enabled || session == null || _rules == null) { r.Reason = PurchaseRefusal.Off; return r; }
            StoreOffer offer = _rules.Find(offerId);
            if (offer == null) { r.Reason = PurchaseRefusal.NoSuchOffer; return r; }
            string why;
            bool badChoice;
            if (!_rules.CanBuy(session, offerId, choiceHeroId, nowUnix, out why, out badChoice))
            {
                r.Reason = badChoice ? PurchaseRefusal.BadChoice : PurchaseRefusal.NotAvailable;
                r.Message = why ?? "";
                return r;
            }
            _pending[offerId] = choiceHeroId ?? "";
            r.Success = true;
            return r;
        }

        /// <summary>Ends a pending purchase that did not complete: cancelled, failed, timed out, or the store
        /// reported it still pending. Grants nothing and writes nothing.</summary>
        public void Abandon(string offerId)
        {
            if (offerId != null) _pending.Remove(offerId);
        }

        public void AbandonAll()
        {
            _pending.Clear();
        }

        /// <summary>The store reports a purchase complete. Grants once per receipt id. The choice is the one
        /// given to Begin (a purchase delivered with no pending entry has none).</summary>
        public PurchaseResult Complete(GameSession session, string offerId, string receiptId, long nowUnix)
        {
            string choice;
            if (offerId == null || !_pending.TryGetValue(offerId, out choice)) choice = "";
            PurchaseResult r = Deliver(session, _rules == null ? null : _rules.Find(offerId), receiptId, choice, nowUnix, false);
            if (r.CanConfirm && offerId != null) _pending.Remove(offerId);
            return r;
        }

        /// <summary>The store hands back a purchase of this product from an earlier install or profile.
        /// Delivers it once to a profile with no receipt for it. A product that is not restorable is refused.</summary>
        public PurchaseResult Restore(GameSession session, string productId, string receiptId, string choiceHeroId, long nowUnix)
        {
            PurchaseResult r = new PurchaseResult();
            if (!StoreSwitch.Enabled || session == null || _rules == null) { r.Reason = PurchaseRefusal.Off; return r; }
            StoreOffer offer = _rules.FindByProduct(productId);
            if (offer == null) { r.Reason = PurchaseRefusal.NoSuchOffer; return r; }
            r.OfferId = offer.Id;
            if (!offer.Restorable) { r.Reason = PurchaseRefusal.NotRestorable; return r; }
            return Deliver(session, offer, receiptId, choiceHeroId ?? "", nowUnix, true);
        }

        // ------------------------------------------------------------------ the one grant

        private PurchaseResult Deliver(GameSession session, StoreOffer offer, string receiptId, string choiceHeroId, long nowUnix, bool restored)
        {
            PurchaseResult r = new PurchaseResult();
            if (!StoreSwitch.Enabled || session == null || _rules == null) { r.Reason = PurchaseRefusal.Off; return r; }
            if (offer == null) { r.Reason = PurchaseRefusal.NoSuchOffer; return r; }
            r.OfferId = offer.Id;
            if (string.IsNullOrEmpty(receiptId)) { r.Reason = PurchaseRefusal.NoReceipt; return r; }

            StoreState store = session.Profile.Store;
            if (store.FindReceipt(receiptId) != null) { r.Reason = PurchaseRefusal.AlreadyGranted; return r; }

            // The rules grant and count in one all-or-nothing call. A refusal writes nothing and leaves no
            // receipt, so the store keeps the purchase open (a charged purchase the game could not grant is
            // never confirmed, which is what lets a store refund it).
            FulfilOutcome g = _rules.Fulfil(session, offer.Id, choiceHeroId, nowUnix);
            r.Fulfilled = g;
            if (g == null || !g.Success)
            {
                r.Reason = g != null && g.BadChoice ? PurchaseRefusal.BadChoice : PurchaseRefusal.BadGrant;
                r.Message = g == null ? "" : g.Reason ?? "";
                return r;
            }

            // ---- recorded after the grant, so a refused grant never leaves a receipt behind
            PurchaseReceipt receipt = new PurchaseReceipt();
            receipt.ReceiptId = receiptId;
            receipt.ProductId = offer.ProductId;
            receipt.OfferId = offer.Id;
            receipt.GrantedUnix = nowUnix;
            receipt.Restored = restored;
            if (store.Receipts.Count >= StoreState.ReceiptsMax) store.Receipts.RemoveAt(0);
            store.Receipts.Add(receipt);
            r.Success = true;
            return r;
        }
    }
}
