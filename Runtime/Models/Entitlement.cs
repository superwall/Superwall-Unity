using System;
using System.Collections.Generic;

namespace Superwall
{
    [Serializable]
    public class Entitlement
    {
        public string Id;
        public EntitlementType Type;
        /// <summary>
        /// Whether the entitlement is active. A new <c>Entitlement</c> is active, as in the native SDKs, so one built
        /// for <c>SubscriptionStatus.CreateActive</c> is sent without setting this.
        /// </summary>
        public bool IsActive = true;
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
