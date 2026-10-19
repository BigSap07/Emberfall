using System.Collections.Generic;
using Emberfall.Core.Data;

namespace Emberfall.Core.Progression
{
    /// <summary>Why a store reported a purchase as not done.</summary>
    public enum StoreFailure
    {
        Cancelled,
        Failed,
        Unavailable,
        /// <summary>Raised by PurchaseFlow itself when the store never answered.</summary>
        TimedOut
    }

    /// <summary>What a store backend tells the flow. Always called on the main thread.</summary>
    public interface IStoreBackendListener
    {
        /// <summary>The store finished a purchase and holds a receipt for it. The flow grants once per
        /// receipt id and then calls IStoreBackend.Confirm.</summary>
        void OnPurchaseCompleted(string productId, string receiptId);

        /// <summary>The purchase did not happen. Nothing was charged and nothing is granted.</summary>
        void OnPurchaseFailed(string productId, StoreFailure reason, string detail);

        /// <summary>The store hands back an earlier purchase of a non-consumable product.</summary>
        void OnRestoredPurchase(string productId, string receiptId);

        void OnRestoreFinished(bool success, string detail);
    }

    /// <summary>
    /// A store, as the purchase flow sees it: Unity IAP's fake store in the editor, the debug store in a
    /// development build, nothing in a release build. A THIN adapter: it moves a product id out and a
    /// receipt id back and decides nothing about what a purchase grants.
    /// </summary>
    public interface IStoreBackend
    {
        string Name { get; }
        bool IsReady { get; }

        void Initialize(IList<StoreOffer> offers, IStoreBackendListener listener);

        /// <summary>Starts a purchase of a product. The answer comes later through the listener.</summary>
        void Purchase(string productId);

        /// <summary>The purchase was granted (or was already granted): the store may close it. Until this
        /// is called a real store keeps the purchase open and delivers it again at the next start.</summary>
        void Confirm(string productId, string receiptId);

        /// <summary>Asks the store for the player's earlier purchases of non-consumable products.</summary>
        void Restore();

        /// <summary>Called every frame with the seconds since the last call.</summary>
        void Tick(double deltaSeconds);
    }
}
