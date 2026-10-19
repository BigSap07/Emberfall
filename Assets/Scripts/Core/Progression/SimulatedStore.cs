using System;
using System.Collections.Generic;
using Emberfall.Core.Data;
using Emberfall.Core.Json;

namespace Emberfall.Core.Progression
{
    /// <summary>
    /// THE DEBUG STORE (R19): a stand-in for a real store that runs without Unity and without money. A
    /// development build uses it, and the harness uses it to prove the purchase flow. It behaves like a store
    /// that matters here:
    ///  - a purchase takes DelaySeconds and then completes with a receipt id, or waits for Approve or Decline
    ///    when AutoApprove is off (a purchase that stays pending);
    ///  - a completed purchase stays OPEN until Confirm. An open purchase is delivered again at the next
    ///    Initialize, as a real store does, and a confirmed one is not;
    ///  - the ACCOUNT remembers confirmed purchases of restorable products, so a new profile on the same
    ///    account gets them back through Restore. The account is the store's memory, not the save's: it is
    ///    kept through the Load and Save delegates the host gives (a file in the debug build, a string in the harness).
    ///
    /// Receipt ids are DEBUG- plus a serial, deterministic, and say plainly that no money moved. A release
    /// build never creates one (PurchaseService), so the class holds no key and no real product id.
    /// </summary>
    public sealed class SimulatedStore : IStoreBackend
    {
        private sealed class Held
        {
            public string ProductId = "";
            public string ReceiptId = "";
            public bool Restorable;
            public bool Confirmed;
        }

        private sealed class Queued
        {
            public string ProductId = "";
            public double Remaining;
        }

        public Func<string> LoadAccount;
        public Action<string> SaveAccount;

        public double DelaySeconds = 0.4;

        /// <summary>False holds every purchase pending until Approve or Decline. A held purchase that is
        /// never approved is a pending purchase that never completes.</summary>
        public bool AutoApprove = true;

        private readonly List<Held> _account = new List<Held>();
        private readonly List<Queued> _queue = new List<Queued>();
        private readonly Dictionary<string, bool> _restorable = new Dictionary<string, bool>();
        private IStoreBackendListener _listener;
        private int _serial;
        private bool _ready;

        public string Name { get { return "debug store"; } }
        public bool IsReady { get { return _ready; } }
        public int PendingCount { get { return _queue.Count; } }

        /// <summary>Purchases the store holds open (completed, not confirmed).</summary>
        public int OpenCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _account.Count; i++) if (!_account[i].Confirmed) n++;
                return n;
            }
        }

        /// <summary>Confirmed purchases of restorable products on this account.</summary>
        public int RestorableCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _account.Count; i++) if (_account[i].Confirmed && _account[i].Restorable) n++;
                return n;
            }
        }

        public void Initialize(IList<StoreOffer> offers, IStoreBackendListener listener)
        {
            _listener = listener;
            _restorable.Clear();
            for (int i = 0; i < offers.Count; i++) _restorable[offers[i].ProductId] = offers[i].Restorable;
            ReadAccount();
            _ready = true;
            // A purchase the store finished and nobody confirmed is delivered again.
            for (int i = 0; i < _account.Count; i++)
                if (!_account[i].Confirmed) listener.OnPurchaseCompleted(_account[i].ProductId, _account[i].ReceiptId);
        }

        public void Purchase(string productId)
        {
            if (!_ready || _listener == null) return;
            if (!_restorable.ContainsKey(productId))
            {
                _listener.OnPurchaseFailed(productId, StoreFailure.Unavailable, "The debug store does not sell " + productId + ".");
                return;
            }
            Queued q = new Queued();
            q.ProductId = productId;
            q.Remaining = DelaySeconds;
            _queue.Add(q);
        }

        public void Tick(double deltaSeconds)
        {
            if (!AutoApprove || _queue.Count == 0) return;
            for (int i = 0; i < _queue.Count; i++) _queue[i].Remaining -= deltaSeconds;
            while (_queue.Count > 0 && _queue[0].Remaining <= 0.0)
            {
                string product = _queue[0].ProductId;
                _queue.RemoveAt(0);
                Complete(product);
            }
        }

        /// <summary>Completes the oldest held purchase now (the player pressed Buy in the store's window).</summary>
        public bool Approve()
        {
            if (_queue.Count == 0) return false;
            string product = _queue[0].ProductId;
            _queue.RemoveAt(0);
            Complete(product);
            return true;
        }

        /// <summary>Cancels the oldest held purchase (the player closed the store's window).</summary>
        public bool Decline()
        {
            if (_queue.Count == 0) return false;
            string product = _queue[0].ProductId;
            _queue.RemoveAt(0);
            if (_listener != null) _listener.OnPurchaseFailed(product, StoreFailure.Cancelled, "Cancelled in the debug store.");
            return true;
        }

        private void Complete(string productId)
        {
            Held h = new Held();
            h.ProductId = productId;
            h.ReceiptId = "DEBUG-" + (++_serial).ToString("D6");
            bool r;
            h.Restorable = _restorable.TryGetValue(productId, out r) && r;
            _account.Add(h);
            WriteAccount();
            if (_listener != null) _listener.OnPurchaseCompleted(productId, h.ReceiptId);
        }

        public void Confirm(string productId, string receiptId)
        {
            for (int i = 0; i < _account.Count; i++)
            {
                if (_account[i].ReceiptId != receiptId) continue;
                _account[i].Confirmed = true;
                // A consumable has nothing to restore: it is closed and forgotten.
                if (!_account[i].Restorable) _account.RemoveAt(i);
                WriteAccount();
                return;
            }
        }

        public void Restore()
        {
            if (_listener == null) return;
            for (int i = 0; i < _account.Count; i++)
                if (_account[i].Confirmed && _account[i].Restorable)
                    _listener.OnRestoredPurchase(_account[i].ProductId, _account[i].ReceiptId);
            _listener.OnRestoreFinished(true, "");
        }

        // ------------------------------------------------------------------ the account's memory

        private void ReadAccount()
        {
            _account.Clear();
            _serial = 0;
            string text = LoadAccount == null ? null : LoadAccount();
            if (string.IsNullOrEmpty(text)) return;
            JsonValue root;
            string error;
            if (!JsonValue.TryParse(text, out root, out error) || !root.IsObject) return;
            _serial = Math.Max(0, root.GetInt("serial", 0));
            List<JsonValue> list = root.GetList("purchases");
            for (int i = 0; i < list.Count && i < 500; i++)
            {
                JsonValue j = list[i];
                if (j == null || !j.IsObject) continue;
                Held h = new Held();
                h.ProductId = j.GetString("product", "");
                h.ReceiptId = j.GetString("receipt", "");
                h.Restorable = j.GetBool("restorable", false);
                h.Confirmed = j.GetBool("confirmed", false);
                if (h.ProductId.Length > 0 && h.ReceiptId.Length > 0) _account.Add(h);
            }
        }

        private void WriteAccount()
        {
            if (SaveAccount == null) return;
            JsonValue root = JsonValue.NewObject();
            root.Set("serial", _serial);
            JsonValue list = JsonValue.NewArray();
            for (int i = 0; i < _account.Count; i++)
            {
                JsonValue j = JsonValue.NewObject();
                j.Set("product", _account[i].ProductId);
                j.Set("receipt", _account[i].ReceiptId);
                j.Set("restorable", _account[i].Restorable);
                j.Set("confirmed", _account[i].Confirmed);
                list.Add(j);
            }
            root.Set("purchases", list);
            SaveAccount(root.ToJson(true));
        }
    }
}
