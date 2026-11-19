using System;

namespace Superwall
{
    /// <summary>
    /// Android only. A Google Play purchase made outside Superwall, as Google Play Billing returned it, for
    /// <see cref="Superwall.ObservePurchaseResult"/>.
    /// </summary>
    [Serializable]
    public class GooglePlayPurchase
    {
        /// <summary>
        /// The purchase's original JSON (<c>Purchase.getOriginalJson()</c>). Unity IAP has it as the <c>json</c> value
        /// of a Google Play receipt's payload.
        /// </summary>
        public string OriginalJson;
        /// <summary>The purchase's signature (<c>Purchase.getSignature()</c>): the <c>signature</c> value of a Unity IAP receipt's payload.</summary>
        public string Signature;

        public GooglePlayPurchase() { }

        public GooglePlayPurchase(string originalJson, string signature)
        {
            OriginalJson = originalJson;
            Signature = signature;
        }
    }
}
