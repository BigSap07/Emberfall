using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Session;

namespace Emberfall.Core.Progression
{
    /// <summary>
    /// THE PURCHASE FLOW (R19): the wiring between a store backend and the ledger, in Core, so the harness
    /// proves it with the debug store and no Unity. The Game layer only builds it (PurchaseService), gives it
    /// the live session and the clock, and ticks it.
    ///
    ///  - Buy asks the ledger to Begin, then asks the backend to Purchase. COMING SOON refuses here, before
    ///    the backend is touched: a release build has no backend and no buy path.
    ///  - The backend's answer comes back through the listener. A completed purchase goes to the ledger
    ///    (Complete grants once per receipt); the backend is told to Confirm when the ledger says the
    ///    purchase is finished (CanConfirm), and the host is told to save when something was granted.
    ///  - A PENDING PURCHASE THAT NEVER COMPLETES GRANTS NOTHING: Tick abandons a purchase the store has not
    ///    answered within PendingTimeoutSeconds and reports it as TimedOut. If the store answers later, the
    ///    ledger still grants it once (the player was charged): a late answer is delivered like any other.
    ///  - Restore asks the backend for earlier purchases. Each goes to the ledger's Restore with the pick the
    ///    host's delegate supplies; an offer that needs a pick and got none waits in RestoreWaiting until
    ///    the screen asks the player and calls FinishRestore.
    /// </summary>
    public sealed class PurchaseFlow : IStoreBackendListener
    {
        public const double PendingTimeoutSeconds = 120.0;

        private readonly PurchaseLedger _ledger;
        private readonly IStoreBackend _backend;
        private readonly StoreMode _mode;
        private readonly Func<GameSession> _session;
        private readonly Func<long> _now;
        private readonly Action _save;
        private readonly Dictionary<string, double> _age = new Dictionary<string, double>();
        private readonly List<string[]> _restoreWaiting = new List<string[]>();
        private Func<string, string> _restoreChoice;

        /// <summary>Every finished purchase, restore or refusal of a delivered purchase, for the screen.</summary>
        public event Action<PurchaseResult> Finished;

        /// <summary>A purchase that did not happen: offer id and a sentence for the player.</summary>
        public event Action<string, string> Failed;

        public PurchaseFlow(IStoreRules rules, IStoreBackend backend, StoreMode mode, Func<GameSession> session, Func<long> now, Action save)
        {
            _ledger = new PurchaseLedger(rules);
            _backend = backend;
            _mode = mode;
            _session = session;
            _now = now;
            _save = save;
        }

        public StoreMode Mode { get { return _mode; } }
        public PurchaseLedger Ledger { get { return _ledger; } }
        public IStoreBackend Backend { get { return _backend; } }

        /// <summary>True when the screen may draw a Buy button.</summary>
        public bool CanBuy { get { return StoreModes.CanBuy(_mode) && _backend != null; } }

        public bool IsPending(string offerId) { return _ledger.IsPending(offerId); }

        /// <summary>Offers a restore found that still need the player's pick: { productId, receiptId }.</summary>
        public IList<string[]> RestoreWaiting { get { return _restoreWaiting; } }

        /// <summary>Starts the store and delivers any purchase it finished and nobody confirmed.</summary>
        public void Start(IList<StoreOffer> offers)
        {
            if (_backend != null && StoreSwitch.Enabled && CanBuy) _backend.Initialize(offers, this);
        }

        public PurchaseResult Buy(string offerId, string choiceHeroId)
        {
            if (!StoreSwitch.Enabled || _mode == StoreMode.Off || _backend == null && _mode != StoreMode.ComingSoon)
            {
                PurchaseResult off = new PurchaseResult();
                off.OfferId = offerId ?? "";
                off.Reason = PurchaseRefusal.Off;
                return off;
            }
            if (!CanBuy)
            {
                PurchaseResult soon = new PurchaseResult();
                soon.OfferId = offerId ?? "";
                soon.Reason = PurchaseRefusal.ComingSoon;
                soon.Message = "Coming soon.";
                return soon;
            }
            StoreOffer offer = _ledger.Rules.Find(offerId);
            if (offer == null)
            {
                PurchaseResult none = new PurchaseResult();
                none.OfferId = offerId ?? "";
                none.Reason = PurchaseRefusal.NoSuchOffer;
                return none;
            }
            if (_ledger.IsPending(offerId))
            {
                PurchaseResult busy = new PurchaseResult();
                busy.OfferId = offerId;
                busy.Reason = PurchaseRefusal.NotAvailable;
                busy.Message = "A purchase of this is already waiting for the store.";
                return busy;
            }
            PurchaseResult begun = _ledger.Begin(_session(), offerId, choiceHeroId, _now());
            if (!begun.Success) return begun;
            _age[offerId] = 0.0;
            _backend.Purchase(offer.ProductId);
            return begun;
        }

        /// <summary>Asks the store for earlier purchases. choiceFor(offerId) gives the pick for an offer that
        /// needs one, or "" when the player has not chosen yet.</summary>
        public void Restore(Func<string, string> choiceFor)
        {
            if (!StoreSwitch.Enabled || !CanBuy) return;
            _restoreChoice = choiceFor;
            _backend.Restore();
        }

        /// <summary>Finishes a restore that was waiting for a pick.</summary>
        public PurchaseResult FinishRestore(string productId, string choiceHeroId)
        {
            for (int i = 0; i < _restoreWaiting.Count; i++)
            {
                if (_restoreWaiting[i][0] != productId) continue;
                string receipt = _restoreWaiting[i][1];
                PurchaseResult r = _ledger.Restore(_session(), productId, receipt, choiceHeroId, _now());
                if (r.CanConfirm) _restoreWaiting.RemoveAt(i);
                Report(r);
                return r;
            }
            PurchaseResult none = new PurchaseResult();
            none.Reason = PurchaseRefusal.NoSuchOffer;
            return none;
        }

        public void Tick(double deltaSeconds)
        {
            if (_backend != null) _backend.Tick(deltaSeconds);
            if (_age.Count == 0) return;
            List<string> expired = null;
            List<string> keys = new List<string>(_age.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                if (!_ledger.IsPending(keys[i])) { _age.Remove(keys[i]); continue; }
                double age = _age[keys[i]] + deltaSeconds;
                _age[keys[i]] = age;
                if (age >= PendingTimeoutSeconds)
                {
                    if (expired == null) expired = new List<string>();
                    expired.Add(keys[i]);
                }
            }
            if (expired == null) return;
            for (int i = 0; i < expired.Count; i++)
            {
                _ledger.Abandon(expired[i]);
                _age.Remove(expired[i]);
                if (Failed != null) Failed(expired[i], "The store did not answer. Nothing was bought.");
            }
        }

        // ------------------------------------------------------------------ the store's answers

        public void OnPurchaseCompleted(string productId, string receiptId)
        {
            StoreOffer offer = _ledger.Rules.FindByProduct(productId);
            if (offer == null)
            {
                // A product this build does not know: keep it open, grant nothing.
                if (Failed != null) Failed("", "The store delivered a purchase this game does not sell.");
                return;
            }
            PurchaseResult r = _ledger.Complete(_session(), offer.Id, receiptId, _now());
            _age.Remove(offer.Id);
            if (r.CanConfirm && _backend != null) _backend.Confirm(productId, receiptId);
            if (r.Success && _save != null) _save();
            Report(r);
        }

        public void OnPurchaseFailed(string productId, StoreFailure reason, string detail)
        {
            StoreOffer offer = _ledger.Rules.FindByProduct(productId);
            string id = offer == null ? "" : offer.Id;
            if (offer != null) { _ledger.Abandon(offer.Id); _age.Remove(offer.Id); }
            if (Failed != null)
                Failed(id, reason == StoreFailure.Cancelled ? "Purchase cancelled. Nothing was bought." : "The purchase did not go through. Nothing was bought.");
        }

        public void OnRestoredPurchase(string productId, string receiptId)
        {
            StoreOffer offer = _ledger.Rules.FindByProduct(productId);
            if (offer == null) return;
            string choice = _restoreChoice == null ? "" : (_restoreChoice(offer.Id) ?? "");
            PurchaseResult r = _ledger.Restore(_session(), productId, receiptId, choice, _now());
            if (r.Reason == PurchaseRefusal.BadChoice)
            {
                bool queued = false;
                for (int i = 0; i < _restoreWaiting.Count; i++) if (_restoreWaiting[i][1] == receiptId) queued = true;
                if (!queued) _restoreWaiting.Add(new[] { productId, receiptId });
            }
            if (r.Success && _save != null) _save();
            Report(r);
        }

        public void OnRestoreFinished(bool success, string detail)
        {
            if (!success && Failed != null) Failed("", "Restore did not finish. " + detail);
        }

        private void Report(PurchaseResult r)
        {
            if (Finished != null) Finished(r);
        }
    }
}
