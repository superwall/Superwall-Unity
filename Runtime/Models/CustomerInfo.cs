using System;
using System.Collections.Generic;

namespace Superwall
{
    [Serializable]
    public class CustomerInfo
    {
        public List<SubscriptionTransaction> Subscriptions;
        public List<NonSubscriptionTransaction> NonSubscriptions;
        public List<Entitlement> Entitlements;
        public string UserId;
    }

    [Serializable]
    public class SubscriptionTransaction
    {
        public string TransactionId;
        public string ProductId;
        public long PurchaseDate;
        public bool WillRenew;
        public bool IsRevoked;
        public bool IsInGracePeriod;
        public bool IsInBillingRetryPeriod;
        public bool IsActive;
        public long? ExpirationDate;
        public LatestSubscriptionOfferType? OfferType;
        public string SubscriptionGroupId;
        public ProductStore? Store;
    }

    [Serializable]
    public class NonSubscriptionTransaction
    {
        public string TransactionId;
        public string ProductId;
        public long PurchaseDate;
        public bool IsConsumable;
        public bool IsRevoked;
        public ProductStore? Store;
    }
}
