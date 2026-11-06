using System;

namespace Superwall
{
    /// <summary>
    /// An existing Google Play subscription that a purchase replaces (an upgrade, downgrade or
    /// plan switch), instead of the purchase being bought alongside it.
    /// Passed to <see cref="IPurchaseController.PurchaseFromGooglePlay(string, string, string, ProductReplacement, Action{PurchaseResult})"/>
    /// only when the paywall's Purchase action is set to replace a subscription and the user has an
    /// active one that shares an entitlement with the product being purchased.
    /// </summary>
    [Serializable]
    public class ProductReplacement
    {
        public enum ReplacementMode
        {
            /// <summary>Let Google Play decide, using the replacement mode configured in Play Console.</summary>
            Default,
            /// <summary>Replace now and charge the prorated price difference immediately (upgrades only).
            /// Maps to <c>ReplacementMode.CHARGE_PRORATED_PRICE</c>.</summary>
            ChargeProrated,
            /// <summary>Replace now and charge the full price of the new product immediately.
            /// Maps to <c>ReplacementMode.CHARGE_FULL_PRICE</c>.</summary>
            ChargeFullPrice
        }

        /// <summary>How the store should charge for the switch.</summary>
        public ReplacementMode Mode;

        /// <summary>The subscription ID of the product being replaced, if known.</summary>
        public string OldProductId;

        /// <summary>The Google Play purchase token of the subscription being replaced. Pass it as
        /// <c>SubscriptionUpdateParams.oldPurchaseToken</c> (RevenueCat: <c>oldProductId</c> +
        /// <c>googleReplacementMode</c>).</summary>
        public string OldPurchaseToken;

        public ProductReplacement(ReplacementMode mode, string oldProductId, string oldPurchaseToken)
        {
            Mode = mode;
            OldProductId = oldProductId;
            OldPurchaseToken = oldPurchaseToken;
        }

        internal static ReplacementMode ParseMode(string rawName)
        {
            switch (rawName)
            {
                case "charge_prorated": return ReplacementMode.ChargeProrated;
                case "charge_full_price": return ReplacementMode.ChargeFullPrice;
                default: return ReplacementMode.Default;
            }
        }
    }
}
