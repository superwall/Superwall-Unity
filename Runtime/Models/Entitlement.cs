using System;
using System.Collections.Generic;

namespace Superwall
{
    [Serializable]
    public class Entitlement
    {
        public string Id;
        public EntitlementType Type;
        public bool IsActive;
        public List<string> ProductIds;
        public string LatestProductId;
        public ProductStore? Store;
        public long? StartsAt;
        public long? RenewedAt;
        public long? ExpiresAt;
        public bool? IsLifetime;
        public bool? WillRenew;
        public LatestSubscriptionState? State;
        public LatestSubscriptionOfferType? OfferType;
    }

    [Serializable]
    public class Entitlements
    {
        public List<Entitlement> Active;
        public List<Entitlement> Inactive;
        public List<Entitlement> All;
        public List<Entitlement> Web;
    }
}
