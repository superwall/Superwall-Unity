using System;

namespace Superwall
{
    public interface IPurchaseController
    {
        void PurchaseFromAppStore(string productId, Action<PurchaseResult> completion);
        void PurchaseFromGooglePlay(string productId, string basePlanId, string offerId, Action<PurchaseResult> completion);

        /// <summary>
        /// Called on Android for every Google Play purchase. <paramref name="replacement"/> is
        /// non-null when the purchase should replace one of the user's existing subscriptions
        /// (upgrade, downgrade or plan switch): pass its purchase token and mode to the billing
        /// flow's <c>SubscriptionUpdateParams</c> so the user isn't charged for a second subscription.
        /// The default implementation ignores <paramref name="replacement"/> and calls
        /// <see cref="PurchaseFromGooglePlay(string, string, string, Action{PurchaseResult})"/>,
        /// so existing controllers keep working without changes.
        /// </summary>
        void PurchaseFromGooglePlay(string productId, string basePlanId, string offerId,
            ProductReplacement replacement, Action<PurchaseResult> completion)
            => PurchaseFromGooglePlay(productId, basePlanId, offerId, completion);

        void RestorePurchases(Action<RestorationResult> completion);
    }
}
