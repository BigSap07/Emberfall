#if EMBERFALL_UNITY_IAP
// Compile-time stand-in for the slice of com.unity.purchasing 5.x that UnityIapBackend uses, so
// Tools/build-check.ps1 can type-check that adapter without the package. Member names and shapes are copied
// from the package's own source (UnityEngine.Purchasing: StoreController, Order, PendingOrder,
// FailedOrder, ConfirmedOrder, Orders, ICart, CartItem, Product, ProductDefinition). The real Unity compile
// in the editor is the authority; this only catches typos in our own code early.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnityEngine.Purchasing
{
    public enum ProductType { Consumable, NonConsumable, Subscription, Unknown }

    public enum PurchaseFailureReason
    {
        PurchasingUnavailable = 0, ExistingPurchasePending = 1, ProductUnavailable = 2, SignatureInvalid = 3, UserCancelled = 4,
        PaymentDeclined = 5, DuplicateTransaction = 6, ValidationFailure = 7, StoreNotConnected = 8, PurchaseMissing = 9,
        Unknown = 10, UserNotAuthenticated = 11, NotSupported = 12, OrderCancelled = 13, OrderStateChanged = 14
    }

    public enum PurchasesFetchFailureReason { Unknown }

    public class ProductDefinition
    {
        public ProductDefinition(string id, ProductType type) { this.id = id; this.type = type; }
        public string id { get; private set; }
        public ProductType type { get; private set; }
    }

    public class Product
    {
        public ProductDefinition definition { get { return null; } }
    }

    public class CartItem
    {
        public Product Product { get { return null; } }
    }

    public interface ICart
    {
        IReadOnlyList<CartItem> Items();
    }

    public interface IOrderInfo
    {
        string TransactionID { get; }
        string Receipt { get; }
    }

    public abstract class Order
    {
        public ICart CartOrdered { get; internal set; }
        public IOrderInfo Info { get; private set; }
    }

    public class PendingOrder : Order { }
    public class ConfirmedOrder : Order { }

    public class FailedOrder : Order
    {
        public PurchaseFailureReason FailureReason { get { return PurchaseFailureReason.Unknown; } }
        public string Details { get { return ""; } }
    }

    public class Orders
    {
        public IReadOnlyList<ConfirmedOrder> ConfirmedOrders { get; private set; }
        public IReadOnlyList<PendingOrder> PendingOrders { get; private set; }
    }

    public class StoreConnectionFailureDescription { public string message; }
    public class ProductFetchFailed { public List<ProductDefinition> FailedFetchProducts { get; private set; } public string FailureReason { get; private set; } }
    public class PurchasesFetchFailureDescription { public string Message { get { return ""; } } }

    public class StoreController
    {
        public event Action<PendingOrder> OnPurchasePending;
        public event Action<Order> OnPurchaseConfirmed;
        public event Action<FailedOrder> OnPurchaseFailed;
        public event Action<Orders> OnPurchasesFetched;
        public event Action<PurchasesFetchFailureDescription> OnPurchasesFetchFailed;
        public event Action OnStoreConnected;
        public event Action<StoreConnectionFailureDescription> OnStoreDisconnected;
        public event Action<List<Product>> OnProductsFetched;
        public event Action<ProductFetchFailed> OnProductsFetchFailed;

        public Task Connect() { return null; }
        public void FetchProducts(List<ProductDefinition> productDefinitions) { }
        public void PurchaseProduct(string catalogListingId) { }
        public void ConfirmPurchase(PendingOrder order) { }
        public void FetchPurchases() { }
        public void RestoreTransactions(Action<bool, string> callback) { }
    }

    public static class UnityIAPServices
    {
        public static StoreController StoreController(string storeName = null) { return null; }
        public static string GetDefaultStore() { return "fake"; }
    }
}
#endif
