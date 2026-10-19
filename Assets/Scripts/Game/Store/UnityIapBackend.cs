#if EMBERFALL_UNITY_IAP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using Emberfall.Core.Data;
using Emberfall.Core.Progression;

namespace Emberfall.Game
{
    /// <summary>
    /// UNITY IAP (com.unity.purchasing 5.x) behind IStoreBackend. A THIN adapter: it fetches the offers'
    /// products, starts a purchase by product id, and hands a pending order back to the flow as a product
    /// id and a transaction id. It decides nothing about what a purchase grants, and it confirms an order
    /// only when the flow says the purchase is finished (Confirm), so a purchase the game has not granted
    /// stays open in the store and is delivered again.
    ///
    /// In the editor the default store is Unity IAP's FAKE store (a dialog, no money). Nothing in this file
    /// holds a store key or a real product id: product ids come from the offers (placeholders in data).
    /// Reached only when PurchaseService is in Test mode in the editor, or in Live mode, which nothing in
    /// the R19 run enables.
    /// </summary>
    public sealed class UnityIapBackend : IStoreBackend
    {
        private StoreController _controller;
        private IStoreBackendListener _listener;
        private readonly List<StoreOffer> _offers = new List<StoreOffer>();
        private readonly Dictionary<string, PendingOrder> _open = new Dictionary<string, PendingOrder>();
        private bool _ready;

        public string Name { get { return "Unity IAP (" + UnityIAPServices.GetDefaultStore() + ")"; } }
        public bool IsReady { get { return _ready; } }

        public async void Initialize(IList<StoreOffer> offers, IStoreBackendListener listener)
        {
            _listener = listener;
            _offers.Clear();
            for (int i = 0; i < offers.Count; i++) _offers.Add(offers[i]);

            _controller = UnityIAPServices.StoreController();
            _controller.OnPurchasePending += OnPending;
            _controller.OnPurchaseConfirmed += OnConfirmed;
            _controller.OnPurchaseFailed += OnFailed;
            _controller.OnPurchasesFetched += OnFetched;
            _controller.OnPurchasesFetchFailed += OnFetchFailed;
            _controller.OnStoreConnected += OnConnected;
            _controller.OnStoreDisconnected += OnDisconnected;
            _controller.OnProductsFetched += OnProductsFetched;
            _controller.OnProductsFetchFailed += OnProductsFetchFailed;
            try
            {
                await _controller.Connect();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Emberfall store: Unity IAP did not connect: " + ex.Message);
            }
        }

        public void Purchase(string productId)
        {
            if (_controller == null || !_ready)
            {
                if (_listener != null) _listener.OnPurchaseFailed(productId, StoreFailure.Unavailable, "The store is not ready.");
                return;
            }
            _controller.PurchaseProduct(productId);
        }

        public void Confirm(string productId, string receiptId)
        {
            PendingOrder order;
            if (_controller == null || !_open.TryGetValue(receiptId, out order)) return;
            _open.Remove(receiptId);
            _controller.ConfirmPurchase(order);
        }

        public void Restore()
        {
            if (_controller == null || !_ready) { if (_listener != null) _listener.OnRestoreFinished(false, "The store is not ready."); return; }
            _controller.RestoreTransactions((ok, error) =>
            {
                if (!ok && _listener != null) _listener.OnRestoreFinished(false, error ?? "");
            });
            _controller.FetchPurchases();
        }

        public void Tick(double deltaSeconds)
        {
        }

        // ------------------------------------------------------------------ store events

        private void OnConnected()
        {
            List<ProductDefinition> defs = new List<ProductDefinition>();
            for (int i = 0; i < _offers.Count; i++)
                defs.Add(new ProductDefinition(_offers[i].ProductId, _offers[i].Restorable ? ProductType.NonConsumable : ProductType.Consumable));
            _controller.FetchProducts(defs);
        }

        private void OnDisconnected(StoreConnectionFailureDescription description)
        {
            _ready = false;
            Debug.LogWarning("Emberfall store: disconnected: " + (description == null ? "" : description.message));
        }

        private void OnProductsFetched(List<Product> products)
        {
            _ready = true;
            _controller.FetchPurchases();
        }

        private void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            Debug.LogWarning("Emberfall store: products did not load: " + (failure == null ? "" : failure.FailureReason));
        }

        private void OnPending(PendingOrder order)
        {
            string productId = ProductOf(order);
            if (productId == null || order.Info == null) return;
            string receiptId = order.Info.TransactionID;
            _open[receiptId] = order;
            if (_listener != null) _listener.OnPurchaseCompleted(productId, receiptId);
        }

        private void OnConfirmed(Order order)
        {
            FailedOrder failed = order as FailedOrder;
            if (failed != null) Debug.LogWarning("Emberfall store: confirming a purchase failed: " + failed.Details);
        }

        private void OnFailed(FailedOrder order)
        {
            string productId = ProductOf(order) ?? "";
            StoreFailure reason = order.FailureReason == PurchaseFailureReason.UserCancelled || order.FailureReason == PurchaseFailureReason.OrderCancelled
                ? StoreFailure.Cancelled : StoreFailure.Failed;
            if (_listener != null) _listener.OnPurchaseFailed(productId, reason, order.Details ?? "");
        }

        private void OnFetched(Orders orders)
        {
            if (_listener == null || orders == null) return;
            // Orders the store finished and the game has not confirmed arrive through OnPending; confirmed
            // orders of a non-consumable product are what a restore hands back.
            for (int i = 0; i < orders.ConfirmedOrders.Count; i++)
            {
                ConfirmedOrder c = orders.ConfirmedOrders[i];
                string productId = ProductOf(c);
                if (productId == null || c.Info == null) continue;
                if (IsRestorable(productId)) _listener.OnRestoredPurchase(productId, c.Info.TransactionID);
            }
            _listener.OnRestoreFinished(true, "");
        }

        private void OnFetchFailed(PurchasesFetchFailureDescription failure)
        {
            if (_listener != null) _listener.OnRestoreFinished(false, failure == null ? "" : failure.Message);
        }

        private bool IsRestorable(string productId)
        {
            for (int i = 0; i < _offers.Count; i++) if (_offers[i].ProductId == productId) return _offers[i].Restorable;
            return false;
        }

        private static string ProductOf(Order order)
        {
            if (order == null || order.CartOrdered == null) return null;
            IReadOnlyList<CartItem> items = order.CartOrdered.Items();
            if (items == null || items.Count == 0 || items[0] == null || items[0].Product == null || items[0].Product.definition == null) return null;
            return items[0].Product.definition.id;
        }
    }
}
#endif
