using System;
using System.Collections.Generic;

namespace Superwall
{
    [Serializable]
    public class StoreTransaction
    {
        public string ConfigRequestId;
        public string AppSessionId;
        public string TransactionDate;
        public string OriginalTransactionIdentifier;
        public string StoreTransactionId;
        public string OriginalTransactionDate;
        public string WebOrderLineItemID;
        public string AppBundleId;
        public string SubscriptionGroupId;
        public bool? IsUpgraded;
        public string ExpirationDate;
        public string OfferId;
        public string RevocationDate;
        public StoreTransactionState? State;
        /// <summary>The payment the transaction is for.</summary>
        public StorePayment Payment;
        /// <summary>The UUID the app associated with the purchase, if any.</summary>
        public string AppAccountToken;
        /// <summary>Android only. The Google Play purchase token, which <see cref="Superwall.Consume"/> takes.</summary>
        public string PurchaseToken;
        /// <summary>Android only. The purchase's signature from Google Play.</summary>
        public string Signature;
    }

    [Serializable]
    public enum StoreTransactionState
    {
        Purchasing,
        Purchased,
        Failed,
        Restored,
        Deferred
    }

    [Serializable]
    public class StorePayment
    {
        public string ProductIdentifier;
        public int Quantity;
        /// <summary>The ID of the discount offer applied, if any.</summary>
        public string DiscountIdentifier;
    }
}
