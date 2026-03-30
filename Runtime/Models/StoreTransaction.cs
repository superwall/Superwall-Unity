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
    }
}
