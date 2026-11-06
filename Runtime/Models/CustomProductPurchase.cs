using System;

namespace Superwall
{
    /// <summary>
    /// A purchase of a custom product: a product whose store is set to Custom on the Superwall
    /// dashboard, sold through your own payment system (Stripe, Paddle, a web checkout or another
    /// store) instead of the App Store or Google Play.
    /// Passed to <see cref="IPurchaseController.PurchaseCustomProduct"/>.
    /// </summary>
    [Serializable]
    public class CustomProductPurchase
    {
        /// <summary>The product's identifier on the Superwall dashboard.</summary>
        public string ProductId;

        /// <summary>
        /// The transaction ID Superwall generated for this purchase. Pass it to your checkout, for
        /// example as metadata, and have your server send it back as <c>custom_transaction_id</c>
        /// when it reports the purchase to Superwall, so the revenue is attributed to the paywall
        /// that sold it. It is also the purchase's original transaction ID in Superwall's events.
        /// </summary>
        public string CustomTransactionId;

        /// <summary>
        /// Android only: set when the paywall's Purchase action replaces an existing subscription
        /// that shares an entitlement with this product. For custom products,
        /// <see cref="ProductReplacement.OldProductId"/> comes from the active entitlements you
        /// reported and <see cref="ProductReplacement.OldPurchaseToken"/> is null. Always null on iOS.
        /// </summary>
        public ProductReplacement Replacement;

        public CustomProductPurchase(string productId, string customTransactionId, ProductReplacement replacement = null)
        {
            ProductId = productId;
            CustomTransactionId = customTransactionId;
            Replacement = replacement;
        }
    }
}
